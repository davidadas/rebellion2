using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

/// <summary>
/// Builds standalone player artifacts without packaging external content.
/// </summary>
public static class StandalonePlayerBuild
{
    private const string _developmentContentAssetPrefix = "Assets/Content/";
    private const string _developmentModelAssetPrefix = "Assets/Art/Models/MainMenu/";
    private const string _bootScenePath = "Assets/Scenes/BootScene.unity";
    private const string _mainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string _strategyScenePath = "Assets/Scenes/StrategyView.unity";
    private const string _buildTargetArgument = "-buildTarget";
    private const string _buildPlayerPathArgument = "-buildPlayerPath";
    private const string _gameCIBuildPathArgument = "-customBuildPath";

    /// <summary>
    /// Builds an external-content player for the active desktop target from the Unity editor.
    /// </summary>
    [UnityEditor.MenuItem("Rebellion/Build/Build and Run Player", false, 100)]
    public static void BuildFromEditor()
    {
        UnityEditor.BuildTarget target = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
        (string fileName, string extension) = GetDefaultArtifact(target);
        string projectRoot = GetProjectRoot();
        string contentPath = Path.Combine(UnityEngine.Application.dataPath, "Content");
        string catalogPath = Path.Combine(contentPath, "catalog.xml");
        if (!File.Exists(catalogPath))
            throw new FileNotFoundException("Development content catalog not found.", catalogPath);

        string outputPath = UnityEditor.EditorUtility.SaveFilePanel(
            "Build Rebellion",
            Path.Combine(projectRoot, "build"),
            fileName,
            extension
        );
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        try
        {
            string builtPlayerPath = BuildPlayer(target, outputPath);
            LaunchPlayer(target, builtPlayerPath, contentPath);
        }
        finally
        {
            UIBuilderMenu.BuildAll();
        }
    }

    /// <summary>
    /// Launches a successfully built desktop player.
    /// </summary>
    /// <param name="target">The desktop platform that was built.</param>
    /// <param name="outputPath">The player artifact path.</param>
    /// <param name="contentPath">The external development content root.</param>
    private static void LaunchPlayer(
        UnityEditor.BuildTarget target,
        string outputPath,
        string contentPath
    )
    {
        ProcessStartInfo startInfo;
        if (target == UnityEditor.BuildTarget.StandaloneOSX)
        {
            startInfo = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add("--args");
        }
        else
        {
            startInfo = new ProcessStartInfo(outputPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty,
            };
        }

        startInfo.ArgumentList.Add("-contentPath");
        startInfo.ArgumentList.Add(contentPath);
        Process.Start(startInfo);
    }

    /// <summary>
    /// Runs the standalone player build requested by the Unity command line.
    /// </summary>
    public static void Build()
    {
        UnityEditor.BuildTarget target = GetBuildTarget();
        string outputPath = ResolveProjectPath(
            GetRequiredArgument(_buildPlayerPathArgument, _gameCIBuildPathArgument)
        );
        BuildPlayer(target, outputPath);
    }

    /// <summary>
    /// Builds and verifies an external-content player at the requested path.
    /// </summary>
    /// <param name="target">The desktop platform to build.</param>
    /// <param name="outputPath">The player artifact path.</param>
    /// <returns>The platform-correct player artifact path.</returns>
    private static string BuildPlayer(UnityEditor.BuildTarget target, string outputPath)
    {
        _ = GetDefaultArtifact(target);
        outputPath = NormalizeOutputPath(target, outputPath);
        try
        {
            UIBuilderMenu.BuildRuntimeUI();
            return BuildGeneratedPlayer(target, outputPath);
        }
        finally
        {
            BootPrefabBuilder.DeleteScene();
        }
    }

    /// <summary>
    /// Builds the player after all generated UI and scene assets are ready.
    /// </summary>
    /// <param name="target">The desktop platform to build.</param>
    /// <param name="outputPath">The normalized player artifact path.</param>
    /// <returns>The platform-correct player artifact path.</returns>
    private static string BuildGeneratedPlayer(UnityEditor.BuildTarget target, string outputPath)
    {
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        string[] scenes = { _bootScenePath, _mainMenuScenePath, _strategyScenePath };
        string missingScene = scenes.FirstOrDefault(path => !File.Exists(path));
        if (missingScene != null)
            throw new FileNotFoundException("Generated player scene not found.", missingScene);

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

        return outputPath;
    }

    /// <summary>
    /// Adds the macOS application-bundle extension when a build caller supplies a directory-like
    /// artifact path, so the post-build output check matches what Unity actually writes.
    /// </summary>
    /// <param name="target">The desktop platform being built.</param>
    /// <param name="outputPath">The requested player artifact path.</param>
    /// <returns>The output path Unity creates for the target platform.</returns>
    private static string NormalizeOutputPath(UnityEditor.BuildTarget target, string outputPath)
    {
        return
            target == UnityEditor.BuildTarget.StandaloneOSX
            && !outputPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            ? outputPath + ".app"
            : outputPath;
    }

    /// <summary>
    /// Returns the conventional artifact name for a supported desktop target.
    /// </summary>
    /// <param name="target">The active Unity build target.</param>
    /// <returns>The default file name and extension.</returns>
    private static (string FileName, string Extension) GetDefaultArtifact(
        UnityEditor.BuildTarget target
    )
    {
        switch (target)
        {
            case UnityEditor.BuildTarget.StandaloneOSX:
                return ("rebellion2", "app");
            case UnityEditor.BuildTarget.StandaloneWindows:
            case UnityEditor.BuildTarget.StandaloneWindows64:
                return ("rebellion2", "exe");
            case UnityEditor.BuildTarget.StandaloneLinux64:
                return ("rebellion2", "x86_64");
            default:
                throw new InvalidOperationException(
                    $"Rebellion player builds do not support target '{target}'."
                );
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
            .Where(IsStrippedDevelopmentAsset)
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
    /// Determines whether a packed asset is development content that must never ship in the player.
    /// </summary>
    /// <param name="path">The packed source asset path.</param>
    /// <returns>True when the asset is stripped development content.</returns>
    private static bool IsStrippedDevelopmentAsset(string path)
    {
        if (path.StartsWith(_developmentContentAssetPrefix, StringComparison.Ordinal))
            return true;

        // Main-menu 3D models ship as GLB in the content pack; only their runtime-rendered
        // RenderTextures may remain baked in the player.
        return path.StartsWith(_developmentModelAssetPrefix, StringComparison.Ordinal)
            && !path.EndsWith(".renderTexture", StringComparison.Ordinal);
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

        return Path.Combine(GetProjectRoot(), path);
    }

    /// <summary>
    /// Resolves the Unity project directory that owns the Assets folder.
    /// </summary>
    /// <returns>The absolute project directory.</returns>
    private static string GetProjectRoot()
    {
        DirectoryInfo projectDirectory = Directory.GetParent(UnityEngine.Application.dataPath);
        return projectDirectory?.FullName
            ?? throw new InvalidOperationException("Could not resolve project directory.");
    }
}
