namespace BattleSim.Core.Results
{
    /// <summary>
    /// Why the battle terminated. Independent of <see cref="BattleOutcome"/>.
    /// </summary>
    public enum BattleEndReason
    {
        None = 0,
        EnemyBaseDestroyed = 1,
        PlayerBaseDestroyed = 2,
        TimeOut = 3,
    }
}
