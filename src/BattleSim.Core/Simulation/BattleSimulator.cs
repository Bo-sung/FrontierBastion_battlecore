using System;
using System.Collections.Generic;
using BattleSim.Core.Commands;
using BattleSim.Core.Config;
using BattleSim.Core.FixedPoint;
using BattleSim.Core.Results;
using BattleSim.Core.Rng;
using BattleSim.Core.State;

namespace BattleSim.Core.Simulation
{
    /// <summary>
    /// Deterministic battle simulator — v0.4 (SideA/SideB symmetric).
    ///
    /// Position convention (per lane):
    ///   0               == SideA base wall
    ///   LaneLengthMilli == SideB base wall
    ///
    /// SideA entities spawn at 0 and advance toward LaneLengthMilli.
    /// SideB entities spawn at LaneLengthMilli and advance toward 0.
    ///
    /// PvE interpretation: SideA = local player, SideB = AI controller.
    /// PvP: both sides driven by external command streams via SubmitCommand.
    ///
    /// Tick order inside AdvanceTick():
    ///   1. Apply pending commands (entity create / remove)
    ///   2. Energy regen for both sides (clamped)
    ///   3. Cooldown decay for both sides
    ///   4. Increment CurrentTick
    ///   5. Combat phase (attack or move; base damage on reach)
    ///   6. Remove dead entities; update pilot KO state
    ///   7. Check termination: SideBBaseDestroyed → SideABaseDestroyed → TimeOut
    /// </summary>
    public sealed class BattleSimulator
    {
        private readonly BattleConfigSnapshot _config;
        private readonly BattleInitialState _initial;
#pragma warning disable CS0414  // RNG reserved for future skill/crit use
        private readonly Xoshiro256StarStar _rng;
#pragma warning restore CS0414

        private int _currentTick;
        private bool _isTerminated;
        private BattleEndReason _endReason;
        private readonly RuntimeSideState _sideA;
        private readonly RuntimeSideState _sideB;
        private readonly List<BattleCommand> _pendingCommands;
        private BattleResult? _result;

        // Entity store: ordered by NumericId (creation order) for determinism.
        private readonly List<RuntimeEntity> _entities;
        private int _nextEntityId;

        public BattleSimulator(BattleConfigSnapshot config, BattleInitialState initial)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (initial == null) throw new ArgumentNullException("initial");
            _config = config;
            _initial = initial;
            _rng = new Xoshiro256StarStar(initial.RngSeed);
            _currentTick = 0;
            _isTerminated = false;
            _endReason = BattleEndReason.None;
            _pendingCommands = new List<BattleCommand>();
            _entities = new List<RuntimeEntity>();
            _nextEntityId = 1;

            _sideA = BuildSideState(config.SideA, initial.SideA);
            _sideB = BuildSideState(config.SideB, initial.SideB);
        }

        private static RuntimeSideState BuildSideState(BattleSideConfig cfg, BattleSideInitialState init)
        {
            RuntimeSlotState[] slots = new RuntimeSlotState[init.Slots.Length];
            for (int i = 0; i < init.Slots.Length; i++)
                slots[i] = new RuntimeSlotState { SlotIndex = init.Slots[i].SlotIndex };
            return new RuntimeSideState
            {
                Side               = cfg.Side,
                BaseHp             = cfg.BaseInitialHp,
                InitialBaseHp      = cfg.BaseInitialHp,
                Energy             = cfg.InitialEnergy,
                MaxEnergy          = cfg.MaxEnergy,
                EnergyRegenPerTick = cfg.EnergyRegenPerTick,
                SlotStates         = slots,
            };
        }

        public int CurrentTick { get { return _currentTick; } }
        public bool IsTerminated { get { return _isTerminated; } }

        /// <summary>
        /// Queue a command for the current tick.
        /// The command's <see cref="BattleCommand.Tick"/> must equal <see cref="CurrentTick"/>.
        /// Both SideA and SideB may submit commands in the same tick.
        /// </summary>
        public void SubmitCommand(BattleCommand command)
        {
            if (command == null) throw new ArgumentNullException("command");
            if (_isTerminated)
                throw new InvalidOperationException("Cannot submit commands after termination.");
            if (command.Tick != _currentTick)
                throw new ArgumentException(
                    "Command tick (" + command.Tick + ") does not match CurrentTick (" + _currentTick + ").",
                    "command");
            _pendingCommands.Add(command);
        }

        public void AdvanceTick()
        {
            if (_isTerminated)
                throw new InvalidOperationException("Cannot advance tick after termination.");

            // 1. Apply pending commands.
            foreach (BattleCommand cmd in _pendingCommands)
                ApplyCommand(cmd);
            _pendingCommands.Clear();

            // 2. Energy regen for both sides, clamped at MaxEnergy.
            RegenEnergy(_sideA);
            RegenEnergy(_sideB);

            // 3. Decay slot cooldowns toward zero.
            DecayCooldowns(_sideA);
            DecayCooldowns(_sideB);

            // 4. Advance tick.
            _currentTick++;

            // 5. Combat phase (movement + attacks + base damage).
            RunCombat();

            // 6. Remove dead entities; handle pilot knockouts.
            RemoveDeadEntities();

            // 7. Check termination.
            CheckTermination();
        }

        private static void RegenEnergy(RuntimeSideState side)
        {
            Fp regenned = side.Energy + side.EnergyRegenPerTick;
            side.Energy = regenned > side.MaxEnergy ? side.MaxEnergy : regenned;
        }

        private static void DecayCooldowns(RuntimeSideState side)
        {
            foreach (RuntimeSlotState s in side.SlotStates)
            {
                if (s.DroneCooldownTick > 0) s.DroneCooldownTick--;
                if (s.PilotCooldownTick > 0) s.PilotCooldownTick--;
            }
        }

        public BattleState GetState()
        {
            BattleSideState[] sides = new BattleSideState[]
            {
                BuildSideSnapshot(_sideA),
                BuildSideSnapshot(_sideB),
            };

            LaneState[] lanes = new LaneState[_config.Lanes.Length];
            for (int i = 0; i < _config.Lanes.Length; i++)
            {
                string laneId = _config.Lanes[i].LaneId;
                List<BattleEntity> snapshot = new List<BattleEntity>();
                foreach (RuntimeEntity e in _entities)
                    if (e.LaneId == laneId)
                        snapshot.Add(new BattleEntity(e.EntityId, e.Owner, e.Hp, e.PositionMilli));
                lanes[i] = new LaneState(laneId, snapshot);
            }

            return new BattleState(_currentTick, _isTerminated, _endReason, sides, lanes);
        }

        private static BattleSideState BuildSideSnapshot(RuntimeSideState side)
        {
            SlotState[] slots = new SlotState[side.SlotStates.Length];
            for (int i = 0; i < side.SlotStates.Length; i++)
            {
                RuntimeSlotState s = side.SlotStates[i];
                slots[i] = new SlotState(s.SlotIndex, s.IsPilotDeployed, s.IsPilotKnockedOut,
                    s.DroneCooldownTick, s.PilotCooldownTick);
            }
            return new BattleSideState(side.Side, side.BaseHp, side.Energy, slots);
        }

        public BattleResult GetResult()
        {
            if (!_isTerminated)
                throw new InvalidOperationException("Battle has not terminated.");
            return _result!;
        }

        // ------------------------------------------------------------------ commands

        private void ApplyCommand(BattleCommand cmd)
        {
            switch (cmd.CommandType)
            {
                case BattleCommandType.SpawnDroneSquad: ApplySpawnDroneSquad(cmd); break;
                case BattleCommandType.DeployPilot:     ApplyDeployPilot(cmd);     break;
                case BattleCommandType.RecallPilot:     ApplyRecallPilot(cmd);     break;
                default:
                    throw new ArgumentException("Unknown command type: " + cmd.CommandType, "command");
            }
        }

        private void ApplySpawnDroneSquad(BattleCommand cmd)
        {
            if (!LaneExists(cmd.LaneId))
                throw new ArgumentException("Lane not found: " + cmd.LaneId, "command");

            RuntimeSideState sideState = GetSideState(cmd.Side);
            RuntimeSlotState? slot = FindSlotState(sideState, cmd.SlotIndex);
            if (slot == null)
                throw new ArgumentException("Slot index not found: " + cmd.SlotIndex, "command");

            SlotDefinition? def = FindSlotDefinition(cmd.Side, cmd.SlotIndex);
            if (def == null)
                throw new ArgumentException("Slot definition not found: " + cmd.SlotIndex, "command");

            if (slot.DroneCooldownTick > 0)
                throw new InvalidOperationException(
                    "Slot " + cmd.SlotIndex + " drone is on cooldown (" + slot.DroneCooldownTick + " ticks remaining).");
            if (slot.IsPilotDeployed)
                throw new InvalidOperationException(
                    "Slot " + cmd.SlotIndex + " pilot is deployed; cannot spawn drone squad.");
            if (sideState.Energy < def.EnergyCost)
                throw new InvalidOperationException(
                    "Insufficient energy: need " + def.EnergyCost + ", have " + sideState.Energy + ".");

            sideState.Energy = sideState.Energy - def.EnergyCost;
            slot.DroneCooldownTick = def.CooldownTick;

            long startPos = cmd.Side == BattleSide.SideA ? 0L : GetLaneLengthMilli(cmd.LaneId);
            _entities.Add(CreateEntity(cmd.LaneId, cmd.Side,
                def.DroneHp, def.DroneAttack, def.DroneDefense, def.DroneRangeMilli, def.DroneSpeedMilliPerTick, def.DroneAttackPeriodTick,
                startPosition: startPos, slotIndex: -1));
        }

        private void ApplyDeployPilot(BattleCommand cmd)
        {
            if (!LaneExists(cmd.LaneId))
                throw new ArgumentException("Lane not found: " + cmd.LaneId, "command");

            RuntimeSideState sideState = GetSideState(cmd.Side);
            RuntimeSlotState? slot = FindSlotState(sideState, cmd.SlotIndex);
            if (slot == null)
                throw new ArgumentException("Slot index not found: " + cmd.SlotIndex, "command");

            SlotDefinition? def = FindSlotDefinition(cmd.Side, cmd.SlotIndex);
            if (def == null)
                throw new ArgumentException("Slot definition not found: " + cmd.SlotIndex, "command");

            if (slot.IsPilotDeployed)
                throw new InvalidOperationException(
                    "Slot " + cmd.SlotIndex + " pilot is already deployed.");
            if (slot.IsPilotKnockedOut)
                throw new InvalidOperationException(
                    "Slot " + cmd.SlotIndex + " pilot is knocked out and cannot deploy.");

            long startPos = cmd.Side == BattleSide.SideA ? 0L : GetLaneLengthMilli(cmd.LaneId);
            RuntimeEntity pilot = CreateEntity(cmd.LaneId, cmd.Side,
                def.PilotHp, def.PilotAttack, def.PilotDefense, def.PilotRangeMilli, def.PilotSpeedMilliPerTick, def.PilotAttackPeriodTick,
                startPosition: startPos, slotIndex: cmd.SlotIndex);
            _entities.Add(pilot);

            slot.IsPilotDeployed = true;
            slot.PilotEntity = pilot;
        }

        private void ApplyRecallPilot(BattleCommand cmd)
        {
            RuntimeSideState sideState = GetSideState(cmd.Side);
            RuntimeSlotState? slot = FindSlotState(sideState, cmd.SlotIndex);
            if (slot == null)
                throw new ArgumentException("Slot index not found: " + cmd.SlotIndex, "command");
            if (!slot.IsPilotDeployed)
                throw new InvalidOperationException(
                    "Slot " + cmd.SlotIndex + " pilot is not deployed; cannot recall.");

            if (slot.PilotEntity != null)
            {
                _entities.Remove(slot.PilotEntity);
                slot.PilotEntity = null;
            }
            slot.IsPilotDeployed = false;
            slot.PilotCooldownTick = _config.PilotReturnCooldownTick;
        }

        // ------------------------------------------------------------------ per-tick phases

        private void RunCombat()
        {
            // Snapshot active entities in creation order (NumericId order) for determinism.
            List<RuntimeEntity> activeEntities = new List<RuntimeEntity>(_entities);
            activeEntities.Sort((a, b) => a.NumericId.CompareTo(b.NumericId));

            foreach (RuntimeEntity entity in activeEntities)
            {
                if (entity.Hp <= Fp.Zero) continue; // died earlier this tick

                RuntimeEntity? target = FindNearestOpponent(entity);
                if (target != null)
                {
                    // Target in range. Check if we can attack.
                    if (_currentTick >= entity.NextAttackReadyTick)
                    {
                        // Attack: deal damage, no movement.
                        Fp calculatedDamage = entity.Attack - target.Defense;
                        Fp minDamage = Fp.FromRaw(BattleCoreDefaults.MinDamageRaw);
                        Fp finalDamage = calculatedDamage < minDamage ? minDamage : calculatedDamage;

                        target.Hp = target.Hp - finalDamage;

                        // Reset cooldown.
                        entity.NextAttackReadyTick = _currentTick + entity.AttackPeriodTick;
                    }
                    // Else: Attack cooldown prevents attack, holds position (do not move).
                }
                else
                {
                    // No target in range: Move toward the opposing base; deal damage if base wall reached.
                    LaneDefinition laneDef = FindLaneDefinition(entity.LaneId);
                    long laneLen = laneDef.LaneLengthMilli;

                    if (entity.Owner == BattleSide.SideA)
                    {
                        entity.PositionMilli += entity.SpeedMilliPerTick;
                        if (entity.PositionMilli >= laneLen)
                        {
                            entity.PositionMilli = laneLen;
                            _sideB.BaseHp = _sideB.BaseHp - entity.Attack;
                        }
                    }
                    else // SideB
                    {
                        entity.PositionMilli -= entity.SpeedMilliPerTick;
                        if (entity.PositionMilli <= 0)
                        {
                            entity.PositionMilli = 0;
                            _sideA.BaseHp = _sideA.BaseHp - entity.Attack;
                        }
                    }
                }
            }
        }

        private void RemoveDeadEntities()
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                RuntimeEntity e = _entities[i];
                if (e.Hp > Fp.Zero) continue;

                // If this is a deployed pilot, mark slot as knocked out.
                if (e.SlotIndex >= 0)
                {
                    RuntimeSideState sideState = GetSideState(e.Owner);
                    RuntimeSlotState? slot = FindSlotState(sideState, e.SlotIndex);
                    if (slot != null && slot.PilotEntity == e)
                    {
                        slot.IsPilotDeployed = false;
                        slot.IsPilotKnockedOut = true;
                        slot.PilotEntity = null;
                        // Block drone spawns for the knockout recovery window.
                        if (slot.DroneCooldownTick < _config.PilotKnockoutDroneResumeTick)
                            slot.DroneCooldownTick = _config.PilotKnockoutDroneResumeTick;
                    }
                }
                _entities.RemoveAt(i);
            }
        }

        private void CheckTermination()
        {
            if (_isTerminated) return;

            if (_sideB.BaseHp <= Fp.Zero)
            {
                Fp sideARatio = _sideA.InitialBaseHp > Fp.Zero
                    ? (_sideA.BaseHp < Fp.Zero ? Fp.Zero : _sideA.BaseHp) / _sideA.InitialBaseHp
                    : Fp.Zero;
                _endReason = BattleEndReason.SideBBaseDestroyed;
                _isTerminated = true;
                _result = new BattleResult(BattleSide.SideA, BattleEndReason.SideBBaseDestroyed,
                    _currentTick, sideARatio, Fp.Zero);
                return;
            }

            if (_sideA.BaseHp <= Fp.Zero)
            {
                Fp sideBRatio = _sideB.InitialBaseHp > Fp.Zero
                    ? (_sideB.BaseHp < Fp.Zero ? Fp.Zero : _sideB.BaseHp) / _sideB.InitialBaseHp
                    : Fp.Zero;
                _endReason = BattleEndReason.SideABaseDestroyed;
                _isTerminated = true;
                _result = new BattleResult(BattleSide.SideB, BattleEndReason.SideABaseDestroyed,
                    _currentTick, Fp.Zero, sideBRatio);
                return;
            }

            if (_currentTick >= _config.MaxBattleTick)
            {
                Fp sideARatio = _sideA.InitialBaseHp > Fp.Zero
                    ? _sideA.BaseHp / _sideA.InitialBaseHp : Fp.Zero;
                Fp sideBRatio = _sideB.InitialBaseHp > Fp.Zero
                    ? _sideB.BaseHp / _sideB.InitialBaseHp : Fp.Zero;
                _endReason = BattleEndReason.TimeOut;
                _isTerminated = true;
                _result = BattleResult.FromTimeOut(
                    _currentTick, sideARatio, sideBRatio, _config.TimeOutTieWinnerSide);
            }
        }

        // ------------------------------------------------------------------ combat helpers

        private RuntimeEntity? FindNearestOpponent(RuntimeEntity attacker)
        {
            RuntimeEntity? nearest = null;
            long nearestDist = long.MaxValue;

            LaneDefinition attackerLane = FindLaneDefinition(attacker.LaneId);

            foreach (RuntimeEntity e in _entities)
            {
                if (e == attacker) continue;
                if (e.Owner == attacker.Owner) continue;
                if (e.Hp <= Fp.Zero) continue;

                LaneDefinition targetLane = FindLaneDefinition(e.LaneId);

                long dx = Math.Abs(e.PositionMilli - attacker.PositionMilli);
                long dy = Math.Abs(targetLane.LaneWorldYMilli - attackerLane.LaneWorldYMilli);
                long dist = dx + dy; // Manhattan distance

                if (dist > attacker.RangeMilli) continue;

                if (nearest == null
                    || dist < nearestDist
                    || (dist == nearestDist && e.NumericId < nearest.NumericId))
                {
                    nearest = e;
                    nearestDist = dist;
                }
            }
            return nearest;
        }

        private LaneDefinition FindLaneDefinition(string laneId)
        {
            foreach (LaneDefinition lane in _config.Lanes)
                if (lane.LaneId == laneId) return lane;
            throw new InvalidOperationException("Lane not found: " + laneId);
        }

        private RuntimeEntity CreateEntity(
            string laneId, BattleSide owner,
            Fp hp, Fp attack, Fp defense, long rangeMilli, long speedMilliPerTick, int attackPeriodTick,
            long startPosition, int slotIndex)
        {
            int id = _nextEntityId++;
            return new RuntimeEntity
            {
                NumericId           = id,
                EntityId            = "e_" + id,
                LaneId              = laneId,
                Owner               = owner,
                Hp                  = hp,
                Attack              = attack,
                Defense             = defense,
                RangeMilli          = rangeMilli,
                SpeedMilliPerTick   = speedMilliPerTick,
                PositionMilli       = startPosition,
                SlotIndex           = slotIndex,
                AttackPeriodTick    = attackPeriodTick,
                NextAttackReadyTick = _currentTick,
            };
        }

        // ------------------------------------------------------------------ lookup helpers

        private RuntimeSideState GetSideState(BattleSide side)
        {
            if (side == BattleSide.SideA) return _sideA;
            if (side == BattleSide.SideB) return _sideB;
            throw new ArgumentException("Invalid side: " + side, "side");
        }

        private bool LaneExists(string laneId)
        {
            foreach (LaneDefinition lane in _config.Lanes)
                if (lane.LaneId == laneId) return true;
            return false;
        }

        private long GetLaneLengthMilli(string laneId)
        {
            foreach (LaneDefinition lane in _config.Lanes)
                if (lane.LaneId == laneId) return lane.LaneLengthMilli;
            return 10_000L; // unreachable when called after LaneExists
        }

        private static RuntimeSlotState? FindSlotState(RuntimeSideState side, int slotIndex)
        {
            foreach (RuntimeSlotState s in side.SlotStates)
                if (s.SlotIndex == slotIndex) return s;
            return null;
        }

        private SlotDefinition? FindSlotDefinition(BattleSide side, int slotIndex)
        {
            BattleSideInitialState sideInit = side == BattleSide.SideA ? _initial.SideA : _initial.SideB;
            foreach (SlotDefinition def in sideInit.Slots)
                if (def.SlotIndex == slotIndex) return def;
            return null;
        }

        // ------------------------------------------------------------------ inner types

        private sealed class RuntimeSideState
        {
            public BattleSide Side;
            public Fp BaseHp;
            public Fp InitialBaseHp;
            public Fp Energy;
            public Fp MaxEnergy;
            public Fp EnergyRegenPerTick;
            public RuntimeSlotState[] SlotStates = new RuntimeSlotState[0];
        }

        private sealed class RuntimeSlotState
        {
            public int SlotIndex;
            public bool IsPilotDeployed;
            public bool IsPilotKnockedOut;
            public int DroneCooldownTick;
            public int PilotCooldownTick;
            public RuntimeEntity? PilotEntity;
        }

        private sealed class RuntimeEntity
        {
            public int NumericId;
            public string EntityId = "";
            public string LaneId = "";
            public BattleSide Owner;
            public Fp Hp;
            public Fp Attack;
            public Fp Defense;
            public long RangeMilli;
            public long SpeedMilliPerTick;
            public long PositionMilli;
            /// <summary>Pilot slot index, or -1 for drone entities.</summary>
            public int SlotIndex;
            public int AttackPeriodTick;
            public int NextAttackReadyTick;
        }
    }
}
