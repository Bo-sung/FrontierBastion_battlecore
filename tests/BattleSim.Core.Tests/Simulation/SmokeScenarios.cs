using BattleSim.Core.Commands;
using BattleSim.Core.Config;
using BattleSim.Core.FixedPoint;
using BattleSim.Core.Results;
using BattleSim.Core.Simulation;
using BattleSim.Core.State;

namespace BattleSim.Core.Tests.Simulation
{
    /// <summary>
    /// Three canonical smoke scenarios for client integration testing.
    /// All values are tuned to terminate in the minimum number of ticks.
    ///
    /// Scenario summary:
    ///   SideAVictory          — SideA SpawnDroneSquad → SideB base destroyed at tick 1.
    ///   SideBVictory          — SideB SpawnDroneSquad → SideA base destroyed at tick 1.
    ///   TimeoutSideBTiebreak  — no commands, 3 ticks → tie → SideB wins (TimeOutTieWinnerSide=SideB).
    ///
    /// PvE interpretation: SideA = local player, SideB = AI controller.
    /// </summary>
    internal static class SmokeScenarios
    {
        // ------------------------------------------------------------------ scenario builders

        /// <summary>
        /// SideA spawns one drone that crosses the short lane and destroys the SideB
        /// base (HP = 1) in a single tick.
        /// Commands: SpawnDroneSquad at tick 0, slot 0, lane "lane_ground", side SideA.
        /// Expected: WinnerSide=SideA / SideBBaseDestroyed / ClearTimeTick=1.
        /// </summary>
        public static (BattleConfigSnapshot Config, BattleInitialState Initial, BattleCommand[] Commands)
            SideAVictory()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_ground", LaneType.Ground, 1_000L, 0L),
            };
            BattleSideConfig cfgA = new BattleSideConfig(
                BattleSide.SideA,
                baseInitialHp:      Fp.FromInt(1000),
                initialEnergy:      Fp.FromInt(20),
                maxEnergy:          Fp.FromInt(100),
                energyRegenPerTick: Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(
                BattleSide.SideB,
                baseInitialHp:      Fp.FromInt(1),   // destroyed by one drone attack
                initialEnergy:      Fp.Zero,
                maxEnergy:          Fp.FromInt(100),
                energyRegenPerTick: Fp.Zero);

            BattleConfigSnapshot cfg = new BattleConfigSnapshot(
                configVersion:              "smoke_v2",
                sideA:                      cfgA,
                sideB:                      cfgB,
                pilotDeployCooldownTick:    160,
                pilotReturnCooldownTick:    80,
                pilotKnockoutDroneResumeTick: 40,
                maxBattleTick:              10,
                lanes:                      lanes,
                timeOutTieWinnerSide:       BattleSide.SideB);

            // Drone: speed 2000 > lane 1000 → reaches SideB base in 1 tick.
            // Attack 50 > sideBBaseHp 1 → destroys in 1 hit.
            SlotDefinition[] slotsA = new SlotDefinition[]
            {
                new SlotDefinition(
                    slotIndex: 0, pilotId: "pilot_a", droneSquadId: "drone_a",
                    energyCost: Fp.FromInt(20), cooldownTick: 5,
                    droneHp: Fp.FromInt(100), droneAttack: Fp.FromInt(50), droneDefense: Fp.Zero,
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 2_000L, droneAttackPeriodTick: 1,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20), pilotDefense: Fp.Zero,
                    pilotRangeMilli: 1_000L, pilotSpeedMilliPerTick: 300L, pilotAttackPeriodTick: 1),
            };
            SlotDefinition[] slotsB = new SlotDefinition[]
            {
                new SlotDefinition(
                    slotIndex: 0, pilotId: "pilot_b", droneSquadId: "drone_b",
                    energyCost: Fp.FromInt(20), cooldownTick: 5,
                    droneHp: Fp.FromInt(100), droneAttack: Fp.FromInt(10), droneDefense: Fp.Zero,
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 500L, droneAttackPeriodTick: 1,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20), pilotDefense: Fp.Zero,
                    pilotRangeMilli: 1_000L, pilotSpeedMilliPerTick: 300L, pilotAttackPeriodTick: 1),
            };
            BattleInitialState initial = new BattleInitialState(
                "smoke_side_a_victory", 1L,
                new BattleSideInitialState(BattleSide.SideA, slotsA),
                new BattleSideInitialState(BattleSide.SideB, slotsB));

            BattleCommand[] commands = new BattleCommand[]
            {
                BattleCommand.SpawnDroneSquad(tick: 0, slotIndex: 0, laneId: "lane_ground", side: BattleSide.SideA),
            };

            return (cfg, initial, commands);
        }

        /// <summary>
        /// SideB spawns one fast drone that crosses the short lane and destroys the SideA
        /// base (HP = 1) at tick 1. No SideA commands.
        /// Expected: WinnerSide=SideB / SideABaseDestroyed / ClearTimeTick=1.
        /// </summary>
        public static (BattleConfigSnapshot Config, BattleInitialState Initial, BattleCommand[] Commands)
            SideBVictory()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_ground", LaneType.Ground, 1_000L, 0L),
            };
            BattleSideConfig cfgA = new BattleSideConfig(
                BattleSide.SideA,
                baseInitialHp:      Fp.FromInt(1),   // destroyed by one drone attack
                initialEnergy:      Fp.Zero,
                maxEnergy:          Fp.FromInt(100),
                energyRegenPerTick: Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(
                BattleSide.SideB,
                baseInitialHp:      Fp.FromInt(1000),
                initialEnergy:      Fp.FromInt(20),  // covers drone cost
                maxEnergy:          Fp.FromInt(100),
                energyRegenPerTick: Fp.Zero);

            BattleConfigSnapshot cfg = new BattleConfigSnapshot(
                configVersion:              "smoke_v2",
                sideA:                      cfgA,
                sideB:                      cfgB,
                pilotDeployCooldownTick:    160,
                pilotReturnCooldownTick:    80,
                pilotKnockoutDroneResumeTick: 40,
                maxBattleTick:              10,
                lanes:                      lanes,
                timeOutTieWinnerSide:       BattleSide.SideB);

            SlotDefinition[] slotsA = new SlotDefinition[]
            {
                new SlotDefinition(
                    slotIndex: 0, pilotId: "pilot_a", droneSquadId: "drone_a",
                    energyCost: Fp.FromInt(20), cooldownTick: 5,
                    droneHp: Fp.FromInt(100), droneAttack: Fp.FromInt(10), droneDefense: Fp.Zero,
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 500L, droneAttackPeriodTick: 1,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20), pilotDefense: Fp.Zero,
                    pilotRangeMilli: 1_000L, pilotSpeedMilliPerTick: 300L, pilotAttackPeriodTick: 1),
            };
            // SideB drone: speed 2000 > lane 1000 → reaches SideA base in 1 tick. Attack 50 > hp 1.
            SlotDefinition[] slotsB = new SlotDefinition[]
            {
                new SlotDefinition(
                    slotIndex: 0, pilotId: "pilot_b", droneSquadId: "drone_b",
                    energyCost: Fp.FromInt(20), cooldownTick: 5,
                    droneHp: Fp.FromInt(100), droneAttack: Fp.FromInt(50), droneDefense: Fp.Zero,
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 2_000L, droneAttackPeriodTick: 1,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20), pilotDefense: Fp.Zero,
                    pilotRangeMilli: 1_000L, pilotSpeedMilliPerTick: 300L, pilotAttackPeriodTick: 1),
            };
            BattleInitialState initial = new BattleInitialState(
                "smoke_side_b_victory", 2L,
                new BattleSideInitialState(BattleSide.SideA, slotsA),
                new BattleSideInitialState(BattleSide.SideB, slotsB));

            BattleCommand[] commands = new BattleCommand[]
            {
                BattleCommand.SpawnDroneSquad(tick: 0, slotIndex: 0, laneId: "lane_ground", side: BattleSide.SideB),
            };

            return (cfg, initial, commands);
        }

        /// <summary>
        /// No commands from either side. Battle times out after 3 ticks.
        /// Both bases untouched (ratio 1.0 each) → tie → TimeOutTieWinnerSide=SideB wins.
        /// Expected: WinnerSide=SideB / TimeOut / ClearTimeTick=3.
        /// </summary>
        public static (BattleConfigSnapshot Config, BattleInitialState Initial, BattleCommand[] Commands)
            TimeoutSideBTiebreak()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_ground", LaneType.Ground, 100_000L, 0L),
            };
            BattleSideConfig cfgA = new BattleSideConfig(
                BattleSide.SideA,
                baseInitialHp:      Fp.FromInt(1000),
                initialEnergy:      Fp.Zero,
                maxEnergy:          Fp.FromInt(100),
                energyRegenPerTick: Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(
                BattleSide.SideB,
                baseInitialHp:      Fp.FromInt(1000),
                initialEnergy:      Fp.Zero,
                maxEnergy:          Fp.FromInt(100),
                energyRegenPerTick: Fp.Zero);

            BattleConfigSnapshot cfg = new BattleConfigSnapshot(
                configVersion:              "smoke_v2",
                sideA:                      cfgA,
                sideB:                      cfgB,
                pilotDeployCooldownTick:    160,
                pilotReturnCooldownTick:    80,
                pilotKnockoutDroneResumeTick: 40,
                maxBattleTick:              3,
                lanes:                      lanes,
                timeOutTieWinnerSide:       BattleSide.SideB);

            SlotDefinition[] slots = new SlotDefinition[]
            {
                new SlotDefinition(
                    slotIndex: 0, pilotId: "pilot_a", droneSquadId: "drone_a",
                    energyCost: Fp.FromInt(20), cooldownTick: 5,
                    droneHp: Fp.FromInt(100), droneAttack: Fp.FromInt(10), droneDefense: Fp.Zero,
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 500L, droneAttackPeriodTick: 1,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20), pilotDefense: Fp.Zero,
                    pilotRangeMilli: 1_000L, pilotSpeedMilliPerTick: 300L, pilotAttackPeriodTick: 1),
            };
            BattleInitialState initial = new BattleInitialState(
                "smoke_timeout_side_b_tiebreak", 3L,
                new BattleSideInitialState(BattleSide.SideA, slots),
                new BattleSideInitialState(BattleSide.SideB, (SlotDefinition[])slots.Clone()));

            return (cfg, initial, new BattleCommand[0]);
        }

        // ------------------------------------------------------------------ runner

        /// <summary>
        /// Runs a complete battle and returns the result.
        /// Commands are dispatched by matching their Tick to sim.CurrentTick;
        /// commands must be ordered by tick ascending.
        /// Both SideA and SideB commands at the same tick are all submitted before AdvanceTick.
        /// </summary>
        public static BattleResult RunToCompletion(
            BattleConfigSnapshot cfg,
            BattleInitialState initial,
            BattleCommand[] commands)
        {
            BattleSimulator sim = new BattleSimulator(cfg, initial);
            int cmdIdx = 0;
            while (!sim.IsTerminated)
            {
                while (cmdIdx < commands.Length && commands[cmdIdx].Tick == sim.CurrentTick)
                {
                    sim.SubmitCommand(commands[cmdIdx]);
                    cmdIdx++;
                }
                sim.AdvanceTick();
            }
            return sim.GetResult();
        }
    }
}
