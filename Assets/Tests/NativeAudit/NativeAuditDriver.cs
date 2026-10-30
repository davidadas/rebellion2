using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rebellion.Tests.NativeAudit
{
    /// <summary>
    /// Records public state and drives explicitly requested public operations in audit players.
    /// The assembly is excluded from ordinary player builds by its define constraint.
    /// </summary>
    public sealed class NativeAuditDriver : MonoBehaviour
    {
        private const string _controlArgument = "-nativeAuditControl";
        private string _directory;
        private string[] _lastRaycasts = Array.Empty<string>();
        private Vector2 _targetPoint;
        private Task<bool> _operation;
        private string _operationName;
        private Keyboard _keyboard;

        /// <summary>
        /// Starts the driver only when an audit player receives an absolute control directory.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, _controlArgument);
            if (index < 0)
                return;
            if (index + 1 >= arguments.Length || !Path.IsPathFullyQualified(arguments[index + 1]))
                throw new ArgumentException("Native audit control requires an absolute directory.");
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameObject owner = new(nameof(NativeAuditDriver));
            DontDestroyOnLoad(owner);
            NativeAuditDriver driver = owner.AddComponent<NativeAuditDriver>();
            driver._directory = Path.GetFullPath(arguments[index + 1]);
            Directory.CreateDirectory(driver._directory);
        }

        /// <summary>
        /// Polls only the explicit test-command file at five hertz, leaving simulation scheduling alone.
        /// </summary>
        /// <returns>The command-processing coroutine.</returns>
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
        private IEnumerator Start()
        {
            Publish(new Response { id = 0, completed = true });
            string requestPath = Path.Combine(_directory, "request.json");
            while (true)
            {
                if (File.Exists(requestPath))
                {
                    Command command = null;
                    Response response = new();
                    try
                    {
                        command = JsonUtility.FromJson<Command>(File.ReadAllText(requestPath));
                        File.Delete(requestPath);
                        if (command == null)
                            throw new InvalidDataException("The audit command is empty.");
                        response.id = command.id;
                    }
                    catch (Exception exception)
                    {
                        response.error = exception.Message;
                    }
                    if (command != null)
                    {
                        IEnumerator action = Execute(command);
                        while (true)
                        {
                            bool more;
                            try
                            {
                                more = action.MoveNext();
                            }
                            catch (Exception exception)
                            {
                                response.error = exception.Message;
                                break;
                            }
                            if (!more)
                                break;
                            yield return action.Current;
                        }
                        (action as IDisposable)?.Dispose();
                    }
                    response.completed = string.IsNullOrEmpty(response.error);
                    Publish(response);
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        /// <summary>
        /// Runs an explicit public service operation, resolves a pointer target, or captures the viewport.
        /// </summary>
        /// <param name="command">The explicit audit operation.</param>
        /// <returns>The frames needed for normal input processing and presentation.</returns>
        private IEnumerator Execute(Command command)
        {
            switch (command.operation)
            {
                case "state":
                    break;
                case "browse":
                    Track(
                        command.operation,
                        () =>
                            CompleteAsync(MultiplayerCoordinator.EnsureExists().RefreshGamesAsync())
                    );
                    break;
                case "host":
                    Track(command.operation, () => HostAsync(command));
                    break;
                case "join":
                    MultiplayerCoordinator joining = MultiplayerCoordinator.EnsureExists();
                    Track(
                        command.operation,
                        () =>
                            CompleteAsync(
                                joining.JoinAsync(
                                    joining.PlayerSession.DisplayName,
                                    joining.Games.Single(game => game.id == command.target)
                                )
                            )
                    );
                    break;
                case "ready":
                    MultiplayerCoordinator.EnsureExists().SetLobbyReady(command.ready);
                    break;
                case "launch":
                    MultiplayerCoordinator.EnsureExists().LaunchLobby();
                    break;
                case "resume":
                    Track(
                        command.operation,
                        () => CompleteAsync(MultiplayerCoordinator.EnsureExists().ResumeAsync())
                    );
                    break;
                case "save":
                    Track(
                        command.operation,
                        () =>
                            AppBootstrap
                                .Instance.GetRuntime()
                                .SaveGameAsync(command.target, command.text)
                    );
                    break;
                case "quicksave":
                    Track(
                        command.operation,
                        () => AppBootstrap.Instance.GetRuntime().QuickSaveAsync()
                    );
                    break;
                case "load":
                    Track(
                        command.operation,
                        () => AppBootstrap.Instance.GetRuntime().LoadGameAsync(command.target)
                    );
                    break;
                case "fight":
                    Track(command.operation, FightAsync);
                    break;
                case "speed":
                    AppBootstrap
                        .Instance.GetRuntime()
                        .GetActiveGameManager()
                        .SetGameSpeed(
                            (Rebellion.Game.TickSpeed)
                                Enum.Parse(typeof(Rebellion.Game.TickSpeed), command.target)
                        );
                    break;
                case "key":
                    _keyboard ??= InputSystem.AddDevice<Keyboard>("NativeAuditKeyboard");
                    Key[] keys = command
                        .keys.Select(value => (Key)Enum.Parse(typeof(Key), value))
                        .ToArray();
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
                    yield return null;
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    yield return null;
                    break;
                case "point":
                    Vector2 point = GetTargetPoint(command.target);
                    if (
                        point.x < 0
                        || point.y < 0
                        || point.x >= Screen.width
                        || point.y >= Screen.height
                    )
                        throw new ArgumentOutOfRangeException(
                            nameof(command),
                            "The click lies outside the game viewport."
                        );
                    _lastRaycasts = GetRaycasts(point);
                    _targetPoint = point;
                    break;
                case "wait":
                    if (command.milliseconds < 0)
                        throw new ArgumentOutOfRangeException(nameof(command));
                    yield return new WaitForSecondsRealtime(command.milliseconds / 1000f);
                    break;
                case "capture":
                    yield return new WaitForEndOfFrame();
                    Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        File.WriteAllBytes(
                            Path.Combine(_directory, "frame-" + command.id + ".png"),
                            capture.EncodeToPNG()
                        );
                    }
                    finally
                    {
                        Destroy(capture);
                    }
                    break;
                default:
                    throw new ArgumentException(
                        "Unknown native audit operation: " + command.operation
                    );
            }
            yield return null;
        }

        /// <summary>
        /// Tracks completion without blocking observation while a public operation is pending.
        /// </summary>
        /// <param name="name">The explicitly requested operation.</param>
        /// <param name="operation">Starts the public service operation after checking ownership.</param>
        private void Track(string name, Func<Task<bool>> operation)
        {
            if (_operation?.IsCompleted == false)
                throw new InvalidOperationException("An audit operation is still pending.");
            _operationName = name;
            _operation = operation();
        }

        /// <summary>
        /// Submits an explicitly requested Fight response through the participant command boundary.
        /// </summary>
        /// <returns>Whether this participant's current combat response was accepted.</returns>
        private static async Task<bool> FightAsync()
        {
            GameSession session = GetSession();
            if (
                !session
                    .GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            )
                return false;
            PlayerRequestOutcome result = await MultiplayerCoordinator.Instance.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = false,
                }
            );
            return result.Accepted;
        }

        /// <summary>
        /// Adapts completion of a public operation that reports its result through coordinator state.
        /// </summary>
        /// <param name="operation">The public service operation.</param>
        /// <returns>True when the call completes without an exception.</returns>
        private static async Task<bool> CompleteAsync(Task operation)
        {
            await operation;
            return true;
        }

        /// <summary>
        /// Hosts using launch settings read from the explicitly named ordinary save fixture.
        /// </summary>
        /// <param name="command">The fixture identifier and requested room name.</param>
        /// <returns>Whether the host has an admitted waiting lobby.</returns>
        private static async Task<bool> HostAsync(Command command)
        {
            Rebellion.Game.GameRoot source = await SaveGameManager.Instance.LoadGameDataAsync(
                command.target
            );
            MultiplayerCoordinator coordinator = MultiplayerCoordinator.EnsureExists();
            await coordinator.HostAsync(
                coordinator.PlayerSession.DisplayName,
                command.text,
                source.Summary
            );
            return coordinator.IsHost && coordinator.Lobby != null && coordinator.Failure == null;
        }

        /// <summary>
        /// Resolves the public runtime session for an explicitly requested diagnostic capture.
        /// </summary>
        /// <returns>The active simulation session.</returns>
        private static GameSession GetSession() =>
            (
                AppBootstrap.Instance == null
                    ? null
                    : AppBootstrap.Instance.GetRuntime()?.GetActiveGameSession()
            ) ?? throw new InvalidOperationException("There is no active session to measure.");

        /// <summary>
        /// Finds a unique active UI target by exact hierarchy path or object name.
        /// </summary>
        /// <param name="target">The exact path or unique object name.</param>
        /// <returns>The target's viewport center.</returns>
        private static Vector2 GetTargetPoint(string target)
        {
            Component[] matches = GetTargets()
                .Where(component =>
                    GetPath(component.transform) == target || component.name == target
                )
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException(
                    "The audit target must identify one active control; matches="
                        + matches.Length
                        + "."
                );
            Component component = matches[0];
            Canvas canvas = component.GetComponentInParent<Canvas>();
            Camera camera =
                canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas?.worldCamera ?? Camera.main;
            if (component.transform is RectTransform rectangle)
                return RectTransformUtility.WorldToScreenPoint(
                    camera,
                    rectangle.TransformPoint(rectangle.rect.center)
                );
            if (camera == null)
                throw new InvalidOperationException("The audit target has no projection camera.");
            return camera.WorldToScreenPoint(component.transform.position);
        }

        /// <summary>
        /// Resolves active UI controls only when an explicit audit command requires them.
        /// </summary>
        /// <returns>The distinct selectable and authored pointer controls.</returns>
        private static Component[] GetTargets() =>
            FindObjectsByType<Selectable>(FindObjectsSortMode.None)
                .Cast<Component>()
                .Concat(FindObjectsByType<UIRaycastArea>(FindObjectsSortMode.None))
                .GroupBy(component => component.gameObject.GetInstanceID())
                .Select(group => group.First())
                .ToArray();

        /// <summary>
        /// Captures the real event-system hit order without pressing a control directly.
        /// </summary>
        /// <param name="point">The requested viewport position.</param>
        /// <returns>The raycast target paths in event-system order.</returns>
        private static string[] GetRaycasts(Vector2 point)
        {
            if (EventSystem.current == null)
                return Array.Empty<string>();
            List<RaycastResult> results = new();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current) { position = point },
                results
            );
            return results.Select(result => GetPath(result.gameObject.transform)).ToArray();
        }

        /// <summary>
        /// Writes a complete response containing public input and UI state, without field text or credentials.
        /// </summary>
        /// <param name="response">The result for the consumed request.</param>
        private void Publish(Response response)
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected =
                eventSystem == null ? null : eventSystem.currentSelectedGameObject;
            TMP_InputField entry =
                selected == null ? null : selected.GetComponentInParent<TMP_InputField>();
            MultiplayerCoordinator multiplayer = MultiplayerCoordinator.Instance;
            GameRuntime runtime =
                AppBootstrap.Instance == null ? null : AppBootstrap.Instance.GetRuntime();
            Rebellion.Gameplay.GameSession session = runtime?.GetActiveGameSession();
            response.frame = Time.frameCount;
            response.scene = SceneManager.GetActiveScene().name;
            response.focused = Application.isFocused;
            response.width = Screen.width;
            response.height = Screen.height;
            response.status = multiplayer?.Status;
            response.multiplayer = multiplayer?.IsMultiplayerGame == true;
            response.canRequestSharedSave = multiplayer?.CanRequestSave == true;
            response.canSendPeerChat = multiplayer?.CanSendPeerChat == true;
            response.operation = _operationName;
            response.operationPending = _operation?.IsCompleted == false;
            response.operationSucceeded =
                _operation?.Status == TaskStatus.RanToCompletion && _operation.Result;
            response.operationError = _operation?.Exception?.GetBaseException().Message;
            response.hostReady = multiplayer?.Lobby?.hostReady == true;
            response.guestReady = multiplayer?.Lobby?.guestReady == true;
            response.games =
                multiplayer == null
                    ? Array.Empty<DiscoveredGame>()
                    : multiplayer
                        .Games.Select(game => new DiscoveredGame
                        {
                            id = game.id,
                            name = game.name,
                            state = game.state,
                        })
                        .ToArray();
            response.role = multiplayer == null ? null : multiplayer.Role.ToString();
            response.runtimeCanSave = runtime?.CanSave == true;
            response.runtimeCanRequestSave = runtime?.CanRequestSave == true;
            response.tickSettled = session?.Tick.IsSettled == true;
            response.tickBusy = session?.Tick.IsBusy == true;
            response.commandReady = session?.CanExecuteCommands == true;
            response.pendingBattle =
                session?.GetService<SpaceCombatCommands>().HasPendingDecision == true;
            response.persistenceNotice =
                AppBootstrap.Instance?.PersistenceNotice.Operation?.ToString();
            response.tick = session?.Game.CurrentTick ?? -1;
            response.speed = session == null ? null : session.Game.GetGameSpeed().ToString();
            response.speedSelections =
                session == null
                    ? Array.Empty<string>()
                    : session
                        .Game.GetPlayers()
                        .Select(player => player.RequestedGameSpeed.ToString())
                        .ToArray();
            response.selected = selected == null ? null : GetPath(selected.transform);
            response.textEntryFocused = entry?.isFocused == true;
            response.enabledActions = InputSystem
                .ListEnabledActions()
                .Select(action => action.actionMap.name + "/" + action.name)
                .ToArray();
            response.globalBindings = InputSystem
                .ListEnabledActions()
                .Where(action => action.actionMap.name == "Global")
                .Select(action => action.name + ": " + action.GetBindingDisplayString())
                .ToArray();
            response.pointerX = UnityEngine.Input.mousePosition.x;
            response.pointerY = UnityEngine.Input.mousePosition.y;
            response.inputOwners = FindObjectsByType<AppInputController>(FindObjectsSortMode.None)
                .Select(owner => new InputOwner
                {
                    path = GetPath(owner.transform),
                    shortcutsSuppressed = owner.ShortcutsSuppressed,
                })
                .ToArray();
            response.eventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
                .Select(system => GetPath(system.transform) + "@" + system.gameObject.scene.name)
                .ToArray();
            response.controls = GetTargets()
                .Select(component => new Control
                {
                    path = GetPath(component.transform),
                    type = component.GetType().Name,
                    interactable =
                        component is not Selectable selectable || selectable.IsInteractable(),
                })
                .ToArray();
            response.raycasts = _lastRaycasts;
            response.targetX = _targetPoint.x;
            response.targetY = _targetPoint.y;
            string temporary = Path.Combine(_directory, "response.tmp");
            string destination = Path.Combine(_directory, "response-" + response.id + ".json");
            File.WriteAllText(temporary, JsonUtility.ToJson(response, true));
            File.Move(temporary, destination);
        }

        /// <summary>
        /// Names a UI object without inspecting serialized simulation state.
        /// </summary>
        /// <param name="transform">The target UI transform.</param>
        /// <returns>Its absolute hierarchy path.</returns>
        private static string GetPath(Transform transform)
        {
            List<string> parts = new();
            for (Transform current = transform; current != null; current = current.parent)
                parts.Add(current.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>
        /// Releases only the virtual keyboard owned by this audit instance.
        /// </summary>
        private void OnDestroy()
        {
            if (_keyboard?.added == true)
                InputSystem.RemoveDevice(_keyboard);
        }

        /// <summary>
        /// Describes one explicit UI observation or pointer-target query.
        /// </summary>
        [Serializable]
        private sealed class Command
        {
            public long id;
            public string operation;
            public string target;
            public int milliseconds;
            public string text;
            public bool ready;
            public string[] keys;
        }

        /// <summary>
        /// Identifies a discoverable room without exporting account identifiers.
        /// </summary>
        [Serializable]
        private sealed class DiscoveredGame
        {
            public string id;
            public string name;
            public string state;
        }

        /// <summary>
        /// Reports observable state after a query; completion does not establish input delivery or gameplay success.
        /// </summary>
        [Serializable]
        private sealed class Response
        {
            public long id;
            public bool completed;
            public string error;
            public int frame;
            public string scene;
            public bool focused;
            public int width;
            public int height;
            public float targetX;
            public float targetY;
            public float pointerX;
            public float pointerY;
            public string status;
            public bool multiplayer;
            public bool canRequestSharedSave;
            public bool canSendPeerChat;
            public string role;
            public string operation;
            public bool operationPending;
            public bool operationSucceeded;
            public string operationError;
            public bool hostReady;
            public bool guestReady;
            public DiscoveredGame[] games;
            public bool runtimeCanSave;
            public bool runtimeCanRequestSave;
            public bool tickSettled;
            public bool tickBusy;
            public bool commandReady;
            public bool pendingBattle;
            public string persistenceNotice;
            public int tick;
            public string speed;
            public string[] speedSelections;
            public string selected;
            public bool textEntryFocused;
            public string[] enabledActions;
            public string[] globalBindings;
            public InputOwner[] inputOwners;
            public string[] eventSystems;
            public Control[] controls;
            public string[] raycasts;
        }

        /// <summary>
        /// Reports the public shortcut-suppression state of an input controller.
        /// </summary>
        [Serializable]
        private sealed class InputOwner
        {
            public string path;
            public bool shortcutsSuppressed;
        }

        /// <summary>
        /// Identifies an active UI control without recording input-field values.
        /// </summary>
        [Serializable]
        private sealed class Control
        {
            public string path;
            public string type;
            public bool interactable;
        }
    }
}
