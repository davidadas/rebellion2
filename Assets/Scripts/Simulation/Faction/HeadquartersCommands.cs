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
    /// Relocates headquarters and applies their arrival and ownership changes.
    /// </summary>
    public sealed class HeadquartersCommands
    {
        private readonly GameRoot _game;
        private readonly MovementCommands _movement;
        private readonly HeadquartersQueries _queries;

        /// <summary>
        /// Creates headquarters operations.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="movement">The movement implementation that executes relocation.</param>
        /// <param name="queries">The active game's headquarters eligibility queries.</param>
        public HeadquartersCommands(
            GameRoot game,
            MovementCommands movement,
            HeadquartersQueries queries
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Starts a validated headquarters relocation and clears its planetary marker in transit.
        /// </summary>
        /// <param name="headquarters">The headquarters building to move.</param>
        /// <param name="destination">The requested destination planet.</param>
        /// <returns>True when the movement order was accepted.</returns>
        public bool TryRelocate(Building headquarters, Planet destination)
        {
            if (!_queries.CanRelocate(headquarters, destination))
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
        /// Assigns an arriving mobile headquarters to its destination and clears its previous marker.
        /// </summary>
        /// <param name="headquarters">The arriving headquarters building.</param>
        /// <param name="destination">The arrival planet.</param>
        public void Arrive(Building headquarters, Planet destination)
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

        /// <summary>
        /// Updates fixed headquarters markers and destroys mobile headquarters captured on the planet.
        /// </summary>
        /// <param name="planet">The planet whose ownership changed.</param>
        /// <param name="previousOwner">The faction that previously controlled the planet.</param>
        /// <param name="newOwner">The faction now controlling the planet, or null for neutrality.</param>
        /// <returns>The headquarters consequences of this ownership change.</returns>
        public List<GameResult> UpdateOwnership(
            Planet planet,
            Faction previousOwner,
            Faction newOwner
        )
        {
            List<GameResult> reactions = new();
            HeadquartersCapturedResult captured = UpdateFixedHeadquartersMarker(
                planet,
                previousOwner,
                newOwner
            );
            if (captured != null)
                reactions.Add(captured);

            HeadquartersSettings settings = previousOwner?.Settings?.Headquarters;
            if (
                settings?.IsMobile != true
                || newOwner == null
                || newOwner == previousOwner
                || planet == null
            )
                return reactions;

            Building headquarters = planet
                .GetChildren<Building>()
                .SingleOrDefault(building => building.BuildingType == BuildingType.Headquarters);
            if (headquarters == null)
                return reactions;

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
                    Tick = _game.CurrentTick,
                }
            );
            return reactions;
        }

        /// <summary>
        /// Clears or restores a fixed headquarters marker when its configured planet changes hands.
        /// The faction's headquarters location remains configured so recapture can restore it.
        /// </summary>
        /// <param name="planet">The planet whose ownership changed.</param>
        /// <param name="previousOwner">The previous controller.</param>
        /// <param name="newOwner">The new controller, or null for neutrality.</param>
        /// <returns>A capture result for an enemy takeover, or null.</returns>
        private HeadquartersCapturedResult UpdateFixedHeadquartersMarker(
            Planet planet,
            Faction previousOwner,
            Faction newOwner
        )
        {
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

            planet.IsHeadquarters = newOwner == fixedHeadquartersFaction;
            if (
                previousOwner != fixedHeadquartersFaction
                || newOwner == null
                || newOwner == fixedHeadquartersFaction
            )
                return null;

            return new HeadquartersCapturedResult
            {
                Planet = planet,
                Defender = fixedHeadquartersFaction,
                Attacker = newOwner,
                Tick = _game.CurrentTick,
            };
        }
    }
}
