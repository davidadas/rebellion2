using System;
using System.IO;
using UnityEditor;

/// <summary>
/// Builds the standalone content-pack editor beside a Rebellion 2 player artifact.
/// </summary>
public static class ContentPackEditorBuild
{
    private const string _editorProductName = "Rebellion 2 Content Pack Editor";
    private const string _editorIconPath = "Assets/GameIcon/rebellion2_editor_icon.png";
    private const string _editorScriptingDefine = "REBELLION_CONTENT_PACK_EDITOR";
    private const string _buildPlayerPathArgument = "-buildPlayerPath";

    /// <summary>
    /// Builds the standalone editor requested by the Unity command line.
    /// </summary>
    public static void Build()
    {
        UnityEditor.BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string outputPath = GetRequiredArgument(_buildPlayerPathArgument);
        BuildPlayer(target, outputPath, false);
    }

    /// <summary>
    /// Builds and runs the editor for the active desktop target.
    /// </summary>
    [MenuItem("Rebellion/Build/Build and Run Content Pack Editor", false, 101)]
    public static void BuildFromEditor()
    {
        UnityEditor.BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string outputPath = EditorUtility.SaveFilePanel(
            "Build Rebellion 2 Content Pack Editor",
            Path.Combine(Directory.GetCurrentDirectory(), "build"),
            GetArtifactFileName(target),
            GetArtifactExtension(target)
        );
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        string builtPath = BuildPlayer(target, outputPath, false);
        StandalonePlayerBuild.LaunchPlayer(
            target,
            builtPath,
            Path.Combine(UnityEngine.Application.dataPath, "Content")
        );
    }

    /// <summary>
    /// Builds the editor artifact beside an already built game player.
    /// </summary>
    /// <param name="target">The desktop build target.</param>
    /// <param name="gamePlayerPath">The game player artifact path.</param>
    /// <param name="enableDiagnostics">Whether engine diagnostics remain enabled.</param>
    /// <returns>The editor artifact path.</returns>
    internal static string BuildBesideGame(
        UnityEditor.BuildTarget target,
        string gamePlayerPath,
        bool enableDiagnostics
    )
    {
        if (target == UnityEditor.BuildTarget.StandaloneOSX)
            return BuildEmbeddedMacEditor(gamePlayerPath, enableDiagnostics);

        string directory = Path.GetDirectoryName(gamePlayerPath) ?? string.Empty;
        string editorPath = Path.Combine(directory, GetArtifactFileName(target));
        return BuildPlayer(target, editorPath, enableDiagnostics);
    }

    /// <summary>
    /// Removes legacy companion compiler diagnostics from a host bundle before it is signed.
    /// </summary>
    /// <param name="target">The host player's desktop platform.</param>
    /// <param name="gamePlayerPath">The normalized host player artifact path.</param>
    internal static void PrepareGameBuild(UnityEditor.BuildTarget target, string gamePlayerPath)
    {
        if (target != UnityEditor.BuildTarget.StandaloneOSX)
            return;

        string diagnostics = Path.Combine(
            gamePlayerPath,
            "Contents",
            "Helpers",
            _editorProductName + "_BurstDebugInformation_DoNotShip"
        );
        if (Directory.Exists(diagnostics))
            Directory.Delete(diagnostics, true);
    }

    /// <summary>
    /// Builds the companion outside its host, embeds only the completed app, and signs the host again.
    /// </summary>
    /// <param name="gamePlayerPath">The completed host app bundle.</param>
    /// <param name="enableDiagnostics">Whether engine diagnostics remain enabled.</param>
    /// <returns>The embedded companion app path.</returns>
    private static string BuildEmbeddedMacEditor(string gamePlayerPath, bool enableDiagnostics)
    {
        string stagingDirectory = Path.Combine(
            Path.GetDirectoryName(gamePlayerPath) ?? string.Empty,
            ".rebellion2-editor-" + Guid.NewGuid().ToString("N")
        );
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            string fileName = GetArtifactFileName(UnityEditor.BuildTarget.StandaloneOSX);
            string builtPath = BuildPlayer(
                UnityEditor.BuildTarget.StandaloneOSX,
                Path.Combine(stagingDirectory, fileName),
                enableDiagnostics
            );
            string helpers = Path.Combine(gamePlayerPath, "Contents", "Helpers");
            Directory.CreateDirectory(helpers);
            string embeddedPath = Path.Combine(helpers, fileName);
            if (Directory.Exists(embeddedPath))
                Directory.Delete(embeddedPath, true);
            Directory.Move(builtPath, embeddedPath);
#if UNITY_STANDALONE_OSX
            UnityEditor.OSXStandalone.MacOSCodeSigning.CodeSignAppBundle(gamePlayerPath);
#endif
            return embeddedPath;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, true);
        }
    }

    /// <summary>
    /// Generates the editor scene and builds its standalone player.
    /// </summary>
    /// <param name="target">The desktop build target.</param>
    /// <param name="outputPath">The requested editor artifact path.</param>
    /// <param name="enableDiagnostics">Whether engine diagnostics remain enabled.</param>
    /// <returns>The platform-correct editor artifact path.</returns>
    private static string BuildPlayer(
        UnityEditor.BuildTarget target,
        string outputPath,
        bool enableDiagnostics
    )
    {
        UIBuilderMenu.BuildRuntimeContentPackEditor();
        string originalProductName = PlayerSettings.productName;
        string originalIdentifier = PlayerSettings.GetApplicationIdentifier(
            UnityEditor.Build.NamedBuildTarget.Standalone
        );
        bool originalResizableWindow = PlayerSettings.resizableWindow;
        UnityEngine.FullScreenMode originalFullScreenMode = PlayerSettings.fullScreenMode;
        int originalScreenWidth = PlayerSettings.defaultScreenWidth;
        int originalScreenHeight = PlayerSettings.defaultScreenHeight;
        UnityEngine.Texture2D[] originalIcons = PlayerSettings.GetIcons(
            UnityEditor.Build.NamedBuildTarget.Standalone,
            IconKind.Any
        );
        UnityEngine.Texture2D[] originalDefaultIcons = PlayerSettings.GetIcons(
            UnityEditor.Build.NamedBuildTarget.Unknown,
            IconKind.Any
        );
        try
        {
            UnityEngine.Texture2D icon =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(_editorIconPath)
                ?? throw new FileNotFoundException(
                    "Content editor icon not found.",
                    _editorIconPath
                );
            PlayerSettings.SetIcons(
                UnityEditor.Build.NamedBuildTarget.Unknown,
                new[] { icon },
                IconKind.Any
            );
            PlayerSettings.SetIcons(
                UnityEditor.Build.NamedBuildTarget.Standalone,
                Array.Empty<UnityEngine.Texture2D>(),
                IconKind.Any
            );
            ConfigureNativeDialogPlugins(true);
            PlayerSettings.productName = _editorProductName;
            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Standalone,
                originalIdentifier + ".editor"
            );
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            string builtPath = StandalonePlayerBuild.BuildGeneratedPlayer(
                target,
                outputPath,
                enableDiagnostics,
                new[] { ContentPackEditorPrefabBuilder.ScenePath },
                new[] { _editorScriptingDefine }
            );
            CopyNativeDialogNotices(target, builtPath);
#if UNITY_STANDALONE_OSX
            if (target == UnityEditor.BuildTarget.StandaloneOSX)
                UnityEditor.OSXStandalone.MacOSCodeSigning.CodeSignAppBundle(builtPath);
#endif
            return builtPath;
        }
        finally
        {
            PlayerSettings.SetIcons(
                UnityEditor.Build.NamedBuildTarget.Unknown,
                originalDefaultIcons,
                IconKind.Any
            );
            PlayerSettings.SetIcons(
                UnityEditor.Build.NamedBuildTarget.Standalone,
                originalIcons,
                IconKind.Any
            );
            ConfigureNativeDialogPlugins(false);
            PlayerSettings.productName = originalProductName;
            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Standalone,
                originalIdentifier
            );
            PlayerSettings.resizableWindow = originalResizableWindow;
            PlayerSettings.fullScreenMode = originalFullScreenMode;
            PlayerSettings.defaultScreenWidth = originalScreenWidth;
            PlayerSettings.defaultScreenHeight = originalScreenHeight;
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// Excludes editor dialog libraries from ordinary players until an editor build requests them.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void ExcludeNativeDialogPlugins()
    {
        ConfigureNativeDialogPlugins(false);
    }

    /// <summary>
    /// Restricts native file dialog libraries to the companion editor artifact.
    /// </summary>
    /// <param name="include">Whether an editor player is being built.</param>
    private static void ConfigureNativeDialogPlugins(bool include)
    {
        foreach (PluginImporter plugin in PluginImporter.GetAllImporters())
            if (
                plugin.assetPath.StartsWith(
                    "Assets/Plugins/NativeFileDialog/",
                    StringComparison.Ordinal
                )
            )
                plugin.SetIncludeInBuildDelegate(_ => include);
    }

    /// <summary>
    /// Includes the native dialog dependency's license and version notice with the editor player.
    /// </summary>
    /// <param name="target">The desktop platform.</param>
    /// <param name="playerPath">The completed editor artifact.</param>
    private static void CopyNativeDialogNotices(UnityEditor.BuildTarget target, string playerPath)
    {
        string dataPath =
            target == UnityEditor.BuildTarget.StandaloneOSX
                ? Path.Combine(playerPath, "Contents", "Resources")
                : Path.Combine(
                    Path.GetDirectoryName(playerPath),
                    Path.GetFileNameWithoutExtension(playerPath) + "_Data"
                );
        string noticesPath = Path.Combine(dataPath, "ThirdPartyNotices");
        Directory.CreateDirectory(noticesPath);
        string sourcePath = Path.Combine(
            UnityEngine.Application.dataPath,
            "Plugins",
            "NativeFileDialog"
        );
        File.WriteAllText(
            Path.Combine(noticesPath, "NativeFileDialog.txt"),
            File.ReadAllText(Path.Combine(sourcePath, "NOTICE.txt"))
                + Environment.NewLine
                + File.ReadAllText(Path.Combine(sourcePath, "LICENSE.txt"))
        );
    }

    /// <summary>
    /// Returns the editor artifact file name for a supported desktop target.
    /// </summary>
    /// <param name="target">The desktop build target.</param>
    /// <returns>The platform-specific artifact name.</returns>
    private static string GetArtifactFileName(UnityEditor.BuildTarget target)
    {
        return target switch
        {
            UnityEditor.BuildTarget.StandaloneWindows => "rebellion2-editor.exe",
            UnityEditor.BuildTarget.StandaloneWindows64 => "rebellion2-editor.exe",
            UnityEditor.BuildTarget.StandaloneLinux64 => "rebellion2-editor.x86_64",
            UnityEditor.BuildTarget.StandaloneOSX => "rebellion2-editor.app",
            _ => throw new InvalidOperationException(
                $"Content-pack editor builds do not support target '{target}'."
            ),
        };
    }

    /// <summary>
    /// Returns the file-panel extension for a supported desktop target.
    /// </summary>
    /// <param name="target">The desktop build target.</param>
    /// <returns>The artifact extension without a leading period.</returns>
    private static string GetArtifactExtension(UnityEditor.BuildTarget target)
    {
        return target switch
        {
            UnityEditor.BuildTarget.StandaloneWindows => "exe",
            UnityEditor.BuildTarget.StandaloneWindows64 => "exe",
            UnityEditor.BuildTarget.StandaloneLinux64 => "x86_64",
            UnityEditor.BuildTarget.StandaloneOSX => "app",
            _ => throw new InvalidOperationException(
                $"Content-pack editor builds do not support target '{target}'."
            ),
        };
    }

    /// <summary>
    /// Reads a required value from the Unity command line.
    /// </summary>
    /// <param name="name">The command-line argument name.</param>
    /// <returns>The following argument value.</returns>
    private static string GetRequiredArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(arguments[index + 1]);
        }
        throw new ArgumentException($"Required command-line argument '{name}' is missing.");
    }
}
