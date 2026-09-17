using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Map
{
    /// <summary>
    /// 정사각 타일 격자의 논리 데이터. 렌더링과 무관하게 타일 종류와 경로 순서만 담는다.
    /// x = 열(0..Width-1), y = 행(0..Height-1). Spawn에서 Goal까지의 경로 타일은 <see cref="Path"/> 순서대로.
    /// </summary>
    public sealed class MapGrid
    {
        public int Width { get; }
        public int Height { get; }
        public Vector2Int Spawn { get; }
        public Vector2Int Goal { get; }

        /// <summary>Spawn(첫 원소)부터 Goal(마지막 원소)까지 4방향으로 이어진 경로 타일.</summary>
        public IReadOnlyList<Vector2Int> Path => _path;

        private readonly TileType[] _tiles;
        private readonly List<Vector2Int> _path;

        public MapGrid(int width, int height, IReadOnlyList<Vector2Int> path)
        {
            Width = width;
            Height = height;
            _tiles = new TileType[width * height];
            _path = new List<Vector2Int>(path);
            Spawn = _path[0];
            Goal = _path[_path.Count - 1];
            foreach (var p in _path) _tiles[Index(p.x, p.y)] = TileType.Path;
            _tiles[Index(Spawn.x, Spawn.y)] = TileType.Spawn;
            _tiles[Index(Goal.x, Goal.y)] = TileType.Goal;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(Vector2Int p) => InBounds(p.x, p.y);
        public TileType Get(int x, int y) => _tiles[Index(x, y)];
        public TileType Get(Vector2Int p) => _tiles[Index(p.x, p.y)];

        /// <summary>적이 걸을 수 있는 타일(Path/Spawn/Goal)인가.</summary>
        public bool IsWalkable(int x, int y) => InBounds(x, y) && Get(x, y) != TileType.Ground;
        public bool IsWalkable(Vector2Int p) => IsWalkable(p.x, p.y);

        private int Index(int x, int y) => y * Width + x;

        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        /// <summary>BFS로 Spawn에서 Goal까지 걷기 가능한 타일만으로 연결되어 있는지 검사.</summary>
        public bool IsConnected()
        {
            var visited = new bool[Width * Height];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(Spawn);
            visited[Index(Spawn.x, Spawn.y)] = true;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == Goal) return true;
                foreach (var d in Dirs)
                {
                    var n = c + d;
                    if (!IsWalkable(n)) continue;
                    int i = Index(n.x, n.y);
                    if (visited[i]) continue;
                    visited[i] = true;
                    queue.Enqueue(n);
                }
            }
            return false;
        }
    }
}
