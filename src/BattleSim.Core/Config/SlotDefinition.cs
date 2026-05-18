using System;
using BattleSim.Core.FixedPoint;

namespace BattleSim.Core.Config
{
    /// <summary>
    /// Per-deck-slot config snapshot consumed by the simulation.
    /// Excludes server-only metadata (deck_hash, attempt_id, reward).
    /// </summary>
    public sealed class SlotDefinition
    {
        public int SlotIndex { get; private set; }
        public string PilotId { get; private set; }
        public string DroneSquadId { get; private set; }
        public Fp EnergyCost { get; private set; }
        public int CooldownTick { get; private set; }

        public SlotDefinition(int slotIndex, string pilotId, string droneSquadId, Fp energyCost, int cooldownTick)
        {
            if (slotIndex < 0)
            {
                throw new ArgumentOutOfRangeException("slotIndex");
            }
            SlotIndex = slotIndex;
            PilotId = pilotId;
            DroneSquadId = droneSquadId;
            EnergyCost = energyCost;
            CooldownTick = cooldownTick;
        }
    }
}
