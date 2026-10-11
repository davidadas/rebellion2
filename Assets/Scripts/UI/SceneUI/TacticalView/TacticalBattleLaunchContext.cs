using System;
using System.Linq;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Carries the standalone tactical-scene battle created from the active content defaults.
/// </summary>
public static class TacticalBattleLaunchContext
{
    private static ContentPack contentPack;
    private static ActiveBattle activeBattle;

    /// <summary>
    /// Gets the default battle used when the tactical scene is opened without an active game.
    /// </summary>
    public static ActiveBattle ActiveBattle =>
        activeBattle ??= CreateDefaultBattle(
            contentPack
                ?? throw new InvalidOperationException(
                    "The tactical battle launch context has not been initialized."
                )
        );

    /// <summary>
    /// Restores the tactical launch context to the active content defaults.
    /// </summary>
    /// <param name="activeContentPack">The active content pack.</param>
    public static void Reset(ContentPack activeContentPack)
    {
        contentPack =
            activeContentPack ?? throw new ArgumentNullException(nameof(activeContentPack));
        activeBattle = null;
    }

    /// <summary>
    /// Creates a space battle containing modeled conventional capital ships and fighter squadrons for each playable faction.
    /// </summary>
    /// <param name="activeContentPack">The active content pack.</param>
    /// <returns>The default tactical battle.</returns>
    private static ActiveBattle CreateDefaultBattle(ContentPack activeContentPack)
    {
        GameDataCatalog gameData = activeContentPack.GameData;
        BattleMap battleMap = gameData
            .DefaultSpaceBattleMapInstanceIDs.Select(instanceID =>
                gameData.TryGetBattleMap(instanceID, out BattleMap map) ? map : null
            )
            .FirstOrDefault(map => map?.Kind == BattleKind.Space);
        if (battleMap == null)
        {
            throw new InvalidOperationException(
                "The active content pack does not define a default space battle map."
            );
        }

        string[] factionInstanceIDs = activeContentPack.Scenario.PlayableFactionIDs.ToArray();
        BattleMapDeploymentRegion[] deploymentRegions = battleMap.GetDeploymentRegions().ToArray();
        if (deploymentRegions.Length < factionInstanceIDs.Length)
        {
            throw new InvalidOperationException(
                $"Default battle map '{battleMap.InstanceID}' does not have enough deployment regions for every playable faction."
            );
        }

        ActiveBattle battle = new ActiveBattle
        {
            BattleMapInstanceID = battleMap.InstanceID,
            Kind = BattleKind.Space,
        };
        BaseSceneNode[] templates = gameData
            .CapitalShips.Cast<BaseSceneNode>()
            .Concat(gameData.Starfighters)
            .Where(unit =>
                !string.IsNullOrWhiteSpace(unit.ModelPath)
                && unit is not CapitalShip { CanDestroyPlanets: true }
            )
            .ToArray();
        for (int factionIndex = 0; factionIndex < factionInstanceIDs.Length; factionIndex++)
        {
            string factionInstanceID = factionInstanceIDs[factionIndex];
            BaseSceneNode[] factionTemplates = templates
                .Where(unit =>
                    ((IManufacturable)unit).ManufacturingFactionInstanceIDs?.Contains(
                        factionInstanceID
                    ) == true
                )
                .ToArray();
            if (factionTemplates.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Playable faction '{factionInstanceID}' does not define any ships with models."
                );
            }

            foreach (BaseSceneNode template in factionTemplates)
            {
                BaseSceneNode ship = (BaseSceneNode)template.CreateCopy();
                ship.InstanceID = null;
                ship.OwnerInstanceID = factionInstanceID;
                battle.AddCombatants(ship);
            }
            BattleMapDeploymentRegion deploymentRegion = deploymentRegions[factionIndex];
            BattleParticipant participant = battle.GetParticipants()[factionIndex];
            participant.BattleMapSlotID = deploymentRegion.ParticipantSlotID;
        }

        return battle;
    }
}
