using System.Linq;
using TowerDefense.Core;
using TowerDefense.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerDefense.UI
{
    /// <summary>
    /// 스킬 메뉴의 영웅 기술 탭 (docs/SKILL_MENU.md): 맨 위 영웅 고르기 → 그 영웅의 기술 카드 → 고른 카드의 설명·배우기·Q/E 장착.
    /// 잠긴 영웅의 기술도 볼 수는 있지만 배울 수 없다. 바꾸면 바로 저장 슬롯에 저장한다.
    /// </summary>
    public sealed class HeroSkillsView
    {
        private readonly PlayerProfile _profile;
        private readonly VisualElement _heroes, _cards;
        private readonly Label _loadout, _detail;
        private readonly Button _learn, _equipQ, _equipE;
        private readonly System.Action _changed;
        private HeroKind _hero;
        private HeroSkillDef _sel;

        public HeroSkillsView(VisualElement root, PlayerProfile profile, System.Action changed)
        {
            _profile = profile;
            _changed = changed;
            _hero = Heroes.Chosen(profile);

            root.Add(_heroes = new VisualElement().WithClass("hero-row"));
            _heroes.style.justifyContent = Justify.FlexStart;
            root.Add(_cards = new VisualElement().WithClass("skill-cards"));
            root.Add(_loadout = new Label().WithClass("skills-info"));
            root.Add(_detail = new Label("카드를 누르면 설명이 나온다").WithClass("skill-detail"));
            var actions = new VisualElement().WithClass("skill-actions");
            actions.Add(_learn = new Button(Learn).WithClass("menu-button"));
            actions.Add(_equipQ = new Button(() => Equip(0)) { text = "Q에 장착" }.WithClass("menu-button"));
            actions.Add(_equipE = new Button(() => Equip(1)) { text = "E에 장착" }.WithClass("menu-button"));
            root.Add(actions);
            Refresh();
        }

        public void ResetHero()
        {
            Heroes.Reset(_profile, _hero);
            Save();
        }

        private void Learn()
        {
            if (Heroes.Learn(_profile, _sel)) Save();
        }

        private void Equip(int slot)
        {
            if (_sel != null && Heroes.Equip(_profile, _hero, slot, _sel.id)) Save();
        }

        private void Save()
        {
            try { ProfileStore.SaveCurrent(_profile); }
            catch (System.Exception e) { Debug.LogError($"[HeroSkillsView] 프로필 저장 실패: {e}"); }
            _changed?.Invoke();
            Refresh();
        }

        public void Refresh()
        {
            _heroes.Clear();
            foreach (var k in Heroes.All)
            {
                var kind = k;
                bool open = Heroes.IsUnlocked(_profile, k);
                var b = new Button(() => { _hero = kind; _sel = null; Refresh(); })
                    { text = open ? Heroes.Name(k) : $"{Heroes.Name(k)} · 스테이지 {Heroes.UnlockStage(k)}" }.WithClass("menu-button").WithClass("hero-pick");
                HeroPicker.Tint(b, k, open);
                b.EnableInClassList("chosen", k == _hero);
                _heroes.Add(b);
            }

            _cards.Clear();
            var skills = Heroes.SkillsOf(_hero).ToList();
            if (skills.Count == 0) _cards.Add(new Label("아직 기술이 없다 (기획 전)").WithClass("skill-detail"));
            foreach (var s in skills)
            {
                var skill = s;
                int lv = Heroes.LevelOf(_profile, s);
                bool open = Heroes.SkillOpen(_profile, s);
                string state = lv > 0 ? new string('★', lv) + new string('☆', Heroes.MaxSkillLevel - lv)
                    : open ? $"배울 수 있음 · {Heroes.SkillCost}포인트" : $"잠김 · 스테이지 {s.unlockStage} 클리어";
                var card = new Button(() => { _sel = skill; Refresh(); }) { text = $"{s.name}\n{state}\n쿨타임 {s.cooldownSec:0}초" }.WithClass("skill-card");
                card.EnableInClassList("learned", lv > 0);
                card.EnableInClassList("locked", lv == 0 && !open);
                card.EnableInClassList("selected", s == _sel);
                _cards.Add(card);
            }

            var l = Heroes.Loadout(_profile, _hero);
            _loadout.text = $"장착   Q: {Heroes.Skill(l.q)?.name ?? "비어 있음"}   E: {Heroes.Skill(l.e)?.name ?? "비어 있음"}";

            int level = _sel != null ? Heroes.LevelOf(_profile, _sel) : 0;
            if (_sel != null)
            {
                string now = level > 0 ? $"{level}단계: {_sel.Describe(level)}" : "아직 배우지 않음";
                string next = level < Heroes.MaxSkillLevel ? $"\n{(level == 0 ? "배우면" : "다음 단계")}: {_sel.Describe(level + 1)}" : "\n최대 단계";
                _detail.text = $"{_sel.name} · 쿨타임 {_sel.cooldownSec:0}초\n{now}{next}";
            }
            _learn.text = level == 0 ? $"배우기 ({Heroes.SkillCost}포인트)" : $"강화 ({Heroes.SkillCost}포인트)";
            _learn.SetEnabled(Heroes.CanLearn(_profile, _sel));
            _equipQ.SetEnabled(level > 0);
            _equipE.SetEnabled(level > 0);
        }
    }

    /// <summary>스테이지 선택 화면의 영웅 고르기 (docs/HERO.md): 열린 영웅만 고를 수 있고, 고르면 저장 슬롯에 기억한다.</summary>
    public static class HeroPicker
    {
        public static void Build(VisualElement row, PlayerProfile profile)
        {
            row.Clear();
            row.Add(new Label("영웅").WithClass("skills-info"));
            var chosen = Heroes.Chosen(profile);
            foreach (var k in Heroes.All)
            {
                var kind = k;
                bool open = Heroes.IsUnlocked(profile, k);
                var b = new Button(() =>
                {
                    profile.hero = kind;
                    try { ProfileStore.SaveCurrent(profile); }
                    catch (System.Exception e) { Debug.LogError($"[HeroPicker] 저장 실패: {e}"); }
                    Build(row, profile);
                }) { text = open ? Heroes.Name(k) : $"{Heroes.Name(k)}\n잠김 · 스테이지 {Heroes.UnlockStage(k)}" }.WithClass("menu-button").WithClass("hero-pick");
                Tint(b, k, open);
                b.SetEnabled(open);
                b.EnableInClassList("chosen", k == chosen);
                row.Add(b);
            }
        }

        /// <summary>영웅 색 (디자인 전 임시 구분, Heroes.Rgb)으로 버튼 바탕을 칠한다. 잠긴 영웅은 어둡게.</summary>
        public static void Tint(VisualElement b, HeroKind k, bool open)
        {
            var (r, g, bl) = Heroes.Rgb(k);
            float m = open ? 0.55f : 0.2f;
            b.style.backgroundColor = new Color(r * m, g * m, bl * m, 0.95f);
        }
    }
}
