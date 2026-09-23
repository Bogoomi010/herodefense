using System;
using System.Collections.Generic;

namespace TowerDefense.Core
{
    public enum PlaceResult { Ok, NoGold, NotGround, Tree, TooClose }

    /// <summary>원 하나 (월드 XZ 평면, m)</summary>
    public readonly struct Circle
    {
        public readonly float X, Z, R;
        public Circle(float x, float z, float r) { X = x; Z = z; R = r; }
    }

    /// <summary>포탑 설치 판정 (docs/TOWER_PLACEMENT.md). 순서: 골드 → 녹색 지대 → 나무 → 포탑 구역.</summary>
    public static class PlacementRules
    {
        /// <summary>바닥 둘레 샘플 수. 타일(1.5m)보다 바닥이 훨씬 작아 이 정도로 충분하다.</summary>
        public const int RimSamples = 12;
        private const float Eps = 1e-4f;

        /// <param name="isGround">(x, z)가 녹색 지대인가</param>
        /// <param name="trees">나무 바닥 원</param>
        /// <param name="towers">기존 포탑 중심 (R 무시)</param>
        /// <param name="zoneRadius">현재 포탑 구역 반지름 (모든 포탑 공통)</param>
        public static PlaceResult Check(float x, float z, float footprint, float zoneRadius, int gold, int cost,
            Func<float, float, bool> isGround, IEnumerable<Circle> trees, IEnumerable<Circle> towers)
        {
            if (gold < cost) return PlaceResult.NoGold;

            if (!isGround(x, z)) return PlaceResult.NotGround;
            for (int i = 0; i < RimSamples; i++)
            {
                double a = i * Math.PI * 2 / RimSamples;
                if (!isGround(x + footprint * (float)Math.Cos(a), z + footprint * (float)Math.Sin(a))) return PlaceResult.NotGround;
            }

            foreach (var t in trees)
                if (Dist2(x, z, t) < Sq(footprint + t.R) - Eps) return PlaceResult.Tree;

            // 구역끼리 맞닿는 것까지는 허용
            foreach (var t in towers)
                if (Dist2(x, z, t) < Sq(2f * zoneRadius) - Eps) return PlaceResult.TooClose;

            return PlaceResult.Ok;
        }

        /// <summary>현재 포탑 구역 반지름: 기준값 × 스킬 배율, 하한 바닥 반지름.</summary>
        public static float ZoneRadius(float baseRadius, float zoneMul, float footprint) => Math.Max(footprint, baseRadius * zoneMul);

        private static float Dist2(float x, float z, Circle c) => Sq(x - c.X) + Sq(z - c.Z);
        private static float Sq(float v) => v * v;
    }
}
