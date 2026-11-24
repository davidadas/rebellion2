using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Input;
using Rebellion.Multiplayer;
using Rebellion.Tactical;
using Rebellion.UI.SceneUI.TacticalCombat.Audio;
using Rebellion.Util.Random;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assembly = System.Reflection.Assembly;

/// <summary>
/// Builds one opt-in Windows player for isolated two-account tactical integration runs.
/// </summary>
public static class EosTacticalIntegrationPlayerBuild
{
    private const string _outputDirectoryFlag = "-eosTacticalBuildDirectory";
    private const string _harnessAssemblyName = "EosTacticalIntegration";
    private const string _harnessTypeName =
        "Rebellion.Tests.MultiplayerIntegration.EosTacticalIntegrationRunner";
    private const string _harnessDefine = "REBELLION_EOS_TACTICAL_TEST";
    private const string _harnessSourceRoot = "Assets/Tests/Runtime";
    private const string _builderSourceRoot = "Assets/Editor/Simulation/Multiplayer";
    private const string _playerName = "EosTacticalIntegration";
    private const string _identityKey = "SimulationBuild";

    /// <summary>
    /// Builds the current source into a temporary player without launching either account.
    /// </summary>
    public static void Build()
    {
        string outputDirectory = ReadOutputDirectory();
        Directory.CreateDirectory(outputDirectory);
        string manifestPath = Path.Combine(outputDirectory, "build-manifest.json");
        BuildManifest manifest = new()
        {
            playerPath = Path.Combine(outputDirectory, _playerName + ".exe"),
        };
        NamedBuildTarget namedTarget = NamedBuildTarget.Standalone;
        ScriptingImplementation originalBackend = PlayerSettings.GetScriptingBackend(namedTarget);
        ManagedStrippingLevel originalStripping = PlayerSettings.GetManagedStrippingLevel(
            namedTarget
        );
        bool originalRunInBackground = PlayerSettings.runInBackground;
        StandaloneBuildSubtarget originalSubtarget =
            EditorUserBuildSettings.standaloneBuildSubtarget;
        string scenePath =
            "Assets/Scenes/" + _playerName + "-" + Guid.NewGuid().ToString("N") + ".unity";
        try
        {
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            VerifyLoadedBuild(manifest);
            VerifyExternalInputs(manifest);
            manifest.sourceHash = HashSourceInputs();
            PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(namedTarget, ManagedStrippingLevel.Disabled);
            PlayerSettings.runInBackground = true;
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
            CreateEmptyScene(scenePath);
            StandalonePlayerBuild.BuildGeneratedPlayer(
                BuildTarget.StandaloneWindows64,
                manifest.playerPath,
                false,
                new[] { scenePath },
                new[] { _harnessDefine }
            );
            if (HashSourceInputs() != manifest.sourceHash)
                throw new InvalidOperationException(
                    "Source inputs changed during the integration player build."
                );
            VerifyPlayerArtifacts(manifest, outputDirectory);
            manifest.succeeded = true;
            Debug.Log("[EosTacticalBuild] Built integration player: " + manifest.playerPath);
        }
        catch (Exception exception)
        {
            manifest.failureType = exception.GetType().FullName;
            throw;
        }
        finally
        {
            PlayerSettings.SetScriptingBackend(namedTarget, originalBackend);
            PlayerSettings.SetManagedStrippingLevel(namedTarget, originalStripping);
            PlayerSettings.runInBackground = originalRunInBackground;
            EditorUserBuildSettings.standaloneBuildSubtarget = originalSubtarget;
            if (File.Exists(scenePath))
                AssetDatabase.DeleteAsset(scenePath);
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        }
    }

    /// <summary>
    /// Requires a named child directory of the operating system's temporary directory.
    /// </summary>
    /// <returns>The normalized directory for this disposable build.</returns>
    private static string ReadOutputDirectory()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int[] matches = arguments
            .Select((value, index) => (value, index))
            .Where(item => item.value == _outputDirectoryFlag)
            .Select(item => item.index)
            .ToArray();
        if (matches.Length != 1 || matches[0] + 1 >= arguments.Length)
            throw new ArgumentException(
                "Supply one "
                    + _outputDirectoryFlag
                    + " with an absolute temporary output directory."
            );
        string value = arguments[matches[0] + 1];
        if (!Path.IsPathRooted(value))
            throw new ArgumentException(
                "The integration player output directory must be absolute."
            );
        string output = Path.GetFullPath(value)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string temporaryRoot =
            Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!output.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "The integration player must be built inside a named temporary directory."
            );
        return output;
    }

    /// <summary>
    /// Verifies the loaded game, builder, and opt-in harness match their current source inputs.
    /// </summary>
    /// <param name="manifest">Receives nonsecret source and assembly identities.</param>
    private static void VerifyLoadedBuild(BuildManifest manifest)
    {
        if (
            EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode
        )
            throw new InvalidOperationException(
                "Finish compilation and leave Play mode before building the integration player."
            );
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException(
                "Select the Windows x64 build target before invoking the integration build entry."
            );
        if (
            !BuildPipeline.IsBuildTargetSupported(
                BuildTargetGroup.Standalone,
                BuildTarget.StandaloneWindows64
            )
        )
            throw new PlatformNotSupportedException(
                "Windows x64 player build support is not installed."
            );
        Assembly game = typeof(GameRoot).Assembly;
        Assembly editor = typeof(EosTacticalIntegrationPlayerBuild).Assembly;
        Assembly harness =
            AppDomain
                .CurrentDomain.GetAssemblies()
                .SingleOrDefault(assembly => assembly.GetName().Name == _harnessAssemblyName)
            ?? throw new InvalidOperationException(
                "The integration harness has not finished compiling."
            );
        if (harness.GetType(_harnessTypeName) == null)
            throw new InvalidOperationException(
                "The integration assembly does not contain its agreed runtime entry."
            );
        VerifyAssemblyFreshness(game);
        VerifyAssemblyFreshness(typeof(CombatSide).Assembly);
        VerifyAssemblyFreshness(typeof(TacticalCombatDefinition).Assembly);
        VerifyAssemblyFreshness(typeof(TacticalCombatSession).Assembly);
        VerifyAssemblyFreshness(typeof(TacticalCombatAudio).Assembly);
        VerifyAssemblyFreshness(typeof(IRandomNumberProvider).Assembly);
        VerifyAssemblyFreshness(typeof(PlayerInputActions).Assembly);
        VerifyAssemblyFreshness(typeof(IMultiplayerPeer).Assembly);
        VerifyAssemblyFreshness(editor);
        VerifyAssemblyFreshness(harness);
        VerifyDirectoryFreshness(editor, _builderSourceRoot);
        VerifyDirectoryFreshness(harness, _harnessSourceRoot);
        string identity = game.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == _identityKey)
            .Value;
        Type inputs = AppDomain
            .CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "Unity.Rebellion.Build.CodeGen")
            .GetType("SimulationBuildIdentityInputs", true);
        string expected = (string)
            inputs
                .GetMethod("ComputeIdentity", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { Directory.GetCurrentDirectory() });
        if (identity != expected)
            throw new InvalidOperationException(
                "The loaded game source fingerprint is stale; finish a clean script compilation first."
            );
        manifest.gameBuildIdentity = identity;
        manifest.gameAssemblyMvid = game.ManifestModule.ModuleVersionId.ToString();
        manifest.editorAssemblyMvid = editor.ManifestModule.ModuleVersionId.ToString();
        manifest.harnessAssemblyMvid = harness.ManifestModule.ModuleVersionId.ToString();
        manifest.gameAssemblyHash = HashFile(game.Location);
        manifest.harnessAssemblyHash = HashFile(harness.Location);
    }

    /// <summary>
    /// Checks the compiled assembly against the files owned by Unity's current compilation graph.
    /// </summary>
    /// <param name="loaded">The assembly used by this Editor invocation.</param>
    private static void VerifyAssemblyFreshness(Assembly loaded)
    {
        UnityEditor.Compilation.Assembly compilation = CompilationPipeline
            .GetAssemblies(AssembliesType.Editor)
            .Single(assembly => assembly.name == loaded.GetName().Name);
        DateTime compiled = File.GetLastWriteTimeUtc(loaded.Location);
        string definition = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(
            compilation.name
        );
        if (
            compilation.sourceFiles.Any(path => File.GetLastWriteTimeUtc(path) > compiled)
            || !string.IsNullOrEmpty(definition) && File.GetLastWriteTimeUtc(definition) > compiled
        )
            throw new InvalidOperationException(
                "A loaded integration dependency predates its current sources: " + compilation.name
            );
    }

    /// <summary>
    /// Rejects newly added or changed verification files that have not reached their loaded assembly.
    /// </summary>
    /// <param name="loaded">The loaded verification assembly.</param>
    /// <param name="directory">The corresponding verification source directory.</param>
    private static void VerifyDirectoryFreshness(Assembly loaded, string directory)
    {
        DateTime compiled = File.GetLastWriteTimeUtc(loaded.Location);
        if (
            Directory
                .GetFiles(directory, "*", SearchOption.AllDirectories)
                .Any(path =>
                    Path.GetExtension(path) is ".cs" or ".asmdef"
                    && File.GetLastWriteTimeUtc(path) > compiled
                )
        )
            throw new InvalidOperationException(
                "The loaded verification assembly predates its current files: "
                    + loaded.GetName().Name
            );
    }

    /// <summary>
    /// Requires current external content and local EOS configuration.
    /// </summary>
    /// <param name="manifest">Receives the nonsecret content identity.</param>
    private static void VerifyExternalInputs(BuildManifest manifest)
    {
        manifest.contentRoot = ContentPackLoader.OpenActive().ContentRootPath;
        if (!File.Exists(Path.Combine(manifest.contentRoot, "catalog.xml")))
            throw new FileNotFoundException("The active external content catalog is missing.");
        string configurationDirectory = Path.Combine(Application.streamingAssetsPath, "EOS");
        if (
            !Directory.Exists(configurationDirectory)
            || !Directory.EnumerateFiles(configurationDirectory, "*.json").Any()
        )
            throw new FileNotFoundException(
                "Install the local EOS configuration in the ignored StreamingAssets/EOS directory before building."
            );
        manifest.mediaRevision = File.ReadAllText(".github/media-revision").Trim();
    }

    /// <summary>
    /// Saves an empty ignored scene without replacing or saving any existing authoring scene.
    /// </summary>
    /// <param name="scenePath">The unique generated scene asset path.</param>
    private static void CreateEmptyScene(string scenePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
        Scene active = SceneManager.GetActiveScene();
        bool replacesEmptyBatchScene =
            Application.isBatchMode
            && SceneManager.sceneCount == 1
            && active.IsValid()
            && active.isLoaded
            && !active.isDirty
            && string.IsNullOrEmpty(active.path);
        if (
            !replacesEmptyBatchScene
            && Enumerable
                .Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(scene => scene.isLoaded && string.IsNullOrEmpty(scene.path))
        )
            throw new InvalidOperationException(
                "Save untitled authoring scenes before building the integration player."
            );
        Scene generated = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            replacesEmptyBatchScene ? NewSceneMode.Single : NewSceneMode.Additive
        );
        try
        {
            if (!EditorSceneManager.SaveScene(generated, scenePath))
                throw new IOException("Could not save the integration player's temporary scene.");
        }
        finally
        {
            if (replacesEmptyBatchScene)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else
            {
                if (active.IsValid() && active.isLoaded)
                    SceneManager.SetActiveScene(active);
                EditorSceneManager.CloseScene(generated, true);
            }
        }
    }

    /// <summary>
    /// Confirms the opt-in harness, tactical assembly, and EOS SDK reached the completed player.
    /// </summary>
    /// <param name="manifest">Receives the built artifact hashes.</param>
    /// <param name="outputDirectory">The disposable player output directory.</param>
    private static void VerifyPlayerArtifacts(BuildManifest manifest, string outputDirectory)
    {
        string data = Path.Combine(outputDirectory, _playerName + "_Data");
        manifest.playerHash = HashFile(manifest.playerPath);
        manifest.builtGameModelAssemblyHash = HashFile(
            Path.Combine(data, "Managed", "Rebellion.Game.dll")
        );
        manifest.builtHarnessAssemblyHash = HashFile(
            Path.Combine(data, "Managed", _harnessAssemblyName + ".dll")
        );
        manifest.builtTacticalAssemblyHash = HashFile(
            Path.Combine(data, "Managed", "Rebellion.Tactical.Core.dll")
        );
        manifest.eosSdkHash = HashFile(
            Directory
                .GetFiles(data, "EOSSDK-Win64-Shipping.dll", SearchOption.AllDirectories)
                .Single()
        );
        string configuration = Path.Combine(data, "StreamingAssets", "EOS");
        if (
            !Directory.Exists(configuration)
            || !Directory.EnumerateFiles(configuration, "*.json").Any()
        )
            throw new InvalidOperationException(
                "The integration player is missing its local EOS configuration."
            );
    }

    /// <summary>
    /// Fingerprints source, assembly definitions, dependency locks, and the selected media revision.
    /// </summary>
    /// <returns>The normalized digest shared by the prebuild and postbuild checks.</returns>
    private static string HashSourceInputs()
    {
        string[] dependencies =
        {
            "Packages/manifest.json",
            "Packages/packages-lock.json",
            "ProjectSettings/ProjectVersion.txt",
            ".github/media-revision",
        };
        string[] roots = { "Assets/Scripts", "Assets/Input", "Assets/Editor", _harnessSourceRoot };
        StringBuilder inputs = new();
        foreach (
            string file in roots
                .SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                .Where(path => Path.GetExtension(path) is ".cs" or ".asmdef")
                .Concat(dependencies)
                .OrderBy(path => path, StringComparer.Ordinal)
        )
            inputs
                .Append(file.Replace('\\', '/'))
                .Append('\0')
                .Append(File.ReadAllText(file).Replace("\r\n", "\n").Replace('\r', '\n'))
                .Append('\0');
        using SHA256 hash = SHA256.Create();
        return BitConverter
            .ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(inputs.ToString())))
            .Replace("-", string.Empty);
    }

    /// <summary>
    /// Hashes one compiled or native artifact without recording its contents.
    /// </summary>
    /// <param name="path">The existing artifact path.</param>
    /// <returns>The artifact's SHA-256 digest.</returns>
    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
    }

    [Serializable]
    private sealed class BuildManifest
    {
        public bool succeeded;
        public string failureType;
        public string playerPath;
        public string contentRoot;
        public string mediaRevision;
        public string sourceHash;
        public string gameBuildIdentity;
        public string gameAssemblyMvid;
        public string editorAssemblyMvid;
        public string harnessAssemblyMvid;
        public string gameAssemblyHash;
        public string harnessAssemblyHash;
        public string playerHash;
        public string builtGameModelAssemblyHash;
        public string builtHarnessAssemblyHash;
        public string builtTacticalAssemblyHash;
        public string eosSdkHash;
    }
}
