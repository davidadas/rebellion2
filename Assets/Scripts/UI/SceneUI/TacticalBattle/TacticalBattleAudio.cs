using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;

/// <summary>
/// Presents faction-specific tactical cues through bounded tactical audio channels.
/// </summary>
internal sealed class TacticalBattleAudio
{
    private const float _combatCueLifetime = 3f;
    private const int _mediumDestructionHullThreshold = 1100;
    private const int _largeDestructionHullThreshold = 2000;
    private const float _unitCueLifetime = 8f;
    private const float _voiceCueLifetime = 8f;
    private readonly Action<string> play;
    private readonly Func<string, float> getDuration;
    private readonly IReadOnlyDictionary<TacticalBattleSide, TacticalBattleTheme> themes;
    private readonly Queue<PendingCue> combatCues = new Queue<PendingCue>();
    private readonly Queue<PendingCue> unitCues = new Queue<PendingCue>();
    private readonly Queue<PendingCue> voiceCues = new Queue<PendingCue>();
    private float combatCueRemainingDuration;
    private float elapsedTime;
    private float unitCueRemainingDuration;
    private float voiceCueRemainingDuration;

    /// <summary>
    /// Gets whether every queued spoken report has finished.
    /// </summary>
    internal bool IsVoiceIdle => voiceCues.Count == 0 && voiceCueRemainingDuration <= 0f;

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
    /// Queues the unit-class arrival cue for one tactical unit.
    /// </summary>
    /// <param name="unit">The arriving tactical unit.</param>
    internal void QueueArrival(TacticalUnitState unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        Enqueue(GetArrivalPath(unit), TacticalAudioChannel.Unit);
    }

    /// <summary>
    /// Queues the played faction's opening fleet-ready report.
    /// </summary>
    /// <param name="side">The tactical side reporting readiness.</param>
    internal void QueueFleetReady(TacticalBattleSide side)
    {
        TacticalVoiceTheme voice = GetTheme(side).Voice;
        Enqueue(voice?.GetAudioPath(voice.FleetReady), TacticalAudioChannel.Voice);
    }

    /// <summary>
    /// Queues the selected command group's request for orders.
    /// </summary>
    /// <param name="side">The side whose group was selected.</param>
    /// <param name="kind">The kind of selected command group.</param>
    /// <param name="groupIndex">The zero-based command-group number.</param>
    internal void QueueOrdersRequested(
        TacticalBattleSide side,
        TacticalUnitKind kind,
        int groupIndex
    )
    {
        QueueGroupVoice(side, GetTheme(side).Voice?.OrdersRequested, kind, groupIndex);
    }

    /// <summary>
    /// Queues the numbered command group's maneuver acknowledgement.
    /// </summary>
    /// <param name="side">The side receiving the order.</param>
    /// <param name="kind">The kind of command group receiving the order.</param>
    /// <param name="groupIndex">The zero-based command-group number.</param>
    internal void QueueManeuverAcknowledged(
        TacticalBattleSide side,
        TacticalUnitKind kind,
        int groupIndex
    )
    {
        QueueGroupVoice(side, GetTheme(side).Voice?.ManeuverAcknowledged, kind, groupIndex);
    }

    /// <summary>
    /// Queues the numbered command group's target-engagement acknowledgement.
    /// </summary>
    /// <param name="side">The side receiving the order.</param>
    /// <param name="kind">The kind of command group receiving the order.</param>
    /// <param name="groupIndex">The zero-based command-group number.</param>
    internal void QueueAttackAcknowledged(
        TacticalBattleSide side,
        TacticalUnitKind kind,
        int groupIndex
    )
    {
        QueueGroupVoice(side, GetTheme(side).Voice?.AttackAcknowledged, kind, groupIndex);
    }

    /// <summary>
    /// Queues the numbered task force's formation acknowledgement.
    /// </summary>
    /// <param name="side">The side receiving the order.</param>
    /// <param name="groupIndex">The zero-based task-force number.</param>
    internal void QueueFormationAcknowledged(TacticalBattleSide side, int groupIndex)
    {
        QueueGroupVoice(
            side,
            GetTheme(side).Voice?.FormationAcknowledged,
            TacticalUnitKind.CapitalShip,
            groupIndex
        );
    }

    /// <summary>
    /// Queues the numbered command group's mission acknowledgement.
    /// </summary>
    /// <param name="side">The side receiving the order.</param>
    /// <param name="kind">The kind of command group receiving the order.</param>
    /// <param name="groupIndex">The zero-based command-group number.</param>
    internal void QueueMissionAcknowledged(
        TacticalBattleSide side,
        TacticalUnitKind kind,
        int groupIndex
    )
    {
        QueueGroupVoice(side, GetTheme(side).Voice?.MissionAcknowledged, kind, groupIndex);
    }

    /// <summary>
    /// Queues the report that one numbered fighter group has launched.
    /// </summary>
    /// <param name="side">The side whose fighters launched.</param>
    /// <param name="groupIndex">The zero-based fighter-group number.</param>
    internal void QueueFightersLaunched(TacticalBattleSide side, int groupIndex)
    {
        QueueGroupVoice(
            side,
            GetTheme(side).Voice?.FightersLaunched,
            TacticalUnitKind.Fighters,
            groupIndex
        );
    }

    /// <summary>
    /// Queues the report that one numbered fighter group has recovered aboard its carrier.
    /// </summary>
    /// <param name="side">The side whose fighters recovered.</param>
    /// <param name="groupIndex">The zero-based fighter-group number.</param>
    internal void QueueFightersRecovered(TacticalBattleSide side, int groupIndex)
    {
        QueueGroupVoice(
            side,
            GetTheme(side).Voice?.FightersRecovered,
            TacticalUnitKind.Fighters,
            groupIndex
        );
    }

    /// <summary>
    /// Queues the played faction's loss report for one numbered command group.
    /// </summary>
    /// <param name="side">The side that lost the unit.</param>
    /// <param name="kind">The kind of command group that lost the unit.</param>
    /// <param name="groupIndex">The zero-based command-group number.</param>
    internal void QueueUnitLost(TacticalBattleSide side, TacticalUnitKind kind, int groupIndex)
    {
        QueueGroupVoice(side, GetTheme(side).Voice?.UnitLost, kind, groupIndex);
    }

    /// <summary>
    /// Queues the played faction's target-destroyed report for one numbered command group.
    /// </summary>
    /// <param name="side">The side that destroyed the target.</param>
    /// <param name="kind">The kind of command group that destroyed the target.</param>
    /// <param name="groupIndex">The zero-based command-group number.</param>
    internal void QueueTargetDestroyed(
        TacticalBattleSide side,
        TacticalUnitKind kind,
        int groupIndex
    )
    {
        QueueGroupVoice(side, GetTheme(side).Voice?.TargetDestroyed, kind, groupIndex);
    }

    /// <summary>
    /// Queues the report that the played fleet is preparing to withdraw.
    /// </summary>
    /// <param name="side">The side preparing to withdraw.</param>
    internal void QueueWithdrawalPreparing(TacticalBattleSide side)
    {
        TacticalVoiceTheme voice = GetTheme(side).Voice;
        Enqueue(voice?.GetAudioPath(voice.WithdrawalPreparing), TacticalAudioChannel.Voice);
    }

    /// <summary>
    /// Queues the report that opposing gravity wells prevent withdrawal.
    /// </summary>
    /// <param name="side">The side whose withdrawal is blocked.</param>
    internal void QueueWithdrawalBlocked(TacticalBattleSide side)
    {
        TacticalVoiceTheme voice = GetTheme(side).Voice;
        Enqueue(voice?.GetAudioPath(voice.WithdrawalBlocked), TacticalAudioChannel.Voice);
    }

    /// <summary>
    /// Queues the final report heard by the played side when combat ends.
    /// </summary>
    /// <param name="side">The side receiving the report.</param>
    /// <param name="playedOutcome">The played side's final outcome.</param>
    /// <param name="opposingOutcome">The opposing side's final outcome.</param>
    internal void QueueOutcome(
        TacticalBattleSide side,
        SpaceCombatSideOutcome playedOutcome,
        SpaceCombatSideOutcome opposingOutcome
    )
    {
        TacticalVoiceTheme voice = GetTheme(side).Voice;
        string audio = voice?.Outcome?.GetAudio(playedOutcome, opposingOutcome);
        Enqueue(voice?.GetAudioPath(audio), TacticalAudioChannel.Voice);
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
            if (combatEvent.Kind == TacticalCombatEventKind.WeaponImpact)
                Enqueue(GetWeaponFirePath(combatEvent), TacticalAudioChannel.Combat);

            TacticalAudioChannel channel = combatEvent.Kind switch
            {
                TacticalCombatEventKind.WeaponImpact => TacticalAudioChannel.Combat,
                TacticalCombatEventKind.UnitDestroyed => TacticalAudioChannel.Combat,
                TacticalCombatEventKind.TractorLock or TacticalCombatEventKind.TractorRelease =>
                    TacticalAudioChannel.Combat,
                TacticalCombatEventKind.UnitWithdrawn or TacticalCombatEventKind.SuperlaserFired =>
                    TacticalAudioChannel.Unit,
                _ => TacticalAudioChannel.None,
            };
            string path = combatEvent.Kind switch
            {
                TacticalCombatEventKind.WeaponImpact => GetImpactPath(combatEvent),
                TacticalCombatEventKind.UnitDestroyed => GetDestructionPath(
                    combatEvent.DestroyedUnit
                ),
                TacticalCombatEventKind.TractorLock => GetTheme(
                    combatEvent.Target.Side
                ).TractorLockAudioPath,
                TacticalCombatEventKind.TractorRelease => GetTheme(
                    combatEvent.Target.Side
                ).TractorReleaseAudioPath,
                TacticalCombatEventKind.UnitWithdrawn => GetWithdrawalPath(combatEvent.Source),
                TacticalCombatEventKind.SuperlaserFired => GetTheme(
                    combatEvent.Source.Side
                ).SuperlaserAudioPath,
                _ => null,
            };
            Enqueue(path, channel);
        }
    }

    /// <summary>
    /// Resolves the firing cue for the attacking unit and weapon family.
    /// </summary>
    /// <param name="combatEvent">The completed weapon attack to resolve.</param>
    /// <returns>The configured firing cue.</returns>
    private string GetWeaponFirePath(TacticalCombatEvent combatEvent)
    {
        TacticalBattleTheme theme = GetTheme(combatEvent.Source.Side);
        bool fighters = combatEvent.Source.Kind == TacticalUnitKind.Fighters;
        return combatEvent.WeaponType switch
        {
            TacticalWeaponType.LaserCannon => fighters
                ? theme.FighterLaserCannonFireAudioPath
                : theme.LaserCannonFireAudioPath,
            TacticalWeaponType.Turbolaser => theme.TurbolaserFireAudioPath,
            TacticalWeaponType.IonCannon => fighters
                ? theme.FighterIonCannonFireAudioPath
                : theme.IonCannonFireAudioPath,
            TacticalWeaponType.Torpedo => theme.TorpedoFireAudioPath,
            _ => throw new ArgumentOutOfRangeException(nameof(combatEvent)),
        };
    }

    /// <summary>
    /// Resolves the arrival cue for the arriving unit class.
    /// </summary>
    /// <param name="unit">The arriving tactical unit.</param>
    /// <returns>The configured arrival cue.</returns>
    private string GetArrivalPath(TacticalUnitState unit)
    {
        TacticalBattleTheme theme = GetTheme(unit.Side);
        return unit.Kind switch
        {
            TacticalUnitKind.CapitalShip => theme.CapitalShipArrivalAudioPath,
            TacticalUnitKind.Fighters => theme.FighterArrivalAudioPath,
            _ => throw new ArgumentOutOfRangeException(nameof(unit)),
        };
    }

    /// <summary>
    /// Resolves the withdrawal cue for the departing unit class.
    /// </summary>
    /// <param name="unit">The departing tactical unit.</param>
    /// <returns>The configured withdrawal cue.</returns>
    private string GetWithdrawalPath(TacticalUnitState unit)
    {
        TacticalBattleTheme theme = GetTheme(unit.Side);
        return unit.Kind switch
        {
            TacticalUnitKind.CapitalShip => theme.CapitalShipWithdrawalAudioPath,
            TacticalUnitKind.Fighters => theme.FighterWithdrawalAudioPath,
            _ => throw new ArgumentOutOfRangeException(nameof(unit)),
        };
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
            TacticalWeaponType.LaserCannon => combatEvent.PenetratedShields
                ? theme.ProjectileShieldPenetrationAudioPath
                : theme.ProjectileShieldHitAudioPath,
            TacticalWeaponType.Turbolaser or TacticalWeaponType.Torpedo =>
                combatEvent.PenetratedShields
                    ? theme.EnergyShieldPenetrationAudioPath
                    : theme.EnergyShieldHitAudioPath,
            _ => throw new ArgumentOutOfRangeException(nameof(combatEvent)),
        };
    }

    /// <summary>
    /// Resolves the destruction cue from the destroyed unit's original hull class.
    /// </summary>
    /// <param name="unit">The destroyed tactical unit.</param>
    /// <returns>The configured destruction cue.</returns>
    private string GetDestructionPath(TacticalUnitState unit)
    {
        TacticalBattleTheme theme = GetTheme(unit.Side);
        if (unit.Kind == TacticalUnitKind.Fighters)
            return theme.SmallShipDestructionAudioPath;

        int maximumHull = ((CapitalShip)unit.Unit).MaxHullStrength;
        if (maximumHull < _mediumDestructionHullThreshold)
            return theme.SmallShipDestructionAudioPath;
        if (maximumHull <= _largeDestructionHullThreshold)
            return theme.MediumShipDestructionAudioPath;

        return theme.LargeShipDestructionAudioPath;
    }

    /// <summary>
    /// Advances the tactical cue queue using real elapsed time, including while combat is paused.
    /// </summary>
    /// <param name="elapsedTime">The unscaled elapsed time in seconds.</param>
    internal void Advance(float elapsedTime)
    {
        if (elapsedTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedTime));

        AdvanceChannel(
            unitCues,
            _unitCueLifetime,
            elapsedTime,
            this.elapsedTime,
            ref unitCueRemainingDuration
        );
        AdvanceChannel(
            combatCues,
            _combatCueLifetime,
            elapsedTime,
            this.elapsedTime,
            ref combatCueRemainingDuration
        );
        AdvanceChannel(
            voiceCues,
            _voiceCueLifetime,
            elapsedTime,
            this.elapsedTime,
            ref voiceCueRemainingDuration
        );
        this.elapsedTime += elapsedTime;
    }

    /// <summary>
    /// Resolves and queues one numbered command-group response.
    /// </summary>
    /// <param name="side">The side receiving the order.</param>
    /// <param name="groupVoice">The configured response category.</param>
    /// <param name="kind">The kind of command group receiving the order.</param>
    /// <param name="groupIndex">The zero-based command-group number.</param>
    private void QueueGroupVoice(
        TacticalBattleSide side,
        TacticalGroupVoiceTheme groupVoice,
        TacticalUnitKind kind,
        int groupIndex
    )
    {
        TacticalVoiceTheme voice = GetTheme(side).Voice;
        string audio = groupVoice?.GetAudio(kind, groupIndex);
        Enqueue(voice?.GetAudioPath(audio), TacticalAudioChannel.Voice);
    }

    /// <summary>
    /// Advances one independently bounded tactical audio channel.
    /// </summary>
    /// <param name="pending">The channel's pending cues.</param>
    /// <param name="lifetime">The maximum time a pending cue remains relevant.</param>
    /// <param name="availableTime">The elapsed real time available to the channel.</param>
    /// <param name="currentTime">The real time at the beginning of the update.</param>
    /// <param name="remainingDuration">The duration remaining on the active cue.</param>
    private void AdvanceChannel(
        Queue<PendingCue> pending,
        float lifetime,
        float availableTime,
        float currentTime,
        ref float remainingDuration
    )
    {
        while (remainingDuration <= availableTime && pending.Count > 0)
        {
            availableTime -= remainingDuration;
            currentTime += remainingDuration;
            remainingDuration = 0f;

            PendingCue cue = pending.Dequeue();
            if (currentTime - cue.QueuedAt > lifetime)
                continue;

            play(cue.Path);
            remainingDuration = Math.Max(0f, getDuration(cue.Path));
        }

        remainingDuration = Math.Max(0f, remainingDuration - availableTime);
    }

    /// <summary>
    /// Adds a configured cue to its tactical audio channel.
    /// </summary>
    /// <param name="path">The optional addressed audio path.</param>
    /// <param name="channel">The independently bounded channel that presents the cue.</param>
    private void Enqueue(string path, TacticalAudioChannel channel)
    {
        if (string.IsNullOrWhiteSpace(path) || channel == TacticalAudioChannel.None)
            return;

        Queue<PendingCue> pending = channel switch
        {
            TacticalAudioChannel.Combat => combatCues,
            TacticalAudioChannel.Unit => unitCues,
            TacticalAudioChannel.Voice => voiceCues,
            _ => throw new ArgumentOutOfRangeException(nameof(channel)),
        };
        pending.Enqueue(new PendingCue(path.Trim(), elapsedTime));
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

    /// <summary>
    /// Identifies independently bounded tactical cue families.
    /// </summary>
    private enum TacticalAudioChannel
    {
        None,
        Combat,
        Unit,
        Voice,
    }

    /// <summary>
    /// Holds one addressed cue until its channel can present it.
    /// </summary>
    private readonly struct PendingCue
    {
        /// <summary>
        /// Creates a pending cue at the current tactical audio time.
        /// </summary>
        /// <param name="path">The addressed audio path.</param>
        /// <param name="queuedAt">The real time at which the cue was queued.</param>
        internal PendingCue(string path, float queuedAt)
        {
            Path = path;
            QueuedAt = queuedAt;
        }

        /// <summary>
        /// Gets the addressed audio path.
        /// </summary>
        internal string Path { get; }

        /// <summary>
        /// Gets the real time at which the cue was queued.
        /// </summary>
        internal float QueuedAt { get; }
    }
}
