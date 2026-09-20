using System;
using Rebellion.Game;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Tracks real-time accumulation and determines when the simulation should advance.
    /// </summary>
    public sealed class GameClock
    {
        private readonly GameRoot _game;
        private float? _tickInterval;
        private float _elapsedSeconds;

        /// <summary>
        /// Raised after the active game speed changes.
        /// </summary>
        public event Action SpeedChanged;

        /// <summary>
        /// Gets the active simulation speed.
        /// </summary>
        public TickSpeed Speed => _game.GetGameSpeed();

        /// <summary>
        /// Creates a clock for the supplied game.
        /// </summary>
        /// <param name="game">The game whose configured speed controls the clock.</param>
        public GameClock(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            SetSpeed(game.GetGameSpeed());
        }

        /// <summary>
        /// Changes the simulation speed and resets no accumulated time.
        /// </summary>
        /// <param name="speed">The desired simulation speed.</param>
        public void SetSpeed(TickSpeed speed)
        {
            TickSpeed previousSpeed = _game.GetGameSpeed();
            _game.SetGameSpeed(speed);
            _tickInterval = speed switch
            {
                TickSpeed.Fast => _game.Config.GameSpeed.FastTickIntervalSeconds,
                TickSpeed.Medium => _game.Config.GameSpeed.MediumTickIntervalSeconds,
                TickSpeed.Slow => _game.Config.GameSpeed.SlowTickIntervalSeconds,
                TickSpeed.VerySlow => _game.Config.GameSpeed.VerySlowTickIntervalSeconds,
                TickSpeed.Paused => null,
                _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, null),
            };

            if (previousSpeed != speed)
                SpeedChanged?.Invoke();
        }

        /// <summary>
        /// Accumulates elapsed time when the simulation can advance.
        /// </summary>
        /// <param name="elapsedSeconds">The elapsed frame time in seconds.</param>
        /// <param name="canAdvance">Whether the session is currently able to start a tick.</param>
        /// <returns>True when one tick interval has elapsed.</returns>
        public bool Advance(float elapsedSeconds, bool canAdvance)
        {
            if (elapsedSeconds <= 0f || !canAdvance || _tickInterval == null)
                return false;

            _elapsedSeconds += elapsedSeconds;
            if (_elapsedSeconds < _tickInterval)
                return false;

            _elapsedSeconds = 0f;
            return true;
        }

        /// <summary>
        /// Clears accumulated frame time.
        /// </summary>
        public void Reset()
        {
            _elapsedSeconds = 0f;
        }
    }
}
