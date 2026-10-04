using System;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates headquarters movement eligibility without changing the game graph.
    /// </summary>
    public sealed class HeadquartersQueries
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates headquarters eligibility queries for the active game.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        public HeadquartersQueries(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
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
        /// Determines whether a completed building is the owning faction's active mobile headquarters.
        /// </summary>
        /// <param name="building">The completed building requesting movement.</param>
        /// <returns>True when headquarters policy permits the building to move.</returns>
        internal bool CanMove(Building building)
        {
            if (building?.BuildingType != BuildingType.Headquarters || building.Movement != null)
                return false;

            Faction faction = _game.GetFactionByOwnerInstanceID(building.OwnerInstanceID);
            Planet origin = building.GetParentOfType<Planet>();
            return faction?.Settings?.Headquarters?.IsMobile == true
                && origin != null
                && faction.HQInstanceID == origin.InstanceID;
        }
    }
}
