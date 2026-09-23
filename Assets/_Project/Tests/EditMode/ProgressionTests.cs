using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense.Core;

namespace TowerDefense.Tests
{
    /// <summary>플레이어 성장(레벨업·스킬트리·보너스)과 포탑 설치 판정.</summary>
    public class ProgressionTests
    {
        // ---------- 레벨업 ----------

        [Test]
        public void AddExp_LevelsUp_AndGrantsPoints_CarryingOverflow()
        {
            var p = new PlayerProfile();
            int need = PlayerProfile.ExpToNext(1) + PlayerProfile.ExpToNext(2);
            Assert.AreEqual(2, p.AddExp(need + 7));
            Assert.AreEqual(3, p.level);
            Assert.AreEqual(2, p.skillPoints);
            Assert.AreEqual(7, p.exp);
            Assert.AreEqual(0, p.AddExp(0));
        }

        // ---------- 스킬트리 ----------

        // start ─ a ─ b      c (고립)
        private static SkillTree Tree() => new SkillTree(new List<SkillNode>
        {
            new SkillNode { id = "start", start = true, effect = SkillEffect.StartGold, value = 10 },
            new SkillNode { id = "a", effect = SkillEffect.ZoneShrinkPct, value = 0.1f, links = { "start" } },
            new SkillNode { id = "b", effect = SkillEffect.ZoneShrinkPct, value = 0.1f, links = { "a" } },
            new SkillNode { id = "c", effect = SkillEffect.GoldPct, value = 0.2f },
        });

        [Test]
        public void SkillTree_OpensChain_RejectsLocked_ResetRefunds()
        {
            var t = Tree();
            var p = new PlayerProfile { skillPoints = 2 };

            Assert.AreEqual(NodeState.Taken, t.StateOf(p, "start"));
            Assert.AreEqual(NodeState.Open, t.StateOf(p, "a"));
            Assert.AreEqual(NodeState.Locked, t.StateOf(p, "b"));
            Assert.IsFalse(t.Take(p, "b"), "잠긴 노드");
            Assert.IsFalse(t.Take(p, "c"), "이어지지 않은 노드");

            Assert.IsTrue(t.Take(p, "a"));
            Assert.AreEqual(NodeState.Open, t.StateOf(p, "b"), "연쇄 개방 (링크는 양방향)");
            Assert.IsTrue(t.Take(p, "b"));
            Assert.IsFalse(t.Take(p, "b"), "이미 찍음");
            Assert.AreEqual(0, p.skillPoints);

            var bonus = t.Bonuses(p);
            Assert.AreEqual(10, bonus.StartGold, "시작 노드 효과 포함");
            Assert.AreEqual(0.8f, bonus.ZoneMul, 1e-5f);

            t.Reset(p);
            Assert.AreEqual(2, p.skillPoints);
            Assert.IsEmpty(p.nodes);
            Assert.AreEqual(NodeState.Taken, t.StateOf(p, "start"), "시작 노드는 남는다");
        }

        [Test]
        public void SkillTree_NeedsPoint()
        {
            var t = Tree();
            var p = new PlayerProfile();
            Assert.IsFalse(t.Take(p, "a"));
        }

        // ---------- 설치 판정 ----------

        private const float Foot = 0.4f, Zone = 1.5f;
        private static readonly Circle[] None = new Circle[0];
        private static bool AllGround(float x, float z) => true;

        private static PlaceResult Check(float x, float z, int gold = 100, System.Func<float, float, bool> ground = null,
            Circle[] trees = null, Circle[] towers = null) =>
            PlacementRules.Check(x, z, Foot, Zone, gold, 50, ground ?? AllGround, trees ?? None, towers ?? None);

        [Test]
        public void Place_Ok_OnOpenGround() => Assert.AreEqual(PlaceResult.Ok, Check(0, 0));

        [Test]
        public void Place_NoGold() => Assert.AreEqual(PlaceResult.NoGold, Check(0, 0, gold: 49));

        [Test]
        public void Place_FootprintEdgeOnPath_Rejected()
        {
            // x ≥ 0.3 은 경로: 중심은 녹색이지만 바닥 가장자리(x = 0.4)가 걸친다
            Assert.AreEqual(PlaceResult.NotGround, Check(0, 0, ground: (x, z) => x < 0.3f));
            Assert.AreEqual(PlaceResult.Ok, Check(-0.2f, 0, ground: (x, z) => x < 0.3f));
        }

        [Test]
        public void Place_OverlappingTree_Rejected()
        {
            var trees = new[] { new Circle(0.7f, 0, 0.4f) };
            Assert.AreEqual(PlaceResult.Tree, Check(0, 0, trees: trees));
            Assert.AreEqual(PlaceResult.Ok, Check(-0.1f, 0, trees: trees), "바닥끼리 맞닿음");
        }

        [Test]
        public void Place_ZonesTouching_Ok_Overlapping_Rejected()
        {
            var towers = new[] { new Circle(3f, 0, 0) };
            Assert.AreEqual(PlaceResult.Ok, Check(0, 0, towers: towers), "거리 = 2R");
            Assert.AreEqual(PlaceResult.TooClose, Check(0.1f, 0, towers: towers));
        }

        [Test]
        public void ZoneRadius_ShrinksWithSkill_ButNotBelowFootprint()
        {
            Assert.AreEqual(1.2f, PlacementRules.ZoneRadius(1.5f, 0.8f, Foot), 1e-5f);
            Assert.AreEqual(Foot, PlacementRules.ZoneRadius(1.5f, 0.1f, Foot), 1e-5f);
        }
    }
}
