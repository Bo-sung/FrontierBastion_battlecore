using System;
using BattleSim.Core.State;

namespace BattleSim.Core.Config
{
    /// <summary>
    /// Frozen stage-level tunable parameters for a single battle.
    /// Immutable once handed to <see cref="Simulation.BattleSimulator"/>.
    /// No HTTP, DB, reward, server-validation, or deck fields here.
    /// Per-side deck selection lives in <see cref="BattleSim.Core.State.BattleInitialState"/>.
    /// </summary>
    public sealed class BattleConfigSnapshot
    {
        public string ConfigVersion { get; private set; }

        // Per-side energy economy and base HP.
        public BattleSideConfig SideA { get; private set; }
        public BattleSideConfig SideB { get; private set; }

        // Pilot deploy timings, tick units (shared by both sides).
        public int PilotDeployCooldownTick { get; private set; }
        public int PilotReturnCooldownTick { get; private set; }
        public int PilotKnockoutDroneResumeTick { get; private set; }

        // Hard upper bound for battle length, in ticks.
        public int MaxBattleTick { get; private set; }

        // Lane definitions (required for combat validation).
        public LaneDefinition[] Lanes { get; private set; }

        // Which side wins when TimeOut occurs with equal HP ratios.
        public BattleSide TimeOutTieWinnerSide { get; private set; }

        public BattleConfigSnapshot(
            string configVersion,
            BattleSideConfig sideA,
            BattleSideConfig sideB,
            int pilotDeployCooldownTick,
            int pilotReturnCooldownTick,
            int pilotKnockoutDroneResumeTick,
            int maxBattleTick,
            LaneDefinition[] lanes,
            BattleSide timeOutTieWinnerSide = BattleSide.SideB)
        {
            if (sideA == null) throw new ArgumentNullException("sideA");
            if (sideB == null) throw new ArgumentNullException("sideB");
            if (lanes == null || lanes.Length == 0)
                throw new ArgumentException("lanes is required.", "lanes");
            if (maxBattleTick <= 0)
                throw new ArgumentOutOfRangeException("maxBattleTick");
            ConfigVersion = configVersion;
            SideA = sideA;
            SideB = sideB;
            PilotDeployCooldownTick = pilotDeployCooldownTick;
            PilotReturnCooldownTick = pilotReturnCooldownTick;
            PilotKnockoutDroneResumeTick = pilotKnockoutDroneResumeTick;
            MaxBattleTick = maxBattleTick;
            Lanes = (LaneDefinition[])lanes.Clone();
            TimeOutTieWinnerSide = timeOutTieWinnerSide;
        }
    }
}
