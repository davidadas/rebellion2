using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Rebellion.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;

/// <summary>
/// Recompiles the game when fingerprint inputs change outside its normal compilation dependencies.
/// </summary>
[InitializeOnLoad]
internal sealed class SimulationBuildIdentityInvalidator : IPreprocessBuildWithReport
{
    private static readonly List<FileSystemWatcher> _watchers = new();
    private static volatile bool _dirty = true;
    private static bool _compilationRequested;

    public int callbackOrder => 0;

    /// <summary>
    /// Watches all declared source inputs and checks the loaded identity on the editor thread.
    /// </summary>
    static SimulationBuildIdentityInvalidator()
    {
        Watch(SimulationBuildIdentityInputs.SourceRoot, "*", true);
        foreach (string input in SimulationBuildIdentityInputs.DependencyInputs)
        {
            if (
                !input.StartsWith(
                    SimulationBuildIdentityInputs.SourceRoot + "/",
                    StringComparison.Ordinal
                )
            )
                Watch(Path.GetDirectoryName(input), Path.GetFileName(input), false);
        }
        EditorApplication.update += Refresh;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Dispose;
    }

    /// <summary>
    /// Blocks player builds until the loaded game assembly matches every fingerprint input.
    /// </summary>
    /// <param name="report">The pending player build.</param>
    public void OnPreprocessBuild(BuildReport report)
    {
        if (RecompileIfStale())
            throw new BuildFailedException(
                "Simulation identity changed. Let script compilation finish, then build again."
            );
    }

    /// <summary>
    /// Coalesces file notifications and checks once the editor has finished importing and compiling.
    /// </summary>
    private static void Refresh()
    {
        if (
            !_dirty
            || _compilationRequested
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
        )
            return;
        _dirty = false;
        RecompileIfStale();
    }

    /// <summary>
    /// Prevents entering gameplay with a stale compiled identity.
    /// </summary>
    /// <param name="state">The pending play mode transition.</param>
    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode && RecompileIfStale())
            EditorApplication.isPlaying = false;
    }

    /// <summary>
    /// Requests a complete compilation only when the loaded fingerprint no longer matches its inputs.
    /// </summary>
    /// <returns>True while a matching game assembly still needs to be compiled.</returns>
    private static bool RecompileIfStale()
    {
        if (_compilationRequested)
            return true;
        string actual = typeof(GameRoot)
            .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == SimulationBuildIdentityInputs.MetadataKey)
            ?.Value;
        if (actual == SimulationBuildIdentityInputs.ComputeIdentity())
            return false;
        _compilationRequested = true;
        CompilationPipeline.RequestScriptCompilation(
            RequestScriptCompilationOptions.CleanBuildCache
        );
        return true;
    }

    /// <summary>
    /// Creates a notification source without doing hashing or calling Unity from its worker thread.
    /// </summary>
    /// <param name="directory">The project-relative input directory.</param>
    /// <param name="filter">The file filter for this input.</param>
    /// <param name="includeSubdirectories">Whether nested source directories are watched.</param>
    private static void Watch(string directory, string filter, bool includeSubdirectories)
    {
        FileSystemWatcher watcher = new(Path.GetFullPath(directory), filter)
        {
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter =
                NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
        };
        watcher.Changed += MarkDirty;
        watcher.Created += MarkDirty;
        watcher.Deleted += MarkDirty;
        watcher.Renamed += MarkDirty;
        watcher.Error += MarkDirty;
        watcher.EnableRaisingEvents = true;
        _watchers.Add(watcher);
    }

    /// <summary>
    /// Marks input changes for processing on the editor thread, including notification overflow.
    /// </summary>
    /// <param name="sender">The input watcher.</param>
    /// <param name="args">The file notification.</param>
    private static void MarkDirty(object sender, EventArgs args) => _dirty = true;

    /// <summary>
    /// Releases notification handles and editor callbacks before the script domain reloads.
    /// </summary>
    private static void Dispose()
    {
        EditorApplication.update -= Refresh;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        foreach (FileSystemWatcher watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
    }
}
