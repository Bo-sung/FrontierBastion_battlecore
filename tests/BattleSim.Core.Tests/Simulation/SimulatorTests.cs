using System;
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
            ConstructsAndExposesInitialState();
            AdvanceTickThrowsNotImplemented();
            GetResultBeforeTerminationThrows();
            CommandFactoriesEnforceLaneRequirements();
            BattleResultFromTimeOutResolvesByRatio();
        }

        private static BattleConfigSnapshot MinimalConfig()
        {
            LaneDefinition[] lanes = new LaneDefinition[]
            {
                new LaneDefinition("lane_ground", LaneType.Ground),
            };
            return new BattleConfigSnapshot(
                configVersion: "test",
                initialEnergy: Fp.FromInt(0),
                maxEnergy: Fp.FromInt(100),
                energyRegenPerTick: Fp.FromFraction(1, 4),
                pilotDeployCooldownTick: 200,
                pilotReturnCooldownTick: 100,
                pilotKnockoutDroneResumeTick: 50,
                playerBaseInitialHp: Fp.FromInt(1000),
                enemyBaseInitialHp: Fp.FromInt(1000),
                maxBattleTick: 3600,
                lanes: lanes);
        }

        private static BattleInitialState MinimalInitialState()
        {
            SlotDefinition[] slots = new SlotDefinition[]
            {
                new SlotDefinition(0, "pilot_a", "drone_a", Fp.FromInt(10), 100),
            };
            return new BattleInitialState("stage_1", 42L, slots);
        }

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

        private static void AdvanceTickThrowsNotImplemented()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(),
                new BattleInitialState("stage_1", 1L, new SlotDefinition[]
                {
                    new SlotDefinition(0, "pilot_a", "drone_a", Fp.FromInt(10), 100),
                }));
            bool threw = false;
            try { sim.AdvanceTick(); }
            catch (NotImplementedException) { threw = true; }
            if (!threw) throw new InvalidOperationException("AdvanceTick should be NotImplemented in skeleton.");
        }

        private static void GetResultBeforeTerminationThrows()
        {
            BattleSimulator sim = new BattleSimulator(MinimalConfig(),
                new BattleInitialState("stage_1", 1L, new SlotDefinition[]
                {
                    new SlotDefinition(0, "pilot_a", "drone_a", Fp.FromInt(10), 100),
                }));
            bool threw = false;
            try { sim.GetResult(); }
            catch (InvalidOperationException) { threw = true; }
            if (!threw) throw new InvalidOperationException("GetResult should throw before termination.");
        }

        private static void CommandFactoriesEnforceLaneRequirements()
        {
            bool threw;

            threw = false;
#pragma warning disable CS8625 // intentional: verifying runtime null guard rejects null
            try { BattleCommand.SpawnDroneSquad(0, 0, null); }
#pragma warning restore CS8625
            catch (ArgumentException) { threw = true; }
            if (!threw) throw new InvalidOperationException("SpawnDroneSquad must require laneId.");

            threw = false;
            try { BattleCommand.DeployPilot(0, 0, ""); }
            catch (ArgumentException) { threw = true; }
            if (!threw) throw new InvalidOperationException("DeployPilot must require laneId.");

            // RecallPilot: lane policy unresolved — null must NOT throw here (PD-A).
#pragma warning disable CS8625 // intentional: PendingDecision — laneId may remain nullable for RecallPilot
            BattleCommand recall = BattleCommand.RecallPilot(0, 0, null);
#pragma warning restore CS8625
            if (recall.CommandType != BattleCommandType.RecallPilot)
            {
                throw new InvalidOperationException("RecallPilot command type mismatch.");
            }
        }

        private static void BattleResultFromTimeOutResolvesByRatio()
        {
            // Player higher → Victory
            BattleResult win = BattleResult.FromTimeOut(3600,
                Fp.FromFraction(7, 10), Fp.FromFraction(3, 10));
            if (win.Outcome != BattleOutcome.Victory) throw new InvalidOperationException("TimeOut win");
            if (win.EndReason != BattleEndReason.TimeOut) throw new InvalidOperationException("TimeOut reason");

            // Tie → Defeat
            BattleResult tie = BattleResult.FromTimeOut(3600,
                Fp.FromFraction(5, 10), Fp.FromFraction(5, 10));
            if (tie.Outcome != BattleOutcome.Defeat) throw new InvalidOperationException("Tie must be Defeat");

            // Player lower → Defeat
            BattleResult loss = BattleResult.FromTimeOut(3600,
                Fp.FromFraction(2, 10), Fp.FromFraction(4, 10));
            if (loss.Outcome != BattleOutcome.Defeat) throw new InvalidOperationException("TimeOut loss");
        }
    }
}
