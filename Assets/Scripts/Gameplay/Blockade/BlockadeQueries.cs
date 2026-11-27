using System.Collections.Generic;
using System.Linq;
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
        /// Finds a campaign planet and evaluates its orbital forces.
        /// </summary>
        /// <param name="game">The active campaign.</param>
        /// <param name="planetID">The planet to look up.</param>
        /// <returns>Whether the identified planet is blockaded.</returns>
        public static bool IsBlockaded(GameRoot game, string planetID) =>
            IsBlockaded(
                game,
                game?.GetSceneNodeByInstanceID<Planet>(planetID, includeDisabled: true)
            );

        /// <summary>
        /// Evaluates a planet's orbital forces using the campaign faction pair when available.
        /// </summary>
        /// <param name="game">The campaign providing faction and scene identity.</param>
        /// <param name="planet">The live planet or a detached copy of its orbital state.</param>
        /// <returns>Whether an opposing fleet has established a blockade.</returns>
        public static bool IsBlockaded(GameRoot game, Planet planet)
        {
            if (
                planet == null
                || planet.IsDestroyed
                || !planet.IsColonized && string.IsNullOrEmpty(planet.OwnerInstanceID)
            )
                return false;

            GameRoot liveGame = IsRegistered(game, planet) ? game : null;
            bool hasFactionPair = TryGetFactionPair(
                game,
                planet.OwnerInstanceID,
                out string first,
                out string second
            );
            bool firstPresent = false;
            bool secondPresent = false;
            bool hasOpposingFleet = false;
            bool hasDefendingFleet = false;
            foreach (Fleet fleet in GetReadyFleets(liveGame, planet))
            {
                string owner = fleet.OwnerInstanceID;
                firstPresent |= owner == first;
                secondPresent |= owner == second;
                bool belongsToPlanet =
                    (owner ?? string.Empty) == (planet.OwnerInstanceID ?? string.Empty);
                hasDefendingFleet |= belongsToPlanet;
                hasOpposingFleet |= !belongsToPlanet;
            }

            if (!hasOpposingFleet)
                return false;
            if (!hasFactionPair)
                return !hasDefendingFleet;

            foreach (Starfighter fighter in planet.DirectOrbitalStarfighters)
            {
                if (firstPresent && secondPresent)
                    break;
                if (
                    !fighter.IsEnabled
                    || fighter.ManufacturingStatus != ManufacturingStatus.Complete
                    || fighter.Movement != null
                    || liveGame != null && !IsRegistered(liveGame, fighter)
                )
                    continue;

                firstPresent |= fighter.OwnerInstanceID == first;
                secondPresent |= fighter.OwnerInstanceID == second;
            }
            return !(firstPresent && secondPresent);
        }

        /// <summary>
        /// Checks whether an active blockade opposes the supplied faction.
        /// </summary>
        /// <param name="game">The active campaign.</param>
        /// <param name="planet">The affected planet.</param>
        /// <param name="factionID">The faction whose access is being checked.</param>
        /// <returns>Whether another faction blockades the planet.</returns>
        public static bool IsBlockadedFor(GameRoot game, Planet planet, string factionID) =>
            !string.IsNullOrEmpty(factionID)
            && IsBlockaded(game, planet)
            && GetReadyFleets(IsRegistered(game, planet) ? game : null, planet)
                .Any(fleet => fleet.OwnerInstanceID != factionID);

        /// <summary>
        /// Enumerates active fleets with ready capital ships in the planet's orbit.
        /// </summary>
        /// <param name="game">The registry for a live planet, when available.</param>
        /// <param name="planet">The orbital location.</param>
        /// <returns>The ready fleets in local order.</returns>
        private static IEnumerable<Fleet> GetReadyFleets(GameRoot game, Planet planet)
        {
            foreach (Fleet fleet in planet.DirectOrbitalFleets)
                if (
                    fleet.IsActive()
                    && fleet.Movement == null
                    && (game == null || IsRegistered(game, fleet))
                    && fleet.HasOperationalCapitalShips()
                )
                    yield return fleet;
        }

        /// <summary>
        /// Checks whether this exact scene node belongs to the campaign's live registry.
        /// </summary>
        /// <param name="game">The campaign owning the scene registry.</param>
        /// <param name="node">The planet or orbital unit to check.</param>
        /// <returns>Whether the registered instance is the supplied node.</returns>
        private static bool IsRegistered(GameRoot game, ISceneNode node) =>
            game != null
            && !string.IsNullOrEmpty(node.InstanceID)
            && game.NodesByInstanceID.TryGetValue(node.InstanceID, out ISceneNode registered)
            && ReferenceEquals(registered, node);

        /// <summary>
        /// Resolves the participating two-faction domain from the campaign's roster and starting IDs.
        /// </summary>
        /// <param name="game">The campaign supplying both faction records.</param>
        /// <param name="owner">The planet's owner, when inhabited by one faction.</param>
        /// <param name="first">The first participating faction.</param>
        /// <param name="second">The second participating faction.</param>
        /// <returns>Whether the two factions can be used to assess the planet.</returns>
        private static bool TryGetFactionPair(
            GameRoot game,
            string owner,
            out string first,
            out string second
        )
        {
            first = null;
            second = null;
            IReadOnlyList<Faction> factions = game?.GetFactions();
            if (factions?.Count != 2)
                return false;
            first = factions[0]?.InstanceID;
            second = factions[1]?.InstanceID;
            if (
                string.IsNullOrEmpty(first)
                || string.IsNullOrEmpty(second)
                || first == second
                || !string.IsNullOrEmpty(owner) && owner != first && owner != second
            )
                return false;

            string[] starting = game.Summary.StartingFactionIDs;
            return starting is not { Length: > 0 }
                || starting.Length == 2
                    && (
                        starting[0] == first && starting[1] == second
                        || starting[0] == second && starting[1] == first
                    );
        }
    }
}
