using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Messages;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Generation;
using Rebellion.Multiplayer;
using Rebellion.PlayerRequests;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class GameSessionManagerTests
    {
        private ContentPack _contentPack;
        private UserGameplaySettings _gameplaySettings;
        private GameSessionManager _sessionManager;
        private SaveGameManager _saveGameManager;
        private string _saveDirectoryPath;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            ContentPack source = TestContent.Pack;
            _contentPack = new ContentPack(
                source.ContentRootPath,
                source.PackRootPath,
                source.FileResolver,
                source.Definition,
                source.Scenario,
                source.Factions,
                TestGameData.WithGameEvents(source.GameData, Array.Empty<GameEvent>()),
                new Dictionary<string, ContentPreloadManifest>()
            );
            _saveDirectoryPath = Path.Combine(
                Path.GetTempPath(),
                nameof(GameSessionManagerTests),
                Guid.NewGuid().ToString("N")
            );
            _gameplaySettings = new UserGameplaySettings();
            _saveGameManager = new SaveGameManager(_saveDirectoryPath);
            _sessionManager = new GameSessionManager(
                _contentPack,
                _saveGameManager,
                () => _gameplaySettings
            );
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _sessionManager?.EndGame();
            if (Directory.Exists(_saveDirectoryPath))
                Directory.Delete(_saveDirectoryPath, true);
        }

        [Test]
        public void StartGame_AnalyticsEnabled_RecordsRuntimeLifecycle()
        {
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            _sessionManager = new GameSessionManager(
                _contentPack,
                _saveGameManager,
                () => _gameplaySettings,
                sink
            );
            GameRoot game = CreateGame();

            _sessionManager.StartGame(game);
            _sessionManager.EndGame();

            CollectionAssert.AreEqual(new[] { "campaign_started" }, sink.EventNames);
            Assert.IsNotEmpty(game.Metadata.CampaignID);
        }

        [Test]
        public void StartLoadedGame_AnalyticsEnabled_InitializesCampaignIdentityBeforeTicks()
        {
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            _sessionManager = new GameSessionManager(
                _contentPack,
                _saveGameManager,
                () => _gameplaySettings,
                sink
            );
            GameRoot game = CreateGame();
            game.Metadata.CampaignID = null;

            _sessionManager.StartLoadedGame(game);

            Assert.IsNotEmpty(game.Metadata.CampaignID);
            Assert.IsEmpty(sink.EventNames);
        }

        [Test]
        public void StartGame_VictoryPublished_RecordsFinishedCampaign()
        {
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            _sessionManager = new GameSessionManager(
                _contentPack,
                _saveGameManager,
                () => _gameplaySettings,
                sink
            );
            GameRoot game = CreateGame();
            Faction winner = new Faction
            {
                InstanceID = _contentPack.Scenario.PlayableFactionIDs[0],
            };
            game.GetFactions().Add(winner);
            GameSession session = _sessionManager.StartGame(game);

            session.Pipeline.ProcessResults(
                new[]
                {
                    new VictoryResult { Winner = winner, Tick = game.CurrentTick },
                },
                processMessages: false
            );

            CollectionAssert.AreEqual(
                new[] { "campaign_started", "campaign_finished" },
                sink.EventNames
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecordMultiplayerCampaignStarted_ReplacingSameCampaign_RecordsOneStart(
            bool synchronizedBeforeReplacement
        )
        {
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            _sessionManager = new GameSessionManager(
                _contentPack,
                _saveGameManager,
                () => _gameplaySettings,
                sink
            );
            GameRoot initial = CreateGame();
            ConfigureMultiplayerPresentation(initial, "campaign");
            initial.LocalPlayerFactionID = "FNALL1";
            _sessionManager.StartGame(initial, new TestSessionOwner());
            if (synchronizedBeforeReplacement)
                _sessionManager.RecordMultiplayerCampaignStarted();
            GameRoot synchronized = CreateGame();
            ConfigureMultiplayerPresentation(synchronized, "campaign");
            synchronized.LocalPlayerFactionID = "FNALL1";

            _sessionManager.ReplaceGame(synchronized);
            _sessionManager.RecordMultiplayerCampaignStarted();
            _sessionManager.RecordMultiplayerCampaignStarted();

            CollectionAssert.AreEqual(new[] { "campaign_started" }, sink.EventNames);
        }

        [Test]
        public void RecordMultiplayerCampaignStarted_ReplacingDifferentCampaign_DoesNotCountCreation()
        {
            RecordingAnalyticsSink sink = new RecordingAnalyticsSink();
            _sessionManager = new GameSessionManager(
                _contentPack,
                _saveGameManager,
                () => _gameplaySettings,
                sink
            );
            GameRoot initial = CreateGame();
            ConfigureMultiplayerPresentation(initial, "initial");
            initial.LocalPlayerFactionID = "FNALL1";
            _sessionManager.StartGame(initial, new TestSessionOwner());
            GameRoot loaded = CreateGame();
            ConfigureMultiplayerPresentation(loaded, "loaded");
            loaded.LocalPlayerFactionID = "FNALL1";

            _sessionManager.ReplaceGame(loaded);
            _sessionManager.RecordMultiplayerCampaignStarted();

            Assert.IsEmpty(sink.EventNames);
        }

        [Test]
        public void StartGame_DeclaredOwner_BindsBeforePresentationReadiness()
        {
            GameRoot game = CreateGame();
            ConfigureMultiplayerPresentation(game, Guid.NewGuid().ToString("N"));
            TestSessionOwner owner = new();

            GameSession session = _sessionManager.StartGame(game, owner);

            CollectionAssert.AreEqual(new[] { "prepare", "bind" }, owner.Calls);
            Assert.IsTrue(_sessionManager.HasCoordinatedSession);
            Assert.IsTrue(_sessionManager.NotifyPresentationReady(session));
            CollectionAssert.AreEqual(new[] { "prepare", "bind", "ready" }, owner.Calls);
        }

        [Test]
        public void NotifyPresentationReady_ReplacedSession_DoesNotNotifyNewOwner()
        {
            GameSession outgoing = _sessionManager.StartGame(CreateGame());
            TestSessionOwner owner = new();
            _sessionManager.StartGame(CreateGame(), owner);

            Assert.IsFalse(_sessionManager.NotifyPresentationReady(outgoing));
            CollectionAssert.AreEqual(new[] { "prepare", "bind" }, owner.Calls);
        }

        [Test]
        public void ResumeMultiplayerOperations_VerifiedCoordinatedSession_ReopensPersistence()
        {
            _sessionManager.StartGame(CreateGame(), new TestSessionOwner());
            _sessionManager.BlockGameOperations();
            Assert.IsFalse(_sessionManager.CanSave);
            _sessionManager.ResumeMultiplayerOperations();
            Assert.IsTrue(_sessionManager.CanSave);
        }

        [Test]
        public void ResumeMultiplayerOperations_UncoordinatedSession_RejectsResume()
        {
            _sessionManager.StartGame(CreateGame());
            Assert.Throws<InvalidOperationException>(_sessionManager.ResumeMultiplayerOperations);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StartGame_MultiplayerCampaignWithoutOwner_RejectsBeforeSessionCreation(
            bool loaded
        )
        {
            GameRoot game = CreateGame();
            ConfigureMultiplayerPresentation(game, Guid.NewGuid().ToString("N"));

            Assert.Throws<InvalidOperationException>(() =>
            {
                if (loaded)
                    _sessionManager.StartLoadedGame(game);
                else
                    _sessionManager.StartGame(game);
            });
            Assert.IsFalse(_sessionManager.HasActiveGame);
        }

        [Test]
        public void StartGame_QueuedOutgoingRequest_CancelsBeforeReplacementOwnerStarts()
        {
            GameSession session = _sessionManager.StartGame(CreateGame());
            Task<int> pending = null;
            session.Game.SetGameSpeed(TickSpeed.Fast);
            session.Tick.TickCompleted += () =>
                pending = session.ExecuteCommandAsync(() => ++session.Game.CurrentTick);
            session.Tick.ProcessTick();
            int completedTick = session.Game.CurrentTick;
            Assert.IsNotNull(pending);
            Assert.IsFalse(pending.IsCompleted);
            _sessionManager.StartGame(CreateGame(), new TestSessionOwner());
            Assert.IsTrue(pending.IsCanceled);
            Assert.AreEqual(completedTick, session.Game.CurrentTick);
        }

        [Test]
        public async Task StartGame_DeclaredOwner_ReceivesRequestsBeforePresentationReadyAsync()
        {
            GameRoot game = CreateGame();
            ConfigureMultiplayerPresentation(game, Guid.NewGuid().ToString("N"));
            RecordingPlayerRequestDispatcher negotiatedOwner = TestPlayerRequests.RejectAll();
            _sessionManager.StartGame(game, new TestSessionOwner(negotiatedOwner));
            IPlayerRequestDispatcher capturedByWindow = _sessionManager.PlayerRequests;
            SetSpeedRequest action = new() { Speed = TickSpeed.Fast };

            await capturedByWindow.ExecuteAsync(action);

            Assert.That(negotiatedOwner.Requests, Is.EqualTo(new[] { action }));
        }

        [Test]
        public async Task StartGame_BeforePresentationReady_DoesNotExecuteLocallyAsync()
        {
            GameRoot game = CreateGame();
            ConfigureMultiplayerPresentation(game, Guid.NewGuid().ToString("N"));
            _sessionManager.StartGame(game, new TestSessionOwner());
            _sessionManager.GetActiveGameManager().SetGameSpeed(TickSpeed.Paused);
            IPlayerRequestDispatcher capturedByWindow = _sessionManager.PlayerRequests;

            await capturedByWindow.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Fast });

            Assert.That(game.GetGameSpeed(), Is.EqualTo(TickSpeed.Paused));
        }

        [Test]
        public async Task EndGame_PreviouslyCapturedDispatcher_RejectsWithoutExecutingAsync()
        {
            GameRoot game = CreateGame();
            ConfigureMultiplayerPresentation(game, Guid.NewGuid().ToString("N"));
            RecordingPlayerRequestDispatcher owner = TestPlayerRequests.RejectAll();
            _sessionManager.StartGame(game, new TestSessionOwner(owner));
            IPlayerRequestDispatcher capturedByWindow = _sessionManager.PlayerRequests;
            _sessionManager.EndGame();

            PlayerRequestOutcome result = await capturedByWindow.ExecuteAsync(
                new SetSpeedRequest { Speed = TickSpeed.Fast }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(owner.Requests);
        }

        [Test]
        public async Task PlayerRequests_AcceptedAdvisorOption_PublishesCompletedStateBeforeReturningAsync()
        {
            GameRoot game = CreateGame();
            Faction faction = new() { InstanceID = "FNALL1", TranslateCounterpart = false };
            game.GetFactions().Add(faction);
            game.Summary.PlayerFactionID = faction.InstanceID;
            game.SetFactionController(faction.InstanceID, "player", PlayerControllerType.Human);
            _sessionManager.StartGame(game);
            int changes = 0;
            bool observedSetting = false;
            _sessionManager.GameStateChanged += () =>
            {
                changes++;
                observedSetting = faction.TranslateCounterpart;
            };

            PlayerRequestOutcome outcome = await _sessionManager.PlayerRequests.ExecuteAsync(
                new ToggleAdvisorOptionRequest { Option = PlayerAdvisorOption.TranslateCounterpart }
            );

            Assert.IsTrue(outcome.Accepted);
            Assert.AreEqual(1, changes);
            Assert.IsTrue(observedSetting);
        }

        [Test]
        public async Task PlayerRequests_RejectedAdvisorOption_DoesNotPublishStateChangeAsync()
        {
            GameRoot game = CreateGame();
            Faction faction = new() { InstanceID = "FNALL1" };
            game.GetFactions().Add(faction);
            game.Summary.PlayerFactionID = faction.InstanceID;
            game.SetFactionController(faction.InstanceID, "player", PlayerControllerType.Human);
            _sessionManager.StartGame(game);
            int changes = 0;
            _sessionManager.GameStateChanged += () => changes++;

            PlayerRequestOutcome outcome = await _sessionManager.PlayerRequests.ExecuteAsync(
                new ToggleAdvisorOptionRequest { Option = (PlayerAdvisorOption)int.MaxValue }
            );

            Assert.IsFalse(outcome.Accepted);
            Assert.Zero(changes);
        }

        [Test]
        public async Task StartGame_ReplacedSessionDispatcher_RejectsWithoutReachingNewOwnerAsync()
        {
            GameRoot first = CreateGame();
            ConfigureMultiplayerPresentation(first, Guid.NewGuid().ToString("N"));
            _sessionManager.StartGame(first, new TestSessionOwner());
            IPlayerRequestDispatcher capturedByOldWindow = _sessionManager.PlayerRequests;
            GameRoot replacement = CreateGame();
            ConfigureMultiplayerPresentation(replacement, Guid.NewGuid().ToString("N"));
            RecordingPlayerRequestDispatcher newOwner = TestPlayerRequests.RejectAll();
            _sessionManager.StartGame(replacement, new TestSessionOwner(newOwner));

            PlayerRequestOutcome result = await capturedByOldWindow.ExecuteAsync(
                new SetSpeedRequest { Speed = TickSpeed.Fast }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(newOwner.Requests);
        }

        [Test]
        public void StartGame_InvalidContent_EndsPreviousSession()
        {
            _sessionManager.StartGame(CreateGame());
            GameRoot replacement = CreateGame();
            replacement.Summary.PackID = "different-pack";

            Assert.Throws<InvalidOperationException>(() => _sessionManager.StartGame(replacement));

            Assert.IsFalse(_sessionManager.HasActiveGame);
        }

        [Test]
        public void LoadGameAsync_InvalidContent_KeepsActiveManager()
        {
            GameSession session = _sessionManager.StartGame(CreateGame());
            GameManager manager = _sessionManager.GetActiveGameManager();
            GameRoot replacement = CreateGame();
            replacement.Summary.PackID = "different-pack";
            _saveGameManager.SaveGameData(replacement, "invalid-content", "Invalid Content");

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sessionManager.LoadGameAsync("invalid-content")
            );

            Assert.AreSame(manager, _sessionManager.GetActiveGameManager());
        }

        [Test]
        public async Task LoadGameAsync_ValidSave_KeepsManagerIdentityAsync()
        {
            GameSession session = _sessionManager.StartGame(CreateGame());
            GameManager manager = _sessionManager.GetActiveGameManager();
            _saveGameManager.SaveGameData(CreateGame(), "replacement", "Replacement");

            Assert.IsTrue(await _sessionManager.LoadGameAsync("replacement"));

            Assert.AreSame(manager, _sessionManager.GetActiveGameManager());
        }

        /// <summary>
        /// Verifies that missing ship suppression values use content while explicit values survive loading.
        /// </summary>
        /// <param name="savedDivisor">The saved value, or null for a legacy unit without this property.</param>
        /// <param name="expected">The restored ship divisor.</param>
        [TestCase(null, 2)]
        [TestCase(0, 0)]
        [TestCase(3, 3)]
        public void StartLoadedGame_SectorSuppression_RestoresOnlyMissingValues(
            int? savedDivisor,
            int expected
        )
        {
            GameRoot game = CreateGame();
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet" };
            Fleet fleet = new Fleet { InstanceID = "fleet" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "retained-ship",
                TypeID = "CSEM015",
                CanDestroyPlanets = true,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            if (savedDivisor.HasValue)
                ship.SectorSuppression = savedDivisor.Value;
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            _sessionManager.StartLoadedGame(game);

            Assert.AreEqual(expected, ship.SectorSuppression);
        }

        [Test]
        public void StartLoadedGame_LegacyFactionSettings_RestoresActiveContentSettings()
        {
            GameRoot legacyGame = CreateGameWithLegacyFactionSettings();

            GameSession session = _sessionManager.StartLoadedGame(legacyGame);

            AssertFactionSettingsMatchActiveContent(session.Game);
        }

        [Test]
        public void StartGame_FactionSettings_RestoresActiveContentSettings()
        {
            GameRoot game = CreateGameWithLegacyFactionSettings();

            GameSession session = _sessionManager.StartGame(game);

            AssertFactionSettingsMatchActiveContent(session.Game);
        }

        [Test]
        public void StartGame_FactionModifierOverride_RestoresConfiguredSettings()
        {
            GameRoot game = CreateGameWithLegacyFactionSettings();
            Faction template = _contentPack.GameData.Factions[0];
            FactionSettings configured = template.Settings.CreateCopy();
            configured.GarrisonEfficiency++;
            GameGenerationConfig config = _contentPack.GameData.GenerationConfig.CreateCopy();
            config.FactionModifiers.Add(
                new FactionModifierConfig { FactionID = template.InstanceID, Settings = configured }
            );
            game.Summary.CustomSettings = new CustomGameSettings { GenerationConfig = config };

            GameSession session = _sessionManager.StartGame(game);

            Faction loaded = session
                .Game.GetFactions()
                .Single(faction => faction.InstanceID == template.InstanceID);
            Assert.AreEqual(configured.GarrisonEfficiency, loaded.Settings.GarrisonEfficiency);
            Assert.AreNotSame(configured, loaded.Settings);
        }

        [Test]
        public void StartLoadedGame_CustomGarrisonRules_RestoresRuntimeConfiguration()
        {
            int authoredThreshold = _contentPack.GameData.GameConfig.AI.Garrison.SupportThreshold;
            GameRoot game = CreateGame();
            game.Summary.CustomSettings = new CustomGameSettings
            {
                GarrisonRules = new GarrisonRuleSettings
                {
                    SupportThreshold = 70,
                    SupportDeficitPerRegiment = 5,
                    UprisingMultiplier = 3,
                },
            };

            GameSession session = _sessionManager.StartLoadedGame(game);

            Assert.AreEqual(70, session.Game.Config.AI.Garrison.SupportThreshold);
            Assert.AreEqual(5, session.Game.Config.AI.Garrison.GarrisonDivisor);
            Assert.AreEqual(3, session.Game.Config.AI.Garrison.UprisingMultiplier);
            Assert.AreEqual(
                authoredThreshold,
                _contentPack.GameData.GameConfig.AI.Garrison.SupportThreshold
            );
        }

        [Test]
        public void StartLoadedGame_CustomMovementRules_RestoresRuntimeConfiguration()
        {
            double authoredDivisor = _contentPack.GameData.GameConfig.Movement.DistanceDivisor;
            GameRoot game = CreateGame();
            game.Summary.CustomSettings = new CustomGameSettings
            {
                MovementRules = new MovementRuleSettings { TravelSpeedPercent = 150 },
            };

            GameSession session = _sessionManager.StartLoadedGame(game);

            Assert.AreEqual(
                authoredDivisor * 1.5d,
                session.Game.Config.Movement.DistanceDivisor,
                0.0001d
            );
            Assert.AreEqual(
                authoredDivisor,
                _contentPack.GameData.GameConfig.Movement.DistanceDivisor
            );
        }

        [Test]
        public void StartLoadedGame_SerializedGalaxyLayout_RestoresActiveContentCoordinates()
        {
            GameRoot game = CreateGameWithStaleGalaxyLayout();

            GameSession session = _sessionManager.StartLoadedGame(game);

            AssertGalaxyLayoutMatchesActiveContent(session.Game);
        }

        [Test]
        public async Task LoadGameAsync_LegacyFactionSettings_RestoresActiveContentSettingsAsync()
        {
            _sessionManager.StartGame(CreateGame());
            GameRoot legacyGame = CreateGameWithLegacyFactionSettings();
            _saveGameManager.SaveGameData(legacyGame, "legacy-faction-settings", "Legacy");

            bool loaded = await _sessionManager.LoadGameAsync("legacy-faction-settings");

            Assert.IsTrue(loaded);
            AssertFactionSettingsMatchActiveContent(_sessionManager.GetActiveGame());
        }

        [Test]
        public async Task LoadGameAsync_SerializedGalaxyLayout_RestoresActiveContentCoordinatesAsync()
        {
            _sessionManager.StartGame(CreateGame());
            GameRoot game = CreateGameWithStaleGalaxyLayout();
            _saveGameManager.SaveGameData(game, "stale-galaxy-layout", "Stale Galaxy Layout");

            bool loaded = await _sessionManager.LoadGameAsync("stale-galaxy-layout");

            Assert.IsTrue(loaded);
            AssertGalaxyLayoutMatchesActiveContent(_sessionManager.GetActiveGame());
        }

        [Test]
        public void StartLoadedGame_LegacyFactionSettings_BombardmentDeliversCombatReport()
        {
            GameRoot legacyGame = CreateGameWithLegacyFactionSettings();
            Faction alliance = legacyGame
                .GetFactions()
                .Single(faction => faction.InstanceID == "FNALL1");
            Faction empire = legacyGame
                .GetFactions()
                .Single(faction => faction.InstanceID == "FNEMP1");
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                DisplayName = "Planet",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
            };
            legacyGame.AttachNode(sector, legacyGame.GetGalaxyMap());
            legacyGame.AttachNode(planet, sector);
            GameSession session = _sessionManager.StartLoadedGame(legacyGame);
            BombardmentResult result = new BombardmentResult
            {
                Planet = planet,
                AttackingFaction = alliance,
                AttackerOwnerInstanceID = alliance.InstanceID,
                DefenderOwnerInstanceID = empire.InstanceID,
                DestroyedRegiments = { new Regiment { UprisingDefense = 80 } },
            };

            Assert.DoesNotThrow(() => session.Pipeline.ProcessResults(new[] { result }));

            CombatReport report = empire
                .Messages[MessageType.Conflict]
                .OfType<CombatReport>()
                .Single(message => message.CombatType == CombatReportType.Bombardment);
            Assert.AreEqual(planet.InstanceID, report.PlanetInstanceID);
        }

        [Test]
        public void EndGame_SpeedObserverThrows_ReleasesActiveSession()
        {
            GameSession session = _sessionManager.StartGame(CreateGame());
            GameManager manager = _sessionManager.GetActiveGameManager();
            Action fail = () => throw new InvalidOperationException("Pause observer failed.");
            manager.GameSpeedChanged += fail;

            try
            {
                Assert.Throws<InvalidOperationException>(_sessionManager.EndGame);
                Assert.IsFalse(_sessionManager.HasActiveGame);
                Assert.IsNull(_sessionManager.GetActiveGameManager());
                Assert.IsNull(_sessionManager.PlayerRequests);
                Assert.IsFalse(session.CanExecuteCommands);
            }
            finally
            {
                manager.GameSpeedChanged -= fail;
            }
        }

        [Test]
        public void EndGame_SpeedObserverThrows_ReplacementSessionCanAutosave()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 39;
            _gameplaySettings.AutosaveIntervalTicks = 40;
            GameSession session = _sessionManager.StartGame(game);
            GameManager manager = _sessionManager.GetActiveGameManager();
            Action fail = () => throw new InvalidOperationException("Pause observer failed.");
            manager.GameSpeedChanged += fail;

            try
            {
                Assert.Throws<InvalidOperationException>(_sessionManager.EndGame);
            }
            finally
            {
                manager.GameSpeedChanged -= fail;
            }
            GameRoot replacement = CreateGame();
            replacement.CurrentTick = 39;
            _sessionManager.StartGame(replacement).Tick.ProcessTick();

            SaveGameEntry saved = _saveGameManager.GetSavedGames().Single();
            Assert.AreEqual(replacement.Metadata.CampaignID, saved.Metadata.CampaignID);
            Assert.AreNotEqual(game.Metadata.CampaignID, saved.Metadata.CampaignID);
        }

        [Test]
        public void StartGame_PendingCombat_DefersAutosaveUntilResolution()
        {
            GameRoot game = CreateContestedGame();
            game.CurrentTick = 39;
            _gameplaySettings.AutosaveIntervalTicks = 40;
            GameSession session = _sessionManager.StartGame(game);
            GameManager manager = _sessionManager.GetActiveGameManager();
            session.Tick.ProcessTick();

            Assert.IsEmpty(_saveGameManager.GetSavedGames());
            Assert.IsFalse(_sessionManager.CanSave);

            session.Tick.ResolveCombat(true);

            string autosavePath = _saveGameManager.GetSaveFilePath(
                SaveGameManager.AutosaveFilePrefix + game.Metadata.CampaignID + "_0000000040"
            );
            Assert.IsTrue(File.Exists(autosavePath));
            Assert.IsTrue(_sessionManager.CanSave);
        }

        [Test]
        public void StartLoadedGame_CoordinatedContestedWorld_DetectsBattleInAgreedTurn()
        {
            _sessionManager.StartLoadedGame(CreateContestedGame(), new TestSessionOwner());
            AssertCoordinatedBattleWaitsForTurn();
        }

        [Test]
        public void ReplaceGame_CoordinatedContestedWorld_DetectsBattleInAgreedTurn()
        {
            _sessionManager.StartGame(CreateGame(), new TestSessionOwner());
            _sessionManager.ReplaceGame(CreateContestedGame());
            AssertCoordinatedBattleWaitsForTurn();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReplaceGame_InterruptedBattle_RestoresCompletedWorldAndReopensSaving(
            bool incremental
        )
        {
            GameSession session = _sessionManager.StartGame(
                CreateContestedGame(),
                new TestSessionOwner()
            );
            MultiplayerSnapshot checkpoint = MultiplayerSnapshotCodec.Capture(session);
            int completedTick = session.Game.CurrentTick;
            int replacements = 0;
            _sessionManager.GameReplaced += _ => replacements++;
            _sessionManager.BlockGameOperations();
            if (incremental)
            {
                System.Collections.IEnumerator tick = session.Tick.ProcessTickIncrementally();
                Assert.That(tick.MoveNext(), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    MultiplayerSnapshotCodec.Capture(session)
                );
                while (tick.MoveNext()) { }
                (tick as IDisposable)?.Dispose();
            }
            else
                session.Tick.ProcessTick();
            Assert.That(session.Tick.IsSettled, Is.False);
            Assert.That(_sessionManager.CanSave, Is.False);

            _sessionManager.ReplaceGame(
                MultiplayerSnapshotCodec.Restore(checkpoint.Payload, checkpoint.Hash)
            );

            Assert.That(_sessionManager.GetActiveGameSession(), Is.SameAs(session));
            Assert.That(session.Game.CurrentTick, Is.EqualTo(completedTick));
            Assert.That(session.Tick.IsSettled, Is.True);
            Assert.That(session.GetService<SpaceCombatCommands>().HasPendingDecision, Is.False);
            Assert.That(_sessionManager.CanSave, Is.True);
            Assert.That(replacements, Is.EqualTo(1));
            session.Tick.ProcessTick();
            Assert.That(
                session
                    .GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending),
                Is.True
            );
            Assert.That(pending.AttackerHasResponded || pending.DefenderHasResponded, Is.False);
            Assert.That(_sessionManager.CanSave, Is.False);
            session.Tick.ResolveCombat(autoResolve: true);
            Assert.That(_sessionManager.CanSave, Is.True);
            Assert.That(session.Game.CurrentTick, Is.EqualTo(completedTick + 1));
        }

        [Test]
        public async Task QuickSaveAsync_PendingCombat_DoesNotWriteSaveAsync()
        {
            GameRoot game = CreateContestedGame();
            _sessionManager.StartLoadedGame(game);

            bool saved = await _sessionManager.QuickSaveAsync();

            Assert.IsFalse(saved);
            Assert.IsFalse(
                File.Exists(_saveGameManager.GetSaveFilePath(SaveGameManager.QuickSaveFileName))
            );
        }

        [Test]
        public async Task SaveGameAsync_PendingCombat_DoesNotWriteSaveAsync()
        {
            GameRoot game = CreateContestedGame();
            game.CurrentTick = 40;
            GameSession session = _sessionManager.StartLoadedGame(game);
            GameManager manager = _sessionManager.GetActiveGameManager();

            bool saved = await _sessionManager.SaveGameAsync("pending_combat", "Pending Combat");

            Assert.IsTrue(session.GetService<SpaceCombatCommands>().HasPendingDecision);
            Assert.IsFalse(_sessionManager.CanSave);
            Assert.IsFalse(saved);
            Assert.IsFalse(File.Exists(_saveGameManager.GetSaveFilePath("pending_combat")));
            Assert.AreEqual(40, game.CurrentTick);
        }

        [Test]
        public async Task QuickLoadAsync_AfterQuickSave_ReplacesMutatedGameWithSavedStateAsync()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 123;
            GameSession session = _sessionManager.StartGame(game);
            GameManager manager = _sessionManager.GetActiveGameManager();
            GameRoot replacement = null;
            _sessionManager.GameReplaced += loadedGame => replacement = loadedGame;

            Assert.IsTrue(await _sessionManager.QuickSaveAsync());
            game.CurrentTick = 999;
            Assert.IsTrue(await _sessionManager.QuickLoadAsync());

            Assert.IsNotNull(replacement);
            Assert.AreNotSame(game, replacement);
            Assert.AreSame(replacement, _sessionManager.GetActiveGame());
            Assert.AreEqual(123, replacement.CurrentTick);
        }

        [Test]
        public void LoadGameAsync_InvalidEvent_DoesNotNotifyPresentation()
        {
            _sessionManager.StartGame(CreateGame());
            GameRoot replacement = CreateGame();
            replacement
                .GetEventPool()
                .Add(new GameEvent { InstanceID = "INVALID", MaximumActivations = 0 });
            _saveGameManager.SaveGameData(replacement, "invalid-event", "Invalid Event");
            int announcements = 0;
            _sessionManager.GameReplaced += _ => announcements++;

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sessionManager.LoadGameAsync("invalid-event")
            );

            Assert.AreEqual(0, announcements);
        }

        [Test]
        public async Task LoadGameAsync_ValidSave_KeepsSessionIdentityAsync()
        {
            GameSession session = _sessionManager.StartGame(CreateGame());
            _saveGameManager.SaveGameData(CreateGame(), "replacement", "Replacement");

            Assert.IsTrue(await _sessionManager.LoadGameAsync("replacement"));

            Assert.AreSame(session, _sessionManager.GetActiveGameSession());
        }

        [Test]
        public async Task LoadGameAsync_ValidSave_ResetsClockBeforeNotificationAsync()
        {
            _sessionManager.StartGame(CreateGame());
            GameManager clock = _sessionManager.GetActiveGameManager();
            clock.SetGameSpeed(TickSpeed.Fast);
            float interval = _sessionManager
                .GetActiveGame()
                .Config.GameSpeed.FastTickIntervalSeconds;
            clock.TryAdvanceTickTimer(interval / 2f);
            GameRoot replacement = CreateGame();
            replacement.SetGameSpeed(TickSpeed.Fast);
            _saveGameManager.SaveGameData(replacement, "replacement", "Replacement");
            bool? ready = null;
            _sessionManager.GameReplaced += _ => ready = clock.TryAdvanceTickTimer(interval / 2f);

            Assert.IsTrue(await _sessionManager.LoadGameAsync("replacement"));

            Assert.IsFalse(ready);
        }

        [Test]
        public void LoadGameAsync_PresentationThrows_LeavesReconciliationPending()
        {
            GameSession session = _sessionManager.StartGame(CreateGame());
            _saveGameManager.SaveGameData(CreateContestedGame(), "contested", "Contested");
            _sessionManager.GameReplaced += _ =>
                throw new InvalidOperationException("Presentation failed.");

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sessionManager.LoadGameAsync("contested")
            );

            Assert.IsFalse(session.GetService<SpaceCombatCommands>().HasPendingDecision);
        }

        [Test]
        public void CanRequestSave_CoordinatedOwnerWhileSimulationBusy_DoesNotRequireImmediateSnapshot()
        {
            GameSession session = _sessionManager.StartLoadedGame(
                CreateContestedGame(),
                new TestPersistenceCoordinator()
            );
            session.Tick.ProcessTick();

            Assert.IsFalse(_sessionManager.CanSave);
            Assert.IsTrue(_sessionManager.CanRequestSave);
        }

        [Test]
        public async Task SaveGameAsync_SessionOwnerDeclines_DoesNotFallBackToLocalSaveAsync()
        {
            _sessionManager.StartGame(CreateGame(), new TestSessionOwner());

            Assert.IsFalse(await _sessionManager.SaveGameAsync("checkpoint", "Checkpoint"));
            Assert.IsFalse(Directory.Exists(_saveDirectoryPath));
        }

        [Test]
        public async Task LoadGameAsync_SessionOwnerDeclines_DoesNotFallBackToLocalSaveAsync()
        {
            GameRoot original = CreateGame();
            original.CurrentTick = 14;
            _saveGameManager.SaveGameData(CreateGame(), "checkpoint", "Checkpoint");
            _sessionManager.StartGame(original, new TestSessionOwner());

            Assert.IsFalse(await _sessionManager.LoadGameAsync("checkpoint"));
            Assert.AreSame(original, _sessionManager.GetActiveGame());
            Assert.AreEqual(14, original.CurrentTick);
        }

        [Test]
        public async Task SaveGameAsync_CoordinatedOwner_WaitsWithoutIndependentWriteAsync()
        {
            GameRoot game = CreateGame();
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _sessionManager.StartGame(game, coordinator);

            Task<bool> saving = _sessionManager.SaveGameAsync(
                "shared-checkpoint",
                "Shared checkpoint"
            );

            Assert.IsTrue(_sessionManager.HasCoordinatedSession);
            Assert.IsFalse(saving.IsCompleted);
            Assert.AreEqual("shared-checkpoint", coordinator.SavedFileName);
            Assert.AreEqual("Shared checkpoint", coordinator.SavedDisplayName);
            Assert.IsFalse(Directory.Exists(_saveDirectoryPath));

            coordinator.SaveCompletion.SetResult(true);

            Assert.IsTrue(await saving);
            Assert.IsFalse(Directory.Exists(_saveDirectoryPath));
        }

        [Test]
        public async Task LoadGameAsync_CoordinatedOwner_UsesOwnerWithoutLocalFileLookupAsync()
        {
            GameRoot game = CreateGame();
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _sessionManager.StartGame(game, coordinator);

            Task<bool> loading = _sessionManager.LoadGameAsync("host-only-file");

            Assert.IsFalse(loading.IsCompleted);
            Assert.AreEqual("host-only-file", coordinator.LoadedFileName);
            Assert.AreSame(game, _sessionManager.GetActiveGame());
            coordinator.LoadCompletion.SetResult(false);

            Assert.IsFalse(await loading);
            Assert.AreSame(game, _sessionManager.GetActiveGame());
            Assert.IsFalse(Directory.Exists(_saveDirectoryPath));
        }

        [Test]
        public async Task QuickSaveAsync_CoordinatedOwner_UsesQuicksaveSlotAndAwaitsCompletionAsync()
        {
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _sessionManager.StartGame(CreateGame(), coordinator);

            Task<bool> saving = _sessionManager.QuickSaveAsync();
            Task<bool> loading = _sessionManager.QuickLoadAsync();

            Assert.AreEqual(SaveGameManager.QuickSaveFileName, coordinator.SavedFileName);
            Assert.IsNull(coordinator.SavedDisplayName);
            Assert.AreEqual(SaveGameManager.QuickSaveFileName, coordinator.LoadedFileName);
            Assert.IsFalse(saving.IsCompleted);
            Assert.IsFalse(loading.IsCompleted);
            coordinator.SaveCompletion.SetResult(true);
            coordinator.LoadCompletion.SetResult(true);

            Assert.IsTrue(await saving);
            Assert.IsTrue(await loading);
        }

        [Test]
        public void StartGame_CoordinatedAutosaveTick_DelegatesWithoutLocalWrite()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 39;
            _gameplaySettings.AutosaveIntervalTicks = 40;
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            GameSession session = _sessionManager.StartGame(game, coordinator);

            session.Tick.ProcessTick();

            Assert.AreEqual(40, game.CurrentTick);
            Assert.AreEqual(1, coordinator.AutosaveTicks);
            Assert.AreSame(_gameplaySettings, coordinator.AutosaveSettings);
            Assert.IsFalse(Directory.Exists(_saveDirectoryPath));
        }

        [Test]
        public async Task StartGame_ReplacingOwnedSessionWithLocalGame_RestoresLocalSavingAsync()
        {
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _sessionManager.StartGame(CreateGame(), coordinator);

            _sessionManager.EndGame();
            _sessionManager.StartGame(CreateGame());

            Assert.IsFalse(_sessionManager.HasCoordinatedSession);
            Assert.IsTrue(
                await _sessionManager.SaveGameAsync("local-checkpoint", "Local checkpoint")
            );
            Assert.IsTrue(File.Exists(_saveGameManager.GetSaveFilePath("local-checkpoint")));
            Assert.IsNull(coordinator.SavedFileName);
        }

        [Test]
        public async Task LoadGameAsync_LocalOwner_RestoresSaveWithoutReplacingSessionAsync()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 14;
            GameSession session = _sessionManager.StartGame(game);
            Assert.IsTrue(
                await _sessionManager.SaveGameAsync("local-checkpoint", "Local checkpoint")
            );
            game.CurrentTick = 91;

            bool loaded = await _sessionManager.LoadGameAsync("local-checkpoint");

            Assert.IsTrue(loaded);
            Assert.AreSame(session, _sessionManager.GetActiveGameSession());
            Assert.AreEqual(14, _sessionManager.GetActiveGame().CurrentTick);
        }

        [Test]
        public void SaveGameAsync_CanceledOwner_PropagatesCancellationWithoutLocalWrite()
        {
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _sessionManager.StartGame(CreateGame(), coordinator);
            Task<bool> saving = _sessionManager.SaveGameAsync(
                "shared-checkpoint",
                "Shared checkpoint"
            );

            coordinator.SaveCompletion.SetCanceled();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await saving);
            Assert.IsFalse(Directory.Exists(_saveDirectoryPath));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReplaceGame_CampaignOwnership_LeavesPreparedLocalUIUnchanged(bool sameCampaign)
        {
            string campaignID = Guid.NewGuid().ToString("N");
            GameRoot current = CreateGame();
            ConfigureMultiplayerPresentation(current, campaignID);
            var currentGuestUI = current.GetFactionPlayer("FNEMP1").UIState;
            currentGuestUI.GetOrCreateSection("AdvisorNotifications").Values["deadline"] = "150";
            _sessionManager.StartGame(current, new TestSessionOwner());
            GameRoot incoming = CreateGame();
            ConfigureMultiplayerPresentation(
                incoming,
                sameCampaign ? campaignID : Guid.NewGuid().ToString("N")
            );
            incoming.CurrentTick = 120;
            var incomingHostUI = incoming.GetFactionPlayer("FNALL1").UIState;
            var incomingGuestUI = incoming.GetFactionPlayer("FNEMP1").UIState;
            using GameSession source = new GameSession(incoming, _contentPack.GameData);

            _sessionManager.ReplaceGame(source.Game);

            Assert.AreSame(incoming, _sessionManager.GetActiveGame());
            Assert.AreSame(incomingHostUI, incoming.GetFactionPlayer("FNALL1").UIState);
            Assert.AreSame(incomingGuestUI, incoming.GetFactionPlayer("FNEMP1").UIState);
            Assert.AreEqual(120, _sessionManager.GetActiveGame().CurrentTick);
        }

        /// <summary>
        /// Checks that coordinated loading leaves encounter detection to the next ordered turn.
        /// </summary>
        private void AssertCoordinatedBattleWaitsForTurn()
        {
            GameSession session = _sessionManager.GetActiveGameSession();
            Assert.That(session.Tick.IsSettled, Is.True);
            Assert.That(session.GetService<SpaceCombatCommands>().HasPendingDecision, Is.False);
            Assert.That(_sessionManager.CanSave, Is.True);
            session.Tick.ProcessTick();
            Assert.That(session.GetService<SpaceCombatCommands>().HasPendingDecision, Is.True);
            Assert.That(_sessionManager.CanSave, Is.False);
        }

        /// <summary>
        /// Binds a minimal two-human graph to explicit campaign and local presentation ownership.
        /// </summary>
        /// <param name="game">The content-compatible fixture graph.</param>
        /// <param name="campaignID">The stable campaign identity.</param>
        private static void ConfigureMultiplayerPresentation(GameRoot game, string campaignID)
        {
            game.Metadata = new GameMetadata
            {
                CampaignID = campaignID,
                MultiplayerCampaign = new MultiplayerCampaignIdentity
                {
                    HostUserID = "host-account",
                    GuestUserID = "guest-account",
                    HostFactionID = "FNALL1",
                    GuestFactionID = "FNEMP1",
                },
            };
            game.LocalPlayerFactionID = "FNEMP1";
            game.GetFactions().Add(new Faction { InstanceID = "FNALL1" });
            game.GetFactions().Add(new Faction { InstanceID = "FNEMP1" });
            game.SetFactionController("FNALL1", "host", PlayerControllerType.Human);
            game.SetFactionController("FNEMP1", "guest", PlayerControllerType.Human);
        }

        [Test]
        public async Task SaveGameAsync_StorageFailure_ReturnsFalseAndReportsFailureAsync()
        {
            TestPersistenceCoordinator coordinator = new();
            _sessionManager.StartGame(CreateGame(), coordinator);
            SaveGameOperation? reported = null;
            _sessionManager.PersistenceFailed += (_, operation, _) => reported = operation;
            Task<bool> saving = _sessionManager.SaveGameAsync("checkpoint", "Checkpoint");
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Exception,
                new System.Text.RegularExpressions.Regex("test storage failure")
            );
            coordinator.SaveCompletion.SetException(new IOException("test storage failure"));

            Assert.That(await saving, Is.False);
            Assert.That(reported, Is.EqualTo(SaveGameOperation.Save));
        }

        [Test]
        public async Task QuickSaveAsync_StorageFailure_ReportsFailureAsync()
        {
            TestPersistenceCoordinator coordinator = new();
            _sessionManager.StartGame(CreateGame(), coordinator);
            bool reported = false;
            _sessionManager.PersistenceFailed += (_, _, _) => reported = true;
            Task<bool> saving = _sessionManager.QuickSaveAsync();
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Exception,
                new System.Text.RegularExpressions.Regex("test storage failure")
            );
            coordinator.SaveCompletion.SetException(new IOException("test storage failure"));
            await saving;

            Assert.That(reported, Is.True);
        }

        [Test]
        public async Task LoadGameAsync_StorageFailure_ReportsFailedLoadAsync()
        {
            TestPersistenceCoordinator coordinator = new();
            _sessionManager.StartGame(CreateGame(), coordinator);
            SaveGameOperation? reported = null;
            _sessionManager.PersistenceFailed += (_, operation, _) => reported = operation;
            Task<bool> loading = _sessionManager.LoadGameAsync("checkpoint");
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Exception,
                new System.Text.RegularExpressions.Regex("test storage failure")
            );
            coordinator.LoadCompletion.SetException(new IOException("test storage failure"));
            await loading;

            Assert.That(reported, Is.EqualTo(SaveGameOperation.Load));
        }

        [Test]
        public async Task SaveGameAsync_FailureAfterSessionReplacement_DoesNotNotifyReplacementAsync()
        {
            TestPersistenceCoordinator coordinator = new();
            _sessionManager.StartGame(CreateGame(), coordinator);
            bool reported = false;
            _sessionManager.PersistenceFailed += (_, _, _) => reported = true;
            Task<bool> saving = _sessionManager.SaveGameAsync("checkpoint", "Checkpoint");
            _sessionManager.StartGame(CreateGame());
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Exception,
                new System.Text.RegularExpressions.Regex("test storage failure")
            );
            coordinator.SaveCompletion.SetException(new IOException("test storage failure"));
            await saving;

            Assert.That(reported, Is.False);
        }

        private class TestSessionOwner : IGameSessionOwner
        {
            public bool CanRequestSave => true;
            private readonly IPlayerRequestDispatcher _actions;
            public readonly List<string> Calls = new();

            /// <summary>
            /// Records lifecycle ownership while delegating typed requests to the fixture dispatcher.
            /// </summary>
            /// <param name="actions">The explicit command owner.</param>
            public TestSessionOwner(IPlayerRequestDispatcher actions = null)
            {
                _actions = actions ?? TestPlayerRequests.RejectAll();
            }

            /// <summary>
            /// Records composition before the session services are created.
            /// </summary>
            /// <param name="game">The incoming graph.</param>
            public void PrepareGame(GameRoot game) => Calls.Add("prepare");

            /// <summary>
            /// Records runtime attachment before presentation readiness.
            /// </summary>
            /// <param name="runtime">The newly composed runtime.</param>
            public void BindSessionManager(GameSessionManager runtime) => Calls.Add("bind");

            /// <summary>
            /// Records the presentation's readiness notification.
            /// </summary>
            /// <param name="runtime">The ready runtime.</param>
            public void NotifyPresentationReady(GameSessionManager runtime) => Calls.Add("ready");

            /// <summary>
            /// Records release of the outgoing session.
            /// </summary>
            /// <param name="runtime">The outgoing runtime.</param>
            public void ReleaseSessionManager(GameSessionManager runtime) => Calls.Add("release");

            /// <summary>
            /// Keeps the fixture clock stopped.
            /// </summary>
            /// <param name="elapsedSeconds">The elapsed frame interval.</param>
            /// <returns>False while exercising lifecycle composition.</returns>
            public bool TryBeginGameTick(float elapsedSeconds) => false;

            /// <summary>
            /// Forwards commands to the fixture's declared owner.
            /// </summary>
            /// <param name="action">The submitted command.</param>
            /// <returns>The fixture execution result.</returns>
            public Task<PlayerRequestOutcome> ExecuteAsync(PlayerRequest action) =>
                _actions.ExecuteAsync(action);

            /// <summary>
            /// Leaves storage outside these lifecycle tests.
            /// </summary>
            /// <param name="fileName">The requested identifier.</param>
            /// <param name="displayName">The requested display name.</param>
            /// <returns>False without publishing a save.</returns>
            public virtual Task<bool> SaveGameAsync(string fileName, string displayName) =>
                Task.FromResult(false);

            /// <summary>
            /// Leaves loading outside these lifecycle tests.
            /// </summary>
            /// <param name="fileName">The requested identifier.</param>
            /// <returns>False without replacing the graph.</returns>
            public virtual Task<bool> LoadGameAsync(string fileName) => Task.FromResult(false);

            /// <summary>
            /// Leaves automatic persistence outside these lifecycle tests.
            /// </summary>
            /// <param name="settings">The configured save preferences.</param>
            public virtual void ProcessAutosaveTick(UserGameplaySettings settings) { }
        }

        private sealed class TestPersistenceCoordinator : TestSessionOwner
        {
            internal readonly TaskCompletionSource<bool> SaveCompletion = new();
            internal readonly TaskCompletionSource<bool> LoadCompletion = new();
            internal string SavedFileName;
            internal string SavedDisplayName;
            internal string LoadedFileName;
            internal int AutosaveTicks;
            internal UserGameplaySettings AutosaveSettings;

            /// <summary>
            /// Captures a save request while the fixture controls durable completion.
            /// </summary>
            /// <param name="fileName">The requested save identifier.</param>
            /// <param name="displayName">The requested display name.</param>
            /// <returns>The fixture's pending completion.</returns>
            public override Task<bool> SaveGameAsync(string fileName, string displayName)
            {
                SavedFileName = fileName;
                SavedDisplayName = displayName;
                return SaveCompletion.Task;
            }

            /// <summary>
            /// Captures a shared load without consulting the local save store.
            /// </summary>
            /// <param name="fileName">The requested save identifier.</param>
            /// <returns>The fixture's pending completion.</returns>
            public override Task<bool> LoadGameAsync(string fileName)
            {
                LoadedFileName = fileName;
                return LoadCompletion.Task;
            }

            /// <summary>
            /// Records the completed tick's automatic-save preferences without writing.
            /// </summary>
            /// <param name="settings">The preferences supplied by the runtime.</param>
            public override void ProcessAutosaveTick(UserGameplaySettings settings)
            {
                AutosaveTicks++;
                AutosaveSettings = settings;
            }
        }

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <returns>The created game.</returns>
        private GameRoot CreateGame()
        {
            return new GameRoot
            {
                Summary = new GameSummary
                {
                    PackID = _contentPack.Definition.ID,
                    PackVersion = _contentPack.Definition.Version,
                    ScenarioID = _contentPack.Scenario.ID,
                },
            };
        }

        /// <summary>
        /// Creates a load-compatible game whose serialized faction settings predate current content.
        /// </summary>
        /// <returns>The prepared legacy game.</returns>
        private GameRoot CreateGameWithLegacyFactionSettings()
        {
            GameRoot game = CreateGame();
            foreach (Faction template in _contentPack.GameData.Factions)
            {
                game.GetFactions()
                    .Add(
                        new Faction
                        {
                            InstanceID = template.InstanceID,
                            DisplayName = template.DisplayName,
                            Settings = new FactionSettings
                            {
                                BattleLossLoyaltyDivisor = 0,
                                GarrisonEfficiency = 99,
                            },
                        }
                    );
            }

            return game;
        }

        /// <summary>
        /// Creates a load-compatible game with stale serialized galaxy coordinates.
        /// </summary>
        /// <returns>The prepared game.</returns>
        private GameRoot CreateGameWithStaleGalaxyLayout()
        {
            PlanetSector template = _contentPack.GameData.PlanetSectors.First(sector =>
                sector.GetChildren<Planet>().Count > 0
            );
            Planet planetTemplate = template.GetChildren<Planet>()[0];
            PlanetSector sector = new PlanetSector
            {
                InstanceID = template.InstanceID,
                TypeID = template.TypeID,
                PositionX = template.PositionX + 100,
                PositionY = template.PositionY + 100,
            };
            Planet planet = new Planet
            {
                InstanceID = planetTemplate.InstanceID,
                TypeID = planetTemplate.TypeID,
                PositionX = planetTemplate.PositionX + 100,
                PositionY = planetTemplate.PositionY + 100,
            };
            GameRoot game = CreateGame();
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            return game;
        }

        /// <summary>
        /// Asserts that loaded factions own independent copies of active authored settings.
        /// </summary>
        /// <param name="game">The loaded game to inspect.</param>
        private void AssertFactionSettingsMatchActiveContent(GameRoot game)
        {
            foreach (Faction template in _contentPack.GameData.Factions)
            {
                Faction loaded = game.GetFactions()
                    .Single(faction => faction.InstanceID == template.InstanceID);
                Assert.AreEqual(
                    template.Settings.BattleLossLoyaltyDivisor,
                    loaded.Settings.BattleLossLoyaltyDivisor
                );
                Assert.AreEqual(
                    template.Settings.GarrisonEfficiency,
                    loaded.Settings.GarrisonEfficiency
                );
                Assert.AreNotSame(template.Settings, loaded.Settings);
                Assert.AreNotSame(template.Settings.Headquarters, loaded.Settings.Headquarters);
            }
        }

        /// <summary>
        /// Asserts that loaded galaxy coordinates match the active authored layout.
        /// </summary>
        /// <param name="game">The loaded game to inspect.</param>
        private void AssertGalaxyLayoutMatchesActiveContent(GameRoot game)
        {
            PlanetSector loaded = game.GetGalaxyMap().GetChildren<PlanetSector>().Single();
            PlanetSector template = _contentPack.GameData.PlanetSectors.Single(sector =>
                sector.TypeID == loaded.TypeID
            );
            Planet loadedPlanet = loaded.GetChildren<Planet>().Single();
            Planet templatePlanet = template
                .GetChildren<Planet>()
                .Single(planet => planet.InstanceID == loadedPlanet.InstanceID);
            Assert.AreEqual(template.PositionX, loaded.PositionX);
            Assert.AreEqual(template.PositionY, loaded.PositionY);
            Assert.AreEqual(templatePlanet.PositionX, loadedPlanet.PositionX);
            Assert.AreEqual(templatePlanet.PositionY, loadedPlanet.PositionY);
        }

        /// <summary>
        /// Creates contested game.
        /// </summary>
        /// <returns>The created contested game.</returns>
        private GameRoot CreateContestedGame()
        {
            GameRoot game = CreateGame();
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            game.SetFactionController(alliance.InstanceID, "player", PlayerControllerType.Human);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                DisplayName = "Planet",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            AddFleet(game, planet, "ALLIANCE_FLEET", alliance.InstanceID);
            AddFleet(game, planet, "EMPIRE_FLEET", empire.InstanceID);
            return game;
        }

        /// <summary>
        /// Adds fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerId">The owner id.</param>
        private static void AddFleet(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerId
        )
        {
            Fleet fleet = new Fleet
            {
                InstanceID = instanceId,
                DisplayName = instanceId,
                OwnerInstanceID = ownerId,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = instanceId + "_SHIP",
                DisplayName = instanceId + " Ship",
                OwnerInstanceID = ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 100,
                MaxHullStrength = 100,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
        }

        private sealed class RecordingAnalyticsSink : IAnalyticsEventSink
        {
            internal List<string> EventNames { get; } = new List<string>();

            public bool IsAvailable => true;
            public bool IsCollectionEnabled => true;

            /// <summary>
            /// Records an event name for lifecycle assertions.
            /// </summary>
            /// <param name="eventName">The event name.</param>
            /// <param name="parameters">The event parameters.</param>
            public void RecordEvent(
                string eventName,
                IReadOnlyDictionary<string, object> parameters
            )
            {
                EventNames.Add(eventName);
            }

            /// <summary>
            /// Completes a no-op flush for the in-memory sink.
            /// </summary>
            public void Flush() { }
        }
    }
}
