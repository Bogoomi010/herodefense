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
        public void Difficulty_ScalesCount()
        {
            Assert.Less(MobDefs.DifficultyCountMul(Difficulty.Easy), 1f);
            Assert.Greater(MobDefs.DifficultyCountMul(Difficulty.Hard), 1f);
        }
    }
}
