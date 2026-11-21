using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Resolves the process's user-data location for persistent application files.
/// </summary>
public static class UserDataPaths
{
    public static string Root =>
        Resolve(Environment.GetCommandLineArgs(), Application.persistentDataPath);

    /// <summary>
    /// Uses an explicit portable profile when requested, otherwise retaining the normal application path.
    /// </summary>
    /// <param name="arguments">The process command line.</param>
    /// <param name="defaultDirectory">The platform's persistent application directory.</param>
    /// <returns>The absolute user-data directory.</returns>
    internal static string Resolve(IReadOnlyList<string> arguments, string defaultDirectory)
    {
        string directory = null;
        for (int i = 0; i < arguments.Count; i++)
        {
            if (!string.Equals(arguments[i], "-userDataPath", StringComparison.OrdinalIgnoreCase))
                continue;
            if (
                directory != null
                || i + 1 >= arguments.Count
                || string.IsNullOrWhiteSpace(arguments[i + 1])
                || arguments[i + 1].StartsWith("-", StringComparison.Ordinal)
            )
                throw new ArgumentException("Supply one directory after -userDataPath.");
            directory = arguments[++i];
        }
        return Path.GetFullPath(directory ?? defaultDirectory);
    }
}
