using System;
using System.IO;
using System.Linq;
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
            _coordinator.Initialize(
                () => TestContent.Pack,
                new SaveGameManager(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
                () => null,
                () => false,
                (_, _) => { },
                _ => false
            );
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
        public void Initialize_AlreadyOwned_RejectsReplacingApplicationServices()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _coordinator.Initialize(
                    () => TestContent.Pack,
                    new SaveGameManager(Path.GetTempPath()),
                    () => null,
                    () => false,
                    (_, _) => { },
                    _ => false
                )
            );
        }

        [Test]
        public async Task CheckForRecoveryAsync_NoCheckpoint_LeavesCoordinatorOfflineAsync()
        {
            await _coordinator.CheckForRecoveryAsync();

            Assert.IsFalse(_coordinator.HasRecovery);
            Assert.AreEqual("Offline", _coordinator.Status);
        }

        [Test]
        public void DiscardRecovery_NoCheckpoint_LeavesCoordinatorOffline()
        {
            Assert.DoesNotThrow(_coordinator.DiscardRecovery);
            Assert.IsFalse(_coordinator.HasRecovery);
            Assert.AreEqual("Offline", _coordinator.Status);
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
            MultiplayerGameListing game = new MultiplayerGameListing
            {
                transport = EpicLobbyListing.Transport,
                contentFingerprint = EpicLobbyListing.HashContent("pack|1|scenario|"),
            };
            Assert.That(_coordinator.IsCompatible(game, CreateSummary()), Is.True);
        }

        [Test]
        public void IsCompatible_DifferentContentVersion_ReturnsFalse()
        {
            MultiplayerGameListing game = new MultiplayerGameListing
            {
                transport = EpicLobbyListing.Transport,
                contentFingerprint = EpicLobbyListing.HashContent("pack|2|scenario|"),
            };
            Assert.That(_coordinator.IsCompatible(game, CreateSummary()), Is.False);
        }

        [Test]
        public void IsCompatible_ClientOnlyThemeDiffers_AcceptsSharedGameplay()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "multiplayer-mods-" + Guid.NewGuid().ToString("N")
            );
            try
            {
                ContentPack host = TestPackFixture.Install(Path.Combine(directory, "Content"));
                string mod = Path.Combine(directory, "Mods", "local");
                string theme = Path.Combine(
                    mod,
                    "Content",
                    "Pack",
                    "Factions",
                    "Empire",
                    "Strategy",
                    "UI"
                );
                Directory.CreateDirectory(theme);
                File.WriteAllText(
                    Path.Combine(mod, "mod.xml"),
                    $"<ContentModDefinition><ID>local</ID><Version>1.0.0</Version><DisplayName>Local UI</DisplayName><BasePackID>{host.Definition.ID}</BasePackID><BasePackVersion>{host.Definition.Version}</BasePackVersion><ClientOnly>true</ClientOnly></ContentModDefinition>"
                );
                File.WriteAllText(
                    Path.Combine(theme, "theme.xml"),
                    "<FactionThemes Inherit='true'><FactionTheme><FactionInstanceID>FNEMP1</FactionInstanceID><FactionPrimaryColorHex>ABCDEF</FactionPrimaryColorHex></FactionTheme></FactionThemes>"
                );
                ContentPack guest = ContentPackLoader.OpenExact(
                    host.ContentRootPath,
                    host.Definition.ID,
                    host.Scenario.ID,
                    null
                );
                GameSummary summary = host.CreateDefaultGameSummary();
                MultiplayerGameListing listing = new()
                {
                    transport = EpicLobbyListing.Transport,
                    contentFingerprint = EpicLobbyListing.HashContent(
                        $"{summary.PackID}|{summary.PackVersion}|{summary.ScenarioID}|"
                    ),
                };

                Assert.That(
                    _coordinator.IsCompatible(listing, guest.CreateDefaultGameSummary()),
                    Is.True
                );
                Assert.DoesNotThrow(() =>
                    MultiplayerCompatibility.ValidateContent(
                        GameplayContentIdentity.Compute(host.GameData),
                        GameplayContentIdentity.Compute(guest.GameData)
                    )
                );
                Assert.That(
                    guest
                        .GameData.FactionThemes.Single(item => item.FactionInstanceID == "FNEMP1")
                        .FactionPrimaryColorHex,
                    Is.EqualTo("ABCDEF")
                );
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void IsCompatible_UnsupportedTransport_ReturnsFalse()
        {
            MultiplayerGameListing game = new MultiplayerGameListing
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
            LogAssert.Expect(LogType.Error, new Regex("Simulation state mismatch\\."));

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
