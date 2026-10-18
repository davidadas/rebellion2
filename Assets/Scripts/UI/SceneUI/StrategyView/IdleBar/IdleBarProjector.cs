using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
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
        StatusWindowTheme manufacturingTheme = uiContext
            ?.GetTheme(playerFaction?.InstanceID)
            ?.StrategyWindows?.Status;
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
                    .OrderBy(
                        candidate => candidate.GetDisplayName(),
                        StringComparer.OrdinalIgnoreCase
                    )
                    .ThenBy(candidate => candidate.InstanceID, StringComparer.Ordinal)
                    .SelectMany(planet =>
                        GetIdleManufacturingTypes(planet)
                            .Select(type => new IdleBarEntry(
                                planet,
                                uiContext?.GetTexture(
                                    GetManufacturingImagePath(type, manufacturingTheme)
                                ),
                                type
                            ))
                    )
                    ?? Enumerable.Empty<IdleBarEntry>()
            )
            .ToList();

        return new IdleBarRenderData(true, entries, desktopBounds);
    }

    /// <summary>
    /// Enumerates artwork needed by the current idle-bar entries without loading it.
    /// </summary>
    /// <param name="playerFaction">The faction whose available entities are displayed.</param>
    /// <param name="manufacturingTheme">The faction's configured manufacturing artwork.</param>
    /// <returns>The configured texture paths.</returns>
    internal static IEnumerable<string> GetTexturePaths(
        Faction playerFaction,
        StatusWindowTheme manufacturingTheme
    )
    {
        IEnumerable<ISceneNode> entities = FindAvailableParticipants<Officer>(playerFaction)
            .Cast<ISceneNode>()
            .Concat(FindAvailableParticipants<SpecialForces>(playerFaction));

        return entities
            .Select(entity =>
                string.IsNullOrEmpty(entity.SmallDisplayImagePath)
                    ? entity.GetDisplayImagePath()
                    : entity.SmallDisplayImagePath
            )
            .Concat(
                playerFaction
                    ?.GetOwnedUnitsByType<Planet>()
                    .SelectMany(planet =>
                        GetIdleManufacturingTypes(planet)
                            .Select(type => GetManufacturingImagePath(type, manufacturingTheme))
                    )
                    ?? Enumerable.Empty<string>()
            )
            .Where(path => !string.IsNullOrEmpty(path));
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
        {
            return officer.InjuryPoints <= 0
                && !officer.IsRetired
                && officer.CurrentRank == OfficerRank.None;
        }

        return participant is not SpecialForces specialForces || !specialForces.IsRetired;
    }

    /// <summary>
    /// Selects each manufacturing lane with available capacity once per planet.
    /// </summary>
    /// <param name="planet">The planet to evaluate.</param>
    /// <returns>The idle lane types in display order.</returns>
    private static IEnumerable<ManufacturingType> GetIdleManufacturingTypes(Planet planet)
    {
        return planet is { IsDestroyed: false }
            ? _manufacturingTypes.Where(type => planet.GetIdleManufacturingFacilities(type) > 0)
            : Enumerable.Empty<ManufacturingType>();
    }

    /// <summary>
    /// Resolves the existing faction artwork for one manufacturing lane.
    /// </summary>
    /// <param name="type">The manufacturing lane type.</param>
    /// <param name="theme">The faction's status-window artwork.</param>
    /// <returns>The configured manufacturing icon path.</returns>
    private static string GetManufacturingImagePath(ManufacturingType type, StatusWindowTheme theme)
    {
        return type switch
        {
            ManufacturingType.Ship => theme?.ShipyardImagePath,
            ManufacturingType.Troop => theme?.TrainingImagePath,
            ManufacturingType.Building => theme?.ConstructionImagePath,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
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
