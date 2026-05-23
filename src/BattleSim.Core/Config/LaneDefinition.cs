using System;

namespace BattleSim.Core.Config
{
    /// <summary>
    /// Lane definition used by both config and initial state.
    /// <see cref="LaneLengthMilli"/> is the total distance in milliunits from the player
    /// base wall (position 0) to the enemy base wall (position == LaneLengthMilli).
    /// </summary>
    public sealed class LaneDefinition
    {
        public string LaneId { get; private set; }
        public LaneType LaneType { get; private set; }
        public long LaneLengthMilli { get; private set; }
        public long LaneWorldYMilli { get; private set; }

        public LaneDefinition(string laneId, LaneType laneType, long laneLengthMilli, long laneWorldYMilli)
        {
            if (string.IsNullOrEmpty(laneId))
                throw new ArgumentException("laneId is required.", "laneId");
            if (laneLengthMilli <= 0)
                throw new ArgumentOutOfRangeException("laneLengthMilli", "LaneLengthMilli must be positive.");
            LaneId = laneId;
            LaneType = laneType;
            LaneLengthMilli = laneLengthMilli;
            LaneWorldYMilli = laneWorldYMilli;
        }
    }
}
