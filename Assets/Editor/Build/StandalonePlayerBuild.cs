using System;
using System.IO;
using System.Linq;

/// <summary>
/// Builds standalone player artifacts without packaging external content.
/// </summary>
public static class StandalonePlayerBuild
{
    private const string _developmentContentAssetPrefix = "Assets/Content/";
    private const string _buildTargetArgument = "-buildTarget";
    private const string _buildPlayerPathArgument = "-buildPlayerPath";
    private const string _gameCIBuildPathArgument = "-customBuildPath";

    /// <summary>
    /// Runs the standalone player build requested by the Unity command line.
    /// </summary>
    public static void Build()
    {
        UIBuilderMenu.BuildAllForPlayer();

        UnityEditor.BuildTarget target = GetBuildTarget();
        string outputPath = ResolveProjectPath(
            GetRequiredArgument(_buildPlayerPathArgument, _gameCIBuildPathArgument)
        );
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        string[] scenes = UnityEditor
            .EditorBuildSettings.scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes configured for player build.");
        }

        UnityEditor.BuildPlayerOptions options = new UnityEditor.BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,
            options = UnityEditor.BuildOptions.None,
        };

        UnityEditor.Build.Reporting.BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(
            options
        );
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Player build failed with result {report.summary.result}."
            );
        }

        VerifyDevelopmentContentWasNotPacked(report);

        if (!File.Exists(outputPath) && !Directory.Exists(outputPath))
        {
            throw new InvalidOperationException($"Player build output not found at {outputPath}.");
        }

    }

    /// <summary>
    /// Fails the build if editor-only preview content leaked into Unity's player data.
    /// </summary>
    private static void VerifyDevelopmentContentWasNotPacked(
        UnityEditor.Build.Reporting.BuildReport report
    )
    {
        string[] packedDevelopmentAssets = report
            .packedAssets.SelectMany(packed => packed.contents)
            .Select(info => info.sourceAssetPath)
            .Where(path =>
                path.StartsWith(_developmentContentAssetPrefix, StringComparison.Ordinal)
            )
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (packedDevelopmentAssets.Length == 0)
            return;

        throw new InvalidOperationException(
            "Development content was packed into the player:\n"
                + string.Join("\n", packedDevelopmentAssets.Take(20))
        );
    }

    /// <summary>
    /// Reads the requested Unity build target from the command line.
    /// </summary>
    /// <returns>The parsed build target.</returns>
    private static UnityEditor.BuildTarget GetBuildTarget()
    {
        string value = GetRequiredArgument(_buildTargetArgument);
        if (string.Equals(value, "Win64", StringComparison.OrdinalIgnoreCase))
        {
            value = nameof(UnityEditor.BuildTarget.StandaloneWindows64);
        }

        if (Enum.TryParse(value, true, out UnityEditor.BuildTarget target))
        {
            return target;
        }

        throw new InvalidOperationException($"Unsupported build target '{value}'.");
    }

    /// <summary>
    /// Reads a required command-line argument value.
    /// </summary>
    /// <param name="arguments">The equivalent argument names to find.</param>
    /// <returns>The non-empty value following the argument.</returns>
    private static string GetRequiredArgument(params string[] arguments)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (
                arguments.Any(argument =>
                    string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                string value = args[i + 1];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        throw new InvalidOperationException(
            $"Required argument missing: {string.Join(" or ", arguments)}."
        );
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

        DirectoryInfo assetsDirectory = Directory.GetParent(UnityEngine.Application.dataPath);
        if (assetsDirectory == null)
        {
            throw new InvalidOperationException("Could not resolve project directory.");
        }

        return Path.Combine(assetsDirectory.FullName, path);
    }
}
