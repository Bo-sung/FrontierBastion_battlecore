using System;
using BattleSim.Core.Config;

namespace BattleSim.Core.State
{
    /// <summary>
    /// Inputs that bootstrap a single battle session.
    /// Pure simulation seed — no attempt_id, deck_hash, or transport metadata.
    /// Slot/deck selection lives here because it is player-specific per-battle
    /// data, not stage-level configuration.
    /// </summary>
    public sealed class BattleInitialState
    {
        public string StageId { get; private set; }
        public long RngSeed { get; private set; }

        /// <summary>
        /// Frozen snapshot of the player's deck for this battle.
        /// Defensive copy — callers cannot mutate the stored array.
        /// </summary>
        public SlotDefinition[] Slots { get; private set; }

        public BattleInitialState(string stageId, long rngSeed, SlotDefinition[] slots)
        {
            if (string.IsNullOrEmpty(stageId))
            {
                throw new ArgumentException("stageId is required.", "stageId");
            }
            if (slots == null || slots.Length == 0)
            {
                throw new ArgumentException("slots is required.", "slots");
            }
            StageId = stageId;
            RngSeed = rngSeed;
            Slots = (SlotDefinition[])slots.Clone();
        }
    }
}
