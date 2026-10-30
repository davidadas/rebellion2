using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.PlayerRequests;
using Rebellion.Gameplay;
using Rebellion.PlayerRequests;

/// <summary>
/// Creates explicit player-action execution boundaries for UI fixtures.
/// </summary>
public static class TestPlayerRequests
{
    /// <summary>
    /// Routes fixture actions through an existing session's domain executor.
    /// </summary>
    /// <param name="session">The fixture-owned game session.</param>
    /// <param name="getActorFactionID">The explicit actor, or the game's configured player.</param>
    /// <returns>The immediate domain dispatcher.</returns>
    public static IPlayerRequestDispatcher Create(
        GameSession session,
        Func<string> getActorFactionID = null
    )
    {
        GameManager clock = new GameManager(() => session.Game, session.Tick);
        return new LocalPlayerRequestDispatcher(
            new PlayerRequestExecutor(session, clock),
            getActorFactionID ?? (() => session.Game.GetPlayerFaction()?.InstanceID)
        );
    }

    /// <summary>
    /// Creates a domain session around the supplied fixture graph before routing actions.
    /// </summary>
    /// <param name="game">The fixture graph whose entities remain shared with the view.</param>
    /// <param name="getActorFactionID">The explicit actor, or the game's configured player.</param>
    /// <returns>The immediate domain dispatcher.</returns>
    public static IPlayerRequestDispatcher Create(
        GameRoot game,
        Func<string> getActorFactionID = null
    )
    {
        return Create(new GameSession(game, TestContent.Data), getActorFactionID);
    }

    /// <summary>
    /// Creates a boundary that records requests and explicitly rejects domain changes.
    /// </summary>
    /// <returns>The recording rejection boundary for query-only fixtures.</returns>
    public static RecordingPlayerRequestDispatcher RejectAll()
    {
        return new RecordingPlayerRequestDispatcher(_ =>
            Task.FromResult(
                new PlayerRequestOutcome(false, "This fixture does not execute actions.")
            )
        );
    }
}

/// <summary>
/// Captures submission intent while a fixture controls completion explicitly.
/// </summary>
public sealed class RecordingPlayerRequestDispatcher : IPlayerRequestDispatcher
{
    private readonly Func<PlayerRequest, Task<PlayerRequestOutcome>> execute;
    private readonly List<PlayerRequest> requests = new List<PlayerRequest>();

    public IReadOnlyList<PlayerRequest> Requests => requests;

    /// <summary>
    /// Creates a recording boundary with an explicit acceptance or rejection policy.
    /// </summary>
    /// <param name="execute">The fixture's completion policy.</param>
    public RecordingPlayerRequestDispatcher(Func<PlayerRequest, Task<PlayerRequestOutcome>> execute)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <summary>
    /// Records the request before returning the fixture-controlled completion.
    /// </summary>
    /// <param name="action">The typed player request.</param>
    /// <returns>The fixture's completed or pending execution outcome.</returns>
    public Task<PlayerRequestOutcome> ExecuteAsync(PlayerRequest action)
    {
        requests.Add(action);
        return execute(action);
    }
}
