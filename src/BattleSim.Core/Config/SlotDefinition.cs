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
        public AttackKind DroneAttackKind { get; private set; }
        public long DroneProjectileSpeedMilliPerTick { get; private set; }

        // Pilot entity stats.
        public Fp PilotHp { get; private set; }
        public Fp PilotAttack { get; private set; }
        public Fp PilotDefense { get; private set; }
        public long PilotRangeMilli { get; private set; }
        public long PilotSpeedMilliPerTick { get; private set; }
        public int PilotAttackPeriodTick { get; private set; }
        public AttackKind PilotAttackKind { get; private set; }
        public long PilotProjectileSpeedMilliPerTick { get; private set; }

        // v0.4 Backward compatible constructor
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
            DroneAttackKind = AttackKind.Melee;
            DroneProjectileSpeedMilliPerTick = 0;

            PilotHp = pilotHp;
            PilotAttack = pilotAttack;
            PilotDefense = pilotDefense;
            PilotRangeMilli = pilotRangeMilli;
            PilotSpeedMilliPerTick = pilotSpeedMilliPerTick;
            PilotAttackPeriodTick = pilotAttackPeriodTick;
            PilotAttackKind = AttackKind.Melee;
            PilotProjectileSpeedMilliPerTick = 0;
        }

        // v0.5 New constructor with projectile support
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
            AttackKind droneAttackKind,
            long droneProjectileSpeedMilliPerTick,
            Fp pilotHp,
            Fp pilotAttack,
            Fp pilotDefense,
            long pilotRangeMilli,
            long pilotSpeedMilliPerTick,
            int pilotAttackPeriodTick,
            AttackKind pilotAttackKind,
            long pilotProjectileSpeedMilliPerTick)
        {
            if (slotIndex < 0)
                throw new ArgumentOutOfRangeException("slotIndex");
            if (droneAttackPeriodTick < 1)
                throw new ArgumentOutOfRangeException("droneAttackPeriodTick", "Drone attack period tick must be >= 1.");
            if (pilotAttackPeriodTick < 1)
                throw new ArgumentOutOfRangeException("pilotAttackPeriodTick", "Pilot attack period tick must be >= 1.");

            if (!Enum.IsDefined(typeof(AttackKind), droneAttackKind))
                throw new ArgumentOutOfRangeException("droneAttackKind", "Invalid drone AttackKind.");
            if (!Enum.IsDefined(typeof(AttackKind), pilotAttackKind))
                throw new ArgumentOutOfRangeException("pilotAttackKind", "Invalid pilot AttackKind.");

            if (droneAttackKind == AttackKind.Projectile && droneProjectileSpeedMilliPerTick <= 0)
                throw new ArgumentOutOfRangeException("droneProjectileSpeedMilliPerTick", "Drone projectile speed must be positive for Projectile attacks.");
            if (droneAttackKind == AttackKind.Melee && droneProjectileSpeedMilliPerTick < 0)
                throw new ArgumentOutOfRangeException("droneProjectileSpeedMilliPerTick", "Drone projectile speed must be non-negative.");

            if (pilotAttackKind == AttackKind.Projectile && pilotProjectileSpeedMilliPerTick <= 0)
                throw new ArgumentOutOfRangeException("pilotProjectileSpeedMilliPerTick", "Pilot projectile speed must be positive for Projectile attacks.");
            if (pilotAttackKind == AttackKind.Melee && pilotProjectileSpeedMilliPerTick < 0)
                throw new ArgumentOutOfRangeException("pilotProjectileSpeedMilliPerTick", "Pilot projectile speed must be non-negative.");

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
            DroneAttackKind = droneAttackKind;
            DroneProjectileSpeedMilliPerTick = droneProjectileSpeedMilliPerTick;

            PilotHp = pilotHp;
            PilotAttack = pilotAttack;
            PilotDefense = pilotDefense;
            PilotRangeMilli = pilotRangeMilli;
            PilotSpeedMilliPerTick = pilotSpeedMilliPerTick;
            PilotAttackPeriodTick = pilotAttackPeriodTick;
            PilotAttackKind = pilotAttackKind;
            PilotProjectileSpeedMilliPerTick = pilotProjectileSpeedMilliPerTick;
        }
    }
}
