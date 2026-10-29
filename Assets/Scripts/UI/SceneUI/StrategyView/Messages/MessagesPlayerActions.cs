using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.App;
using Rebellion.App.PlayerActions;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using UnityEngine;

/// <summary>
/// Orders durable Messages requests and rechecks completed state before sending repeated gestures.
/// </summary>
internal sealed class MessagesPlayerActions
{
    private readonly IPlayerActionDispatcher dispatcher;
    private readonly Func<Faction> getFaction;
    private readonly SemaphoreSlim mutationGate = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Binds message gestures to the authenticated player action boundary.
    /// </summary>
    /// <param name="dispatcher">The session's completed-action dispatcher.</param>
    /// <param name="getFaction">Returns the currently displayed player faction.</param>
    public MessagesPlayerActions(IPlayerActionDispatcher dispatcher, Func<Faction> getFaction)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.getFaction = getFaction ?? throw new ArgumentNullException(nameof(getFaction));
    }

    /// <summary>
    /// Marks a delivered message as read after earlier pending message gestures complete.
    /// </summary>
    /// <param name="message">The presented message.</param>
    /// <returns>True when the message is already read or its read request was accepted.</returns>
    public Task<bool> MarkReadAsync(Message message)
    {
        string messageID = message?.InstanceID;
        return ExecuteAsync(() =>
        {
            Message current = GetMessages()
                .FirstOrDefault(candidate => candidate.InstanceID == messageID);
            return current?.Read == false
                ? new ReadMessagesPlayerAction { MessageIDs = new[] { messageID } }
                : null;
        });
    }

    /// <summary>
    /// Acknowledges presented notifications without changing read state.
    /// </summary>
    /// <param name="messages">The messages presented by the window.</param>
    /// <returns>True when the notifications are acknowledged or the request was accepted.</returns>
    public Task<bool> AcknowledgeAsync(IEnumerable<Message> messages)
    {
        HashSet<string> selected = GetIDs(messages?.Select(message => message?.InstanceID));
        return ExecuteAsync(() =>
        {
            string[] pending = GetMessages()
                .Where(message =>
                    selected.Contains(message.InstanceID) && !message.NotificationAcknowledged
                )
                .Select(message => message.InstanceID)
                .ToArray();
            return pending.Length == 0
                ? null
                : new AcknowledgeMessagesPlayerAction { MessageIDs = pending };
        });
    }

    /// <summary>
    /// Removes a stable selection through domain execution rather than editing faction buckets.
    /// </summary>
    /// <param name="messageIDs">The selected delivered message identifiers.</param>
    /// <returns>True when domain execution accepted the removal.</returns>
    public Task<bool> RemoveAsync(IReadOnlyCollection<string> messageIDs)
    {
        string[] selected = GetIDs(messageIDs).ToArray();
        return selected.Length == 0
            ? Task.FromResult(false)
            : ExecuteAsync(() => new RemoveMessagesPlayerAction { MessageIDs = selected });
    }

    /// <summary>
    /// Consumes a report's continuation choice only after the session accepts the request.
    /// </summary>
    /// <param name="message">The report whose continuation choice was presented.</param>
    /// <returns>True when no choice remains or its consumption was accepted.</returns>
    public Task<bool> ConsumeContinuationAsync(Message message)
    {
        string messageID = message?.InstanceID;
        return ExecuteAsync(() =>
        {
            Message current = GetMessages()
                .FirstOrDefault(candidate => candidate.InstanceID == messageID);
            return current?.MissionContinuationPending == true
                ? new ConsumeMissionContinuationPlayerAction { MessageID = messageID }
                : null;
        });
    }

    /// <summary>
    /// Toggles the active category's persisted notification policy through ordered execution.
    /// </summary>
    /// <param name="tab">The selected category or aggregate index.</param>
    /// <returns>True when the session accepted the setting change.</returns>
    public Task<bool> ToggleNotificationAsync(MessagesTab tab)
    {
        return ExecuteAsync(() =>
            new ToggleMessageNotificationPlayerAction
            {
                MessageType = MessagesTabCatalog.GetMessageType(tab),
            }
        );
    }

    /// <summary>
    /// Resolves requests after earlier completions so repeated reads and acknowledgments coalesce.
    /// </summary>
    /// <param name="createRequest">Rechecks current durable state and returns any remaining request.</param>
    /// <returns>True when no change remains or domain execution accepted the request.</returns>
    private async Task<bool> ExecuteAsync(Func<PlayerAction> createRequest)
    {
        await mutationGate.WaitAsync();
        try
        {
            PlayerAction request = createRequest();
            return request == null || (await dispatcher.ExecuteAsync(request)).Accepted;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            mutationGate.Release();
        }
    }

    /// <summary>
    /// Reads the current faction's delivered messages without changing their ordering or state.
    /// </summary>
    /// <returns>The current delivered messages.</returns>
    private IEnumerable<Message> GetMessages()
    {
        Faction faction = getFaction();
        return faction
                ?.Messages?.Values.Where(bucket => bucket != null)
                .SelectMany(bucket => bucket)
                .Where(message => message != null)
            ?? Enumerable.Empty<Message>();
    }

    /// <summary>
    /// Captures unique nonempty message identities before an asynchronous gesture can outlive selection.
    /// </summary>
    /// <param name="messageIDs">The source message identities.</param>
    /// <returns>The captured identity selection.</returns>
    private static HashSet<string> GetIDs(IEnumerable<string> messageIDs)
    {
        return new HashSet<string>(
            (messageIDs ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.Ordinal
        );
    }
}
