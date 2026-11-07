using System;
using System.IO;
using Rebellion.Gameplay;

/// <summary>
/// Owns battle-station movies, their presentation visibility, and completion acknowledgements.
/// </summary>
public sealed class TacticalCombatCutsceneController : IDisposable
{
    private readonly TacticalCombatSession session;
    private readonly Func<string, Action, IDisposable> play;
    private readonly Action<bool> setPresentationActive;
    private readonly Action<Exception> reportPlaybackError;
    private readonly Action<Action> postCompletion;
    private TacticalCombatMovieRequest activeRequest;
    private IDisposable activePlayback;
    private bool disposed;
    public bool IsPlaying => activeRequest != null;

    /// <summary>
    /// Binds required movie addresses and the playback ownership supplied by the active scene.
    /// </summary>
    /// <param name="session">The battle whose movie pauses are acknowledged.</param>
    /// <param name="play">Begins scoped playback and invokes its completion callback afterward.</param>
    /// <param name="setPresentationActive">Hides or restores the battle while video owns focus.</param>
    /// <param name="reportPlaybackError">Reports a playback failure before its deferred acknowledgement.</param>
    /// <param name="postCompletion">Posts failure acknowledgement after the current command finishes.</param>
    public TacticalCombatCutsceneController(
        TacticalCombatSession session,
        Func<string, Action, IDisposable> play,
        Action<bool> setPresentationActive,
        Action<Exception> reportPlaybackError,
        Action<Action> postCompletion
    )
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.play = play ?? throw new ArgumentNullException(nameof(play));
        this.setPresentationActive =
            setPresentationActive ?? throw new ArgumentNullException(nameof(setPresentationActive));
        this.reportPlaybackError =
            reportPlaybackError ?? throw new ArgumentNullException(nameof(reportPlaybackError));
        this.postCompletion =
            postCompletion ?? throw new ArgumentNullException(nameof(postCompletion));
        if (
            session.BattleStation != null
            && (
                string.IsNullOrWhiteSpace(session.Definition?.AttackRunSuccessMoviePath)
                || string.IsNullOrWhiteSpace(session.Definition?.AttackRunFailureMoviePath)
            )
        )
            throw new InvalidDataException(
                "Tactical combat requires success and failure movie addresses for battle-station attacks."
            );
        session.AttackRunMovieRequested += HandleMovie;
    }

    /// <summary>
    /// Cancels this controller's video and releases its simulation and presentation ownership.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        session.AttackRunMovieRequested -= HandleMovie;
        if (activeRequest != null)
            FinishMovie(activeRequest);
    }

    /// <summary>
    /// Starts the selected outcome movie while the attack's conclusion finishes synchronously.
    /// </summary>
    /// <param name="request">The simulation pause and terminal attack state being presented.</param>
    private void HandleMovie(TacticalCombatMovieRequest request)
    {
        if (activeRequest != null)
            FinishMovie(activeRequest);
        activeRequest = request;
        setPresentationActive(true);
        try
        {
            string path =
                request.AttackRun.State == TacticalCombatAttackRunState.Succeeded
                    ? session.Definition.AttackRunSuccessMoviePath
                    : session.Definition.AttackRunFailureMoviePath;
            IDisposable playback = play(path, () => FinishMovie(request));
            if (ReferenceEquals(activeRequest, request))
                activePlayback = playback;
            else
                playback?.Dispose();
        }
        catch (Exception exception)
        {
            reportPlaybackError(exception);
            postCompletion(() => FinishMovie(request));
        }
    }

    /// <summary>
    /// Restores the battle only when the callback still owns the active movie.
    /// </summary>
    /// <param name="request">The movie that completed or was cancelled.</param>
    private void FinishMovie(TacticalCombatMovieRequest request)
    {
        if (!ReferenceEquals(activeRequest, request))
            return;
        activeRequest = null;
        IDisposable playback = activePlayback;
        activePlayback = null;
        playback?.Dispose();
        request.Complete();
        setPresentationActive(false);
    }
}
