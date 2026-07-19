using System.Collections.Generic;
using Rebellion.Game.Messages;
using Rebellion.Util.Serialization;

/// <summary>
/// Defines planet-marker artwork for each supported map size.
/// </summary>
[PersistableObject]
public class PlanetIcons
{
    public string Small { get; set; }

    public string Medium { get; set; }

    public string Large { get; set; }

    public string XL { get; set; }

    public string Mixed { get; set; }
}

/// <summary>
/// Defines the strategy HUD artwork, source layouts, and interactive controls.
/// </summary>
[PersistableObject]
public class TacticalHUDLayout
{
    public string ImagePath { get; set; }

    public SourceRectLayout TickCounterSourceLayout { get; set; }

    public SourceRectLayout RawMaterialsSourceLayout { get; set; }

    public SourceRectLayout RefinedMaterialsSourceLayout { get; set; }

    public SourceRectLayout MaintenanceSourceLayout { get; set; }

    public SourceRectLayout SpeedIndicatorSourceLayout { get; set; }

    public SourceRectLayout SpeedContextSourceLayout { get; set; }

    public string GalacticInformationDisplayImagePath { get; set; }

    public SourceRectLayout GalacticInformationDisplayImageLayout { get; set; }

    public SpeedIndicatorTheme SpeedIndicators { get; set; }

    public List<StrategyHudMessageNotificationTheme> MessageNotifications { get; set; } =
        new List<StrategyHudMessageNotificationTheme>();

    /// <summary>
    /// Gets or sets the buttons.
    /// </summary>
    public List<StrategyHudButtonTheme> Buttons { get; set; } = new List<StrategyHudButtonTheme>();
}

/// <summary>
/// Defines the action, artwork, and hit area for a strategy HUD button.
/// </summary>
[PersistableObject]
public class StrategyHudButtonTheme
{
    public StrategyHudAction Action { get; set; }

    public string UpImagePath { get; set; }

    public string PressedImagePath { get; set; }

    public SourceRectLayout PressedImageLayout { get; set; }

    public SourceRectLayout HitArea { get; set; }
}

/// <summary>
/// Defines notification artwork and placement for a strategy HUD message category.
/// </summary>
[PersistableObject]
public class StrategyHudMessageNotificationTheme
{
    public MessageType MessageType { get; set; }

    public MessagesTab Tab { get; set; }

    public string DefaultImagePath { get; set; }

    public string HighlightedImagePath { get; set; }

    public SourceRectLayout SourceLayout { get; set; }
}

/// <summary>
/// Defines message-list icon artwork for a message category.
/// </summary>
[PersistableObject]
public class MessageWindowIconTheme
{
    public MessageType MessageType { get; set; }

    public string ImagePath { get; set; }

    public string NormalImagePath { get; set; }
}

/// <summary>
/// Defines speed-indicator artwork for each strategy speed.
/// </summary>
[PersistableObject]
public class SpeedIndicatorTheme
{
    public string PausedImagePath { get; set; }

    public string VerySlowImagePath { get; set; }

    public string SlowImagePath { get; set; }

    public string MediumImagePath { get; set; }

    public string FastImagePath { get; set; }

    /// <summary>
    /// Gets the indicator image path for a source speed value.
    /// </summary>
    /// <param name="sourceSpeed">The source speed value.</param>
    /// <returns>The matching indicator image path.</returns>
    public string GetImagePath(int sourceSpeed)
    {
        return sourceSpeed switch
        {
            1 => VerySlowImagePath,
            2 => SlowImagePath,
            3 => MediumImagePath,
            4 => FastImagePath,
            _ => PausedImagePath,
        };
    }
}

/// <summary>
/// Defines the galaxy background, placement, and planet-marker artwork.
/// </summary>
[PersistableObject]
public class GalaxyBackground
{
    public string ImagePath { get; set; }

    public SourcePointLayout SourcePosition { get; set; }

    public string UnexploredPlanetIconPath { get; set; }

    public string DestroyedPlanetIconPath { get; set; }

    public PlanetIcons PlanetIcons { get; set; }
}
