using System;
using System.Collections.Generic;
using TowerDefense.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.Game
{
    /// <summary>스테이지 하나의 데이터 (docs/STAGE.md). 번호 = 목록 순서 + 1, 보스 여부는 번호로 정한다.</summary>
    [Serializable]
    public sealed class StageDef
    {
        [Tooltip("이 스테이지의 씬 이름 (빌드 설정에 등록)")]
        public string sceneName;
        [Tooltip("별 기준 시간(초). 이 시간 안에 못 깨면 별 −1")]
        public float starTimeSec = 420f;
        [Tooltip("이 스테이지만의 크립 HP 배율 (보스 포함). 번호로 정하는 배율(StageRules.StageHpMul)에 곱한다. 농장 구역(1~4)은 0.5")]
        public float hpMul = 1f;
        [Tooltip("이 스테이지에 나오는 크립 종류 id (MobDefs). 웨이브마다 차례로 돈다. 비우면 원본 규칙. HP 배율은 번호로 정한다(StageRules.StageHpMul)")]
        public List<string> creeps = new List<string>();
        [Tooltip("보스 스테이지의 보스 id (MobDefs.BossById, 예: bull). 비우면 기본 스테이지 보스")]
        public string boss = "";
        [Tooltip("클리어 보상 경험치 (다시 깨도 준다)")]
        public int clearExp = 60;
        [Tooltip("처음 클리어할 때만 주는 스킬 포인트")]
        public int clearSkillPoints = 1;
    }

    /// <summary>스테이지 목록. 에셋은 Resources/Stages 한 개.</summary>
    [CreateAssetMenu(menuName = "TowerDefense/Stage List", fileName = "Stages")]
    public sealed class StageList : ScriptableObject
    {
        public List<StageDef> stages = new List<StageDef>();

        public const string ResourcePath = "Stages";
        private static StageList _cached;

        public static StageList Instance => _cached != null ? _cached : (_cached = Resources.Load<StageList>(ResourcePath));

        public int Count => stages.Count;

        /// <summary>스테이지 번호(1부터)로 찾는다. 없으면 null.</summary>
        public StageDef Get(int stage) => stage >= 1 && stage <= stages.Count ? stages[stage - 1] : null;

        /// <summary>씬 이름으로 스테이지 번호를 찾는다. 없으면 0.</summary>
        public int NumberOf(string sceneName)
        {
            for (int i = 0; i < stages.Count; i++) if (stages[i].sceneName == sceneName) return i + 1;
            return 0;
        }
    }

    /// <summary>씬 이동: 스테이지 선택 ↔ 스테이지. 지금 스테이지 번호는 스테이지 씬 이름으로 알아낸다.</summary>
    public static class StageFlow
    {
        public const string StageSelectScene = "StageSelect";
        public const string MainMenuScene = "MainMenu";

        public static void LoadStage(int stage)
        {
            var def = StageList.Instance != null ? StageList.Instance.Get(stage) : null;
            if (def == null) { Debug.LogError($"[StageFlow] 스테이지 {stage}가 목록에 없습니다"); return; }
            SceneManager.LoadScene(def.sceneName);
        }

        public static void LoadStageSelect() => SceneManager.LoadScene(StageSelectScene);
        public static void LoadMainMenu() => SceneManager.LoadScene(MainMenuScene);

        /// <summary>씬 이름의 스테이지 번호. 목록에 없으면 0.</summary>
        public static int StageOf(string sceneName) => StageList.Instance != null ? StageList.Instance.NumberOf(sceneName) : 0;

        public static bool IsBoss(int stage) => StageRules.IsBoss(stage);
    }
}
