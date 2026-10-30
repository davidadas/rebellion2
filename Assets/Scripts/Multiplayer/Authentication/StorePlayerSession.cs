using System;
using System.Text;
using JObject = Newtonsoft.Json.Linq.JObject;
using JsonException = Newtonsoft.Json.JsonException;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Carries a store account credential whose signature is verified by the online identity service.
    /// </summary>
    public sealed class StorePlayerSession
    {
        public string AccessToken { get; }
        public string Subject { get; }
        public string DisplayName { get; }
        public string Provider { get; }
        public DateTimeOffset ExpiresAt { get; }

        /// <summary>
        /// Creates a parsed session without treating local claims as proof of authentication.
        /// </summary>
        /// <param name="accessToken">The signed credential submitted to the identity service.</param>
        /// <param name="claims">The decoded credential metadata.</param>
        private StorePlayerSession(string accessToken, JObject claims)
        {
            AccessToken = accessToken;
            Subject = claims.Value<string>("sub");
            DisplayName = claims.Value<string>("name");
            Provider = claims.Value<string>("provider");
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(claims.Value<long>("exp"));
        }

        /// <summary>
        /// Reads cached player metadata and rejects expired or legacy download sessions.
        /// </summary>
        /// <param name="json">The launcher session file contents.</param>
        /// <param name="now">The current time used for the expiry check.</param>
        /// <returns>The session, or null when renewed store sign-in is required.</returns>
        public static StorePlayerSession Parse(string json, DateTimeOffset now)
        {
            try
            {
                string token = JObject.Parse(json).Value<string>("accessToken");
                string[] parts = token?.Split('.');
                if (parts?.Length != 2 || string.IsNullOrEmpty(parts[1]))
                    return null;
                string encoded = parts[0].Replace('-', '+').Replace('_', '/');
                encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
                JObject claims = JObject.Parse(
                    Encoding.UTF8.GetString(Convert.FromBase64String(encoded))
                );
                StorePlayerSession session = new(token, claims);
                string prefix = session.Provider + ":";
                if (
                    claims.Value<string>("aud") != "rebellion2-multiplayer"
                    || (session.Provider != "steam" && session.Provider != "gog")
                    || string.IsNullOrWhiteSpace(session.DisplayName)
                    || session.Subject?.StartsWith(prefix, StringComparison.Ordinal) != true
                    || !ulong.TryParse(session.Subject.Substring(prefix.Length), out _)
                    || session.ExpiresAt <= now
                )
                    return null;
                return session;
            }
            catch (Exception exception)
                when (exception is JsonException
                    || exception is FormatException
                    || exception is ArgumentException
                    || exception is OverflowException
                    || exception is InvalidCastException
                )
            {
                return null;
            }
        }
    }
}
