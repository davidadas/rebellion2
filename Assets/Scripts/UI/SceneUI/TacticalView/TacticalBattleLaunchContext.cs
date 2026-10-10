using System;
using System.Linq;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;

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
    /// Creates a space battle containing one capital ship for every playable faction.
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
        for (int factionIndex = 0; factionIndex < factionInstanceIDs.Length; factionIndex++)
        {
            string factionInstanceID = factionInstanceIDs[factionIndex];
            CapitalShip template = gameData.CapitalShips.FirstOrDefault(ship =>
                ship.ManufacturingFactionInstanceIDs?.Contains(factionInstanceID) == true
                && !string.IsNullOrWhiteSpace(ship.ModelPath)
            );
            if (template == null)
            {
                throw new InvalidOperationException(
                    $"Playable faction '{factionInstanceID}' does not define a capital ship with a model."
                );
            }

            CapitalShip ship = (CapitalShip)template.CreateCopy();
            ship.InstanceID = null;
            ship.OwnerInstanceID = factionInstanceID;
            CombatUnit combatant = battle.AddCombatants(ship).Single();
            BattleMapDeploymentRegion deploymentRegion = deploymentRegions[factionIndex];
            BattleParticipant participant = battle.GetParticipants()[factionIndex];
            participant.BattleMapSlotID = deploymentRegion.ParticipantSlotID;
            combatant.Position = GetCenter(deploymentRegion.Bounds);
        }

        return battle;
    }

    /// <summary>
    /// Returns the center of a deployment region in battle coordinates.
    /// </summary>
    /// <param name="bounds">The deployment bounds.</param>
    /// <returns>The center position.</returns>
    private static BattleVector3 GetCenter(BattleMapBounds bounds)
    {
        if (bounds == null)
            throw new ArgumentNullException(nameof(bounds));

        return new BattleVector3
        {
            X = (bounds.MinimumX + bounds.MaximumX) / 2f,
            Y = (bounds.MinimumY + bounds.MaximumY) / 2f,
            Z = (bounds.MinimumZ + bounds.MaximumZ) / 2f,
        };
    }
}
