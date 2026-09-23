using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerDefense.UI.Editor
{
    /// <summary>스테이지 선택 씬 구성: UIDocument(StageSelect.uxml) + 컨트롤러 + 카메라 (멱등).</summary>
    public static class StageSelectSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/StageSelect.unity";
        private const string UxmlPath = "Assets/_Project/UI/StageSelect.uxml";
        private const string PanelSettingsPath = "Assets/_Project/UI/MainMenuPanelSettings.asset";

        [MenuItem("TowerDefense/Setup Stage Select Scene")]
        public static void Setup()
        {
            var scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ??는 쓰지 않는다 — 에디터의 GetComponent는 "가짜 null"을 돌려줄 수 있다
            var go = GameObject.Find("StageSelect");
            if (go == null) go = new GameObject("StageSelect");
            var doc = go.GetComponent<UIDocument>();
            if (doc == null) doc = go.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (go.GetComponent<StageSelectController>() == null) go.AddComponent<StageSelectController>();

            var camGo = GameObject.Find("Main Camera");
            if (camGo == null) camGo = new GameObject("Main Camera");
            var cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.tag = "MainCamera";

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[StageSelectSetup] StageSelect 씬 구성 완료");
        }
    }
}
