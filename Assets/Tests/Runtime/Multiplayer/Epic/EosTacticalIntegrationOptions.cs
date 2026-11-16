using System;
using System.Collections.Generic;
using System.IO;

namespace Rebellion.Tests.MultiplayerIntegration
{
    /// <summary>
    /// Requires an explicit isolated profile for each opt-in real-service integration participant.
    /// </summary>
    internal sealed class EosTacticalIntegrationOptions
    {
        internal string Role;
        internal string RunID;
        internal string ReportDirectory;
        internal string SessionPath;
        internal bool ForceRelay;
        internal bool VerifyRecovery;
        internal int CapitalsPerSide = 1;
        internal int SquadronsPerSide = 2;
        internal int GuestWithdrawalAtSystemTime;
        internal double DeadlineSeconds = 600;

        /// <summary>
        /// Parses only an explicitly requested run and validates profile isolation arguments.
        /// </summary>
        /// <param name="arguments">The process arguments supplied by the test launcher.</param>
        /// <returns>The isolated run options, or null when this is not an integration process.</returns>
        internal static EosTacticalIntegrationOptions Parse(IReadOnlyList<string> arguments)
        {
            Dictionary<string, string> values = new(StringComparer.Ordinal);
            for (int index = 0; index < arguments.Count; index++)
            {
                string key = arguments[index];
                if (
                    !key.StartsWith("-eosTactical", StringComparison.Ordinal)
                    && key is not ("-userDataPath" or "-saveDirectory" or "-playerSessionFile")
                )
                    continue;
                if (
                    index + 1 >= arguments.Count
                    || arguments[index + 1].StartsWith("-", StringComparison.Ordinal)
                    || !values.TryAdd(key, arguments[++index])
                )
                    throw new ArgumentException("An integration argument is missing or repeated.");
            }
            if (!values.TryGetValue("-eosTacticalRole", out string role))
                return null;
            if (role is not ("host" or "guest"))
                throw new ArgumentException("The integration role must be host or guest.");
            if (
                !values.TryGetValue("-eosTacticalRunID", out string runID)
                || !Guid.TryParseExact(runID, "N", out _)
            )
                throw new ArgumentException("An exact integration run identity is required.");
            string reportDirectory = RequiredPath(values, "-eosTacticalReportDirectory");
            string userDirectory = RequiredPath(values, "-userDataPath");
            string saveDirectory = RequiredPath(values, "-saveDirectory");
            string sessionPath = RequiredPath(values, "-playerSessionFile");
            if (
                string.Equals(userDirectory, saveDirectory, StringComparison.OrdinalIgnoreCase)
                || string.Equals(userDirectory, reportDirectory, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(sessionPath)
            )
                throw new ArgumentException(
                    "Separate profile, save, report, and existing credential paths are required."
                );
            EosTacticalIntegrationOptions options = new()
            {
                Role = role,
                RunID = runID,
                ReportDirectory = Path.Combine(reportDirectory, runID),
                SessionPath = sessionPath,
            };
            if (values.TryGetValue("-eosTacticalForceRelay", out string relay))
            {
                if (!bool.TryParse(relay, out options.ForceRelay))
                    throw new ArgumentException("The forced-relay option must be true or false.");
            }
            if (values.TryGetValue("-eosTacticalVerifyRecovery", out string recovery))
            {
                if (!bool.TryParse(recovery, out options.VerifyRecovery))
                    throw new ArgumentException(
                        "The recovery verification option must be true or false."
                    );
            }
            if (values.TryGetValue("-eosTacticalCapitalsPerSide", out string capitals))
            {
                if (
                    !int.TryParse(capitals, out options.CapitalsPerSide)
                    || options.CapitalsPerSide is not (1 or 24)
                )
                    throw new ArgumentException("Select 1 or 24 authored capital ships per side.");
            }
            if (values.TryGetValue("-eosTacticalSquadronsPerSide", out string squadrons))
            {
                if (
                    !int.TryParse(squadrons, out options.SquadronsPerSide)
                    || options.SquadronsPerSide is not (2 or 8)
                )
                    throw new ArgumentException(
                        "Select 2 or 8 authored fighter squadrons per side."
                    );
            }
            if (values.TryGetValue("-eosTacticalDeadlineSeconds", out string seconds))
            {
                if (
                    !double.TryParse(
                        seconds,
                        System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double timeout
                    )
                    || !double.IsFinite(timeout)
                    || timeout < 30
                    || timeout > 1800
                )
                    throw new ArgumentException(
                        "The integration deadline must be between 30 and 1800 seconds."
                    );
                options.DeadlineSeconds = timeout;
            }
            if (
                values.TryGetValue(
                    "-eosTacticalGuestWithdrawalAtSystemTime",
                    out string withdrawalTime
                )
            )
            {
                if (
                    !int.TryParse(withdrawalTime, out options.GuestWithdrawalAtSystemTime)
                    || options.GuestWithdrawalAtSystemTime <= 0
                )
                    throw new ArgumentException(
                        "The optional guest withdrawal time must be a positive system-time integer."
                    );
            }
            if (options.VerifyRecovery && options.GuestWithdrawalAtSystemTime != 0)
                throw new ArgumentException(
                    "Recovery verification and terminal withdrawal are separate integration cases."
                );
            return options;
        }

        /// <summary>
        /// Requires a single absolute filesystem location without opening credential contents.
        /// </summary>
        /// <param name="values">The validated command-line argument pairs.</param>
        /// <param name="name">The required path argument.</param>
        /// <returns>The normalized absolute path.</returns>
        private static string RequiredPath(Dictionary<string, string> values, string name)
        {
            if (!values.TryGetValue(name, out string value) || !Path.IsPathFullyQualified(value))
                throw new ArgumentException("An absolute isolated path is required: " + name);
            return Path.GetFullPath(value);
        }
    }
}
