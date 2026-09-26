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

        [Header("강림")]
        public float descendDuration = 0.7f;
        public float descendArcHeight = 4f;
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

        private void TickDescending()
        {
            _descendT += Time.deltaTime / Mathf.Max(0.0001f, descendDuration);
            float t = Mathf.Clamp01(_descendT);

            var pos = Vector3.Lerp(_descendFrom, _descendTo, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * descendArcHeight;
            transform.position = pos;

            var dir = _descendTo - _descendFrom;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized);

            if (t >= 1f)
            {
                SnapToGround(_descendTo);
                _activeSince = Time.time;
                SetState(HeroState.Active);
            }
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
