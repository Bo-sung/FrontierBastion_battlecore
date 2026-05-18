using System.Collections.Generic;
using BattleSim.Core.FixedPoint;
using BattleSim.Core.Results;

namespace BattleSim.Core.State
{
    /// <summary>
    /// Immutable snapshot of a battle at a specific tick.
    /// Returned from <see cref="Simulation.BattleSimulator.GetState"/>.
    /// </summary>
    public sealed class BattleState
    {
        public int CurrentTick { get; private set; }
        public Fp PlayerBaseHp { get; private set; }
        public Fp EnemyBaseHp { get; private set; }
        public Fp PlayerEnergy { get; private set; }
        public bool IsTerminated { get; private set; }
        public BattleEndReason EndReason { get; private set; }
        public IReadOnlyList<SlotState> Slots { get; private set; }
        public IReadOnlyList<LaneState> Lanes { get; private set; }

        public BattleState(
            int currentTick,
            Fp playerBaseHp,
            Fp enemyBaseHp,
            Fp playerEnergy,
            bool isTerminated,
            BattleEndReason endReason,
            IReadOnlyList<SlotState> slots,
            IReadOnlyList<LaneState> lanes)
        {
            CurrentTick = currentTick;
            PlayerBaseHp = playerBaseHp;
            EnemyBaseHp = enemyBaseHp;
            PlayerEnergy = playerEnergy;
            IsTerminated = isTerminated;
            EndReason = endReason;
            Slots = slots;
            Lanes = lanes;
        }
    }

    public sealed class SlotState
    {
        public int SlotIndex { get; private set; }
        public bool IsPilotDeployed { get; private set; }
        public bool IsPilotKnockedOut { get; private set; }
        public int DroneCooldownTick { get; private set; }
        public int PilotCooldownTick { get; private set; }

        public SlotState(int slotIndex, bool isPilotDeployed, bool isPilotKnockedOut,
            int droneCooldownTick, int pilotCooldownTick)
        {
            SlotIndex = slotIndex;
            IsPilotDeployed = isPilotDeployed;
            IsPilotKnockedOut = isPilotKnockedOut;
            DroneCooldownTick = droneCooldownTick;
            PilotCooldownTick = pilotCooldownTick;
        }
    }

    public sealed class LaneState
    {
        public string LaneId { get; private set; }
        public IReadOnlyList<BattleEntity> Entities { get; private set; }

        public LaneState(string laneId, IReadOnlyList<BattleEntity> entities)
        {
            LaneId = laneId;
            Entities = entities;
        }
    }

    public sealed class BattleEntity
    {
        public string EntityId { get; private set; }
        public OwnerSide OwnerSide { get; private set; }
        public Fp Hp { get; private set; }
        /// <summary>Position in milliunits (see <see cref="Milliunits"/>).</summary>
        public long PositionMilli { get; private set; }

        public BattleEntity(string entityId, OwnerSide ownerSide, Fp hp, long positionMilli)
        {
            EntityId = entityId;
            OwnerSide = ownerSide;
            Hp = hp;
            PositionMilli = positionMilli;
        }
    }
}
