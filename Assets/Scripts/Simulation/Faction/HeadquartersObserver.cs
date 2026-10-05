using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Routes arrival and ownership result batches to headquarters operations.
    /// </summary>
    public sealed class HeadquartersObserver : IResultObserver, IDisposable
    {
        private readonly GameRoot _game;
        private IDisposable[] _subscriptions;

        /// <summary>
        /// Creates the headquarters result observer.
        /// </summary>
        /// <param name="game">The active game graph containing headquarters state.</param>
        public HeadquartersObserver(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>Registers arrival and ownership callbacks with the result bus.</summary>
        /// <param name="results">The bus that delivers arrivals and ownership changes.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscriptions != null)
                throw new InvalidOperationException("Headquarters observer is already connected.");
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<UnitArrivedResult>(HandleResults),
                results.Subscribe<PlanetOwnershipChangedResult>(HandleResults),
            };
        }

        /// <summary>Stops receiving arrivals and ownership changes.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
                subscription.Dispose();
        }

        /// <summary>
        /// Applies headquarters arrivals in their existing batch order.
        /// </summary>
        /// <param name="results">The completed unit arrivals.</param>
        /// <returns>No additional results, as headquarters arrival only updates its marker.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<UnitArrivedResult> results)
        {
            foreach (UnitArrivedResult result in results ?? Array.Empty<UnitArrivedResult>())
            {
                if (result?.Unit is Building headquarters)
                    ApplyArrival(headquarters, result.Destination);
            }
            return new List<GameResult>();
        }

        /// <summary>
        /// Applies ownership changes and collects headquarters consequences in their existing batch order.
        /// </summary>
        /// <param name="results">The completed planetary ownership changes.</param>
        /// <returns>The headquarters capture and destruction results for the whole batch.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            List<GameResult> reactions = new();
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                ApplyOwnershipChange(result, reactions);
            }
            return reactions;
        }

        /// <summary>Applies the headquarters marker for an arriving mobile headquarters.</summary>
        /// <param name="headquarters">The arriving headquarters building.</param>
        /// <param name="destination">The arrival planet.</param>
        private void ApplyArrival(Building headquarters, Planet destination)
        {
            if (headquarters?.BuildingType != BuildingType.Headquarters)
                return;

            Faction faction = _game.GetFactionByOwnerInstanceID(headquarters.OwnerInstanceID);
            if (faction?.Settings?.Headquarters?.IsMobile != true || destination == null)
                return;

            Planet previous = _game.GetSceneNodeByInstanceID<Planet>(faction.HQInstanceID);
            if (previous != null)
                previous.IsHeadquarters = false;

            destination.IsHeadquarters = true;
            faction.HQInstanceID = destination.InstanceID;
        }

        /// <summary>Applies headquarters consequences for one completed ownership change.</summary>
        /// <param name="result">The ownership change being observed.</param>
        /// <param name="reactions">The collection receiving headquarters results.</param>
        private void ApplyOwnershipChange(
            PlanetOwnershipChangedResult result,
            ICollection<GameResult> reactions
        )
        {
            Planet planet = result?.Planet;
            Faction previousOwner = result?.PreviousOwner;
            Faction newOwner = result?.NewOwner;
            HeadquartersCapturedResult captured = UpdateFixedHeadquartersMarker(
                planet,
                previousOwner,
                newOwner
            );
            if (captured != null)
                reactions.Add(captured);

            if (
                previousOwner?.Settings?.Headquarters?.IsMobile != true
                || newOwner == null
                || newOwner == previousOwner
                || planet == null
            )
                return;

            Building headquarters = planet
                .GetChildren<Building>()
                .SingleOrDefault(building => building.BuildingType == BuildingType.Headquarters);
            if (headquarters == null)
                return;

            _game.DeleteNode(headquarters);
            planet.IsHeadquarters = false;
            previousOwner.HQInstanceID = null;
            reactions.Add(
                new HeadquartersDestroyedResult
                {
                    Headquarters = headquarters,
                    Planet = planet,
                    Defender = previousOwner,
                    Attacker = newOwner,
                    Tick = result.Tick,
                }
            );
        }

        /// <summary>Updates a fixed headquarters marker after its configured planet changes hands.</summary>
        /// <param name="planet">The planet whose ownership changed.</param>
        /// <param name="previousOwner">The previous controller.</param>
        /// <param name="newOwner">The new controller, or null.</param>
        /// <returns>A capture result for an enemy takeover, or null.</returns>
        private HeadquartersCapturedResult UpdateFixedHeadquartersMarker(
            Planet planet,
            Faction previousOwner,
            Faction newOwner
        )
        {
            if (planet == null)
                return null;

            Faction headquartersFaction = _game
                .GetFactions()
                .SingleOrDefault(faction =>
                    faction.Settings?.Headquarters?.IsMobile != true
                    && faction.HQInstanceID == planet.InstanceID
                );
            if (headquartersFaction == null)
                return null;

            planet.IsHeadquarters = newOwner == headquartersFaction;
            if (
                previousOwner != headquartersFaction
                || newOwner == null
                || newOwner == headquartersFaction
            )
                return null;

            return new HeadquartersCapturedResult
            {
                Planet = planet,
                Defender = headquartersFaction,
                Attacker = newOwner,
                Tick = _game.CurrentTick,
            };
        }
    }
}
