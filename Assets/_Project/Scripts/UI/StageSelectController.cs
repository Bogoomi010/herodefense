using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TowerDefense.UI
{
    /// <summary>
    /// 스테이지 선택 (docs/STAGE.md): 한 페이지 15칸(5×3), 칸마다 번호·별·자물쇠·보스 표시, 좌우로 페이지를 넘긴다.
    /// 지금 저장 슬롯(ProfileStore.Current)의 기록을 보여 준다. 스킬트리도 슬롯마다 따로라 여기서 연다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StageSelectController : MonoBehaviour
    {
        public const int PerPage = 15;

        private VisualElement _root, _grid, _skills;
        private Label _pageLabel;
        private PlayerProfile _profile;
        private int _page;

        private int StageCount => StageList.Instance != null ? StageList.Instance.Count : 0;
        private int PageCount => Mathf.Max(1, (StageCount + PerPage - 1) / PerPage);

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _profile = ProfileStore.LoadCurrent();
            _grid = _root.Q<VisualElement>("stage-grid");
            _pageLabel = _root.Q<Label>("page-label");
            _skills = _root.Q<VisualElement>("panel-skills");

            _root.Q<Label>("slot-info").text =
                $"슬롯 {ProfileStore.Current + 1} · {MainMenuController.DifficultyName(_profile.difficulty)} · Lv {_profile.level} · 스킬 포인트 {_profile.skillPoints}";
            _root.Q<Button>("btn-back").clicked += StageFlow.LoadMainMenu;
            _root.Q<Button>("page-prev").clicked += () => ShowPage(_page - 1);
            _root.Q<Button>("page-next").clicked += () => ShowPage(_page + 1);
            SetupSkills();
            HeroPicker.Build(_root.Q<VisualElement>("hero-row"), _profile);

            // 가장 최근에 열린 스테이지가 있는 페이지부터
            int frontier = 1;
            while (frontier < StageCount && _profile.IsCleared(frontier)) frontier++;
            ShowPage((frontier - 1) / PerPage);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && _skills != null && !_skills.ClassListContains("hidden"))
                _skills.AddToClassList("hidden");
        }

        private void ShowPage(int page)
        {
            _page = Mathf.Clamp(page, 0, PageCount - 1);
            _grid.Clear();
            int first = _page * PerPage + 1;
            for (int n = first; n < first + PerPage && n <= StageCount; n++) _grid.Add(Cell(n));
            _pageLabel.text = $"{_page + 1} / {PageCount}";
            _root.Q<Button>("page-prev").SetEnabled(_page > 0);
            _root.Q<Button>("page-next").SetEnabled(_page < PageCount - 1);
        }

        private VisualElement Cell(int stage)
        {
            bool unlocked = _profile.IsUnlocked(stage);
            bool boss = StageRules.IsBoss(stage);
            int stars = _profile.StarsOf(stage);

            var cell = new Button(() => { if (unlocked) StageFlow.LoadStage(stage); });
            cell.AddToClassList("stage-cell");
            cell.EnableInClassList("boss", boss);
            cell.EnableInClassList("locked", !unlocked);
            cell.Add(new Label(stage.ToString()) { pickingMode = PickingMode.Ignore }.WithClass("stage-number"));
            string under = unlocked ? new string('★', stars) + new string('☆', 3 - stars) : "잠김";
            cell.Add(new Label(under) { pickingMode = PickingMode.Ignore }.WithClass("stage-stars"));
            if (boss) cell.Add(new Label("보스") { pickingMode = PickingMode.Ignore }.WithClass("stage-tag"));
            return cell;
        }

        /// <summary>
        /// 스킬 메뉴 (docs/SKILL_MENU.md): 탭 두 개(플레이어 스킬트리 | 영웅 기술), 포인트는 함께 쓴다. 튜토리얼을 마친 뒤에만 열린다.
        /// 초기화는 보고 있는 탭만. 쓰지 않은 포인트가 있으면 "스킬" 버튼에 점.
        /// </summary>
        private void SetupSkills()
        {
            var btn = _root.Q<Button>("btn-skills");
            if (!_profile.tutorialDone || _skills == null) return;
            btn.RemoveFromClassList("hidden");
            var info = _root.Q<Label>("skills-info");
            var viewport = _root.Q<VisualElement>("skills-viewport");
            var heroPane = _root.Q<VisualElement>("hero-skills");
            var desc = _root.Q<Label>("skills-desc");
            var tabPlayer = _root.Q<Button>("tab-player");
            var tabHero = _root.Q<Button>("tab-hero");

            void UpdateHeader()
            {
                info.text = $"Lv {_profile.level}  ·  경험치 {_profile.exp}/{PlayerProfile.ExpToNext(_profile.level)}  ·  스킬 포인트 {_profile.skillPoints}";
                btn.text = _profile.skillPoints > 0 ? "스킬 ●" : "스킬";
                _root.Q<Label>("slot-info").text =
                    $"슬롯 {ProfileStore.Current + 1} · {MainMenuController.DifficultyName(_profile.difficulty)} · Lv {_profile.level} · 스킬 포인트 {_profile.skillPoints}";
            }

            var tree = new SkillTreeView(viewport, info, desc, _profile);

            var heroes = new HeroSkillsView(heroPane, _profile, () => { tree.Refresh(); UpdateHeader(); });
            bool heroTab = PlayerPrefs.GetInt("SkillTab", 0) == 1; // 마지막으로 본 탭

            void ShowTab(bool hero)
            {
                heroTab = hero;
                PlayerPrefs.SetInt("SkillTab", hero ? 1 : 0);
                viewport.EnableInClassList("hidden", hero);
                desc.EnableInClassList("hidden", hero);
                heroPane.EnableInClassList("hidden", !hero);
                tabPlayer.EnableInClassList("active", !hero);
                tabHero.EnableInClassList("active", hero);
                tree.Refresh();
                heroes.Refresh();
                UpdateHeader();
            }

            tabPlayer.clicked += () => ShowTab(false);
            tabHero.clicked += () => ShowTab(true);
            btn.clicked += () => { _skills.RemoveFromClassList("hidden"); ShowTab(heroTab); };
            _root.Q<Button>("skills-reset").clicked += () =>
            {
                if (heroTab) heroes.ResetHero();
                else { tree.ResetSkills(); heroes.Refresh(); }
                UpdateHeader();
            };
            _root.Q<Button>("skills-back").clicked += () => { _skills.AddToClassList("hidden"); UpdateHeader(); };
            UpdateHeader();
        }
    }

    internal static class VisualElementExt
    {
        public static T WithClass<T>(this T e, string cls) where T : VisualElement
        {
            e.AddToClassList(cls);
            return e;
        }
    }
}
