using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Selects newly blockaded destinations and requests their inbound-unit reactions.
    /// </summary>
    public sealed class MovementObserver : IResultObserver, IDisposable
    {
        private readonly MovementCommands _commands;
        private IDisposable[] _subscriptions;

        /// <summary>
        /// Creates the blockade result observer.
        /// </summary>
        /// <param name="commands">The movement operations that handle inbound units.</param>
        public MovementObserver(MovementCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>Registers the blockade callback with the result bus.</summary>
        /// <param name="results">The bus that delivers blockade changes.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscriptions != null)
                throw new InvalidOperationException("Movement observer is already connected.");
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<BlockadeChangedResult>(HandleResults),
                results.Subscribe<GameObjectDestroyedResult>(HandleResults),
                results.Subscribe<GameObjectScrappedResult>(HandleResults),
            };
        }

        /// <summary>Stops receiving blockade changes.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
                subscription.Dispose();
        }

        /// <summary>
        /// Applies movement reactions to newly started blockades.
        /// </summary>
        /// <param name="results">The blockade changes to inspect.</param>
        /// <returns>The movement and destruction results caused by blockade starts.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<BlockadeChangedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            HashSet<string> handledPlanets = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlockadeChangedResult result in results)
            {
                if (
                    result?.Blockaded != true
                    || result.Planet == null
                    || result.BlockadingFleet == null
                    || !result.Planet.IsBlockaded()
                    || !handledPlanets.Add(result.Planet.InstanceID)
                )
                    continue;

                _commands.HandleBlockadeStarted(result, reactions);
            }

            return reactions;
        }

        /// <summary>
        /// Relocates eligible occupants after a destruction result removes their capital ship.
        /// </summary>
        /// <param name="results">The destruction results to inspect.</param>
        /// <returns>Movement and follow-up destruction results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameObjectDestroyedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            HashSet<string> destroyedInstanceIds = results
                .Select(result => result?.DestroyedObject?.InstanceID)
                .Where(instanceId => !string.IsNullOrEmpty(instanceId))
                .ToHashSet(StringComparer.Ordinal);
            foreach (
                GameObjectDestroyedResult result in results.Where(result =>
                    result?.DestroyedObject is CapitalShip
                )
            )
            {
                _commands.RelocateRemovedCapitalShipOccupants(
                    (CapitalShip)result.DestroyedObject,
                    result.Context,
                    destroyedInstanceIds,
                    recoverStarfighters: result.Reason == UnitDestructionReason.Combat,
                    results: reactions
                );
            }

            return reactions;
        }

        /// <summary>
        /// Relocates eligible occupants after intentional scrapping removes their capital ship.
        /// </summary>
        /// <param name="results">The scrapping results to inspect.</param>
        /// <returns>Movement and follow-up destruction results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameObjectScrappedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            HashSet<string> scrappedInstanceIds = results
                .Select(result => result?.ScrappedObject?.InstanceID)
                .Where(instanceId => !string.IsNullOrEmpty(instanceId))
                .ToHashSet(StringComparer.Ordinal);
            foreach (
                GameObjectScrappedResult result in results.Where(result =>
                    result?.ScrappedObject is CapitalShip
                )
            )
            {
                _commands.RelocateRemovedCapitalShipOccupants(
                    (CapitalShip)result.ScrappedObject,
                    result.Context,
                    scrappedInstanceIds,
                    recoverStarfighters: false,
                    results: reactions
                );
            }

            return reactions;
        }
    }
}
