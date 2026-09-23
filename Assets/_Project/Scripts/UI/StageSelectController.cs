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

        /// <summary>스킬트리는 튜토리얼을 마친 뒤에만 열린다 (docs/PLAYER_SKILL_TREE.md).</summary>
        private void SetupSkills()
        {
            var btn = _root.Q<Button>("btn-skills");
            if (!_profile.tutorialDone || _skills == null) return;
            btn.RemoveFromClassList("hidden");
            var view = new SkillTreeView(_root.Q<VisualElement>("skills-viewport"), _root.Q<Label>("skills-info"), _root.Q<Label>("skills-desc"), _profile);
            btn.clicked += () => _skills.RemoveFromClassList("hidden");
            _root.Q<Button>("skills-reset").clicked += view.ResetSkills;
            _root.Q<Button>("skills-back").clicked += () =>
            {
                _skills.AddToClassList("hidden");
                _root.Q<Label>("slot-info").text =
                    $"슬롯 {ProfileStore.Current + 1} · {MainMenuController.DifficultyName(_profile.difficulty)} · Lv {_profile.level} · 스킬 포인트 {_profile.skillPoints}";
            };
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
