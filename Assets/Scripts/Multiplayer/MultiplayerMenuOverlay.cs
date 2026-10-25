using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Provides a temporary runtime main-menu surface for hosting, browsing, and joining games.
    /// </summary>
    public sealed class MultiplayerMenuOverlay : MonoBehaviour
    {
        private const int _defaultPort = 27888;
        private const int _windowID = 2788801;
        private MultiplayerCoordinator _coordinator;
        private Rect _windowRect = new Rect(24f, 70f, 470f, 540f);
        private Vector2 _scrollPosition;
        private string _playerName = Environment.UserName;
        private string _port = _defaultPort.ToString(CultureInfo.InvariantCulture);
        private string _matchmakerUrl = "http://127.0.0.1:8787";
        private string _directAddress = $"127.0.0.1:{_defaultPort}";
        private bool _visible;

        /// <summary>
        /// Connects the overlay to the persistent multiplayer coordinator.
        /// </summary>
        /// <param name="coordinator">The coordinator serving menu requests.</param>
        public void Initialize(MultiplayerCoordinator coordinator)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        /// <summary>
        /// Draws the temporary multiplayer entry point and modal controls.
        /// </summary>
        private void OnGUI()
        {
            if (_coordinator == null)
                return;

            if (GUI.Button(new Rect(24f, 24f, 150f, 36f), "Multiplayer"))
                _visible = !_visible;
            if (_visible)
                _windowRect = GUI.Window(_windowID, _windowRect, DrawWindow, "Multiplayer");
        }

        /// <summary>
        /// Draws the contents of the multiplayer modal window.
        /// </summary>
        /// <param name="windowID">The Unity immediate-mode window identifier.</param>
        private void DrawWindow(int windowID)
        {
            GUILayout.Label("Player name");
            _playerName = GUILayout.TextField(_playerName, 32);
            GUILayout.Space(6f);
            GUILayout.Label("Directory server");
            _matchmakerUrl = GUILayout.TextField(_matchmakerUrl);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh games"))
                Observe(_coordinator.RefreshGamesAsync(_matchmakerUrl));
            if (GUILayout.Button("Disconnect"))
                _coordinator.Disconnect();
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.Label("Host port");
            _port = GUILayout.TextField(_port);
            if (GUILayout.Button("Host game"))
                StartHosting();
            GUILayout.Space(6f);
            GUILayout.Label("Direct address (host:port)");
            _directAddress = GUILayout.TextField(_directAddress);
            if (GUILayout.Button("Join direct"))
                JoinDirect();
            GUILayout.Space(8f);
            GUILayout.Label(_coordinator.Status);
            GUILayout.Label("Available games");
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(190f));
            foreach (MatchmakerGame game in _coordinator.Games)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"{game.name}\n{game.address}:{game.port}");
                if (GUILayout.Button("Join", GUILayout.Width(72f)))
                    Observe(
                        _coordinator.JoinAsync(_playerName, game.address, game.port, _matchmakerUrl)
                    );
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close"))
                _visible = false;
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
        }

        /// <summary>
        /// Validates the port and starts hosting the current main-menu game settings.
        /// </summary>
        private void StartHosting()
        {
            if (!TryParsePort(_port, out int port))
                return;
            Observe(
                _coordinator.HostAsync(_playerName, port, _matchmakerUrl, GameLaunchContext.Summary)
            );
        }

        /// <summary>
        /// Parses the direct address and starts a guest connection.
        /// </summary>
        private void JoinDirect()
        {
            int separator = _directAddress.LastIndexOf(':');
            if (separator <= 0 || separator == _directAddress.Length - 1)
                return;
            string host = _directAddress.Substring(0, separator).Trim();
            if (!TryParsePort(_directAddress.Substring(separator + 1), out int port))
                return;
            Observe(_coordinator.JoinAsync(_playerName, host, port, _matchmakerUrl));
        }

        /// <summary>
        /// Parses a valid TCP port.
        /// </summary>
        /// <param name="text">The port text.</param>
        /// <param name="port">Receives the parsed port.</param>
        /// <returns>True when the value is a usable TCP port.</returns>
        private static bool TryParsePort(string text, out int port)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port)
                && port > 0
                && port <= ushort.MaxValue;
        }

        /// <summary>
        /// Observes a menu task so synchronous validation failures are logged.
        /// </summary>
        /// <param name="task">The task to observe.</param>
        private static async void Observe(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
