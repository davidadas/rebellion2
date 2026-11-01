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
    private const string _buildVersionArgument = "-buildVersion";
    private const string _enableDiagnosticsArgument = "-enableDiagnostics";
    private const string _enableAnalyticsArgument = "-enableAnalytics";
    private const string _skipRuntimeUIBuildArgument = "-skipRuntimeUIBuild";
    private const string _analyticsScriptingDefine = "REBELLION_ANALYTICS_ENABLED";

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
            string builtPlayerPath = BuildPlayer(
                target,
                outputPath,
                enableDiagnostics: false,
                enableAnalytics: false,
                prepareRuntimeUI: true
            );
            ContentPackEditorBuild.BuildBesideGame(target, builtPlayerPath, false);
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
    internal static void LaunchPlayer(
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
        string originalVersion = UnityEditor.PlayerSettings.bundleVersion;
#if UNITY_STANDALONE_OSX
        UnityEditor.Build.OSArchitecture originalMacArchitecture = default;
        if (target == UnityEditor.BuildTarget.StandaloneOSX)
        {
            originalMacArchitecture = UnityEditor.OSXStandalone.UserBuildSettings.architecture;
        }
#endif
        try
        {
            ApplyBuildVersion();
#if UNITY_STANDALONE_OSX
            ApplyBuildArchitecture(target);
#endif
            string builtPlayerPath = BuildPlayer(
                target,
                outputPath,
                HasArgument(_enableDiagnosticsArgument),
                HasArgument(_enableAnalyticsArgument),
                !HasArgument(_skipRuntimeUIBuildArgument)
            );
            ContentPackEditorBuild.BuildBesideGame(
                target,
                builtPlayerPath,
                HasArgument(_enableDiagnosticsArgument)
            );
        }
        finally
        {
            UnityEditor.PlayerSettings.bundleVersion = originalVersion;
#if UNITY_STANDALONE_OSX
            if (target == UnityEditor.BuildTarget.StandaloneOSX)
            {
                UnityEditor.OSXStandalone.UserBuildSettings.architecture = originalMacArchitecture;
            }
#endif
        }
    }

#if UNITY_STANDALONE_OSX
    /// <summary>
    /// Builds macOS releases for both Intel and Apple Silicon.
    /// </summary>
    /// <param name="target">The desktop platform being built.</param>
    private static void ApplyBuildArchitecture(UnityEditor.BuildTarget target)
    {
        if (target == UnityEditor.BuildTarget.StandaloneOSX)
        {
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor
                .Build
                .OSArchitecture
                .x64ARM64;
        }
    }
#endif

    /// <summary>
    /// Applies the release version supplied by the build environment, when present.
    /// </summary>
    private static void ApplyBuildVersion()
    {
        string buildVersion = GetOptionalArgument(_buildVersionArgument);
        if (string.IsNullOrWhiteSpace(buildVersion))
            return;

        UnityEditor.PlayerSettings.bundleVersion = buildVersion.Trim();
    }

    /// <summary>
    /// Builds and verifies an external-content player at the requested path.
    /// </summary>
    /// <param name="target">The desktop platform to build.</param>
    /// <param name="outputPath">The player artifact path.</param>
    /// <param name="enableDiagnostics">Whether the built player may report engine diagnostics.</param>
    /// <param name="enableAnalytics">Whether the built player may send opted-in analytics.</param>
    /// <param name="prepareRuntimeUI">Whether to regenerate stripped runtime UI before building.</param>
    /// <returns>The platform-correct player artifact path.</returns>
    private static string BuildPlayer(
        UnityEditor.BuildTarget target,
        string outputPath,
        bool enableDiagnostics,
        bool enableAnalytics,
        bool prepareRuntimeUI
    )
    {
        _ = GetDefaultArtifact(target);
        outputPath = NormalizeOutputPath(target, outputPath);
        if (prepareRuntimeUI)
            UIBuilderMenu.BuildRuntimeUI();
        return BuildGeneratedPlayer(
            target,
            outputPath,
            enableDiagnostics,
            new[] { _bootScenePath, _mainMenuScenePath, _strategyScenePath },
            enableAnalytics ? new[] { _analyticsScriptingDefine } : Array.Empty<string>()
        );
    }

    /// <summary>
    /// Builds the player after all generated UI and scene assets are ready.
    /// </summary>
    /// <param name="target">The desktop platform to build.</param>
    /// <param name="outputPath">The normalized player artifact path.</param>
    /// <param name="enableDiagnostics">Whether the built player may report engine diagnostics.</param>
    /// <param name="scenes">The generated scenes to include in the player.</param>
    /// <param name="extraScriptingDefines">Build-only symbols that select companion assemblies.</param>
    /// <returns>The platform-correct player artifact path.</returns>
    internal static string BuildGeneratedPlayer(
        UnityEditor.BuildTarget target,
        string outputPath,
        bool enableDiagnostics,
        string[] scenes,
        string[] extraScriptingDefines
    )
    {
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        string missingScene = scenes.FirstOrDefault(path => !File.Exists(path));
        if (missingScene != null)
            throw new FileNotFoundException("Generated player scene not found.", missingScene);

        UnityEditor.BuildPlayerOptions options = new UnityEditor.BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,
            options = UnityEditor.BuildOptions.None,
            extraScriptingDefines = extraScriptingDefines,
        };

        bool originalDiagnosticsEnabled = UnityEditor
            .EngineDiagnostics
            .EngineDiagnosticsSettings
            .enabled;
        try
        {
            UnityEditor.EngineDiagnostics.EngineDiagnosticsSettings.enabled = enableDiagnostics;
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
                throw new InvalidOperationException(
                    $"Player build output not found at {outputPath}."
                );
            }

            return outputPath;
        }
        finally
        {
            UnityEditor.EngineDiagnostics.EngineDiagnosticsSettings.enabled =
                originalDiagnosticsEnabled;
        }
    }

    /// <summary>
    /// Adds the desktop artifact extension when a build caller supplies only its base name.
    /// </summary>
    /// <param name="target">The desktop platform being built.</param>
    /// <param name="outputPath">The requested player artifact path.</param>
    /// <returns>The output path Unity creates for the target platform.</returns>
    private static string NormalizeOutputPath(UnityEditor.BuildTarget target, string outputPath)
    {
        string extension = "." + GetDefaultArtifact(target).Extension;
        return !outputPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? outputPath + extension
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
    /// <param name="report">The report.</param>
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
        return GetOptionalArgument(arguments)
            ?? throw new InvalidOperationException(
                $"Required argument missing: {string.Join(" or ", arguments)}."
            );
    }

    /// <summary>
    /// Reads an optional command-line argument value.
    /// </summary>
    /// <param name="arguments">The equivalent argument names to find.</param>
    /// <returns>The non-empty value following the argument, or null when it is absent.</returns>
    private static string GetOptionalArgument(params string[] arguments)
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

        return null;
    }

    /// <summary>
    /// Determines whether a command-line switch was supplied.
    /// </summary>
    /// <param name="argument">The switch to find.</param>
    /// <returns>True when the switch is present.</returns>
    private static bool HasArgument(string argument)
    {
        return Environment
            .GetCommandLineArgs()
            .Any(value => string.Equals(value, argument, StringComparison.OrdinalIgnoreCase));
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
