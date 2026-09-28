using System;
using System.Collections.Generic;

namespace TowerDefense.Core
{
    /// <summary>영웅 4종 (docs/HERO.md). 저장 파일에 정수로 남으므로 새 값은 끝에만 붙인다.</summary>
    public enum HeroKind { Sword, Gunner, Archer, Mage }

    /// <summary>영웅 기술 하나 (docs/SKILL_MENU.md, docs/HERO_GUNNER.md). 수치는 단계(1~3)마다 value + perLevel × (단계 − 1).</summary>
    public sealed class HeroSkillDef
    {
        public string id, name;
        public HeroKind hero;
        public float cooldownSec, value, perLevel;
        /// <summary>이 스테이지를 깨면 배울 수 있다. 0 = 처음부터 1단계로 배워 있는 첫 기술.</summary>
        public int unlockStage;
        /// <summary>설명 틀. {0} = 이 단계의 수치</summary>
        public string desc;

        public float ValueAt(int level) => value + perLevel * (Math.Max(1, level) - 1);
        public bool Starter => unlockStage == 0;
        public string Describe(int level) => string.Format(desc, ValueAt(level));
    }

    /// <summary>영웅 하나의 장착 칸 (Q·E). 빈 칸은 null 또는 "".</summary>
    [Serializable]
    public sealed class HeroLoadout
    {
        public HeroKind hero;
        public string q, e;
    }

    [Serializable]
    public sealed class HeroSkillLevel
    {
        public string id;
        public int level;
    }

    /// <summary>
    /// 영웅 종류·해금·기술 규칙 (docs/HERO.md, docs/SKILL_MENU.md).
    /// 영웅은 구역 보스 스테이지를 깨면 열린다. 기술은 단계마다 스킬 포인트 1점(플레이어 스킬트리와 같은 포인트), 최대 3단계.
    /// 첫 기술은 항상 1단계 이상이다(초기화해도 남는다 — 스킬트리의 시작 노드와 같다).
    /// </summary>
    public static class Heroes
    {
        public const int MaxSkillLevel = 3;
        public const int SkillCost = 1;

        public static readonly HeroKind[] All = { HeroKind.Sword, HeroKind.Gunner, HeroKind.Archer, HeroKind.Mage };

        public static string Name(HeroKind k) => k switch
        {
            HeroKind.Sword => "검사",
            HeroKind.Gunner => "거너",
            HeroKind.Archer => "궁수",
            _ => "마법사",
        };

        /// <summary>해금 스테이지 (0 = 처음부터). 구역 보스: 4 농장, 8 도심, 12 좀비 (docs/STORY_ZONES.md)</summary>
        public static int UnlockStage(HeroKind k) => k switch
        {
            HeroKind.Sword => 0,
            HeroKind.Gunner => 4,
            HeroKind.Archer => 8,
            _ => 12,
        };

        // ponytail: 디자인이 없어 몸 색으로만 구분 (docs/HERO.md). 모델이 생기면 모델로 바꾼다
        /// <summary>영웅 몸 색 (0~1 RGB)</summary>
        public static (float r, float g, float b) Rgb(HeroKind k) => k switch
        {
            HeroKind.Sword => (0.85f, 0.25f, 0.2f),
            HeroKind.Gunner => (0.25f, 0.5f, 0.95f),
            HeroKind.Archer => (0.3f, 0.75f, 0.3f),
            _ => (0.65f, 0.35f, 0.9f),
        };

        public static bool IsUnlocked(PlayerProfile p, HeroKind k) => UnlockStage(k) == 0 || p.IsCleared(UnlockStage(k));

        /// <summary>데려갈 영웅: 고른 영웅이 잠겨 있으면(기록이 바뀐 경우 등) 검사.</summary>
        public static HeroKind Chosen(PlayerProfile p) => IsUnlocked(p, p.hero) ? p.hero : HeroKind.Sword;

        /// <summary>이 스테이지를 처음 깨면 열리는 영웅들</summary>
        public static IEnumerable<HeroKind> UnlockedBy(int stage)
        {
            foreach (var k in All) if (UnlockStage(k) == stage && stage > 0) yield return k;
        }

        // ---------- 기술 ----------

        public static readonly HeroSkillDef[] Skills =
        {
            // 검사 (docs/SKILL_MENU.md)
            new HeroSkillDef { id = "sword.whirl", hero = HeroKind.Sword, name = "회오리 베기", cooldownSec = 8f, value = 40f, perLevel = 20f, unlockStage = 0, desc = "주변 3m 크립에게 피해 {0:0}" },
            new HeroSkillDef { id = "sword.dash", hero = HeroKind.Sword, name = "돌진", cooldownSec = 10f, value = 30f, perLevel = 15f, unlockStage = 2, desc = "앞으로 8m 돌진, 지나간 크립에게 피해 {0:0} + 옆으로 밀침 (3단계 쿨타임 −2초)" },
            new HeroSkillDef { id = "sword.cry", hero = HeroKind.Sword, name = "전투 함성", cooldownSec = 20f, value = 6f, perLevel = 2f, unlockStage = 3, desc = "주변 10m 포탑의 공격 간격 −25%, {0:0}초" },
            new HeroSkillDef { id = "sword.spear", hero = HeroKind.Sword, name = "창 던지기", cooldownSec = 12f, value = 150f, perLevel = 75f, unlockStage = 4, desc = "앞으로 창을 던져 처음 맞은 크립에게 피해 {0:0} (20m)" },
            // 거너 (docs/HERO_GUNNER.md)
            new HeroSkillDef { id = "gun.rapid", hero = HeroKind.Gunner, name = "속사", cooldownSec = 15f, value = 4f, perLevel = 1f, unlockStage = 0, desc = "{0:0}초 동안 연사 간격 절반, 탄을 쓰지 않는다" },
            new HeroSkillDef { id = "gun.shotgun", hero = HeroKind.Gunner, name = "산탄", cooldownSec = 8f, value = 50f, perLevel = 25f, unlockStage = 5, desc = "앞쪽 부채꼴(8m, 60°) 크립에게 피해 {0:0} + 뒤로 밀침" },
            new HeroSkillDef { id = "gun.bomb", hero = HeroKind.Gunner, name = "화약 폭탄", cooldownSec = 12f, value = 80f, perLevel = 40f, unlockStage = 6, desc = "조준 지점에 던져 1초 뒤 폭발, 반지름 4m 피해 {0:0}" },
            new HeroSkillDef { id = "gun.pierce", hero = HeroKind.Gunner, name = "관통탄", cooldownSec = 12f, value = 150f, perLevel = 75f, unlockStage = 7, desc = "조준선 방향 일직선 40m, 맞은 크립 전부에게 피해 {0:0}" },
        };

        public static HeroSkillDef Skill(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var s in Skills) if (s.id == id) return s;
            return null;
        }

        public static IEnumerable<HeroSkillDef> SkillsOf(HeroKind k)
        {
            foreach (var s in Skills) if (s.hero == k) yield return s;
        }

        /// <summary>배운 단계 (0 = 안 배움). 첫 기술은 최소 1.</summary>
        public static int LevelOf(PlayerProfile p, HeroSkillDef s)
        {
            int lv = 0;
            foreach (var x in p.heroSkills) if (x.id == s.id) lv = x.level;
            return s.Starter ? Math.Max(1, lv) : lv;
        }

        /// <summary>기술이 열렸는가 (영웅 해금 + 기술 해금 스테이지)</summary>
        public static bool SkillOpen(PlayerProfile p, HeroSkillDef s) =>
            IsUnlocked(p, s.hero) && (s.Starter || p.IsCleared(s.unlockStage));

        public static bool CanLearn(PlayerProfile p, HeroSkillDef s) =>
            s != null && SkillOpen(p, s) && LevelOf(p, s) < MaxSkillLevel && p.skillPoints >= SkillCost;

        /// <summary>배우기·강화 한 단계. 처음 배우면 빈 칸에 자동 장착.</summary>
        public static bool Learn(PlayerProfile p, HeroSkillDef s)
        {
            if (!CanLearn(p, s)) return false;
            int lv = LevelOf(p, s) + 1;
            p.skillPoints -= SkillCost;
            var entry = p.heroSkills.Find(x => x.id == s.id);
            if (entry == null) p.heroSkills.Add(entry = new HeroSkillLevel { id = s.id });
            entry.level = lv;
            if (lv == 1)
            {
                var l = Loadout(p, s.hero);
                if (string.IsNullOrEmpty(l.q)) l.q = s.id;
                else if (string.IsNullOrEmpty(l.e) && l.q != s.id) l.e = s.id;
            }
            return true;
        }

        /// <summary>이 영웅의 장착 칸. 없으면 만든다(첫 기술을 Q에).</summary>
        public static HeroLoadout Loadout(PlayerProfile p, HeroKind k)
        {
            var l = p.loadouts.Find(x => x.hero == k);
            if (l != null) return l;
            l = new HeroLoadout { hero = k, q = StarterOf(k)?.id ?? "" };
            p.loadouts.Add(l);
            return l;
        }

        private static HeroSkillDef StarterOf(HeroKind k)
        {
            foreach (var s in SkillsOf(k)) if (s.Starter) return s;
            return null;
        }

        /// <summary>slot 0 = Q, 1 = E. 배운 기술만. 같은 기술이 다른 칸에 있으면 그 칸을 비운다. id가 비면 칸을 비운다.</summary>
        public static bool Equip(PlayerProfile p, HeroKind k, int slot, string id)
        {
            var s = Skill(id);
            if (!string.IsNullOrEmpty(id) && (s == null || s.hero != k || LevelOf(p, s) == 0)) return false;
            var l = Loadout(p, k);
            if (slot == 0) { l.q = id ?? ""; if (l.e == id) l.e = ""; }
            else { l.e = id ?? ""; if (l.q == id) l.q = ""; }
            return true;
        }

        /// <summary>이 영웅의 기술을 모두 되돌린다 (무료). 첫 기술은 1단계로 남고 Q에 다시 들어간다. 돌려받은 포인트 반환.</summary>
        public static int Reset(PlayerProfile p, HeroKind k)
        {
            int refund = 0;
            foreach (var s in SkillsOf(k)) refund += LevelOf(p, s) - (s.Starter ? 1 : 0);
            p.heroSkills.RemoveAll(x => Skill(x.id)?.hero == k);
            p.loadouts.RemoveAll(x => x.hero == k);
            p.skillPoints += refund;
            return refund;
        }
    }
}
