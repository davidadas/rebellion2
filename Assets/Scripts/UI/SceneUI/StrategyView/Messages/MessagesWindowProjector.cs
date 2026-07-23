using System;
using System.Collections.Generic;
using Rebellion.Game.Messages;
using UnityEngine;

/// <summary>
/// Projects messages domain state and faction theme resources into immutable view snapshots.
/// </summary>
internal static class MessagesWindowProjector
{
    /// <summary>
    /// Builds the complete presentation for controller-owned Messages state.
    /// </summary>
    /// <param name="uiContext">The current theme and texture context.</param>
    /// <param name="messages">The active tab's messages.</param>
    /// <param name="activeTab">The active semantic Messages tab.</param>
    /// <param name="detailVisible">Whether the detail panel is visible.</param>
    /// <param name="selectedMessageId">The selected source message identifier.</param>
    /// <param name="selectedMessageIds">The selected source message identifiers.</param>
    /// <param name="notificationsEnabled">Whether notifications are enabled for the active tab.</param>
    /// <param name="hasNavigationTarget">Whether the selected message has a strategy target.</param>
    /// <param name="x">The window's source-space x coordinate.</param>
    /// <param name="y">The window's source-space y coordinate.</param>
    /// <returns>The complete immutable messages presentation.</returns>
    internal static MessagesWindowRenderData Project(
        UIContext uiContext,
        IReadOnlyList<Message> messages,
        MessagesTab activeTab,
        bool detailVisible,
        string selectedMessageId,
        IReadOnlyCollection<string> selectedMessageIds,
        bool notificationsEnabled,
        bool hasNavigationTarget,
        int x,
        int y
    )
    {
        IReadOnlyList<Message> safeMessages = messages ?? Array.Empty<Message>();
        int selectedIndex = FindMessageIndex(safeMessages, selectedMessageId);
        bool showDetail = detailVisible && selectedIndex >= 0 && selectedIndex < safeMessages.Count;
        MessagesWindowTheme theme = uiContext?.GetPlayerFactionTheme()?.StrategyWindows?.Messages;

        return new MessagesWindowRenderData(
            showDetail,
            new Vector2Int(x, y),
            GetTexture(uiContext, theme?.OverlayFrameImagePath),
            CreateCommandBarRenderData(
                uiContext,
                theme,
                showDetail,
                activeTab,
                safeMessages.Count,
                notificationsEnabled,
                hasNavigationTarget
            ),
            showDetail
                ? null
                : CreateIndexPanelRenderData(
                    uiContext,
                    theme,
                    activeTab,
                    safeMessages,
                    selectedMessageIds
                ),
            showDetail
                ? CreateDetailPanelRenderData(uiContext, theme, safeMessages, selectedIndex)
                : null
        );
    }

    /// <summary>
    /// Projects the complete command bar for the current panel and selection state.
    /// </summary>
    /// <param name="uiContext">The current texture context.</param>
    /// <param name="theme">The current messages theme.</param>
    /// <param name="panel">Whether the detail panel is visible.</param>
    /// <param name="activeTab">The active semantic Messages tab.</param>
    /// <param name="messageCount">The active tab's message count.</param>
    /// <param name="notificationsEnabled">Whether notifications are enabled.</param>
    /// <param name="hasNavigationTarget">Whether the selected detail has a target.</param>
    /// <returns>The immutable command-bar presentation.</returns>
    private static MessagesCommandBarRenderData CreateCommandBarRenderData(
        UIContext uiContext,
        MessagesWindowTheme theme,
        bool panel,
        MessagesTab activeTab,
        int messageCount,
        bool notificationsEnabled,
        bool hasNavigationTarget
    )
    {
        return new MessagesCommandBarRenderData(
            GetTexture(uiContext, theme?.ButtonStripImagePath),
            CreateCommandButton(uiContext, theme?.CloseButton, true, true, false),
            CreateCommandButton(uiContext, theme?.DisplayButton, !panel, messageCount > 0, false),
            CreateCommandButton(uiContext, theme?.IndexButton, panel, true, false),
            CreateSignalButton(uiContext, theme, notificationsEnabled),
            CreateCommandButton(
                uiContext,
                theme?.SignalTargetButton,
                true,
                hasNavigationTarget,
                false
            ),
            CreateCommandButton(
                uiContext,
                theme?.ChatCommandButton,
                true,
                activeTab != MessagesTab.Chat || panel,
                activeTab == MessagesTab.Chat && !panel
            )
        );
    }

    /// <summary>
    /// Projects one themed command button.
    /// </summary>
    /// <param name="uiContext">The current texture context.</param>
    /// <param name="theme">The configured button theme.</param>
    /// <param name="visible">Whether the button is displayed.</param>
    /// <param name="enabled">Whether the button accepts input.</param>
    /// <param name="pressed">Whether the pressed texture is displayed.</param>
    /// <returns>The immutable button presentation.</returns>
    private static MessagesCommandButtonRenderData CreateCommandButton(
        UIContext uiContext,
        WindowButtonImageTheme theme,
        bool visible,
        bool enabled,
        bool pressed
    )
    {
        Texture texture = GetButtonTexture(uiContext, theme, false, enabled);
        Texture pressedTexture = GetButtonTexture(uiContext, theme, true, enabled) ?? texture;
        if (pressed && enabled)
            texture = pressedTexture;

        return new MessagesCommandButtonRenderData(texture, pressedTexture, visible, enabled);
    }

    /// <summary>
    /// Projects the signal button from current notification policy.
    /// </summary>
    /// <param name="uiContext">The current texture context.</param>
    /// <param name="theme">The current messages theme.</param>
    /// <param name="notificationsEnabled">Whether notifications are enabled.</param>
    /// <returns>The immutable signal-button presentation.</returns>
    private static MessagesCommandButtonRenderData CreateSignalButton(
        UIContext uiContext,
        MessagesWindowTheme theme,
        bool notificationsEnabled
    )
    {
        Texture texture = notificationsEnabled
            ? GetButtonTexture(uiContext, theme?.SignalButton, false, true)
            : GetTexture(uiContext, theme?.SignalSilentImagePath);
        Texture pressedTexture =
            GetButtonTexture(uiContext, theme?.SignalButton, true, true) ?? texture;
        return new MessagesCommandButtonRenderData(texture, pressedTexture, true, true);
    }

    /// <summary>
    /// Projects index tabs and rows with fully resolved faction presentation.
    /// </summary>
    /// <param name="uiContext">The current texture context.</param>
    /// <param name="theme">The current messages theme.</param>
    /// <param name="activeTab">The active semantic Messages tab.</param>
    /// <param name="messages">The active tab's source messages.</param>
    /// <param name="selectedMessageIds">The selected source message identifiers.</param>
    /// <returns>The immutable index-panel presentation.</returns>
    private static MessagesIndexPanelRenderData CreateIndexPanelRenderData(
        UIContext uiContext,
        MessagesWindowTheme theme,
        MessagesTab activeTab,
        IReadOnlyList<Message> messages,
        IReadOnlyCollection<string> selectedMessageIds
    )
    {
        List<MessageWindowRowRenderData> rows = CreateIndexRows(messages, selectedMessageIds);
        for (int index = 0; index < rows.Count; index++)
            rows[index] = ResolveRowPresentation(uiContext, theme, rows[index]);

        return new MessagesIndexPanelRenderData(
            activeTab,
            MessagesTabCatalog.GetTitle(activeTab),
            CreateTabRenderData(uiContext, theme, activeTab),
            rows
        );
    }

    /// <summary>
    /// Builds message-index rows in newest-first display order.
    /// </summary>
    /// <param name="messages">The source messages in storage order.</param>
    /// <param name="selectedMessageIds">The selected message identifiers.</param>
    /// <returns>The semantic row presentations.</returns>
    internal static List<MessageWindowRowRenderData> CreateIndexRows(
        IReadOnlyList<Message> messages,
        IReadOnlyCollection<string> selectedMessageIds
    )
    {
        List<MessageWindowRowRenderData> rows = new List<MessageWindowRowRenderData>();
        if (messages == null)
            return rows;

        for (int index = messages.Count - 1; index >= 0; index--)
        {
            Message message = messages[index];
            string messageId = message?.InstanceID ?? string.Empty;
            rows.Add(
                new MessageWindowRowRenderData(
                    messageId,
                    GetHeader(message),
                    message?.Type ?? default,
                    ContainsMessageID(selectedMessageIds, messageId),
                    message?.Read == false
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Selects the title used for a message row and detail header.
    /// </summary>
    /// <param name="message">The source message.</param>
    /// <returns>The explicit title or the first body line.</returns>
    internal static string GetHeader(Message message)
    {
        if (message == null)
            return string.Empty;

        string header = !string.IsNullOrEmpty(message.Title) ? message.Title : message.Text;
        if (string.IsNullOrEmpty(header))
            return string.Empty;

        int lineBreak = header.IndexOf('\n', StringComparison.Ordinal);
        return lineBreak >= 0 ? header.Substring(0, lineBreak) : header;
    }

    /// <summary>
    /// Resolves one semantic row's icons, selection art, and text color.
    /// </summary>
    /// <param name="uiContext">The current texture context.</param>
    /// <param name="theme">The current messages theme.</param>
    /// <param name="row">The semantic row snapshot.</param>
    /// <returns>The complete immutable row presentation.</returns>
    private static MessageWindowRowRenderData ResolveRowPresentation(
        UIContext uiContext,
        MessagesWindowTheme theme,
        MessageWindowRowRenderData row
    )
    {
        Color32 color =
            row.Selected ? theme?.GetSelectedRowTextColor() ?? Color.white
            : row.Unread ? Color.white
            : Color.gray;
        return new MessageWindowRowRenderData(
            row.MessageId,
            row.Header,
            row.Type,
            row.Selected,
            row.Unread,
            GetTexture(uiContext, theme?.SelectionImagePath),
            GetTexture(uiContext, theme?.GetIconImagePath(row.Type)),
            GetTexture(uiContext, theme?.GetNormalIconImagePath(row.Type)),
            color
        );
    }

    /// <summary>
    /// Resolves all tab textures in authored slot order.
    /// </summary>
    /// <param name="uiContext">The current texture context.</param>
    /// <param name="theme">The current messages theme.</param>
    /// <param name="activeTab">The active semantic Messages tab.</param>
    /// <returns>The immutable tab presentations.</returns>
    private static IReadOnlyList<MessagesTabRenderData> CreateTabRenderData(
        UIContext uiContext,
        MessagesWindowTheme theme,
        MessagesTab activeTab
    )
    {
        List<MessagesTabRenderData> tabs = new List<MessagesTabRenderData>(
            MessagesTabCatalog.Count
        );
        foreach (MessagesTab tab in MessagesTabCatalog.OrderedTabs)
        {
            Texture pressedTexture = GetTabTexture(uiContext, theme, tab, true);
            tabs.Add(
                new MessagesTabRenderData(
                    tab,
                    GetTabTexture(uiContext, theme, tab, tab == activeTab),
                    pressedTexture
                )
            );
        }

        return tabs;
    }

    /// <summary>
    /// Resolves one tab texture from static or faction-themed configuration.
    /// </summary>
    /// <param name="uiContext">The current texture context.</param>
    /// <param name="theme">The current messages theme.</param>
    /// <param name="tab">The semantic Messages tab.</param>
    /// <param name="active">Whether the active-state texture is requested.</param>
    /// <returns>The resolved tab texture.</returns>
    private static Texture GetTabTexture(
        UIContext uiContext,
        MessagesWindowTheme theme,
        MessagesTab tab,
        bool active
    )
    {
        string path = tab switch
        {
            MessagesTab.All => theme?.AllButton?.GetImagePath(active),
            MessagesTab.Support => theme?.SupportButton?.GetImagePath(active),
            MessagesTab.Fleet => theme?.FleetButton?.GetImagePath(active),
            MessagesTab.Mission => theme?.MissionsButton?.GetImagePath(active),
            MessagesTab.Resource => theme?.ResourceButton?.GetImagePath(active),
            MessagesTab.Manufacturing => theme?.ManufacturingButton?.GetImagePath(active),
            MessagesTab.Defense => theme?.DefenseButton?.GetImagePath(active),
            MessagesTab.Conflict => theme?.ConflictButton?.GetImagePath(active),
            MessagesTab.Chat => theme?.ChatButton?.GetImagePath(active),
            MessagesTab.Advice => theme?.AdviceButton?.GetImagePath(active),
            _ => null,
        };
        return GetTexture(uiContext, path);
    }

    /// <summary>
    /// Projects the selected message's resolved detail presentation.
    /// </summary>
    /// <param name="uiContext">The current texture context.</param>
    /// <param name="theme">The current messages theme.</param>
    /// <param name="messages">The active tab's source messages.</param>
    /// <param name="selectedIndex">The selected source message index.</param>
    /// <returns>The immutable detail-panel presentation.</returns>
    private static MessagesDetailPanelRenderData CreateDetailPanelRenderData(
        UIContext uiContext,
        MessagesWindowTheme theme,
        IReadOnlyList<Message> messages,
        int selectedIndex
    )
    {
        selectedIndex = Math.Max(0, Math.Min(selectedIndex, messages.Count - 1));
        Message message = messages[selectedIndex];
        string imagePath = !string.IsNullOrEmpty(message.DisplayImagePath)
            ? message.DisplayImagePath
            : theme?.GetDetailImagePath(GetDetailImageKey(message));
        return new MessagesDetailPanelRenderData(
            message.InstanceID,
            GetHeader(message),
            message.Text,
            GetTexture(uiContext, imagePath),
            GetTexture(uiContext, message.OverlayImagePath),
            GetTexture(uiContext, theme?.GetNormalIconImagePath(message.Type)),
            selectedIndex == 0,
            selectedIndex == messages.Count - 1
        );
    }

    /// <summary>
    /// Selects the configured detail image key for a message.
    /// </summary>
    /// <param name="message">The source message.</param>
    /// <returns>The explicit key or the category default key.</returns>
    private static string GetDetailImageKey(Message message)
    {
        if (!string.IsNullOrEmpty(message?.DisplayImageKey))
            return message.DisplayImageKey;

        return message?.Type == MessageType.Advice ? "advice" : null;
    }

    /// <summary>
    /// Resolves one themed button state, including a configured disabled state.
    /// </summary>
    /// <param name="uiContext">The current texture context.</param>
    /// <param name="theme">The configured button theme.</param>
    /// <param name="pressed">Whether the pressed state is requested.</param>
    /// <param name="enabled">Whether the button is enabled.</param>
    /// <returns>The resolved texture.</returns>
    private static Texture GetButtonTexture(
        UIContext uiContext,
        WindowButtonImageTheme theme,
        bool pressed,
        bool enabled
    )
    {
        if (!enabled && !string.IsNullOrEmpty(theme?.DisabledImagePath))
            return GetTexture(uiContext, theme.DisabledImagePath);

        return GetTexture(uiContext, theme?.GetImagePath(pressed));
    }

    /// <summary>
    /// Resolves a texture without placing resource ownership in the view layer.
    /// </summary>
    /// <param name="uiContext">The current texture context.</param>
    /// <param name="path">The configured resource path.</param>
    /// <returns>The resolved texture, or <see langword="null"/>.</returns>
    private static Texture GetTexture(UIContext uiContext, string path)
    {
        return uiContext?.GetTexture(path);
    }

    /// <summary>
    /// Finds a message identifier in the current source ordering.
    /// </summary>
    /// <param name="messages">The active tab's messages.</param>
    /// <param name="messageId">The message identifier.</param>
    /// <returns>The matching source index, or negative one.</returns>
    private static int FindMessageIndex(IReadOnlyList<Message> messages, string messageId)
    {
        if (messages == null || string.IsNullOrEmpty(messageId))
            return -1;

        for (int index = 0; index < messages.Count; index++)
        {
            if (messages[index]?.InstanceID == messageId)
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Returns whether a message identifier belongs to the selected snapshot.
    /// </summary>
    /// <param name="selectedMessageIds">The selected message identifiers.</param>
    /// <param name="messageId">The identifier to find.</param>
    /// <returns>True when the message is selected.</returns>
    private static bool ContainsMessageID(
        IReadOnlyCollection<string> selectedMessageIds,
        string messageId
    )
    {
        if (selectedMessageIds == null || string.IsNullOrEmpty(messageId))
            return false;

        foreach (string selectedId in selectedMessageIds)
        {
            if (string.Equals(selectedId, messageId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
