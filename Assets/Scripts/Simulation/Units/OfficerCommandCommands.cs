using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Assigns officers to local Commander, Admiral, and General posts.
    /// </summary>
    public sealed class OfficerCommandCommands
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Raised after an immediate command appointment produces results.
        /// </summary>
        public event Action<IReadOnlyList<GameResult>> ResultsProduced;

        /// <summary>
        /// Creates command-appointment operations for the active game.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        public OfficerCommandCommands(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Determines whether the requested player may assign an officer to a command post.
        /// </summary>
        /// <param name="officer">The live officer to inspect.</param>
        /// <param name="rank">The requested command post, or None to resign.</param>
        /// <param name="requestingFactionInstanceId">The faction issuing the order.</param>
        /// <returns>True when the command may be applied.</returns>
        public bool CanSetRank(
            Officer officer,
            OfficerRank rank,
            string requestingFactionInstanceId
        )
        {
            ISceneNode commandTarget = ResolveCommandTarget(officer);
            if (
                officer == null
                || string.IsNullOrWhiteSpace(requestingFactionInstanceId)
                || !IsSupportedRank(rank)
                || !string.Equals(
                    officer.GetOwnerInstanceID(),
                    requestingFactionInstanceId,
                    StringComparison.Ordinal
                )
                || officer.IsCaptured
                || officer.IsKilled
                || officer.IsRetired
                || officer.InjuryPoints > 0
                || ((IMovable)officer).GetTransitMovement() != null
                || commandTarget == null
            )
                return false;

            return rank == OfficerRank.None || officer.AllowedRanks?.Contains(rank) == true;
        }

        /// <summary>
        /// Applies one command appointment, replacing the officer who already holds that post
        /// in the same system or fleet.
        /// </summary>
        /// <param name="officerInstanceId">The selected officer's live instance identifier.</param>
        /// <param name="rank">The requested command post, or None to resign.</param>
        /// <param name="requestingFactionInstanceId">The faction issuing the order.</param>
        /// <returns>True when an appointment changed.</returns>
        public bool TrySetRank(
            string officerInstanceId,
            OfficerRank rank,
            string requestingFactionInstanceId
        )
        {
            Officer officer = _game.GetSceneNodeByInstanceID<Officer>(officerInstanceId);
            if (!CanSetRank(officer, rank, requestingFactionInstanceId))
                return false;

            ISceneNode commandTarget = ResolveCommandTarget(officer);
            List<GameResult> results = new List<GameResult>();

            // Selecting the active post resigns the officer from that post.
            if (rank != OfficerRank.None && officer.CurrentRank == rank)
                rank = OfficerRank.None;

            if (rank != OfficerRank.None)
            {
                foreach (
                    Officer incumbent in GetCommandOfficers(commandTarget)
                        .Where(candidate =>
                            !ReferenceEquals(candidate, officer)
                            && candidate.CurrentRank == rank
                            && string.Equals(
                                candidate.GetOwnerInstanceID(),
                                requestingFactionInstanceId,
                                StringComparison.Ordinal
                            )
                        )
                        .ToList()
                )
                    ApplyRank(incumbent, OfficerRank.None, commandTarget, results);
            }

            if (officer.CurrentRank == rank)
            {
                if (results.Count == 0)
                    return false;

                ResultsProduced?.Invoke(results);
                return true;
            }

            ApplyRank(officer, rank, commandTarget, results);
            ResultsProduced?.Invoke(results);
            return true;
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
        private static ISceneNode ResolveCommandHierarchyTarget(Officer officer)
        {
            if (officer?.IsOnMission() != false)
                return null;

            Fleet fleet = officer.GetParentOfType<Fleet>();
            return fleet != null ? fleet : officer.GetParentOfType<Planet>();
        }

        /// <summary>
        /// Removes command posts from relocated officers when their destination command already
        /// has an officer holding the same post. Officers traveling with an entire fleet retain
        /// that fleet's command because every officer in the command moves together.
        /// </summary>
        /// <param name="movedNode">The relocated officer or container.</param>
        /// <param name="tick">The current game tick.</param>
        /// <param name="results">The movement results receiving command changes.</param>
        internal static void ReconcileRelocatedCommandRanks(
            ISceneNode movedNode,
            int tick,
            ICollection<GameResult> results
        )
        {
            if (movedNode == null)
                return;
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            HashSet<Officer> movedOfficers = new HashSet<Officer>(
                movedNode is Officer officer ? new[] { officer }
                : movedNode is ContainerNode container
                    ? container.GetChildren<Officer>(recursive: true)
                : Enumerable.Empty<Officer>()
            );
            HashSet<Officer> retainedMovedOfficers = new HashSet<Officer>();

            foreach (
                Officer relocatedOfficer in movedOfficers
                    .Where(candidate => candidate.CurrentRank != OfficerRank.None)
                    .ToList()
            )
            {
                ISceneNode commandTarget = ResolveCommandHierarchyTarget(relocatedOfficer);
                if (commandTarget == null)
                    continue;

                bool postAlreadyFilled = GetCommandOfficers(commandTarget)
                    .Any(candidate =>
                        !ReferenceEquals(candidate, relocatedOfficer)
                        && candidate.CurrentRank == relocatedOfficer.CurrentRank
                        && string.Equals(
                            candidate.GetOwnerInstanceID(),
                            relocatedOfficer.GetOwnerInstanceID(),
                            StringComparison.Ordinal
                        )
                        && (
                            !movedOfficers.Contains(candidate)
                            || retainedMovedOfficers.Contains(candidate)
                        )
                    );
                if (postAlreadyFilled)
                {
                    ApplyRankChange(
                        relocatedOfficer,
                        OfficerRank.None,
                        commandTarget,
                        tick,
                        results
                    );
                    continue;
                }

                retainedMovedOfficers.Add(relocatedOfficer);
            }
        }

        /// <summary>
        /// Enumerates officers competing for posts in one fleet or planetary system.
        /// Planetary posts exclude officers attached to fleets in orbit.
        /// </summary>
        /// <param name="commandTarget">The fleet or planet.</param>
        /// <returns>The officers in that command.</returns>
        private static IEnumerable<Officer> GetCommandOfficers(ISceneNode commandTarget)
        {
            if (commandTarget is Fleet fleet)
                return fleet.GetChildren<Officer>(recursive: true);

            if (commandTarget is Planet planet)
            {
                return planet
                    .GetChildren<Officer>(recursive: true)
                    .Where(officer => officer.GetParentOfType<Fleet>() == null);
            }

            return Enumerable.Empty<Officer>();
        }

        /// <summary>
        /// Changes one officer's post and records both the rank and command-target notifications.
        /// </summary>
        /// <param name="officer">The officer to update.</param>
        /// <param name="rank">The new post.</param>
        /// <param name="commandTarget">The fleet or system being commanded.</param>
        /// <param name="results">The destination result collection.</param>
        private void ApplyRank(
            Officer officer,
            OfficerRank rank,
            ISceneNode commandTarget,
            ICollection<GameResult> results
        )
        {
            ApplyRankChange(officer, rank, commandTarget, _game.CurrentTick, results);
        }

        /// <summary>
        /// Changes one officer's post and records both command notifications.
        /// </summary>
        /// <param name="officer">The officer to update.</param>
        /// <param name="rank">The new post.</param>
        /// <param name="commandTarget">The command associated with the change.</param>
        /// <param name="tick">The current game tick.</param>
        /// <param name="results">The destination result collection.</param>
        private static void ApplyRankChange(
            Officer officer,
            OfficerRank rank,
            ISceneNode commandTarget,
            int tick,
            ICollection<GameResult> results
        )
        {
            OfficerRank previousRank = officer.CurrentRank;
            officer.CurrentRank = rank;
            results.Add(
                new CommandKindChangedResult
                {
                    Officer = officer,
                    CommandKind = (int)rank,
                    Detail = (int)previousRank,
                    Tick = tick,
                }
            );
            results.Add(
                new OfficerCommandingResult
                {
                    Officer = officer,
                    CommandTarget = rank == OfficerRank.None ? null : commandTarget,
                    Context = commandTarget,
                    Tick = tick,
                }
            );
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
    }
}
