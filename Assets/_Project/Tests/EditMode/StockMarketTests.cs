using NUnit.Framework;
using TowerDefense.Core;

namespace TowerDefense.Tests
{
    public class StockMarketTests
    {
        [Test]
        public void StartsAt100_NoShares()
        {
            var market = new StockMarket(new Economy());
            foreach (var d in StockMarket.Defs)
            {
                var st = market.Get(d.Id);
                Assert.AreEqual(StockMarket.StartPrice, st.Price);
                Assert.AreEqual(0, st.Shares);
                Assert.AreEqual(1, st.History.Count);
            }
            Assert.AreEqual(0, market.PortfolioValue());
        }

        [Test]
        public void Buy_SpendsGold_AndTracksAverageCost()
        {
            var eco = new Economy(startGold: 500);
            var market = new StockMarket(eco);
            Assert.IsTrue(market.Buy("fish", 3));
            Assert.AreEqual(200, eco.Gold);
            Assert.AreEqual(3, market.Get("fish").Shares);
            Assert.AreEqual(100f, market.Get("fish").AvgCost, 1e-4f);
            Assert.AreEqual(300, eco.TotalFrom(GoldSource.StockBuy));

            Assert.IsFalse(market.Buy("fish", 3)); // 300 필요, 200 보유
            Assert.AreEqual(200, eco.Gold);
            Assert.IsFalse(market.Buy("nope", 1));
            Assert.IsFalse(market.Buy("fish", 0));
        }

        [Test]
        public void Sell_EarnsGold_ReportsProfit_AndSellAll()
        {
            var eco = new Economy(startGold: 300);
            var market = new StockMarket(eco);
            market.Buy("rider", 3); // 평단 100, 잔액 0
            market.Get("rider").Price = 150; // 시세 상승 가정

            int profit = market.Sell("rider", 1, out int sold);
            Assert.AreEqual(1, sold);
            Assert.AreEqual(50, profit);
            Assert.AreEqual(150, eco.Gold);
            Assert.AreEqual(2, market.Get("rider").Shares);

            profit = market.Sell("rider", StockMarket.SellAll, out sold);
            Assert.AreEqual(2, sold);
            Assert.AreEqual(100, profit);
            Assert.AreEqual(450, eco.Gold);
            Assert.AreEqual(0, market.Get("rider").Shares);
            Assert.AreEqual(0f, market.Get("rider").AvgCost);
            Assert.AreEqual(450, eco.TotalFrom(GoldSource.StockSell));

            Assert.AreEqual(0, market.Sell("rider", 5, out sold));
            Assert.AreEqual(0, sold);
        }

        [Test]
        public void Tick_StaysWithinBounds_AndKeepsHistoryLength()
        {
            var market = new StockMarket(new Economy());
            var rng = new System.Random(42);
            for (int r = 0; r < 200; r++)
            {
                market.Tick(rng);
                foreach (var d in StockMarket.Defs)
                {
                    var st = market.Get(d.Id);
                    Assert.GreaterOrEqual(st.Price, StockMarket.MinPrice);
                    Assert.LessOrEqual(st.Price, StockMarket.MaxPrice);
                    Assert.LessOrEqual(st.History.Count, StockMarket.HistoryLength);
                }
            }
        }

        [Test]
        public void Tick_IsDeterministicForSeed()
        {
            var a = new StockMarket(new Economy());
            var b = new StockMarket(new Economy());
            var ra = new System.Random(7);
            var rb = new System.Random(7);
            for (int r = 0; r < 20; r++) { a.Tick(ra); b.Tick(rb); }
            foreach (var d in StockMarket.Defs) Assert.AreEqual(a.Get(d.Id).Price, b.Get(d.Id).Price);
        }

        [Test]
        public void PendingBoost_RaisesLinkedStock_OnceOnly()
        {
            // 붕어빵 속보 → fish 급등 예약. 같은 시드로 예약 유무만 다르게 비교
            var boosted = new StockMarket(new Economy());
            var plain = new StockMarket(new Economy());
            boosted.NotifyCityEvent(Events.CityEventId.Fishbread);
            Assert.AreEqual("fish", boosted.PendingBoost);
            boosted.Tick(new System.Random(3));
            plain.Tick(new System.Random(3));
            Assert.Greater(boosted.Get("fish").Price, plain.Get("fish").Price);
            Assert.AreEqual(boosted.Get("redev").Price, plain.Get("redev").Price);
            Assert.IsNull(boosted.PendingBoost);

            var rush = new StockMarket(new Economy());
            rush.NotifyMutator(Events.MutatorId.Rush);
            Assert.AreEqual("rider", rush.PendingBoost);
        }

        [Test]
        public void Sparkline_MapsMinToLowBar_MaxToHighBar()
        {
            string s = StockMarket.Sparkline(new[] { 100, 150, 200 });
            Assert.AreEqual(3, s.Length);
            Assert.AreEqual('▁', s[0]);
            Assert.AreEqual('▇', s[2]);
            Assert.AreEqual("▁▁", StockMarket.Sparkline(new[] { 50, 50 }));
        }
    }
}
