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
    /// Reads community filter choices and resolves section membership for offline filtering.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The community filters and their cacheable document.</returns>
    public async Task<(ThunderstoreCommunityFilters Filters, string Json)> GetCommunityFiltersAsync(
        CancellationToken cancellationToken
    )
    {
        using UnityWebRequest request = UnityWebRequest.Get(
            "https://thunderstore.io/api/cyberstorm/community/rebellion-2/filters/"
        );
        await SendAsync(request, cancellationToken);
        ThunderstoreCommunityFilters filters = ParseCommunityFilters(request.downloadHandler.text);
        for (int offset = 0; offset < filters.sections.Length; offset += 4)
            await Task.WhenAll(
                filters
                    .sections.Skip(offset)
                    .Take(4)
                    .Select(section => ReadSectionPackagesAsync(section, cancellationToken))
            );
        return (filters, JsonUtility.ToJson(filters));
    }

    /// <summary>
    /// Validates cached or downloaded community filter metadata.
    /// </summary>
    /// <param name="json">The community filter document.</param>
    /// <returns>The validated filter choices.</returns>
    public static ThunderstoreCommunityFilters ParseCommunityFilters(string json)
    {
        if (
            string.IsNullOrWhiteSpace(json)
            || !json.TrimStart().StartsWith("{", StringComparison.Ordinal)
        )
            throw new InvalidDataException("Thunderstore did not return community filters.");
        ThunderstoreCommunityFilters result = JsonUtility.FromJson<ThunderstoreCommunityFilters>(
            json
        );
        if (result?.package_categories == null || result.sections == null)
            throw new InvalidDataException("Thunderstore returned incomplete community filters.");
        if (
            result.package_categories.Any(category =>
                category == null
                || !int.TryParse(category.id, out int id)
                || id <= 0
                || string.IsNullOrWhiteSpace(category.name)
            )
            || result.sections.Any(section =>
                section == null
                || !Guid.TryParse(section.uuid, out _)
                || string.IsNullOrWhiteSpace(section.name)
            )
            || result.package_categories.Select(category => category.id).Distinct().Count()
                != result.package_categories.Length
            || result.sections.Select(section => section.uuid).Distinct().Count()
                != result.sections.Length
        )
            throw new InvalidDataException(
                "Thunderstore returned invalid community filter choices."
            );
        foreach (ThunderstorePackageSection section in result.sections)
            section.packages ??= Array.Empty<string>();
        return result;
    }

    /// <summary>
    /// Resolves a section through its public listing query without assuming category rules.
    /// </summary>
    /// <param name="section">The community section to populate.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of the section query.</returns>
    private static async Task ReadSectionPackagesAsync(
        ThunderstorePackageSection section,
        CancellationToken cancellationToken
    )
    {
        System.Collections.Generic.HashSet<string> packages = new(StringComparer.Ordinal);
        for (int page = 1; ; page++)
        {
            using UnityWebRequest request = UnityWebRequest.Get(
                "https://thunderstore.io/api/cyberstorm/listing/rebellion-2/?deprecated=true&nsfw=true&section="
                    + Uri.EscapeDataString(section.uuid)
                    + "&page="
                    + page
            );
            await SendAsync(request, cancellationToken);
            TeamPackagePage result = JsonUtility.FromJson<TeamPackagePage>(
                request.downloadHandler.text
            );
            if (
                result?.results == null
                || result.results.Length == 0 && !string.IsNullOrEmpty(result.next)
            )
                throw new InvalidDataException(
                    "Thunderstore returned an incomplete section listing."
                );
            foreach (TeamPackageReference package in result.results)
            {
                ModPackageStore.ValidateSegment(package.@namespace, "package author");
                ModPackageStore.ValidateSegment(package.name, "package name");
                if (!packages.Add(package.@namespace + "-" + package.name))
                    throw new InvalidDataException(
                        "Thunderstore returned duplicate section packages."
                    );
            }
            if (string.IsNullOrEmpty(result.next))
                break;
        }
        section.packages = packages.OrderBy(package => package, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Reads a team's community listings, including deprecated releases omitted from the browsing catalogue.
    /// </summary>
    /// <param name="team">The connected publishing namespace.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The team's published packages with their latest release metadata.</returns>
    public async Task<ThunderstorePackage[]> GetTeamPackagesAsync(
        string team,
        CancellationToken cancellationToken
    )
    {
        ModPackageStore.ValidateSegment(team, "publishing team");
        System.Collections.Generic.List<ThunderstorePackage> packages = new();
        System.Collections.Generic.HashSet<string> seen = new(StringComparer.Ordinal);
        for (int page = 1; ; page++)
        {
            string url =
                "https://thunderstore.io/api/cyberstorm/listing/rebellion-2/"
                + Uri.EscapeDataString(team)
                + "/?deprecated=true&nsfw=true&page="
                + page;
            using UnityWebRequest listing = UnityWebRequest.Get(url);
            try
            {
                await SendAsync(listing, cancellationToken);
            }
            catch (IOException) when (listing.responseCode == 404 && page == 1)
            {
                return Array.Empty<ThunderstorePackage>();
            }
            TeamPackagePage result = JsonUtility.FromJson<TeamPackagePage>(
                listing.downloadHandler.text
            );
            if (result?.results == null)
                throw new InvalidDataException("Thunderstore did not return this team's packages.");
            if (result.results.Length == 0 && !string.IsNullOrEmpty(result.next))
                throw new InvalidDataException("Thunderstore returned an incomplete package page.");
            foreach (TeamPackageReference item in result.results)
            {
                ModPackageStore.ValidateSegment(item.name, "package name");
                if (item.@namespace != team || !seen.Add(team + "-" + item.name))
                    throw new InvalidDataException(
                        "Thunderstore returned an unexpected team package."
                    );
            }
            for (int offset = 0; offset < result.results.Length; offset += 4)
                packages.AddRange(
                    await Task.WhenAll(
                        result
                            .results.Skip(offset)
                            .Take(4)
                            .Select(item =>
                                ReadTeamPackageAsync(team, item.name, cancellationToken)
                            )
                    )
                );
            if (string.IsNullOrEmpty(result.next))
                return packages.ToArray();
        }
    }

    /// <summary>
    /// Loads canonical release metadata without deriving versions from artwork or download filenames.
    /// </summary>
    /// <param name="team">The publishing namespace.</param>
    /// <param name="name">The published package name.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The validated package's latest release.</returns>
    private static async Task<ThunderstorePackage> ReadTeamPackageAsync(
        string team,
        string name,
        CancellationToken cancellationToken
    )
    {
        using UnityWebRequest request = UnityWebRequest.Get(
            "https://thunderstore.io/api/experimental/package/"
                + Uri.EscapeDataString(team)
                + "/"
                + Uri.EscapeDataString(name)
                + "/"
        );
        await SendAsync(request, cancellationToken);
        TeamPackage detail = JsonUtility.FromJson<TeamPackage>(request.downloadHandler.text);
        if (
            detail == null
            || detail.@namespace != team
            || detail.name != name
            || detail.latest == null
        )
            throw new InvalidDataException(
                "Thunderstore returned incomplete published-package details."
            );
        ModPackageStore.ValidateVersion(detail.latest.version_number);
        ValidateUrl(detail.latest.download_url);
        return new ThunderstorePackage
        {
            name = name,
            owner = team,
            full_name = team + "-" + name,
            package_url =
                CommunityUrl
                + "p/"
                + Uri.EscapeDataString(team)
                + "/"
                + Uri.EscapeDataString(name)
                + "/",
            is_deprecated = detail.is_deprecated,
            versions = new[] { detail.latest },
        };
    }

    /// <summary>
    /// Defines the community's paginated namespace listing response.
    /// </summary>
    [Serializable]
    private sealed class TeamPackagePage
    {
        public string next;
        public TeamPackageReference[] results;
    }

    /// <summary>
    /// Identifies one namespace-scoped community listing.
    /// </summary>
    [Serializable]
    private sealed class TeamPackageReference
    {
        public string @namespace;
        public string name;
    }

    /// <summary>
    /// Defines the latest-release metadata returned for one published package.
    /// </summary>
    [Serializable]
    private sealed class TeamPackage
    {
        public string @namespace;
        public string name;
        public bool is_deprecated;
        public ThunderstorePackageVersion latest;
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
