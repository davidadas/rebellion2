using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Evaluates live orbital blockades for an campaign with a configured pair of factions.
    /// </summary>
    internal static class SpaceCombatBlockadeRules
    {
        /// <summary>
        /// Reads current faction identities and orbital readiness without retaining a stale domain.
        /// Unsupported faction domains remain owned by the planet's existing fleet predicate.
        /// </summary>
        /// <param name="game">The game owning the registered planet and readiness index.</param>
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
                || string.IsNullOrEmpty(planet.InstanceID)
                || !game.NodesByInstanceID.TryGetValue(planet.InstanceID, out ISceneNode registered)
                || !ReferenceEquals(registered, planet)
            )
                return false;

            (bool firstPresent, bool secondPresent) = game.CaptureSpaceCombatPresence(
                planet,
                first,
                second
            );
            if (firstPresent && secondPresent)
                return true;

            blockaded = game.HasReadyOpposingFleet(planet);
            return true;
        }

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
