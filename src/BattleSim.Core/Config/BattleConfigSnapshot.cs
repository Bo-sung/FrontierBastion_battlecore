using System;
using BattleSim.Core.FixedPoint;

namespace BattleSim.Core.Config
{
    /// <summary>
    /// Frozen stage-level tunable parameters for a single battle.
    /// Immutable once handed to <see cref="Simulation.BattleSimulator"/>.
    /// No HTTP, DB, reward, server-validation, or player-deck fields here.
    /// Player deck (slot/pilot/drone selection) lives in
    /// <see cref="BattleSim.Core.State.BattleInitialState"/>.
    /// </summary>
    public sealed class BattleConfigSnapshot
    {
        public string ConfigVersion { get; private set; }

        // Energy economy (fp10000).
        public Fp InitialEnergy { get; private set; }
        public Fp MaxEnergy { get; private set; }
        public Fp EnergyRegenPerTick { get; private set; }

        // Pilot deploy timings, tick units.
        public int PilotDeployCooldownTick { get; private set; }
        public int PilotReturnCooldownTick { get; private set; }
        public int PilotKnockoutDroneResumeTick { get; private set; }

        // Base HP (fp10000).
        public Fp PlayerBaseInitialHp { get; private set; }
        public Fp EnemyBaseInitialHp { get; private set; }

        // Hard upper bound for battle length, in ticks.
        public int MaxBattleTick { get; private set; }

        // Lane definitions (required for combat validation).
        public LaneDefinition[] Lanes { get; private set; }

        public BattleConfigSnapshot(
            string configVersion,
            Fp initialEnergy,
            Fp maxEnergy,
            Fp energyRegenPerTick,
            int pilotDeployCooldownTick,
            int pilotReturnCooldownTick,
            int pilotKnockoutDroneResumeTick,
            Fp playerBaseInitialHp,
            Fp enemyBaseInitialHp,
            int maxBattleTick,
            LaneDefinition[] lanes)
        {
            if (lanes == null || lanes.Length == 0)
            {
                throw new ArgumentException("lanes is required.", "lanes");
            }
            if (maxBattleTick <= 0)
            {
                throw new ArgumentOutOfRangeException("maxBattleTick");
            }
            ConfigVersion = configVersion;
            InitialEnergy = initialEnergy;
            MaxEnergy = maxEnergy;
            EnergyRegenPerTick = energyRegenPerTick;
            PilotDeployCooldownTick = pilotDeployCooldownTick;
            PilotReturnCooldownTick = pilotReturnCooldownTick;
            PilotKnockoutDroneResumeTick = pilotKnockoutDroneResumeTick;
            PlayerBaseInitialHp = playerBaseInitialHp;
            EnemyBaseInitialHp = enemyBaseInitialHp;
            MaxBattleTick = maxBattleTick;
            Lanes = (LaneDefinition[])lanes.Clone();
        }
    }
}
