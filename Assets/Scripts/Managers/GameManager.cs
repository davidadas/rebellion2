using System;
using System.Collections;
using System.Diagnostics;
using Rebellion.Game;
using Rebellion.Simulation;

/// <summary>
/// Maintains game speed and elapsed-time accumulation for the application frame loop.
/// </summary>
public sealed class GameManager
{
    private readonly Func<GameRoot> _getGame;
    private readonly GameTickProcessor _tick;
    private readonly Func<long> _getTimestamp;
    private float? _tickInterval;
    private float _tickProgress;

    public event Action GameSpeedChanged;

    /// <summary>
    /// Connects the clock to the current game and the simulation's busy boundary.
    /// </summary>
    /// <param name="getGame">Returns the active graph, including after hot loading.</param>
    /// <param name="tick">The session's stable tick processor.</param>
    public GameManager(Func<GameRoot> getGame, GameTickProcessor tick)
        : this(getGame, tick, Stopwatch.GetTimestamp) { }

    /// <summary>
    /// Connects the frame clock to the active game and simulation's busy boundary.
    /// </summary>
    /// <param name="getGame">Returns the active graph.</param>
    /// <param name="tick">The session's stable tick processor.</param>
    /// <param name="getTimestamp">Returns monotonic timestamps measured in Stopwatch.Frequency units.</param>
    internal GameManager(Func<GameRoot> getGame, GameTickProcessor tick, Func<long> getTimestamp)
    {
        _getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        _getTimestamp = getTimestamp ?? throw new ArgumentNullException(nameof(getTimestamp));
        _tick.CombatResumed += ResetTickTimer;
        Reset();
    }

    /// <summary>
    /// Returns the active game speed.
    /// </summary>
    /// <returns>The active game speed.</returns>
    public TickSpeed GetGameSpeed() => _getGame().GetGameSpeed();

    /// <summary>
    /// Sets the game speed and adjusts the tick interval accordingly.
    /// </summary>
    /// <param name="speed">The desired tick speed.</param>
    public void SetGameSpeed(TickSpeed speed)
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
    /// Advances ordered simulation work within the configured application-frame budget.
    /// </summary>
    /// <param name="tick">The current tick enumerator, still owned and disposed by its caller.</param>
    /// <returns>True when work remains for a later frame; false when the tick has completed.</returns>
    public bool AdvanceTick(IEnumerator tick)
    {
        if (tick == null)
            return false;
        double budget = _getGame().Config.GameSpeed.TickWorkBudgetMilliseconds;
        long started = _getTimestamp();
        do
        {
            if (!tick.MoveNext())
                return false;
        } while (budget > 0 && (_getTimestamp() - started) * 1000d / Stopwatch.Frequency < budget);
        return true;
    }

    /// <summary>
    /// Resets elapsed time and restores the configured speed after successful game replacement.
    /// </summary>
    public void Reset()
    {
        _tickProgress = 0f;
        SetGameSpeed(_getGame().GetGameSpeed());
    }

    /// <summary>
    /// Clears accumulated time at the existing completed-combat boundary.
    /// </summary>
    private void ResetTickTimer()
    {
        _tickProgress = 0f;
    }
}
