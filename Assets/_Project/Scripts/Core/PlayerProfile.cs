using System;
using System.Collections.Generic;

namespace TowerDefense.Core
{
    /// <summary>
    /// 저장 슬롯 하나의 전체 기록 (docs/STAGE.md, docs/PLAYER_SKILL_TREE.md).
    /// 슬롯 3개는 서로 완전히 다른 게임이다: 난이도, 레벨, 스킬트리, 스테이지 기록 모두 슬롯마다 따로.
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
        /// <summary>슬롯을 만들 때 정하고 바꿀 수 없다</summary>
        public Difficulty difficulty = Difficulty.Normal;
        /// <summary>스테이지별 최고 별. 인덱스 = 스테이지 번호 - 1, 0 = 아직 못 깸</summary>
        public List<int> stageStars = new List<int>();
        /// <summary>이 저장 슬롯에서 이미 만난 크립 종류 id — 처음 만날 때만 소개 카드를 띄운다</summary>
        public List<string> seenCreeps = new List<string>();

        public const int PointsPerLevel = 1;

        /// <summary>level → level+1 에 필요한 경험치.</summary>
        // ponytail: 선형 임시 곡선, 테스트 플레이로 수치를 정하면 표로 바꾼다
        public static int ExpToNext(int level) => 100 + 50 * (level - 1);

        public int StarsOf(int stage) => stage >= 1 && stage <= stageStars.Count ? stageStars[stage - 1] : 0;
        public bool IsCleared(int stage) => StarsOf(stage) >= 1;
        /// <summary>순차 해금: 1스테이지는 처음부터, 그 뒤는 앞 스테이지를 깨야 열린다.</summary>
        public bool IsUnlocked(int stage) => stage == 1 || (stage > 1 && IsCleared(stage - 1));

        /// <summary>클리어 기록 (최고 별 유지). 처음 클리어면 true.</summary>
        public bool RecordClear(int stage, int stars)
        {
            if (stage < 1 || stars < 1) return false;
            bool first = !IsCleared(stage);
            while (stageStars.Count < stage) stageStars.Add(0);
            if (stars > stageStars[stage - 1]) stageStars[stage - 1] = stars;
            return first;
        }

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
