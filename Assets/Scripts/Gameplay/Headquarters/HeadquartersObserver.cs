using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Routes headquarters lifecycle result batches to headquarters operations.
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

        /// <summary>
        /// Registers headquarters lifecycle callbacks with the result bus.
        /// </summary>
        /// <param name="results">The bus that delivers headquarters lifecycle changes.</param>
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
                results.Subscribe<GameObjectDestroyedResult>(HandleResults),
                results.Subscribe<UnitOwnershipChangedResult>(HandleResults),
            };
        }

        /// <summary>
        /// Stops receiving headquarters lifecycle changes.
        /// </summary>
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

        /// <summary>
        /// Applies mobile headquarters destruction from sabotage, arrival rejection, and other
        /// unit lifecycle operations.
        /// </summary>
        /// <param name="results">The completed unit destructions.</param>
        /// <returns>The headquarters losses caused by the destructions.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameObjectDestroyedResult> results)
        {
            List<GameResult> reactions = new();
            foreach (
                GameObjectDestroyedResult result in results
                    ?? Array.Empty<GameObjectDestroyedResult>()
            )
            {
                HeadquartersDestroyedResult destroyed = ApplyDestruction(result);
                if (destroyed != null)
                    reactions.Add(destroyed);
            }
            return reactions;
        }

        /// <summary>
        /// Applies mobile headquarters losses caused by direct unit ownership transfers.
        /// </summary>
        /// <param name="results">The completed unit ownership changes.</param>
        /// <returns>The headquarters losses caused by the transfers.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<UnitOwnershipChangedResult> results)
        {
            List<GameResult> reactions = new();
            foreach (
                UnitOwnershipChangedResult result in results
                    ?? Array.Empty<UnitOwnershipChangedResult>()
            )
            {
                HeadquartersCapturedResult captured = ApplyUnitOwnershipChange(result);
                if (captured != null)
                    reactions.Add(captured);
            }
            return reactions;
        }

        /// <summary>
        /// Applies the headquarters marker for an arriving mobile headquarters.
        /// </summary>
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
            faction.HasLostHeadquarters = false;
        }

        /// <summary>
        /// Marks the owner of a destroyed mobile headquarters as having lost it.
        /// </summary>
        /// <param name="result">The completed destruction.</param>
        /// <returns>The headquarters destruction result, or null when the unit was not an active headquarters.</returns>
        private HeadquartersDestroyedResult ApplyDestruction(GameObjectDestroyedResult result)
        {
            if (result?.DestroyedObject is not Building headquarters)
                return null;

            Faction owner = _game
                .GetFactions()
                .FirstOrDefault(faction =>
                    faction.InstanceID == headquarters.OwnerInstanceID
                    && IsMobileHeadquarters(headquarters, faction)
                );
            if (owner is not { HasLostHeadquarters: false })
                return null;

            Planet planet = result.Context as Planet ?? headquarters.GetParentOfType<Planet>();
            MarkHeadquartersLost(owner, planet);
            Faction attacker = result.DestroyedBy is ISceneNode destroyer
                ? _game
                    .GetFactions()
                    .FirstOrDefault(faction => faction.InstanceID == destroyer.GetOwnerInstanceID())
                : null;
            return new HeadquartersDestroyedResult
            {
                Headquarters = headquarters,
                Planet = planet,
                Defender = owner,
                Attacker = attacker,
                Tick = result.Tick,
            };
        }

        /// <summary>
        /// Marks a mobile headquarters as lost when ownership of its building changes directly.
        /// </summary>
        /// <param name="result">The completed unit ownership change.</param>
        /// <returns>The headquarters capture result, or null when the unit was not an active headquarters.</returns>
        private HeadquartersCapturedResult ApplyUnitOwnershipChange(
            UnitOwnershipChangedResult result
        )
        {
            if (
                result?.Unit is not Building headquarters
                || result.PreviousOwner == null
                || result.NewOwner == null
                || result.PreviousOwner == result.NewOwner
                || result.PreviousOwner.HasLostHeadquarters
                || !IsMobileHeadquarters(headquarters, result.PreviousOwner)
            )
                return null;

            Planet planet = headquarters.GetParentOfType<Planet>();
            MarkHeadquartersLost(result.PreviousOwner, planet);
            return new HeadquartersCapturedResult
            {
                Planet = planet,
                Defender = result.PreviousOwner,
                Attacker = result.NewOwner,
                Tick = result.Tick,
            };
        }

        /// <summary>
        /// Determines whether a building represents a faction's mobile headquarters.
        /// </summary>
        /// <param name="building">The headquarters candidate.</param>
        /// <param name="faction">The faction whose configuration is evaluated.</param>
        /// <returns>True when the building matches the faction's mobile headquarters.</returns>
        private static bool IsMobileHeadquarters(Building building, Faction faction)
        {
            return building?.BuildingType == BuildingType.Headquarters
                && faction?.Settings?.Headquarters?.IsMobile == true
                && (
                    string.IsNullOrEmpty(faction.Settings.Headquarters.FacilityTypeID)
                    || building.TypeID == faction.Settings.Headquarters.FacilityTypeID
                );
        }

        /// <summary>
        /// Clears a faction's headquarters marker and records its loss.
        /// </summary>
        /// <param name="faction">The faction losing its headquarters.</param>
        /// <param name="location">The headquarters location, when available.</param>
        private void MarkHeadquartersLost(Faction faction, Planet location)
        {
            Planet markedPlanet = _game.GetSceneNodeByInstanceID<Planet>(faction.HQInstanceID);
            if (markedPlanet != null)
                markedPlanet.IsHeadquarters = false;
            if (location != null)
                location.IsHeadquarters = false;

            faction.HQInstanceID = null;
            faction.HasLostHeadquarters = true;
        }

        /// <summary>
        /// Applies headquarters consequences for one completed ownership change.
        /// </summary>
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
            previousOwner.HasLostHeadquarters = true;
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

        /// <summary>
        /// Updates a fixed headquarters marker after its configured planet changes hands.
        /// </summary>
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
            headquartersFaction.HasLostHeadquarters = newOwner != headquartersFaction;
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
