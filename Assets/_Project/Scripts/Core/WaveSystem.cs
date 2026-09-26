namespace TowerDefense.Core
{
    public interface IWaveCallbacks
    {
        /// <summary>몹 1기 스폰. 스폰 불가(게임 종료)면 null.</summary>
        MobState Spawn(int round, bool boss);
        int MobCount();
        void RoundStart(int round, bool boss);
        void RoundClear(int round);
        void Message(string text);
        void Defeat(string reason);
        void Victory();
    }

    /// <summary>
    /// 스테이지의 웨이브 상태 기계 (docs/STAGE.md). 원본 systems/WaveSystem.ts에서 출발.
    /// 휴식 → 웨이브 진행(크립 N마리 0.8초 간격, 전멸 시 조기 종료, 시간이 다 되면 남은 크립을 둔 채 다음 웨이브) → 휴식.
    /// 일반 스테이지: 마지막 웨이브는 시간과 관계없이, 크립을 모두 내보냈고 필드가 비면 클리어(Victory).
    /// 보스 스테이지: 마지막 웨이브에 크립과 보스 1기(맨 먼저)가 함께 나오고, 보스를 잡는 즉시 클리어(<see cref="NotifyBossKilled"/>).
    /// 보스가 도착하면 실패는 세션이 처리한다. 스테이지 제한 시간은 없다.
    /// </summary>
    public sealed class WaveSystem
    {
        public enum WaveState { Break, Running }

        public const int StageWaves = 15;

        public int Round { get; private set; }
        public WaveState State { get; private set; } = WaveState.Break;
        public int WaveCount { get; }
        public int MobsPerWave { get; }
        public bool BossAtEnd { get; }

        private readonly IWaveCallbacks _cb;
        private float _timer = GameConfig.FirstBreak * 1000f;
        private int _spawned;
        private float _spawnT;
        private bool _done;

        public WaveSystem(IWaveCallbacks cb, int waveCount = StageWaves, int mobsPerWave = GameConfig.MobsPerRound, bool bossAtEnd = false)
        {
            _cb = cb;
            WaveCount = System.Math.Max(1, waveCount);
            MobsPerWave = System.Math.Max(1, mobsPerWave);
            BossAtEnd = bossAtEnd;
        }

        public int TimeLeftSec => (int)System.Math.Max(0, System.Math.Ceiling(_timer / 1000f));
        public bool Done => _done;
        public bool IsLastWave => Round >= WaveCount;

        public bool IsBossRound(int r) => BossAtEnd && r == WaveCount;

        /// <summary>보스 처치 → 필드에 크립이 남아 있어도 즉시 클리어.</summary>
        public void NotifyBossKilled()
        {
            if (_done) return;
            _cb.Message("보스 처치!");
            _done = true;
            _cb.RoundClear(Round);
            _cb.Victory();
        }

        public void Stop() => _done = true;

        /// <summary>테스트용: 휴식 중에 웨이브 번호만 앞으로 넘긴다 (마지막 웨이브 직전까지).</summary>
        public void SkipRound()
        {
            if (State == WaveState.Break && !_done && Round < WaveCount - 1) Round++;
        }

        public void Update(float deltaMs)
        {
            if (_done) return;
            _timer -= deltaMs;

            if (State == WaveState.Break)
            {
                if (_timer <= 0f) StartRound();
                return;
            }

            bool boss = IsBossRound(Round);
            int toSpawn = MobsPerWave + (boss ? 1 : 0);
            _spawnT -= deltaMs;
            if (_spawned < toSpawn && _spawnT <= 0f)
            {
                _cb.Spawn(Round, boss && _spawned == 0); // 보스 웨이브는 보스가 맨 먼저
                _spawned++;
                _spawnT = GameConfig.SpawnInterval * 1000f;
            }
            bool allOut = _spawned >= toSpawn;

            if (IsLastWave)
            {
                // 마지막 웨이브: 시간 제한 없이 필드가 빌 때까지. 보스 스테이지는 보스를 잡아야 끝난다
                if (!BossAtEnd && allOut && _cb.MobCount() == 0)
                {
                    _done = true;
                    _cb.RoundClear(Round);
                    _cb.Victory();
                }
                return;
            }

            // 전멸하면 조기 종료, 시간이 다 되면 남은 몹을 둔 채 종료
            if ((allOut && _cb.MobCount() == 0) || _timer <= 0f) EndRound();
        }

        private void StartRound()
        {
            Round++;
            State = WaveState.Running;
            _spawned = 0;
            _spawnT = 0f;
            bool boss = IsBossRound(Round);
            _timer = (boss ? GameConfig.BossTime : GameConfig.RoundTime) * 1000f;
            _cb.RoundStart(Round, boss);
        }

        private void EndRound()
        {
            if (State != WaveState.Running) return;
            State = WaveState.Break;
            _timer = GameConfig.RoundBreak * 1000f;
            _cb.RoundClear(Round);
        }
    }
}
