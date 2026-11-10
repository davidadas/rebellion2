using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using JObject = Newtonsoft.Json.Linq.JObject;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Reuses launcher store sign-in and caches the resulting player session for multiplayer.
    /// </summary>
    public sealed class StorePlayerAuthentication : IDisposable
    {
        private readonly string _launcherPath;
        private readonly string _sessionPath;
        private readonly CancellationTokenSource _lifetime = new();
        private Task<StorePlayerSession> _signingIn;
        private StorePlayerSession _session;

        public StorePlayerSession Session =>
            _session?.ExpiresAt > DateTimeOffset.UtcNow ? _session : null;

        /// <summary>
        /// Resolves the launcher and session cache for this installation or editor checkout.
        /// </summary>
        public StorePlayerAuthentication()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _launcherPath = Path.Combine(
                root,
                Application.platform == RuntimePlatform.WindowsPlayer
                || Application.platform == RuntimePlatform.WindowsEditor
                    ? "rebellion2-launcher.exe"
                    : "rebellion2-launcher"
            );
            _sessionPath = Path.Combine(root, ".player-session.json");
            if (Application.isEditor)
            {
                string configPath = Path.Combine(
                    Application.streamingAssetsPath,
                    "EOS",
                    "player_auth_config.json"
                );
                if (File.Exists(configPath))
                {
                    JObject config = JObject.Parse(File.ReadAllText(configPath));
                    _launcherPath = config.Value<string>("launcherPath") ?? _launcherPath;
                    _sessionPath = config.Value<string>("sessionPath") ?? _sessionPath;
                }
            }
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (arguments[index] == "-playerSessionFile")
                    _sessionPath = Path.GetFullPath(arguments[index + 1]);
                if (arguments[index] == "-playerLauncher")
                    _launcherPath = Path.GetFullPath(arguments[index + 1]);
            }
            _session = ReadSession(_sessionPath);
        }

        /// <summary>
        /// Opens store selection in the launcher and saves a successfully completed player sign-in.
        /// </summary>
        /// <returns>The newly authenticated store session.</returns>
        public Task<StorePlayerSession> SignInAsync()
        {
            if (_signingIn?.IsCompleted != false)
                _signingIn = SignInCoreAsync();
            return _signingIn;
        }

        /// <summary>
        /// Cancels an outstanding sign-in when the application owner shuts down.
        /// </summary>
        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        /// <summary>
        /// Waits for the authentication window without blocking the Unity main thread.
        /// </summary>
        /// <returns>The completed store session.</returns>
        private async Task<StorePlayerSession> SignInCoreAsync()
        {
            if (!File.Exists(_launcherPath))
                throw new InvalidOperationException(
                    "The Rebellion 2 launcher is required for store sign-in."
                );
            Directory.CreateDirectory(Path.GetDirectoryName(_sessionPath));
            string resultPath = _sessionPath + "." + Guid.NewGuid().ToString("N");
            CancellationToken cancellationToken = _lifetime.Token;
            try
            {
                using Process process = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = _launcherPath,
                        Arguments = "--auth-only --session-file " + QuoteArgument(resultPath),
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(_launcherPath),
                    }
                );
                if (process == null)
                    throw new InvalidOperationException("The sign-in window could not be opened.");
                using CancellationTokenRegistration registration = cancellationToken.Register(() =>
                    CloseSignIn(process)
                );
                await Task.Run(process.WaitForExit, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                StorePlayerSession session = ReadSession(resultPath);
                if (process.ExitCode != 0 || session == null)
                    throw new InvalidOperationException(
                        "Store sign-in was not completed. Please try again."
                    );
                File.WriteAllText(_sessionPath, File.ReadAllText(resultPath));
                _session = session;
                return session;
            }
            finally
            {
                if (File.Exists(resultPath))
                    File.Delete(resultPath);
            }
        }

        /// <summary>
        /// Reads a usable cached session without preventing startup when the file is unavailable.
        /// </summary>
        /// <param name="path">The session file path.</param>
        /// <returns>The cached session, or null when sign-in is needed.</returns>
        private static StorePlayerSession ReadSession(string path)
        {
            try
            {
                return File.Exists(path)
                    ? StorePlayerSession.Parse(File.ReadAllText(path), DateTimeOffset.UtcNow)
                    : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Quotes one process argument while preserving embedded quotes and trailing directory separators.
        /// </summary>
        /// <param name="value">The argument value.</param>
        /// <returns>A quoted argument accepted by the process argument parser.</returns>
        private static string QuoteArgument(string value)
        {
            System.Text.StringBuilder result = new("\"");
            int slashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    slashes++;
                    continue;
                }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                result.Append(character);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        /// <summary>
        /// Closes the owned sign-in process during application shutdown.
        /// </summary>
        /// <param name="process">The authentication process.</param>
        private static void CloseSignIn(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (InvalidOperationException) { }
        }
    }
}
