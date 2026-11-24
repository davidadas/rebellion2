using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Evaluates live orbital blockades for a campaign with a configured pair of factions.
    /// </summary>
    internal static class SpaceCombatBlockadeRules
    {
        /// <summary>
        /// Reads current faction identities and orbital readiness without retaining a stale domain.
        /// Unsupported faction domains remain owned by the planet's existing fleet predicate.
        /// </summary>
        /// <param name="game">The game owning the registered orbital units.</param>
        /// <param name="factionIds">The campaign's starting faction identities.</param>
        /// <param name="planet">The exact registered planet whose orbital state is inspected.</param>
        /// <param name="blockaded">The current blockade state when this domain applies.</param>
        /// <returns>Whether the current faction domain supports these orbital rules.</returns>
        internal static bool TryCalculate(
            GameRoot game,
            IReadOnlyList<string> factionIds,
            Planet planet,
            out bool blockaded
        )
        {
            blockaded = false;
            if (
                !TryGetFactionPair(
                    game.GetFactions(),
                    factionIds,
                    out string first,
                    out string second
                )
                || !string.IsNullOrEmpty(planet.OwnerInstanceID)
                    && planet.OwnerInstanceID != first
                    && planet.OwnerInstanceID != second
                || !IsRegistered(game, planet)
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
