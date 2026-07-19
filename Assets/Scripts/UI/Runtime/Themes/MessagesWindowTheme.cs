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
    public WindowButtonImageTheme AllButton { get; set; }

    public WindowButtonImageTheme SupportButton { get; set; }

    public WindowButtonImageTheme FleetButton { get; set; }

    public WindowButtonImageTheme MissionsButton { get; set; }

    public WindowButtonImageTheme ResourceButton { get; set; }

    public WindowButtonImageTheme ManufacturingButton { get; set; }

    public WindowButtonImageTheme DefenseButton { get; set; }

    public WindowButtonImageTheme ConflictButton { get; set; }

    public WindowButtonImageTheme ChatButton { get; set; }

    public WindowButtonImageTheme AdviceButton { get; set; }

    public WindowButtonImageTheme CloseButton { get; set; }

    public WindowButtonImageTheme DisplayButton { get; set; }

    public WindowButtonImageTheme IndexButton { get; set; }

    public WindowButtonImageTheme SignalButton { get; set; }

    public string SignalSilentImagePath { get; set; }

    public WindowButtonImageTheme SignalTargetButton { get; set; }

    public WindowButtonImageTheme ChatCommandButton { get; set; }

    /// <summary>
    /// Gets or sets the icons.
    /// </summary>
    public List<MessageWindowIconTheme> Icons { get; set; } = new List<MessageWindowIconTheme>();

    public string SelectionImagePath { get; set; }

    public string OverlayFrameImagePath { get; set; }

    public string ButtonStripImagePath { get; set; }

    public string SelectedRowTextColorHex { get; set; }

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
    public string Key { get; set; }

    public string ImagePath { get; set; }
}
