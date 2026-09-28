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
        public string Id;   // 크립 종류 id (소개 카드·스테이지 풀)
        public string Model; // 모델 프리팹 이름 (Resources/Creeps/<Model>). 비면 세션 기본 모델
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
            public string Id, Desc;
            public string Name;
            public string Model; // 비면 양 모델
            public float Scale;  // 0이면 단계로 정함 (1.0 / 1.25 / 1.5)
            public float HpMul, ArmorMul, SpeedMul;
            public bool Splits;
        }

        private struct MobFamily
        {
            public uint Color;
            public MobKind[] Tiers;
        }

        /// <summary>
        /// 크립 계열. 앞 4개는 농장 구역(docs/STORY_ZONES.md), 뒤 3개는 도심 구역 — 하찮은 것들의 침공 (design-origin/mob-ideas.md).
        /// 역할: 기본 / 빠름 / 단단함(방어력) / 질김·분열.
        /// </summary>
        private static readonly MobFamily[] Families =
        {
            // 농장 — 몸통 머티리얼("Wool"/"Body")을 계열 색으로 칠한다
            new MobFamily { Color = 0xf2f2f2, Tiers = new[] { new MobKind { Id = "sheep", Name = "양", Model = "Sheep", Scale = 1f, Desc = "평범한 양. 이상하게 눈빛이 사납다", HpMul = 1.0f, ArmorMul = 0.8f, SpeedMul = 1.0f } } },
            new MobFamily { Color = 0xfafafa, Tiers = new[] { new MobKind { Id = "chicken", Name = "닭", Model = "Chicken", Scale = 0.8f, Desc = "약하지만 빠르다. 냉기 포탑으로 늦추자", HpMul = 0.6f, ArmorMul = 0.4f, SpeedMul = 1.45f } } },
            new MobFamily { Color = 0xf5f1ea, Tiers = new[] { new MobKind { Id = "cow", Name = "소", Model = "Cow", Scale = 1.35f, Desc = "단단하다. 방어력이 높아 약한 공격은 잘 안 먹힌다", HpMul = 1.3f, ArmorMul = 1.7f, SpeedMul = 0.75f } } },
            new MobFamily { Color = 0xf2a7b3, Tiers = new[] { new MobKind { Id = "pig", Name = "돼지", Model = "Pig", Scale = 1.1f, Desc = "체력이 많다. 쓰러지면 새끼 돼지 둘로 갈라진다", HpMul = 1.5f, ArmorMul = 0.7f, SpeedMul = 0.85f, Splits = true } } },

            new MobFamily
            {
                // 🪨 돌 — 고방어 저속: 마법/방깎 체크
                Color = 0x9e9e9e,
                Tiers = new[]
                {
                    new MobKind { Id = "rock", Name = "돌멩이", Desc = "단단하다. 방어력이 높아 약한 공격은 잘 안 먹힌다", HpMul = 1.0f, ArmorMul = 1.6f, SpeedMul = 0.8f },
                    new MobKind { Id = "rock_hard", Name = "매우 딱딱한 돌멩이", Desc = "더 단단한 돌멩이", HpMul = 1.0f, ArmorMul = 1.8f, SpeedMul = 0.8f },
                    new MobKind { Id = "rock_super", Name = "슈퍼 돌멩이", Desc = "가장 단단한 돌멩이", HpMul = 1.0f, ArmorMul = 2.0f, SpeedMul = 0.85f },
                },
            },
            new MobFamily
            {
                // 🕊 비둘기 — 저체력 고속: 감속/연타 체크
                Color = 0x81d4fa,
                Tiers = new[]
                {
                    new MobKind { Id = "pigeon", Name = "비둘기", Desc = "약하지만 빠르다", HpMul = 0.7f, ArmorMul = 0.5f, SpeedMul = 1.3f },
                    new MobKind { Id = "pigeon_angry", Name = "화난 비둘기", Desc = "더 빠르다. 냉기 포탑으로 늦추자", HpMul = 0.7f, ArmorMul = 0.5f, SpeedMul = 1.4f },
                    new MobKind { Id = "pigeon_demon", Name = "악마 비둘기", Desc = "가장 빠른 비둘기", HpMul = 0.75f, ArmorMul = 0.5f, SpeedMul = 1.5f },
                },
            },
            new MobFamily
            {
                // 🗑 쓰레기 — 고체력 분열: 광역 체크
                Color = 0x8bc34a,
                Tiers = new[]
                {
                    new MobKind { Id = "trash", Name = "쓰레기 봉투", Desc = "체력이 많고 느리다. 뭉쳐 다니니 투석기로", HpMul = 1.4f, ArmorMul = 0.7f, SpeedMul = 0.85f },
                    new MobKind { Id = "trash_big", Name = "큰 쓰레기", Desc = "쓰러지면 작은 봉투 둘로 갈라진다", HpMul = 1.5f, ArmorMul = 0.7f, SpeedMul = 0.85f, Splits = true },
                    new MobKind { Id = "trash_demon", Name = "쓰레기의 악마", Desc = "가장 질긴 쓰레기. 역시 갈라진다", HpMul = 1.6f, ArmorMul = 0.7f, SpeedMul = 0.85f, Splits = true },
                },
            },
        };

        private const int CityStart = 4; // Families에서 도심 계열이 시작하는 자리

        /// <summary>라운드 → 도심 계열 로테이션(r%3) + 단계(1~13/14~26/27~). 스테이지 크립 풀이 없을 때(원본 규칙).</summary>
        public static MobStats MobStatsFor(int round)
        {
            var fam = Families[CityStart + round % 3];
            int tier = round <= 13 ? 0 : round <= 26 ? 1 : 2;
            return Build(fam, tier, round);
        }

        /// <summary>
        /// 스테이지 크립 풀 (docs/STAGE.md 크립 구성): 한 웨이브 안에서 풀의 종류가 섞여 나온다.
        /// 웨이브 r의 i번째(0부터) 크립 = pool[(r − 1 + i) % 개수] — 풀을 차례로 돌되 웨이브마다 첫 종류가 바뀐다.
        /// 풀이 비었거나 모르는 id면 원본 규칙(MobStatsFor).
        /// </summary>
        public static MobStats MobStatsFor(int round, System.Collections.Generic.IReadOnlyList<string> pool, int spawnIndex = 0)
        {
            if (pool == null || pool.Count == 0) return MobStatsFor(round);
            var id = pool[(Math.Max(1, round) - 1 + Math.Max(0, spawnIndex)) % pool.Count];
            for (int f = 0; f < Families.Length; f++)
                for (int t = 0; t < Families[f].Tiers.Length; t++)
                    if (Families[f].Tiers[t].Id == id) return Build(Families[f], t, round);
            return MobStatsFor(round);
        }

        /// <summary>크립 종류 id → (이름, 소개 문구). 소개 카드용. 모르면 null.</summary>
        public static (string name, string desc)? Describe(string id)
        {
            if (BossById(id) is BossDef b) return (b.Name, b.Trait);
            foreach (var fam in Families)
                foreach (var k in fam.Tiers)
                    if (k.Id == id) return (k.Name, k.Desc);
            return null;
        }

        private static MobStats Build(MobFamily fam, int tier, int round)
        {
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
                Scale = kind.Scale > 0f ? kind.Scale : 1f + tier * 0.25f, // 사람 크기 1.0 ~ 1.5배 (docs/SCALE.md)
                Splits = kind.Splits,
                Id = kind.Id,
                Model = kind.Model,
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

        /// <summary>분열 자식 — 부모 최대 HP의 25%, 골드 절반. 돼지 → 새끼 돼지, 쓰레기 → 쓰레기 봉투</summary>
        public static MobStats SplitChildStats(MobState parent)
        {
            bool pig = parent.Id == "pig";
            return new MobStats
            {
                Hp = Math.Max(1f, RoundJs(parent.MaxHp * 0.25f)),
                Speed = parent.BaseSpeed * (pig ? 1.15f : 1f),
                Gold = Math.Max(1, parent.Gold / 2),
                Armor = parent.Armor,
                Boss = false,
                Name = pig ? "새끼 돼지" : "쓰레기 봉투",
                Color = pig ? 0xf2a7b3u : 0x8bc34au,
                Scale = pig ? 0.6f : 1f,
                Id = pig ? "piglet" : "trash",
                Model = pig ? "Pig" : null,
            };
        }

        public struct BossDef
        {
            public string Id, Model;
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
            15 => new BossDef { Id = "boss", Name = "스테이지 보스", HpMul = 20, ArmorMul = 1.0f, Speed = 55, Trait = "크고 느리지만 체력이 아주 많다. 도착하기 전에 잡아야 클리어" },
            _ => new BossDef { Name = "야근의 화신", HpMul = 24, ArmorMul = 1.5f, Speed = 50, Trait = "무한 모드" },
        };

        /// <summary>구역 보스 (docs/STORY_ZONES.md). 스테이지 데이터의 boss id로 고른다. 모르면 null.</summary>
        public static BossDef? BossById(string id) => id switch
        {
            "boss" => BossDefFor(15),
            "bull" => new BossDef { Id = "bull", Model = "Bull", Name = "성난 황소", HpMul = 20, ArmorMul = 1.2f, Speed = 55, Trait = "농장의 우두머리. 크고 질기다. 도착하기 전에 잡아야 클리어" },
            _ => null,
        };

        public static MobStats BossStats(int round, string bossId = null)
        {
            var def = BossById(bossId) ?? BossDefFor(round);
            return new MobStats
            {
                Hp = RoundJs(BaseHp(round) * def.HpMul * DiffMul()),
                Speed = def.Speed,
                Gold = 20 + round,
                Armor = (int)Math.Floor(MobArmor(round) * def.ArmorMul),
                Boss = true,
                Name = def.Name,
                Color = def.Model != null ? 0x7a3322u : 0xff2e2eu,
                Scale = 2.2f, // 약 4m — 크립 최대보다 크다
                Id = def.Id ?? "boss",
                Model = def.Model,
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
