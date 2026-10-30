using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Gameplay;
using Rebellion.Generation;
using Rebellion.SceneGraph;

/// <summary>
/// Checks that a loaded authored scenario can construct and initialize a playable campaign.
/// </summary>
internal static class ContentEditorPackValidator
{
    /// <summary>
    /// Checks authored identities and generation references independently of random selections.
    /// </summary>
    /// <param name="pack">The loaded scenario and catalogs.</param>
    internal static void ValidateDefinitions(ContentPack pack)
    {
        GameDataCatalog data = pack.GameData;
        HashSet<string> factions = Index(
            data.Factions.Select(item => item.InstanceID),
            "faction ID"
        );
        HashSet<string> planets = Index(
            data.PlanetSectors.SelectMany(sector => sector.GetChildren<Planet>())
                .Select(item => item.TypeID),
            "planet TypeID"
        );
        Index(data.PlanetSectors.Select(item => item.TypeID), "sector TypeID");
        HashSet<string> officers = Index(
            data.Officers.Select(item => item.InstanceID),
            "officer ID"
        );
        IEnumerable<ISceneNode> units = data
            .Buildings.Cast<ISceneNode>()
            .Concat(data.CapitalShips)
            .Concat(data.Starfighters)
            .Concat(data.Regiments)
            .Concat(data.SpecialForces);
        HashSet<string> unitTypes = Index(units.Select(item => item.TypeID), "unit TypeID");
        HashSet<string> buildings = new HashSet<string>(
            data.Buildings.Select(item => item.TypeID),
            StringComparer.Ordinal
        );
        HashSet<string> ships = new HashSet<string>(
            data.CapitalShips.Select(item => item.TypeID),
            StringComparer.Ordinal
        );
        HashSet<string> regiments = new HashSet<string>(
            data.Regiments.Select(item => item.TypeID),
            StringComparer.Ordinal
        );
        foreach (var faction in data.Factions)
            if (!string.IsNullOrEmpty(faction.GarrisonTroopTypeID))
                RequireReference(regiments, faction.GarrisonTroopTypeID, "garrison troop");

        GameGenerationConfig config = data.GenerationConfig;
        foreach (StartingOfficerRule rule in config.Officers.StartingOfficers)
        {
            RequireReference(officers, rule.OfficerInstanceID, "starting officer");
            if (rule.DestinationTypeID != null)
                RequirePlanet(planets, rule.DestinationTypeID);
        }
        foreach (FactionSetup setup in config.GalaxyClassification.FactionSetups)
        {
            RequireReference(factions, setup.FactionID, "starting faction");
            foreach (StartingPlanet planet in setup.StartingPlanets)
            {
                if (!planet.PickFromRim)
                    RequireReference(planets, planet.PlanetTypeID, "starting planet");
                foreach (string factionID in planet.VisibleToFactionIDs ?? new List<string>())
                    RequireReference(factions, factionID, "planet visibility faction");
            }
        }
        foreach (FactionBucketConfig bucket in config.GalaxyClassification.FactionBuckets)
            RequireReference(factions, bucket.FactionID, "ownership faction");
        foreach (FactionModifierConfig modifier in config.FactionModifiers)
            RequireReference(factions, modifier.FactionID, "modifier faction");

        FacilityGenerationSection facilities = config.FacilityGeneration;
        RequireReference(buildings, facilities.MineTypeID, "mine building");
        foreach (
            WeightedFacilityEntry entry in (
                facilities.CoreFacilityTable ?? new List<WeightedFacilityEntry>()
            ).Concat(facilities.RimFacilityTable ?? new List<WeightedFacilityEntry>())
        )
            if (entry.TypeID != null)
                RequireReference(buildings, entry.TypeID, "weighted building");
        foreach (
            HQFacilityLoadout loadout in facilities.HQLoadouts ?? new List<HQFacilityLoadout>()
        )
        {
            RequirePlanet(planets, loadout.PlanetTypeID);
            if (loadout.PlanetTypeID == GameGenerationConfig.FactionHqSentinel)
                RequireReference(factions, loadout.FactionID, "headquarters faction");
            foreach (string typeID in loadout.FacilityTypeIDs ?? new List<string>())
                RequireReference(buildings, typeID, "headquarters building");
        }
        foreach (FixedGarrison garrison in config.UnitDeployment.FixedGarrisons)
        {
            RequirePlanet(planets, garrison.PlanetTypeID);
            RequireReference(factions, garrison.FactionID, "garrison faction");
            ValidateUnits(unitTypes, garrison.Units);
        }
        foreach (FixedFleet fleet in config.UnitDeployment.FixedFleets)
        {
            RequireReference(factions, fleet.FactionID, "fleet faction");
            if (fleet.TargetPlanets?.Count > 0)
            {
                foreach (string target in fleet.TargetPlanets)
                    RequirePlanet(planets, target);
            }
            else
                RequirePlanet(planets, fleet.PlanetTypeID);
            foreach (FixedFleetShip ship in fleet.ShipEntries)
            {
                RequireReference(ships, ship.TypeID, "fleet capital ship");
                ValidateUnits(unitTypes, ship.Cargo);
            }
        }
        foreach (FactionBudget budget in config.UnitDeployment.FactionBudgets)
        {
            RequireReference(factions, budget.FactionID, "unit budget faction");
            foreach (WeightedUnitEntry entry in budget.UnitTable ?? new List<WeightedUnitEntry>())
                ValidateUnits(unitTypes, entry.Units);
        }
    }

    /// <summary>
    /// Enumerates every exposed new-game setting using a reproducible generation seed.
    /// </summary>
    /// <param name="pack">The scenario and catalogs already checked by the content loader.</param>
    /// <returns>The independent startup configurations to check.</returns>
    internal static IEnumerable<GameSummary> GetStartupConfigurations(ContentPack pack)
    {
        foreach (string factionID in pack.Scenario.PlayableFactionIDs)
        foreach (GameSize size in Enum.GetValues(typeof(GameSize)))
        foreach (GameDifficulty difficulty in Enum.GetValues(typeof(GameDifficulty)))
        foreach (
            GameResourceAvailability resources in Enum.GetValues(typeof(GameResourceAvailability))
        )
        {
            yield return new GameSummary
            {
                PackID = pack.Definition.ID,
                PackVersion = pack.Definition.Version,
                ScenarioID = pack.Scenario.ID,
                PlayerFactionID = factionID,
                GalaxySize = size,
                Difficulty = difficulty,
                ResourceAvailability = resources,
                Seed = 0,
            };
        }
    }

    /// <summary>
    /// Constructs and initializes one campaign without running gameplay ticks.
    /// </summary>
    /// <param name="pack">The loaded scenario and catalogs.</param>
    /// <param name="summary">The configuration being checked.</param>
    internal static void ValidateStartup(ContentPack pack, GameSummary summary)
    {
        try
        {
            GameRoot game = new GameBuilder(summary, pack.GameData).Build();
            using GameSession session = new GameSession(game, pack.GameData);
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                $"Scenario '{pack.Scenario.ID}' cannot start ({summary.PlayerFactionID}, {summary.GalaxySize}, {summary.Difficulty}, {summary.ResourceAvailability}): {exception.Message}",
                exception
            );
        }
    }

    /// <summary>
    /// Rejects absent or ambiguous identities within one runtime lookup domain.
    /// </summary>
    /// <param name="identifiers">The authored identities.</param>
    /// <param name="description">The identity domain shown in errors.</param>
    /// <returns>The unique identities.</returns>
    private static HashSet<string> Index(IEnumerable<string> identifiers, string description)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
        foreach (string identifier in identifiers)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new InvalidDataException($"Missing {description}.");
            if (!result.Add(identifier))
                throw new InvalidDataException($"Duplicate {description} '{identifier}'.");
        }
        return result;
    }

    /// <summary>
    /// Checks every authored unit entry, including entries a particular random seed never uses.
    /// </summary>
    /// <param name="unitTypes">The manufacturable unit identities.</param>
    /// <param name="entries">The optional deployment entries.</param>
    private static void ValidateUnits(ISet<string> unitTypes, IEnumerable<UnitEntry> entries)
    {
        foreach (UnitEntry entry in entries ?? Enumerable.Empty<UnitEntry>())
            RequireReference(unitTypes, entry.TypeID, "deployment unit");
    }

    /// <summary>
    /// Accepts a concrete planet type or the generation system's headquarters selector.
    /// </summary>
    /// <param name="planets">The authored planet identities.</param>
    /// <param name="identifier">The deployment destination.</param>
    private static void RequirePlanet(ISet<string> planets, string identifier)
    {
        if (identifier != GameGenerationConfig.FactionHqSentinel)
            RequireReference(planets, identifier, "deployment planet");
    }

    /// <summary>
    /// Reports a reference that cannot be resolved by its owning runtime lookup.
    /// </summary>
    /// <param name="identifiers">The valid target identities.</param>
    /// <param name="identifier">The authored reference.</param>
    /// <param name="description">The purpose of the reference.</param>
    private static void RequireReference(
        ISet<string> identifiers,
        string identifier,
        string description
    )
    {
        if (identifier == null || !identifiers.Contains(identifier))
            throw new InvalidDataException($"Unknown {description} '{identifier}'.");
    }
}
