using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
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
        /// <summary>Gets or sets the trigger arguments explicitly exposed to the event.</summary>
        public List<GameEventBinding> Bindings { get; set; } = new List<GameEventBinding>();

        /// <summary>Gets the concrete simulation-result type consumed by this trigger.</summary>
        internal abstract Type ResultType { get; }

        /// <summary>Returns whether the supplied result satisfies the authored predicate.</summary>
        internal abstract bool Matches(GameResult result);

        /// <summary>Exposes the explicitly authored result arguments under their binding names.</summary>
        internal void Bind(GameEventEvaluationContext context, GameResult result)
        {
            foreach (GameEventBinding binding in Bindings)
            {
                GameEventTriggerArgument argument = GameEventTriggerArguments.Get(
                    ResultType,
                    binding.Argument
                );
                context.Bind(binding.As, argument.Resolve(result));
            }
        }

        /// <summary>Gets the declared value type for one authored trigger argument.</summary>
        internal Type GetBindingType(string argument) =>
            GameEventTriggerArguments.Get(ResultType, argument).ValueType;

        /// <summary>Matches an optional authored instance ID against an actual value.</summary>
        protected static bool MatchesInstanceID(string expected, string actual) =>
            string.IsNullOrWhiteSpace(expected)
            || string.Equals(expected, actual, StringComparison.Ordinal);

        /// <summary>Matches an optional authored source-event ID against a result.</summary>
        protected static bool MatchesSource(string expected, GameResult result) =>
            MatchesInstanceID(expected, result?.SourceEventInstanceID);
    }

    /// <summary>
    /// Describes one stable value that a trigger may expose without reflecting over result objects.
    /// </summary>
    internal sealed class GameEventTriggerArgument
    {
        private readonly Func<GameResult, object> _resolve;

        internal Type ValueType { get; }

        private GameEventTriggerArgument(Type valueType, Func<GameResult, object> resolve)
        {
            ValueType = valueType;
            _resolve = resolve;
        }

        /// <summary>Resolves the argument value from the matched result.</summary>
        internal object Resolve(GameResult result) => _resolve(result);

        /// <summary>Creates a strongly typed trigger-argument accessor.</summary>
        internal static GameEventTriggerArgument Create<TResult, TValue>(
            Func<TResult, TValue> resolve
        )
            where TResult : GameResult =>
            new GameEventTriggerArgument(typeof(TValue), result => resolve((TResult)result));
    }

    /// <summary>
    /// Defines the stable arguments exposed by each supported trigger-result contract.
    /// </summary>
    internal static class GameEventTriggerArguments
    {
        private static readonly IReadOnlyDictionary<(Type, string), GameEventTriggerArgument> _all =
            Build();

        /// <summary>Gets one declared argument or rejects the unsupported authoring request.</summary>
        internal static GameEventTriggerArgument Get(Type resultType, string argument)
        {
            if (
                string.IsNullOrWhiteSpace(argument)
                || !_all.TryGetValue((resultType, argument), out GameEventTriggerArgument value)
            )
                throw new InvalidOperationException(
                    $"Trigger result '{resultType?.Name}' does not expose argument '{argument}'."
                );
            return value;
        }

        /// <summary>Builds the explicit result-to-argument contract used by event bindings.</summary>
        private static IReadOnlyDictionary<(Type, string), GameEventTriggerArgument> Build()
        {
            Dictionary<(Type, string), GameEventTriggerArgument> arguments = new();
            Add<PlanetOwnershipChangedResult, Planet>(arguments, "Planet", result => result.Planet);
            Add<PlanetOwnershipChangedResult, Faction>(
                arguments,
                "PreviousOwner",
                result => result.PreviousOwner
            );
            Add<PlanetOwnershipChangedResult, Faction>(
                arguments,
                "NewOwner",
                result => result.NewOwner
            );
            Add<PlanetOwnershipChangedResult, PlanetOwnershipChangeReason>(
                arguments,
                "Reason",
                result => result.Reason
            );
            Add<PlanetStatChangedResult, Planet>(arguments, "Planet", result => result.Planet);
            Add<PlanetStatChangedResult, Faction>(arguments, "Faction", result => result.Faction);
            Add<PlanetStatChangedResult, PlanetChangeCategory>(
                arguments,
                "Category",
                result => result.Category
            );
            Add<PlanetStatChangedResult, int>(
                arguments,
                "PreviousValue",
                result => result.OldValue
            );
            Add<PlanetStatChangedResult, int>(arguments, "CurrentValue", result => result.NewValue);
            Add<BlockadeChangedResult, Planet>(arguments, "Planet", result => result.Planet);
            Add<BlockadeChangedResult, Fleet>(
                arguments,
                "BlockadingFleet",
                result => result.BlockadingFleet
            );
            Add<BlockadeChangedResult, bool>(arguments, "IsBlockaded", result => result.Blockaded);
            Add<PlanetUprisingStartedResult, Planet>(arguments, "Planet", result => result.Planet);
            Add<PlanetUprisingStartedResult, Faction>(
                arguments,
                "InstigatorFaction",
                result => result.InstigatorFaction
            );
            Add<PlanetUprisingEndedResult, Planet>(arguments, "Planet", result => result.Planet);
            Add<PlanetUprisingEndedResult, Faction>(arguments, "Faction", result => result.Faction);
            Add<IntelligenceRevealedResult, Faction>(
                arguments,
                "Recipient",
                result => result.Recipient
            );
            Add<IntelligenceRevealedResult, List<ISceneNode>>(
                arguments,
                "Observations",
                result => result.Observations
            );
            Add<MaintenanceRequiredResult, Faction>(arguments, "Faction", result => result.Faction);
            Add<MaintenanceRequiredResult, int>(arguments, "Amount", result => result.Amount);
            Add<ResearchOrderedResult, Faction>(arguments, "Faction", result => result.Faction);
            Add<ResearchOrderedResult, ResearchDiscipline>(
                arguments,
                "Discipline",
                result => result.Discipline
            );
            Add<ResearchOrderedResult, int>(
                arguments,
                "ResearchOrder",
                result => result.ResearchOrder
            );
            Add<ResearchOrderedResult, int>(arguments, "Capacity", result => result.Capacity);
            Add<ResearchOrderedResult, Technology>(
                arguments,
                "Technology",
                result => result.Technology
            );
            Add<MissionCompletedResult, Mission>(arguments, "Mission", result => result.Mission);
            Add<MissionCompletedResult, string>(
                arguments,
                "MissionName",
                result => result.MissionName
            );
            Add<MissionCompletedResult, string>(
                arguments,
                "MissionTypeID",
                result => result.MissionTypeID
            );
            Add<MissionCompletedResult, string>(
                arguments,
                "TargetName",
                result => result.TargetName
            );
            Add<MissionCompletedResult, Planet>(arguments, "Location", result => result.Location);
            Add<MissionCompletedResult, ContainerNode>(
                arguments,
                "ReturnDestination",
                result => result.ReturnDestination
            );
            Add<MissionCompletedResult, List<IMissionParticipant>>(
                arguments,
                "Participants",
                result => result.Participants
            );
            Add<MissionCompletedResult, MissionOutcome>(
                arguments,
                "Outcome",
                result => result.Outcome
            );
            Add<MissionCompletedResult, MissionCompletionReason>(
                arguments,
                "CompletionReason",
                result => result.CompletionReason
            );
            Add<MissionCompletedResult, bool>(
                arguments,
                "CanContinue",
                result => result.CanContinue
            );
            Add<OfficerCaptureStateResult, Officer>(
                arguments,
                "Officer",
                result => result.TargetOfficer
            );
            Add<OfficerCaptureStateResult, bool>(
                arguments,
                "IsCaptured",
                result => result.IsCaptured
            );
            Add<OfficerCaptureStateResult, Officer>(
                arguments,
                "LinkedOfficer",
                result => result.LinkedOfficer
            );
            Add<OfficerCaptureStateResult, IGameEntity>(
                arguments,
                "Context",
                result => result.Context
            );
            Add<OfficerKilledResult, Officer>(arguments, "Officer", result => result.TargetOfficer);
            Add<OfficerKilledResult, IGameEntity>(arguments, "Assassin", result => result.Assassin);
            Add<OfficerKilledResult, IGameEntity>(arguments, "Context", result => result.Context);
            Add<OfficerInjuredResult, Officer>(arguments, "Officer", result => result.Officer);
            Add<OfficerInjuredResult, int>(arguments, "Severity", result => result.Severity);
            Add<OfficerRecruitedResult, Officer>(arguments, "Officer", result => result.Officer);
            Add<OfficerRecruitedResult, Faction>(arguments, "Faction", result => result.Faction);
            Add<OfficerRecruitedResult, Planet>(arguments, "Planet", result => result.Planet);
            Add<ForceDiscoveryResult, Officer>(arguments, "Officer", result => result.Officer);
            Add<ForceDiscoveryResult, Officer>(
                arguments,
                "Discoverer",
                result => result.Discoverer
            );
            Add<ForceDiscoveryResult, int>(arguments, "ForceRank", result => result.ForceRank);
            Add<ForceDiscoveryResult, ForceEventType>(
                arguments,
                "EventType",
                result => result.EventType
            );
            Add<UnitOwnershipChangedResult, ISceneNode>(arguments, "Unit", result => result.Unit);
            Add<UnitOwnershipChangedResult, Faction>(
                arguments,
                "PreviousOwner",
                result => result.PreviousOwner
            );
            Add<UnitOwnershipChangedResult, Faction>(
                arguments,
                "NewOwner",
                result => result.NewOwner
            );
            Add<GameObjectCreatedResult, IGameEntity>(
                arguments,
                "Unit",
                result => result.GameObject
            );
            Add<GameObjectDestroyedResult, IGameEntity>(
                arguments,
                "Unit",
                result => result.DestroyedObject
            );
            Add<GameObjectDestroyedResult, IGameEntity>(
                arguments,
                "DestroyedBy",
                result => result.DestroyedBy
            );
            Add<GameObjectDestroyedResult, IGameEntity>(
                arguments,
                "Context",
                result => result.Context
            );
            Add<GameObjectDestroyedResult, UnitDestructionReason>(
                arguments,
                "Reason",
                result => result.Reason
            );
            Add<UnitArrivedResult, IGameEntity>(arguments, "Unit", result => result.Unit);
            Add<UnitArrivedResult, Planet>(arguments, "Destination", result => result.Destination);
            Add<UnitArrivedResult, string>(
                arguments,
                "MovementGroupID",
                result => result.MovementGroupID
            );
            Add<SpaceCombatResult, Fleet>(
                arguments,
                "AttackerFleet",
                result => result.AttackerFleet
            );
            Add<SpaceCombatResult, Fleet>(
                arguments,
                "DefenderFleet",
                result => result.DefenderFleet
            );
            Add<SpaceCombatResult, Planet>(arguments, "Planet", result => result.Planet);
            Add<SpaceCombatResult, CombatSide>(arguments, "Winner", result => result.Winner);
            Add<BombardmentResult, Planet>(arguments, "Planet", result => result.Planet);
            Add<BombardmentResult, Faction>(
                arguments,
                "AttackingFaction",
                result => result.AttackingFaction
            );
            Add<BombardmentResult, BombardmentType>(arguments, "Type", result => result.Type);
            Add<BombardmentResult, bool>(
                arguments,
                "PlanetDestroyed",
                result => result.PlanetDestroyed
            );
            Add<PlanetaryAssaultResult, Planet>(arguments, "Planet", result => result.Planet);
            Add<PlanetaryAssaultResult, Faction>(
                arguments,
                "AttackingFaction",
                result => result.AttackingFaction
            );
            Add<PlanetaryAssaultResult, bool>(arguments, "Success", result => result.Success);
            Add<PlanetaryAssaultResult, bool>(
                arguments,
                "BlockedByShields",
                result => result.BlockedByShields
            );
            Add<DuelResult, Officer>(
                arguments,
                "FirstOfficer",
                result => result.EncounteredOfficer
            );
            Add<DuelResult, Officer>(arguments, "SecondOfficer", result => result.OpposingOfficer);
            Add<DuelResult, string>(
                arguments,
                "FirstOfficerInstanceID",
                result => result.EncounteredOfficer?.InstanceID
            );
            Add<DuelResult, string>(
                arguments,
                "SecondOfficerInstanceID",
                result => result.OpposingOfficer?.InstanceID
            );
            Add<DuelResult, Planet>(arguments, "Location", result => result.Location);
            Add<DuelResult, bool>(
                arguments,
                "FirstOfficerCaptured",
                result => result.EncounteredOfficerCaptured
            );
            Add<DuelResult, int>(
                arguments,
                "FirstOfficerInjury",
                result => result.EncounteredOfficerInjury
            );
            Add<DuelResult, int>(
                arguments,
                "SecondOfficerInjury",
                result => result.OpposingOfficerInjury
            );
            Add<DuelResult, string>(arguments, "ImagePath", result => result.ImagePath);
            Add<DuelResult, string>(arguments, "AudioPath", result => result.AudioPath);
            Add<ManufacturingDeployedResult, Faction>(
                arguments,
                "Faction",
                result => result.Faction
            );
            Add<ManufacturingDeployedResult, IGameEntity>(
                arguments,
                "DeployedObject",
                result => result.DeployedObject
            );
            Add<ManufacturingDeployedResult, IGameEntity>(
                arguments,
                "Location",
                result => result.Location
            );
            return arguments;
        }

        /// <summary>Adds one strongly typed argument to the trigger contract.</summary>
        private static void Add<TResult, TValue>(
            IDictionary<(Type, string), GameEventTriggerArgument> arguments,
            string name,
            Func<TResult, TValue> resolve
        )
            where TResult : GameResult =>
            arguments.Add((typeof(TResult), name), GameEventTriggerArgument.Create(resolve));
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

    /// <summary>Activates when intelligence is revealed to a faction.</summary>
    [PersistableObject(Name = "IntelligenceRevealed")]
    public sealed class IntelligenceRevealedTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string RecipientFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string ObservationInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(IntelligenceRevealedResult);

        internal override bool Matches(GameResult result) =>
            result is IntelligenceRevealedResult revealed
            && MatchesInstanceID(RecipientFactionInstanceID, revealed.Recipient?.InstanceID)
            && (
                string.IsNullOrWhiteSpace(ObservationInstanceID)
                || revealed.Observations?.Any(observation =>
                    MatchesInstanceID(ObservationInstanceID, observation?.InstanceID)
                ) == true
            )
            && MatchesSource(SourceEventInstanceID, revealed);
    }

    /// <summary>Activates when a faction cannot meet a maintenance obligation.</summary>
    [PersistableObject(Name = "MaintenanceRequired")]
    public sealed class MaintenanceRequiredTrigger : GameEventTrigger
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(MaintenanceRequiredResult);

        internal override bool Matches(GameResult result) =>
            result is MaintenanceRequiredResult required
            && MatchesInstanceID(FactionInstanceID, required.Faction?.InstanceID)
            && MatchesSource(SourceEventInstanceID, required);
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

    /// <summary>Activates when an officer's Force discovery state changes.</summary>
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

        internal override Type ResultType => typeof(ForceDiscoveryResult);

        internal override bool Matches(GameResult result) =>
            result is ForceDiscoveryResult changed
            && MatchesInstanceID(OfficerInstanceID, changed.Officer?.InstanceID)
            && MatchesInstanceID(DiscovererInstanceID, changed.Discoverer?.InstanceID)
            && (!EventType.HasValue || changed.EventType == EventType.Value)
            && MatchesSource(SourceEventInstanceID, changed);
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
        public UnitDestructionReason? Reason { get; set; }

        [PersistableAttribute]
        public string SourceEventInstanceID { get; set; }

        internal override Type ResultType => typeof(GameObjectDestroyedResult);

        internal override bool Matches(GameResult result) =>
            result is GameObjectDestroyedResult destroyed
            && MatchesInstanceID(UnitInstanceID, destroyed.DestroyedObject?.InstanceID)
            && (!Reason.HasValue || destroyed.Reason == Reason.Value)
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
