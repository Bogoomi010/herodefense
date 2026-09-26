using System.Collections.Generic;

namespace TowerDefense.Core
{
    /// <summary>포탑 표적 고르기 (docs/TOWER_TYPES.md).</summary>
    public static class Targeting
    {
        /// <summary>
        /// 반경 안 이웃이 가장 많은 점의 번호 (투석기: 크립이 가장 많이 뭉친 곳). 같으면 앞쪽. 비었으면 -1.
        /// ponytail: O(n²) — 사거리 안 크립 수(수십)에서는 충분하다.
        /// </summary>
        public static int DensestIndex(IReadOnlyList<(float x, float z)> pts, float radius)
        {
            float r2 = radius * radius;
            int best = -1, bestCount = -1;
            for (int i = 0; i < pts.Count; i++)
            {
                int count = 0;
                for (int j = 0; j < pts.Count; j++)
                {
                    float dx = pts[i].x - pts[j].x, dz = pts[i].z - pts[j].z;
                    if (dx * dx + dz * dz <= r2) count++;
                }
                if (count > bestCount) { best = i; bestCount = count; }
            }
            return best;
        }
    }
}
