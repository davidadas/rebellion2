using System;
using Rebellion.Game;

public static partial class HeadlessSimulationRunner
{
    private sealed class SimulationOptions
    {
        public int TickCount { get; set; }
        public string OutputPath { get; set; }
        public int? Seed { get; set; }
        public GameDifficulty Difficulty { get; set; } = GameDifficulty.Easy;
        public string SaveFileName { get; set; }
        public string SaveDisplayName { get; set; }
        public string PlayerFactionId { get; set; }

        /// <summary>
        /// Parses simulation options from command-line arguments.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The parsed simulation options.</returns>
        public static SimulationOptions Parse(string[] args)
        {
            return new SimulationOptions
            {
                TickCount = ParseInt(args, _tickCountFlag, 20),
                OutputPath = ParseString(
                    args,
                    _outputPathFlag,
                    "SimulationResults/headless-simulation-summary.json"
                ),
                Seed = ParseNullableInt(args, _seedFlag),
                Difficulty = ParseDifficulty(args),
            };
        }

        /// <summary>
        /// Parses the requested game difficulty.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The requested difficulty, or Easy when none is supplied.</returns>
        private static GameDifficulty ParseDifficulty(string[] args)
        {
            string value = ParseString(args, _difficultyFlag, null);
            if (
                !string.IsNullOrWhiteSpace(value)
                && Enum.TryParse(value, true, out GameDifficulty difficulty)
                && Enum.IsDefined(typeof(GameDifficulty), difficulty)
            )
            {
                return difficulty;
            }

            return GameDifficulty.Easy;
        }

        /// <summary>
        /// Parses an integer command-line option.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="flag">The option flag to read.</param>
        /// <param name="defaultValue">The value to use when the flag is absent.</param>
        /// <returns>The parsed integer value.</returns>
        private static int ParseInt(string[] args, string flag, int defaultValue)
        {
            string value = ParseString(args, flag, null);
            return int.TryParse(value, out int parsed) ? parsed : defaultValue;
        }

        /// <summary>
        /// Parses an optional integer command-line option.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="flag">The option flag to read.</param>
        /// <returns>The parsed integer value, or null if the flag is absent.</returns>
        private static int? ParseNullableInt(string[] args, string flag)
        {
            string value = ParseString(args, flag, null);
            return int.TryParse(value, out int parsed) ? parsed : null;
        }

        /// <summary>
        /// Parses a string command-line option.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="flag">The option flag to read.</param>
        /// <param name="defaultValue">The value to use when the flag is absent.</param>
        /// <returns>The parsed string value.</returns>
        private static string ParseString(string[] args, string flag, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return defaultValue;
        }
    }
}
