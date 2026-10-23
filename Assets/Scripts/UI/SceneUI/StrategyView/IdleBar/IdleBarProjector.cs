using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Selects idle personnel, fleets, and manufacturing capacity for the strategy desktop.
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
    /// Projects available officers, special-forces units, fleets, and manufacturing planets in
    /// order, with main characters ahead of other officers.
    /// </summary>
    /// <param name="factionView">The faction-filtered galaxy view.</param>
    /// <param name="playerFactionId">The represented faction identifier.</param>
    /// <param name="desktopBounds">The strategy desktop bounds.</param>
    /// <returns>The ordered idle-bar presentation.</returns>
    internal IdleBarRenderData Project(
        GalaxyMap factionView,
        string playerFactionId,
        RectInt desktopBounds
    )
    {
        UIContext uiContext = getUIContext();
        List<IdleBarEntry> entries = FindAvailableParticipants<Officer>(
                factionView,
                playerFactionId
            )
            .OrderByDescending(officer => officer.IsMain)
            .Select(officer => CreateEntry(officer, uiContext))
            .Concat(
                FindAvailableParticipants<SpecialForces>(factionView, playerFactionId)
                    .Select(specialForces => CreateEntry(specialForces, uiContext))
            )
            .Concat(
                FindIdleFleets(factionView, playerFactionId)
                    .Select(fleet => CreateEntry(fleet, uiContext))
            )
            .Concat(
                factionView
                    ?.GetChildren<Planet>(recursive: true)
                    .Where(planet => planet.OwnerInstanceID == playerFactionId)
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
    /// Enumerates artwork needed by the current idle-bar entries without loading it.
    /// </summary>
    /// <param name="factionView">The faction-filtered galaxy view.</param>
    /// <param name="playerFactionId">The represented faction identifier.</param>
    /// <param name="playerFactionTheme">The represented faction's presentation theme.</param>
    /// <returns>The configured texture paths.</returns>
    internal static IEnumerable<string> GetTexturePaths(
        GalaxyMap factionView,
        string playerFactionId,
        FactionTheme playerFactionTheme
    )
    {
        IEnumerable<ISceneNode> entities = FindAvailableParticipants<Officer>(
                factionView,
                playerFactionId
            )
            .Cast<ISceneNode>()
            .Concat(FindAvailableParticipants<SpecialForces>(factionView, playerFactionId))
            .Concat(FindIdleFleets(factionView, playerFactionId))
            .Concat(
                factionView
                    ?.GetChildren<Planet>(recursive: true)
                    .Where(planet => planet.OwnerInstanceID == playerFactionId)
                    .Where(HasIdleManufacturing)
                    .Cast<ISceneNode>()
                    ?? Enumerable.Empty<ISceneNode>()
            );

        return entities
            .Select(entity => GetTexturePath(entity, playerFactionTheme))
            .Where(path => !string.IsNullOrEmpty(path));
    }

    /// <summary>
    /// Enumerates stationary, operational fleets owned by the represented faction.
    /// </summary>
    /// <param name="factionView">The faction-filtered galaxy view.</param>
    /// <param name="playerFactionId">The represented faction identifier.</param>
    /// <returns>The idle fleets in display order.</returns>
    private static IEnumerable<Fleet> FindIdleFleets(GalaxyMap factionView, string playerFactionId)
    {
        return factionView
                ?.GetChildren<Fleet>(recursive: true)
                .Where(fleet => fleet.OwnerInstanceID == playerFactionId)
                .Where(fleet => fleet.Movement == null && fleet.HasOperationalCapitalShips())
                .OrderBy(candidate => candidate.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.InstanceID, StringComparer.Ordinal)
            ?? Enumerable.Empty<Fleet>();
    }

    /// <summary>
    /// Selects all available mission participants of one type by display name.
    /// </summary>
    /// <typeparam name="T">The mission-participant type to select.</typeparam>
    /// <param name="factionView">The faction-filtered galaxy view.</param>
    /// <param name="playerFactionId">The represented faction identifier.</param>
    /// <returns>The available participants in display order.</returns>
    private static IEnumerable<T> FindAvailableParticipants<T>(
        GalaxyMap factionView,
        string playerFactionId
    )
        where T : class, IMissionParticipant
    {
        return factionView
                ?.GetChildren<T>(recursive: true)
                .Where(participant => participant.OwnerInstanceID == playerFactionId)
                .Where(IsAvailableParticipant)
                .OfType<T>()
                .Where(IsHealthyParticipant)
                .OrderBy(candidate => candidate.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.InstanceID, StringComparer.Ordinal)
            ?? Enumerable.Empty<T>();
    }

    /// <summary>
    /// Reports whether a visible participant can currently receive mission orders.
    /// </summary>
    /// <param name="participant">The participant to inspect.</param>
    /// <returns>True when the participant is available.</returns>
    private static bool IsAvailableParticipant(IMissionParticipant participant)
    {
        if (
            participant?.IsOnMission() != false
            || !participant.IsMovable()
            || participant.GetTransitMovement() != null
        )
            return false;

        if (participant is Officer officer)
            return !officer.IsCaptured && !officer.IsKilled;

        return participant is not SpecialForces specialForces
            || specialForces.ManufacturingStatus == ManufacturingStatus.Complete
                && specialForces.AllowedMissionTypeIDs.Count > 0;
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

        Texture2D texture = entity switch
        {
            Planet planet => uiContext?.GetPlanetTexture(planet),
            Fleet fleet => uiContext?.GetTexture(
                GetFleetTexturePath(uiContext.GetTheme(fleet.OwnerInstanceID))
            ),
            _ => uiContext?.GetEntityTexture(entity, true),
        };
        return new IdleBarEntry(entity, texture);
    }

    /// <summary>
    /// Resolves the configured compact artwork path for one projected entity.
    /// </summary>
    /// <param name="entity">The projected strategy entity.</param>
    /// <param name="playerFactionTheme">The represented faction's presentation theme.</param>
    /// <returns>The configured texture path, or null when no artwork is configured.</returns>
    private static string GetTexturePath(ISceneNode entity, FactionTheme playerFactionTheme)
    {
        return entity switch
        {
            Planet planet => planet.GetPlanetIconPath(),
            Fleet => GetFleetTexturePath(playerFactionTheme),
            _ when !string.IsNullOrEmpty(entity.SmallDisplayImagePath) =>
                entity.SmallDisplayImagePath,
            _ => entity.GetDisplayImagePath(),
        };
    }

    /// <summary>
    /// Resolves the normal fleet-list artwork shared by compact fleet presentations.
    /// </summary>
    /// <param name="theme">The represented fleet owner's presentation theme.</param>
    /// <returns>The configured fleet-list texture path, or null.</returns>
    private static string GetFleetTexturePath(FactionTheme theme)
    {
        return theme?.PlanetOverlayTheme?.UnitTileIcons?.FleetListIconImagePath;
    }
}
