using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Presents native discovery, room creation, save selection, and explicit two-player launch approval.
    /// </summary>
    public sealed class MultiplayerMenuOverlay : MonoBehaviour, ICancelable
    {
        [SerializeField]
        private GameObject browserPage;

        [SerializeField]
        private GameObject lobbyPage;

        [SerializeField]
        private GameObject createDialog;

        [SerializeField]
        private GameObject saveDialog;

        [SerializeField]
        private GameObject passwordDialog;

        [SerializeField]
        private GameObject authDialog;

        [SerializeField]
        private GameObject gameRowTemplate;

        [SerializeField]
        private TMP_Text pageTitle;

        [SerializeField]
        private TMP_Text identityText;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private TMP_Text browserCountText;

        [SerializeField]
        private TMP_Text emptyBrowserText;

        [SerializeField]
        private TMP_Text campaignText;

        [SerializeField]
        private TMP_Text hostNameText;

        [SerializeField]
        private TMP_Text guestNameText;

        [SerializeField]
        private TMP_Text hostReadyText;

        [SerializeField]
        private TMP_Text guestReadyText;

        [SerializeField]
        private TMP_Text createStatusText;

        [SerializeField]
        private TMP_Text passwordStatusText;

        [SerializeField]
        private TMP_Text saveStatusText;

        [SerializeField]
        private TMP_Text authStatusText;

        [SerializeField]
        private Button saveLoadButton;

        [SerializeField]
        private Button[] sortButtons;

        [SerializeField]
        private TMP_Text lobbyChatText;

        [SerializeField]
        private TMP_Text generalChatText;

        [SerializeField]
        private TMP_InputField searchInput;

        [SerializeField]
        private TMP_InputField gameNameInput;

        [SerializeField]
        private TMP_InputField hostPasswordInput;

        [SerializeField]
        private TMP_InputField joinPasswordInput;

        [SerializeField]
        private TMP_InputField lobbyChatInput;

        [SerializeField]
        private TMP_InputField generalChatInput;

        [SerializeField]
        private TMP_Dropdown packFilter;

        [SerializeField]
        private TMP_Dropdown contentPackDropdown;

        [SerializeField]
        private TMP_Dropdown hostFactionDropdown;

        [SerializeField]
        private TMP_Dropdown guestFactionDropdown;

        [SerializeField]
        private TMP_Dropdown galaxySizeDropdown;

        [SerializeField]
        private TMP_Dropdown victoryConditionDropdown;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Button refreshButton;

        [SerializeField]
        private Button hostButton;

        [SerializeField]
        private Button joinButton;

        [SerializeField]
        private Button resumeButton;

        [SerializeField]
        private Button loadButton;

        [SerializeField]
        private Button leaveButton;

        [SerializeField]
        private Button readyButton;

        [SerializeField]
        private Button launchButton;

        [SerializeField]
        private Button cancelCreateButton;

        [SerializeField]
        private Button createButton;

        [SerializeField]
        private Button cancelSaveButton;

        [SerializeField]
        private Button cancelPasswordButton;

        [SerializeField]
        private Button confirmJoinButton;

        [SerializeField]
        private Button steamSignInButton;

        [SerializeField]
        private Button gogSignInButton;

        [SerializeField]
        private Button lobbySendButton;

        [SerializeField]
        private Button generalSendButton;

        [SerializeField]
        private Button authBackButton;

        [SerializeField]
        private ScrollAreaView gameScrollArea;

        [SerializeField]
        private RectTransform gameRowsRoot;

        [SerializeField]
        private OptionsSaveListView saveList;

        [SerializeField]
        private ConfirmationDialogView leaveConfirmation;

        [SerializeField]
        private GameObject leaveConfirmationRoot;

        private readonly List<GameObject> _gameRows = new();
        private readonly List<OptionsSaveSlot> _saveSlots = new();
        private IReadOnlyList<SaveGameEntry> _saves;
        private bool _closeAfterLeaving;
        private MultiplayerCoordinator _coordinator;
        private IReadOnlyList<ContentPackDefinition> _packs;
        private GameSummary _initialSummary;
        private MatchmakerGame _selectedGame;
        private SaveGameEntry _selectedSave;
        private string[] _factionIDs = Array.Empty<string>();
        private string _gamesSignature;
        private int _chatCount = -1;
        private int _generalChatCount = -1;
        private readonly MultiplayerMenuOperation _operation = new();
        private MultiplayerGameColumn _sortColumn;
        private bool _sortDescending;
        private bool _busy => _operation.IsBusy;
        private bool _bound;

        /// <summary>
        /// Occurs when the player closes the multiplayer console.
        /// </summary>
        public event Action CloseRequested;

        /// <summary>
        /// Connects authored controls to the persistent session and opens discovery with the cached account.
        /// </summary>
        /// <param name="multiplayerCoordinator">The persistent session owner.</param>
        /// <param name="launchSummary">The current main-menu settings.</param>
        public void Initialize(
            MultiplayerCoordinator multiplayerCoordinator,
            GameSummary launchSummary
        )
        {
            _coordinator =
                multiplayerCoordinator
                ?? throw new ArgumentNullException(nameof(multiplayerCoordinator));
            _initialSummary =
                launchSummary ?? throw new ArgumentNullException(nameof(launchSummary));
            _coordinator.OpenBrowser();
            ContentPack current = AppBootstrap.EnsureExists().GetContentPack();
            _packs = ContentPackLoader.DiscoverPacks(current.ContentRootPath);
            contentPackDropdown.ClearOptions();
            contentPackDropdown.AddOptions(_packs.Select(pack => pack.DisplayName).ToList());
            contentPackDropdown.SetValueWithoutNotify(
                Math.Max(0, _packs.ToList().FindIndex(pack => pack.ID == current.Definition.ID))
            );
            packFilter.ClearOptions();
            packFilter.AddOptions(
                new[] { "All content packs" }
                    .Concat(_packs.Select(pack => pack.DisplayName))
                    .ToList()
            );
            gameNameInput.characterLimit = 64;
            lobbyChatInput.characterLimit = 512;
            generalChatInput.characterLimit = 256;
            _operation.Changed += Render;
            BindControls();
            _coordinator.StateChanged += Render;
            saveList.SetLoadOnly(true);
            leaveConfirmation.InitializeContent(AppBootstrap.EnsureExists().GetContentAssets());
            Render();
            if (_coordinator.PlayerSession != null)
                Run(RefreshBrowserAsync);
        }

        /// <summary>
        /// Releases presentation subscriptions without disconnecting a launched session.
        /// </summary>
        private void OnDestroy()
        {
            _operation.Changed -= Render;
            _operation.Dispose();
            if (_coordinator != null)
            {
                _coordinator.StateChanged -= Render;
                _coordinator.CloseBrowser();
            }
        }

        /// <summary>
        /// Closes the topmost modal before leaving the room or console.
        /// </summary>
        /// <returns>True because the console consumes cancellation.</returns>
        public bool TryCancel()
        {
            if (leaveConfirmationRoot.activeSelf)
            {
                CancelLeave();
                return true;
            }
            if (_busy || _coordinator.IsConnecting)
            {
                bool dialog =
                    createDialog.activeSelf || passwordDialog.activeSelf || saveDialog.activeSelf;
                bool joining = _coordinator.Role == MultiplayerRole.Guest;
                CancelOperation();
                createDialog.SetActive(false);
                passwordDialog.SetActive(false);
                saveDialog.SetActive(false);
                if (!dialog && !joining)
                    CloseRequested?.Invoke();
                Render();
                return true;
            }
            if (createDialog.activeSelf)
                HideCreate();
            else if (saveDialog.activeSelf)
                HideSaves();
            else if (passwordDialog.activeSelf)
                HidePassword();
            else if (_coordinator.Lobby != null)
                LeaveLobby();
            else
                CloseRequested?.Invoke();
            return true;
        }

        /// <summary>
        /// Wires native button, dropdown, text-entry, and keyboard actions once.
        /// </summary>
        private void BindControls()
        {
            if (_bound)
                return;
            _bound = true;
            closeButton.onClick.AddListener(RequestClose);
            authBackButton.onClick.AddListener(RequestClose);
            hostButton.onClick.AddListener(ShowCreate);
            cancelCreateButton.onClick.AddListener(HideCreate);
            createButton.onClick.AddListener(CreateGame);
            refreshButton.onClick.AddListener(RefreshBrowser);
            joinButton.onClick.AddListener(JoinSelected);
            resumeButton.onClick.AddListener(ResumeGame);
            cancelPasswordButton.onClick.AddListener(HidePassword);
            confirmJoinButton.onClick.AddListener(JoinProtected);
            leaveButton.onClick.AddListener(LeaveLobby);
            readyButton.onClick.AddListener(ToggleReady);
            launchButton.onClick.AddListener(_coordinator.LaunchLobby);
            loadButton.onClick.AddListener(ShowSaves);
            cancelSaveButton.onClick.AddListener(HideSaves);
            saveList.LoadRequested += LoadSelectedSave;
            saveLoadButton.onClick.AddListener(LoadSelectedSave);
            for (int index = 0; index < sortButtons.Length; index++)
            {
                MultiplayerGameColumn column = (MultiplayerGameColumn)index;
                sortButtons[index].onClick.AddListener(() => SortGames(column));
            }
            saveList.SlotSelected += SelectSaveIndex;
            leaveConfirmation.Confirmed += ConfirmLeave;
            leaveConfirmation.Canceled += CancelLeave;
            steamSignInButton.onClick.AddListener(SignInSteam);
            gogSignInButton.onClick.AddListener(SignInGog);
            lobbySendButton.onClick.AddListener(SendLobbyChat);
            generalSendButton.onClick.AddListener(SendGeneralChat);
            lobbyChatInput.onSubmit.AddListener(SubmitLobbyChat);
            generalChatInput.onSubmit.AddListener(SubmitGeneralChat);
            searchInput.onValueChanged.AddListener(FilterGames);
            packFilter.onValueChanged.AddListener(FilterPacks);
            hostFactionDropdown.onValueChanged.AddListener(SelectHostFaction);
            guestFactionDropdown.onValueChanged.AddListener(SelectGuestFaction);
            galaxySizeDropdown.ClearOptions();
            galaxySizeDropdown.AddOptions(new List<string> { "Small", "Medium", "Large" });
            victoryConditionDropdown.ClearOptions();
            victoryConditionDropdown.AddOptions(new List<string> { "Conquest", "Headquarters" });
            galaxySizeDropdown.onValueChanged.AddListener(SelectGalaxySize);
            victoryConditionDropdown.onValueChanged.AddListener(SelectVictoryCondition);
        }

        /// <summary>
        /// Renders state-driven navigation, membership, account status, and control availability.
        /// </summary>
        private void Render()
        {
            if (_coordinator == null)
                return;
            bool signedIn = _coordinator.PlayerSession != null;
            bool inLobby = _coordinator.Lobby != null && !_coordinator.IsConnecting;
            bool idle =
                !_busy
                && !_coordinator.IsConnecting
                && !_coordinator.IsSigningIn
                && !_coordinator.IsLoadingSave;
            identityText.text = signedIn
                ? _coordinator.PlayerSession.DisplayName + "  |  Online"
                : "Sign in to play online";
            string error = FormatError(_operation.Error ?? _coordinator.Failure);
            statusText.text =
                _operation.Page == MultiplayerMenuPage.Browser
                    ? error
                        ?? (
                            (_busy || _coordinator.IsConnecting) && !_coordinator.IsSearchingGames
                                ? _coordinator.Status
                                : string.Empty
                        )
                    : string.Empty;
            createStatusText.text =
                _operation.Page == MultiplayerMenuPage.Create
                    ? error ?? (_busy ? "Creating game..." : string.Empty)
                    : string.Empty;
            passwordStatusText.text =
                _operation.Page == MultiplayerMenuPage.Password
                    ? error ?? (_busy || _coordinator.IsConnecting ? "Connecting..." : string.Empty)
                    : string.Empty;
            saveStatusText.text =
                _operation.Page == MultiplayerMenuPage.Save
                    ? error ?? (_busy ? "Checking saved game..." : string.Empty)
                    : string.Empty;
            authStatusText.text =
                _operation.Page == MultiplayerMenuPage.Authentication
                    ? error ?? (_busy ? _coordinator.Status : string.Empty)
                    : string.Empty;
            statusText.gameObject.SetActive(
                !inLobby && !createDialog.activeSelf && !passwordDialog.activeSelf
            );
            closeButton.gameObject.SetActive(!inLobby);
            saveLoadButton.interactable = idle && _selectedSave != null;
            authDialog.SetActive(!signedIn);
            steamSignInButton.interactable = gogSignInButton.interactable = idle;
            authBackButton.interactable = true;
            browserPage.SetActive(!inLobby);
            lobbyPage.SetActive(inLobby);
            pageTitle.text = inLobby ? _coordinator.Lobby.gameName : "SERVER BROWSER";
            hostButton.interactable = refreshButton.interactable = signedIn && idle;
            resumeButton.gameObject.SetActive(_coordinator.HasRecovery);
            resumeButton.interactable = signedIn && idle;
            createButton.interactable = signedIn && idle && _packs.Count > 0;
            contentPackDropdown.interactable =
                gameNameInput.interactable =
                hostPasswordInput.interactable =
                    idle;
            cancelCreateButton.interactable = cancelPasswordButton.interactable = true;
            confirmJoinButton.interactable = joinPasswordInput.interactable = idle;
            closeButton.interactable = true;
            if (inLobby)
            {
                createDialog.SetActive(false);
                passwordDialog.SetActive(false);
                RenderLobby(idle);
            }
            else
            {
                string signature =
                    _coordinator.IsSearchingGames
                    + ":"
                    + string.Join(
                        ";",
                        _coordinator.Games.Select(game =>
                            game.id
                            + game.name
                            + game.currentPlayers
                            + game.state
                            + game.contentFingerprint
                            + game.packName
                            + game.passwordProtected
                        )
                    );
                if (_gamesSignature != signature)
                {
                    _gamesSignature = signature;
                    RenderGames();
                }
                joinButton.interactable =
                    signedIn && idle && EpicLobbyListing.IsJoinable(_selectedGame);
                RenderGeneralChat();
            }
        }

        /// <summary>
        /// Updates each player's owned faction control and readiness without rebuilding the hierarchy.
        /// </summary>
        /// <param name="idle">Whether commands may currently be issued.</param>
        private void RenderLobby(bool idle)
        {
            MultiplayerLobbyState state = _coordinator.Lobby;
            ContentPack pack = _coordinator.LobbyContent;
            bool host = _coordinator.IsHost;
            if (pack != null)
            {
                string[] factions = pack.Scenario.PlayableFactionIDs.ToArray();
                if (!_factionIDs.SequenceEqual(factions))
                {
                    _factionIDs = factions;
                    List<string> labels = factions
                        .Select(id => pack.Factions.First(faction => faction.ID == id).DisplayName)
                        .ToList();
                    hostFactionDropdown.ClearOptions();
                    guestFactionDropdown.ClearOptions();
                    hostFactionDropdown.AddOptions(labels);
                    guestFactionDropdown.AddOptions(labels);
                }
                campaignText.text =
                    pack.Definition.DisplayName
                    + "\n"
                    + pack.Scenario.DisplayName
                    + "\n"
                    + (
                        string.IsNullOrWhiteSpace(state.saveName)
                            ? "New game"
                            : "Save: " + state.saveName
                    );
            }
            else
                campaignText.text = "Verifying required content...";
            if (state.FactionSelectionError != null)
                campaignText.text += "\n" + state.FactionSelectionError;
            hostNameText.text = state.hostName + "  (Host)";
            guestNameText.text = state.guestName ?? "Waiting for a player...";
            hostReadyText.text = state.hostReady ? "Ready" : "Not ready";
            guestReadyText.text =
                state.guestName == null ? "Open slot"
                : !state.guestContentReady ? "Verifying content..."
                : state.guestReady ? "Ready"
                : "Not ready";
            hostFactionDropdown.SetValueWithoutNotify(
                Math.Max(0, Array.IndexOf(_factionIDs, state.hostFactionID))
            );
            guestFactionDropdown.SetValueWithoutNotify(
                Math.Max(0, Array.IndexOf(_factionIDs, state.guestFactionID))
            );
            bool editable = idle && pack != null && !state.launching && !_coordinator.IsLobbyClosed;
            hostFactionDropdown.interactable = editable && host;
            guestFactionDropdown.interactable = editable && !host;
            galaxySizeDropdown.SetValueWithoutNotify((int)state.galaxySize);
            victoryConditionDropdown.SetValueWithoutNotify(
                state.victoryCondition == GameVictoryCondition.Conquest ? 0 : 1
            );
            galaxySizeDropdown.interactable = victoryConditionDropdown.interactable =
                editable && host && !state.hasSavedGame;
            bool localReady = host ? state.hostReady : state.guestReady;
            readyButton.interactable =
                idle
                && state.HasValidFactionSelection
                && (host || state.guestContentReady)
                && !state.launching
                && !_coordinator.IsLobbyClosed;
            SetButtonLabel(readyButton, localReady ? "NOT READY" : "READY");
            launchButton.gameObject.SetActive(host);
            launchButton.interactable = idle && state.CanLaunch;
            loadButton.gameObject.SetActive(host);
            lobbySendButton.interactable = lobbyChatInput.interactable =
                !_coordinator.IsLobbyClosed;
            loadButton.interactable = idle && !state.launching;
            leaveButton.interactable = idle && !state.launching;
            if (_chatCount != _coordinator.Chat.Count)
            {
                _chatCount = _coordinator.Chat.Count;
                RenderTranscript(lobbyChatText, _coordinator.Chat);
            }
        }

        /// <summary>
        /// Rebuilds discoverable rows only when a listing or user filter changes.
        /// </summary>
        private void RenderGames()
        {
            foreach (GameObject row in _gameRows)
                Destroy(row);
            _gameRows.Clear();
            string selectedID = _selectedGame?.id;
            _selectedGame = null;
            string filter = searchInput.text.Trim();
            foreach (
                MatchmakerGame game in MultiplayerGameBrowser.Sort(
                    _coordinator.Games,
                    _sortColumn,
                    _sortDescending
                )
            )
            {
                if (
                    (
                        !string.IsNullOrEmpty(filter)
                        && game.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    ) || (packFilter.value > 0 && game.packID != _packs[packFilter.value - 1].ID)
                )
                    continue;
                GameObject row = Instantiate(gameRowTemplate, gameRowsRoot, false);
                row.name = "Game " + game.id;
                row.SetActive(true);
                RectTransform rect = (RectTransform)row.transform;
                rect.anchoredPosition = new Vector2(0, -_gameRows.Count * 38);
                row.GetComponent<MenuServerRowView>()
                    .Render(
                        game.name,
                        game.packName ?? game.packID,
                        game.currentPlayers + "/2",
                        game.passwordProtected,
                        game.id == selectedID
                    );
                row.GetComponent<Button>().onClick.AddListener(() => SelectGame(game));
                _gameRows.Add(row);
                if (game.id == selectedID)
                    _selectedGame = game;
            }
            gameScrollArea.SetContentHeight(_gameRows.Count * 38, 38, true);
            emptyBrowserText.gameObject.SetActive(_gameRows.Count == 0);
            emptyBrowserText.text = _coordinator.IsSearchingGames
                ? "Searching for games..."
                : "No games found.";
            browserCountText.text =
                _coordinator.IsSearchingGames && _gameRows.Count > 0 ? "Searching..."
                : _gameRows.Count == 0 ? string.Empty
                : _gameRows.Count + (_gameRows.Count == 1 ? " game" : " games");
            string[] headers = { "GAME", "CONTENT PACK", "PLAYERS", "ACCESS" };
            for (int index = 0; index < sortButtons.Length; index++)
                SetButtonLabel(
                    sortButtons[index],
                    headers[index]
                        + (
                            (int)_sortColumn == index
                                ? (_sortDescending ? " \u25bc" : " \u25b2")
                                : string.Empty
                        )
                );
            HighlightSelection();
        }

        /// <summary>
        /// Highlights the selected server while preserving authored button feedback.
        /// </summary>
        private void HighlightSelection()
        {
            foreach (GameObject row in _gameRows)
            {
                MatchmakerGame game = _coordinator.Games.First(item =>
                    row.name == "Game " + item.id
                );
                row.GetComponent<MenuServerRowView>()
                    .Render(
                        game.name,
                        game.packName ?? game.packID,
                        game.currentPlayers + "/2",
                        game.passwordProtected,
                        game.id == _selectedGame?.id
                    );
            }
            joinButton.interactable = !_busy && EpicLobbyListing.IsJoinable(_selectedGame);
        }

        /// <summary>
        /// Selects one currently displayed discovery result.
        /// </summary>
        /// <param name="game">The selected result.</param>
        private void SelectGame(MatchmakerGame game)
        {
            _selectedGame = game;
            HighlightSelection();
        }

        /// <summary>
        /// Shows creation settings with an account-based default room name.
        /// </summary>
        private void ShowCreate()
        {
            if (string.IsNullOrWhiteSpace(gameNameInput.text))
                gameNameInput.text = _coordinator.PlayerSession.DisplayName + "'s Game";
            hostPasswordInput.text = string.Empty;
            createDialog.SetActive(true);
            gameNameInput.ActivateInputField();
        }

        /// <summary>
        /// Hides creation settings without changing the active room.
        /// </summary>
        private void HideCreate()
        {
            CancelOperation();
            createDialog.SetActive(false);
        }

        /// <summary>
        /// Starts the asynchronous host operation from the authored creation command.
        /// </summary>
        private void CreateGame()
        {
            Run(CreateGameAsync, MultiplayerMenuPage.Create);
        }

        /// <summary>
        /// Opens the selected installed pack and creates a waiting room using its authored defaults.
        /// </summary>
        /// <param name="token">Cancels this form command.</param>
        /// <returns>The pending exact-content selection and lobby creation.</returns>
        private async Task CreateGameAsync(CancellationToken token)
        {
            ContentPackDefinition definition = _packs[contentPackDropdown.value];
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            ContentPack active = bootstrap.GetContentPack();
            string[] disabled = bootstrap.GetUserSettingsManager().Settings.Content.DisabledModIDs;
            ContentPack selected = await Task.Run(() =>
                ContentPackLoader.OpenExact(
                    active.ContentRootPath,
                    definition.ID,
                    definition.DefaultScenarioID,
                    disabled
                )
            );
            token.ThrowIfCancellationRequested();
            if (this == null)
                return;
            GameSummary summary = selected.MatchesContentIdentity(_initialSummary)
                ? _initialSummary
                : GameLaunchContext.CreateDefaultSummary(selected);
            await _coordinator.HostAsync(
                _coordinator.PlayerSession.DisplayName,
                gameNameInput.text,
                summary,
                hostPasswordInput.text
            );
        }

        /// <summary>
        /// Refreshes discovery from its native command.
        /// </summary>
        private void RefreshBrowser()
        {
            Run(RefreshBrowserAsync);
        }

        /// <summary>
        /// Refreshes public games while allowing conversation to connect independently.
        /// </summary>
        /// <param name="token">Cancels this form command.</param>
        /// <returns>The pending browser initialization.</returns>
        private async Task RefreshBrowserAsync(CancellationToken token)
        {
            await _coordinator.RefreshGamesAsync();
            token.ThrowIfCancellationRequested();
            _ = _coordinator.OpenGeneralChatAsync();
        }

        /// <summary>
        /// Opens password entry when required or joins a public result directly.
        /// </summary>
        private void JoinSelected()
        {
            if (!EpicLobbyListing.IsJoinable(_selectedGame))
                return;
            if (_selectedGame.state == "rejoining" && _coordinator.HasRecovery)
            {
                MatchmakerGame selected = _selectedGame;
                Run(_ => _coordinator.ResumeAsync(selected));
                return;
            }
            if (_selectedGame.passwordProtected)
            {
                joinPasswordInput.text = string.Empty;
                passwordDialog.SetActive(true);
                joinPasswordInput.ActivateInputField();
            }
            else
                Run(_ =>
                    _coordinator.JoinAsync(_coordinator.PlayerSession.DisplayName, _selectedGame)
                );
        }

        /// <summary>
        /// Reopens this account's interrupted campaign using the existing native loading flow.
        /// </summary>
        private void ResumeGame()
        {
            Run(_ => _coordinator.ResumeAsync());
        }

        /// <summary>
        /// Joins the selected protected result using the entered admission password.
        /// </summary>
        private void JoinProtected()
        {
            Run(
                _ =>
                    _coordinator.JoinAsync(
                        _coordinator.PlayerSession.DisplayName,
                        _selectedGame,
                        joinPasswordInput.text
                    ),
                MultiplayerMenuPage.Password
            );
        }

        /// <summary>
        /// Dismisses protected-game admission.
        /// </summary>
        private void HidePassword()
        {
            CancelOperation();
            passwordDialog.SetActive(false);
        }

        /// <summary>
        /// Leaves a pre-game room and returns to the server browser.
        /// </summary>
        private void LeaveLobby()
        {
            _closeAfterLeaving = false;
            leaveConfirmationRoot.SetActive(true);
            leaveConfirmation.Show("Are you sure you want to leave this lobby?");
        }

        /// <summary>
        /// Toggles the local participant's explicit approval.
        /// </summary>
        private void ToggleReady()
        {
            MultiplayerLobbyState state = _coordinator.Lobby;
            _coordinator.SetLobbyReady(!(_coordinator.IsHost ? state.hostReady : state.guestReady));
        }

        /// <summary>
        /// Applies a host-owned faction selection from its dropdown.
        /// </summary>
        /// <param name="index">The authored faction index.</param>
        private void SelectHostFaction(int index)
        {
            if (_coordinator.IsHost)
                _coordinator.SelectLobbyFaction(_factionIDs[index]);
        }

        /// <summary>
        /// Applies a guest-owned faction selection from its dropdown.
        /// </summary>
        /// <param name="index">The authored faction index.</param>
        private void SelectGuestFaction(int index)
        {
            if (!_coordinator.IsHost)
                _coordinator.SelectLobbyFaction(_factionIDs[index]);
        }

        /// <summary>
        /// Applies the host's galaxy selection without changing the victory condition.
        /// </summary>
        /// <param name="index">The selected size index.</param>
        private void SelectGalaxySize(int index)
        {
            if (_coordinator.IsHost && _coordinator.Lobby != null)
                _coordinator.SelectLobbyGameSetup(
                    (GameSize)index,
                    _coordinator.Lobby.victoryCondition
                );
        }

        /// <summary>
        /// Applies the host's victory selection without changing the galaxy size.
        /// </summary>
        /// <param name="index">The selected victory index.</param>
        private void SelectVictoryCondition(int index)
        {
            if (_coordinator.IsHost && _coordinator.Lobby != null)
                _coordinator.SelectLobbyGameSetup(
                    _coordinator.Lobby.galaxySize,
                    index == 0 ? GameVictoryCondition.Conquest : GameVictoryCondition.Headquarters
                );
        }

        /// <summary>
        /// Populates the native save picker from the persistence service's actual metadata.
        /// </summary>
        private void ShowSaves()
        {
            _selectedSave = null;
            saveLoadButton.interactable = false;
            _saves = _coordinator.GetVisibleSavedGames(SaveGameManager.Instance);
            _saveSlots.Clear();
            ContentPack pack = _coordinator.LobbyContent;
            FactionThemeLibrary themes = new FactionThemeLibrary(pack.GameData.FactionThemes);
            IContentAssetSource assets = AppBootstrap.EnsureExists().GetContentAssets();
            foreach (SaveGameEntry entry in _saves)
            {
                string path = string.IsNullOrEmpty(entry.Metadata?.PlayerFactionID)
                    ? null
                    : themes.GetTheme(entry.Metadata.PlayerFactionID)?.SavedGameSlotIconImagePath;
                _saveSlots.Add(
                    OptionsSaveSlot.FromSave(
                        entry,
                        string.IsNullOrEmpty(path) ? null : assets.GetTexture(path)
                    )
                );
            }
            saveList.Render(_saveSlots, -1, false, true);
            saveDialog.SetActive(true);
        }

        /// <summary>
        /// Selects a real save entry for host-side validation.
        /// </summary>
        /// <param name="index">The selected shared-list row.</param>
        private void SelectSaveIndex(int index)
        {
            _selectedSave = _saves[index];
            saveLoadButton.interactable = !_busy;
            saveList.Render(_saveSlots, index, false, false);
        }

        /// <summary>
        /// Stages the selected save and returns to the waiting room.
        /// </summary>
        private void LoadSelectedSave()
        {
            if (_selectedSave == null)
                return;
            Run(
                async token =>
                {
                    await _coordinator.LoadLobbySaveAsync(_selectedSave, token);
                    token.ThrowIfCancellationRequested();
                    if (this != null && _coordinator.Failure == null)
                        saveDialog.SetActive(false);
                },
                MultiplayerMenuPage.Save
            );
        }

        /// <summary>
        /// Dismisses the saved-game picker.
        /// </summary>
        private void HideSaves()
        {
            CancelOperation();
            saveDialog.SetActive(false);
        }

        /// <summary>
        /// Starts Steam authentication through the existing launcher workflow.
        /// </summary>
        private void SignInSteam()
        {
            Run(token => SignInAsync("steam", token), MultiplayerMenuPage.Authentication);
        }

        /// <summary>
        /// Starts GOG authentication through the existing launcher workflow.
        /// </summary>
        private void SignInGog()
        {
            Run(token => SignInAsync("gog", token), MultiplayerMenuPage.Authentication);
        }

        /// <summary>
        /// Completes store authentication and opens discovery with the resulting cached account.
        /// </summary>
        /// <param name="provider">The selected store provider.</param>
        /// <param name="token">Cancels this form command.</param>
        /// <returns>The pending authentication and discovery.</returns>
        private async Task SignInAsync(string provider, CancellationToken token)
        {
            await _coordinator.SignInAsync(provider);
            token.ThrowIfCancellationRequested();
            if (_coordinator.PlayerSession != null)
                await RefreshBrowserAsync(token);
        }

        /// <summary>
        /// Sends the lobby input and preserves keyboard focus for continued conversation.
        /// </summary>
        private void SendLobbyChat()
        {
            if (string.IsNullOrWhiteSpace(lobbyChatInput.text))
                return;
            _coordinator.SendLobbyChat(lobbyChatInput.text);
            lobbyChatInput.text = string.Empty;
            lobbyChatInput.ActivateInputField();
        }

        /// <summary>
        /// Handles Enter in the lobby message field.
        /// </summary>
        /// <param name="value">The submitted field text.</param>
        private void SubmitLobbyChat(string value)
        {
            SendLobbyChat();
        }

        /// <summary>
        /// Sends the general conversation input through the authenticated online channel.
        /// </summary>
        private void SendGeneralChat()
        {
            if (_coordinator.SendGeneralChat(generalChatInput.text))
                generalChatInput.text = string.Empty;
            generalChatInput.ActivateInputField();
        }

        /// <summary>
        /// Handles Enter in the general message field.
        /// </summary>
        /// <param name="value">The submitted field text.</param>
        private void SubmitGeneralChat(string value)
        {
            SendGeneralChat();
        }

        /// <summary>
        /// Renders general conversation only when its received message count changes.
        /// </summary>
        private void RenderGeneralChat()
        {
            generalSendButton.interactable = _coordinator.IsGeneralChatConnected;
            if (_generalChatCount == _coordinator.GeneralChat.Count)
                return;
            _generalChatCount = _coordinator.GeneralChat.Count;
            RenderTranscript(generalChatText, _coordinator.GeneralChat);
        }

        /// <summary>
        /// Reflows a plain-text conversation into its reusable scroll area.
        /// </summary>
        /// <param name="text">The plain-text transcript label.</param>
        /// <param name="messages">The received conversation.</param>
        private static void RenderTranscript(
            TMP_Text text,
            IReadOnlyList<MultiplayerChatMessage> messages
        )
        {
            StringBuilder transcript = new StringBuilder();
            foreach (MultiplayerChatMessage message in messages)
            {
                if (!message.isSystem)
                    transcript.Append(message.sender).Append(": ");
                transcript.Append(message.text).Append('\n');
            }
            text.GetComponentInParent<MenuChatPanelView>().SetTranscript(transcript.ToString());
        }

        /// <summary>
        /// Applies the search text without making another service request.
        /// </summary>
        /// <param name="value">The edited search text.</param>
        private void FilterGames(string value)
        {
            RenderGames();
        }

        /// <summary>
        /// Applies the installed-pack filter without making another service request.
        /// </summary>
        /// <param name="index">The selected pack filter.</param>
        private void FilterPacks(int index)
        {
            RenderGames();
        }

        /// <summary>
        /// Requests closure through the main menu's existing modal ownership boundary.
        /// </summary>
        private void RequestClose()
        {
            if (_busy || _coordinator.IsConnecting)
                CancelOperation();
            if (_coordinator.Lobby == null)
                CloseRequested?.Invoke();
            else
            {
                _closeAfterLeaving = true;
                leaveConfirmationRoot.SetActive(true);
                leaveConfirmation.Show("Are you sure you want to leave this lobby?");
            }
        }

        /// <summary>
        /// Leaves only after the shared native confirmation is accepted.
        /// </summary>
        private void ConfirmLeave()
        {
            CancelLeave();
            if (_closeAfterLeaving)
                CloseRequested?.Invoke();
            else
            {
                _coordinator.Disconnect();
                _chatCount = -1;
                Render();
                Run(RefreshBrowserAsync);
            }
        }

        /// <summary>
        /// Dismisses leave confirmation without changing the online session.
        /// </summary>
        private void CancelLeave()
        {
            leaveConfirmation.Hide();
            leaveConfirmationRoot.SetActive(false);
        }

        /// <summary>
        /// Runs form-owned work and isolates failures from other pages.
        /// </summary>
        /// <param name="operation">The cancellable form command.</param>
        /// <param name="page">The form receiving progress and errors.</param>
        private async void Run(
            Func<CancellationToken, Task> operation,
            MultiplayerMenuPage page = MultiplayerMenuPage.Browser
        )
        {
            if (_busy)
                return;
            _coordinator.ClearFailure();
            await _operation.RunAsync(page, operation);
            if (this != null && _operation.Error != null)
                Debug.LogWarning(_operation.Error);
        }

        /// <summary>
        /// Cancels pending connection work without abandoning a room while validating a save.
        /// </summary>
        private void CancelOperation()
        {
            bool cancelConnection =
                _operation.Page != MultiplayerMenuPage.Save
                && (_busy || _coordinator.IsConnecting || _coordinator.Lobby == null);
            _operation.Cancel();
            _coordinator.ClearFailure();
            if (cancelConnection)
                _coordinator.Disconnect();
            Render();
        }

        /// <summary>
        /// Formats content failures for their owning form while leaving diagnostics in the console.
        /// </summary>
        /// <param name="exception">The form failure, when present.</param>
        /// <returns>The user-facing message or null.</returns>
        private static string FormatError(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
                if (current is XmlException || current is XmlSchemaException)
                    return "Unable to load this content pack. Check that its files are up to date.";
            return exception?.Message;
        }

        /// <summary>
        /// Changes a table's sort direction through its clickable column header.
        /// </summary>
        /// <param name="column">The selected table column.</param>
        private void SortGames(MultiplayerGameColumn column)
        {
            _sortDescending = _sortColumn == column && !_sortDescending;
            _sortColumn = column;
            RenderGames();
        }

        /// <summary>
        /// Updates a shared action button's caption.
        /// </summary>
        /// <param name="button">The authored action button.</param>
        /// <param name="value">The new caption.</param>
        private static void SetButtonLabel(Button button, string value)
        {
            button.GetComponentInChildren<TMP_Text>().text = value;
        }
    }
}
