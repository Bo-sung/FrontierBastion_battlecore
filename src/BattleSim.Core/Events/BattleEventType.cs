namespace BattleSim.Core.Events
{
    /// <summary>
    /// Types of deterministic combat events emitted by the simulation.
    /// </summary>
    public enum BattleEventType
    {
        EntitySpawned = 1,
        AttackStarted = 2,
        ProjectileSpawned = 3,
        ProjectileHit = 4,
        ProjectileMiss = 5,
        DamageApplied = 6,
        KnockbackApplied = 7,
        EntityDied = 8,
        BaseDamaged = 9,
        BattleEnded = 10,
        EntityRemoved = 11
    }
}
