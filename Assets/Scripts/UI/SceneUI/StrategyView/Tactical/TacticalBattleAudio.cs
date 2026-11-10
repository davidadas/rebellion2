using System;
using System.Collections.Generic;
using Rebellion.Gameplay;

/// <summary>
/// Turns tactical battle events into throttled sound-effect playback.
/// </summary>
internal sealed class TacticalBattleAudio
{
    internal const string FighterLaunchPath = "Pack/Shared/Tactical/Audio/fighter-launch";
    internal const string HyperspacePath = "Pack/Shared/Tactical/Audio/hyperspace-arrival";
    internal const string ExplosionPath =
        "Application/Strategy/Messages/Audio/message-sabotage-maintenance-explosion";
    internal static readonly IReadOnlyList<string> WeaponFirePaths = new[]
    {
        "Pack/Shared/Tactical/Audio/weapon-fire-1",
        "Pack/Shared/Tactical/Audio/weapon-fire-2",
        "Pack/Shared/Tactical/Audio/weapon-fire-3",
        "Pack/Shared/Tactical/Audio/weapon-fire-4",
    };
    internal static readonly IReadOnlyList<string> WeaponImpactPaths = new[]
    {
        "Pack/Shared/Tactical/Audio/weapon-impact-1",
        "Pack/Shared/Tactical/Audio/weapon-impact-2",
        "Pack/Shared/Tactical/Audio/weapon-impact-3",
    };

    private const int _maximumWeaponFiresPerUpdate = 2;
    private const int _maximumExplosionsPerUpdate = 3;
    private const float _weaponFireInterval = 0.08f;
    private const float _impactInterval = 0.25f;
    private const float _withdrawalInterval = 0.5f;

    private readonly Action<string> _playSfx;
    private readonly Func<string, bool> _isAvailable;
    private readonly Dictionary<string, bool> _availability = new Dictionary<string, bool>();
    private int _fireIndex;
    private int _impactIndex;
    private float _weaponFireCooldown;
    private float _impactCooldown;
    private float _withdrawalCooldown;

    /// <summary>
    /// Creates battle audio that plays through a sound-effect callback.
    /// </summary>
    /// <param name="playSfx">Plays a sound-effect content path.</param>
    /// <param name="isAvailable">
    /// Reports whether a sound-effect content path exists, or null to assume every path exists.
    /// </param>
    internal TacticalBattleAudio(Action<string> playSfx, Func<string, bool> isAvailable = null)
    {
        _playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        _isAvailable = isAvailable;
    }

    /// <summary>
    /// Resets throttling for a new battle and plays the opening launch cue.
    /// </summary>
    /// <param name="hasFighters">Whether either side brought fighter squadrons.</param>
    internal void Begin(bool hasFighters)
    {
        _fireIndex = 0;
        _impactIndex = 0;
        _weaponFireCooldown = 0f;
        _impactCooldown = 0f;
        _withdrawalCooldown = 0f;
        if (hasFighters)
            Play(FighterLaunchPath);
    }

    /// <summary>
    /// Advances the throttling timers.
    /// </summary>
    /// <param name="deltaTime">The elapsed real seconds.</param>
    internal void Advance(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        _weaponFireCooldown = Math.Max(_weaponFireCooldown - deltaTime, 0f);
        _impactCooldown = Math.Max(_impactCooldown - deltaTime, 0f);
        _withdrawalCooldown = Math.Max(_withdrawalCooldown - deltaTime, 0f);
    }

    /// <summary>
    /// Plays the sounds for one update's worth of battle events.
    /// </summary>
    /// <param name="events">The events raised since the previous update.</param>
    internal void Handle(IReadOnlyList<TacticalBattleEvent> events)
    {
        if (events == null || events.Count == 0)
            return;

        int explosions = 0;
        bool impactPlayed = false;
        foreach (TacticalBattleEvent battleEvent in events)
        {
            switch (battleEvent.Kind)
            {
                case TacticalBattleEventKind.UnitDestroyed:
                case TacticalBattleEventKind.StationDestroyed:
                    if (explosions < _maximumExplosionsPerUpdate && Play(ExplosionPath))
                        explosions++;
                    break;
                case TacticalBattleEventKind.SuperlaserFired:
                    // The content has no superlaser recording yet; a weapon cue marks the shot.
                    PlayNext(WeaponFirePaths, ref _fireIndex);
                    break;
                case TacticalBattleEventKind.UnitWithdrew:
                    if (_withdrawalCooldown <= 0f && Play(HyperspacePath))
                        _withdrawalCooldown = _withdrawalInterval;
                    break;
                case TacticalBattleEventKind.UnitHit:
                    if (
                        !impactPlayed
                        && _impactCooldown <= 0f
                        && PlayNext(WeaponImpactPaths, ref _impactIndex)
                    )
                    {
                        impactPlayed = true;
                        _impactCooldown = _impactInterval;
                    }
                    break;
            }
        }

        if (_weaponFireCooldown > 0f)
            return;

        int fires = PlayWeaponFire(events, starfighters: false, 0);
        fires = PlayWeaponFire(events, starfighters: true, fires);
        if (fires > 0)
            _weaponFireCooldown = _weaponFireInterval;
    }

    /// <summary>
    /// Plays weapon-fire cues for one class of shooter up to the per-update limit.
    /// </summary>
    /// <param name="events">The events raised since the previous update.</param>
    /// <param name="starfighters">Whether fighter squadrons or capital ships are played.</param>
    /// <param name="firesPlayed">The weapon-fire cues already played this update.</param>
    /// <returns>The updated count of weapon-fire cues played this update.</returns>
    private int PlayWeaponFire(
        IReadOnlyList<TacticalBattleEvent> events,
        bool starfighters,
        int firesPlayed
    )
    {
        foreach (TacticalBattleEvent battleEvent in events)
        {
            if (firesPlayed >= _maximumWeaponFiresPerUpdate)
                break;
            if (
                battleEvent.Kind != TacticalBattleEventKind.WeaponFired
                || battleEvent.Unit.IsStarfighter != starfighters
            )
                continue;

            if (PlayNext(WeaponFirePaths, ref _fireIndex))
                firesPlayed++;
        }

        return firesPlayed;
    }

    /// <summary>
    /// Plays the next variant in a rotating set of sounds.
    /// </summary>
    /// <param name="paths">The sound variants.</param>
    /// <param name="index">The rotation position, advanced after playback.</param>
    /// <returns>True when a sound was played.</returns>
    private bool PlayNext(IReadOnlyList<string> paths, ref int index)
    {
        for (int attempt = 0; attempt < paths.Count; attempt++)
        {
            string path = paths[index];
            index = (index + 1) % paths.Count;
            if (Play(path))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Plays one sound when its content exists.
    /// </summary>
    /// <param name="path">The sound-effect content path.</param>
    /// <returns>True when the sound was played.</returns>
    private bool Play(string path)
    {
        if (!IsAvailable(path))
            return false;

        _playSfx(path);
        return true;
    }

    /// <summary>
    /// Returns whether a sound exists, checking each path at most once.
    /// </summary>
    /// <param name="path">The sound-effect content path.</param>
    /// <returns>True when the sound can be played.</returns>
    private bool IsAvailable(string path)
    {
        if (_isAvailable == null)
            return true;

        if (!_availability.TryGetValue(path, out bool available))
        {
            available = _isAvailable(path);
            _availability[path] = available;
        }

        return available;
    }
}
