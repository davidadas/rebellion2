using System;
using System.Collections.Generic;
using Rebellion.Game.Messages;

/// <summary>
/// Identifies a semantic tab in the authored Messages window order.
/// </summary>
public enum MessagesTab
{
    /// <summary>
    /// Displays messages from every category.
    /// </summary>
    All = 0,

    /// <summary>
    /// Displays popular-support messages.
    /// </summary>
    Support = 1,

    /// <summary>
    /// Displays fleet messages.
    /// </summary>
    Fleet = 2,

    /// <summary>
    /// Displays mission messages.
    /// </summary>
    Mission = 3,

    /// <summary>
    /// Displays resource messages.
    /// </summary>
    Resource = 4,

    /// <summary>
    /// Displays manufacturing messages.
    /// </summary>
    Manufacturing = 5,

    /// <summary>
    /// Displays defense messages.
    /// </summary>
    Defense = 6,

    /// <summary>
    /// Displays conflict messages.
    /// </summary>
    Conflict = 7,

    /// <summary>
    /// Displays chat messages.
    /// </summary>
    Chat = 8,

    /// <summary>
    /// Displays advice messages.
    /// </summary>
    Advice = 9,
}

/// <summary>
/// Defines the single ordered catalog used to validate, project, and route Messages tabs.
/// </summary>
internal static class MessagesTabCatalog
{
    private static readonly MessagesTab[] _orderedTabs =
    {
        MessagesTab.All,
        MessagesTab.Support,
        MessagesTab.Fleet,
        MessagesTab.Mission,
        MessagesTab.Resource,
        MessagesTab.Manufacturing,
        MessagesTab.Defense,
        MessagesTab.Conflict,
        MessagesTab.Chat,
        MessagesTab.Advice,
    };
    private static readonly IReadOnlyList<MessagesTab> _readOnlyOrderedTabs = Array.AsReadOnly(
        _orderedTabs
    );

    /// <summary>
    /// Gets the number of authored Messages tabs.
    /// </summary>
    internal static int Count => _orderedTabs.Length;

    /// <summary>
    /// Gets the Messages tabs in authored slot order.
    /// </summary>
    internal static IReadOnlyList<MessagesTab> OrderedTabs => _readOnlyOrderedTabs;

    /// <summary>
    /// Clamps an external numeric tab value to the authored tab range.
    /// </summary>
    /// <param name="tab">The external zero-based tab value.</param>
    /// <returns>The corresponding authored Messages tab.</returns>
    internal static MessagesTab Clamp(int tab)
    {
        return _orderedTabs[Math.Max(0, Math.Min(tab, _orderedTabs.Length - 1))];
    }

    /// <summary>
    /// Gets the semantic tab assigned to an authored slot.
    /// </summary>
    /// <param name="index">The zero-based authored slot.</param>
    /// <returns>The semantic Messages tab.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the slot is outside the catalog.</exception>
    internal static MessagesTab GetAt(int index)
    {
        if (index < 0 || index >= _orderedTabs.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _orderedTabs[index];
    }

    /// <summary>
    /// Gets the message category represented by a semantic tab.
    /// </summary>
    /// <param name="tab">The semantic Messages tab.</param>
    /// <returns>The represented category, or null for the aggregate tab.</returns>
    internal static MessageType? GetMessageType(MessagesTab tab)
    {
        return tab switch
        {
            MessagesTab.Support => MessageType.PopularSupport,
            MessagesTab.Fleet => MessageType.Fleet,
            MessagesTab.Mission => MessageType.Mission,
            MessagesTab.Resource => MessageType.Resource,
            MessagesTab.Manufacturing => MessageType.Manufacturing,
            MessagesTab.Defense => MessageType.Defense,
            MessagesTab.Conflict => MessageType.Conflict,
            MessagesTab.Chat => MessageType.Chat,
            MessagesTab.Advice => MessageType.Advice,
            _ => null,
        };
    }

    /// <summary>
    /// Gets the displayed title assigned to a semantic tab.
    /// </summary>
    /// <param name="tab">The semantic Messages tab.</param>
    /// <returns>The displayed tab title.</returns>
    internal static string GetTitle(MessagesTab tab)
    {
        return tab switch
        {
            MessagesTab.All => "All Messages",
            MessagesTab.Support => "Popular Support Messages",
            MessagesTab.Fleet => "Fleet Messages",
            MessagesTab.Mission => "Mission Messages",
            MessagesTab.Resource => "Resource Messages",
            MessagesTab.Manufacturing => "Manufacturing Messages",
            MessagesTab.Defense => "Defense Messages",
            MessagesTab.Conflict => "Conflict Messages",
            MessagesTab.Chat => "Chat Messages",
            MessagesTab.Advice => "Advice Messages",
            _ => string.Empty,
        };
    }
}
