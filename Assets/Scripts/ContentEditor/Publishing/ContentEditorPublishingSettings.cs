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
    /// <param name="definition">The pack whose version and identity are being released.</param>
    internal void Validate(ContentPackDefinition definition)
    {
        if (!Regex.IsMatch(PackageName ?? string.Empty, "^[A-Za-z0-9_]{1,128}$"))
            throw new ArgumentException(
                "Package name must contain 1–128 letters, numbers, or underscores."
            );
        if (string.IsNullOrWhiteSpace(Description) || Description.Length > 250)
            throw new ArgumentException("Enter a description of 1–250 characters.");
        if (
            !Regex.IsMatch(
                definition.Version ?? string.Empty,
                "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$"
            )
        )
            throw new ArgumentException(
                "Set the pack version to three numbers, such as 1.2.0, before exporting."
            );
        if (
            string.IsNullOrWhiteSpace(definition.ID)
            || !Regex.IsMatch(definition.ID, "^[A-Za-z0-9_-]+$")
        )
            throw new ArgumentException(
                "The pack ID must contain only letters, numbers, underscores, or hyphens."
            );
        if (string.IsNullOrWhiteSpace(Readme))
            throw new ArgumentException("Add a readme describing your content pack.");
        if (
            !string.IsNullOrWhiteSpace(WebsiteUrl)
            && (
                !Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out Uri website)
                || website.Scheme is not ("https" or "http")
            )
        )
            throw new ArgumentException("Website must be an http or https address, or left empty.");
        if (
            GetDependencies()
                .Any(dependency =>
                    !Regex.IsMatch(
                        dependency,
                        "^[A-Za-z0-9_]+-[A-Za-z0-9_]+-(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$"
                    )
                )
        )
            throw new ArgumentException(
                "Each dependency must use Team-Package-1.2.0 format, one per line."
            );
        if (string.IsNullOrWhiteSpace(IconPath) || !File.Exists(IconPath))
            throw new ArgumentException("Choose a 256 × 256 PNG package icon.");
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
