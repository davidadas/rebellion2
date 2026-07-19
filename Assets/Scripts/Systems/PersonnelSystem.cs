using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Systems
{
    /// <summary>
    /// Owns personnel retirement validation and mutation for the active game graph.
    /// </summary>
    public sealed class PersonnelSystem
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates the personnel system for one game.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        public PersonnelSystem(GameRoot game)
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
        /// Retires an entire validated personnel selection.
        /// </summary>
        /// <param name="personnel">The personnel or their snapshots.</param>
        /// <param name="ownerInstanceId">The faction authorized to retire the personnel.</param>
        /// <returns>True when every selected person was retired.</returns>
        public bool Retire(IReadOnlyList<ISceneNode> personnel, string ownerInstanceId)
        {
            if (
                !TryResolveRetirementSelection(
                    personnel,
                    ownerInstanceId,
                    out List<ISceneNode> live
                )
            )
                return false;

            foreach (ISceneNode person in live)
                _game.DetachNode(person);

            return true;
        }

        /// <summary>
        /// Resolves and validates a complete retirement selection before mutation.
        /// </summary>
        /// <param name="personnel">The personnel or their snapshots.</param>
        /// <param name="ownerInstanceId">The faction authorized to retire the personnel.</param>
        /// <param name="livePersonnel">Receives the registered personnel in selection order.</param>
        /// <returns>True when the complete selection is valid.</returns>
        private bool TryResolveRetirementSelection(
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
