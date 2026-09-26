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
        private float _roll;         // 앞뒤 축 기울기 (도). 180 = 뒤집힘
        private Quaternion _yaw = Quaternion.identity;
        private const float FlipSpeed = 540f, RightSpeed = 1440f; // 뒤집힘 / 일어남 (도/초)

        private PathFollower _path;
        private float _yOffset;
        private readonly List<(Renderer r, int slot)> _tint = new List<(Renderer, int)>();
        private readonly List<(Renderer r, int slot)> _allSlots = new List<(Renderer, int)>();
        // 피격 번쩍임: HP가 줄면 몸 전체가 잠깐 빨개진다. 독처럼 매 프레임 줄어드는 피해는 FlashGap마다 한 번씩 깜빡인다
        private const float FlashSec = 0.12f, FlashGap = 0.35f;
        private static readonly Color FlashColor = new Color(1f, 0.12f, 0.1f);
        private static MaterialPropertyBlock _clearMpb;
        private float _lastHp, _flashUntil, _nextFlashAt;
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
                        if (mats[i] != null && mats[i].name.Contains("Wool")) tint.Add((r, i));
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
            view._session = session;
            view.Init(state, session.Path, yOffset, tint, all);
            return view;
        }

        private void Init(MobState state, PathFollower path, float yOffset, List<(Renderer, int)> tint, List<(Renderer, int)> all)
        {
            _allSlots.AddRange(all);
            _lastHp = state.Hp;
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
            switch (Mode)
            {
                case Move.Path: TickPath(nowMs, deltaMs, speedMul, dt); break;
                case Move.Air: TickAir(nowMs, dt); break;
                case Move.Return: TickReturn(nowMs, speedMul, dt); break;
            }
            Place(dt);
            if (State.Hp < _lastHp - 1e-3f && Time.time >= _nextFlashAt)
            {
                _flashUntil = Time.time + FlashSec;
                _nextFlashAt = Time.time + FlashGap;
            }
            _lastHp = State.Hp;
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

        private void ApplyColor(float hpRatio)
        {
            if (Time.time < _flashUntil)
            {
                _mpb.SetColor(BaseColorId, FlashColor);
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
