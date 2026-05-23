using System;
using BattleSim.Core.FixedPoint;

namespace BattleSim.Core.Config
{
    /// <summary>
    /// Per-deck-slot config snapshot consumed by the simulation.
    /// Includes drone and pilot entity stats needed for deterministic combat.
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
        public Fp DroneDefense { get; private set; }
        public long DroneRangeMilli { get; private set; }
        public long DroneSpeedMilliPerTick { get; private set; }
        public int DroneAttackPeriodTick { get; private set; }

        // Pilot entity stats.
        public Fp PilotHp { get; private set; }
        public Fp PilotAttack { get; private set; }
        public Fp PilotDefense { get; private set; }
        public long PilotRangeMilli { get; private set; }
        public long PilotSpeedMilliPerTick { get; private set; }
        public int PilotAttackPeriodTick { get; private set; }

        public SlotDefinition(
            int slotIndex,
            string pilotId,
            string droneSquadId,
            Fp energyCost,
            int cooldownTick,
            Fp droneHp,
            Fp droneAttack,
            Fp droneDefense,
            long droneRangeMilli,
            long droneSpeedMilliPerTick,
            int droneAttackPeriodTick,
            Fp pilotHp,
            Fp pilotAttack,
            Fp pilotDefense,
            long pilotRangeMilli,
            long pilotSpeedMilliPerTick,
            int pilotAttackPeriodTick)
        {
            if (slotIndex < 0)
                throw new ArgumentOutOfRangeException("slotIndex");
            if (droneAttackPeriodTick < 1)
                throw new ArgumentOutOfRangeException("droneAttackPeriodTick", "Drone attack period tick must be >= 1.");
            if (pilotAttackPeriodTick < 1)
                throw new ArgumentOutOfRangeException("pilotAttackPeriodTick", "Pilot attack period tick must be >= 1.");

            SlotIndex = slotIndex;
            PilotId = pilotId;
            DroneSquadId = droneSquadId;
            EnergyCost = energyCost;
            CooldownTick = cooldownTick;
            DroneHp = droneHp;
            DroneAttack = droneAttack;
            DroneDefense = droneDefense;
            DroneRangeMilli = droneRangeMilli;
            DroneSpeedMilliPerTick = droneSpeedMilliPerTick;
            DroneAttackPeriodTick = droneAttackPeriodTick;
            PilotHp = pilotHp;
            PilotAttack = pilotAttack;
            PilotDefense = pilotDefense;
            PilotRangeMilli = pilotRangeMilli;
            PilotSpeedMilliPerTick = pilotSpeedMilliPerTick;
            PilotAttackPeriodTick = pilotAttackPeriodTick;
        }
    }
}
