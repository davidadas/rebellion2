using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Resolves carried-unit consequences when combat destroys a capital ship.
    /// </summary>
    internal static class CapitalShipDestruction
    {
        /// <summary>
        /// Removes a destroyed capital ship after resolving its carried units.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="movement">Movement system used for surviving passenger evacuation.</param>
        /// <param name="ship">The destroyed capital ship.</param>
        internal static void Resolve(GameRoot game, MovementSystem movement, CapitalShip ship)
        {
            Fleet fleet = ship.GetParentOfType<Fleet>();

            EvacuateOfficers(game, movement, ship, fleet);
            EvacuateStarfighters(game, movement, ship, fleet);
            DestroyRegiments(game, ship);

            game.DetachNode(ship);
            GameLogger.Log($"Ship destroyed: {ship.GetDisplayName()}");
        }

        /// <summary>
        /// Moves officers off a destroyed capital ship.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="movement">Movement system used for planet evacuation.</param>
        /// <param name="ship">The destroyed capital ship.</param>
        /// <param name="fleet">The fleet that contained the destroyed ship.</param>
        private static void EvacuateOfficers(
            GameRoot game,
            MovementSystem movement,
            CapitalShip ship,
            Fleet fleet
        )
        {
            List<Officer> officers = ship.Officers.ToList();
            if (officers.Count == 0)
                return;

            CapitalShip survivingShip = FindSurvivingShip(fleet, ship);

            foreach (Officer officer in officers)
            {
                if (survivingShip != null)
                {
                    game.MoveNode(officer, survivingShip);
                    GameLogger.Log(
                        $"{officer.GetDisplayName()} evacuated to {survivingShip.GetDisplayName()} after {ship.GetDisplayName()} destroyed."
                    );
                }
                else
                {
                    movement.EvacuateToNearestFriendlyPlanet(officer);
                }
            }
        }

        /// <summary>
        /// Moves surviving starfighters off a destroyed capital ship.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="movement">Movement system used for planet evacuation.</param>
        /// <param name="ship">The destroyed capital ship.</param>
        /// <param name="fleet">The fleet that contained the destroyed ship.</param>
        private static void EvacuateStarfighters(
            GameRoot game,
            MovementSystem movement,
            CapitalShip ship,
            Fleet fleet
        )
        {
            List<Starfighter> starfighters = ship
                .Starfighters.Where(starfighter =>
                    starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                )
                .ToList();

            foreach (Starfighter starfighter in starfighters)
            {
                CapitalShip survivingCarrier = FindSurvivingCarrier(fleet, ship);
                if (survivingCarrier != null)
                    game.MoveNode(starfighter, survivingCarrier);
                else
                    movement.EvacuateToNearestFriendlyPlanet(starfighter);
            }
        }

        /// <summary>
        /// Removes complete regiments aboard a destroyed capital ship.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="ship">The destroyed capital ship.</param>
        private static void DestroyRegiments(GameRoot game, CapitalShip ship)
        {
            List<Regiment> regiments = ship
                .Regiments.Where(regiment =>
                    regiment.ManufacturingStatus == ManufacturingStatus.Complete
                )
                .ToList();

            foreach (Regiment regiment in regiments)
                game.DetachNode(regiment);
        }

        /// <summary>
        /// Finds another surviving capital ship in the same fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="destroyedShip">The destroyed ship to exclude.</param>
        /// <returns>A surviving capital ship, or null if none exists.</returns>
        private static CapitalShip FindSurvivingShip(Fleet fleet, CapitalShip destroyedShip)
        {
            return fleet?.CapitalShips.FirstOrDefault(ship =>
                !ReferenceEquals(ship, destroyedShip)
                && ship.ManufacturingStatus == ManufacturingStatus.Complete
                && ship.Movement == null
                && ship.CurrentHullStrength > 0
            );
        }

        /// <summary>
        /// Finds another surviving capital ship with starfighter capacity.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="destroyedShip">The destroyed ship to exclude.</param>
        /// <returns>A surviving carrier, or null if none exists.</returns>
        private static CapitalShip FindSurvivingCarrier(Fleet fleet, CapitalShip destroyedShip)
        {
            return fleet?.CapitalShips.FirstOrDefault(ship =>
                !ReferenceEquals(ship, destroyedShip)
                && ship.ManufacturingStatus == ManufacturingStatus.Complete
                && ship.Movement == null
                && ship.CurrentHullStrength > 0
                && ship.GetExcessStarfighterCapacity() > 0
            );
        }
    }
}
