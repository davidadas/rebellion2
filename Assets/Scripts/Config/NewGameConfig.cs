using System.Collections;
using System.Reflection;
using System.Linq;
using System;

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
public class NewGameInitialPlanetsConfig : Config
{
    public int Snall;
    public int Medium;
    public int Large;

    public NewGameInitialPlanetsConfig() { }
}

[Serializable]
public class NewGamePlanetConfig : Config
{
    public NewGameResourceAvailabilityConfig ResourceAvailability;
    public NewGameInitialPlanetsConfig InitialPlanets;

    public NewGamePlanetConfig() { }
}

/// **********************
/// START OFFICER CONFIG
/// **********************
[Serializable]
public class NewGameInitialOfficersConfig : Config
{
    public int Small;
    public int Medium;
    public int Large;

    public NewGameInitialOfficersConfig() { }
}

[Serializable]
public class NewGameOfficerConfig : Config
{
    public NewGameInitialOfficersConfig InitialOfficers;

    public NewGameOfficerConfig() { }
}

/// **********************
/// START BUILDINGS CONFIG
/// **********************

[Serializable]
public class NewGameInitialBuildingsConfig : Config
{
    public string[] GameIDs;
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
/// START ROOT CONFIG
/// **********************
[Serializable]
public class NewGameConfig : Config
{
    public NewGamePlanetConfig Planets;
    public NewGameOfficerConfig Officers;
    public NewGameBuildingConfig Buildings;

    public NewGameConfig() { }
}
