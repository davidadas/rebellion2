using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebellion.App;
using Rebellion.App.PlayerActions;
using Rebellion.Game;
using Rebellion.Simulation;

/// <summary>
/// Creates explicit player-action execution boundaries for UI fixtures.
/// </summary>
public static class TestPlayerActions
{
    /// <summary>
    /// Routes fixture actions through an existing session's domain executor.
    /// </summary>
    /// <param name="session">The fixture-owned game session.</param>
    /// <param name="getActorFactionID">The explicit actor, or the game's configured player.</param>
    /// <returns>The immediate domain dispatcher.</returns>
    public static IPlayerActionDispatcher Create(
        GameSession session,
        Func<string> getActorFactionID = null
    )
    {
        GameManager clock = new GameManager(() => session.Game, session.Tick);
        return new LocalPlayerActionDispatcher(
            new PlayerActionExecutor(session, clock),
            getActorFactionID ?? (() => session.Game.GetPlayerFaction()?.InstanceID)
        );
    }

    /// <summary>
    /// Creates a domain session around the supplied fixture graph before routing actions.
    /// </summary>
    /// <param name="game">The fixture graph whose entities remain shared with the view.</param>
    /// <param name="getActorFactionID">The explicit actor, or the game's configured player.</param>
    /// <returns>The immediate domain dispatcher.</returns>
    public static IPlayerActionDispatcher Create(
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
    public static RecordingPlayerActionDispatcher RejectAll()
    {
        return new RecordingPlayerActionDispatcher(_ =>
            Task.FromResult(new PlayerActionResult(false, "This fixture does not execute actions."))
        );
    }
}

/// <summary>
/// Captures submission intent while a fixture controls completion explicitly.
/// </summary>
public sealed class RecordingPlayerActionDispatcher : IPlayerActionDispatcher
{
    private readonly Func<PlayerAction, Task<PlayerActionResult>> execute;
    private readonly List<PlayerAction> requests = new List<PlayerAction>();

    public IReadOnlyList<PlayerAction> Requests => requests;

    /// <summary>
    /// Creates a recording boundary with an explicit acceptance or rejection policy.
    /// </summary>
    /// <param name="execute">The fixture's completion policy.</param>
    public RecordingPlayerActionDispatcher(Func<PlayerAction, Task<PlayerActionResult>> execute)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <summary>
    /// Records the request before returning the fixture-controlled completion.
    /// </summary>
    /// <param name="action">The typed player request.</param>
    /// <returns>The fixture's completed or pending execution outcome.</returns>
    public Task<PlayerActionResult> ExecuteAsync(PlayerAction action)
    {
        requests.Add(action);
        return execute(action);
    }
}
