using System;
using System.Collections.Generic;
using TowerDefense.Core;
using UnityEngine;

namespace TowerDefense.Game
{
    public enum UpgradeBranch { AtkSpeed, Power, Poison }

    /// <summary>
    /// 포탑 수치 (docs/TOWER_PLACEMENT.md, docs/TOWER_UPGRADE.md). 베타는 일반 공격 포탑 1종.
    /// ponytail: 종류가 하나라 TowerPlacer 인스펙터에 둔다. 2종 이상이 되면 ScriptableObject로 뺀다.
    /// </summary>
    [Serializable]
    public sealed class TowerSpec
    {
        [Header("설치")]
        public int cost = 50;
        [Tooltip("포탑이 차지하는 바닥 원 반지름 (m)")]
        public float footprintRadius = 1.5f;
        [Tooltip("포탑 구역 기준 반지름 (m). 플레이어 스킬로 줄어든다")]
        public float zoneRadius = 5f;
        [Tooltip("영웅 상호작용 반지름 (m). 구역 축소와 무관")]
        public float interactRadius = 5f;

        [Header("공격")]
        public float atk = 14f;
        [Tooltip("사거리 (px, GameConfig.PxToWorld로 m 변환) — 280px ≈ 15m")]
        public float rangePx = 280f;
        public float cooldownSec = 0.7f;
        public DmgType dmgType = DmgType.Phys;

        [Header("업그레이드 (단계별, 길이 = 최대 단계)")]
        public int[] upgradeCost = { 40, 80, 120 };
        [Tooltip("최소 10초부터 (docs/IMPLEMENTATION_PLAN.md 결정 3)")]
        public float[] upgradeSec = { 10f, 15f, 20f };
        [Tooltip("단계당 초당 공격 횟수 증가율")]
        public float atkSpeedPerLevel = 0.25f;
        [Tooltip("단계당 공격력 증가율")]
        public float powerPerLevel = 0.3f;
        [Tooltip("단계당 독 초당 피해")]
        public float poisonDpsPerLevel = 4f;
        public float poisonSec = 3f;

        public int MaxLevel => Mathf.Min(upgradeCost.Length, upgradeSec.Length);
    }

    /// <summary>플레이어가 설치한 포탑. 사거리 안 가장 가까운 몹을 공격하고, 방향별 업그레이드 단계를 가진다.</summary>
    public sealed class Tower : MonoBehaviour, IHeroInteractable
    {
        private static readonly List<Tower> _all = new List<Tower>();
        public static IReadOnlyList<Tower> All => _all;

        public TowerSpec Spec { get; private set; }
        private readonly int[] _levels = new int[3];

        private EnemySpawner _session;
        private float _cd;
        private LineRenderer _beam;
        private float _beamUntil;
        private LineRenderer _zoneRing;

        public Vector3 Position => transform.position;
        public float InteractRadius => Spec.interactRadius;
        public string Prompt => "E: 포탑 업그레이드";
        public bool IsAlive => this != null;

        public int Level(UpgradeBranch b) => _levels[(int)b];
        /// <summary>다음 단계 비용. 최대 단계면 -1.</summary>
        public int NextCost(UpgradeBranch b) => Level(b) < Spec.MaxLevel ? Spec.upgradeCost[Level(b)] : -1;
        public float NextSec(UpgradeBranch b) => Level(b) < Spec.MaxLevel ? Spec.upgradeSec[Level(b)] : 0f;
        public void ApplyUpgrade(UpgradeBranch b) { if (Level(b) < Spec.MaxLevel) _levels[(int)b]++; }

        private float Atk => Spec.atk * (1f + Spec.powerPerLevel * Level(UpgradeBranch.Power));
        private float Cooldown => Spec.cooldownSec / (1f + Spec.atkSpeedPerLevel * Level(UpgradeBranch.AtkSpeed));

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public static Tower Create(EnemySpawner session, TowerSpec spec, Vector3 ground, GameObject prefab)
        {
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, ground, Quaternion.identity, session.transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(session.transform, false);
                // ponytail: 풍차 크기 자리표시 상자 (밑면 3m, 높이 11m). 모델이 나오면 towerPrefab으로
                go.transform.localScale = new Vector3(3f, 11f, 3f);
                go.transform.position = ground + Vector3.up * 5.5f;
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col); // 레이캐스트가 지형에 닿도록
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", new Color(0.55f, 0.35f, 0.25f));
                go.GetComponent<Renderer>().SetPropertyBlock(mpb);
            }
            go.name = $"Tower_{_all.Count + 1}";

            var t = go.AddComponent<Tower>();
            t.Spec = spec;
            t._session = session;
            t._beam = Rings.Line(go.transform, new Color(1f, 0.85f, 0.4f), 0.25f);
            t._beam.enabled = false;
            t._zoneRing = Rings.Circle(null, new Color(1f, 1f, 1f, 0.6f), 0.15f);
            t._zoneRing.transform.position = ground + Vector3.up * 0.05f;
            t._zoneRing.transform.SetParent(go.transform, true);
            t._zoneRing.gameObject.SetActive(false);
            return t;
        }

        /// <summary>설치 모드에서 구역 원 표시.</summary>
        public void ShowZone(bool on, float radius)
        {
            _zoneRing.gameObject.SetActive(on);
            if (on) Rings.SetRadius(_zoneRing, radius);
        }

        private void Update()
        {
            if (_session == null || _session.Over) return;
            _cd -= Time.deltaTime * _session.timeScale;
            if (_beam.enabled && Time.time >= _beamUntil) _beam.enabled = false;
            if (_cd > 0f) return;

            var target = _session.NearestEnemy(transform.position, Spec.rangePx * _session.Mods.RangeMul * GameConfig.PxToWorld);
            if (target == null) return;

            _cd = Cooldown * _session.Mods.CdMul;
            _beam.SetPosition(0, transform.position + Vector3.up * 4.5f); // 풍차 윗부분에서
            _beam.SetPosition(1, target.transform.position);
            _beam.enabled = true;
            _beamUntil = Time.time + 0.08f;

            int poison = Level(UpgradeBranch.Poison);
            if (poison > 0) target.State.ApplyDot(Spec.poisonDpsPerLevel * poison, Spec.poisonSec * 1000f, _session.GameTimeMs);
            _session.Damage(target, Atk, Spec.dmgType);
        }

        private void OnDrawGizmosSelected()
        {
            if (Spec == null) return;
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, Spec.rangePx * GameConfig.PxToWorld);
        }
    }

    /// <summary>바닥 원·빔 표시용 LineRenderer 헬퍼.</summary>
    public static class Rings
    {
        private const int Segments = 48;
        private static Material _mat;

        private static Material Mat(Color c)
        {
            var m = new Material(_mat != null ? _mat : (_mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))));
            m.SetColor("_BaseColor", c);
            return m;
        }

        /// <summary>XZ 평면의 단위 원 (반지름은 <see cref="SetRadius"/>).</summary>
        public static LineRenderer Circle(Transform parent, Color c, float width)
        {
            var go = new GameObject("Ring");
            if (parent != null) go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.useWorldSpace = false;
            lr.startWidth = lr.endWidth = width;
            lr.material = Mat(c);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = Segments;
            SetRadius(lr, 1f);
            return lr;
        }

        public static void SetRadius(LineRenderer lr, float r)
        {
            // 부모 스케일을 상쇄해 월드 반지름으로 맞춘다
            var s = lr.transform.parent != null ? lr.transform.parent.lossyScale : Vector3.one;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r / s.x, 0f, Mathf.Sin(a) * r / s.z));
            }
        }

        public static void SetColor(LineRenderer lr, Color c) => lr.material.SetColor("_BaseColor", c);

        public static LineRenderer Line(Transform parent, Color c, float width)
        {
            var go = new GameObject("Beam");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width * 0.4f;
            lr.material = Mat(c);
            return lr;
        }
    }
}
