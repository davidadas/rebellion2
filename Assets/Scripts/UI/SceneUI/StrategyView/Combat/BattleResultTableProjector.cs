using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using UnityEngine;

/// <summary>
/// Projects one faction's detached combat report into result-table rows.
/// </summary>
internal sealed class BattleResultTableProjector
{
    /// <summary>
    /// Creates result-table presentation for one owner and category.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="result">The detached combat presentation.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="category">The selected result category.</param>
    /// <returns>The operational and destroyed result columns.</returns>
    internal BattleResultTableRenderData Project(
        UIContext uiContext,
        BattleResultPresentation result,
        string ownerInstanceId,
        BattleResultCategory category
    )
    {
        return result?.ProjectTable(this, uiContext, ownerInstanceId, category)
            ?? CreateEmptyTable();
    }

    /// <summary>
    /// Creates result-table presentation from a detached combat report.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="report">The completed encounter report.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="category">The selected result category.</param>
    /// <returns>The operational and destroyed result columns.</returns>
    internal BattleResultTableRenderData ProjectReport(
        UIContext uiContext,
        CombatReport report,
        string ownerInstanceId,
        BattleResultCategory category
    )
    {
        bool attacker = ownerInstanceId == report?.AttackerOwnerInstanceID;
        bool defender = ownerInstanceId == report?.DefenderOwnerInstanceID;
        if (!attacker && !defender)
            return CreateEmptyTable();

        bool withdrawing =
            report.CombatType == CombatReportType.SpaceBattle
            && (
                attacker
                    ? report.AttackerOutcome == SpaceCombatSideOutcome.Withdrawn
                    : report.DefenderOutcome == SpaceCombatSideOutcome.Withdrawn
            );
        return ProjectUnits(
            uiContext,
            attacker ? report.AttackingUnits : report.DefendingUnits,
            category,
            withdrawing
        );
    }

    /// <summary>
    /// Creates category rows from units captured in a detached combat report.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="units">The detached report units.</param>
    /// <param name="category">The selected category.</param>
    /// <param name="withdrawing">Whether surviving units withdrew.</param>
    /// <returns>The projected result columns.</returns>
    private static BattleResultTableRenderData ProjectUnits(
        UIContext uiContext,
        IEnumerable<CombatReportUnit> units,
        BattleResultCategory category,
        bool withdrawing
    )
    {
        List<BattleResultItemRenderData> operational = new List<BattleResultItemRenderData>();
        List<BattleResultItemRenderData> destroyed = new List<BattleResultItemRenderData>();
        HashSet<string> addedOperational = new HashSet<string>();
        HashSet<string> addedDestroyed = new HashSet<string>();

        foreach (
            CombatReportUnit unit in (units ?? Enumerable.Empty<CombatReportUnit>()).Where(unit =>
                unit != null && MatchesCategory(unit.Category, category)
            )
        )
        {
            if (!unit.WasOperational && !unit.Destroyed)
                continue;

            HashSet<string> added = unit.Destroyed ? addedDestroyed : addedOperational;
            if (!string.IsNullOrEmpty(unit.InstanceID) && !added.Add(unit.InstanceID))
                continue;

            List<BattleResultItemRenderData> destination = unit.Destroyed ? destroyed : operational;
            destination.Add(
                new BattleResultItemRenderData(
                    unit.DisplayName,
                    GetTexture(
                        uiContext,
                        BattleResultPresentation.FirstNonBlank(
                            unit.ResultImagePath,
                            unit.SmallDisplayImagePath,
                            unit.DisplayImagePath
                        )
                    ),
                    !unit.Destroyed && withdrawing
                        ? GetTexture(
                            uiContext,
                            BattleResultPresentation.FirstNonBlank(
                                unit.ResultInTransitImagePath,
                                unit.InTransitImagePath,
                                unit.InTransitSmallImagePath
                            )
                        )
                        : null,
                    unit.Damaged || unit.Destroyed
                        ? GetTexture(
                            uiContext,
                            BattleResultPresentation.FirstNonBlank(
                                unit.ResultDamagedImagePath,
                                unit.DamagedImagePath,
                                unit.DamagedSmallImagePath
                            )
                        )
                        : null,
                    unit.Captured ? GetTexture(uiContext, unit.CapturedOverlayImagePath) : null
                )
            );
        }

        AddEmptyRows(operational, destroyed);
        return new BattleResultTableRenderData(operational, destroyed);
    }

    /// <summary>
    /// Returns whether a report-unit category belongs to the selected result tab.
    /// </summary>
    /// <param name="unitCategory">The report-unit category.</param>
    /// <param name="category">The selected result category.</param>
    /// <returns>True when the categories match.</returns>
    private static bool MatchesCategory(
        CombatReportUnitCategory unitCategory,
        BattleResultCategory category
    )
    {
        return (unitCategory, category) switch
        {
            (CombatReportUnitCategory.CapitalShip, BattleResultCategory.CapitalShips) => true,
            (CombatReportUnitCategory.Starfighter, BattleResultCategory.Starfighters) => true,
            (CombatReportUnitCategory.ManufacturingFacility, BattleResultCategory.Manufacturing) =>
                true,
            (CombatReportUnitCategory.DefenseFacility, BattleResultCategory.Defense) => true,
            (CombatReportUnitCategory.Troops, BattleResultCategory.Troops) => true,
            (CombatReportUnitCategory.Personnel, BattleResultCategory.Personnel) => true,
            _ => false,
        };
    }

    /// <summary>
    /// Adds the standard empty-state row to any empty result column.
    /// </summary>
    /// <param name="operational">The operational result column.</param>
    /// <param name="destroyed">The destroyed result column.</param>
    private static void AddEmptyRows(
        List<BattleResultItemRenderData> operational,
        List<BattleResultItemRenderData> destroyed
    )
    {
        if (operational.Count == 0)
            operational.Add(new BattleResultItemRenderData("None", null));
        if (destroyed.Count == 0)
            destroyed.Add(new BattleResultItemRenderData("No Casualties", null));
    }

    /// <summary>
    /// Creates an empty result table using the established empty-state labels.
    /// </summary>
    /// <returns>The empty result table.</returns>
    private static BattleResultTableRenderData CreateEmptyTable()
    {
        return new BattleResultTableRenderData(
            new[] { new BattleResultItemRenderData("None", null) },
            new[] { new BattleResultItemRenderData("No Casualties", null) }
        );
    }

    /// <summary>
    /// Returns a texture from the current UI context.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="path">The configured texture path.</param>
    /// <returns>The loaded texture, or null when unavailable.</returns>
    private static Texture2D GetTexture(UIContext uiContext, string path)
    {
        return uiContext?.GetTexture(path);
    }
}
