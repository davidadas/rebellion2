using System.Collections.Generic;
using Rebellion.Util.Serialization;

/// <summary>
/// Defines facility-window tabs, selection artwork, and construction artwork.
/// </summary>
[PersistableObject]
public class FacilityWindowTheme
{
    /// <summary>
    /// Gets or sets the control tab.
    /// </summary>
    public WindowTabImageTheme ControlTab { get; set; }

    /// <summary>
    /// Gets or sets the selection image path.
    /// </summary>
    public string SelectionImagePath { get; set; }

    /// <summary>
    /// Gets or sets the raw resource node image path.
    /// </summary>
    public string RawResourceNodeImagePath { get; set; }

    /// <summary>
    /// Gets or sets the construction images.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the type ID.
    /// </summary>
    public string TypeID { get; set; }

    /// <summary>
    /// Gets or sets the image path.
    /// </summary>
    public string ImagePath { get; set; }
}

/// <summary>
/// Defines defense-window tabs, card backgrounds, and selection artwork.
/// </summary>
[PersistableObject]
public class DefenseWindowTheme
{
    /// <summary>
    /// Gets or sets the personnel tab.
    /// </summary>
    public WindowTabImageTheme PersonnelTab { get; set; }

    /// <summary>
    /// Gets or sets the troop tab.
    /// </summary>
    public WindowTabImageTheme TroopTab { get; set; }

    /// <summary>
    /// Gets or sets the fighter tab.
    /// </summary>
    public WindowTabImageTheme FighterTab { get; set; }

    /// <summary>
    /// Gets or sets the shield tab.
    /// </summary>
    public WindowTabImageTheme ShieldTab { get; set; }

    /// <summary>
    /// Gets or sets the battery tab.
    /// </summary>
    public WindowTabImageTheme BatteryTab { get; set; }

    /// <summary>
    /// Gets or sets the selection image path.
    /// </summary>
    public string SelectionImagePath { get; set; }

    /// <summary>
    /// Gets or sets the personnel background image path.
    /// </summary>
    public string PersonnelBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the enroute background image path.
    /// </summary>
    public string EnrouteBackgroundImagePath { get; set; }
}

/// <summary>
/// Defines fleet-window backgrounds, banner, and tabs.
/// </summary>
[PersistableObject]
public class FleetWindowTheme
{
    /// <summary>
    /// Gets or sets the detail background image path.
    /// </summary>
    public string DetailBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the banner image path.
    /// </summary>
    public string BannerImagePath { get; set; }

    /// <summary>
    /// Gets or sets the personnel background image path.
    /// </summary>
    public string PersonnelBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the personnel enroute background image path.
    /// </summary>
    public string PersonnelEnrouteBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the tabs.
    /// </summary>
    public FleetWindowTabsTheme Tabs { get; set; }
}

/// <summary>
/// Defines artwork for each fleet-window unit tab.
/// </summary>
[PersistableObject]
public class FleetWindowTabsTheme
{
    /// <summary>
    /// Gets or sets the capital ships.
    /// </summary>
    public WindowTabImageTheme CapitalShips { get; set; }

    /// <summary>
    /// Gets or sets the starfighters.
    /// </summary>
    public WindowTabImageTheme Starfighters { get; set; }

    /// <summary>
    /// Gets or sets the regiments.
    /// </summary>
    public WindowTabImageTheme Regiments { get; set; }

    /// <summary>
    /// Gets or sets the officers.
    /// </summary>
    public WindowTabImageTheme Officers { get; set; }
}

/// <summary>
/// Defines mission-window tabs and selection artwork.
/// </summary>
[PersistableObject]
public class MissionsWindowTheme
{
    /// <summary>
    /// Gets or sets the agents tab.
    /// </summary>
    public WindowTabImageTheme AgentsTab { get; set; }

    /// <summary>
    /// Gets or sets the decoys tab.
    /// </summary>
    public WindowTabImageTheme DecoysTab { get; set; }

    /// <summary>
    /// Gets or sets the selection image path.
    /// </summary>
    public string SelectionImagePath { get; set; }
}

/// <summary>
/// Defines mission-creation title, tabs, and participant headers.
/// </summary>
[PersistableObject]
public class MissionCreateWindowTheme
{
    /// <summary>
    /// Gets or sets the title image path.
    /// </summary>
    public string TitleImagePath { get; set; }

    /// <summary>
    /// Gets or sets the mission tab.
    /// </summary>
    public WindowTabImageTheme MissionTab { get; set; }

    /// <summary>
    /// Gets or sets the personnel tab.
    /// </summary>
    public WindowTabImageTheme PersonnelTab { get; set; }

    /// <summary>
    /// Gets or sets the agents header image path.
    /// </summary>
    public string AgentsHeaderImagePath { get; set; }

    /// <summary>
    /// Gets or sets the decoys header image path.
    /// </summary>
    public string DecoysHeaderImagePath { get; set; }
}

/// <summary>
/// Defines status-window backgrounds and unit-state artwork.
/// </summary>
[PersistableObject]
public class StatusWindowTheme
{
    /// <summary>
    /// Gets or sets the background image path.
    /// </summary>
    public string BackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet banner image path.
    /// </summary>
    public string FleetBannerImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet banner enroute image path.
    /// </summary>
    public string FleetBannerEnrouteImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet banner damaged image path.
    /// </summary>
    public string FleetBannerDamagedImagePath { get; set; }

    /// <summary>
    /// Gets or sets the shipyard image path.
    /// </summary>
    public string ShipyardImagePath { get; set; }

    /// <summary>
    /// Gets or sets the construction image path.
    /// </summary>
    public string ConstructionImagePath { get; set; }

    /// <summary>
    /// Gets or sets the training image path.
    /// </summary>
    public string TrainingImagePath { get; set; }

    /// <summary>
    /// Gets or sets the enroute image path.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the condition.
    /// </summary>
    public AdvisorObjectiveCondition Condition { get; set; }

    /// <summary>
    /// Gets or sets the target instance ID.
    /// </summary>
    public string TargetInstanceID { get; set; }

    /// <summary>
    /// Gets or sets the target faction instance ID.
    /// </summary>
    public string TargetFactionInstanceID { get; set; }

    /// <summary>
    /// Gets or sets the true text.
    /// </summary>
    public string TrueText { get; set; }

    /// <summary>
    /// Gets or sets the false text.
    /// </summary>
    public string FalseText { get; set; }

    /// <summary>
    /// Gets or sets the image path.
    /// </summary>
    public string ImagePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the control is limited to conquest mode.
    /// </summary>
    public bool ConquestOnly { get; set; }
}

/// <summary>
/// Defines advisor-report backgrounds and objective presentation.
/// </summary>
[PersistableObject]
public class AdvisorReportWindowTheme
{
    /// <summary>
    /// Gets or sets the background image path.
    /// </summary>
    public string BackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the galaxy image path.
    /// </summary>
    public string GalaxyImagePath { get; set; }

    /// <summary>
    /// Gets or sets the objectives.
    /// </summary>
    public List<AdvisorObjectiveTheme> Objectives { get; set; } = new List<AdvisorObjectiveTheme>();
}
