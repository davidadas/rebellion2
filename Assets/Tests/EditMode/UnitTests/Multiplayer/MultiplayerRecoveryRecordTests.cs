using System;
using System.IO;
using NUnit.Framework;
using Rebellion.Multiplayer;
using UnityEngine;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerRecoveryRecordTests
    {
        [TestCase("host", MultiplayerRole.Host)]
        [TestCase("guest", MultiplayerRole.Guest)]
        public void GetRole_RoomOwnershipChanges_PreservesOriginalSimulationRole(
            string userID,
            MultiplayerRole role
        )
        {
            MultiplayerRecoveryRecord record = CreateRecord();
            record.lobbyID = "replacement-room";
            Assert.AreEqual(role, record.GetRole(userID));
        }

        [Test]
        public void GetRole_UnrelatedAccount_RejectsReservation()
        {
            Assert.Throws<InvalidDataException>(() => CreateRecord().GetRole("other"));
        }

        [TestCase("host", "guest")]
        [TestCase("guest", "host")]
        public void ValidatePeer_OriginalCounterpart_AllowsReturningPlayer(
            string local,
            string remote
        )
        {
            Assert.DoesNotThrow(() => CreateRecord().ValidatePeer(local, remote));
        }

        [TestCase("host", "other")]
        [TestCase("guest", "other")]
        [TestCase("host", "host")]
        public void ValidatePeer_UnreservedCounterpart_RejectsAdmission(string local, string remote)
        {
            Assert.Throws<InvalidDataException>(() => CreateRecord().ValidatePeer(local, remote));
        }

        [Test]
        public void ValidateOffer_RecreatedRoomWithNewCheckpoint_AcceptsSameCampaign()
        {
            MultiplayerRecoveryRecord record = CreateRecord();
            MultiplayerRecoveryRecord offered = Clone(record);
            offered.lobbyID = "replacement-room";
            offered.snapshotHash = "new-checkpoint";
            Assert.DoesNotThrow(() => record.ValidateOffer(offered));
        }

        [Test]
        public void ValidateListing_DifferentCampaign_RejectsSelectedRoom()
        {
            MultiplayerRecoveryRecord record = CreateRecord();
            Assert.Throws<InvalidDataException>(() =>
                record.ValidateListing(
                    new MatchmakerGame { campaignID = Guid.NewGuid().ToString("N") }
                )
            );
        }

        [Test]
        public void ValidateListing_RecreatedRoomForRetainedCampaign_AcceptsSelectedRoom()
        {
            MultiplayerRecoveryRecord record = CreateRecord();
            Assert.DoesNotThrow(() =>
                record.ValidateListing(
                    new MatchmakerGame { id = "replacement-room", campaignID = record.campaignID }
                )
            );
        }

        [Test]
        public void ValidateListing_MissingSelection_RejectsSelectedRoom()
        {
            Assert.Throws<InvalidDataException>(() => CreateRecord().ValidateListing(null));
        }

        [TestCase("campaignID")]
        [TestCase("hostUserID")]
        [TestCase("guestUserID")]
        [TestCase("hostFactionID")]
        [TestCase("guestFactionID")]
        [TestCase("contentFingerprint")]
        [TestCase("simulationBuild")]
        [TestCase("gameplayContent")]
        public void ValidateOffer_DifferentCampaignBinding_RejectsCheckpoint(string field)
        {
            MultiplayerRecoveryRecord record = CreateRecord();
            MultiplayerRecoveryRecord offered = Clone(record);
            switch (field)
            {
                case "campaignID":
                    offered.campaignID = Guid.NewGuid().ToString("N");
                    break;
                case "hostUserID":
                    offered.hostUserID = "other";
                    break;
                case "guestUserID":
                    offered.guestUserID = "other";
                    break;
                case "hostFactionID":
                    offered.hostFactionID = "other";
                    break;
                case "guestFactionID":
                    offered.guestFactionID = "other";
                    break;
                case "contentFingerprint":
                    offered.contentFingerprint = "other";
                    break;
                case "simulationBuild":
                    offered.simulationBuild = "other";
                    break;
                default:
                    offered.gameplayContent = "other";
                    break;
            }
            Assert.Throws<InvalidDataException>(() => record.ValidateOffer(offered));
        }

        [Test]
        public void Validate_UnsupportedVersion_RejectsRecord()
        {
            MultiplayerRecoveryRecord record = CreateRecord();
            record.version++;
            Assert.Throws<InvalidDataException>(record.Validate);
        }

        [Test]
        public void Validate_MissingRoomName_RejectsUndiscoverableRecoveryRecord()
        {
            MultiplayerRecoveryRecord record = CreateRecord();
            record.gameName = null;
            Assert.Throws<InvalidDataException>(record.Validate);
        }

        /// <summary>
        /// Builds an independent serializable campaign reservation.
        /// </summary>
        /// <returns>The complete reservation.</returns>
        internal static MultiplayerRecoveryRecord CreateRecord() =>
            new()
            {
                lobbyID = "room",
                campaignID = Guid.NewGuid().ToString("N"),
                gameName = "Recovery game",
                hostName = "First player",
                guestName = "Second player",
                hostUserID = "host",
                guestUserID = "guest",
                hostFactionID = "FNALL1",
                guestFactionID = "FNEMP1",
                contentFingerprint = "content",
                simulationBuild = "build",
                gameplayContent = "gameplay",
                snapshotHash = "hash",
            };

        /// <summary>
        /// Copies a record through its supported wire representation.
        /// </summary>
        /// <param name="record">The original reservation.</param>
        /// <returns>An independent record.</returns>
        private static MultiplayerRecoveryRecord Clone(MultiplayerRecoveryRecord record) =>
            JsonUtility.FromJson<MultiplayerRecoveryRecord>(JsonUtility.ToJson(record));
    }
}
