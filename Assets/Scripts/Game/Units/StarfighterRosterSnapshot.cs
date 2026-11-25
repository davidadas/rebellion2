using System.Collections.Generic;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Retains one host's exact squadron order until a relationship update is admitted.
    /// </summary>
    internal sealed class StarfighterRosterSnapshot
    {
        private readonly List<Starfighter> _roster;
        private readonly Starfighter[] _members;

        /// <summary>
        /// Captures the existing roster without changing its capacity or membership.
        /// </summary>
        /// <param name="roster">The host's authoritative squadron collection.</param>
        internal StarfighterRosterSnapshot(List<Starfighter> roster)
        {
            _roster = roster;
            _members = roster.ToArray();
        }

        /// <summary>
        /// Restores only the previously captured roster, including its original order.
        /// </summary>
        internal void Restore()
        {
            _roster.Clear();
            _roster.AddRange(_members);
        }
    }
}
