using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

/// <summary>
/// Identifies the report displayed by the advisor utility window.
/// </summary>
public enum AdvisorReportMode
{
    /// <summary>
    /// Summarizes completed stationary faction assets by type.
    /// </summary>
    GalaxyOverview,

    /// <summary>
    /// Reports configured game objectives in display order.
    /// </summary>
    Objectives,
}

/// <summary>
/// Contains one immutable advisor-report projection before asset resolution.
/// </summary>
public sealed class AdvisorReportRow
{
    /// <summary>
    /// Creates one advisor-report row projection.
    /// </summary>
    /// <param name="item">The optional entity represented by the row.</param>
    /// <param name="imagePath">The optional configured image path.</param>
    /// <param name="primaryText">The primary row text.</param>
    /// <param name="secondaryText">The secondary row text.</param>
    public AdvisorReportRow(
        ISceneNode item,
        string imagePath,
        string primaryText,
        string secondaryText
    )
    {
        Item = item;
        ImagePath = imagePath;
        PrimaryText = primaryText ?? string.Empty;
        SecondaryText = secondaryText ?? string.Empty;
    }

    public ISceneNode Item { get; }

    public string ImagePath { get; }

    public string PrimaryText { get; }

    public string SecondaryText { get; }
}

/// <summary>
/// Projects current faction and objective state into ordered advisor-report rows.
/// </summary>
public static class AdvisorReportBuilder
{
    /// <summary>
    /// Builds the rows for one advisor report mode.
    /// </summary>
    /// <param name="game">The active game.</param>
    /// <param name="faction">The player faction.</param>
    /// <param name="theme">The configured advisor-report theme.</param>
    /// <param name="mode">The requested report mode.</param>
    /// <returns>The ordered report rows.</returns>
    public static IReadOnlyList<AdvisorReportRow> Build(
        GameRoot game,
        Faction faction,
        AdvisorReportWindowTheme theme,
        AdvisorReportMode mode
    )
    {
        if (game == null || faction == null)
            return Array.Empty<AdvisorReportRow>();

        return mode switch
        {
            AdvisorReportMode.GalaxyOverview => BuildGalaxyOverview(faction),
            AdvisorReportMode.Objectives => BuildObjectives(game, theme),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    /// <summary>
    /// Groups completed stationary faction assets by type for the galaxy overview.
    /// </summary>
    /// <param name="faction">The faction whose assets should be summarized.</param>
    /// <returns>Overview rows ordered by unit category and type identifier.</returns>
    internal static IReadOnlyList<AdvisorReportRow> BuildGalaxyOverview(Faction faction)
    {
        if (faction == null)
            return Array.Empty<AdvisorReportRow>();

        return faction
            .GetAllOwnedManufacturables()
            .Where(IsIncludedInGalaxyOverview)
            .OfType<ISceneNode>()
            .GroupBy(item => item.GetTypeID(), StringComparer.Ordinal)
            .Select(group => CreateGalaxyOverviewRow(group.ToList()))
            .OrderBy(row => GetGalaxyOverviewOrder(row.Item))
            .ThenBy(row => row.Item?.GetTypeID(), StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Evaluates configured objectives in display order and applies victory-mode gates.
    /// </summary>
    /// <param name="game">The active game.</param>
    /// <param name="theme">The configured advisor-report theme.</param>
    /// <returns>The visible objective rows.</returns>
    internal static IReadOnlyList<AdvisorReportRow> BuildObjectives(
        GameRoot game,
        AdvisorReportWindowTheme theme
    )
    {
        List<AdvisorReportRow> rows = new List<AdvisorReportRow>();
        if (game == null || theme?.Objectives == null)
            return Array.Empty<AdvisorReportRow>();

        bool conquest = game.Summary?.VictoryCondition == GameVictoryCondition.Conquest;
        foreach (AdvisorObjectiveTheme objective in theme.Objectives)
        {
            if (objective == null || objective.ConquestOnly && !conquest)
                continue;

            bool condition = EvaluateObjective(game, objective);
            rows.Add(
                new AdvisorReportRow(
                    null,
                    objective.ImagePath,
                    condition ? objective.TrueText : objective.FalseText,
                    string.Empty
                )
            );
        }

        return rows.AsReadOnly();
    }

    /// <summary>
    /// Determines whether a manufacturable belongs in the overview totals.
    /// </summary>
    /// <param name="item">The candidate manufacturable.</param>
    /// <returns>True for completed stationary assets.</returns>
    private static bool IsIncludedInGalaxyOverview(IManufacturable item)
    {
        return item != null
            && item.GetManufacturingStatus() == ManufacturingStatus.Complete
            && item.GetTransitMovement() == null;
    }

    /// <summary>
    /// Creates an overview row from assets sharing one type identifier.
    /// </summary>
    /// <param name="items">The grouped assets.</param>
    /// <returns>The formatted overview row.</returns>
    private static AdvisorReportRow CreateGalaxyOverviewRow(List<ISceneNode> items)
    {
        ISceneNode representative = items.FirstOrDefault();
        int maintenance = items.OfType<IManufacturable>().Sum(item => item.GetMaintenanceCost());
        return new AdvisorReportRow(
            representative,
            null,
            items.Count.ToString("D3"),
            maintenance.ToString("D4")
        );
    }

    /// <summary>
    /// Gets the configured category order for an overview entity.
    /// </summary>
    /// <param name="item">The representative entity.</param>
    /// <returns>The category sort index.</returns>
    private static int GetGalaxyOverviewOrder(ISceneNode item)
    {
        return item switch
        {
            Regiment => 0,
            CapitalShip => 1,
            Starfighter => 2,
            Building => 3,
            SpecialForces => 4,
            _ => 5,
        };
    }

    /// <summary>
    /// Evaluates one configured objective condition against the active game.
    /// </summary>
    /// <param name="game">The active game.</param>
    /// <param name="objective">The configured objective.</param>
    /// <returns>True when the objective condition is satisfied.</returns>
    private static bool EvaluateObjective(GameRoot game, AdvisorObjectiveTheme objective)
    {
        return objective.Condition switch
        {
            AdvisorObjectiveCondition.PlanetOwnedByFaction => IsPlanetOwnedByFaction(
                game,
                objective.TargetInstanceID,
                objective.TargetFactionInstanceID
            ),
            AdvisorObjectiveCondition.HeadquartersOwnedByFaction => IsHeadquartersOwnedByFaction(
                game,
                objective.TargetFactionInstanceID
            ),
            AdvisorObjectiveCondition.OfficerCaptured => game.GetSceneNodeByInstanceID<Officer>(
                objective.TargetInstanceID
            )?.IsCaptured == true,
            _ => false,
        };
    }

    /// <summary>
    /// Determines whether a configured faction owns a configured planet.
    /// </summary>
    /// <param name="game">The active game.</param>
    /// <param name="planetInstanceId">The target planet identifier.</param>
    /// <param name="factionInstanceId">The expected owner identifier.</param>
    /// <returns>True when the planet exists and has the expected owner.</returns>
    private static bool IsPlanetOwnedByFaction(
        GameRoot game,
        string planetInstanceId,
        string factionInstanceId
    )
    {
        Planet planet = game.GetSceneNodeByInstanceID<Planet>(planetInstanceId);
        return planet != null
            && string.Equals(
                planet.GetOwnerInstanceID(),
                factionInstanceId,
                StringComparison.Ordinal
            );
    }

    /// <summary>
    /// Determines whether a faction still owns its configured headquarters planet.
    /// </summary>
    /// <param name="game">The active game.</param>
    /// <param name="factionInstanceId">The faction identifier.</param>
    /// <returns>True when the faction and its owned headquarters planet exist.</returns>
    private static bool IsHeadquartersOwnedByFaction(GameRoot game, string factionInstanceId)
    {
        Faction faction = game.GetFactions()
            .FirstOrDefault(item =>
                string.Equals(item.InstanceID, factionInstanceId, StringComparison.Ordinal)
            );
        return faction != null
            && IsPlanetOwnedByFaction(game, faction.HQInstanceID, faction.InstanceID);
    }
}
