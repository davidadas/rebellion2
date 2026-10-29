using System;
using System.Security.Cryptography;
using System.Text;
using AttributeData = Epic.OnlineServices.Lobby.AttributeData;
using LobbyAttribute = Epic.OnlineServices.Lobby.Attribute;
using LobbyAttributeVisibility = Epic.OnlineServices.Lobby.LobbyAttributeVisibility;
using LobbyDetails = Epic.OnlineServices.Lobby.LobbyDetails;
using LobbyDetailsCopyAttributeByKeyOptions = Epic.OnlineServices.Lobby.LobbyDetailsCopyAttributeByKeyOptions;
using LobbyDetailsCopyInfoOptions = Epic.OnlineServices.Lobby.LobbyDetailsCopyInfoOptions;
using LobbyDetailsInfo = Epic.OnlineServices.Lobby.LobbyDetailsInfo;
using LobbyModification = Epic.OnlineServices.Lobby.LobbyModification;
using LobbyModificationAddAttributeOptions = Epic.OnlineServices.Lobby.LobbyModificationAddAttributeOptions;
using Result = Epic.OnlineServices.Result;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Defines the public metadata boundary between EOS lobbies and the game browser.
    /// </summary>
    public static class EpicLobbyListing
    {
        public const string Transport = "eos-p2p";
        public const string BucketId = "rebellion2-v2";
        private const string _nameKey = "game-name";
        private const string _contentKey = "content";
        private const string _factionKey = "host-faction";
        private const string _stateKey = "state";

        /// <summary>
        /// Determines whether a published game is still accepting its second player.
        /// </summary>
        /// <param name="game">The published game.</param>
        /// <returns>True for a waiting EOS game with an available slot.</returns>
        public static bool IsJoinable(MatchmakerGame game)
        {
            return game != null
                && string.Equals(game.transport, Transport, StringComparison.Ordinal)
                && (
                    string.Equals(game.state, "waiting", StringComparison.Ordinal)
                    || string.Equals(game.state, "rejoining", StringComparison.Ordinal)
                )
                && game.maxPlayers == 2
                && game.currentPlayers < game.maxPlayers;
        }

        /// <summary>
        /// Hashes the full content identity to fit the service's attribute size limit.
        /// </summary>
        /// <param name="contentFingerprint">The unchanged game handshake identity.</param>
        /// <returns>The advertised SHA-256 identity.</returns>
        public static string HashContent(string contentFingerprint)
        {
            using SHA256 hash = SHA256.Create();
            return BitConverter
                .ToString(
                    hash.ComputeHash(Encoding.UTF8.GetBytes(contentFingerprint ?? string.Empty))
                )
                .Replace("-", string.Empty);
        }

        /// <summary>
        /// Publishes the browser fields on an existing lobby modification.
        /// </summary>
        /// <param name="modification">The modification to populate.</param>
        /// <param name="game">The host's game listing.</param>
        public static void Write(LobbyModification modification, MatchmakerGame game)
        {
            ValidateForPublication(game);
            AddAttribute(modification, _nameKey, game.name);
            AddAttribute(modification, _contentKey, HashContent(game.contentFingerprint));
            AddAttribute(modification, _factionKey, game.hostFaction);
            AddAttribute(modification, _stateKey, game.state);
            AddAttribute(modification, "pack-id", game.packID ?? string.Empty);
            AddAttribute(modification, "pack-name", game.packName ?? game.packID ?? string.Empty);
            AddAttribute(modification, "pack-version", game.packVersion ?? string.Empty);
            AddAttribute(modification, "scenario-id", game.scenarioID ?? string.Empty);
            AddAttribute(modification, "password", game.passwordProtected ? "yes" : "no");
            AddAttribute(modification, "campaign-id", game.campaignID ?? string.Empty);
        }

        /// <summary>
        /// Rejects metadata that would make an existing room undiscoverable before native mutation.
        /// </summary>
        /// <param name="game">The complete advertised room metadata.</param>
        internal static void ValidateForPublication(MatchmakerGame game)
        {
            if (string.IsNullOrWhiteSpace(game?.name))
                throw new ArgumentException(
                    "A published room requires its game name.",
                    nameof(game)
                );
        }

        /// <summary>
        /// Reads only fully published listings belonging to this multiplayer protocol.
        /// </summary>
        /// <param name="details">The lobby details supplied by EOS.</param>
        /// <returns>The browser listing, or null for a lobby outside the browser contract.</returns>
        public static MatchmakerGame Read(LobbyDetails details)
        {
            LobbyDetailsCopyInfoOptions options = new LobbyDetailsCopyInfoOptions();
            if (
                details.CopyInfo(ref options, out LobbyDetailsInfo? info) != Result.Success
                || !info.HasValue
                || !string.Equals(info.Value.BucketId, BucketId, StringComparison.Ordinal)
                || info.Value.LobbyOwnerUserId == null
            )
                return null;
            string name = ReadAttribute(details, _nameKey);
            string content = ReadAttribute(details, _contentKey);
            string faction = ReadAttribute(details, _factionKey);
            string state = ReadAttribute(details, _stateKey);
            if (
                string.IsNullOrWhiteSpace(name)
                || string.IsNullOrWhiteSpace(content)
                || string.IsNullOrWhiteSpace(faction)
                || string.IsNullOrWhiteSpace(state)
            )
                return null;
            return new MatchmakerGame
            {
                id = info.Value.LobbyId,
                name = name,
                transport = Transport,
                joinCode = info.Value.LobbyId,
                address = info.Value.LobbyOwnerUserId.ToString(),
                currentPlayers = checked((int)(info.Value.MaxMembers - info.Value.AvailableSlots)),
                maxPlayers = checked((int)info.Value.MaxMembers),
                state = state,
                hostFaction = faction,
                contentFingerprint = content,
                packID = ReadAttribute(details, "pack-id"),
                packName = ReadAttribute(details, "pack-name"),
                packVersion = ReadAttribute(details, "pack-version"),
                scenarioID = ReadAttribute(details, "scenario-id"),
                passwordProtected = ReadAttribute(details, "password") == "yes",
                campaignID = ReadAttribute(details, "campaign-id"),
            };
        }

        /// <summary>
        /// Adds one public string attribute and checks the SDK result.
        /// </summary>
        /// <param name="modification">The pending lobby modification.</param>
        /// <param name="key">The attribute key.</param>
        /// <param name="value">The attribute value.</param>
        private static void AddAttribute(LobbyModification modification, string key, string value)
        {
            LobbyModificationAddAttributeOptions options = new LobbyModificationAddAttributeOptions
            {
                Attribute = new AttributeData { Key = key, Value = value },
                Visibility = LobbyAttributeVisibility.Public,
            };
            Result result = modification.AddAttribute(ref options);
            if (result != Result.Success)
                throw new InvalidOperationException("Publish lobby metadata: " + result);
        }

        /// <summary>
        /// Copies a required public string attribute from a lobby.
        /// </summary>
        /// <param name="details">The lobby details.</param>
        /// <param name="key">The requested attribute key.</param>
        /// <returns>The attribute string, or null when it is absent or has another type.</returns>
        private static string ReadAttribute(LobbyDetails details, string key)
        {
            LobbyDetailsCopyAttributeByKeyOptions options =
                new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = key };
            if (
                details.CopyAttributeByKey(ref options, out LobbyAttribute? attribute)
                    != Result.Success
                || !attribute.HasValue
                || !attribute.Value.Data.HasValue
            )
                return null;
            return attribute.Value.Data.Value.Value.AsUtf8;
        }
    }
}
