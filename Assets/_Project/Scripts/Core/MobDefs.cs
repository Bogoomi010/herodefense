using System;

namespace TowerDefense.Core
{
    /// <summary>몹 스탯 스냅샷 (원본 waves.ts MobStats).</summary>
    public struct MobStats
    {
        public float Hp;
        public float Speed; // px/s
        public int Gold;
        public int Armor; // 물리 피해 감소 (마법은 무시)
        public bool Boss;
        public string Name;
        public uint Color; // 계열 색 0xRRGGBB
        public float Scale; // 표시 크기 배율
        public bool Splits; // 사망 시 분열 (쓰레기 상위)
        public bool Golden; // 황금 비둘기 — 보너스 몹
    }

    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>
    /// 몹 곡선·계열·보스 정의. 원본 src/data/waves.ts 1:1 이식.
    /// HP = 18 × 1.21^(r-1) × 계열 × 난이도, 방어 = ⌊r × 2.2⌋ × 계열, 속도 = 60 + 1.5r px/s × 계열.
    /// </summary>
    public static class MobDefs
    {
        public static Difficulty Difficulty = Difficulty.Normal;

        public static float DifficultyHpMul(Difficulty d) => d switch
        {
            Difficulty.Easy => 0.85f,
            Difficulty.Hard => 1.25f,
            _ => 1f,
        };

        /// <summary>난이도별 웨이브당 크립 수 배율 (docs/STAGE.md 난이도)</summary>
        public static float DifficultyCountMul(Difficulty d) => d switch
        {
            Difficulty.Easy => 0.8f,
            Difficulty.Hard => 1.3f,
            _ => 1f,
        };

        /// <summary>스테이지 크립 강도(HP) 배율. 스테이지 시작 시 세션이 넣는다.</summary>
        public static float StageHpMul = 1f;

        private static float DiffMul() => DifficultyHpMul(Difficulty) * StageHpMul;

        private static float RoundJs(double v) => (float)Math.Round(v, MidpointRounding.AwayFromZero);

        public static int MobArmor(int round) => (int)Math.Floor(round * 2.2);

        public static float BaseHp(int round)
        {
            // ♾ 무한 모드 (41R+): 지수 완화 1.21 → 1.13
            if (round > 40) return RoundJs(BaseHp(40) * Math.Pow(1.13, round - 40));
            return RoundJs(18 * Math.Pow(1.21, round - 1));
        }

        public static float BaseSpeed(int round) => 60f + round * 1.5f;

        private struct MobKind
        {
            public string Name;
            public float HpMul, ArmorMul, SpeedMul;
            public bool Splits;
        }

        private struct MobFamily
        {
            public uint Color;
            public MobKind[] Tiers;
        }

        /// <summary>몹 계열 3종 × 3단계 — 하찮은 것들의 침공 (design-origin/mob-ideas.md)</summary>
        private static readonly MobFamily[] Families =
        {
            new MobFamily
            {
                // 🪨 돌 — 고방어 저속: 마법/방깎 체크
                Color = 0x9e9e9e,
                Tiers = new[]
                {
                    new MobKind { Name = "돌멩이", HpMul = 1.0f, ArmorMul = 1.6f, SpeedMul = 0.8f },
                    new MobKind { Name = "매우 딱딱한 돌멩이", HpMul = 1.0f, ArmorMul = 1.8f, SpeedMul = 0.8f },
                    new MobKind { Name = "슈퍼 돌멩이", HpMul = 1.0f, ArmorMul = 2.0f, SpeedMul = 0.85f },
                },
            },
            new MobFamily
            {
                // 🕊 비둘기 — 저체력 고속: 감속/연타 체크
                Color = 0x81d4fa,
                Tiers = new[]
                {
                    new MobKind { Name = "비둘기", HpMul = 0.7f, ArmorMul = 0.5f, SpeedMul = 1.3f },
                    new MobKind { Name = "화난 비둘기", HpMul = 0.7f, ArmorMul = 0.5f, SpeedMul = 1.4f },
                    new MobKind { Name = "악마 비둘기", HpMul = 0.75f, ArmorMul = 0.5f, SpeedMul = 1.5f },
                },
            },
            new MobFamily
            {
                // 🗑 쓰레기 — 고체력 분열: 광역 체크
                Color = 0x8bc34a,
                Tiers = new[]
                {
                    new MobKind { Name = "쓰레기 봉투", HpMul = 1.4f, ArmorMul = 0.7f, SpeedMul = 0.85f },
                    new MobKind { Name = "큰 쓰레기", HpMul = 1.5f, ArmorMul = 0.7f, SpeedMul = 0.85f, Splits = true },
                    new MobKind { Name = "쓰레기의 악마", HpMul = 1.6f, ArmorMul = 0.7f, SpeedMul = 0.85f, Splits = true },
                },
            },
        };

        /// <summary>라운드 → 계열 로테이션(r%3) + 단계(1~13/14~26/27~)</summary>
        public static MobStats MobStatsFor(int round)
        {
            var fam = Families[round % 3];
            int tier = round <= 13 ? 0 : round <= 26 ? 1 : 2;
            var kind = fam.Tiers[tier];
            return new MobStats
            {
                Hp = Math.Max(1f, RoundJs(BaseHp(round) * kind.HpMul * DiffMul())),
                Speed = BaseSpeed(round) * kind.SpeedMul,
                Gold = 2 + round / 5,
                Armor = (int)Math.Floor(MobArmor(round) * kind.ArmorMul),
                Boss = false,
                Name = kind.Name,
                Color = fam.Color,
                Scale = 1f + tier * 0.2f,
                Splits = kind.Splits,
            };
        }

        /// <summary>✨ 황금 비둘기 — 잡으면 골드 ×10, 놓쳐도 벌 없음</summary>
        public static MobStats GoldenStats(int round) => new MobStats
        {
            Hp = Math.Max(1f, RoundJs(BaseHp(round) * 0.5f * DiffMul())),
            Speed = BaseSpeed(round) * 1.8f,
            Gold = (2 + round / 5) * 10,
            Armor = 0,
            Boss = false,
            Name = "황금 비둘기",
            Color = 0xffd700,
            Scale = 1.1f,
            Golden = true,
        };

        /// <summary>🗑 분열 자식 — 부모 최대 HP의 25%, 골드 절반</summary>
        public static MobStats SplitChildStats(MobState parent) => new MobStats
        {
            Hp = Math.Max(1f, RoundJs(parent.MaxHp * 0.25f)),
            Speed = parent.BaseSpeed,
            Gold = Math.Max(1, parent.Gold / 2),
            Armor = parent.Armor,
            Boss = false,
            Name = "쓰레기 봉투",
            Color = 0x8bc34a,
            Scale = 1f,
        };

        public struct BossDef
        {
            public string Name;
            public float HpMul, ArmorMul, Speed;
            public string Trait;
        }

        /// <summary>보스 4종 — 라운드별 개성 (v3.9 HP 흡수 반영)</summary>
        public static BossDef BossDefFor(int round) => round switch
        {
            10 => new BossDef { Name = "폭주 덤프트럭", HpMul = 20, ArmorMul = 1.0f, Speed = 78, Trait = "빠른 이동속도" },
            20 => new BossDef { Name = "장갑 수송차", HpMul = 20, ArmorMul = 2.0f, Speed = 45, Trait = "높은 방어력 — 마법/방깎 추천" },
            30 => new BossDef { Name = "스텔스 헬기", HpMul = 35, ArmorMul = 1.5f, Speed = 62, Trait = "빠르고 단단함" },
            40 => new BossDef { Name = "시티 브레이커", HpMul = 30, ArmorMul = 2.0f, Speed = 40, Trait = "최종 보스" },
            // 보스 스테이지 15웨이브 보스: 크고(2.2배) 느리고 HP가 많다. ponytail: 수치는 테스트 플레이로 조정
            15 => new BossDef { Name = "스테이지 보스", HpMul = 20, ArmorMul = 1.0f, Speed = 55, Trait = "도착하기 전에 잡아야 클리어" },
            _ => new BossDef { Name = "야근의 화신", HpMul = 24, ArmorMul = 1.5f, Speed = 50, Trait = "무한 모드" },
        };

        public static MobStats BossStats(int round)
        {
            var def = BossDefFor(round);
            return new MobStats
            {
                Hp = RoundJs(BaseHp(round) * def.HpMul * DiffMul()),
                Speed = def.Speed,
                Gold = 20 + round,
                Armor = (int)Math.Floor(MobArmor(round) * def.ArmorMul),
                Boss = true,
                Name = def.Name,
                Color = 0xff2e2e,
                Scale = 2.2f,
            };
        }

        /// <summary>물리 피해 감소율: armor / (armor + 100), 최대 85%</summary>
        public static float ArmorReduction(int armor)
        {
            if (armor <= 0) return 0f;
            return Math.Min(0.85f, armor / (armor + 100f));
        }

        public static int RoundClearBonus(int round) => 10 + round * 2;
    }
}
