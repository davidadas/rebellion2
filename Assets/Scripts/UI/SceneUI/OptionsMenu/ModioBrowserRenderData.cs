using System;
using System.Collections.Generic;

/// <summary>
/// Contains the state displayed by the community mod browser.
/// </summary>
public sealed class ModioBrowserRenderData
{
    public bool Visible { get; set; }
    public bool Busy { get; set; }
    public bool CanSearch { get; set; }
    public bool CanGoBack { get; set; }
    public bool CanGoForward { get; set; }
    public string Search { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<ModioBrowserRow> Rows { get; set; } = Array.Empty<ModioBrowserRow>();
}

/// <summary>
/// Describes one published mod and its available installation action.
/// </summary>
public sealed class ModioBrowserRow
{
    public string Name { get; set; }
    public string Summary { get; set; }
    public string Version { get; set; }
    public string Action { get; set; }
    public bool CanInstall { get; set; }
}
