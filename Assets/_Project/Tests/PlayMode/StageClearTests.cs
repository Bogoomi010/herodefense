using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense.Core;
using TowerDefense.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerDefense.Tests
{
    /// <summary>
    /// 스테이지를 실제로 돌려 클리어되는지 본다. 새 저장 슬롯(스킬 없음, 보통 난이도) + 포탑만 쓰는 자동 플레이어, 영웅은 쓰지 않는다.
    /// 자동 플레이어: 골드가 되면 길을 가장 많이 덮는 자리에 포탑을 세우고(최대 10개), 다 세우면 기본 포탑을 업그레이드한다.
    /// 농장 구역(1~4)은 쉬워야 한다: 영웅 없이도 크립이 하나도 통과하지 않아야 통과.
    /// ponytail: 업그레이드는 영웅이 가서 기다리는 시간 없이 바로 올린다. 영웅 없이도 깨지면 실제 플레이는 더 여유 있다.
    /// </summary>
    public sealed class StageClearTests
    {
        private const float TimeScale = 5f;
        // 세울 순서: 잠긴 종류는 기본 포탑으로 바꾼다
        private static readonly TowerKind[] BuildOrder =
        {
            TowerKind.Basic, TowerKind.Catapult, TowerKind.Basic, TowerKind.Frost, TowerKind.Ballista,
            TowerKind.Basic, TowerKind.Catapult, TowerKind.Ballista, TowerKind.Frost, TowerKind.Basic,
        };

        private int _errors;
        private void CountErrors(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) { _errors++; if (_errors <= 5) Debug.Log($"[StageClear] 오류 기록: {msg}"); }
        }

        [SetUp] public void SetUp() { ProfileStore.Sandbox = true; _errors = 0; Application.logMessageReceived += CountErrors; }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Application.logMessageReceived -= CountErrors;
            ProfileStore.Sandbox = false;
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator Stage_IsClearable([Values(1, 2, 3, 4)] int stage)
        {
            LogAssert.ignoreFailingMessages = true; // 오류는 세어서 따로 보고한다
            var def = StageList.Instance.Get(stage);
            Assert.IsNotNull(def, $"스테이지 {stage}가 목록에 없음");
            Debug.Log($"[StageClear] 스테이지 {stage} 씬 불러오는 중: {def.sceneName}");
            SceneManager.LoadScene(def.sceneName);
            EnemySpawner s = null;
            while (s == null || s.Wave == null || s.Placer == null) { yield return null; s = Object.FindFirstObjectByType<EnemySpawner>(); }

            var spots = new Dictionary<TowerKind, List<Vector3>>();
            Time.timeScale = TimeScale;
            float nextThink = 0f, nextBeat = 0f;
            while (!s.Over)
            {
                if (s.GameTimeMs >= nextThink)
                {
                    nextThink = s.GameTimeMs + 500f;
                    Think(s, spots);
                }
                if (Time.realtimeSinceStartup >= nextBeat)
                {
                    nextBeat = Time.realtimeSinceStartup + 20f;
                    Debug.Log($"[StageClear] 스테이지 {stage} 진행: 웨이브 {s.Wave.Round} 시간 {s.GameTimeMs / 1000f:0}s 골드 {s.Gold} 포탑 {Tower.All.Count} 필드 {s.MobCount()} 데스 {s.Death}");
                }
                yield return null;
            }
            Time.timeScale = 1f;

            int spent = s.Economy.TotalFrom(GoldSource.TowerBuild) + s.Economy.TotalFrom(GoldSource.TowerUpgrade);
            string kinds = string.Join(",", System.Linq.Enumerable.Select(Tower.All, t => t.Spec.kind.ToString()));
            Debug.Log($"[StageClear] 스테이지 {stage}: {(s.Won ? "클리어" : "실패")} 웨이브 {s.Wave.Round}/{s.Wave.WaveCount} 시간 {s.GameTimeMs / 1000f:0}s " +
                      $"처치 {s.Kills} 통과 {s.Leaked} 남은 데스 {s.Death} 별 {s.Stars} 남은 골드 {s.Gold} 쓴 골드 {spent} 포탑 [{kinds}] 오류 로그 {_errors}");
            Assert.IsTrue(s.Won, $"스테이지 {stage} 실패: {s.LastMessage} (웨이브 {s.Wave.Round}, 통과 {s.Leaked})");
            Assert.AreEqual(0, s.Leaked, $"스테이지 {stage}: 농장 구역인데 크립 {s.Leaked}마리가 통과했다");
        }

        private static void Think(EnemySpawner s, Dictionary<TowerKind, List<Vector3>> spots)
        {
            var placer = s.Placer;
            if (Tower.All.Count < PlacementRules.MaxTowers)
            {
                var kind = BuildOrder[Tower.All.Count];
                int i = placer.specs.FindIndex(x => x.kind == kind);
                if (!placer.IsUnlocked(i)) i = 0;
                placer.selected = i;
                if (s.Gold < placer.Selected.cost) return; // 다음 포탑을 모은다
                if (!spots.TryGetValue(placer.Selected.kind, out var list)) spots[placer.Selected.kind] = list = RankSpots(s, placer.Selected.rangePx * GameConfig.PxToWorld);
                foreach (var p in list)
                {
                    var r = placer.TryPlace(p);
                    if (r == PlaceResult.Ok || r == PlaceResult.NoGold || r == PlaceResult.TooMany) return;
                }
                return;
            }
            // 다 세웠으면 가장 싼 업그레이드부터
            Tower best = null;
            UpgradeBranch bestB = UpgradeBranch.Power;
            int bestCost = int.MaxValue;
            foreach (var t in Tower.All)
                foreach (var b in new[] { UpgradeBranch.Power, UpgradeBranch.AtkSpeed })
                {
                    int c = t.NextCost(b);
                    if (c > 0 && c < bestCost) { best = t; bestB = b; bestCost = c; }
                }
            if (best != null && s.Economy.TrySpend(bestCost, GoldSource.TowerUpgrade)) best.ApplyUpgrade(bestB);
        }

        /// <summary>1m 격자 후보를 사거리 안 길 길이 순으로 정렬한다.</summary>
        private static List<Vector3> RankSpots(EnemySpawner s, float range)
        {
            var path = new List<Vector3>();
            for (float d = 0f; d <= s.Path.Total; d += 1f) path.Add(s.Path.PosAt(d));
            var o = s.map.transform.position;
            float w = s.map.width * s.map.TileSize, h = s.map.height * s.map.TileSize, r2 = range * range;
            var scored = new List<(float score, Vector3 p)>();
            for (float x = o.x + 0.5f; x < o.x + w; x += 1f)
                for (float z = o.z + 0.5f; z < o.z + h; z += 1f)
                {
                    var p = new Vector3(x, 0f, z);
                    if (!s.map.IsGround(p)) continue;
                    int n = 0;
                    foreach (var q in path) { float dx = q.x - x, dz = q.z - z; if (dx * dx + dz * dz <= r2) n++; }
                    if (n > 0) scored.Add((n, new Vector3(x, s.GroundY(p), z)));
                }
            scored.Sort((a, b) => b.score.CompareTo(a.score));
            return scored.ConvertAll(e => e.p);
        }
    }
}
