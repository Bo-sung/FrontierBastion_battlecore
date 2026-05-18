using System;
using BattleSim.Core.FixedPoint;

namespace BattleSim.Core.Results
{
    /// <summary>
    /// Final, deterministic result of a battle.
    /// <para>
    /// <see cref="Outcome"/> and <see cref="EndReason"/> are intentionally
    /// separate: a TimeOut still resolves to Victory or Defeat by comparing
    /// remaining base-HP ratios; ties resolve to Defeat.
    /// </para>
    /// <para>
    /// Stars, reward grants, ranking points, and replay envelopes are
    /// computed outside Core and are not represented here.
    /// </para>
    /// </summary>
    public sealed class BattleResult
    {
        public BattleOutcome Outcome { get; private set; }
        public BattleEndReason EndReason { get; private set; }
        public int ClearTimeTick { get; private set; }
        public Fp PlayerBaseHpRatio { get; private set; }
        public Fp EnemyBaseHpRatio { get; private set; }

        public BattleResult(
            BattleOutcome outcome,
            BattleEndReason endReason,
            int clearTimeTick,
            Fp playerBaseHpRatio,
            Fp enemyBaseHpRatio)
        {
            if (clearTimeTick < 0)
            {
                throw new ArgumentOutOfRangeException("clearTimeTick");
            }
            Outcome = outcome;
            EndReason = endReason;
            ClearTimeTick = clearTimeTick;
            PlayerBaseHpRatio = playerBaseHpRatio;
            EnemyBaseHpRatio = enemyBaseHpRatio;
        }

        /// <summary>
        /// Build a result for a TimeOut termination. Outcome is decided by
        /// remaining base-HP ratios; ties resolve to <see cref="BattleOutcome.Defeat"/>.
        /// </summary>
        public static BattleResult FromTimeOut(int clearTimeTick, Fp playerHpRatio, Fp enemyHpRatio)
        {
            BattleOutcome outcome = playerHpRatio > enemyHpRatio
                ? BattleOutcome.Victory
                : BattleOutcome.Defeat;
            return new BattleResult(outcome, BattleEndReason.TimeOut, clearTimeTick, playerHpRatio, enemyHpRatio);
        }
    }
}
