namespace BattleSim.Core.Results
{
    /// <summary>
    /// Why the battle terminated. Independent of <see cref="BattleResult.WinnerSide"/>.
    /// </summary>
    public enum BattleEndReason
    {
        None               = 0,
        SideBBaseDestroyed = 1,  // SideA wins
        SideABaseDestroyed = 2,  // SideB wins
        TimeOut            = 3,
    }
}
