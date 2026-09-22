using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>Routes garrison and support changes to planetary control operations.</summary>
    public sealed class PlanetaryControlObserver : IDisposable
    {
        private readonly PlanetaryControlCommands _commands;
        private readonly IDisposable[] _subscriptions;

        /// <summary>Creates the planetary control listener.</summary>
        /// <param name="commands">The ownership and support operations.</param>
        /// <param name="results">The bus that delivers garrison and support changes.</param>
        public PlanetaryControlObserver(PlanetaryControlCommands commands, GameResultBus results)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<PlanetGarrisonChangedResult>(HandleResults),
                results.Subscribe<PopularSupportShiftResult>(HandleResults),
            };
        }

        /// <summary>Stops receiving garrison and support changes.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions)
                subscription.Dispose();
        }

        /// <summary>
        /// Reconciles planets whose active garrisons changed.
        /// </summary>
        /// <param name="results">The result batch to inspect.</param>
        /// <returns>Any ownership changes caused by the garrison changes.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetGarrisonChangedResult> results)
        {
            List<GameResult> controlResults = new List<GameResult>();
            if (results == null)
                return controlResults;

            IEnumerable<Planet> affectedPlanets = results
                .Select(result => result.Planet)
                .Where(planet => planet != null)
                .Distinct();
            foreach (Planet planet in affectedPlanets)
                controlResults.AddRange(_commands.ReconcilePlanet(planet));

            return controlResults;
        }

        /// <summary>Applies support changes in their incoming batch order.</summary>
        /// <param name="results">The requested support shifts.</param>
        /// <returns>The completed stat and ownership changes.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PopularSupportShiftResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            foreach (PopularSupportShiftResult result in results)
            {
                if (result == null)
                    continue;

                reactions.AddRange(
                    _commands.ApplySupportShift(
                        result.Planet,
                        result.Faction,
                        result.Shift,
                        result.Tick
                    )
                );
            }

            return reactions;
        }
    }
}
