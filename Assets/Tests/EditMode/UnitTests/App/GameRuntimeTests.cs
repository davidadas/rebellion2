using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.App;
using Rebellion.App.PlayerActions;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting.Events;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Generation;
using Rebellion.Multiplayer;
using Rebellion.Util.Reflection;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class GameRuntimeTests
    {
        private ContentPack _contentPack;
        private UserGameplaySettings _gameplaySettings;
        private GameRuntime _runtime;
        private SaveGameManager _saveGameManager;
        private string _saveDirectoryPath;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _contentPack = TestContent.Pack;
            _saveDirectoryPath = Path.Combine(
                Path.GetTempPath(),
                nameof(GameRuntimeTests),
                Guid.NewGuid().ToString("N")
            );
            _gameplaySettings = new UserGameplaySettings();
            _saveGameManager = new SaveGameManager(_saveDirectoryPath);
            _runtime = new GameRuntime(_contentPack, _saveGameManager, () => _gameplaySettings);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _runtime?.EndGame();
            if (Directory.Exists(_saveDirectoryPath))
                Directory.Delete(_saveDirectoryPath, true);
        }

        [Test]
        public void StartGame_DeclaredOwner_BindsBeforePresentationReadiness()
        {
            GameRoot game = CreateGame();
            ConfigureMultiplayerPresentation(game, Guid.NewGuid().ToString("N"));
            TestSessionOwner owner = new();

            GameSession session = _runtime.StartGame(game, owner);

            CollectionAssert.AreEqual(new[] { "prepare", "bind" }, owner.Calls);
            Assert.IsTrue(_runtime.HasCoordinatedPersistence);
            Assert.IsTrue(_runtime.NotifyPresentationReady(session));
            CollectionAssert.AreEqual(new[] { "prepare", "bind", "ready" }, owner.Calls);
        }

        [Test]
        public void NotifyPresentationReady_ReplacedSession_DoesNotNotifyNewOwner()
        {
            GameSession outgoing = _runtime.StartGame(CreateGame());
            TestSessionOwner owner = new();
            _runtime.StartGame(CreateGame(), owner);

            Assert.IsFalse(_runtime.NotifyPresentationReady(outgoing));
            CollectionAssert.AreEqual(new[] { "prepare", "bind" }, owner.Calls);
        }

        [Test]
        public void ResumeMultiplayerOperations_VerifiedCoordinatedSession_ReopensPersistence()
        {
            _runtime.StartGame(CreateGame(), new TestSessionOwner());
            _runtime.BlockGameOperations();
            Assert.IsFalse(_runtime.CanSave);
            _runtime.ResumeMultiplayerOperations();
            Assert.IsTrue(_runtime.CanSave);
        }

        [Test]
        public void ResumeMultiplayerOperations_UncoordinatedSession_RejectsResume()
        {
            _runtime.StartGame(CreateGame());
            Assert.Throws<InvalidOperationException>(_runtime.ResumeMultiplayerOperations);
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
                    _runtime.StartLoadedGame(game);
                else
                    _runtime.StartGame(game);
            });
            Assert.IsFalse(_runtime.HasActiveGame);
        }

        [Test]
        public void StartGame_QueuedOutgoingRequest_CancelsBeforeReplacementOwnerStarts()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            Task<int> pending = null;
            session.Game.SetGameSpeed(TickSpeed.Fast);
            session.Tick.TickCompleted += () =>
                pending = session.ExecuteCommandAsync(() => ++session.Game.CurrentTick);
            session.Tick.ProcessTick();
            int completedTick = session.Game.CurrentTick;
            Assert.IsNotNull(pending);
            Assert.IsFalse(pending.IsCompleted);
            _runtime.StartGame(CreateGame(), new TestSessionOwner());
            Assert.IsTrue(pending.IsCanceled);
            Assert.AreEqual(completedTick, session.Game.CurrentTick);
        }

        [Test]
        public async Task StartGame_DeclaredOwner_ReceivesRequestsBeforePresentationReadyAsync()
        {
            GameRoot game = CreateGame();
            ConfigureMultiplayerPresentation(game, Guid.NewGuid().ToString("N"));
            RecordingPlayerActionDispatcher negotiatedOwner = TestPlayerActions.RejectAll();
            _runtime.StartGame(game, new TestSessionOwner(negotiatedOwner));
            IPlayerActionDispatcher capturedByWindow = _runtime.PlayerActions;
            SetSpeedPlayerAction action = new() { Speed = TickSpeed.Fast };

            await capturedByWindow.ExecuteAsync(action);

            Assert.That(negotiatedOwner.Requests, Is.EqualTo(new[] { action }));
        }

        [Test]
        public async Task StartGame_BeforePresentationReady_DoesNotExecuteLocallyAsync()
        {
            GameRoot game = CreateGame();
            ConfigureMultiplayerPresentation(game, Guid.NewGuid().ToString("N"));
            _runtime.StartGame(game, new TestSessionOwner());
            _runtime.GetActiveGameManager().SetGameSpeed(TickSpeed.Paused);
            IPlayerActionDispatcher capturedByWindow = _runtime.PlayerActions;

            await capturedByWindow.ExecuteAsync(
                new SetSpeedPlayerAction { Speed = TickSpeed.Fast }
            );

            Assert.That(game.GetGameSpeed(), Is.EqualTo(TickSpeed.Paused));
        }

        [Test]
        public async Task EndGame_PreviouslyCapturedDispatcher_RejectsWithoutExecutingAsync()
        {
            GameRoot game = CreateGame();
            ConfigureMultiplayerPresentation(game, Guid.NewGuid().ToString("N"));
            RecordingPlayerActionDispatcher owner = TestPlayerActions.RejectAll();
            _runtime.StartGame(game, new TestSessionOwner(owner));
            IPlayerActionDispatcher capturedByWindow = _runtime.PlayerActions;
            _runtime.EndGame();

            PlayerActionResult result = await capturedByWindow.ExecuteAsync(
                new SetSpeedPlayerAction { Speed = TickSpeed.Fast }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(owner.Requests);
        }

        [Test]
        public async Task StartGame_ReplacedSessionDispatcher_RejectsWithoutReachingNewOwnerAsync()
        {
            GameRoot first = CreateGame();
            ConfigureMultiplayerPresentation(first, Guid.NewGuid().ToString("N"));
            _runtime.StartGame(first, new TestSessionOwner());
            IPlayerActionDispatcher capturedByOldWindow = _runtime.PlayerActions;
            GameRoot replacement = CreateGame();
            ConfigureMultiplayerPresentation(replacement, Guid.NewGuid().ToString("N"));
            RecordingPlayerActionDispatcher newOwner = TestPlayerActions.RejectAll();
            _runtime.StartGame(replacement, new TestSessionOwner(newOwner));

            PlayerActionResult result = await capturedByOldWindow.ExecuteAsync(
                new SetSpeedPlayerAction { Speed = TickSpeed.Fast }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(newOwner.Requests);
        }

        [Test]
        public void StartGame_InvalidContent_EndsPreviousSession()
        {
            _runtime.StartGame(CreateGame());
            GameRoot replacement = CreateGame();
            replacement.Summary.PackID = "different-pack";

            Assert.Throws<InvalidOperationException>(() => _runtime.StartGame(replacement));

            Assert.IsFalse(_runtime.HasActiveGame);
        }

        [Test]
        public void LoadGame_InvalidContent_KeepsActiveManager()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            GameManager manager = _runtime.GetActiveGameManager();
            GameRoot replacement = CreateGame();
            replacement.Summary.PackID = "different-pack";
            _saveGameManager.SaveGameData(replacement, "invalid-content", "Invalid Content");

            Assert.Throws<InvalidOperationException>(() => _runtime.LoadGame("invalid-content"));

            Assert.AreSame(manager, _runtime.GetActiveGameManager());
        }

        [Test]
        public void LoadGame_ValidSave_KeepsManagerIdentity()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            GameManager manager = _runtime.GetActiveGameManager();
            _saveGameManager.SaveGameData(CreateGame(), "replacement", "Replacement");

            _runtime.LoadGame("replacement");

            Assert.AreSame(manager, _runtime.GetActiveGameManager());
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

            _runtime.StartLoadedGame(game);

            Assert.AreEqual(expected, ship.SectorSuppression);
        }

        [Test]
        public void StartLoadedGame_LegacyFactionSettings_RestoresActiveContentSettings()
        {
            GameRoot legacyGame = CreateGameWithLegacyFactionSettings();

            GameSession session = _runtime.StartLoadedGame(legacyGame);

            AssertFactionSettingsMatchActiveContent(session.Game);
        }

        [Test]
        public void StartGame_FactionSettings_RestoresActiveContentSettings()
        {
            GameRoot game = CreateGameWithLegacyFactionSettings();

            GameSession session = _runtime.StartGame(game);

            AssertFactionSettingsMatchActiveContent(session.Game);
        }

        [Test]
        public void StartGame_FactionModifierOverride_RestoresConfiguredSettings()
        {
            GameRoot game = CreateGameWithLegacyFactionSettings();
            Faction template = _contentPack.GameData.Factions[0];
            FactionSettings configured = template.Settings.GetDeepCopy();
            configured.GarrisonEfficiency++;
            GameGenerationConfig config = _contentPack.GameData.GenerationConfig.GetDeepCopy();
            config.FactionModifiers.Add(
                new FactionModifierConfig { FactionID = template.InstanceID, Settings = configured }
            );
            game.Summary.CustomSettings = new CustomGameSettings { GenerationConfig = config };

            GameSession session = _runtime.StartGame(game);

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

            GameSession session = _runtime.StartLoadedGame(game);

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

            GameSession session = _runtime.StartLoadedGame(game);

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

            GameSession session = _runtime.StartLoadedGame(game);

            AssertGalaxyLayoutMatchesActiveContent(session.Game);
        }

        [Test]
        public void LoadGame_LegacyFactionSettings_RestoresActiveContentSettings()
        {
            _runtime.StartGame(CreateGame());
            GameRoot legacyGame = CreateGameWithLegacyFactionSettings();
            _saveGameManager.SaveGameData(legacyGame, "legacy-faction-settings", "Legacy");

            bool loaded = _runtime.LoadGame("legacy-faction-settings");

            Assert.IsTrue(loaded);
            AssertFactionSettingsMatchActiveContent(_runtime.GetActiveGame());
        }

        [Test]
        public void LoadGame_SerializedGalaxyLayout_RestoresActiveContentCoordinates()
        {
            _runtime.StartGame(CreateGame());
            GameRoot game = CreateGameWithStaleGalaxyLayout();
            _saveGameManager.SaveGameData(game, "stale-galaxy-layout", "Stale Galaxy Layout");

            bool loaded = _runtime.LoadGame("stale-galaxy-layout");

            Assert.IsTrue(loaded);
            AssertGalaxyLayoutMatchesActiveContent(_runtime.GetActiveGame());
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
            GameSession session = _runtime.StartLoadedGame(legacyGame);
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
        public void EndGame_SpeedObserverThrows_KeepsActiveManager()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            GameManager manager = _runtime.GetActiveGameManager();
            Action fail = () => throw new InvalidOperationException("Pause observer failed.");
            manager.GameSpeedChanged += fail;

            try
            {
                Assert.Throws<InvalidOperationException>(_runtime.EndGame);
                Assert.AreSame(manager, _runtime.GetActiveGameManager());
            }
            finally
            {
                manager.GameSpeedChanged -= fail;
            }
        }

        [Test]
        public void EndGame_SpeedObserverThrows_DetachesAutosave()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 39;
            _gameplaySettings.AutosaveIntervalTicks = 40;
            GameSession session = _runtime.StartGame(game);
            GameManager manager = _runtime.GetActiveGameManager();
            Action fail = () => throw new InvalidOperationException("Pause observer failed.");
            manager.GameSpeedChanged += fail;

            try
            {
                Assert.Throws<InvalidOperationException>(_runtime.EndGame);
            }
            finally
            {
                manager.GameSpeedChanged -= fail;
            }
            manager.SetGameSpeed(TickSpeed.Fast);
            session.Tick.ProcessTick();

            Assert.IsEmpty(_saveGameManager.GetSavedGames());
        }

        [Test]
        public void StartGame_PendingCombat_DefersAutosaveUntilResolution()
        {
            GameRoot game = CreateContestedGame();
            game.CurrentTick = 39;
            _gameplaySettings.AutosaveIntervalTicks = 40;
            GameSession session = _runtime.StartGame(game);
            GameManager manager = _runtime.GetActiveGameManager();
            session.Tick.ProcessTick();

            Assert.IsEmpty(_saveGameManager.GetSavedGames());
            Assert.IsFalse(_runtime.CanSave);

            session.Tick.ResolveCombat(true);

            string autosavePath = _saveGameManager.GetSaveFilePath(
                SaveGameManager.AutosaveFilePrefix + game.Metadata.CampaignID + "_0000000040"
            );
            Assert.IsTrue(File.Exists(autosavePath));
            Assert.IsTrue(_runtime.CanSave);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StartLoadedGame_CoordinatedContestedWorld_DetectsBattleInAgreedTurn(
            bool replace
        )
        {
            GameRoot game = CreateContestedGame();
            if (replace)
            {
                _runtime.StartGame(CreateGame(), new TestSessionOwner());
                _runtime.ReplaceMultiplayerGame(game);
            }
            else
                _runtime.StartLoadedGame(game, new TestSessionOwner());
            GameSession session = _runtime.GetActiveGameSession();

            Assert.That(session.Tick.IsSettled, Is.True);
            Assert.That(session.GetService<SpaceCombatCommands>().HasPendingDecision, Is.False);
            Assert.That(_runtime.CanSave, Is.True);
            session.Tick.ProcessTick();
            Assert.That(session.GetService<SpaceCombatCommands>().HasPendingDecision, Is.True);
            Assert.That(_runtime.CanSave, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReplaceMultiplayerRecovery_InterruptedBattle_RestoresCompletedWorldAndReopensSaving(
            bool incremental
        )
        {
            GameSession session = _runtime.StartGame(CreateContestedGame(), new TestSessionOwner());
            MultiplayerSnapshot checkpoint = MultiplayerRecoverySnapshotCodec.Capture(session);
            int completedTick = session.Game.CurrentTick;
            int replacements = 0;
            _runtime.GameReplaced += _ => replacements++;
            _runtime.BlockGameOperations();
            if (incremental)
            {
                System.Collections.IEnumerator tick = session.Tick.ProcessTickIncrementally();
                Assert.That(tick.MoveNext(), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    MultiplayerRecoverySnapshotCodec.Capture(session)
                );
                while (tick.MoveNext()) { }
                (tick as IDisposable)?.Dispose();
            }
            else
                session.Tick.ProcessTick();
            Assert.That(session.Tick.IsSettled, Is.False);
            Assert.That(_runtime.CanSave, Is.False);

            _runtime.ReplaceMultiplayerRecovery(
                MultiplayerRecoverySnapshotCodec.Deserialize(checkpoint.Payload, checkpoint.Hash)
            );

            Assert.That(_runtime.GetActiveGameSession(), Is.SameAs(session));
            Assert.That(session.Game.CurrentTick, Is.EqualTo(completedTick));
            Assert.That(session.Tick.IsSettled, Is.True);
            Assert.That(session.GetService<SpaceCombatCommands>().HasPendingDecision, Is.False);
            Assert.That(_runtime.CanSave, Is.True);
            Assert.That(replacements, Is.EqualTo(1));
            session.Tick.ProcessTick();
            Assert.That(
                session
                    .GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending),
                Is.True
            );
            Assert.That(pending.AttackerHasResponded || pending.DefenderHasResponded, Is.False);
            Assert.That(_runtime.CanSave, Is.False);
            session.Tick.ResolveCombat(autoResolve: true);
            Assert.That(_runtime.CanSave, Is.True);
            Assert.That(session.Game.CurrentTick, Is.EqualTo(completedTick + 1));
        }

        [Test]
        public void QuickSave_PendingCombat_DoesNotWriteSave()
        {
            GameRoot game = CreateContestedGame();
            _runtime.StartLoadedGame(game);

            bool saved = _runtime.QuickSave();

            Assert.IsFalse(saved);
            Assert.IsFalse(
                File.Exists(_saveGameManager.GetSaveFilePath(SaveGameManager.QuickSaveFileName))
            );
        }

        [Test]
        public void SaveGame_PendingCombat_DoesNotWriteSave()
        {
            GameRoot game = CreateContestedGame();
            game.CurrentTick = 40;
            GameSession session = _runtime.StartLoadedGame(game);
            GameManager manager = _runtime.GetActiveGameManager();

            bool saved = _runtime.SaveGame("pending_combat", "Pending Combat");

            Assert.IsTrue(session.GetService<SpaceCombatCommands>().HasPendingDecision);
            Assert.IsFalse(_runtime.CanSave);
            Assert.IsFalse(saved);
            Assert.IsFalse(File.Exists(_saveGameManager.GetSaveFilePath("pending_combat")));
            Assert.AreEqual(40, game.CurrentTick);
        }

        [Test]
        public void QuickLoad_AfterQuickSave_ReplacesMutatedGameWithSavedState()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 123;
            GameSession session = _runtime.StartGame(game);
            GameManager manager = _runtime.GetActiveGameManager();
            GameRoot replacement = null;
            _runtime.GameReplaced += loadedGame => replacement = loadedGame;

            _runtime.QuickSave();
            game.CurrentTick = 999;
            _runtime.QuickLoad();

            Assert.IsNotNull(replacement);
            Assert.AreNotSame(game, replacement);
            Assert.AreSame(replacement, _runtime.GetActiveGame());
            Assert.AreEqual(123, replacement.CurrentTick);
        }

        [Test]
        public void ValidateGameContent_MatchingIdentity_DoesNotThrow()
        {
            GameRoot game = CreateGame();

            Assert.DoesNotThrow(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_MissingIdentity_ThrowsInvalidOperationException()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary() };

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentPack_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.PackID = "different-pack";

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentVersion_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.PackVersion = "different-version";

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentScenario_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.ScenarioID = "different-scenario";

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_SaveHasDifferentMods_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.ModIDs = new[] { "missing-mod" };
            game.Summary.ModVersions = new[] { "1.0.0" };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _runtime.ValidateGameContent(game)
            );
            StringAssert.Contains("mods [missing-mod@1.0.0]", exception.Message);
            StringAssert.Contains("mods [] is active", exception.Message);
        }

        [Test]
        public void LoadGame_InvalidEvent_DoesNotNotifyPresentation()
        {
            _runtime.StartGame(CreateGame());
            GameRoot replacement = CreateGame();
            replacement
                .GetEventPool()
                .Add(new GameEvent { InstanceID = "INVALID", MaximumActivations = 0 });
            _saveGameManager.SaveGameData(replacement, "invalid-event", "Invalid Event");
            int announcements = 0;
            _runtime.GameReplaced += _ => announcements++;

            Assert.Throws<InvalidOperationException>(() => _runtime.LoadGame("invalid-event"));

            Assert.AreEqual(0, announcements);
        }

        [Test]
        public void LoadGame_ValidSave_KeepsSessionIdentity()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            _saveGameManager.SaveGameData(CreateGame(), "replacement", "Replacement");

            _runtime.LoadGame("replacement");

            Assert.AreSame(session, _runtime.GetActiveGameSession());
        }

        [Test]
        public void LoadGame_ValidSave_ResetsClockBeforeNotification()
        {
            _runtime.StartGame(CreateGame());
            GameManager clock = _runtime.GetActiveGameManager();
            clock.SetGameSpeed(TickSpeed.Fast);
            float interval = _runtime.GetActiveGame().Config.GameSpeed.FastTickIntervalSeconds;
            clock.TryAdvanceTickTimer(interval / 2f);
            GameRoot replacement = CreateGame();
            replacement.SetGameSpeed(TickSpeed.Fast);
            _saveGameManager.SaveGameData(replacement, "replacement", "Replacement");
            bool? ready = null;
            _runtime.GameReplaced += _ => ready = clock.TryAdvanceTickTimer(interval / 2f);

            _runtime.LoadGame("replacement");

            Assert.IsFalse(ready);
        }

        [Test]
        public void LoadGame_PresentationThrows_LeavesReconciliationPending()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            _saveGameManager.SaveGameData(CreateContestedGame(), "contested", "Contested");
            _runtime.GameReplaced += _ =>
                throw new InvalidOperationException("Presentation failed.");

            Assert.Throws<InvalidOperationException>(() => _runtime.LoadGame("contested"));

            Assert.IsFalse(session.GetService<SpaceCombatCommands>().HasPendingDecision);
        }

        [Test]
        public void CanRequestSave_CoordinatedOwnerWhileSimulationBusy_DoesNotRequireImmediateSnapshot()
        {
            _runtime.StartLoadedGame(CreateContestedGame());
            _runtime.BindPersistenceCoordinator(new TestPersistenceCoordinator());

            Assert.IsFalse(_runtime.CanSave);
            Assert.IsTrue(_runtime.CanRequestSave);
        }

        [Test]
        public async Task SaveGameAsync_CoordinatedOwner_WaitsWithoutIndependentWriteAsync()
        {
            GameRoot game = CreateGame();
            _runtime.StartGame(game);
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _runtime.BindPersistenceCoordinator(coordinator);

            Task<bool> saving = _runtime.SaveGameAsync("shared-checkpoint", "Shared checkpoint");

            Assert.IsTrue(_runtime.HasCoordinatedPersistence);
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
            _runtime.StartGame(game);
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _runtime.BindPersistenceCoordinator(coordinator);

            Task<bool> loading = _runtime.LoadGameAsync("host-only-file");

            Assert.IsFalse(loading.IsCompleted);
            Assert.AreEqual("host-only-file", coordinator.LoadedFileName);
            Assert.AreSame(game, _runtime.GetActiveGame());
            coordinator.LoadCompletion.SetResult(false);

            Assert.IsFalse(await loading);
            Assert.AreSame(game, _runtime.GetActiveGame());
            Assert.IsFalse(Directory.Exists(_saveDirectoryPath));
        }

        [Test]
        public void SaveGame_CoordinatedOwner_RefusesEverySynchronousPersistenceEntry()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 12;
            _runtime.StartGame(game);
            Assert.IsTrue(_runtime.SaveGame("checkpoint", "Checkpoint"));
            Assert.IsTrue(_runtime.QuickSave());
            byte[] originalSave = File.ReadAllBytes(_saveGameManager.GetSaveFilePath("checkpoint"));
            game.CurrentTick = 99;
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _runtime.BindPersistenceCoordinator(coordinator);

            Assert.IsFalse(_runtime.SaveGame("checkpoint", "Changed checkpoint"));
            Assert.IsFalse(_runtime.QuickSave());
            Assert.IsFalse(_runtime.LoadGame("checkpoint"));
            _runtime.QuickLoad();

            Assert.AreSame(game, _runtime.GetActiveGame());
            Assert.AreEqual(99, game.CurrentTick);
            Assert.IsNull(coordinator.SavedFileName);
            Assert.IsNull(coordinator.LoadedFileName);
            CollectionAssert.AreEqual(
                originalSave,
                File.ReadAllBytes(_saveGameManager.GetSaveFilePath("checkpoint"))
            );
            Assert.AreEqual(
                12,
                _saveGameManager.LoadGameData(SaveGameManager.QuickSaveFileName).CurrentTick
            );
        }

        [Test]
        public async Task QuickSaveAsync_CoordinatedOwner_UsesQuicksaveSlotAndAwaitsCompletionAsync()
        {
            _runtime.StartGame(CreateGame());
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _runtime.BindPersistenceCoordinator(coordinator);

            Task<bool> saving = _runtime.QuickSaveAsync();
            Task<bool> loading = _runtime.QuickLoadAsync();

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
            GameSession session = _runtime.StartGame(game);
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _runtime.BindPersistenceCoordinator(coordinator);

            session.Tick.ProcessTick();

            Assert.AreEqual(40, game.CurrentTick);
            Assert.AreEqual(1, coordinator.AutosaveTicks);
            Assert.AreSame(_gameplaySettings, coordinator.AutosaveSettings);
            Assert.IsFalse(Directory.Exists(_saveDirectoryPath));
        }

        [Test]
        public async Task SaveGameAsync_UnboundOwner_RestoresLocalPersistenceAsync()
        {
            _runtime.StartGame(CreateGame());
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _runtime.BindPersistenceCoordinator(coordinator);

            _runtime.UnbindPersistenceCoordinator();

            Assert.IsFalse(_runtime.HasCoordinatedPersistence);
            Assert.IsTrue(await _runtime.SaveGameAsync("local-checkpoint", "Local checkpoint"));
            Assert.IsTrue(File.Exists(_saveGameManager.GetSaveFilePath("local-checkpoint")));
            Assert.IsNull(coordinator.SavedFileName);
        }

        [Test]
        public async Task LoadGameAsync_LocalOwner_RestoresSaveWithoutReplacingSessionAsync()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 14;
            GameSession session = _runtime.StartGame(game);
            Assert.IsTrue(await _runtime.SaveGameAsync("local-checkpoint", "Local checkpoint"));
            game.CurrentTick = 91;

            bool loaded = await _runtime.LoadGameAsync("local-checkpoint");

            Assert.IsTrue(loaded);
            Assert.AreSame(session, _runtime.GetActiveGameSession());
            Assert.AreEqual(14, _runtime.GetActiveGame().CurrentTick);
        }

        [Test]
        public void SaveGameAsync_CanceledOwner_PropagatesCancellationWithoutLocalWrite()
        {
            _runtime.StartGame(CreateGame());
            TestPersistenceCoordinator coordinator = new TestPersistenceCoordinator();
            _runtime.BindPersistenceCoordinator(coordinator);
            Task<bool> saving = _runtime.SaveGameAsync("shared-checkpoint", "Shared checkpoint");

            coordinator.SaveCompletion.SetCanceled();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await saving);
            Assert.IsFalse(Directory.Exists(_saveDirectoryPath));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReplaceMultiplayerRecovery_CampaignOwnership_PreservesOnlyMatchingLocalUI(
            bool sameCampaign
        )
        {
            string campaignID = Guid.NewGuid().ToString("N");
            GameRoot current = CreateGame();
            ConfigureMultiplayerPresentation(current, campaignID);
            var currentGuestUI = current.GetFactionPlayer("FNEMP1").UIState;
            currentGuestUI.GetOrCreateSection("AdvisorNotifications").Values["deadline"] = "150";
            _runtime.StartGame(current, new TestSessionOwner());
            GameRoot incoming = CreateGame();
            ConfigureMultiplayerPresentation(
                incoming,
                sameCampaign ? campaignID : Guid.NewGuid().ToString("N")
            );
            incoming.CurrentTick = 120;
            var incomingHostUI = incoming.GetFactionPlayer("FNALL1").UIState;
            var incomingGuestUI = incoming.GetFactionPlayer("FNEMP1").UIState;
            using GameSession source = new GameSession(incoming, _contentPack.GameData);

            _runtime.ReplaceMultiplayerRecovery(source.CaptureRecoveryState());

            Assert.AreSame(incoming, _runtime.GetActiveGame());
            Assert.AreSame(incomingHostUI, incoming.GetFactionPlayer("FNALL1").UIState);
            Assert.AreSame(
                sameCampaign ? currentGuestUI : incomingGuestUI,
                incoming.GetFactionPlayer("FNEMP1").UIState
            );
            Assert.AreEqual(120, _runtime.GetActiveGame().CurrentTick);
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
            _runtime.StartGame(CreateGame());
            TestPersistenceCoordinator coordinator = new();
            _runtime.BindPersistenceCoordinator(coordinator);
            PersistenceOperation? reported = null;
            _runtime.PersistenceFailed += (_, operation, _) => reported = operation;
            Task<bool> saving = _runtime.SaveGameAsync("checkpoint", "Checkpoint");
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Exception,
                new System.Text.RegularExpressions.Regex("test storage failure")
            );
            coordinator.SaveCompletion.SetException(new IOException("test storage failure"));

            Assert.That(await saving, Is.False);
            Assert.That(reported, Is.EqualTo(PersistenceOperation.Save));
        }

        [Test]
        public async Task QuickSaveAsync_StorageFailure_ReportsFailureAsync()
        {
            _runtime.StartGame(CreateGame());
            TestPersistenceCoordinator coordinator = new();
            _runtime.BindPersistenceCoordinator(coordinator);
            bool reported = false;
            _runtime.PersistenceFailed += (_, _, _) => reported = true;
            Task<bool> saving = _runtime.QuickSaveAsync();
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
            _runtime.StartGame(CreateGame());
            TestPersistenceCoordinator coordinator = new();
            _runtime.BindPersistenceCoordinator(coordinator);
            PersistenceOperation? reported = null;
            _runtime.PersistenceFailed += (_, operation, _) => reported = operation;
            Task<bool> loading = _runtime.LoadGameAsync("checkpoint");
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Exception,
                new System.Text.RegularExpressions.Regex("test storage failure")
            );
            coordinator.LoadCompletion.SetException(new IOException("test storage failure"));
            await loading;

            Assert.That(reported, Is.EqualTo(PersistenceOperation.Load));
        }

        [Test]
        public async Task SaveGameAsync_FailureAfterSessionReplacement_DoesNotNotifyReplacementAsync()
        {
            _runtime.StartGame(CreateGame());
            TestPersistenceCoordinator coordinator = new();
            _runtime.BindPersistenceCoordinator(coordinator);
            bool reported = false;
            _runtime.PersistenceFailed += (_, _, _) => reported = true;
            Task<bool> saving = _runtime.SaveGameAsync("checkpoint", "Checkpoint");
            _runtime.StartGame(CreateGame());
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Exception,
                new System.Text.RegularExpressions.Regex("test storage failure")
            );
            coordinator.SaveCompletion.SetException(new IOException("test storage failure"));
            await saving;

            Assert.That(reported, Is.False);
        }

        private sealed class TestSessionOwner : IGameSessionOwner
        {
            public bool CanRequestSave => true;
            private readonly IPlayerActionDispatcher _actions;
            public readonly List<string> Calls = new();

            /// <summary>
            /// Records lifecycle ownership while delegating typed requests to the fixture dispatcher.
            /// </summary>
            /// <param name="actions">The explicit command owner.</param>
            public TestSessionOwner(IPlayerActionDispatcher actions = null)
            {
                _actions = actions ?? TestPlayerActions.RejectAll();
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
            public void BindRuntime(GameRuntime runtime) => Calls.Add("bind");

            /// <summary>
            /// Records the presentation's readiness notification.
            /// </summary>
            /// <param name="runtime">The ready runtime.</param>
            public void NotifyPresentationReady(GameRuntime runtime) => Calls.Add("ready");

            /// <summary>
            /// Records release of the outgoing session.
            /// </summary>
            /// <param name="runtime">The outgoing runtime.</param>
            public void ReleaseRuntime(GameRuntime runtime) => Calls.Add("release");

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
            public Task<PlayerActionResult> ExecuteAsync(PlayerAction action) =>
                _actions.ExecuteAsync(action);

            /// <summary>
            /// Leaves storage outside these lifecycle tests.
            /// </summary>
            /// <param name="fileName">The requested identifier.</param>
            /// <param name="displayName">The requested display name.</param>
            /// <returns>False without publishing a save.</returns>
            public Task<bool> SaveGameAsync(string fileName, string displayName) =>
                Task.FromResult(false);

            /// <summary>
            /// Leaves loading outside these lifecycle tests.
            /// </summary>
            /// <param name="fileName">The requested identifier.</param>
            /// <returns>False without replacing the graph.</returns>
            public Task<bool> LoadGameAsync(string fileName) => Task.FromResult(false);

            /// <summary>
            /// Leaves automatic persistence outside these lifecycle tests.
            /// </summary>
            /// <param name="settings">The configured save preferences.</param>
            public void ProcessAutosaveTick(UserGameplaySettings settings) { }
        }

        private sealed class TestPersistenceCoordinator : IGamePersistenceCoordinator
        {
            public bool CanRequestSave => true;
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
            public Task<bool> SaveGameAsync(string fileName, string displayName)
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
            public Task<bool> LoadGameAsync(string fileName)
            {
                LoadedFileName = fileName;
                return LoadCompletion.Task;
            }

            /// <summary>
            /// Records the completed tick's automatic-save preferences without writing.
            /// </summary>
            /// <param name="settings">The preferences supplied by the runtime.</param>
            public void ProcessAutosaveTick(UserGameplaySettings settings)
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
    }
}
