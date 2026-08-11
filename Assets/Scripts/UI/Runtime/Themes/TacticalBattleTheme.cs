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
/// Defines faction-specific spoken responses for tactical command and battle reports.
/// </summary>
[PersistableObject]
public sealed class TacticalVoiceTheme
{
    public string AudioRoot { get; set; }

    public string FleetReady { get; set; }

    public string WithdrawalPreparing { get; set; }

    public string WithdrawalBlocked { get; set; }

    public TacticalGroupVoiceTheme ManeuverAcknowledged { get; set; }

    public TacticalGroupVoiceTheme AttackAcknowledged { get; set; }

    public TacticalGroupVoiceTheme FormationAcknowledged { get; set; }

    public TacticalGroupVoiceTheme MissionAcknowledged { get; set; }

    public TacticalGroupVoiceTheme UnitLost { get; set; }

    public TacticalGroupVoiceTheme TargetDestroyed { get; set; }

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
            ManeuverAcknowledged,
            AttackAcknowledged,
            FormationAcknowledged,
            MissionAcknowledged,
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

    public string CapitalShipArrivalAudioPath { get; set; }

    public string CapitalShipWithdrawalAudioPath { get; set; }

    public string FighterArrivalAudioPath { get; set; }

    public string FighterWithdrawalAudioPath { get; set; }

    public string LaserCannonFireAudioPath { get; set; }

    public string FighterLaserCannonFireAudioPath { get; set; }

    public string TurbolaserFireAudioPath { get; set; }

    public string IonCannonFireAudioPath { get; set; }

    public string FighterIonCannonFireAudioPath { get; set; }

    public string TorpedoFireAudioPath { get; set; }

    public string TractorLockAudioPath { get; set; }

    public string TractorReleaseAudioPath { get; set; }

    public string SmallShipDestructionAudioPath { get; set; }

    public string MediumShipDestructionAudioPath { get; set; }

    public string LargeShipDestructionAudioPath { get; set; }

    public string SuperlaserAudioPath { get; set; }

    public string EnergyShieldHitAudioPath { get; set; }

    public string EnergyShieldPenetrationAudioPath { get; set; }

    public string ProjectileShieldHitAudioPath { get; set; }

    public string ProjectileShieldPenetrationAudioPath { get; set; }

    public string IonShieldHitAudioPath { get; set; }

    public string IonShieldPenetrationAudioPath { get; set; }

    public string LeftShipHighlightFactionInstanceID { get; set; }

    public string LeftShipHighlightColorHex { get; set; }

    public string RightShipHighlightFactionInstanceID { get; set; }

    public string RightShipHighlightColorHex { get; set; }

    public float InitialCameraYaw { get; set; }
}
