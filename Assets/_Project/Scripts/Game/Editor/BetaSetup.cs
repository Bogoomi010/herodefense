using System.Collections.Generic;
using System.IO;
using TowerDefense.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using TowerDefense.Map;
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

        private const string SceneDir = "Assets/_Project/Scenes";
        private const string StagesPath = "Assets/_Project/Resources/Stages.asset";

        /// <summary>
        /// 스테이지 씬과 목록 (docs/STAGE.md). Map → Stage_01로 이름을 바꾸고(GUID 유지),
        /// 테스트용 Stage_02~04를 복제해 시드만 바꾼다(녹색 지대 밖으로 밀려난 나무는 지운다). 4번은 보스 스테이지.
        /// 빌드 설정: MainMenu → StageSelect → Stage_01…
        /// </summary>
        [MenuItem("TowerDefense/Beta/Setup Stages")]
        public static void SetupStages()
        {
            string first = $"{SceneDir}/Stage_01.unity";
            if (!File.Exists(first) && File.Exists($"{SceneDir}/Map.unity"))
            {
                string err = AssetDatabase.MoveAsset($"{SceneDir}/Map.unity", first);
                if (!string.IsNullOrEmpty(err)) { Debug.LogError($"[BetaSetup] Map 이름 변경 실패: {err}"); return; }
            }

            // 스테이지별 맵 시드. 2026-09-27에 1·3스테이지 맵을 맞바꿨다(Stage_01 = 시드 23, Stage_03 = 시드 1)
            int[] seeds = { 23, 7, 1, 42 };
            for (int n = 2; n <= seeds.Length; n++)
            {
                string path = $"{SceneDir}/Stage_{n:00}.unity";
                if (File.Exists(path)) continue;
                AssetDatabase.CopyAsset(first, path);
                var scene = EditorSceneManager.OpenScene(path);
                var map = Object.FindFirstObjectByType<TileMap>();
                map.seed = seeds[n - 1];
                map.Generate();
                int removed = 0;
                foreach (var t in Object.FindObjectsByType<FieldTree>(FindObjectsSortMode.None))
                    if (!map.IsGround(t.transform.position)) { Object.DestroyImmediate(t.gameObject); removed++; }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[BetaSetup] {path} 생성 (시드 {map.seed}, 경로에 걸린 나무 {removed}그루 제거)");
            }

            var list = AssetDatabase.LoadAssetAtPath<StageList>(StagesPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<StageList>();
                // 농장 구역 크립 풀 (docs/STAGE.md 크립 구성, docs/STORY_ZONES.md) — 한 종류씩 늘며 웨이브 안에서 섞여 나온다
                string[][] pools =
                {
                    new[] { "sheep" },
                    new[] { "sheep", "pig" },
                    new[] { "sheep", "pig", "chicken" },
                    new[] { "sheep", "pig", "chicken", "cow" },
                };
                int[] exp = { 60, 70, 80, 120 };
                for (int n = 1; n <= seeds.Length; n++)
                    list.stages.Add(new StageDef { sceneName = $"Stage_{n:00}", starTimeSec = 420f, creeps = new List<string>(pools[n - 1]), boss = n == 4 ? "bull" : "", clearExp = exp[n - 1], clearSkillPoints = 1 });
                AssetDatabase.CreateAsset(list, StagesPath);
            }

            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene($"{SceneDir}/MainMenu.unity", true),
                new EditorBuildSettingsScene($"{SceneDir}/StageSelect.unity", true),
            };
            foreach (var st in list.stages) scenes.Add(new EditorBuildSettingsScene($"{SceneDir}/{st.sceneName}.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[BetaSetup] 스테이지 {list.stages.Count}개, 빌드 씬 {scenes.Count}개 설정");
        }
    }
}
