using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Game.Tactical;
using Rebellion.Util.Serialization;

/// <summary>
/// Defines the spoken responses available to one kind of tactical command group.
/// </summary>
[PersistableObject]
public sealed class TacticalGroupVoiceTheme
{
    public string Ship { get; set; }

    public List<string> TaskForces { get; set; } = new List<string>();

    public List<string> FighterGroups { get; set; } = new List<string>();

    /// <summary>
    /// Resolves the configured response for one numbered tactical command group.
    /// </summary>
    /// <param name="kind">The kind of units assigned to the group.</param>
    /// <param name="groupIndex">The zero-based task-force or fighter-group number.</param>
    /// <returns>The configured audio name, or the generic ship response when available.</returns>
    public string GetAudio(TacticalUnitKind kind, int groupIndex)
    {
        if (groupIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(groupIndex));

        IReadOnlyList<string> groupAudio = kind switch
        {
            TacticalUnitKind.CapitalShip => TaskForces,
            TacticalUnitKind.Fighters => FighterGroups,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return groupIndex < groupAudio.Count ? groupAudio[groupIndex] : Ship;
    }

    /// <summary>
    /// Enumerates every non-empty response configured for preloading.
    /// </summary>
    /// <returns>The configured audio names.</returns>
    public IEnumerable<string> GetAudioNames()
    {
        if (!string.IsNullOrWhiteSpace(Ship))
            yield return Ship;
        foreach (string audio in TaskForces)
        {
            if (!string.IsNullOrWhiteSpace(audio))
                yield return audio;
        }
        foreach (string audio in FighterGroups)
        {
            if (!string.IsNullOrWhiteSpace(audio))
                yield return audio;
        }
    }
}

/// <summary>
/// Defines the four reports associated with one named fighter group's Death Star attack.
/// </summary>
[PersistableObject]
public sealed class TacticalDeathStarAttackGroupVoiceTheme
{
    public string Begin { get; set; }

    public string Running { get; set; }

    public string Failed { get; set; }

    public string Succeeded { get; set; }

    /// <summary>
    /// Resolves the report for one attack-run phase.
    /// </summary>
    /// <param name="kind">The attack-run phase.</param>
    /// <returns>The configured report, or null for a timed chatter checkpoint.</returns>
    public string GetAudio(TacticalCombatEventKind kind)
    {
        return kind switch
        {
            TacticalCombatEventKind.DeathStarAttackStarted => Running,
            TacticalCombatEventKind.DeathStarAttackFailed => Failed,
            TacticalCombatEventKind.DeathStarAttackSucceeded => Succeeded,
            _ => null,
        };
    }

    /// <summary>
    /// Enumerates every non-empty group report configured for preloading.
    /// </summary>
    /// <returns>The configured audio names.</returns>
    public IEnumerable<string> GetAudioNames()
    {
        if (!string.IsNullOrWhiteSpace(Begin))
            yield return Begin;
        if (!string.IsNullOrWhiteSpace(Running))
            yield return Running;
        if (!string.IsNullOrWhiteSpace(Failed))
            yield return Failed;
        if (!string.IsNullOrWhiteSpace(Succeeded))
            yield return Succeeded;
    }
}

/// <summary>
/// Defines faction-specific Death Star tactical reports.
/// </summary>
[PersistableObject]
public sealed class TacticalDeathStarVoiceTheme
{
    public string Approaching { get; set; }

    public string AttackWindowOpen { get; set; }

    public string FighterScreen { get; set; }

    public string Shielded { get; set; }

    public string InsufficientFighterScreen { get; set; }

    public string AttackContinuing { get; set; }

    public string SuperlaserFiring { get; set; }

    public string SuperlaserReady { get; set; }

    public string SuperlaserWarning { get; set; }

    public string UnderAttack { get; set; }

    public string AttackBrokenOff { get; set; }

    public string Destroyed { get; set; }

    public List<string> AttackReports { get; set; } = new List<string>();

    public List<TacticalDeathStarAttackGroupVoiceTheme> AttackGroups { get; set; } =
        new List<TacticalDeathStarAttackGroupVoiceTheme>();

    /// <summary>
    /// Resolves one numbered fighter group's opening attack-run report.
    /// </summary>
    /// <param name="groupIndex">The zero-based fighter-group number.</param>
    /// <returns>The configured report, or null when the group is not configured.</returns>
    public string GetAttackGroupBegin(int groupIndex)
    {
        if (groupIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(groupIndex));

        return groupIndex < AttackGroups.Count ? AttackGroups[groupIndex]?.Begin : null;
    }

    /// <summary>
    /// Resolves one numbered fighter group's attack-run report.
    /// </summary>
    /// <param name="groupIndex">The zero-based fighter-group number.</param>
    /// <param name="kind">The attack-run phase.</param>
    /// <returns>The configured report, or null when the group or phase is not configured.</returns>
    public string GetAttackGroupAudio(int groupIndex, TacticalCombatEventKind kind)
    {
        if (groupIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(groupIndex));

        return groupIndex < AttackGroups.Count ? AttackGroups[groupIndex]?.GetAudio(kind) : null;
    }

    /// <summary>
    /// Resolves one timed attack-run chatter report.
    /// </summary>
    /// <param name="reportIndex">The zero-based chatter report number.</param>
    /// <returns>The configured report, or null when the report is not configured.</returns>
    public string GetAttackReport(int reportIndex)
    {
        if (reportIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(reportIndex));

        return reportIndex < AttackReports.Count ? AttackReports[reportIndex] : null;
    }

    /// <summary>
    /// Enumerates every non-empty Death Star response configured for preloading.
    /// </summary>
    /// <returns>The configured audio names.</returns>
    public IEnumerable<string> GetAudioNames()
    {
        if (!string.IsNullOrWhiteSpace(Approaching))
            yield return Approaching;
        if (!string.IsNullOrWhiteSpace(AttackWindowOpen))
            yield return AttackWindowOpen;
        if (!string.IsNullOrWhiteSpace(FighterScreen))
            yield return FighterScreen;
        if (!string.IsNullOrWhiteSpace(Shielded))
            yield return Shielded;
        if (!string.IsNullOrWhiteSpace(InsufficientFighterScreen))
            yield return InsufficientFighterScreen;
        if (!string.IsNullOrWhiteSpace(AttackContinuing))
            yield return AttackContinuing;
        if (!string.IsNullOrWhiteSpace(SuperlaserFiring))
            yield return SuperlaserFiring;
        if (!string.IsNullOrWhiteSpace(SuperlaserReady))
            yield return SuperlaserReady;
        if (!string.IsNullOrWhiteSpace(SuperlaserWarning))
            yield return SuperlaserWarning;
        if (!string.IsNullOrWhiteSpace(UnderAttack))
            yield return UnderAttack;
        if (!string.IsNullOrWhiteSpace(AttackBrokenOff))
            yield return AttackBrokenOff;
        if (!string.IsNullOrWhiteSpace(Destroyed))
            yield return Destroyed;
        foreach (string report in AttackReports)
        {
            if (!string.IsNullOrWhiteSpace(report))
                yield return report;
        }
        foreach (TacticalDeathStarAttackGroupVoiceTheme group in AttackGroups)
        {
            if (group == null)
                continue;
            foreach (string audio in group.GetAudioNames())
                yield return audio;
        }
    }
}

/// <summary>
/// Defines faction-specific spoken responses for tactical command and battle reports.
/// </summary>
[PersistableObject]
public sealed class TacticalVoiceTheme
{
    public string AudioRoot { get; set; }

    public string FleetReady { get; set; }

    public string WithdrawalPreparing { get; set; }

    public string WithdrawalBlocked { get; set; }

    public TacticalGroupVoiceTheme OrdersRequested { get; set; }

    public TacticalGroupVoiceTheme ManeuverAcknowledged { get; set; }

    public TacticalGroupVoiceTheme AttackAcknowledged { get; set; }

    public TacticalGroupVoiceTheme FormationAcknowledged { get; set; }

    public TacticalGroupVoiceTheme MissionAcknowledged { get; set; }

    public TacticalGroupVoiceTheme FightersLaunched { get; set; }

    public TacticalGroupVoiceTheme FightersRecovered { get; set; }

    public TacticalGroupVoiceTheme UnitLost { get; set; }

    public TacticalGroupVoiceTheme TargetDestroyed { get; set; }

    public TacticalDeathStarVoiceTheme DeathStar { get; set; }

    public TacticalOutcomeVoiceTheme Outcome { get; set; }

    /// <summary>
    /// Builds the content address for one configured tactical voice clip.
    /// </summary>
    /// <param name="audio">The configured audio name.</param>
    /// <returns>The external content address, or null when no audio is configured.</returns>
    public string GetAudioPath(string audio)
    {
        if (string.IsNullOrWhiteSpace(audio))
            return null;
        if (string.IsNullOrWhiteSpace(AudioRoot))
            throw new InvalidOperationException("Tactical voice audio requires an audio root.");

        return $"{AudioRoot.TrimEnd('/')}/{audio.Trim()}";
    }

    /// <summary>
    /// Enumerates every distinct tactical voice address required by this theme.
    /// </summary>
    /// <returns>The configured voice content addresses.</returns>
    public IEnumerable<string> GetAudioPaths()
    {
        HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (string audio in GetAudioNames())
        {
            string path = GetAudioPath(audio);
            if (path != null && paths.Add(path))
                yield return path;
        }
    }

    /// <summary>
    /// Enumerates configured audio names before applying the faction audio root.
    /// </summary>
    /// <returns>The configured audio names.</returns>
    private IEnumerable<string> GetAudioNames()
    {
        if (!string.IsNullOrWhiteSpace(FleetReady))
            yield return FleetReady;
        if (!string.IsNullOrWhiteSpace(WithdrawalPreparing))
            yield return WithdrawalPreparing;
        if (!string.IsNullOrWhiteSpace(WithdrawalBlocked))
            yield return WithdrawalBlocked;

        TacticalGroupVoiceTheme[] groups =
        {
            OrdersRequested,
            ManeuverAcknowledged,
            AttackAcknowledged,
            FormationAcknowledged,
            MissionAcknowledged,
            FightersLaunched,
            FightersRecovered,
            UnitLost,
            TargetDestroyed,
        };
        foreach (TacticalGroupVoiceTheme group in groups)
        {
            if (group == null)
                continue;
            foreach (string audio in group.GetAudioNames())
                yield return audio;
        }

        if (DeathStar != null)
        {
            foreach (string audio in DeathStar.GetAudioNames())
                yield return audio;
        }

        if (Outcome == null)
            yield break;
        foreach (string audio in Outcome.GetAudioNames())
            yield return audio;
    }
}

/// <summary>
/// Defines the final spoken report for each tactical battle outcome.
/// </summary>
[PersistableObject]
public sealed class TacticalOutcomeVoiceTheme
{
    public string WithdrawalComplete { get; set; }

    public string EnemyWithdrew { get; set; }

    public string EnemyDestroyed { get; set; }

    public string FleetDestroyed { get; set; }

    /// <summary>
    /// Resolves the report heard by the played side when tactical combat ends.
    /// </summary>
    /// <param name="playedOutcome">The played side's final outcome.</param>
    /// <param name="opposingOutcome">The opposing side's final outcome.</param>
    /// <returns>The configured outcome report.</returns>
    public string GetAudio(
        SpaceCombatSideOutcome playedOutcome,
        SpaceCombatSideOutcome opposingOutcome
    )
    {
        if (playedOutcome == SpaceCombatSideOutcome.Withdrawn)
            return WithdrawalComplete;
        if (playedOutcome != SpaceCombatSideOutcome.Active)
            return FleetDestroyed;

        return opposingOutcome switch
        {
            SpaceCombatSideOutcome.Withdrawn => EnemyWithdrew,
            SpaceCombatSideOutcome.Destroyed => EnemyDestroyed,
            _ => null,
        };
    }

    /// <summary>
    /// Enumerates every configured outcome report for preloading.
    /// </summary>
    /// <returns>The configured audio names.</returns>
    public IEnumerable<string> GetAudioNames()
    {
        if (!string.IsNullOrWhiteSpace(WithdrawalComplete))
            yield return WithdrawalComplete;
        if (!string.IsNullOrWhiteSpace(EnemyWithdrew))
            yield return EnemyWithdrew;
        if (!string.IsNullOrWhiteSpace(EnemyDestroyed))
            yield return EnemyDestroyed;
        if (!string.IsNullOrWhiteSpace(FleetDestroyed))
            yield return FleetDestroyed;
    }
}

/// <summary>
/// Defines the interchangeable sound effects for one tactical event.
/// </summary>
[PersistableObject]
public sealed class TacticalAudioCueTheme
{
    public List<string> Paths { get; set; } = new List<string>();
}

/// <summary>
/// Defines the faction-specific artwork and shared asset root for tactical space battles.
/// </summary>
[PersistableObject]
public sealed class TacticalBattleTheme
{
    public string SharedUIRoot { get; set; }

    public string SharedEffectsRoot { get; set; }

    public string TaskForceHeaderImagePath { get; set; }

    public string FighterGroupHeaderImagePath { get; set; }

    public string FighterOrderVariant { get; set; }

    public TacticalVoiceTheme Voice { get; set; }

    public TacticalAudioCueTheme CapitalShipArrivalAudio { get; set; }

    public TacticalAudioCueTheme CapitalShipWithdrawalAudio { get; set; }

    public TacticalAudioCueTheme FighterArrivalAudio { get; set; }

    public TacticalAudioCueTheme FighterWithdrawalAudio { get; set; }

    public TacticalAudioCueTheme FighterLaunchAudio { get; set; }

    public TacticalAudioCueTheme LaserCannonFireAudio { get; set; }

    public TacticalAudioCueTheme FighterLaserCannonFireAudio { get; set; }

    public TacticalAudioCueTheme TurbolaserFireAudio { get; set; }

    public TacticalAudioCueTheme IonCannonFireAudio { get; set; }

    public TacticalAudioCueTheme FighterIonCannonFireAudio { get; set; }

    public TacticalAudioCueTheme TorpedoFireAudio { get; set; }

    public TacticalAudioCueTheme TractorLockAudio { get; set; }

    public TacticalAudioCueTheme TractorReleaseAudio { get; set; }

    public TacticalAudioCueTheme SmallShipDestructionAudio { get; set; }

    public TacticalAudioCueTheme MediumShipDestructionAudio { get; set; }

    public TacticalAudioCueTheme LargeShipDestructionAudio { get; set; }

    public TacticalAudioCueTheme SuperlaserAudio { get; set; }

    public TacticalAudioCueTheme EnergyShieldHitAudio { get; set; }

    public TacticalAudioCueTheme EnergyShieldPenetrationAudio { get; set; }

    public TacticalAudioCueTheme ProjectileShieldHitAudio { get; set; }

    public TacticalAudioCueTheme ProjectileShieldPenetrationAudio { get; set; }

    public TacticalAudioCueTheme IonShieldHitAudio { get; set; }

    public TacticalAudioCueTheme IonShieldPenetrationAudio { get; set; }

    public string LeftShipHighlightFactionInstanceID { get; set; }

    public string LeftShipHighlightColorHex { get; set; }

    public string RightShipHighlightFactionInstanceID { get; set; }

    public string RightShipHighlightColorHex { get; set; }

    public float InitialCameraYaw { get; set; }
}
