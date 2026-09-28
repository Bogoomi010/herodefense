using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Map;
using UnityEngine;
using UnityEngine.InputSystem;
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
        public PathFollower Path => _path;
        /// <summary>크립이 퍼져 걸을 수 있는 길 반폭 (m). 타일 반폭보다 조금 좁게 — 몸이 길 밖으로 삐져나오지 않게.</summary>
        public float PathHalfWidth { get; private set; }

        [Header("크립 무리 이동")]
        [Tooltip("크립끼리 이 거리(m) 안으로 붙지 않게 밀어낸다")]
        public float creepSpacing = 1.1f;
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
            MobDefs.StageHpMul = StageRules.StageHpMul(Stage) * Def.hpMul;
            Bonuses = SkillTreeDef.LoadOrEmpty().Bonuses(Profile);
            Economy = new Economy(new Mods(), GameConfig.StartGold + Bonuses.StartGold);
        }

        private void Start()
        {
            if (map == null) map = FindFirstObjectByType<TileMap>();
            if (map.Grid == null) map.Generate();
            _path = new PathFollower(map.Waypoints);
            PathHalfWidth = map.TileSize * 0.5f * 0.8f;
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
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && !HeroInField) Wave.StartNow(); // 필드 시점의 Space는 영웅 점프
            Wave.Update(d);
            if (Over) return;

            Separate();
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var e = _enemies[i];
                var m = e.State;
                e.Tick(GameTimeMs, d, Mods.MobSpeedMul);

                // 도트
                float hpBefore = m.Hp;
                if (Combat.DotTick(m, GameTimeMs, d))
                {
                    OnKilled(e, poison: true);
                    continue;
                }
                if (m.Hp < hpBefore) e.Flash(poison: true); // 독: 초록

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
            if (!Combat.Damage(e.State, amount, type, GameTimeMs, Mods))
            {
                e.Flash(poison: false); // 직접 피해: 빨강
                return false;
            }
            OnKilled(e);
            return true;
        }

        private void OnKilled(EnemyView e, bool poison = false)
        {
            var m = e.State;
            Kills++;
            Economy.RewardKill(m, GameTimeMs, Bonuses.GoldMul);
            _enemies.Remove(e);
            if (e != null) e.Die(poison); // 막타도 번쩍인 뒤 사라진다 (독에 죽으면 초록)

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

        // 크립 모델: Resources/Creeps/<Model> 프리팹. 없으면 씬의 기본 모델(enemyPrefab)
        private readonly Dictionary<string, GameObject> _models = new Dictionary<string, GameObject>();

        private GameObject ModelFor(string model)
        {
            if (string.IsNullOrEmpty(model)) return enemyPrefab;
            if (!_models.TryGetValue(model, out var go)) _models[model] = go = Resources.Load<GameObject>($"Creeps/{model}");
            return go != null ? go : enemyPrefab;
        }

        private EnemyView AddEnemy(MobState state)
        {
            var view = EnemyView.Create(state, this, _enemyRoot, ModelFor(state.Model));
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
            var stats = boss ? MobDefs.BossStats(round, Def.boss) : MobDefs.MobStatsFor(round, Def.creeps, _spawnInRound++);
            return AddEnemy(new MobState(stats)).State;
        }

        public int MobCount() => _enemies.Count;

        private int _spawnInRound; // 이번 웨이브에서 나온 크립 수 — 섞어 내보낼 차례

        public void RoundStart(int round, bool boss)
        {
            Economy.OnRoundStart(); // 변이 라운드 배율 초기화 (변이 자체는 도파민 시스템 이식 때)
            _spawnInRound = 0;
            // 이 웨이브에 섞여 나오는 종류 (풀 크기만큼 돌면 전부 나온다)
            var names = new List<string>();
            for (int i = 0; i < Mathf.Max(1, Def.creeps.Count); i++)
            {
                var c = MobDefs.MobStatsFor(round, Def.creeps, i);
                if (!names.Contains(c.Name)) names.Add(c.Name);
                Introduce(c.Id);
            }
            var bossDef = MobDefs.BossStats(round, Def.boss);
            Message(boss ? $"라운드 {round} — 보스 {bossDef.Name}" : $"라운드 {round} — {string.Join("·", names)}");
            if (boss) Introduce(bossDef.Id);
        }

        // ---------- 새 크립 소개 카드 (docs/INGAME_UI.md) ----------

        private readonly List<string> _intros = new List<string>();
        private float _introUntil;
        private const float IntroSec = 5f;

        /// <summary>이 저장 슬롯에서 처음 만나는 크립이면 소개 카드를 줄 세운다. 만난 기록은 정산 때 프로필과 함께 저장된다.</summary>
        private void Introduce(string id)
        {
            if (string.IsNullOrEmpty(id) || Profile.seenCreeps.Contains(id) || MobDefs.Describe(id) == null) return;
            Profile.seenCreeps.Add(id);
            _intros.Add(id);
            if (_intros.Count == 1) _introUntil = GameTimeMs + IntroSec * 1000f;
        }

        private void DrawIntro()
        {
            if (_intros.Count == 0) return;
            if (GameTimeMs >= _introUntil)
            {
                _intros.RemoveAt(0);
                if (_intros.Count == 0) return;
                _introUntil = GameTimeMs + IntroSec * 1000f;
            }
            var (name, desc) = MobDefs.Describe(_intros[0]).Value;
            var r = Hud.IntroCard;
            Hud.Panel(r, 0.85f);
            Hud.Frame(r, Hud.Gold);
            GUI.Label(new Rect(r.x + 14, r.y + 6, r.width - 28, 22), $"<color={Hud.Hex(Hud.Gold)}>새 크립</color>", Hud.Text(14));
            GUI.Label(new Rect(r.x + 14, r.y + 28, r.width - 28, 26), name, Hud.Text(19));
            GUI.Label(new Rect(r.x + 14, r.y + 54, r.width - 28, 30), desc, Hud.Text(14, TextAnchor.MiddleLeft, wrap: true));
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

        // ---------- 크립 무리 이동·넉백 (docs/CREEP_MOVEMENT.md) ----------

        /// <summary>
        /// 길 위 크립끼리 겹치지 않게 (진행 거리, 옆 위치) 평면에서 서로 밀어낸다.
        /// ponytail: O(n²) — 필드 크립 수 상한(50)에서는 충분하다. 수백 마리가 되면 진행 거리로 정렬해 이웃만 본다.
        /// </summary>
        private void Separate()
        {
            float r = creepSpacing, r2 = r * r;
            for (int i = 0; i < _enemies.Count; i++)
            {
                var a = _enemies[i];
                if (a.Mode != EnemyView.Move.Path) continue;
                for (int j = i + 1; j < _enemies.Count; j++)
                {
                    var b = _enemies[j];
                    if (b.Mode != EnemyView.Move.Path) continue;
                    float da = a.DistWorld - b.DistWorld, dl = a.Lateral - b.Lateral;
                    float d2 = da * da + dl * dl;
                    if (d2 >= r2) continue;
                    float d = Mathf.Sqrt(d2);
                    if (d < 1e-4f) { dl = Random.Range(-1f, 1f); da = 0f; d = Mathf.Abs(dl) + 1e-4f; }
                    float push = (r - d) * 0.5f / d;
                    a.State.Dist += da * push * GameConfig.WorldToPx;
                    b.State.Dist -= da * push * GameConfig.WorldToPx;
                    a.Lateral += dl * push;
                    b.Lateral -= dl * push;
                    a.ClampLateral();
                    b.ClampLateral();
                }
            }
        }

        /// <summary>
        /// 폭발 넉백: center 반경 radius 안 크립이 바깥으로 날아간다. 중심에 가까울수록 세게(가장자리에서 절반).
        /// landStunMs &gt; 0이면 뒤집혀 떨어지고 착지 순간부터 스턴 (영웅 강림).
        /// </summary>
        public void Explode(Vector3 center, float radius, float force, float landStunMs = 0f)
        {
            foreach (var e in _enemies)
            {
                var p = e.transform.position;
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(center.x, center.z));
                if (d > radius) continue;
                e.Knockback(center, force * (1f - 0.5f * d / radius), landStunMs);
            }
        }

        /// <summary>지형 높이 (지형 메시에 레이캐스트). 못 맞으면 경로 높이.</summary>
        public float GroundY(Vector3 p) =>
            Physics.Raycast(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, out var hit, 100f) ? hit.point.y : _path.Start.y;

        /// <summary>맵 바깥으로 날아가지 않게 XZ를 맵 안으로 자른다.</summary>
        public Vector3 ClampToField(Vector3 p)
        {
            var o = map.transform.position;
            p.x = Mathf.Clamp(p.x, o.x, o.x + map.width * map.TileSize);
            p.z = Mathf.Clamp(p.z, o.z, o.z + map.height * map.TileSize);
            return p;
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
            lines.AppendLine($"{StarText(Stars)}   시간 {Clock(elapsed)} / 기준 {Clock(Def.starTimeSec)}   처치 {Kills}   통과한 크립 {Leaked}");

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
                if (first) foreach (var k in Heroes.UnlockedBy(Stage)) lines.AppendLine($"<color={Hud.Hex(Hud.Gold)}>새 영웅: {Heroes.Name(k)}</color> — 스테이지 선택에서 고를 수 있다");
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

        // ---------- HUD (docs/INGAME_UI.md) ----------

        /// <summary>마우스(스크린 좌표, 좌하단 원점)가 HUD 위에 있는가 — HUD 클릭이 설치 클릭으로 새지 않게.</summary>
        public bool IsOverHud(Vector2 mouse)
        {
            if (!showHud) return false;
            var p = new Vector2(mouse.x, Screen.height - mouse.y);
            return Hud.TopLeft.Contains(p) || Hud.TopRight.Contains(p) || Hud.BottomBar.Contains(p) || Hud.HeroBox.Contains(p)
                || (_intros.Count > 0 && Hud.IntroCard.Contains(p)); // 소개 카드를 누른 클릭이 설치로 새지 않게
        }

        /// <summary>남은 크립: 필드에 있는 크립 + 이번 웨이브에서 아직 나오지 않은 크립. 보스도 하나로 센다.</summary>
        public int CreepsLeft => _enemies.Count + (Wave != null ? Wave.ToSpawn : 0);

        // ponytail: 베타 HUD는 IMGUI. 레이아웃이 확정되면 UI Toolkit으로 옮긴다
        private void OnGUI()
        {
            if (!showHud || Wave == null) return;
            var btn = new GUIStyle(GUI.skin.button) { fontSize = 15, richText = true };
            DrawTopBar(btn);
            if (!Over) DrawIntro(); // 끝나면 게임 시계가 멈춰 카드가 영영 남으므로 그리지 않는다
            if (Over) { DrawSettlement(Hud.Text(20), new GUIStyle(GUI.skin.button) { fontSize = 16 }); return; }
            if (!HeroInField) DrawTowerBar(btn);
        }

        /// <summary>위 줄: 왼쪽 자원, 가운데 별, 오른쪽 배속·다음 웨이브. 두 시점 공통.</summary>
        private void DrawTopBar(GUIStyle btn)
        {
            var big = Hud.Text(18);
            var r = Hud.TopLeft;
            Hud.Panel(r);
            string boss = Wave.BossAtEnd && Wave.IsLastWave ? " 보스" : "";
            GUI.Label(new Rect(r.x + 12, r.y, r.width - 24, r.height),
                $"<color={Hud.Hex(Hud.Gold)}>G</color> {Gold}     <color={Hud.Hex(Hud.Bad)}>♥</color> {Death}     웨이브 {Wave.Round}/{Wave.WaveCount}{boss}     크립 {CreepsLeft}", big);

            // 별: 지금 클리어하면 받을 별. 크립 통과·기준 시간 초과로 하나씩 꺼진다 (docs/STAGE.md)
            float elapsed = GameTimeMs / 1000f;
            int stars = StageRules.Stars(true, Leaked, elapsed, Def.starTimeSec);
            r = Hud.TopCenter;
            Hud.Panel(r);
            GUI.Label(r, $"<color={Hud.Hex(Hud.Gold)}>{new string('★', stars)}</color><color={Hud.Hex(Hud.Dim)}>{new string('★', 3 - stars)}</color>   " +
                         $"<size=14>{Clock(elapsed)} / {Clock(Def.starTimeSec)}</size>", Hud.Text(20, TextAnchor.MiddleCenter));
            if (GameTimeMs < _messageUntil)
            {
                var m = new Rect(Screen.width / 2f - 300, r.yMax + 8, 600, 32);
                Hud.Panel(m, 0.6f);
                GUI.Label(m, LastMessage, Hud.Text(16, TextAnchor.MiddleCenter));
            }

            r = Hud.TopRight;
            Hud.Panel(HeroInField ? r : new Rect(r.x, r.y, r.width, 76));
            float x = r.x + 10, y = r.y + 8;
            foreach (float s in new[] { 1f, 2f })
            {
                string t = Mathf.Approximately(timeScale, s) ? $"<color={Hud.Hex(Hud.Gold)}>{s:0}×</color>" : $"{s:0}×";
                if (GUI.Button(new Rect(x, y, 44, 28), t, btn)) timeScale = s;
                x += 48;
            }
            // ponytail: 베타 테스트용 — 15웨이브를 다 돌지 않고 정산을 보려고 (실패로 처리)
            if (!Over && GUI.Button(new Rect(r.xMax - 100, y, 90, 28), "종료", btn)) GameOver(false, "스테이지 종료");

            var small = Hud.Text(15);
            y += 34;
            if (Wave.State == WaveSystem.WaveState.Break && !Wave.Done)
            {
                string next = Wave.IsBossRound(Wave.Round + 1) ? " 보스" : "";
                GUI.Label(new Rect(r.x + 10, y, 180, 28), $"다음 웨이브 {Wave.Round + 1}{next} · {Wave.TimeLeftSec}초", small);
                if (GUI.Button(new Rect(r.xMax - 160, y, 150, 28), HeroInField ? "지금 시작" : "지금 시작 (Space)", btn)) Wave.StartNow();
            }
            else GUI.Label(new Rect(r.x + 10, y, r.width - 20, 28), Wave.IsLastWave ? "마지막 웨이브" : $"웨이브 진행 중 · {Wave.TimeLeftSec}초", small);
            y += 32;
            if (HeroInField) GUI.Label(new Rect(r.x + 10, y, r.width - 20, 24), $"<color={Hud.Hex(Hud.Bad)}>절벽 비움 · 포탑 설치 불가</color>", small);
        }

        /// <summary>절벽 시점 아래 줄: 포탑 종류 1~4. 잠긴 종류는 해금 스테이지, 골드가 모자라면 비용을 빨갛게.</summary>
        private void DrawTowerBar(GUIStyle btn)
        {
            if (Placer == null) return;
            var r = Hud.BottomBar;
            float w = (r.width - 6f * (Placer.specs.Count - 1)) / Placer.specs.Count;
            for (int i = 0; i < Placer.specs.Count; i++)
            {
                var sp = Placer.specs[i];
                var slot = new Rect(r.x + i * (w + 6f), r.y, w, r.height);
                bool open = Placer.IsUnlocked(i);
                string cost = Gold >= sp.cost ? $"{sp.cost}G" : $"<color={Hud.Hex(Hud.Bad)}>{sp.cost}G</color>";
                string text = open ? $"{i + 1}  {sp.displayName.Replace(" 포탑", "")}\n{cost}" : $"<color={Hud.Hex(Hud.Dim)}>{i + 1}  잠김\n스테이지 {sp.unlockStage}</color>";
                if (GUI.Button(slot, text, btn)) Placer.Select(i); // 잠긴 종류는 Select가 무시한다
                if (open && i == Placer.selected) Hud.Frame(slot, Placer.Active ? Color.white : Hud.Dim);
            }

            string hint = Placer.Active
                ? $"{Placer.Selected.displayName} 설치 ({Tower.All.Count}/{PlacementRules.MaxTowers}) · <color={Hud.Hex(Placer.LastResult == PlaceResult.Ok ? Hud.Good : Hud.Bad)}>{TowerPlacer.Describe(Placer.LastResult)}</color> · 우클릭 취소"
                : "1~4 또는 B: 설치";
            var hs = Hud.Text(15, TextAnchor.MiddleCenter);
            float hw = hs.CalcSize(new GUIContent(hint)).x + 24f;
            var hr = new Rect(Screen.width / 2f - hw / 2f, r.y - 32, hw, 26);
            Hud.Panel(hr, 0.6f);
            GUI.Label(hr, hint, hs);
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

    /// <summary>인게임 HUD 공용 배치·그리기 (docs/INGAME_UI.md). 위치는 화면 크기에서 바로 계산한다 — 클릭 막기(IsOverHud)와 그리기가 같은 값을 쓴다.</summary>
    public static class Hud
    {
        public const float Pad = 10f;
        public static Rect TopLeft => new Rect(Pad, Pad, 470, 40);
        public static Rect TopCenter => new Rect(Screen.width / 2f - 130, Pad, 260, 40);
        public static Rect TopRight => new Rect(Screen.width - 350 - Pad, Pad, 350, 102);
        public static Rect BottomBar => new Rect(Screen.width / 2f - 210, Screen.height - 64 - Pad, 420, 64);
        public static Rect HeroBox => new Rect(Pad, Screen.height - 64 - Pad, 250, 64);
        public static Rect IntroCard => new Rect(Pad, TopLeft.yMax + 12, 340, 86);

        public static readonly Color Gold = new Color(0.98f, 0.78f, 0.46f), Dim = new Color(0.7f, 0.7f, 0.66f),
            Bad = new Color(0.94f, 0.58f, 0.58f), Good = new Color(0.6f, 0.85f, 0.4f);

        public static void Panel(Rect r, float alpha = 0.78f) => Fill(r, new Color(0.09f, 0.09f, 0.08f, alpha));

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Frame(Rect r, Color c, float w = 2f)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        /// <summary>진행 막대 (t = 0~1)</summary>
        public static void Bar(Rect r, float t, Color c)
        {
            Fill(r, new Color(0.27f, 0.27f, 0.25f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), c);
        }

        // 스타일은 (크기, 정렬, 줄바꿈)마다 한 번만 만든다 — OnGUI는 매 프레임 여러 번 불려 매번 만들면 쓰레기가 쌓인다.
        // 돌려받은 스타일을 고치면 같은 키를 쓰는 곳이 모두 바뀌므로 고치지 말 것
        private static readonly Dictionary<(int, TextAnchor, bool), GUIStyle> _styles = new Dictionary<(int, TextAnchor, bool), GUIStyle>();

        public static GUIStyle Text(int size, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            if (_styles.TryGetValue((size, anchor, wrap), out var s)) return s;
            s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, richText = true, wordWrap = wrap };
            s.normal.textColor = Color.white;
            return _styles[(size, anchor, wrap)] = s;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>월드 위치 → GUI 좌표(좌상단 원점). 카메라 뒤면 null.</summary>
        public static Vector2? ToGui(Camera cam, Vector3 world)
        {
            if (cam == null) return null;
            var p = cam.WorldToScreenPoint(world);
            return p.z > 0f ? new Vector2(p.x, Screen.height - p.y) : (Vector2?)null;
        }
    }
}
