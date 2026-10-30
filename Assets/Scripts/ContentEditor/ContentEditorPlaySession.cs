using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
/// Owns a frozen working-pack copy and an isolated profile for one desktop test game.
/// </summary>
public sealed class ContentEditorPlaySession : IDisposable
{
    private Process process;
    private bool disposed;
    private readonly string contentRoot;

    public string DirectoryPath { get; }
    public string RequestPath => Path.Combine(DirectoryPath, "launch.xml");
    public string ProfilePath => Path.Combine(DirectoryPath, "UserData");
    public string PackPath => Path.Combine(DirectoryPath, "Pack");

    /// <summary>
    /// Takes ownership of one newly created, private test workspace.
    /// </summary>
    /// <param name="directory">The fixture-independent temporary directory.</param>
    /// <param name="contentRootPath">The shared, read-only application and base content.</param>
    private ContentEditorPlaySession(string directory, string contentRootPath)
    {
        DirectoryPath = directory;
        contentRoot = contentRootPath;
    }

    /// <summary>
    /// Freezes current edits and checks the requested campaign without saving the author's project.
    /// </summary>
    /// <param name="project">The current working content.</param>
    /// <param name="options">The selected scenario, faction, size and difficulty.</param>
    /// <param name="cancellationToken">Cancellation before the game starts.</param>
    /// <returns>The prepared test workspace, owned by the caller until launch succeeds.</returns>
    public static async Task<ContentEditorPlaySession> PrepareAsync(
        ContentPackEditorProject project,
        ContentEditorPlayOptions options,
        CancellationToken cancellationToken = default
    )
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        if (
            !Enum.IsDefined(typeof(GameSize), options.GalaxySize)
            || !Enum.IsDefined(typeof(GameDifficulty), options.Difficulty)
        )
            throw new ArgumentException(
                "Select a valid galaxy size and difficulty.",
                nameof(options)
            );
        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.Combine(
            Path.GetTempPath(),
            "rebellion2-play-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(directory);
        ContentEditorPlaySession session = new ContentEditorPlaySession(
            directory,
            project.ContentRootPath
        );
        try
        {
            Directory.CreateDirectory(session.PackPath);
            await ContentPackEditorExporter.WriteSnapshotAsync(
                project,
                session.PackPath,
                cancellationToken
            );
            cancellationToken.ThrowIfCancellationRequested();
            ContentPack pack = ContentPackLoader.OpenFolder(
                project.ContentRootPath,
                session.PackPath,
                options.ScenarioID
            );
            ContentEditorPackValidator.ValidateDefinitions(pack);
            GameSummary summary = GameLaunchContext.CreateDefaultSummary(pack);
            if (!pack.Scenario.PlayableFactionIDs.Contains(options.FactionID))
                throw new ArgumentException(
                    "Select a playable faction for this scenario.",
                    nameof(options)
                );
            summary.PlayerFactionID = options.FactionID;
            summary.GalaxySize = options.GalaxySize;
            summary.Difficulty = options.Difficulty;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            ContentEditorPackValidator.ValidateStartup(pack, summary);
            Directory.CreateDirectory(session.ProfilePath);
            using FileStream output = new FileStream(
                session.RequestPath,
                FileMode.CreateNew,
                FileAccess.Write
            );
            new GameSerializer(typeof(NewGameLaunchRequest)).Serialize(
                output,
                new NewGameLaunchRequest { PackDirectory = session.PackPath, Summary = summary }
            );
            cancellationToken.ThrowIfCancellationRequested();
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Starts the bundled game and retains its temporary content until that process exits.
    /// </summary>
    /// <param name="dataPath">The editor player's Unity data path.</param>
    /// <param name="platform">The desktop player platform.</param>
    public void Launch(string dataPath, RuntimePlatform platform)
    {
        if (disposed || process != null)
            throw new InvalidOperationException(
                "This test session has already been launched or closed."
            );
        ProcessStartInfo startInfo = CreateStartInfo(dataPath, platform);
        process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Exited += HandleGameExited;
        try
        {
            if (!process.Start())
                throw new IOException("The test game could not be started.");
        }
        catch
        {
            process.Exited -= HandleGameExited;
            process.Dispose();
            process = null;
            throw;
        }
    }

    /// <summary>
    /// Resolves the companion game artifact and supplies structured launch arguments.
    /// </summary>
    /// <param name="dataPath">The editor player's Unity data path.</param>
    /// <param name="platform">The desktop player platform.</param>
    /// <returns>The native game process configuration.</returns>
    public ProcessStartInfo CreateStartInfo(string dataPath, RuntimePlatform platform)
    {
        string playerDirectory =
            Directory.GetParent(Path.GetFullPath(dataPath))?.FullName
            ?? throw new IOException("The editor's installation directory could not be found.");
        ProcessStartInfo info;
        if (platform == RuntimePlatform.OSXPlayer)
        {
            string gameBundle =
                Directory.GetParent(playerDirectory)?.Parent?.Parent?.FullName
                ?? throw new IOException("The companion game application could not be found.");
            if (
                !gameBundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(gameBundle)
            )
                throw new IOException("The companion game application could not be found.");
            info = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
            info.ArgumentList.Add("-n");
            info.ArgumentList.Add("-W");
            info.ArgumentList.Add(gameBundle);
            info.ArgumentList.Add("--args");
        }
        else
        {
            string gameName = platform switch
            {
                RuntimePlatform.WindowsPlayer => "rebellion2.exe",
                RuntimePlatform.LinuxPlayer => "rebellion2.x86_64",
                _ => throw new InvalidOperationException(
                    "Run test games from the built desktop editor."
                ),
            };
            string executable = Path.Combine(playerDirectory, gameName);
            if (!File.Exists(executable))
                throw new FileNotFoundException(
                    "The game executable is missing beside the editor.",
                    executable
                );
            info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                WorkingDirectory = playerDirectory,
            };
        }
        info.ArgumentList.Add("-contentPath");
        info.ArgumentList.Add(contentRoot);
        info.ArgumentList.Add("-newGame");
        info.ArgumentList.Add(RequestPath);
        info.ArgumentList.Add("-userDataPath");
        info.ArgumentList.Add(ProfilePath);
        info.ArgumentList.Add("-logFile");
        info.ArgumentList.Add(Path.Combine(DirectoryPath, "Player.log"));
        return info;
    }

    /// <summary>
    /// Releases the snapshot only after the child process has closed its content files.
    /// </summary>
    /// <param name="sender">The completed desktop process.</param>
    /// <param name="eventArgs">The process completion notification.</param>
    private void HandleGameExited(object sender, EventArgs eventArgs)
    {
        try
        {
            Dispose();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Test game cleanup could not complete: " + exception.Message);
        }
    }

    /// <summary>
    /// Deletes only this session's private workspace, leaving running child games and authored files intact.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;
        if (process?.HasExited == false)
            return;
        disposed = true;
        if (process != null)
        {
            process.Exited -= HandleGameExited;
            process.Dispose();
            process = null;
        }
        if (Directory.Exists(DirectoryPath))
            Directory.Delete(DirectoryPath, true);
    }
}
