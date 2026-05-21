using System;
using BattleSim.Core.Config;

namespace BattleSim.Core.State
{
    /// <summary>
    /// Per-side slot/deck selection for one battle session.
    /// </summary>
    public sealed class BattleSideInitialState
    {
        public BattleSide Side { get; private set; }

        /// <summary>Frozen snapshot of this side's deck. Defensive copy — callers cannot mutate.</summary>
        public SlotDefinition[] Slots { get; private set; }

        public BattleSideInitialState(BattleSide side, SlotDefinition[] slots)
        {
            if (side == BattleSide.None)
                throw new ArgumentException("side must be SideA or SideB.", "side");
            if (slots == null || slots.Length == 0)
                throw new ArgumentException("slots is required.", "slots");
            Side = side;
            Slots = (SlotDefinition[])slots.Clone();
        }
    }
}
