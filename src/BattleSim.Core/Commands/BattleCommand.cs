using System;
using BattleSim.Core.State;

namespace BattleSim.Core.Commands
{
    /// <summary>
    /// Single side input applied at a specific tick.
    /// Pure simulation input — no API/DB framing fields here.
    /// Both SideA and SideB use the same command types.
    /// </summary>
    /// <remarks>
    /// Lane policy:
    /// <list type="bullet">
    /// <item><see cref="BattleCommandType.SpawnDroneSquad"/> — <see cref="LaneId"/> required.</item>
    /// <item><see cref="BattleCommandType.DeployPilot"/>      — <see cref="LaneId"/> required.</item>
    /// <item><see cref="BattleCommandType.RecallPilot"/>      — keyed by <see cref="SlotIndex"/> +
    ///       <see cref="Side"/>; <see cref="LaneId"/> may be null.</item>
    /// </list>
    /// </remarks>
    public sealed class BattleCommand
    {
        public int Tick { get; private set; }
        public BattleSide Side { get; private set; }
        public int SlotIndex { get; private set; }
        public string LaneId { get; private set; }
        public BattleCommandType CommandType { get; private set; }

        private BattleCommand(int tick, BattleSide side, int slotIndex, string laneId, BattleCommandType commandType)
        {
            Tick = tick;
            Side = side;
            SlotIndex = slotIndex;
            LaneId = laneId;
            CommandType = commandType;
        }

        public static BattleCommand SpawnDroneSquad(int tick, int slotIndex, string laneId, BattleSide side)
        {
            if (string.IsNullOrEmpty(laneId))
                throw new ArgumentException("SpawnDroneSquad requires laneId.", "laneId");
            if (side == BattleSide.None)
                throw new ArgumentException("SpawnDroneSquad requires a valid side.", "side");
            return new BattleCommand(tick, side, slotIndex, laneId, BattleCommandType.SpawnDroneSquad);
        }

        public static BattleCommand DeployPilot(int tick, int slotIndex, string laneId, BattleSide side)
        {
            if (string.IsNullOrEmpty(laneId))
                throw new ArgumentException("DeployPilot requires laneId.", "laneId");
            if (side == BattleSide.None)
                throw new ArgumentException("DeployPilot requires a valid side.", "side");
            return new BattleCommand(tick, side, slotIndex, laneId, BattleCommandType.DeployPilot);
        }

        /// <summary>
        /// RecallPilot is keyed by slot + side. <paramref name="laneId"/> may be null.
        /// </summary>
        public static BattleCommand RecallPilot(int tick, int slotIndex, string laneId, BattleSide side)
        {
            if (side == BattleSide.None)
                throw new ArgumentException("RecallPilot requires a valid side.", "side");
            return new BattleCommand(tick, side, slotIndex, laneId, BattleCommandType.RecallPilot);
        }
    }
}
