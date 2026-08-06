using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Systems
{
    /// <summary>
    /// Validates mobile-headquarters orders and updates the faction headquarters on arrival.
    /// </summary>
    public sealed class HeadquartersSystem : IGameResultHandler<UnitArrivedResult>
    {
        private readonly GameRoot _game;
        private readonly MovementSystem _movement;

        public HeadquartersSystem(GameRoot game, MovementSystem movement)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
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
                headquarters?.BuildingType != BuildingType.Headquarters
                || headquarters.Movement != null
                || destination?.IsDestroyed != false
                || destination?.IsColonized != true
            )
                return false;

            Faction faction = _game.GetFactionByOwnerInstanceID(headquarters.OwnerInstanceID);
            Planet origin = headquarters.GetParentOfType<Planet>();
            return faction?.Settings?.Headquarters?.IsMobile == true
                && headquarters.TypeID == faction.Settings.Headquarters.FacilityTypeID
                && origin != null
                && faction.HQInstanceID == origin.InstanceID
                && destination != origin
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
                if (faction == null || destination == null)
                    continue;

                Planet previous = _game.GetSceneNodeByInstanceID<Planet>(faction.HQInstanceID);
                if (previous != null)
                    previous.IsHeadquarters = false;

                destination.IsHeadquarters = true;
                faction.HQInstanceID = destination.InstanceID;
            }

            return new List<GameResult>();
        }
    }
}
