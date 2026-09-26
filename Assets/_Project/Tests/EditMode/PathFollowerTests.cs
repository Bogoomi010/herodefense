using NUnit.Framework;
using TowerDefense.Game;
using UnityEngine;

namespace TowerDefense.Tests
{
    /// <summary>크립 무리 이동·복귀에 쓰는 경로 기하 (docs/CREEP_MOVEMENT.md).</summary>
    public class PathFollowerTests
    {
        // (0,0) → (10,0) → (10,10): 동쪽으로 가다 북쪽으로 꺾는 길
        private static PathFollower L() => new PathFollower(new[] { Vector3.zero, new Vector3(10, 0, 0), new Vector3(10, 0, 10) });

        [Test]
        public void Closest_GivesProgressAndSignedLateral()
        {
            var (d, lat) = L().Closest(new Vector3(4, 0, -1.5f)); // 동쪽으로 갈 때 오른쪽 = 남쪽(-z)
            Assert.AreEqual(4f, d, 1e-4f);
            Assert.AreEqual(1.5f, lat, 1e-4f);

            (d, lat) = L().Closest(new Vector3(8, 0, 5)); // 북쪽 구간의 왼쪽(서쪽)
            Assert.AreEqual(15f, d, 1e-4f);
            Assert.AreEqual(-2f, lat, 1e-4f);
        }

        [Test]
        public void Closest_Window_DoesNotJumpToNeighbouringSegment()
        {
            // 점 (9,0,6)은 북쪽 구간(진행 16)에 더 가깝지만, 진행 8~11 창 안에서는 동쪽 구간 끝 쪽으로 잡힌다
            var (d, _) = L().Closest(new Vector3(9, 0, 6), 8f, 11f);
            Assert.LessOrEqual(d, 11f + 1e-4f);
            Assert.GreaterOrEqual(d, 8f - 1e-4f);
        }

        [Test]
        public void PosAtWithLateral_RoundTripsThroughClosest()
        {
            var path = L();
            var p = path.PosAt(3f, 0.5f);
            var (d, lat) = path.Closest(p);
            Assert.AreEqual(3f, d, 1e-3f);
            Assert.AreEqual(0.5f, lat, 1e-3f);
        }
    }
}
