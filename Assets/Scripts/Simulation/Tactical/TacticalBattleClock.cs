using System;

namespace Rebellion.Simulation.Tactical
{
    /// <summary>
    /// Identifies the player-selected rate at which a tactical battle advances.
    /// </summary>
    public enum TacticalBattleSpeed
    {
        Paused,

        Half,

        Normal,

        Double,

        Quadruple,
    }

    /// <summary>
    /// Converts unscaled presentation time into elapsed tactical-battle time.
    /// </summary>
    public sealed class TacticalBattleClock
    {
        private bool _discardNextElapsedTime;
        private bool _isSuspended;

        /// <summary>
        /// Gets the selected tactical-battle speed.
        /// </summary>
        public TacticalBattleSpeed Speed { get; private set; } = TacticalBattleSpeed.Normal;

        /// <summary>
        /// Gets the multiplier applied to unscaled elapsed time.
        /// </summary>
        public float TimeScale => GetTimeScale(Speed);

        /// <summary>
        /// Gets whether tactical-battle advancement is paused.
        /// </summary>
        public bool IsPaused => Speed == TacticalBattleSpeed.Paused || _isSuspended;

        /// <summary>
        /// Selects the rate at which the tactical battle advances.
        /// </summary>
        /// <param name="speed">The requested supported speed.</param>
        public void SetSpeed(TacticalBattleSpeed speed)
        {
            if (!Enum.IsDefined(typeof(TacticalBattleSpeed), speed))
                throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
        }

        /// <summary>
        /// Suspends tactical time without changing the player-selected battle speed.
        /// </summary>
        public void Suspend()
        {
            _isSuspended = true;
        }

        /// <summary>
        /// Resumes tactical time and discards the first elapsed sample accumulated while suspended.
        /// </summary>
        public void Resume()
        {
            if (!_isSuspended)
                return;
            _isSuspended = false;
            _discardNextElapsedTime = true;
        }

        /// <summary>
        /// Scales one frame's unscaled elapsed time by the selected battle speed.
        /// </summary>
        /// <param name="unscaledDeltaTime">The non-negative real elapsed time.</param>
        /// <returns>The elapsed tactical-battle time.</returns>
        public float ScaleDeltaTime(float unscaledDeltaTime)
        {
            if (
                float.IsNaN(unscaledDeltaTime)
                || float.IsInfinity(unscaledDeltaTime)
                || unscaledDeltaTime < 0f
            )
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            if (_discardNextElapsedTime)
            {
                _discardNextElapsedTime = false;
                return 0f;
            }
            if (IsPaused)
                return 0f;
            return unscaledDeltaTime * TimeScale;
        }

        /// <summary>
        /// Resolves the numeric time multiplier represented by a supported battle speed.
        /// </summary>
        /// <param name="speed">The supported battle speed.</param>
        /// <returns>The corresponding non-negative time multiplier.</returns>
        public static float GetTimeScale(TacticalBattleSpeed speed)
        {
            return speed switch
            {
                TacticalBattleSpeed.Paused => 0f,
                TacticalBattleSpeed.Half => 0.5f,
                TacticalBattleSpeed.Normal => 1f,
                TacticalBattleSpeed.Double => 2f,
                TacticalBattleSpeed.Quadruple => 4f,
                _ => throw new ArgumentOutOfRangeException(nameof(speed)),
            };
        }
    }
}
