using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Map
{
    /// <summary>
    /// 왼쪽 가장자리의 Spawn에서 오른쪽 가장자리의 Goal까지 구불구불한 단일 경로를 만든다.
    /// 규칙: 경로는 자기 자신과 붙지 않는다 (각 경로 타일의 경로 이웃은 앞뒤 2개뿐).
    /// 따라서 2×2 덩어리나 막다른 곁가지가 생기지 않는다.
    /// 결과는 항상 <see cref="MapGrid.IsConnected"/>를 통과한다 (모든 시도가 실패하면 직선 경로로 대체).
    /// </summary>
    public static class PathGenerator
    {
        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0),
        };

        /// <param name="width">열 수 (4 이상)</param>
        /// <param name="height">행 수 (4 이상)</param>
        /// <param name="seed">난수 시드. 같은 시드는 같은 맵.</param>
        /// <param name="windiness">0 = 목표로 곧장, 1 = 매우 구불구불</param>
        public static MapGrid Generate(int width, int height, int seed, float windiness = 0.6f)
        {
            width = Mathf.Max(4, width);
            height = Mathf.Max(4, height);
            windiness = Mathf.Clamp01(windiness);
            var rng = new System.Random(seed);

            for (int attempt = 0; attempt < 40; attempt++)
            {
                // 위아래 한 줄은 지형 여백으로 남긴다.
                var spawn = new Vector2Int(0, rng.Next(1, height - 1));
                var goal = new Vector2Int(width - 1, rng.Next(1, height - 1));
                var path = Carve(width, height, spawn, goal, rng, windiness);
                if (path == null) continue;
                var grid = new MapGrid(width, height, path);
                if (grid.IsConnected()) return grid;
            }

            // 대체 경로: 절대 실패하지 않는 가로 직선
            var fallback = new List<Vector2Int>();
            int y0 = height / 2;
            for (int x = 0; x < width; x++) fallback.Add(new Vector2Int(x, y0));
            return new MapGrid(width, height, fallback);
        }

        /// <summary>무작위 순서 DFS(백트래킹)로 자기 자신과 붙지 않는 경로를 판다. 탐색 예산 초과 시 null.</summary>
        private static List<Vector2Int> Carve(int w, int h, Vector2Int spawn, Vector2Int goal, System.Random rng, float windiness)
        {
            var onPath = new bool[w * h];
            var path = new List<Vector2Int> { spawn };
            onPath[spawn.y * w + spawn.x] = true;

            var frames = new Stack<List<Vector2Int>>();
            frames.Push(Candidates(spawn, goal, w, h, onPath, rng, windiness));

            int budget = 200_000;
            while (frames.Count > 0 && budget-- > 0)
            {
                var cands = frames.Peek();
                if (cands.Count == 0)
                {
                    frames.Pop();
                    var last = path[path.Count - 1];
                    onPath[last.y * w + last.x] = false;
                    path.RemoveAt(path.Count - 1);
                    continue;
                }
                var next = cands[cands.Count - 1];
                cands.RemoveAt(cands.Count - 1);

                path.Add(next);
                onPath[next.y * w + next.x] = true;
                if (next == goal) return path;
                frames.Push(Candidates(next, goal, w, h, onPath, rng, windiness));
            }
            return null;
        }

        /// <summary>
        /// cur에서 갈 수 있는 이웃. 조건: 격자 안(위아래 여백 제외), 미방문, 경로 이웃이 cur 하나뿐(붙음 방지).
        /// 목표에 가까워지는 방향을 우선하되 windiness만큼 무작위성을 섞는다. 스택 뒤에서부터 꺼내므로 역순 정렬.
        /// </summary>
        private static List<Vector2Int> Candidates(Vector2Int cur, Vector2Int goal, int w, int h, bool[] onPath, System.Random rng, float windiness)
        {
            var list = new List<(Vector2Int p, float score)>(4);
            foreach (var d in Dirs)
            {
                var n = cur + d;
                if (n.x < 0 || n.x >= w || n.y < 1 || n.y >= h - 1) continue;
                if (onPath[n.y * w + n.x]) continue;
                if (PathNeighbors(n, w, h, onPath) != 1) continue;
                float dist = Mathf.Abs(goal.x - n.x) + Mathf.Abs(goal.y - n.y);
                float noise = (float)rng.NextDouble() * windiness * (w + h);
                list.Add((n, dist + noise));
            }
            // 점수가 낮을수록 좋다. 나중에 넣은 것을 먼저 꺼내므로 내림차순 정렬.
            list.Sort((a, b) => b.score.CompareTo(a.score));
            var result = new List<Vector2Int>(list.Count);
            foreach (var (p, _) in list) result.Add(p);
            return result;
        }

        private static int PathNeighbors(Vector2Int p, int w, int h, bool[] onPath)
        {
            int n = 0;
            foreach (var d in Dirs)
            {
                var q = p + d;
                if (q.x < 0 || q.x >= w || q.y < 0 || q.y >= h) continue;
                if (onPath[q.y * w + q.x]) n++;
            }
            return n;
        }
    }
}
