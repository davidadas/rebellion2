using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Reads and changes whether strategy entities appear in the idle bar.
/// </summary>
public interface IIdleBarTrackingActions
{
    /// <summary>Reports whether an entity appears in the idle bar.</summary>
    /// <param name="entity">The entity whose tracking state is requested.</param>
    /// <returns><see langword="true"/> when the entity is tracked.</returns>
    bool IsIdleBarTracked(ISceneNode entity);

    /// <summary>Changes whether an entity appears in the idle bar.</summary>
    /// <param name="entity">The entity whose tracking state should change.</param>
    void ToggleIdleBarTracking(ISceneNode entity);
}

/// <summary>
/// Describes one available strategy entity shown in the idle bar.
/// </summary>
internal sealed class IdleBarEntry
{
    internal string Name { get; }

    internal Texture2D Texture { get; }

    internal ISceneNode Entity { get; }

    internal IdleBarEntry(ISceneNode entity, Texture2D texture)
    {
        Entity = entity;
        Name = entity?.GetDisplayName() ?? string.Empty;
        Texture = texture;
    }
}

/// <summary>
/// Contains the ordered idle bar rendered on the strategy desktop.
/// </summary>
internal sealed class IdleBarRenderData
{
    internal IReadOnlyList<IdleBarEntry> Entries { get; }

    internal RectInt DesktopBounds { get; }

    internal IdleBarRenderData(IReadOnlyList<IdleBarEntry> entries, RectInt desktopBounds)
    {
        Entries = entries ?? Array.Empty<IdleBarEntry>();
        DesktopBounds = desktopBounds;
    }
}

/// <summary>
/// Selects idle personnel and manufacturing capacity for the strategy desktop.
/// </summary>
internal static class IdleBarProjector
{
    private static readonly ManufacturingType[] _manufacturingTypes =
    {
        ManufacturingType.Ship,
        ManufacturingType.Troop,
        ManufacturingType.Building,
    };

    /// <summary>
    /// Projects available officers, special-forces units, and manufacturing planets in order.
    /// </summary>
    internal static IdleBarRenderData Project(
        Faction playerFaction,
        UIContext uiContext,
        RectInt desktopBounds
    )
    {
        List<IdleBarEntry> entries = FindAvailableParticipants<Officer>(playerFaction)
            .Select(officer => CreateEntry(officer, uiContext))
            .Concat(
                FindAvailableParticipants<SpecialForces>(playerFaction)
                    .Select(specialForces => CreateEntry(specialForces, uiContext))
            )
            .Concat(
                playerFaction
                    ?.GetOwnedUnitsByType<Planet>()
                    .Where(HasIdleManufacturing)
                    .OrderBy(
                        candidate => candidate.GetDisplayName(),
                        StringComparer.OrdinalIgnoreCase
                    )
                    .ThenBy(candidate => candidate.InstanceID, StringComparer.Ordinal)
                    .Select(planet => CreateEntry(planet, uiContext))
                    ?? Enumerable.Empty<IdleBarEntry>()
            )
            .ToList();

        return new IdleBarRenderData(entries, desktopBounds);
    }

    /// <summary>
    /// Selects all available mission participants of one type by display name.
    /// </summary>
    private static IEnumerable<T> FindAvailableParticipants<T>(Faction playerFaction)
        where T : class, IMissionParticipant
    {
        return playerFaction
                ?.GetAvailableMissionParticipants()
                .OfType<T>()
                .Where(IsHealthyParticipant)
                .OrderBy(candidate => candidate.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.InstanceID, StringComparer.Ordinal)
            ?? Enumerable.Empty<T>();
    }

    /// <summary>
    /// Excludes personnel that cannot presently accept useful orders.
    /// </summary>
    private static bool IsHealthyParticipant(IMissionParticipant participant)
    {
        if (participant is Officer officer)
            return officer.InjuryPoints <= 0 && !officer.IsRetired;

        return participant is not SpecialForces specialForces || !specialForces.IsRetired;
    }

    /// <summary>
    /// Reports whether an owned planet has at least one empty manufacturing lane.
    /// </summary>
    private static bool HasIdleManufacturing(Planet planet)
    {
        return planet is { IsDestroyed: false }
            && _manufacturingTypes.Any(type => planet.GetIdleManufacturingFacilities(type) > 0);
    }

    /// <summary>
    /// Builds one optional display entry and resolves its compact artwork.
    /// </summary>
    private static IdleBarEntry CreateEntry(ISceneNode entity, UIContext uiContext)
    {
        if (entity == null)
            return null;

        Texture2D texture = entity is Planet planet
            ? uiContext?.GetPlanetTexture(planet)
            : uiContext?.GetEntityTexture(entity, true);
        return new IdleBarEntry(entity, texture);
    }
}
