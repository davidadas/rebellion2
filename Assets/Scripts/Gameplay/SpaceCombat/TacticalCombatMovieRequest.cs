using System;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Owns one battle-station movie's completion acknowledgement and simulation pause.
    /// </summary>
    public sealed class TacticalCombatMovieRequest
    {
        private Action<TacticalCombatMovieRequest> _complete;
        public TacticalCombatAttackRun AttackRun { get; }
        public bool IsComplete => _complete == null;

        /// <summary>
        /// Captures the attack outcome and the pause released when its movie ends.
        /// </summary>
        /// <param name="attackRun">The attack whose terminal state selected the movie.</param>
        /// <param name="complete">Releases this request's simulation pause.</param>
        internal TacticalCombatMovieRequest(
            TacticalCombatAttackRun attackRun,
            Action<TacticalCombatMovieRequest> complete
        )
        {
            AttackRun = attackRun ?? throw new ArgumentNullException(nameof(attackRun));
            _complete = complete ?? throw new ArgumentNullException(nameof(complete));
        }

        /// <summary>
        /// Acknowledges playback completion once, preserving every other pause owner.
        /// </summary>
        public void Complete()
        {
            Action<TacticalCombatMovieRequest> complete = _complete;
            _complete = null;
            complete?.Invoke(this);
        }
    }
}
