using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
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
            return TryRelocate(headquarters, destination, Array.Empty<ISceneNode>());
        }

        /// <summary>
        /// Starts a validated headquarters relocation with accompanying owned units and clears its
        /// planetary marker in transit.
        /// </summary>
        /// <param name="headquarters">The headquarters building to move.</param>
        /// <param name="destination">The requested destination planet.</param>
        /// <param name="accompanyingUnits">Owned units that must depart with the headquarters.</param>
        /// <returns>True when the complete movement order was accepted.</returns>
        public bool TryRelocate(
            Building headquarters,
            Planet destination,
            IReadOnlyList<ISceneNode> accompanyingUnits
        )
        {
            if (
                accompanyingUnits == null
                || !_queries.TryResolveRelocation(
                    headquarters,
                    destination,
                    out Building liveHeadquarters,
                    out Planet liveDestination
                )
            )
                return false;

            Planet origin = liveHeadquarters.GetParentOfType<Planet>();
            List<ISceneNode> relocationGroup = new List<ISceneNode> { liveHeadquarters };
            relocationGroup.AddRange(accompanyingUnits);
            bool moved = _movement.TryRequestMove(
                relocationGroup,
                liveDestination,
                liveHeadquarters.OwnerInstanceID
            );
            if (!moved || liveHeadquarters.Movement == null)
                return false;

            origin.IsHeadquarters = false;
            Faction faction = _game.GetFactionByOwnerInstanceID(liveHeadquarters.OwnerInstanceID);
            faction.HQInstanceID = null;
            return true;
        }
    }
}
