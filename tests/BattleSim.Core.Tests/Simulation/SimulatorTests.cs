using System;
using System.Collections.Generic;
using BattleSim.Core;
using BattleSim.Core.Commands;
using BattleSim.Core.Config;
using BattleSim.Core.FixedPoint;
using BattleSim.Core.Results;
using BattleSim.Core.Simulation;
using BattleSim.Core.State;

namespace BattleSim.Core.Tests.Simulation
{
    internal static class SimulatorTests
    {
        public static void Run()
        {
            // ---- original skeleton tests (still valid) ----
            ConstructsAndExposesInitialState();
            GetResultBeforeTerminationThrows();
            CommandFactoriesEnforceLaneRequirements();
            BattleResultFromTimeOutResolvesByRatio();

            // ---- v0.1 implementation tests ----
            AdvanceTick_IncrementsTick();
            Energy_RegeneratesAndClampsAtMax();
            SlotDroneCooldown_DecreasesEachTick();
            PilotCooldown_DecreasesEachTick();
            SpawnDroneSquad_ConsumesEnergyAndStartsCooldown();
            SpawnDroneSquad_RejectsInvalidLane();
            SpawnDroneSquad_RejectsInsufficientEnergy();
            SpawnDroneSquad_RejectsWhenPilotDeployed();
            DeployPilot_DoesNotConsumeEnergy();
            DeployPilot_SetsPilotDeployed();
            RecallPilot_ClearsPilotAndStartsCooldown();
            RecallPilot_RejectsWhenNotDeployed();
            MaxBattleTick_TerminatesBattle();
            TimeoutTie_ProducesDefeat();

            // ---- smoke scenario tests ----
            Smoke_PlayerVictory_ProducesExpectedResult();
            Smoke_PlayerDefeat_ProducesExpectedResult();
            Smoke_TimeoutDefeat_ProducesExpectedResult();
            Smoke_PlayerVictory_IsDeterministicAcrossRepeatedRuns();

            // ---- v0.2 implementation tests ----
            SpawnDroneSquad_CreatesEntityInLane();
            DeployPilot_CreatesEntityInLane();
            RecallPilot_RemovesEntityFromLane();
            EnemySpawnSchedule_CreatesEnemyAtConfiguredTick();
            PlayerEntity_MovesTowardEnemyBase();
            EnemyEntity_MovesTowardPlayerBase();
            Entity_NeverChangesLane();
            Entity_AttacksNearestEnemyInSameLane();
            DeadEntity_IsRemoved();
            PlayerEntity_DamagesEnemyBase();
            EnemyEntity_DamagesPlayerBase();
            EnemyBaseDestroyed_ProducesVictory();
            PlayerBaseDestroyed_ProducesDefeat();
            MaxBattleTick_TimeoutStillWorks();
            Determinism_SameSetupProducesSameResult();
        }

        // ------------------------------------------------------------------ config / state helpers

        private static readonly long DefaultLaneLen = 100_000L;

        /// <summary>
        /// One ground lane, energy 0→100 at 0.25/tick, maxBattleTick 3600.
        /// </summary>
        private static BattleConfigSnapshot MinimalConfig()
        {
            return MakeConfig(
                initialEnergy: Fp.FromInt(0),
                maxEnergy: Fp.FromInt(100),
                energyRegenPerTick: Fp.FromFraction(1, 4));
        }

        private static BattleConfigSnapshot MakeConfig(
            Fp initialEnergy,
            Fp maxEnergy,
            Fp energyRegenPerTick,
            int maxBattleTick = 3600,
            long laneLengthMilli = 0,        // 0 → use DefaultLaneLen
            EnemySpawnDefinition[]? enemySpawns = null,
            Fp? playerBaseHp = null,
            Fp? enemyBaseHp = null)
        {
            long laneLen = laneLengthMilli > 0 ? laneLengthMilli : DefaultLaneLen;
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_ground", LaneType.Ground, laneLen),
            };
            return new BattleConfigSnapshot(
                configVersion: "test",
                initialEnergy: initialEnergy,
                maxEnergy: maxEnergy,
                energyRegenPerTick: energyRegenPerTick,
                pilotDeployCooldownTick: 200,
                pilotReturnCooldownTick: 100,
                pilotKnockoutDroneResumeTick: 50,
                playerBaseInitialHp: playerBaseHp ?? Fp.FromInt(1000),
                enemyBaseInitialHp: enemyBaseHp ?? Fp.FromInt(1000),
                maxBattleTick: maxBattleTick,
                lanes: lanes,
                enemySpawnSchedule: enemySpawns);
        }

        /// <summary>
        /// Slot 0: energyCost 10, cooldown 100, droneHp 100/atk 10/range 1000/speed 500,
        /// pilotHp 200/atk 20/range 1500/speed 300.
        /// </summary>
        private static BattleInitialState MinimalInitialState()
        {
            return new BattleInitialState("stage_1", 42L, new SlotDefinition[]
            {
                new SlotDefinition(
                    slotIndex: 0, pilotId: "pilot_a", droneSquadId: "drone_a",
                    energyCost: Fp.FromInt(10), cooldownTick: 100,
                    droneHp: Fp.FromInt(100), droneAttack: Fp.FromInt(10),
                    droneRangeMilli: 1_000L, droneSpeedMilliPerTick: 500L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
        }

        private static SlotState GetSlotState(BattleState state, int slotIndex)
        {
            foreach (SlotState s in state.Slots)
                if (s.SlotIndex == slotIndex) return s;
            throw new InvalidOperationException("Slot " + slotIndex + " not found in BattleState.");
        }

        private static LaneState GetLane(BattleState state, string laneId)
        {
            foreach (LaneState ls in state.Lanes)
                if (ls.LaneId == laneId) return ls;
            throw new InvalidOperationException("Lane '" + laneId + "' not found in BattleState.");
        }

        private static BattleEntity? FindByOwner(BattleState state, string laneId, OwnerSide owner)
        {
            LaneState lane = GetLane(state, laneId);
            foreach (BattleEntity e in lane.Entities)
                if (e.OwnerSide == owner) return e;
            return null;
        }

        // ------------------------------------------------------------------ original skeleton tests

        private static void ConstructsAndExposesInitialState()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
            BattleState s = sim.GetState();
            if (s.CurrentTick != 0) throw new InvalidOperationException("CurrentTick != 0");
            if (s.PlayerBaseHp != Fp.FromInt(1000)) throw new InvalidOperationException("PlayerBaseHp init");
            if (s.EnemyBaseHp != Fp.FromInt(1000)) throw new InvalidOperationException("EnemyBaseHp init");
            if (s.IsTerminated) throw new InvalidOperationException("Should not be terminated");
            if (s.EndReason != BattleEndReason.None) throw new InvalidOperationException("EndReason init");
        }

        private static void GetResultBeforeTerminationThrows()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
            bool threw = false;
            try { sim.GetResult(); }
            catch (InvalidOperationException) { threw = true; }
            if (!threw) throw new InvalidOperationException("GetResult should throw before termination.");
        }

        private static void CommandFactoriesEnforceLaneRequirements()
        {
            bool threw;
            threw = false;
#pragma warning disable CS8625
            try { BattleCommand.SpawnDroneSquad(0, 0, null); }
#pragma warning restore CS8625
            catch (ArgumentException) { threw = true; }
            if (!threw) throw new InvalidOperationException("SpawnDroneSquad must require laneId.");

            threw = false;
            try { BattleCommand.DeployPilot(0, 0, ""); }
            catch (ArgumentException) { threw = true; }
            if (!threw) throw new InvalidOperationException("DeployPilot must require laneId.");

            // RecallPilot: lane policy unresolved — null must NOT throw here (PD-A).
#pragma warning disable CS8625
            BattleCommand recall = BattleCommand.RecallPilot(0, 0, null);
#pragma warning restore CS8625
            if (recall.CommandType != BattleCommandType.RecallPilot)
                throw new InvalidOperationException("RecallPilot command type mismatch.");
        }

        private static void BattleResultFromTimeOutResolvesByRatio()
        {
            BattleResult win = BattleResult.FromTimeOut(3600,
                Fp.FromFraction(7, 10), Fp.FromFraction(3, 10));
            if (win.Outcome != BattleOutcome.Victory) throw new InvalidOperationException("TimeOut win");
            if (win.EndReason != BattleEndReason.TimeOut) throw new InvalidOperationException("TimeOut reason");

            BattleResult tie = BattleResult.FromTimeOut(3600,
                Fp.FromFraction(5, 10), Fp.FromFraction(5, 10));
            if (tie.Outcome != BattleOutcome.Defeat) throw new InvalidOperationException("Tie must be Defeat");

            BattleResult loss = BattleResult.FromTimeOut(3600,
                Fp.FromFraction(2, 10), Fp.FromFraction(4, 10));
            if (loss.Outcome != BattleOutcome.Defeat) throw new InvalidOperationException("TimeOut loss");
        }

        // ------------------------------------------------------------------ v0.1 tests

        private static void AdvanceTick_IncrementsTick()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
            if (sim.CurrentTick != 0) throw new InvalidOperationException("Initial CurrentTick should be 0.");
            sim.AdvanceTick();
            if (sim.CurrentTick != 1) throw new InvalidOperationException("CurrentTick should be 1.");
            sim.AdvanceTick();
            if (sim.CurrentTick != 2) throw new InvalidOperationException("CurrentTick should be 2.");
        }

        private static void Energy_RegeneratesAndClampsAtMax()
        {
            // Normal regen: 0 + 0.25 = 0.25
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.Zero, Fp.FromInt(100), Fp.FromFraction(1, 4)),
                MinimalInitialState());
            sim.AdvanceTick();
            Fp energy = sim.GetState().PlayerEnergy;
            if (energy != Fp.FromFraction(1, 4))
                throw new InvalidOperationException("Energy should regen to 0.25. Got: " + energy);

            // Clamp: 99 + 2 = 101 → 100.
            BattleSimulator sim2 = new BattleSimulator(
                MakeConfig(Fp.FromInt(99), Fp.FromInt(100), Fp.FromInt(2)),
                MinimalInitialState());
            sim2.AdvanceTick();
            Fp energy2 = sim2.GetState().PlayerEnergy;
            if (energy2 != Fp.FromInt(100))
                throw new InvalidOperationException("Energy should clamp at MaxEnergy. Got: " + energy2);
        }

        private static void SlotDroneCooldown_DecreasesEachTick()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));
            sim.AdvanceTick();
            int cd1 = GetSlotState(sim.GetState(), 0).DroneCooldownTick;
            if (cd1 != 99)
                throw new InvalidOperationException("Drone cooldown should be 99. Got: " + cd1);

            sim.AdvanceTick();
            int cd2 = GetSlotState(sim.GetState(), 0).DroneCooldownTick;
            if (cd2 != 98)
                throw new InvalidOperationException("Drone cooldown should be 98. Got: " + cd2);
        }

        private static void PilotCooldown_DecreasesEachTick()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());

            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground"));
            sim.AdvanceTick(); // tick → 1
#pragma warning disable CS8625
            sim.SubmitCommand(BattleCommand.RecallPilot(1, 0, null));
#pragma warning restore CS8625
            sim.AdvanceTick(); // tick → 2, pilotCooldown = 100 → 99

            int cd1 = GetSlotState(sim.GetState(), 0).PilotCooldownTick;
            if (cd1 != 99)
                throw new InvalidOperationException("Pilot cooldown should be 99. Got: " + cd1);

            sim.AdvanceTick(); // tick → 3, pilotCooldown → 98
            int cd2 = GetSlotState(sim.GetState(), 0).PilotCooldownTick;
            if (cd2 != 98)
                throw new InvalidOperationException("Pilot cooldown should be 98. Got: " + cd2);
        }

        private static void SpawnDroneSquad_ConsumesEnergyAndStartsCooldown()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));
            sim.AdvanceTick();

            BattleState state = sim.GetState();
            if (state.PlayerEnergy != Fp.Zero)
                throw new InvalidOperationException("Energy should be 0 after spawn. Got: " + state.PlayerEnergy);
            int cd = GetSlotState(state, 0).DroneCooldownTick;
            if (cd != 99)
                throw new InvalidOperationException("Drone cooldown should be 99. Got: " + cd);
        }

        private static void SpawnDroneSquad_RejectsInvalidLane()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "no_such_lane"));
            bool threw = false;
            try { sim.AdvanceTick(); }
            catch (ArgumentException) { threw = true; }
            if (!threw) throw new InvalidOperationException("SpawnDroneSquad should reject invalid lane.");
        }

        private static void SpawnDroneSquad_RejectsInsufficientEnergy()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));
            bool threw = false;
            try { sim.AdvanceTick(); }
            catch (InvalidOperationException) { threw = true; }
            if (!threw) throw new InvalidOperationException("SpawnDroneSquad should reject insufficient energy.");
        }

        private static void SpawnDroneSquad_RejectsWhenPilotDeployed()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(50), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());

            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground"));
            sim.AdvanceTick(); // tick → 1, pilot deployed

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(1, 0, "lane_ground"));
            bool threw = false;
            try { sim.AdvanceTick(); }
            catch (InvalidOperationException) { threw = true; }
            if (!threw) throw new InvalidOperationException("SpawnDroneSquad should reject when pilot is deployed.");
        }

        private static void DeployPilot_DoesNotConsumeEnergy()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(50), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());
            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground"));
            sim.AdvanceTick();
            Fp energy = sim.GetState().PlayerEnergy;
            if (energy != Fp.FromInt(50))
                throw new InvalidOperationException("DeployPilot should not consume energy. Got: " + energy);
        }

        private static void DeployPilot_SetsPilotDeployed()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
            if (GetSlotState(sim.GetState(), 0).IsPilotDeployed)
                throw new InvalidOperationException("Pilot should not be deployed initially.");

            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground"));
            sim.AdvanceTick();

            if (!GetSlotState(sim.GetState(), 0).IsPilotDeployed)
                throw new InvalidOperationException("Pilot should be deployed after DeployPilot command.");
        }

        private static void RecallPilot_ClearsPilotAndStartsCooldown()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground"));
            sim.AdvanceTick(); // tick → 1

#pragma warning disable CS8625
            sim.SubmitCommand(BattleCommand.RecallPilot(1, 0, null));
#pragma warning restore CS8625
            sim.AdvanceTick(); // tick → 2

            SlotState slot = GetSlotState(sim.GetState(), 0);
            if (slot.IsPilotDeployed)
                throw new InvalidOperationException("Pilot should not be deployed after recall.");
            if (slot.PilotCooldownTick != 99)
                throw new InvalidOperationException("Pilot cooldown should be 99. Got: " + slot.PilotCooldownTick);
        }

        private static void RecallPilot_RejectsWhenNotDeployed()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
#pragma warning disable CS8625
            sim.SubmitCommand(BattleCommand.RecallPilot(0, 0, null));
#pragma warning restore CS8625
            bool threw = false;
            try { sim.AdvanceTick(); }
            catch (InvalidOperationException) { threw = true; }
            if (!threw) throw new InvalidOperationException("RecallPilot should reject when pilot is not deployed.");
        }

        private static void MaxBattleTick_TerminatesBattle()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.Zero, Fp.FromInt(100), Fp.Zero, maxBattleTick: 3),
                MinimalInitialState());

            sim.AdvanceTick();
            if (sim.IsTerminated) throw new InvalidOperationException("Should not terminate at tick 1.");
            sim.AdvanceTick();
            if (sim.IsTerminated) throw new InvalidOperationException("Should not terminate at tick 2.");
            sim.AdvanceTick();
            if (!sim.IsTerminated) throw new InvalidOperationException("Should terminate at tick 3.");

            BattleResult result = sim.GetResult();
            if (result.EndReason != BattleEndReason.TimeOut)
                throw new InvalidOperationException("EndReason should be TimeOut. Got: " + result.EndReason);
        }

        private static void TimeoutTie_ProducesDefeat()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.Zero, Fp.FromInt(100), Fp.Zero, maxBattleTick: 1),
                MinimalInitialState());

            sim.AdvanceTick();

            BattleResult result = sim.GetResult();
            if (result.Outcome != BattleOutcome.Defeat)
                throw new InvalidOperationException("Timeout tie should produce Defeat. Got: " + result.Outcome);
        }

        // ------------------------------------------------------------------ v0.2 tests

        // v2-1. SpawnDroneSquad creates player entity in selected lane.
        private static void SpawnDroneSquad_CreatesEntityInLane()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));
            sim.AdvanceTick();

            BattleEntity? entity = FindByOwner(sim.GetState(), "lane_ground", OwnerSide.Player);
            if (entity == null)
                throw new InvalidOperationException("SpawnDroneSquad should create a player entity in lane_ground.");
            if (entity.OwnerSide != OwnerSide.Player)
                throw new InvalidOperationException("Entity owner should be Player.");
        }

        // v2-2. DeployPilot creates player pilot entity in selected lane.
        private static void DeployPilot_CreatesEntityInLane()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());

            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground"));
            sim.AdvanceTick();

            BattleEntity? entity = FindByOwner(sim.GetState(), "lane_ground", OwnerSide.Player);
            if (entity == null)
                throw new InvalidOperationException("DeployPilot should create a player entity in lane_ground.");
        }

        // v2-3. RecallPilot removes deployed pilot entity.
        private static void RecallPilot_RemovesEntityFromLane()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());

            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground"));
            sim.AdvanceTick(); // pilot entity created

#pragma warning disable CS8625
            sim.SubmitCommand(BattleCommand.RecallPilot(1, 0, null));
#pragma warning restore CS8625
            sim.AdvanceTick(); // pilot entity removed

            LaneState lane = GetLane(sim.GetState(), "lane_ground");
            if (lane.Entities.Count != 0)
                throw new InvalidOperationException(
                    "Lane should be empty after pilot recall. Found " + lane.Entities.Count + " entities.");
        }

        // v2-4. Enemy spawn schedule creates enemy entity at configured tick.
        private static void EnemySpawnSchedule_CreatesEnemyAtConfiguredTick()
        {
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.Zero, Fp.FromInt(100), Fp.Zero,
                enemySpawns: new EnemySpawnDefinition[]
                {
                    new EnemySpawnDefinition(1, "lane_ground", Fp.FromInt(50), Fp.FromInt(5), 500L, 200L),
                });
            BattleSimulator sim = new BattleSimulator(cfg, MinimalInitialState());

            sim.AdvanceTick(); // tick → 1, enemy spawns

            BattleEntity? enemy = FindByOwner(sim.GetState(), "lane_ground", OwnerSide.Enemy);
            if (enemy == null)
                throw new InvalidOperationException("Enemy should have spawned at tick 1 per schedule.");
            if (enemy.OwnerSide != OwnerSide.Enemy)
                throw new InvalidOperationException("Entity owner should be Enemy.");
        }

        // v2-5. Player entity moves toward enemy base within same lane.
        private static void PlayerEntity_MovesTowardEnemyBase()
        {
            // Large lane, no enemies → drone moves forward freely.
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));
            sim.AdvanceTick(); // tick → 1, drone at 0+500=500

            BattleEntity? drone = FindByOwner(sim.GetState(), "lane_ground", OwnerSide.Player);
            if (drone == null) throw new InvalidOperationException("Player entity not found.");
            if (drone.PositionMilli <= 0)
                throw new InvalidOperationException(
                    "Player entity should have moved toward enemy base. Pos: " + drone.PositionMilli);
        }

        // v2-6. Enemy entity moves toward player base within same lane.
        private static void EnemyEntity_MovesTowardPlayerBase()
        {
            long laneLen = DefaultLaneLen;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.Zero, Fp.FromInt(100), Fp.Zero,
                enemySpawns: new EnemySpawnDefinition[]
                {
                    new EnemySpawnDefinition(1, "lane_ground", Fp.FromInt(50), Fp.FromInt(5), 500L, 200L),
                });
            BattleSimulator sim = new BattleSimulator(cfg, MinimalInitialState());

            sim.AdvanceTick(); // tick → 1: enemy spawns at laneLen, moves to laneLen-200

            BattleEntity? enemy = FindByOwner(sim.GetState(), "lane_ground", OwnerSide.Enemy);
            if (enemy == null) throw new InvalidOperationException("Enemy entity not found.");
            if (enemy.PositionMilli >= laneLen)
                throw new InvalidOperationException(
                    "Enemy entity should have moved toward player base. Pos: " + enemy.PositionMilli);
        }

        // v2-7. Entity never changes lane.
        private static void Entity_NeverChangesLane()
        {
            // Two-lane config; entity spawned in lane_a must never appear in lane_b.
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_a", LaneType.Ground, DefaultLaneLen),
                new LaneDefinition("lane_b", LaneType.Ground, DefaultLaneLen),
            };
            BattleConfigSnapshot cfg = new BattleConfigSnapshot(
                "test", Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                200, 100, 50,
                Fp.FromInt(1000), Fp.FromInt(1000), 3600, lanes);
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, new SlotDefinition[]
            {
                new SlotDefinition(
                    0, "pilot_a", "drone_a", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(10), 1_000L, 500L,
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a"));
            for (int i = 0; i < 5; i++) sim.AdvanceTick();

            BattleState state = sim.GetState();
            bool foundInA = false;
            foreach (LaneState ls in state.Lanes)
            {
                if (ls.LaneId == "lane_b" && ls.Entities.Count > 0)
                    throw new InvalidOperationException("Entity appeared in lane_b — lane crossing detected!");
                if (ls.LaneId == "lane_a" && ls.Entities.Count > 0)
                    foundInA = true;
            }
            if (!foundInA) throw new InvalidOperationException("Player entity not found in lane_a.");
        }

        // v2-8. Entity attacks nearest enemy in same lane (doesn't move while attacking).
        private static void Entity_AttacksNearestEnemyInSameLane()
        {
            // Short lane; drone range covers the full lane → always in attack range of spawned enemy.
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                enemySpawns: new EnemySpawnDefinition[]
                {
                    // Spawns at tick 1 at position laneLen=1000; drone range=2000 covers it.
                    new EnemySpawnDefinition(1, "lane_ground", Fp.FromInt(50), Fp.FromInt(5), 500L, 100L),
                });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, new SlotDefinition[]
            {
                new SlotDefinition(
                    0, "pilot_a", "drone_a", Fp.FromInt(10), 100,
                    Fp.FromInt(200), Fp.FromInt(10), 2_000L, 500L, // drone range=2000 (> laneLen=1000)
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));
            sim.AdvanceTick(); // tick 0→1: drone at 0, enemy at 1000; drone attacks enemy, enemy moves

            BattleState state = sim.GetState();
            BattleEntity? drone = FindByOwner(state, "lane_ground", OwnerSide.Player);
            BattleEntity? enemy = FindByOwner(state, "lane_ground", OwnerSide.Enemy);

            if (drone == null) throw new InvalidOperationException("Drone not found after tick.");
            if (drone.PositionMilli != 0)
                throw new InvalidOperationException(
                    "Drone should not have moved while attacking. Pos: " + drone.PositionMilli);
            if (enemy == null) throw new InvalidOperationException("Enemy should still be alive (HP > 0).");
            if (enemy.Hp >= Fp.FromInt(50))
                throw new InvalidOperationException(
                    "Enemy HP should have decreased from drone attack. HP: " + enemy.Hp);
        }

        // v2-9. Dead entity is removed.
        private static void DeadEntity_IsRemoved()
        {
            // Drone attack (10) × 1 hit kills enemy (HP=5).
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                enemySpawns: new EnemySpawnDefinition[]
                {
                    new EnemySpawnDefinition(1, "lane_ground", Fp.FromInt(5), Fp.FromInt(2), 500L, 100L),
                });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, new SlotDefinition[]
            {
                new SlotDefinition(
                    0, "pilot_a", "drone_a", Fp.FromInt(10), 100,
                    Fp.FromInt(200), Fp.FromInt(10), 2_000L, 500L, // drone attack=10, kills enemy(hp=5) in 1 hit
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));
            sim.AdvanceTick(); // drone attacks enemy → enemy dies → removed

            LaneState lane = GetLane(sim.GetState(), "lane_ground");
            int enemyCount = 0;
            foreach (BattleEntity e in lane.Entities)
                if (e.OwnerSide == OwnerSide.Enemy) enemyCount++;
            if (enemyCount != 0)
                throw new InvalidOperationException("Dead enemy should have been removed. Found: " + enemyCount);
        }

        // v2-10. Player entity reaching enemy base damages enemy base.
        private static void PlayerEntity_DamagesEnemyBase()
        {
            // Lane 1000 milli, drone speed 2000 (exceeds lane) → reaches enemy base in 1 tick.
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen);
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, new SlotDefinition[]
            {
                new SlotDefinition(
                    0, "pilot_a", "drone_a", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(10), 500L, 2_000L, // speed=2000 > laneLen=1000
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));
            sim.AdvanceTick(); // drone reaches laneLen → attacks enemy base

            Fp enemyBaseHp = sim.GetState().EnemyBaseHp;
            if (enemyBaseHp >= Fp.FromInt(1000))
                throw new InvalidOperationException(
                    "Enemy base HP should have decreased. Got: " + enemyBaseHp);
        }

        // v2-11. Enemy entity reaching player base damages player base.
        private static void EnemyEntity_DamagesPlayerBase()
        {
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.Zero, Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                enemySpawns: new EnemySpawnDefinition[]
                {
                    // speed=2000 exceeds laneLen=1000 → reaches player base in same tick
                    new EnemySpawnDefinition(1, "lane_ground", Fp.FromInt(50), Fp.FromInt(10), 500L, 2_000L),
                });
            BattleSimulator sim = new BattleSimulator(cfg, MinimalInitialState());

            sim.AdvanceTick(); // enemy spawns at 1000, moves to 0 → attacks player base

            Fp playerBaseHp = sim.GetState().PlayerBaseHp;
            if (playerBaseHp >= Fp.FromInt(1000))
                throw new InvalidOperationException(
                    "Player base HP should have decreased. Got: " + playerBaseHp);
        }

        // v2-12. Enemy base destroyed produces Victory + EnemyBaseDestroyed.
        private static void EnemyBaseDestroyed_ProducesVictory()
        {
            // EnemyBaseHp=1, drone attack=50, lane=1000, speed=2000 → reaches base in 1 tick → destroys it.
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                enemyBaseHp: Fp.FromInt(1));
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, new SlotDefinition[]
            {
                new SlotDefinition(
                    0, "pilot_a", "drone_a", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(50), 500L, 2_000L, // attack=50, speed=2000
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));
            sim.AdvanceTick(); // drone reaches enemy base (HP=1), attacks for 50 → enemy base destroyed

            if (!sim.IsTerminated)
                throw new InvalidOperationException("Battle should have terminated.");
            BattleResult result = sim.GetResult();
            if (result.Outcome != BattleOutcome.Victory)
                throw new InvalidOperationException("Outcome should be Victory. Got: " + result.Outcome);
            if (result.EndReason != BattleEndReason.EnemyBaseDestroyed)
                throw new InvalidOperationException("EndReason should be EnemyBaseDestroyed. Got: " + result.EndReason);
        }

        // v2-13. Player base destroyed produces Defeat + PlayerBaseDestroyed.
        private static void PlayerBaseDestroyed_ProducesDefeat()
        {
            // PlayerBaseHp=1, enemy attack=50, lane=1000, speed=2000 → reaches player base in 1 tick.
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.Zero, Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                playerBaseHp: Fp.FromInt(1),
                enemySpawns: new EnemySpawnDefinition[]
                {
                    new EnemySpawnDefinition(1, "lane_ground", Fp.FromInt(50), Fp.FromInt(50), 500L, 2_000L),
                });
            BattleSimulator sim = new BattleSimulator(cfg, MinimalInitialState());

            sim.AdvanceTick(); // enemy spawns → reaches player base (HP=1) → attacks for 50 → destroyed

            if (!sim.IsTerminated)
                throw new InvalidOperationException("Battle should have terminated.");
            BattleResult result = sim.GetResult();
            if (result.Outcome != BattleOutcome.Defeat)
                throw new InvalidOperationException("Outcome should be Defeat. Got: " + result.Outcome);
            if (result.EndReason != BattleEndReason.PlayerBaseDestroyed)
                throw new InvalidOperationException("EndReason should be PlayerBaseDestroyed. Got: " + result.EndReason);
        }

        // v2-14. Timeout behavior from v0.1 remains passing.
        private static void MaxBattleTick_TimeoutStillWorks()
        {
            // No entities, no spawns; should time out cleanly.
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.Zero, Fp.FromInt(100), Fp.Zero, maxBattleTick: 2),
                MinimalInitialState());
            sim.AdvanceTick();
            sim.AdvanceTick();
            if (!sim.IsTerminated) throw new InvalidOperationException("Should be terminated at maxBattleTick.");
            if (sim.GetResult().EndReason != BattleEndReason.TimeOut)
                throw new InvalidOperationException("EndReason should be TimeOut.");
        }

        // ------------------------------------------------------------------ smoke scenario tests

        // smoke-1. Player victory scenario: SpawnDroneSquad destroys enemy base at tick 1.
        private static void Smoke_PlayerVictory_ProducesExpectedResult()
        {
            var (cfg, initial, commands) = SmokeScenarios.PlayerVictory();
            BattleResult result = SmokeScenarios.RunToCompletion(cfg, initial, commands);

            if (result.Outcome != BattleOutcome.Victory)
                throw new InvalidOperationException(
                    "Smoke Victory: outcome should be Victory. Got: " + result.Outcome);
            if (result.EndReason != BattleEndReason.EnemyBaseDestroyed)
                throw new InvalidOperationException(
                    "Smoke Victory: end reason should be EnemyBaseDestroyed. Got: " + result.EndReason);
            if (result.ClearTimeTick != 1)
                throw new InvalidOperationException(
                    "Smoke Victory: should end at tick 1. Got: " + result.ClearTimeTick);
            if (result.PlayerBaseHpRatio != Fp.One)
                throw new InvalidOperationException(
                    "Smoke Victory: player base hp ratio should be 1.0. Got: " + result.PlayerBaseHpRatio);
            if (result.EnemyBaseHpRatio != Fp.Zero)
                throw new InvalidOperationException(
                    "Smoke Victory: enemy base hp ratio should be 0.0. Got: " + result.EnemyBaseHpRatio);
        }

        // smoke-2. Player defeat scenario: enemy schedule destroys player base at tick 1.
        private static void Smoke_PlayerDefeat_ProducesExpectedResult()
        {
            var (cfg, initial, commands) = SmokeScenarios.PlayerDefeat();
            BattleResult result = SmokeScenarios.RunToCompletion(cfg, initial, commands);

            if (result.Outcome != BattleOutcome.Defeat)
                throw new InvalidOperationException(
                    "Smoke Defeat: outcome should be Defeat. Got: " + result.Outcome);
            if (result.EndReason != BattleEndReason.PlayerBaseDestroyed)
                throw new InvalidOperationException(
                    "Smoke Defeat: end reason should be PlayerBaseDestroyed. Got: " + result.EndReason);
            if (result.ClearTimeTick != 1)
                throw new InvalidOperationException(
                    "Smoke Defeat: should end at tick 1. Got: " + result.ClearTimeTick);
            if (result.PlayerBaseHpRatio != Fp.Zero)
                throw new InvalidOperationException(
                    "Smoke Defeat: player base hp ratio should be 0.0. Got: " + result.PlayerBaseHpRatio);
            if (result.EnemyBaseHpRatio != Fp.One)
                throw new InvalidOperationException(
                    "Smoke Defeat: enemy base hp ratio should be 1.0. Got: " + result.EnemyBaseHpRatio);
        }

        // smoke-3. Timeout scenario: no commands, 3 ticks, equal HP → Defeat.
        private static void Smoke_TimeoutDefeat_ProducesExpectedResult()
        {
            var (cfg, initial, commands) = SmokeScenarios.TimeoutDefeat();
            BattleResult result = SmokeScenarios.RunToCompletion(cfg, initial, commands);

            if (result.Outcome != BattleOutcome.Defeat)
                throw new InvalidOperationException(
                    "Smoke Timeout: outcome should be Defeat. Got: " + result.Outcome);
            if (result.EndReason != BattleEndReason.TimeOut)
                throw new InvalidOperationException(
                    "Smoke Timeout: end reason should be TimeOut. Got: " + result.EndReason);
            if (result.ClearTimeTick != 3)
                throw new InvalidOperationException(
                    "Smoke Timeout: should end at tick 3. Got: " + result.ClearTimeTick);
            if (result.PlayerBaseHpRatio != Fp.One)
                throw new InvalidOperationException(
                    "Smoke Timeout: player base hp ratio should be 1.0. Got: " + result.PlayerBaseHpRatio);
            if (result.EnemyBaseHpRatio != Fp.One)
                throw new InvalidOperationException(
                    "Smoke Timeout: enemy base hp ratio should be 1.0. Got: " + result.EnemyBaseHpRatio);
        }

        // smoke-4. Running the victory scenario twice produces identical results.
        private static void Smoke_PlayerVictory_IsDeterministicAcrossRepeatedRuns()
        {
            var (cfgA, initialA, commandsA) = SmokeScenarios.PlayerVictory();
            var (cfgB, initialB, commandsB) = SmokeScenarios.PlayerVictory();

            BattleResult ra = SmokeScenarios.RunToCompletion(cfgA, initialA, commandsA);
            BattleResult rb = SmokeScenarios.RunToCompletion(cfgB, initialB, commandsB);

            if (ra.Outcome != rb.Outcome)
                throw new InvalidOperationException("Smoke Determinism: outcome mismatch.");
            if (ra.EndReason != rb.EndReason)
                throw new InvalidOperationException("Smoke Determinism: end reason mismatch.");
            if (ra.ClearTimeTick != rb.ClearTimeTick)
                throw new InvalidOperationException("Smoke Determinism: clear_time_tick mismatch.");
            if (ra.PlayerBaseHpRatio != rb.PlayerBaseHpRatio)
                throw new InvalidOperationException("Smoke Determinism: player_base_hp_ratio mismatch.");
            if (ra.EnemyBaseHpRatio != rb.EnemyBaseHpRatio)
                throw new InvalidOperationException("Smoke Determinism: enemy_base_hp_ratio mismatch.");
        }

        // v2-15. Same config + initial + command sequence produces same BattleResult (determinism).
        private static void Determinism_SameSetupProducesSameResult()
        {
            long laneLen = 2_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                maxBattleTick: 10,
                laneLengthMilli: laneLen,
                enemySpawns: new EnemySpawnDefinition[]
                {
                    new EnemySpawnDefinition(1, "lane_ground", Fp.FromInt(30), Fp.FromInt(5), 500L, 200L),
                });
            BattleInitialState initial = MinimalInitialState();

            BattleSimulator simA = new BattleSimulator(cfg, initial);
            BattleSimulator simB = new BattleSimulator(cfg, initial);

            simA.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));
            simB.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground"));

            for (int i = 0; i < 10 && !simA.IsTerminated; i++) simA.AdvanceTick();
            for (int i = 0; i < 10 && !simB.IsTerminated; i++) simB.AdvanceTick();

            if (simA.IsTerminated != simB.IsTerminated)
                throw new InvalidOperationException("Determinism: termination state mismatch.");
            if (!simA.IsTerminated) return; // neither terminated, state comparison would also suffice

            BattleResult ra = simA.GetResult();
            BattleResult rb = simB.GetResult();
            if (ra.Outcome != rb.Outcome)
                throw new InvalidOperationException(
                    "Determinism: outcome mismatch. A=" + ra.Outcome + " B=" + rb.Outcome);
            if (ra.EndReason != rb.EndReason)
                throw new InvalidOperationException("Determinism: EndReason mismatch.");
            if (ra.ClearTimeTick != rb.ClearTimeTick)
                throw new InvalidOperationException("Determinism: ClearTimeTick mismatch.");
        }
    }
}
