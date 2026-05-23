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
            // ---- skeleton tests ----
            ConstructsAndExposesInitialState();
            GetResultBeforeTerminationThrows();
            CommandFactoriesEnforceLaneRequirements();
            BattleResultFromTimeOutResolvesByRatio();

            // ---- v0.1 tests ----
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
            TimeoutTie_TieWinnerSideWins();

            // ---- smoke scenario tests ----
            Smoke_SideAVictory_ProducesExpectedResult();
            Smoke_SideBVictory_ProducesExpectedResult();
            Smoke_TimeoutSideBTiebreak_ProducesExpectedResult();
            Smoke_SideAVictory_IsDeterministicAcrossRepeatedRuns();

            // ---- v0.2 tests ----
            SpawnDroneSquad_CreatesEntityInLane();
            DeployPilot_CreatesEntityInLane();
            RecallPilot_RemovesEntityFromLane();
            SideBCommand_CreatesEntityAtFarEnd();
            SideAEntity_MovesTowardSideBBase();
            SideBEntity_MovesTowardSideABase();
            SideAAndSideB_CanSubmitCommandsAtSameTick();
            Entity_NeverChangesLane();
            Entity_AttacksNearestOpponentInSameLane();
            Entity_OnlyAttacksOppositeOwner();
            DeadEntity_IsRemoved();
            SideAEntity_DamagesSideBBase();
            SideBEntity_DamagesSideABase();
            SideBBaseDestroyed_SideAWins();
            SideABaseDestroyed_SideBWins();
            Timeout_SideAWinsByHpRatio();
            Timeout_SideBWinsByHpRatio();
            MaxBattleTick_TimeoutStillWorks();
            Determinism_SameSetupProducesSameResult();

            // ---- v0.4 tests ----
            Defense_ReducesDamageButNotBelowMinDamage();
            AttackPeriod_PreventsEveryTickAttack();
            AttackCooldown_HoldsPositionWhenTargetInRange();
            CrossLaneTargeting_UsesLaneWorldYManhattanDistance();
            CrossLaneTargeting_DoesNotHitWhenVerticalDistanceOutOfRange();
            TargetTieBreak_UsesNumericId();

            // ---- v0.5 tests ----
            Melee_SameLane_DamagesImmediately();
            Projectile_DoesNotDamageOnFireTick();
            Projectile_HitsAfterTravelTime();
            Projectile_MissesIfTargetRemovedBeforeArrival();
            Projectile_MissesIfTargetMovedOutsideHitRadius();
            Projectile_CrossLane_SpawnsOnTargetLane();
            Projectile_StateSnapshot_IsReturnedByGetState();
            Projectile_Determinism_SameSetupProducesSameResult();
            SlotDefinition_RejectsUnknownAttackKind();
        }

        // ------------------------------------------------------------------ config / state helpers

        private static readonly long DefaultLaneLen = 100_000L;

        private static BattleSideConfig SideACfg(
            Fp initialEnergy, Fp maxEnergy, Fp energyRegen,
            Fp? baseHp = null)
        {
            return new BattleSideConfig(
                BattleSide.SideA, baseHp ?? Fp.FromInt(1000),
                initialEnergy, maxEnergy, energyRegen);
        }

        private static BattleSideConfig SideBCfg(
            Fp? initialEnergy = null, Fp? maxEnergy = null, Fp? energyRegen = null,
            Fp? baseHp = null)
        {
            return new BattleSideConfig(
                BattleSide.SideB, baseHp ?? Fp.FromInt(1000),
                initialEnergy ?? Fp.Zero,
                maxEnergy ?? Fp.FromInt(100),
                energyRegen ?? Fp.Zero);
        }

        /// <summary>
        /// One ground lane, SideA energy 0→100 at 0.25/tick, maxBattleTick 3600.
        /// </summary>
        private static BattleConfigSnapshot MinimalConfig()
        {
            return MakeConfig(
                sideAInitialEnergy:    Fp.FromInt(0),
                sideAMaxEnergy:        Fp.FromInt(100),
                sideAEnergyRegenPerTick: Fp.FromFraction(1, 4));
        }

        private static BattleConfigSnapshot MakeConfig(
            Fp sideAInitialEnergy,
            Fp sideAMaxEnergy,
            Fp sideAEnergyRegenPerTick,
            int maxBattleTick = 3600,
            long laneLengthMilli = 0,
            Fp? sideABaseHp = null,
            Fp? sideBBaseHp = null,
            Fp? sideBInitialEnergy = null,
            Fp? sideBMaxEnergy = null,
            Fp? sideBEnergyRegen = null,
            BattleSide timeOutTieWinnerSide = BattleSide.SideB)
        {
            long laneLen = laneLengthMilli > 0 ? laneLengthMilli : DefaultLaneLen;
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_ground", LaneType.Ground, laneLen, 0L),
            };
            BattleSideConfig cfgA = new BattleSideConfig(
                BattleSide.SideA, sideABaseHp ?? Fp.FromInt(1000),
                sideAInitialEnergy, sideAMaxEnergy, sideAEnergyRegenPerTick);
            BattleSideConfig cfgB = new BattleSideConfig(
                BattleSide.SideB, sideBBaseHp ?? Fp.FromInt(1000),
                sideBInitialEnergy ?? Fp.Zero,
                sideBMaxEnergy ?? Fp.FromInt(100),
                sideBEnergyRegen ?? Fp.Zero);
            return new BattleConfigSnapshot(
                configVersion:              "test",
                sideA:                      cfgA,
                sideB:                      cfgB,
                pilotDeployCooldownTick:    200,
                pilotReturnCooldownTick:    100,
                pilotKnockoutDroneResumeTick: 50,
                maxBattleTick:              maxBattleTick,
                lanes:                      lanes,
                timeOutTieWinnerSide:       timeOutTieWinnerSide);
        }

        private static SlotDefinition CreateTestSlotDef(
            int slotIndex,
            string pilotId,
            string droneSquadId,
            Fp energyCost,
            int cooldownTick,
            Fp droneHp,
            Fp droneAttack,
            long droneRangeMilli,
            long droneSpeedMilliPerTick,
            Fp pilotHp,
            Fp pilotAttack,
            long pilotRangeMilli,
            long pilotSpeedMilliPerTick,
            Fp? droneDefense = null,
            int droneAttackPeriodTick = 1,
            Fp? pilotDefense = null,
            int pilotAttackPeriodTick = 1)
        {
            return new SlotDefinition(
                slotIndex, pilotId, droneSquadId, energyCost, cooldownTick,
                droneHp, droneAttack, droneDefense ?? Fp.Zero, droneRangeMilli, droneSpeedMilliPerTick, droneAttackPeriodTick,
                pilotHp, pilotAttack, pilotDefense ?? Fp.Zero, pilotRangeMilli, pilotSpeedMilliPerTick, pilotAttackPeriodTick);
        }

        private static SlotDefinition CreateTestSlotDefProjectile(
            int slotIndex,
            string pilotId,
            string droneSquadId,
            Fp energyCost,
            int cooldownTick,
            Fp droneHp,
            Fp droneAttack,
            long droneRangeMilli,
            long droneSpeedMilliPerTick,
            AttackKind droneAttackKind,
            long droneProjectileSpeedMilliPerTick,
            Fp pilotHp,
            Fp pilotAttack,
            long pilotRangeMilli,
            long pilotSpeedMilliPerTick,
            Fp? droneDefense = null,
            int droneAttackPeriodTick = 1,
            Fp? pilotDefense = null,
            int pilotAttackPeriodTick = 1)
        {
            return new SlotDefinition(
                slotIndex, pilotId, droneSquadId, energyCost, cooldownTick,
                droneHp, droneAttack, droneDefense ?? Fp.Zero, droneRangeMilli, droneSpeedMilliPerTick, droneAttackPeriodTick,
                droneAttackKind, droneProjectileSpeedMilliPerTick,
                pilotHp, pilotAttack, pilotDefense ?? Fp.Zero, pilotRangeMilli, pilotSpeedMilliPerTick, pilotAttackPeriodTick,
                AttackKind.Melee, 0L);
        }

        /// <summary>
        /// Slot 0: energyCost 10, cooldown 100, droneHp 100/atk 10/range 1000/speed 500,
        /// pilotHp 200/atk 20/range 1500/speed 300.
        /// </summary>
        private static BattleSideInitialState DefaultSideInitialState(BattleSide side)
        {
            return new BattleSideInitialState(side, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    slotIndex: 0, pilotId: "pilot_a", droneSquadId: "drone_a",
                    energyCost: Fp.FromInt(10), cooldownTick: 100,
                    droneHp: Fp.FromInt(100), droneAttack: Fp.FromInt(10),
                    droneRangeMilli: 1_000L, droneSpeedMilliPerTick: 500L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
        }

        private static BattleInitialState MinimalInitialState()
        {
            return new BattleInitialState("stage_1", 42L,
                DefaultSideInitialState(BattleSide.SideA),
                DefaultSideInitialState(BattleSide.SideB));
        }

        private static BattleSideState GetSideState(BattleState state, BattleSide side)
        {
            foreach (BattleSideState ss in state.Sides)
                if (ss.Side == side) return ss;
            throw new InvalidOperationException("Side " + side + " not found in BattleState.");
        }

        private static SlotState GetSlotState(BattleState state, BattleSide side, int slotIndex)
        {
            BattleSideState ss = GetSideState(state, side);
            foreach (SlotState s in ss.Slots)
                if (s.SlotIndex == slotIndex) return s;
            throw new InvalidOperationException("Slot " + slotIndex + " not found for side " + side);
        }

        private static LaneState GetLane(BattleState state, string laneId)
        {
            foreach (LaneState ls in state.Lanes)
                if (ls.LaneId == laneId) return ls;
            throw new InvalidOperationException("Lane '" + laneId + "' not found in BattleState.");
        }

        private static BattleEntity? FindByOwner(BattleState state, string laneId, BattleSide owner)
        {
            LaneState lane = GetLane(state, laneId);
            foreach (BattleEntity e in lane.Entities)
                if (e.Side == owner) return e;
            return null;
        }

        // ------------------------------------------------------------------ skeleton tests

        private static void ConstructsAndExposesInitialState()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
            BattleState s = sim.GetState();
            if (s.CurrentTick != 0) throw new InvalidOperationException("CurrentTick != 0");
            if (GetSideState(s, BattleSide.SideA).BaseHp != Fp.FromInt(1000))
                throw new InvalidOperationException("SideA BaseHp init");
            if (GetSideState(s, BattleSide.SideB).BaseHp != Fp.FromInt(1000))
                throw new InvalidOperationException("SideB BaseHp init");
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
            try { BattleCommand.SpawnDroneSquad(0, 0, null, BattleSide.SideA); }
#pragma warning restore CS8625
            catch (ArgumentException) { threw = true; }
            if (!threw) throw new InvalidOperationException("SpawnDroneSquad must require laneId.");

            threw = false;
            try { BattleCommand.DeployPilot(0, 0, "", BattleSide.SideA); }
            catch (ArgumentException) { threw = true; }
            if (!threw) throw new InvalidOperationException("DeployPilot must require laneId.");

            // RecallPilot: laneId may be null (keyed by slot+side).
#pragma warning disable CS8625
            BattleCommand recall = BattleCommand.RecallPilot(0, 0, null, BattleSide.SideA);
#pragma warning restore CS8625
            if (recall.CommandType != BattleCommandType.RecallPilot)
                throw new InvalidOperationException("RecallPilot command type mismatch.");
            if (recall.Side != BattleSide.SideA)
                throw new InvalidOperationException("RecallPilot side mismatch.");
        }

        private static void BattleResultFromTimeOutResolvesByRatio()
        {
            BattleResult win = BattleResult.FromTimeOut(3600,
                Fp.FromFraction(7, 10), Fp.FromFraction(3, 10), BattleSide.SideB);
            if (win.WinnerSide != BattleSide.SideA)
                throw new InvalidOperationException("SideA higher ratio should win. Got: " + win.WinnerSide);
            if (win.EndReason != BattleEndReason.TimeOut)
                throw new InvalidOperationException("EndReason should be TimeOut.");

            BattleResult winB = BattleResult.FromTimeOut(3600,
                Fp.FromFraction(3, 10), Fp.FromFraction(7, 10), BattleSide.SideB);
            if (winB.WinnerSide != BattleSide.SideB)
                throw new InvalidOperationException("SideB higher ratio should win.");

            BattleResult tie = BattleResult.FromTimeOut(3600,
                Fp.FromFraction(5, 10), Fp.FromFraction(5, 10), BattleSide.SideB);
            if (tie.WinnerSide != BattleSide.SideB)
                throw new InvalidOperationException("Tie should resolve to tieWinnerSide=SideB.");

            BattleResult tieA = BattleResult.FromTimeOut(3600,
                Fp.FromFraction(5, 10), Fp.FromFraction(5, 10), BattleSide.SideA);
            if (tieA.WinnerSide != BattleSide.SideA)
                throw new InvalidOperationException("Tie with tieWinnerSide=SideA should resolve to SideA.");
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
            Fp energy = GetSideState(sim.GetState(), BattleSide.SideA).Energy;
            if (energy != Fp.FromFraction(1, 4))
                throw new InvalidOperationException("Energy should regen to 0.25. Got: " + energy);

            // Clamp: 99 + 2 = 101 → 100.
            BattleSimulator sim2 = new BattleSimulator(
                MakeConfig(Fp.FromInt(99), Fp.FromInt(100), Fp.FromInt(2)),
                MinimalInitialState());
            sim2.AdvanceTick();
            Fp energy2 = GetSideState(sim2.GetState(), BattleSide.SideA).Energy;
            if (energy2 != Fp.FromInt(100))
                throw new InvalidOperationException("Energy should clamp at MaxEnergy. Got: " + energy2);
        }

        private static void SlotDroneCooldown_DecreasesEachTick()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick();
            int cd1 = GetSlotState(sim.GetState(), BattleSide.SideA, 0).DroneCooldownTick;
            if (cd1 != 99)
                throw new InvalidOperationException("Drone cooldown should be 99. Got: " + cd1);

            sim.AdvanceTick();
            int cd2 = GetSlotState(sim.GetState(), BattleSide.SideA, 0).DroneCooldownTick;
            if (cd2 != 98)
                throw new InvalidOperationException("Drone cooldown should be 98. Got: " + cd2);
        }

        private static void PilotCooldown_DecreasesEachTick()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());

            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick(); // tick → 1
#pragma warning disable CS8625
            sim.SubmitCommand(BattleCommand.RecallPilot(1, 0, null, BattleSide.SideA));
#pragma warning restore CS8625
            sim.AdvanceTick(); // tick → 2, pilotCooldown = 100 → 99

            int cd1 = GetSlotState(sim.GetState(), BattleSide.SideA, 0).PilotCooldownTick;
            if (cd1 != 99)
                throw new InvalidOperationException("Pilot cooldown should be 99. Got: " + cd1);

            sim.AdvanceTick(); // tick → 3, pilotCooldown → 98
            int cd2 = GetSlotState(sim.GetState(), BattleSide.SideA, 0).PilotCooldownTick;
            if (cd2 != 98)
                throw new InvalidOperationException("Pilot cooldown should be 98. Got: " + cd2);
        }

        private static void SpawnDroneSquad_ConsumesEnergyAndStartsCooldown()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick();

            BattleState state = sim.GetState();
            if (GetSideState(state, BattleSide.SideA).Energy != Fp.Zero)
                throw new InvalidOperationException("Energy should be 0 after spawn. Got: " +
                    GetSideState(state, BattleSide.SideA).Energy);
            int cd = GetSlotState(state, BattleSide.SideA, 0).DroneCooldownTick;
            if (cd != 99)
                throw new InvalidOperationException("Drone cooldown should be 99. Got: " + cd);
        }

        private static void SpawnDroneSquad_RejectsInvalidLane()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "no_such_lane", BattleSide.SideA));
            bool threw = false;
            try { sim.AdvanceTick(); }
            catch (ArgumentException) { threw = true; }
            if (!threw) throw new InvalidOperationException("SpawnDroneSquad should reject invalid lane.");
        }

        private static void SpawnDroneSquad_RejectsInsufficientEnergy()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
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

            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick(); // tick → 1, pilot deployed

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(1, 0, "lane_ground", BattleSide.SideA));
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
            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick();
            Fp energy = GetSideState(sim.GetState(), BattleSide.SideA).Energy;
            if (energy != Fp.FromInt(50))
                throw new InvalidOperationException("DeployPilot should not consume energy. Got: " + energy);
        }

        private static void DeployPilot_SetsPilotDeployed()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
            if (GetSlotState(sim.GetState(), BattleSide.SideA, 0).IsPilotDeployed)
                throw new InvalidOperationException("Pilot should not be deployed initially.");

            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick();

            if (!GetSlotState(sim.GetState(), BattleSide.SideA, 0).IsPilotDeployed)
                throw new InvalidOperationException("Pilot should be deployed after DeployPilot command.");
        }

        private static void RecallPilot_ClearsPilotAndStartsCooldown()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick(); // tick → 1
#pragma warning disable CS8625
            sim.SubmitCommand(BattleCommand.RecallPilot(1, 0, null, BattleSide.SideA));
#pragma warning restore CS8625
            sim.AdvanceTick(); // tick → 2

            SlotState slot = GetSlotState(sim.GetState(), BattleSide.SideA, 0);
            if (slot.IsPilotDeployed)
                throw new InvalidOperationException("Pilot should not be deployed after recall.");
            if (slot.PilotCooldownTick != 99)
                throw new InvalidOperationException("Pilot cooldown should be 99. Got: " + slot.PilotCooldownTick);
        }

        private static void RecallPilot_RejectsWhenNotDeployed()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());
#pragma warning disable CS8625
            sim.SubmitCommand(BattleCommand.RecallPilot(0, 0, null, BattleSide.SideA));
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

        private static void TimeoutTie_TieWinnerSideWins()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.Zero, Fp.FromInt(100), Fp.Zero, maxBattleTick: 1,
                    timeOutTieWinnerSide: BattleSide.SideB),
                MinimalInitialState());
            sim.AdvanceTick();
            BattleResult result = sim.GetResult();
            if (result.WinnerSide != BattleSide.SideB)
                throw new InvalidOperationException("Timeout tie should resolve to SideB (tieWinnerSide). Got: " + result.WinnerSide);

            // Also verify SideA tieWinnerSide works.
            BattleSimulator sim2 = new BattleSimulator(
                MakeConfig(Fp.Zero, Fp.FromInt(100), Fp.Zero, maxBattleTick: 1,
                    timeOutTieWinnerSide: BattleSide.SideA),
                MinimalInitialState());
            sim2.AdvanceTick();
            BattleResult result2 = sim2.GetResult();
            if (result2.WinnerSide != BattleSide.SideA)
                throw new InvalidOperationException("Timeout tie with tieWinnerSide=SideA should resolve to SideA.");
        }

        // ------------------------------------------------------------------ v0.2 tests

        // v2-1. SpawnDroneSquad creates SideA entity in selected lane.
        private static void SpawnDroneSquad_CreatesEntityInLane()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick();

            BattleEntity? entity = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideA);
            if (entity == null)
                throw new InvalidOperationException("SpawnDroneSquad should create a SideA entity in lane_ground.");
            if (entity.Side != BattleSide.SideA)
                throw new InvalidOperationException("Entity owner should be SideA.");
        }

        // v2-2. DeployPilot creates SideA pilot entity in selected lane.
        private static void DeployPilot_CreatesEntityInLane()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());

            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick();

            BattleEntity? entity = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideA);
            if (entity == null)
                throw new InvalidOperationException("DeployPilot should create a SideA entity in lane_ground.");
        }

        // v2-3. RecallPilot removes deployed pilot entity.
        private static void RecallPilot_RemovesEntityFromLane()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(), MinimalInitialState());

            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick(); // pilot entity created
#pragma warning disable CS8625
            sim.SubmitCommand(BattleCommand.RecallPilot(1, 0, null, BattleSide.SideA));
#pragma warning restore CS8625
            sim.AdvanceTick(); // pilot entity removed

            LaneState lane = GetLane(sim.GetState(), "lane_ground");
            if (lane.Entities.Count != 0)
                throw new InvalidOperationException(
                    "Lane should be empty after pilot recall. Found " + lane.Entities.Count + " entities.");
        }

        // v2-4. SideB SpawnDroneSquad creates entity at far end of lane (LaneLengthMilli).
        private static void SideBCommand_CreatesEntityAtFarEnd()
        {
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.Zero, Fp.FromInt(100), Fp.Zero,
                sideBInitialEnergy: Fp.FromInt(10));
            BattleSimulator sim = new BattleSimulator(cfg, MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            sim.AdvanceTick();

            BattleEntity? entity = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideB);
            if (entity == null)
                throw new InvalidOperationException("SideB SpawnDroneSquad should create a SideB entity.");
            if (entity.Side != BattleSide.SideB)
                throw new InvalidOperationException("Entity owner should be SideB.");
        }

        // v2-5. SideA entity moves toward SideB base (position increases).
        private static void SideAEntity_MovesTowardSideBBase()
        {
            BattleSimulator sim = new BattleSimulator(
                MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero),
                MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick(); // drone starts at 0, moves to 500

            BattleEntity? drone = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideA);
            if (drone == null) throw new InvalidOperationException("SideA entity not found.");
            if (drone.PositionMilli <= 0)
                throw new InvalidOperationException(
                    "SideA entity should have moved toward SideB base. Pos: " + drone.PositionMilli);
        }

        // v2-6. SideB entity moves toward SideA base (position decreases).
        private static void SideBEntity_MovesTowardSideABase()
        {
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.Zero, Fp.FromInt(100), Fp.Zero,
                sideBInitialEnergy: Fp.FromInt(10));
            BattleSimulator sim = new BattleSimulator(cfg, MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            sim.AdvanceTick(); // SideB entity at laneLen, moves toward 0

            BattleEntity? entity = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideB);
            if (entity == null) throw new InvalidOperationException("SideB entity not found.");
            if (entity.PositionMilli >= DefaultLaneLen)
                throw new InvalidOperationException(
                    "SideB entity should have moved toward SideA base. Pos: " + entity.PositionMilli);
        }

        // v2-7. SideA and SideB can submit commands at the same tick.
        private static void SideAAndSideB_CanSubmitCommandsAtSameTick()
        {
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                sideBInitialEnergy: Fp.FromInt(10));
            BattleSimulator sim = new BattleSimulator(cfg, MinimalInitialState());

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            sim.AdvanceTick();

            BattleState state = sim.GetState();
            BattleEntity? eA = FindByOwner(state, "lane_ground", BattleSide.SideA);
            BattleEntity? eB = FindByOwner(state, "lane_ground", BattleSide.SideB);
            if (eA == null) throw new InvalidOperationException("SideA entity not found after same-tick commands.");
            if (eB == null) throw new InvalidOperationException("SideB entity not found after same-tick commands.");
        }

        // v2-8. Entity never changes lane.
        private static void Entity_NeverChangesLane()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_a", LaneType.Ground, DefaultLaneLen, 0L),
                new LaneDefinition("lane_b", LaneType.Ground, DefaultLaneLen, 2000L),
            };
            BattleSideConfig cfgA = new BattleSideConfig(
                BattleSide.SideA, Fp.FromInt(1000), Fp.FromInt(10), Fp.FromInt(100), Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(
                BattleSide.SideB, Fp.FromInt(1000), Fp.Zero, Fp.FromInt(100), Fp.Zero);
            BattleConfigSnapshot cfg = new BattleConfigSnapshot(
                "test", cfgA, cfgB, 200, 100, 50, 3600, lanes);

            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "pilot_a", "drone_a", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(10), 1_000L, 500L,
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "pilot_b", "drone_b", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(10), 1_000L, 500L,
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a", BattleSide.SideA));
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
            if (!foundInA) throw new InvalidOperationException("SideA entity not found in lane_a.");
        }

        // v2-9. Entity attacks nearest opponent in same lane (doesn't move while attacking).
        private static void Entity_AttacksNearestOpponentInSameLane()
        {
            // Short lane; SideA drone range covers the full lane → always in attack range of SideB entity.
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                sideBInitialEnergy: Fp.FromInt(10));

            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "pilot_a", "drone_a", Fp.FromInt(10), 100,
                    Fp.FromInt(200), Fp.FromInt(10), 2_000L, 500L, // drone range=2000 (> laneLen=1000)
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "pilot_b", "drone_b", Fp.FromInt(10), 100,
                    Fp.FromInt(50), Fp.FromInt(5), 500L, 100L,     // SideB entity HP=50, range=500
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            sim.AdvanceTick(); // SideA drone attacks SideB entity; SideB entity out of range → moves

            BattleState state = sim.GetState();
            BattleEntity? drone = FindByOwner(state, "lane_ground", BattleSide.SideA);
            BattleEntity? sideBEntity = FindByOwner(state, "lane_ground", BattleSide.SideB);

            if (drone == null) throw new InvalidOperationException("SideA drone not found after tick.");
            if (drone.PositionMilli != 0)
                throw new InvalidOperationException(
                    "Drone should not have moved while attacking. Pos: " + drone.PositionMilli);
            if (sideBEntity == null)
                throw new InvalidOperationException("SideB entity should still be alive (HP > 0).");
            if (sideBEntity.Hp >= Fp.FromInt(50))
                throw new InvalidOperationException(
                    "SideB entity HP should have decreased from drone attack. HP: " + sideBEntity.Hp);
        }

        // v2-10. Entity only attacks opposite owner in same lane.
        private static void Entity_OnlyAttacksOppositeOwner()
        {
            // Two SideA entities + one SideB entity in the same lane.
            // SideA entities should only attack the SideB entity, not each other.
            long laneLen = 2_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(20), Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "p0", "d0", Fp.FromInt(10), 5,
                    Fp.FromInt(100), Fp.FromInt(10), 3_000L, 200L, // range covers whole lane
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
                CreateTestSlotDef(
                    1, "p1", "d1", Fp.FromInt(10), 5,
                    Fp.FromInt(100), Fp.FromInt(10), 3_000L, 200L,
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "pb", "db", Fp.FromInt(10), 5,
                    Fp.FromInt(50), Fp.FromInt(5), 500L, 100L,
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 1, "lane_ground", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            sim.AdvanceTick();

            BattleState state = sim.GetState();
            LaneState lane = GetLane(state, "lane_ground");
            int sideACount = 0;
            foreach (BattleEntity e in lane.Entities)
                if (e.Side == BattleSide.SideA) sideACount++;

            // Both SideA entities attacked SideB entity; SideA entities should be unharmed by each other.
            if (sideACount < 2)
                throw new InvalidOperationException(
                    "Both SideA entities should survive — they must not attack each other. Count: " + sideACount);
        }

        // v2-11. Dead entity is removed.
        private static void DeadEntity_IsRemoved()
        {
            // SideA drone attack 10 × 1 hit kills SideB entity (HP=5).
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "p", "d", Fp.FromInt(10), 100,
                    Fp.FromInt(200), Fp.FromInt(10), 2_000L, 500L, // drone atk=10, kills SideB(HP=5) in 1 hit
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "pb", "db", Fp.FromInt(10), 100,
                    Fp.FromInt(5), Fp.FromInt(2), 500L, 100L,      // HP=5 → one-shot
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            sim.AdvanceTick(); // SideA drone attacks SideB entity → dead → removed

            LaneState lane = GetLane(sim.GetState(), "lane_ground");
            int sideBCount = 0;
            foreach (BattleEntity e in lane.Entities)
                if (e.Side == BattleSide.SideB) sideBCount++;
            if (sideBCount != 0)
                throw new InvalidOperationException("Dead SideB entity should have been removed. Found: " + sideBCount);
        }

        // v2-12. SideA entity reaching SideB base wall damages SideB base.
        private static void SideAEntity_DamagesSideBBase()
        {
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen);
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "p", "d", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(10), 500L, 2_000L, // speed=2000 > laneLen=1000
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA,
                DefaultSideInitialState(BattleSide.SideB));
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick(); // drone reaches SideB base wall → attacks SideB base

            Fp sideBBaseHp = GetSideState(sim.GetState(), BattleSide.SideB).BaseHp;
            if (sideBBaseHp >= Fp.FromInt(1000))
                throw new InvalidOperationException(
                    "SideB base HP should have decreased. Got: " + sideBBaseHp);
        }

        // v2-13. SideB entity reaching SideA base wall damages SideA base.
        private static void SideBEntity_DamagesSideABase()
        {
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.Zero, Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "pb", "db", Fp.FromInt(10), 100,
                    Fp.FromInt(50), Fp.FromInt(10), 500L, 2_000L,  // speed=2000 > laneLen=1000
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L,
                DefaultSideInitialState(BattleSide.SideA), sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            sim.AdvanceTick(); // SideB entity reaches SideA base wall → attacks SideA base

            Fp sideABaseHp = GetSideState(sim.GetState(), BattleSide.SideA).BaseHp;
            if (sideABaseHp >= Fp.FromInt(1000))
                throw new InvalidOperationException(
                    "SideA base HP should have decreased. Got: " + sideABaseHp);
        }

        // v2-14. SideB base destroyed → WinnerSide=SideA / SideBBaseDestroyed.
        private static void SideBBaseDestroyed_SideAWins()
        {
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                sideBBaseHp: Fp.FromInt(1));
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "p", "d", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(50), 500L, 2_000L, // attack=50, speed=2000
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA,
                DefaultSideInitialState(BattleSide.SideB));
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.AdvanceTick(); // drone reaches SideB base (HP=1), attacks for 50 → destroyed

            if (!sim.IsTerminated)
                throw new InvalidOperationException("Battle should have terminated.");
            BattleResult result = sim.GetResult();
            if (result.WinnerSide != BattleSide.SideA)
                throw new InvalidOperationException("WinnerSide should be SideA. Got: " + result.WinnerSide);
            if (result.EndReason != BattleEndReason.SideBBaseDestroyed)
                throw new InvalidOperationException("EndReason should be SideBBaseDestroyed. Got: " + result.EndReason);
        }

        // v2-15. SideA base destroyed → WinnerSide=SideB / SideABaseDestroyed.
        private static void SideABaseDestroyed_SideBWins()
        {
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.Zero, Fp.FromInt(100), Fp.Zero,
                laneLengthMilli: laneLen,
                sideABaseHp: Fp.FromInt(1),
                sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "pb", "db", Fp.FromInt(10), 100,
                    Fp.FromInt(50), Fp.FromInt(50), 500L, 2_000L,  // attack=50, speed=2000
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L,
                DefaultSideInitialState(BattleSide.SideA), sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            sim.AdvanceTick(); // SideB entity reaches SideA base (HP=1), attacks for 50 → destroyed

            if (!sim.IsTerminated)
                throw new InvalidOperationException("Battle should have terminated.");
            BattleResult result = sim.GetResult();
            if (result.WinnerSide != BattleSide.SideB)
                throw new InvalidOperationException("WinnerSide should be SideB. Got: " + result.WinnerSide);
            if (result.EndReason != BattleEndReason.SideABaseDestroyed)
                throw new InvalidOperationException("EndReason should be SideABaseDestroyed. Got: " + result.EndReason);
        }

        // v2-16. Timeout: SideA wins by HP ratio (SideB base was damaged more).
        private static void Timeout_SideAWinsByHpRatio()
        {
            long laneLen = 1_000L;
            // SideA drone: speed=2000, attack=10. Reaches SideB base and deals 10 damage.
            // maxBattleTick=2 → SideA base HP=1.0, SideB base HP=990/1000=0.99 → SideA wins.
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                maxBattleTick: 2,
                laneLengthMilli: laneLen,
                sideBBaseHp: Fp.FromInt(1000));
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "p", "d", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(10), 500L, 2_000L,
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA,
                DefaultSideInitialState(BattleSide.SideB));
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            while (!sim.IsTerminated) sim.AdvanceTick();

            BattleResult result = sim.GetResult();
            if (result.EndReason != BattleEndReason.TimeOut)
                throw new InvalidOperationException("EndReason should be TimeOut.");
            if (result.WinnerSide != BattleSide.SideA)
                throw new InvalidOperationException(
                    "SideA should win by HP ratio. Got WinnerSide: " + result.WinnerSide);
        }

        // v2-17. Timeout: SideB wins by HP ratio (SideA base was damaged more).
        private static void Timeout_SideBWinsByHpRatio()
        {
            long laneLen = 1_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.Zero, Fp.FromInt(100), Fp.Zero,
                maxBattleTick: 2,
                laneLengthMilli: laneLen,
                sideABaseHp: Fp.FromInt(1000),
                sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "pb", "db", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(10), 500L, 2_000L,
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L,
                DefaultSideInitialState(BattleSide.SideA), sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            while (!sim.IsTerminated) sim.AdvanceTick();

            BattleResult result = sim.GetResult();
            if (result.EndReason != BattleEndReason.TimeOut)
                throw new InvalidOperationException("EndReason should be TimeOut.");
            if (result.WinnerSide != BattleSide.SideB)
                throw new InvalidOperationException(
                    "SideB should win by HP ratio. Got WinnerSide: " + result.WinnerSide);
        }

        // v2-18. Timeout behavior (no entities) remains passing.
        private static void MaxBattleTick_TimeoutStillWorks()
        {
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

        private static void Smoke_SideAVictory_ProducesExpectedResult()
        {
            var (cfg, initial, commands) = SmokeScenarios.SideAVictory();
            BattleResult result = SmokeScenarios.RunToCompletion(cfg, initial, commands);

            if (result.WinnerSide != BattleSide.SideA)
                throw new InvalidOperationException(
                    "Smoke SideAVictory: WinnerSide should be SideA. Got: " + result.WinnerSide);
            if (result.EndReason != BattleEndReason.SideBBaseDestroyed)
                throw new InvalidOperationException(
                    "Smoke SideAVictory: EndReason should be SideBBaseDestroyed. Got: " + result.EndReason);
            if (result.ClearTimeTick != 1)
                throw new InvalidOperationException(
                    "Smoke SideAVictory: should end at tick 1. Got: " + result.ClearTimeTick);
            if (result.SideABaseHpRatio != Fp.One)
                throw new InvalidOperationException(
                    "Smoke SideAVictory: SideA base hp ratio should be 1.0. Got: " + result.SideABaseHpRatio);
            if (result.SideBBaseHpRatio != Fp.Zero)
                throw new InvalidOperationException(
                    "Smoke SideAVictory: SideB base hp ratio should be 0.0. Got: " + result.SideBBaseHpRatio);
        }

        private static void Smoke_SideBVictory_ProducesExpectedResult()
        {
            var (cfg, initial, commands) = SmokeScenarios.SideBVictory();
            BattleResult result = SmokeScenarios.RunToCompletion(cfg, initial, commands);

            if (result.WinnerSide != BattleSide.SideB)
                throw new InvalidOperationException(
                    "Smoke SideBVictory: WinnerSide should be SideB. Got: " + result.WinnerSide);
            if (result.EndReason != BattleEndReason.SideABaseDestroyed)
                throw new InvalidOperationException(
                    "Smoke SideBVictory: EndReason should be SideABaseDestroyed. Got: " + result.EndReason);
            if (result.ClearTimeTick != 1)
                throw new InvalidOperationException(
                    "Smoke SideBVictory: should end at tick 1. Got: " + result.ClearTimeTick);
            if (result.SideABaseHpRatio != Fp.Zero)
                throw new InvalidOperationException(
                    "Smoke SideBVictory: SideA base hp ratio should be 0.0. Got: " + result.SideABaseHpRatio);
            if (result.SideBBaseHpRatio != Fp.One)
                throw new InvalidOperationException(
                    "Smoke SideBVictory: SideB base hp ratio should be 1.0. Got: " + result.SideBBaseHpRatio);
        }

        private static void Smoke_TimeoutSideBTiebreak_ProducesExpectedResult()
        {
            var (cfg, initial, commands) = SmokeScenarios.TimeoutSideBTiebreak();
            BattleResult result = SmokeScenarios.RunToCompletion(cfg, initial, commands);

            if (result.WinnerSide != BattleSide.SideB)
                throw new InvalidOperationException(
                    "Smoke TimeoutTiebreak: WinnerSide should be SideB. Got: " + result.WinnerSide);
            if (result.EndReason != BattleEndReason.TimeOut)
                throw new InvalidOperationException(
                    "Smoke TimeoutTiebreak: EndReason should be TimeOut. Got: " + result.EndReason);
            if (result.ClearTimeTick != 3)
                throw new InvalidOperationException(
                    "Smoke TimeoutTiebreak: should end at tick 3. Got: " + result.ClearTimeTick);
            if (result.SideABaseHpRatio != Fp.One)
                throw new InvalidOperationException(
                    "Smoke TimeoutTiebreak: SideA base hp ratio should be 1.0. Got: " + result.SideABaseHpRatio);
            if (result.SideBBaseHpRatio != Fp.One)
                throw new InvalidOperationException(
                    "Smoke TimeoutTiebreak: SideB base hp ratio should be 1.0. Got: " + result.SideBBaseHpRatio);
        }

        private static void Smoke_SideAVictory_IsDeterministicAcrossRepeatedRuns()
        {
            var (cfgA, initialA, commandsA) = SmokeScenarios.SideAVictory();
            var (cfgB, initialB, commandsB) = SmokeScenarios.SideAVictory();

            BattleResult ra = SmokeScenarios.RunToCompletion(cfgA, initialA, commandsA);
            BattleResult rb = SmokeScenarios.RunToCompletion(cfgB, initialB, commandsB);

            if (ra.WinnerSide != rb.WinnerSide)
                throw new InvalidOperationException("Smoke Determinism: WinnerSide mismatch.");
            if (ra.EndReason != rb.EndReason)
                throw new InvalidOperationException("Smoke Determinism: EndReason mismatch.");
            if (ra.ClearTimeTick != rb.ClearTimeTick)
                throw new InvalidOperationException("Smoke Determinism: ClearTimeTick mismatch.");
            if (ra.SideABaseHpRatio != rb.SideABaseHpRatio)
                throw new InvalidOperationException("Smoke Determinism: SideABaseHpRatio mismatch.");
            if (ra.SideBBaseHpRatio != rb.SideBBaseHpRatio)
                throw new InvalidOperationException("Smoke Determinism: SideBBaseHpRatio mismatch.");
        }

        // v2-19. Same config + initial + command sequence produces same BattleResult (determinism).
        private static void Determinism_SameSetupProducesSameResult()
        {
            long laneLen = 2_000L;
            BattleConfigSnapshot cfg = MakeConfig(
                Fp.FromInt(10), Fp.FromInt(100), Fp.Zero,
                maxBattleTick: 10,
                laneLengthMilli: laneLen,
                sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "p", "d", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(10), 1_000L, 200L,
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(
                    0, "pb", "db", Fp.FromInt(10), 100,
                    Fp.FromInt(30), Fp.FromInt(5), 500L, 200L,
                    Fp.FromInt(200), Fp.FromInt(20), 1_500L, 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);

            BattleSimulator simA = new BattleSimulator(cfg, initial);
            BattleSimulator simB = new BattleSimulator(cfg, initial);

            simA.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            simB.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            simA.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            simB.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));

            for (int i = 0; i < 10 && !simA.IsTerminated; i++) simA.AdvanceTick();
            for (int i = 0; i < 10 && !simB.IsTerminated; i++) simB.AdvanceTick();

            if (simA.IsTerminated != simB.IsTerminated)
                throw new InvalidOperationException("Determinism: termination state mismatch.");
            if (!simA.IsTerminated) return;

            BattleResult ra = simA.GetResult();
            BattleResult rb = simB.GetResult();
            if (ra.WinnerSide != rb.WinnerSide)
                throw new InvalidOperationException(
                    "Determinism: WinnerSide mismatch. A=" + ra.WinnerSide + " B=" + rb.WinnerSide);
            if (ra.EndReason != rb.EndReason)
                throw new InvalidOperationException("Determinism: EndReason mismatch.");
            if (ra.ClearTimeTick != rb.ClearTimeTick)
                throw new InvalidOperationException("Determinism: ClearTimeTick mismatch.");
        }

        private static void Defense_ReducesDamageButNotBelowMinDamage()
        {
            // Case 1: Attack=100, Defense=40 -> damage=60
            {
                BattleConfigSnapshot cfg = MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero, laneLengthMilli: 1_000L, sideBInitialEnergy: Fp.FromInt(10));
                BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
                {
                    CreateTestSlotDef(0, "p", "d", Fp.FromInt(10), 100,
                        droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(100),
                        droneRangeMilli: 2_000L, droneSpeedMilliPerTick: 100L,
                        pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                        pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
                });
                BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
                {
                    CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                        droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(10), droneDefense: Fp.FromInt(40),
                        droneRangeMilli: 2_000L, droneSpeedMilliPerTick: 100L,
                        pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                        pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
                });
                BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
                BattleSimulator sim = new BattleSimulator(cfg, initial);

                sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
                sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
                sim.AdvanceTick(); // Attack!

                BattleEntity? target = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideB);
                if (target == null) throw new InvalidOperationException("Target should be alive.");
                Fp expectedHp = Fp.FromInt(200) - Fp.FromInt(60);
                if (target.Hp != expectedHp)
                    throw new InvalidOperationException(string.Format("Expected HP {0}, got {1}", expectedHp, target.Hp));
            }

            // Case 2: Attack=10, Defense=40 -> damage should be capped at MinDamageRaw (1.0)
            {
                BattleConfigSnapshot cfg = MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero, laneLengthMilli: 1_000L, sideBInitialEnergy: Fp.FromInt(10));
                BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
                {
                    CreateTestSlotDef(0, "p", "d", Fp.FromInt(10), 100,
                        droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(10),
                        droneRangeMilli: 2_000L, droneSpeedMilliPerTick: 100L,
                        pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                        pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
                });
                BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
                {
                    CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                        droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(10), droneDefense: Fp.FromInt(40),
                        droneRangeMilli: 2_000L, droneSpeedMilliPerTick: 100L,
                        pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                        pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
                });
                BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
                BattleSimulator sim = new BattleSimulator(cfg, initial);

                sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
                sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
                sim.AdvanceTick(); // Attack!

                BattleEntity? target = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideB);
                if (target == null) throw new InvalidOperationException("Target should be alive.");
                Fp expectedHp = Fp.FromInt(200) - Fp.One; // damage capped at 1.0
                if (target.Hp != expectedHp)
                    throw new InvalidOperationException(string.Format("Expected HP {0}, got {1}", expectedHp, target.Hp));
            }
        }

        private static void AttackPeriod_PreventsEveryTickAttack()
        {
            // AttackPeriodTick = 3. Spawns at tick 0.
            // Tick 1: Attacks, NextAttackReadyTick becomes 1 + 3 = 4.
            // Tick 2: Attack cooldown. Target HP remains same.
            // Tick 3: Attack cooldown. Target HP remains same.
            // Tick 4: Attacks again.
            BattleConfigSnapshot cfg = MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero, laneLengthMilli: 1_000L, sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(10),
                    droneRangeMilli: 2_000L, droneSpeedMilliPerTick: 0L, droneAttackPeriodTick: 3,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 2_000L, droneSpeedMilliPerTick: 0L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));
            
            sim.AdvanceTick(); // Tick 1: attack occurs (HP: 200 -> 190)
            Fp hp1 = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideB)!.Hp;
            if (hp1 != Fp.FromInt(190)) throw new InvalidOperationException("Tick 1 attack failed.");

            sim.AdvanceTick(); // Tick 2: cooldown (HP: 190)
            Fp hp2 = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideB)!.Hp;
            if (hp2 != Fp.FromInt(190)) throw new InvalidOperationException("Tick 2 should be cooldown.");

            sim.AdvanceTick(); // Tick 3: cooldown (HP: 190)
            Fp hp3 = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideB)!.Hp;
            if (hp3 != Fp.FromInt(190)) throw new InvalidOperationException("Tick 3 should be cooldown.");

            sim.AdvanceTick(); // Tick 4: attack occurs (HP: 190 -> 180)
            Fp hp4 = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideB)!.Hp;
            if (hp4 != Fp.FromInt(180)) throw new InvalidOperationException("Tick 4 attack failed.");
        }

        private static void AttackCooldown_HoldsPositionWhenTargetInRange()
        {
            // Attacker has RangeMilli = 1_000. Spawns at 0.
            // Target is at 500 (inside range).
            // Tick 1: Attacks (NextAttackReadyTick = 4), Position = 0.
            // Tick 2: Attack cooldown but target is in range -> holds position. Position should remain 0.
            BattleConfigSnapshot cfg = MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero, laneLengthMilli: 1_000L, sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(10),
                    droneRangeMilli: 1_000L, droneSpeedMilliPerTick: 200L, droneAttackPeriodTick: 3,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 0L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));

            sim.AdvanceTick(); // Tick 1: attacks, holds position. (Pos = 0)
            long pos1 = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideA)!.PositionMilli;
            if (pos1 != 0) throw new InvalidOperationException("Attacker should not move on tick 1.");

            sim.AdvanceTick(); // Tick 2: cooldown, target still at 1000 (dist 1000 <= 1000) -> holds position! (Pos = 0)
            long pos2 = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideA)!.PositionMilli;
            if (pos2 != 0) throw new InvalidOperationException("Attacker should hold position on tick 2 during cooldown.");
        }

        private static void CrossLaneTargeting_UsesLaneWorldYManhattanDistance()
        {
            // Attacker is in lane_a (Y = 0L) at position 0. RangeMilli = 1_000.
            // Target 1 is in lane_b (Y = 800L) at position 100.
            //   Manhattan distance = |100 - 0| + |800 - 0| = 900 <= 1000.
            // Target 2 is in lane_c (Y = 500L) at position 600.
            //   Manhattan distance = |600 - 0| + |500 - 0| = 1100 > 1000.
            // Only Target 1 is in range. Attacker should attack Target 1.
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_a", LaneType.Ground, 1_000L, 0L),
                new LaneDefinition("lane_b", LaneType.Ground, 1_000L, 800L),
                new LaneDefinition("lane_c", LaneType.Ground, 1_000L, 500L),
            };
            BattleSideConfig cfgA = new BattleSideConfig(BattleSide.SideA, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(BattleSide.SideB, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleConfigSnapshot cfg = new BattleConfigSnapshot("test", cfgA, cfgB, 200, 100, 50, 3600, lanes);

            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDefProjectile(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 1_000L, droneSpeedMilliPerTick: 0L,
                    droneAttackKind: AttackKind.Projectile, droneProjectileSpeedMilliPerTick: 100_000L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 1000L, // speed=1000 to reach 0 (stops at base)
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
                CreateTestSlotDef(1, "pb2", "db2", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 400L, // speed=400 to reach pos 600 (1000-400)
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_b", BattleSide.SideB)); // Target 1
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 1, "lane_c", BattleSide.SideB)); // Target 2

            sim.AdvanceTick(); // Tick 1: Target 1 reaches 0, Target 2 moves to 600.
            sim.AdvanceTick(); // Tick 2: Attacker is at 0, Target 1 is at 0 -> Spawns Projectile.
            sim.AdvanceTick(); // Tick 3: Projectile moves and hits Target 1.

            Fp hpTarget1 = FindByOwner(sim.GetState(), "lane_b", BattleSide.SideB)!.Hp;
            Fp hpTarget2 = FindByOwner(sim.GetState(), "lane_c", BattleSide.SideB)!.Hp;

            if (hpTarget1 != Fp.FromInt(150)) throw new InvalidOperationException("Target 1 should have been attacked.");
            if (hpTarget2 != Fp.FromInt(200)) throw new InvalidOperationException("Target 2 should NOT have been attacked.");
        }

        private static void CrossLaneTargeting_DoesNotHitWhenVerticalDistanceOutOfRange()
        {
            // Attacker is in lane_a (Y = 0L) at position 0. RangeMilli = 500.
            // Target is in lane_b (Y = 600L) at position 0.
            // Manhattan distance = |0 - 0| + |600 - 0| = 600 > 500.
            // Target is out of range. Attacker should NOT attack, and should move instead.
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_a", LaneType.Ground, 1_000L, 0L),
                new LaneDefinition("lane_b", LaneType.Ground, 1_000L, 600L),
            };
            BattleSideConfig cfgA = new BattleSideConfig(BattleSide.SideA, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(BattleSide.SideB, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleConfigSnapshot cfg = new BattleConfigSnapshot("test", cfgA, cfgB, 200, 100, 50, 3600, lanes);

            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDefProjectile(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 200L,
                    droneAttackKind: AttackKind.Projectile, droneProjectileSpeedMilliPerTick: 100_000L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 1000L, // speed=1000 reaches 0
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_b", BattleSide.SideB));

            sim.AdvanceTick(); // Tick 1: Target reaches pos 0. SideA drone out of range -> moves to pos 200.

            Fp hpTarget = FindByOwner(sim.GetState(), "lane_b", BattleSide.SideB)!.Hp;
            long posAttacker = FindByOwner(sim.GetState(), "lane_a", BattleSide.SideA)!.PositionMilli;

            if (hpTarget != Fp.FromInt(200)) throw new InvalidOperationException("Target should NOT have been attacked.");
            if (posAttacker != 200) throw new InvalidOperationException("Attacker should have moved since target was out of range.");
        }

        private static void TargetTieBreak_UsesNumericId()
        {
            // Attacker is in lane_a (Y = 0) at position 0. RangeMilli = 1_000.
            // Target 1 (spawned first -> NumericId = 2) is in lane_b (Y = 500) at position 300 (Dist = 800).
            // Target 2 (spawned second -> NumericId = 3) is in lane_c (Y = 500) at position 300 (Dist = 800).
            // Distances are identical. NumericId tiebreaker should select Target 1 (NumericId 2).
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_a", LaneType.Ground, 1_000L, 0L),
                new LaneDefinition("lane_b", LaneType.Ground, 1_000L, 500L),
                new LaneDefinition("lane_c", LaneType.Ground, 1_000L, 500L),
            };
            BattleSideConfig cfgA = new BattleSideConfig(BattleSide.SideA, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(BattleSide.SideB, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleConfigSnapshot cfg = new BattleConfigSnapshot("test", cfgA, cfgB, 200, 100, 50, 3600, lanes);

            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDefProjectile(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 1_000L, droneSpeedMilliPerTick: 0L,
                    droneAttackKind: AttackKind.Projectile, droneProjectileSpeedMilliPerTick: 100_000L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 1000L, // reaches 0 (stops at base)
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
                CreateTestSlotDef(1, "pb2", "db2", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 1000L, // reaches 0 (stops at base)
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a", BattleSide.SideA)); // NumericId = 1
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_b", BattleSide.SideB)); // NumericId = 2 (Target 1)
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 1, "lane_c", BattleSide.SideB)); // NumericId = 3 (Target 2)

            sim.AdvanceTick(); // Tick 1: both targets move to 0.
            sim.AdvanceTick(); // Tick 2: both targets at 0 -> Attacks Target 1 (lower NumericId) -> Spawns Projectile.
            sim.AdvanceTick(); // Tick 3: Projectile hits Target 1.

            Fp hpTarget1 = FindByOwner(sim.GetState(), "lane_b", BattleSide.SideB)!.Hp;
            Fp hpTarget2 = FindByOwner(sim.GetState(), "lane_c", BattleSide.SideB)!.Hp;

            if (hpTarget1 != Fp.FromInt(150)) throw new InvalidOperationException("Target 1 (lower NumericId) should have been attacked.");
            if (hpTarget2 != Fp.FromInt(200)) throw new InvalidOperationException("Target 2 (higher NumericId) should NOT have been attacked.");
        }

        // ========================================== v0.5 Projectile Tests ==========================================

        private static void Melee_SameLane_DamagesImmediately()
        {
            BattleConfigSnapshot cfg = MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero, laneLengthMilli: 1_000L, sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 0L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 1000L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));

            sim.AdvanceTick(); // Tick 1: Spawn. Target moves to pos 0.
            sim.AdvanceTick(); // Tick 2: Attack. Melee deals damage immediately.

            Fp hpTarget = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideB)!.Hp;
            if (hpTarget != Fp.FromInt(150)) throw new InvalidOperationException("Melee should deal damage immediately on attack tick.");
        }

        private static void Projectile_DoesNotDamageOnFireTick()
        {
            BattleConfigSnapshot cfg = MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero, laneLengthMilli: 1_000L, sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDefProjectile(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 0L,
                    droneAttackKind: AttackKind.Projectile, droneProjectileSpeedMilliPerTick: 500L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 1000L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));

            sim.AdvanceTick(); // Tick 1: Spawn. Target moves to pos 0.
            sim.AdvanceTick(); // Tick 2: Attack. Projectile spawns but does not move or hit on this tick.

            Fp hpTarget = FindByOwner(sim.GetState(), "lane_ground", BattleSide.SideB)!.Hp;
            if (hpTarget != Fp.FromInt(200)) throw new InvalidOperationException("Projectile should NOT deal damage on fire tick.");
            
            int activeProjectiles = sim.GetState().Projectiles.Count;
            if (activeProjectiles != 1) throw new InvalidOperationException("Should have 1 active projectile.");
        }

        private static void Projectile_HitsAfterTravelTime()
        {
            // Target is at 2000 (fixed). Attacker spawns at 0, moves to 1000 in Tick 1.
            // In Tick 2, distance is |2000 - 1000| + |100 - 0| = 1100 <= Range(1200) -> Projectile fires.
            // Projected start = 1000, Target = 2000. Distance = 1000.
            // Speed = 500 -> needs 2 ticks of travel.
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_a", LaneType.Ground, 2_000L, 0L),
                new LaneDefinition("lane_b", LaneType.Ground, 2_000L, 100L) // Y diff 100
            };
            BattleSideConfig cfgA = new BattleSideConfig(BattleSide.SideA, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(BattleSide.SideB, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleConfigSnapshot cfg = new BattleConfigSnapshot("test", cfgA, cfgB, 200, 100, 50, 3600, lanes);

            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDefProjectile(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 1_200L, droneSpeedMilliPerTick: 1000L, // reaches 1000 in 1 tick
                    droneAttackKind: AttackKind.Projectile, droneProjectileSpeedMilliPerTick: 500L, // 500 milli per tick
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 0L, // stays at 2000
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_b", BattleSide.SideB));

            sim.AdvanceTick(); // Tick 1: spawn. Attacker moves to 1000. Target stays at 2000.
            sim.AdvanceTick(); // Tick 2: Attack. Projectile spawns at pos 1000. Target is at 2000. (RemainingTravel = 1000)
            
            Fp hpTarget2 = FindByOwner(sim.GetState(), "lane_b", BattleSide.SideB)!.Hp;
            if (hpTarget2 != Fp.FromInt(200)) throw new InvalidOperationException("Must be 200 at Tick 2.");

            sim.AdvanceTick(); // Tick 3: Projectile moves from 1000 to 1500. Still traveling.
            Fp hpTarget3 = FindByOwner(sim.GetState(), "lane_b", BattleSide.SideB)!.Hp;
            if (hpTarget3 != Fp.FromInt(200)) throw new InvalidOperationException("Must be 200 at Tick 3.");

            sim.AdvanceTick(); // Tick 4: Projectile moves from 1500 to 2000. Arrives! Target hit!
            Fp hpTarget4 = FindByOwner(sim.GetState(), "lane_b", BattleSide.SideB)!.Hp;
            if (hpTarget4 != Fp.FromInt(150)) throw new InvalidOperationException("Must be 150 at Tick 4. Got: " + hpTarget4);
        }

        private static void Projectile_MissesIfTargetRemovedBeforeArrival()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_a", LaneType.Ground, 1_000L, 0L),
                new LaneDefinition("lane_b", LaneType.Ground, 1_000L, 100L)
            };
            BattleSideConfig cfgA = new BattleSideConfig(BattleSide.SideA, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(BattleSide.SideB, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleConfigSnapshot cfg = new BattleConfigSnapshot("test", cfgA, cfgB, 200, 100, 50, 3600, lanes);

            // Pilot will be target, so we can recall it!
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDefProjectile(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 1_200L, droneSpeedMilliPerTick: 1000L,
                    droneAttackKind: AttackKind.Projectile, droneProjectileSpeedMilliPerTick: 500L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 1000L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(0),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 1000L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.DeployPilot(0, 0, "lane_b", BattleSide.SideB)); // Target

            sim.AdvanceTick(); // Tick 1: Spawn drone (A) and pilot (B).
            sim.AdvanceTick(); // Tick 2: Attack starts. Projectile spawned targeting pilot.
            
            // Recall pilot at tick 2.
            sim.SubmitCommand(BattleCommand.RecallPilot(2, 0, "lane_b", BattleSide.SideB));
            sim.AdvanceTick(); // Tick 3: Pilot is recalled and removed from entities. Projectile moves.
            
            sim.AdvanceTick(); // Tick 4: Projectile arrives at 0, target is missing -> removed without error.
            
            int activeProjectiles = sim.GetState().Projectiles.Count;
            if (activeProjectiles != 0) throw new InvalidOperationException("Projectile should have been removed.");
        }

        private static void Projectile_MissesIfTargetMovedOutsideHitRadius()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_a", LaneType.Ground, 1_000L, 0L),
                new LaneDefinition("lane_b", LaneType.Ground, 1_000L, 100L)
            };
            BattleSideConfig cfgA = new BattleSideConfig(BattleSide.SideA, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(BattleSide.SideB, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleConfigSnapshot cfg = new BattleConfigSnapshot("test", cfgA, cfgB, 200, 100, 50, 3600, lanes);

            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDefProjectile(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 1_200L, droneSpeedMilliPerTick: 1000L,
                    droneAttackKind: AttackKind.Projectile, droneProjectileSpeedMilliPerTick: 500L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 300L, // moves 300 per tick
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_b", BattleSide.SideB)); // Target

            sim.AdvanceTick(); // Tick 1: Spawn. Drone (A) at 1000. Target (B) at 700 (1000 - 300).
            sim.AdvanceTick(); // Tick 2: Attack starts. Target is at 400 (700 - 300). Projectile ImpactProgressMilli snapshotted at 400.
                               // Projectile starts at 1000, travels 500 milli per tick.
            
            sim.AdvanceTick(); // Tick 3: Target moves to 100 (400 - 300). Projectile is at 500 (1000 - 500).
            sim.AdvanceTick(); // Tick 4: Target moves to 0 (reaches base). Projectile reaches 400 (ImpactProgressMilli).
                               // Projectile arrives! But target live position is 0.
                               // Abs(0 - 400) = 400 > ProjectileHitRadiusMilli (100) -> Miss!

            BattleEntity? target = FindByOwner(sim.GetState(), "lane_b", BattleSide.SideB);
            if (target == null) throw new InvalidOperationException("Target should still be alive.");
            if (target.Hp != Fp.FromInt(200)) throw new InvalidOperationException("Should be a miss! Target HP got damaged: " + target.Hp);
        }

        private static void Projectile_CrossLane_SpawnsOnTargetLane()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_a", LaneType.Ground, 1_000L, 0L),
                new LaneDefinition("lane_b", LaneType.Ground, 2_000L, 100L) // target lane is twice as long!
            };
            BattleSideConfig cfgA = new BattleSideConfig(BattleSide.SideA, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(BattleSide.SideB, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleConfigSnapshot cfg = new BattleConfigSnapshot("test", cfgA, cfgB, 200, 100, 50, 3600, lanes);

            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDefProjectile(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 2_000L, droneSpeedMilliPerTick: 500L, // reaches 500 (out of 1000)
                    droneAttackKind: AttackKind.Projectile, droneProjectileSpeedMilliPerTick: 500L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 1000L, // stays at 1000 (2000-1000)
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_b", BattleSide.SideB)); // Target

            sim.AdvanceTick(); // Tick 1: Spawn. Drone A reaches 500 (out of 1000). Drone B reaches 1000 (out of 2000).
            sim.AdvanceTick(); // Tick 2: Attack starts. Manhattan distance check:
                               // Attacker A at pos 500 (Y=0). Target B at pos 1000 (Y=100).
                               // Attacker X,Y = (500, 0). Target X,Y = (1000, 100).
                               // Distance = |500-1000| + |0-100| = 600 <= Range (2000).
                               // Spawns projectile!
                               // Projection check:
                               // Attacker progress = 500. Target lane length = 2000. Source lane length = 1000.
                               // Projected start progress = 500 * 2000 / 1000 = 1000.
                               // ImpactProgressMilli snapshot = 1000.

            BattleState state = sim.GetState();
            if (state.Projectiles.Count != 1) throw new InvalidOperationException("Projectile should be spawned.");
            BattleProjectile p = state.Projectiles[0];

            if (p.ProjectileLaneId != "lane_b") throw new InvalidOperationException("Projectile should spawn on target lane. Got: " + p.ProjectileLaneId);
            if (p.SourceLaneId != "lane_a") throw new InvalidOperationException("Source lane mismatch. Got: " + p.SourceLaneId);
            if (p.PositionMilli != 1000) throw new InvalidOperationException("Projected start progress should be 1000. Got: " + p.PositionMilli);
            if (p.ImpactProgressMilli != 1000) throw new InvalidOperationException("Impact snapshot progress should be 1000. Got: " + p.ImpactProgressMilli);
        }

        private static void Projectile_StateSnapshot_IsReturnedByGetState()
        {
            BattleConfigSnapshot cfg = MakeConfig(Fp.FromInt(10), Fp.FromInt(100), Fp.Zero, laneLengthMilli: 1_000L, sideBInitialEnergy: Fp.FromInt(10));
            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDefProjectile(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 0L,
                    droneAttackKind: AttackKind.Projectile, droneProjectileSpeedMilliPerTick: 100L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 1000L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);
            BattleSimulator sim = new BattleSimulator(cfg, initial);

            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideA));
            sim.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_ground", BattleSide.SideB));

            sim.AdvanceTick(); // Tick 1: Spawn. Target moves to pos 0.
            sim.AdvanceTick(); // Tick 2: Attack. Spawns projectile.

            BattleState state = sim.GetState();
            if (state.Projectiles.Count != 1) throw new InvalidOperationException("Should be 1 projectile active.");

            BattleProjectile p = state.Projectiles[0];
            if (p.ProjectileId != "p_1") throw new InvalidOperationException("ID should be p_1");
            if (p.Damage != Fp.FromInt(50)) throw new InvalidOperationException("Damage should be 50");
            if (p.RemainingTtlTick != 200) throw new InvalidOperationException("TTL should be 200");
        }

        private static void Projectile_Determinism_SameSetupProducesSameResult()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_a", LaneType.Ground, 1_000L, 0L),
                new LaneDefinition("lane_b", LaneType.Ground, 1_000L, 100L)
            };
            BattleSideConfig cfgA = new BattleSideConfig(BattleSide.SideA, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleSideConfig cfgB = new BattleSideConfig(BattleSide.SideB, Fp.FromInt(1000), Fp.FromInt(100), Fp.FromInt(100), Fp.Zero);
            BattleConfigSnapshot cfg = new BattleConfigSnapshot("test", cfgA, cfgB, 200, 100, 50, 3600, lanes);

            BattleSideInitialState sideA = new BattleSideInitialState(BattleSide.SideA, new SlotDefinition[]
            {
                CreateTestSlotDefProjectile(0, "p", "d", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(50),
                    droneRangeMilli: 1_200L, droneSpeedMilliPerTick: 1000L,
                    droneAttackKind: AttackKind.Projectile, droneProjectileSpeedMilliPerTick: 500L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L)
            });
            BattleSideInitialState sideB = new BattleSideInitialState(BattleSide.SideB, new SlotDefinition[]
            {
                CreateTestSlotDef(0, "pb", "db", Fp.FromInt(10), 100,
                    droneHp: Fp.FromInt(200), droneAttack: Fp.FromInt(0),
                    droneRangeMilli: 500L, droneSpeedMilliPerTick: 300L,
                    pilotHp: Fp.FromInt(200), pilotAttack: Fp.FromInt(20),
                    pilotRangeMilli: 1_500L, pilotSpeedMilliPerTick: 300L),
            });
            BattleInitialState initial = new BattleInitialState("stage_1", 42L, sideA, sideB);

            BattleSimulator sim1 = new BattleSimulator(cfg, initial);
            BattleSimulator sim2 = new BattleSimulator(cfg, initial);

            sim1.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a", BattleSide.SideA));
            sim1.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_b", BattleSide.SideB));
            sim2.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_a", BattleSide.SideA));
            sim2.SubmitCommand(BattleCommand.SpawnDroneSquad(0, 0, "lane_b", BattleSide.SideB));

            for (int i = 0; i < 5; i++)
            {
                sim1.AdvanceTick();
                sim2.AdvanceTick();
            }

            BattleState s1 = sim1.GetState();
            BattleState s2 = sim2.GetState();

            if (s1.Projectiles.Count != s2.Projectiles.Count) throw new InvalidOperationException("Projectiles count mismatch");
            if (s1.Projectiles.Count > 0)
            {
                if (s1.Projectiles[0].PositionMilli != s2.Projectiles[0].PositionMilli) throw new InvalidOperationException("Projectile pos mismatch");
            }
        }

        private static void SlotDefinition_RejectsUnknownAttackKind()
        {
            bool threw = false;
            try
            {
                new SlotDefinition(
                    0, "pilot_a", "drone_a", Fp.FromInt(10), 100,
                    Fp.FromInt(100), Fp.FromInt(10), Fp.Zero, 1_000L, 500L, 1,
                    (AttackKind)999, 100L,
                    Fp.FromInt(200), Fp.FromInt(20), Fp.Zero, 1_500L, 300L, 1,
                    AttackKind.Melee, 0L);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
            if (!threw) throw new InvalidOperationException("SlotDefinition constructor should reject invalid AttackKind.");
        }
    }
}
