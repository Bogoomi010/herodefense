using System;

namespace TowerDefense.Core
{
    /// <summary>스테이지 판정 (docs/STAGE.md): 보스 스테이지 번호, 별, 클리어 보상.</summary>
    public static class StageRules
    {
        /// <summary>일반 스테이지 3개마다 보스 스테이지 1개: 4, 8, 12 …</summary>
        public static bool IsBoss(int stage) => stage > 0 && stage % 4 == 0;

        /// <summary>
        /// 별: 3에서 시작해 통과한 크립이 있으면 −1, 기준 시간 안에 못 깨면 −1. 클리어하면 최소 1, 클리어 못 하면 0.
        /// </summary>
        public static int Stars(bool cleared, int leaked, float elapsedSec, float starTimeSec)
        {
            if (!cleared) return 0;
            int stars = 3;
            if (leaked > 0) stars--;
            if (elapsedSec > starTimeSec) stars--;
            return Math.Max(1, stars);
        }

        /// <summary>클리어 보상: 처음 클리어는 경험치 + 스킬 포인트, 다시 클리어는 경험치만.</summary>
        public static (int exp, int skillPoints) ClearReward(bool firstClear, int exp, int skillPoints) =>
            (exp, firstClear ? skillPoints : 0);
    }
}
