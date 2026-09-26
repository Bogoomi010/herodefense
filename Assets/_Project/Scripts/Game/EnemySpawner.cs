using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Map;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        [Tooltip("게임 배속")]
        [Range(0.25f, 8f)] public float timeScale = 1f;
        [Tooltip("테스트용: 시작 웨이브 - 1 (예: 13 → 14웨이브부터)")]
        [Min(0)] public int skipRounds = 0;
        public bool showHud = true;

        // ---------- 세션 상태 ----------
        public int Death { get; private set; } = GameConfig.DeathStart;
        /// <summary>골드 경제 — 모든 골드 증감은 여기를 지난다 (docs/GOLD_SYSTEM.md)</summary>
        public Economy Economy { get; private set; }
        public int Gold => Economy.Gold;
        public int Kills { get; private set; }
        public int Leaked { get; private set; }
        public bool Over { get; private set; }
        public string LastMessage { get; private set; } = "";
        public float GameTimeMs { get; private set; }
        public WaveSystem Wave { get; private set; }
        public Mods Mods => Economy.Mods;
        public IReadOnlyList<EnemyView> Enemies => _enemies;

        // ---------- 플레이어 성장 (docs/PLAYER_SKILL_TREE.md) ----------
        public PlayerProfile Profile { get; private set; }
        /// <summary>스테이지 시작 시 찍혀 있던 스킬의 합. 스테이지 도중에는 바뀌지 않는다.</summary>
        public PlayerBonuses Bonuses { get; private set; }
        /// <summary>이번 스테이지에서 얻은 플레이어 경험치 (스킬 배율 적용 후). 정산 때 프로필에 더한다.</summary>
        public int StageExp { get; private set; }
        /// <summary>영웅이 필드에 내려가 있는가 (포탑 설치는 절벽 시점에서만)</summary>
        public bool HeroInField { get; set; }
        public TowerPlacer Placer { get; private set; }

        /// <summary>이 씬의 스테이지 번호와 데이터 (docs/STAGE.md)</summary>
        public int Stage { get; private set; }
        public StageDef Def { get; private set; }
        public bool Won { get; private set; }
        public int Stars { get; private set; }

        private string _settlement = "";
        private bool _recordable;

        private readonly List<EnemyView> _enemies = new List<EnemyView>();
        private PathFollower _path;
        private Transform _enemyRoot;
        private float _messageUntil;
        private string _result = "";

        private void Awake()
        {
            Profile = ProfileStore.LoadCurrent();
            // 목록에 없는 씬(테스트용)은 1스테이지 수치로 돌리되, 기록은 저장하지 않는다
            int n = StageFlow.StageOf(gameObject.scene.name);
            _recordable = n > 0 && ProfileStore.Exists(ProfileStore.Current);
            Stage = n > 0 ? n : 1;
            Def = (StageList.Instance != null ? StageList.Instance.Get(Stage) : null) ?? new StageDef();
            MobDefs.Difficulty = Profile.difficulty; // 저장 슬롯을 만들 때 고정
            MobDefs.StageHpMul = Def.hpMul;
            Bonuses = SkillTreeDef.LoadOrEmpty().Bonuses(Profile);
            Economy = new Economy(new Mods(), GameConfig.StartGold + Bonuses.StartGold);
        }

        private void Start()
        {
            if (map == null) map = FindFirstObjectByType<TileMap>();
            if (map.Grid == null) map.Generate();
            _path = new PathFollower(map.Waypoints);
            _enemyRoot = new GameObject("Enemies").transform;
            _enemyRoot.SetParent(transform, false);
            int perWave = Mathf.RoundToInt(GameConfig.MobsPerRound * MobDefs.DifficultyCountMul(Profile.difficulty));
            Wave = new WaveSystem(this, WaveSystem.StageWaves, perWave, StageRules.IsBoss(Stage));
            for (int i = 0; i < skipRounds; i++) Wave.SkipRound();
            Placer = GetComponent<TowerPlacer>();
            if (Placer == null) Placer = gameObject.AddComponent<TowerPlacer>();
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
                if (m.IsBoss)
                {
                    // 보스가 도착점에 닿으면 실패 (docs/STAGE.md 보스 스테이지)
                    GameOver(false, "보스가 도착했습니다");
                    return;
                }
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
            Economy.RewardKill(m, GameTimeMs, Bonuses.GoldMul);
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
            int bonus = Economy.RewardRoundClear(round, Bonuses.GoldMul);
            Message($"라운드 {round} 클리어 +{bonus}G");
        }

        public void Message(string text)
        {
            LastMessage = text;
            _messageUntil = GameTimeMs + 3000f;
            Debug.Log($"[EnemySpawner] {text}");
        }

        public void Defeat(string reason) => GameOver(false, reason);
        public void Victory() => GameOver(true, Wave.BossAtEnd ? "보스 처치" : $"{Wave.WaveCount}웨이브 방어 성공");

        private void GameOver(bool win, string reason)
        {
            if (Over) return;
            Over = true;
            Wave.Stop();
            Won = win;
            _result = win ? $"클리어 — {reason}" : $"실패 — {reason}";
            Debug.Log($"[EnemySpawner] {_result}");
            _settlement = Settle(win);
            if (Placer != null) Placer.SetActive(false);
        }

        private int CountNonGolden()
        {
            int n = 0;
            foreach (var e in _enemies) if (!e.State.Golden) n++;
            return n;
        }

        // ---------- 조회 ----------

        /// <summary>XZ 거리 기준 사거리 안 가장 가까운 살아 있는 몹. 없으면 null.</summary>
        public EnemyView NearestEnemy(Vector3 from, float range)
        {
            float best = range * range;
            EnemyView target = null;
            foreach (var e in _enemies)
            {
                if (e.State.Dead) continue;
                var p = e.transform.position;
                float dx = p.x - from.x, dz = p.z - from.z;
                float d2 = dx * dx + dz * dz;
                if (d2 > best) continue;
                best = d2;
                target = e;
            }
            return target;
        }

        /// <summary>나무 베기 등으로 얻은 플레이어 경험치 (스킬 배율 적용). 실제 증가량 반환.</summary>
        public int AddStageExp(int raw)
        {
            int gained = Mathf.RoundToInt(raw * Bonuses.ExpMul);
            StageExp += gained;
            return gained;
        }

        /// <summary>
        /// 스테이지 정산 (docs/STAGE.md): 별 계산, 클리어 기록·해금, 보상(처음 = 경험치 + 스킬 포인트, 다시 = 경험치만),
        /// 나무 경험치와 함께 프로필에 더하고 저장한다. 결과 문구 반환.
        /// </summary>
        private string Settle(bool win)
        {
            float elapsed = GameTimeMs / 1000f;
            Stars = StageRules.Stars(win, Leaked, elapsed, Def.starTimeSec);
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"{StarText(Stars)}   시간 {Clock(elapsed)} / 기준 {Clock(Def.starTimeSec)}   통과한 크립 {Leaked}");

            if (!_recordable)
            {
                lines.Append("테스트 실행 — 저장 슬롯이 없거나 목록에 없는 씬이라 기록하지 않음");
                return lines.ToString();
            }

            int exp = StageExp, points = 0;
            if (win)
            {
                bool first = Profile.RecordClear(Stage, Stars);
                var (rewardExp, rewardPoints) = StageRules.ClearReward(first, Def.clearExp, Def.clearSkillPoints);
                exp += Mathf.RoundToInt(rewardExp * Bonuses.ExpMul);
                points = rewardPoints;
                lines.AppendLine(first ? "처음 클리어 보상" : "다시 클리어 보상 (스킬 포인트 없음)");
            }

            int before = Profile.level;
            int levels = Profile.AddExp(exp);
            Profile.skillPoints += points;
            points += levels * PlayerProfile.PointsPerLevel;
            Profile.tutorialDone = true; // 임시: 첫 스테이지를 끝내면 튜토리얼 완료
            try { ProfileStore.SaveCurrent(Profile); }
            catch (System.Exception e) { Debug.LogError($"[EnemySpawner] 저장 실패: {e}"); return "저장 실패 — 로그를 확인하세요"; }

            lines.Append($"경험치 +{exp}" + (points > 0 ? $"   스킬 포인트 +{points}" : ""));
            lines.Append(levels > 0 ? $"   레벨 {before} → {Profile.level}" : $"   (다음 레벨까지 {PlayerProfile.ExpToNext(Profile.level) - Profile.exp})");
            return lines.ToString();
        }

        private static string StarText(int n) => new string('★', n) + new string('☆', 3 - n);
        private static string Clock(float sec) => $"{(int)sec / 60}:{(int)sec % 60:00}";

        // ---------- HUD ----------

        private static readonly Rect HudRect = new Rect(10, 10, 520, 200);

        /// <summary>마우스(스크린 좌표, 좌하단 원점)가 HUD 위에 있는가 — HUD 클릭이 설치 클릭으로 새지 않게.</summary>
        public bool IsOverHud(Vector2 mouse) => showHud && HudRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));

        // ponytail: 베타 HUD는 IMGUI. 레이아웃이 확정되면 UI Toolkit으로 옮긴다
        private void OnGUI()
        {
            if (!showHud || Wave == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;
            var btn = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            GUI.Box(HudRect, GUIContent.none);
            GUILayout.BeginArea(new Rect(HudRect.x + 10, HudRect.y + 6, HudRect.width - 20, HudRect.height - 10));
            string state = Wave.State == WaveSystem.WaveState.Break ? "휴식" : "진행";
            string wave = Wave.IsLastWave ? "(마지막)" : $"[{state}] {Wave.TimeLeftSec}s";
            GUILayout.Label($"스테이지 {Stage}{(Wave.BossAtEnd ? " 보스" : "")}  웨이브 {Wave.Round}/{Wave.WaveCount}  {wave}", style);
            int streak = Economy.StreakAt(GameTimeMs);
            GUILayout.Label($"몹 {_enemies.Count}  처치 {Kills}  진입 {Leaked}  데스 {Death}  골드 {Gold}" +
                            (streak >= Events.StreakMin ? $"  🔥{streak}" : ""), style);
            GUILayout.Label($"Lv {Profile.level}  경과 {Clock(GameTimeMs / 1000f)} / 별 기준 {Clock(Def.starTimeSec)}  ×{timeScale:0.#}", style);
            if (GameTimeMs < _messageUntil) GUILayout.Label(LastMessage, style);

            GUILayout.BeginHorizontal();
            if (Placer != null)
            {
                GUI.enabled = Placer.CanEnter;
                string label = Placer.Active ? $"설치 중: {TowerPlacer.Describe(Placer.LastResult)} (우클릭 취소)" : $"포탑 설치 {Placer.spec.cost}G (B)";
                if (GUILayout.Button(label, btn, GUILayout.Height(30))) Placer.Toggle();
                GUI.enabled = true;
            }
            // ponytail: 베타 테스트용 — 15웨이브를 다 돌지 않고 정산을 보려고 (실패로 처리)
            if (!Over && GUILayout.Button("스테이지 종료", btn, GUILayout.Width(120), GUILayout.Height(30))) GameOver(false, "스테이지 종료");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (Over) DrawSettlement(style, btn);
        }

        private void DrawSettlement(GUIStyle style, GUIStyle btn)
        {
            var r = new Rect(Screen.width / 2f - 320, Screen.height / 2f - 130, 640, 260);
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 20, r.y + 16, r.width - 40, r.height - 30));
            GUILayout.Label("스테이지 정산", style);
            GUILayout.Label(_result, style);
            GUILayout.Label(_settlement, style);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            bool hasNext = StageList.Instance != null && Stage < StageList.Instance.Count;
            if (Won && hasNext && GUILayout.Button("다음 스테이지", btn, GUILayout.Height(40))) StageFlow.LoadStage(Stage + 1);
            if (!Won && GUILayout.Button("다시 하기", btn, GUILayout.Height(40))) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            if (GUILayout.Button("스테이지 선택", btn, GUILayout.Height(40))) StageFlow.LoadStageSelect();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
