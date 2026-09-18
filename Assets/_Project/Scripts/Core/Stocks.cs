using System;
using System.Collections.Generic;

namespace TowerDefense.Core
{
    /// <summary>종목 정의 (원본 src/data/stocks.ts)</summary>
    public sealed class StockDef
    {
        public string Id;
        public string Name;
        public string Icon;
        public string Desc;
        public float Vol; // 라운드당 변동폭 (±균등)
        public float JumpChance; // 급등/급락 확률
        public Events.CityEventId? EventBoost; // 📰 속보 연동 급등
        public Events.MutatorId? MutatorBoost; // 🎲 변이 연동 급등
    }

    public sealed class StockState
    {
        public int Price = StockMarket.StartPrice;
        public readonly List<int> History = new List<int> { StockMarket.StartPrice };
        public int Shares;
        public float AvgCost; // 평단
    }

    public readonly struct StockAlert
    {
        public readonly StockDef Def;
        public readonly float Pct;
        public StockAlert(StockDef def, float pct) { Def = def; Pct = pct; }
    }

    /// <summary>
    /// 📈 미니 주식 — 도시 증권거래소. 두 번째 골드 사용처 ("지금 뽑을까, 묻어둘까").
    /// 드리프트 +0.5%/R라 존버 수익은 미미, 진짜 수익은 저점 매수·급등 매도 타이밍. 판 종료 시 잔여 주식 소멸.
    /// </summary>
    public sealed class StockMarket
    {
        public const int StartPrice = 100;
        public const float Drift = 0.005f;
        public const float Jump = 0.3f;
        public const float EventBoost = 0.18f;
        public const int MinPrice = 20;
        public const int MaxPrice = 400;
        public const int HistoryLength = 9;
        public const int SellAll = -999;

        public static readonly StockDef[] Defs =
        {
            new StockDef { Id = "fish", Name = "붕어빵F&B", Icon = "🐟", Desc = "안정주 — 겨울마다 배당 같은 존재감", Vol = 0.08f, JumpChance = 0.06f, EventBoost = Events.CityEventId.Fishbread },
            new StockDef { Id = "rider", Name = "배달로켓", Icon = "🛵", Desc = "성장주 — 러시 아워에 강하다", Vol = 0.12f, JumpChance = 0.10f, MutatorBoost = Events.MutatorId.Rush },
            new StockDef { Id = "redev", Name = "재개발홀딩스", Icon = "🏗", Desc = "테마주 — 하이리스크 하이리턴", Vol = 0.18f, JumpChance = 0.14f },
        };

        private readonly Economy _economy;
        private readonly Dictionary<string, StockState> _states = new Dictionary<string, StockState>();

        /// <summary>이번 클리어에 급등을 걸 종목 (속보·변이 연동). Tick 후 비워진다.</summary>
        public string PendingBoost { get; set; }

        public StockMarket(Economy economy)
        {
            _economy = economy;
            foreach (var d in Defs) _states[d.Id] = new StockState();
        }

        public StockState Get(string id) => _states.TryGetValue(id, out var s) ? s : null;
        public IReadOnlyDictionary<string, StockState> States => _states;

        /// <summary>보유 주식 평가액</summary>
        public int PortfolioValue()
        {
            int v = 0;
            foreach (var d in Defs) v += _states[d.Id].Price * _states[d.Id].Shares;
            return v;
        }

        /// <summary>속보 이벤트 → 연동 종목 급등 예약</summary>
        public void NotifyCityEvent(Events.CityEventId id)
        {
            foreach (var d in Defs) if (d.EventBoost == id) PendingBoost = d.Id;
        }

        /// <summary>변이 라운드 → 연동 종목 급등 예약</summary>
        public void NotifyMutator(Events.MutatorId id)
        {
            foreach (var d in Defs) if (d.MutatorBoost == id) PendingBoost = d.Id;
        }

        /// <summary>
        /// 라운드 클리어마다 시세 갱신. 반환: ±20% 이상 변동한 종목 (알림용).
        /// mul = 1 + 드리프트 + 균등(±vol) [+ 급등/급락 ±30%] [+ 연동 +18%] [소프트 평균회귀 ∓5%]
        /// </summary>
        public List<StockAlert> Tick(Random rng)
        {
            var alerts = new List<StockAlert>();
            string boost = PendingBoost;
            PendingBoost = null;
            foreach (var def in Defs)
            {
                var st = _states[def.Id];
                float mul = 1f + Drift + ((float)rng.NextDouble() * 2f - 1f) * def.Vol;
                if (rng.NextDouble() < def.JumpChance) mul += rng.NextDouble() < 0.5 ? Jump : -Jump;
                if (def.Id == boost) mul += EventBoost;
                if (st.Price > MaxPrice * 0.8f) mul -= 0.05f;
                if (st.Price < MinPrice * 2) mul += 0.05f;
                int next = Math.Max(MinPrice, Math.Min(MaxPrice, (int)Math.Round(st.Price * mul, MidpointRounding.AwayFromZero)));
                float pct = (next - st.Price) / (float)st.Price;
                st.Price = next;
                st.History.Add(next);
                if (st.History.Count > HistoryLength) st.History.RemoveAt(0);
                if (Math.Abs(pct) >= 0.2f) alerts.Add(new StockAlert(def, pct));
            }
            return alerts;
        }

        /// <summary>매수. 골드 부족·잘못된 수량이면 false.</summary>
        public bool Buy(string id, int n)
        {
            var st = Get(id);
            if (st == null || n <= 0) return false;
            int cost = st.Price * n;
            if (!_economy.TrySpend(cost, GoldSource.StockBuy)) return false;
            st.AvgCost = (st.AvgCost * st.Shares + cost) / (st.Shares + n);
            st.Shares += n;
            return true;
        }

        /// <summary>매도 (n = SellAll이면 전량). 반환: 실현 손익 (골드). 판 수량이 없으면 0.</summary>
        public int Sell(string id, int n, out int sold)
        {
            sold = 0;
            var st = Get(id);
            if (st == null) return 0;
            sold = n == SellAll ? st.Shares : Math.Min(st.Shares, Math.Max(0, n));
            if (sold <= 0) return 0;
            int gain = st.Price * sold;
            int profit = (int)Math.Round((st.Price - st.AvgCost) * sold, MidpointRounding.AwayFromZero);
            _economy.Earn(gain, GoldSource.StockSell);
            st.Shares -= sold;
            if (st.Shares == 0) st.AvgCost = 0f;
            return profit;
        }

        /// <summary>텍스트 스파크라인 — 최근 가격을 ▁▂▃▄▅▆▇로</summary>
        public static string Sparkline(IReadOnlyList<int> history)
        {
            const string bars = "▁▂▃▄▅▆▇";
            int min = int.MaxValue, max = int.MinValue;
            foreach (var p in history) { if (p < min) min = p; if (p > max) max = p; }
            int span = Math.Max(1, max - min);
            var sb = new System.Text.StringBuilder(history.Count);
            foreach (var p in history) sb.Append(bars[Math.Min(6, (int)Math.Floor((p - min) / (float)span * 6.99f))]);
            return sb.ToString();
        }
    }
}
