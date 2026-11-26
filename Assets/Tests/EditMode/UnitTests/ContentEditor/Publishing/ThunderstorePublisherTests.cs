using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor.Publishing
{
    [TestFixture]
    public sealed class ThunderstorePublisherTests
    {
        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Forbidden)]
        public async Task PublishAsync_RejectedSession_ClearsTeamsAndRetainsSavedCredentialAsync(
            HttpStatusCode status
        )
        {
            using ResponseHandler handler = new();
            MemoryCredentialStore credentials = new();
            using ThunderstorePublisher publisher = new(handler, credentials);
            int rejections = 0;
            publisher.AuthenticationRejected += () => rejections++;
            await publisher.ConnectAsync("fixture-token", CancellationToken.None);
            handler.Status = status;

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                publisher.PublishAsync(
                    "unused.zip",
                    "Fixture",
                    "Sample",
                    "1.0.0",
                    null,
                    CancellationToken.None
                )
            );

            Assert.IsEmpty(publisher.Teams);
            Assert.AreEqual(1, rejections);
            Assert.IsTrue(credentials.HasCredential);
        }

        [Test]
        public async Task RestoreAsync_AlreadyConnected_RechecksTeamPermissionsWithoutReadingStoreAsync()
        {
            using ResponseHandler handler = new();
            MemoryCredentialStore credentials = new();
            using ThunderstorePublisher publisher = new(handler, credentials);
            await publisher.ConnectAsync("fixture-token", CancellationToken.None);
            handler.Team = "ChangedTeam";

            Assert.IsTrue(await publisher.RestoreAsync(CancellationToken.None));

            CollectionAssert.AreEqual(new[] { "ChangedTeam" }, publisher.Teams);
            Assert.AreEqual(2, handler.Requests);
            Assert.AreEqual(0, credentials.Reads);
        }

        [Test]
        public async Task RestoreAsync_TeamAccessRemoved_ClearsStaleConnectionAsync()
        {
            using ResponseHandler handler = new();
            MemoryCredentialStore credentials = new();
            using ThunderstorePublisher publisher = new(handler, credentials);
            await publisher.ConnectAsync("fixture-token", CancellationToken.None);
            handler.Team = string.Empty;

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                publisher.RestoreAsync(CancellationToken.None)
            );

            Assert.IsEmpty(publisher.Teams);
            Assert.IsTrue(credentials.HasCredential);
        }

        [Test]
        public async Task RestoreAsync_NetworkFailure_PreservesSessionAndSavedCredentialAsync()
        {
            using ResponseHandler handler = new();
            MemoryCredentialStore credentials = new();
            using ThunderstorePublisher publisher = new(handler, credentials);
            await publisher.ConnectAsync("fixture-token", CancellationToken.None);
            handler.Status = HttpStatusCode.ServiceUnavailable;

            Assert.ThrowsAsync<HttpRequestException>(() =>
                publisher.RestoreAsync(CancellationToken.None)
            );

            CollectionAssert.AreEqual(new[] { "Fixture" }, publisher.Teams);
            Assert.IsTrue(credentials.HasCredential);
        }

        [Test]
        public async Task ConnectAsync_ReplacementTokenRejected_PreservesExistingConnectionAsync()
        {
            using ResponseHandler handler = new();
            MemoryCredentialStore credentials = new();
            using ThunderstorePublisher publisher = new(handler, credentials);
            await publisher.ConnectAsync("fixture-token", CancellationToken.None);
            handler.Status = HttpStatusCode.Unauthorized;

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                publisher.ConnectAsync("different-fixture-token", CancellationToken.None)
            );

            CollectionAssert.AreEqual(new[] { "Fixture" }, publisher.Teams);
            Assert.IsTrue(credentials.HasCredential);
        }

        /// <summary>
        /// Returns controlled authentication and permission responses without a network connection.
        /// </summary>
        private sealed class ResponseHandler : HttpMessageHandler
        {
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal string Team = "Fixture";
            internal int Requests;

            /// <summary>
            /// Supplies the next account response without retaining request credentials.
            /// </summary>
            /// <param name="request">The outgoing authentication or publication request.</param>
            /// <param name="cancellationToken">The requested cancellation.</param>
            /// <returns>The configured account response.</returns>
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests++;
                return Task.FromResult(
                    new HttpResponseMessage(Status)
                    {
                        Content = new StringContent(
                            "{\"username\":\"fixture\",\"teams\":[\"" + Team + "\"]}",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                );
            }
        }

        /// <summary>
        /// Records credential-store access without using the operating system's saved connections.
        /// </summary>
        private sealed class MemoryCredentialStore : ContentEditorCredentialStore
        {
            private string credential;
            internal int Reads;
            internal bool HasCredential => credential != null;

            /// <summary>
            /// Creates the fixture-only credential identity.
            /// </summary>
            internal MemoryCredentialStore()
                : base("fixture-only") { }

            /// <summary>
            /// Returns the fixture credential and counts explicit store reads.
            /// </summary>
            /// <param name="cancellationToken">The requested cancellation.</param>
            /// <returns>The saved fixture credential.</returns>
            internal override string Read(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Reads++;
                return credential;
            }

            /// <summary>
            /// Retains only a fixture credential in memory.
            /// </summary>
            /// <param name="value">The fixture token.</param>
            /// <param name="cancellationToken">The requested cancellation.</param>
            internal override void Write(string value, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                credential = value;
            }

            /// <summary>
            /// Clears the fixture credential when explicitly disconnected.
            /// </summary>
            /// <param name="cancellationToken">The requested cancellation.</param>
            internal override void Delete(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                credential = null;
            }
        }
    }
}
