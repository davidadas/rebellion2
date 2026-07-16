using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Rebellion.Game.Galaxy;

/// <summary>
/// Identifies a semantic planet-window overlay category.
/// </summary>
public enum PlanetIcon
{
    None,
    Facility,
    Defense,
    Fleet,
    Mission,
}

/// <summary>
/// Contains one visible planet system and its projected galaxy-map planets.
/// </summary>
public sealed class GalaxyMapSector
{
    /// <summary>
    /// Creates a visible galaxy-map sector for one planet system.
    /// </summary>
    /// <param name="system">The represented planet system.</param>
    /// <param name="planets">The visible planets in source render order.</param>
    public GalaxyMapSector(PlanetSystem system, IReadOnlyList<GalaxyMapPlanet> planets)
    {
        System = system;
        Planets = CreatePlanetSnapshot(planets);
    }

    /// <summary>
    /// Gets the represented planet system.
    /// </summary>
    public PlanetSystem System { get; }

    /// <summary>
    /// Gets the visible planets in source render order.
    /// </summary>
    public IReadOnlyList<GalaxyMapPlanet> Planets { get; }

    /// <summary>
    /// Copies visible planets into an immutable sector-owned snapshot.
    /// </summary>
    /// <param name="planets">The visible planets in source render order.</param>
    /// <returns>An isolated read-only planet collection.</returns>
    private IReadOnlyList<GalaxyMapPlanet> CreatePlanetSnapshot(
        IReadOnlyList<GalaxyMapPlanet> planets
    )
    {
        if (planets == null || planets.Count == 0)
            return Array.Empty<GalaxyMapPlanet>();

        GalaxyMapPlanet[] snapshot = new GalaxyMapPlanet[planets.Count];
        for (int i = 0; i < planets.Count; i++)
        {
            GalaxyMapPlanet planet = planets[i];
            planet?.AttachToSector(this);
            snapshot[i] = planet;
        }

        return new ReadOnlyCollection<GalaxyMapPlanet>(snapshot);
    }
}

/// <summary>
/// Associates one visible planet with its source sector and presentation identity.
/// </summary>
public sealed class GalaxyMapPlanet
{
    /// <summary>
    /// Creates a visible galaxy-map planet snapshot.
    /// </summary>
    /// <param name="sector">The represented planet system.</param>
    /// <param name="planet">The visible planet snapshot.</param>
    /// <param name="planetIconPath">The resolved planet-art resource path.</param>
    public GalaxyMapPlanet(PlanetSystem sector, Planet planet, string planetIconPath)
    {
        SectorSystem = sector;
        Planet = planet;
        PlanetIconPath = planetIconPath ?? string.Empty;
    }

    /// <summary>
    /// Gets the owning visible galaxy-map sector.
    /// </summary>
    public GalaxyMapSector Sector { get; private set; }

    /// <summary>
    /// Gets the represented planet system.
    /// </summary>
    public PlanetSystem SectorSystem { get; }

    /// <summary>
    /// Gets the visible planet snapshot.
    /// </summary>
    public Planet Planet { get; }

    /// <summary>
    /// Gets the resolved planet-art resource path.
    /// </summary>
    public string PlanetIconPath { get; }

    /// <summary>
    /// Gets the visible planet owner's faction identifier.
    /// </summary>
    public string OwnerFactionId => Planet?.OwnerInstanceID;

    /// <summary>
    /// Associates this planet snapshot with its immutable owning sector.
    /// </summary>
    /// <param name="sector">The owning visible galaxy-map sector.</param>
    internal void AttachToSector(GalaxyMapSector sector)
    {
        if (sector == null)
            throw new ArgumentNullException(nameof(sector));
        if (Sector != null && !ReferenceEquals(Sector, sector))
        {
            throw new InvalidOperationException(
                "A galaxy-map planet snapshot cannot belong to more than one sector."
            );
        }

        Sector = sector;
    }
}
