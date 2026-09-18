using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Map;
using UnityEngine;

namespace TowerDefense.Game
{
    /// <summary>
    /// 적군 이동 테스트용 세션. 원본 GameScene의 몹 관련 로직(스폰·수용 한계·피해·분열·황금 비둘기·보스)을 담고,
    /// 새 규칙 하나를 더한다: 몹이 도착 지점에 닿으면 소멸하며 데스 카운트 -1 (황금 비둘기는 벌 없음).
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour, IWaveCallbacks
    {
        [Header("참조")]
        public TileMap map;
        [Tooltip("몹 모델 프리팹 (발밑 원점, +Z 정면). 비우면 캡슐 프리미티브")]
        public GameObject enemyPrefab;

        [Header("설정")]
        public Difficulty difficulty = Difficulty.Normal;
        [Tooltip("게임 배속")]
        [Range(0.25f, 8f)] public float timeScale = 1f;
        [Tooltip("시작 라운드 - 1 (예: 9 → 10라운드 보스부터 시작)")]
        [Min(0)] public int skipRounds = 0;
        [Tooltip("경로 옆에 자동 배치할 테스트 타워 수")]
        [Min(0)] public int testTowers = 3;
        public bool showHud = true;

        // ---------- 세션 상태 ----------
        public int Death { get; private set; } = GameConfig.DeathStart;
        /// <summary>골드 경제 — 모든 골드 증감은 여기를 지난다 (docs/GOLD_SYSTEM.md)</summary>
        public Economy Economy { get; } = new Economy(new Mods());
        public int Gold => Economy.Gold;
        public int Kills { get; private set; }
        public int Leaked { get; private set; }
        public bool Over { get; private set; }
        public string LastMessage { get; private set; } = "";
        public float GameTimeMs { get; private set; }
        public WaveSystem Wave { get; private set; }
        public Mods Mods => Economy.Mods;
        public IReadOnlyList<EnemyView> Enemies => _enemies;

        private readonly List<EnemyView> _enemies = new List<EnemyView>();
        private PathFollower _path;
        private Transform _enemyRoot;
        private float _messageUntil;
        private string _result = "";

        private void Start()
        {
            if (map == null) map = FindFirstObjectByType<TileMap>();
            if (map.Grid == null) map.Generate();
            MobDefs.Difficulty = difficulty;
            _path = new PathFollower(map.Waypoints);
            _enemyRoot = new GameObject("Enemies").transform;
            _enemyRoot.SetParent(transform, false);
            Wave = new WaveSystem(this);
            for (int i = 0; i < skipRounds; i++) Wave.SkipRound();
            PlaceTestTowers();
        }

        private void Update()
        {
            if (Over) return;
            float d = Time.deltaTime * 1000f * timeScale;
            GameTimeMs += d;
            Wave.Update(d);
            if (Over) return;

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var e = _enemies[i];
                var m = e.State;
                e.Tick(GameTimeMs, d, Mods.MobSpeedMul);

                // 도트
                if (Combat.DotTick(m, GameTimeMs, d))
                {
                    OnKilled(e);
                    continue;
                }

                if (!e.ReachedEnd) continue;

                if (m.Golden)
                {
                    // ✨ 놓쳐도 벌 없음
                    Remove(e);
                    continue;
                }
                Leaked++;
                Death--;
                Remove(e);
                Message($"몹이 도시에 진입! 데스 -1 (남은 데스 {Death})");
                if (Death <= 0)
                {
                    GameOver(false, "라인이 붕괴되었습니다");
                    return;
                }
            }
        }

        // ---------- 피해 ----------

        /// <summary>타워 등이 호출. 처치 시 true.</summary>
        public bool Damage(EnemyView e, float amount, DmgType type)
        {
            if (Over || e == null || e.State.Dead) return false;
            if (!Combat.Damage(e.State, amount, type, GameTimeMs, Mods)) return false;
            OnKilled(e);
            return true;
        }

        private void OnKilled(EnemyView e)
        {
            var m = e.State;
            Kills++;
            Economy.RewardKill(m, GameTimeMs); // 스킬 killGoldMul은 유닛 전투 이식(M4) 때 전달
            Remove(e);

            // 🗑 분열: 쓰레기 상위 몹은 죽으면 봉투 2개로 갈라진다
            if (m.Splits)
            {
                foreach (float off in new[] { -16f, 16f })
                    AddEnemy(new MobState(MobDefs.SplitChildStats(m), Mathf.Max(0f, m.Dist + off)));
            }

            if (m.IsBoss)
            {
                Message($"보스 {m.Name} 처치!");
                Wave.NotifyBossKilled();
            }
        }

        private void Remove(EnemyView e)
        {
            _enemies.Remove(e);
            if (e != null) Destroy(e.gameObject);
        }

        private EnemyView AddEnemy(MobState state)
        {
            var view = EnemyView.Create(state, _path, _enemyRoot, enemyPrefab);
            _enemies.Add(view);
            return view;
        }

        // ---------- IWaveCallbacks ----------

        public MobState Spawn(int round, bool boss)
        {
            if (Over) return null;
            // ✨ 황금 비둘기 — 스폰당 0.25% (라운드당 ~5%)
            if (!boss && Random.value < GameConfig.GoldenChance)
            {
                AddEnemy(new MobState(MobDefs.GoldenStats(round)));
                Message("✨ 황금 비둘기 출현! 잡으면 골드 ×10");
            }
            if (!boss && CountNonGolden() >= GameConfig.MobCap)
            {
                Death--;
                Message($"수용 한계 초과! 데스 -1 (남은 데스 {Death})");
                if (Death <= 0)
                {
                    GameOver(false, "라인이 붕괴되었습니다");
                    return null;
                }
            }
            var stats = boss ? MobDefs.BossStats(round) : MobDefs.MobStatsFor(round);
            return AddEnemy(new MobState(stats)).State;
        }

        public int MobCount() => _enemies.Count;

        public void RoundStart(int round, bool boss)
        {
            Economy.OnRoundStart(); // 변이 라운드 배율 초기화 (변이 자체는 도파민 시스템 이식 때)
            Message(boss ? $"라운드 {round} — 보스 {MobDefs.BossDefFor(round).Name} ({MobDefs.BossDefFor(round).Trait})" : $"라운드 {round} — {MobDefs.MobStatsFor(round).Name}");
        }

        public void RoundClear(int round)
        {
            int bonus = Economy.RewardRoundClear(round);
            Message($"라운드 {round} 클리어 +{bonus}G");
        }

        public void Message(string text)
        {
            LastMessage = text;
            _messageUntil = GameTimeMs + 3000f;
            Debug.Log($"[EnemySpawner] {text}");
        }

        public void Defeat(string reason) => GameOver(false, reason);
        public void Victory() => GameOver(true, "40라운드 방어 성공");

        private void GameOver(bool win, string reason)
        {
            if (Over) return;
            Over = true;
            Wave.Stop();
            _result = win ? $"승리 — {reason}" : $"패배 — {reason}";
            Debug.Log($"[EnemySpawner] {_result}");
        }

        private int CountNonGolden()
        {
            int n = 0;
            foreach (var e in _enemies) if (!e.State.Golden) n++;
            return n;
        }

        // ---------- 테스트 타워 ----------

        /// <summary>경로를 N등분한 지점마다 가장 가까운 지형 타일에 큐브 타워를 놓는다.</summary>
        private void PlaceTestTowers()
        {
            if (testTowers <= 0) return;
            var grid = map.Grid;
            var used = new HashSet<Vector2Int>();
            for (int k = 1; k <= testTowers; k++)
            {
                int idx = Mathf.Clamp(Mathf.RoundToInt((float)k / (testTowers + 1) * (grid.Path.Count - 1)), 0, grid.Path.Count - 1);
                var p = grid.Path[idx];
                Vector2Int? best = null;
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
                                          new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) })
                {
                    var q = p + d;
                    if (!grid.InBounds(q) || grid.IsWalkable(q) || used.Contains(q)) continue;
                    best = q;
                    break;
                }
                if (best == null) continue;
                used.Add(best.Value);
                TestTower.Create(this, map, best.Value, k);
            }
        }

        // ---------- HUD ----------

        private void OnGUI()
        {
            if (!showHud || Wave == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;
            var box = new GUIStyle(GUI.skin.box);
            GUI.Box(new Rect(10, 10, 520, 150), GUIContent.none, box);
            GUILayout.BeginArea(new Rect(20, 16, 500, 140));
            string state = Wave.State == WaveSystem.WaveState.Break ? "휴식" : "진행";
            GUILayout.Label($"라운드 {Wave.Round}/{GameConfig.RoundMax}  [{state}]  남은 시간 {Wave.TimeLeftSec}s  ×{timeScale:0.#}", style);
            int streak = Economy.StreakAt(GameTimeMs);
            GUILayout.Label($"몹 {_enemies.Count}  처치 {Kills}  진입 {Leaked}  데스 {Death}  골드 {Gold}" +
                            (streak >= Events.StreakMin ? $"  🔥{streak}" : ""), style);
            if (GameTimeMs < _messageUntil) GUILayout.Label(LastMessage, style);
            if (Over) GUILayout.Label(_result, style);
            GUILayout.EndArea();
        }
    }
}
