namespace TowerDefense.Map
{
    /// <summary>타일 종류. Ground는 지형, 나머지 셋은 적 이동 경로(평탄).</summary>
    public enum TileType : byte
    {
        Ground = 0,
        Path = 1,
        Spawn = 2,
        Goal = 3,
    }
}
