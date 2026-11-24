using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Evaluates live orbital blockades for an exact authored two-faction campaign domain.
    /// </summary>
    internal static class SpaceCombatBlockadeRules
    {
        /// <summary>
        /// Reads current faction identities and orbital readiness without retaining a stale domain.
        /// Unsupported faction domains remain owned by the planet's existing fleet predicate.
        /// </summary>
        /// <param name="game">The game owning the registered orbital units.</param>
        /// <param name="definition">The current authored tactical faction mapping.</param>
        /// <param name="planet">The exact registered planet whose orbital state is inspected.</param>
        /// <param name="blockaded">The current blockade state when this domain applies.</param>
        /// <returns>Whether the current faction domain supports these orbital rules.</returns>
        internal static bool TryCalculate(
            GameRoot game,
            TacticalCombatDefinition definition,
            Planet planet,
            out bool blockaded
        )
        {
            blockaded = false;
            if (
                !TryGetFactionPair(
                    game.GetFactions(),
                    definition,
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
        /// Validates the two current campaign identities against distinct authored tactical indexes.
        /// The fixed-size checks also cover mutations to either list or its identity fields.
        /// </summary>
        /// <param name="factions">The campaign's current faction collection.</param>
        /// <param name="definition">The mutable authored tactical definition.</param>
        /// <param name="first">The faction currently mapped to index zero.</param>
        /// <param name="second">The faction currently mapped to index one.</param>
        /// <returns>Whether the authored pair exactly covers the campaign's faction identities.</returns>
        private static bool TryGetFactionPair(
            IReadOnlyList<Faction> factions,
            TacticalCombatDefinition definition,
            out string first,
            out string second
        )
        {
            first = null;
            second = null;
            if (factions.Count != 2 || definition?.Factions?.Count != 2)
                return false;

            string firstOwner = factions[0]?.InstanceID;
            string secondOwner = factions[1]?.InstanceID;
            TacticalCombatFactionDefinition firstDefinition = definition.Factions[0];
            TacticalCombatFactionDefinition secondDefinition = definition.Factions[1];
            if (
                string.IsNullOrEmpty(firstOwner)
                || string.IsNullOrEmpty(secondOwner)
                || firstOwner == secondOwner
                || firstDefinition == null
                || secondDefinition == null
                || !(
                    firstDefinition.FactionIndex == 0 && secondDefinition.FactionIndex == 1
                    || firstDefinition.FactionIndex == 1 && secondDefinition.FactionIndex == 0
                )
                || !(
                    firstDefinition.FactionInstanceID == firstOwner
                        && secondDefinition.FactionInstanceID == secondOwner
                    || firstDefinition.FactionInstanceID == secondOwner
                        && secondDefinition.FactionInstanceID == firstOwner
                )
            )
                return false;

            first =
                firstDefinition.FactionIndex == 0
                    ? firstDefinition.FactionInstanceID
                    : secondDefinition.FactionInstanceID;
            second =
                firstDefinition.FactionIndex == 1
                    ? firstDefinition.FactionInstanceID
                    : secondDefinition.FactionInstanceID;
            return true;
        }
    }
}
