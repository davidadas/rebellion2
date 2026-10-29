using System;
using System.Collections.Generic;
using System.Globalization;
using Rebellion.Game.Messages;
using Rebellion.Game.UIState;

/// <summary>
/// Tracks sampled simulation time, durable pending deadlines, and transient notification cooldowns.
/// </summary>
internal sealed class AdvisorNotificationTiming
{
    private const string _sectionID = "AdvisorNotifications";
    private const string _pendingPrefix = "Pending.";
    private readonly UIStateSection state;
    private readonly Dictionary<AdvisorNotificationType, int> cooldowns =
        new Dictionary<AdvisorNotificationType, int>();
    private int sampledTick;

    /// <summary>
    /// Restores pending deadlines while starting a new presentation clock and cooldown lifetime.
    /// </summary>
    /// <param name="playerState">The local player's durable interface state.</param>
    public AdvisorNotificationTiming(PlayerUIState playerState)
    {
        if (playerState == null)
            throw new ArgumentNullException(nameof(playerState));
        state = playerState.GetOrCreateSection(_sectionID);
    }

    /// <summary>
    /// Samples the simulation counter at the start of an advisor scheduling poll.
    /// </summary>
    /// <param name="currentTick">The current game simulation counter.</param>
    public void SampleClock(int currentTick)
    {
        sampledTick = currentTick;
    }

    /// <summary>
    /// Arms or extends one pending notification using the most recently sampled counter.
    /// </summary>
    /// <param name="type">The semantic notification whose pending slot is updated.</param>
    /// <param name="maxPendingTicks">The authored pending lifetime in simulation ticks.</param>
    public void RecordArrival(AdvisorNotificationType type, int maxPendingTicks)
    {
        state.Values[GetPendingKey(type)] = (sampledTick + maxPendingTicks).ToString(
            CultureInfo.InvariantCulture
        );
    }

    /// <summary>
    /// Checks whether a pending slot survived the previous scheduling poll or save/load cycle.
    /// </summary>
    /// <param name="type">The semantic notification to inspect.</param>
    /// <returns>True when the player's saved interface state contains its pending deadline.</returns>
    public bool HasPending(AdvisorNotificationType type)
    {
        return state.Values.ContainsKey(GetPendingKey(type));
    }

    /// <summary>
    /// Consumes a pending slot before deciding expiry and strict cooldown eligibility.
    /// </summary>
    /// <param name="type">The semantic notification being considered for presentation.</param>
    /// <param name="repeatCooldownTicks">The optional authored repeat cooldown.</param>
    /// <returns>True when its deadline remains live and its cooldown has elapsed.</returns>
    public bool TryConsume(AdvisorNotificationType type, int? repeatCooldownTicks)
    {
        string key = GetPendingKey(type);
        if (!state.Values.TryGetValue(key, out string encodedDeadline))
            return false;
        int deadline = int.Parse(encodedDeadline, CultureInfo.InvariantCulture);
        state.Values.Remove(key);
        if (deadline < sampledTick)
            return false;
        if (repeatCooldownTicks.HasValue)
        {
            cooldowns.TryGetValue(type, out int cooldownUntil);
            if (cooldownUntil >= sampledTick)
                return false;
            cooldowns[type] = sampledTick + repeatCooldownTicks.Value;
        }
        return true;
    }

    /// <summary>
    /// Builds the stable per-type key used by the local presentation state section.
    /// </summary>
    /// <param name="type">The semantic notification identifier.</param>
    /// <returns>The durable pending-deadline key.</returns>
    private static string GetPendingKey(AdvisorNotificationType type)
    {
        return _pendingPrefix + type;
    }
}
