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
    /// All values are tuned to terminate in the minimum number of ticks
    /// so the Unity integration loop can be validated quickly.
    ///
    /// Scenario summary:
    ///   PlayerVictory  — player SpawnDroneSquad → enemy base destroyed at tick 1.
    ///   PlayerDefeat   — enemy schedule only   → player base destroyed at tick 1.
    ///   TimeoutDefeat  — no commands, 3 ticks  → tie → Defeat (TimeOut).
    /// </summary>
    internal static class SmokeScenarios
    {
        // ------------------------------------------------------------------ scenario builders

        /// <summary>
        /// Player spawns one drone that crosses the short lane and destroys the enemy
        /// base (HP = 1) in a single tick.
        /// Commands: SpawnDroneSquad at tick 0, slot 0, lane "lane_ground".
        /// Expected: Victory / EnemyBaseDestroyed / ClearTimeTick = 1.
        /// </summary>
        public static (BattleConfigSnapshot Config, BattleInitialState Initial, BattleCommand[] Commands)
            PlayerVictory()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_ground", LaneType.Ground, 1_000L),
            };
            BattleConfigSnapshot cfg = new BattleConfigSnapshot(
                configVersion: "smoke_v1",
                initialEnergy: Fp.FromInt(20),    // covers drone cost
                maxEnergy: Fp.FromInt(100),
                energyRegenPerTick: Fp.Zero,
                pilotDeployCooldownTick: 200,
                pilotReturnCooldownTick: 100,
                pilotKnockoutDroneResumeTick: 50,
                playerBaseInitialHp: Fp.FromInt(1000),
                enemyBaseInitialHp: Fp.FromInt(1),  // destroyed by one drone attack
                maxBattleTick: 10,
                lanes: lanes);

            // Drone: speed 2000 > lane 1000 → reaches enemy base in 1 tick.
            // Attack 50 > enemyBaseHp 1 → destroys in 1 hit.
            SlotDefinition[] slots = new SlotDefinition[]
            {
                new SlotDefinition(
                    slotIndex: 0, pilotId: "pilot_a", droneSquadId: "drone_a",
                    energyCost: Fp.FromInt(20), cooldownTick: 5,
                    droneHp: Fp.FromInt(100), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 2_000L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_000L, pilotSpeedMilliPerTick: 300L),
            };
            BattleInitialState initial = new BattleInitialState("smoke_victory", 1L, slots);

            BattleCommand[] commands = new BattleCommand[]
            {
                BattleCommand.SpawnDroneSquad(tick: 0, slotIndex: 0, laneId: "lane_ground"),
            };

            return (cfg, initial, commands);
        }

        /// <summary>
        /// Enemy schedule spawns one fast enemy that crosses the short lane and
        /// destroys the player base (HP = 1) at tick 1. No player commands.
        /// Expected: Defeat / PlayerBaseDestroyed / ClearTimeTick = 1.
        /// </summary>
        public static (BattleConfigSnapshot Config, BattleInitialState Initial, BattleCommand[] Commands)
            PlayerDefeat()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_ground", LaneType.Ground, 1_000L),
            };
            // Enemy speed 2000 > lane 1000 → reaches player base in same tick it spawns.
            // Attack 50 > playerBaseHp 1 → destroys in 1 hit.
            EnemySpawnDefinition[] spawns = new EnemySpawnDefinition[]
            {
                new EnemySpawnDefinition(
                    spawnTick: 1, laneId: "lane_ground",
                    hp: Fp.FromInt(200), attack: Fp.FromInt(50),
                    rangeMilli: 500L, speedMilliPerTick: 2_000L),
            };
            BattleConfigSnapshot cfg = new BattleConfigSnapshot(
                configVersion: "smoke_v1",
                initialEnergy: Fp.FromInt(0),
                maxEnergy: Fp.FromInt(100),
                energyRegenPerTick: Fp.Zero,
                pilotDeployCooldownTick: 200,
                pilotReturnCooldownTick: 100,
                pilotKnockoutDroneResumeTick: 50,
                playerBaseInitialHp: Fp.FromInt(1),   // destroyed by one enemy attack
                enemyBaseInitialHp: Fp.FromInt(1000),
                maxBattleTick: 10,
                lanes: lanes,
                enemySpawnSchedule: spawns);

            SlotDefinition[] slots = new SlotDefinition[]
            {
                new SlotDefinition(
                    slotIndex: 0, pilotId: "pilot_a", droneSquadId: "drone_a",
                    energyCost: Fp.FromInt(20), cooldownTick: 5,
                    droneHp: Fp.FromInt(100), droneAttack: Fp.FromInt(10),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 500L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_000L, pilotSpeedMilliPerTick: 300L),
            };
            BattleInitialState initial = new BattleInitialState("smoke_defeat", 2L, slots);

            return (cfg, initial, new BattleCommand[0]);
        }

        /// <summary>
        /// No commands, no enemy spawns. Battle times out after 3 ticks.
        /// Both bases untouched (ratio 1.0 each) → tie → Defeat.
        /// Expected: Defeat / TimeOut / ClearTimeTick = 3.
        /// </summary>
        public static (BattleConfigSnapshot Config, BattleInitialState Initial, BattleCommand[] Commands)
            TimeoutDefeat()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_ground", LaneType.Ground, 100_000L),
            };
            BattleConfigSnapshot cfg = new BattleConfigSnapshot(
                configVersion: "smoke_v1",
                initialEnergy: Fp.FromInt(0),
                maxEnergy: Fp.FromInt(100),
                energyRegenPerTick: Fp.Zero,
                pilotDeployCooldownTick: 200,
                pilotReturnCooldownTick: 100,
                pilotKnockoutDroneResumeTick: 50,
                playerBaseInitialHp: Fp.FromInt(1000),
                enemyBaseInitialHp: Fp.FromInt(1000),
                maxBattleTick: 3,
                lanes: lanes);

            SlotDefinition[] slots = new SlotDefinition[]
            {
                new SlotDefinition(
                    slotIndex: 0, pilotId: "pilot_a", droneSquadId: "drone_a",
                    energyCost: Fp.FromInt(20), cooldownTick: 5,
                    droneHp: Fp.FromInt(100), droneAttack: Fp.FromInt(10),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 500L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_000L, pilotSpeedMilliPerTick: 300L),
            };
            BattleInitialState initial = new BattleInitialState("smoke_timeout", 3L, slots);

            return (cfg, initial, new BattleCommand[0]);
        }

        // ------------------------------------------------------------------ runner

        /// <summary>
        /// Runs a complete battle and returns the result.
        /// Commands are dispatched by matching their Tick to sim.CurrentTick;
        /// commands must be ordered by tick ascending.
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
