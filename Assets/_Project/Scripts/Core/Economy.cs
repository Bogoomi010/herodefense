using System;

namespace TowerDefense.Core
{
    /// <summary>골드 증감 출처. 원장(ledger) 집계와 HUD 표시에 쓴다.</summary>
    public enum GoldSource
    {
        Start,
        Kill,
        BossKill,
        GoldenKill,
        RoundClear,
        Card,
        CityEvent,
        Story,
        DayStart,
        StockSell,
        Gacha, // 지출
        StockBuy, // 지출
        TowerBuild, // 지출: 포탑 설치
        TowerUpgrade, // 지출: 포탑 업그레이드
        Debug,
    }

    public readonly struct GoldChange
    {
        public readonly int Delta; // + 수입 / − 지출
        public readonly GoldSource Source;
        public readonly int Balance; // 변경 후 잔액
        public GoldChange(int delta, GoldSource source, int balance) { Delta = delta; Source = source; Balance = balance; }
    }

    /// <summary>
    /// 골드 경제 (docs/GOLD_SYSTEM.md). 골드에 닿는 모든 배율은 이 클래스를 지난다.
    /// - 정수 골드, 반올림은 원본 JS Math.round와 같은 AwayFromZero
    /// - 부족하면 거부(TrySpend=false), 빚 없음
    /// - 출처별 누적 원장 + Changed 이벤트
    /// </summary>
    public sealed class Economy
    {
        public int Gold { get; private set; }
        public Mods Mods { get; }

        /// <summary>🎲 변이 라운드 골드 배율 — 처치 골드에만 곱한다. 라운드 시작마다 1로.</summary>
        public float RoundGoldMul { get; set; } = 1f;

        /// <summary>🔥 현재 스트릭 (마지막 처치 시각 기준 원시 값). 표시용은 <see cref="StreakAt"/>.</summary>
        public int Streak { get; private set; }
        public int MaxStreak { get; private set; }
        private float _lastKillAt = float.NegativeInfinity;

        public int TotalEarned { get; private set; }
        public int TotalSpent { get; private set; }
        private readonly int[] _totals = new int[Enum.GetValues(typeof(GoldSource)).Length];

        /// <summary>골드가 바뀔 때마다 (증감, 출처, 잔액)</summary>
        public event Action<GoldChange> Changed;

        public Economy(Mods mods = null, int startGold = GameConfig.StartGold)
        {
            Mods = mods ?? new Mods();
            Gold = Math.Max(0, startGold);
            _totals[(int)GoldSource.Start] = Gold;
        }

        // ---------- 조회 ----------

        /// <summary>뽑기 비용: max(5, 20 − 누적 할인)</summary>
        public int GachaCost() => Math.Max(GameConfig.GachaCostMin, GameConfig.GachaCost - Mods.GachaDiscount);

        public bool CanAfford(int cost) => cost <= Gold;

        /// <summary>표시용 스트릭 — 마지막 처치로부터 3초가 지나면 0</summary>
        public int StreakAt(float nowMs) => nowMs - _lastKillAt <= Events.StreakWindowMs ? Streak : 0;

        /// <summary>출처별 누적액 (지출 출처는 양수로 누적)</summary>
        public int TotalFrom(GoldSource source) => _totals[(int)source];

        // ---------- 증감 ----------

        /// <summary>골드 획득. 0 이하이면 아무 일도 하지 않는다. 실제 증가량 반환.</summary>
        public int Earn(int amount, GoldSource source)
        {
            if (amount <= 0) return 0;
            Gold += amount;
            TotalEarned += amount;
            _totals[(int)source] += amount;
            Changed?.Invoke(new GoldChange(amount, source, Gold));
            return amount;
        }

        /// <summary>골드 지출. 잔액이 부족하면 false, 잔액 불변.</summary>
        public bool TrySpend(int amount, GoldSource source)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Gold) return false;
            Gold -= amount;
            TotalSpent += amount;
            _totals[(int)source] += amount;
            Changed?.Invoke(new GoldChange(-amount, source, Gold));
            return true;
        }

        // ---------- 게임 규칙 ----------

        /// <summary>
        /// 몹 처치 보상: round(몹골드 × 스킬 killGoldMul × 스트릭 배율 × 변이 배율).
        /// 스트릭은 직전 처치로부터 3초 이내면 +1, 아니면 1로 리셋.
        /// </summary>
        public int RewardKill(MobState mob, float nowMs, float killGoldMul = 1f)
        {
            Streak = nowMs - _lastKillAt <= Events.StreakWindowMs ? Streak + 1 : 1;
            _lastKillAt = nowMs;
            if (Streak > MaxStreak) MaxStreak = Streak;

            int gold = RoundJs(mob.Gold * killGoldMul * Events.StreakGoldMul(Streak) * RoundGoldMul);
            var source = mob.Golden ? GoldSource.GoldenKill : mob.IsBoss ? GoldSource.BossKill : GoldSource.Kill;
            return Earn(gold, source);
        }

        /// <summary>라운드 클리어 보상: round((10 + 2r) × 필드 유닛 roundClearGoldMul 곱). 변이 배율은 적용하지 않는다.</summary>
        public int RewardRoundClear(int round, float roundClearGoldMul = 1f) =>
            Earn(RoundJs(MobDefs.RoundClearBonus(round) * roundClearGoldMul), GoldSource.RoundClear);

        /// <summary>낮 시작 골드 (tf5 패시브 합)</summary>
        public int RewardDayStart(int bonus) => bonus > 0 ? Earn(bonus, GoldSource.DayStart) : 0;

        /// <summary>속보 "붕어빵 트럭"</summary>
        public int RewardFishbread() => Earn(Events.EventFishbreadGold, GoldSource.CityEvent);

        /// <summary>뽑기 할인 누적 (카드 +5 / 야시장 +2 / 스토리 2장 +2). 바닥은 GachaCost가 보장.</summary>
        public void AddGachaDiscount(int amount)
        {
            if (amount > 0) Mods.GachaDiscount += amount;
        }

        /// <summary>새 라운드 시작 — 변이 배율 초기화 (스트릭은 시간 창으로 자연 소멸)</summary>
        public void OnRoundStart() => RoundGoldMul = 1f;

        private static int RoundJs(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }
}
