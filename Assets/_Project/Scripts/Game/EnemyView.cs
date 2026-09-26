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

        private PathFollower _path;
        private float _yOffset;
        private readonly List<(Renderer r, int slot)> _tint = new List<(Renderer, int)>();
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
                        if (mats[i] != null && mats[i].name.Contains("Wool")) tint.Add((r, i));
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
                yOffset = s; // 캡슐 중심을 발밑에서 반높이만큼 올린다
            }

            var view = go.AddComponent<EnemyView>();
            view._session = session;
            view.Init(state, session.Path, yOffset, tint);
            return view;
        }

        private void Init(MobState state, PathFollower path, float yOffset, List<(Renderer, int)> tint)
        {
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
                case Move.Air: TickAir(dt); break;
                case Move.Return: TickReturn(nowMs, speedMul, dt); break;
            }
            Place(dt);
            ApplyColor(Mathf.Clamp01(State.Hp / State.MaxHp));
        }

        /// <summary>폭발에 맞아 from에서 바깥쪽으로 날아간다. force = 수평 초기 속도(m/s). 보스는 무시.</summary>
        public void Knockback(Vector3 from, float force)
        {
            if (State.IsBoss || State.Dead || force <= 0f) return;
            var dir = _feet - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            dir.Normalize();
            _vel = dir * force + Vector3.up * force * 0.8f;
            if (Mode == Move.Path) _launchDist = DistWorld; // 이미 날아가는 중이면 처음 자리 기준 유지
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

        private void TickAir(float dt)
        {
            _vel.y -= Gravity * dt;
            _feet += _vel * dt;
            _feet = _session.ClampToField(_feet);
            float ground = _session.GroundY(_feet);
            if (_feet.y > ground || _vel.y > 0f) return;
            _feet.y = ground;
            (_returnDist, _returnLat) = _path.Closest(_feet, _launchDist - ReturnWindow, _launchDist + ReturnWindow);
            _returnLat = Mathf.Clamp(_returnLat, -_session.PathHalfWidth, _session.PathHalfWidth);
            Mode = Move.Return;
        }

        private void TickReturn(float nowMs, float speedMul, float dt)
        {
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
            if (face.sqrMagnitude > 1e-6f)
            {
                var want = Quaternion.LookRotation(face.normalized, Vector3.up);
                transform.rotation = dt > 0f ? Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-10f * dt)) : want;
            }
        }

        private void ApplyColor(float hpRatio)
        {
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
