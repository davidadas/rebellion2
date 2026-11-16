using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Content.Modio;

namespace Rebellion.Tests.ContentEditor.UI
{
    public sealed class ContentLibrarySessionTests
    {
        private string root;
        private int notifications;

        /// <summary>
        /// Creates an isolated content root without an editing project.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(
                Path.GetTempPath(),
                "reb2-library-session-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(Path.Combine(root, "Content"));
            notifications = 0;
        }

        /// <summary>
        /// Removes all fixture-owned downloads and installations.
        /// </summary>
        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [Test]
        public void Refresh_UnconfiguredCatalog_StillShowsInstalledContent()
        {
            Pack("custom");
            using ContentLibrarySession session = new ContentLibrarySession(
                Path.Combine(root, "Content"),
                new ModioConfiguration(),
                () => notifications++,
                () => { },
                () => true
            );
            session.Refresh();
            Assert.AreEqual("custom", session.CreatePresentation().Rows.Single().Name);
            Assert.IsFalse(session.CreatePresentation().CanBrowse);
        }

        [Test]
        public async Task OpenAsync_UnconfiguredCatalog_ExplainsUnavailableBrowseAsync()
        {
            using ContentLibrarySession session = new ContentLibrarySession(
                Path.Combine(root, "Content"),
                new ModioConfiguration(),
                () => { },
                () => { },
                () => true
            );
            await session.OpenAsync(ContentLibraryPage.Browse);
            Assert.IsFalse(session.CreatePresentation().CanBrowse);
            Assert.That(session.CreatePresentation().EmptyMessage, Does.Contain("unavailable"));
        }

        [Test]
        public async Task OpenAsync_EmptyCatalog_PresentsEmptyBrowseCollectionAsync()
        {
            using ContentLibrarySession session = Session(_ =>
                Json("{\"data\":[],\"result_offset\":0,\"result_limit\":20,\"result_total\":0}")
            );
            await session.OpenAsync(ContentLibraryPage.Browse);
            Assert.AreEqual(ContentLibraryPage.Browse, session.CreatePresentation().Page);
            Assert.IsEmpty(session.CreatePresentation().Rows);
            Assert.IsFalse(session.IsBusy);
        }

        [Test]
        public async Task OpenAsync_PublicMetadata_ProjectsCreatorDescriptionAndThumbnailAsync()
        {
            using ContentLibrarySession session = Session(_ =>
                Json(
                    Page(
                        "{\"id\":12,\"game_id\":7,\"name\":\"Ships\",\"summary\":\"Short\",\"description_plaintext\":\"Full description\",\"submitted_by\":{\"username\":\"Creator\"},\"logo\":{\"thumb_640x360\":\"https://images.test/cover.png\"}}"
                    )
                )
            );
            await session.OpenAsync(ContentLibraryPage.Browse);
            ContentLibraryRow row = session.CreatePresentation().Selected;
            Assert.AreEqual("Creator", row.Author);
            Assert.AreEqual("Full description", row.Description);
            Assert.AreEqual("https://images.test/cover.png", row.ImageURL);
        }

        [Test]
        public void SetSearch_LocalLibrary_FiltersWithoutNetworkAccess()
        {
            Pack("ships");
            Pack("troops");
            using ContentLibrarySession session = Session(_ =>
                throw new InvalidOperationException("Unexpected request")
            );
            session.Refresh();
            session.SetSearch("SHIP");
            Assert.AreEqual("ships", session.CreatePresentation().Rows.Single().Name);
        }

        [Test]
        public void CreatePresentation_FilesRemovedAfterDiscovery_UsesCachedMetadata()
        {
            Pack("custom");
            using ContentLibrarySession session = Session(_ =>
                throw new InvalidOperationException("Unexpected request")
            );
            session.Refresh();
            Directory.Delete(Path.Combine(root, "Mods"), true);
            Assert.AreEqual("custom", session.CreatePresentation().Selected.Name);
        }

        [Test]
        public async Task CheckUpdatesAsync_DifferentPublishedFile_OffersUpdateAsync()
        {
            ManagedPack(41);
            using ContentLibrarySession session = Session(_ => Json(Mod(42)));
            session.Refresh();
            await session.OpenAsync(ContentLibraryPage.Updates);
            ContentLibraryRow row = session.CreatePresentation().Rows.Single();
            Assert.IsTrue(row.CanInstall);
            Assert.AreEqual("Update", row.Action);
        }

        [Test]
        public async Task CheckUpdatesAsync_SamePublishedFile_ExcludesCurrentInstallationAsync()
        {
            ManagedPack(42);
            using ContentLibrarySession session = Session(_ => Json(Mod(42)));
            session.Refresh();
            await session.OpenAsync(ContentLibraryPage.Updates);
            Assert.IsEmpty(session.CreatePresentation().Rows);
        }

        [Test]
        public async Task InstallSelectedAsync_VerifiedArchive_CreatesDiscoverableInstallationAsync()
        {
            byte[] archive = Archive();
            using MD5 md5 = MD5.Create();
            string hash = string.Concat(
                md5.ComputeHash(archive).Select(value => value.ToString("x2"))
            );
            string mod = Mod(42, archive.Length, hash);
            using ContentLibrarySession session = Session(request =>
                request.RequestUri.Host == "download.test"
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(archive),
                    }
                    : Json(
                        request.RequestUri.AbsolutePath.EndsWith("/mods", StringComparison.Ordinal)
                            ? Page(mod)
                            : mod
                    )
            );
            await session.OpenAsync(ContentLibraryPage.Browse);
            await session.InstallSelectedAsync();
            Assert.IsTrue(File.Exists(Path.Combine(root, "Mods/modio-12/pack.xml")));
            Assert.AreEqual(42, ModioInstallation.Read(Path.Combine(root, "Mods/modio-12")).FileID);
            Assert.AreEqual("Installed", session.CreatePresentation().Selected.Action);
        }

        [Test]
        public async Task InstallSelectedAsync_OpenProject_ProtectsExistingInstallationAsync()
        {
            ManagedPack(41);
            using ContentLibrarySession session = Session(
                request =>
                    Json(
                        request.RequestUri.AbsolutePath.EndsWith("/mods", StringComparison.Ordinal)
                            ? Page(Mod(42))
                            : Mod(42)
                    ),
                false
            );
            session.Refresh();
            await session.OpenAsync(ContentLibraryPage.Browse);
            await session.InstallSelectedAsync();
            Assert.AreEqual(41, ModioInstallation.Read(Path.Combine(root, "Mods/modio-12")).FileID);
            Assert.IsFalse(session.IsBusy);
        }

        [Test]
        public void Remove_OpenProject_LeavesInstalledFilesIntact()
        {
            string directory = Pack("custom");
            using ContentLibrarySession session = Session(_ => Json("{}"), false);
            session.Refresh();
            session.Remove(directory);
            Assert.IsTrue(Directory.Exists(directory));
        }

        [Test]
        public async Task OpenAsync_RequestFailure_RedactsCredentialsAndRestoresControlsAsync()
        {
            using ContentLibrarySession session = Session(_ =>
                throw new HttpRequestException("Failure: test-key")
            );
            await session.OpenAsync(ContentLibraryPage.Browse);
            Assert.IsFalse(session.IsBusy);
            Assert.That(session.CreatePresentation().Status, Does.Not.Contain("test-key"));
        }

        [Test]
        public void Dispose_SubsequentRefresh_DoesNotNotifyOwner()
        {
            ContentLibrarySession session = Session(_ => Json("{}"));
            session.Dispose();
            session.Refresh();
            Assert.AreEqual(0, notifications);
        }

        /// <summary>
        /// Creates a session around a deterministic public catalog transport.
        /// </summary>
        /// <param name="respond">The response for each outgoing request.</param>
        /// <param name="replace">Whether no editing project holds the files.</param>
        /// <returns>The isolated library session.</returns>
        private ContentLibrarySession Session(
            Func<HttpRequestMessage, HttpResponseMessage> respond,
            bool replace = true
        )
        {
            ModioConfiguration configuration = new ModioConfiguration
            {
                GameID = 7,
                APIKey = "test-key",
                APIBaseURL = "https://catalog.test/v1",
            };
            return new ContentLibrarySession(
                Path.Combine(root, "Content"),
                configuration,
                () => notifications++,
                () => { },
                () => replace,
                new ModioCatalogClient(configuration, new Handler(respond))
            );
        }

        /// <summary>
        /// Creates a minimal local pack whose metadata can be discovered without game data.
        /// </summary>
        /// <param name="name">The content ID and directory name.</param>
        /// <returns>The created package directory.</returns>
        private string Pack(string name)
        {
            string directory = Path.Combine(root, "Mods", name);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "pack.xml"),
                "<ContentPack><ID>" + name + "</ID><Version>1</Version></ContentPack>"
            );
            return directory;
        }

        /// <summary>
        /// Adds a managed installation with a known published-file identity.
        /// </summary>
        /// <param name="fileID">The installed archive identity.</param>
        private void ManagedPack(long fileID)
        {
            string directory = Pack("modio-12");
            new ModioInstallation
            {
                GameID = 7,
                ModID = 12,
                FileID = fileID,
                ContentID = "modio-12",
                ContentVersion = "1",
                Name = "Ships",
            }.Write(directory);
        }

        /// <summary>
        /// Describes a current published pack archive.
        /// </summary>
        /// <param name="fileID">The published archive ID.</param>
        /// <param name="size">The archive byte count.</param>
        /// <param name="hash">The archive digest.</param>
        /// <returns>The public catalog JSON.</returns>
        private static string Mod(long fileID, int size = 20, string hash = "hash") =>
            "{\"id\":12,\"game_id\":7,\"name\":\"Ships\",\"modfile\":{\"id\":"
            + fileID
            + ",\"mod_id\":12,\"version\":\"2\",\"filesize\":"
            + size
            + ",\"filehash\":{\"md5\":\""
            + hash
            + "\"},\"download\":{\"binary_url\":\"https://download.test/archive.zip\"}}}";

        /// <summary>
        /// Wraps one public mod in a searchable catalog page.
        /// </summary>
        /// <param name="mod">The public mod JSON.</param>
        /// <returns>The complete page JSON.</returns>
        private static string Page(string mod) =>
            "{\"data\":[" + mod + "],\"result_offset\":0,\"result_limit\":20,\"result_total\":1}";

        /// <summary>
        /// Creates an installable ZIP whose bytes traverse the production verification flow.
        /// </summary>
        /// <returns>The zipped content pack.</returns>
        private static byte[] Archive()
        {
            using MemoryStream stream = new MemoryStream();
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            using (
                StreamWriter writer = new StreamWriter(
                    archive.CreateEntry("pack.xml").Open(),
                    new UTF8Encoding(false)
                )
            )
                writer.Write("<ContentPack><ID>ships</ID><Version>2</Version></ContentPack>");
            return stream.ToArray();
        }

        /// <summary>
        /// Returns JSON from the deterministic transport seam.
        /// </summary>
        /// <param name="text">The response body.</param>
        /// <returns>The successful JSON response.</returns>
        private static HttpResponseMessage Json(string text) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(text, Encoding.UTF8, "application/json"),
            };

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

            /// <summary>
            /// Captures the response provider used by one isolated session.
            /// </summary>
            /// <param name="respond">The request response provider.</param>
            public Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
                this.respond = respond;

            /// <summary>
            /// Handles a request without reaching an external service.
            /// </summary>
            /// <param name="request">The outgoing catalog or download request.</param>
            /// <param name="cancellationToken">The operation cancellation token.</param>
            /// <returns>The fixture response.</returns>
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(respond(request));
            }
        }
    }
}
