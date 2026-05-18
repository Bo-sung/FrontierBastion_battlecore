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

        public BattleInitialState(string stageId, long rngSeed)
        {
            if (string.IsNullOrEmpty(stageId))
            {
                throw new ArgumentException("stageId is required.", "stageId");
            }
            StageId = stageId;
            RngSeed = rngSeed;
        }
    }
}
