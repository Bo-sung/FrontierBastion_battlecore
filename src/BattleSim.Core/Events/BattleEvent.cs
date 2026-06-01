using BattleSim.Core.FixedPoint;
using BattleSim.Core.Results;
using BattleSim.Core.Config;
using BattleSim.Core.State;
using BattleSim.Core.Commands;

namespace BattleSim.Core.Events
{
    /// <summary>
    /// Immutable payload representing a deterministic simulation event.
    /// </summary>
    public sealed class BattleEvent
    {
        public int Tick { get; }
        public int Sequence { get; }
        public BattleEventType EventType { get; }

        // Context / Identifiers (Nullable where applicable)
        public string SourceEntityId { get; }
        public string TargetEntityId { get; }
        public string EntityId { get; }
        public string ProjectileId { get; }
        public string LaneId { get; }

        // Tri-side tracking
        public BattleSide SourceSide { get; }
        public BattleSide TargetSide { get; }
        public BattleSide WinnerSide { get; }

        // Payloads
        public Fp DamageAmount { get; }
        public long PositionMilli { get; }
        public long PreviousPositionMilli { get; }
        public AttackKind AttackKind { get; }
        public BattleEndReason EndReason { get; }

        // Support tracks (v0.8)
        public BattleSupportTrack SupportTrack { get; }
        public int SupportLevel { get; }

#pragma warning disable CS8625
        public BattleEvent(
            int tick,
            int sequence,
            BattleEventType eventType,
            string sourceEntityId = null,
            string targetEntityId = null,
            string entityId = null,
            string projectileId = null,
            string laneId = null,
            BattleSide sourceSide = BattleSide.None,
            BattleSide targetSide = BattleSide.None,
            BattleSide winnerSide = BattleSide.None,
            Fp? damageAmount = null,
            long positionMilli = 0,
            long previousPositionMilli = 0,
            AttackKind attackKind = AttackKind.Melee,
            BattleEndReason endReason = BattleEndReason.None,
            BattleSupportTrack supportTrack = BattleSupportTrack.None,
            int supportLevel = 0)
#pragma warning restore CS8625
        {
            Tick = tick;
            Sequence = sequence;
            EventType = eventType;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            EntityId = entityId;
            ProjectileId = projectileId;
            LaneId = laneId;
            SourceSide = sourceSide;
            TargetSide = targetSide;
            WinnerSide = winnerSide;
            DamageAmount = damageAmount ?? Fp.Zero;
            PositionMilli = positionMilli;
            PreviousPositionMilli = previousPositionMilli;
            AttackKind = attackKind;
            EndReason = endReason;
            SupportTrack = supportTrack;
            SupportLevel = supportLevel;
        }
    }
}
