using System;
using System.IO;
using System.Linq;

/// <summary>
/// Builds standalone player artifacts and packages their external content.
/// </summary>
public static class StandalonePlayerBuild
{
    private const string _buildTargetArgument = "-buildTarget";
    private const string _buildPlayerPathArgument = "-buildPlayerPath";
    private const string _contentDirectoryName = "Content";

    /// <summary>
    /// Runs the standalone player build requested by the Unity command line.
    /// </summary>
    public static void Build()
    {
        UnityEditor.BuildTarget target = GetBuildTarget();
        string outputPath = ResolveProjectPath(GetRequiredArgument(_buildPlayerPathArgument));
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

        if (!File.Exists(outputPath) && !Directory.Exists(outputPath))
        {
            throw new InvalidOperationException($"Player build output not found at {outputPath}.");
        }

        CopyExternalContent(outputPath);
    }

    /// <summary>
    /// Replaces a destination directory with external content from a source directory.
    /// </summary>
    /// <param name="sourcePath">The external content source directory.</param>
    /// <param name="destinationPath">The destination directory to replace.</param>
    private static void CopyContentDirectory(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(destinationPath))
            Directory.Delete(destinationPath, true);

        Directory.CreateDirectory(destinationPath);
        foreach (
            string directoryPath in Directory.EnumerateDirectories(
                sourcePath,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            string relativePath = Path.GetRelativePath(sourcePath, directoryPath);
            Directory.CreateDirectory(Path.Combine(destinationPath, relativePath));
        }

        foreach (
            string filePath in Directory.EnumerateFiles(
                sourcePath,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            if (ShouldSkipContentFile(filePath))
                continue;

            string relativePath = Path.GetRelativePath(sourcePath, filePath);
            string destinationFilePath = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));
            File.Copy(filePath, destinationFilePath, true);
        }
    }

    /// <summary>
    /// Copies the project's external content beside a completed player artifact.
    /// </summary>
    /// <param name="playerPath">The completed player artifact path.</param>
    private static void CopyExternalContent(string playerPath)
    {
        string sourcePath = Path.Combine(UnityEngine.Application.dataPath, _contentDirectoryName);
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Content directory not found: {sourcePath}");

        CopyContentDirectory(sourcePath, GetContentDestinationPath(playerPath));
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
    /// Resolves the external content destination beside a player artifact.
    /// </summary>
    /// <param name="playerPath">The player artifact path.</param>
    /// <returns>The external content destination path.</returns>
    private static string GetContentDestinationPath(string playerPath)
    {
        if (string.IsNullOrWhiteSpace(playerPath))
            throw new ArgumentException("A player output path is required.", nameof(playerPath));

        string absolutePlayerPath = Path.GetFullPath(playerPath);
        string outputDirectory = Path.GetDirectoryName(absolutePlayerPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new InvalidOperationException(
                "The player output directory could not be resolved."
            );

        return Path.Combine(outputDirectory, _contentDirectoryName);
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

        DirectoryInfo assetsDirectory = Directory.GetParent(UnityEngine.Application.dataPath);
        if (assetsDirectory == null)
        {
            throw new InvalidOperationException("Could not resolve project directory.");
        }

        return Path.Combine(assetsDirectory.FullName, path);
    }

    /// <summary>
    /// Determines whether a source file contains editor-only metadata.
    /// </summary>
    /// <param name="filePath">The source file path.</param>
    /// <returns>True when the file should be excluded from the external content package.</returns>
    private static bool ShouldSkipContentFile(string filePath)
    {
        return filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Path.GetFileName(filePath),
                ".DS_Store",
                StringComparison.OrdinalIgnoreCase
            );
    }
}
