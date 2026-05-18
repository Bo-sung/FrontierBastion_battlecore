using System;
using BattleSim.Core.FixedPoint;

namespace BattleSim.Core.Config
{
    /// <summary>
    /// One enemy unit spawned at a specific tick and lane.
    /// Stats live here directly for v0.2 simplicity; a proper enemy catalogue
    /// referencing IDs is left for a future phase.
    /// </summary>
    public sealed class EnemySpawnDefinition
    {
        public int SpawnTick { get; private set; }
        public string LaneId { get; private set; }
        public Fp Hp { get; private set; }
        public Fp Attack { get; private set; }
        public long RangeMilli { get; private set; }
        public long SpeedMilliPerTick { get; private set; }

        public EnemySpawnDefinition(
            int spawnTick,
            string laneId,
            Fp hp,
            Fp attack,
            long rangeMilli,
            long speedMilliPerTick)
        {
            if (string.IsNullOrEmpty(laneId))
                throw new ArgumentException("laneId is required.", "laneId");
            if (spawnTick < 0)
                throw new ArgumentOutOfRangeException("spawnTick");
            SpawnTick = spawnTick;
            LaneId = laneId;
            Hp = hp;
            Attack = attack;
            RangeMilli = rangeMilli;
            SpeedMilliPerTick = speedMilliPerTick;
        }
    }
}
