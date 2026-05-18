using System;

namespace BattleSim.Core.Config
{
    /// <summary>
    /// Lane definition used by both config and initial state.
    /// Lanes are required for combat validation.
    /// </summary>
    public sealed class LaneDefinition
    {
        public string LaneId { get; private set; }
        public LaneType LaneType { get; private set; }

        public LaneDefinition(string laneId, LaneType laneType)
        {
            if (string.IsNullOrEmpty(laneId))
            {
                throw new ArgumentException("laneId is required.", "laneId");
            }
            LaneId = laneId;
            LaneType = laneType;
        }
    }
}
