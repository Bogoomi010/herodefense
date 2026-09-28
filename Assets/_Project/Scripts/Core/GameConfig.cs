namespace TowerDefense.Core
{
    /// <summary>
    /// 게임 상수. 원본 src/data/config.ts를 그대로 옮겼다.
    /// 거리·속도는 원본 px 단위를 유지하고 <see cref="PxToWorld"/>로만 월드 단위(m)로 변환한다 (docs/PORTING_PLAN.md 1.2).
    /// </summary>
    public static class GameConfig
    {
        public const int RoundMax = 40;
        public const int MobsPerRound = 20;
        public const float SpawnInterval = 0.8f; // 초
        public const float RoundTime = 25f; // 일반 라운드 시간(초)
        public const float BossTime = 45f; // 보스 제한 시간(초)
        public const float RoundBreak = 4f; // 라운드 간 휴식(초)
        public const float FirstBreak = 3f;
        public const int MobCap = 50; // 필드 몹 수용 한계
        public const int DeathStart = 10; // 데스 카운트
        public const int GachaCost = 20;
        public const int GachaCostMin = 5; // 할인 누적 시 바닥
        public const int StartGold = 150; // 2026-09-28 100 → 150: 첫 웨이브 전에 포탑 3개 (농장 구역 초반 돼지)
        public const float PhaseSec = 180f; // 낮/밤 지속 시간(초)
        public const float PhaseBuff = 1.25f;

        /// <summary>황금 비둘기 스폰 확률 (스폰당)</summary>
        public const float GoldenChance = 0.0025f;

        /// <summary>
        /// 1 Unity 단위(m) = 18.75 px. 2026-09-26 크기 기획(docs/SCALE.md)으로 맵 타일을 1.5m → 4m로 키우면서
        /// 이전 값(50 px)에서 같은 배율(8/3)로 바꿨다 — 크립이 길을 건너는 시간, 사거리/맵 비율이 그대로 유지된다.
        /// </summary>
        public const float WorldToPx = 18.75f;
        public const float PxToWorld = 1f / WorldToPx;
    }
}
