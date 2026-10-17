using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

/// <summary>
/// Identifies the report displayed by the advisor utility window.
/// </summary>
public enum AdvisorReportMode
{
    GalaxyOverview,

    Objectives,
}

/// <summary>
/// Contains one immutable advisor-report projection before asset resolution.
/// </summary>
public sealed class AdvisorReportRow
{
    public ISceneNode Item { get; }

    public string ImagePath { get; }

    public string PrimaryText { get; }

    public string SecondaryText { get; }

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
}

/// <summary>
/// Projects current faction and objective state into ordered advisor-report rows.
/// </summary>
public static class AdvisorReportBuilder
{
    /// <summary>
    /// Builds the rows for one advisor report mode.
    /// </summary>
    /// <param name="factionView">The faction-filtered galaxy view.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <param name="victoryQueries">The authorized objective-status queries.</param>
    /// <param name="theme">The configured advisor-report theme.</param>
    /// <param name="mode">The requested report mode.</param>
    /// <returns>The ordered report rows.</returns>
    public static IReadOnlyList<AdvisorReportRow> Build(
        GalaxyMap factionView,
        string playerFactionId,
        VictoryQueries victoryQueries,
        AdvisorReportWindowTheme theme,
        AdvisorReportMode mode
    )
    {
        if (factionView == null || string.IsNullOrEmpty(playerFactionId))
            return Array.Empty<AdvisorReportRow>();

        return mode switch
        {
            AdvisorReportMode.GalaxyOverview => BuildGalaxyOverview(factionView, playerFactionId),
            AdvisorReportMode.Objectives => BuildObjectives(victoryQueries, theme),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    /// <summary>
    /// Groups completed stationary faction assets by type for the galaxy overview.
    /// </summary>
    /// <param name="factionView">The faction-filtered galaxy view.</param>
    /// <param name="playerFactionId">The faction whose assets should be summarized.</param>
    /// <returns>Overview rows ordered by unit category and type identifier.</returns>
    internal static IReadOnlyList<AdvisorReportRow> BuildGalaxyOverview(
        GalaxyMap factionView,
        string playerFactionId
    )
    {
        if (factionView == null || string.IsNullOrEmpty(playerFactionId))
            return Array.Empty<AdvisorReportRow>();

        return factionView
            .GetChildren<ISceneNode>(recursive: true, includeDisabled: true)
            .Where(item => item.OwnerInstanceID == playerFactionId)
            .OfType<IManufacturable>()
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
    /// <param name="victoryQueries">The authorized objective-status queries.</param>
    /// <param name="theme">The configured advisor-report theme.</param>
    /// <returns>The visible objective rows.</returns>
    internal static IReadOnlyList<AdvisorReportRow> BuildObjectives(
        VictoryQueries victoryQueries,
        AdvisorReportWindowTheme theme
    )
    {
        List<AdvisorReportRow> rows = new List<AdvisorReportRow>();
        if (victoryQueries == null || theme?.Objectives == null)
            return Array.Empty<AdvisorReportRow>();

        bool conquest = victoryQueries.IsConquestGame();
        foreach (AdvisorObjectiveTheme objective in theme.Objectives)
        {
            if (objective == null || objective.ConquestOnly && !conquest)
                continue;

            bool condition = EvaluateObjective(victoryQueries, objective);
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
            && item is ISceneNode node
            && node.IsActive()
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
    /// <param name="victoryQueries">The authorized objective-status queries.</param>
    /// <param name="objective">The configured objective.</param>
    /// <returns>True when the objective condition is satisfied.</returns>
    private static bool EvaluateObjective(
        VictoryQueries victoryQueries,
        AdvisorObjectiveTheme objective
    )
    {
        return objective.Condition switch
        {
            AdvisorObjectiveCondition.PlanetOwnedByFaction => victoryQueries.IsPlanetOwnedByFaction(
                objective.TargetInstanceID,
                objective.TargetFactionInstanceID
            ),
            AdvisorObjectiveCondition.HeadquartersOwnedByFaction =>
                victoryQueries.IsHeadquartersOwnedByFaction(objective.TargetFactionInstanceID),
            AdvisorObjectiveCondition.OfficerCaptured => victoryQueries.IsOfficerCaptured(
                objective.TargetInstanceID
            ),
            _ => false,
        };
    }
}
