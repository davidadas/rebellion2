using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Registers and discovers player-hosted sessions through the stateless directory service.
    /// </summary>
    public sealed class MatchmakerClient : IDisposable
    {
        private readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8),
        };

        /// <summary>
        /// Registers or refreshes a player-hosted game.
        /// </summary>
        /// <param name="baseUrl">The directory base URL.</param>
        /// <param name="game">The game registration.</param>
        /// <param name="ownerToken">The secret that authorizes listing mutation.</param>
        /// <returns>The normalized listing returned by the directory.</returns>
        public async Task<MatchmakerGame> RegisterAsync(
            string baseUrl,
            MatchmakerGame game,
            string ownerToken
        )
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            string url = BuildUrl(baseUrl, "games");
            MatchmakerRegistration registration = new MatchmakerRegistration
            {
                id = game.id,
                name = game.name,
                transport = game.transport,
                joinCode = game.joinCode,
                address = game.address,
                port = game.port,
                currentPlayers = game.currentPlayers,
                maxPlayers = game.maxPlayers,
                state = game.state,
                hostFaction = game.hostFaction,
                contentFingerprint = game.contentFingerprint,
            };
            using StringContent content = new StringContent(
                JsonUtility.ToJson(registration),
                Encoding.UTF8,
                "application/json"
            );
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content,
            };
            ApplyOwnerAuthorization(request, ownerToken);
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            string json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            return JsonUtility.FromJson<MatchmakerGame>(json);
        }

        /// <summary>
        /// Retrieves all live player-hosted games.
        /// </summary>
        /// <param name="baseUrl">The directory base URL.</param>
        /// <returns>The active listings.</returns>
        public async Task<IReadOnlyList<MatchmakerGame>> ListAsync(string baseUrl)
        {
            string json = await _httpClient.GetStringAsync(BuildUrl(baseUrl, "games"));
            MatchmakerGameList result = JsonUtility.FromJson<MatchmakerGameList>(json);
            return result?.games ?? new List<MatchmakerGame>();
        }

        /// <summary>
        /// Removes one hosted game from the directory.
        /// </summary>
        /// <param name="baseUrl">The directory base URL.</param>
        /// <param name="gameID">The listing identifier.</param>
        /// <param name="ownerToken">The secret that authorizes listing removal.</param>
        /// <returns>A task representing the request.</returns>
        public async Task RemoveAsync(string baseUrl, string gameID, string ownerToken)
        {
            if (string.IsNullOrWhiteSpace(gameID))
                return;
            using HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Delete,
                BuildUrl(baseUrl, $"games/{Uri.EscapeDataString(gameID)}")
            );
            ApplyOwnerAuthorization(request, ownerToken);
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Releases HTTP resources.
        /// </summary>
        public void Dispose()
        {
            _httpClient.Dispose();
        }

        /// <summary>
        /// Combines and validates a directory base URL and relative path.
        /// </summary>
        /// <param name="baseUrl">The directory base URL.</param>
        /// <param name="path">The relative request path.</param>
        /// <returns>The absolute request URL.</returns>
        private static string BuildUrl(string baseUrl, string path)
        {
            if (!Uri.TryCreate(baseUrl?.TrimEnd('/') + "/", UriKind.Absolute, out Uri root))
                throw new ArgumentException("A valid matchmaker URL is required.", nameof(baseUrl));
            return new Uri(root, path).AbsoluteUri;
        }

        /// <summary>
        /// Adds the private listing-owner credential to a mutation request.
        /// </summary>
        /// <param name="request">The outgoing directory mutation request.</param>
        /// <param name="ownerToken">The secret listing-owner token.</param>
        private static void ApplyOwnerAuthorization(HttpRequestMessage request, string ownerToken)
        {
            if (string.IsNullOrWhiteSpace(ownerToken))
                throw new ArgumentException(
                    "A listing owner token is required.",
                    nameof(ownerToken)
                );
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                ownerToken.Trim()
            );
        }
    }
}
