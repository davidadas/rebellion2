using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    public sealed partial class GameEventExecutor
    {
        /// <summary>Tests a simulation result against an authored trigger.</summary>
        /// <param name="definition">The authored trigger and filters.</param>
        /// <param name="result">The existing simulation result.</param>
        /// <returns>Whether the result satisfies the trigger.</returns>
        internal static bool Matches(GameEventTrigger definition, GameResult result) =>
            definition switch
            {
                PlanetOwnershipChangedTrigger value => Matches(value, result),
                PlanetStatChangedTrigger value => Matches(value, result),
                BlockadeChangedTrigger value => Matches(value, result),
                UprisingStartedTrigger value => Matches(value, result),
                UprisingEndedTrigger value => Matches(value, result),
                IntelligenceRevealedTrigger value => Matches(value, result),
                MaintenanceRequiredTrigger value => Matches(value, result),
                ResearchAdvancedTrigger value => Matches(value, result),
                MissionCompletedTrigger value => Matches(value, result),
                OfficerCaptureChangedTrigger value => Matches(value, result),
                OfficerKilledTrigger value => Matches(value, result),
                OfficerInjuredTrigger value => Matches(value, result),
                OfficerRecruitedTrigger value => Matches(value, result),
                ForceDiscoveryChangedTrigger value => Matches(value, result),
                UnitOwnershipChangedTrigger value => Matches(value, result),
                UnitCreatedTrigger value => Matches(value, result),
                UnitDestroyedTrigger value => Matches(value, result),
                UnitArrivedTrigger value => Matches(value, result),
                SpaceCombatCompletedTrigger value => Matches(value, result),
                BombardmentCompletedTrigger value => Matches(value, result),
                PlanetaryAssaultCompletedTrigger value => Matches(value, result),
                DuelCompletedTrigger value => Matches(value, result),
                ManufacturingCompletedTrigger value => Matches(value, result),
                null => throw new NullReferenceException(),
                _ => throw new InvalidOperationException(
                    $"Unsupported trigger '{definition.GetType().Name}'."
                ),
            };

        /// <summary>Returns the result contract selected by an authored trigger.</summary>
        /// <param name="definition">The authored trigger.</param>
        /// <returns>The existing simulation-result type consumed by the trigger.</returns>
        private static Type GetResultType(GameEventTrigger definition) =>
            definition switch
            {
                PlanetOwnershipChangedTrigger => typeof(PlanetOwnershipChangedResult),
                PlanetStatChangedTrigger => typeof(PlanetStatChangedResult),
                BlockadeChangedTrigger => typeof(BlockadeChangedResult),
                UprisingStartedTrigger => typeof(PlanetUprisingStartedResult),
                UprisingEndedTrigger => typeof(PlanetUprisingEndedResult),
                IntelligenceRevealedTrigger => typeof(IntelligenceRevealedResult),
                MaintenanceRequiredTrigger => typeof(MaintenanceRequiredResult),
                ResearchAdvancedTrigger => typeof(ResearchOrderedResult),
                MissionCompletedTrigger => typeof(MissionCompletedResult),
                OfficerCaptureChangedTrigger => typeof(OfficerCaptureStateResult),
                OfficerKilledTrigger => typeof(OfficerKilledResult),
                OfficerInjuredTrigger => typeof(OfficerInjuredResult),
                OfficerRecruitedTrigger => typeof(OfficerRecruitedResult),
                ForceDiscoveryChangedTrigger => typeof(ForceDiscoveryResult),
                UnitOwnershipChangedTrigger => typeof(UnitOwnershipChangedResult),
                UnitCreatedTrigger => typeof(GameObjectCreatedResult),
                UnitDestroyedTrigger => typeof(GameObjectDestroyedResult),
                UnitArrivedTrigger => typeof(UnitArrivedResult),
                SpaceCombatCompletedTrigger => typeof(SpaceCombatResult),
                BombardmentCompletedTrigger => typeof(BombardmentResult),
                PlanetaryAssaultCompletedTrigger => typeof(PlanetaryAssaultResult),
                DuelCompletedTrigger => typeof(DuelResult),
                ManufacturingCompletedTrigger => typeof(ManufacturingDeployedResult),
                null => throw new NullReferenceException(),
                _ => throw new InvalidOperationException(
                    $"Unsupported trigger '{definition.GetType().Name}'."
                ),
            };

        /// <summary>
        /// Adds the authored result arguments to the event evaluation context.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="context">The event evaluation context receiving the bindings.</param>
        /// <param name="result">The matched simulation result.</param>
        internal static void Bind(
            GameEventTrigger definition,
            GameEventEvaluationContext context,
            GameResult result
        )
        {
            foreach (GameEventBinding binding in definition.Bindings)
            {
                GameEventTriggerArgument argument = GameEventTriggerArguments.Get(
                    GetResultType(definition),
                    binding.Argument
                );
                context.Bind(binding.As, argument.Resolve(result));
            }
        }

        /// <summary>
        /// Resolves the declared value type for an authored trigger argument.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="argument">The authored trigger argument name.</param>
        /// <returns>The declared argument type.</returns>
        private static Type GetBindingType(GameEventTrigger definition, string argument) =>
            GameEventTriggerArguments.Get(GetResultType(definition), argument).ValueType;

        /// <summary>
        /// Compares an optional authored instance ID with an actual instance ID.
        /// </summary>
        /// <param name="expected">The optional authored instance ID.</param>
        /// <param name="actual">The actual instance ID.</param>
        /// <returns>True when no value was authored or the instance IDs match.</returns>
        private static bool MatchesInstanceID(string expected, string actual) =>
            string.IsNullOrWhiteSpace(expected)
            || string.Equals(expected, actual, StringComparison.Ordinal);

        /// <summary>
        /// Compares an optional authored source-event ID with a simulation result.
        /// </summary>
        /// <param name="expected">The optional authored source-event ID.</param>
        /// <param name="result">The simulation result.</param>
        /// <returns>True when no source was authored or the source IDs match.</returns>
        private static bool MatchesSource(string expected, GameResult result) =>
            MatchesInstanceID(expected, result?.SourceEventInstanceID);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(PlanetOwnershipChangedTrigger definition, GameResult result) =>
            result is PlanetOwnershipChangedResult changed
            && MatchesInstanceID(definition.PlanetInstanceID, changed.Planet?.InstanceID)
            && MatchesInstanceID(
                definition.PreviousOwnerFactionInstanceID,
                changed.PreviousOwner?.InstanceID
            )
            && MatchesInstanceID(definition.NewOwnerFactionInstanceID, changed.NewOwner?.InstanceID)
            && (!definition.Reason.HasValue || changed.Reason == definition.Reason.Value)
            && MatchesSource(definition.SourceEventInstanceID, changed);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(PlanetStatChangedTrigger definition, GameResult result) =>
            result is PlanetStatChangedResult changed
            && MatchesInstanceID(definition.PlanetInstanceID, changed.Planet?.InstanceID)
            && MatchesInstanceID(definition.FactionInstanceID, changed.Faction?.InstanceID)
            && (!definition.Category.HasValue || changed.Category == definition.Category.Value)
            && MatchesSource(definition.SourceEventInstanceID, changed);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(BlockadeChangedTrigger definition, GameResult result) =>
            result is BlockadeChangedResult changed
            && MatchesInstanceID(definition.PlanetInstanceID, changed.Planet?.InstanceID)
            && (
                !definition.IsBlockaded.HasValue
                || changed.Blockaded == definition.IsBlockaded.Value
            )
            && MatchesSource(definition.SourceEventInstanceID, changed);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(UprisingStartedTrigger definition, GameResult result) =>
            result is PlanetUprisingStartedResult started
            && MatchesInstanceID(definition.PlanetInstanceID, started.Planet?.InstanceID)
            && MatchesInstanceID(
                definition.InstigatorFactionInstanceID,
                started.InstigatorFaction?.InstanceID
            )
            && MatchesSource(definition.SourceEventInstanceID, started);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(UprisingEndedTrigger definition, GameResult result) =>
            result is PlanetUprisingEndedResult ended
            && MatchesInstanceID(definition.PlanetInstanceID, ended.Planet?.InstanceID)
            && MatchesInstanceID(definition.FactionInstanceID, ended.Faction?.InstanceID)
            && MatchesSource(definition.SourceEventInstanceID, ended);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(IntelligenceRevealedTrigger definition, GameResult result) =>
            result is IntelligenceRevealedResult revealed
            && MatchesInstanceID(
                definition.RecipientFactionInstanceID,
                revealed.Recipient?.InstanceID
            )
            && (
                string.IsNullOrWhiteSpace(definition.ObservationInstanceID)
                || revealed.Observations?.Any(observation =>
                    MatchesInstanceID(definition.ObservationInstanceID, observation?.InstanceID)
                ) == true
            )
            && MatchesSource(definition.SourceEventInstanceID, revealed);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(MaintenanceRequiredTrigger definition, GameResult result) =>
            result is MaintenanceRequiredResult required
            && MatchesInstanceID(definition.FactionInstanceID, required.Faction?.InstanceID)
            && MatchesSource(definition.SourceEventInstanceID, required);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(ResearchAdvancedTrigger definition, GameResult result) =>
            result is ResearchOrderedResult advanced
            && MatchesInstanceID(definition.FactionInstanceID, advanced.Faction?.InstanceID)
            && (
                !definition.Discipline.HasValue
                || advanced.Discipline == definition.Discipline.Value
            )
            && MatchesInstanceID(
                definition.TechnologyTypeID,
                advanced.Technology?.Manufacturable?.GetTypeID()
            )
            && MatchesSource(definition.SourceEventInstanceID, advanced);

        /// <summary>
        /// Checks whether a completed mission contains the authored participants.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="participants">The completed mission's participants.</param>
        /// <returns>True when the participant filter is satisfied.</returns>
        private static bool Matches(
            MissionParticipantFilter definition,
            IReadOnlyCollection<IMissionParticipant> participants
        )
        {
            if (definition.Units.Count == 0)
                return true;

            HashSet<string> participantIDs = (participants ?? Array.Empty<IMissionParticipant>())
                .Where(participant => participant != null)
                .Select(participant => participant.GetInstanceID())
                .ToHashSet(StringComparer.Ordinal);
            return definition.Match == ParticipantMatch.All
                ? definition.Units.All(unit => participantIDs.Contains(unit.UnitInstanceID))
                : definition.Units.Any(unit => participantIDs.Contains(unit.UnitInstanceID));
        }

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(MissionCompletedTrigger definition, GameResult result)
        {
            if (result is not MissionCompletedResult completed)
                return false;
            return MatchesInstanceID(definition.MissionTypeID, completed.MissionTypeID)
                && (!definition.Outcome.HasValue || completed.Outcome == definition.Outcome.Value)
                && (
                    !definition.CompletionReason.HasValue
                    || completed.CompletionReason == definition.CompletionReason.Value
                )
                && MatchesInstanceID(
                    definition.SourceEventInstanceID,
                    completed.SourceEventInstanceID
                )
                && (
                    definition.Participants == null
                    || Matches(definition.Participants, completed.Participants)
                );
        }

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(OfficerCaptureChangedTrigger definition, GameResult result)
        {
            if (result is not OfficerCaptureStateResult changed)
                return false;
            string officerID = (changed.TargetOfficer ?? changed.CapturedOfficer)?.InstanceID;
            return MatchesInstanceID(definition.OfficerInstanceID, officerID)
                && (
                    !definition.IsCaptured.HasValue
                    || changed.IsCaptured == definition.IsCaptured.Value
                )
                && MatchesSource(definition.SourceEventInstanceID, changed);
        }

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(OfficerKilledTrigger definition, GameResult result) =>
            result is OfficerKilledResult killed
            && MatchesInstanceID(definition.OfficerInstanceID, killed.TargetOfficer?.InstanceID)
            && MatchesSource(definition.SourceEventInstanceID, killed);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(OfficerInjuredTrigger definition, GameResult result) =>
            result is OfficerInjuredResult injured
            && MatchesInstanceID(definition.OfficerInstanceID, injured.Officer?.InstanceID)
            && MatchesSource(definition.SourceEventInstanceID, injured);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(OfficerRecruitedTrigger definition, GameResult result) =>
            result is OfficerRecruitedResult recruited
            && MatchesInstanceID(definition.OfficerInstanceID, recruited.Officer?.InstanceID)
            && MatchesInstanceID(definition.FactionInstanceID, recruited.Faction?.InstanceID)
            && MatchesInstanceID(definition.PlanetInstanceID, recruited.Planet?.InstanceID)
            && MatchesSource(definition.SourceEventInstanceID, recruited);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(ForceDiscoveryChangedTrigger definition, GameResult result) =>
            result is ForceDiscoveryResult changed
            && MatchesInstanceID(definition.OfficerInstanceID, changed.Officer?.InstanceID)
            && MatchesInstanceID(definition.DiscovererInstanceID, changed.Discoverer?.InstanceID)
            && (!definition.EventType.HasValue || changed.EventType == definition.EventType.Value)
            && MatchesSource(definition.SourceEventInstanceID, changed);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(UnitOwnershipChangedTrigger definition, GameResult result) =>
            result is UnitOwnershipChangedResult changed
            && MatchesInstanceID(definition.UnitInstanceID, changed.Unit?.InstanceID)
            && MatchesInstanceID(
                definition.PreviousOwnerFactionInstanceID,
                changed.PreviousOwner?.InstanceID
            )
            && MatchesInstanceID(definition.NewOwnerFactionInstanceID, changed.NewOwner?.InstanceID)
            && MatchesSource(definition.SourceEventInstanceID, changed);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(UnitCreatedTrigger definition, GameResult result) =>
            result is GameObjectCreatedResult created
            && MatchesInstanceID(definition.UnitInstanceID, created.GameObject?.InstanceID)
            && MatchesSource(definition.SourceEventInstanceID, created);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(UnitDestroyedTrigger definition, GameResult result) =>
            result is GameObjectDestroyedResult destroyed
            && MatchesInstanceID(definition.UnitInstanceID, destroyed.DestroyedObject?.InstanceID)
            && (!definition.Reason.HasValue || destroyed.Reason == definition.Reason.Value)
            && MatchesSource(definition.SourceEventInstanceID, destroyed);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(UnitArrivedTrigger definition, GameResult result)
        {
            if (result is not UnitArrivedResult arrived)
                return false;
            return MatchesInstanceID(definition.UnitInstanceID, arrived.Unit?.InstanceID)
                && MatchesInstanceID(
                    definition.DestinationInstanceID,
                    arrived.Destination?.InstanceID
                )
                && MatchesInstanceID(
                    definition.SourceEventInstanceID,
                    arrived.SourceEventInstanceID
                );
        }

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(SpaceCombatCompletedTrigger definition, GameResult result) =>
            result is SpaceCombatResult combat
            && MatchesInstanceID(definition.PlanetInstanceID, combat.Planet?.InstanceID)
            && MatchesInstanceID(
                definition.AttackerFactionInstanceID,
                combat.AttackerOwnerInstanceID
            )
            && MatchesInstanceID(
                definition.DefenderFactionInstanceID,
                combat.DefenderOwnerInstanceID
            )
            && (!definition.Winner.HasValue || combat.Winner == definition.Winner.Value)
            && MatchesSource(definition.SourceEventInstanceID, combat);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(BombardmentCompletedTrigger definition, GameResult result) =>
            result is BombardmentResult bombardment
            && MatchesInstanceID(definition.PlanetInstanceID, bombardment.Planet?.InstanceID)
            && MatchesInstanceID(
                definition.AttackerFactionInstanceID,
                bombardment.AttackerOwnerInstanceID
            )
            && MatchesInstanceID(
                definition.DefenderFactionInstanceID,
                bombardment.DefenderOwnerInstanceID
            )
            && (!definition.Type.HasValue || bombardment.Type == definition.Type.Value)
            && (
                !definition.PlanetDestroyed.HasValue
                || bombardment.PlanetDestroyed == definition.PlanetDestroyed.Value
            )
            && MatchesSource(definition.SourceEventInstanceID, bombardment);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(
            PlanetaryAssaultCompletedTrigger definition,
            GameResult result
        ) =>
            result is PlanetaryAssaultResult assault
            && MatchesInstanceID(definition.PlanetInstanceID, assault.Planet?.InstanceID)
            && MatchesInstanceID(
                definition.AttackerFactionInstanceID,
                assault.AttackerOwnerInstanceID
            )
            && MatchesInstanceID(
                definition.DefenderFactionInstanceID,
                assault.DefenderOwnerInstanceID
            )
            && (!definition.Success.HasValue || assault.Success == definition.Success.Value)
            && (
                !definition.BlockedByShields.HasValue
                || assault.BlockedByShields == definition.BlockedByShields.Value
            )
            && MatchesSource(definition.SourceEventInstanceID, assault);

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(DuelCompletedTrigger definition, GameResult result)
        {
            if (result is not DuelResult duel)
                return false;
            return MatchesInstanceID(
                    definition.FirstOfficerInstanceID,
                    duel.EncounteredOfficer?.InstanceID
                )
                && MatchesInstanceID(
                    definition.SecondOfficerInstanceID,
                    duel.OpposingOfficer?.InstanceID
                )
                && MatchesInstanceID(definition.SourceEventInstanceID, duel.SourceEventInstanceID);
        }

        /// <summary>
        /// Checks whether the value matches the required criteria.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="result">The result.</param>
        /// <returns>True when the value matches the required criteria; otherwise false.</returns>
        private static bool Matches(ManufacturingCompletedTrigger definition, GameResult result) =>
            result is ManufacturingDeployedResult completed
            && MatchesInstanceID(definition.FactionInstanceID, completed.Faction?.InstanceID)
            && MatchesInstanceID(definition.UnitInstanceID, completed.DeployedObject?.InstanceID)
            && MatchesInstanceID(definition.LocationInstanceID, completed.Location?.InstanceID)
            && MatchesSource(definition.SourceEventInstanceID, completed);
    }

    /// <summary>
    /// Describes one stable value that a trigger may expose without reflecting over result objects.
    /// </summary>
    internal sealed class GameEventTriggerArgument
    {
        private readonly Func<GameResult, object> _resolve;

        // Argument Type.
        internal Type ValueType { get; }

        /// <summary>
        /// Initializes a new instance of the GameEventTriggerArgument class.
        /// </summary>
        /// <param name="valueType">The value type.</param>
        /// <param name="resolve">The resolve.</param>
        private GameEventTriggerArgument(Type valueType, Func<GameResult, object> resolve)
        {
            ValueType = valueType;
            _resolve = resolve;
        }

        /// <summary>
        /// Resolves the argument value from a matched simulation result.
        /// </summary>
        /// <param name="result">The matched simulation result.</param>
        /// <returns>The exposed argument value.</returns>
        internal object Resolve(GameResult result) => _resolve(result);

        /// <summary>
        /// Creates a strongly typed trigger-argument accessor.
        /// </summary>
        /// <typeparam name="TResult">The supported simulation-result type.</typeparam>
        /// <typeparam name="TValue">The exposed argument type.</typeparam>
        /// <param name="resolve">The function that resolves the argument value.</param>
        /// <returns>The trigger-argument accessor.</returns>
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

        /// <summary>
        /// Resolves a declared trigger argument.
        /// </summary>
        /// <param name="resultType">The simulation-result type.</param>
        /// <param name="argument">The authored argument name.</param>
        /// <returns>The declared trigger argument.</returns>
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

        /// <summary>
        /// Builds the result-to-argument contracts used by event bindings.
        /// </summary>
        /// <returns>The supported trigger arguments indexed by result type and argument name.</returns>
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

        /// <summary>
        /// Adds a strongly typed argument to the trigger contract.
        /// </summary>
        /// <typeparam name="TResult">The supported simulation-result type.</typeparam>
        /// <typeparam name="TValue">The exposed argument type.</typeparam>
        /// <param name="arguments">The trigger contract being built.</param>
        /// <param name="name">The authored argument name.</param>
        /// <param name="resolve">The function that resolves the argument value.</param>
        private static void Add<TResult, TValue>(
            IDictionary<(Type, string), GameEventTriggerArgument> arguments,
            string name,
            Func<TResult, TValue> resolve
        )
            where TResult : GameResult =>
            arguments.Add((typeof(TResult), name), GameEventTriggerArgument.Create(resolve));
    }
}
