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
    /// Minimal public surface for the deterministic battle simulator.
    /// The full per-tick combat algorithm is intentionally not implemented
    /// yet; this skeleton fixes the public API so dependents can compile
    /// against it. Methods that require gameplay logic throw
    /// <see cref="NotImplementedException"/>.
    /// </summary>
    public sealed class BattleSimulator
    {
        private readonly BattleConfigSnapshot _config;
        private readonly BattleInitialState _initial;
        private readonly Xoshiro256StarStar _rng;

        private int _currentTick;
        private bool _isTerminated;
        private BattleEndReason _endReason;
        private Fp _playerBaseHp;
        private Fp _enemyBaseHp;
        private Fp _playerEnergy;
        private readonly List<BattleCommand> _pendingCommands;

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
        }

        public int CurrentTick { get { return _currentTick; } }
        public bool IsTerminated { get { return _isTerminated; } }

        /// <summary>
        /// Queue a command for the current tick. The command's
        /// <see cref="BattleCommand.Tick"/> must equal <see cref="CurrentTick"/>.
        /// </summary>
        public void SubmitCommand(BattleCommand command)
        {
            if (command == null) throw new ArgumentNullException("command");
            if (_isTerminated)
            {
                throw new InvalidOperationException("Cannot submit commands after termination.");
            }
            if (command.Tick != _currentTick)
            {
                throw new ArgumentException(
                    "Command tick (" + command.Tick + ") does not match CurrentTick (" + _currentTick + ").",
                    "command");
            }
            _pendingCommands.Add(command);
        }

        /// <summary>
        /// Advance the simulation by one tick (50ms).
        /// Combat resolution is not implemented in this skeleton.
        /// </summary>
        public void AdvanceTick()
        {
            throw new NotImplementedException(
                "BattleSimulator.AdvanceTick is not implemented yet (skeleton).");
        }

        public BattleState GetState()
        {
            return new BattleState(
                _currentTick,
                _playerBaseHp,
                _enemyBaseHp,
                _playerEnergy,
                _isTerminated,
                _endReason,
                new SlotState[0],
                new LaneState[0]);
        }

        public BattleResult GetResult()
        {
            if (!_isTerminated)
            {
                throw new InvalidOperationException("Battle has not terminated.");
            }
            throw new NotImplementedException(
                "BattleSimulator.GetResult is not implemented yet (skeleton).");
        }
    }
}
