using System;
using System.Collections.Generic;
using TowerDefense.Core;
using UnityEngine;

namespace TowerDefense.Game
{
    public enum UpgradeBranch { AtkSpeed, Power, Poison }

    /// <summary>포탑 종류 (docs/TOWER_TYPES.md)</summary>
    public enum TowerKind { Basic, Catapult, Frost, Ballista }

    /// <summary>
    /// 포탑 수치 (docs/TOWER_PLACEMENT.md, docs/TOWER_UPGRADE.md, docs/TOWER_TYPES.md).
    /// 종류별 기본값은 <see cref="Defaults"/>. 업그레이드는 기본 포탑만 있다.
    /// ponytail: 4종이라 코드 기본값 + TowerPlacer 인스펙터로 충분하다. 종류가 늘거나 기획자가 직접 만지게 되면 ScriptableObject로 뺀다.
    /// </summary>
    [Serializable]
    public sealed class TowerSpec
    {
        public TowerKind kind = TowerKind.Basic;
        public string displayName = "기본 포탑";
        [Tooltip("이 스테이지 번호부터 설치할 수 있다")]
        public int unlockStage = 1;
        public Color color = new Color(0.55f, 0.35f, 0.25f);

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

        [Header("투석기 · 냉기 (탄이 떨어진 자리 주변)")]
        [Tooltip("탄이 날아가는 시간 (초). 떨어질 자리는 쏠 때 정한다 — 빠른 크립은 빗나갈 수 있다")]
        public float projectileSec = 0f;
        [Tooltip("탄이 떨어진 자리의 효과 반경 (m)")]
        public float splashRadius = 0f;
        [Tooltip("투석기: 폭발 넉백 세기 (m/s)")]
        public float knockForce = 0f;
        [Tooltip("냉기: 감속 비율 (0.35 = 35% 느려짐). 보스는 절반")]
        public float slowPct = 0f;
        public float slowSec = 0f;

        [Header("업그레이드 (단계별, 길이 = 최대 단계)")]
        public int[] upgradeCost = { };
        [Tooltip("최소 10초부터 (docs/IMPLEMENTATION_PLAN.md 결정 3)")]
        public float[] upgradeSec = { };
        [Tooltip("단계당 초당 공격 횟수 증가율")]
        public float atkSpeedPerLevel = 0.25f;
        [Tooltip("단계당 공격력 증가율")]
        public float powerPerLevel = 0.3f;
        [Tooltip("단계당 독 초당 피해")]
        public float poisonDpsPerLevel = 4f;
        public float poisonSec = 3f;

        public int MaxLevel => Mathf.Min(upgradeCost.Length, upgradeSec.Length);

        /// <summary>파워 lv단계의 한 발 피해</summary>
        public float AtkAt(int lv) => atk * (1f + powerPerLevel * lv);
        /// <summary>공격속도 lv단계의 공격 간격(초)</summary>
        public float CooldownAt(int lv) => cooldownSec / (1f + atkSpeedPerLevel * lv);

        public bool IsUnlocked(int stage) => stage >= unlockStage;

        /// <summary>
        /// 종류별 기본값. 해금: 1스테이지부터 한 스테이지에 하나씩 — 투석기(1) → 냉기(2) → 발리스타(3). 기본 포탑은 처음부터.
        /// ponytail: 수치는 초안, 테스트 플레이로 조정.
        /// </summary>
        public static List<TowerSpec> Defaults() => new List<TowerSpec>
        {
            new TowerSpec
            {
                kind = TowerKind.Basic, displayName = "기본 포탑", cost = 50, atk = 14f, rangePx = 280f, cooldownSec = 0.7f,
                upgradeCost = new[] { 40, 80, 120 }, upgradeSec = new[] { 10f, 15f, 20f },
            },
            new TowerSpec
            {
                kind = TowerKind.Catapult, displayName = "투석기 포탑", unlockStage = 1, color = new Color(0.45f, 0.4f, 0.38f),
                cost = 80, atk = 30f, rangePx = 375f, cooldownSec = 3f, // 20m, 최소 사거리 없음
                projectileSec = 1f, splashRadius = 3f, knockForce = 9f,
            },
            new TowerSpec
            {
                kind = TowerKind.Frost, displayName = "냉기 포탑", unlockStage = 2, color = new Color(0.55f, 0.8f, 0.95f),
                cost = 70, atk = 0f, rangePx = 190f, cooldownSec = 1.5f, dmgType = DmgType.Magic, // 10m
                projectileSec = 0.4f, splashRadius = 2.5f, slowPct = 0.35f, slowSec = 2f,
            },
            new TowerSpec
            {
                kind = TowerKind.Ballista, displayName = "발리스타 포탑", unlockStage = 3, color = new Color(0.3f, 0.42f, 0.28f),
                cost = 90, atk = 112f, rangePx = 560f, cooldownSec = 3.5f, // 30m, 기본 포탑 한 발의 8배
            },
        };
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
        private LineRenderer _rangeRing;
        private Renderer[] _outline;

        private static readonly Color SelectColor = new Color(1f, 0.85f, 0.3f);

        public Vector3 Position => transform.position;
        /// <summary>지금 공격 사거리 (m)</summary>
        public float Range => Spec.rangePx * _session.Mods.RangeMul * GameConfig.PxToWorld;
        public float InteractRadius => Spec.interactRadius;
        public string Prompt => Spec.MaxLevel > 0 ? "E: 포탑 업그레이드" : $"{Spec.displayName} (업그레이드 없음)";
        public bool IsAlive => this != null;

        public int Level(UpgradeBranch b) => _levels[(int)b];
        /// <summary>다음 단계 비용. 최대 단계면 -1.</summary>
        public int NextCost(UpgradeBranch b) => Level(b) < Spec.MaxLevel ? Spec.upgradeCost[Level(b)] : -1;
        public float NextSec(UpgradeBranch b) => Level(b) < Spec.MaxLevel ? Spec.upgradeSec[Level(b)] : 0f;
        public void ApplyUpgrade(UpgradeBranch b) { if (Level(b) < Spec.MaxLevel) _levels[(int)b]++; }

        private float Atk => Spec.AtkAt(Level(UpgradeBranch.Power));
        private float Cooldown => Spec.CooldownAt(Level(UpgradeBranch.AtkSpeed));

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
                mpb.SetColor("_BaseColor", spec.color);
                go.GetComponent<Renderer>().SetPropertyBlock(mpb);
            }
            go.name = $"{spec.kind}_{_all.Count + 1}";

            var t = go.AddComponent<Tower>();
            t.Spec = spec;
            t._session = session;
            var beamColor = spec.kind == TowerKind.Ballista ? new Color(0.25f, 0.2f, 0.15f) : new Color(1f, 0.85f, 0.4f);
            t._beam = Rings.Line(go.transform, beamColor, spec.kind == TowerKind.Ballista ? 0.4f : 0.25f);
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

        /// <summary>절벽 시점에서 클릭해 고른 포탑: 공격 사거리 원과 테두리를 보인다 (docs/INGAME_UI.md).</summary>
        public void SetSelected(bool on)
        {
            if (on && _rangeRing == null)
            {
                _rangeRing = Rings.Circle(null, SelectColor, 0.25f);
                _rangeRing.transform.position = _zoneRing.transform.position;
                _rangeRing.transform.SetParent(transform, true);
                _outline = Rings.Outline(gameObject, SelectColor, 0.35f);
            }
            if (_rangeRing == null) return;
            if (on) Rings.SetRadius(_rangeRing, Range);
            _rangeRing.gameObject.SetActive(on);
            foreach (var r in _outline) r.enabled = on;
        }

        private void Update()
        {
            if (_session == null || _session.Over) return;
            _cd -= Time.deltaTime * _session.timeScale;
            if (_beam.enabled && Time.time >= _beamUntil) _beam.enabled = false;
            if (_cd > 0f) return;

            float range = Range;
            if (Spec.kind == TowerKind.Catapult)
            {
                if (!LobAtDensest(range)) return;
            }
            else
            {
                // 기본·냉기·발리스타: 가장 가까운 크립
                var target = _session.NearestEnemy(transform.position, range);
                if (target == null) return;
                if (Spec.kind == TowerKind.Frost) Projectile.Launch(Muzzle, Ground(target.transform.position), Spec.projectileSec, 2f, Spec.color, 0.6f, Chill);
                else Shoot(target);
            }
            _cd = Cooldown * _session.Mods.CdMul;
        }

        private Vector3 Muzzle => transform.position + Vector3.up * 4.5f; // 풍차 윗부분에서

        /// <summary>기본·발리스타: 즉시 맞는 한 발 (빔으로 표시)</summary>
        private void Shoot(EnemyView target)
        {
            _beam.SetPosition(0, Muzzle);
            _beam.SetPosition(1, target.transform.position);
            _beam.enabled = true;
            _beamUntil = Time.time + (Spec.kind == TowerKind.Ballista ? 0.15f : 0.08f);

            int poison = Level(UpgradeBranch.Poison);
            if (poison > 0) target.State.ApplyDot(Spec.poisonDpsPerLevel * poison, Spec.poisonSec * 1000f, _session.GameTimeMs);
            _session.Damage(target, Atk, Spec.dmgType);
        }

        /// <summary>투석기: 사거리 안에서 크립이 가장 많이 뭉친 곳에 돌을 던진다. 떨어질 자리는 지금 정한다.</summary>
        private bool LobAtDensest(float range)
        {
            var inRange = new List<EnemyView>();
            var pts = new List<(float, float)>();
            float r2 = range * range;
            foreach (var e in _session.Enemies)
            {
                if (e.State.Dead) continue;
                var p = e.transform.position;
                float dx = p.x - transform.position.x, dz = p.z - transform.position.z;
                if (dx * dx + dz * dz > r2) continue;
                inRange.Add(e);
                pts.Add((p.x, p.z));
            }
            int i = Targeting.DensestIndex(pts, Spec.splashRadius);
            if (i < 0) return false;
            Projectile.Launch(Muzzle, Ground(inRange[i].transform.position), Spec.projectileSec, 8f, Spec.color, 1.2f, Blast);
            return true;
        }

        /// <summary>투석기 탄착: 반경 안 크립 전부 피해 + 넉백 (보스는 피해만)</summary>
        private void Blast(Vector3 at)
        {
            if (this == null || _session == null || _session.Over) return;
            foreach (var e in InSplash(at)) _session.Damage(e, Atk, Spec.dmgType);
            _session.Explode(at, Spec.splashRadius, Spec.knockForce);
        }

        /// <summary>냉기 탄착: 반경 안 크립 감속 (보스는 절반)</summary>
        private void Chill(Vector3 at)
        {
            if (this == null || _session == null || _session.Over) return;
            foreach (var e in InSplash(at))
                e.State.ApplySlow(e.State.IsBoss ? Spec.slowPct * 0.5f : Spec.slowPct, Spec.slowSec * 1000f, _session.GameTimeMs);
        }

        private List<EnemyView> InSplash(Vector3 at)
        {
            var hit = new List<EnemyView>(); // 피해로 목록이 바뀌므로 먼저 모은다
            float r2 = Spec.splashRadius * Spec.splashRadius;
            foreach (var e in _session.Enemies)
            {
                var p = e.transform.position;
                float dx = p.x - at.x, dz = p.z - at.z;
                if (!e.State.Dead && dx * dx + dz * dz <= r2) hit.Add(e);
            }
            return hit;
        }

        private Vector3 Ground(Vector3 p) => new Vector3(p.x, _session.GroundY(p), p.z);

        private void OnDrawGizmosSelected()
        {
            if (Spec == null) return;
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, Spec.rangePx * GameConfig.PxToWorld);
        }
    }

    /// <summary>포물선으로 날아가 정해진 자리에 떨어지는 탄 (투석기 돌, 냉기 얼음탄). 떨어지면 onHit(탄착 지점).</summary>
    public sealed class Projectile : MonoBehaviour
    {
        private Vector3 _from, _to;
        private float _t, _duration, _arc;
        private Action<Vector3> _onHit;

        public static void Launch(Vector3 from, Vector3 to, float duration, float arcHeight, Color color, float size, Action<Vector3> onHit)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            Destroy(go.GetComponent<Collider>()); // 지형 레이캐스트를 가리지 않게
            go.transform.localScale = Vector3.one * size;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().SetPropertyBlock(mpb);
            var p = go.AddComponent<Projectile>();
            p._from = from; p._to = to; p._duration = Mathf.Max(0.05f, duration); p._arc = arcHeight; p._onHit = onHit;
            go.transform.position = from;
        }

        private void Update()
        {
            _t += Time.deltaTime / _duration;
            float t = Mathf.Clamp01(_t);
            var pos = Vector3.Lerp(_from, _to, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * _arc;
            transform.position = pos;
            if (t < 1f) return;
            _onHit?.Invoke(_to);
            Destroy(gameObject);
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
            // 점은 링 자신의 로컬 좌표라 링의 월드 스케일을 상쇄해 월드 반지름으로 맞춘다
            // (부모 스케일만 보면, 월드 위치 유지로 붙인 링은 자기 스케일이 1/부모라 원이 부모 배율만큼 작아진다)
            var s = lr.transform.lossyScale;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r / s.x, 0f, Mathf.Sin(a) * r / s.z));
            }
        }

        public static void SetColor(LineRenderer lr, Color c) => lr.material.SetColor("_BaseColor", c);

        /// <summary>
        /// 테두리 (inverted hull): 대상의 메시마다 사방으로 thickness(m)만큼 키운 복제를 붙이고 뒷면만 단색으로 그린다.
        /// 경계 상자 중심을 기준으로 키우므로 원점이 발밑인 모델도 고르게 두꺼워진다. 처음엔 꺼 둔다.
        /// ponytail: 상자처럼 볼록한 모양에 맞다. 오목한 모델이 들어와 테두리가 어긋나면 법선 방향으로 밀어내는 셰이더로
        /// </summary>
        public static Renderer[] Outline(GameObject target, Color c, float thickness)
        {
            var m = Mat(c);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
            var list = new List<Renderer>();
            foreach (var mf in target.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var b = mesh.bounds;
                var size = Vector3.Scale(b.size, mf.transform.lossyScale);
                var k = new Vector3(1f + 2f * thickness / Mathf.Max(size.x, 0.01f), 1f + 2f * thickness / Mathf.Max(size.y, 0.01f), 1f + 2f * thickness / Mathf.Max(size.z, 0.01f));
                var go = new GameObject("Outline");
                go.transform.SetParent(mf.transform, false);
                go.transform.localScale = k;
                go.transform.localPosition = Vector3.Scale(b.center, Vector3.one - k);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                list.Add(mr);
            }
            return list.ToArray();
        }

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
