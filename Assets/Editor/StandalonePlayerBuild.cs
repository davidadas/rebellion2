using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds standalone player artifacts from Unity batch-mode invocations.
/// </summary>
public static class StandalonePlayerBuild
{
    private const string _buildTargetArgument = "-buildTarget";
    private const string _buildPlayerPathArgument = "-buildPlayerPath";

    /// <summary>
    /// Runs the standalone player build requested by the Unity command line.
    /// </summary>
    public static void Build()
    {
        BuildTarget target = GetBuildTarget();
        string outputPath = ResolveProjectPath(GetRequiredArgument(_buildPlayerPathArgument));
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string[] scenes = EditorBuildSettings
            .scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes configured for player build.");
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Player build failed with result {report.summary.result}."
            );
        }

        if (!File.Exists(outputPath) && !Directory.Exists(outputPath))
        {
            throw new InvalidOperationException($"Player build output not found at {outputPath}.");
        }
    }

    /// <summary>
    /// Reads the requested Unity build target from the command line.
    /// </summary>
    /// <returns>The parsed build target.</returns>
    private static BuildTarget GetBuildTarget()
    {
        string value = GetRequiredArgument(_buildTargetArgument);
        if (Enum.TryParse(value, true, out BuildTarget target))
        {
            return target;
        }

        throw new InvalidOperationException($"Unsupported build target '{value}'.");
    }

    /// <summary>
    /// Reads a required command-line argument value.
    /// </summary>
    /// <param name="argument">The argument name to find.</param>
    /// <returns>The non-empty value following the argument.</returns>
    private static string GetRequiredArgument(string argument)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
            {
                string value = args[i + 1];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        throw new InvalidOperationException($"{argument} argument missing.");
    }

    /// <summary>
    /// Resolves a build output path relative to the Unity project root.
    /// </summary>
    /// <param name="path">The absolute or project-relative path to resolve.</param>
    /// <returns>The absolute output path.</returns>
    private static string ResolveProjectPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        DirectoryInfo assetsDirectory = Directory.GetParent(Application.dataPath);
        if (assetsDirectory == null)
        {
            throw new InvalidOperationException("Could not resolve project directory.");
        }

        return Path.Combine(assetsDirectory.FullName, path);
    }
}
