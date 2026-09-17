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
