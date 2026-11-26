using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Connects a publishing session and streams validated archives to Thunderstore.
/// </summary>
internal sealed class ThunderstorePublisher : IDisposable
{
    private const string _apiRoot = "https://thunderstore.io/api/experimental/";
    private readonly HttpClient client;
    private readonly ContentEditorCredentialStore credentials;
    private string token;
    private bool disposed;
    internal string[] Teams { get; private set; } = Array.Empty<string>();
    internal event Action AuthenticationRejected;

    /// <summary>
    /// Creates an owned HTTP transport that does not forward credentials through redirects.
    /// </summary>
    /// <param name="handler">An optional transport supplied by the host.</param>
    /// <param name="credentialStore">The system credential store, when supplied by the host.</param>
    internal ThunderstorePublisher(
        HttpMessageHandler handler = null,
        ContentEditorCredentialStore credentialStore = null
    )
    {
        credentials =
            credentialStore
            ?? ContentEditorCredentialStore.Create("com.adasgames.rebellion2.toolkit.thunderstore");
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromMinutes(30),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Rebellion2-ContentEditor/1.0");
    }

    /// <summary>
    /// Validates and securely saves a token before marking the connection as established.
    /// </summary>
    /// <param name="credential">The team service-account token.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of authentication and protected storage.</returns>
    internal async Task ConnectAsync(string credential, CancellationToken cancellationToken)
    {
        credential = credential?.Trim();
        string[] teams = await AuthenticateAsync(credential, cancellationToken);
        await Task.Run(() => credentials.Write(credential, cancellationToken), cancellationToken);
        if (disposed)
            return;
        token = credential;
        Teams = teams;
    }

    /// <summary>
    /// Restores a saved connection and checks its current team permissions without rewriting the token.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>Whether a usable saved or current connection exists.</returns>
    internal async Task<bool> RestoreAsync(CancellationToken cancellationToken)
    {
        string credential =
            token ?? await Task.Run(() => credentials.Read(cancellationToken), cancellationToken);
        if (credential == null)
            return false;
        string[] teams = await AuthenticateAsync(credential, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (disposed)
            return false;
        token = credential;
        Teams = teams;
        return true;
    }

    /// <summary>
    /// Removes the protected credential before clearing the live publishing connection.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of removal from the system store.</returns>
    internal async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() => credentials.Delete(cancellationToken), cancellationToken);
        ClearSession();
    }

    /// <summary>
    /// Checks token syntax and the active publishing teams before accepting a connection.
    /// </summary>
    /// <param name="credential">The candidate token.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The authenticated publishing teams.</returns>
    private async Task<string[]> AuthenticateAsync(
        string credential,
        CancellationToken cancellationToken
    )
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ThunderstorePublisher));
        if (
            string.IsNullOrEmpty(credential)
            || credential.Length > 4096
            || credential.Any(char.IsControl)
        )
            throw new ArgumentException("Enter a Thunderstore service-account token.");
        Profile profile = await RequestAsync<Profile>(
            HttpMethod.Get,
            "current-user/",
            null,
            credential,
            cancellationToken
        );
        string[] teams = (profile.teams ?? Array.Empty<string>())
            .Where(team => Regex.IsMatch(team, "^[A-Za-z0-9_]+$"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (string.IsNullOrEmpty(profile.username) || teams.Length == 0)
        {
            RejectSession(credential);
            throw new InvalidOperationException(
                "This token has no active publishing team. Create a service account under your Thunderstore team."
            );
        }
        return teams;
    }

    /// <summary>
    /// Invalidates rejected live access without deleting the saved token or another valid session.
    /// </summary>
    /// <param name="credential">The credential whose access was rejected.</param>
    private void RejectSession(string credential)
    {
        if (token == null || !string.Equals(token, credential, StringComparison.Ordinal))
            return;
        ClearSession();
        AuthenticationRejected?.Invoke();
    }

    /// <summary>
    /// Drops in-memory credentials without changing the device's saved connection.
    /// </summary>
    private void ClearSession()
    {
        token = null;
        Teams = Array.Empty<string>();
    }

    /// <summary>
    /// Streams a package in server-defined parts, then submits its completed upload once.
    /// </summary>
    /// <param name="archive">The validated package archive.</param>
    /// <param name="team">The connected publishing namespace.</param>
    /// <param name="name">The package name in the archive.</param>
    /// <param name="version">The immutable release version.</param>
    /// <param name="progress">The completed upload fraction.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The published release URL.</returns>
    internal async Task<string> PublishAsync(
        string archive,
        string team,
        string name,
        string version,
        IProgress<float> progress,
        CancellationToken cancellationToken
    )
    {
        if (token == null || !Teams.Contains(team, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Connect a Thunderstore token and choose its publishing team."
            );
        string packagePath =
            "package/"
            + Uri.EscapeDataString(team)
            + "/"
            + Uri.EscapeDataString(name)
            + "/"
            + Uri.EscapeDataString(version)
            + "/";
        PackageVersion existing = await RequestAsync<PackageVersion>(
            HttpMethod.Get,
            packagePath,
            null,
            token,
            cancellationToken,
            true
        );
        if (existing != null)
            throw new InvalidOperationException(
                "This version is already published. Increase the content pack version before publishing again."
            );
        string uploadID = null;
        bool finished = false;
        bool submitting = false;
        try
        {
            long size = new FileInfo(archive).Length;
            Upload upload = await RequestAsync<Upload>(
                HttpMethod.Post,
                "usermedia/initiate-upload/",
                new FileData { filename = Path.GetFileName(archive), file_size_bytes = size },
                token,
                cancellationToken
            );
            uploadID = upload.user_media?.uuid;
            if (!Guid.TryParse(uploadID, out _))
                throw new InvalidDataException(
                    "Thunderstore returned an invalid upload identifier."
                );
            UploadPart[] parts = upload.upload_urls?.OrderBy(part => part.offset).ToArray();
            long offset = 0;
            if (parts == null || parts.Length == 0)
                throw new InvalidDataException("Thunderstore did not provide upload parts.");
            foreach (UploadPart part in parts)
            {
                if (
                    part.offset != offset
                    || part.length <= 0
                    || part.length > size - offset
                    || part.part_number <= 0
                )
                    throw new InvalidDataException(
                        "Thunderstore returned inconsistent upload ranges."
                    );
                offset += part.length;
            }
            if (
                offset != size
                || parts.Select(part => part.part_number).Distinct().Count() != parts.Length
            )
                throw new InvalidDataException("Thunderstore returned an incomplete upload plan.");
            CompletedPart[] completed = new CompletedPart[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                UploadPart part = parts[i];
                if (
                    !Uri.TryCreate(part.url, UriKind.Absolute, out Uri url)
                    || url.Scheme != "https"
                    || !string.IsNullOrEmpty(url.UserInfo)
                )
                    throw new InvalidDataException(
                        "Thunderstore returned an invalid upload address."
                    );
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url);
                request.Content = new PackagePartContent(
                    archive,
                    part.offset,
                    part.length,
                    size,
                    progress,
                    cancellationToken
                );
                using HttpResponseMessage response = await client.SendAsync(
                    request,
                    cancellationToken
                );
                if (!response.IsSuccessStatusCode)
                    throw new IOException(
                        "Package upload failed (HTTP "
                            + (int)response.StatusCode
                            + "). You can retry the upload."
                    );
                string etag = response.Headers.ETag?.ToString();
                if (string.IsNullOrEmpty(etag))
                    throw new InvalidDataException(
                        "The upload server did not acknowledge a package part."
                    );
                completed[i] = new CompletedPart { ETag = etag, PartNumber = part.part_number };
            }
            await RequestAsync<Media>(
                HttpMethod.Post,
                "usermedia/" + uploadID + "/finish-upload/",
                new CompletedUpload { parts = completed },
                token,
                cancellationToken
            );
            finished = true;
            progress?.Report(0.95f);
            cancellationToken.ThrowIfCancellationRequested();
            submitting = true;
            Submission result = await RequestAsync<Submission>(
                HttpMethod.Post,
                "submission/submit/",
                new SubmissionData { author_name = team, upload_uuid = uploadID },
                token,
                cancellationToken
            );
            if (
                !string.Equals(
                    result.package_version?.name,
                    name,
                    StringComparison.OrdinalIgnoreCase
                )
                || result.package_version.version_number != version
            )
                throw new InvalidDataException(
                    "Thunderstore did not confirm the expected package version."
                );
            progress?.Report(1);
            return GetPackageUrl(team, name, version);
        }
        catch (Exception exception)
            when (submitting
                && exception
                    is HttpRequestException
                        or OperationCanceledException
                        or InvalidDataException
                        or TimeoutException
            )
        {
            throw new InvalidOperationException(
                "Thunderstore did not confirm publication. It may still complete; check the package page before trying again.",
                exception
            );
        }
        finally
        {
            if (uploadID != null && !finished)
            {
                using CancellationTokenSource cleanup = new CancellationTokenSource(
                    TimeSpan.FromSeconds(5)
                );
                try
                {
                    await RequestAsync<Media>(
                        HttpMethod.Post,
                        "usermedia/" + uploadID + "/abort-upload/",
                        null,
                        token,
                        cleanup.Token
                    );
                }
                catch (Exception)
                {
                    // Expired or unreachable uploads are removed by the server.
                }
            }
        }
    }

    /// <summary>
    /// Builds the community link for a reviewed release without accepting arbitrary hosts.
    /// </summary>
    /// <param name="team">The publishing team.</param>
    /// <param name="name">The package name.</param>
    /// <param name="version">The release version.</param>
    /// <returns>The HTTPS release page.</returns>
    internal static string GetPackageUrl(string team, string name, string version) =>
        ThunderstoreClient.CommunityUrl
        + "p/"
        + Uri.EscapeDataString(team)
        + "/"
        + Uri.EscapeDataString(name)
        + "/v/"
        + Uri.EscapeDataString(version)
        + "/";

    /// <summary>
    /// Sends authenticated JSON only to the fixed API origin and reports bounded server errors.
    /// </summary>
    /// <typeparam name="T">The expected API response.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The fixed API-relative route.</param>
    /// <param name="body">The optional JSON body.</param>
    /// <param name="credential">The session credential.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <param name="allowMissing">Whether a missing release is an expected response.</param>
    /// <returns>The parsed response, or null for an allowed missing release.</returns>
    private async Task<T> RequestAsync<T>(
        HttpMethod method,
        string path,
        object body,
        string credential,
        CancellationToken cancellationToken,
        bool allowMissing = false
    )
        where T : class
    {
        using HttpRequestMessage request = new HttpRequestMessage(method, _apiRoot + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        if (body != null)
            request.Content = new StringContent(
                JsonUtility.ToJson(body),
                Encoding.UTF8,
                "application/json"
            );
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        HttpResponseMessage received;
        try
        {
            received = await client.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Thunderstore timed out. Check your connection and try again."
            );
        }
        using HttpResponseMessage response = received;
        if (allowMissing && response.StatusCode == HttpStatusCode.NotFound)
            return null;
        string json = await response.Content.ReadAsStringAsync();
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                RejectSession(credential);
                throw new InvalidOperationException(
                    "Thunderstore rejected the token or team permissions. Connect a valid service-account token for this team."
                );
            }
            if ((int)response.StatusCode >= 500)
                throw new HttpRequestException(
                    "Thunderstore is temporarily unavailable (HTTP "
                        + (int)response.StatusCode
                        + "). Try again later."
                );
            if ((int)response.StatusCode == 429)
                throw new InvalidOperationException(
                    "Thunderstore is limiting publishing requests. Wait a little before trying again."
                );
            string message = "Check the package name, version, dependencies, and team permissions.";
            if (response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                try
                {
                    ApiProblem problem = JsonUtility.FromJson<ApiProblem>(json);
                    message =
                        problem?.detail
                        ?? problem?.file?.FirstOrDefault()
                        ?? problem?.author_name?.FirstOrDefault()
                        ?? problem?.communities?.FirstOrDefault()
                        ?? problem?.non_field_errors?.FirstOrDefault()
                        ?? problem?.__all__?.FirstOrDefault()
                        ?? message;
                }
                catch (ArgumentException)
                {
                    message = "Thunderstore returned an unreadable error response.";
                }
            }
            if (!string.IsNullOrEmpty(credential))
                message = message.Replace(credential, "[redacted]");
            throw new InvalidOperationException(
                "Thunderstore rejected the request (HTTP "
                    + (int)response.StatusCode
                    + "): "
                    + message
            );
        }
        if (string.IsNullOrWhiteSpace(json) || json.Length > 4 * 1024 * 1024)
            throw new InvalidDataException("Thunderstore returned an invalid publishing response.");
        return JsonUtility.FromJson<T>(json)
            ?? throw new InvalidDataException(
                "Thunderstore returned an empty publishing response."
            );
    }

    /// <summary>
    /// Releases memory and transport resources while preserving the saved connection.
    /// </summary>
    public void Dispose()
    {
        disposed = true;
        ClearSession();
        client.Dispose();
    }

    [Serializable]
    private sealed class ApiProblem
    {
        public string detail;
        public string[] file;
        public string[] author_name;
        public string[] communities;
        public string[] non_field_errors;
        public string[] __all__;
    }

    [Serializable]
    private sealed class Profile
    {
        public string username;
        public string[] teams;
    }

    [Serializable]
    private sealed class FileData
    {
        public string filename;
        public long file_size_bytes;
    }

    [Serializable]
    private sealed class Media
    {
        public string uuid;
    }

    [Serializable]
    private sealed class Upload
    {
        public Media user_media;
        public UploadPart[] upload_urls;
    }

    [Serializable]
    private sealed class UploadPart
    {
        public int part_number;
        public string url;
        public long offset;
        public long length;
    }

    [Serializable]
    private sealed class CompletedPart
    {
        public string ETag;
        public int PartNumber;
    }

    [Serializable]
    private sealed class CompletedUpload
    {
        public CompletedPart[] parts;
    }

    [Serializable]
    private sealed class SubmissionData
    {
        public string author_name;
        public string upload_uuid;
        public string[] communities = { "rebellion-2" };
        public bool has_nsfw_content;
    }

    [Serializable]
    private sealed class Submission
    {
        public PackageVersion package_version;
    }

    [Serializable]
    private sealed class PackageVersion
    {
        public string name;
        public string version_number;
    }

    /// <summary>
    /// Streams one server-requested file range without loading the archive into memory.
    /// </summary>
    private sealed class PackagePartContent : HttpContent
    {
        private readonly string path;
        private readonly long offset;
        private readonly long length;
        private readonly long total;
        private readonly IProgress<float> progress;
        private readonly CancellationToken cancellationToken;

        /// <summary>
        /// Freezes the file range and upload progress destination.
        /// </summary>
        /// <param name="path">The staged archive.</param>
        /// <param name="offset">The first byte in this part.</param>
        /// <param name="length">The number of bytes to send.</param>
        /// <param name="total">The full archive length.</param>
        /// <param name="progress">The UI-context progress sink.</param>
        /// <param name="cancellationToken">The requested cancellation.</param>
        internal PackagePartContent(
            string path,
            long offset,
            long length,
            long total,
            IProgress<float> progress,
            CancellationToken cancellationToken
        )
        {
            this.path = path;
            this.offset = offset;
            this.length = length;
            this.total = total;
            this.progress = progress;
            this.cancellationToken = cancellationToken;
            Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }

        /// <summary>
        /// Reports the exact part length so the server does not require chunked transfer encoding.
        /// </summary>
        /// <param name="value">The requested content length.</param>
        /// <returns>True because the server has already fixed the range length.</returns>
        protected override bool TryComputeLength(out long value)
        {
            value = length;
            return true;
        }

        /// <summary>
        /// Copies exactly the requested range to the upload connection.
        /// </summary>
        /// <param name="stream">The transport destination.</param>
        /// <param name="context">The transport's optional authentication context.</param>
        /// <returns>The completion of this part's upload.</returns>
        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext context
        )
        {
            using FileStream file = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                true
            );
            file.Position = offset;
            byte[] buffer = new byte[81920];
            long sent = 0;
            long reported = 0;
            while (sent < length)
            {
                int count = await file.ReadAsync(
                        buffer,
                        0,
                        (int)Math.Min(buffer.Length, length - sent),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (count == 0)
                    throw new EndOfStreamException("The package archive changed during upload.");
                await stream.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
                sent += count;
                if (sent - reported >= 1024 * 1024 || sent == length)
                {
                    progress?.Report(0.9f * (offset + sent) / total);
                    reported = sent;
                }
            }
        }
    }
}
