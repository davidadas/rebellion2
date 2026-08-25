using System;
using System.Collections.Generic;
using System.IO;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using Rebellion.Generation;

/// <summary>
/// Provides the typed game data composed from the selected content pack and scenario.
/// </summary>
public sealed class GameDataCatalog
{
    public GameConfig GameConfig { get; }

    public GameGenerationConfig GenerationConfig { get; }

    public Faction[] Factions { get; }

    public PlanetSector[] PlanetSectors { get; }

    public Building[] Buildings { get; }

    public CapitalShip[] CapitalShips { get; }

    public Starfighter[] Starfighters { get; }

    public Regiment[] Regiments { get; }

    public SpecialForces[] SpecialForces { get; }

    public Officer[] Officers { get; }

    public GameEvent[] GameEvents { get; }

    public MessageDefinition[] MessageDefinitions { get; }

    public EncyclopediaEntries EncyclopediaEntries { get; }

    public FactionThemes FactionThemes { get; }

    /// <summary>
    /// Creates a complete typed game-data catalog.
    /// </summary>
    /// <param name="gameConfig">The runtime game configuration.</param>
    /// <param name="generationConfig">The selected scenario's generation configuration.</param>
    /// <param name="factions">The faction templates.</param>
    /// <param name="planetSectors">The planet-sector templates.</param>
    /// <param name="buildings">The building templates.</param>
    /// <param name="capitalShips">The capital-ship templates.</param>
    /// <param name="starfighters">The starfighter templates.</param>
    /// <param name="regiments">The regiment templates.</param>
    /// <param name="specialForces">The special-forces templates.</param>
    /// <param name="officers">The officer templates.</param>
    /// <param name="gameEvents">The game-event definitions.</param>
    /// <param name="messageDefinitions">The message definitions.</param>
    /// <param name="encyclopediaEntries">The authored encyclopedia entries.</param>
    /// <param name="factionThemes">The neutral and faction presentation themes.</param>
    public GameDataCatalog(
        GameConfig gameConfig,
        GameGenerationConfig generationConfig,
        Faction[] factions,
        PlanetSector[] planetSectors,
        Building[] buildings,
        CapitalShip[] capitalShips,
        Starfighter[] starfighters,
        Regiment[] regiments,
        SpecialForces[] specialForces,
        Officer[] officers,
        GameEvent[] gameEvents,
        MessageDefinition[] messageDefinitions,
        EncyclopediaEntries encyclopediaEntries,
        FactionThemes factionThemes
    )
    {
        GameConfig = gameConfig ?? throw new ArgumentNullException(nameof(gameConfig));
        GenerationConfig =
            generationConfig ?? throw new ArgumentNullException(nameof(generationConfig));
        Factions = factions ?? throw new ArgumentNullException(nameof(factions));
        PlanetSectors = planetSectors ?? throw new ArgumentNullException(nameof(planetSectors));
        Buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        ValidateBuildingUpgrades(Buildings);
        CapitalShips = capitalShips ?? throw new ArgumentNullException(nameof(capitalShips));
        Starfighters = starfighters ?? throw new ArgumentNullException(nameof(starfighters));
        Regiments = regiments ?? throw new ArgumentNullException(nameof(regiments));
        SpecialForces = specialForces ?? throw new ArgumentNullException(nameof(specialForces));
        Officers = officers ?? throw new ArgumentNullException(nameof(officers));
        GameEvents = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
        MessageDefinitions =
            messageDefinitions ?? throw new ArgumentNullException(nameof(messageDefinitions));
        EncyclopediaEntries =
            encyclopediaEntries ?? throw new ArgumentNullException(nameof(encyclopediaEntries));
        FactionThemes = factionThemes ?? throw new ArgumentNullException(nameof(factionThemes));
    }

    /// <summary>
    /// Validates authored building upgrade references and rejects cyclic upgrade paths.
    /// </summary>
    /// <param name="buildings">The building templates to validate.</param>
    internal static void ValidateBuildingUpgrades(IReadOnlyCollection<Building> buildings)
    {
        if (buildings == null)
            throw new ArgumentNullException(nameof(buildings));

        Dictionary<string, Building> buildingsByTypeID = new Dictionary<string, Building>(
            StringComparer.Ordinal
        );
        foreach (Building building in buildings)
        {
            if (building == null || string.IsNullOrWhiteSpace(building.TypeID))
                throw new InvalidDataException("Every building requires a TypeID.");
            if (!buildingsByTypeID.TryAdd(building.TypeID, building))
                throw new InvalidDataException($"Duplicate building TypeID '{building.TypeID}'.");
        }

        foreach (Building building in buildings)
        {
            HashSet<string> discoveredUpgrades = new HashSet<string>(StringComparer.Ordinal);
            foreach (string upgradeTypeID in building.Upgrades ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(upgradeTypeID))
                    throw new InvalidDataException(
                        $"Building '{building.TypeID}' contains a blank upgrade TypeID."
                    );
                if (!buildingsByTypeID.ContainsKey(upgradeTypeID))
                    throw new InvalidDataException(
                        $"Building '{building.TypeID}' references missing upgrade '{upgradeTypeID}'."
                    );
                if (upgradeTypeID == building.TypeID)
                    throw new InvalidDataException(
                        $"Building '{building.TypeID}' cannot upgrade to itself."
                    );
                if (!discoveredUpgrades.Add(upgradeTypeID))
                    throw new InvalidDataException(
                        $"Building '{building.TypeID}' contains duplicate upgrade '{upgradeTypeID}'."
                    );
            }
        }

        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> activePath = new HashSet<string>(StringComparer.Ordinal);
        foreach (Building building in buildings)
            ValidateBuildingUpgradePath(building, buildingsByTypeID, visited, activePath);
    }

    /// <summary>
    /// Validates one building upgrade path using depth-first graph traversal.
    /// </summary>
    /// <param name="building">The building whose upgrade path is being traversed.</param>
    /// <param name="buildingsByTypeID">The building templates indexed by type identifier.</param>
    /// <param name="visited">The building types whose complete paths have been validated.</param>
    /// <param name="activePath">The building types in the current traversal path.</param>
    private static void ValidateBuildingUpgradePath(
        Building building,
        IReadOnlyDictionary<string, Building> buildingsByTypeID,
        ISet<string> visited,
        ISet<string> activePath
    )
    {
        if (visited.Contains(building.TypeID))
            return;
        if (!activePath.Add(building.TypeID))
            throw new InvalidDataException(
                $"Building upgrade paths contain a cycle at '{building.TypeID}'."
            );

        foreach (string upgradeTypeID in building.Upgrades ?? new List<string>())
            ValidateBuildingUpgradePath(
                buildingsByTypeID[upgradeTypeID],
                buildingsByTypeID,
                visited,
                activePath
            );

        activePath.Remove(building.TypeID);
        visited.Add(building.TypeID);
    }
}
