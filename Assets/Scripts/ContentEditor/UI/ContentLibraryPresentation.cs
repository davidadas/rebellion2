using System;

/// <summary>
/// Selects the installed library, public catalog or known updates.
/// </summary>
public enum ContentLibraryPage
{
    Installed,
    Browse,
    Updates,
}

/// <summary>
/// Supplies reusable library controls with a file-access-free presentation.
/// </summary>
public sealed class ContentLibraryPresentation
{
    public ContentLibraryPage Page { get; set; }
    public bool Busy { get; set; }
    public string Search { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string EmptyMessage { get; set; } = string.Empty;
    public bool CanBrowse { get; set; }
    public bool CanPrevious { get; set; }
    public bool CanNext { get; set; }
    public ContentLibraryRow[] Rows { get; set; } = Array.Empty<ContentLibraryRow>();
    public ContentLibraryRow Selected { get; set; }
}

/// <summary>
/// Describes one selectable library entry and the actions its owner permits.
/// </summary>
public sealed class ContentLibraryRow
{
    public string Key { get; set; }
    public string Name { get; set; }
    public string Summary { get; set; }
    public string Description { get; set; }
    public string Version { get; set; }
    public string Author { get; set; }
    public string ImageURL { get; set; }
    public string Status { get; set; }
    public string Action { get; set; }
    public long Size { get; set; }
    public bool CanInstall { get; set; }
    public bool CanRemove { get; set; }
    public string Directory { get; set; }
    public long ModID { get; set; }
}
