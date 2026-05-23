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

        public long LaneWorldStartXMilli { get; private set; }
        public long LaneWorldStartYMilli { get; private set; }
        public long LaneWorldEndXMilli { get; private set; }
        public long LaneWorldEndYMilli { get; private set; }

        // Backward compatibility property
        public long LaneWorldYMilli => LaneWorldStartYMilli;

        // v0.4 Backward compatible constructor
        public LaneDefinition(string laneId, LaneType laneType, long laneLengthMilli, long laneWorldYMilli)
        {
            if (string.IsNullOrEmpty(laneId))
                throw new ArgumentException("laneId is required.", "laneId");
            if (laneLengthMilli <= 0)
                throw new ArgumentOutOfRangeException("laneLengthMilli", "LaneLengthMilli must be positive.");
            
            LaneId = laneId;
            LaneType = laneType;
            LaneLengthMilli = laneLengthMilli;
            
            LaneWorldStartXMilli = 0;
            LaneWorldStartYMilli = laneWorldYMilli;
            LaneWorldEndXMilli = laneLengthMilli;
            LaneWorldEndYMilli = laneWorldYMilli;
        }

        // v0.5 New constructor with full world coordinates support
        public LaneDefinition(
            string laneId,
            LaneType laneType,
            long laneLengthMilli,
            long laneWorldStartXMilli,
            long laneWorldStartYMilli,
            long laneWorldEndXMilli,
            long laneWorldEndYMilli)
        {
            if (string.IsNullOrEmpty(laneId))
                throw new ArgumentException("laneId is required.", "laneId");
            if (laneLengthMilli <= 0)
                throw new ArgumentOutOfRangeException("laneLengthMilli", "LaneLengthMilli must be positive.");

            LaneId = laneId;
            LaneType = laneType;
            LaneLengthMilli = laneLengthMilli;
            
            LaneWorldStartXMilli = laneWorldStartXMilli;
            LaneWorldStartYMilli = laneWorldStartYMilli;
            LaneWorldEndXMilli = laneWorldEndXMilli;
            LaneWorldEndYMilli = laneWorldEndYMilli;
        }
    }
}
