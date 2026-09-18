using System.Collections.Generic;
using System.Linq;
using TowerDefense.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TowerDefense.UI
{
    /// <summary>
    /// 시작 메뉴 UI 컨트롤러. UIDocument의 UXML에서 이름으로 요소를 찾아 동작을 연결한다.
    /// 패널을 스택으로 관리해 ESC/뒤로/아니오 버튼으로 최상단 패널을 닫는다.
    ///
    /// 사용하는 PlayerPrefs 키:
    /// - "Difficulty" (int, TowerDefense.Core.Difficulty)
    /// - "MasterVolume" (float, 기본 1)
    /// - "BgmVolume" (float, 기본 0.8)
    /// - "SfxVolume" (float, 기본 1)
    /// - "MouseSensitivity" (float, 기본 0.12)
    /// - "Fullscreen" (int 0/1, 기본 Screen.fullScreen)
    /// - "ResolutionIndex" (int)
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        public string gameSceneName = "Map";

        private VisualElement _root;
        private readonly Dictionary<string, VisualElement> _panels = new Dictionary<string, VisualElement>();
        private readonly Stack<VisualElement> _panelStack = new Stack<VisualElement>();
        private readonly List<(int width, int height)> _resolutions = new List<(int, int)>();

        public int OpenCount => _panelStack.Count;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;

            RegisterPanel("panel-difficulty");
            RegisterPanel("panel-settings");
            RegisterPanel("panel-credits");
            RegisterPanel("panel-quit");

            BindClick("btn-start", () => Open("panel-difficulty"));
            BindClick("btn-settings", () => Open("panel-settings"));
            BindClick("btn-credits", () => Open("panel-credits"));
            BindClick("btn-quit", () => Open("panel-quit"));

            BindClick("diff-easy", () => SelectDifficulty(Difficulty.Easy));
            BindClick("diff-normal", () => SelectDifficulty(Difficulty.Normal));
            BindClick("diff-hard", () => SelectDifficulty(Difficulty.Hard));
            BindClick("diff-back", CloseTop);

            BindClick("settings-back", CloseTop);
            BindClick("credits-back", CloseTop);

            BindClick("quit-yes", QuitGame);
            BindClick("quit-no", CloseTop);

            SetupSettings();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseTop();
            }
        }

        // 패널 스택 -----------------------------------------------------

        public void Open(string panelName)
        {
            if (!_panels.TryGetValue(panelName, out var panel) || panel == null)
            {
                return;
            }

            panel.RemoveFromClassList("hidden");
            _panelStack.Push(panel);
        }

        public void CloseTop()
        {
            if (_panelStack.Count == 0)
            {
                return;
            }

            var top = _panelStack.Pop();
            top.AddToClassList("hidden");
        }

        private void RegisterPanel(string name)
        {
            var panel = Find<VisualElement>(name);
            if (panel != null)
            {
                _panels[name] = panel;
            }
        }

        // 난이도/종료 ------------------------------------------------

        private void SelectDifficulty(Difficulty difficulty)
        {
            PlayerPrefs.SetInt("Difficulty", (int)difficulty);
            PlayerPrefs.Save();
            SceneManager.LoadScene(gameSceneName);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // 설정 패널 -----------------------------------------------------

        private void SetupSettings()
        {
            var master = Find<Slider>("sl-master");
            if (master != null)
            {
                master.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
                AudioListener.volume = master.value;
                master.RegisterValueChangedCallback(evt =>
                {
                    PlayerPrefs.SetFloat("MasterVolume", evt.newValue);
                    PlayerPrefs.Save();
                    AudioListener.volume = evt.newValue;
                });
            }

            var bgm = Find<Slider>("sl-bgm");
            if (bgm != null)
            {
                bgm.value = PlayerPrefs.GetFloat("BgmVolume", 0.8f);
                bgm.RegisterValueChangedCallback(evt =>
                {
                    PlayerPrefs.SetFloat("BgmVolume", evt.newValue);
                    PlayerPrefs.Save();
                });
            }

            var sfx = Find<Slider>("sl-sfx");
            if (sfx != null)
            {
                sfx.value = PlayerPrefs.GetFloat("SfxVolume", 1f);
                sfx.RegisterValueChangedCallback(evt =>
                {
                    PlayerPrefs.SetFloat("SfxVolume", evt.newValue);
                    PlayerPrefs.Save();
                });
            }

            var sens = Find<Slider>("sl-sens");
            if (sens != null)
            {
                sens.value = PlayerPrefs.GetFloat("MouseSensitivity", 0.12f);
                sens.RegisterValueChangedCallback(evt =>
                {
                    PlayerPrefs.SetFloat("MouseSensitivity", evt.newValue);
                    PlayerPrefs.Save();
                });
            }

            var fullscreen = Find<Toggle>("tg-fullscreen");
            if (fullscreen != null)
            {
                fullscreen.value = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
                fullscreen.RegisterValueChangedCallback(evt =>
                {
                    PlayerPrefs.SetInt("Fullscreen", evt.newValue ? 1 : 0);
                    PlayerPrefs.Save();
                    Screen.fullScreen = evt.newValue;
                });
            }

            SetupResolutionDropdown();
        }

        private void SetupResolutionDropdown()
        {
            var dropdown = Find<DropdownField>("dd-resolution");
            if (dropdown == null)
            {
                return;
            }

            _resolutions.Clear();
            _resolutions.AddRange(Screen.resolutions
                .Select(r => (r.width, r.height))
                .Distinct());

            dropdown.choices = _resolutions
                .Select(r => $"{r.width} x {r.height}")
                .ToList();

            int currentIndex = _resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
            int savedIndex = PlayerPrefs.GetInt("ResolutionIndex", currentIndex >= 0 ? currentIndex : 0);
            dropdown.index = Mathf.Clamp(savedIndex, 0, Mathf.Max(0, _resolutions.Count - 1));

            dropdown.RegisterValueChangedCallback(evt =>
            {
                int index = dropdown.index;
                if (index < 0 || index >= _resolutions.Count)
                {
                    return;
                }

                PlayerPrefs.SetInt("ResolutionIndex", index);
                PlayerPrefs.Save();

                var (width, height) = _resolutions[index];
                Screen.SetResolution(width, height, Screen.fullScreenMode);
            });
        }

        // 공통 헬퍼 -----------------------------------------------------

        private void BindClick(string name, System.Action action)
        {
            var button = Find<Button>(name);
            if (button != null)
            {
                button.clicked += action;
            }
        }

        private T Find<T>(string name) where T : VisualElement
        {
            var element = _root.Q<T>(name);
            if (element == null)
            {
                Debug.LogWarning($"[MainMenuController] '{name}' 요소를 찾을 수 없습니다.");
            }

            return element;
        }
    }
}
