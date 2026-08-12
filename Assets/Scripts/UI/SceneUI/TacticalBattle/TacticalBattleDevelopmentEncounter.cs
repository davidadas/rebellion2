using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Extensions;

/// <summary>
/// Builds a deterministic, content-driven encounter for direct tactical-scene development.
/// </summary>
internal static class TacticalBattleDevelopmentEncounter
{
    /// <summary>
    /// Creates an encounter containing one of every eligible capital ship and starfighter for
    /// each of the first two configured factions.
    /// </summary>
    /// <param name="gameData">The active content pack's game-data catalog.</param>
    /// <returns>The preview encounter and the faction controlled by the developer.</returns>
    internal static TacticalBattleDevelopmentEncounterResult Create(GameDataCatalog gameData)
    {
        if (gameData == null)
            throw new ArgumentNullException(nameof(gameData));

        List<FactionTheme> factionThemes = new FactionThemeLibrary(gameData.FactionThemes)
            .GetAllThemes()
            .Where(theme => theme.TacticalBattle != null)
            .Take(2)
            .ToList();
        if (factionThemes.Count != 2)
        {
            throw new InvalidOperationException(
                "Tactical development preview requires two faction tactical themes."
            );
        }

        string attackerId = factionThemes[0].FactionInstanceID;
        string defenderId = factionThemes[1].FactionInstanceID;
        Planet planet = CreatePlanet(gameData);
        Fleet attacker = CreateFleet(gameData, planet, attackerId, "Development Attacker");
        Fleet defender = CreateFleet(gameData, planet, defenderId, "Development Defender");

        return new TacticalBattleDevelopmentEncounterResult(
            new PendingCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
                AttackerOwnerInstanceID = attackerId,
                DefenderOwnerInstanceID = defenderId,
                Planet = planet,
                AttackerCanRetreat = true,
                DefenderCanRetreat = true,
            },
            attackerId
        );
    }

    /// <summary>
    /// Creates the neutral preview planet from the first configured tactical planet texture.
    /// </summary>
    /// <param name="gameData">The active game data.</param>
    /// <returns>A clean planet used only by the preview encounter.</returns>
    private static Planet CreatePlanet(GameDataCatalog gameData)
    {
        Planet template = gameData
            .PlanetSystems.Where(system => system?.Planets != null)
            .SelectMany(system => system.Planets)
            .FirstOrDefault(planet => !string.IsNullOrWhiteSpace(planet?.TacticalTexturePath));
        if (template == null)
            throw new InvalidOperationException(
                "Tactical development preview requires a planet texture."
            );

        return new Planet
        {
            DisplayName = template.DisplayName,
            TacticalTexturePath = template.TacticalTexturePath,
        };
    }

    /// <summary>
    /// Creates one faction fleet and stations one of each faction fighter on the preview planet.
    /// </summary>
    /// <param name="gameData">The active game data.</param>
    /// <param name="planet">The preview planet that supplies deployed fighters.</param>
    /// <param name="factionId">The faction that owns the preview units.</param>
    /// <param name="displayName">The fleet's developer-facing name.</param>
    /// <returns>The populated preview fleet.</returns>
    private static Fleet CreateFleet(
        GameDataCatalog gameData,
        Planet planet,
        string factionId,
        string displayName
    )
    {
        List<CapitalShip> ships = gameData
            .CapitalShips.Where(template => template?.HasAllowedOwnerInstanceID(factionId) == true)
            .Select(template => CreateCapitalShip(template, factionId))
            .ToList();
        if (ships.Count == 0)
            throw new InvalidOperationException($"Faction '{factionId}' has no capital ships.");

        foreach (
            Starfighter template in gameData.Starfighters.Where(template =>
                template?.HasAllowedOwnerInstanceID(factionId) == true
            )
        )
        {
            Starfighter fighter = template.GetDeepCopy();
            fighter.SetOwnerInstanceID(factionId);
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            fighter.Movement = null;
            fighter.CurrentSquadronSize = Math.Max(1, fighter.MaxSquadronSize);
            fighter.SetParent(planet);
            planet.Starfighters.Add(fighter);
        }

        return new Fleet(factionId, displayName, ships);
    }

    /// <summary>
    /// Creates one operational capital ship from a content template.
    /// </summary>
    /// <param name="template">The configured capital-ship template.</param>
    /// <param name="factionId">The owning faction.</param>
    /// <returns>The operational preview ship.</returns>
    private static CapitalShip CreateCapitalShip(CapitalShip template, string factionId)
    {
        CapitalShip ship = template.GetDeepCopy();
        ship.SetOwnerInstanceID(factionId);
        ship.ManufacturingStatus = ManufacturingStatus.Complete;
        ship.Movement = null;
        ship.CurrentHullStrength = Math.Max(1, ship.MaxHullStrength);
        ship.Starfighters.Clear();
        return ship;
    }
}

/// <summary>
/// Carries the generated development encounter and its locally controlled faction.
/// </summary>
internal sealed class TacticalBattleDevelopmentEncounterResult
{
    /// <summary>Gets the generated tactical encounter.</summary>
    internal PendingCombatResult Encounter { get; }

    /// <summary>Gets the faction controlled by the developer.</summary>
    internal string PlayerFactionInstanceID { get; }

    /// <summary>
    /// Creates one complete development-preview result.
    /// </summary>
    /// <param name="encounter">The generated encounter.</param>
    /// <param name="playerFactionInstanceId">The locally controlled faction.</param>
    internal TacticalBattleDevelopmentEncounterResult(
        PendingCombatResult encounter,
        string playerFactionInstanceId
    )
    {
        Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        PlayerFactionInstanceID =
            playerFactionInstanceId
            ?? throw new ArgumentNullException(nameof(playerFactionInstanceId));
    }
}
