using TowerDefense.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerDefense.UI.Editor
{
    /// <summary>시작 메뉴 씬 구성: PanelSettings 에셋 생성 + UIDocument/카메라 배치 + 빌드 설정 등록 (멱등).</summary>
    public static class MainMenuSetup
    {
        private const string PanelSettingsPath = "Assets/_Project/UI/MainMenuPanelSettings.asset";
        private const string UxmlPath = "Assets/_Project/UI/MainMenu.uxml";
        private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string DefaultThemePath = "Assets/_Project/UI/Themes/UnityDefaultRuntimeTheme.tss";

        [MenuItem("TowerDefense/Setup Main Menu Scene")]
        public static void Setup()
        {
            var panelSettings = EnsurePanelSettings();

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            UnityEngine.SceneManagement.Scene scene;
            if (System.IO.File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var menuGo = GameObject.Find("MainMenu");
            if (menuGo == null)
            {
                menuGo = new GameObject("MainMenu");
                Undo.RegisterCreatedObjectUndo(menuGo, "Setup Main Menu Scene");
            }

            var uiDocument = menuGo.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = menuGo.AddComponent<UIDocument>();
            }
            uiDocument.panelSettings = panelSettings;

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                Debug.LogWarning($"MainMenuSetup: UXML을 찾을 수 없습니다 ({UxmlPath}). 나중에 수동으로 연결하세요.");
            }
            else
            {
                uiDocument.visualTreeAsset = visualTree;
            }

            if (menuGo.GetComponent<MainMenuController>() == null)
            {
                menuGo.AddComponent<MainMenuController>();
            }

            var cameraGo = GameObject.Find("Main Camera");
            if (cameraGo == null)
            {
                cameraGo = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(cameraGo, "Setup Main Menu Scene");
            }

            var camera = cameraGo.GetComponent<Camera>();
            if (camera == null)
            {
                camera = cameraGo.AddComponent<Camera>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraGo.tag = "MainCamera";

            // 빌드 설정은 BetaSetup.SetupStages가 관리한다 (메뉴 → 스테이지 선택 → 스테이지들)
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("MainMenuSetup: MainMenu 씬 구성 완료 (UIDocument/카메라/빌드 설정).");
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings != null) return panelSettings;

            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(DefaultThemePath);
            if (theme == null)
            {
                var guids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
                if (guids.Length > 0)
                {
                    theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (theme == null)
            {
                Debug.LogWarning("MainMenuSetup: ThemeStyleSheet을 찾을 수 없어 PanelSettings.themeStyleSheet을 비워 둡니다.");
            }
            else
            {
                panelSettings.themeStyleSheet = theme;
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/UI"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "UI");
            }

            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return panelSettings;
        }
    }
}
