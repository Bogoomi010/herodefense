using System.Collections.Generic;
using System.Linq;
using TowerDefense.Core;
using TowerDefense.Game;
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
    /// 시작 → 저장 슬롯 3개 (docs/STAGE.md). 빈 슬롯은 난이도를 골라 새로 만들고, 있는 슬롯은 스테이지 선택으로 간다.
    ///
    /// 사용하는 PlayerPrefs 키:
    /// - "SaveSlot" (int, 지금 슬롯 — ProfileStore.Current)
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
            RegisterPanel("panel-slots");
            RegisterPanel("panel-delete");

            BindClick("btn-start", () => { RefreshSlots(); Open("panel-slots"); });
            BindClick("btn-settings", () => Open("panel-settings"));
            BindClick("btn-credits", () => Open("panel-credits"));
            BindClick("btn-quit", () => Open("panel-quit"));

            BindClick("diff-easy", () => SelectDifficulty(Difficulty.Easy));
            BindClick("diff-normal", () => SelectDifficulty(Difficulty.Normal));
            BindClick("diff-hard", () => SelectDifficulty(Difficulty.Hard));
            BindClick("diff-back", CloseTop);

            BindClick("settings-back", CloseTop);
            BindClick("credits-back", CloseTop);

            SetupSlots();

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

            // 한 번에 맨 위 패널 하나만 보인다 — 아래 패널의 버튼을 잘못 누르지 않게
            if (_panelStack.Count > 0) _panelStack.Peek().AddToClassList("hidden");
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
            if (_panelStack.Count > 0) _panelStack.Peek().RemoveFromClassList("hidden");
        }

        private void RegisterPanel(string name)
        {
            var panel = Find<VisualElement>(name);
            if (panel != null)
            {
                _panels[name] = panel;
            }
        }

        // 저장 슬롯 ------------------------------------------------

        private int _newSlot, _deleteSlot;

        private void SetupSlots()
        {
            BindClick("slots-back", CloseTop);
            BindClick("delete-no", CloseTop);
            BindClick("delete-yes", () =>
            {
                ProfileStore.Delete(_deleteSlot);
                RefreshSlots();
                CloseTop();
            });
            for (int i = 0; i < ProfileStore.SlotCount; i++)
            {
                int slot = i;
                BindClick($"slot-{slot}", () => PickSlot(slot));
                BindClick($"slot-del-{slot}", () =>
                {
                    _deleteSlot = slot;
                    var title = Find<Label>("delete-title");
                    if (title != null) title.text = $"슬롯 {slot + 1}을 삭제할까요?";
                    Open("panel-delete");
                });
            }
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < ProfileStore.SlotCount; i++)
            {
                var p = ProfileStore.Load(i);
                var btn = Find<Button>($"slot-{i}");
                if (btn != null) btn.text = p == null ? $"슬롯 {i + 1} — 새 게임" : $"슬롯 {i + 1} — {DifficultyName(p.difficulty)} · Lv {p.level} · 스테이지 {Progress(p)}";
                // 깨진 파일도 지울 수 있게: 삭제 버튼은 파일이 있으면 보인다
                var del = Find<Button>($"slot-del-{i}");
                if (del != null) del.EnableInClassList("hidden", !ProfileStore.Exists(i));
                if (btn != null && p == null && ProfileStore.Exists(i)) btn.text = $"슬롯 {i + 1} — 읽을 수 없음 (삭제 후 새로 시작)";
            }
        }

        private void PickSlot(int slot)
        {
            if (ProfileStore.Load(slot) != null)
            {
                ProfileStore.Current = slot;
                StageFlow.LoadStageSelect();
                return;
            }
            if (ProfileStore.Exists(slot)) return; // 깨진 파일 — 삭제해야 새로 시작할 수 있다
            _newSlot = slot;
            Open("panel-difficulty");
        }

        /// <summary>빈 슬롯에 새 게임을 만든다. 난이도는 이후 바꿀 수 없다.</summary>
        private void SelectDifficulty(Difficulty difficulty)
        {
            if (ProfileStore.Exists(_newSlot)) { Debug.LogWarning($"[MainMenuController] 슬롯 {_newSlot + 1}에 이미 기록이 있어 새로 만들지 않습니다"); return; }
            try { ProfileStore.Save(_newSlot, new PlayerProfile { difficulty = difficulty }); }
            catch (System.Exception e) { Debug.LogError($"[MainMenuController] 슬롯 저장 실패: {e}"); return; }
            ProfileStore.Current = _newSlot;
            StageFlow.LoadStageSelect();
        }

        public static string DifficultyName(Difficulty d) => d switch
        {
            Difficulty.Easy => "쉬움",
            Difficulty.Hard => "어려움",
            _ => "보통",
        };

        /// <summary>도전할 수 있는 가장 뒤 스테이지 번호</summary>
        private static int Progress(PlayerProfile p)
        {
            int n = 1;
            while (p.IsCleared(n)) n++;
            return n;
        }

        // 종료 ------------------------------------------------

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
