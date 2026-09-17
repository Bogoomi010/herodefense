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
    /// 라운드 상태 기계. 원본 systems/WaveSystem.ts 1:1 이식.
    /// 휴식 → 라운드 진행(20마리 0.8초 간격 스폰, 전멸 시 조기 종료) → 휴식. 10의 배수 라운드는 보스 1기 + 제한 시간.
    /// </summary>
    public sealed class WaveSystem
    {
        public enum WaveState { Break, Running }

        public int Round { get; private set; }
        public WaveState State { get; private set; } = WaveState.Break;
        public bool Endless { get; private set; }

        private readonly IWaveCallbacks _cb;
        private float _timer = GameConfig.FirstBreak * 1000f;
        private int _spawned;
        private float _spawnT;
        private MobState _boss;
        private bool _done;

        public WaveSystem(IWaveCallbacks cb) => _cb = cb;

        public int TimeLeftSec => (int)System.Math.Max(0, System.Math.Ceiling(_timer / 1000f));
        public bool Done => _done;

        public bool IsBossRound(int r) => r > 0 && r % 10 == 0;

        public void NotifyBossKilled()
        {
            if (_done) return;
            if (Round >= GameConfig.RoundMax && !Endless)
            {
                _done = true;
                _cb.Victory();
                return;
            }
            _cb.Message("보스 처치!");
            EndRound();
        }

        /// <summary>♾ 승리(40R) 후 무한 모드로 재개</summary>
        public void ResumeEndless()
        {
            Endless = true;
            _done = false;
            State = WaveState.Break;
            _timer = GameConfig.RoundBreak * 1000f;
        }

        public void Stop() => _done = true;

        /// <summary>테스트용: 휴식 중에 라운드 번호만 앞으로 넘긴다 (예: 보스 라운드 바로 확인).</summary>
        public void SkipRound()
        {
            if (State == WaveState.Break && !_done) Round++;
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

            bool bossRound = IsBossRound(Round);
            if (bossRound)
            {
                if (_spawned == 0)
                {
                    _boss = _cb.Spawn(Round, true);
                    _spawned = 1;
                }
            }
            else
            {
                _spawnT -= deltaMs;
                if (_spawned < GameConfig.MobsPerRound && _spawnT <= 0f)
                {
                    _cb.Spawn(Round, false);
                    _spawned++;
                    _spawnT = GameConfig.SpawnInterval * 1000f;
                }
                // 모든 몹을 다 잡았으면 조기 종료
                if (_spawned >= GameConfig.MobsPerRound && _cb.MobCount() == 0)
                {
                    EndRound();
                    return;
                }
            }

            if (_timer <= 0f)
            {
                if (bossRound && _boss != null && !_boss.Dead)
                {
                    _done = true;
                    _cb.Defeat("제한 시간 내에 보스를 처치하지 못했습니다");
                    return;
                }
                EndRound();
            }
        }

        private void StartRound()
        {
            Round++;
            State = WaveState.Running;
            _spawned = 0;
            _spawnT = 0f;
            _boss = null;
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
