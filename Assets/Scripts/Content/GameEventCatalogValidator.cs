using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game.Events;

/// <summary>
/// Validates data-defined game events before a content pack can enter runtime.
/// </summary>
public static class GameEventCatalogValidator
{
    /// <summary>
    /// Rejects malformed event catalogs with one actionable, aggregate content error.
    /// </summary>
    /// <param name="events">The deserialized event definitions.</param>
    public static void Validate(IReadOnlyList<GameEvent> events)
    {
        if (events == null)
            throw new InvalidDataException("Game event catalog is missing.");

        List<string> errors = new List<string>();
        Dictionary<string, GameEvent> eventsById = new Dictionary<string, GameEvent>(
            StringComparer.Ordinal
        );
        for (int index = 0; index < events.Count; index++)
        {
            GameEvent gameEvent = events[index];
            if (gameEvent == null)
            {
                errors.Add($"Event at index {index} is null.");
                continue;
            }

            string eventId = gameEvent.InstanceID;
            if (string.IsNullOrWhiteSpace(eventId))
            {
                errors.Add($"Event at index {index} has no InstanceID.");
                continue;
            }

            if (!eventsById.TryAdd(eventId, gameEvent))
                errors.Add($"Event '{eventId}' is defined more than once.");

            ValidateEvent(gameEvent, errors);
        }

        ValidateEventReferences(eventsById, errors);
        ValidateTriggerCycles(eventsById, errors);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                "Game event catalog is invalid:\n- " + string.Join("\n- ", errors)
            );
        }
    }

    private static void ValidateEvent(GameEvent gameEvent, List<string> errors)
    {
        string context = $"Event '{gameEvent.InstanceID}'";
        ValidateDelay(
            gameEvent.InitialDelayTicks,
            context,
            nameof(gameEvent.InitialDelayTicks),
            errors
        );
        ValidateDelay(
            gameEvent.InitialDelayRandomTicks,
            context,
            nameof(gameEvent.InitialDelayRandomTicks),
            errors
        );
        ValidateDelay(
            gameEvent.RepeatDelayTicks,
            context,
            nameof(gameEvent.RepeatDelayTicks),
            errors
        );
        ValidateDelay(
            gameEvent.RepeatDelayRandomTicks,
            context,
            nameof(gameEvent.RepeatDelayRandomTicks),
            errors
        );

        ValidateConditionList(gameEvent.Conditionals, $"{context}.Conditionals", errors);
        ValidateActionList(gameEvent.Actions, $"{context}.Actions", errors);
    }

    private static void ValidateDelay(
        int value,
        string context,
        string memberName,
        List<string> errors
    )
    {
        if (value < 0)
            errors.Add($"{context}.{memberName} cannot be negative.");
    }

    private static void ValidateConditionList(
        IReadOnlyList<GameConditional> conditions,
        string path,
        List<string> errors
    )
    {
        if (conditions == null)
        {
            errors.Add($"{path} is null.");
            return;
        }

        for (int index = 0; index < conditions.Count; index++)
        {
            GameConditional condition = conditions[index];
            string conditionPath = $"{path}[{index}]";
            if (condition == null)
            {
                errors.Add($"{conditionPath} is null.");
                continue;
            }

            switch (condition)
            {
                case EventVariableConditional variable when string.IsNullOrWhiteSpace(variable.Key):
                    errors.Add($"{conditionPath}.Key is required.");
                    break;
                case IsAtLocationConditional atLocation:
                    if (string.IsNullOrWhiteSpace(atLocation.UnitInstanceID))
                        errors.Add($"{conditionPath}.UnitInstanceID is required.");
                    if (string.IsNullOrWhiteSpace(atLocation.LocationInstanceID))
                        errors.Add($"{conditionPath}.LocationInstanceID is required.");
                    break;
                case AndConditional and:
                    ValidateComposite(and.Conditionals, conditionPath, 1, errors);
                    break;
                case OrConditional or:
                    ValidateComposite(or.Conditionals, conditionPath, 1, errors);
                    break;
                case NotConditional not:
                    ValidateComposite(not.Conditionals, conditionPath, 1, errors, exactly: true);
                    break;
                case XorConditional xor:
                    ValidateComposite(xor.Conditionals, conditionPath, 2, errors);
                    break;
                case AreOnSamePlanetConditional samePlanet:
                    ValidateIds(samePlanet.UnitInstanceIDs, conditionPath, 2, errors);
                    break;
                case AreOnOpposingFactionsConditional opposing:
                    ValidateIds(opposing.UnitInstanceIDs, conditionPath, 2, errors, exactly: true);
                    break;
                case AreOnPlanetConditional onPlanet:
                    ValidateIds(onPlanet.UnitInstanceIDs, conditionPath, 1, errors);
                    break;
            }
        }
    }

    private static void ValidateComposite(
        IReadOnlyList<GameConditional> conditions,
        string path,
        int minimum,
        List<string> errors,
        bool exactly = false
    )
    {
        if (
            conditions == null
            || (exactly ? conditions.Count != minimum : conditions.Count < minimum)
        )
        {
            string requirement = exactly ? $"exactly {minimum}" : $"at least {minimum}";
            errors.Add($"{path} requires {requirement} child condition(s).");
            return;
        }

        ValidateConditionList(conditions, $"{path}.Conditionals", errors);
    }

    private static void ValidateActionList(
        IReadOnlyList<GameAction> actions,
        string path,
        List<string> errors
    )
    {
        if (actions == null)
        {
            errors.Add($"{path} is null.");
            return;
        }

        for (int index = 0; index < actions.Count; index++)
        {
            GameAction action = actions[index];
            string actionPath = $"{path}[{index}]";
            if (action == null)
            {
                errors.Add($"{actionPath} is null.");
                continue;
            }

            switch (action)
            {
                case ConditionalAction conditional:
                    ValidateComposite(conditional.Conditionals, actionPath, 1, errors);
                    ValidateActionList(conditional.Actions, $"{actionPath}.Actions", errors);
                    ValidateActionList(
                        conditional.ElseActions,
                        $"{actionPath}.ElseActions",
                        errors
                    );
                    break;
                case RandomOutcomeAction randomOutcome:
                    if (randomOutcome.Probability < 0 || randomOutcome.Probability > 1)
                        errors.Add($"{actionPath}.Probability must be between 0 and 1.");
                    if (randomOutcome.Actions == null || randomOutcome.Actions.Count == 0)
                        errors.Add($"{actionPath} requires at least one child action.");
                    else
                        ValidateActionList(randomOutcome.Actions, $"{actionPath}.Actions", errors);
                    break;
                case TriggerDuelAction duel:
                    ValidateIds(duel.AttackerInstanceIDs, $"{actionPath}.Attackers", 1, errors);
                    ValidateIds(duel.DefenderInstanceIDs, $"{actionPath}.Defenders", 1, errors);
                    break;
                case NarrativeMessageAction message:
                    ValidateNarrativeMessage(message, actionPath, errors);
                    break;
                case SetEventVariableAction variable when string.IsNullOrWhiteSpace(variable.Key):
                    errors.Add($"{actionPath}.Key is required.");
                    break;
                case RequestMovementAction movement:
                    if (string.IsNullOrWhiteSpace(movement.UnitInstanceID))
                        errors.Add($"{actionPath}.UnitInstanceID is required.");
                    if (string.IsNullOrWhiteSpace(movement.DestinationInstanceID))
                        errors.Add($"{actionPath}.DestinationInstanceID is required.");
                    break;
                case StartScriptedTrainingAction training:
                    if (string.IsNullOrWhiteSpace(training.TraineeInstanceID))
                        errors.Add($"{actionPath}.TraineeInstanceID is required.");
                    if (training.DurationTicks < 1)
                        errors.Add($"{actionPath}.DurationTicks must be at least 1.");
                    if (training.CompletionBonusPercent < 0)
                        errors.Add($"{actionPath}.CompletionBonusPercent cannot be negative.");
                    if (string.IsNullOrWhiteSpace(training.CompletionVariableKey))
                        errors.Add($"{actionPath}.CompletionVariableKey is required.");
                    break;
                case TriggerEventAction trigger
                    when string.IsNullOrWhiteSpace(trigger.EventInstanceID):
                    errors.Add($"{actionPath}.EventInstanceID is required.");
                    break;
            }
        }
    }

    private static void ValidateNarrativeMessage(
        NarrativeMessageAction message,
        string path,
        List<string> errors
    )
    {
        if (
            string.IsNullOrWhiteSpace(message.RecipientFactionInstanceID)
            && string.IsNullOrWhiteSpace(message.RecipientUnitInstanceID)
            && string.IsNullOrWhiteSpace(message.SubjectInstanceID)
        )
        {
            errors.Add(
                $"{path} requires RecipientFactionInstanceID, RecipientUnitInstanceID, or SubjectInstanceID."
            );
        }

        if (
            string.IsNullOrWhiteSpace(message.TitleTemplate)
            && string.IsNullOrWhiteSpace(message.BodyTemplate)
        )
            errors.Add($"{path} requires a title or body template.");
    }

    private static void ValidateIds(
        IReadOnlyList<string> ids,
        string path,
        int minimum,
        List<string> errors,
        bool exactly = false
    )
    {
        if (ids == null || (exactly ? ids.Count != minimum : ids.Count < minimum))
        {
            string requirement = exactly ? $"exactly {minimum}" : $"at least {minimum}";
            errors.Add($"{path} requires {requirement} non-empty ID(s).");
            return;
        }

        if (ids.Any(string.IsNullOrWhiteSpace))
            errors.Add($"{path} contains an empty ID.");
    }

    private static void ValidateEventReferences(
        IReadOnlyDictionary<string, GameEvent> eventsById,
        List<string> errors
    )
    {
        foreach ((string eventId, GameEvent gameEvent) in eventsById)
        {
            foreach (TriggerEventAction trigger in EnumerateTriggers(gameEvent.Actions))
            {
                if (
                    !string.IsNullOrWhiteSpace(trigger.EventInstanceID)
                    && !eventsById.ContainsKey(trigger.EventInstanceID)
                )
                {
                    errors.Add(
                        $"Event '{eventId}' triggers unknown event '{trigger.EventInstanceID}'."
                    );
                }
            }
        }
    }

    private static void ValidateTriggerCycles(
        IReadOnlyDictionary<string, GameEvent> eventsById,
        List<string> errors
    )
    {
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> active = new HashSet<string>(StringComparer.Ordinal);
        foreach (string eventId in eventsById.Keys)
            Visit(eventId, eventsById, visited, active, errors);
    }

    private static void Visit(
        string eventId,
        IReadOnlyDictionary<string, GameEvent> eventsById,
        HashSet<string> visited,
        HashSet<string> active,
        List<string> errors
    )
    {
        if (visited.Contains(eventId))
            return;
        if (!active.Add(eventId))
        {
            errors.Add($"Event trigger cycle reaches '{eventId}'.");
            return;
        }

        foreach (TriggerEventAction trigger in EnumerateTriggers(eventsById[eventId].Actions))
        {
            if (eventsById.ContainsKey(trigger.EventInstanceID))
                Visit(trigger.EventInstanceID, eventsById, visited, active, errors);
        }

        active.Remove(eventId);
        visited.Add(eventId);
    }

    private static IEnumerable<TriggerEventAction> EnumerateTriggers(
        IEnumerable<GameAction> actions
    )
    {
        if (actions == null)
            yield break;

        foreach (GameAction action in actions)
        {
            if (action is TriggerEventAction trigger)
                yield return trigger;
            if (action is RandomOutcomeAction random)
            {
                foreach (TriggerEventAction nested in EnumerateTriggers(random.Actions))
                    yield return nested;
            }
            if (action is ConditionalAction conditional)
            {
                foreach (TriggerEventAction nested in EnumerateTriggers(conditional.Actions))
                    yield return nested;
                foreach (TriggerEventAction nested in EnumerateTriggers(conditional.ElseActions))
                    yield return nested;
            }
        }
    }
}
