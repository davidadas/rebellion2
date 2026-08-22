using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects one stable simulation event and declares the result arguments exposed locally.
    /// </summary>
    [PersistableObject(Name = "Trigger")]
    public sealed class GameEventTrigger
    {
        [PersistableAttribute]
        public string Event { get; set; }

        /// <summary>Gets the authored mappings from public result arguments to local bindings.</summary>
        public List<GameEventTriggerBinding> Bindings { get; set; } =
            new List<GameEventTriggerBinding>();

        /// <summary>Creates an empty trigger for deserialization.</summary>
        public GameEventTrigger() { }

        /// <summary>Creates a trigger for a stable event contract and its local bindings.</summary>
        public GameEventTrigger(string eventID, params (string Argument, string As)[] bindings)
        {
            Event = eventID;
            foreach ((string argument, string localName) in bindings)
                Bindings.Add(new GameEventTriggerBinding { Argument = argument, As = localName });
        }

        /// <summary>Gets the concrete result type consumed by this trigger.</summary>
        internal Type ResultType => GameEventTriggerRegistry.GetResultType(Event);

        /// <summary>
        /// Gets the statically typed arguments exposed by this trigger contract.
        /// </summary>
        [PersistableIgnore]
        public IReadOnlyDictionary<string, Type> AvailableArguments =>
            GameEventTriggerRegistry.GetArguments(Event);

        /// <summary>Returns whether a result satisfies this trigger contract.</summary>
        internal bool Matches(GameResult result) => GameEventTriggerRegistry.Matches(Event, result);

        /// <summary>Gets the declared type of one public trigger argument.</summary>
        internal Type GetArgumentType(string argument) =>
            GameEventTriggerRegistry.GetArgumentType(Event, argument);

        /// <summary>Copies authored result arguments into the event activation context.</summary>
        internal void Bind(GameEventExecutionContext context, GameResult result) =>
            GameEventTriggerRegistry.Bind(context, this, result);
    }

    /// <summary>
    /// Gives one public trigger argument a local name within an event activation.
    /// </summary>
    [PersistableObject(Name = "Bind")]
    public sealed class GameEventTriggerBinding
    {
        [PersistableAttribute]
        public string Argument { get; set; }

        [PersistableAttribute]
        public string As { get; set; }
    }

    /// <summary>
    /// Defines the stable, explicitly typed simulation-result contracts available to content.
    /// </summary>
    internal static class GameEventTriggerRegistry
    {
        private interface ITriggerContract
        {
            Type ResultType { get; }
            IReadOnlyDictionary<string, Type> Arguments { get; }
            bool Matches(GameResult result);
            bool TryReadArgument(GameResult result, string argument, out object value);
            bool TryGetArgumentType(string argument, out Type type);
        }

        private sealed class TriggerContract<TResult> : ITriggerContract
            where TResult : GameResult
        {
            private sealed class TriggerArgument
            {
                /// <summary>Gets the stable public type of the argument.</summary>
                internal Type Type { get; set; }

                /// <summary>Gets the argument value from a typed result.</summary>
                internal Func<TResult, object> Getter { get; set; }
            }

            private readonly Dictionary<string, TriggerArgument> _arguments = new(
                StringComparer.Ordinal
            );

            public Type ResultType => typeof(TResult);

            public IReadOnlyDictionary<string, Type> Arguments =>
                _arguments.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value.Type,
                    StringComparer.Ordinal
                );

            internal TriggerContract<TResult> Argument<TValue>(
                string name,
                Func<TResult, TValue> getter
            )
            {
                _arguments.Add(
                    name,
                    new TriggerArgument { Type = typeof(TValue), Getter = result => getter(result) }
                );
                return this;
            }

            /// <summary>Returns whether a result belongs to this concrete trigger contract.</summary>
            public bool Matches(GameResult result) => result is TResult;

            /// <summary>Reads a declared argument from a result handled by this contract.</summary>
            public bool TryReadArgument(GameResult result, string argument, out object value)
            {
                if (
                    result is TResult typed
                    && _arguments.TryGetValue(argument, out TriggerArgument contract)
                )
                {
                    value = contract.Getter(typed);
                    return true;
                }

                value = null;
                return false;
            }

            /// <summary>Gets the declared public type of one trigger argument.</summary>
            public bool TryGetArgumentType(string argument, out Type type)
            {
                if (_arguments.TryGetValue(argument, out TriggerArgument contract))
                {
                    type = contract.Type;
                    return true;
                }
                type = null;
                return false;
            }
        }

        private static readonly Dictionary<string, ITriggerContract> Contracts = CreateContracts();

        internal static Type GetResultType(string eventID) => GetContract(eventID).ResultType;

        internal static IReadOnlyDictionary<string, Type> GetArguments(string eventID) =>
            GetContract(eventID).Arguments;

        internal static bool Matches(string eventID, GameResult result) =>
            result != null && GetContract(eventID).Matches(result);

        internal static Type GetArgumentType(string eventID, string argument)
        {
            if (GetContract(eventID).TryGetArgumentType(argument, out Type type))
                return type;
            throw new InvalidOperationException(
                $"Trigger '{eventID}' does not expose argument '{argument}'."
            );
        }

        internal static void Bind(
            GameEventExecutionContext context,
            GameEventTrigger trigger,
            GameResult result
        )
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (trigger == null || result == null)
                return;

            ITriggerContract contract = GetContract(trigger.Event);
            foreach (GameEventTriggerBinding binding in trigger.Bindings)
            {
                if (!contract.TryReadArgument(result, binding.Argument, out object value))
                    throw new InvalidOperationException(
                        $"Trigger '{trigger.Event}' does not expose argument '{binding.Argument}'."
                    );
                context.Bind(binding.As, value);
            }
        }

        private static ITriggerContract GetContract(string eventID)
        {
            if (
                string.IsNullOrWhiteSpace(eventID)
                || !Contracts.TryGetValue(eventID, out ITriggerContract contract)
            )
                throw new InvalidOperationException($"Unknown game-event trigger '{eventID}'.");
            return contract;
        }

        /// <summary>Creates the complete set of supported game-event trigger contracts.</summary>
        private static Dictionary<string, ITriggerContract> CreateContracts()
        {
            Dictionary<string, ITriggerContract> contracts = new(StringComparer.Ordinal);
            RegisterPlanetTriggers(contracts);
            RegisterFactionTriggers(contracts);
            RegisterMissionTriggers(contracts);
            RegisterOfficerTriggers(contracts);
            RegisterUnitLifecycleTriggers(contracts);
            RegisterCombatTriggers(contracts);
            RegisterManufacturingTriggers(contracts);
            return contracts;
        }

        /// <summary>Registers planet result contracts exposed to authored events.</summary>
        private static void RegisterPlanetTriggers(IDictionary<string, ITriggerContract> contracts)
        {
            Register<PlanetOwnershipChangedResult>(contracts, "core:planet.owner-changed")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("PreviousOwner", result => result.PreviousOwner)
                .Argument("PreviousOwnerInstanceID", result => result.PreviousOwner?.InstanceID)
                .Argument("NewOwner", result => result.NewOwner)
                .Argument("NewOwnerInstanceID", result => result.NewOwner?.InstanceID)
                .Argument("Reason", result => result.Reason)
                .Argument("ObserverFactionInstanceIDs", result => result.ObserverFactionInstanceIDs)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<PlanetStatChangedResult>(contracts, "core:planet.stat-changed")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("Faction", result => result.Faction)
                .Argument("FactionInstanceID", result => result.Faction?.InstanceID)
                .Argument("Category", result => result.Category)
                .Argument("OldValue", result => result.OldValue)
                .Argument("NewValue", result => result.NewValue)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<SmugglingChangedResult>(contracts, "core:smuggling.changed")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("ControllerFaction", result => result.Controller)
                .Argument("ControllerFactionInstanceID", result => result.Controller?.InstanceID)
                .Argument("BeneficiaryFaction", result => result.Beneficiary)
                .Argument("BeneficiaryFactionInstanceID", result => result.Beneficiary?.InstanceID)
                .Argument("OldPercent", result => result.OldPercent)
                .Argument("NewPercent", result => result.NewPercent)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<BlockadeChangedResult>(contracts, "core:blockade.changed")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("BlockadingFleet", result => result.BlockadingFleet)
                .Argument("BlockadingFleetInstanceID", result => result.BlockadingFleet?.InstanceID)
                .Argument("IsBlockaded", result => result.Blockaded)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<PlanetUprisingStartedResult>(contracts, "core:uprising.started")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("InstigatorFaction", result => result.InstigatorFaction)
                .Argument(
                    "InstigatorFactionInstanceID",
                    result => result.InstigatorFaction?.InstanceID
                )
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<PlanetNearUprisingResult>(contracts, "core:uprising.nearing")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<PlanetUprisingEndedResult>(contracts, "core:uprising.ended")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("Faction", result => result.Faction)
                .Argument("FactionInstanceID", result => result.Faction?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<HeadquartersDestroyedResult>(contracts, "core:headquarters.destroyed")
                .Argument("Headquarters", result => result.Headquarters)
                .Argument("HeadquartersInstanceID", result => result.Headquarters?.InstanceID)
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("DefenderFaction", result => result.Defender)
                .Argument("DefenderFactionInstanceID", result => result.Defender?.InstanceID)
                .Argument("AttackerFaction", result => result.Attacker)
                .Argument("AttackerFactionInstanceID", result => result.Attacker?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<PlanetGarrisonChangedResult>(contracts, "core:planet.garrison-changed")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<PlanetIncidentResult>(contracts, "core:planet.incident")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("IncidentType", result => result.IncidentType)
                .Argument("Severity", result => result.Severity)
                .Argument("ChangedStat", result => result.ChangedStat)
                .Argument("OldValue", result => result.OldValue)
                .Argument("NewValue", result => result.NewValue)
                .Argument("DestroyedObjects", result => result.DestroyedObjects)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
        }

        /// <summary>Registers faction result contracts exposed to authored events.</summary>
        private static void RegisterFactionTriggers(IDictionary<string, ITriggerContract> contracts)
        {
            Register<IntelligenceRevealedResult>(contracts, "core:intelligence.revealed")
                .Argument("RecipientFaction", result => result.Recipient)
                .Argument("RecipientFactionInstanceID", result => result.Recipient?.InstanceID)
                .Argument("Observations", result => result.Observations)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<MaintenanceRequiredResult>(contracts, "core:maintenance.required")
                .Argument("Faction", result => result.Faction)
                .Argument("FactionInstanceID", result => result.Faction?.InstanceID)
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("Amount", result => result.Amount)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<ResearchOrderedResult>(contracts, "core:research.completed")
                .Argument("Faction", result => result.Faction)
                .Argument("FactionInstanceID", result => result.Faction?.InstanceID)
                .Argument("Discipline", result => result.Discipline)
                .Argument("ResearchOrder", result => result.ResearchOrder)
                .Argument("Technology", result => result.Technology)
                .Argument(
                    "TechnologyTypeID",
                    result => result.Technology?.Manufacturable?.GetTypeID()
                )
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<ResearchExhaustedResult>(contracts, "core:research.exhausted")
                .Argument("Faction", result => result.Faction)
                .Argument("FactionInstanceID", result => result.Faction?.InstanceID)
                .Argument("Discipline", result => result.Discipline)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<RecruitmentExhaustedResult>(contracts, "core:recruitment.exhausted")
                .Argument("Faction", result => result.Faction)
                .Argument("FactionInstanceID", result => result.Faction?.InstanceID)
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<VictoryResult>(contracts, "core:game.completed")
                .Argument("WinnerFaction", result => result.Winner)
                .Argument("WinnerFactionInstanceID", result => result.Winner?.InstanceID)
                .Argument("LoserFaction", result => result.Loser)
                .Argument("LoserFactionInstanceID", result => result.Loser?.InstanceID)
                .Argument("GameMode", result => result.GameMode)
                .Argument("Description", result => result.Description)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
        }

        /// <summary>Registers mission result contracts exposed to authored events.</summary>
        private static void RegisterMissionTriggers(IDictionary<string, ITriggerContract> contracts)
        {
            Register<MissionCompletedResult>(contracts, "core:mission.completed")
                .Argument("Mission", result => result.Mission)
                .Argument("Outcome", result => result.Outcome)
                .Argument("CompletionReason", result => result.CompletionReason)
                .Argument("Participants", result => result.Participants)
                .Argument("Location", result => result.Location)
                .Argument("ReturnDestination", result => result.ReturnDestination)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<PlanetSectorsRevealedResult>(contracts, "core:planet-sectors.revealed")
                .Argument("PlanetSectors", result => result.AdditionalSectors)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
        }

        /// <summary>Registers officer result contracts exposed to authored events.</summary>
        private static void RegisterOfficerTriggers(IDictionary<string, ITriggerContract> contracts)
        {
            Register<ForceDiscoveryResult>(contracts, "core:force.discovered")
                .Argument("Officer", result => result.Officer)
                .Argument("Discoverer", result => result.Discoverer)
                .Argument("ForceRank", result => result.ForceRank)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<OfficerRecruitedResult>(contracts, "core:officer.recruited")
                .Argument("Officer", result => result.Officer)
                .Argument("OfficerInstanceID", result => result.Officer?.InstanceID)
                .Argument("Faction", result => result.Faction)
                .Argument("FactionInstanceID", result => result.Faction?.InstanceID)
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<OfficerCaptureStateResult>(contracts, "core:officer.capture-changed")
                .Argument("Officer", result => result.TargetOfficer ?? result.CapturedOfficer)
                .Argument(
                    "OfficerInstanceID",
                    result => (result.TargetOfficer ?? result.CapturedOfficer)?.InstanceID
                )
                .Argument("LinkedOfficer", result => result.LinkedOfficer)
                .Argument("Context", result => result.Context)
                .Argument("IsCaptured", result => result.IsCaptured)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<OfficerKilledResult>(contracts, "core:officer.killed")
                .Argument("Officer", result => result.TargetOfficer)
                .Argument("OfficerInstanceID", result => result.TargetOfficer?.InstanceID)
                .Argument("Assassin", result => result.Assassin)
                .Argument("Context", result => result.Context)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<OfficerInjuredResult>(contracts, "core:officer.injured")
                .Argument("Officer", result => result.Officer)
                .Argument("OfficerInstanceID", result => result.Officer?.InstanceID)
                .Argument("Severity", result => result.Severity)
                .Argument("Detail", result => result.Detail)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<OfficerRescuedResult>(contracts, "core:officer.rescued")
                .Argument("Officer", result => result.Officer)
                .Argument("OfficerInstanceID", result => result.Officer?.InstanceID)
                .Argument("RescuingFaction", result => result.RescuingFaction)
                .Argument("RescuingFactionInstanceID", result => result.RescuingFaction?.InstanceID)
                .Argument("Planet", result => result.Location)
                .Argument("PlanetInstanceID", result => result.Location?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<CommandKindChangedResult>(contracts, "core:officer.command-changed")
                .Argument("Officer", result => result.Officer)
                .Argument("OfficerInstanceID", result => result.Officer?.InstanceID)
                .Argument("CommandKind", result => result.CommandKind)
                .Argument("Detail", result => result.Detail)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<OfficerCommandingResult>(contracts, "core:officer.command-assigned")
                .Argument("Officer", result => result.Officer)
                .Argument("OfficerInstanceID", result => result.Officer?.InstanceID)
                .Argument("CommandTarget", result => result.CommandTarget)
                .Argument("CommandTargetInstanceID", result => result.CommandTarget?.InstanceID)
                .Argument("Context", result => result.Context)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<TraitorDiscoveredResult>(contracts, "core:officer.traitor-discovered")
                .Argument("Officer", result => result.Officer)
                .Argument("OfficerInstanceID", result => result.Officer?.InstanceID)
                .Argument("DiscoveredBy", result => result.DiscoveredBy)
                .Argument("Context", result => result.Context)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<ForceTrainingResult>(contracts, "core:force.training-completed")
                .Argument("Officer", result => result.Officer)
                .Argument("OfficerInstanceID", result => result.Officer?.InstanceID)
                .Argument("Progress", result => result.Progress)
                .Argument("Detail", result => result.Detail)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<ForceExperienceResult>(contracts, "core:force.experience-gained")
                .Argument("Officer", result => result.Officer)
                .Argument("OfficerInstanceID", result => result.Officer?.InstanceID)
                .Argument("ExperienceGained", result => result.ExperienceGained)
                .Argument("PreviousForceRank", result => result.PreviousForceRank)
                .Argument("CurrentForceRank", result => result.CurrentForceRank)
                .Argument("Detail", result => result.Detail)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
        }

        /// <summary>Registers unit-lifecycle result contracts exposed to authored events.</summary>
        private static void RegisterUnitLifecycleTriggers(
            IDictionary<string, ITriggerContract> contracts
        )
        {
            Register<UnitOwnershipChangedResult>(contracts, "core:unit.owner-changed")
                .Argument("Unit", result => result.Unit)
                .Argument("UnitInstanceID", result => result.Unit?.InstanceID)
                .Argument("PreviousOwner", result => result.PreviousOwner)
                .Argument("PreviousOwnerInstanceID", result => result.PreviousOwner?.InstanceID)
                .Argument("NewOwner", result => result.NewOwner)
                .Argument("NewOwnerInstanceID", result => result.NewOwner?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<GameObjectCreatedResult>(contracts, "core:unit.created")
                .Argument("Unit", result => result.GameObject)
                .Argument("UnitInstanceID", result => result.GameObject?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<GameObjectDeployedResult>(contracts, "core:unit.deployed")
                .Argument("Unit", result => result.GameObject)
                .Argument("UnitInstanceID", result => result.GameObject?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<GameObjectEnrouteResult>(contracts, "core:unit.movement-started")
                .Argument("Unit", result => result.GameObject)
                .Argument("UnitInstanceID", result => result.GameObject?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<UnitArrivedResult>(contracts, "core:unit.arrived")
                .Argument("Unit", result => result.Unit)
                .Argument("UnitInstanceID", result => result.Unit?.InstanceID)
                .Argument("Destination", result => result.Destination)
                .Argument("DestinationInstanceID", result => result.Destination?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<GameObjectDamagedResult>(contracts, "core:unit.damaged")
                .Argument("Unit", result => result.GameObject)
                .Argument("UnitInstanceID", result => result.GameObject?.InstanceID)
                .Argument("Damage", result => result.DamageValue)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<GameObjectDestroyedResult>(contracts, "core:unit.destroyed")
                .Argument("Unit", result => result.DestroyedObject)
                .Argument("UnitInstanceID", result => result.DestroyedObject?.InstanceID)
                .Argument("DestroyedBy", result => result.DestroyedBy)
                .Argument("Context", result => result.Context)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<GameObjectDestroyedOnArrivalResult>(
                    contracts,
                    "core:unit.destroyed-on-arrival"
                )
                .Argument("Unit", result => result.DestroyedObject)
                .Argument("UnitInstanceID", result => result.DestroyedObject?.InstanceID)
                .Argument("Reference", result => result.Ref)
                .Argument("Context", result => result.Context)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<GameObjectAutoscrappedResult>(contracts, "core:unit.autoscrapped")
                .Argument("Unit", result => result.DestroyedObject)
                .Argument("UnitInstanceID", result => result.DestroyedObject?.InstanceID)
                .Argument("Reference", result => result.Ref)
                .Argument("Context", result => result.Context)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<GameObjectSabotagedResult>(contracts, "core:unit.sabotaged")
                .Argument("Unit", result => result.SabotagedObject)
                .Argument("UnitInstanceID", result => result.SabotagedObject?.InstanceID)
                .Argument("Saboteur", result => result.Saboteur)
                .Argument("Context", result => result.Context)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
        }

        /// <summary>Registers combat result contracts exposed to authored events.</summary>
        private static void RegisterCombatTriggers(IDictionary<string, ITriggerContract> contracts)
        {
            Register<DuelResult>(contracts, "core:duel.completed")
                .Argument("Officer", result => result.EncounteredOfficer)
                .Argument("OfficerInstanceID", result => result.EncounteredOfficer?.InstanceID)
                .Argument("Opponent", result => result.OpposingOfficer)
                .Argument("OpponentInstanceID", result => result.OpposingOfficer?.InstanceID)
                .Argument("Location", result => result.Location)
                .Argument("OfficerCaptured", result => result.EncounteredOfficerCaptured)
                .Argument("OfficerInjury", result => result.EncounteredOfficerInjury)
                .Argument("OpponentInjury", result => result.OpposingOfficerInjury)
                .Argument("ImagePath", result => result.ImagePath)
                .Argument("AudioPath", result => result.AudioPath)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<SpaceCombatResult>(contracts, "core:combat.completed")
                .Argument("AttackerFleet", result => result.AttackerFleet)
                .Argument("DefenderFleet", result => result.DefenderFleet)
                .Argument("AttackerFactionInstanceID", result => result.AttackerOwnerInstanceID)
                .Argument("DefenderFactionInstanceID", result => result.DefenderOwnerInstanceID)
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("Winner", result => result.Winner)
                .Argument("AttackerOutcome", result => result.AttackerOutcome)
                .Argument("DefenderOutcome", result => result.DefenderOutcome)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<BombardmentResult>(contracts, "core:bombardment.completed")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("AttackingFaction", result => result.AttackingFaction)
                .Argument("AttackerFactionInstanceID", result => result.AttackerOwnerInstanceID)
                .Argument("DefenderFactionInstanceID", result => result.DefenderOwnerInstanceID)
                .Argument("Type", result => result.Type)
                .Argument("SuccessfulStrikes", result => result.SuccessfulStrikes)
                .Argument("HeadquartersDestroyed", result => result.HeadquartersDestroyed)
                .Argument("PlanetDestroyed", result => result.PlanetDestroyed)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<PlanetaryAssaultResult>(contracts, "core:planetary-assault.completed")
                .Argument("Planet", result => result.Planet)
                .Argument("PlanetInstanceID", result => result.Planet?.InstanceID)
                .Argument("AttackingFaction", result => result.AttackingFaction)
                .Argument("AttackerFactionInstanceID", result => result.AttackerOwnerInstanceID)
                .Argument("DefenderFactionInstanceID", result => result.DefenderOwnerInstanceID)
                .Argument("Success", result => result.Success)
                .Argument("BlockedByShields", result => result.BlockedByShields)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<EvacuationLossesResult>(contracts, "core:evacuation.completed")
                .Argument("Faction", result => result.Faction)
                .Argument("FactionInstanceID", result => result.Faction?.InstanceID)
                .Argument("Planet", result => result.Location)
                .Argument("PlanetInstanceID", result => result.Location?.InstanceID)
                .Argument("LostCapitalShips", result => result.LostShips)
                .Argument("LostStarfighters", result => result.LostStarfighters)
                .Argument("LostRegiments", result => result.LostRegiments)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
        }

        /// <summary>Registers manufacturing result contracts exposed to authored events.</summary>
        private static void RegisterManufacturingTriggers(
            IDictionary<string, ITriggerContract> contracts
        )
        {
            Register<ManufacturingDeployedResult>(contracts, "core:manufacturing.completed")
                .Argument("Faction", result => result.Faction)
                .Argument("FactionInstanceID", result => result.Faction?.InstanceID)
                .Argument("Unit", result => result.DeployedObject)
                .Argument("UnitInstanceID", result => result.DeployedObject?.InstanceID)
                .Argument("Location", result => result.Location)
                .Argument("LocationInstanceID", result => result.Location?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<ManufacturingIdleResult>(contracts, "core:manufacturing.idle")
                .Argument("Planet", result => result.ProductionPlanet)
                .Argument("PlanetInstanceID", result => result.ProductionPlanet?.InstanceID)
                .Argument("Faction", result => result.Faction)
                .Argument("FactionInstanceID", result => result.Faction?.InstanceID)
                .Argument("ManufacturingType", result => result.ManufacturingType)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
        }

        private static TriggerContract<TResult> Register<TResult>(
            IDictionary<string, ITriggerContract> contracts,
            string eventID
        )
            where TResult : GameResult
        {
            TriggerContract<TResult> contract = new();
            contracts.Add(eventID, contract);
            return contract;
        }
    }
}
