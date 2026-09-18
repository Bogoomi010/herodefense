using NUnit.Framework;
using TowerDefense.Core;

namespace TowerDefense.Tests
{
    public class EconomyTests
    {
        private static MobState Mob(int gold, bool boss = false, bool golden = false) =>
            new MobState(new MobStats { Hp = 10, Speed = 60, Gold = gold, Armor = 0, Boss = boss, Golden = golden, Name = "t" });

        [Test]
        public void StartsWithStartGold_AndLedgerRecordsIt()
        {
            var eco = new Economy();
            Assert.AreEqual(GameConfig.StartGold, eco.Gold);
            Assert.AreEqual(GameConfig.StartGold, eco.TotalFrom(GoldSource.Start));
            Assert.AreEqual(0, eco.TotalEarned);
        }

        [Test]
        public void GachaCost_AppliesDiscount_WithFloor()
        {
            var eco = new Economy();
            Assert.AreEqual(20, eco.GachaCost());
            eco.AddGachaDiscount(5); // 카드 "뽑기 보조금"
            Assert.AreEqual(15, eco.GachaCost());
            eco.AddGachaDiscount(2); // 야시장
            eco.AddGachaDiscount(2); // 스토리 2장
            Assert.AreEqual(11, eco.GachaCost());
            eco.AddGachaDiscount(50);
            Assert.AreEqual(GameConfig.GachaCostMin, eco.GachaCost());
        }

        [Test]
        public void TrySpend_RefusesWhenShort_AndKeepsBalance()
        {
            var eco = new Economy(startGold: 10);
            Assert.IsFalse(eco.TrySpend(11, GoldSource.Gacha));
            Assert.AreEqual(10, eco.Gold);
            Assert.IsTrue(eco.TrySpend(10, GoldSource.Gacha));
            Assert.AreEqual(0, eco.Gold);
            Assert.AreEqual(10, eco.TotalSpent);
            Assert.AreEqual(10, eco.TotalFrom(GoldSource.Gacha));
        }

        [Test]
        public void Earn_IgnoresNonPositive()
        {
            var eco = new Economy(startGold: 0);
            Assert.AreEqual(0, eco.Earn(0, GoldSource.Card));
            Assert.AreEqual(0, eco.Earn(-5, GoldSource.Card));
            Assert.AreEqual(0, eco.Gold);
        }

        [Test]
        public void MobGold_FollowsRoundFormula()
        {
            // 몹 2 + ⌊r/5⌋, 보스 20 + r, 황금 ×10, 클리어 10 + 2r
            Assert.AreEqual(2, MobDefs.MobStatsFor(1).Gold);
            Assert.AreEqual(3, MobDefs.MobStatsFor(5).Gold);
            Assert.AreEqual(10, MobDefs.MobStatsFor(40).Gold);
            Assert.AreEqual(30, MobDefs.BossStats(10).Gold);
            Assert.AreEqual(80, MobDefs.GoldenStats(30).Gold); // (2 + ⌊30/5⌋) × 10
            Assert.AreEqual(12, MobDefs.RoundClearBonus(1));
            Assert.AreEqual(90, MobDefs.RoundClearBonus(40));
        }

        [Test]
        public void RewardKill_NoStreakBelowFive()
        {
            var eco = new Economy(startGold: 0);
            for (int i = 0; i < 4; i++) eco.RewardKill(Mob(10), nowMs: i * 500f);
            Assert.AreEqual(4, eco.Streak);
            Assert.AreEqual(40, eco.Gold); // 배율 1.0
        }

        [Test]
        public void RewardKill_StreakBonus_CappedAtTenPercent()
        {
            var eco = new Economy(startGold: 0);
            // 20킬을 0.5초 간격으로 — 5킬째부터 +1%/킬, 10킬째 +10% 캡
            int expected = 0;
            for (int k = 1; k <= 20; k++)
            {
                float mul = k < 5 ? 1f : 1f + System.Math.Min(0.10f, k * 0.01f);
                expected += (int)System.Math.Round(100 * mul, System.MidpointRounding.AwayFromZero);
                eco.RewardKill(Mob(100), nowMs: k * 500f);
            }
            Assert.AreEqual(20, eco.Streak);
            Assert.AreEqual(20, eco.MaxStreak);
            Assert.AreEqual(expected, eco.Gold);
            Assert.AreEqual(1.10f, Events.StreakGoldMul(50), 1e-6f);
        }

        [Test]
        public void RewardKill_StreakResetsAfterWindow()
        {
            var eco = new Economy(startGold: 0);
            for (int k = 1; k <= 6; k++) eco.RewardKill(Mob(1), nowMs: k * 100f);
            Assert.AreEqual(6, eco.Streak);
            Assert.AreEqual(6, eco.StreakAt(600f));
            Assert.AreEqual(0, eco.StreakAt(600f + Events.StreakWindowMs + 1f)); // 표시용은 창이 지나면 0
            eco.RewardKill(Mob(1), nowMs: 600f + Events.StreakWindowMs + 1f);
            Assert.AreEqual(1, eco.Streak); // 실제 스트릭도 리셋
            Assert.AreEqual(6, eco.MaxStreak);
        }

        [Test]
        public void RewardKill_AppliesSkillAndMutatorMultipliers_AndRoutesSources()
        {
            var eco = new Economy(startGold: 0) { RoundGoldMul = 2f };
            Assert.AreEqual(60, eco.RewardKill(Mob(10), 0f, killGoldMul: 3f)); // 10 × 3 × 1.0 × 2
            Assert.AreEqual(60, eco.TotalFrom(GoldSource.Kill));

            eco.OnRoundStart(); // 변이 배율 초기화
            Assert.AreEqual(1f, eco.RoundGoldMul);
            Assert.AreEqual(30, eco.RewardKill(Mob(30, boss: true), 10_000f));
            Assert.AreEqual(30, eco.TotalFrom(GoldSource.BossKill));
            Assert.AreEqual(20, eco.RewardKill(Mob(20, golden: true), 20_000f));
            Assert.AreEqual(20, eco.TotalFrom(GoldSource.GoldenKill));
        }

        [Test]
        public void RewardKill_RoundsHalfAwayFromZero_LikeJsMathRound()
        {
            var eco = new Economy(startGold: 0);
            // 5킬째: 배율 1.05 → 3 × 1.05 = 3.15 → 3 ; 7 × 1.05 = 7.35 → 7 ; 10 × 1.05 = 10.5 → 11
            for (int k = 1; k <= 4; k++) eco.RewardKill(Mob(0), k * 10f);
            Assert.AreEqual(3, eco.RewardKill(Mob(3), 50f));
            Assert.AreEqual(7, eco.RewardKill(Mob(7), 60f)); // 6킬째 1.06 → 7.42
            Assert.AreEqual(11, eco.RewardKill(Mob(10), 70f)); // 7킬째 1.07 → 10.7
        }

        [Test]
        public void RewardRoundClear_UsesBonusFormula_AndSkillMultiplier()
        {
            var eco = new Economy(startGold: 0) { RoundGoldMul = 2f }; // 변이는 클리어 골드에 영향 없음
            Assert.AreEqual(12, eco.RewardRoundClear(1));
            Assert.AreEqual(38, eco.RewardRoundClear(10, roundClearGoldMul: 1.25f)); // 30 × 1.25 = 37.5 → 38
            Assert.AreEqual(50, eco.TotalFrom(GoldSource.RoundClear));
        }

        [Test]
        public void ChangedEvent_ReportsDeltaSourceBalance()
        {
            var eco = new Economy(startGold: 100);
            GoldChange last = default;
            int calls = 0;
            eco.Changed += c => { last = c; calls++; };

            eco.Earn(150, GoldSource.Card);
            Assert.AreEqual(150, last.Delta);
            Assert.AreEqual(GoldSource.Card, last.Source);
            Assert.AreEqual(250, last.Balance);

            eco.TrySpend(20, GoldSource.Gacha);
            Assert.AreEqual(-20, last.Delta);
            Assert.AreEqual(230, last.Balance);
            Assert.AreEqual(2, calls);

            Assert.IsFalse(eco.TrySpend(999, GoldSource.Gacha));
            Assert.AreEqual(2, calls); // 거부된 지출은 이벤트 없음
        }

        [Test]
        public void LedgerTotals_SumUp()
        {
            var eco = new Economy(startGold: 100);
            eco.RewardRoundClear(1); // 12
            eco.RewardFishbread(); // 60
            eco.RewardDayStart(15); // 15
            eco.RewardDayStart(0);
            eco.TrySpend(20, GoldSource.Gacha);
            Assert.AreEqual(87, eco.TotalEarned);
            Assert.AreEqual(20, eco.TotalSpent);
            Assert.AreEqual(100 + 87 - 20, eco.Gold);
            Assert.AreEqual(60, eco.TotalFrom(GoldSource.CityEvent));
            Assert.AreEqual(15, eco.TotalFrom(GoldSource.DayStart));
        }
    }
}
