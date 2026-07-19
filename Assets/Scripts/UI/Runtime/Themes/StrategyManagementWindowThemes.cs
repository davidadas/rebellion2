using System.Collections.Generic;
using Rebellion.Util.Serialization;

/// <summary>
/// Defines facility-window tabs, selection artwork, and construction artwork.
/// </summary>
[PersistableObject]
public class FacilityWindowTheme
{
    public WindowTabImageTheme ControlTab { get; set; }

    public string SelectionImagePath { get; set; }

    public string RawResourceNodeImagePath { get; set; }

    public List<FacilityConstructionImageTheme> ConstructionImages { get; set; } =
        new List<FacilityConstructionImageTheme>();

    /// <summary>
    /// Gets the construction image path for a facility type.
    /// </summary>
    /// <param name="typeId">The facility type identifier.</param>
    /// <returns>The configured image path, or <see langword="null"/>.</returns>
    public string GetConstructionImagePath(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
            return null;

        foreach (FacilityConstructionImageTheme image in ConstructionImages)
        {
            if (image?.TypeID == typeId)
                return image.ImagePath;
        }

        return null;
    }
}

/// <summary>
/// Maps a facility type to its construction image.
/// </summary>
[PersistableObject]
public class FacilityConstructionImageTheme
{
    public string TypeID { get; set; }

    public string ImagePath { get; set; }
}

/// <summary>
/// Defines defense-window tabs, card backgrounds, and selection artwork.
/// </summary>
[PersistableObject]
public class DefenseWindowTheme
{
    public WindowTabImageTheme PersonnelTab { get; set; }

    public WindowTabImageTheme TroopTab { get; set; }

    public WindowTabImageTheme FighterTab { get; set; }

    public WindowTabImageTheme ShieldTab { get; set; }

    public WindowTabImageTheme BatteryTab { get; set; }

    public string SelectionImagePath { get; set; }

    public string PersonnelBackgroundImagePath { get; set; }

    public string EnrouteBackgroundImagePath { get; set; }
}

/// <summary>
/// Defines fleet-window backgrounds, banner, and tabs.
/// </summary>
[PersistableObject]
public class FleetWindowTheme
{
    public string DetailBackgroundImagePath { get; set; }

    public string BannerImagePath { get; set; }

    public string PersonnelBackgroundImagePath { get; set; }

    public string PersonnelEnrouteBackgroundImagePath { get; set; }

    public FleetWindowTabsTheme Tabs { get; set; }
}

/// <summary>
/// Defines artwork for each fleet-window unit tab.
/// </summary>
[PersistableObject]
public class FleetWindowTabsTheme
{
    public WindowTabImageTheme CapitalShips { get; set; }

    public WindowTabImageTheme Starfighters { get; set; }

    public WindowTabImageTheme Regiments { get; set; }

    public WindowTabImageTheme Officers { get; set; }
}

/// <summary>
/// Defines mission-window tabs and selection artwork.
/// </summary>
[PersistableObject]
public class MissionsWindowTheme
{
    public WindowTabImageTheme AgentsTab { get; set; }

    public WindowTabImageTheme DecoysTab { get; set; }

    public string SelectionImagePath { get; set; }
}

/// <summary>
/// Defines mission-creation title, tabs, and participant headers.
/// </summary>
[PersistableObject]
public class MissionCreateWindowTheme
{
    public string TitleImagePath { get; set; }

    public WindowTabImageTheme MissionTab { get; set; }

    public WindowTabImageTheme PersonnelTab { get; set; }

    public string AgentsHeaderImagePath { get; set; }

    public string DecoysHeaderImagePath { get; set; }
}

/// <summary>
/// Defines status-window backgrounds and unit-state artwork.
/// </summary>
[PersistableObject]
public class StatusWindowTheme
{
    public string BackgroundImagePath { get; set; }

    public string FleetBannerImagePath { get; set; }

    public string FleetBannerEnrouteImagePath { get; set; }

    public string FleetBannerDamagedImagePath { get; set; }

    public string ShipyardImagePath { get; set; }

    public string ConstructionImagePath { get; set; }

    public string TrainingImagePath { get; set; }

    public string EnrouteImagePath { get; set; }
}

/// <summary>
/// Identifies the condition used to select advisor objective text.
/// </summary>
public enum AdvisorObjectiveCondition
{
    PlanetOwnedByFaction,
    HeadquartersOwnedByFaction,
    OfficerCaptured,
}

/// <summary>
/// Defines one advisor-report objective and its conditional presentation.
/// </summary>
[PersistableObject]
public class AdvisorObjectiveTheme
{
    public AdvisorObjectiveCondition Condition { get; set; }

    public string TargetInstanceID { get; set; }

    public string TargetFactionInstanceID { get; set; }

    public string TrueText { get; set; }

    public string FalseText { get; set; }

    public string ImagePath { get; set; }

    public bool ConquestOnly { get; set; }
}

/// <summary>
/// Defines advisor-report backgrounds and objective presentation.
/// </summary>
[PersistableObject]
public class AdvisorReportWindowTheme
{
    public string BackgroundImagePath { get; set; }

    public string GalaxyImagePath { get; set; }

    /// <summary>
    /// Gets or sets the objectives.
    /// </summary>
    public List<AdvisorObjectiveTheme> Objectives { get; set; } = new List<AdvisorObjectiveTheme>();
}
