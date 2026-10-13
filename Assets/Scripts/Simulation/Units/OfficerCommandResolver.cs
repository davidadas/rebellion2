using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Applies low-level officer command transitions shared by application operations.
    /// </summary>
    internal sealed class OfficerCommandResolver
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates an officer command resolver for one game.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        internal OfficerCommandResolver(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Captures the command target of every ranked officer relocated with a scene node.
        /// </summary>
        /// <param name="movedNode">The officer or container about to be relocated.</param>
        /// <returns>The ranked officers and their command targets before relocation.</returns>
        internal IReadOnlyDictionary<Officer, ISceneNode> CaptureRelocatedCommandTargets(
            ISceneNode movedNode
        )
        {
            if (movedNode == null)
                return new Dictionary<Officer, ISceneNode>();

            IEnumerable<Officer> officers =
                movedNode is Officer officer ? new[] { officer }
                : movedNode is ContainerNode container
                    ? container.GetChildren<Officer>(recursive: true)
                : Enumerable.Empty<Officer>();

            return officers
                .Where(candidate => candidate.CurrentRank != OfficerRank.None)
                .ToDictionary(
                    candidate => candidate,
                    OfficerCommandRules.ResolveCommandHierarchyTarget
                );
        }

        /// <summary>
        /// Resigns relocated officers whose fleet or planetary command changed.
        /// </summary>
        /// <param name="previousCommandTargets">The command targets captured before relocation.</param>
        /// <param name="results">The movement results receiving command changes.</param>
        internal void ReconcileRelocatedCommandRanks(
            IReadOnlyDictionary<Officer, ISceneNode> previousCommandTargets,
            ICollection<GameResult> results
        )
        {
            if (previousCommandTargets == null)
                throw new ArgumentNullException(nameof(previousCommandTargets));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            foreach (KeyValuePair<Officer, ISceneNode> command in previousCommandTargets)
            {
                Officer officer = command.Key;
                if (
                    officer.CurrentRank == OfficerRank.None
                    || ReferenceEquals(
                        command.Value,
                        OfficerCommandRules.ResolveCommandHierarchyTarget(officer)
                    )
                )
                    continue;

                ApplyRank(
                    officer,
                    OfficerRank.None,
                    command.Value ?? OfficerCommandRules.ResolveCommandHierarchyTarget(officer),
                    results
                );
            }
        }

        /// <summary>
        /// Changes one officer's post and records both command notifications.
        /// </summary>
        /// <param name="officer">The officer to update.</param>
        /// <param name="rank">The new post.</param>
        /// <param name="commandTarget">The command associated with the change.</param>
        /// <param name="results">The destination result collection.</param>
        internal void ApplyRank(
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
    }
}
