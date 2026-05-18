using System;
using BattleSim.Core.FixedPoint;

namespace BattleSim.Core.Config
{
    /// <summary>
    /// Per-deck-slot config snapshot consumed by the simulation.
    /// Includes minimal drone and pilot entity stats needed for v0.2 combat.
    /// Excludes server-only metadata (deck_hash, attempt_id, reward).
    /// </summary>
    public sealed class SlotDefinition
    {
        public int SlotIndex { get; private set; }
        public string PilotId { get; private set; }
        public string DroneSquadId { get; private set; }
        public Fp EnergyCost { get; private set; }
        public int CooldownTick { get; private set; }

        // Drone entity stats.
        public Fp DroneHp { get; private set; }
        public Fp DroneAttack { get; private set; }
        public long DroneRangeMilli { get; private set; }
        public long DroneSpeedMilliPerTick { get; private set; }

        // Pilot entity stats.
        public Fp PilotHp { get; private set; }
        public Fp PilotAttack { get; private set; }
        public long PilotRangeMilli { get; private set; }
        public long PilotSpeedMilliPerTick { get; private set; }

        public SlotDefinition(
            int slotIndex,
            string pilotId,
            string droneSquadId,
            Fp energyCost,
            int cooldownTick,
            Fp droneHp,
            Fp droneAttack,
            long droneRangeMilli,
            long droneSpeedMilliPerTick,
            Fp pilotHp,
            Fp pilotAttack,
            long pilotRangeMilli,
            long pilotSpeedMilliPerTick)
        {
            if (slotIndex < 0)
                throw new ArgumentOutOfRangeException("slotIndex");
            SlotIndex = slotIndex;
            PilotId = pilotId;
            DroneSquadId = droneSquadId;
            EnergyCost = energyCost;
            CooldownTick = cooldownTick;
            DroneHp = droneHp;
            DroneAttack = droneAttack;
            DroneRangeMilli = droneRangeMilli;
            DroneSpeedMilliPerTick = droneSpeedMilliPerTick;
            PilotHp = pilotHp;
            PilotAttack = pilotAttack;
            PilotRangeMilli = pilotRangeMilli;
            PilotSpeedMilliPerTick = pilotSpeedMilliPerTick;
        }
    }
}
