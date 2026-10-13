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
        private readonly OfficerCommandResolver _resolver;

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
            _resolver = new OfficerCommandResolver(game);
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
            if (
                officer == null
                || string.IsNullOrWhiteSpace(requestingFactionInstanceId)
                || !string.Equals(
                    officer.GetOwnerInstanceID(),
                    requestingFactionInstanceId,
                    StringComparison.Ordinal
                )
            )
                return false;

            return OfficerCommandRules.CanChangeRank(officer, rank);
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

            ISceneNode commandTarget = OfficerCommandRules.ResolveCommandTarget(officer);
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
                    _resolver.ApplyRank(incumbent, OfficerRank.None, commandTarget, results);
            }

            if (officer.CurrentRank == rank)
            {
                if (results.Count == 0)
                    return false;

                ResultsProduced?.Invoke(results);
                return true;
            }

            _resolver.ApplyRank(officer, rank, commandTarget, results);
            ResultsProduced?.Invoke(results);
            return true;
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
    }
}
