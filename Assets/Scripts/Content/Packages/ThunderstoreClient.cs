using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Reads the public Rebellion 2 package catalog and streams releases to local staging files.
/// </summary>
public sealed class ThunderstoreClient
{
    public const string CommunityUrl = "https://thunderstore.io/c/rebellion-2/";
    private const string _catalogUrl = CommunityUrl + "api/v1/package/";
    private const string _userAgent = "Rebellion2-ModManager/1.0";

    /// <summary>
    /// Downloads the public catalog without requiring an account or publication credentials.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The package catalog and its cacheable source document.</returns>
    public async Task<(ThunderstorePackage[] Packages, string Json)> GetCatalogAsync(
        CancellationToken cancellationToken
    )
    {
        using UnityWebRequest request = UnityWebRequest.Get(_catalogUrl);
        await SendAsync(request, cancellationToken);
        string json = request.downloadHandler.text;
        return (ParseCatalog(json), json);
    }

    /// <summary>
    /// Reads a cached catalog using the same response model as a live request.
    /// </summary>
    /// <param name="json">The catalog's JSON array.</param>
    /// <returns>The validated package entries.</returns>
    public static ThunderstorePackage[] ParseCatalog(string json)
    {
        if (
            string.IsNullOrWhiteSpace(json)
            || !json.TrimStart().StartsWith("[", StringComparison.Ordinal)
        )
            throw new InvalidDataException("Thunderstore did not return a package catalog.");
        Catalog result = JsonUtility.FromJson<Catalog>("{\"Packages\":" + json + "}");
        ThunderstorePackage[] packages =
            result?.Packages
            ?? throw new InvalidDataException("The Thunderstore catalog could not be read.");
        foreach (ThunderstorePackage package in packages)
        {
            ModPackageStore.ValidateSegment(package.owner, "package author");
            ModPackageStore.ValidateSegment(package.name, "package name");
            if (
                package.full_name != package.owner + "-" + package.name
                || package.versions == null
                || package.versions.Length == 0
            )
                throw new InvalidDataException(
                    "Thunderstore returned an incomplete package listing."
                );
            foreach (ThunderstorePackageVersion version in package.versions)
            {
                ModPackageStore.ValidateVersion(version.version_number);
                ValidateUrl(version.download_url);
                version.dependencies ??= Array.Empty<string>();
            }
        }
        return packages.OrderBy(package => package.full_name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Streams a package into an owned temporary file and checks its advertised archive size.
    /// </summary>
    /// <param name="version">The chosen release.</param>
    /// <param name="destination">The owned temporary archive path.</param>
    /// <param name="progress">The optional download progress callback.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of the download.</returns>
    public async Task DownloadAsync(
        ThunderstorePackageVersion version,
        string destination,
        Action<float> progress,
        CancellationToken cancellationToken
    )
    {
        ValidateUrl(version.download_url);
        using UnityWebRequest request = new UnityWebRequest(
            version.download_url,
            UnityWebRequest.kHttpVerbGET
        );
        request.downloadHandler = new DownloadHandlerFile(destination) { removeFileOnAbort = true };
        await SendAsync(request, cancellationToken, progress);
        if (version.file_size > 0 && new FileInfo(destination).Length != version.file_size)
            throw new InvalidDataException(
                "The downloaded archive does not match the listed package size. Refresh the catalog and retry."
            );
    }

    /// <summary>
    /// Reads the selected listing's icon without retaining a texture owned by the network client.
    /// </summary>
    /// <param name="url">The icon's public URL.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The encoded image bytes.</returns>
    public async Task<byte[]> GetIconAsync(string url, CancellationToken cancellationToken)
    {
        ValidateUrl(url);
        using UnityWebRequest request = UnityWebRequest.Get(url);
        await SendAsync(request, cancellationToken);
        return request.downloadHandler.data;
    }

    /// <summary>
    /// Completes a cancellable Unity request while allowing the application to keep rendering.
    /// </summary>
    /// <param name="request">The owned request.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <param name="progress">The optional download progress callback.</param>
    /// <returns>The request completion.</returns>
    private static async Task SendAsync(
        UnityWebRequest request,
        CancellationToken cancellationToken,
        Action<float> progress = null
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        request.SetRequestHeader("User-Agent", _userAgent);
        request.timeout = progress == null ? 60 : 600;
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        try
        {
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke(Mathf.Clamp01(request.downloadProgress));
                await Task.Yield();
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException(
                    $"Thunderstore request failed (HTTP {request.responseCode}): {request.error}. URL: {request.url}"
                );
            progress?.Invoke(1);
        }
        catch (OperationCanceledException)
        {
            request.Abort();
            throw;
        }
    }

    /// <summary>
    /// Restricts package requests to HTTPS resources belonging to Thunderstore.
    /// </summary>
    /// <param name="url">The catalog-provided address.</param>
    private static void ValidateUrl(string url)
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(
                uri.Host.Equals("thunderstore.io", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".thunderstore.io", StringComparison.OrdinalIgnoreCase)
            )
        )
            throw new InvalidDataException(
                "The package listing contains an unsupported download address."
            );
    }

    /// <summary>
    /// Wraps the public array for Unity's object-oriented JSON reader.
    /// </summary>
    [Serializable]
    private sealed class Catalog
    {
        public ThunderstorePackage[] Packages;
    }
}
