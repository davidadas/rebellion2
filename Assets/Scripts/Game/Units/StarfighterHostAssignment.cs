using System;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Identifies a squadron's final host within an ordered relationship update.
    /// </summary>
    public readonly struct StarfighterHostAssignment
    {
        public Starfighter Fighter { get; }
        public ContainerNode Host { get; }

        /// <summary>
        /// Describes the final host of one squadron.
        /// </summary>
        /// <param name="fighter">The squadron being relocated.</param>
        /// <param name="host">The destination planet or capital ship.</param>
        public StarfighterHostAssignment(Starfighter fighter, ContainerNode host)
        {
            Fighter = fighter ?? throw new ArgumentNullException(nameof(fighter));
            Host = host ?? throw new ArgumentNullException(nameof(host));
        }
    }
}
