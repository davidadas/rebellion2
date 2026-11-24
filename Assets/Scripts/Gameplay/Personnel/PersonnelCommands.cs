using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Changes personnel lifecycle, command, and loyalty state.
    /// </summary>
    public sealed class PersonnelCommands
    {
        private readonly PersonnelQueries _queries;
        private readonly GameRoot _game;

        /// <summary>
        /// Raised after an immediate personnel operation produces results.
        /// </summary>
        public event Action<IReadOnlyList<GameResult>> ResultsProduced;

        /// <summary>
        /// Creates personnel commands using the active game's eligibility rules.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="queries">The personnel queries for the active game.</param>
        public PersonnelCommands(GameRoot game, PersonnelQueries queries)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Marks an officer as killed and disables it while preserving historical references.
        /// </summary>
        /// <param name="officer">The officer to kill and retain.</param>
        public void KillOfficer(Officer officer)
        {
            if (officer == null)
                throw new ArgumentNullException(nameof(officer));

            officer.IsEnabled = false;
            officer.IsKilled = true;
            officer.Movement = null;
        }

        /// <summary>
        /// Retires an entire validated personnel selection.
        /// </summary>
        /// <param name="personnel">The personnel or their snapshots.</param>
        /// <param name="ownerInstanceId">The faction authorized to retire the personnel.</param>
        /// <returns>True when every selected person was retired.</returns>
        public bool Retire(IReadOnlyList<ISceneNode> personnel, string ownerInstanceId)
        {
            if (
                !_queries.TryResolveRetirementSelection(
                    personnel,
                    ownerInstanceId,
                    out List<ISceneNode> live
                )
            )
                return false;

            foreach (ISceneNode person in live)
            {
                person.IsEnabled = false;
                if (person is Officer officer)
                    officer.IsRetired = true;
                else if (person is SpecialForces specialForces)
                    specialForces.IsRetired = true;
            }

            return true;
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
            if (
                officer == null
                || string.IsNullOrWhiteSpace(requestingFactionInstanceId)
                || !string.Equals(
                    officer.GetOwnerInstanceID(),
                    requestingFactionInstanceId,
                    StringComparison.Ordinal
                )
                || !PersonnelQueries.CanChangeCommand(officer, rank)
            )
                return false;

            ISceneNode commandTarget = GetCommandTarget(officer);
            List<GameResult> results = new List<GameResult>();
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
        /// Applies one signed global loyalty shift relative to the favored faction.
        /// </summary>
        /// <param name="favoredFaction">The faction for which the shift is positive.</param>
        /// <param name="shift">The signed shift relative to the favored faction.</param>
        public void ApplyGlobalLoyaltyShift(Faction favoredFaction, int shift)
        {
            if (favoredFaction == null)
                throw new ArgumentNullException(nameof(favoredFaction));
            if (shift == 0)
                return;

            foreach (
                Officer officer in _game.GetRegisteredSceneNodesByType<Officer>(
                    includeDisabled: true
                )
            )
            {
                int officerShift =
                    officer.GetOwnerInstanceID() == favoredFaction.InstanceID ? shift : -shift;
                officer.TryAdjustLoyalty(officerShift);
            }
        }

        /// <summary>
        /// Changes one officer's post and records both command notifications.
        /// </summary>
        /// <param name="officer">The officer to update.</param>
        /// <param name="rank">The new command post.</param>
        /// <param name="commandTarget">The command associated with the change.</param>
        /// <param name="results">The destination result collection.</param>
        private void ApplyRank(
            Officer officer,
            OfficerRank rank,
            ISceneNode commandTarget,
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
                    Tick = _game.CurrentTick,
                }
            );
            results.Add(
                new OfficerCommandingResult
                {
                    Officer = officer,
                    CommandTarget = rank == OfficerRank.None ? null : commandTarget,
                    Context = commandTarget,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Finds the fleet or planetary system receiving an explicit command appointment.
        /// </summary>
        /// <param name="officer">The officer being appointed.</param>
        /// <returns>The containing fleet or planet.</returns>
        private static ISceneNode GetCommandTarget(Officer officer) =>
            officer.GetParentOfType<Fleet>() ?? (ISceneNode)officer.GetParentOfType<Planet>();

        /// <summary>
        /// Enumerates officers competing for posts in one fleet or planetary system.
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
    }
}
