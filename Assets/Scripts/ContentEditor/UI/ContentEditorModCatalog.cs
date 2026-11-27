using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Combines public listings with local installation state for searching and browsing.
/// </summary>
internal sealed class ContentEditorModEntry
{
    internal ThunderstorePackage Listing { get; }
    internal InstalledModPackage Installation { get; }
    internal ContentPackDefinition IncludedPack { get; }
    internal bool IsActivePack { get; }
    internal bool IsInstalled => Installation != null || IncludedPack != null;
    internal string CompatibilityIssue { get; }
    internal string ID => Listing?.full_name ?? Installation?.PackageID ?? IncludedPack.ID;
    internal string Name => Listing?.name ?? Installation?.Name ?? IncludedPack.DisplayName;
    internal string Author => Listing?.owner ?? Installation?.Author ?? "Included with Rebellion 2";
    internal string Description =>
        Listing?.Latest.description ?? Installation?.Description ?? "Installed content pack.";
    internal string Version =>
        Installation?.Version
        ?? Listing?.Latest.version_number
        ?? IncludedPack?.Version
        ?? "Unknown version";
    internal string IconUrl => Listing?.Latest.icon;
    internal string[] Categories => Listing?.categories ?? Array.Empty<string>();
    internal long Downloads => Listing?.versions.Sum(version => version.downloads) ?? 0;
    internal int Likes => Listing?.rating_score ?? 0;
    internal string LikeCount => Likes.ToString("N0") + (Likes == 1 ? " like" : " likes");
    internal string DownloadCount =>
        Downloads.ToString("N0") + (Downloads == 1 ? " download" : " downloads");
    internal string Popularity =>
        Listing == null ? string.Empty : LikeCount + " · " + DownloadCount;
    internal DateTimeOffset Updated => ReadDate(Listing?.date_updated);
    internal DateTimeOffset Created => ReadDate(Listing?.date_created);
    internal bool IsDeprecated => Listing?.is_deprecated == true;
    internal bool HasUpdate =>
        Listing != null
        && Installation != null
        && System.Version.TryParse(Listing.Latest.version_number, out System.Version latest)
        && System.Version.TryParse(Installation.Version, out System.Version installed)
        && latest > installed;
    internal string State =>
        !string.IsNullOrEmpty(CompatibilityIssue) ? "Needs attention"
        : IsActivePack ? "Default content pack"
        : IncludedPack != null ? "Included with the game"
        : HasUpdate ? "Update available"
        : Installation != null
            ? Installation.Local ? "Local"
                : "Installed"
        : IsDeprecated ? "Deprecated"
        : "Available";

    /// <summary>
    /// Associates a public listing with its exact installation record without changing either.
    /// </summary>
    /// <param name="listing">The public listing, or null for local-only content.</param>
    /// <param name="installation">The installation record, or null for an available download.</param>
    /// <param name="compatibilityIssue">The assessed installation requirement, when available.</param>
    /// <param name="includedPack">A pack supplied by the game outside managed mod folders.</param>
    /// <param name="isActivePack">Whether this package contains the selected default pack.</param>
    internal ContentEditorModEntry(
        ThunderstorePackage listing,
        InstalledModPackage installation,
        string compatibilityIssue = null,
        ContentPackDefinition includedPack = null,
        bool isActivePack = false
    )
    {
        Listing = listing;
        Installation = installation;
        CompatibilityIssue = compatibilityIssue;
        IncludedPack = includedPack;
        IsActivePack = isActivePack;
    }

    /// <summary>
    /// Reads catalog timestamps consistently regardless of the user's regional settings.
    /// </summary>
    /// <param name="value">The timestamp supplied by the catalog.</param>
    /// <returns>The supplied timestamp, or the earliest date for local-only content.</returns>
    private static DateTimeOffset ReadDate(string value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out DateTimeOffset date
        )
            ? date
            : DateTimeOffset.MinValue;
}

/// <summary>
/// Searches, filters, and orders the same catalog in both browsing and installed views.
/// </summary>
internal static class ContentEditorModCatalog
{
    /// <summary>
    /// Selects entries using all search words and stable ordering for equal-ranked matches.
    /// </summary>
    /// <param name="catalog">The public package listings.</param>
    /// <param name="installed">The installed package records.</param>
    /// <param name="installedOnly">Whether to show only installed entries.</param>
    /// <param name="query">Words matched across names, authors, descriptions, and categories.</param>
    /// <param name="filters">The combined community and installation filters.</param>
    /// <param name="sort">The requested result ordering.</param>
    /// <returns>The ordered, matching packages.</returns>
    internal static IReadOnlyList<ContentEditorModEntry> Find(
        IReadOnlyList<ThunderstorePackage> catalog,
        IReadOnlyList<InstalledModPackage> installed,
        bool installedOnly,
        string query,
        ContentEditorModFilterOptions filters,
        ContentEditorModSort sort
    )
    {
        Dictionary<string, ThunderstorePackage> listings = catalog.ToDictionary(
            package => package.full_name,
            StringComparer.Ordinal
        );
        Dictionary<string, InstalledModPackage> installations = installed.ToDictionary(
            package => package.PackageID,
            StringComparer.Ordinal
        );
        IEnumerable<ContentEditorModEntry> entries = installedOnly
            ? installed.Select(package => new ContentEditorModEntry(
                listings.TryGetValue(package.PackageID, out ThunderstorePackage listing)
                    ? listing
                    : null,
                package
            ))
            : catalog
                .Select(package => new ContentEditorModEntry(
                    package,
                    installations.TryGetValue(
                        package.full_name,
                        out InstalledModPackage installation
                    )
                        ? installation
                        : null
                ))
                .Concat(
                    installed
                        .Where(package => !listings.ContainsKey(package.PackageID))
                        .Select(package => new ContentEditorModEntry(null, package))
                );
        string[] terms = (query ?? string.Empty)
            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Where(term => term.Length > 0)
            .ToArray();
        var matches = entries
            .Where(entry => installedOnly || MatchesFilters(entry, filters))
            .Select(entry => (Entry: entry, Score: Score(entry, terms)))
            .Where(item => item.Score >= 0);
        var ordered = sort switch
        {
            ContentEditorModSort.Downloads => matches.OrderByDescending(item =>
                item.Entry.Downloads
            ),
            ContentEditorModSort.Likes => matches.OrderByDescending(item => item.Entry.Likes),
            ContentEditorModSort.Updated => matches.OrderByDescending(item => item.Entry.Updated),
            ContentEditorModSort.Newest => matches.OrderByDescending(item => item.Entry.Created),
            ContentEditorModSort.Name => matches.OrderBy(
                item => item.Entry.Name,
                StringComparer.OrdinalIgnoreCase
            ),
            _ => matches
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Entry.Updated),
        };
        return ordered
            .ThenBy(item => item.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Entry.ID, StringComparer.Ordinal)
            .Select(item => item.Entry)
            .ToArray();
    }

    /// <summary>
    /// Evaluates a package's recorded installation and publication state.
    /// </summary>
    /// <param name="entry">The combined catalog entry.</param>
    /// <param name="filter">The requested state.</param>
    /// <returns>Whether the entry belongs in the selected filter.</returns>
    private static bool MatchesFilter(ContentEditorModEntry entry, ContentEditorModFilter filter) =>
        filter switch
        {
            ContentEditorModFilter.NotInstalled => entry.Installation == null,
            ContentEditorModFilter.Installed => entry.Installation != null,
            ContentEditorModFilter.Updates => entry.HasUpdate,
            ContentEditorModFilter.Local => entry.Installation?.Local == true,
            _ => true,
        };

    /// <summary>
    /// Combines category choices, section membership, visibility, and installation state.
    /// </summary>
    /// <param name="entry">The package to inspect.</param>
    /// <param name="filters">The current browsing criteria.</param>
    /// <returns>Whether every active filter admits the package.</returns>
    private static bool MatchesFilters(
        ContentEditorModEntry entry,
        ContentEditorModFilterOptions filters
    ) =>
        (filters.ShowDeprecated || !entry.IsDeprecated)
        && (filters.ShowNsfw || entry.Listing?.has_nsfw_content != true)
        && filters.SectionPackages?.Contains(entry.ID) != false
        && (
            filters.IncludedCategories.Length == 0
            || filters.IncludedCategories.Any(category => MatchesCategory(entry, category))
        )
        && !filters.ExcludedCategories.Any(category => MatchesCategory(entry, category))
        && MatchesFilter(entry, filters.Installation);

    /// <summary>
    /// Matches a named category or the explicit uncategorized choice.
    /// </summary>
    /// <param name="entry">The package to inspect.</param>
    /// <param name="category">The category name, or an empty string for uncategorized packages.</param>
    /// <returns>Whether the package belongs to the category.</returns>
    private static bool MatchesCategory(ContentEditorModEntry entry, string category) =>
        category.Length == 0
            ? entry.Categories.Length == 0
            : entry.Categories.Contains(category, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ranks name and author matches above descriptive matches while requiring every search word.
    /// </summary>
    /// <param name="entry">The searchable package.</param>
    /// <param name="terms">The normalized search words.</param>
    /// <returns>The relevance score, or minus one when any word does not match.</returns>
    private static int Score(ContentEditorModEntry entry, string[] terms)
    {
        string name = Normalize(entry.Name);
        string author = Normalize(entry.Author);
        string identity = Normalize(entry.ID);
        string description = Normalize(entry.Description);
        string categories = Normalize(string.Join(" ", entry.Categories));
        int score = name == string.Join(string.Empty, terms) ? 100 : 0;
        foreach (string term in terms)
        {
            if (name == term)
                score += 40;
            else if (name.StartsWith(term, StringComparison.Ordinal))
                score += 30;
            else if (name.Contains(term))
                score += 20;
            else if (author.Contains(term))
                score += 12;
            else if (identity.Contains(term))
                score += 8;
            else if (categories.Contains(term))
                score += 6;
            else if (description.Contains(term))
                score += 4;
            else
                return -1;
        }
        return score;
    }

    /// <summary>
    /// Ignores case, spacing, punctuation, and accent marks when matching user-entered words.
    /// </summary>
    /// <param name="value">The authored field or entered search word.</param>
    /// <returns>The normalized letters and digits.</returns>
    private static string Normalize(string value)
    {
        StringBuilder result = new StringBuilder();
        foreach (char character in (value ?? string.Empty).Normalize(NormalizationForm.FormD))
            if (char.IsLetterOrDigit(character))
                result.Append(char.ToLowerInvariant(character));
        return result.ToString();
    }
}

internal enum ContentEditorModFilter
{
    All,
    NotInstalled,
    Installed,
    Updates,
    Local,
}

/// <summary>
/// Captures independent browsing criteria without changing installed-package visibility.
/// </summary>
internal sealed class ContentEditorModFilterOptions
{
    internal string[] IncludedCategories { get; set; } = Array.Empty<string>();
    internal string[] ExcludedCategories { get; set; } = Array.Empty<string>();
    internal IReadOnlyCollection<string> SectionPackages { get; set; }
    internal ContentEditorModFilter Installation { get; set; }
    internal bool ShowDeprecated { get; set; }
    internal bool ShowNsfw { get; set; }
}

internal enum ContentEditorModSort
{
    Relevance,
    Downloads,
    Updated,
    Newest,
    Name,
    Likes,
}
