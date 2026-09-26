using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Game
{
    /// <summary>열린 폴리라인(스폰 → 도착)을 따라 거리 → 위치/방향 변환. 원본 core/path.ts의 비순환 버전.</summary>
    public sealed class PathFollower
    {
        private readonly Vector3[] _pts;
        private readonly float[] _cum; // 각 점까지의 누적 거리

        /// <summary>전체 길이 (월드 단위)</summary>
        public float Total { get; }

        public Vector3 Start => _pts[0];
        public Vector3 End => _pts[_pts.Length - 1];

        public PathFollower(IReadOnlyList<Vector3> pts)
        {
            _pts = new Vector3[pts.Count];
            _cum = new float[pts.Count];
            float total = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                _pts[i] = pts[i];
                if (i > 0) total += Vector3.Distance(_pts[i - 1], _pts[i]);
                _cum[i] = total;
            }
            Total = total;
        }

        public Vector3 PosAt(float d)
        {
            if (d <= 0f) return _pts[0];
            if (d >= Total) return _pts[_pts.Length - 1];
            int i = Seg(d);
            float len = _cum[i + 1] - _cum[i];
            float t = len <= 1e-6f ? 0f : (d - _cum[i]) / len;
            return Vector3.Lerp(_pts[i], _pts[i + 1], t);
        }

        public Vector3 DirAt(float d)
        {
            int i = Seg(Mathf.Clamp(d, 0f, Total - 1e-4f));
            var dir = _pts[i + 1] - _pts[i];
            return dir.sqrMagnitude <= 1e-8f ? Vector3.forward : dir.normalized;
        }

        /// <summary>
        /// 진행 방향의 오른쪽(XZ 평면). 모퉁이에서 옆 위치가 튀지 않도록 앞뒤 smooth m 구간의 방향을 평균한다.
        /// </summary>
        public Vector3 RightAt(float d, float smooth = 0.6f)
        {
            var f = PosAt(d + smooth) - PosAt(d - smooth);
            f.y = 0f;
            if (f.sqrMagnitude < 1e-8f) f = DirAt(d);
            f.Normalize();
            return new Vector3(f.z, 0f, -f.x);
        }

        /// <summary>경로 위 d 지점에서 옆으로 lateral m 떨어진 위치.</summary>
        public Vector3 PosAt(float d, float lateral) => PosAt(d) + RightAt(d) * lateral;

        /// <summary>
        /// 월드 점 p(XZ)에 가장 가까운 경로 지점: 진행 거리와 옆 거리(오른쪽 +). 넉백 후 복귀 지점을 찾는 데 쓴다.
        /// [minDist, maxDist] 구간의 길만 본다 — 구불구불한 길에서 이웃한 다른 구간으로 건너뛰지 않게.
        /// </summary>
        public (float dist, float lateral) Closest(Vector3 p, float minDist = float.MinValue, float maxDist = float.MaxValue)
        {
            float bestD2 = float.MaxValue, bestDist = 0f, bestLat = 0f;
            for (int i = 0; i < _pts.Length - 1; i++)
            {
                if (_cum[i + 1] < minDist || _cum[i] > maxDist) continue;
                Vector3 a = _pts[i], b = _pts[i + 1];
                var ab = new Vector2(b.x - a.x, b.z - a.z);
                var ap = new Vector2(p.x - a.x, p.z - a.z);
                float len2 = ab.sqrMagnitude;
                float t = len2 <= 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(ap, ab) / len2);
                if (len2 > 1e-8f)
                {
                    float segLen = Mathf.Sqrt(len2); // 구간 제한을 선분 안의 t 범위로
                    t = Mathf.Clamp(t, Mathf.Clamp01((minDist - _cum[i]) / segLen), Mathf.Clamp01((maxDist - _cum[i]) / segLen));
                }
                var q = ab * t;
                float d2 = (ap - q).sqrMagnitude;
                if (d2 >= bestD2) continue;
                bestD2 = d2;
                bestDist = _cum[i] + Mathf.Sqrt(len2) * t;
                var right = len2 <= 1e-8f ? Vector2.zero : new Vector2(ab.y, -ab.x).normalized;
                bestLat = Vector2.Dot(ap - q, right);
            }
            return (bestDist, bestLat);
        }

        private int Seg(float d)
        {
            int lo = 0, hi = _pts.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_cum[mid] <= d) lo = mid; else hi = mid - 1;
            }
            return lo;
        }
    }
}
