using System.Collections.Generic;
using Rebellion.Game.Messages;
using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
/// Defines messages-window controls, icons, detail artwork, and selection presentation.
/// </summary>
[PersistableObject]
public class MessagesWindowTheme
{
    /// <summary>
    /// Gets or sets the all button.
    /// </summary>
    public WindowButtonImageTheme AllButton { get; set; }

    /// <summary>
    /// Gets or sets the support button.
    /// </summary>
    public WindowButtonImageTheme SupportButton { get; set; }

    /// <summary>
    /// Gets or sets the fleet button.
    /// </summary>
    public WindowButtonImageTheme FleetButton { get; set; }

    /// <summary>
    /// Gets or sets the missions button.
    /// </summary>
    public WindowButtonImageTheme MissionsButton { get; set; }

    /// <summary>
    /// Gets or sets the resource button.
    /// </summary>
    public WindowButtonImageTheme ResourceButton { get; set; }

    /// <summary>
    /// Gets or sets the manufacturing button.
    /// </summary>
    public WindowButtonImageTheme ManufacturingButton { get; set; }

    /// <summary>
    /// Gets or sets the defense button.
    /// </summary>
    public WindowButtonImageTheme DefenseButton { get; set; }

    /// <summary>
    /// Gets or sets the conflict button.
    /// </summary>
    public WindowButtonImageTheme ConflictButton { get; set; }

    /// <summary>
    /// Gets or sets the chat button.
    /// </summary>
    public WindowButtonImageTheme ChatButton { get; set; }

    /// <summary>
    /// Gets or sets the advice button.
    /// </summary>
    public WindowButtonImageTheme AdviceButton { get; set; }

    /// <summary>
    /// Gets or sets the close button.
    /// </summary>
    public WindowButtonImageTheme CloseButton { get; set; }

    /// <summary>
    /// Gets or sets the display button.
    /// </summary>
    public WindowButtonImageTheme DisplayButton { get; set; }

    /// <summary>
    /// Gets or sets the index button.
    /// </summary>
    public WindowButtonImageTheme IndexButton { get; set; }

    /// <summary>
    /// Gets or sets the signal button.
    /// </summary>
    public WindowButtonImageTheme SignalButton { get; set; }

    /// <summary>
    /// Gets or sets the signal silent image path.
    /// </summary>
    public string SignalSilentImagePath { get; set; }

    /// <summary>
    /// Gets or sets the signal target button.
    /// </summary>
    public WindowButtonImageTheme SignalTargetButton { get; set; }

    /// <summary>
    /// Gets or sets the chat command button.
    /// </summary>
    public WindowButtonImageTheme ChatCommandButton { get; set; }

    /// <summary>
    /// Gets or sets the icons.
    /// </summary>
    public List<MessageWindowIconTheme> Icons { get; set; } = new List<MessageWindowIconTheme>();

    /// <summary>
    /// Gets or sets the selection image path.
    /// </summary>
    public string SelectionImagePath { get; set; }

    /// <summary>
    /// Gets or sets the overlay frame image path.
    /// </summary>
    public string OverlayFrameImagePath { get; set; }

    /// <summary>
    /// Gets or sets the button strip image path.
    /// </summary>
    public string ButtonStripImagePath { get; set; }

    /// <summary>
    /// Gets or sets the selected row text color hex.
    /// </summary>
    public string SelectedRowTextColorHex { get; set; }

    /// <summary>
    /// Gets or sets the detail images.
    /// </summary>
    public List<MessageDetailImageTheme> DetailImages { get; set; } =
        new List<MessageDetailImageTheme>();

    private Color selectedRowTextColor;
    private bool selectedRowTextColorParsed;

    /// <summary>
    /// Gets a message detail image path by key.
    /// </summary>
    /// <param name="key">The detail image key.</param>
    /// <returns>The matching image path, or <see langword="null"/>.</returns>
    public string GetDetailImagePath(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        foreach (MessageDetailImageTheme image in DetailImages)
        {
            if (image?.Key == key)
                return image.ImagePath;
        }

        return null;
    }

    /// <summary>
    /// Gets the selected message icon path for a message type.
    /// </summary>
    /// <param name="type">The message type.</param>
    /// <returns>The matching image path, or <see langword="null"/>.</returns>
    public string GetIconImagePath(MessageType type)
    {
        foreach (MessageWindowIconTheme icon in Icons)
        {
            if (icon?.MessageType == type)
                return icon.ImagePath;
        }

        return null;
    }

    /// <summary>
    /// Gets the normal message icon path for a message type.
    /// </summary>
    /// <param name="type">The message type.</param>
    /// <returns>The configured normal path, selected path fallback, or <see langword="null"/>.</returns>
    public string GetNormalIconImagePath(MessageType type)
    {
        foreach (MessageWindowIconTheme icon in Icons)
        {
            if (icon?.MessageType == type)
                return string.IsNullOrEmpty(icon.NormalImagePath)
                    ? icon.ImagePath
                    : icon.NormalImagePath;
        }

        return null;
    }

    /// <summary>
    /// Gets the parsed selected-row text color.
    /// </summary>
    /// <returns>The cached selected-row text color.</returns>
    public Color GetSelectedRowTextColor()
    {
        if (!selectedRowTextColorParsed)
        {
            selectedRowTextColor = ThemeColorParser.Parse(SelectedRowTextColorHex, Color.white);
            selectedRowTextColorParsed = true;
        }

        return selectedRowTextColor;
    }
}

/// <summary>
/// Maps a message detail image key to its artwork.
/// </summary>
[PersistableObject]
public class MessageDetailImageTheme
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the image path.
    /// </summary>
    public string ImagePath { get; set; }
}
