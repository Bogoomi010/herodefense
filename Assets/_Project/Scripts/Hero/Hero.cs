using TowerDefense.Core;
using TowerDefense.Game;
using TowerDefense.Map;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense.Hero
{
    public enum HeroState { Perched, Descending, Active }

    /// <summary>
    /// 절벽 위(perch)에 서서 전장을 내려다보는 영웅.
    /// 조작: Ctrl+호버(Perched) = 강림 지점 표시, Ctrl+좌클릭(Perched) = 강림, WASD(Active) = 이동, 좌클릭(Active) = 가까운 적 공격, T = 절벽 귀환 (강림 후 returnCooldownSec가 지나야 가능).
    /// 강림은 귀환 후 descendCooldownSec(플레이어 스킬로 단축)가 지나야 다시 할 수 있다 (docs/HERO_DESCENT.md).
    /// </summary>
    public sealed class Hero : MonoBehaviour
    {
        public HeroState State { get; private set; } = HeroState.Perched;
        public event System.Action<HeroState> StateChanged;

        [Header("참조")]
        public Transform perch;
        public Camera cam;
        public TowerDefense.Map.TileMap map;
        public TowerDefense.Game.EnemySpawner session;

        [Header("이동")]
        public float moveSpeed = 6f;
        public float turnSpeed = 720f;

        [Header("전투")]
        public float atk = 25f;
        public float rangePx = 220f;
        public float cooldownSec = 0.4f;
        public TowerDefense.Core.DmgType dmgType = TowerDefense.Core.DmgType.Phys;
        [Tooltip("공격 지점 주변 크립을 날려 보내는 폭발 반경 (m) — docs/CREEP_MOVEMENT.md")]
        public float knockRadius = 3f;
        [Tooltip("넉백 세기 (날아가는 수평 초기 속도 m/s)")]
        public float knockForce = 9f;

        [Header("강림 효과 (docs/HERO.md)")]
        [Tooltip("착지 지점 중심, 이 반경(m) 안 크립에게 스턴 + 에어본")]
        public float impactRadius = 5f;
        [Tooltip("에어본 세기 (수평 초기 속도 m/s, 가장자리는 절반). 날아가는 거리는 세기²에 비례 — 11 ≈ 투석기(9)의 1.5배 거리")]
        public float impactForce = 11f;
        [Tooltip("스턴 시간(초). 날아간 크립은 뒤집혀 떨어진 순간부터, 날아가지 않는 보스는 강림 순간부터 절반")]
        public float impactStunSec = 1f;

        [Header("강림 궤적 (운석: 솟구침 → 정점 → 내리꽂힘)")]
        [Tooltip("절벽에서 정점까지 솟구치는 시간(초). 처음엔 빠르고 정점에서 느려진다")]
        public float riseSec = 0.45f;
        [Tooltip("정점에서 잠깐 멈추는 시간(초)")]
        public float hangSec = 0.1f;
        [Tooltip("정점에서 착지까지 내리꽂는 시간(초). 갈수록 빨라진다")]
        public float slamSec = 0.22f;
        [Tooltip("정점 높이: 절벽·착지점 중 높은 쪽보다 이만큼(m) 위")]
        public float apexHeight = 18f;
        [Tooltip("정점을 착지점에서 절벽 쪽으로 이만큼(m) 물린다 — 수직 낙하 대신 비스듬히 꽂히게")]
        public float slamBack = 6f;
        [Tooltip("귀환한 뒤 이 시간이 지나야 다시 강림할 수 있다 (플레이어 스킬로 단축)")]
        public float descendCooldownSec = 20f;
        /// <summary>다시 강림할 수 있을 때까지 남은 시간(초). Perched가 아니면 0.</summary>
        public float DescendCooldownLeft => State == HeroState.Perched ? Mathf.Max(0f, _returnedAt + DescendCooldownTotal - Time.time) : 0f;
        /// <summary>스킬 배율을 적용한 강림 쿨타임(초)</summary>
        public float DescendCooldownTotal => descendCooldownSec * (session != null ? session.Bonuses.DescentCdMul : 1f);
        private float _returnedAt = float.NegativeInfinity;
        [Header("귀환")]
        [Tooltip("강림 완료 후 이 시간이 지나야 T로 귀환할 수 있다")]
        public float returnCooldownSec = 10f;
        /// <summary>T 귀환까지 남은 시간(초). Active가 아니면 0.</summary>
        public float ReturnCooldownLeft => State == HeroState.Active ? Mathf.Max(0f, _activeSince + returnCooldownSec - Time.time) : 0f;
        private float _activeSince;

        [Header("강림 마커")]
        public float markerRadius = 0.9f;
        public Color markerColor = new Color(1f, 0.85f, 0.3f);

        public Vector3 Facing
        {
            get
            {
                var f = transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            }
        }

        private Vector3 _perchPosCache;
        private Quaternion _perchRotCache;
        private Vector3 _descendFrom;
        private Vector3 _descendTo;
        private float _descendT;
        private float _halfHeight = 1f;
        private float _atkCd;
        private LineRenderer _beam;
        private float _beamUntil;
        private GameObject _marker;
        private LineRenderer _markerRing;

        public bool MarkerVisible => _marker != null && _marker.activeSelf;

        private Vector3 PerchPos => (perch != null ? perch.position : _perchPosCache) + Vector3.up * _halfHeight; // perch는 발밑 지점
        private Quaternion PerchRot => perch != null ? perch.rotation : _perchRotCache;

        private void Start()
        {
            if (cam == null) cam = Camera.main;
            if (map == null) map = FindFirstObjectByType<TowerDefense.Map.TileMap>();
            if (session == null) session = FindFirstObjectByType<TowerDefense.Game.EnemySpawner>();

            var capsule = GetComponent<CapsuleCollider>();
            _halfHeight = capsule != null ? capsule.height * 0.5f : 1f;
            _perchPosCache = transform.position - Vector3.up * _halfHeight;
            _perchRotCache = transform.rotation;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            transform.SetPositionAndRotation(PerchPos, PerchRot);
            if (GetComponent<HeroInteractor>() == null) gameObject.AddComponent<HeroInteractor>();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame && State == HeroState.Active && ReturnCooldownLeft <= 0f) Return();

            switch (State)
            {
                case HeroState.Perched: TickPerched(); break;
                case HeroState.Descending: TickDescending(); break;
                case HeroState.Active: TickActive(); break;
            }
            TickShock();
        }

        private void TickPerched()
        {
            if (cam == null || Mouse.current == null || Keyboard.current == null) { HideMarker(); return; }

            bool ctrl = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            if (!ctrl || DescendCooldownLeft > 0f) { HideMarker(); return; }

            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit) || map == null || map.WorldToTile(hit.point) == null)
            {
                HideMarker();
                return;
            }

            ShowMarker(hit.point);

            if (Mouse.current.leftButton.wasPressedThisFrame) Descend(hit.point);
        }

        /// <summary>강림 정점: 착지점 위 높은 곳, 절벽 쪽으로 조금 물러난 자리.</summary>
        private Vector3 Apex
        {
            get
            {
                var back = _descendFrom - _descendTo;
                back.y = 0f;
                back = back.sqrMagnitude > 0.0001f ? back.normalized * slamBack : Vector3.zero;
                return new Vector3(_descendTo.x + back.x, Mathf.Max(_descendFrom.y, _descendTo.y) + apexHeight, _descendTo.z + back.z);
            }
        }

        /// <summary>
        /// 운석 강림 (docs/HERO_DESCENT.md): ① 솟구침 — 수평은 부드럽게, 높이는 빠르게 올라 정점에서 느려짐
        /// ② 정점에서 잠깐 멈춤 ③ 내리꽂힘 — 가속(세제곱)하며 비스듬히 착지. 착지 순간 강림 효과·충격파.
        /// </summary>
        private void TickDescending()
        {
            _descendT += Time.deltaTime;
            var apex = Apex;
            Vector3 pos;
            if (_descendT < riseSec)
            {
                float u = _descendT / riseSec;
                pos = Vector3.Lerp(_descendFrom, apex, Mathf.SmoothStep(0f, 1f, u));
                pos.y = Mathf.Lerp(_descendFrom.y, apex.y, 1f - (1f - u) * (1f - u));
            }
            else if (_descendT < riseSec + hangSec) pos = apex;
            else
            {
                float u = Mathf.Clamp01((_descendT - riseSec - hangSec) / Mathf.Max(0.0001f, slamSec));
                pos = Vector3.Lerp(apex, _descendTo, u * u * u);
            }
            transform.position = pos;

            var dir = _descendTo - _descendFrom;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized);

            if (_descendT >= riseSec + hangSec + slamSec)
            {
                SnapToGround(_descendTo);
                _activeSince = Time.time;
                SetState(HeroState.Active); // HeroCamera가 이 순간 흔들린다
                Impact(transform.position);
                Shockwave(transform.position);
            }
        }

        // ---------- 착지 충격파: 바닥 원이 강림 지역 끝까지 퍼지며 사라진다 ----------

        private const float ShockSec = 0.3f;
        private LineRenderer _shock;
        private float _shockT = -1f;

        private void Shockwave(Vector3 at)
        {
            if (_shock == null) _shock = TowerDefense.Game.Rings.Circle(null, markerColor, 0.35f);
            _shock.transform.position = at + Vector3.up * 0.1f;
            _shock.gameObject.SetActive(true);
            _shockT = 0f;
        }

        private void TickShock()
        {
            if (_shockT < 0f || _shock == null) return;
            _shockT += Time.deltaTime;
            float u = _shockT / ShockSec;
            if (u >= 1f) { _shock.gameObject.SetActive(false); _shockT = -1f; return; }
            TowerDefense.Game.Rings.SetRadius(_shock, Mathf.Lerp(0.5f, impactRadius, 1f - (1f - u) * (1f - u)));
            _shock.widthMultiplier = 1f - u;
        }

        /// <summary>
        /// 강림 효과 (docs/HERO.md): 착지 반경 안 크립에게 강림 피해(스킬로 얻음, 기본 0) → 바깥으로 에어본(피해 없음).
        /// 날아간 크립은 뒤집혀 떨어지고 착지 순간부터 스턴, 끝나면 바로 길로 돌아간다.
        /// 보스는 날아가지 않고(Knockback이 보스를 무시) 강림 순간 스턴 절반.
        /// </summary>
        private void Impact(Vector3 center)
        {
            if (session == null || session.Over) return;
            float dmg = session.Bonuses.DescentDamage, now = session.GameTimeMs, r2 = impactRadius * impactRadius;
            var list = session.Enemies;
            // 거꾸로 돈다: 피해로 죽은 크립은 목록에서 빠지고, 분열 자식은 끝에 붙는다(이번 충격 대상 아님)
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var e = list[i];
                var p = e.transform.position;
                if ((p.x - center.x) * (p.x - center.x) + (p.z - center.z) * (p.z - center.z) > r2) continue;
                if (dmg > 0f && session.Damage(e, dmg, dmgType)) continue;
                if (e.State.IsBoss) e.State.ApplyStun(impactStunSec * 1000f * 0.5f, now);
            }
            session.Explode(center, impactRadius, impactForce, impactStunSec * 1000f);
        }

        private void TickActive()
        {
            Move();
            SnapToGround(transform.position);
            ClampToMap();

            _atkCd -= Time.deltaTime;
            if (_beam != null && _beam.enabled && Time.time >= _beamUntil) _beam.enabled = false;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && _atkCd <= 0f)
                TryAttack();
        }

        private void Move()
        {
            if (Keyboard.current == null) return;

            Vector3 fwd = cam != null ? cam.transform.forward : transform.forward;
            fwd.y = 0f;
            fwd.Normalize();
            Vector3 right = cam != null ? cam.transform.right : transform.right;
            right.y = 0f;
            right.Normalize();

            float x = 0f, z = 0f;
            if (Keyboard.current.wKey.isPressed) z += 1f;
            if (Keyboard.current.sKey.isPressed) z -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;

            var moveDir = fwd * z + right * x;
            if (moveDir.sqrMagnitude <= 0.0001f) return;
            moveDir.Normalize();

            transform.position += moveDir * moveSpeed * Time.deltaTime;
            var targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        private void SnapToGround(Vector3 at)
        {
            if (Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out var hit, 100f))
                transform.position = new Vector3(at.x, hit.point.y + _halfHeight, at.z);
            else
                transform.position = at;
        }

        private void ClampToMap()
        {
            if (map == null) return;
            var origin = map.transform.position;
            float maxX = origin.x + map.width * map.TileSize;
            float maxZ = origin.z + map.height * map.TileSize;
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, origin.x, maxX);
            p.z = Mathf.Clamp(p.z, origin.z, maxZ);
            transform.position = p;
        }

        private void TryAttack()
        {
            if (session == null) return;

            float range = rangePx * TowerDefense.Core.GameConfig.PxToWorld;
            float r2 = range * range;
            EnemyView target = null;
            float best = float.MaxValue;
            var here = transform.position;

            foreach (var e in session.Enemies)
            {
                if (e.State.Dead) continue;
                var p = e.transform.position;
                float dx = p.x - here.x, dz = p.z - here.z;
                float d2 = dx * dx + dz * dz;
                if (d2 > r2 || d2 >= best) continue;
                best = d2;
                target = e;
            }
            if (target == null) return;

            _atkCd = cooldownSec;
            var hitPos = target.transform.position;
            session.Damage(target, atk, dmgType);
            session.Explode(hitPos, knockRadius, knockForce);

            if (_beam == null) CreateBeam();
            _beam.SetPosition(0, here + Vector3.up * _halfHeight);
            _beam.SetPosition(1, target.transform.position);
            _beam.enabled = true;
            _beamUntil = Time.time + 0.08f;
        }

        private void CreateBeam()
        {
            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(transform, false);
            _beam = beamGo.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.startWidth = 0.06f;
            _beam.endWidth = 0.02f;
            _beam.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _beam.material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.4f));
            _beam.enabled = false;
        }

        public void Descend(Vector3 worldPoint)
        {
            if (State != HeroState.Perched || DescendCooldownLeft > 0f) return;
            HideMarker();
            _descendFrom = PerchPos;
            _descendTo = worldPoint;
            _descendT = 0f;
            SetState(HeroState.Descending);
        }

        public void Return()
        {
            if (State == HeroState.Perched) return;
            HideMarker();
            transform.SetPositionAndRotation(PerchPos, PerchRot);
            if (_beam != null) _beam.enabled = false;
            _returnedAt = Time.time;
            SetState(HeroState.Perched);
        }

        public void ShowMarker(Vector3 worldPoint)
        {
            if (_marker == null) CreateMarker();
            _marker.transform.position = worldPoint + Vector3.up * 0.05f;
            _markerRing.transform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.time * 6f));
            _marker.SetActive(true);
        }

        public void HideMarker()
        {
            if (_marker != null) _marker.SetActive(false);
        }

        private void CreateMarker()
        {
            _marker = new GameObject("DescendMarker");
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", markerColor);

            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(_marker.transform, false);
            _markerRing = ringGo.AddComponent<LineRenderer>();
            _markerRing.loop = true;
            _markerRing.useWorldSpace = false;
            _markerRing.startWidth = _markerRing.endWidth = 0.08f;
            _markerRing.material = mat;
            _markerRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _markerRing.receiveShadows = false;
            _markerRing.positionCount = 32;
            for (int i = 0; i < 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                _markerRing.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * markerRadius);
            }

            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(_marker.transform, false);
            var beam = beamGo.AddComponent<LineRenderer>();
            beam.useWorldSpace = false;
            beam.startWidth = beam.endWidth = 0.04f;
            beam.material = mat;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;
            beam.positionCount = 2;
            beam.SetPosition(0, Vector3.zero);
            beam.SetPosition(1, Vector3.up * 2.5f);

            _marker.SetActive(false);
        }

        private void SetState(HeroState next)
        {
            State = next;
            if (session != null) session.HeroInField = next != HeroState.Perched;
            StateChanged?.Invoke(next);
        }
    }
}
