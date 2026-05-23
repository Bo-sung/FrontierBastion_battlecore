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
        public bool IsTerminated { get; private set; }
        public BattleEndReason EndReason { get; private set; }

        /// <summary>Index 0 = SideA, Index 1 = SideB.</summary>
        public IReadOnlyList<BattleSideState> Sides { get; private set; }
        public IReadOnlyList<LaneState> Lanes { get; private set; }
        public IReadOnlyList<BattleProjectile> Projectiles { get; private set; }

        // v0.4 Backward compatible constructor
        public BattleState(
            int currentTick,
            bool isTerminated,
            BattleEndReason endReason,
            IReadOnlyList<BattleSideState> sides,
            IReadOnlyList<LaneState> lanes)
            : this(currentTick, isTerminated, endReason, sides, lanes, new BattleProjectile[0])
        {
        }

        // v0.5 Constructor with projectiles support
        public BattleState(
            int currentTick,
            bool isTerminated,
            BattleEndReason endReason,
            IReadOnlyList<BattleSideState> sides,
            IReadOnlyList<LaneState> lanes,
            IReadOnlyList<BattleProjectile> projectiles)
        {
            CurrentTick = currentTick;
            IsTerminated = isTerminated;
            EndReason = endReason;
            Sides = sides;
            Lanes = lanes;
            Projectiles = projectiles;
        }
    }

    public sealed class BattleSideState
    {
        public BattleSide Side { get; private set; }
        public Fp BaseHp { get; private set; }
        public Fp Energy { get; private set; }
        public IReadOnlyList<SlotState> Slots { get; private set; }

        public BattleSideState(BattleSide side, Fp baseHp, Fp energy, IReadOnlyList<SlotState> slots)
        {
            Side = side;
            BaseHp = baseHp;
            Energy = energy;
            Slots = slots;
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
        public BattleSide Side { get; private set; }
        public Fp Hp { get; private set; }
        /// <summary>Position in milliunits (see <see cref="Milliunits"/>).</summary>
        public long PositionMilli { get; private set; }

        public BattleEntity(string entityId, BattleSide side, Fp hp, long positionMilli)
        {
            EntityId = entityId;
            Side = side;
            Hp = hp;
            PositionMilli = positionMilli;
        }
    }
}
