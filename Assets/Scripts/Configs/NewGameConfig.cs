using System.Collections;
using System.Reflection;
using System.Linq;
using System;

/// **********************
/// START SHARED CONFIGS
/// **********************
[Serializable]
public class NewGamePlanetSizeConfig : Config
{
    public int Snall;
    public int Medium;
    public int Large;

    public NewGamePlanetSizeConfig() { }
}

/// **********************
/// START PLANET CONFIG
/// **********************
[Serializable]
public class NewGameResourceRangesConfig : Config
{
    public int[] GroundSlotRange;
    public int[] OrbitSlotRange;
    public int[] ResourceRange;

    public NewGameResourceRangesConfig() { }
}

[Serializable]
public class NewGameResourceSystemConfig : Config
{
    public NewGameResourceRangesConfig CoreSystem;
    public NewGameResourceRangesConfig OuterRim;

    public NewGameResourceSystemConfig() { }
}

[Serializable]
public class NewGameResourceAvailabilityConfig : Config
{
    public NewGameResourceSystemConfig Limited;
    public NewGameResourceSystemConfig Normal;
    public NewGameResourceSystemConfig Abundant;

    public NewGameResourceAvailabilityConfig() { }
}

[Serializable]
public class NewGameNumInitialPlanetsConfig : Config
{
    public NewGamePlanetSizeConfig GalaxySize;

    public NewGameNumInitialPlanetsConfig() { }
}

[Serializable]
public class NewGamePlanetConfig : Config
{
    public NewGameResourceAvailabilityConfig ResourceAvailability;
    public NewGameNumInitialPlanetsConfig NumInitialPlanets;
    public double InitialColonizationRate;

    public NewGamePlanetConfig() { }
}

/// **********************
/// START OFFICER CONFIG
/// **********************
[Serializable]
public class NewGameNumInitialOfficersConfig : Config
{
    public NewGamePlanetSizeConfig GalaxySize;

    public NewGameNumInitialOfficersConfig() { }
}

[Serializable]
public class NewGameOfficerConfig : Config
{
    public NewGameNumInitialOfficersConfig NumInitialOfficers;

    public NewGameOfficerConfig() { }
}

/// **********************
/// START BUILDINGS CONFIG
/// **********************

[Serializable]
public class NewGameInitialBuildingsConfig : Config
{
    public string[] TypeIDs;
    public double[] Frequency;

    public NewGameInitialBuildingsConfig() { }
}

[Serializable]
public class NewGameBuildingConfig : Config
{
    public NewGameInitialBuildingsConfig InitialBuildings;

    public NewGameBuildingConfig() { }
}

/// **********************
/// START CAPITALSHIPS CONFIG
/// **********************

[Serializable]
public class NewGameCapitalShipOptions : Config
{
    public string OwnerTypeID;
    public string TypeID;
    public string InitialParentTypeID;

    public NewGameCapitalShipOptions() { }
}

[Serializable]
public class NewGameCapitalShipsGalaxyConfig : Config
{
    public NewGameCapitalShipOptions[] Small;
    public NewGameCapitalShipOptions[] Medium;
    public NewGameCapitalShipOptions[] Large;

    public NewGameCapitalShipsGalaxyConfig() { }
}

[Serializable]
public class NewGameInitialCapitalShipsConfig : Config
{
    public NewGameCapitalShipsGalaxyConfig GalaxySize;

    public NewGameInitialCapitalShipsConfig() { }
}

[Serializable]
public class NewGameCapitalShipsConfig : Config
{
    public NewGameInitialCapitalShipsConfig InitialCapitalShips;

    public NewGameCapitalShipsConfig() { }
}

/// **********************
/// START ROOT CONFIG
/// **********************
[Serializable]
public class NewGameConfig : Config
{
    public NewGamePlanetConfig Planets;
    public NewGameOfficerConfig Officers;
    public NewGameBuildingConfig Buildings;
    public NewGameCapitalShipsConfig CapitalShips;

    public NewGameConfig() { }
}
