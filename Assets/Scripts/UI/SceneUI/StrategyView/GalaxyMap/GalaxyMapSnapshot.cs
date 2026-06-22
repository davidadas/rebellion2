using System.Collections.Generic;
using Rebellion.Game.Galaxy;

public enum PlanetIcon
{
    None,
    Facility,
    Defense,
    Fleet,
    Mission,
}

public sealed class GalaxyMapSector
{
    public GalaxyMapSector(PlanetSystem system)
    {
        System = system;
    }

    public PlanetSystem System { get; }
    public List<GalaxyMapPlanet> Planets { get; } = new List<GalaxyMapPlanet>();
}

public sealed class GalaxyMapPlanet
{
    public GalaxyMapPlanet(PlanetSystem sector, Planet planet, string planetIconPath)
    {
        Sector = null;
        SectorSystem = sector;
        Planet = planet;
        PlanetIconPath = planetIconPath ?? string.Empty;
    }

    public GalaxyMapSector Sector { get; set; }
    public PlanetSystem SectorSystem { get; }
    public Planet Planet { get; }
    public string PlanetIconPath { get; }

    public string OwnerFactionId => Planet?.OwnerInstanceID;
}
