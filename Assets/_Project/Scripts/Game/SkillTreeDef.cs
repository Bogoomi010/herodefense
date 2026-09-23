using System.Collections.Generic;
using TowerDefense.Core;
using UnityEngine;

namespace TowerDefense.Game
{
    /// <summary>
    /// 플레이어 스킬트리 노드 목록 (docs/PLAYER_SKILL_TREE.md). 개발자가 인스펙터에서 채운다.
    /// 에셋은 Resources/SkillTree 한 개 — 메뉴와 인게임이 같은 에셋을 읽는다.
    /// </summary>
    [CreateAssetMenu(menuName = "TowerDefense/Skill Tree", fileName = "SkillTree")]
    public sealed class SkillTreeDef : ScriptableObject
    {
        public List<SkillNode> nodes = new List<SkillNode>();

        public const string ResourcePath = "SkillTree";

        public static SkillTreeDef LoadDefault() => Resources.Load<SkillTreeDef>(ResourcePath);

        public SkillTree Build() => new SkillTree(nodes);

        /// <summary>에셋이 없을 때도 게임이 돌도록: 효과 없는 빈 트리.</summary>
        public static SkillTree LoadOrEmpty()
        {
            var def = LoadDefault();
            return def != null ? def.Build() : new SkillTree(new SkillNode[0]);
        }
    }
}
