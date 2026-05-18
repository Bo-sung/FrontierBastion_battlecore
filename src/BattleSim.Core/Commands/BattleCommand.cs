using System;

namespace BattleSim.Core.Commands
{
    /// <summary>
    /// Single player input applied at a specific tick.
    /// Pure simulation input — no API/DB framing fields here.
    /// </summary>
    /// <remarks>
    /// Lane policy:
    /// <list type="bullet">
    /// <item><see cref="BattleCommandType.SpawnDroneSquad"/> — <see cref="LaneId"/> required.</item>
    /// <item><see cref="BattleCommandType.DeployPilot"/>      — <see cref="LaneId"/> required.</item>
    /// <item><see cref="BattleCommandType.RecallPilot"/>      — keyed by <see cref="SlotIndex"/>;
    ///       whether <see cref="LaneId"/> must be provided is a PendingDecision
    ///       (see handoff: PD-A).</item>
    /// </list>
    /// </remarks>
    public sealed class BattleCommand
    {
        public int Tick { get; private set; }
        public int SlotIndex { get; private set; }
        public string LaneId { get; private set; }
        public BattleCommandType CommandType { get; private set; }

        private BattleCommand(int tick, int slotIndex, string laneId, BattleCommandType commandType)
        {
            Tick = tick;
            SlotIndex = slotIndex;
            LaneId = laneId;
            CommandType = commandType;
        }

        public static BattleCommand SpawnDroneSquad(int tick, int slotIndex, string laneId)
        {
            if (string.IsNullOrEmpty(laneId))
            {
                throw new ArgumentException("SpawnDroneSquad requires laneId.", "laneId");
            }
            return new BattleCommand(tick, slotIndex, laneId, BattleCommandType.SpawnDroneSquad);
        }

        public static BattleCommand DeployPilot(int tick, int slotIndex, string laneId)
        {
            if (string.IsNullOrEmpty(laneId))
            {
                throw new ArgumentException("DeployPilot requires laneId.", "laneId");
            }
            return new BattleCommand(tick, slotIndex, laneId, BattleCommandType.DeployPilot);
        }

        /// <summary>
        /// RecallPilot is keyed by slot. <paramref name="laneId"/> may be null
        /// until the lane-requirement PendingDecision is resolved.
        /// </summary>
        public static BattleCommand RecallPilot(int tick, int slotIndex, string laneId)
        {
            return new BattleCommand(tick, slotIndex, laneId, BattleCommandType.RecallPilot);
        }
    }
}
