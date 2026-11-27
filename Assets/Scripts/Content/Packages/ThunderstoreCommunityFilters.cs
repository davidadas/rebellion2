using System;

/// <summary>
/// Describes community categories and the packages belonging to its listed sections.
/// </summary>
[Serializable]
public sealed class ThunderstoreCommunityFilters
{
    public ThunderstorePackageCategory[] package_categories;
    public ThunderstorePackageSection[] sections;
}

/// <summary>
/// Identifies a category independently of its display name.
/// </summary>
[Serializable]
public sealed class ThunderstorePackageCategory
{
    public string id;
    public string name;
}

/// <summary>
/// Stores a community section and its cached package membership.
/// </summary>
[Serializable]
public sealed class ThunderstorePackageSection
{
    public string uuid;
    public string name;
    public string[] packages = Array.Empty<string>();
}
