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
    /// Executes scripts owned by shared trait definitions through the common scripting pipeline.
    /// </summary>
    public sealed class OfficerTraitCommands
    {
        private readonly GameRoot _game;
        private readonly GameActionCommands _actions;
        private readonly GameResultBus _results;
        private readonly OfficerTraitDefinition[] _traits;

        /// <summary>
        /// Validates script contracts for the active catalog's trait definitions.
        /// </summary>
        /// <param name="game">The authoritative game addressed by authored scripts.</param>
        /// <param name="gameData">The active catalog supplying shared trait definitions.</param>
        /// <param name="actions">The common gameplay action executor.</param>
        /// <param name="results">The session's reaction pipeline and causal context.</param>
        public OfficerTraitCommands(
            GameRoot game,
            GameDataCatalog gameData,
            GameActionCommands actions,
            GameResultBus results
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _results = results ?? throw new ArgumentNullException(nameof(results));
            OfficerTraitDefinition[] definitions = (
                gameData ?? throw new ArgumentNullException(nameof(gameData))
            ).OfficerTraits;
            foreach (OfficerTraitDefinition trait in definitions)
                ValidateScript(trait);
            _traits = definitions.Where(trait => trait.Actions.Count > 0).ToArray();
        }

        /// <summary>
        /// Evaluates unspecialized trait scripts once before AI planning, in catalog order.
        /// </summary>
        /// <returns>The factual results produced by scripts without result triggers.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new();
            foreach (OfficerTraitDefinition trait in _traits)
                if (trait.Triggers.Count == 0)
                    results.AddRange(Activate(trait, null, null));
            return results;
        }

        /// <summary>
        /// Gives each triggered definition one activation opportunity per matching result.
        /// </summary>
        /// <param name="incoming">The result wave delivered by the session.</param>
        /// <returns>The script reactions, in input-result and catalog order.</returns>
        public List<GameResult> ProcessResults(IReadOnlyList<GameResult> incoming)
        {
            List<GameResult> results = new();
            if (incoming == null)
                return results;
            foreach (GameResult result in incoming)
            {
                if (result == null)
                    continue;
                foreach (OfficerTraitDefinition trait in _traits)
                {
                    GameTrigger trigger = trait.Triggers.FirstOrDefault(candidate =>
                        GameTriggerMatcher.Matches(candidate, result)
                    );
                    if (trigger != null)
                        results.AddRange(Activate(trait, trigger, result));
                }
            }
            return results;
        }

        /// <summary>
        /// Resolves one definition's bindings, checks its conditions, and executes its ordered actions.
        /// </summary>
        /// <param name="trait">The shared definition owning this script.</param>
        /// <param name="trigger">The matched result trigger, or null for the tick phase.</param>
        /// <param name="result">The triggering result, or null for the tick phase.</param>
        /// <returns>The resulting facts; self-caused reaction chains are suppressed.</returns>
        private List<GameResult> Activate(
            OfficerTraitDefinition trait,
            GameTrigger trigger,
            GameResult result
        )
        {
            return _results.RunReaction(
                trait,
                result,
                () =>
                {
                    GameEvaluationContext context = new(
                        $"trait/{Uri.EscapeDataString(trait.ID)}",
                        result,
                        trigger,
                        sourceKind: "Officer trait"
                    );
                    foreach (GameBinding binding in trait.Bindings)
                        GameBindingResolver.Bind(binding, _game, _game.Random, context);
                    if (
                        !trait.Conditionals.All(condition =>
                            GameConditionEvaluator.IsMet(condition, _game, context)
                        )
                    )
                        return new List<GameResult>();
                    return _actions.Execute(trait.Actions, context);
                }
            );
        }

        /// <summary>
        /// Validates every trigger path against the same authored binding contract.
        /// </summary>
        /// <param name="trait">The trait whose script contract is checked.</param>
        private static void ValidateScript(OfficerTraitDefinition trait)
        {
            string source = $"Officer trait '{trait.ID}'";
            if (trait.Actions.Count == 0)
            {
                if (
                    trait.Triggers.Count > 0
                    || trait.Bindings.Count > 0
                    || trait.Conditionals.Count > 0
                )
                    throw new InvalidOperationException(
                        $"{source} requires actions when script inputs are configured."
                    );
                return;
            }
            Dictionary<string, Type> contract = null;
            foreach (GameTrigger trigger in trait.Triggers.DefaultIfEmpty())
            {
                Dictionary<string, Type> aliases =
                    trigger == null
                        ? new(StringComparer.Ordinal)
                        : GameBindingResolver.ValidateTriggerBindings(trigger, source);
                if (trait.Triggers.Count > 0 && trigger == null)
                    throw new InvalidOperationException($"{source} contains a null trigger.");
                GameBindingResolver.ValidateBindings(trait.Bindings, aliases, source);
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
