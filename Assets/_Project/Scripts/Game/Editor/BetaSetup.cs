using System.IO;
using TowerDefense.Core;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Game.EditorTools
{
    /// <summary>
    /// 베타 기초 시스템 에셋 준비 (docs/IMPLEMENTATION_PLAN.md).
    /// 스킬트리 예시 에셋 (Resources/SkillTree)을 만든다.
    /// 나무 프리팹(Prefabs/Trees)과 Map 씬의 나무 15그루는 1회용 이관으로 만들었다 (2026-09-23, TreeScatter 제거).
    /// 배치 모드: -executeMethod TowerDefense.Game.EditorTools.BetaSetup.CreateSkillTree
    /// </summary>
    public static class BetaSetup
    {
        private const string SkillTreePath = "Assets/_Project/Resources/SkillTree.asset";

        [MenuItem("TowerDefense/Beta/Create Skill Tree Asset")]
        public static void CreateSkillTree()
        {
            if (AssetDatabase.LoadAssetAtPath<SkillTreeDef>(SkillTreePath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(SkillTreePath));
            var def = ScriptableObject.CreateInstance<SkillTreeDef>();

            void Node(string id, string title, SkillEffect e, float v, float x, float y, string link, bool key = false) =>
                def.nodes.Add(new SkillNode { id = id, title = title, effect = e, value = v, x = x, y = y, keystone = key, links = { link } });

            def.nodes.Add(new SkillNode { id = "start", title = "기본", start = true, x = 0, y = 0 });
            // 건설 (위)
            Node("zone1", "구역 축소", SkillEffect.ZoneShrinkPct, 0.1f, 0, 1, "start");
            Node("zone2", "구역 축소", SkillEffect.ZoneShrinkPct, 0.1f, 0, 2, "zone1");
            Node("zone3", "밀집 건설", SkillEffect.ZoneShrinkPct, 0.1f, 0, 3, "zone2", true);
            // 작업 (오른쪽)
            Node("upg1", "빠른 업그레이드", SkillEffect.UpgradeTimePct, 0.1f, 1, 0, "start");
            Node("upg2", "빠른 업그레이드", SkillEffect.UpgradeTimePct, 0.1f, 2, 0, "upg1");
            Node("upg3", "숙련공", SkillEffect.UpgradeTimePct, 0.15f, 3, 0, "upg2", true);
            // 영웅 (왼쪽)
            Node("desc1", "강림 쿨타임", SkillEffect.DescentCdPct, 0.1f, -1, 0, "start");
            Node("desc2", "강림 쿨타임", SkillEffect.DescentCdPct, 0.1f, -2, 0, "desc1");
            Node("desc3", "신속 강림", SkillEffect.DescentCdPct, 0.15f, -3, 0, "desc2", true);
            // 성장 (왼쪽 아래)
            Node("exp1", "경험치", SkillEffect.ExpPct, 0.1f, -1, -1, "start");
            Node("exp2", "배움의 기쁨", SkillEffect.ExpPct, 0.2f, -2, -2, "exp1", true);
            // 경제 (오른쪽 아래)
            Node("sgold", "시작 골드", SkillEffect.StartGold, 30f, 1, -1, "start");
            Node("gold1", "골드 획득", SkillEffect.GoldPct, 0.1f, 2, -2, "sgold");
            Node("gold2", "황금 손", SkillEffect.GoldPct, 0.15f, 3, -3, "gold1", true);

            AssetDatabase.CreateAsset(def, SkillTreePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BetaSetup] 스킬트리 예시 생성: {SkillTreePath} ({def.nodes.Count}노드)");
        }
    }
}
