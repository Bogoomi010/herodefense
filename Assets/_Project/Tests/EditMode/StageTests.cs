using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense.Core;

namespace TowerDefense.Tests
{
    /// <summary>스테이지: 별·보상·해금·보스 번호, 웨이브 15 클리어 조건 (docs/STAGE.md).</summary>
    public class StageTests
    {
        [Test]
        public void Stars_StartAtThree_MinusLeakAndOvertime_MinOneWhenCleared()
        {
            Assert.AreEqual(3, StageRules.Stars(true, 0, 100f, 120f));
            Assert.AreEqual(2, StageRules.Stars(true, 1, 100f, 120f));
            Assert.AreEqual(2, StageRules.Stars(true, 0, 121f, 120f));
            Assert.AreEqual(1, StageRules.Stars(true, 5, 999f, 120f));
            Assert.AreEqual(0, StageRules.Stars(false, 0, 10f, 120f), "클리어 못 하면 0");
        }

        [Test]
        public void Reward_FirstClearGivesSkillPoints_ReplayOnlyExp()
        {
            Assert.AreEqual((50, 1), StageRules.ClearReward(true, 50, 1));
            Assert.AreEqual((50, 0), StageRules.ClearReward(false, 50, 1));
        }

        [Test]
        public void BossStages_AreMultiplesOfFour()
        {
            Assert.IsFalse(StageRules.IsBoss(3));
            Assert.IsTrue(StageRules.IsBoss(4));
            Assert.IsTrue(StageRules.IsBoss(8));
            Assert.IsFalse(StageRules.IsBoss(9));
        }

        [Test]
        public void Unlock_IsSequential_AndBestStarsKept()
        {
            var p = new PlayerProfile();
            Assert.IsTrue(p.IsUnlocked(1));
            Assert.IsFalse(p.IsUnlocked(2));

            Assert.IsTrue(p.RecordClear(1, 1), "처음 클리어");
            Assert.IsTrue(p.IsUnlocked(2));
            Assert.IsFalse(p.RecordClear(1, 3), "다시 클리어");
            Assert.IsFalse(p.RecordClear(1, 2));
            Assert.AreEqual(3, p.StarsOf(1), "최고 별 유지");
            Assert.IsFalse(p.RecordClear(2, 0), "별 0은 클리어가 아님");
            Assert.IsFalse(p.IsUnlocked(3));
        }

        // ---------- 웨이브 ----------

        private sealed class FakeField : IWaveCallbacks
        {
            public int Alive, Spawned, BossSpawned, Cleared;
            public bool Won;
            public MobState Spawn(int round, bool boss)
            {
                Spawned++;
                if (boss) BossSpawned++;
                Alive++;
                return null;
            }
            public int MobCount() => Alive;
            public void RoundStart(int round, bool boss) { }
            public void RoundClear(int round) => Cleared++;
            public void Message(string text) { }
            public void Defeat(string reason) { }
            public void Victory() => Won = true;
        }

        private static void Run(WaveSystem w, float ms) { for (float t = 0; t < ms; t += 100f) w.Update(100f); }

        [Test]
        public void Wave_ClearsOnlyAfterLastWaveAndFieldEmpty()
        {
            var f = new FakeField();
            var w = new WaveSystem(f, waveCount: 3, mobsPerWave: 2);
            Run(w, 120_000f); // 크립을 하나도 안 잡으면 시간으로 웨이브가 넘어간다
            Assert.AreEqual(3, w.Round);
            Assert.AreEqual(6, f.Spawned);
            Assert.IsFalse(f.Won, "필드에 크립이 남아 있으면 클리어 아님");
            Assert.IsFalse(w.Done);

            f.Alive = 0;
            w.Update(100f);
            Assert.IsTrue(f.Won);
            Assert.IsTrue(w.Done);
            Assert.AreEqual(3, w.Round, "마지막 웨이브 뒤에 더 진행하지 않음");
        }

        [Test]
        public void Wave_BossStage_CreepsAndOneBoss_ClearsOnlyByKillingBoss()
        {
            var f = new FakeField();
            var w = new WaveSystem(f, waveCount: 2, mobsPerWave: 3, bossAtEnd: true);
            Run(w, 120_000f);
            Assert.AreEqual(1, f.BossSpawned, "보스 1기");
            Assert.AreEqual(3 + 3 + 1, f.Spawned, "마지막 웨이브는 크립 3 + 보스 1");

            f.Alive = 0; // 보스를 못 잡았는데 필드만 비어도
            w.Update(100f);
            Assert.IsFalse(f.Won, "보스를 잡아야만 클리어");

            f.Alive = 2; // 크립이 남아 있어도
            w.NotifyBossKilled();
            Assert.IsTrue(f.Won, "보스를 잡는 즉시 클리어");
            Assert.IsTrue(w.Done);
        }

        [Test]
        public void Wave_StartNowAndToSpawn_CountsBoss()
        {
            var f = new FakeField();
            var w = new WaveSystem(f, waveCount: 1, mobsPerWave: 3, bossAtEnd: true);
            Assert.AreEqual(0, w.ToSpawn, "휴식 중에는 0");
            w.StartNow();
            w.Update(1f);
            Assert.AreEqual(1, w.Round, "휴식을 건너뛰고 바로 시작");
            Assert.AreEqual(4, w.ToSpawn, "크립 3 + 보스 1");
            w.Update(1f);
            Assert.AreEqual(3, w.ToSpawn, "보스가 먼저 나옴");
            Run(w, 60_000f);
            Assert.AreEqual(0, w.ToSpawn);
        }

        [Test]
        public void StageHpMul_GrowsExponentially()
        {
            Assert.AreEqual(1f, StageRules.StageHpMul(1), 1e-4f);
            Assert.AreEqual(1.15f, StageRules.StageHpMul(2), 1e-4f);
            Assert.AreEqual(1.15f * 1.15f * 1.15f, StageRules.StageHpMul(4), 1e-4f);
        }

        [Test]
        public void CreepPool_RotatesByWave_AndFallsBack()
        {
            var pool = new List<string> { "pigeon", "trash", "rock" };
            Assert.AreEqual("pigeon", MobDefs.MobStatsFor(1, pool).Id);
            Assert.AreEqual("trash", MobDefs.MobStatsFor(2, pool).Id);
            Assert.AreEqual("rock", MobDefs.MobStatsFor(3, pool).Id);
            Assert.AreEqual("pigeon", MobDefs.MobStatsFor(4, pool).Id);
            // 한 웨이브 안에서 섞여 나온다: 웨이브 1의 0, 1, 2, 3번째 크립
            Assert.AreEqual("pigeon", MobDefs.MobStatsFor(1, pool, 0).Id);
            Assert.AreEqual("trash", MobDefs.MobStatsFor(1, pool, 1).Id);
            Assert.AreEqual("rock", MobDefs.MobStatsFor(1, pool, 2).Id);
            Assert.AreEqual("pigeon", MobDefs.MobStatsFor(1, pool, 3).Id);
            Assert.AreEqual("trash", MobDefs.MobStatsFor(2, pool, 0).Id, "웨이브마다 첫 종류가 바뀐다");
            Assert.AreEqual(MobDefs.MobStatsFor(5).Id, MobDefs.MobStatsFor(5, new List<string>()).Id, "빈 풀은 원본 규칙");
            Assert.AreEqual(MobDefs.MobStatsFor(5).Id, MobDefs.MobStatsFor(5, new List<string> { "nope" }).Id, "모르는 id도 원본 규칙");
            Assert.IsTrue(MobDefs.MobStatsFor(7, new List<string> { "trash_big" }).Splits);
            Assert.IsNotNull(MobDefs.Describe("pigeon_angry"));
            Assert.IsNotNull(MobDefs.Describe("boss"));
        }

        [Test]
        public void FarmCreeps_ModelsSplitAndBoss()
        {
            var farm = new List<string> { "sheep", "chicken", "cow", "pig" };
            Assert.AreEqual("Sheep", MobDefs.MobStatsFor(1, farm).Model);
            Assert.Greater(MobDefs.MobStatsFor(2, farm).Speed, MobDefs.MobStatsFor(2, new List<string> { "sheep" }).Speed, "닭이 양보다 빠름");
            Assert.Greater(MobDefs.MobStatsFor(3, farm).Armor, MobDefs.MobStatsFor(3, new List<string> { "sheep" }).Armor, "소가 양보다 단단함");
            var pig = new MobState(MobDefs.MobStatsFor(4, farm));
            Assert.IsTrue(pig.Splits);
            var piglet = MobDefs.SplitChildStats(pig);
            Assert.AreEqual("piglet", piglet.Id);
            Assert.AreEqual("Pig", piglet.Model);
            Assert.AreEqual("Bull", MobDefs.BossStats(15, "bull").Model);
            Assert.AreEqual("boss", MobDefs.BossStats(15).Id, "구역 보스가 없으면 기본 보스");
        }

        [Test]
        public void Difficulty_ScalesCount()
        {
            Assert.Less(MobDefs.DifficultyCountMul(Difficulty.Easy), 1f);
            Assert.Greater(MobDefs.DifficultyCountMul(Difficulty.Hard), 1f);
        }
    }
}
