using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Answers whether an officer may change command posts.
    /// </summary>
    internal static class OfficerCommandQueries
    {
        /// <summary>
        /// Determines whether an officer's state and deployment permit a command change.
        /// </summary>
        /// <param name="officer">The officer to inspect.</param>
        /// <param name="rank">The requested command post, or None to resign.</param>
        /// <returns>True when the officer may receive the requested rank.</returns>
        internal static bool CanChangeCommand(Officer officer, OfficerRank rank)
        {
            ISceneNode commandTarget = GetCommandTarget(officer);
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
        /// Finds the fleet or planetary system containing a deployed officer.
        /// </summary>
        /// <param name="officer">The officer to inspect.</param>
        /// <returns>The containing fleet or planet, or null when the officer is not deployed.</returns>
        private static ISceneNode GetCommandTarget(Officer officer)
        {
            if (officer?.Movement != null || officer?.IsOnMission() != false)
                return null;

            return officer.GetParentOfType<Fleet>()
                ?? (ISceneNode)officer.GetParentOfType<Planet>();
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
