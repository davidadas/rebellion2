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

    /// <summary>
    /// Creates one Messages window session.
    /// </summary>
    /// <param name="window">The owning Messages window.</param>
    public MessagesWindowSession(UIWindow window)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public MessagesTab ActiveTab { get; private set; } = MessagesTab.All;

    public bool DetailVisible { get; private set; }

    public IReadOnlyList<Message> Messages => messages;

    public string SelectedMessageId => selectedMessageId;

    public UIWindow Window { get; }

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

        HashSet<string> availableMessageIds = GetMessageIds(messages);
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
        selectedMessageId = GetMessageId(message);
        if (selectedMessageId != null)
            selectedMessageIds.Add(selectedMessageId);
    }

    /// <summary>
    /// Selects every source message while preserving the primary selection.
    /// </summary>
    public void SelectAll()
    {
        selectedMessageIds.Clear();
        foreach (string messageId in GetMessageIds(messages))
            selectedMessageIds.Add(messageId);
    }

    /// <summary>
    /// Gets an immutable snapshot of all selected message identifiers.
    /// </summary>
    /// <returns>The selected message identifiers.</returns>
    public IReadOnlyCollection<string> GetSelectedMessageIds()
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
            if (GetMessageId(messages[index]) == selectedMessageId)
                return messages[index];
        }

        return null;
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
    private static HashSet<string> GetMessageIds(IReadOnlyList<Message> messages)
    {
        HashSet<string> messageIds = new HashSet<string>(StringComparer.Ordinal);
        if (messages == null)
            return messageIds;

        for (int index = 0; index < messages.Count; index++)
        {
            string messageId = GetMessageId(messages[index]);
            if (messageId != null)
                messageIds.Add(messageId);
        }

        return messageIds;
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
            if (GetMessageId(messages[index]) == messageId)
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Gets the stable identifier for one message.
    /// </summary>
    /// <param name="message">The message to inspect.</param>
    /// <returns>The message identifier, or null.</returns>
    private static string GetMessageId(Message message)
    {
        return message?.InstanceID;
    }
}
