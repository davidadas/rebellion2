using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Util.IO;

/// <summary>
/// Retains validated Thunderstore listings and filters for online and offline browsing.
/// </summary>
public sealed class ThunderstoreCatalog
{
    private readonly string directory;
    private readonly ThunderstoreClient _client;
    private ThunderstorePackage[] _catalog;
    private ThunderstoreCommunityFilters _communityFilters;
    private string CommunityFiltersPath => Path.Combine(directory, "community-filters.json");
    public string CachePath => Path.Combine(directory, "catalog.json");

    /// <summary>
    /// Shares one catalog cache between browsing and publishing.
    /// </summary>
    /// <param name="directory">The package manager's cache directory.</param>
    /// <param name="client">The public catalog transport.</param>
    public ThunderstoreCatalog(string directory, ThunderstoreClient client)
    {
        this.directory = directory;
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Reads the shared catalogue snapshot without requiring a network connection.
    /// </summary>
    /// <returns>The last usable package catalogue, or an empty snapshot.</returns>
    public ThunderstorePackage[] Read()
    {
        if (_catalog != null)
            return _catalog;
        try
        {
            _catalog = File.Exists(CachePath)
                ? ThunderstoreClient.ParseCatalog(File.ReadAllText(CachePath))
                : Array.Empty<ThunderstorePackage>();
        }
        catch (Exception exception)
            when (exception is IOException or InvalidDataException or ArgumentException)
        {
            _catalog = Array.Empty<ThunderstorePackage>();
        }
        return _catalog;
    }

    /// <summary>
    /// Refreshes the public catalogue used by browsing and package dependency selection.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The refreshed package snapshot.</returns>
    public async Task<ThunderstorePackage[]> RefreshAsync(CancellationToken cancellationToken)
    {
        var result = await _client.GetCatalogAsync(cancellationToken);
        AtomicTextFile.WriteAllText(CachePath, result.Json);
        _catalog = result.Packages;
        return _catalog;
    }

    /// <summary>
    /// Reads the last successful community filters without requiring a network connection.
    /// </summary>
    /// <returns>The cached choices, or empty choices when no usable snapshot exists.</returns>
    public ThunderstoreCommunityFilters ReadFilters()
    {
        if (_communityFilters != null)
            return _communityFilters;
        try
        {
            _communityFilters = File.Exists(CommunityFiltersPath)
                ? ThunderstoreClient.ParseCommunityFilters(File.ReadAllText(CommunityFiltersPath))
                : new ThunderstoreCommunityFilters();
        }
        catch (Exception exception)
            when (exception is IOException or InvalidDataException or ArgumentException)
        {
            _communityFilters = new ThunderstoreCommunityFilters();
        }
        return _communityFilters;
    }

    /// <summary>
    /// Refreshes community filter metadata while retaining the last usable offline snapshot.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The refreshed community choices and section membership.</returns>
    public async Task<ThunderstoreCommunityFilters> RefreshFiltersAsync(
        CancellationToken cancellationToken
    )
    {
        var result = await _client.GetCommunityFiltersAsync(cancellationToken);
        AtomicTextFile.WriteAllText(CommunityFiltersPath, result.Json);
        _communityFilters = result.Filters;
        return _communityFilters;
    }
}
