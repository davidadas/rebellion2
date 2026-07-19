using System.Collections.Generic;
using Rebellion.Util.Serialization;

/// <summary>
/// Defines the keyed mission icons available to strategy UI.
/// </summary>
[PersistableObject]
public class MissionIconSetTheme
{
    /// <summary>
    /// Gets or sets the icons.
    /// </summary>
    public List<MissionIconTheme> Icons { get; set; } = new List<MissionIconTheme>();

    /// <summary>
    /// Gets a mission icon image path by key and size.
    /// </summary>
    /// <param name="key">The mission icon key.</param>
    /// <param name="small">Whether to select the small image.</param>
    /// <returns>The matching image path, or <see langword="null"/>.</returns>
    public string GetImagePath(string key, bool small)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        foreach (MissionIconTheme icon in Icons)
        {
            if (icon?.Key == key)
                return icon.GetImagePath(small);
        }

        return null;
    }
}

/// <summary>
/// Defines the large and small artwork for one mission icon key.
/// </summary>
[PersistableObject]
public class MissionIconTheme
{
    public string Key { get; set; }

    public string LargeImagePath { get; set; }

    public string SmallImagePath { get; set; }

    /// <summary>
    /// Gets the mission icon image path for the requested size.
    /// </summary>
    /// <param name="small">Whether to select the small image.</param>
    /// <returns>The requested image path.</returns>
    public string GetImagePath(bool small)
    {
        return small ? SmallImagePath : LargeImagePath;
    }
}
