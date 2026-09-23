using System;
using System.Collections.Generic;

namespace TowerDefense.Core
{
    /// <summary>
    /// 계정에 영구 저장되는 플레이어 기록 (docs/PLAYER_SKILL_TREE.md). 레벨, 경험치, 스킬 포인트, 찍은 노드.
    /// 필드는 JsonUtility 직렬화를 위해 public.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public int level = 1;
        /// <summary>현재 레벨 안에서 모은 경험치</summary>
        public int exp;
        /// <summary>쓰지 않은 스킬 포인트</summary>
        public int skillPoints;
        /// <summary>찍은 노드 id (시작 노드는 넣지 않는다 — 항상 찍힌 것으로 본다)</summary>
        public List<string> nodes = new List<string>();
        /// <summary>임시: 첫 스테이지를 한 번 끝내면 true (docs/IMPLEMENTATION_PLAN.md 결정 2)</summary>
        public bool tutorialDone;

        public const int PointsPerLevel = 1;

        /// <summary>level → level+1 에 필요한 경험치.</summary>
        // ponytail: 선형 임시 곡선, 테스트 플레이로 수치를 정하면 표로 바꾼다
        public static int ExpToNext(int level) => 100 + 50 * (level - 1);

        /// <summary>경험치를 더하고 레벨업마다 스킬 포인트를 준다. 오른 레벨 수 반환.</summary>
        public int AddExp(int amount)
        {
            if (amount <= 0) return 0;
            exp += amount;
            int gained = 0;
            while (exp >= ExpToNext(level))
            {
                exp -= ExpToNext(level);
                level++;
                skillPoints += PointsPerLevel;
                gained++;
            }
            return gained;
        }
    }
}
