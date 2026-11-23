using System;
using System.Linq;

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
    public ThunderstorePackageVersion[] versions;

    public ThunderstorePackageVersion Latest => versions?.FirstOrDefault();
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
