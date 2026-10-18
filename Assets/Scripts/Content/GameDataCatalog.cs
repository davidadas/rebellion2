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
    private readonly IReadOnlyDictionary<string, OfficerTraitDefinition> _officerTraitsByID;

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

    public OfficerTraitDefinition[] OfficerTraits { get; }

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
    /// <param name="officerTraits">The content-authored officer trait definitions.</param>
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
        FactionThemes factionThemes,
        OfficerTraitDefinition[] officerTraits = null
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
        OfficerTraits = officerTraits ?? Array.Empty<OfficerTraitDefinition>();
        _officerTraitsByID = ValidateOfficerTraits(OfficerTraits, Officers);
        foreach (Officer officer in Officers)
            officer.BindTraitDefinitions(_officerTraitsByID);
        GameEvents = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
        MessageDefinitions =
            messageDefinitions ?? throw new ArgumentNullException(nameof(messageDefinitions));
        EncyclopediaEntries =
            encyclopediaEntries ?? throw new ArgumentNullException(nameof(encyclopediaEntries));
        FactionThemes = factionThemes ?? throw new ArgumentNullException(nameof(factionThemes));
    }

    /// <summary>
    /// Validates trait definitions and authored officer references, then indexes the definitions.
    /// </summary>
    /// <param name="traits">The trait definitions to validate.</param>
    /// <param name="officers">The officer templates that reference those definitions.</param>
    /// <returns>The validated definitions indexed by identifier.</returns>
    internal static IReadOnlyDictionary<string, OfficerTraitDefinition> ValidateOfficerTraits(
        IReadOnlyCollection<OfficerTraitDefinition> traits,
        IReadOnlyCollection<Officer> officers
    )
    {
        if (traits == null)
            throw new ArgumentNullException(nameof(traits));
        if (officers == null)
            throw new ArgumentNullException(nameof(officers));

        Dictionary<string, OfficerTraitDefinition> traitsByID = new Dictionary<
            string,
            OfficerTraitDefinition
        >(StringComparer.Ordinal);
        foreach (OfficerTraitDefinition trait in traits)
        {
            if (trait == null || string.IsNullOrWhiteSpace(trait.ID))
                throw new InvalidDataException("Every officer trait requires an ID.");
            if (!traitsByID.TryAdd(trait.ID, trait))
                throw new InvalidDataException($"Duplicate officer trait ID '{trait.ID}'.");
            if (trait.LeadershipBonusPercent < 0)
                throw new InvalidDataException(
                    $"Officer trait '{trait.ID}' has a negative leadership bonus."
                );
            if (trait.PersonnelTravelTimePercent is < 1 or > 100)
                throw new InvalidDataException(
                    $"Officer trait '{trait.ID}' requires a personnel travel-time percentage from 1 through 100."
                );
        }

        foreach (Officer officer in officers)
        {
            if (officer == null)
                throw new InvalidDataException("Officer templates cannot contain null entries.");
            HashSet<string> discoveredTraits = new HashSet<string>(StringComparer.Ordinal);
            foreach (string traitID in officer.Traits ?? new List<string>())
            {
                if (!traitsByID.ContainsKey(traitID))
                    throw new InvalidDataException(
                        $"Officer '{officer.InstanceID}' references missing trait '{traitID}'."
                    );
                if (!discoveredTraits.Add(traitID))
                    throw new InvalidDataException(
                        $"Officer '{officer.InstanceID}' contains duplicate trait '{traitID}'."
                    );
            }
        }

        return traitsByID;
    }

    /// <summary>
    /// Connects runtime officers to the validated trait definitions in this catalog.
    /// </summary>
    /// <param name="officers">The runtime officers to bind.</param>
    internal void BindOfficerTraits(IEnumerable<Officer> officers)
    {
        foreach (Officer officer in officers ?? throw new ArgumentNullException(nameof(officers)))
        {
            HashSet<(string TraitID, string SourceID)> discoveredGrants =
                new HashSet<(string TraitID, string SourceID)>();
            foreach (
                OfficerTraitGrant grant in officer.TraitGrants ?? new List<OfficerTraitGrant>()
            )
            {
                if (grant == null || !_officerTraitsByID.ContainsKey(grant.TraitID))
                    throw new InvalidDataException(
                        $"Officer '{officer.InstanceID}' contains an invalid trait grant."
                    );
                if (string.IsNullOrWhiteSpace(grant.SourceID))
                    throw new InvalidDataException(
                        $"Officer '{officer.InstanceID}' contains a trait grant without a source."
                    );
                if (!discoveredGrants.Add((grant.TraitID, grant.SourceID)))
                    throw new InvalidDataException(
                        $"Officer '{officer.InstanceID}' contains a duplicate trait grant."
                    );
            }
            officer.BindTraitDefinitions(_officerTraitsByID);
        }
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
