using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerCoordinatorTests
    {
        private GameObject _owner;
        private MultiplayerCoordinator _coordinator;
        private bool _previousRunInBackground;

        /// <summary>
        /// Creates an isolated coordinator for public contract tests.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _previousRunInBackground = Application.runInBackground;
            _owner = new GameObject("Multiplayer coordinator test");
            _coordinator = _owner.AddComponent<MultiplayerCoordinator>();
        }

        /// <summary>
        /// Releases the coordinator object after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_owner);
            Application.runInBackground = _previousRunInBackground;
        }

        [Test]
        public void OpenBrowser_BackgroundDisabled_KeepsOnlineCallbacksRunning()
        {
            Application.runInBackground = false;
            _coordinator.OpenBrowser();
            Assert.That(Application.runInBackground, Is.True);
        }

        [Test]
        public void CloseBrowser_AfterDisconnect_RestoresPreviousBackgroundPreference()
        {
            Application.runInBackground = false;
            _coordinator.OpenBrowser();
            _coordinator.Disconnect();
            _coordinator.CloseBrowser();
            Assert.That(Application.runInBackground, Is.False);
        }

        [Test]
        public void OpenBrowser_RepeatedCalls_PreservesOriginalBackgroundPreference()
        {
            Application.runInBackground = false;
            _coordinator.OpenBrowser();
            _coordinator.OpenBrowser();
            _coordinator.CloseBrowser();
            Assert.That(Application.runInBackground, Is.False);
        }

        [Test]
        public void IsCompatible_MatchingEpicContent_ReturnsTrue()
        {
            MatchmakerGame game = new MatchmakerGame
            {
                transport = EpicLobbyListing.Transport,
                contentFingerprint = EpicLobbyListing.HashContent("pack|1|scenario|"),
            };
            Assert.That(_coordinator.IsCompatible(game, CreateSummary()), Is.True);
        }

        [Test]
        public void IsCompatible_DifferentContentVersion_ReturnsFalse()
        {
            MatchmakerGame game = new MatchmakerGame
            {
                transport = EpicLobbyListing.Transport,
                contentFingerprint = EpicLobbyListing.HashContent("pack|2|scenario|"),
            };
            Assert.That(_coordinator.IsCompatible(game, CreateSummary()), Is.False);
        }

        [Test]
        public void IsCompatible_UnsupportedTransport_ReturnsFalse()
        {
            MatchmakerGame game = new MatchmakerGame
            {
                transport = "direct-tcp",
                contentFingerprint = EpicLobbyListing.HashContent("pack|1|scenario|"),
            };
            Assert.That(_coordinator.IsCompatible(game, CreateSummary()), Is.False);
        }

        [Test]
        public void Disconnect_DestroyedCoordinator_DoesNotTouchDisposedSession()
        {
            UnityEngine.Object.DestroyImmediate(_coordinator.gameObject);
            Assert.DoesNotThrow(_coordinator.Disconnect);
        }

        [Test]
        public void Disconnect_PendingSessionEnd_RetainsNoticeUntilAcknowledged()
        {
            _coordinator.SessionEndNotice.ShowOpponentDeparture();

            _coordinator.Disconnect();

            Assert.IsTrue(_coordinator.SessionEndNotice.IsPending);
        }

        [Test]
        public void Disconnect_AcknowledgedSessionEnd_DoesNotReopenNotice()
        {
            _coordinator.SessionEndNotice.ShowOpponentDeparture();
            _coordinator.SessionEndNotice.Acknowledge();

            _coordinator.Disconnect();

            Assert.IsFalse(_coordinator.SessionEndNotice.IsPending);
        }

        [Test]
        public void ReportGameplayFailure_StateMismatch_PublishesStoppedSessionNotice()
        {
            InvalidOperationException failure = new("Simulation state mismatch.");
            string observedStatus = null;
            _coordinator.StateChanged += () => observedStatus = _coordinator.Status;
            LogAssert.Expect(LogType.Exception, new Regex("Simulation state mismatch\\."));

            _coordinator.ReportGameplayFailure(failure);

            Assert.AreSame(failure, _coordinator.Failure);
            Assert.IsFalse(_coordinator.IsSynchronized);
            Assert.AreEqual("Game synchronization stopped.", observedStatus);
        }

        [Test]
        public async Task HostAsync_InvalidPlayableFactions_ReportsFailureBeforeOnlineLoginAsync()
        {
            await _coordinator.HostAsync("Player", "Game", CreateSummary());
            Assert.That(_coordinator.Role, Is.EqualTo(MultiplayerRole.None));
            Assert.That(_coordinator.Status, Does.Contain("two distinct playable factions"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task HostAsync_FailedSetup_RestoresBackgroundSettingAsync(bool runInBackground)
        {
            Application.runInBackground = runInBackground;
            await _coordinator.HostAsync("Player", "Game", CreateSummary());
            Assert.That(Application.runInBackground, Is.EqualTo(runInBackground));
        }

        [Test]
        public void JoinAsync_NullListing_RejectsRequest()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _coordinator.JoinAsync("Player", null)
            );
        }

        /// <summary>
        /// Creates the shared content identity used by the contract tests.
        /// </summary>
        /// <returns>The launch summary.</returns>
        private static GameSummary CreateSummary()
        {
            return new GameSummary
            {
                PackID = "pack",
                PackVersion = "1",
                ScenarioID = "scenario",
                ModIDs = Array.Empty<string>(),
                ModVersions = Array.Empty<string>(),
            };
        }
    }
}
