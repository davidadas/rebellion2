using System;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay
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
            return TryResolveRelocation(headquarters, destination, out _, out _);
        }

        /// <summary>
        /// Resolves a headquarters order against the active game and validates its destination.
        /// </summary>
        /// <param name="headquarters">The selected headquarters or its snapshot.</param>
        /// <param name="destination">The selected planet or its snapshot.</param>
        /// <param name="liveHeadquarters">Receives the registered headquarters.</param>
        /// <param name="liveDestination">Receives the registered destination.</param>
        /// <returns>True when the registered units permit relocation.</returns>
        internal bool TryResolveRelocation(
            Building headquarters,
            Planet destination,
            out Building liveHeadquarters,
            out Planet liveDestination
        )
        {
            liveHeadquarters = _game.GetSceneNodeByInstanceID<Building>(headquarters?.InstanceID);
            liveDestination = _game.GetSceneNodeByInstanceID<Planet>(destination?.InstanceID);
            if (
                !CanMove(liveHeadquarters)
                || liveDestination?.IsDestroyed != false
                || !liveDestination.IsColonized
            )
                return false;

            Planet origin = liveHeadquarters.GetParentOfType<Planet>();
            return liveDestination != origin
                && liveDestination.OwnerInstanceID == liveHeadquarters.OwnerInstanceID
                && liveDestination.CanAcceptChild(liveHeadquarters);
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
                && faction.HeadquartersUnitInstanceID == building.InstanceID
                && !faction.HasLostHeadquarters
                && origin != null
                && faction.HQInstanceID == origin.InstanceID;
        }
    }
}
