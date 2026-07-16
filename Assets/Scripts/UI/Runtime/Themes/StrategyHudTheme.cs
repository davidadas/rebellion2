using System.Collections.Generic;
using Rebellion.Game.Messages;
using Rebellion.Util.Serialization;

/// <summary>
/// Defines planet-marker artwork for each supported map size.
/// </summary>
[PersistableObject]
public class PlanetIcons
{
    /// <summary>
    /// Gets or sets the small planet-marker image path.
    /// </summary>
    public string Small { get; set; }

    /// <summary>
    /// Gets or sets the medium planet-marker image path.
    /// </summary>
    public string Medium { get; set; }

    /// <summary>
    /// Gets or sets the large planet-marker image path.
    /// </summary>
    public string Large { get; set; }

    /// <summary>
    /// Gets or sets the extra-large planet-marker image path.
    /// </summary>
    public string XL { get; set; }

    /// <summary>
    /// Gets or sets the mixed-size planet-marker image path.
    /// </summary>
    public string Mixed { get; set; }
}

/// <summary>
/// Defines the strategy HUD artwork, source layouts, and interactive controls.
/// </summary>
[PersistableObject]
public class TacticalHUDLayout
{
    /// <summary>
    /// Gets or sets the image path.
    /// </summary>
    public string ImagePath { get; set; }

    /// <summary>
    /// Gets or sets the tick counter source layout.
    /// </summary>
    public SourceRectLayout TickCounterSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the raw materials source layout.
    /// </summary>
    public SourceRectLayout RawMaterialsSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the refined materials source layout.
    /// </summary>
    public SourceRectLayout RefinedMaterialsSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the maintenance source layout.
    /// </summary>
    public SourceRectLayout MaintenanceSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the speed indicator source layout.
    /// </summary>
    public SourceRectLayout SpeedIndicatorSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the speed context source layout.
    /// </summary>
    public SourceRectLayout SpeedContextSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the galactic information display image path.
    /// </summary>
    public string GalacticInformationDisplayImagePath { get; set; }

    /// <summary>
    /// Gets or sets the galactic information display image layout.
    /// </summary>
    public SourceRectLayout GalacticInformationDisplayImageLayout { get; set; }

    /// <summary>
    /// Gets or sets the speed indicators.
    /// </summary>
    public SpeedIndicatorTheme SpeedIndicators { get; set; }

    /// <summary>
    /// Gets or sets the message notifications.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the action.
    /// </summary>
    public StrategyHudAction Action { get; set; }

    /// <summary>
    /// Gets or sets the up image path.
    /// </summary>
    public string UpImagePath { get; set; }

    /// <summary>
    /// Gets or sets the pressed image path.
    /// </summary>
    public string PressedImagePath { get; set; }

    /// <summary>
    /// Gets or sets the pressed image layout.
    /// </summary>
    public SourceRectLayout PressedImageLayout { get; set; }

    /// <summary>
    /// Gets or sets the hit area.
    /// </summary>
    public SourceRectLayout HitArea { get; set; }
}

/// <summary>
/// Defines notification artwork and placement for a strategy HUD message category.
/// </summary>
[PersistableObject]
public class StrategyHudMessageNotificationTheme
{
    /// <summary>
    /// Gets or sets the message type.
    /// </summary>
    public MessageType MessageType { get; set; }

    /// <summary>
    /// Gets or sets the tab.
    /// </summary>
    public MessagesTab Tab { get; set; }

    /// <summary>
    /// Gets or sets the default image path.
    /// </summary>
    public string DefaultImagePath { get; set; }

    /// <summary>
    /// Gets or sets the highlighted image path.
    /// </summary>
    public string HighlightedImagePath { get; set; }

    /// <summary>
    /// Gets or sets the source layout.
    /// </summary>
    public SourceRectLayout SourceLayout { get; set; }
}

/// <summary>
/// Defines message-list icon artwork for a message category.
/// </summary>
[PersistableObject]
public class MessageWindowIconTheme
{
    /// <summary>
    /// Gets or sets the message type.
    /// </summary>
    public MessageType MessageType { get; set; }

    /// <summary>
    /// Gets or sets the image path.
    /// </summary>
    public string ImagePath { get; set; }

    /// <summary>
    /// Gets or sets the normal image path.
    /// </summary>
    public string NormalImagePath { get; set; }
}

/// <summary>
/// Defines speed-indicator artwork for each strategy speed.
/// </summary>
[PersistableObject]
public class SpeedIndicatorTheme
{
    /// <summary>
    /// Gets or sets the paused image path.
    /// </summary>
    public string PausedImagePath { get; set; }

    /// <summary>
    /// Gets or sets the very slow image path.
    /// </summary>
    public string VerySlowImagePath { get; set; }

    /// <summary>
    /// Gets or sets the slow image path.
    /// </summary>
    public string SlowImagePath { get; set; }

    /// <summary>
    /// Gets or sets the medium image path.
    /// </summary>
    public string MediumImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fast image path.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the image path.
    /// </summary>
    public string ImagePath { get; set; }

    /// <summary>
    /// Gets or sets the source position.
    /// </summary>
    public SourcePointLayout SourcePosition { get; set; }

    /// <summary>
    /// Gets or sets the unexplored planet icon path.
    /// </summary>
    public string UnexploredPlanetIconPath { get; set; }

    /// <summary>
    /// Gets or sets the destroyed planet icon path.
    /// </summary>
    public string DestroyedPlanetIconPath { get; set; }

    /// <summary>
    /// Gets or sets the size-specific planet-marker artwork.
    /// </summary>
    public PlanetIcons PlanetIcons { get; set; }
}
