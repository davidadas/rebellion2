using System;
using System.IO;
using System.Xml;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerCampaignAdmissionTests
    {
        [TestCase(null)]
        [TestCase("  Player <A> & \U0001F680\t\n  ")]
        public void ConfigureGame_ValidParticipantNames_PreservesSaveableNames(string name)
        {
            GameRoot game = CreateSavedGame();
            MultiplayerCampaignIdentity admitted = CreateIdentity();
            MultiplayerLaunchConfiguration configuration = new(
                game.Summary,
                "alliance",
                "alliance",
                "empire",
                game
            );

            MultiplayerCampaignAdmission.ConfigureGame(
                game,
                admitted,
                configuration,
                true,
                name,
                name
            );

            Assert.AreEqual(name, game.GetFactionPlayer("alliance").DisplayName);
            Assert.AreEqual(name, game.GetFactionPlayer("empire").DisplayName);
            using MemoryStream save = new();
            GameSerializer serializer = new(typeof(GameRoot));
            Assert.DoesNotThrow(() => serializer.Serialize(save, game));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfigureGame_InvalidParticipantName_PreservesExistingControllers(
            bool invalidHost
        )
        {
            GameRoot game = CreateSavedGame();
            MultiplayerCampaignIdentity admitted = CreateIdentity();
            MultiplayerLaunchConfiguration configuration = new(
                game.Summary,
                "alliance",
                "alliance",
                "empire",
                game
            );

            Assert.Throws<XmlException>(() =>
                MultiplayerCampaignAdmission.ConfigureGame(
                    game,
                    admitted,
                    configuration,
                    true,
                    invalidHost ? "Host \u0001" : "Host",
                    invalidHost ? "Guest" : "Guest \u0001"
                )
            );

            Assert.AreEqual("saved-host-controller", game.GetFactionPlayer("alliance").PlayerID);
            Assert.AreEqual("saved-guest-controller", game.GetFactionPlayer("empire").PlayerID);
            Assert.AreEqual("Saved host name", game.GetFactionPlayer("alliance").DisplayName);
            Assert.AreEqual("Saved guest name", game.GetFactionPlayer("empire").DisplayName);
            Assert.AreEqual("empire", game.LocalPlayerFactionID);
        }

        /// <summary>
        /// Allows selection before the reserved guest joins while still requiring the saved host.
        /// </summary>
        [Test]
        public void GetParticipantError_ReservedGuestNotYetJoined_AcceptsSavedHost()
        {
            string error = MultiplayerCampaignAdmission.GetParticipantError(
                CreateIdentity(),
                "host",
                null,
                false
            );

            Assert.That(error, Is.Null);
        }

        /// <summary>
        /// Applies the same participant check at reserved guest admission and after an existing join.
        /// </summary>
        [Test]
        public void GetParticipantError_JoinedGuestDoesNotOwnSave_RejectsParticipant()
        {
            string error = MultiplayerCampaignAdmission.GetParticipantError(
                CreateIdentity(),
                "host",
                "different-guest",
                true
            );

            Assert.That(error, Is.Not.Null.And.Contains("authenticated participants"));
        }

        /// <summary>
        /// Verifies saved ownership before changing either controller, names, or local faction.
        /// </summary>
        [TestCase("different-host", "guest", "alliance", "empire")]
        [TestCase("host", "different-guest", "alliance", "empire")]
        [TestCase("host", "guest", "empire", "alliance")]
        public void ConfigureGame_SavedOwnershipMismatch_PreservesExistingControllers(
            string hostUserID,
            string guestUserID,
            string hostFactionID,
            string guestFactionID
        )
        {
            GameRoot game = CreateSavedGame();
            Player savedHost = game.GetFactionPlayer("alliance");
            Player savedGuest = game.GetFactionPlayer("empire");
            MultiplayerCampaignIdentity admitted = new MultiplayerCampaignIdentity
            {
                HostUserID = hostUserID,
                GuestUserID = guestUserID,
                HostFactionID = hostFactionID,
                GuestFactionID = guestFactionID,
            };
            MultiplayerLaunchConfiguration configuration = new MultiplayerLaunchConfiguration(
                game.Summary,
                hostFactionID,
                hostFactionID,
                guestFactionID,
                game
            );

            Assert.Throws<InvalidDataException>(() =>
                MultiplayerCampaignAdmission.ConfigureGame(
                    game,
                    admitted,
                    configuration,
                    true,
                    "New host name",
                    "New guest name"
                )
            );

            Assert.That(game.GetFactionPlayer("alliance"), Is.SameAs(savedHost));
            Assert.That(game.GetFactionPlayer("empire"), Is.SameAs(savedGuest));
            Assert.That(savedHost.PlayerID, Is.EqualTo("saved-host-controller"));
            Assert.That(savedGuest.PlayerID, Is.EqualTo("saved-guest-controller"));
            Assert.That(savedHost.ControllerType, Is.EqualTo(PlayerControllerType.AI));
            Assert.That(savedGuest.ControllerType, Is.EqualTo(PlayerControllerType.AI));
            Assert.That(savedHost.DisplayName, Is.EqualTo("Saved host name"));
            Assert.That(savedGuest.DisplayName, Is.EqualTo("Saved guest name"));
            Assert.That(game.LocalPlayerFactionID, Is.EqualTo("empire"));
            Assert.That(game.UseDeterministicRuntimeIdentifiers, Is.False);
            Assert.That(game.Metadata.MultiplayerCampaign.HostUserID, Is.EqualTo("host"));
            Assert.That(game.Metadata.MultiplayerCampaign.GuestUserID, Is.EqualTo("guest"));
        }

        /// <summary>
        /// Binds accepted saved ownership and the local view on both participant roles.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ConfigureGame_SavedOwnershipMatches_AppliesHumanControllers(bool authoritative)
        {
            GameRoot game = CreateSavedGame();
            MultiplayerCampaignIdentity admitted = CreateIdentity();
            string localFactionID = authoritative ? "alliance" : "empire";
            MultiplayerLaunchConfiguration configuration = new MultiplayerLaunchConfiguration(
                game.Summary,
                localFactionID,
                admitted.HostFactionID,
                admitted.GuestFactionID,
                game
            );

            MultiplayerCampaignAdmission.ConfigureGame(
                game,
                admitted,
                configuration,
                authoritative,
                "Host",
                "Guest"
            );

            Player host = game.GetFactionPlayer("alliance");
            Player guest = game.GetFactionPlayer("empire");
            Assert.That(host.PlayerID, Is.EqualTo("MULTIPLAYER_HOST"));
            Assert.That(guest.PlayerID, Is.EqualTo("MULTIPLAYER_GUEST"));
            Assert.That(host.ControllerType, Is.EqualTo(PlayerControllerType.Human));
            Assert.That(guest.ControllerType, Is.EqualTo(PlayerControllerType.Human));
            Assert.That(host.DisplayName, Is.EqualTo("Host"));
            Assert.That(guest.DisplayName, Is.EqualTo("Guest"));
            Assert.That(game.LocalPlayerFactionID, Is.EqualTo(localFactionID));
            Assert.That(game.UseDeterministicRuntimeIdentifiers, Is.True);
        }

        /// <summary>
        /// Promotes a host-owned source without overriding the agreed faction pair.
        /// </summary>
        [Test]
        public void InitializeCampaign_SinglePlayerSource_AssignsAdmittedOwnership()
        {
            GameRoot game = new GameRoot();
            MultiplayerCampaignIdentity admitted = CreateIdentity();

            MultiplayerCampaignAdmission.InitializeCampaign(game, admitted, true);

            Assert.That(game.Metadata.MultiplayerCampaign, Is.SameAs(admitted));
            Assert.That(Guid.TryParseExact(game.Metadata.CampaignID, "N", out _), Is.True);
        }

        /// <summary>
        /// Creates a saved graph with existing controller identities and authenticated ownership.
        /// </summary>
        /// <returns>The graph whose admission effects can be observed.</returns>
        private static GameRoot CreateSavedGame()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary
                {
                    StartingFactionIDs = new[] { "alliance", "empire" },
                    PlayerFactionID = "alliance",
                },
                Metadata = new GameMetadata
                {
                    CampaignID = Guid.NewGuid().ToString("N"),
                    MultiplayerCampaign = CreateIdentity(),
                },
                LocalPlayerFactionID = "empire",
            };
            game.SetFactionController("alliance", "saved-host-controller", PlayerControllerType.AI);
            game.SetFactionController("empire", "saved-guest-controller", PlayerControllerType.AI);
            game.GetFactionPlayer("alliance").DisplayName = "Saved host name";
            game.GetFactionPlayer("empire").DisplayName = "Saved guest name";
            return game;
        }

        /// <summary>
        /// Creates the known account and faction ownership of the saved campaign.
        /// </summary>
        /// <returns>The saved ownership.</returns>
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
    }
}
