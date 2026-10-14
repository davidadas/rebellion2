using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Routes arrival and ownership result batches to headquarters operations.
    /// </summary>
    public sealed class HeadquartersObserver : IResultObserver, IDisposable
    {
        private readonly GameRoot _game;
        private readonly UnitCommands _unitCommands;
        private IDisposable[] _subscriptions;

        /// <summary>
        /// Creates the headquarters result observer.
        /// </summary>
        /// <param name="game">The active game graph containing headquarters state.</param>
        public HeadquartersObserver(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _unitCommands = new UnitCommands(game);
        }

        /// <summary>
        /// Registers arrival and ownership callbacks with the result bus.
        /// </summary>
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
                results.Subscribe<GameObjectDestroyedResult>(HandleResults),
                results.Subscribe<BombardmentResult>(HandleResults),
            };
        }

        /// <summary>
        /// Stops receiving arrivals and ownership changes.
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
        /// Converts generic destruction facts for mobile headquarters into headquarters-loss facts.
        /// </summary>
        /// <param name="results">The completed unit destructions.</param>
        /// <returns>The headquarters losses represented by the destruction batch.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameObjectDestroyedResult> results)
        {
            List<GameResult> reactions = new();
            foreach (
                GameObjectDestroyedResult result in results
                    ?? Array.Empty<GameObjectDestroyedResult>()
            )
            {
                if (
                    result?.DestroyedObject
                    is not Building { BuildingType: BuildingType.Headquarters } headquarters
                )
                    continue;

                AddDestroyedResult(
                    headquarters,
                    result.Context as Planet,
                    FindFaction(headquarters.OwnerInstanceID),
                    FindAttacker(result.DestroyedBy, result.Context as Planet),
                    result.Tick,
                    reactions
                );
            }
            return reactions;
        }

        /// <summary>
        /// Converts headquarters destruction during bombardment into a headquarters-loss fact.
        /// </summary>
        /// <param name="results">The completed bombardments.</param>
        /// <returns>The headquarters losses represented by the bombardment batch.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<BombardmentResult> results)
        {
            List<GameResult> reactions = new();
            foreach (BombardmentResult result in results ?? Array.Empty<BombardmentResult>())
            {
                if (result?.HeadquartersDestroyed != true)
                    continue;

                AddDestroyedResult(
                    result.DestroyedHeadquarters,
                    result.Planet,
                    FindFaction(result.DefenderOwnerInstanceID),
                    result.AttackingFaction ?? FindFaction(result.AttackerOwnerInstanceID),
                    result.Tick,
                    reactions
                );
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
            faction.HeadquartersDestroyedByFactionInstanceID = null;
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

            planet.IsHeadquarters = false;
            previousOwner.HQInstanceID = null;
            previousOwner.HeadquartersDestroyedByFactionInstanceID = newOwner.InstanceID;
            _unitCommands.Destroy(headquarters);
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
        /// Adds a headquarters-loss fact when both participating factions are known and opposed.
        /// </summary>
        /// <param name="headquarters">The destroyed headquarters building, when one existed.</param>
        /// <param name="planet">The planet where the headquarters was destroyed.</param>
        /// <param name="defender">The faction that lost the headquarters.</param>
        /// <param name="attacker">The faction responsible for the destruction.</param>
        /// <param name="tick">The tick when destruction occurred.</param>
        /// <param name="reactions">The collection receiving the headquarters-loss fact.</param>
        private static void AddDestroyedResult(
            Building headquarters,
            Planet planet,
            Faction defender,
            Faction attacker,
            int tick,
            ICollection<GameResult> reactions
        )
        {
            if (defender == null || attacker == null || attacker == defender)
                return;

            defender.HeadquartersDestroyedByFactionInstanceID = attacker.InstanceID;
            reactions.Add(
                new HeadquartersDestroyedResult
                {
                    Headquarters = headquarters,
                    Planet = planet,
                    Defender = defender,
                    Attacker = attacker,
                    Tick = tick,
                }
            );
        }

        /// <summary>
        /// Resolves the faction responsible for a destruction fact.
        /// </summary>
        /// <param name="destroyedBy">The entity directly credited with the destruction.</param>
        /// <param name="planet">The destruction location used when direct attribution is absent.</param>
        /// <returns>The responsible faction, or null when it cannot be determined.</returns>
        private Faction FindAttacker(IGameEntity destroyedBy, Planet planet)
        {
            if (destroyedBy is Faction faction)
                return faction;
            if (destroyedBy is ISceneNode sceneNode)
                return FindFaction(sceneNode.OwnerInstanceID);
            return FindFaction(planet?.OwnerInstanceID);
        }

        /// <summary>
        /// Finds a faction by its stable instance identifier without throwing for an absent owner.
        /// </summary>
        /// <param name="instanceId">The faction identifier.</param>
        /// <returns>The matching faction, or null.</returns>
        private Faction FindFaction(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;
            return _game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceId);
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
