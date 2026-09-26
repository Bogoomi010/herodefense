using System;

namespace TowerDefense.Core
{
    /// <summary>
    /// 몹 하나의 전투 상태. 원본 entities/Mob.ts에서 렌더링을 뺀 것.
    /// 시간은 게임 시계 ms, 거리는 경로 진행 px.
    /// </summary>
    public sealed class MobState
    {
        public float Hp;
        public readonly float MaxHp;
        public readonly float BaseSpeed; // px/s
        public readonly int Gold;
        public readonly int Armor;
        public readonly bool IsBoss;
        public readonly bool Splits;
        public readonly bool Golden;
        public readonly string Name;
        public readonly uint Color;
        public readonly float Scale;
        public readonly string Id;    // 크립 종류 id (분열 자식 종류를 정한다)
        public readonly string Model; // 모델 프리팹 이름

        public bool Dead;
        /// <summary>경로 진행 거리(px). 분열 자식 배치·도착 판정에 사용.</summary>
        public float Dist;

        private float _slowPct;
        private float _slowUntil;
        private float _stunUntil;
        private float _shredAmt;
        private float _shredUntil;
        private float _markMul = 1f; // 받는 피해 증가 (스킬 마킹)
        private float _markUntil;
        public float DotDps; // 도트 (마법 판정) — 세션이 틱마다 적용
        public float DotUntil;

        public MobState(MobStats s, float startDist = 0f)
        {
            Hp = s.Hp;
            MaxHp = s.Hp;
            BaseSpeed = s.Speed;
            Gold = s.Gold;
            Armor = s.Armor;
            IsBoss = s.Boss;
            Splits = s.Splits;
            Golden = s.Golden;
            Name = s.Name ?? "";
            Color = s.Color;
            Scale = s.Scale <= 0f ? 1f : s.Scale;
            Id = s.Id;
            Model = s.Model;
            Dist = startDist;
        }

        /// <summary>현재 이동 속도(px/s). 기절 중이면 0.</summary>
        public float CurrentSpeed(float now, float speedMul = 1f)
        {
            if (now < _stunUntil) return 0f;
            float slow = now < _slowUntil ? _slowPct : 0f;
            return BaseSpeed * (1f - slow) * speedMul;
        }

        public void Advance(float now, float deltaMs, float speedMul = 1f)
        {
            if (Dead) return;
            Dist += CurrentSpeed(now, speedMul) * deltaMs / 1000f;
        }

        /// <summary>true를 반환하면 사망</summary>
        public bool Hit(float dmg)
        {
            if (Dead) return false;
            Hp -= dmg;
            if (Hp <= 0f)
            {
                Dead = true;
                return true;
            }
            return false;
        }

        public void ApplySlow(float pct, float durMs, float now)
        {
            if (pct >= _slowPct || now >= _slowUntil)
            {
                _slowPct = Math.Min(pct, 0.9f);
                _slowUntil = now + durMs;
            }
        }

        public void ApplyStun(float durMs, float now) => _stunUntil = Math.Max(_stunUntil, now + durMs);

        public void ApplyShred(float amount, float durMs, float now)
        {
            if (amount >= _shredAmt || now >= _shredUntil)
            {
                _shredAmt = amount;
                _shredUntil = now + durMs;
            }
        }

        /// <summary>방깎 디버프가 적용된 현재 방어력</summary>
        public int EffArmor(float now)
        {
            float shred = now < _shredUntil ? _shredAmt : 0f;
            return (int)Math.Max(0f, Armor - shred);
        }

        public void ApplyMark(float mul, float durMs, float now)
        {
            if (mul >= _markMul || now >= _markUntil)
            {
                _markMul = mul;
                _markUntil = now + durMs;
            }
        }

        /// <summary>받는 피해 배율 (마킹)</summary>
        public float TakenMul(float now) => now < _markUntil ? _markMul : 1f;

        public void ApplyDot(float dps, float durMs, float now)
        {
            if (dps >= DotDps || now >= DotUntil)
            {
                DotDps = dps;
                DotUntil = now + durMs;
            }
        }

        public bool IsStunned(float now) => now < _stunUntil;
        public bool IsShredded(float now) => now < _shredUntil && _shredAmt > 0f;
        public bool IsSlowed(float now) => now < _slowUntil && _slowPct > 0f;
    }
}
