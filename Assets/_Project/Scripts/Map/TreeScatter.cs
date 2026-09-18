using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Map
{
    /// <summary>
    /// 지형의 넓은 빈 땅(경로에서 pathMargin 타일 이상 떨어진 Ground 타일의 4방향 연결 영역)마다
    /// 나무를 최대 maxPerRegion그루 심는다. <see cref="TileMap.Generate"/>가 호출하며 같은 시드는 같은 배치.
    /// 생성물은 씬에 저장하지 않는다 (DontSave) — 지형 메시와 같은 정책.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TileMap))]
    public sealed class TreeScatter : MonoBehaviour
    {
        public GameObject[] treePrefabs;
        [Tooltip("빈 땅 영역 하나당 최대 나무 수")]
        [Min(0)] public int maxPerRegion = 10;
        [Tooltip("경로(스폰·도착 포함)에서 이 타일 수 이내(대각 포함)에는 심지 않는다")]
        [Min(0)] public int pathMargin = 1;
        [Tooltip("타일 중심에서 무작위로 벗어나는 최대 거리 (타일 크기 대비 비율)")]
        [Range(0f, 0.45f)] public float jitter = 0.3f;
        [Tooltip("모델 원본은 2~4m. 타일(1.5m) 기준으로 줄여서 심는다")]
        public Vector2 scaleRange = new Vector2(0.4f, 0.6f);

        private const string RootName = "Trees";

        [ContextMenu("Scatter")]
        public void Scatter()
        {
            Clear();
            var map = GetComponent<TileMap>();
            var grid = map.Grid;
            if (grid == null || treePrefabs == null || treePrefabs.Length == 0) return;

            var root = new GameObject(RootName) { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(transform, false);
            var rng = new System.Random(map.seed * 31 + 7);
            Physics.SyncTransforms(); // 에디터에서 방금 바꾼 MeshCollider로 레이캐스트하기 위해

            foreach (var region in Regions(grid))
            {
                // Fisher-Yates 부분 셔플로 서로 다른 타일 n개를 고른다
                int n = Mathf.Min(maxPerRegion, region.Count);
                for (int i = 0; i < n; i++)
                {
                    int j = i + rng.Next(region.Count - i);
                    (region[i], region[j]) = (region[j], region[i]);
                    Place(map, root, region[i], rng);
                }
            }
        }

        public void Clear()
        {
            var old = transform.Find(RootName);
            if (old == null) return;
            if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
        }

        private void Place(TileMap map, Transform root, Vector2Int tile, System.Random rng)
        {
            float j = jitter * map.TileSize;
            var pos = map.TileToWorld(tile) + new Vector3(Rand(rng, -j, j), 0f, Rand(rng, -j, j));
            pos.y = Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var hit, 100f) ? hit.point.y - 0.03f : pos.y;

            var prefab = treePrefabs[rng.Next(treePrefabs.Length)];
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f), root);
            go.name = prefab.name;
            go.transform.localScale = Vector3.one * Rand(rng, scaleRange.x, scaleRange.y);
            go.hideFlags = HideFlags.DontSave;
        }

        private static float Rand(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>경로에서 충분히 떨어진 Ground 타일을 4방향 연결 영역으로 묶는다.</summary>
        private List<List<Vector2Int>> Regions(MapGrid grid)
        {
            int w = grid.Width, h = grid.Height;
            var eligible = new bool[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                eligible[y * w + x] = grid.Get(x, y) == TileType.Ground && !NearPath(grid, x, y);

            var regions = new List<List<Vector2Int>>();
            var queue = new Queue<Vector2Int>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!eligible[y * w + x]) continue;
                var region = new List<Vector2Int>();
                eligible[y * w + x] = false;
                queue.Enqueue(new Vector2Int(x, y));
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    region.Add(c);
                    foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                    {
                        var nb = c + d;
                        if (!grid.InBounds(nb) || !eligible[nb.y * w + nb.x]) continue;
                        eligible[nb.y * w + nb.x] = false;
                        queue.Enqueue(nb);
                    }
                }
                regions.Add(region);
            }
            return regions;
        }

        private bool NearPath(MapGrid grid, int x, int y)
        {
            for (int dy = -pathMargin; dy <= pathMargin; dy++)
            for (int dx = -pathMargin; dx <= pathMargin; dx++)
                if (grid.IsWalkable(x + dx, y + dy)) return true;
            return false;
        }
    }
}
