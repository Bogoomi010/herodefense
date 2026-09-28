using UnityEditor;
using UnityEditor.SceneManagement;

namespace TowerDefense.UI.Editor
{
    /// <summary>
    /// 에디터에서 Play를 누르면 열려 있는 씬과 관계없이 메인 메뉴부터 시작한다 (빌드도 MainMenu가 첫 씬).
    /// 스테이지 씬을 바로 시험하려면 메뉴 TowerDefense/Play From Main Menu 체크를 끈다.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromMainMenu
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string MenuPath = "TowerDefense/Play From Main Menu";
        private const string PrefKey = "TowerDefense.PlayFromMainMenu";

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static PlayFromMainMenu()
        {
            EditorApplication.delayCall += Apply;
            // Play 직전에 한 번 더 — 컴파일 직후 delayCall이 돌기 전에 Play를 눌러도 적용되게
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Apply(); };
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        // 테스트 러너가 연 씬(InitTestScene…)에서 Play하면 비운다: 메인 메뉴로 바꾸면 PlayMode 테스트가 시작되지 않고 멈춘다
        private static void Apply() =>
            EditorSceneManager.playModeStartScene = Enabled && !EditorSceneManager.GetActiveScene().name.StartsWith("InitTestScene")
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
    }
}
