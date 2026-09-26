using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Map
{
    /// <summary>지형 메시 생성 파라미터. 인스펙터에서 편집한다.</summary>
    [System.Serializable]
    public sealed class TerrainMeshSettings
    {
        [Header("타일")]
        [Tooltip("타일 한 변의 길이 (m)")]
        public float tileSize = 4f;
        [Tooltip("타일 한 변을 몇 칸으로 쪼갤지. 클수록 지형이 부드럽고 삼각형이 늘어난다.")]
        [Range(1, 6)] public int subdivisions = 3;

        [Header("지형 높낮이")]
        [Tooltip("지형 높이 진폭 (m)")]
        public float heightAmplitude = 1.9f;
        [Tooltip("노이즈 주파수. 작을수록 완만한 언덕.")]
        public float noiseFrequency = 0.0675f;
        [Range(1, 4)] public int octaves = 3;
        [Tooltip("타일 경계를 감추기 위한 꼭짓점 XZ 흔들림 (서브셀 크기 대비 비율)")]
        [Range(0f, 0.45f)] public float xzJitter = 0.3f;

        [Header("경로")]
        [Tooltip("경로 표면 높이 (m). 경로 전체가 이 높이로 평탄해진다.")]
        public float pathHeight = 0f;
        [Tooltip("경로 절반 너비 (타일 크기 대비 비율)")]
        [Range(0.2f, 0.5f)] public float pathHalfWidth = 0.34f;
        [Tooltip("경로 가장자리에서 지형 높이로 올라가는 완충 폭 (타일 크기 대비 비율)")]
        [Range(0.1f, 1.5f)] public float pathBlendWidth = 0.6f;
        [Tooltip("경로 가장자리를 울퉁불퉁하게 만드는 노이즈 (타일 크기 대비 비율)")]
        [Range(0f, 0.2f)] public float pathEdgeNoise = 0.07f;
        [Tooltip("스폰/도착 지점 원형 반경 (타일 크기 대비 비율)")]
        [Range(0.3f, 0.7f)] public float spotRadius = 0.45f;
    }

    /// <summary>
    /// <see cref="MapGrid"/>를 하나의 연속된 지형 메시로 만든다.
    /// - 격자 꼭짓점을 타일끼리 공유하고 XZ를 흔들어 타일 경계가 드러나지 않게 한다.
    /// - 경로는 타일 중심을 잇는 폴리라인까지의 거리장으로 정의해 모서리가 둥글고 가장자리가 유기적이다.
    /// - 경로 영역은 pathHeight로 평탄, 바깥은 노이즈 지형으로 부드럽게 이어진다.
    /// - 서브메시 4개: 0 Ground, 1 Path, 2 Spawn, 3 Goal (평면 셰이딩용으로 삼각형마다 꼭짓점 분리).
    /// </summary>
    public static class TerrainMeshBuilder
    {
        public static Mesh Build(MapGrid grid, TerrainMeshSettings s, int seed)
        {
            int w = grid.Width, h = grid.Height;
            int sub = Mathf.Max(1, s.subdivisions);
            float cell = s.tileSize / sub;
            int vw = w * sub + 1, vh = h * sub + 1;

            var rng = new System.Random(seed * 7919 + 17);
            float ox = (float)rng.NextDouble() * 1000f, oz = (float)rng.NextDouble() * 1000f;
            float ex = (float)rng.NextDouble() * 1000f, ez = (float)rng.NextDouble() * 1000f;

            // 경로 폴리라인 (타일 중심, XZ 평면)
            var poly = new List<Vector2>(grid.Path.Count);
            foreach (var p in grid.Path) poly.Add(TileCenter(p, s.tileSize));
            var spawnC = TileCenter(grid.Spawn, s.tileSize);
            var goalC = TileCenter(grid.Goal, s.tileSize);
            float halfW = s.pathHalfWidth * s.tileSize;
            float blendW = s.pathBlendWidth * s.tileSize;
            float spotR = s.spotRadius * s.tileSize;
            float edgeN = s.pathEdgeNoise * s.tileSize;

            float PathDist(Vector2 p)
            {
                float d = DistToPolyline(p, poly);
                // 스폰/도착 원형 광장: 원 안은 경로와 같은 취급
                d = Mathf.Min(d, Vector2.Distance(p, spawnC) - (spotR - halfW));
                d = Mathf.Min(d, Vector2.Distance(p, goalC) - (spotR - halfW));
                return d;
            }

            // 1) 공유 꼭짓점 위치 계산
            var pos = new Vector3[vw * vh];
            for (int j = 0; j < vh; j++)
            for (int i = 0; i < vw; i++)
            {
                float x = i * cell, z = j * cell;
                bool border = i == 0 || j == 0 || i == vw - 1 || j == vh - 1;
                if (!border && s.xzJitter > 0f)
                {
                    // 결정적 지터 (시드·인덱스 기반)
                    float jx = Hash01(seed, i, j, 1) - 0.5f;
                    float jz = Hash01(seed, i, j, 2) - 0.5f;
                    x += jx * 2f * s.xzJitter * cell;
                    z += jz * 2f * s.xzJitter * cell;
                }
                var p2 = new Vector2(x, z);
                float terrain = Fbm(x * s.noiseFrequency + ox, z * s.noiseFrequency + oz, s.octaves) * s.heightAmplitude;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfW, halfW + blendW, PathDist(p2)));
                float y = Mathf.Lerp(s.pathHeight, s.pathHeight + terrain, t);
                pos[j * vw + i] = new Vector3(x, y, z);
            }

            // 2) 서브셀마다 종류 판정 후 삼각형 2개 (평면 셰이딩: 꼭짓점 분리)
            var verts = new List<Vector3>(w * h * sub * sub * 6);
            var colors = new List<Color>(verts.Capacity);
            var tris = new List<int>[4];
            for (int k = 0; k < 4; k++) tris[k] = new List<int>();

            for (int ty = 0; ty < h; ty++)
            for (int tx = 0; tx < w; tx++)
            for (int sj = 0; sj < sub; sj++)
            for (int si = 0; si < sub; si++)
            {
                int i0 = tx * sub + si, j0 = ty * sub + sj;
                var a = pos[j0 * vw + i0];
                var b = pos[j0 * vw + i0 + 1];
                var c = pos[(j0 + 1) * vw + i0 + 1];
                var d = pos[(j0 + 1) * vw + i0];
                var center = (a + b + c + d) * 0.25f;
                var c2 = new Vector2(center.x, center.z);

                int kind;
                if (Vector2.Distance(c2, spawnC) < spotR) kind = 2;
                else if (Vector2.Distance(c2, goalC) < spotR) kind = 3;
                else
                {
                    float edge = (Fbm(c2.x * 0.9f + ex, c2.y * 0.9f + ez, 2) - 0.5f) * 2f * edgeN;
                    kind = PathDist(c2) + edge < halfW ? 1 : 0;
                }

                // 짧은 대각선으로 분할 → 로우폴리 지형이 덜 규칙적으로 보인다.
                bool splitAC = (a - c).sqrMagnitude <= (b - d).sqrMagnitude;
                var col = kind == 0 ? GroundColor(center, seed) : Color.white;
                if (splitAC)
                {
                    AddTri(verts, colors, tris[kind], a, c, b, col);
                    AddTri(verts, colors, tris[kind], a, d, c, col);
                }
                else
                {
                    AddTri(verts, colors, tris[kind], a, d, b, col);
                    AddTri(verts, colors, tris[kind], b, d, c, col);
                }
            }

            var mesh = new Mesh { name = "TerrainMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.subMeshCount = 4;
            for (int k = 0; k < 4; k++) mesh.SetTriangles(tris[k], k, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Vector2 TileCenter(Vector2Int t, float tileSize) =>
            new Vector2((t.x + 0.5f) * tileSize, (t.y + 0.5f) * tileSize);

        private static void AddTri(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 cc, Color col)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(cc);
            c.Add(col); c.Add(col); c.Add(col);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        private static Color GroundColor(Vector3 p, int seed)
        {
            // 초원 색 미세 변주 (셰이더가 버텍스 컬러를 쓰면 반영, 아니면 무시)
            float n = Fbm(p.x * 0.35f + seed * 0.013f, p.z * 0.35f, 2);
            return Color.Lerp(new Color(0.40f, 0.60f, 0.28f), new Color(0.52f, 0.70f, 0.33f), n);
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        private static float Hash01(int seed, int i, int j, int k)
        {
            unchecked
            {
                uint h = (uint)seed * 374761393u + (uint)i * 668265263u + (uint)j * 2246822519u + (uint)k * 3266489917u;
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static float DistToPolyline(Vector2 p, List<Vector2> poly)
        {
            if (poly.Count == 1) return Vector2.Distance(p, poly[0]);
            float best = float.MaxValue;
            for (int i = 0; i < poly.Count - 1; i++)
            {
                float d = DistToSegment(p, poly[i], poly[i + 1]);
                if (d < best) best = d;
            }
            return best;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
