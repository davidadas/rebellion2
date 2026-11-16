using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Content.Modio;

namespace Rebellion.Tests.Content.Modio
{
    public sealed class ModioCatalogClientTests
    {
        private string _destination;

        /// <summary>
        /// Selects an isolated download destination for each case.
        /// </summary>
        [SetUp]
        public void SetUp() =>
            _destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");

        /// <summary>
        /// Removes any complete or interrupted download created by the case.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_destination))
                File.Delete(_destination);
        }

        [Test]
        public async Task BrowseAsync_EmptyCatalog_ReturnsEmptyPageAsync()
        {
            using ModioCatalogClient client = CreateClient(_ =>
                Json("{\"data\":[],\"result_offset\":0,\"result_limit\":20,\"result_total\":0}")
            );

            ModioCatalogPage page = await client.BrowseAsync(
                string.Empty,
                0,
                CancellationToken.None
            );

            Assert.IsEmpty(page.Mods);
            Assert.AreEqual(0, page.Total);
        }

        [Test]
        public async Task BrowseAsync_SearchWithQueryCharacters_EncodesSearchTextAsync()
        {
            Uri requestURL = null;
            using ModioCatalogClient client = CreateClient(request =>
            {
                requestURL = request.RequestUri;
                return Json(
                    "{\"data\":[],\"result_offset\":0,\"result_limit\":20,\"result_total\":0}"
                );
            });

            await client.BrowseAsync("ships & fighters?", 20, CancellationToken.None);

            StringAssert.Contains("name-lk=%2Aships%20%26%20fighters%3F%2A", requestURL.Query);
            StringAssert.Contains("_offset=20", requestURL.Query);
            Assert.AreEqual("/v1/games/7/mods", requestURL.AbsolutePath);
        }

        [Test]
        public void BrowseAsync_ServerFailure_ReportsStatusWithoutCredentials()
        {
            using ModioCatalogClient client = CreateClient(_ => new HttpResponseMessage(
                HttpStatusCode.TooManyRequests
            ));

            HttpRequestException exception = Assert.ThrowsAsync<HttpRequestException>(() =>
                client.BrowseAsync(string.Empty, 0, CancellationToken.None)
            );

            StringAssert.Contains("429", exception.Message);
            StringAssert.DoesNotContain("test-api-key", exception.Message);
        }

        [Test]
        public async Task GetModAsync_PublishedArchive_ReadsArchiveIdentityAsync()
        {
            using ModioCatalogClient client = CreateClient(_ =>
                Json(
                    "{\"id\":12,\"game_id\":7,\"name\":\"Ships\",\"modfile\":{\"id\":44,\"mod_id\":12,\"filesize\":120,\"version\":\"2\",\"filehash\":{\"md5\":\"digest\"},\"download\":{\"binary_url\":\"https://downloads.example/mod.zip\"}}}"
                )
            );

            ModioMod mod = await client.GetModAsync(12, CancellationToken.None);

            Assert.AreEqual(44, mod.File.ID);
            Assert.AreEqual(12, mod.File.ModID);
            Assert.AreEqual("https://downloads.example/mod.zip", mod.File.Download.URL);
        }

        [Test]
        public async Task DownloadAsync_VerifiedBytes_WritesCompleteDownloadAsync()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("archive contents");
            Uri requestURL = null;
            using ModioCatalogClient client = CreateClient(request =>
            {
                requestURL = request.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes),
                };
            });

            await client.DownloadAsync(FileFor(bytes), _destination, null, CancellationToken.None);

            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_destination));
            Assert.IsEmpty(requestURL.Query);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DownloadAsync_ArchiveMismatch_RejectsDownload(bool truncated)
        {
            byte[] bytes = Encoding.UTF8.GetBytes("archive contents");
            ModioFile file = FileFor(bytes);
            if (truncated)
                file.Size++;
            else
                file.Hash.MD5 = new string('0', 32);
            using ModioCatalogClient client = CreateClient(_ => new HttpResponseMessage(
                HttpStatusCode.OK
            )
            {
                Content = new ByteArrayContent(bytes),
            });

            Assert.ThrowsAsync<InvalidDataException>(() =>
                client.DownloadAsync(file, _destination, null, CancellationToken.None)
            );
        }

        /// <summary>
        /// Creates a catalog client whose transport returns controlled responses.
        /// </summary>
        /// <param name="response">The request-to-response callback.</param>
        /// <returns>The client owning the fake transport.</returns>
        private static ModioCatalogClient CreateClient(
            Func<HttpRequestMessage, HttpResponseMessage> response
        ) =>
            new ModioCatalogClient(
                new ModioConfiguration { GameID = 7, APIKey = "test-api-key" },
                new Handler(response)
            );

        /// <summary>
        /// Creates a successful JSON response.
        /// </summary>
        /// <param name="body">The response JSON.</param>
        /// <returns>The fake API response.</returns>
        private static HttpResponseMessage Json(string body) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

        /// <summary>
        /// Describes the exact bytes expected from a fake download.
        /// </summary>
        /// <param name="bytes">The expected archive bytes.</param>
        /// <returns>The published download metadata.</returns>
        private static ModioFile FileFor(byte[] bytes)
        {
            using MD5 hash = MD5.Create();
            return new ModioFile
            {
                ID = 44,
                ModID = 12,
                Size = bytes.Length,
                Hash = new ModioFileHash
                {
                    MD5 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty),
                },
                Download = new ModioDownload { URL = "https://downloads.example/mod.zip" },
            };
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

            /// <summary>
            /// Selects the controlled response callback.
            /// </summary>
            /// <param name="response">The request-to-response callback.</param>
            internal Handler(Func<HttpRequestMessage, HttpResponseMessage> response) =>
                _response = response;

            /// <summary>
            /// Returns a controlled response without using a network connection.
            /// </summary>
            /// <param name="request">The outgoing request.</param>
            /// <param name="cancellationToken">The caller's cancellation signal.</param>
            /// <returns>The fake response.</returns>
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            ) => Task.FromResult(_response(request));
        }
    }
}
