using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Systems;
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

        /// <summary>Matches an optional authored source-event ID against a result.</summary>
        protected static bool MatchesSource(string expected, GameResult result) =>
            MatchesInstanceID(expected, result?.SourceEventInstanceID);
    }

    #region Planet

    /// <summary>Activates when ownership of a planet changes.</summary>
    [PersistableObject(Name = "PlanetOwnershipChanged")]
    public sealed class PlanetOwnershipChangedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PreviousOwnerFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string NewOwnerFactionInstanceID { get; set; }

        [PersistableAttribute]
        public PlanetOwnershipChangeReason? Reason { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(PlanetOwnershipChangedResult);

        internal override bool Matches(GameResult result) =>
            result is PlanetOwnershipChangedResult changed
            && MatchesInstanceID(PlanetInstanceID, changed.Planet?.InstanceID)
            && MatchesInstanceID(PreviousOwnerFactionInstanceID, changed.PreviousOwner?.InstanceID)
            && MatchesInstanceID(NewOwnerFactionInstanceID, changed.NewOwner?.InstanceID)
            && (!Reason.HasValue || changed.Reason == Reason.Value)
            && MatchesSource(SourceEventInstanceID, changed);
    }

    /// <summary>Activates when a recorded planet statistic changes.</summary>
    [PersistableObject(Name = "PlanetStatChanged")]
    public sealed class PlanetStatChangedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableAttribute]
        public PlanetChangeCategory? Category { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(PlanetStatChangedResult);

        internal override bool Matches(GameResult result) =>
            result is PlanetStatChangedResult changed
            && MatchesInstanceID(PlanetInstanceID, changed.Planet?.InstanceID)
            && MatchesInstanceID(FactionInstanceID, changed.Faction?.InstanceID)
            && (!Category.HasValue || changed.Category == Category.Value)
            && MatchesSource(SourceEventInstanceID, changed);
    }

    /// <summary>Activates when a planet's blockade state changes.</summary>
    [PersistableObject(Name = "BlockadeChanged")]
    public sealed class BlockadeChangedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public bool? IsBlockaded { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(BlockadeChangedResult);

        internal override bool Matches(GameResult result) =>
            result is BlockadeChangedResult changed
            && MatchesInstanceID(PlanetInstanceID, changed.Planet?.InstanceID)
            && (!IsBlockaded.HasValue || changed.Blockaded == IsBlockaded.Value)
            && MatchesSource(SourceEventInstanceID, changed);
    }

    /// <summary>Activates when an uprising begins on a planet.</summary>
    [PersistableObject(Name = "UprisingStarted")]
    public sealed class UprisingStartedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string InstigatorFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(PlanetUprisingStartedResult);

        internal override bool Matches(GameResult result) =>
            result is PlanetUprisingStartedResult started
            && MatchesInstanceID(PlanetInstanceID, started.Planet?.InstanceID)
            && MatchesInstanceID(InstigatorFactionInstanceID, started.InstigatorFaction?.InstanceID)
            && MatchesSource(SourceEventInstanceID, started);
    }

    /// <summary>Activates when an uprising ends on a planet.</summary>
    [PersistableObject(Name = "UprisingEnded")]
    public sealed class UprisingEndedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(PlanetUprisingEndedResult);

        internal override bool Matches(GameResult result) =>
            result is PlanetUprisingEndedResult ended
            && MatchesInstanceID(PlanetInstanceID, ended.Planet?.InstanceID)
            && MatchesInstanceID(FactionInstanceID, ended.Faction?.InstanceID)
            && MatchesSource(SourceEventInstanceID, ended);
    }

    /// <summary>Activates when a data-defined planet incident is recorded.</summary>
    [PersistableObject(Name = "PlanetIncident")]
    public sealed class PlanetIncidentTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public PlanetIncidentType? Type { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(PlanetIncidentResult);

        internal override bool Matches(GameResult result) =>
            result is PlanetIncidentResult incident
            && MatchesInstanceID(PlanetInstanceID, incident.Planet?.InstanceID)
            && (!Type.HasValue || incident.IncidentType == Type.Value)
            && MatchesSource(SourceEventInstanceID, incident);
    }

    #endregion

    #region Faction

    /// <summary>Activates when a faction advances one research discipline.</summary>
    [PersistableObject(Name = "ResearchAdvanced")]
    public sealed class ResearchAdvancedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableAttribute]
        public ResearchDiscipline? Discipline { get; set; }

        [PersistableAttribute]
        public string TechnologyTypeID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(ResearchOrderedResult);

        internal override bool Matches(GameResult result) =>
            result is ResearchOrderedResult advanced
            && MatchesInstanceID(FactionInstanceID, advanced.Faction?.InstanceID)
            && (!Discipline.HasValue || advanced.Discipline == Discipline.Value)
            && MatchesInstanceID(TechnologyTypeID, advanced.Technology?.Manufacturable?.GetTypeID())
            && MatchesSource(SourceEventInstanceID, advanced);
    }

    #endregion

    #region Mission

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

    #endregion

    #region Officer

    /// <summary>Activates when an officer's capture state changes.</summary>
    [PersistableObject(Name = "OfficerCaptureChanged")]
    public sealed class OfficerCaptureChangedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public bool? IsCaptured { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(OfficerCaptureStateResult);

        internal override bool Matches(GameResult result)
        {
            if (result is not OfficerCaptureStateResult changed)
                return false;
            string officerID = (changed.TargetOfficer ?? changed.CapturedOfficer)?.InstanceID;
            return MatchesInstanceID(OfficerInstanceID, officerID)
                && (!IsCaptured.HasValue || changed.IsCaptured == IsCaptured.Value)
                && MatchesSource(SourceEventInstanceID, changed);
        }
    }

    /// <summary>Activates when an officer is killed.</summary>
    [PersistableObject(Name = "OfficerKilled")]
    public sealed class OfficerKilledTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(OfficerKilledResult);

        internal override bool Matches(GameResult result) =>
            result is OfficerKilledResult killed
            && MatchesInstanceID(OfficerInstanceID, killed.TargetOfficer?.InstanceID)
            && MatchesSource(SourceEventInstanceID, killed);
    }

    /// <summary>Activates when an officer is injured.</summary>
    [PersistableObject(Name = "OfficerInjured")]
    public sealed class OfficerInjuredTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(OfficerInjuredResult);

        internal override bool Matches(GameResult result) =>
            result is OfficerInjuredResult injured
            && MatchesInstanceID(OfficerInstanceID, injured.Officer?.InstanceID)
            && MatchesSource(SourceEventInstanceID, injured);
    }

    /// <summary>Activates when an officer recruitment result is produced.</summary>
    [PersistableObject(Name = "OfficerRecruited")]
    public sealed class OfficerRecruitedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(OfficerRecruitedResult);

        internal override bool Matches(GameResult result) =>
            result is OfficerRecruitedResult recruited
            && MatchesInstanceID(OfficerInstanceID, recruited.Officer?.InstanceID)
            && MatchesInstanceID(FactionInstanceID, recruited.Faction?.InstanceID)
            && MatchesInstanceID(PlanetInstanceID, recruited.Planet?.InstanceID)
            && MatchesSource(SourceEventInstanceID, recruited);
    }

    #endregion

    #region Unit Lifecycle

    /// <summary>Activates when ownership of a unit changes.</summary>
    [PersistableObject(Name = "UnitOwnershipChanged")]
    public sealed class UnitOwnershipChangedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string PreviousOwnerFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string NewOwnerFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(UnitOwnershipChangedResult);

        internal override bool Matches(GameResult result) =>
            result is UnitOwnershipChangedResult changed
            && MatchesInstanceID(UnitInstanceID, changed.Unit?.InstanceID)
            && MatchesInstanceID(PreviousOwnerFactionInstanceID, changed.PreviousOwner?.InstanceID)
            && MatchesInstanceID(NewOwnerFactionInstanceID, changed.NewOwner?.InstanceID)
            && MatchesSource(SourceEventInstanceID, changed);
    }

    /// <summary>Activates when a game unit is created.</summary>
    [PersistableObject(Name = "UnitCreated")]
    public sealed class UnitCreatedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(GameObjectCreatedResult);

        internal override bool Matches(GameResult result) =>
            result is GameObjectCreatedResult created
            && MatchesInstanceID(UnitInstanceID, created.GameObject?.InstanceID)
            && MatchesSource(SourceEventInstanceID, created);
    }

    /// <summary>Activates when a game unit is destroyed.</summary>
    [PersistableObject(Name = "UnitDestroyed")]
    public sealed class UnitDestroyedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(GameObjectDestroyedResult);

        internal override bool Matches(GameResult result) =>
            result is GameObjectDestroyedResult destroyed
            && MatchesInstanceID(UnitInstanceID, destroyed.DestroyedObject?.InstanceID)
            && MatchesSource(SourceEventInstanceID, destroyed);
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

    #endregion

    #region Combat

    /// <summary>Activates when a space battle is resolved.</summary>
    [PersistableObject(Name = "SpaceCombatCompleted")]
    public sealed class SpaceCombatCompletedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string AttackerFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string DefenderFactionInstanceID { get; set; }

        [PersistableAttribute]
        public CombatSide? Winner { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(SpaceCombatResult);

        internal override bool Matches(GameResult result) =>
            result is SpaceCombatResult combat
            && MatchesInstanceID(PlanetInstanceID, combat.Planet?.InstanceID)
            && MatchesInstanceID(AttackerFactionInstanceID, combat.AttackerOwnerInstanceID)
            && MatchesInstanceID(DefenderFactionInstanceID, combat.DefenderOwnerInstanceID)
            && (!Winner.HasValue || combat.Winner == Winner.Value)
            && MatchesSource(SourceEventInstanceID, combat);
    }

    /// <summary>Activates when orbital bombardment is resolved.</summary>
    [PersistableObject(Name = "BombardmentCompleted")]
    public sealed class BombardmentCompletedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string AttackerFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string DefenderFactionInstanceID { get; set; }

        [PersistableAttribute]
        public BombardmentType? Type { get; set; }

        [PersistableAttribute]
        public bool? PlanetDestroyed { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(BombardmentResult);

        internal override bool Matches(GameResult result) =>
            result is BombardmentResult bombardment
            && MatchesInstanceID(PlanetInstanceID, bombardment.Planet?.InstanceID)
            && MatchesInstanceID(AttackerFactionInstanceID, bombardment.AttackerOwnerInstanceID)
            && MatchesInstanceID(DefenderFactionInstanceID, bombardment.DefenderOwnerInstanceID)
            && (!Type.HasValue || bombardment.Type == Type.Value)
            && (!PlanetDestroyed.HasValue || bombardment.PlanetDestroyed == PlanetDestroyed.Value)
            && MatchesSource(SourceEventInstanceID, bombardment);
    }

    /// <summary>Activates when a planetary assault is resolved.</summary>
    [PersistableObject(Name = "PlanetaryAssaultCompleted")]
    public sealed class PlanetaryAssaultCompletedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string AttackerFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string DefenderFactionInstanceID { get; set; }

        [PersistableAttribute]
        public bool? Success { get; set; }

        [PersistableAttribute]
        public bool? BlockedByShields { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(PlanetaryAssaultResult);

        internal override bool Matches(GameResult result) =>
            result is PlanetaryAssaultResult assault
            && MatchesInstanceID(PlanetInstanceID, assault.Planet?.InstanceID)
            && MatchesInstanceID(AttackerFactionInstanceID, assault.AttackerOwnerInstanceID)
            && MatchesInstanceID(DefenderFactionInstanceID, assault.DefenderOwnerInstanceID)
            && (!Success.HasValue || assault.Success == Success.Value)
            && (!BlockedByShields.HasValue || assault.BlockedByShields == BlockedByShields.Value)
            && MatchesSource(SourceEventInstanceID, assault);
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

    #endregion

    #region Manufacturing

    /// <summary>Activates when a manufactured unit is deployed.</summary>
    [PersistableObject(Name = "ManufacturingCompleted")]
    public sealed class ManufacturingCompletedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string LocationInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(ManufacturingDeployedResult);

        internal override bool Matches(GameResult result) =>
            result is ManufacturingDeployedResult completed
            && MatchesInstanceID(FactionInstanceID, completed.Faction?.InstanceID)
            && MatchesInstanceID(UnitInstanceID, completed.DeployedObject?.InstanceID)
            && MatchesInstanceID(LocationInstanceID, completed.Location?.InstanceID)
            && MatchesSource(SourceEventInstanceID, completed);
    }

    #endregion
}
