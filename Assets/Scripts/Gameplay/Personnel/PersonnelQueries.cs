using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Evaluates personnel eligibility against the active game graph without changing it.
    /// </summary>
    public sealed class PersonnelQueries
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates personnel queries for one game.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        public PersonnelQueries(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Determines whether a faction may retire an entire personnel selection.
        /// </summary>
        /// <param name="personnel">The personnel or their snapshots.</param>
        /// <param name="ownerInstanceId">The faction authorized to retire the personnel.</param>
        /// <returns>True when every selected person may be retired.</returns>
        public bool CanRetire(IReadOnlyList<ISceneNode> personnel, string ownerInstanceId)
        {
            return TryResolveRetirementSelection(personnel, ownerInstanceId, out _);
        }

        /// <summary>
        /// Determines whether an officer's state and deployment permit a command change.
        /// </summary>
        /// <param name="officer">The officer to inspect.</param>
        /// <param name="rank">The requested command post, or None to resign.</param>
        /// <returns>True when the officer may receive the requested rank.</returns>
        public static bool CanChangeCommand(Officer officer, OfficerRank rank)
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
                || (rank == OfficerRank.Admiral && commandTarget is not Fleet)
            )
                return false;

            return rank == OfficerRank.None || officer.AllowedRanks?.Contains(rank) == true;
        }

        /// <summary>
        /// Finds the fleet or planetary system containing a deployed officer.
        /// </summary>
        /// <param name="officer">The officer to inspect.</param>
        /// <returns>The containing fleet or planet, or null when not deployed.</returns>
        private static ISceneNode GetCommandTarget(Officer officer)
        {
            if (officer?.Movement != null || officer?.IsOnMission() != false)
                return null;

            return officer.GetParentOfType<Fleet>()
                ?? (ISceneNode)officer.GetParentOfType<Planet>();
        }

        /// <summary>
        /// Reports whether a value is one of the supported command choices.
        /// </summary>
        /// <param name="rank">The value to inspect.</param>
        /// <returns>True when the value is a supported command choice.</returns>
        private static bool IsSupportedRank(OfficerRank rank) =>
            rank
                is OfficerRank.None
                    or OfficerRank.Commander
                    or OfficerRank.Admiral
                    or OfficerRank.General;

        /// <summary>
        /// Resolves and validates a complete retirement selection before mutation.
        /// </summary>
        /// <param name="personnel">The personnel or their snapshots.</param>
        /// <param name="ownerInstanceId">The faction authorized to retire the personnel.</param>
        /// <param name="livePersonnel">Receives the registered personnel in selection order.</param>
        /// <returns>True when the complete selection is valid.</returns>
        internal bool TryResolveRetirementSelection(
            IReadOnlyList<ISceneNode> personnel,
            string ownerInstanceId,
            out List<ISceneNode> livePersonnel
        )
        {
            livePersonnel = new List<ISceneNode>();
            if (personnel == null || personnel.Count == 0 || string.IsNullOrEmpty(ownerInstanceId))
                return false;

            HashSet<string> instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ISceneNode person in personnel)
            {
                ISceneNode livePerson = string.IsNullOrEmpty(person?.InstanceID)
                    ? null
                    : _game.GetSceneNodeByInstanceID<ISceneNode>(person.InstanceID);
                if (
                    livePerson == null
                    || livePerson.GetParent() == null
                    || !instanceIds.Add(livePerson.InstanceID)
                    || !string.Equals(
                        livePerson.GetOwnerInstanceID(),
                        ownerInstanceId,
                        StringComparison.Ordinal
                    )
                    || !CanRetirePerson(livePerson)
                )
                    return false;

                livePersonnel.Add(livePerson);
            }

            return true;
        }

        /// <summary>
        /// Determines whether one registered personnel node is currently retireable.
        /// </summary>
        /// <param name="person">The personnel node to inspect.</param>
        /// <returns>True when the personnel node may be retired.</returns>
        private static bool CanRetirePerson(ISceneNode person)
        {
            return person switch
            {
                Officer officer => !officer.IsMain
                    && !officer.IsCaptured
                    && officer.Movement == null,
                SpecialForces specialForces => specialForces.Movement == null
                    && specialForces.ManufacturingStatus != ManufacturingStatus.Building,
                _ => false,
            };
        }
    }
}
