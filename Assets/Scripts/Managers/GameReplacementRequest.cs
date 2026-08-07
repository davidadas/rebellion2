using System;
using Rebellion.Game;

/// <summary>
/// Represents one game replacement that presentation may defer until it is ready to reveal it.
/// </summary>
public sealed class GameReplacementRequest
{
    private readonly Action<GameRoot> complete;
    private bool completed;

    public GameRoot Game { get; }

    public bool IsDeferred { get; private set; }

    internal GameReplacementRequest(GameRoot game, Action<GameRoot> complete)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
    }

    /// <summary>
    /// Indicates that a presentation transition will complete this replacement later.
    /// </summary>
    public void Defer()
    {
        if (completed)
            throw new InvalidOperationException("A completed game replacement cannot be deferred.");

        IsDeferred = true;
    }

    /// <summary>
    /// Applies the replacement exactly once.
    /// </summary>
    public void Complete()
    {
        if (completed)
            return;

        completed = true;
        complete(Game);
    }
}
