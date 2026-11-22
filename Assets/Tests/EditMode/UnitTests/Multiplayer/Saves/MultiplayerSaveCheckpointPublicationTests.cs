using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class MultiplayerSaveCheckpointPublicationTests
    {
        private string _directory;
        private SaveGameManager _store;
        private GameSessionManager _sessionManager;

        /// <summary>
        /// Creates an isolated save store for each publication test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                nameof(MultiplayerSaveCheckpointPublicationTests),
                Guid.NewGuid().ToString("N")
            );
            _store = new SaveGameManager(_directory);
        }

        /// <summary>
        /// Releases the active session and its temporary save files.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _sessionManager?.EndGame();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public async Task CompleteHostSaveAsync_PresentationPending_SerializesPublishedMetadataAsync()
        {
            TaskCompletionSource<bool> presented = new();
            _sessionManager = new GameSessionManager(
                TestContent.Pack,
                _store,
                presentSaveIndicator: () => presented.Task
            );
            GameRoot game = CreateGame();
            _sessionManager.StartGame(game);
            Task<bool> publication = _sessionManager.SaveGameLocallyAsync(
                "multiplayer-host-checkpoint",
                "Checkpoint"
            );
            Task<MultiplayerSaveCheckpoint> completion =
                MultiplayerSaveCheckpointPublication.CompleteHostSaveAsync(
                    new MultiplayerSaveRequest(),
                    game.Metadata,
                    publication,
                    () => true,
                    CancellationToken.None
                );

            Assert.IsFalse(completion.IsCompleted);
            Assert.AreEqual(7, game.Metadata.CurrentTick);
            presented.SetResult(true);
            MultiplayerSaveCheckpoint checkpoint = await completion;
            GameMetadata published = (GameMetadata)
                MultiplayerSnapshotCodec.Deserialize(typeof(GameMetadata), checkpoint.Payload);

            Assert.AreEqual(42, published.CurrentTick);
        }

        /// <summary>
        /// Creates a campaign whose previously published metadata trails its current simulation tick.
        /// </summary>
        /// <returns>The campaign used to verify metadata publication ordering.</returns>
        private static GameRoot CreateGame()
        {
            ContentPack content = TestContent.Pack;
            return new GameRoot
            {
                CurrentTick = 42,
                Summary = new GameSummary
                {
                    PackID = content.Definition.ID,
                    PackVersion = content.Definition.Version,
                    ScenarioID = content.Scenario.ID,
                },
                Metadata = new GameMetadata
                {
                    CampaignID = Guid.NewGuid().ToString("N"),
                    CurrentTick = 7,
                },
            };
        }
    }
}
