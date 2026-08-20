using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Systems
{
    /// <summary>
    /// Owns mobile-headquarters relocation, arrival, and destruction policy.
    /// </summary>
    public sealed class HeadquartersSystem
        : IGameResultHandler<UnitArrivedResult>,
            IGameResultHandler<PlanetOwnershipChangedResult>
    {
        private readonly GameRoot _game;
        private readonly MovementSystem _movement;

        public HeadquartersSystem(GameRoot game, MovementSystem movement)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _movement.SetCompletedBuildingMovementPolicy(CanMove);
        }

        /// <summary>
        /// Determines whether a completed building is the owning faction's active mobile headquarters.
        /// </summary>
        /// <param name="building">The completed building requesting movement.</param>
        /// <returns>True when headquarters policy permits the building to move.</returns>
        private bool CanMove(Building building)
        {
            if (building?.BuildingType != BuildingType.Headquarters || building.Movement != null)
                return false;

            Faction faction = _game.GetFactionByOwnerInstanceID(building.OwnerInstanceID);
            Planet origin = building.GetParentOfType<Planet>();
            return faction?.Settings?.Headquarters?.IsMobile == true
                && origin != null
                && faction.HQInstanceID == origin.InstanceID;
        }

        /// <summary>
        /// Determines whether a headquarters building may relocate to a friendly planet.
        /// </summary>
        /// <param name="headquarters">The headquarters building to move.</param>
        /// <param name="destination">The requested destination planet.</param>
        /// <returns>True when the relocation order is valid.</returns>
        public bool CanRelocate(Building headquarters, Planet destination)
        {
            if (
                !CanMove(headquarters)
                || destination?.IsDestroyed != false
                || destination?.IsColonized != true
            )
                return false;

            Faction faction = _game.GetFactionByOwnerInstanceID(headquarters.OwnerInstanceID);
            Planet origin = headquarters.GetParentOfType<Planet>();
            return destination != origin
                && destination.OwnerInstanceID == faction.InstanceID
                && destination.CanAcceptChild(headquarters);
        }

        /// <summary>
        /// Starts a validated headquarters relocation and clears its planetary marker in transit.
        /// </summary>
        /// <param name="headquarters">The headquarters building to move.</param>
        /// <param name="destination">The requested destination planet.</param>
        /// <returns>True when the movement order was accepted.</returns>
        public bool TryRelocate(Building headquarters, Planet destination)
        {
            if (!CanRelocate(headquarters, destination))
                return false;

            Planet origin = headquarters.GetParentOfType<Planet>();
            bool moved = _movement.TryRequestMove(
                new List<ISceneNode> { headquarters },
                destination,
                headquarters.OwnerInstanceID
            );
            if (!moved || headquarters.Movement == null)
                return false;

            origin.IsHeadquarters = false;
            Faction faction = _game.GetFactionByOwnerInstanceID(headquarters.OwnerInstanceID);
            faction.HQInstanceID = null;
            return true;
        }

        /// <summary>
        /// Assigns an arriving headquarters building to its destination planet.
        /// </summary>
        /// <param name="results">The unit arrivals produced by the movement system.</param>
        /// <returns>No additional game results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<UnitArrivedResult> results)
        {
            foreach (UnitArrivedResult result in results ?? Array.Empty<UnitArrivedResult>())
            {
                if (result?.Unit is not Building { BuildingType: BuildingType.Headquarters } hq)
                    continue;

                Faction faction = _game.GetFactionByOwnerInstanceID(hq.OwnerInstanceID);
                Planet destination = result.Destination;
                if (faction?.Settings?.Headquarters?.IsMobile != true || destination == null)
                    continue;

                Planet previous = _game.GetSceneNodeByInstanceID<Planet>(faction.HQInstanceID);
                if (previous != null)
                    previous.IsHeadquarters = false;

                destination.IsHeadquarters = true;
                faction.HQInstanceID = destination.InstanceID;
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Destroys a mobile headquarters when an enemy takes control of its planet.
        /// </summary>
        /// <param name="results">The completed planetary ownership changes.</param>
        /// <returns>Headquarters destruction results caused by hostile captures.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                HeadquartersCapturedResult captured = UpdateFixedHeadquartersMarker(result);
                if (captured != null)
                    reactions.Add(captured);

                Faction defender = result?.PreviousOwner;
                Faction attacker = result?.NewOwner;
                Planet planet = result?.Planet;
                HeadquartersSettings settings = defender?.Settings?.Headquarters;
                if (
                    settings?.IsMobile != true
                    || attacker == null
                    || attacker == defender
                    || planet == null
                )
                    continue;

                Building headquarters = planet
                    .GetChildren<Building>(_ => true, recurse: false)
                    .SingleOrDefault(building =>
                        building.BuildingType == BuildingType.Headquarters
                    );
                if (headquarters == null)
                    continue;

                _game.DeleteNode(headquarters);

                planet.IsHeadquarters = false;
                defender.HQInstanceID = null;
                reactions.Add(
                    new HeadquartersDestroyedResult
                    {
                        Headquarters = headquarters,
                        Planet = planet,
                        Defender = defender,
                        Attacker = attacker,
                        Tick = _game.CurrentTick,
                    }
                );
            }

            return reactions;
        }

        /// <summary>
        /// Clears or restores a fixed headquarters marker when its configured planet changes hands.
        /// The faction's headquarters location remains configured so recapture can restore it.
        /// </summary>
        /// <param name="result">The planetary ownership change to apply.</param>
        private HeadquartersCapturedResult UpdateFixedHeadquartersMarker(
            PlanetOwnershipChangedResult result
        )
        {
            Planet planet = result?.Planet;
            if (planet == null)
                return null;

            Faction fixedHeadquartersFaction = _game
                .GetFactions()
                .SingleOrDefault(faction =>
                    faction.Settings?.Headquarters?.IsMobile != true
                    && faction.HQInstanceID == planet.InstanceID
                );
            if (fixedHeadquartersFaction == null)
                return null;

            planet.IsHeadquarters = result.NewOwner == fixedHeadquartersFaction;
            if (
                result.PreviousOwner != fixedHeadquartersFaction
                || result.NewOwner == null
                || result.NewOwner == fixedHeadquartersFaction
            )
                return null;

            return new HeadquartersCapturedResult
            {
                Planet = planet,
                Defender = fixedHeadquartersFaction,
                Attacker = result.NewOwner,
                Tick = _game.CurrentTick,
            };
        }
    }
}
