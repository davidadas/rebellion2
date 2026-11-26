using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Describes a published community package using Thunderstore's catalog field names.
/// </summary>
[Serializable]
public sealed class ThunderstorePackage
{
    public string name;
    public string full_name;
    public string owner;
    public string package_url;
    public string date_updated;
    public string date_created;
    public string[] categories;
    public bool is_deprecated;
    public bool has_nsfw_content;
    public int rating_score;
    public ThunderstorePackageVersion[] versions;

    public ThunderstorePackageVersion Latest => versions?.FirstOrDefault();

    /// <summary>
    /// Validates a public package path segment before accepting catalog data.
    /// </summary>
    /// <param name="value">The author or package segment.</param>
    /// <param name="label">The field being checked.</param>
    public static void ValidateName(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, "^[A-Za-z0-9_-]+$"))
            throw new InvalidDataException("The listing contains an invalid " + label + ".");
    }

    /// <summary>
    /// Checks the numeric distribution version used by Thunderstore.
    /// </summary>
    /// <param name="version">The distribution release version.</param>
    public static void ValidateVersion(string version)
    {
        if (
            !System.Version.TryParse(version, out System.Version parsed)
            || parsed.Build < 0
            || parsed.Revision >= 0
        )
            throw new InvalidDataException("The listing contains an invalid package version.");
    }
}

/// <summary>
/// Describes one downloadable package release independently of its authored content version.
/// </summary>
[Serializable]
public sealed class ThunderstorePackageVersion
{
    public string version_number;
    public string description;
    public string download_url;
    public string icon;
    public long file_size;
    public long downloads;
    public string[] dependencies;
}

/// <summary>
/// Records the files and native content identities owned by one installed package.
/// </summary>
[Serializable]
public sealed class InstalledModPackage
{
    public string PackageID;
    public string Name;
    public string Author;
    public string Version;
    public string Description;
    public string Website;
    public string[] Dependencies = Array.Empty<string>();
    public InstalledModContent[] Content = Array.Empty<InstalledModContent>();
    public InstalledModFile[] Files = Array.Empty<InstalledModFile>();
    public bool Local;
}

/// <summary>
/// Identifies an installed add-on or selectable pack and its native requirements.
/// </summary>
[Serializable]
public sealed class InstalledModContent
{
    public string ID;
    public string Version;
    public string DisplayName;
    public string Folder;
    public string BasePackID;
    public string BasePackVersion;
    public bool IsPack;
}

/// <summary>
/// Stores the checksum used to detect local edits before replacing a managed file.
/// </summary>
[Serializable]
public sealed class InstalledModFile
{
    public string Path;
    public string SHA256;
}
