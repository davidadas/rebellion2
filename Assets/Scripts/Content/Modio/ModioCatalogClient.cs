using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Rebellion.Content.Modio
{
    /// <summary>
    /// Browses the public mod catalog and verifies streamed archive downloads.
    /// </summary>
    public sealed class ModioCatalogClient : IDisposable
    {
        private const int _pageSize = 20;
        private readonly ModioConfiguration configuration;
        private readonly HttpClient client;

        /// <summary>
        /// Creates a catalog client using public game credentials.
        /// </summary>
        /// <param name="configuration">The game catalog's public credentials.</param>
        /// <param name="handler">An optional transport supplied by the owner.</param>
        public ModioCatalogClient(
            ModioConfiguration configuration,
            HttpMessageHandler handler = null
        )
        {
            this.configuration =
                configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (!configuration.IsConfigured)
                throw new InvalidOperationException(
                    "mod.io browsing is unavailable in this build."
                );
            if (
                !Uri.TryCreate(configuration.BaseURL, UriKind.Absolute, out Uri baseURL)
                || baseURL.Scheme != Uri.UriSchemeHttps
            )
                throw new InvalidOperationException("The mod.io API address must use HTTPS.");
            client = handler == null ? new HttpClient() : new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Rebellion2");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        /// <summary>
        /// Reads one searchable catalog page.
        /// </summary>
        /// <param name="search">The optional search text.</param>
        /// <param name="offset">The number of matching mods to skip.</param>
        /// <param name="cancellationToken">The owner's cancellation signal.</param>
        /// <returns>The matching catalog page.</returns>
        public Task<ModioCatalogPage> BrowseAsync(
            string search,
            int offset,
            CancellationToken cancellationToken
        )
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            string query =
                $"&_limit={_pageSize}&_offset={offset.ToString(CultureInfo.InvariantCulture)}&_sort=-date_live";
            if (!string.IsNullOrWhiteSpace(search))
                query += "&name-lk=" + Uri.EscapeDataString("*" + search.Trim() + "*");
            return ReadAsync<ModioCatalogPage>("mods", query, cancellationToken);
        }

        /// <summary>
        /// Refreshes a mod's metadata so installation uses its current archive and download URL.
        /// </summary>
        /// <param name="modID">The catalog mod identifier.</param>
        /// <param name="cancellationToken">The owner's cancellation signal.</param>
        /// <returns>The current mod metadata.</returns>
        public Task<ModioMod> GetModAsync(long modID, CancellationToken cancellationToken)
        {
            if (modID <= 0)
                throw new ArgumentOutOfRangeException(nameof(modID));
            return ReadAsync<ModioMod>(
                "mods/" + modID.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                cancellationToken
            );
        }

        /// <summary>
        /// Downloads a published archive, rejecting truncated files and digest mismatches.
        /// </summary>
        /// <param name="file">The current archive metadata.</param>
        /// <param name="destination">The temporary archive path.</param>
        /// <param name="progress">An optional receiver for completed fractions.</param>
        /// <param name="cancellationToken">The owner's cancellation signal.</param>
        /// <returns>The completion of the verified download.</returns>
        public async Task DownloadAsync(
            ModioFile file,
            string destination,
            IProgress<double> progress,
            CancellationToken cancellationToken
        )
        {
            if (
                file == null
                || file.Size <= 0
                || string.IsNullOrWhiteSpace(file.Hash?.MD5)
                || !Uri.TryCreate(file.Download?.URL, UriKind.Absolute, out Uri downloadURL)
                || downloadURL.Scheme != Uri.UriSchemeHttps
            )
                throw new InvalidDataException(
                    "This mod has no valid archive available to download."
                );
            using HttpResponseMessage response = await client
                .GetAsync(downloadURL, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using FileStream output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                true
            );
            using MD5 hash = MD5.Create();
            byte[] buffer = new byte[81920];
            long received = 0;
            int reportedPercent = -1;
            int count;
            while (
                (
                    count = await source
                        .ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                        .ConfigureAwait(false)
                ) != 0
            )
            {
                received = checked(received + count);
                if (received > file.Size)
                    throw new InvalidDataException(
                        "The downloaded archive is larger than its published size."
                    );
                hash.TransformBlock(buffer, 0, count, null, 0);
                await output.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
                int percent = (int)((double)received / file.Size * 100);
                if (percent != reportedPercent)
                {
                    reportedPercent = percent;
                    progress?.Report((double)received / file.Size);
                }
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            string digest = BitConverter.ToString(hash.Hash).Replace("-", string.Empty);
            if (
                received != file.Size
                || !string.Equals(digest, file.Hash.MD5, StringComparison.OrdinalIgnoreCase)
            )
                throw new InvalidDataException(
                    "The downloaded archive failed verification. Please try again."
                );
        }

        /// <summary>
        /// Releases the owned HTTP transport.
        /// </summary>
        public void Dispose() => client.Dispose();

        /// <summary>
        /// Reads a game-scoped API response without exposing credentials in failure messages.
        /// </summary>
        /// <typeparam name="T">The response contract.</typeparam>
        /// <param name="path">The path below the game catalog.</param>
        /// <param name="query">The additional encoded query parameters.</param>
        /// <param name="cancellationToken">The owner's cancellation signal.</param>
        /// <returns>The API response.</returns>
        private async Task<T> ReadAsync<T>(
            string path,
            string query,
            CancellationToken cancellationToken
        )
        {
            string url =
                configuration.BaseURL
                + "games/"
                + configuration.GameID.ToString(CultureInfo.InvariantCulture)
                + "/"
                + path
                + "?api_key="
                + Uri.EscapeDataString(configuration.APIKey)
                + query;
            using HttpResponseMessage response = await client
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"mod.io could not complete the request (HTTP {(int)response.StatusCode}). Please try again."
                );
            using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return ModioJson.Read<T>(stream);
        }
    }
}
