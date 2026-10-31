using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.UIState;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class LocalPlayerPresentationStateTests
    {
        private const string _hostFactionID = "FNALL1";
        private const string _guestFactionID = "FNEMP1";

        [Test]
        public void TryRestore_SameOwnedCampaign_PreservesLiveLocalSectionsWithoutReplacingHostState()
        {
            GameRoot local = CreateGame("current-local");
            PlayerUIState localState = local.GetFactionPlayer(_guestFactionID).UIState;
            LocalPlayerPresentationState retained = LocalPlayerPresentationState.Capture(
                local,
                _guestFactionID
            );
            localState.GetOrCreateSection("AdvisorNotifications").Values["deadline"] = "150";
            GameRoot incoming = CreateGame("stale-host-copy");
            incoming.CurrentTick = 120;
            incoming.Metadata.SharedSaveToken = Guid.NewGuid().ToString("N");
            PlayerUIState hostState = incoming.GetFactionPlayer(_hostFactionID).UIState;

            bool restored = retained.TryRestore(incoming, _guestFactionID);

            Assert.IsTrue(restored);
            Assert.AreSame(localState, incoming.GetFactionPlayer(_guestFactionID).UIState);
            Assert.AreSame(hostState, incoming.GetFactionPlayer(_hostFactionID).UIState);
            Assert.AreEqual(
                "150",
                incoming
                    .GetFactionPlayer(_guestFactionID)
                    .UIState.GetOrCreateSection("AdvisorNotifications")
                    .Values["deadline"]
            );
            Assert.AreEqual(120, incoming.CurrentTick);
        }

        [TestCase("campaign")]
        [TestCase("host-account")]
        [TestCase("guest-account")]
        [TestCase("host-faction")]
        [TestCase("guest-faction")]
        [TestCase("pack")]
        [TestCase("version")]
        [TestCase("scenario")]
        [TestCase("mods")]
        [TestCase("mod-version")]
        [TestCase("local-faction")]
        public void TryRestore_OwnershipOrContentChanged_LeavesAuthoritativePresentation(
            string changed
        )
        {
            LocalPlayerPresentationState retained = LocalPlayerPresentationState.Capture(
                CreateGame("local"),
                _guestFactionID
            );
            GameRoot incoming = CreateGame("authoritative");
            PlayerUIState authoritative = incoming.GetFactionPlayer(_guestFactionID).UIState;
            string localFactionID = _guestFactionID;
            switch (changed)
            {
                case "campaign":
                    incoming.Metadata.CampaignID = Guid.NewGuid().ToString("N");
                    break;
                case "host-account":
                    incoming.Metadata.MultiplayerCampaign.HostUserID = "other-host";
                    break;
                case "guest-account":
                    incoming.Metadata.MultiplayerCampaign.GuestUserID = "other-guest";
                    break;
                case "host-faction":
                    incoming.Metadata.MultiplayerCampaign.HostFactionID = "other-host-faction";
                    break;
                case "guest-faction":
                    incoming.Metadata.MultiplayerCampaign.GuestFactionID = "other-guest-faction";
                    break;
                case "pack":
                    incoming.Summary.PackID = "other-pack";
                    break;
                case "version":
                    incoming.Summary.PackVersion = "other-version";
                    break;
                case "scenario":
                    incoming.Summary.ScenarioID = "other-scenario";
                    break;
                case "mods":
                    incoming.Summary.ModIDs = new[] { "other-mod" };
                    break;
                case "mod-version":
                    incoming.Summary.ModVersions = new[] { "other-mod-version" };
                    break;
                case "local-faction":
                    localFactionID = _hostFactionID;
                    break;
            }

            Assert.IsFalse(retained.TryRestore(incoming, localFactionID));
            Assert.AreSame(authoritative, incoming.GetFactionPlayer(_guestFactionID).UIState);
        }

        [Test]
        public void Capture_UnadmittedGeneration_DoesNotReuseAnEarlierCampaignPresentation()
        {
            GameRoot generated = CreateGame("generated");
            generated.Metadata.MultiplayerCampaign = null;

            Assert.IsNull(LocalPlayerPresentationState.Capture(generated, _guestFactionID));
        }

        [Test]
        public void TryRestoreSavedCheckpoint_MatchingCopy_UsesSavedLocalStateWithoutChangingWorld()
        {
            GameRoot hostCheckpoint = CreateGame("stale-guest-at-save");
            hostCheckpoint.CurrentTick = 40;
            GameRoot localCheckpoint = CreateGame("guest-at-save");
            PlayerUIState savedLocal = localCheckpoint.GetFactionPlayer(_guestFactionID).UIState;
            PlayerUIState hostState = hostCheckpoint.GetFactionPlayer(_hostFactionID).UIState;

            Assert.IsTrue(
                LocalPlayerPresentationState.TryRestoreSavedCheckpoint(
                    hostCheckpoint,
                    localCheckpoint,
                    _guestFactionID
                )
            );

            Assert.AreSame(savedLocal, hostCheckpoint.GetFactionPlayer(_guestFactionID).UIState);
            Assert.AreSame(hostState, hostCheckpoint.GetFactionPlayer(_hostFactionID).UIState);
            Assert.AreEqual(40, hostCheckpoint.CurrentTick);
        }

        [TestCase("missing-token")]
        [TestCase("different-token")]
        [TestCase("different-campaign")]
        [TestCase("different-owner")]
        [TestCase("different-content")]
        [TestCase("different-content-metadata")]
        public void TryRestoreSavedCheckpoint_UnmatchedCopy_DoesNotImportFutureOrForeignUI(
            string changed
        )
        {
            GameRoot authoritative = CreateGame("authoritative-saved");
            GameRoot localCopy = CreateGame("future-or-foreign");
            PlayerUIState authoritativeState = authoritative
                .GetFactionPlayer(_guestFactionID)
                .UIState;
            switch (changed)
            {
                case "missing-token":
                    authoritative.Metadata.SharedSaveToken = null;
                    break;
                case "different-token":
                    localCopy.Metadata.SharedSaveToken = Guid.NewGuid().ToString("N");
                    break;
                case "different-campaign":
                    localCopy.Metadata.CampaignID = Guid.NewGuid().ToString("N");
                    break;
                case "different-owner":
                    localCopy.Metadata.MultiplayerCampaign.GuestUserID = "other-guest";
                    break;
                case "different-content":
                    localCopy.Summary.PackVersion = "other-version";
                    break;
                case "different-content-metadata":
                    localCopy.Metadata.PackVersion = "other-version";
                    break;
            }

            Assert.IsFalse(
                LocalPlayerPresentationState.TryRestoreSavedCheckpoint(
                    authoritative,
                    localCopy,
                    _guestFactionID
                )
            );
            Assert.AreSame(
                authoritativeState,
                authoritative.GetFactionPlayer(_guestFactionID).UIState
            );
        }

        /// <summary>
        /// Creates two explicitly owned participants and distinct local presentation sections.
        /// </summary>
        /// <param name="guestMarker">The guest presentation marker.</param>
        /// <returns>The minimal admitted campaign.</returns>
        private static GameRoot CreateGame(string guestMarker)
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary
                {
                    PackID = "pack",
                    PackVersion = "1",
                    ScenarioID = "scenario",
                },
                Metadata = new GameMetadata
                {
                    CampaignID = "11111111111111111111111111111111",
                    SharedSaveToken = "22222222222222222222222222222222",
                    PackID = "pack",
                    PackVersion = "1",
                    ScenarioID = "scenario",
                    MultiplayerCampaign = new MultiplayerCampaignIdentity
                    {
                        HostUserID = "host-account",
                        GuestUserID = "guest-account",
                        HostFactionID = _hostFactionID,
                        GuestFactionID = _guestFactionID,
                    },
                },
                LocalPlayerFactionID = _guestFactionID,
            };
            game.SetFactionController(_hostFactionID, "host", PlayerControllerType.Human);
            game.SetFactionController(_guestFactionID, "guest", PlayerControllerType.Human);
            game.GetFactionPlayer(_hostFactionID).UIState.GetOrCreateSection("Strategy").Values[
                "marker"
            ] = "host";
            game.GetFactionPlayer(_guestFactionID).UIState.GetOrCreateSection("Strategy").Values[
                "marker"
            ] = guestMarker;
            return game;
        }
    }
}
