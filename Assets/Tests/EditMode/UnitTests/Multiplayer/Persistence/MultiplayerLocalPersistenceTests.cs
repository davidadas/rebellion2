using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.App;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.PlayerRequests;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class MultiplayerLocalPersistenceTests
    {
        private string _directory;
        private SaveGameManager _store;
        private GameRuntime _runtime;
        private int _publications;

        /// <summary>
        /// Creates an isolated runtime and save directory.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                nameof(MultiplayerLocalPersistenceTests),
                Guid.NewGuid().ToString("N")
            );
            _store = new SaveGameManager(_directory);
            _runtime = new GameRuntime(TestContent.Pack, _store);
            _publications = 0;
        }

        /// <summary>
        /// Releases the runtime and its test files.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _runtime.EndGame();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public async Task SaveGameAsync_PeerAbsent_PublishesOwnedCopyWithCampaignBindingsAsync(
            MultiplayerRole role
        )
        {
            GameRoot game = StartWaitingGame(role);
            string previousToken = game.Metadata.SharedSaveToken;

            Assert.IsTrue(_runtime.CanRequestSave);
            Assert.IsTrue(await _runtime.SaveGameAsync("survivor", "Surviving campaign"));

            string fileName = SaveGameManager.GetMultiplayerSaveFileName(
                game.Metadata.CampaignID,
                role == MultiplayerRole.Host,
                "survivor"
            );
            GameRoot saved = _store.LoadGameData(fileName);
            Assert.AreEqual("Surviving campaign", saved.Metadata.SaveDisplayName);
            Assert.AreEqual(game.CurrentTick, saved.CurrentTick);
            Assert.AreEqual(game.Metadata.CampaignID, saved.Metadata.CampaignID);
            Assert.AreEqual("host-account", saved.Metadata.MultiplayerCampaign.HostUserID);
            Assert.AreEqual("guest-account", saved.Metadata.MultiplayerCampaign.GuestUserID);
            Assert.AreEqual("FNALL1", saved.Metadata.MultiplayerCampaign.HostFactionID);
            Assert.AreEqual("FNEMP1", saved.Metadata.MultiplayerCampaign.GuestFactionID);
            Assert.AreNotEqual(previousToken, saved.Metadata.SharedSaveToken);
            Assert.AreEqual(TickSpeed.Paused, saved.GameSpeed);
            Assert.AreEqual(1, _publications);
            Assert.AreEqual(1, _store.GetSavedGames(role == MultiplayerRole.Host).Count);
            Assert.IsEmpty(_store.GetSavedGames(role != MultiplayerRole.Host));
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public async Task QuickSaveAsync_PeerAbsent_UsesParticipantSlotAndDefaultLabelAsync(
            MultiplayerRole role
        )
        {
            GameRoot game = StartWaitingGame(role);

            Assert.IsTrue(await _runtime.QuickSaveAsync());

            string fileName = SaveGameManager.GetMultiplayerSaveFileName(
                game.Metadata.CampaignID,
                role == MultiplayerRole.Host,
                SaveGameManager.QuickSaveFileName
            );
            Assert.AreEqual("Quicksave", _store.LoadGameData(fileName).Metadata.SaveDisplayName);
        }

        [Test]
        public async Task SaveGameAsync_ExistingPeerFilename_OverwritesOnlySurvivorNamespaceAsync()
        {
            GameRoot game = StartWaitingGame(MultiplayerRole.Guest);
            string hostName = SaveGameManager.GetMultiplayerSaveFileName(
                game.Metadata.CampaignID,
                true,
                "existing"
            );
            _store.SaveGameData(game, hostName, "Host history");
            byte[] hostBytes = File.ReadAllBytes(_store.GetSaveFilePath(hostName));

            Assert.IsTrue(await _runtime.SaveGameAsync(hostName, "Guest history"));

            CollectionAssert.AreEqual(
                hostBytes,
                File.ReadAllBytes(_store.GetSaveFilePath(hostName))
            );
            string guestName = SaveGameManager.GetMultiplayerSaveFileName(
                game.Metadata.CampaignID,
                false,
                "existing"
            );
            Assert.AreEqual(
                "Guest history",
                _store.LoadGameData(guestName).Metadata.SaveDisplayName
            );
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public async Task SaveGameAsync_OperationsBlocked_RejectsWithoutChangingTokenAsync(
            MultiplayerRole role
        )
        {
            GameRoot game = StartWaitingGame(role);
            string token = game.Metadata.SharedSaveToken;
            _runtime.BlockGameOperations();

            Assert.IsFalse(_runtime.CanRequestSave);
            Assert.IsFalse(await _runtime.SaveGameAsync("unsafe", "Unsafe"));
            Assert.IsEmpty(_store.GetSavedGames());
            Assert.AreEqual(0, _publications);
            Assert.AreEqual(token, game.Metadata.SharedSaveToken);
        }

        [Test]
        public async Task SaveGameAsync_CommandExecuting_RejectsWithoutPublicationAsync()
        {
            StartWaitingGame(MultiplayerRole.Host);
            Task<bool> save = null;
            await _runtime
                .GetActiveGameSession()
                .ExecuteCommandAsync(() =>
                {
                    Assert.IsFalse(_runtime.CanRequestSave);
                    save = _runtime.SaveGameAsync("unsafe", "Unsafe");
                    return true;
                });

            Assert.IsFalse(await save);
            Assert.IsEmpty(_store.GetSavedGames());
            Assert.AreEqual(0, _publications);
        }

        [Test]
        public async Task LoadGameAsync_PeerAbsent_DoesNotReplaceCampaignAsync()
        {
            GameRoot game = StartWaitingGame(MultiplayerRole.Guest);
            Assert.IsTrue(await _runtime.SaveGameAsync("survivor", "Saved"));

            Assert.IsFalse(await _runtime.LoadGameAsync("survivor"));
            Assert.IsFalse(await _runtime.QuickLoadAsync());
            Assert.AreSame(game, _runtime.GetActiveGame());
        }

        [Test]
        public async Task SaveGameAsync_SessionReplacedDuringPublication_DoesNotRefreshNewRecoveryAsync()
        {
            StartWaitingGame(MultiplayerRole.Host);
            _store.Saving += () => StartWaitingGame(MultiplayerRole.Guest);

            Assert.IsTrue(await _runtime.SaveGameAsync("outgoing", "Outgoing"));
            Assert.AreEqual(0, _publications);
        }

        [Test]
        public async Task SaveGameAsync_PublicationFails_DoesNotReportCompletedCheckpointAsync()
        {
            GameRoot game = StartWaitingGame(MultiplayerRole.Host);
            string fileName = SaveGameManager.GetMultiplayerSaveFileName(
                game.Metadata.CampaignID,
                true,
                "blocked"
            );
            Directory.CreateDirectory(_store.GetSaveFilePath(fileName));

            LogAssert.Expect(
                LogType.Exception,
                new Regex("IOException|UnauthorizedAccessException")
            );

            Assert.IsFalse(await _runtime.SaveGameAsync("blocked", "Blocked"));
            Assert.AreEqual(0, _publications);
        }

        /// <summary>
        /// Starts a paused campaign with multiplayer execution and survivor-local persistence.
        /// </summary>
        /// <param name="role">The surviving participant's original role.</param>
        /// <returns>The active campaign.</returns>
        private GameRoot StartWaitingGame(MultiplayerRole role)
        {
            ContentPack content = TestContent.Pack;
            GameRoot game = new()
            {
                Summary = new GameSummary
                {
                    PackID = content.Definition.ID,
                    PackVersion = content.Definition.Version,
                    ScenarioID = content.Scenario.ID,
                },
                Metadata = new GameMetadata
                {
                    CampaignID = Guid.NewGuid().ToString("N"),
                    SharedSaveToken = "previous-shared-checkpoint",
                    MultiplayerCampaign = new MultiplayerCampaignIdentity
                    {
                        HostUserID = "host-account",
                        GuestUserID = "guest-account",
                        HostFactionID = "FNALL1",
                        GuestFactionID = "FNEMP1",
                    },
                },
            };
            game.GetFactions().Add(new Faction { InstanceID = "FNALL1" });
            game.GetFactions().Add(new Faction { InstanceID = "FNEMP1" });
            game.SetFactionController("FNALL1", "host", PlayerControllerType.Human);
            game.SetFactionController("FNEMP1", "guest", PlayerControllerType.Human);
            game.GameSpeed = TickSpeed.Paused;
            _runtime.StartGame(game, new WaitingSessionOwner());
            _runtime.BlockGameOperations();
            _runtime.BindPersistenceCoordinator(
                new MultiplayerLocalPersistence(_runtime, role, () => _publications++)
            );
            _runtime.ResumeMultiplayerOperations();
            return game;
        }

        private sealed class WaitingSessionOwner : IGameSessionOwner
        {
            public bool CanRequestSave => false;

            /// <summary>
            /// Retains the fixture's configured participants.
            /// </summary>
            /// <param name="game">The prepared campaign.</param>
            public void PrepareGame(GameRoot game) { }

            /// <summary>
            /// Leaves storage ownership to the local persistence collaborator.
            /// </summary>
            /// <param name="runtime">The attached runtime.</param>
            public void BindRuntime(GameRuntime runtime) { }

            /// <summary>
            /// Keeps gameplay blocked when presentation becomes ready.
            /// </summary>
            /// <param name="runtime">The ready runtime.</param>
            public void NotifyPresentationReady(GameRuntime runtime) { }

            /// <summary>
            /// Releases a fixture without transport work.
            /// </summary>
            /// <param name="runtime">The outgoing runtime.</param>
            public void ReleaseRuntime(GameRuntime runtime) { }

            /// <summary>
            /// Keeps the waiting campaign's clock stopped.
            /// </summary>
            /// <param name="elapsedSeconds">The elapsed frame interval.</param>
            /// <returns>False while the peer is absent.</returns>
            public bool TryBeginGameTick(float elapsedSeconds) => false;

            /// <summary>
            /// Rejects orders while the peer is absent.
            /// </summary>
            /// <param name="action">The requested player action.</param>
            /// <returns>The rejected action result.</returns>
            public Task<PlayerRequestOutcome> ExecuteAsync(PlayerRequest action) =>
                TestPlayerRequests.RejectAll().ExecuteAsync(action);

            /// <summary>
            /// Leaves writes to the separately assigned local persistence owner.
            /// </summary>
            /// <param name="fileName">The requested slot.</param>
            /// <param name="displayName">The requested label.</param>
            /// <returns>False without shared persistence.</returns>
            public Task<bool> SaveGameAsync(string fileName, string displayName) =>
                Task.FromResult(false);

            /// <summary>
            /// Rejects independent scene replacement.
            /// </summary>
            /// <param name="fileName">The requested slot.</param>
            /// <returns>False while the peer is absent.</returns>
            public Task<bool> LoadGameAsync(string fileName) => Task.FromResult(false);

            /// <summary>
            /// Leaves automatic recovery outside the fixture clock.
            /// </summary>
            /// <param name="settings">The automatic-save preferences.</param>
            public void ProcessAutosaveTick(UserGameplaySettings settings) { }
        }
    }
}
