using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Reads blockade status from the campaign's registered orbital forces.
    /// </summary>
    public static class BlockadeQueries
    {
        /// <summary>
        /// Finds a live campaign planet and evaluates its current blockade.
        /// </summary>
        /// <param name="game">The active campaign.</param>
        /// <param name="planetID">The planet to look up.</param>
        /// <returns>Whether the registered planet is blockaded.</returns>
        public static bool IsBlockaded(GameRoot game, string planetID) =>
            !string.IsNullOrEmpty(planetID)
            && game.NodesByInstanceID.TryGetValue(planetID, out ISceneNode node)
            && node is Planet planet
            && Calculate(game, planet);

        /// <summary>
        /// Evaluates the supplied planet using campaign rules for live planets and local orbital data for copies.
        /// </summary>
        /// <param name="game">The campaign whose registry identifies live planets.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>Whether the supplied planet is blockaded.</returns>
        public static bool IsBlockaded(GameRoot game, Planet planet) =>
            game != null && IsRegistered(game, planet)
                ? Calculate(game, planet)
                : planet.IsBlockaded();

        /// <summary>
        /// Checks whether an active blockade opposes the supplied faction.
        /// </summary>
        /// <param name="game">The active campaign.</param>
        /// <param name="planet">The affected planet.</param>
        /// <param name="factionID">The faction whose access is being checked.</param>
        /// <returns>Whether another faction blockades the planet.</returns>
        public static bool IsBlockadedFor(GameRoot game, Planet planet, string factionID) =>
            !string.IsNullOrEmpty(factionID)
            && planet.IsBlockadedFor(factionID, IsBlockaded(game, planet));

        /// <summary>
        /// Applies the campaign rule to a live planet when its faction domain permits it.
        /// </summary>
        /// <param name="game">The active campaign.</param>
        /// <param name="planet">The registered planet.</param>
        /// <returns>Whether the registered planet is blockaded.</returns>
        private static bool Calculate(GameRoot game, Planet planet)
        {
            if (
                planet.IsDestroyed
                || (!planet.IsColonized && string.IsNullOrEmpty(planet.OwnerInstanceID))
            )
                return false;
            return TryCalculateOrbitalBlockade(game, planet, out bool blockaded)
                ? blockaded
                : planet.IsBlockaded();
        }

        /// <summary>
        /// Evaluates blockade using the configured orbital forces.
        /// </summary>
        /// <param name="game">The active campaign providing the required state.</param>
        /// <param name="planet">The live campaign planet.</param>
        /// <param name="blockaded">The resulting blockade state when it can be determined.</param>
        /// <returns>Whether the configured faction data supports a blockade assessment.</returns>
        private static bool TryCalculateOrbitalBlockade(
            GameRoot game,
            Planet planet,
            out bool blockaded
        )
        {
            blockaded = false;
            if (
                !TryGetFactionPair(
                    game.GetFactions(),
                    game.BlockadeFactionIds,
                    out string first,
                    out string second
                )
                || !string.IsNullOrEmpty(planet.OwnerInstanceID)
                    && planet.OwnerInstanceID != first
                    && planet.OwnerInstanceID != second
            )
                return false;

            bool firstPresent = false;
            bool secondPresent = false;
            bool hasOpposingFleet = false;
            foreach (Fleet fleet in planet.DirectOrbitalFleets)
            {
                if (
                    !fleet.IsEnabled
                    || fleet.Movement != null
                    || !IsRegistered(game, fleet)
                    || !fleet.HasOperationalCapitalShips()
                )
                    continue;

                string owner = fleet.OwnerInstanceID;
                firstPresent |= owner == first;
                secondPresent |= owner == second;
                if (firstPresent && secondPresent)
                    return true;

                hasOpposingFleet |=
                    (owner ?? "") != (planet.OwnerInstanceID ?? "") && fleet.IsActive();
            }

            if (!hasOpposingFleet)
                return true;

            foreach (Starfighter fighter in planet.DirectOrbitalStarfighters)
            {
                if (
                    !fighter.IsEnabled
                    || fighter.ManufacturingStatus != ManufacturingStatus.Complete
                    || fighter.Movement != null
                    || !IsRegistered(game, fighter)
                )
                    continue;

                firstPresent |= fighter.OwnerInstanceID == first;
                secondPresent |= fighter.OwnerInstanceID == second;
                if (firstPresent && secondPresent)
                    return true;
            }

            blockaded = true;
            return true;
        }

        /// <summary>
        /// Checks whether this exact scene node is registered in the campaign.
        /// </summary>
        /// <param name="game">The campaign owning the scene registry.</param>
        /// <param name="node">The planet or orbital unit to check.</param>
        /// <returns>True when the registered instance is the supplied node.</returns>
        private static bool IsRegistered(GameRoot game, ISceneNode node) =>
            !string.IsNullOrEmpty(node.InstanceID)
            && game.NodesByInstanceID.TryGetValue(node.InstanceID, out ISceneNode registered)
            && ReferenceEquals(registered, node);

        /// <summary>
        /// Checks whether the live campaign still has its original two factions.
        /// </summary>
        /// <param name="factions">The current campaign factions.</param>
        /// <param name="factionIds">The saved faction order.</param>
        /// <param name="first">The first configured identity.</param>
        /// <param name="second">The second configured identity.</param>
        /// <returns>Whether both configured identities are present in the current pair.</returns>
        private static bool TryGetFactionPair(
            IReadOnlyList<Faction> factions,
            IReadOnlyList<string> factionIds,
            out string first,
            out string second
        )
        {
            first = null;
            second = null;
            if (factions.Count != 2 || factionIds.Count != 2)
                return false;

            first = factionIds[0];
            second = factionIds[1];
            string firstOwner = factions[0]?.InstanceID;
            string secondOwner = factions[1]?.InstanceID;
            return firstOwner == first && secondOwner == second
                || firstOwner == second && secondOwner == first;
        }
    }
}
