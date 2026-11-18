using System;
using Rebellion.Game.Results;

namespace Rebellion.Tactical.Integration.Campaign
{
    /// <summary>
    /// Owns playback advancement and acknowledgement independently of a faction's combat view.
    /// </summary>
    public interface ITacticalCombatPresentationDriver : IDisposable
    {
        TacticalCombatSession Session { get; }
        ITacticalCombatPresentationTimeline ReplicaPlayback { get; }
        bool IsAuthoritative { get; }
        bool CompletionReady { get; }
        SpaceCombatResult CommittedResult { get; }

        /// <summary>
        /// Begins local entry presentation after the required combat assets are ready.
        /// </summary>
        /// <param name="presentation">The local owner of entry transforms.</param>
        void Prepare(ITacticalCombatArrivalPresentation presentation);

        /// <summary>
        /// Advances the owning playback boundary for one unscaled presentation frame.
        /// </summary>
        /// <param name="elapsedSeconds">The unscaled presentation interval.</param>
        void Advance(float elapsedSeconds);

        /// <summary>
        /// Reports that this view has finished an admitted movie.
        /// </summary>
        /// <param name="request">The movie whose local playback ended.</param>
        void AcknowledgeMovie(TacticalCombatMovieRequest request);
    }
}
