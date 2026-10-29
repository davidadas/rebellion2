using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Carries the standalone tactical-scene battle created from the active content defaults.
/// </summary>
public static class TacticalBattleLaunchContext
{
    private const int _capitalShipCopiesPerType = 2;
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
            FlightRandomSeed = Environment.TickCount,
        };
        for (int factionIndex = 0; factionIndex < factionInstanceIDs.Length; factionIndex++)
        {
            string factionInstanceID = factionInstanceIDs[factionIndex];
            BaseSceneNode[] ships = CreateFactionForce(gameData, factionInstanceID);
            if (ships.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Playable faction '{factionInstanceID}' does not define any ships with models."
                );
            }
            foreach (BaseSceneNode ship in ships)
                battle.AddCombatants(ship);
            BattleMapDeploymentRegion deploymentRegion = deploymentRegions[factionIndex];
            BattleParticipant participant = battle.GetParticipants()[factionIndex];
            participant.BattleMapSlotID = deploymentRegion.ParticipantSlotID;
            if (factionIndex > 0)
                DeployCarrierWing(participant);
        }

        return battle;
    }

    /// <summary>
    /// Creates one heavy standalone force from the modeled content available to a playable faction.
    /// </summary>
    /// <param name="gameData">The active content catalog.</param>
    /// <param name="factionInstanceID">The faction receiving the detached units.</param>
    /// <returns>Every capital ship and fighter squadron to add to the preview battle.</returns>
    private static BaseSceneNode[] CreateFactionForce(
        GameDataCatalog gameData,
        string factionInstanceID
    )
    {
        CapitalShip[] capitalTemplates = gameData
            .CapitalShips.Where(ship =>
                !string.IsNullOrWhiteSpace(ship.ModelPath)
                && !ship.CanDestroyPlanets
                && ship.ManufacturingFactionInstanceIDs?.Contains(factionInstanceID) == true
            )
            .ToArray();
        Starfighter[] fighterTemplates = gameData
            .Starfighters.Where(fighter =>
                !string.IsNullOrWhiteSpace(fighter.ModelPath)
                && fighter.ManufacturingFactionInstanceIDs?.Contains(factionInstanceID) == true
            )
            .ToArray();
        List<BaseSceneNode> ships = new List<BaseSceneNode>();
        List<CapitalShip> capitalShips = new List<CapitalShip>();
        List<CapitalShip> carriers = new List<CapitalShip>();
        for (int copy = 0; copy < _capitalShipCopiesPerType; copy++)
            foreach (CapitalShip template in capitalTemplates)
            {
                CapitalShip ship = (CapitalShip)CreateUnitCopy(template, factionInstanceID);
                ships.Add(ship);
                capitalShips.Add(ship);
                if (
                    ship.GetStarfighterCapacity() > 0
                    && ship.GetComponents()
                        .OfType<StarfighterBay>()
                        .Any(bay => bay.FlightPath.Count > 0)
                )
                    carriers.Add(ship);
            }

        AttachOfficers(gameData.Officers, factionInstanceID, capitalShips);

        foreach (Starfighter template in fighterTemplates.Where(fighter => fighter.Hyperdrive <= 0))
        {
            Starfighter fighter = (Starfighter)CreateUnitCopy(template, factionInstanceID);
            CapitalShip carrier = carriers.FirstOrDefault(candidate =>
                candidate.CanAcceptChild(fighter)
            );
            if (carrier == null)
                throw new InvalidOperationException(
                    $"Default fighter '{fighter.TypeID}' requires an available carrier with an authored bay route."
                );
            AttachFighter(carrier, fighter);
            ships.Add(fighter);
        }

        int fighterIndex = 0;
        if (fighterTemplates.Length > 0)
            foreach (CapitalShip carrier in carriers)
                while (carrier.GetExcessStarfighterCapacity() > 0)
                {
                    Starfighter fighter = (Starfighter)CreateUnitCopy(
                        fighterTemplates[fighterIndex % fighterTemplates.Length],
                        factionInstanceID
                    );
                    fighterIndex++;
                    AttachFighter(carrier, fighter);
                    ships.Add(fighter);
                }

        foreach (Starfighter template in fighterTemplates.Where(fighter => fighter.Hyperdrive > 0))
            ships.Add(CreateUnitCopy(template, factionInstanceID));
        return ships.ToArray();
    }

    /// <summary>
    /// Creates a completed detached unit with a fresh identity for the standalone battle.
    /// </summary>
    /// <param name="template">The content template to copy.</param>
    /// <param name="factionInstanceID">The copied unit's owner.</param>
    /// <returns>The completed detached unit.</returns>
    private static BaseSceneNode CreateUnitCopy(BaseSceneNode template, string factionInstanceID)
    {
        BaseSceneNode ship = (BaseSceneNode)template.CreateCopy();
        ship.InstanceID = null;
        ship.OwnerInstanceID = factionInstanceID;
        ((IManufacturable)ship).SetManufacturingStatus(ManufacturingStatus.Complete);
        return ship;
    }

    /// <summary>
    /// Assigns one fighter squadron to a carrier before both enter the tactical battle.
    /// </summary>
    /// <param name="carrier">The carrier receiving the squadron.</param>
    /// <param name="fighter">The detached squadron to dock.</param>
    private static void AttachFighter(CapitalShip carrier, Starfighter fighter)
    {
        carrier.AddChild(fighter);
        fighter.SetParent(carrier);
    }

    /// <summary>
    /// Places three authored officers on one standalone capital ship so the preview exercises a
    /// multi-officer marker stack.
    /// </summary>
    /// <param name="templates">The active content catalog's officer templates.</param>
    /// <param name="factionInstanceID">The faction receiving its officers.</param>
    /// <param name="capitalShips">The detached preview ships that can carry them.</param>
    private static void AttachOfficers(
        IEnumerable<Officer> templates,
        string factionInstanceID,
        IReadOnlyList<CapitalShip> capitalShips
    )
    {
        if (capitalShips.Count == 0)
            return;
        foreach (
            Officer template in templates
                .Where(officer =>
                    string.Equals(
                        officer.OwnerInstanceID,
                        factionInstanceID,
                        StringComparison.Ordinal
                    )
                )
                .Take(3)
        )
        {
            Officer officer = (Officer)template.CreateCopy();
            CapitalShip ship = capitalShips[0];
            ship.AddChild(officer);
            officer.SetParent(ship);
        }
    }

    /// <summary>
    /// Starts an opposing standalone carrier wing in open space because that side has no player
    /// available to issue its launch command in the preview battle.
    /// </summary>
    /// <param name="participant">The opposing preview participant.</param>
    private static void DeployCarrierWing(BattleParticipant participant)
    {
        foreach (SpaceCombatUnit combatant in participant.GetCombatants())
            if (
                combatant is { Kind: SpaceCombatUnitKind.Starfighter }
                && !string.IsNullOrWhiteSpace(combatant.Flight.CarrierSourceUnitInstanceID)
            )
                combatant.Flight.Phase = FighterFlightState.Deployed;
    }
}
