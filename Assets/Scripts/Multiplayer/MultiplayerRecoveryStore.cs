using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Util.IO;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Binds a recoverable room to its authenticated participants and exact simulation content.
    /// </summary>
    [Serializable]
    internal sealed class MultiplayerRecoveryRecord
    {
        public int version = 1;
        public string lobbyID;
        public string campaignID;
        public string gameName;
        public string hostName;
        public string guestName;
        public string hostUserID;
        public string guestUserID;
        public string hostFactionID;
        public string guestFactionID;
        public string contentFingerprint;
        public string simulationBuild;
        public string gameplayContent;
        public string snapshotHash;

        /// <summary>
        /// Resolves an authenticated account without changing its campaign role or faction.
        /// </summary>
        /// <param name="userID">The account authenticated by the online service.</param>
        /// <returns>The account's reserved simulation role.</returns>
        public MultiplayerRole GetRole(string userID)
        {
            Validate();
            if (userID == hostUserID)
                return MultiplayerRole.Host;
            if (userID == guestUserID)
                return MultiplayerRole.Guest;
            throw new InvalidDataException("This recovery belongs to different players.");
        }

        /// <summary>
        /// Records the names accepted during rejoin according to each account's reserved simulation role.
        /// </summary>
        /// <param name="localUserID">The authenticated local participant.</param>
        /// <param name="localName">The normalized local display name.</param>
        /// <param name="remoteName">The normalized counterpart display name.</param>
        public void UpdatePlayerNames(string localUserID, string localName, string remoteName)
        {
            MultiplayerRole role = GetRole(localUserID);
            hostName = role == MultiplayerRole.Host ? localName : remoteName;
            guestName = role == MultiplayerRole.Guest ? localName : remoteName;
        }

        /// <summary>
        /// Checks the transport-authenticated counterpart against the saved reservation.
        /// </summary>
        /// <param name="localUserID">The locally authenticated account.</param>
        /// <param name="remoteUserID">The account admitted by lobby membership.</param>
        public void ValidatePeer(string localUserID, string remoteUserID)
        {
            MultiplayerRole role = GetRole(localUserID);
            if (remoteUserID != (role == MultiplayerRole.Host ? guestUserID : hostUserID))
                throw new InvalidDataException(
                    "This campaign is reserved for its returning player."
                );
        }

        /// <summary>
        /// Rejects a browser selection that does not identify this account's retained campaign.
        /// </summary>
        /// <param name="listing">The explicitly selected recovery room.</param>
        public void ValidateListing(MatchmakerGame listing)
        {
            Validate();
            if (listing == null || listing.campaignID != campaignID)
                throw new InvalidDataException(
                    "The selected game does not match your recovery checkpoint."
                );
        }

        /// <summary>
        /// Rejects a recovery offer from another campaign, participant binding, or installed build.
        /// </summary>
        /// <param name="other">The record offered by the authenticated survivor.</param>
        public void ValidateOffer(MultiplayerRecoveryRecord other)
        {
            Validate();
            other?.Validate();
            if (
                other == null
                || campaignID != other.campaignID
                || hostUserID != other.hostUserID
                || guestUserID != other.guestUserID
                || hostFactionID != other.hostFactionID
                || guestFactionID != other.guestFactionID
                || contentFingerprint != other.contentFingerprint
                || simulationBuild != other.simulationBuild
                || gameplayContent != other.gameplayContent
            )
                throw new InvalidDataException("The recovery offer does not match this campaign.");
        }

        /// <summary>
        /// Checks the decoded graph before it can replace a runtime or enter a loading scene.
        /// </summary>
        /// <param name="state">The complete independently decoded continuation.</param>
        public void ValidateState(GameRoot state)
        {
            Validate();
            if (state?.Metadata?.CampaignID != campaignID)
                throw new InvalidDataException(
                    "The recovery checkpoint belongs to another campaign."
                );
            MultiplayerCampaignAdmission.ValidateSavedCampaign(
                state.Metadata.MultiplayerCampaign,
                new MultiplayerCampaignIdentity
                {
                    HostUserID = hostUserID,
                    GuestUserID = guestUserID,
                    HostFactionID = hostFactionID,
                    GuestFactionID = guestFactionID,
                }
            );
        }

        /// <summary>
        /// Rejects incomplete or unsupported records before using their identifiers.
        /// </summary>
        public void Validate()
        {
            if (
                version != 1
                || string.IsNullOrWhiteSpace(gameName)
                || !Guid.TryParseExact(campaignID, "N", out _)
                || string.IsNullOrWhiteSpace(lobbyID)
                || string.IsNullOrWhiteSpace(hostUserID)
                || string.IsNullOrWhiteSpace(guestUserID)
                || hostUserID == guestUserID
                || string.IsNullOrWhiteSpace(hostFactionID)
                || string.IsNullOrWhiteSpace(guestFactionID)
                || hostFactionID == guestFactionID
                || string.IsNullOrWhiteSpace(contentFingerprint)
                || string.IsNullOrWhiteSpace(simulationBuild)
                || string.IsNullOrWhiteSpace(gameplayContent)
                || string.IsNullOrWhiteSpace(snapshotHash)
            )
                throw new InvalidDataException(
                    "The multiplayer recovery record is incomplete or unsupported."
                );
        }
    }

    /// <summary>
    /// Atomically stores one complete continuation per account, independently of manual save slots.
    /// </summary>
    internal sealed class MultiplayerRecoveryStore
    {
        private const string _signature = "Rebellion multiplayer recovery 1";
        private readonly string _directory;

        /// <summary>
        /// Uses the application's selected save directory without changing ordinary save formats.
        /// </summary>
        /// <param name="directory">The local persistence directory.</param>
        public MultiplayerRecoveryStore(string directory)
        {
            _directory = Path.GetFullPath(directory);
        }

        /// <summary>
        /// Reads only the small room locator, leaving the simulation payload on disk.
        /// </summary>
        /// <param name="userID">The authenticated account owning this local copy.</param>
        /// <returns>The last atomic record, or null when the account has no checkpoint.</returns>
        public MultiplayerRecoveryRecord ReadRecord(string userID)
        {
            string path = GetPath(userID);
            return SerialFileOperations.Run(
                _directory,
                () =>
                {
                    if (!File.Exists(path))
                        return null;
                    using BinaryReader reader = new BinaryReader(
                        File.OpenRead(path),
                        Encoding.UTF8
                    );
                    return ReadHeader(reader, userID);
                }
            );
        }

        /// <summary>
        /// Captures owned bytes and publishes the record and its sole world payload as one durable file.
        /// </summary>
        /// <param name="userID">The local authenticated account.</param>
        /// <param name="record">The matching room locator and checkpoint identity.</param>
        /// <param name="snapshot">The complete compressed runtime continuation.</param>
        /// <returns>The atomic durable publication.</returns>
        public Task WriteAsync(
            string userID,
            MultiplayerRecoveryRecord record,
            MultiplayerSnapshot snapshot
        )
        {
            record.GetRole(userID);
            if (snapshot == null || record.snapshotHash != snapshot.Hash)
                throw new InvalidDataException("The recovery record and checkpoint do not match.");
            string header = JsonUtility.ToJson(record);
            Directory.CreateDirectory(_directory);
            return AtomicFile.WriteAsync(
                GetPath(userID),
                stream =>
                {
                    using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
                    writer.Write(_signature);
                    writer.Write(header);
                    writer.Write(snapshot.Payload.Length);
                    writer.Write(snapshot.Payload);
                }
            );
        }

        /// <summary>
        /// Verifies the recorded payload before restoring its full runtime continuation.
        /// </summary>
        /// <param name="userID">The authenticated account owning this local copy.</param>
        /// <returns>The decoded continuation matching the latest atomically published record.</returns>
        public GameRoot ReadState(string userID)
        {
            return SerialFileOperations.Run(
                _directory,
                () =>
                {
                    using BinaryReader reader = new BinaryReader(
                        File.OpenRead(GetPath(userID)),
                        Encoding.UTF8
                    );
                    MultiplayerRecoveryRecord record = ReadHeader(reader, userID);
                    int length = reader.ReadInt32();
                    if (
                        length <= 0
                        || length > MultiplayerPacketCodec.MaximumPacketLength
                        || reader.BaseStream.Length - reader.BaseStream.Position != length
                    )
                        throw new InvalidDataException("The recovery checkpoint is incomplete.");
                    GameRoot state = MultiplayerSnapshotCodec.Restore(
                        reader.ReadBytes(length),
                        record.snapshotHash
                    );
                    record.ValidateState(state);
                    return state;
                }
            );
        }

        /// <summary>
        /// Checks the file signature and account binding before exposing room metadata.
        /// </summary>
        /// <param name="reader">The local checkpoint reader.</param>
        /// <param name="userID">The authenticated local account.</param>
        /// <returns>The validated room locator.</returns>
        private static MultiplayerRecoveryRecord ReadHeader(BinaryReader reader, string userID)
        {
            if (reader.ReadString() != _signature)
                throw new InvalidDataException("The recovery checkpoint format is unsupported.");
            MultiplayerRecoveryRecord record = JsonUtility.FromJson<MultiplayerRecoveryRecord>(
                reader.ReadString()
            );
            if (record == null)
                throw new InvalidDataException("The recovery checkpoint has no session record.");
            record.GetRole(userID);
            return record;
        }

        /// <summary>
        /// Derives a fixed local filename without placing account identifiers in paths.
        /// </summary>
        /// <param name="userID">The authenticated account owning the checkpoint.</param>
        /// <returns>The account's recovery file path.</returns>
        private string GetPath(string userID)
        {
            if (string.IsNullOrWhiteSpace(userID))
                throw new ArgumentException(
                    "An authenticated account is required.",
                    nameof(userID)
                );
            return Path.Combine(
                _directory,
                "multiplayer-recovery-" + EpicLobbyListing.HashContent(userID) + ".checkpoint"
            );
        }
    }
}
