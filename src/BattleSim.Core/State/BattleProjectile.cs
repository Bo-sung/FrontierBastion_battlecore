using BattleSim.Core.FixedPoint;

namespace BattleSim.Core.State
{
    /// <summary>
    /// Immutable snapshot of a runtime projectile.
    /// Returned from <see cref="BattleState.Projectiles"/>.
    /// </summary>
    public sealed class BattleProjectile
    {
        public string ProjectileId { get; private set; }
        public BattleSide Side { get; private set; }
        public string SourceLaneId { get; private set; }
        public string ProjectileLaneId { get; private set; }
        public string TargetLaneId { get; private set; }
        public string TargetEntityId { get; private set; }
        public long SourceProgressMilli { get; private set; }
        public long PositionMilli { get; private set; }
        public long ImpactProgressMilli { get; private set; }
        public Fp Damage { get; private set; }
        public int RemainingTtlTick { get; private set; }

        public BattleProjectile(
            string projectileId,
            BattleSide side,
            string sourceLaneId,
            string projectileLaneId,
            string targetLaneId,
            string targetEntityId,
            long sourceProgressMilli,
            long positionMilli,
            long impactProgressMilli,
            Fp damage,
            int remainingTtlTick)
        {
            ProjectileId = projectileId;
            Side = side;
            SourceLaneId = sourceLaneId;
            ProjectileLaneId = projectileLaneId;
            TargetLaneId = targetLaneId;
            TargetEntityId = targetEntityId;
            SourceProgressMilli = sourceProgressMilli;
            PositionMilli = positionMilli;
            ImpactProgressMilli = impactProgressMilli;
            Damage = damage;
            RemainingTtlTick = remainingTtlTick;
        }
    }
}
