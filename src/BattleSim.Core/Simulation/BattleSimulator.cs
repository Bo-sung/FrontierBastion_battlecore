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
    /// Deterministic battle simulator — v0.2.
    ///
    /// Position convention (per lane):
    ///   0                       == player base wall
    ///   LaneLengthMilli         == enemy  base wall
    ///
    /// Player entities spawn at 0 and advance toward LaneLengthMilli.
    /// Enemy  entities spawn at LaneLengthMilli and advance toward 0.
    ///
    /// Tick order inside AdvanceTick():
    ///   1. Apply pending commands (entity create / remove)
    ///   2. Energy regen (clamped)
    ///   3. Cooldown decay
    ///   4. Increment CurrentTick
    ///   5. Spawn enemies from schedule
    ///   6. Combat phase (attack or move; base damage on reach)
    ///   7. Remove dead entities; update pilot KO state
    ///   8. Check termination: Victory → Defeat → TimeOut (first match wins)
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
        private Fp _playerBaseHp;
        private Fp _enemyBaseHp;
        private Fp _playerEnergy;
        private readonly List<BattleCommand> _pendingCommands;
        private readonly RuntimeSlotState[] _slotStates;
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
            _playerBaseHp = config.PlayerBaseInitialHp;
            _enemyBaseHp = config.EnemyBaseInitialHp;
            _playerEnergy = config.InitialEnergy;
            _pendingCommands = new List<BattleCommand>();
            _entities = new List<RuntimeEntity>();
            _nextEntityId = 1;

            _slotStates = new RuntimeSlotState[initial.Slots.Length];
            for (int i = 0; i < initial.Slots.Length; i++)
                _slotStates[i] = new RuntimeSlotState { SlotIndex = initial.Slots[i].SlotIndex };
        }

        public int CurrentTick { get { return _currentTick; } }
        public bool IsTerminated { get { return _isTerminated; } }

        /// <summary>
        /// Queue a command for the current tick.
        /// The command's <see cref="BattleCommand.Tick"/> must equal <see cref="CurrentTick"/>.
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

            // 2. Energy regen, clamped at MaxEnergy.
            Fp regenned = _playerEnergy + _config.EnergyRegenPerTick;
            _playerEnergy = regenned > _config.MaxEnergy ? _config.MaxEnergy : regenned;

            // 3. Decay slot cooldowns toward zero.
            for (int i = 0; i < _slotStates.Length; i++)
            {
                RuntimeSlotState s = _slotStates[i];
                if (s.DroneCooldownTick > 0) s.DroneCooldownTick--;
                if (s.PilotCooldownTick > 0) s.PilotCooldownTick--;
            }

            // 4. Advance tick.
            _currentTick++;

            // 5. Spawn enemies from schedule.
            SpawnEnemies();

            // 6. Combat phase (movement + attacks + base damage).
            RunCombat();

            // 7. Remove dead entities; handle pilot knockouts.
            RemoveDeadEntities();

            // 8. Check termination.
            CheckTermination();
        }

        public BattleState GetState()
        {
            SlotState[] slots = new SlotState[_slotStates.Length];
            for (int i = 0; i < _slotStates.Length; i++)
            {
                RuntimeSlotState s = _slotStates[i];
                slots[i] = new SlotState(s.SlotIndex, s.IsPilotDeployed, s.IsPilotKnockedOut,
                    s.DroneCooldownTick, s.PilotCooldownTick);
            }

            // Build lane snapshots from living entities (dead ones already removed after each tick).
            LaneState[] lanes = new LaneState[_config.Lanes.Length];
            for (int i = 0; i < _config.Lanes.Length; i++)
            {
                string laneId = _config.Lanes[i].LaneId;
                List<BattleEntity> snapshot = new List<BattleEntity>();
                foreach (RuntimeEntity e in _entities)
                {
                    if (e.LaneId == laneId)
                        snapshot.Add(new BattleEntity(e.EntityId, e.Owner, e.Hp, e.PositionMilli));
                }
                lanes[i] = new LaneState(laneId, snapshot);
            }

            return new BattleState(_currentTick, _playerBaseHp, _enemyBaseHp, _playerEnergy,
                _isTerminated, _endReason, slots, lanes);
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

            RuntimeSlotState? slot = FindSlotState(cmd.SlotIndex);
            if (slot == null)
                throw new ArgumentException("Slot index not found: " + cmd.SlotIndex, "command");

            SlotDefinition? def = FindSlotDefinition(cmd.SlotIndex);
            if (def == null)
                throw new ArgumentException("Slot definition not found: " + cmd.SlotIndex, "command");

            if (slot.DroneCooldownTick > 0)
                throw new InvalidOperationException(
                    "Slot " + cmd.SlotIndex + " drone is on cooldown (" + slot.DroneCooldownTick + " ticks remaining).");
            if (slot.IsPilotDeployed)
                throw new InvalidOperationException(
                    "Slot " + cmd.SlotIndex + " pilot is deployed; cannot spawn drone squad.");
            if (_playerEnergy < def.EnergyCost)
                throw new InvalidOperationException(
                    "Insufficient energy: need " + def.EnergyCost + ", have " + _playerEnergy + ".");

            _playerEnergy = _playerEnergy - def.EnergyCost;
            slot.DroneCooldownTick = def.CooldownTick;

            _entities.Add(CreateEntity(
                cmd.LaneId, OwnerSide.Player,
                def.DroneHp, def.DroneAttack, def.DroneRangeMilli, def.DroneSpeedMilliPerTick,
                startPosition: 0L, slotIndex: -1));
        }

        private void ApplyDeployPilot(BattleCommand cmd)
        {
            if (!LaneExists(cmd.LaneId))
                throw new ArgumentException("Lane not found: " + cmd.LaneId, "command");

            RuntimeSlotState? slot = FindSlotState(cmd.SlotIndex);
            if (slot == null)
                throw new ArgumentException("Slot index not found: " + cmd.SlotIndex, "command");

            SlotDefinition? def = FindSlotDefinition(cmd.SlotIndex);
            if (def == null)
                throw new ArgumentException("Slot definition not found: " + cmd.SlotIndex, "command");

            if (slot.IsPilotDeployed)
                throw new InvalidOperationException(
                    "Slot " + cmd.SlotIndex + " pilot is already deployed.");
            if (slot.IsPilotKnockedOut)
                throw new InvalidOperationException(
                    "Slot " + cmd.SlotIndex + " pilot is knocked out and cannot deploy.");

            RuntimeEntity pilot = CreateEntity(
                cmd.LaneId, OwnerSide.Player,
                def.PilotHp, def.PilotAttack, def.PilotRangeMilli, def.PilotSpeedMilliPerTick,
                startPosition: 0L, slotIndex: cmd.SlotIndex);
            _entities.Add(pilot);

            slot.IsPilotDeployed = true;
            slot.PilotEntity = pilot;
        }

        private void ApplyRecallPilot(BattleCommand cmd)
        {
            RuntimeSlotState? slot = FindSlotState(cmd.SlotIndex);
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

        private void SpawnEnemies()
        {
            foreach (EnemySpawnDefinition def in _config.EnemySpawnSchedule)
            {
                if (def.SpawnTick != _currentTick) continue;
                if (!LaneExists(def.LaneId)) continue;

                long laneLen = GetLaneLengthMilli(def.LaneId);
                _entities.Add(CreateEntity(
                    def.LaneId, OwnerSide.Enemy,
                    def.Hp, def.Attack, def.RangeMilli, def.SpeedMilliPerTick,
                    startPosition: laneLen, slotIndex: -1));
            }
        }

        private void RunCombat()
        {
            foreach (LaneDefinition laneDef in _config.Lanes)
            {
                string laneId = laneDef.LaneId;
                long laneLen = laneDef.LaneLengthMilli;

                // Snapshot lane entities in creation order (NumericId order).
                List<RuntimeEntity> laneEntities = new List<RuntimeEntity>();
                foreach (RuntimeEntity e in _entities)
                    if (e.LaneId == laneId)
                        laneEntities.Add(e);

                foreach (RuntimeEntity entity in laneEntities)
                {
                    if (entity.Hp <= Fp.Zero) continue; // died earlier this tick

                    RuntimeEntity? target = FindNearestEnemy(entity, laneEntities);
                    if (target != null)
                    {
                        // Attack: deal damage, no movement.
                        target.Hp = target.Hp - entity.Attack;
                    }
                    else
                    {
                        // Move toward the opposing base, deal damage if base reached.
                        if (entity.Owner == OwnerSide.Player)
                        {
                            entity.PositionMilli += entity.SpeedMilliPerTick;
                            if (entity.PositionMilli >= laneLen)
                            {
                                entity.PositionMilli = laneLen;
                                _enemyBaseHp = _enemyBaseHp - entity.Attack;
                            }
                        }
                        else
                        {
                            entity.PositionMilli -= entity.SpeedMilliPerTick;
                            if (entity.PositionMilli <= 0)
                            {
                                entity.PositionMilli = 0;
                                _playerBaseHp = _playerBaseHp - entity.Attack;
                            }
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
                    RuntimeSlotState? slot = FindSlotState(e.SlotIndex);
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

            if (_enemyBaseHp <= Fp.Zero)
            {
                Fp playerRatio = _config.PlayerBaseInitialHp > Fp.Zero
                    ? (_playerBaseHp < Fp.Zero ? Fp.Zero : _playerBaseHp) / _config.PlayerBaseInitialHp
                    : Fp.Zero;
                _endReason = BattleEndReason.EnemyBaseDestroyed;
                _isTerminated = true;
                _result = new BattleResult(BattleOutcome.Victory, BattleEndReason.EnemyBaseDestroyed,
                    _currentTick, playerRatio, Fp.Zero);
                return;
            }

            if (_playerBaseHp <= Fp.Zero)
            {
                Fp enemyRatio = _config.EnemyBaseInitialHp > Fp.Zero
                    ? (_enemyBaseHp < Fp.Zero ? Fp.Zero : _enemyBaseHp) / _config.EnemyBaseInitialHp
                    : Fp.Zero;
                _endReason = BattleEndReason.PlayerBaseDestroyed;
                _isTerminated = true;
                _result = new BattleResult(BattleOutcome.Defeat, BattleEndReason.PlayerBaseDestroyed,
                    _currentTick, Fp.Zero, enemyRatio);
                return;
            }

            if (_currentTick >= _config.MaxBattleTick)
            {
                Fp playerRatio = _config.PlayerBaseInitialHp > Fp.Zero
                    ? _playerBaseHp / _config.PlayerBaseInitialHp : Fp.Zero;
                Fp enemyRatio = _config.EnemyBaseInitialHp > Fp.Zero
                    ? _enemyBaseHp / _config.EnemyBaseInitialHp : Fp.Zero;
                _endReason = BattleEndReason.TimeOut;
                _isTerminated = true;
                _result = BattleResult.FromTimeOut(_currentTick, playerRatio, enemyRatio);
            }
        }

        // ------------------------------------------------------------------ combat helpers

        private RuntimeEntity? FindNearestEnemy(RuntimeEntity attacker, List<RuntimeEntity> laneEntities)
        {
            RuntimeEntity? nearest = null;
            long nearestDist = long.MaxValue;

            foreach (RuntimeEntity e in laneEntities)
            {
                if (e == attacker) continue;
                if (e.Owner == attacker.Owner) continue;
                if (e.Hp <= Fp.Zero) continue;

                long dist = Math.Abs(e.PositionMilli - attacker.PositionMilli);
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

        private RuntimeEntity CreateEntity(
            string laneId, OwnerSide owner,
            Fp hp, Fp attack, long rangeMilli, long speedMilliPerTick,
            long startPosition, int slotIndex)
        {
            int id = _nextEntityId++;
            return new RuntimeEntity
            {
                NumericId          = id,
                EntityId           = "e_" + id,
                LaneId             = laneId,
                Owner              = owner,
                Hp                 = hp,
                Attack             = attack,
                RangeMilli         = rangeMilli,
                SpeedMilliPerTick  = speedMilliPerTick,
                PositionMilli      = startPosition,
                SlotIndex          = slotIndex,
            };
        }

        // ------------------------------------------------------------------ lookup helpers

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

        private RuntimeSlotState? FindSlotState(int slotIndex)
        {
            for (int i = 0; i < _slotStates.Length; i++)
                if (_slotStates[i].SlotIndex == slotIndex) return _slotStates[i];
            return null;
        }

        private SlotDefinition? FindSlotDefinition(int slotIndex)
        {
            foreach (SlotDefinition def in _initial.Slots)
                if (def.SlotIndex == slotIndex) return def;
            return null;
        }

        // ------------------------------------------------------------------ inner types

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
            public OwnerSide Owner;
            public Fp Hp;
            public Fp Attack;
            public long RangeMilli;
            public long SpeedMilliPerTick;
            public long PositionMilli;
            /// <summary>Pilot slot index, or -1 for drones / enemy entities.</summary>
            public int SlotIndex;
        }
    }
}
