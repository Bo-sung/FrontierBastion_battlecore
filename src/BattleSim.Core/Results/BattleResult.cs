using System;
using BattleSim.Core.FixedPoint;
using BattleSim.Core.State;

namespace BattleSim.Core.Results
{
    /// <summary>
    /// Final, deterministic result of a battle.
    /// <para>
    /// <see cref="WinnerSide"/> is the primary result. Callers interpret victory/defeat
    /// by comparing <see cref="WinnerSide"/> to their local side.
    /// </para>
    /// <para>
    /// Stars, reward grants, ranking points, and replay envelopes are
    /// computed outside Core and are not represented here.
    /// </para>
    /// </summary>
    public sealed class BattleResult
    {
        public BattleSide WinnerSide { get; private set; }
        public BattleEndReason EndReason { get; private set; }
        public int ClearTimeTick { get; private set; }
        public Fp SideABaseHpRatio { get; private set; }
        public Fp SideBBaseHpRatio { get; private set; }

        public BattleResult(
            BattleSide winnerSide,
            BattleEndReason endReason,
            int clearTimeTick,
            Fp sideABaseHpRatio,
            Fp sideBBaseHpRatio)
        {
            if (clearTimeTick < 0)
                throw new ArgumentOutOfRangeException("clearTimeTick");
            WinnerSide = winnerSide;
            EndReason = endReason;
            ClearTimeTick = clearTimeTick;
            SideABaseHpRatio = sideABaseHpRatio;
            SideBBaseHpRatio = sideBBaseHpRatio;
        }

        /// <summary>
        /// Build a result for a TimeOut termination. Winner is decided by remaining
        /// base-HP ratios; ties resolve to <paramref name="tieWinnerSide"/>.
        /// </summary>
        public static BattleResult FromTimeOut(
            int clearTimeTick,
            Fp sideAHpRatio,
            Fp sideBHpRatio,
            BattleSide tieWinnerSide)
        {
            BattleSide winner;
            if (sideAHpRatio > sideBHpRatio)
                winner = BattleSide.SideA;
            else if (sideBHpRatio > sideAHpRatio)
                winner = BattleSide.SideB;
            else
                winner = tieWinnerSide;
            return new BattleResult(winner, BattleEndReason.TimeOut, clearTimeTick, sideAHpRatio, sideBHpRatio);
        }
    }
}
