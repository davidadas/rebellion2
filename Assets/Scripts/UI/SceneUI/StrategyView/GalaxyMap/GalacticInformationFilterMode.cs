/// <summary>
/// Identifies the galaxy-map value represented by planet-marker intensity.
/// </summary>
public enum GalacticInformationFilterMode
{
    PopularSupport = 0x11,
    Uprisings = 0x12,
    IdleFleets = 0x21,
    FleetsEnroute = 0x22,
    IdlePersonnel = 0x43,
    ActivePersonnel = 0x44,
    AvailableEnergy = 0x51,
    AvailableRawMaterial = 0x52,
    Mines = 0x53,
    Refineries = 0x54,
    Shipyards = 0x62,
    TrainingFacilities = 0x63,
    ConstructionYards = 0x64,
    IdleShipyards = 0x65,
    IdleTrainingFacilities = 0x66,
    IdleConstructionYards = 0x67,
    Troopers = 0x71,
    FighterSquadrons = 0x72,
    DeathStarShields = 0x73,
    PlanetaryShieldGenerators = 0x74,
    PlanetaryDefenseBatteries = 0x75,
    DisplayOff = 0x80,
}
