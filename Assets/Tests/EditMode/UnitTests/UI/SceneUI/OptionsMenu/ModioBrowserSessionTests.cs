using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Content.Modio;

namespace Rebellion.Tests.UI.SceneUI.OptionsMenu
{
    public sealed class ModioBrowserSessionTests
    {
        private string _root;

        /// <summary>
        /// Creates an isolated shared installation directory.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "reb2-modio-browser-" + Guid.NewGuid());
            Directory.CreateDirectory(Path.Combine(_root, "Content"));
        }

        /// <summary>
        /// Removes the fixture's installation records.
        /// </summary>
        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public async Task OpenAsync_UnconfiguredCatalog_KeepsBrowserAvailableToClose()
        {
            using ModioBrowserSession session = new ModioBrowserSession(
                new ModioConfiguration(),
                Path.Combine(_root, "Content"),
                () => { },
                () => { }
            );

            await session.OpenAsync();

            Assert.IsTrue(session.IsVisible);
            Assert.IsFalse(session.IsBusy);
            Assert.IsFalse(session.CreateRenderData().CanSearch);
        }

        [Test]
        public async Task OpenAsync_ServiceFailure_AllowsRetry()
        {
            using ModioBrowserSession session = Session(
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            );

            await session.OpenAsync();

            Assert.IsTrue(session.IsVisible);
            Assert.IsFalse(session.IsBusy);
            Assert.IsTrue(session.CreateRenderData().CanSearch);
        }

        [TestCase(43, "Update", true)]
        [TestCase(44, "Installed", false)]
        public async Task CreateRenderData_ExistingInstallation_ReflectsPublishedArchive(
            long installedFile,
            string action,
            bool canInstall
        )
        {
            string directory = Path.Combine(_root, "Mods", "modio-12");
            Directory.CreateDirectory(directory);
            new ModioInstallation
            {
                GameID = 7,
                ModID = 12,
                FileID = installedFile,
                ContentID = "community",
                ContentVersion = "1",
                Name = "Community",
            }.Write(directory);
            using ModioBrowserSession session = Session(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"data\":[{\"id\":12,\"game_id\":7,\"name\":\"Community\",\"modfile\":{\"id\":44,\"mod_id\":12}}],\"result_offset\":0,\"result_limit\":20,\"result_total\":1}",
                        Encoding.UTF8,
                        "application/json"
                    ),
                }
            );
            await session.OpenAsync();

            ModioBrowserRow row = session.CreateRenderData().Rows[0];

            Assert.AreEqual(action, row.Action);
            Assert.AreEqual(canInstall, row.CanInstall);
        }

        /// <summary>
        /// Creates a session whose transport returns a controlled response.
        /// </summary>
        /// <param name="response">The fake API response.</param>
        /// <returns>The browser session owning its transport.</returns>
        private ModioBrowserSession Session(HttpResponseMessage response)
        {
            ModioConfiguration configuration = new ModioConfiguration
            {
                GameID = 7,
                APIKey = "test-key",
            };
            return new ModioBrowserSession(
                configuration,
                Path.Combine(_root, "Content"),
                () => { },
                () => { },
                new ModioCatalogClient(configuration, new Handler(response))
            );
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            /// <summary>
            /// Selects the controlled response.
            /// </summary>
            /// <param name="response">The fake API response.</param>
            internal Handler(HttpResponseMessage response) => _response = response;

            /// <summary>
            /// Returns the fake response without contacting a service.
            /// </summary>
            /// <param name="request">The outgoing request.</param>
            /// <param name="cancellationToken">The caller's cancellation signal.</param>
            /// <returns>The controlled response.</returns>
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            ) => Task.FromResult(_response);
        }
    }
}
