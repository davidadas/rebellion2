using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Holds project-local listing information used to prepare a Thunderstore release.
/// </summary>
[Serializable]
internal sealed class ContentEditorPublishingSettings
{
    public string TeamName = string.Empty;
    public string PackageName = string.Empty;
    public string Description = string.Empty;
    public string WebsiteUrl = string.Empty;
    public string Dependencies = string.Empty;
    public string Readme = string.Empty;
    public string Changelog = string.Empty;
    public string IconPath = string.Empty;

    /// <summary>
    /// Checks the listing and release identity before preparing an installable package.
    /// </summary>
    /// <param name="id">The native project identity being released.</param>
    /// <param name="version">The native project release version.</param>
    internal void Validate(string id, string version)
    {
        ValidateListing();
        ValidateRelease(id, version);
    }

    /// <summary>
    /// Checks the required listing fields before advancing to release details.
    /// </summary>
    internal void ValidateListing()
    {
        if (!Regex.IsMatch(PackageName ?? string.Empty, "^[A-Za-z0-9_]{1,128}$"))
            throw new ArgumentException(
                "Package name must contain 1–128 letters, numbers, or underscores."
            );
        if (string.IsNullOrWhiteSpace(Description) || Description.Length > 250)
            throw new ArgumentException("Enter a description of 1–250 characters.");
        if (string.IsNullOrWhiteSpace(Readme))
            throw new ArgumentException("Add a readme describing your project.");
        if (
            !string.IsNullOrWhiteSpace(WebsiteUrl)
            && (
                !Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out Uri website)
                || website.Scheme is not ("https" or "http")
            )
        )
            throw new ArgumentException("Website must be an http or https address, or left empty.");
        if (string.IsNullOrWhiteSpace(IconPath) || !File.Exists(IconPath))
            throw new ArgumentException("Choose a 256 × 256 PNG package icon.");
    }

    /// <summary>
    /// Checks the canonical identity and dependency syntax before opening release review.
    /// </summary>
    /// <param name="id">The native project identity being released.</param>
    /// <param name="version">The native project release version.</param>
    internal void ValidateRelease(string id, string version)
    {
        if (
            !Regex.IsMatch(
                version ?? string.Empty,
                "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$"
            )
        )
            throw new ArgumentException(
                "Set the project version to three numbers, such as 1.2.0, before exporting."
            );
        if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, "^[A-Za-z0-9_-]+$"))
            throw new ArgumentException(
                "The project ID must contain only letters, numbers, underscores, or hyphens."
            );
        if (
            GetDependencies()
                .Any(dependency =>
                    !Regex.IsMatch(
                        dependency,
                        "^[A-Za-z0-9_-]+-[A-Za-z0-9_-]+-(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$"
                    )
                )
        )
            throw new ArgumentException(
                "A saved dependency is invalid. Remove it and choose the package again."
            );
    }

    /// <summary>
    /// Returns distinct dependency references in the author's chosen order.
    /// </summary>
    /// <returns>The normalized Thunderstore package references.</returns>
    internal string[] GetDependencies() =>
        (Dependencies ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
