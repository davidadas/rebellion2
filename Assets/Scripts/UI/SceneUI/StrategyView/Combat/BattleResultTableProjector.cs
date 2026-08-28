using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Projects one faction's surviving and destroyed combat units into result-table rows.
/// </summary>
internal sealed class BattleResultTableProjector
{
    /// <summary>
    /// Creates result-table presentation for one owner and category.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="result">The completed combat result.</param>
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
    /// Creates result-table presentation for one side of completed space combat.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="result">The completed space-combat result.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="category">The selected result category.</param>
    /// <returns>The operational and destroyed result columns.</returns>
    internal BattleResultTableRenderData ProjectSpaceCombat(
        UIContext uiContext,
        SpaceCombatResult result,
        string ownerInstanceId,
        BattleResultCategory category
    )
    {
        CombatSide? side = BattleResultPresentation.GetSideForOwner(result, ownerInstanceId);
        if (!side.HasValue)
            return CreateEmptyTable();

        bool withdrawing =
            BattleResultPresentation.GetOutcome(result, side.Value)
            == SpaceCombatSideOutcome.Withdrawn;
        return ProjectUnits(
            uiContext,
            side == CombatSide.Attacker ? result.AttackingUnits : result.DefendingUnits,
            category,
            withdrawing
        );
    }

    /// <summary>
    /// Creates result-table presentation for one side of an orbital bombardment.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="result">The completed bombardment result.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="category">The selected result category.</param>
    /// <returns>The operational and destroyed result columns.</returns>
    internal BattleResultTableRenderData ProjectBombardment(
        UIContext uiContext,
        BombardmentResult result,
        string ownerInstanceId,
        BattleResultCategory category
    )
    {
        bool attacker = ownerInstanceId == result?.AttackerOwnerInstanceID;
        bool defender = ownerInstanceId == result?.DefenderOwnerInstanceID;
        if (!attacker && !defender)
            return CreateEmptyTable();

        return ProjectUnits(
            uiContext,
            attacker ? result.AttackingUnits : result.DefendingUnits,
            category,
            withdrawing: false
        );
    }

    /// <summary>
    /// Creates result-table presentation for one side of a planetary assault.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="result">The completed planetary-assault result.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="category">The selected result category.</param>
    /// <returns>The operational and destroyed result columns.</returns>
    internal BattleResultTableRenderData ProjectPlanetaryAssault(
        UIContext uiContext,
        PlanetaryAssaultResult result,
        string ownerInstanceId,
        BattleResultCategory category
    )
    {
        bool attacker = ownerInstanceId == result?.AttackerOwnerInstanceID;
        bool defender = ownerInstanceId == result?.DefenderOwnerInstanceID;
        if (!attacker && !defender)
            return CreateEmptyTable();

        return ProjectUnits(
            uiContext,
            attacker ? result.AttackingUnits : result.DefendingUnits,
            category,
            withdrawing: false
        );
    }

    /// <summary>
    /// Creates result-table presentation from a durable combat report.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="report">The saved completed encounter.</param>
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
        return ProjectReportUnits(
            uiContext,
            attacker ? report.AttackingUnits : report.DefendingUnits,
            category,
            withdrawing
        );
    }

    /// <summary>
    /// Creates category rows from detached combat-unit snapshots.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="units">The units captured by the completed combat result.</param>
    /// <param name="category">The selected result category.</param>
    /// <param name="withdrawing">Whether surviving units withdrew from combat.</param>
    /// <returns>The operational and destroyed result columns.</returns>
    private BattleResultTableRenderData ProjectUnits(
        UIContext uiContext,
        IEnumerable<CombatUnitSnapshot> units,
        BattleResultCategory category,
        bool withdrawing
    )
    {
        List<BattleResultItemRenderData> operational = new List<BattleResultItemRenderData>();
        List<BattleResultItemRenderData> destroyed = new List<BattleResultItemRenderData>();
        HashSet<string> addedOperational = new HashSet<string>();
        HashSet<string> addedDestroyed = new HashSet<string>();

        foreach (CombatUnitSnapshot unit in FilterCategory(units, category))
        {
            if (!unit.WasOperational && !unit.Destroyed)
                continue;

            BattleResultUnitState state = BattleResultUnitState.Operational;
            if (unit.Damaged)
                state |= BattleResultUnitState.Damaged;
            if (unit.Destroyed)
                state |= BattleResultUnitState.Destroyed;
            else if (withdrawing)
                state |= BattleResultUnitState.Withdrawing;

            AddItem(
                unit.Destroyed ? destroyed : operational,
                unit,
                state,
                unit.Destroyed ? addedDestroyed : addedOperational,
                uiContext
            );
        }

        AddEmptyRows(operational, destroyed);
        return new BattleResultTableRenderData(operational, destroyed);
    }

    /// <summary>
    /// Creates category rows from units captured in a durable combat report.
    /// </summary>
    private static BattleResultTableRenderData ProjectReportUnits(
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
    /// Returns whether a saved report-unit category belongs to the selected result tab.
    /// </summary>
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
    /// Filters captured units to one result-table category.
    /// </summary>
    /// <param name="units">The candidate unit snapshots.</param>
    /// <param name="category">The requested result category.</param>
    /// <returns>The matching unit snapshots.</returns>
    private static IEnumerable<CombatUnitSnapshot> FilterCategory(
        IEnumerable<CombatUnitSnapshot> units,
        BattleResultCategory category
    )
    {
        return (units ?? Enumerable.Empty<CombatUnitSnapshot>()).Where(unit =>
            unit?.Unit != null
            && (
                category switch
                {
                    BattleResultCategory.CapitalShips => unit.Unit is CapitalShip,
                    BattleResultCategory.Starfighters => unit.Unit is Starfighter,
                    BattleResultCategory.Manufacturing => unit.Unit is Building building
                        && IsManufacturingFacility(building),
                    BattleResultCategory.Defense => unit.Unit is Building building
                        && IsDefenseFacility(building),
                    BattleResultCategory.Troops => unit.Unit is Regiment,
                    BattleResultCategory.Personnel => unit.Unit is Officer or SpecialForces,
                    _ => false,
                }
            )
        );
    }

    /// <summary>
    /// Returns whether a building belongs to the manufacturing-facility result category.
    /// </summary>
    /// <param name="building">The building to classify.</param>
    /// <returns>True for shipyards, training facilities, and construction yards.</returns>
    private static bool IsManufacturingFacility(Building building)
    {
        return building.BuildingType
            is BuildingType.Shipyard
                or BuildingType.TrainingFacility
                or BuildingType.ConstructionFacility;
    }

    /// <summary>
    /// Returns whether a building belongs to the defensive-facility result category.
    /// </summary>
    /// <param name="building">The building to classify.</param>
    /// <returns>True for planetary shields and weapon facilities.</returns>
    private static bool IsDefenseFacility(Building building)
    {
        return building.BuildingType is BuildingType.Defense or BuildingType.Weapon
            || building.IsDefenseFacility();
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
    /// Adds one captured unit with its base and status-overlay textures.
    /// </summary>
    /// <param name="items">The destination result column.</param>
    /// <param name="unit">The captured unit to represent.</param>
    /// <param name="state">The unit's completed-result state.</param>
    /// <param name="addedInstanceIds">The duplicate-suppression identifiers.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    private void AddItem(
        List<BattleResultItemRenderData> items,
        CombatUnitSnapshot unit,
        BattleResultUnitState state,
        HashSet<string> addedInstanceIds,
        UIContext uiContext
    )
    {
        ISceneNode node = unit?.Unit;
        if (node == null)
            return;

        string instanceId = node.GetInstanceID();
        if (!string.IsNullOrEmpty(instanceId) && !addedInstanceIds.Add(instanceId))
            return;

        items.Add(
            new BattleResultItemRenderData(
                node.GetDisplayName(),
                GetBaseTexture(uiContext, node),
                GetWithdrawingOverlayTexture(uiContext, node, state),
                GetDamagedOverlayTexture(uiContext, node, state),
                unit.Captured ? GetTexture(uiContext, node.CapturedOverlayImagePath) : null
            )
        );
    }

    /// <summary>
    /// Returns the result-table base texture for a scene node.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="node">The scene node to represent.</param>
    /// <returns>The selected base texture.</returns>
    private Texture2D GetBaseTexture(UIContext uiContext, ISceneNode node)
    {
        if (node is CapitalShip capitalShip)
        {
            Texture2D resultTexture = GetTexture(uiContext, capitalShip.BattleResultImagePath);
            if (resultTexture != null)
                return resultTexture;
        }

        if (node is Starfighter starfighter)
        {
            Texture2D resultTexture = GetTexture(uiContext, starfighter.BattleResultImagePath);
            if (resultTexture != null)
                return resultTexture;
        }

        return uiContext?.GetEntityTexture(node, true)
            ?? GetTexture(uiContext, node?.SmallDisplayImagePath)
            ?? GetTexture(uiContext, node?.GetDisplayImagePath());
    }

    /// <summary>
    /// Returns the result-table withdrawal overlay for a scene node.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="node">The scene node to represent.</param>
    /// <param name="state">The unit's completed-result state.</param>
    /// <returns>The selected withdrawal overlay texture.</returns>
    private Texture2D GetWithdrawingOverlayTexture(
        UIContext uiContext,
        ISceneNode node,
        BattleResultUnitState state
    )
    {
        if ((state & BattleResultUnitState.Withdrawing) == 0)
            return null;

        if (node is CapitalShip capitalShip)
        {
            return GetTexture(
                uiContext,
                BattleResultPresentation.FirstNonBlank(
                    capitalShip.BattleResultInTransitImagePath,
                    capitalShip.InTransitImagePath,
                    capitalShip.InTransitSmallImagePath
                )
            );
        }

        if (node is Starfighter starfighter)
        {
            return GetTexture(
                uiContext,
                BattleResultPresentation.FirstNonBlank(
                    starfighter.BattleResultInTransitImagePath,
                    starfighter.InTransitImagePath,
                    starfighter.InTransitSmallImagePath
                )
            );
        }

        return GetTexture(
            uiContext,
            BattleResultPresentation.FirstNonBlank(
                node?.InTransitImagePath,
                node?.InTransitSmallImagePath
            )
        );
    }

    /// <summary>
    /// Returns the result-table damage overlay for a scene node.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="node">The scene node to represent.</param>
    /// <param name="state">The unit's completed-result state.</param>
    /// <returns>The selected damage overlay texture.</returns>
    private Texture2D GetDamagedOverlayTexture(
        UIContext uiContext,
        ISceneNode node,
        BattleResultUnitState state
    )
    {
        BattleResultUnitState damagedStates =
            BattleResultUnitState.Damaged | BattleResultUnitState.Destroyed;
        if ((state & damagedStates) == 0)
            return null;

        if (node is CapitalShip capitalShip)
        {
            return GetTexture(
                uiContext,
                BattleResultPresentation.FirstNonBlank(
                    capitalShip.BattleResultDamagedImagePath,
                    capitalShip.DamagedImagePath,
                    capitalShip.DamagedSmallImagePath
                )
            );
        }

        if (node is Starfighter starfighter)
        {
            return GetTexture(
                uiContext,
                BattleResultPresentation.FirstNonBlank(
                    starfighter.BattleResultDamagedImagePath,
                    starfighter.DamagedImagePath,
                    starfighter.DamagedSmallImagePath
                )
            );
        }

        return GetTexture(
            uiContext,
            BattleResultPresentation.FirstNonBlank(
                node?.DamagedImagePath,
                node?.DamagedSmallImagePath
            )
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

    /// <summary>
    /// Identifies composable status overlays for one result-table unit.
    /// </summary>
    [Flags]
    private enum BattleResultUnitState
    {
        Operational = 0,
        Damaged = 1,
        Destroyed = 2,
        Withdrawing = 4,
    }
}
