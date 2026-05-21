using System;

namespace BattleSim.Core.State
{
    /// <summary>
    /// Inputs that bootstrap a single battle session.
    /// Pure simulation seed — no attempt_id, deck_hash, or transport metadata.
    /// </summary>
    public sealed class BattleInitialState
    {
        public string StageId { get; private set; }
        public long RngSeed { get; private set; }
        public BattleSideInitialState SideA { get; private set; }
        public BattleSideInitialState SideB { get; private set; }

        public BattleInitialState(
            string stageId,
            long rngSeed,
            BattleSideInitialState sideA,
            BattleSideInitialState sideB)
        {
            if (string.IsNullOrEmpty(stageId))
                throw new ArgumentException("stageId is required.", "stageId");
            if (sideA == null) throw new ArgumentNullException("sideA");
            if (sideB == null) throw new ArgumentNullException("sideB");
            StageId = stageId;
            RngSeed = rngSeed;
            SideA = sideA;
            SideB = sideB;
        }
    }
}
