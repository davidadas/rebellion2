using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using UnityEngine;
using EOSManager = PlayEveryWare.EpicOnlineServices.EOSManager;
using NetworkConnectionType = Epic.OnlineServices.P2P.NetworkConnectionType;
using RelayControl = Epic.OnlineServices.P2P.RelayControl;

namespace Rebellion.Tests.MultiplayerIntegration
{
    /// <summary>
    /// Runs an explicitly requested two-account integration through real EOS lobby and tactical services.
    /// </summary>
    public sealed partial class EosTacticalIntegrationRunner : MonoBehaviour
    {
        private EosTacticalIntegrationOptions _options;
        private readonly EosTacticalIntegrationReport _report = new();
        private readonly Stopwatch _elapsed = new();
        private ContentPack _content;
        private MultiplayerCoordinator _coordinator;
        private GameSessionManager _runtime;
        private ITacticalCombatPresentationDriver _driver;
        private Task _operation;
        private Task<PlayerRequestOutcome> _begin;
        private Task<PlayerRequestOutcome> _dayFence;
        private IEnumerator _tick;
        private GameResultPipeline _resultPipeline;
        private EosTacticalCombatEvidence _evidence;
        private EosTacticalIntegrationReport _peerReport;
        private double _nextSearch;
        private double _nextReport;
        private double _nextOrders;
        private int _lastSystemTime = -1;
        private bool _dayStarted;
        private bool _launchRequested;
        private bool _formationSent;
        private bool _withdrawalAttempted;
        private bool _outcomePublished;
        private bool _finished;
        private bool _speedRequested;
        private bool _inspectionRequested;
        private bool _pauseReadyPublished;
        private bool _pauseCheckPublished;
        private bool _targetsPublished;
        private EosTacticalRouteObserver _route;
        private string _state;

        /// <summary>
        /// Installs the diagnostics runner only for an explicitly isolated integration invocation.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EosTacticalIntegrationOptions options = EosTacticalIntegrationOptions.Parse(
                Environment.GetCommandLineArgs()
            );
            if (options == null)
                return;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            GameObject owner = new(nameof(EosTacticalIntegrationRunner));
            DontDestroyOnLoad(owner);
            owner.AddComponent<EosTacticalIntegrationRunner>()._options = options;
        }

        /// <summary>
        /// Loads real content and wires the production coordinator without graphical scene dependencies.
        /// </summary>
        private void Start()
        {
            try
            {
                _elapsed.Start();
                _report.runID = _options.RunID;
                _report.role = _options.Role;
                _report.forceRelay = _options.ForceRelay;
                _report.verifyRecovery = _options.VerifyRecovery;
                _report.capitalsPerSide = _options.CapitalsPerSide;
                _report.squadronsPerSide = _options.SquadronsPerSide;
                _report.guestWithdrawalAtSystemTime = _options.GuestWithdrawalAtSystemTime;
                _report.temporaryCachePath = Application.temporaryCachePath;
                _report.simulationBuild = typeof(GameRoot)
                    .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .Single(attribute => attribute.Key == "SimulationBuild")
                    .Value;
                Directory.CreateDirectory(_options.ReportDirectory);
                SetPhase("auth-start");
                _content = ContentPackLoader.OpenActive();
                _coordinator = gameObject.AddComponent<MultiplayerCoordinator>();
                _coordinator.Initialize(
                    () => _content,
                    SaveGameManager.Instance,
                    () => null,
                    () => false,
                    LaunchCampaign,
                    _ => false
                );
                _coordinator.TacticalCombatAvailable += BeginCombat;
                _coordinator.CompletedBattle += HandleCoordinatorResult;
                Require(
                    _coordinator.PlayerSession != null,
                    "A valid cached real-account session is required."
                );
                if (_options.Role == "host")
                {
                    GameSummary summary = _content.CreateDefaultGameSummary();
                    summary.Seed = 314159;
                    summary.GalaxySize = GameSize.Small;
                    summary.PlayerFactionID = "FNEMP1";
                    summary.VictoryCondition = GameVictoryCondition.Conquest;
                    _operation = _coordinator.HostAsync(
                        "EOS integration host",
                        RoomName(),
                        summary
                    );
                    _state = "hosting";
                }
                else
                    _state = "host-barrier";
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Drives normal public lobby, campaign, and presentation operations within one bounded wall-clock run.
        /// </summary>
        private void Update()
        {
            if (_finished)
                return;
            if (_state == "failure-cleanup")
            {
                CompleteFailureCleanup();
                return;
            }
            try
            {
                VerifyForcedRoute();
                Require(
                    _elapsed.Elapsed.TotalSeconds < _options.DeadlineSeconds,
                    "The real-service integration exceeded its deadline."
                );
                if (_coordinator?.Failure != null && !_outcomePublished)
                {
                    Fail(_coordinator.Failure);
                    return;
                }
                if (_operation?.IsCompleted == true)
                    _operation.GetAwaiter().GetResult();
                switch (_state)
                {
                    case "hosting":
                        if (_operation?.IsCompleted == true)
                        {
                            Authenticated();
                            _operation = null;
                            _state = "host-listing";
                        }
                        break;
                    case "host-barrier":
                        EosTacticalIntegrationReport host = ReadPeerReport();
                        if (host?.hostReadyForJoin == true)
                        {
                            Require(
                                host.forceRelay == _options.ForceRelay,
                                "The participants requested different transport policies."
                            );
                            Require(
                                host.verifyRecovery == _options.VerifyRecovery,
                                "The participants requested different recovery cases."
                            );
                            Require(
                                host.capitalsPerSide == _options.CapitalsPerSide
                                    && host.squadronsPerSide == _options.SquadronsPerSide,
                                "The participants requested different authored fleet counts."
                            );
                            Require(
                                host.guestWithdrawalAtSystemTime
                                    == _options.GuestWithdrawalAtSystemTime,
                                "The participants requested different optional guest withdrawal times."
                            );
                            _state = "guest-listing";
                        }
                        break;
                    case "host-listing":
                    case "guest-listing":
                        DiscoverLobby();
                        break;
                    case "joining":
                        if (_operation?.IsCompleted == true)
                        {
                            _operation = null;
                            _state = "lobby";
                            SetPhase("lobby");
                        }
                        break;
                    case "lobby":
                        ReadyLobby();
                        break;
                    case "campaign":
                        AdvanceCampaign();
                        break;
                    case "combat":
                        AdvanceCombat();
                        break;
                    case "recovery":
                        AdvanceRecovery();
                        break;
                    case "recovery-entry":
                        AdvanceRecoveryEntry();
                        break;
                    case "recovery-outcome":
                        CompareRecoveryOutcome();
                        break;
                    case "day-barrier":
                        VerifyDayBarrier();
                        break;
                    case "outcome":
                        CompareOutcome();
                        break;
                    case "cleanup":
                        if (_operation?.IsCompleted == true)
                        {
                            _report.cleanupComplete = true;
                            _report.passed = true;
                            if (!Publish())
                                break;
                            _finished = true;
                            Application.Quit(0);
                        }
                        break;
                }
                if (_elapsed.Elapsed.TotalSeconds >= _nextReport)
                {
                    _nextReport = _elapsed.Elapsed.TotalSeconds + 1;
                    if (_state != "cleanup")
                        ReadPeerReport();
                    CaptureWorkloadProgress();
                    Publish();
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Records the product identity returned by EOS Connect without reading or writing its credential.
        /// </summary>
        private void Authenticated()
        {
            var user = EOSManager
                .Instance.GetEOSPlatformInterface()
                .GetConnectInterface()
                .GetLoggedInUserByIndex(0);
            Require(
                user?.IsValid() == true,
                "EOS did not return an authenticated product identity."
            );
            _report.localUserID = user.ToString();
            _report.authenticated = true;
            SetPhase("auth-success");
        }

        /// <summary>
        /// Uses real EOS discovery and joins only this run's exact publicly advertised lobby.
        /// </summary>
        private void DiscoverLobby()
        {
            if (_operation?.IsCompleted == false)
                return;
            if (_operation != null)
            {
                _operation.GetAwaiter().GetResult();
                _operation = null;
                if (!_report.authenticated)
                    Authenticated();
                MultiplayerGameListing listing = _coordinator.Games.SingleOrDefault(game =>
                    game.name == RoomName()
                );
                if (listing != null)
                {
                    _report.lobbyID = listing.id;
                    if (_options.Role == "host")
                    {
                        Require(
                            listing.ownerUserID == _report.localUserID,
                            "The listed host is not the authenticated local product user."
                        );
                        PrepareTransport(listing);
                        _report.hostReadyForJoin = true;
                        _state = "lobby";
                        SetPhase("lobby");
                    }
                    else
                    {
                        Require(
                            listing.ownerUserID != _report.localUserID,
                            "The guest must use a distinct authenticated account."
                        );
                        PrepareTransport(listing);
                        _operation = _coordinator.JoinAsync("EOS integration guest", listing);
                        _state = "joining";
                    }
                    return;
                }
            }
            if (_elapsed.Elapsed.TotalSeconds >= _nextSearch)
            {
                _nextSearch = _elapsed.Elapsed.TotalSeconds + 1;
                _operation = _coordinator.RefreshGamesAsync();
            }
        }

        /// <summary>
        /// Prepares the optional transport observer before either participant starts peer negotiation.
        /// </summary>
        /// <param name="listing">The genuine discovered EOS lobby for this run.</param>
        private void PrepareTransport(MultiplayerGameListing listing)
        {
            Require(!string.IsNullOrEmpty(listing.id), "EOS returned no lobby identity.");
            var p2p = EOSManager.Instance.GetEOSPlatformInterface().GetP2PInterface();
            RelayControl requested = _options.ForceRelay
                ? RelayControl.ForceRelays
                : RelayControl.AllowRelays;
            _report.requestedRelayPolicy = requested.ToString();
            RelayControl actual = EosTacticalRouteObserver.SetRelayPolicy(p2p, requested);
            _report.actualRelayPolicy = actual.ToString();
            _report.relayPolicyVerified = actual == requested;
            _route = new EosTacticalRouteObserver(p2p, _report.localUserID, listing.id);
        }

        /// <summary>
        /// Rejects direct-route evidence immediately during a forced-relay run on the exact authenticated lobby socket.
        /// </summary>
        private void VerifyForcedRoute()
        {
            if (!_options.ForceRelay || _route == null)
                return;
            EosTacticalRouteObservation[] observations = _route.Capture();
            _report.routes = observations;
            Require(
                !observations.Any(observation =>
                    observation.NetworkType == NetworkConnectionType.DirectConnection
                ),
                "A forced-relay run observed a direct connection on its admitted lobby socket."
            );
        }

        /// <summary>
        /// Samples alive participants and remaining hull once per report interval without capturing or consuming network effects.
        /// </summary>
        private void CaptureWorkloadProgress()
        {
            if (_driver == null)
                return;
            _report.attackerAliveCapitals = 0;
            _report.defenderAliveCapitals = 0;
            _report.attackerAliveFighterSquadrons = 0;
            _report.defenderAliveFighterSquadrons = 0;
            _report.attackerAliveFighterCraft = 0;
            _report.defenderAliveFighterCraft = 0;
            _report.attackerHullTotal = 0;
            _report.defenderHullTotal = 0;
            foreach (TacticalCombatUnit unit in _driver.Session.Units)
            {
                if (!unit.Systems.IsAlive)
                    continue;
                if (unit.Side == CombatSide.Attacker)
                {
                    _report.attackerHullTotal += unit.Systems.Hull;
                    if (unit.IsFighter)
                    {
                        _report.attackerAliveFighterSquadrons++;
                        _report.attackerAliveFighterCraft += unit.SquadronSize;
                    }
                    else
                        _report.attackerAliveCapitals++;
                }
                else
                {
                    _report.defenderHullTotal += unit.Systems.Hull;
                    if (unit.IsFighter)
                    {
                        _report.defenderAliveFighterSquadrons++;
                        _report.defenderAliveFighterCraft += unit.SquadronSize;
                    }
                    else
                        _report.defenderAliveCapitals++;
                }
            }
            _report.progressUpdatedSeconds = _elapsed.Elapsed.TotalSeconds;
        }

        /// <summary>
        /// Uses each participant's real faction selection, content agreement, and explicit ready controls.
        /// </summary>
        private void ReadyLobby()
        {
            MultiplayerLobbyState lobby = _coordinator.Lobby;
            if (lobby == null)
                return;
            string desired = _options.Role == "host" ? "FNEMP1" : "FNALL1";
            string selected = _options.Role == "host" ? lobby.hostFactionID : lobby.guestFactionID;
            if (selected != desired)
            {
                _coordinator.SelectLobbyFaction(desired);
                return;
            }
            bool ready = _options.Role == "host" ? lobby.hostReady : lobby.guestReady;
            if (
                !ready
                && !string.IsNullOrEmpty(lobby.guestName)
                && lobby.guestContentReady
                && lobby.HasValidFactionSelection
            )
                _coordinator.SetLobbyReady(true);
            if (_options.Role == "host" && lobby.CanLaunch && !_launchRequested)
            {
                _launchRequested = true;
                _coordinator.LaunchLobby();
            }
        }

        /// <summary>
        /// Composes the real coordinated runtime from negotiated settings before its initial snapshot handshake.
        /// </summary>
        /// <param name="content">The content verified by the normal multiplayer lobby.</param>
        /// <param name="configuration">The real negotiated launch ownership.</param>
        private void LaunchCampaign(
            ContentPack content,
            MultiplayerLaunchConfiguration configuration
        )
        {
            try
            {
                DetachRuntimeObservers();
                _runtime?.EndGame();
                _runtime = new GameSessionManager(content, SaveGameManager.Instance);
                GameSession session = _runtime.StartGame(
                    configuration.RestoredGame
                        ?? EosTacticalScenario.Create(content, configuration.Summary, _options),
                    _coordinator
                );
                AttachRuntimeObservers(session);
                _coordinator.NotifyPresentationReady(_runtime);
                _report.recoveryLaunchObserved |= configuration.RestoredGame != null;
                _state = _recoveryInterrupted ? "recovery" : "campaign";
                SetPhase(_state);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Advances one agreed campaign day and submits each human's own manual-combat response.
        /// </summary>
        private void AdvanceCampaign()
        {
            _report.synchronized = _coordinator.IsSynchronized;
            if (!_coordinator.IsSynchronized)
                return;
            GameSession session = _runtime.GetActiveGameSession();
            if (_options.VerifyRecovery && !_initialRecoveryTickObserved)
            {
                _initialRecoveryTickObserved = true;
                _report.recoveryInitialTick = session.Game.CurrentTick;
            }
            MultiplayerCampaignIdentity identity = session.Game.Metadata.MultiplayerCampaign;
            _report.hostUserID = identity.HostUserID;
            _report.guestUserID = identity.GuestUserID;
            Require(
                identity.HostUserID != identity.GuestUserID,
                "Two distinct authenticated EOS participants are required."
            );
            Require(
                _report.localUserID
                    == (_options.Role == "host" ? identity.HostUserID : identity.GuestUserID),
                "The campaign actor does not match its authenticated product identity."
            );
            GameManager clock = _runtime.GetActiveGameManager();
            if (!_dayStarted)
            {
                if (!_speedRequested)
                {
                    _speedRequested = true;
                    clock.SetGameSpeed(TickSpeed.Fast);
                }
                if (_coordinator.TryBeginGameTick(Time.unscaledDeltaTime))
                {
                    _dayStarted = true;
                    _tick = session.Tick.ProcessTickIncrementally();
                }
            }
            if (_tick != null && !clock.AdvanceTick(_tick))
            {
                (_tick as IDisposable)?.Dispose();
                _tick = null;
            }
            SpaceCombatCommands commands = session.GetService<SpaceCombatCommands>();
            if (commands.TryGetPendingCombat(out PendingCombatResult pending) && _begin == null)
            {
                Require(
                    pending.PlanetInstanceID == EosTacticalScenario.PlanetID,
                    "An unrelated campaign encounter preceded the integration arena."
                );
                if (_recoveryInterrupted && !VerifyFreshResponses(pending))
                    return;
                _begin = _runtime.PlayerRequests.ExecuteAsync(
                    new BeginTacticalCombatRequest
                    {
                        PlanetID = pending.PlanetInstanceID,
                        Tick = pending.Tick,
                    }
                );
            }
            if (_begin?.IsCompleted == true)
                Require(
                    _begin.GetAwaiter().GetResult().Accepted,
                    "The authenticated manual-combat response was rejected."
                );
        }

        /// <summary>
        /// Prepares a passive headless view for the real coordinator-created tactical driver.
        /// </summary>
        /// <param name="driver">The actual authoritative or replica driver admitted by the campaign.</param>
        private void BeginCombat(ITacticalCombatPresentationDriver driver)
        {
            try
            {
                Require(_driver == null, "The integration received more than one battle driver.");
                Require(
                    _tick == null,
                    "The campaign entry iterator must finish before an ordered manual battle is admitted."
                );
                _driver = driver;
                TacticalCombatSession session = driver.Session;
                _report.replica = session.IsReplica;
                _report.units = session.Units.Count;
                _report.fighters = session.Units.Count(unit => unit.IsFighter);
                Require(
                    session.Units.Count
                        == 2 * (_options.CapitalsPerSide + _options.SquadronsPerSide),
                    "The admitted battle roster does not match the requested authored fleet."
                );
                _report.fighterCraft = session
                    .Units.Where(unit => unit.IsFighter)
                    .Sum(unit =>
                        ((Rebellion.Game.Units.Starfighter)unit.Source).CurrentSquadronSize
                    );
                _report.battleID = _runtime
                    .GetActiveGameSession()
                    .GetService<SpaceCombatCommands>()
                    .ActiveTacticalBattleID;
                if (_recoveryInterrupted)
                {
                    _report.freshBattleID = _report.battleID;
                }
                RecordRecoveryEntry(driver);
                Require(
                    session.IsReplica == (_options.Role == "guest"),
                    "The negotiated tactical authority is incorrect."
                );
                GameRoot game = _runtime.GetActiveGame();
                MultiplayerCampaignIdentity participants = game.Metadata.MultiplayerCampaign;
                Require(
                    participants.HostFactionID != participants.GuestFactionID
                        && game.GetFactionPlayer(participants.HostFactionID)?.ControllerType
                            == PlayerControllerType.Human
                        && game.GetFactionPlayer(participants.GuestFactionID)?.ControllerType
                            == PlayerControllerType.Human,
                    "The admitted campaign must bind both factions to real human controllers."
                );
                _report.humanControllersVerified = true;
                _evidence = new EosTacticalCombatEvidence();
                session.NetworkCommandRequested += HandleCommand;
                session.ShotFired += HandleShot;
                session.ImpactApplied += HandleImpact;
                session.UnitDestroyed += HandleDestroyed;
                session.UnitWithdrawn += HandleWithdrawn;
                session.FighterRecovered += HandleRecovered;
                driver.Prepare(new Arrival());
                _state = _recoveryInterrupted ? "recovery-entry" : "combat";
                SetPhase(_state);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Advances the actual driver with real elapsed time and sends only normal authenticated selection commands.
        /// </summary>
        private void AdvanceCombat()
        {
            if (TryEnterExpectedRecovery())
                return;
            _driver.Advance(Time.unscaledDeltaTime);
            TacticalCombatSession session = _driver.Session;
            _report.systemTime = session.SystemTime;
            if (_lastSystemTime != session.SystemTime)
            {
                _lastSystemTime = session.SystemTime;
                _report.stateUpdatesObserved++;
            }
            _report.hullDamageObserved |= session.Units.Any(unit =>
                unit.Systems.Hull < unit.InitialHull
            );
            bool authoritativeState =
                !session.IsReplica
                || _driver.ReplicaPlayback.TryGetPose(session.Units[0].InstanceID, out _, out _);
            if (!authoritativeState)
                return;
            _report.authoritativeStateObserved = true;
            Require(
                !session.GetSide(CombatSide.Attacker).IsAutomated
                    && !session.GetSide(CombatSide.Defender).IsAutomated,
                "Both real players must enter manual combat."
            );
            _report.manualControlVerified = true;
            ObserveOrders(session);
            AdvanceControlCheck(session);
            if (TryDisconnectForRecovery(session))
                return;
            ObserveGuestWithdrawal(session);
            TryRequestGuestWithdrawal(session);
            if (
                !session.IsArriving
                && !session.IsPaused
                && !session.IsComplete
                && !session.GetSide(session.PlayerSide).IsWithdrawing
                && !_withdrawalAttempted
                && _elapsed.Elapsed.TotalSeconds >= _nextOrders
            )
            {
                _nextOrders = _elapsed.Elapsed.TotalSeconds + 1;
                IssueOrders(session);
            }
            if (session.PendingAttackRunMovie != null)
                _driver.AcknowledgeMovie(session.PendingAttackRunMovie);
            if (!_driver.CompletionReady)
                return;
            Require(
                !_options.VerifyRecovery,
                "The recovery case completed combat before its required real disconnect and rejoin."
            );
            _report.completionAgreed = true;
            _report.orderedEffectDigest = _evidence.Complete();
            Require(
                _report.pauseCheckComplete
                    && _report.sharedResumeObserved
                    && _report.localTargetObserved
                    && _report.remoteTargetObserved,
                "Both players must verify shared pause, target ownership, and resumed combat."
            );
            if (_options.GuestWithdrawalAtSystemTime > 0)
                Require(
                    _report.guestWithdrawalObserved,
                    "The explicitly requested guest withdrawal was not observed on this peer."
                );
            Require(
                _driver.CommittedResult != null,
                "The agreed battle has no applied campaign result."
            );
            Require(
                _report.localOrdersObserved && _report.remoteOrdersObserved,
                "The two authenticated formation commands were not both observed."
            );
            Require(
                _report.shots > 0 && _report.capitalShots > 0 && _report.fighterShots > 0,
                "The battle must discharge real capital and fighter weapons."
            );
            Require(
                _report.impacts > 0 && _report.hullDamageObserved,
                "The battle must produce natural impacts and hull damage."
            );
            Require(
                _report.results == 1 && _report.coordinatorResults == 1,
                "The battle result must publish exactly once through both result boundaries."
            );
            Require(
                _report.completedTicks == 1 && _report.combatResumptions == 1,
                "The campaign must resume and complete its deferred day exactly once."
            );
            Require(
                _runtime.GetActiveGameSession().CanCaptureSettledState,
                "The completed campaign is not settled."
            );
            _state = "day-barrier";
            SetPhase("day-barrier");
        }

        /// <summary>
        /// Exercises authenticated targeting and shared pause while comparing independently observed frozen state.
        /// </summary>
        /// <param name="session">The authoritative-frame-backed local combat view.</param>
        private void AdvanceControlCheck(TacticalCombatSession session)
        {
            if (_report.pauseCheckComplete && !session.IsPaused)
            {
                _report.sharedResumeObserved = true;
                return;
            }
            bool targetsAccepted =
                HasEnemyTargets(session, session.PlayerSide)
                && HasEnemyTargets(
                    session,
                    session.PlayerSide == CombatSide.Attacker
                        ? CombatSide.Defender
                        : CombatSide.Attacker
                );
            if (
                _options.Role == "guest"
                && !_report.pauseRequested
                && !session.IsArriving
                && !session.IsComplete
                && targetsAccepted
                && _report.localOrdersObserved
                && _report.remoteOrdersObserved
                && _targetsPublished
                && _peerReport?.localOrdersObserved == true
                && _peerReport.remoteOrdersObserved
                && _peerReport.localTargetObserved
                && _peerReport.remoteTargetObserved
            )
            {
                _report.pauseRequested = true;
                session.IsPaused = true;
                SetPhase("combat-control-check");
            }
            bool pauseRequested =
                _options.Role == "guest"
                    ? _report.pauseRequested
                    : _peerReport?.pauseRequested == true;
            if (!pauseRequested)
                return;
            if (!session.IsPaused || session.PendingAttackRunMovie != null)
                return;
            _report.sharedPauseObserved = true;
            if (!_inspectionRequested)
            {
                TacticalCombatUnit enemy = session.Units.First(unit =>
                    unit.Side != session.PlayerSide && unit.IsTargetable
                );
                session.Selection.QueueClear();
                session.Selection.QueueObject(enemy);
                _inspectionRequested = true;
                return;
            }
            if (!_report.pauseCheckReady)
            {
                TacticalCombatSelection selection = session.Selection;
                if (selection.Kind != TacticalCombatSelectionKind.EnemyInspection)
                    return;
                Require(
                    selection.PrimaryUnit != null
                        && selection.PrimaryUnit.Side != session.PlayerSide
                        && !selection.CanIssueCommands,
                    "Opponent inspection must never grant command ownership."
                );
                Require(
                    targetsAccepted,
                    "Both factions must retain accepted capital and fighter enemy targets while paused."
                );
                _report.opponentInspectionVerified = true;
                _report.pausedSystemTime = session.SystemTime;
                _report.pausedStateFingerprint = EosTacticalCombatEvidence.CapturePausedState(
                    session,
                    Path.Combine(_options.ReportDirectory, _options.Role + "-paused-state.json")
                );
                _report.pauseCheckReady = true;
                Publish();
            }
            if (!_pauseReadyPublished || _peerReport?.pauseCheckReady != true)
                return;
            Require(
                session.SystemTime == _report.pausedSystemTime
                    && _peerReport.pausedSystemTime == _report.pausedSystemTime
                    && _peerReport.pausedStateFingerprint == _report.pausedStateFingerprint,
                "The paused peers must observe identical public state at the same frozen system time."
            );
            Require(
                _peerReport.sharedPauseObserved
                    && _peerReport.opponentInspectionVerified
                    && _peerReport.localTargetObserved
                    && _peerReport.remoteTargetObserved,
                "The counterpart must independently verify pause, target acceptance, and opponent inspection."
            );
            if (!_report.pauseCheckComplete)
            {
                _report.pauseCheckComplete = true;
                Publish();
            }
            if (
                _options.Role == "host"
                && _pauseCheckPublished
                && _peerReport.pauseCheckComplete
                && !_report.resumeRequested
            )
            {
                _report.resumeRequested = true;
                session.IsPaused = false;
                Publish();
            }
        }

        /// <summary>
        /// Requires accepted enemy targets for both a capital group and a deployed fighter group of one side.
        /// </summary>
        /// <param name="session">The locally observed battle.</param>
        /// <param name="side">The authenticated group owner.</param>
        /// <returns>Whether both group kinds retain an applied or requested valid enemy target.</returns>
        private static bool HasEnemyTargets(TacticalCombatSession session, CombatSide side)
        {
            bool capitals = false;
            bool fighters = false;
            for (int category = 0; category < 12; category++)
            {
                TacticalCombatGroup group =
                    category < 8
                        ? session.GetObjectGroup(side, category)
                        : session.GetFighterGroup(side, category - 8);
                if (
                    group?.Side != side
                    || group.RequestedMembers.Any(unit => unit.IsTargetable) != true
                    || group.RequestedMembers.Any(unit => unit.Side != side)
                    || !group
                        .Targets.Concat(group.RequestedTargets)
                        .Any(unit => unit.Side != side && unit.IsTargetable)
                )
                    continue;
                if (group.IsFighterGroup)
                    fighters = true;
                else
                    capitals = true;
            }
            return capitals && fighters;
        }

        /// <summary>
        /// Submits one explicitly requested guest retreat through the same prepare and confirm operations as the combat view.
        /// </summary>
        /// <param name="session">The authoritative-frame-backed local combat view.</param>
        private void TryRequestGuestWithdrawal(TacticalCombatSession session)
        {
            if (
                _options.Role != "guest"
                || !_report.sharedResumeObserved
                || _options.GuestWithdrawalAtSystemTime == 0
                || _withdrawalAttempted
                || session.IsArriving
                || session.IsComplete
                || session.SystemTime < _options.GuestWithdrawalAtSystemTime
                || _report.capitalShots == 0
                || _report.fighterShots == 0
                || !_report.hullDamageObserved
            )
                return;
            _withdrawalAttempted = true;
            Require(
                session.PreparePlayerWithdrawal(),
                "The normal guest withdrawal eligibility check rejected the requested retreat."
            );
            Require(
                session.ConfirmPlayerWithdrawal(),
                "The normal guest withdrawal confirmation rejected the requested retreat."
            );
            Require(
                _report.withdrawalCommandsSubmitted == 1,
                "The confirmed guest withdrawal did not submit exactly one authenticated semantic command."
            );
            _report.guestWithdrawalSubmitted = true;
            _report.guestWithdrawalSubmittedAtSystemTime = session.SystemTime;
        }

        /// <summary>
        /// Records the guest side's actual withdrawal lifecycle on both authority and replica.
        /// </summary>
        /// <param name="session">The current authoritative state or validated replica projection.</param>
        private void ObserveGuestWithdrawal(TacticalCombatSession session)
        {
            CombatSide guest =
                _options.Role == "guest" ? session.PlayerSide
                : session.PlayerSide == CombatSide.Attacker ? CombatSide.Defender
                : CombatSide.Attacker;
            if (!_report.guestWithdrawalObserved && session.GetSide(guest).IsWithdrawing)
            {
                _report.guestWithdrawalObserved = true;
                _report.guestWithdrawalObservedAtSystemTime = session.SystemTime;
            }
        }

        /// <summary>
        /// Verifies the existing day-receipt barrier with one normal guest request before final state capture.
        /// </summary>
        private void VerifyDayBarrier()
        {
            if (_options.Role == "guest")
            {
                _dayFence ??= _runtime.PlayerRequests.ExecuteAsync(
                    new SetSpeedRequest { Speed = TickSpeed.Fast }
                );
                if (!_dayFence.IsCompleted)
                    return;
                Require(
                    _dayFence.GetAwaiter().GetResult().Accepted,
                    "The post-day authenticated guest request was rejected."
                );
            }
            else
            {
                EosTacticalIntegrationReport peer = ReadPeerReport();
                if (peer?.dayBarrierVerified != true)
                    return;
            }
            _report.dayBarrierVerified = true;
            _report.finalTick = _runtime.GetActiveGame().CurrentTick;
            _report.campaignFingerprint = MultiplayerSnapshotCodec.ComputeStateFingerprint(
                _runtime.GetActiveGame()
            );
            _report.outcomeReady = true;
            _outcomePublished = true;
            _state = "outcome";
            SetPhase("outcome");
        }

        /// <summary>
        /// Sends a distinct persistent formation marker and enemy targeting through the existing semantic command boundary.
        /// </summary>
        /// <param name="session">The current local player's real tactical view.</param>
        private void IssueOrders(TacticalCombatSession session)
        {
            TacticalCombatSelection selection = session.Selection;
            if (!_formationSent)
            {
                if (
                    selection.Kind == TacticalCombatSelectionKind.ObjectGroup
                    && selection.Group?.Side == session.PlayerSide
                    && selection.CanIssueCommands
                )
                {
                    selection.RequestFormation(
                        _options.Role == "host"
                            ? TacticalCombatFormationKind.Surround
                            : TacticalCombatFormationKind.Direct
                    );
                    _formationSent = true;
                }
                else
                {
                    for (int category = 0; category < 8; category++)
                        if (
                            session
                                .GetObjectGroup(session.PlayerSide, category)
                                ?.RequestedMembers.Any(unit => unit.IsTargetable) == true
                        )
                        {
                            selection.QueueObjectCategory(category);
                            return;
                        }
                }
            }
            for (int category = 0; category < 12; category++)
            {
                TacticalCombatGroup group =
                    category < 8
                        ? session.GetObjectGroup(session.PlayerSide, category)
                        : session.GetFighterGroup(session.PlayerSide, category - 8);
                if (
                    group?.RequestedMembers.Any(unit => unit.IsTargetable) != true
                    || group.PrimaryTarget?.IsTargetable == true
                    || group.RequestedTargets.Any(unit => unit.IsTargetable)
                )
                    continue;
                TacticalCombatUnit target =
                    session.Units.FirstOrDefault(unit =>
                        unit.Side != session.PlayerSide
                        && unit.IsTargetable
                        && unit.IsFighter == group.IsFighterGroup
                    )
                    ?? session.Units.FirstOrDefault(unit =>
                        unit.Side != session.PlayerSide && unit.IsTargetable
                    );
                if (target == null)
                    continue;
                selection.QueueClear();
                if (category < 8)
                    selection.QueueObjectCategory(category);
                else
                    selection.QueueFighterCategory(category - 8);
                selection.QueueObject(target);
            }
        }

        /// <summary>
        /// Checks each participant's distinct requested formation on the real authoritative projection.
        /// </summary>
        /// <param name="session">The locally observed real battle state.</param>
        private void ObserveOrders(TacticalCombatSession session)
        {
            CombatSide other =
                session.PlayerSide == CombatSide.Attacker
                    ? CombatSide.Defender
                    : CombatSide.Attacker;
            TacticalCombatFormationKind ownFormation =
                _options.Role == "host"
                    ? TacticalCombatFormationKind.Surround
                    : TacticalCombatFormationKind.Direct;
            TacticalCombatFormationKind otherFormation =
                _options.Role == "host"
                    ? TacticalCombatFormationKind.Direct
                    : TacticalCombatFormationKind.Surround;
            _report.localOrdersObserved |= session.Groups.Any(group =>
                !group.IsFighterGroup
                && group.Side == session.PlayerSide
                && group.RequestedFormation == ownFormation
            );
            _report.remoteOrdersObserved |= session.Groups.Any(group =>
                !group.IsFighterGroup
                && group.Side == other
                && group.RequestedFormation == otherFormation
            );
            _report.localTargetObserved =
                _report.localTargetObserved || HasEnemyTargets(session, session.PlayerSide);
            _report.remoteTargetObserved =
                _report.remoteTargetObserved || HasEnemyTargets(session, other);
        }

        /// <summary>
        /// Compares independently captured terminal evidence before either process performs connection cleanup.
        /// </summary>
        private void CompareOutcome()
        {
            EosTacticalIntegrationReport peer = ReadPeerReport();
            if (peer?.outcomeReady != true)
                return;
            Require(
                peer.authenticated
                    && peer.completionAgreed
                    && peer.dayBarrierVerified
                    && peer.humanControllersVerified
                    && peer.authoritativeStateObserved
                    && peer.manualControlVerified
                    && peer.localTargetObserved
                    && peer.remoteTargetObserved
                    && peer.pauseCheckComplete
                    && peer.sharedResumeObserved,
                "The other real participant did not reach final protocol agreement."
            );
            Require(
                peer.campaignFingerprint == _report.campaignFingerprint,
                "The final independently captured campaign fingerprints differ."
            );
            Require(
                peer.hostUserID == _report.hostUserID && peer.guestUserID == _report.guestUserID,
                "The authenticated participant identities differ."
            );
            Require(
                peer.shots == _report.shots
                    && peer.impacts == _report.impacts
                    && peer.destructions == _report.destructions
                    && peer.withdrawals == _report.withdrawals
                    && peer.recoveries == _report.recoveries
                    && peer.orderedEffectDigest == _report.orderedEffectDigest,
                "Reliable battle events must match in count, order, identity, and lossless payload."
            );
            Require(
                peer.results == 1
                    && peer.coordinatorResults == 1
                    && peer.completedTicks == 1
                    && peer.combatResumptions == 1,
                "The counterpart did not complete one exact campaign result."
            );
            if (_options.GuestWithdrawalAtSystemTime > 0)
            {
                EosTacticalIntegrationReport guest = _options.Role == "guest" ? _report : peer;
                Require(
                    _report.guestWithdrawalObserved
                        && peer.guestWithdrawalObserved
                        && guest.guestWithdrawalSubmitted
                        && guest.withdrawalCommandsSubmitted == 1,
                    "The requested guest retreat must submit one authenticated command and be observed by both peers."
                );
            }
            string remote = _options.Role == "host" ? _report.guestUserID : _report.hostUserID;
            Require(
                _route?.Capture().Any(observation => observation.RemoteUserID == remote) == true,
                "No real SDK connection route was observed for the admitted counterpart."
            );
            if (_options.ForceRelay)
                Require(
                    _report.relayPolicyVerified
                        && peer.relayPolicyVerified
                        && _route
                            .Capture()
                            .Where(observation => observation.RemoteUserID == remote)
                            .All(observation =>
                                observation.NetworkType == NetworkConnectionType.RelayedConnection
                            ),
                    "The forced-relay run must observe only relayed routes for its admitted counterpart."
                );
            SetPhase("cleanup");
            Cleanup();
            _state = "cleanup";
            _operation = EosTacticalLobbyCleanup.WaitForDisappearanceAsync(
                _report.localUserID,
                _report.lobbyID,
                TimeSpan.FromSeconds(30)
            );
        }

        /// <summary>
        /// Counts a local captured intent without treating submission as acceptance.
        /// </summary>
        /// <param name="command">The semantic command sent through the actual tactical driver.</param>
        private void HandleCommand(TacticalCombatNetworkCommand command)
        {
            _report.commandsSubmitted++;
            if (command.Kind == TacticalCombatNetworkCommandKind.Formation)
                _report.formationCommandsSubmitted++;
            if (command.Kind == TacticalCombatNetworkCommandKind.Withdrawal)
                _report.withdrawalCommandsSubmitted++;
        }

        /// <summary>
        /// Counts actual simulation or reliably projected discharge effects by unit kind.
        /// </summary>
        /// <param name="shot">The real discharged or projected shot.</param>
        private void HandleShot(TacticalCombatShot shot)
        {
            _evidence.RecordShot(shot);
            _report.shots++;
            if (shot.Source.IsFighter)
                _report.fighterShots++;
            else
                _report.capitalShots++;
        }

        /// <summary>
        /// Counts actual impact callbacks emitted by simulation or reliable projection.
        /// </summary>
        /// <param name="unit">The struck unit.</param>
        /// <param name="weapon">The discharged weapon type.</param>
        /// <param name="impact">The observed impact kind.</param>
        private void HandleImpact(
            TacticalCombatUnit unit,
            TacticalWeaponType weapon,
            TacticalCombatImpactKind impact
        )
        {
            _evidence.RecordImpact(unit, weapon, impact);
            _report.impacts++;
        }

        /// <summary>
        /// Counts actual destruction callbacks without changing unit state.
        /// </summary>
        /// <param name="unit">The naturally destroyed unit.</param>
        private void HandleDestroyed(TacticalCombatUnit unit)
        {
            _evidence.RecordUnit(TacticalCombatNetworkEffectKind.Destroyed, unit);
            _report.destructions++;
        }

        /// <summary>
        /// Records the reliable withdrawal identity and its position in the common event stream.
        /// </summary>
        /// <param name="unit">The naturally departing participant.</param>
        private void HandleWithdrawn(TacticalCombatUnit unit)
        {
            _evidence.RecordUnit(TacticalCombatNetworkEffectKind.Withdrawn, unit);
            _report.withdrawals++;
        }

        /// <summary>
        /// Records the reliable fighter recovery identity and its position in the common event stream.
        /// </summary>
        /// <param name="unit">The recovered fighter squadron.</param>
        private void HandleRecovered(TacticalCombatUnit unit)
        {
            _evidence.RecordUnit(TacticalCombatNetworkEffectKind.Recovered, unit);
            _report.recoveries++;
        }

        /// <summary>
        /// Counts the coordinator's already committed campaign-result notification.
        /// </summary>
        /// <param name="result">The real local campaign result.</param>
        private void HandleCoordinatorResult(SpaceCombatResult result) =>
            _report.coordinatorResults++;

        /// <summary>
        /// Counts actual settled battle results on the stable pipeline across initial snapshot replacement.
        /// </summary>
        /// <param name="results">The completed gameplay reaction wave published by the current result bus.</param>
        private void HandleResolvedResults(IReadOnlyList<GameResult> results) =>
            _report.results += results.Count(result => result is SpaceCombatResult);

        /// <summary>
        /// Counts the normal completed campaign day.
        /// </summary>
        private void HandleTickCompleted() => _report.completedTicks++;

        /// <summary>
        /// Counts the normal deferred combat-resumption boundary.
        /// </summary>
        private void HandleCombatResumed() => _report.combatResumptions++;

        /// <summary>
        /// Records a fixed test assertion before stopping an invalid real-service run.
        /// </summary>
        /// <param name="condition">The verified runtime condition.</param>
        /// <param name="assertion">A fixed diagnostic without any authentication material.</param>
        private void Require(bool condition, string assertion)
        {
            if (condition)
                return;
            _report.assertion = assertion;
            throw new InvalidOperationException(assertion);
        }

        /// <summary>
        /// Changes a public milestone and immediately writes its credential-free evidence.
        /// </summary>
        /// <param name="phase">The bounded integration stage name.</param>
        private void SetPhase(string phase)
        {
            _report.phase = phase;
            Publish();
        }

        /// <summary>
        /// Atomically writes one role's evolving report without serializing exception messages or credentials.
        /// </summary>
        /// <returns>Whether the latest evidence was committed without a transient sharing conflict.</returns>
        private bool Publish()
        {
            _report.elapsedSeconds = _elapsed.Elapsed.TotalSeconds;
            _report.updatedUTC = DateTimeOffset.UtcNow.ToString("O");
            if (_route != null)
                _report.routes = _route.Capture();
            string path = Path.Combine(_options.ReportDirectory, _options.Role + ".json");
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(_report, true));
            try
            {
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            catch (IOException exception) when (exception.HResult == unchecked((int)0x80070020))
            {
                // A report consumer can briefly prevent replacement; the next Update retries.
                return false;
            }
            _pauseReadyPublished |= _report.pauseCheckReady;
            _pauseCheckPublished |= _report.pauseCheckComplete;
            _targetsPublished |=
                _report.localOrdersObserved
                && _report.remoteOrdersObserved
                && _report.localTargetObserved
                && _report.remoteTargetObserved;
            RecordRecoveryPublication();
            return true;
        }

        /// <summary>
        /// Reads only the peer's public report for run coordination and independent final comparison.
        /// </summary>
        /// <returns>The matching peer evidence, or null before that process publishes it.</returns>
        private EosTacticalIntegrationReport ReadPeerReport()
        {
            string peer = _options.Role == "host" ? "guest" : "host";
            string path = Path.Combine(_options.ReportDirectory, peer + ".json");
            string json;
            try
            {
                using FileStream stream = new(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete
                );
                using StreamReader reader = new(stream);
                json = reader.ReadToEnd();
            }
            catch (FileNotFoundException)
            {
                // A peer replacing its report can briefly remove the path before this open.
                return null;
            }
            catch (IOException exception) when (exception.HResult == unchecked((int)0x80070020))
            {
                // Windows Mono can briefly lock the report path while its peer replaces it.
                return null;
            }
            EosTacticalIntegrationReport report =
                JsonUtility.FromJson<EosTacticalIntegrationReport>(json);
            Require(
                report?.runID == _options.RunID && report.role == peer,
                "The peer report belongs to a different integration run."
            );
            Require(
                string.IsNullOrEmpty(report.errorType),
                "The other real participant reported an integration failure."
            );
            _peerReport = report;
            if (
                _options.Role == "host"
                && _options.GuestWithdrawalAtSystemTime > 0
                && report.guestWithdrawalSubmitted
                && report.withdrawalCommandsSubmitted == 1
            )
            {
                _report.guestWithdrawalSubmitted = true;
                _report.guestWithdrawalSubmittedAtSystemTime =
                    report.guestWithdrawalSubmittedAtSystemTime;
            }
            return report;
        }

        /// <summary>
        /// Creates an exact run-specific public lobby name.
        /// </summary>
        /// <returns>The unique room name searched by both participants.</returns>
        private string RoomName() => "EOS tactical " + _options.RunID;

        /// <summary>
        /// Reports failure without logging signed tokens, client configuration, or exception messages.
        /// </summary>
        /// <param name="exception">The failed integration boundary.</param>
        private void Fail(Exception exception)
        {
            if (_finished || _state == "failure-cleanup")
                return;
            bool cleanupAlreadyFailed = _state == "cleanup" && _operation?.IsCompleted == true;
            _report.errorType = exception.GetType().Name;
            _report.errorStack = exception.StackTrace;
            _report.phase = "failure";
            _report.passed = false;
            _report.cleanupComplete = false;
            TryPublishFailure();
            try
            {
                Cleanup();
            }
            catch (Exception cleanup)
            {
                _report.cleanupErrorType = cleanup.GetType().Name;
            }
            if (cleanupAlreadyFailed)
            {
                _report.cleanupErrorType ??= exception.GetType().Name;
                _finished = true;
                TryPublishFailure();
                Application.Quit(2);
                return;
            }
            _state = "failure-cleanup";
            _operation =
                _report.authenticated && !string.IsNullOrEmpty(_report.lobbyID)
                    ? EosTacticalLobbyCleanup.WaitForDisappearanceAsync(
                        _report.localUserID,
                        _report.lobbyID,
                        TimeSpan.FromSeconds(30)
                    )
                    : Task.CompletedTask;
            CompleteFailureCleanup();
        }

        /// <summary>
        /// Finishes a failed run only after bounded native cleanup verification preserves its original failure evidence.
        /// </summary>
        private void CompleteFailureCleanup()
        {
            if (_operation?.IsCompleted != true)
                return;
            _report.cleanupComplete =
                _operation.Status == TaskStatus.RanToCompletion
                && string.IsNullOrEmpty(_report.cleanupErrorType);
            if (_operation.Status != TaskStatus.RanToCompletion)
                _report.cleanupErrorType =
                    _operation.Exception?.GetBaseException().GetType().Name
                    ?? "TaskCanceledException";
            _finished = true;
            TryPublishFailure();
            Application.Quit(2);
        }

        /// <summary>
        /// Attempts to preserve failure evidence even when the report directory itself becomes unavailable.
        /// </summary>
        private void TryPublishFailure()
        {
            try
            {
                Publish();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Releases test subscriptions and the real shared connection through its existing ownership boundary.
        /// </summary>
        private void Cleanup()
        {
            _route?.Dispose();
            _route = null;
            DetachRuntimeObservers();
            DetachCombatObservers();
            _coordinator?.Disconnect();
            (_tick as IDisposable)?.Dispose();
            _tick = null;
        }

        /// <summary>
        /// Supplies the real arrival timing with a headless presentation endpoint.
        /// </summary>
        private sealed class Arrival : ITacticalCombatArrivalPresentation
        {
            /// <summary>
            /// Receives the arrival pause strobe without drawing UI.
            /// </summary>
            /// <param name="active">Whether the strobe is active.</param>
            public void SetArrivalActive(bool active) { }

            /// <summary>
            /// Receives a unit presentation reset without drawing UI.
            /// </summary>
            /// <param name="unitIndex">The unit's projection index.</param>
            public void Reset(int unitIndex) { }

            /// <summary>
            /// Receives the normal local arrival transform without rendering.
            /// </summary>
            /// <param name="unitIndex">The arriving unit's projection index.</param>
            /// <param name="position">The normal arrival position.</param>
            /// <param name="direction">The normal arrival direction.</param>
            public void SetTransform(
                int unitIndex,
                System.Numerics.Vector4 position,
                System.Numerics.Vector4 direction
            ) { }

            /// <summary>
            /// Receives a unit refresh without drawing UI.
            /// </summary>
            /// <param name="unitIndex">The refreshed unit's projection index.</param>
            public void Refresh(int unitIndex) { }
        }
    }
}
