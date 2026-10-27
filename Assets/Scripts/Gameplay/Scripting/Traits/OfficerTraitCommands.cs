using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay.Scripting.Traits
{
    /// <summary>
    /// Activates trait-owned scripts using the shared scripting components.
    /// </summary>
    public sealed class OfficerTraitCommands
    {
        private const string _officerBinding = "$officer";
        private readonly GameRoot _game;
        private readonly GameActionCommands _actions;
        private readonly OfficerTraitDefinition[] _traits;
        private readonly Dictionary<
            Type,
            List<(OfficerTraitDefinition Trait, OfficerTraitBehavior Behavior, GameTrigger Trigger)>
        > _triggers = new();

        /// <summary>
        /// Validates trait scripts and indexes their result triggers for one game session.
        /// </summary>
        /// <param name="game">The authoritative game whose officers hold traits.</param>
        /// <param name="gameData">The active content catalog supplying trait definitions.</param>
        /// <param name="actions">The shared executor for ordered gameplay actions.</param>
        public OfficerTraitCommands(
            GameRoot game,
            GameDataCatalog gameData,
            GameActionCommands actions
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _traits = (gameData ?? throw new ArgumentNullException(nameof(gameData)))
                .OfficerTraits.Where(trait => trait.Behaviors.Count > 0)
                .ToArray();
            foreach (OfficerTraitDefinition trait in _traits)
            {
                HashSet<string> ids = new(StringComparer.Ordinal);
                foreach (OfficerTraitBehavior behavior in trait.Behaviors)
                {
                    ValidateBehavior(trait, behavior, ids);
                    foreach (GameTrigger trigger in behavior.Triggers)
                    {
                        Type resultType = GameTriggerMatcher.GetResultType(trigger);
                        if (!_triggers.TryGetValue(resultType, out var entries))
                        {
                            entries = new();
                            _triggers.Add(resultType, entries);
                        }
                        entries.Add((trait, behavior, trigger));
                    }
                }
            }
        }

        /// <summary>
        /// Runs tick behaviors in officer identity, trait-definition, and authored behavior order.
        /// </summary>
        /// <returns>The factual results produced by eligible trait holders.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new();
            if (_traits.Length == 0)
                return results;
            foreach (Officer officer in GetOfficers())
            foreach (OfficerTraitDefinition trait in _traits)
            foreach (OfficerTraitBehavior behavior in trait.Behaviors)
                if (behavior.EveryTick)
                    results.AddRange(Activate(officer, trait, behavior, null, null));
            return results;
        }

        /// <summary>
        /// Runs each matching behavior once per officer and incoming result, even if several triggers match.
        /// </summary>
        /// <param name="incoming">The result wave delivered by the session's result bus.</param>
        /// <returns>The reactions produced by eligible trait holders.</returns>
        public List<GameResult> ProcessResults(IReadOnlyList<GameResult> incoming)
        {
            List<GameResult> results = new();
            if (incoming == null || _triggers.Count == 0)
                return results;
            foreach (GameResult result in incoming)
            {
                if (result == null)
                    continue;
                var candidates = _triggers
                    .Where(pair => pair.Key.IsInstanceOfType(result))
                    .SelectMany(pair => pair.Value)
                    .Where(entry => GameTriggerMatcher.Matches(entry.Trigger, result))
                    .ToArray();
                if (candidates.Length == 0)
                    continue;
                foreach (Officer officer in GetOfficers())
                {
                    HashSet<OfficerTraitBehavior> processed = new();
                    foreach (var entry in candidates)
                        if (processed.Add(entry.Behavior))
                            results.AddRange(
                                Activate(
                                    officer,
                                    entry.Trait,
                                    entry.Behavior,
                                    entry.Trigger,
                                    result
                                )
                            );
                }
            }
            return results;
        }

        /// <summary>
        /// Returns authoritative officers in an order independent of container traversal and reload.
        /// </summary>
        /// <returns>The registered officers, including those whose scripts opt into inactivity.</returns>
        private Officer[] GetOfficers() =>
            _game
                .GetRegisteredSceneNodesByType<Officer>(includeDisabled: true)
                .OrderBy(officer => officer.InstanceID, StringComparer.Ordinal)
                .ToArray();

        /// <summary>
        /// Rechecks trait ownership and activity before resolving this activation's bindings and conditions.
        /// </summary>
        /// <param name="officer">The officer holding the trait.</param>
        /// <param name="trait">The content definition supplying the behavior.</param>
        /// <param name="behavior">The script selected for this activation.</param>
        /// <param name="trigger">The matching result trigger, or null during tick processing.</param>
        /// <param name="result">The triggering result, or null during tick processing.</param>
        /// <returns>The results produced when every condition passes.</returns>
        private List<GameResult> Activate(
            Officer officer,
            OfficerTraitDefinition trait,
            OfficerTraitBehavior behavior,
            GameTrigger trigger,
            GameResult result
        )
        {
            if (
                !officer.HasActiveTrait(trait.ID)
                || (!behavior.IncludeInactive && !officer.IsActive())
                || !ReferenceEquals(
                    officer,
                    _game.GetSceneNodeByInstanceID<Officer>(
                        officer.InstanceID,
                        includeDisabled: true
                    )
                )
            )
                return new();
            string sourceID =
                $"trait/{Uri.EscapeDataString(officer.InstanceID)}/{Uri.EscapeDataString(trait.ID)}/{Uri.EscapeDataString(behavior.ID)}";
            GameEvaluationContext context = new(
                sourceID,
                result,
                trigger,
                sourceKind: "Officer trait"
            );
            context.Bind(_officerBinding, officer);
            foreach (GameBinding binding in behavior.Bindings)
                GameBindingResolver.Bind(binding, _game, _game.Random, context);
            if (
                !behavior.Conditionals.All(condition =>
                    GameConditionEvaluator.IsMet(condition, _game, context)
                )
            )
                return new();
            return _actions.Execute(behavior.Actions, context);
        }

        /// <summary>
        /// Rejects ambiguous activation modes, duplicate identities, and conflicting binding contracts.
        /// </summary>
        /// <param name="trait">The trait whose authored behavior is validated.</param>
        /// <param name="behavior">The script to validate.</param>
        /// <param name="ids">The behavior identities already declared by this trait.</param>
        private static void ValidateBehavior(
            OfficerTraitDefinition trait,
            OfficerTraitBehavior behavior,
            HashSet<string> ids
        )
        {
            string source = $"Officer trait '{trait.ID}' behavior '{behavior?.ID}'";
            if (behavior == null || string.IsNullOrWhiteSpace(behavior.ID) || !ids.Add(behavior.ID))
                throw new InvalidOperationException($"{source} requires a unique behavior ID.");
            if (behavior.EveryTick == (behavior.Triggers.Count > 0))
                throw new InvalidOperationException(
                    $"{source} requires exactly one activation mode: EveryTick or Triggers."
                );
            if (behavior.Actions.Count == 0)
                throw new InvalidOperationException($"{source} requires at least one action.");
            Dictionary<string, Type> contract = null;
            foreach (GameTrigger trigger in behavior.Triggers.DefaultIfEmpty())
            {
                Dictionary<string, Type> aliases =
                    trigger == null
                        ? new(StringComparer.Ordinal)
                        : GameBindingResolver.ValidateTriggerBindings(trigger, source);
                if (!aliases.TryAdd(_officerBinding, typeof(Officer)))
                    throw new InvalidOperationException(
                        $"{source} cannot replace the reserved '{_officerBinding}' binding."
                    );
                GameBindingResolver.ValidateBindings(behavior.Bindings, aliases, source);
                if (
                    contract != null
                    && (
                        contract.Count != aliases.Count
                        || contract.Any(pair =>
                            !aliases.TryGetValue(pair.Key, out Type valueType)
                            || valueType != pair.Value
                        )
                    )
                )
                    throw new InvalidOperationException(
                        $"{source} must expose the same binding names and types on every trigger path."
                    );
                contract = aliases;
            }
        }
    }
}
