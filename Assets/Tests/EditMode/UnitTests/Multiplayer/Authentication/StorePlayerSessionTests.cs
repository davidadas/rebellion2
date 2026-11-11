using System;
using System.Text;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class StorePlayerSessionTests
    {
        private static readonly DateTimeOffset _now = DateTimeOffset.FromUnixTimeSeconds(
            1800000000
        );

        [Test]
        public void Parse_StoreCredential_PreservesAccountAndUnicodeName()
        {
            StorePlayerSession session = StorePlayerSession.Parse(
                SessionJson(
                    "steam",
                    "steam:76561198000000000",
                    "Player é",
                    _now.ToUnixTimeSeconds() + 60
                ),
                _now
            );
            Assert.That(session.Subject, Is.EqualTo("steam:76561198000000000"));
            Assert.That(session.DisplayName, Is.EqualTo("Player é"));
            Assert.That(session.Provider, Is.EqualTo("steam"));
        }

        [TestCase("steam", "gog:1234")]
        [TestCase("steam", "steam:")]
        [TestCase("steam", "steam:1234:5678")]
        [TestCase("github", "github:1234")]
        public void Parse_InvalidStoreSubject_ReturnsNull(string provider, string subject)
        {
            Assert.That(
                StorePlayerSession.Parse(
                    SessionJson(provider, subject, "Player", _now.ToUnixTimeSeconds() + 60),
                    _now
                ),
                Is.Null
            );
        }

        [Test]
        public void Parse_ExpiredCredential_ReturnsNull()
        {
            Assert.That(
                StorePlayerSession.Parse(
                    SessionJson("gog", "gog:1234", "Player", _now.ToUnixTimeSeconds()),
                    _now
                ),
                Is.Null
            );
        }

        [TestCase("")]
        [TestCase("{}")]
        [TestCase("{\"accessToken\":\"invalid.signature\"}")]
        [TestCase("{\"accessToken\":\"one.two.three\"}")]
        public void Parse_MalformedSession_ReturnsNull(string json)
        {
            Assert.That(StorePlayerSession.Parse(json, _now), Is.Null);
        }

        [Test]
        public void Parse_LegacyDownloadCredential_ReturnsNull()
        {
            string payload = Convert.ToBase64String(
                Encoding.UTF8.GetBytes("{\"sub\":\"steam\",\"exp\":4102444800}")
            );
            Assert.That(
                StorePlayerSession.Parse("{\"accessToken\":\"" + payload + ".signature\"}", _now),
                Is.Null
            );
        }

        /// <summary>
        /// Creates an unsigned fixture credential for testing local metadata parsing.
        /// </summary>
        /// <param name="provider">The store identifier.</param>
        /// <param name="subject">The provider-qualified account ID.</param>
        /// <param name="name">The display name.</param>
        /// <param name="expiry">The expiry timestamp.</param>
        /// <returns>A session file fixture.</returns>
        private static string SessionJson(string provider, string subject, string name, long expiry)
        {
            string claims =
                $"{{\"aud\":\"rebellion2-multiplayer\",\"sub\":\"{subject}\",\"name\":\"{name}\",\"provider\":\"{provider}\",\"exp\":{expiry}}}";
            string payload = Convert
                .ToBase64String(Encoding.UTF8.GetBytes(claims))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return "{\"accessToken\":\"" + payload + ".signature\"}";
        }
    }
}
