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
        public BattleSupportTrack SupportTrack { get; private set; }

        private BattleCommand(int tick, BattleSide side, int slotIndex, string laneId, BattleCommandType commandType, BattleSupportTrack supportTrack)
        {
            Tick = tick;
            Side = side;
            SlotIndex = slotIndex;
            LaneId = laneId;
            CommandType = commandType;
            SupportTrack = supportTrack;
        }

        public static BattleCommand SpawnDroneSquad(int tick, int slotIndex, string laneId, BattleSide side)
        {
            if (string.IsNullOrEmpty(laneId))
                throw new ArgumentException("SpawnDroneSquad requires laneId.", "laneId");
            if (side == BattleSide.None)
                throw new ArgumentException("SpawnDroneSquad requires a valid side.", "side");
            return new BattleCommand(tick, side, slotIndex, laneId, BattleCommandType.SpawnDroneSquad, BattleSupportTrack.None);
        }

        public static BattleCommand DeployPilot(int tick, int slotIndex, string laneId, BattleSide side)
        {
            if (string.IsNullOrEmpty(laneId))
                throw new ArgumentException("DeployPilot requires laneId.", "laneId");
            if (side == BattleSide.None)
                throw new ArgumentException("DeployPilot requires a valid side.", "side");
            return new BattleCommand(tick, side, slotIndex, laneId, BattleCommandType.DeployPilot, BattleSupportTrack.None);
        }

        /// <summary>
        /// RecallPilot is keyed by slot + side. <paramref name="laneId"/> may be null.
        /// </summary>
        public static BattleCommand RecallPilot(int tick, int slotIndex, string laneId, BattleSide side)
        {
            if (side == BattleSide.None)
                throw new ArgumentException("RecallPilot requires a valid side.", "side");
            return new BattleCommand(tick, side, slotIndex, laneId, BattleCommandType.RecallPilot, BattleSupportTrack.None);
        }

        public static BattleCommand StartSupportUpgrade(int tick, BattleSide side, BattleSupportTrack supportTrack)
        {
            if (side == BattleSide.None)
                throw new ArgumentException("StartSupportUpgrade requires a valid side.", "side");
            if (supportTrack != BattleSupportTrack.Resource && supportTrack != BattleSupportTrack.Pilot)
                throw new ArgumentException("StartSupportUpgrade requires a valid supportTrack (Resource or Pilot).", "supportTrack");
#pragma warning disable CS8625
            return new BattleCommand(tick, side, -1, null, BattleCommandType.StartSupportUpgrade, supportTrack);
#pragma warning restore CS8625
        }
    }
}
