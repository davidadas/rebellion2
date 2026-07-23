using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Projects one faction's surviving and destroyed combat units into result-table rows.
/// </summary>
internal sealed class BattleResultTableProjector
{
    private Dictionary<string, CapitalShip> capitalShipDefinitionsByTypeId;
    private Dictionary<string, Starfighter> starfighterDefinitionsByTypeId;

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
            || building.DefenseFacilityClass != DefenseFacilityClass.None;
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
            CapitalShip definition = GetCapitalShipDefinition(capitalShip);
            Texture2D resultTexture = GetTexture(
                uiContext,
                BattleResultPresentation.FirstNonBlank(
                    definition?.BattleResultImagePath,
                    capitalShip.BattleResultImagePath
                )
            );
            if (resultTexture != null)
                return resultTexture;
        }

        if (node is Starfighter starfighter)
        {
            Starfighter definition = GetStarfighterDefinition(starfighter);
            Texture2D resultTexture = GetTexture(
                uiContext,
                BattleResultPresentation.FirstNonBlank(
                    definition?.BattleResultImagePath,
                    starfighter.BattleResultImagePath
                )
            );
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
            CapitalShip definition = GetCapitalShipDefinition(capitalShip);
            return GetTexture(
                uiContext,
                BattleResultPresentation.FirstNonBlank(
                    definition?.BattleResultInTransitImagePath,
                    capitalShip.BattleResultInTransitImagePath,
                    definition?.InTransitImagePath,
                    capitalShip.InTransitImagePath,
                    definition?.InTransitSmallImagePath,
                    capitalShip.InTransitSmallImagePath
                )
            );
        }

        if (node is Starfighter starfighter)
        {
            Starfighter definition = GetStarfighterDefinition(starfighter);
            return GetTexture(
                uiContext,
                BattleResultPresentation.FirstNonBlank(
                    definition?.BattleResultInTransitImagePath,
                    starfighter.BattleResultInTransitImagePath,
                    definition?.InTransitImagePath,
                    starfighter.InTransitImagePath,
                    definition?.InTransitSmallImagePath,
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
            CapitalShip definition = GetCapitalShipDefinition(capitalShip);
            return GetTexture(
                uiContext,
                BattleResultPresentation.FirstNonBlank(
                    definition?.BattleResultDamagedImagePath,
                    capitalShip.BattleResultDamagedImagePath,
                    definition?.DamagedImagePath,
                    capitalShip.DamagedImagePath,
                    definition?.DamagedSmallImagePath,
                    capitalShip.DamagedSmallImagePath
                )
            );
        }

        if (node is Starfighter starfighter)
        {
            Starfighter definition = GetStarfighterDefinition(starfighter);
            return GetTexture(
                uiContext,
                BattleResultPresentation.FirstNonBlank(
                    definition?.BattleResultDamagedImagePath,
                    starfighter.BattleResultDamagedImagePath,
                    definition?.DamagedImagePath,
                    starfighter.DamagedImagePath,
                    definition?.DamagedSmallImagePath,
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
    /// Resolves immutable capital-ship display data by type identifier.
    /// </summary>
    /// <param name="capitalShip">The runtime capital ship.</param>
    /// <returns>The matching data definition, or null when none exists.</returns>
    private CapitalShip GetCapitalShipDefinition(CapitalShip capitalShip)
    {
        string typeId = capitalShip?.GetTypeID();
        if (string.IsNullOrEmpty(typeId))
            return null;

        capitalShipDefinitionsByTypeId ??= ResourceManager
            .GetEntityData<CapitalShip>()
            .Where(definition => !string.IsNullOrEmpty(definition.GetTypeID()))
            .ToDictionary(definition => definition.GetTypeID());
        capitalShipDefinitionsByTypeId.TryGetValue(typeId, out CapitalShip definition);
        return definition;
    }

    /// <summary>
    /// Resolves immutable starfighter display data by type identifier.
    /// </summary>
    /// <param name="starfighter">The runtime starfighter.</param>
    /// <returns>The matching data definition, or null when none exists.</returns>
    private Starfighter GetStarfighterDefinition(Starfighter starfighter)
    {
        string typeId = starfighter?.GetTypeID();
        if (string.IsNullOrEmpty(typeId))
            return null;

        starfighterDefinitionsByTypeId ??= ResourceManager
            .GetEntityData<Starfighter>()
            .Where(definition => !string.IsNullOrEmpty(definition.GetTypeID()))
            .ToDictionary(definition => definition.GetTypeID());
        starfighterDefinitionsByTypeId.TryGetValue(typeId, out Starfighter definition);
        return definition;
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
