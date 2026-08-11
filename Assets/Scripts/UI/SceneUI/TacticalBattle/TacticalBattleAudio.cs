using System;
using System.Collections.Generic;
using Rebellion.Game.Tactical;

/// <summary>
/// Presents faction-specific tactical cues through a single-channel queue.
/// </summary>
internal sealed class TacticalBattleAudio
{
    private readonly Action<string> play;
    private readonly Func<string, float> getDuration;
    private readonly IReadOnlyDictionary<TacticalBattleSide, TacticalBattleTheme> themes;
    private readonly Queue<string> pending = new Queue<string>();
    private float remainingDuration;

    /// <summary>
    /// Creates a tactical cue presenter using resident content audio.
    /// </summary>
    /// <param name="themes">The faction presentation associated with each tactical side.</param>
    /// <param name="play">The operation that starts one addressed cue.</param>
    /// <param name="getDuration">The operation that returns an addressed cue's duration.</param>
    internal TacticalBattleAudio(
        IReadOnlyDictionary<TacticalBattleSide, TacticalBattleTheme> themes,
        Action<string> play,
        Func<string, float> getDuration
    )
    {
        this.themes = themes ?? throw new ArgumentNullException(nameof(themes));
        this.play = play ?? throw new ArgumentNullException(nameof(play));
        this.getDuration = getDuration ?? throw new ArgumentNullException(nameof(getDuration));
    }

    /// <summary>
    /// Queues the arrival cue for one tactical unit.
    /// </summary>
    /// <param name="side">The arriving unit's tactical side.</param>
    internal void QueueArrival(TacticalBattleSide side)
    {
        Enqueue(GetTheme(side).ArrivalAudioPath);
    }

    /// <summary>
    /// Queues cues produced by tactical simulation events in their simulation order.
    /// </summary>
    /// <param name="events">The events produced since the previous simulation update.</param>
    internal void QueueEvents(IReadOnlyList<TacticalCombatEvent> events)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));

        foreach (TacticalCombatEvent combatEvent in events)
        {
            string path = combatEvent.Kind switch
            {
                TacticalCombatEventKind.WeaponImpact => GetImpactPath(combatEvent),
                TacticalCombatEventKind.UnitWithdrawn => GetTheme(
                    combatEvent.Source.Side
                ).WithdrawalAudioPath,
                TacticalCombatEventKind.SuperlaserFired => GetTheme(
                    combatEvent.Source.Side
                ).SuperlaserAudioPath,
                _ => null,
            };
            Enqueue(path);
        }
    }

    /// <summary>
    /// Resolves the original shield-impact family for one completed attack.
    /// </summary>
    /// <param name="combatEvent">The weapon impact to resolve.</param>
    /// <returns>The configured impact cue.</returns>
    private string GetImpactPath(TacticalCombatEvent combatEvent)
    {
        TacticalBattleTheme theme = GetTheme(combatEvent.Target.Side);
        return combatEvent.WeaponType switch
        {
            TacticalWeaponType.IonCannon => combatEvent.PenetratedShields
                ? theme.IonShieldPenetrationAudioPath
                : theme.IonShieldHitAudioPath,
            TacticalWeaponType.Torpedo => combatEvent.PenetratedShields
                ? theme.ProjectileShieldPenetrationAudioPath
                : theme.ProjectileShieldHitAudioPath,
            TacticalWeaponType.Turbolaser or TacticalWeaponType.LaserCannon =>
                combatEvent.PenetratedShields
                    ? theme.EnergyShieldPenetrationAudioPath
                    : theme.EnergyShieldHitAudioPath,
            _ => throw new ArgumentOutOfRangeException(nameof(combatEvent)),
        };
    }

    /// <summary>
    /// Advances the tactical cue queue using real elapsed time, including while combat is paused.
    /// </summary>
    /// <param name="elapsedTime">The unscaled elapsed time in seconds.</param>
    internal void Advance(float elapsedTime)
    {
        if (elapsedTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedTime));

        float availableTime = elapsedTime;
        while (remainingDuration <= availableTime && pending.Count > 0)
        {
            availableTime -= remainingDuration;
            string path = pending.Dequeue();
            play(path);
            remainingDuration = Math.Max(0f, getDuration(path));
        }

        remainingDuration = Math.Max(0f, remainingDuration - availableTime);
    }

    /// <summary>
    /// Adds a configured cue to the pending queue.
    /// </summary>
    /// <param name="path">The optional addressed audio path.</param>
    private void Enqueue(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            pending.Enqueue(path.Trim());
    }

    /// <summary>
    /// Gets the required faction presentation for one tactical side.
    /// </summary>
    /// <param name="side">The tactical side to resolve.</param>
    /// <returns>The configured tactical presentation.</returns>
    private TacticalBattleTheme GetTheme(TacticalBattleSide side)
    {
        if (!themes.TryGetValue(side, out TacticalBattleTheme theme) || theme == null)
            throw new InvalidOperationException($"Tactical audio theme is missing for {side}.");

        return theme;
    }
}
