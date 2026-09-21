using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
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
        // Trigger Bindings.
        public List<GameEventBinding> Bindings { get; set; } = new List<GameEventBinding>();
    }

    #region Planet

    /// <summary>
    /// Activates when ownership of a planet changes.
    /// </summary>
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
    }

    /// <summary>
    /// Activates when a recorded planet statistic changes.
    /// </summary>
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
    }

    /// <summary>
    /// Activates when a planet's blockade state changes.
    /// </summary>
    [PersistableObject(Name = "BlockadeChanged")]
    public sealed class BlockadeChangedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public bool? IsBlockaded { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
    }

    /// <summary>
    /// Activates when an uprising begins on a planet.
    /// </summary>
    [PersistableObject(Name = "UprisingStarted")]
    public sealed class UprisingStartedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string InstigatorFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
    }

    /// <summary>
    /// Activates when an uprising ends on a planet.
    /// </summary>
    [PersistableObject(Name = "UprisingEnded")]
    public sealed class UprisingEndedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
    }

    /// <summary>
    /// Activates when intelligence is revealed to a faction.
    /// </summary>
    [PersistableObject(Name = "IntelligenceRevealed")]
    public sealed class IntelligenceRevealedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string RecipientFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string ObservationInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
    }

    /// <summary>
    /// Activates when a faction cannot meet a maintenance obligation.
    /// </summary>
    [PersistableObject(Name = "MaintenanceRequired")]
    public sealed class MaintenanceRequiredTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
    }

    #endregion

    #region Faction

    /// <summary>
    /// Activates when a faction advances one research discipline.
    /// </summary>
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
        // Participant Matching.
        [PersistableAttribute]
        public ParticipantMatch Match { get; set; } = ParticipantMatch.Any;

        public List<EventUnitReference> Units { get; set; } = new List<EventUnitReference>();
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
    }

    #endregion

    #region Officer

    /// <summary>
    /// Activates when an officer's capture state changes.
    /// </summary>
    [PersistableObject(Name = "OfficerCaptureChanged")]
    public sealed class OfficerCaptureChangedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public bool? IsCaptured { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
    }

    /// <summary>
    /// Activates when an officer is killed.
    /// </summary>
    [PersistableObject(Name = "OfficerKilled")]
    public sealed class OfficerKilledTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
    }

    /// <summary>
    /// Activates when an officer is injured.
    /// </summary>
    [PersistableObject(Name = "OfficerInjured")]
    public sealed class OfficerInjuredTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
    }

    /// <summary>
    /// Activates when an officer recruitment result is produced.
    /// </summary>
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
    }

    /// <summary>
    /// Activates when an officer's Force discovery state changes.
    /// </summary>
    [PersistableObject(Name = "ForceDiscoveryChanged")]
    public sealed class ForceDiscoveryChangedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string DiscovererInstanceID { get; set; }

        [PersistableAttribute]
        public ForceEventType? EventType { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
    }

    #endregion

    #region Unit Lifecycle

    /// <summary>
    /// Activates when ownership of a unit changes.
    /// </summary>
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
    }

    /// <summary>
    /// Activates when a game unit is created.
    /// </summary>
    [PersistableObject(Name = "UnitCreated")]
    public sealed class UnitCreatedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
    }

    /// <summary>
    /// Activates when a game unit is destroyed.
    /// </summary>
    [PersistableObject(Name = "UnitDestroyed")]
    public sealed class UnitDestroyedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public UnitDestructionReason? Reason { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }
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
    }

    #endregion

    #region Combat

    /// <summary>
    /// Activates when a space battle is resolved.
    /// </summary>
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
    }

    /// <summary>
    /// Activates when orbital bombardment is resolved.
    /// </summary>
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
    }

    /// <summary>
    /// Activates when a planetary assault is resolved.
    /// </summary>
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
    }

    #endregion

    #region Manufacturing

    /// <summary>
    /// Activates when a manufactured unit is deployed.
    /// </summary>
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
    }

    #endregion
}
