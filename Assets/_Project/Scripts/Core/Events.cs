using System;

namespace TowerDefense.Core
{
    /// <summary>
    /// 도파민 시스템 상수 (원본 src/data/events.ts). 골드·뽑기에 닿는 수치는 전부 여기 두고 시뮬레이터가 같은 값으로 검증한다.
    /// 원칙: ① 전부 인게임 재화만 ② 이벤트는 보너스만, 페널티 없음.
    /// </summary>
    public static class Events
    {
        // ---------- 🔥 킬 스트릭 ----------
        public const float StreakWindowMs = 3000f; // 이 간격 안에 연속 처치 시 스트릭 유지
        public const int StreakMin = 5; // 표시·보너스 시작 스트릭
        public const float StreakGoldPer = 0.01f; // 1킬당 골드 보너스 +1%
        public const float StreakGoldCap = 0.10f; // 최대 +10%

        /// <summary>스트릭 골드 배율</summary>
        public static float StreakGoldMul(int streak)
        {
            if (streak < StreakMin) return 1f;
            return 1f + Math.Min(StreakGoldCap, streak * StreakGoldPer);
        }

        /// <summary>시뮬 기대값: 필드 킬의 평균 스트릭 가동 배율</summary>
        public const float StreakSimMul = 1.04f;

        // ---------- 🎰 뽑기 잭팟 & 천장 ----------
        public const float JackpotChance = 0.025f; // 유료 뽑기당 "서비스!" 확률
        public const int JackpotRollsMin = 1; // 무료 연속 뽑기 수 (균등)
        public const int JackpotRollsMax = 2;
        public const int PityLimit = 60; // 이 횟수 동안 전설이 안 나오면 다음 뽑기 전설 확정
        public const int PityMinRound = 21; // 전설이 뽑기 풀에 있는 구간부터 카운트/발동
        public const float NearMissChance = 0.2f; // 연출만

        // ---------- 📰 속보 이벤트 (라운드 사이 · 보너스만) ----------
        public const float EventChance = 0.08f;
        public const int EventMinRound = 5;

        public enum CityEventId { Rain, Fishbread, GoldenFlock, NightMarket }

        public readonly struct CityEventDef
        {
            public readonly CityEventId Id;
            public readonly string Name;
            public readonly string Desc;
            public CityEventDef(CityEventId id, string name, string desc) { Id = id; Name = name; Desc = desc; }
        }

        public static readonly CityEventDef[] CityEvents =
        {
            new CityEventDef(CityEventId.Rain, "게릴라 소나기", "다음 라운드 몹 이동속도 -30% — 물 들어올 때 노 젓기"),
            new CityEventDef(CityEventId.Fishbread, "붕어빵 트럭 출몰", "사장님이 수비대를 응원한다 — 골드 +60"),
            new CityEventDef(CityEventId.GoldenFlock, "황금 비둘기 떼", "✨ 황금 비둘기 2마리가 도시를 가로지른다"),
            new CityEventDef(CityEventId.NightMarket, "야시장 개장", "이번 판 뽑기 비용 -2G"),
        };

        public const float EventRainSlow = 0.3f;
        public const int EventFishbreadGold = 60;
        public const int EventGoldenCount = 2;
        public const int EventMarketDiscount = 2;
        /// <summary>시뮬 기대값: 이벤트 1회당 평균 골드 등가</summary>
        public const int EventSimGold = 65;

        // ---------- 🎲 변이 라운드 (비보스 라운드 시작 시) ----------
        public const float MutatorChance = 0.05f;

        public enum MutatorId { Golden, Rush }

        public readonly struct MutatorDef
        {
            public readonly MutatorId Id;
            public readonly string Name;
            public readonly string Desc;
            public readonly float GoldMul; // 이 라운드 몹 골드 배율
            public readonly float SpeedMul; // 이 라운드 몹 이동속도 배율
            public MutatorDef(MutatorId id, string name, string desc, float goldMul, float speedMul)
            { Id = id; Name = name; Desc = desc; GoldMul = goldMul; SpeedMul = speedMul; }
        }

        public static readonly MutatorDef[] Mutators =
        {
            new MutatorDef(MutatorId.Golden, "💰 황금 라운드", "이 라운드 몹 골드 ×2!", 2f, 1f),
            new MutatorDef(MutatorId.Rush, "🚨 러시 아워", "몹이 빨라진다 — 대신 골드 ×2!", 2f, 1.5f),
        };

        /// <summary>시뮬 기대값: 변이 라운드의 평균 몹 골드 배율</summary>
        public const float MutatorSimGoldMul = 1f + MutatorChance * 1f;

        // ---------- 🎟 다음 판 부스터 (패배 위로 — 판 간 유지) ----------
        public const float HiddenBase = 0.06f; // 흔함 뽑기의 히든 기본 확률
        public const float BoosterPerLoss = 0.01f;
        public const float BoosterCap = 0.03f;
    }
}
