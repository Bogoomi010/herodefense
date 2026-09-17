using TowerDefense.Core;
using TowerDefense.Map;
using UnityEngine;

namespace TowerDefense.Game
{
    /// <summary>
    /// 적군 격추 테스트용 최소 타워: 큐브 프리미티브, 사거리 내 가장 가까운 몹을 주기마다 공격.
    /// 수치는 원본 흔함 유닛 수준(px 단위). 정식 유닛 전투(UnitCombat)는 M1에서 이식한다.
    /// </summary>
    public sealed class TestTower : MonoBehaviour
    {
        public float atk = 14f;
        public float rangePx = 260f;
        public float cooldownSec = 0.7f;
        public DmgType dmgType = DmgType.Phys;

        private EnemySpawner _session;
        private float _cd;
        private LineRenderer _beam;
        private float _beamUntil;

        public static TestTower Create(EnemySpawner session, TileMap map, Vector2Int tile, int index)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"TestTower_{index}";
            go.transform.SetParent(session.transform, false);
            go.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);

            // 지형 표면 높이에 맞춰 놓는다
            var center = map.TileToWorld(tile);
            float y = center.y;
            if (Physics.Raycast(center + Vector3.up * 20f, Vector3.down, out var hit, 50f)) y = hit.point.y;
            go.transform.position = new Vector3(center.x, y + 0.6f, center.z);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var r = go.GetComponent<Renderer>();
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", index % 2 == 0 ? new Color(0.9f, 0.55f, 0.15f) : new Color(0.25f, 0.65f, 0.85f));
            r.SetPropertyBlock(mpb);

            var t = go.AddComponent<TestTower>();
            t._session = session;
            t.dmgType = index % 2 == 0 ? DmgType.Magic : DmgType.Phys; // 물리/마법 섞어서 방어력 처리 확인
            t.atk = index % 2 == 0 ? 10f : 14f;

            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(go.transform, false);
            t._beam = beamGo.AddComponent<LineRenderer>();
            t._beam.positionCount = 2;
            t._beam.startWidth = 0.06f;
            t._beam.endWidth = 0.02f;
            t._beam.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            t._beam.material.SetColor("_BaseColor", t.dmgType == DmgType.Magic ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.85f, 0.4f));
            t._beam.enabled = false;
            return t;
        }

        private void Update()
        {
            if (_session == null || _session.Over) return;
            float dt = Time.deltaTime * _session.timeScale;
            _cd -= dt;
            if (_beam.enabled && Time.time >= _beamUntil) _beam.enabled = false;
            if (_cd > 0f) return;

            float range = rangePx * _session.Mods.RangeMul * GameConfig.PxToWorld;
            float r2 = range * range;
            EnemyView target = null;
            float best = float.MaxValue;
            var here = transform.position;
            foreach (var e in _session.Enemies)
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

            _cd = cooldownSec * _session.Mods.CdMul;
            _beam.SetPosition(0, here + Vector3.up * 0.5f);
            _beam.SetPosition(1, target.transform.position);
            _beam.enabled = true;
            _beamUntil = Time.time + 0.08f;
            _session.Damage(target, atk, dmgType);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            float range = rangePx * GameConfig.PxToWorld;
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
