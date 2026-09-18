using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Map
{
    /// <summary>
    /// 타일맵 = 논리 격자(<see cref="MapGrid"/>) + 연속 지형 메시.
    /// 인스펙터의 Generate 버튼(또는 컨텍스트 메뉴)으로 다시 만든다. 같은 시드는 같은 맵.
    /// 적 이동은 <see cref="Waypoints"/>(스폰 → 도착, 경로 타일 중심, 평탄 높이)를 따른다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class TileMap : MonoBehaviour
    {
        [Header("격자")]
        [Min(4)] public int width = 20;
        [Min(4)] public int height = 12;
        public int seed = 1;
        [Range(0f, 1f)] public float windiness = 0.6f;

        [Header("지형")]
        public TerrainMeshSettings terrain = new TerrainMeshSettings();

        [Header("머티리얼 (Ground, Path, Spawn, Goal 순)")]
        public Material groundMaterial;
        public Material pathMaterial;
        public Material spawnMaterial;
        public Material goalMaterial;

        [Header("실행")]
        [Tooltip("플레이 시작 시 자동 생성")]
        public bool generateOnStart = true;

        public MapGrid Grid { get; private set; }

        private readonly List<Vector3> _waypoints = new List<Vector3>();

        /// <summary>스폰에서 도착까지의 월드 좌표 웨이포인트 (경로 타일 중심, 평탄 높이).</summary>
        public IReadOnlyList<Vector3> Waypoints => _waypoints;

        public float TileSize => terrain.tileSize;

        private void OnEnable()
        {
            // 씬 로드(에디터·플레이 모두)에서 메시가 없으면 시드로 재생성한다. 생성은 결정적이라 씬에 메시를 저장할 필요가 없다.
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh == null || (Application.isPlaying && generateOnStart)) Generate();
            else if (Grid == null) RebuildGridOnly();
        }

        /// <summary>격자와 메시를 새로 만든다.</summary>
        [ContextMenu("Generate")]
        public void Generate()
        {
            Grid = PathGenerator.Generate(width, height, seed, windiness);
            Debug.Assert(Grid.IsConnected(), "경로가 연결되어 있지 않습니다");

            var mesh = TerrainMeshBuilder.Build(Grid, terrain, seed);
            var mf = GetComponent<MeshFilter>();
            var mr = GetComponent<MeshRenderer>();
            var mc = GetComponent<MeshCollider>();

            DestroyMesh(mf.sharedMesh);
            mf.sharedMesh = mesh;
            mc.sharedMesh = mesh;
            mr.sharedMaterials = new[] { groundMaterial, pathMaterial, spawnMaterial, goalMaterial };

            RebuildWaypoints();
            if (TryGetComponent<TreeScatter>(out var trees)) trees.Scatter();
        }

        /// <summary>시드를 바꾸고 다시 생성.</summary>
        [ContextMenu("Generate (new seed)")]
        public void GenerateNewSeed()
        {
            seed = Random.Range(1, int.MaxValue);
            Generate();
        }

        /// <summary>메시는 그대로 두고 논리 격자만 복원 (씬 로드 후).</summary>
        private void RebuildGridOnly()
        {
            Grid = PathGenerator.Generate(width, height, seed, windiness);
            RebuildWaypoints();
            if (TryGetComponent<TreeScatter>(out var trees)) trees.Scatter(); // DontSave라 도메인 리로드 후 사라진다
        }

        private void RebuildWaypoints()
        {
            _waypoints.Clear();
            foreach (var t in Grid.Path) _waypoints.Add(TileToWorld(t));
        }

        /// <summary>타일 좌표 → 타일 중심 월드 좌표 (경로 높이 기준).</summary>
        public Vector3 TileToWorld(Vector2Int t)
        {
            var c = TerrainMeshBuilder.TileCenter(t, terrain.tileSize);
            return transform.TransformPoint(new Vector3(c.x, terrain.pathHeight, c.y));
        }

        /// <summary>월드 좌표 → 타일 좌표. 격자 밖이면 null.</summary>
        public Vector2Int? WorldToTile(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            int x = Mathf.FloorToInt(local.x / terrain.tileSize);
            int y = Mathf.FloorToInt(local.z / terrain.tileSize);
            if (Grid == null || !Grid.InBounds(x, y)) return null;
            return new Vector2Int(x, y);
        }

        private static void DestroyMesh(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
        }

        private void OnDrawGizmosSelected()
        {
            if (Grid == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _waypoints.Count - 1; i++) Gizmos.DrawLine(_waypoints[i] + Vector3.up * 0.1f, _waypoints[i + 1] + Vector3.up * 0.1f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(TileToWorld(Grid.Spawn) + Vector3.up * 0.1f, 0.25f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(TileToWorld(Grid.Goal) + Vector3.up * 0.1f, 0.25f);
        }
    }
}
