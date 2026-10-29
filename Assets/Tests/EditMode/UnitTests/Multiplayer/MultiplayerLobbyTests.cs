using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;
using UnityEngine;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerLobbyTests
    {
        [Test]
        public void BeginLaunch_OnlyHostReady_RejectsLaunch()
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.SetReady(MultiplayerRole.Host, true, lobby.State.settingsRevision);
            Assert.Throws<InvalidOperationException>(() => lobby.BeginLaunch());
        }

        [Test]
        public void BeginLaunch_GuestJoins_DoesNotLaunchAutomatically()
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.Join("Guest");
            Assert.That(lobby.State.CanLaunch, Is.False);
            Assert.That(lobby.State.launching, Is.False);
            Assert.That(lobby.State.hostReady, Is.False);
            Assert.That(lobby.State.guestReady, Is.False);
        }

        [Test]
        public void BeginLaunch_BothPlayersReady_LocksConfiguration()
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            lobby.BeginLaunch();
            Assert.That(lobby.State.launching, Is.True);
            Assert.Throws<InvalidOperationException>(() =>
                lobby.SelectFaction(MultiplayerRole.Host, "alliance")
            );
        }

        [Test]
        public void SelectFaction_AfterBothReady_ClearsBothApprovals()
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            lobby.SelectFaction(MultiplayerRole.Host, "alliance");
            Assert.That(lobby.State.hostReady, Is.False);
            Assert.That(lobby.State.guestReady, Is.False);
            Assert.That(lobby.State.CanLaunch, Is.False);
        }

        [Test]
        public void SelectFaction_GuestSelection_PreservesHostChoice()
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.Join("Guest");
            lobby.SelectFaction(MultiplayerRole.Guest, "empire");
            Assert.That(lobby.State.hostFactionID, Is.EqualTo("empire"));
            Assert.That(lobby.State.guestFactionID, Is.EqualTo("empire"));
        }

        [Test]
        public void SetReady_SameFaction_RejectsApproval()
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.Join("Guest");
            lobby.SelectFaction(MultiplayerRole.Guest, "empire");
            Assert.Throws<InvalidOperationException>(() =>
                lobby.SetReady(MultiplayerRole.Host, true, lobby.State.settingsRevision)
            );
        }

        [Test]
        public void SetReady_GuestContentUnverified_RejectsApproval()
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.Join("Guest");
            Assert.Throws<InvalidOperationException>(() =>
                lobby.SetReady(MultiplayerRole.Guest, true, lobby.State.settingsRevision)
            );
        }

        [Test]
        public void SetReady_StaleSettings_RejectsApproval()
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            long revision = lobby.State.settingsRevision;
            lobby.SelectFaction(MultiplayerRole.Host, "alliance");
            Assert.Throws<InvalidOperationException>(() =>
                lobby.SetReady(MultiplayerRole.Guest, true, revision)
            );
        }

        [Test]
        public void ConfirmContent_StaleSettings_DoesNotApproveCurrentContent()
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            long revision = lobby.State.settingsRevision;
            lobby.SelectSave(CreateSummary(), "Saved campaign");
            lobby.ConfirmContent(revision);
            Assert.That(lobby.State.guestContentReady, Is.False);
        }

        [Test]
        public void SelectSave_AfterBothReady_ClearsBothApprovals()
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            lobby.SelectSave(CreateSummary(), "Saved campaign");
            Assert.That(lobby.State.saveName, Is.EqualTo("Saved campaign"));
            Assert.That(lobby.State.hostReady, Is.False);
            Assert.That(lobby.State.guestReady, Is.False);
        }

        [Test]
        public void RemoveGuest_AfterBothReady_ReopensSlotAndClearsHostApproval()
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            lobby.RemoveGuest();
            Assert.That(lobby.State.guestName, Is.Null);
            Assert.That(lobby.State.hostReady, Is.False);
            Assert.That(lobby.State.CanLaunch, Is.False);
        }

        [Test]
        public void State_MutatingReturnedValue_DoesNotChangeAuthoritativeRoster()
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.State.hostReady = true;
            Assert.That(lobby.State.hostReady, Is.False);
        }

        [Test]
        public void SetReady_ConcurrentApprovalsForSameSettings_AcceptsBoth()
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.Join("Guest");
            long revision = lobby.State.settingsRevision;
            lobby.ConfirmContent(revision);
            lobby.SetReady(MultiplayerRole.Host, true, revision);
            lobby.SetReady(MultiplayerRole.Guest, true, revision);
            Assert.That(lobby.State.CanLaunch, Is.True);
        }

        /// <summary>
        /// Applies the saved ownership pair when selecting an authenticated multiplayer campaign.
        /// </summary>
        [Test]
        public void SelectSave_MultiplayerCampaign_AppliesSavedFactionPair()
        {
            MultiplayerLobby lobby = CreateLobby();

            lobby.SelectSave(CreateSummary(), "Saved campaign", CreateIdentity(), "host");

            Assert.That(lobby.State.hostFactionID, Is.EqualTo("alliance"));
            Assert.That(lobby.State.guestFactionID, Is.EqualTo("empire"));
            Assert.That(lobby.State.hasSavedFactionBinding, Is.True);
            Assert.That(lobby.State.HasValidFactionSelection, Is.True);
        }

        /// <summary>
        /// Rejects another host's save before changing the selected campaign or its roster.
        /// </summary>
        [Test]
        public void SelectSave_WrongHostBeforeGuestJoins_PreservesSelection()
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.SelectSave(CreateSummary(), "Current campaign");
            string before = JsonUtility.ToJson(lobby.State);

            Assert.Throws<InvalidOperationException>(() =>
                lobby.SelectSave(CreateSummary(), "Other campaign", CreateIdentity(), "other-host")
            );

            Assert.That(JsonUtility.ToJson(lobby.State), Is.EqualTo(before));
        }

        /// <summary>
        /// Rechecks authenticated participants when selecting a save after the guest has joined.
        /// </summary>
        [TestCase("other-host", "guest")]
        [TestCase("host", "other-guest")]
        public void SelectSave_AdmittedParticipantMismatch_PreservesSelection(
            string hostUserID,
            string guestUserID
        )
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            string before = JsonUtility.ToJson(lobby.State);

            Assert.Throws<InvalidOperationException>(() =>
                lobby.SelectSave(
                    CreateSummary(),
                    "Other campaign",
                    CreateIdentity(),
                    hostUserID,
                    guestUserID
                )
            );

            Assert.That(JsonUtility.ToJson(lobby.State), Is.EqualTo(before));
        }

        /// <summary>
        /// Allows each player to change their own choice while rejecting a swapped saved pair.
        /// </summary>
        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void SetReady_SavedFactionPairChanged_RejectsApproval(MultiplayerRole role)
        {
            MultiplayerLobby lobby = CreateSavedLobby();
            lobby.SelectFaction(MultiplayerRole.Host, "empire");
            lobby.SelectFaction(MultiplayerRole.Guest, "alliance");
            lobby.ConfirmContent(lobby.State.settingsRevision);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                lobby.SetReady(role, true, lobby.State.settingsRevision)
            );

            Assert.That(exception.Message, Is.EqualTo(lobby.State.FactionSelectionError));
            Assert.That(lobby.State.hostFactionID, Is.EqualTo("empire"));
            Assert.That(lobby.State.guestFactionID, Is.EqualTo("alliance"));
            Assert.That(lobby.State.CanLaunch, Is.False);
        }

        /// <summary>
        /// Restores normal approval after both players select their saved faction again.
        /// </summary>
        [Test]
        public void SetReady_SavedFactionPairRestored_AllowsLaunch()
        {
            MultiplayerLobby lobby = CreateSavedLobby();
            lobby.SelectFaction(MultiplayerRole.Host, "empire");
            lobby.SelectFaction(MultiplayerRole.Guest, "alliance");
            lobby.SelectFaction(MultiplayerRole.Host, "alliance");
            lobby.SelectFaction(MultiplayerRole.Guest, "empire");
            long revision = lobby.State.settingsRevision;
            lobby.ConfirmContent(revision);
            lobby.SetReady(MultiplayerRole.Host, true, revision);
            lobby.SetReady(MultiplayerRole.Guest, true, revision);

            lobby.BeginLaunch();

            Assert.That(lobby.State.launching, Is.True);
        }

        /// <summary>
        /// Keeps launch state unchanged and explains mismatched saved factions.
        /// </summary>
        [Test]
        public void BeginLaunch_SavedFactionPairChanged_PreservesWaitingState()
        {
            MultiplayerLobby lobby = CreateSavedLobby();
            lobby.SelectFaction(MultiplayerRole.Host, "empire");
            lobby.SelectFaction(MultiplayerRole.Guest, "alliance");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                lobby.BeginLaunch()
            );

            Assert.That(exception.Message, Is.EqualTo(lobby.State.FactionSelectionError));
            Assert.That(lobby.State.launching, Is.False);
        }

        /// <summary>
        /// Carries the authoritative saved faction requirements over the existing lobby wire format.
        /// </summary>
        [Test]
        public void State_WireRoundTrip_PreservesSavedFactionRequirement()
        {
            MultiplayerLobby lobby = CreateSavedLobby();
            lobby.SelectFaction(MultiplayerRole.Host, "empire");

            MultiplayerLobbyState received = JsonUtility.FromJson<MultiplayerLobbyState>(
                JsonUtility.ToJson(lobby.State)
            );

            Assert.That(received.hasSavedFactionBinding, Is.True);
            Assert.That(received.savedHostFactionID, Is.EqualTo("alliance"));
            Assert.That(received.savedGuestFactionID, Is.EqualTo("empire"));
            Assert.That(
                received.FactionSelectionError,
                Is.EqualTo(lobby.State.FactionSelectionError)
            );
            Assert.That(received.HasValidFactionSelection, Is.False);
        }

        /// <summary>
        /// Keeps selected roles freely assignable when replacing a multiplayer save with a source to promote.
        /// </summary>
        [Test]
        public void SelectSave_SinglePlayerSource_PreservesChoicesAndClearsSavedBinding()
        {
            MultiplayerLobby lobby = CreateSavedLobby();
            lobby.SelectFaction(MultiplayerRole.Host, "empire");
            lobby.SelectFaction(MultiplayerRole.Guest, "alliance");

            lobby.SelectSave(CreateSummary(), "Single-player source");

            Assert.That(lobby.State.hostFactionID, Is.EqualTo("empire"));
            Assert.That(lobby.State.guestFactionID, Is.EqualTo("alliance"));
            Assert.That(lobby.State.hasSavedFactionBinding, Is.False);
            Assert.That(lobby.State.FactionSelectionError, Is.Null);
            long revision = lobby.State.settingsRevision;
            lobby.ConfirmContent(revision);
            lobby.SetReady(MultiplayerRole.Host, true, revision);
            lobby.SetReady(MultiplayerRole.Guest, true, revision);
            Assert.That(lobby.State.CanLaunch, Is.True);
        }

        [Test]
        public void SelectGameSetup_AllSupportedSettings_RoundTripsLaunchSettings(
            [Values] GameSize size,
            [Values] GameVictoryCondition victory
        )
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.SelectGameSetup(MultiplayerRole.Host, size, victory);
            MultiplayerLobbyState received = JsonUtility.FromJson<MultiplayerLobbyState>(
                JsonUtility.ToJson(lobby.State)
            );
            GameSummary summary = CreateSummary();
            summary.GalaxySize = size;
            summary.VictoryCondition = victory;
            GameSummary launch = MultiplayerSnapshotCodec.DeserializeSummary(
                MultiplayerSnapshotCodec.SerializeSummary(summary)
            );

            Assert.That(received.galaxySize, Is.EqualTo(size));
            Assert.That(received.victoryCondition, Is.EqualTo(victory));
            Assert.DoesNotThrow(() => received.ValidateGameSetup(launch));
        }

        [Test]
        public void SelectGameSetup_ChangedSettings_RequiresFreshApprovals()
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            long revision = lobby.State.settingsRevision;

            Assert.That(
                lobby.SelectGameSetup(
                    MultiplayerRole.Host,
                    GameSize.Small,
                    GameVictoryCondition.Headquarters
                ),
                Is.True
            );

            Assert.That(lobby.State.settingsRevision, Is.EqualTo(revision + 1));
            Assert.That(lobby.State.hostReady, Is.False);
            Assert.That(lobby.State.guestReady, Is.False);
            Assert.That(lobby.State.guestContentReady, Is.False);
            lobby.ConfirmContent(revision);
            Assert.That(lobby.State.guestContentReady, Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                lobby.SetReady(MultiplayerRole.Guest, true, revision)
            );
            long current = lobby.State.settingsRevision;
            lobby.ConfirmContent(current);
            lobby.SetReady(MultiplayerRole.Host, true, current);
            lobby.SetReady(MultiplayerRole.Guest, true, current);
            Assert.That(lobby.State.CanLaunch, Is.True);
        }

        [Test]
        public void SelectGameSetup_UnchangedSettings_PreservesApprovals()
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            string before = JsonUtility.ToJson(lobby.State);

            Assert.That(
                lobby.SelectGameSetup(
                    MultiplayerRole.Host,
                    lobby.State.galaxySize,
                    lobby.State.victoryCondition
                ),
                Is.False
            );

            Assert.That(JsonUtility.ToJson(lobby.State), Is.EqualTo(before));
        }

        [TestCase(MultiplayerRole.Guest)]
        [TestCase(MultiplayerRole.None)]
        [TestCase((MultiplayerRole)99)]
        public void SelectGameSetup_NonHost_PreservesSettings(MultiplayerRole role)
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            string before = JsonUtility.ToJson(lobby.State);

            Assert.Throws<InvalidOperationException>(() =>
                lobby.SelectGameSetup(role, GameSize.Small, GameVictoryCondition.Headquarters)
            );

            Assert.That(JsonUtility.ToJson(lobby.State), Is.EqualTo(before));
        }

        [TestCase((GameSize)(-1), GameVictoryCondition.Conquest)]
        [TestCase((GameSize)3, GameVictoryCondition.Conquest)]
        [TestCase(GameSize.Large, (GameVictoryCondition)(-1))]
        [TestCase(GameSize.Large, (GameVictoryCondition)2)]
        public void SelectGameSetup_UnsupportedValue_PreservesSettings(
            GameSize size,
            GameVictoryCondition victory
        )
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            string before = JsonUtility.ToJson(lobby.State);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                lobby.SelectGameSetup(MultiplayerRole.Host, size, victory)
            );

            Assert.That(JsonUtility.ToJson(lobby.State), Is.EqualTo(before));
        }

        [Test]
        public void SelectGameSetup_Launching_RejectsChange()
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            lobby.BeginLaunch();

            Assert.Throws<InvalidOperationException>(() =>
                lobby.SelectGameSetup(
                    MultiplayerRole.Host,
                    GameSize.Small,
                    GameVictoryCondition.Headquarters
                )
            );
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Saved campaign")]
        public void SelectGameSetup_LoadedSave_PreservesSavedSetup(string name)
        {
            MultiplayerLobby lobby = CreateReadyLobby();
            GameSummary saved = CreateSummary();
            saved.GalaxySize = GameSize.Small;
            saved.VictoryCondition = GameVictoryCondition.Headquarters;
            lobby.SelectSave(saved, name);
            string before = JsonUtility.ToJson(lobby.State);

            Assert.Throws<InvalidOperationException>(() =>
                lobby.SelectGameSetup(
                    MultiplayerRole.Host,
                    GameSize.Large,
                    GameVictoryCondition.Conquest
                )
            );

            Assert.That(lobby.State.hasSavedGame, Is.True);
            Assert.That(lobby.State.galaxySize, Is.EqualTo(GameSize.Small));
            Assert.That(
                lobby.State.victoryCondition,
                Is.EqualTo(GameVictoryCondition.Headquarters)
            );
            Assert.That(JsonUtility.ToJson(lobby.State), Is.EqualTo(before));
        }

        [Test]
        public void State_ModifiedSetupCopy_PreservesAuthoritativeConfiguration()
        {
            MultiplayerLobby lobby = CreateLobby();
            MultiplayerLobbyState copy = lobby.State;
            copy.galaxySize = GameSize.Small;
            copy.victoryCondition = GameVictoryCondition.Headquarters;
            copy.hasSavedGame = true;

            Assert.That(lobby.State.galaxySize, Is.EqualTo(GameSize.Large));
            Assert.That(lobby.State.victoryCondition, Is.EqualTo(GameVictoryCondition.Conquest));
            Assert.That(lobby.State.hasSavedGame, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ValidateGameSetup_MismatchedPayload_RejectsUpdate(bool sizeMismatch)
        {
            MultiplayerLobbyState state = CreateLobby().State;
            GameSummary payload = CreateSummary();
            if (sizeMismatch)
                payload.GalaxySize = GameSize.Small;
            else
                payload.VictoryCondition = GameVictoryCondition.Headquarters;

            Assert.Throws<InvalidOperationException>(() => state.ValidateGameSetup(payload));
        }

        /// <summary>
        /// Creates an admitted room bound to saved participant ownership.
        /// </summary>
        /// <returns>The waiting saved campaign.</returns>
        private static MultiplayerLobby CreateSavedLobby()
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.Join("Guest");
            lobby.SelectSave(CreateSummary(), "Saved campaign", CreateIdentity(), "host", "guest");
            return lobby;
        }

        /// <summary>
        /// Creates the authenticated ownership of a saved campaign with the opposite initial pair.
        /// </summary>
        /// <returns>The saved participant and faction binding.</returns>
        private static MultiplayerCampaignIdentity CreateIdentity()
        {
            return new MultiplayerCampaignIdentity
            {
                HostUserID = "host",
                GuestUserID = "guest",
                HostFactionID = "alliance",
                GuestFactionID = "empire",
            };
        }

        /// <summary>
        /// Creates a waiting room with two authored faction identifiers.
        /// </summary>
        /// <returns>The waiting lobby.</returns>
        private static MultiplayerLobby CreateLobby()
        {
            return new MultiplayerLobby("Test game", "Host", CreateSummary());
        }

        /// <summary>
        /// Creates a roster whose two players approved the current settings.
        /// </summary>
        /// <returns>The approved lobby.</returns>
        private static MultiplayerLobby CreateReadyLobby()
        {
            MultiplayerLobby lobby = CreateLobby();
            lobby.Join("Guest");
            long revision = lobby.State.settingsRevision;
            lobby.ConfirmContent(revision);
            lobby.SetReady(MultiplayerRole.Host, true, revision);
            lobby.SetReady(MultiplayerRole.Guest, true, revision);
            return lobby;
        }

        /// <summary>
        /// Creates the settings required by a two-faction room.
        /// </summary>
        /// <returns>The authored launch settings.</returns>
        private static GameSummary CreateSummary()
        {
            return new GameSummary
            {
                StartingFactionIDs = new[] { "empire", "alliance" },
                PlayerFactionID = "empire",
            };
        }
    }
}
