using System;
using System.Collections.Generic;
using Rebellion.Game.Messages;
using UnityEngine;

/// <summary>
/// Contains the complete immutable presentation projected for a messages window render.
/// </summary>
public sealed class MessagesWindowRenderData
{
    /// <summary>
    /// Creates a messages window presentation snapshot.
    /// </summary>
    /// <param name="detailVisible">Whether the detail panel is visible.</param>
    /// <param name="framePosition">The window position in source coordinates.</param>
    /// <param name="overlayFrameTexture">The faction-themed overlay frame.</param>
    /// <param name="commandBar">The command-bar presentation.</param>
    /// <param name="indexPanel">The message-index presentation.</param>
    /// <param name="detailPanel">The message-detail presentation.</param>
    public MessagesWindowRenderData(
        bool detailVisible,
        Vector2Int framePosition,
        Texture overlayFrameTexture,
        MessagesCommandBarRenderData commandBar,
        MessagesIndexPanelRenderData indexPanel,
        MessagesDetailPanelRenderData detailPanel
    )
    {
        DetailVisible = detailVisible;
        FramePosition = framePosition;
        OverlayFrameTexture = overlayFrameTexture;
        CommandBar = commandBar ?? throw new ArgumentNullException(nameof(commandBar));
        if (detailVisible && detailPanel == null)
            throw new ArgumentNullException(nameof(detailPanel));
        if (!detailVisible && indexPanel == null)
            throw new ArgumentNullException(nameof(indexPanel));
        IndexPanel = indexPanel;
        DetailPanel = detailPanel;
    }

    public bool DetailVisible { get; }

    public Vector2Int FramePosition { get; }

    public Texture OverlayFrameTexture { get; }

    public MessagesCommandBarRenderData CommandBar { get; }

    public MessagesIndexPanelRenderData IndexPanel { get; }

    public MessagesDetailPanelRenderData DetailPanel { get; }
}

/// <summary>
/// Contains the immutable presentation for the messages command bar.
/// </summary>
public sealed class MessagesCommandBarRenderData
{
    /// <summary>
    /// Creates a command-bar presentation snapshot.
    /// </summary>
    /// <param name="buttonStripTexture">The faction-themed strip texture.</param>
    /// <param name="closeButton">The close-command presentation.</param>
    /// <param name="displayButton">The display-command presentation.</param>
    /// <param name="indexButton">The index-command presentation.</param>
    /// <param name="signalButton">The notification-command presentation.</param>
    /// <param name="signalTargetButton">The target-command presentation.</param>
    /// <param name="chatButton">The chat-command presentation.</param>
    public MessagesCommandBarRenderData(
        Texture buttonStripTexture,
        MessagesCommandButtonRenderData closeButton,
        MessagesCommandButtonRenderData displayButton,
        MessagesCommandButtonRenderData indexButton,
        MessagesCommandButtonRenderData signalButton,
        MessagesCommandButtonRenderData signalTargetButton,
        MessagesCommandButtonRenderData chatButton
    )
    {
        ButtonStripTexture = buttonStripTexture;
        CloseButton = closeButton ?? throw new ArgumentNullException(nameof(closeButton));
        DisplayButton = displayButton ?? throw new ArgumentNullException(nameof(displayButton));
        IndexButton = indexButton ?? throw new ArgumentNullException(nameof(indexButton));
        SignalButton = signalButton ?? throw new ArgumentNullException(nameof(signalButton));
        SignalTargetButton =
            signalTargetButton ?? throw new ArgumentNullException(nameof(signalTargetButton));
        ChatButton = chatButton ?? throw new ArgumentNullException(nameof(chatButton));
    }

    public Texture ButtonStripTexture { get; }

    public MessagesCommandButtonRenderData CloseButton { get; }

    public MessagesCommandButtonRenderData DisplayButton { get; }

    public MessagesCommandButtonRenderData IndexButton { get; }

    public MessagesCommandButtonRenderData SignalButton { get; }

    public MessagesCommandButtonRenderData SignalTargetButton { get; }

    public MessagesCommandButtonRenderData ChatButton { get; }
}

/// <summary>
/// Contains one immutable messages command-button presentation.
/// </summary>
public sealed class MessagesCommandButtonRenderData
{
    /// <summary>
    /// Creates a messages command-button presentation snapshot.
    /// </summary>
    /// <param name="texture">The displayed texture.</param>
    /// <param name="pressedTexture">The pointer-down texture.</param>
    /// <param name="visible">Whether the button is displayed.</param>
    /// <param name="enabled">Whether the button accepts input.</param>
    public MessagesCommandButtonRenderData(
        Texture texture,
        Texture pressedTexture,
        bool visible,
        bool enabled
    )
    {
        Texture = texture;
        PressedTexture = pressedTexture;
        Visible = visible;
        Enabled = enabled;
    }

    public Texture Texture { get; }

    public Texture PressedTexture { get; }

    public bool Visible { get; }

    public bool Enabled { get; }
}

/// <summary>
/// Contains the immutable presentation for the messages index panel.
/// </summary>
public sealed class MessagesIndexPanelRenderData
{
    /// <summary>
    /// Creates a messages index presentation snapshot.
    /// </summary>
    /// <param name="activeTab">The active semantic Messages tab.</param>
    /// <param name="title">The displayed tab title.</param>
    /// <param name="tabs">The tab-control presentations in authored slot order.</param>
    /// <param name="rows">The message-row presentations in display order.</param>
    public MessagesIndexPanelRenderData(
        MessagesTab activeTab,
        string title,
        IReadOnlyList<MessagesTabRenderData> tabs,
        IReadOnlyList<MessageWindowRowRenderData> rows
    )
    {
        ActiveTab = activeTab;
        Title = title ?? string.Empty;
        Tabs = Copy(tabs);
        Rows = Copy(rows);
    }

    public MessagesTab ActiveTab { get; }

    public string Title { get; }

    public IReadOnlyList<MessagesTabRenderData> Tabs { get; }

    public IReadOnlyList<MessageWindowRowRenderData> Rows { get; }

    /// <summary>
    /// Copies a nullable read-only list into an immutable snapshot array.
    /// </summary>
    /// <typeparam name="T">The copied item type.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <returns>A copied read-only collection.</returns>
    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<T>();

        T[] result = new T[source.Count];
        for (int index = 0; index < source.Count; index++)
            result[index] = source[index];
        return Array.AsReadOnly(result);
    }
}

/// <summary>
/// Contains one immutable messages tab presentation.
/// </summary>
public sealed class MessagesTabRenderData
{
    /// <summary>
    /// Creates a messages tab presentation snapshot.
    /// </summary>
    /// <param name="tab">The semantic tab represented by the authored slot.</param>
    /// <param name="texture">The displayed texture.</param>
    /// <param name="pressedTexture">The pointer-down texture.</param>
    public MessagesTabRenderData(MessagesTab tab, Texture texture, Texture pressedTexture)
    {
        Tab = tab;
        Texture = texture;
        PressedTexture = pressedTexture;
    }

    public MessagesTab Tab { get; }

    public Texture Texture { get; }

    public Texture PressedTexture { get; }
}

/// <summary>
/// Contains one immutable projected row in the messages index.
/// </summary>
public sealed class MessageWindowRowRenderData
{
    /// <summary>
    /// Creates a semantic message-row snapshot without resolved artwork.
    /// </summary>
    /// <param name="messageId">The source message identifier.</param>
    /// <param name="header">The displayed row header.</param>
    /// <param name="type">The message category.</param>
    /// <param name="selected">Whether the row is selected.</param>
    /// <param name="unread">Whether the message is unread.</param>
    public MessageWindowRowRenderData(
        string messageId,
        string header,
        MessageType type,
        bool selected,
        bool unread
    )
        : this(messageId, header, type, selected, unread, null, null, null, Color.white) { }

    /// <summary>
    /// Creates a complete message-row presentation snapshot.
    /// </summary>
    /// <param name="messageId">The source message identifier.</param>
    /// <param name="header">The displayed row header.</param>
    /// <param name="type">The message category.</param>
    /// <param name="selected">Whether the row is selected.</param>
    /// <param name="unread">Whether the message is unread.</param>
    /// <param name="selectionTexture">The selected-row background texture.</param>
    /// <param name="selectedIconTexture">The selected-row category icon.</param>
    /// <param name="normalIconTexture">The unselected-row category icon.</param>
    /// <param name="headerColor">The displayed header color.</param>
    public MessageWindowRowRenderData(
        string messageId,
        string header,
        MessageType type,
        bool selected,
        bool unread,
        Texture selectionTexture,
        Texture selectedIconTexture,
        Texture normalIconTexture,
        Color32 headerColor
    )
    {
        MessageId = messageId ?? string.Empty;
        Header = header ?? string.Empty;
        Type = type;
        Selected = selected;
        Unread = unread;
        SelectionTexture = selectionTexture;
        SelectedIconTexture = selectedIconTexture;
        NormalIconTexture = normalIconTexture;
        HeaderColor = headerColor;
    }

    public string MessageId { get; }

    public string Header { get; }

    public MessageType Type { get; }

    public bool Selected { get; }

    public bool Unread { get; }

    public Texture SelectionTexture { get; }

    public Texture SelectedIconTexture { get; }

    public Texture NormalIconTexture { get; }

    public Color32 HeaderColor { get; }
}

/// <summary>
/// Contains the immutable presentation for the selected message detail.
/// </summary>
public sealed class MessagesDetailPanelRenderData
{
    /// <summary>
    /// Creates a message-detail presentation snapshot.
    /// </summary>
    /// <param name="messageId">The selected source message identifier.</param>
    /// <param name="header">The detail header.</param>
    /// <param name="text">The detail body text.</param>
    /// <param name="cardTexture">The primary detail artwork.</param>
    /// <param name="overlayTexture">The optional detail overlay artwork.</param>
    /// <param name="iconTexture">The message-category icon.</param>
    /// <param name="previousDisabled">Whether previous navigation is disabled.</param>
    /// <param name="nextDisabled">Whether next navigation is disabled.</param>
    public MessagesDetailPanelRenderData(
        string messageId,
        string header,
        string text,
        Texture cardTexture,
        Texture overlayTexture,
        Texture iconTexture,
        bool previousDisabled,
        bool nextDisabled
    )
    {
        MessageId = messageId ?? string.Empty;
        Header = header ?? string.Empty;
        Text = text ?? string.Empty;
        CardTexture = cardTexture;
        OverlayTexture = overlayTexture;
        IconTexture = iconTexture;
        PreviousDisabled = previousDisabled;
        NextDisabled = nextDisabled;
    }

    public string MessageId { get; }

    public string Header { get; }

    public string Text { get; }

    public Texture CardTexture { get; }

    public Texture OverlayTexture { get; }

    public Texture IconTexture { get; }

    public bool PreviousDisabled { get; }

    public bool NextDisabled { get; }
}
