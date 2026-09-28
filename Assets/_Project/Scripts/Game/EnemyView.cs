using System.Collections.Generic;
using TowerDefense.Core;
using UnityEngine;

namespace TowerDefense.Game
{
    /// <summary>
    /// 몹 1기의 3D 표현. 전투 상태는 <see cref="State"/>(Core)가 갖고, 이 컴포넌트는 위치·색만 반영한다.
    /// 이동 (docs/CREEP_MOVEMENT.md):
    /// - 길: 경로 진행 거리(State.Dist) + 옆 위치(Lateral)로 길 폭 안에서 무리 지어 흐른다. 서로 밀어내기는 세션이 한다.
    /// - 공중: 넉백으로 날아가는 중 (포물선, 길 밖까지 가능). 보스는 날아가지 않는다.
    /// - 복귀: 착지한 뒤 가장 가까운 길로 걸어 돌아와 길 이동을 잇는다.
    /// - 뒤집힘: 착지 스턴이 걸린 넉백(영웅 강림)은 공중에서 뒤집혀 떨어지고, 착지하는 순간부터 스턴. 스턴이 끝나면 바로 일어나 길로 돌아간다.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        public MobState State { get; private set; }

        public enum Move { Path, Air, Return }
        public Move Mode { get; private set; } = Move.Path;
        /// <summary>경로 중심선에서 오른쪽으로 떨어진 거리 (m)</summary>
        public float Lateral;

        private const float Gravity = 20f;
        private const float WanderSpeed = 0.35f; // 옆으로 흘러가는 속도 (m/s)
        private EnemySpawner _session;
        private Vector3 _feet;
        private Vector3 _vel;
        private float _speedJitter = 1f;
        private float _latTarget;
        private float _wanderAt;
        private float _returnDist, _returnLat;
        private float _launchDist; // 날아가기 전 진행 거리 — 복귀는 이 근처 길로
        /// <summary>복귀 지점을 찾는 범위: 날아가기 전 진행 거리 앞뒤 (m)</summary>
        private const float ReturnWindow = 3f;
        private float _landStunMs;   // 착지하는 순간 걸 스턴 (0이면 뒤집히지 않는다)
        private bool _flipped;       // 뒤집혀 누워 있는 중 (공중 포함)

        // 걷기 애니메이션 (Tools/Blender/make_farm.py "Walk", 1초 주기). 실제 이동 속도에 맞춰 재생 속도를 바꾼다
        private AnimationState _walk;
        private const float WalkStride = 1.6f;   // 크기 1일 때 한 주기에 나아가는 거리(m) — 발이 미끄러져 보이면 조정
        private const float FlailSpeed = 2.5f;   // 날아가거나 뒤집혀 있을 때 버둥거리는 빠르기
        private const float MaxWalkSpeed = 4f;
        private float _roll;         // 앞뒤 축 기울기 (도). 180 = 뒤집힘
        private Quaternion _yaw = Quaternion.identity;
        private const float FlipSpeed = 540f, RightSpeed = 1440f; // 뒤집힘 / 일어남 (도/초)

        private PathFollower _path;
        private float _yOffset;
        private readonly List<(Renderer r, int slot)> _tint = new List<(Renderer, int)>();
        private readonly List<(Renderer r, int slot)> _allSlots = new List<(Renderer, int)>();
        // 피격 번쩍임: 피해를 주는 쪽(EnemySpawner)이 Flash를 부른다. 직접 피해는 빨강(맞을 때마다),
        // 독 지속 피해는 초록(매 프레임 들어오므로 FlashGap마다 한 번)
        private const float FlashSec = 0.12f, FlashGap = 0.35f;
        private static readonly Color HitColor = new Color(1f, 0.12f, 0.1f), PoisonColor = new Color(0.25f, 0.95f, 0.2f);
        private static MaterialPropertyBlock _clearMpb;
        private float _flashUntil, _nextPoisonFlashAt;
        private Color _flashColor = HitColor;
        private bool _flashing;
        private MaterialPropertyBlock _mpb;
        private Color _baseColor;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>몹 기본 높이 (m) = 사람 크기. 단계·보스 배율(MobStats.Scale)이 곱해진다 (docs/SCALE.md).</summary>
        public const float BaseHeight = 1.8f;

        /// <summary>경로 끝(도착 지점)에 닿았는가 — 길 위에 있을 때만</summary>
        public bool ReachedEnd => Mode == Move.Path && State.Dist * GameConfig.PxToWorld >= _path.Total;
        public float DistWorld => State.Dist * GameConfig.PxToWorld;

        /// <summary>경로 진행률 0~1</summary>
        public float Progress => Mathf.Clamp01(State.Dist * GameConfig.PxToWorld / _path.Total);

        /// <summary>
        /// 몹 뷰 생성. prefab이 있으면 그 모델(발밑 원점, +Z 정면)을 높이에 맞춰 스케일하고
        /// 이름에 "Wool"이 들어간 머티리얼 슬롯만 계열 색으로 틴트한다. 없으면 캡슐 프리미티브.
        /// </summary>
        public static EnemyView Create(MobState state, EnemySpawner session, Transform parent, GameObject prefab = null)
        {
            GameObject go;
            float yOffset;
            var tint = new List<(Renderer, int)>();
            var all = new List<(Renderer, int)>();
            float height = BaseHeight * state.Scale;

            if (prefab != null)
            {
                go = Object.Instantiate(prefab, parent);
                go.name = state.Name;
                var renderers = go.GetComponentsInChildren<Renderer>();
                var b = new Bounds(go.transform.position, Vector3.zero);
                bool first = true;
                foreach (var r in renderers)
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        all.Add((r, i));
                        if (mats[i] != null && (mats[i].name.Contains("Wool") || mats[i].name.Contains("Body"))) tint.Add((r, i));
                    }
                }
                float modelH = Mathf.Max(0.01f, b.size.y);
                go.transform.localScale = Vector3.one * (height / modelH);
                yOffset = 0f;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = state.Name;
                go.transform.SetParent(parent, false);
                float s = height * 0.5f; // 캡슐 기본 높이 2
                go.transform.localScale = new Vector3(s, s, s);
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col); // 물리 불필요 — 타워는 거리로 판정
                tint.Add((go.GetComponent<Renderer>(), 0));
                all.Add((go.GetComponent<Renderer>(), 0));
                yOffset = s; // 캡슐 중심을 발밑에서 반높이만큼 올린다
            }

            var view = go.AddComponent<EnemyView>();
            var anim = go.GetComponentInChildren<Animation>();
            if (anim != null && anim.clip != null)
            {
                view._walk = anim[anim.clip.name];
                view._walk.wrapMode = WrapMode.Loop;
                anim.Play();
                view._walk.normalizedTime = Random.value; // 무리가 발을 맞춰 걷지 않게
            }
            view._session = session;
            view.Init(state, session.Path, yOffset, tint, all);
            view.BuildHpBar();
            view.BuildStun();
            return view;
        }

        // ---------- HP 바: 머리 위, 항상 카메라(사용자 시점) 정면을 향한다 ----------

        private const float BarW = 1.2f, BarH = 0.15f, BarGap = 0.4f;
        private static Material _barBgMat, _barFillMat;
        private Transform _bar, _barFill;

        private void BuildHpBar()
        {
            if (_barBgMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                _barBgMat = new Material(sh) { color = new Color(0.1f, 0.1f, 0.1f) };
                _barFillMat = new Material(sh) { color = new Color(0.85f, 0.15f, 0.1f) };
            }
            _bar = new GameObject("HpBar").transform;
            _bar.SetParent(transform, false);
            BarQuad(_barBgMat).localScale = new Vector3(BarW, BarH, 1f);
            _barFill = BarQuad(_barFillMat);
        }

        private Transform BarQuad(Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            q.transform.SetParent(_bar, false);
            return q.transform;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // 발 기준으로 올린다 — 뒤집혀도 바는 머리 위 같은 높이
            _bar.position = _feet + Vector3.up * (BaseHeight * State.Scale + BarGap);
            _bar.rotation = cam.transform.rotation;
            _bar.localScale = Vector3.one / transform.lossyScale.x; // 몹 크기와 무관하게 월드 크기 고정
            float t = Mathf.Clamp01(State.Hp / State.MaxHp);
            _barFill.localScale = new Vector3(BarW * t, BarH, 1f);
            _barFill.localPosition = new Vector3(-BarW * (1f - t) * 0.5f, 0f, -0.01f); // 왼쪽 정렬, 배경보다 카메라 쪽
            TickStun();
        }

        // ---------- 스턴 별: 스턴 동안 머리 위에서 별 3개가 수평으로 돈다 (docs/VFX_STUN.md) ----------

        private const float StarOrbit = 0.45f, StarSize = 0.18f, StarAbove = 0.6f; // 크기 1 기준 (m)
        private const float OrbitDegPerSec = 400f, SpinDegPerSec = 600f;          // 궤도 0.9초 / 제자리 0.6초에 한 바퀴
        private const float PopInSec = 0.1f, PopOutSec = 0.15f;
        private const float FlippedStarHeight = 0.85f; // 뒤집혔을 때 별 높이 (몸 높이 비율). 위로 뻗은 다리 끝 조금 아래
        private const float RingWidth = 0.03f; // 별을 잇는 궤도 원 두께 (m)
        private const int RingSegments = 40;
        private static Mesh _starMesh;
        private static Material _starMat, _ringMat;
        private Transform _stun, _head;
        private LineRenderer _ring;
        private readonly Transform[] _stars = new Transform[3];
        private float _pop, _nowMs;

        private void BuildStun()
        {
            if (_starMesh == null)
            {
                _starMesh = StarMesh();
                _starMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _starMat.SetColor(BaseColorId, new Color(1f, 0.82f, 0.2f));
                _starMat.EnableKeyword("_EMISSION"); // 뒤집혀 그늘진 쪽에서도 보이게
                _starMat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.1f) * 0.6f);
                _ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.88f, 0.45f) };
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Head") { _head = t; break; } // make_farm.py 리그의 머리 본. 없으면(캡슐) 몸 위
            _stun = new GameObject("Stun").transform;
            _stun.SetParent(transform, false);
            for (int i = 0; i < 3; i++)
            {
                var s = new GameObject("Star", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                s.GetComponent<MeshFilter>().sharedMesh = _starMesh;
                var r = s.GetComponent<MeshRenderer>();
                r.sharedMaterial = _starMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.SetParent(_stun, false);
                s.localScale = Vector3.one * StarSize;
                _stars[i] = s;
            }
            // 별들을 잇는 궤도 원: 피벗 로컬 좌표라 같이 돌고 커진다. 선은 늘 카메라를 향한다
            _ring = new GameObject("Ring").AddComponent<LineRenderer>();
            _ring.transform.SetParent(_stun, false);
            _ring.useWorldSpace = false;
            _ring.loop = true;
            _ring.sharedMaterial = _ringMat;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.positionCount = RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(a) * StarOrbit, 0f, Mathf.Sin(a) * StarOrbit));
            }
            _stun.gameObject.SetActive(false);
        }

        private void TickStun()
        {
            bool stunned = State.IsStunned(_nowMs);
            _pop = Mathf.MoveTowards(_pop, stunned ? 1f : 0f, Time.deltaTime / (stunned ? PopInSec : PopOutSec));
            _stun.gameObject.SetActive(_pop > 0f);
            if (_pop <= 0f) return;

            float k = State.Scale;
            // 서 있으면 머리 위. 뒤집혀 누우면 머리가 땅 쪽이라 몸에 가리므로, 하늘을 향한 배(모델 하단) 위 다리 사이에 띄운다.
            // 궤도는 늘 월드 수평
            _stun.position = _roll > 90f ? _feet + Vector3.up * (BaseHeight * k * FlippedStarHeight)
                : (_head != null ? _head.position : _feet + Vector3.up * (BaseHeight * k * 0.9f)) + Vector3.up * (StarAbove * k);
            _stun.rotation = Quaternion.Euler(0f, Time.time * OrbitDegPerSec, 0f);
            float pop = stunned ? _pop + 0.15f * Mathf.Sin(_pop * Mathf.PI) : _pop; // 나올 땐 톡 튀어나온다
            _stun.localScale = Vector3.one * (k * pop / transform.lossyScale.x);
            _ring.widthMultiplier = RingWidth * k * pop; // 선 두께는 스케일을 따르지 않는다
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                _stars[i].localPosition = new Vector3(Mathf.Cos(a) * StarOrbit, 0.03f * Mathf.Sin(Time.time * 8f + i * 2.1f), Mathf.Sin(a) * StarOrbit);
                _stars[i].localRotation = Quaternion.Euler(0f, Time.time * SpinDegPerSec, 0f);
            }
        }

        /// <summary>두께 있는 오각 별 (반지름 1, xy 평면). 앞뒤 면 부채꼴 + 옆면</summary>
        private static Mesh StarMesh()
        {
            const float Inner = 0.45f, Depth = 0.15f;
            var v = new List<Vector3>();
            var tri = new List<int>();
            Vector3 Rim(int i)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 1f : Inner;
                return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            foreach (float z in new[] { -Depth, Depth })
            {
                int c = v.Count;
                v.Add(new Vector3(0f, 0f, z));
                for (int i = 0; i < 10; i++) v.Add(Rim(i) + Vector3.forward * z);
                for (int i = 0; i < 10; i++)
                {
                    int a = c + 1 + i, b = c + 1 + (i + 1) % 10;
                    if (z < 0f) tri.AddRange(new[] { c, b, a }); else tri.AddRange(new[] { c, a, b });
                }
            }
            for (int i = 0; i < 10; i++) // 옆면은 꼭짓점을 따로 둬서 각지게
            {
                Vector3 a = Rim(i), b = Rim((i + 1) % 10), f = Vector3.back * Depth;
                int c = v.Count;
                v.AddRange(new[] { a + f, b + f, b - f, a - f });
                tri.AddRange(new[] { c, c + 1, c + 2, c, c + 2, c + 3 });
            }
            var m = new Mesh { name = "Star" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private void Init(MobState state, PathFollower path, float yOffset, List<(Renderer, int)> tint, List<(Renderer, int)> all)
        {
            _allSlots.AddRange(all);
            State = state;
            _path = path;
            _yOffset = yOffset;
            _tint.AddRange(tint);
            _mpb = new MaterialPropertyBlock();
            _baseColor = ToColor(state.Color);
            ApplyColor(1f);
            _speedJitter = Random.Range(0.88f, 1.12f);
            Lateral = _latTarget = Random.Range(-1f, 1f) * _session.PathHalfWidth;
            _feet = _path.PosAt(DistWorld, Lateral);
            Place(0f);
        }

        public void Tick(float nowMs, float deltaMs, float speedMul)
        {
            float dt = deltaMs / 1000f;
            _nowMs = nowMs;
            switch (Mode)
            {
                case Move.Path: TickPath(nowMs, deltaMs, speedMul, dt); break;
                case Move.Air: TickAir(nowMs, dt); break;
                case Move.Return: TickReturn(nowMs, speedMul, dt); break;
            }
            Place(dt);
            ApplyColor(Mathf.Clamp01(State.Hp / State.MaxHp));
        }

        /// <summary>
        /// 폭발에 맞아 from에서 바깥쪽으로 날아간다. force = 수평 초기 속도(m/s). 보스는 무시.
        /// landStunMs &gt; 0이면 공중에서 뒤집히고, 착지하는 순간 그만큼 스턴 (docs/HERO.md 강림 효과).
        /// </summary>
        public void Knockback(Vector3 from, float force, float landStunMs = 0f)
        {
            if (State.IsBoss || State.Dead || force <= 0f) return;
            var dir = _feet - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            dir.Normalize();
            _vel = dir * force + Vector3.up * force * 0.8f;
            if (Mode == Move.Path) _launchDist = DistWorld; // 이미 날아가는 중이면 처음 자리 기준 유지
            if (Mode != Move.Air) _landStunMs = 0f;
            _landStunMs = Mathf.Max(_landStunMs, landStunMs);
            _flipped |= _landStunMs > 0f;
            Mode = Move.Air;
        }

        private void TickPath(float nowMs, float deltaMs, float speedMul, float dt)
        {
            State.Advance(nowMs, deltaMs, speedMul * _speedJitter);
            // 무리 느낌: 가끔 새 옆 위치를 골라 천천히 흘러간다
            if (Time.time >= _wanderAt)
            {
                _wanderAt = Time.time + Random.Range(1.5f, 3.5f);
                _latTarget = Random.Range(-1f, 1f) * _session.PathHalfWidth;
            }
            Lateral = Mathf.MoveTowards(Lateral, _latTarget, WanderSpeed * dt);
            ClampLateral();
            _feet = _path.PosAt(DistWorld, Lateral);
            _feet.y = _session.GroundY(_feet); // 길 가장자리 경사를 따라
        }

        private void TickAir(float nowMs, float dt)
        {
            _vel.y -= Gravity * dt;
            _feet += _vel * dt;
            _feet = _session.ClampToField(_feet);
            float ground = _session.GroundY(_feet);
            if (_feet.y > ground || _vel.y > 0f) return;
            _feet.y = ground;
            (_returnDist, _returnLat) = _path.Closest(_feet, _launchDist - ReturnWindow, _launchDist + ReturnWindow);
            _returnLat = Mathf.Clamp(_returnLat, -_session.PathHalfWidth, _session.PathHalfWidth);
            if (_landStunMs > 0f) State.ApplyStun(_landStunMs, nowMs);
            _landStunMs = 0f;
            Mode = Move.Return;
        }

        private void TickReturn(float nowMs, float speedMul, float dt)
        {
            if (State.IsStunned(nowMs)) return; // 아래 최저 속도(1m/s)가 스턴을 뚫지 않게
            _flipped = false; // 스턴이 끝나면 바로 일어나 돌아간다
            var target = _path.PosAt(_returnDist, _returnLat);
            float speed = Mathf.Max(1f, State.CurrentSpeed(nowMs, speedMul) * GameConfig.PxToWorld);
            var flat = new Vector3(target.x - _feet.x, 0f, target.z - _feet.z);
            float step = speed * dt;
            if (flat.magnitude <= step + 0.05f)
            {
                State.Dist = _returnDist * GameConfig.WorldToPx;
                Lateral = _latTarget = _returnLat;
                _feet = target;
                Mode = Move.Path;
                return;
            }
            _feet += flat.normalized * step;
            _feet.y = _session.GroundY(_feet);
        }

        public void ClampLateral() => Lateral = Mathf.Clamp(Lateral, -_session.PathHalfWidth, _session.PathHalfWidth);

        private void Place(float dt)
        {
            var prev = transform.position;
            var pos = _feet + Vector3.up * _yOffset;
            transform.position = pos;
            var move = pos - prev;
            if (_walk != null && dt > 0f)
            {
                var flat = new Vector3(move.x, 0f, move.z);
                // 애니메이션은 실제 시간으로 재생되므로 실제 시간당 이동으로 나눈다 (배속을 켜면 다리도 그만큼 빨리)
                float realDt = Mathf.Max(Time.deltaTime, 1e-4f);
                _walk.speed = _flipped ? FlailSpeed : Mathf.Min(MaxWalkSpeed * 2f, flat.magnitude / realDt / (WalkStride * State.Scale)); // 멈추면(스턴) 0
            }
            move.y = 0f;
            var face = Mode == Move.Path ? _path.DirAt(DistWorld) : move;
            face.y = 0f;
            if (face.sqrMagnitude > 1e-6f && !(_flipped && Mode != Move.Air))
            {
                var want = Quaternion.LookRotation(face.normalized, Vector3.up);
                _yaw = dt > 0f ? Quaternion.Slerp(_yaw, want, 1f - Mathf.Exp(-10f * dt)) : want;
            }

            // 뒤집힘: 앞뒤 축으로 굴러 누웠다가 일어난다. 발밑 원점 모델은 뒤집히면 땅에 묻히므로 몸 높이만큼 올린다
            _roll = Mathf.MoveTowards(_roll, _flipped ? 180f : 0f, (_flipped ? FlipSpeed : RightSpeed) * dt);
            if (dt <= 0f) _roll = _flipped ? 180f : 0f;
            if (_yOffset <= 0f) transform.position = pos + Vector3.up * (BaseHeight * State.Scale * (1f - Mathf.Cos(_roll * Mathf.Deg2Rad)) * 0.5f);
            transform.rotation = _yaw * Quaternion.Euler(0f, 0f, _roll);
        }

        /// <summary>
        /// 처치됨: 세션 목록에서 빠진 뒤 호출. 막타도 피격으로 보이게 몸 전체를 빨갛게 번쩍이고(FlashSec) 사라진다.
        /// 한 방에 죽는 크립(영웅 공격 등)은 이게 없으면 빨개질 틈 없이 사라진다.
        /// </summary>
        /// <summary>피해를 입었다: 몸 전체를 잠깐 빨강(직접 피해) 또는 초록(독)으로.</summary>
        public void Flash(bool poison)
        {
            if (poison)
            {
                if (Time.time < _nextPoisonFlashAt || Time.time < _flashUntil) return; // 빨강을 덮지 않는다
                _nextPoisonFlashAt = Time.time + FlashGap;
            }
            _flashColor = poison ? PoisonColor : HitColor;
            _flashUntil = Time.time + FlashSec;
        }

        public void Die(bool poison = false)
        {
            enabled = false;
            _bar.gameObject.SetActive(false);
            _stun.gameObject.SetActive(false);
            _mpb.SetColor(BaseColorId, poison ? PoisonColor : HitColor);
            foreach (var (r, slot) in _allSlots) r.SetPropertyBlock(_mpb, slot);
            if (_walk != null) _walk.speed = 0f;
            Destroy(gameObject, FlashSec);
        }

        private void ApplyColor(float hpRatio)
        {
            if (Time.time < _flashUntil)
            {
                _mpb.SetColor(BaseColorId, _flashColor);
                foreach (var (r, slot) in _allSlots) r.SetPropertyBlock(_mpb, slot);
                _flashing = true;
                return;
            }
            if (_flashing)
            {
                // 번쩍임 끝: 틴트하지 않는 슬롯은 덮어쓴 색을 지워 머티리얼 원래 색으로
                _clearMpb ??= new MaterialPropertyBlock();
                foreach (var (r, slot) in _allSlots) r.SetPropertyBlock(_clearMpb, slot);
                _flashing = false;
            }
            // HP가 줄수록 어두워진다 (피격 피드백)
            var c = Color.Lerp(_baseColor * 0.35f, _baseColor, 0.4f + 0.6f * hpRatio);
            c.a = 1f;
            _mpb.SetColor(BaseColorId, c);
            foreach (var (r, slot) in _tint) r.SetPropertyBlock(_mpb, slot);
        }

        private static Color ToColor(uint rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);
    }
}
