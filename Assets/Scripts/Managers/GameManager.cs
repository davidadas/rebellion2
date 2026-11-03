using System;
using Rebellion.Game;
using Rebellion.Gameplay;

/// <summary>
/// Maintains game speed and elapsed-time accumulation for the application frame loop.
/// </summary>
public sealed class GameManager
{
    private readonly Func<GameRoot> _getGame;
    private readonly GameTickProcessor _tick;
    private float? _tickInterval;
    private float _tickProgress;
    private Action<TickSpeed> _requestGameSpeed;
    private bool _coordinatePlayerSpeeds;

    public event Action GameSpeedChanged;

    /// <summary>
    /// Connects the clock to the current game and the simulation's busy boundary.
    /// </summary>
    /// <param name="getGame">Returns the active graph, including after hot loading.</param>
    /// <param name="tick">The session's stable tick processor.</param>
    public GameManager(Func<GameRoot> getGame, GameTickProcessor tick)
    {
        _getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        _tick.CombatResumed += ResetTickTimer;
        Reset();
    }

    /// <summary>
    /// Returns the active game speed.
    /// </summary>
    /// <returns>The active game speed.</returns>
    public TickSpeed GetGameSpeed() => _getGame().GetGameSpeed();

    /// <summary>
    /// Returns the local participant's selection independently of the effective simulation speed.
    /// </summary>
    /// <returns>The local requested speed when coordinated, or the active game speed otherwise.</returns>
    public TickSpeed GetPlayerRequestedGameSpeed()
    {
        if (!_coordinatePlayerSpeeds)
            return GetGameSpeed();

        GameRoot game = _getGame();
        return game.GetFactionPlayer(game.GetPlayerFaction().InstanceID).RequestedGameSpeed
            ?? GetGameSpeed();
    }

    /// <summary>
    /// Sets the game speed and adjusts the tick interval accordingly.
    /// </summary>
    /// <param name="speed">The desired tick speed.</param>
    public void SetGameSpeed(TickSpeed speed)
    {
        if (_requestGameSpeed != null)
            _requestGameSpeed(speed);
        else
            ApplyGameSpeed(speed);
    }

    /// <summary>
    /// Routes UI speed choices through the active multiplayer request ordering boundary.
    /// </summary>
    /// <param name="requestGameSpeed">The local participant's ordered speed request.</param>
    internal void BindSpeedRequests(Action<TickSpeed> requestGameSpeed)
    {
        _requestGameSpeed =
            requestGameSpeed ?? throw new ArgumentNullException(nameof(requestGameSpeed));
    }

    /// <summary>
    /// Restores immediate clock ownership when a multiplayer session is released.
    /// </summary>
    internal void UnbindSpeedRequests()
    {
        _requestGameSpeed = null;
    }

    /// <summary>
    /// Initializes each human participant's saved speed preference from the shared session clock.
    /// </summary>
    internal void EnableCoordinatedSpeeds()
    {
        _coordinatePlayerSpeeds = true;
        foreach (Player player in _getGame().GetPlayers())
        {
            if (player.ControllerType == PlayerControllerType.Human)
                player.RequestedGameSpeed ??= GetGameSpeed();
        }
    }

    /// <summary>
    /// Records an ordered selection and applies the slowest human participant's speed, including Pause.
    /// </summary>
    /// <param name="factionID">The authenticated participant's faction.</param>
    /// <param name="speed">The requested speed.</param>
    internal void ApplyPlayerGameSpeed(string factionID, TickSpeed speed)
    {
        if (!_coordinatePlayerSpeeds)
        {
            ApplyGameSpeed(speed);
            return;
        }
        Player selectingPlayer = _getGame().GetFactionPlayer(factionID);
        selectingPlayer.GameSpeedSelectionRevision = checked(
            selectingPlayer.GameSpeedSelectionRevision + 1
        );
        selectingPlayer.RequestedGameSpeed = speed;
        TickSpeed slowest = speed;
        foreach (Player player in _getGame().GetPlayers())
        {
            if (player.ControllerType != PlayerControllerType.Human)
                continue;
            TickSpeed requested = player.RequestedGameSpeed ?? GetGameSpeed();
            if (GetSpeedOrder(requested) < GetSpeedOrder(slowest))
                slowest = requested;
        }
        ApplyGameSpeed(slowest);
    }

    /// <summary>
    /// Orders supported preferences from stopped time to the fastest clock, independently of enum values.
    /// </summary>
    /// <param name="speed">The participant's supported selection.</param>
    /// <returns>The ascending position from Pause through Fast.</returns>
    private static int GetSpeedOrder(TickSpeed speed)
    {
        return speed switch
        {
            TickSpeed.Paused => 0,
            TickSpeed.VerySlow => 1,
            TickSpeed.Slow => 2,
            TickSpeed.Medium => 3,
            TickSpeed.Fast => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, null),
        };
    }

    /// <summary>
    /// Answers the exact outstanding selection displayed to another human participant.
    /// Declining withdraws only that proposal; accepting records the responding participant's matching choice.
    /// </summary>
    /// <param name="actorFactionID">The authenticated responding faction.</param>
    /// <param name="requestingFactionID">The faction whose proposal was displayed.</param>
    /// <param name="speed">The displayed proposed speed.</param>
    /// <param name="revision">The displayed proposal revision.</param>
    /// <param name="accept">Whether to accept rather than decline the proposal.</param>
    /// <returns>True when the displayed proposal is still outstanding and the response was applied.</returns>
    internal bool TryRespondToGameSpeed(
        string actorFactionID,
        string requestingFactionID,
        TickSpeed speed,
        long revision,
        bool accept
    )
    {
        GameRoot game = _getGame();
        Player actor = game.GetFactionPlayer(actorFactionID);
        Player requester = game.GetFactionPlayer(requestingFactionID);
        if (
            !_coordinatePlayerSpeeds
            || actor == null
            || requester == null
            || actor == requester
            || actor.ControllerType != PlayerControllerType.Human
            || requester.ControllerType != PlayerControllerType.Human
            || requester.RequestedGameSpeed != speed
            || requester.GameSpeedSelectionRevision != revision
            || speed == GetGameSpeed()
            || actor.RequestedGameSpeed == speed
        )
            return false;
        ApplyPlayerGameSpeed(
            accept ? actorFactionID : requestingFactionID,
            accept ? speed : GetGameSpeed()
        );
        return true;
    }

    /// <summary>
    /// Requests the adjacent speed from this participant's selection, including while agreement is pending.
    /// </summary>
    /// <param name="faster">Whether to select the next faster speed.</param>
    public void StepGameSpeed(bool faster) =>
        SetGameSpeed(GetAdjacentGameSpeed(GetPlayerRequestedGameSpeed(), faster));

    /// <summary>
    /// Selects an adjacent supported speed without depending on enum numeric ordering.
    /// </summary>
    /// <param name="speed">The participant's current selection.</param>
    /// <param name="faster">Whether to select the next faster speed.</param>
    /// <returns>The adjacent speed, bounded at Pause and Fast.</returns>
    internal static TickSpeed GetAdjacentGameSpeed(TickSpeed speed, bool faster)
    {
        return (faster, speed) switch
        {
            (true, TickSpeed.Paused) => TickSpeed.VerySlow,
            (true, TickSpeed.VerySlow) => TickSpeed.Slow,
            (true, TickSpeed.Slow) => TickSpeed.Medium,
            (true, _) => TickSpeed.Fast,
            (false, TickSpeed.Fast) => TickSpeed.Medium,
            (false, TickSpeed.Medium) => TickSpeed.Slow,
            (false, TickSpeed.Slow) => TickSpeed.VerySlow,
            (false, _) => TickSpeed.Paused,
        };
    }

    /// <summary>
    /// Applies an already ordered clock transition or an application lifecycle pause.
    /// </summary>
    /// <param name="speed">The authoritative clock speed.</param>
    internal void ApplyGameSpeed(TickSpeed speed)
    {
        TickSpeed previousSpeed = _getGame().GetGameSpeed();
        _getGame().SetGameSpeed(speed);

        switch (speed)
        {
            case TickSpeed.Fast:
                _tickInterval = _getGame().Config.GameSpeed.FastTickIntervalSeconds;
                break;
            case TickSpeed.Medium:
                _tickInterval = _getGame().Config.GameSpeed.MediumTickIntervalSeconds;
                break;
            case TickSpeed.Slow:
                _tickInterval = _getGame().Config.GameSpeed.SlowTickIntervalSeconds;
                break;
            case TickSpeed.VerySlow:
                _tickInterval = _getGame().Config.GameSpeed.VerySlowTickIntervalSeconds;
                break;
            case TickSpeed.Paused:
                _tickInterval = null;
                break;
        }

        if (previousSpeed != speed)
            GameSpeedChanged?.Invoke();
    }

    /// <summary>
    /// Advances the tick timer without immediately processing a completed interval.
    /// </summary>
    /// <param name="elapsedSeconds">The elapsed game-loop time in seconds.</param>
    /// <returns>True when a game tick is ready to process.</returns>
    public bool TryAdvanceTickTimer(float elapsedSeconds)
    {
        if (elapsedSeconds <= 0f || _tick.IsBusy || _tickInterval == null)
            return false;

        _tickProgress += elapsedSeconds / _tickInterval.Value;
        if (_tickProgress < 1f)
            return false;

        _tickProgress = 0f;
        return true;
    }

    /// <summary>
    /// Resets elapsed time and restores the configured speed after successful game replacement.
    /// </summary>
    public void Reset()
    {
        _tickProgress = 0f;
        ApplyGameSpeed(_getGame().GetGameSpeed());
    }

    /// <summary>
    /// Clears accumulated time at the existing completed-combat boundary.
    /// </summary>
    private void ResetTickTimer()
    {
        _tickProgress = 0f;
    }
}
