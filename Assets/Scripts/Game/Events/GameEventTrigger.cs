using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines a typed predicate over one concrete kind of simulation result.
    /// </summary>
    [PersistableObject]
    public abstract class GameEventTrigger
    {
        /// <summary>
        /// Optional name under which the complete matched result is exposed to the event.
        /// </summary>
        [PersistableAttribute]
        public string As { get; set; }

        /// <summary>Gets the concrete simulation-result type consumed by this trigger.</summary>
        internal abstract Type ResultType { get; }

        /// <summary>Returns whether the supplied result satisfies the authored predicate.</summary>
        internal abstract bool Matches(GameResult result);

        /// <summary>Exposes the complete matched result when an authored alias is present.</summary>
        internal void Bind(GameEventEvaluationContext context, GameResult result)
        {
            if (!string.IsNullOrWhiteSpace(As))
                context.Bind(As, result);
        }

        /// <summary>Matches an optional authored instance ID against an actual value.</summary>
        protected static bool MatchesInstanceID(string expected, string actual) =>
            string.IsNullOrWhiteSpace(expected)
            || string.Equals(expected, actual, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines how an authored participant list qualifies a completed mission.
    /// </summary>
    public enum ParticipantMatch
    {
        Any,
        All,
    }

    /// <summary>
    /// Qualifies a mission result by membership in its participant collection.
    /// </summary>
    [PersistableObject(Name = "Participants")]
    public sealed class MissionParticipantFilter
    {
        /// <summary>Gets or sets whether any or all authored units must participate.</summary>
        [PersistableAttribute]
        public ParticipantMatch Match { get; set; } = ParticipantMatch.Any;

        /// <summary>Gets the authored unit identities tested against the result.</summary>
        public List<EventUnitReference> Units { get; set; } = new List<EventUnitReference>();

        /// <summary>Returns whether the completed mission contains the authored participants.</summary>
        internal bool Matches(IReadOnlyCollection<IMissionParticipant> participants)
        {
            if (Units.Count == 0)
                return true;

            HashSet<string> participantIDs = (participants ?? Array.Empty<IMissionParticipant>())
                .Where(participant => participant != null)
                .Select(participant => participant.GetInstanceID())
                .ToHashSet(StringComparer.Ordinal);
            return Match == ParticipantMatch.All
                ? Units.All(unit => participantIDs.Contains(unit.UnitInstanceID))
                : Units.Any(unit => participantIDs.Contains(unit.UnitInstanceID));
        }
    }

    /// <summary>
    /// Activates when a completed mission satisfies the authored mission filters.
    /// </summary>
    [PersistableObject(Name = "MissionCompleted")]
    public sealed class MissionCompletedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string MissionTypeID { get; set; }

        [PersistableAttribute]
        public MissionOutcome? Outcome { get; set; }

        [PersistableAttribute]
        public MissionCompletionReason? CompletionReason { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        public MissionParticipantFilter Participants { get; set; }

        internal override Type ResultType => typeof(MissionCompletedResult);

        internal override bool Matches(GameResult result)
        {
            if (result is not MissionCompletedResult completed)
                return false;
            return MatchesInstanceID(MissionTypeID, completed.MissionTypeID)
                && (!Outcome.HasValue || completed.Outcome == Outcome.Value)
                && (
                    !CompletionReason.HasValue
                    || completed.CompletionReason == CompletionReason.Value
                )
                && MatchesInstanceID(SourceEventInstanceID, completed.SourceEventInstanceID)
                && (Participants?.Matches(completed.Participants) ?? true);
        }
    }

    /// <summary>
    /// Activates when a unit-arrival result satisfies the authored identity filters.
    /// </summary>
    [PersistableObject(Name = "UnitArrived")]
    public sealed class UnitArrivedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string DestinationInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(UnitArrivedResult);

        internal override bool Matches(GameResult result)
        {
            if (result is not UnitArrivedResult arrived)
                return false;
            return MatchesInstanceID(UnitInstanceID, arrived.Unit?.InstanceID)
                && MatchesInstanceID(DestinationInstanceID, arrived.Destination?.InstanceID)
                && MatchesInstanceID(SourceEventInstanceID, arrived.SourceEventInstanceID);
        }
    }

    /// <summary>
    /// Activates when a completed duel satisfies the authored officer and source filters.
    /// </summary>
    [PersistableObject(Name = "DuelCompleted")]
    public sealed class DuelCompletedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string FirstOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string SecondOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(DuelResult);

        internal override bool Matches(GameResult result)
        {
            if (result is not DuelResult duel)
                return false;
            return MatchesInstanceID(FirstOfficerInstanceID, duel.EncounteredOfficer?.InstanceID)
                && MatchesInstanceID(SecondOfficerInstanceID, duel.OpposingOfficer?.InstanceID)
                && MatchesInstanceID(SourceEventInstanceID, duel.SourceEventInstanceID);
        }
    }
}
