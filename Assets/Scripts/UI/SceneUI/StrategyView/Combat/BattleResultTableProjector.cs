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
        SpaceCombatResult result,
        string ownerInstanceId,
        BattleResultCategory category
    )
    {
        Fleet fleet = BattleResultPresentation.GetFleetForOwner(result, ownerInstanceId);
        List<BattleResultItemRenderData> operational = new List<BattleResultItemRenderData>();
        List<BattleResultItemRenderData> destroyed = new List<BattleResultItemRenderData>();

        switch (category)
        {
            case BattleResultCategory.CapitalShips:
                AddCapitalShips(operational, destroyed, result, fleet, ownerInstanceId, uiContext);
                break;
            case BattleResultCategory.Starfighters:
                AddStarfighters(operational, destroyed, result, fleet, ownerInstanceId, uiContext);
                break;
            case BattleResultCategory.Troops:
                AddCurrentUnits(
                    operational,
                    GetRegiments(fleet),
                    GetOperationalState(result, ownerInstanceId, false),
                    uiContext
                );
                break;
            case BattleResultCategory.Personnel:
                AddCurrentUnits(
                    operational,
                    GetPersonnel(fleet),
                    GetOperationalState(result, ownerInstanceId, false),
                    uiContext
                );
                break;
        }

        if (operational.Count == 0)
            operational.Add(new BattleResultItemRenderData("None", null));
        if (destroyed.Count == 0)
            destroyed.Add(new BattleResultItemRenderData("No Casualties", null));

        return new BattleResultTableRenderData(operational, destroyed);
    }

    /// <summary>
    /// Adds surviving, damaged, and destroyed capital ships.
    /// </summary>
    /// <param name="operational">The operational destination column.</param>
    /// <param name="destroyed">The destroyed destination column.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="fleet">The represented result fleet.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    private void AddCapitalShips(
        List<BattleResultItemRenderData> operational,
        List<BattleResultItemRenderData> destroyed,
        SpaceCombatResult result,
        Fleet fleet,
        string ownerInstanceId,
        UIContext uiContext
    )
    {
        Dictionary<string, ShipDamageResult> damageByShipId = IndexShipDamage(
            result,
            ownerInstanceId
        );
        HashSet<string> addedOperational = new HashSet<string>();

        IEnumerable<CapitalShip> currentShips =
            fleet?.CapitalShips ?? Enumerable.Empty<CapitalShip>();
        foreach (CapitalShip ship in currentShips)
        {
            damageByShipId.TryGetValue(ship.GetInstanceID(), out ShipDamageResult damage);
            BattleResultUnitState state = GetOperationalState(
                result,
                ownerInstanceId,
                damage != null && damage.HullAfter < damage.HullBefore
            );
            AddItem(operational, ship, state, addedOperational, uiContext);
        }

        foreach (ShipDamageResult damage in damageByShipId.Values)
        {
            if (damage?.Ship == null)
                continue;

            if (damage.HullAfter <= 0)
            {
                AddItem(destroyed, damage.Ship, BattleResultUnitState.Destroyed, null, uiContext);
                continue;
            }

            AddItem(
                operational,
                damage.Ship,
                GetOperationalState(result, ownerInstanceId, true),
                addedOperational,
                uiContext
            );
        }
    }

    /// <summary>
    /// Adds surviving, damaged, and destroyed starfighters.
    /// </summary>
    /// <param name="operational">The operational destination column.</param>
    /// <param name="destroyed">The destroyed destination column.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="fleet">The represented result fleet.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    private void AddStarfighters(
        List<BattleResultItemRenderData> operational,
        List<BattleResultItemRenderData> destroyed,
        SpaceCombatResult result,
        Fleet fleet,
        string ownerInstanceId,
        UIContext uiContext
    )
    {
        Dictionary<string, FighterLossResult> lossByFighterId = IndexFighterLosses(
            result,
            ownerInstanceId
        );
        HashSet<string> addedOperational = new HashSet<string>();

        IEnumerable<Starfighter> currentFighters =
            fleet?.GetStarfighters() ?? Enumerable.Empty<Starfighter>();
        foreach (Starfighter fighter in currentFighters)
        {
            lossByFighterId.TryGetValue(fighter.GetInstanceID(), out FighterLossResult loss);
            BattleResultUnitState state = GetOperationalState(
                result,
                ownerInstanceId,
                loss != null && loss.SquadsAfter < loss.SquadsBefore
            );
            AddItem(operational, fighter, state, addedOperational, uiContext);
        }

        foreach (FighterLossResult loss in lossByFighterId.Values)
        {
            if (loss?.Fighter == null)
                continue;

            if (loss.SquadsAfter <= 0)
            {
                AddItem(destroyed, loss.Fighter, BattleResultUnitState.Destroyed, null, uiContext);
                continue;
            }

            AddItem(
                operational,
                loss.Fighter,
                GetOperationalState(result, ownerInstanceId, true),
                addedOperational,
                uiContext
            );
        }
    }

    /// <summary>
    /// Indexes capital-ship damage for the represented owner.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <returns>Damage records keyed by ship identifier.</returns>
    private static Dictionary<string, ShipDamageResult> IndexShipDamage(
        SpaceCombatResult result,
        string ownerInstanceId
    )
    {
        Dictionary<string, ShipDamageResult> damageByShipId =
            new Dictionary<string, ShipDamageResult>();
        if (result?.ShipDamage == null)
            return damageByShipId;

        foreach (ShipDamageResult damage in result.ShipDamage)
        {
            CapitalShip ship = damage?.Ship;
            string instanceId = ship?.GetInstanceID();
            if (
                ship == null
                || ship.GetOwnerInstanceID() != ownerInstanceId
                || string.IsNullOrEmpty(instanceId)
            )
                continue;

            damageByShipId[instanceId] = damage;
        }

        return damageByShipId;
    }

    /// <summary>
    /// Indexes starfighter losses for the represented owner.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <returns>Loss records keyed by starfighter identifier.</returns>
    private static Dictionary<string, FighterLossResult> IndexFighterLosses(
        SpaceCombatResult result,
        string ownerInstanceId
    )
    {
        Dictionary<string, FighterLossResult> lossByFighterId =
            new Dictionary<string, FighterLossResult>();
        if (result?.FighterLosses == null)
            return lossByFighterId;

        foreach (FighterLossResult loss in result.FighterLosses)
        {
            Starfighter fighter = loss?.Fighter;
            string instanceId = fighter?.GetInstanceID();
            if (
                fighter == null
                || fighter.GetOwnerInstanceID() != ownerInstanceId
                || string.IsNullOrEmpty(instanceId)
            )
                continue;

            lossByFighterId[instanceId] = loss;
        }

        return lossByFighterId;
    }

    /// <summary>
    /// Adds current scene nodes without duplicate instance identifiers.
    /// </summary>
    /// <param name="items">The destination result column.</param>
    /// <param name="nodes">The current scene nodes.</param>
    /// <param name="state">The presentation state applied to each node.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    private void AddCurrentUnits(
        List<BattleResultItemRenderData> items,
        IEnumerable<ISceneNode> nodes,
        BattleResultUnitState state,
        UIContext uiContext
    )
    {
        HashSet<string> addedInstanceIds = new HashSet<string>();
        foreach (ISceneNode node in nodes ?? Enumerable.Empty<ISceneNode>())
            AddItem(items, node, state, addedInstanceIds, uiContext);
    }

    /// <summary>
    /// Adds one scene node with its base and status-overlay textures.
    /// </summary>
    /// <param name="items">The destination result column.</param>
    /// <param name="node">The scene node to represent.</param>
    /// <param name="state">The unit's completed-result state.</param>
    /// <param name="addedInstanceIds">Optional duplicate-suppression identifiers.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    private void AddItem(
        List<BattleResultItemRenderData> items,
        ISceneNode node,
        BattleResultUnitState state,
        HashSet<string> addedInstanceIds,
        UIContext uiContext
    )
    {
        if (node == null)
            return;

        string instanceId = node.GetInstanceID();
        if (
            addedInstanceIds != null
            && !string.IsNullOrEmpty(instanceId)
            && !addedInstanceIds.Add(instanceId)
        )
            return;

        items.Add(
            new BattleResultItemRenderData(
                node.GetDisplayName(),
                GetBaseTexture(uiContext, node),
                GetWithdrawingOverlayTexture(uiContext, node, state),
                GetDamagedOverlayTexture(uiContext, node, state)
            )
        );
    }

    /// <summary>
    /// Returns the combined operational, damage, and withdrawal state for a surviving unit.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    /// <param name="ownerInstanceId">The unit owner identifier.</param>
    /// <param name="damaged">Whether the unit sustained damage.</param>
    /// <returns>The unit's result-table presentation state.</returns>
    private static BattleResultUnitState GetOperationalState(
        SpaceCombatResult result,
        string ownerInstanceId,
        bool damaged
    )
    {
        CombatSide? side = BattleResultPresentation.GetSideForOwner(result, ownerInstanceId);
        BattleResultUnitState state = damaged
            ? BattleResultUnitState.Damaged
            : BattleResultUnitState.Operational;
        if (
            side.HasValue
            && BattleResultPresentation.GetOutcome(result, side.Value)
                == SpaceCombatSideOutcome.Withdrawn
        )
            state |= BattleResultUnitState.Withdrawing;

        return state;
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
    /// Returns officers and special-forces units carried by a fleet.
    /// </summary>
    /// <param name="fleet">The fleet to inspect.</param>
    /// <returns>The fleet personnel in scene-graph order.</returns>
    private static IEnumerable<ISceneNode> GetPersonnel(Fleet fleet)
    {
        return fleet == null
            ? Enumerable.Empty<ISceneNode>()
            : fleet
                .GetOfficers()
                .Cast<ISceneNode>()
                .Concat(fleet.GetSpecialForces().Cast<ISceneNode>());
    }

    /// <summary>
    /// Returns regiments carried by a fleet.
    /// </summary>
    /// <param name="fleet">The fleet to inspect.</param>
    /// <returns>The fleet regiments in scene-graph order.</returns>
    private static IEnumerable<ISceneNode> GetRegiments(Fleet fleet)
    {
        return fleet == null
            ? Enumerable.Empty<ISceneNode>()
            : fleet.GetRegiments().Cast<ISceneNode>();
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
