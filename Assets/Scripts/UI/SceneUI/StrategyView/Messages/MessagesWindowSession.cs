using System;
using System.Collections.Generic;
using Rebellion.Game.Messages;

/// <summary>
/// Contains identity-backed interaction state for one Messages window instance.
/// </summary>
internal sealed class MessagesWindowSession
{
    private readonly List<Message> messages = new List<Message>();
    private readonly HashSet<string> selectedMessageIds = new HashSet<string>(
        StringComparer.Ordinal
    );
    private string selectedMessageId;

    public MessagesTab ActiveTab { get; private set; } = MessagesTab.All;

    public bool DetailVisible { get; private set; }

    public IReadOnlyList<Message> Messages => messages;

    public string SelectedMessageId => selectedMessageId;

    public UIWindow Window { get; }

    /// <summary>
    /// Creates one Messages window session.
    /// </summary>
    /// <param name="window">The owning Messages window.</param>
    public MessagesWindowSession(UIWindow window)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <summary>
    /// Selects a semantic tab and clears state that does not carry between categories.
    /// </summary>
    /// <param name="tab">The semantic tab to display.</param>
    public void SelectTab(MessagesTab tab)
    {
        ActiveTab = tab;
        DetailVisible = false;
        ClearSelection();
    }

    /// <summary>
    /// Reconciles selected identities against the active tab's current messages.
    /// </summary>
    /// <param name="currentMessages">The active tab's current messages.</param>
    public void Reconcile(IReadOnlyList<Message> currentMessages)
    {
        messages.Clear();
        if (currentMessages != null)
            messages.AddRange(currentMessages);

        HashSet<string> availableMessageIds = GetMessageIDs(messages);
        selectedMessageIds.RemoveWhere(messageId => !availableMessageIds.Contains(messageId));
        if (!availableMessageIds.Contains(selectedMessageId))
            selectedMessageId = null;

        if (availableMessageIds.Count == 0)
        {
            ClearSelection();
            DetailVisible = false;
            return;
        }

        if (DetailVisible && selectedMessageId == null)
            SelectOnly(GetFirstMessage(messages));
    }

    /// <summary>
    /// Selects one source message.
    /// </summary>
    /// <param name="message">The message to select.</param>
    public void SelectOnly(Message message)
    {
        selectedMessageIds.Clear();
        selectedMessageId = GetMessageID(message);
        if (selectedMessageId != null)
            selectedMessageIds.Add(selectedMessageId);
    }

    /// <summary>
    /// Applies replacement, toggle, or range selection to one current message.
    /// </summary>
    /// <param name="message">The message receiving the selection gesture.</param>
    /// <param name="modifiers">The active list-selection modifiers.</param>
    public void Select(Message message, SelectionModifierState modifiers)
    {
        string messageId = GetMessageID(message);
        int messageIndex = FindMessageIndex(messages, messageId);
        if (messageIndex < 0)
            return;

        HashSet<int> selectedIndexes = GetSelectedIndexes();
        SelectableListSelection.SelectIndexedItem(
            selectedIndexes,
            messageIndex,
            messages.Count,
            modifiers
        );
        CaptureSelection(selectedIndexes, messageId);
    }

    /// <summary>
    /// Selects every source message while preserving the primary selection.
    /// </summary>
    public void SelectAll()
    {
        selectedMessageIds.Clear();
        foreach (string messageId in GetMessageIDs(messages))
            selectedMessageIds.Add(messageId);
    }

    /// <summary>
    /// Gets an immutable snapshot of all selected message identifiers.
    /// </summary>
    /// <returns>The selected message identifiers.</returns>
    public IReadOnlyCollection<string> GetSelectedMessageIDs()
    {
        string[] result = new string[selectedMessageIds.Count];
        selectedMessageIds.CopyTo(result);
        return Array.AsReadOnly(result);
    }

    /// <summary>
    /// Resolves the primary selection against a current message projection.
    /// </summary>
    /// <returns>The selected message, or null.</returns>
    public Message GetSelectedMessage()
    {
        if (selectedMessageId == null)
            return null;

        for (int index = 0; index < messages.Count; index++)
        {
            if (GetMessageID(messages[index]) == selectedMessageId)
                return messages[index];
        }

        return null;
    }

    /// <summary>
    /// Selects the nearest unselected message above the primary row, falling back below it.
    /// </summary>
    public void SelectAdjacentMessage()
    {
        int selectedIndex = FindMessageIndex(messages, selectedMessageId);
        if (selectedIndex < 0)
        {
            ClearSelection();
            return;
        }

        for (int index = selectedIndex + 1; index < messages.Count; index++)
        {
            Message message = messages[index];
            string messageId = GetMessageID(message);
            if (messageId != null && !selectedMessageIds.Contains(messageId))
            {
                SelectOnly(message);
                return;
            }
        }

        for (int index = selectedIndex - 1; index >= 0; index--)
        {
            Message message = messages[index];
            string messageId = GetMessageID(message);
            if (messageId != null && !selectedMessageIds.Contains(messageId))
            {
                SelectOnly(message);
                return;
            }
        }

        ClearSelection();
    }

    /// <summary>
    /// Clears primary and multi-selection state.
    /// </summary>
    public void ClearSelection()
    {
        selectedMessageIds.Clear();
        selectedMessageId = null;
    }

    /// <summary>
    /// Moves the primary selection by a signed source-order offset.
    /// </summary>
    /// <param name="direction">The signed source-order offset.</param>
    /// <returns>True when the primary selection changed.</returns>
    public bool MoveSelection(int direction)
    {
        int messageCount = messages.Count;
        int selectedIndex = FindMessageIndex(messages, selectedMessageId);
        int nextIndex = SelectableListSelection.GetMovedIndex(
            selectedIndex,
            messageCount,
            direction
        );
        if (nextIndex == selectedIndex)
            return false;

        SelectOnly(nextIndex >= 0 ? messages[nextIndex] : null);
        return true;
    }

    /// <summary>
    /// Displays message detail for the current primary selection.
    /// </summary>
    public void ShowDetail()
    {
        DetailVisible = true;
    }

    /// <summary>
    /// Returns the session to the message index.
    /// </summary>
    public void HideDetail()
    {
        DetailVisible = false;
    }

    /// <summary>
    /// Gets stable identifiers for a current message projection.
    /// </summary>
    /// <param name="messages">The messages to inspect.</param>
    /// <returns>The available message identifiers.</returns>
    private static HashSet<string> GetMessageIDs(IReadOnlyList<Message> messages)
    {
        HashSet<string> messageIds = new HashSet<string>(StringComparer.Ordinal);
        if (messages == null)
            return messageIds;

        for (int index = 0; index < messages.Count; index++)
        {
            string messageId = GetMessageID(messages[index]);
            if (messageId != null)
                messageIds.Add(messageId);
        }

        return messageIds;
    }

    /// <summary>
    /// Converts the current identity selection to source indexes.
    /// </summary>
    /// <returns>The selected source indexes.</returns>
    private HashSet<int> GetSelectedIndexes()
    {
        HashSet<int> selectedIndexes = new HashSet<int>();
        for (int index = 0; index < messages.Count; index++)
        {
            if (selectedMessageIds.Contains(GetMessageID(messages[index])))
                selectedIndexes.Add(index);
        }

        return selectedIndexes;
    }

    /// <summary>
    /// Rebuilds identity selection from source indexes and reconciles the primary message.
    /// </summary>
    /// <param name="selectedIndexes">The selected source indexes.</param>
    /// <param name="requestedMessageId">The identity receiving the selection gesture.</param>
    private void CaptureSelection(HashSet<int> selectedIndexes, string requestedMessageId)
    {
        selectedMessageIds.Clear();
        foreach (int index in selectedIndexes)
        {
            if (index < 0 || index >= messages.Count)
                continue;

            string messageId = GetMessageID(messages[index]);
            if (messageId != null)
                selectedMessageIds.Add(messageId);
        }

        if (selectedMessageIds.Contains(requestedMessageId))
            selectedMessageId = requestedMessageId;
        else if (!selectedMessageIds.Contains(selectedMessageId))
            selectedMessageId = GetFirstSelectedMessageID();
    }

    /// <summary>
    /// Gets the first selected identity in newest-first display order.
    /// </summary>
    /// <returns>The selected identity, or null.</returns>
    private string GetFirstSelectedMessageID()
    {
        for (int index = messages.Count - 1; index >= 0; index--)
        {
            string messageId = GetMessageID(messages[index]);
            if (selectedMessageIds.Contains(messageId))
                return messageId;
        }

        return null;
    }

    /// <summary>
    /// Gets the first non-null message in a projection.
    /// </summary>
    /// <param name="messages">The messages to inspect.</param>
    /// <returns>The first message, or null.</returns>
    private static Message GetFirstMessage(IReadOnlyList<Message> messages)
    {
        if (messages == null)
            return null;

        for (int index = 0; index < messages.Count; index++)
        {
            if (messages[index] != null)
                return messages[index];
        }

        return null;
    }

    /// <summary>
    /// Finds a message identifier in the current source ordering.
    /// </summary>
    /// <param name="messages">The messages to inspect.</param>
    /// <param name="messageId">The message identifier.</param>
    /// <returns>The matching source index, or negative one.</returns>
    private static int FindMessageIndex(IReadOnlyList<Message> messages, string messageId)
    {
        if (messages == null || messageId == null)
            return -1;

        for (int index = 0; index < messages.Count; index++)
        {
            if (GetMessageID(messages[index]) == messageId)
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Gets the stable identifier for one message.
    /// </summary>
    /// <param name="message">The message to inspect.</param>
    /// <returns>The message identifier, or null.</returns>
    private static string GetMessageID(Message message)
    {
        return message?.InstanceID;
    }
}
