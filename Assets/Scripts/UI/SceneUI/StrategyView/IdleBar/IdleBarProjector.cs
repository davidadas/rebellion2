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
/// Selects idle personnel and manufacturing capacity for the strategy desktop.
/// </summary>
internal sealed class IdleBarProjector
{
    private static readonly ManufacturingType[] _manufacturingTypes =
    {
        ManufacturingType.Ship,
        ManufacturingType.Troop,
        ManufacturingType.Building,
    };

    private readonly Func<UIContext> getUIContext;

    /// <summary>
    /// Creates an idle-bar projector backed by the current strategy UI context.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    internal IdleBarProjector(Func<UIContext> getUIContext)
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
    }

    /// <summary>
    /// Projects available officers, special-forces units, and manufacturing planets in order,
    /// with main characters ahead of other officers.
    /// </summary>
    /// <param name="playerFaction">The faction whose available entities are projected.</param>
    /// <param name="desktopBounds">The strategy desktop bounds.</param>
    /// <returns>The ordered idle-bar presentation.</returns>
    internal IdleBarRenderData Project(Faction playerFaction, RectInt desktopBounds)
    {
        UIContext uiContext = getUIContext();
        List<IdleBarEntry> entries = FindAvailableParticipants<Officer>(playerFaction)
            .OrderByDescending(officer => officer.IsMain)
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

        return new IdleBarRenderData(true, entries, desktopBounds);
    }

    /// <summary>
    /// Selects all available mission participants of one type by display name.
    /// </summary>
    /// <typeparam name="T">The mission-participant type to select.</typeparam>
    /// <param name="playerFaction">The faction whose participants are selected.</param>
    /// <returns>The available participants in display order.</returns>
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
    /// <param name="participant">The participant to evaluate.</param>
    /// <returns><see langword="true"/> when the participant can receive orders.</returns>
    private static bool IsHealthyParticipant(IMissionParticipant participant)
    {
        if (participant is Officer officer)
            return officer.InjuryPoints <= 0 && !officer.IsRetired;

        return participant is not SpecialForces specialForces || !specialForces.IsRetired;
    }

    /// <summary>
    /// Reports whether an owned planet has at least one empty manufacturing lane.
    /// </summary>
    /// <param name="planet">The planet to evaluate.</param>
    /// <returns><see langword="true"/> when any manufacturing lane is idle.</returns>
    private static bool HasIdleManufacturing(Planet planet)
    {
        return planet is { IsDestroyed: false }
            && _manufacturingTypes.Any(type => planet.GetIdleManufacturingFacilities(type) > 0);
    }

    /// <summary>
    /// Builds one optional display entry and resolves its compact artwork.
    /// </summary>
    /// <param name="entity">The represented strategy entity.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <returns>The resolved entry, or null when the entity is absent.</returns>
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
