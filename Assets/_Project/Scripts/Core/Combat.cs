namespace TowerDefense.Core
{
    /// <summary>물리: 방어력 적용 / 마법: 방어 무시</summary>
    public enum DmgType { Phys, Magic }

    /// <summary>런 전체 누적 보정치 (원본 cards.ts Mods).</summary>
    public sealed class Mods
    {
        public float AtkMul = 1f;
        public float CdMul = 1f;
        public float RangeMul = 1f;
        public float PhysMul = 1f;
        public float MagicMul = 1f;
        public float MobSpeedMul = 1f;
        public int GachaDiscount;
    }

    /// <summary>피해 계산 (원본 GameScene.damage의 순수 부분).</summary>
    public static class Combat
    {
        /// <summary>피해를 적용하고 처치 여부를 반환한다. 처치 후 처리(골드·분열·보스)는 호출자 몫.</summary>
        public static bool Damage(MobState m, float amount, DmgType type, float now, Mods mods = null, float teamAtkMul = 1f)
        {
            if (m.Dead) return false;
            if (mods != null)
            {
                amount *= mods.AtkMul;
                amount *= type == DmgType.Phys ? mods.PhysMul : mods.MagicMul;
            }
            amount *= teamAtkMul;
            amount *= m.TakenMul(now);
            if (type == DmgType.Phys) amount *= 1f - MobDefs.ArmorReduction(m.EffArmor(now));
            return m.Hit(amount);
        }

        /// <summary>도트 틱 — 마법 판정, deltaMs만큼 적용. 처치 여부 반환.</summary>
        public static bool DotTick(MobState m, float now, float deltaMs)
        {
            if (m.Dead || m.DotDps <= 0f || now >= m.DotUntil) return false;
            return m.Hit(m.DotDps * deltaMs / 1000f * m.TakenMul(now));
        }
    }
}
