using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense.Core;
using TowerDefense.Game;

namespace TowerDefense.Tests
{
    /// <summary>포탑 종류: 투석기 표적, 스테이지별 해금 (docs/TOWER_TYPES.md).</summary>
    public class TowerTypesTests
    {
        [Test]
        public void Densest_PicksMiddleOfCluster()
        {
            var pts = new List<(float, float)> { (0, 0), (10, 0), (10.5f, 0), (11, 0.5f), (30, 0) };
            Assert.AreEqual(1, Targeting.DensestIndex(pts, 3f), "10 근처 3마리 무리 (같으면 앞쪽)");
            Assert.AreEqual(-1, Targeting.DensestIndex(new List<(float, float)>(), 3f));
        }

        [Test]
        public void Unlock_OnePerStage_CatapultFrostBallista()
        {
            var specs = TowerSpec.Defaults();
            TowerSpec Of(TowerKind k) => specs.Find(s => s.kind == k);
            Assert.IsTrue(Of(TowerKind.Basic).IsUnlocked(1));
            Assert.IsTrue(Of(TowerKind.Catapult).IsUnlocked(1));
            Assert.IsFalse(Of(TowerKind.Frost).IsUnlocked(1));
            Assert.IsTrue(Of(TowerKind.Frost).IsUnlocked(2));
            Assert.IsFalse(Of(TowerKind.Ballista).IsUnlocked(2));
            Assert.IsTrue(Of(TowerKind.Ballista).IsUnlocked(3));
        }

        [Test]
        public void NewTowers_HaveNoUpgrades()
        {
            foreach (var s in TowerSpec.Defaults())
                Assert.AreEqual(s.kind == TowerKind.Basic, s.MaxLevel > 0, s.displayName);
        }
    }
}
