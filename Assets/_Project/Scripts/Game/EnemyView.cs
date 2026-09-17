using System.Collections.Generic;
using TowerDefense.Core;
using UnityEngine;

namespace TowerDefense.Game
{
    /// <summary>
    /// 몹 1기의 3D 표현. 전투 상태는 <see cref="State"/>(Core)가 갖고, 이 컴포넌트는 경로 위치·색만 반영한다.
    /// 프로토타입 모델 = 캡슐 프리미티브. 계열 색, 단계별 크기, HP 비율에 따라 어두워짐.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        public MobState State { get; private set; }

        private PathFollower _path;
        private float _yOffset;
        private readonly List<(Renderer r, int slot)> _tint = new List<(Renderer, int)>();
        private MaterialPropertyBlock _mpb;
        private Color _baseColor;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>몹 기본 높이 (m). 단계·보스 배율(MobStats.Scale)이 곱해진다.</summary>
        public const float BaseHeight = 0.8f;

        /// <summary>경로 끝(도착 지점)에 닿았는가</summary>
        public bool ReachedEnd => State.Dist * GameConfig.PxToWorld >= _path.Total;

        /// <summary>경로 진행률 0~1</summary>
        public float Progress => Mathf.Clamp01(State.Dist * GameConfig.PxToWorld / _path.Total);

        /// <summary>
        /// 몹 뷰 생성. prefab이 있으면 그 모델(발밑 원점, +Z 정면)을 높이에 맞춰 스케일하고
        /// 이름에 "Wool"이 들어간 머티리얼 슬롯만 계열 색으로 틴트한다. 없으면 캡슐 프리미티브.
        /// </summary>
        public static EnemyView Create(MobState state, PathFollower path, Transform parent, GameObject prefab = null)
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
            view.Init(state, path, yOffset, tint);
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
            Place();
        }

        public void Tick(float nowMs, float deltaMs, float speedMul)
        {
            State.Advance(nowMs, deltaMs, speedMul);
            Place();
            ApplyColor(Mathf.Clamp01(State.Hp / State.MaxHp));
        }

        private void Place()
        {
            float d = State.Dist * GameConfig.PxToWorld;
            transform.position = _path.PosAt(d) + Vector3.up * _yOffset;
            var dir = _path.DirAt(d);
            if (dir.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
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
