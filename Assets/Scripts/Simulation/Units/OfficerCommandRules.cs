using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates domain rules shared by officer command operations and presentation.
    /// </summary>
    internal static class OfficerCommandRules
    {
        /// <summary>
        /// Determines whether an officer's state and deployment permit a command change.
        /// </summary>
        /// <param name="officer">The officer to inspect.</param>
        /// <param name="rank">The requested command post, or None to resign.</param>
        /// <returns>True when the officer may receive the requested rank.</returns>
        internal static bool CanChangeRank(Officer officer, OfficerRank rank)
        {
            ISceneNode commandTarget = ResolveCommandTarget(officer);
            if (
                officer == null
                || !IsSupportedRank(rank)
                || officer.IsCaptured
                || officer.IsKilled
                || officer.IsRetired
                || officer.InjuryPoints > 0
                || ((IMovable)officer).GetTransitMovement() != null
                || commandTarget == null
                || !IsRankSupportedByTarget(rank, commandTarget)
            )
                return false;

            return rank == OfficerRank.None || officer.AllowedRanks?.Contains(rank) == true;
        }

        /// <summary>
        /// Finds the fleet or planetary-system command represented by an officer's location.
        /// </summary>
        /// <param name="officer">The officer whose command is requested.</param>
        /// <returns>The local command target, or null when the officer is not deployed.</returns>
        internal static ISceneNode ResolveCommandTarget(Officer officer)
        {
            if (officer?.Movement != null)
                return null;

            return ResolveCommandHierarchyTarget(officer);
        }

        /// <summary>
        /// Finds the fleet or planetary command represented by the scene hierarchy, independent
        /// of an officer's in-transit display state.
        /// </summary>
        /// <param name="officer">The officer whose hierarchy is requested.</param>
        /// <returns>The hierarchy's command target, or null while assigned to a mission.</returns>
        internal static ISceneNode ResolveCommandHierarchyTarget(Officer officer)
        {
            if (officer?.IsOnMission() != false)
                return null;

            Fleet fleet = officer.GetParentOfType<Fleet>();
            return fleet != null ? fleet : officer.GetParentOfType<Planet>();
        }

        /// <summary>
        /// Reports whether a value is one of the four supported command choices.
        /// </summary>
        /// <param name="rank">The value to inspect.</param>
        /// <returns>True for None, Commander, Admiral, or General.</returns>
        private static bool IsSupportedRank(OfficerRank rank) =>
            rank
                is OfficerRank.None
                    or OfficerRank.Commander
                    or OfficerRank.Admiral
                    or OfficerRank.General;

        /// <summary>
        /// Determines whether the requested command post exists at the officer's location.
        /// </summary>
        /// <param name="rank">The requested command post.</param>
        /// <param name="commandTarget">The local fleet or planetary command.</param>
        /// <returns>True when the target supports the requested post.</returns>
        private static bool IsRankSupportedByTarget(OfficerRank rank, ISceneNode commandTarget) =>
            rank != OfficerRank.Admiral || commandTarget is Fleet;
    }
}
