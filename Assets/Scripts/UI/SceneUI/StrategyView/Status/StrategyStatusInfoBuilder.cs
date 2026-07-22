using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Extensions;

/// <summary>
/// Projects game entities into status-window domain information.
/// </summary>
internal sealed class StrategyStatusInfoBuilder
{
    private readonly GameConfig.JediConfig jediConfig;
    private readonly IReadOnlyList<GalaxyMapSector> sectors;
    private readonly string playerFactionId;
    private readonly Func<string, ISceneNode> findVisibleNode;
    private readonly int currentTick;

    /// <summary>
    /// Creates a status projector from the current visible strategy state.
    /// </summary>
    /// <param name="sectors">The visible sectors in presentation order.</param>
    /// <param name="findVisibleNode">Resolves a node from the visible galaxy snapshot.</param>
    /// <param name="game">The active game.</param>
    public StrategyStatusInfoBuilder(
        GameRoot game,
        IReadOnlyList<GalaxyMapSector> sectors,
        Func<string, ISceneNode> findVisibleNode
    )
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        this.sectors = sectors ?? throw new ArgumentNullException(nameof(sectors));
        playerFactionId = game.GetPlayerFaction()?.InstanceID ?? string.Empty;
        currentTick = game.CurrentTick;
        this.findVisibleNode =
            findVisibleNode ?? throw new ArgumentNullException(nameof(findVisibleNode));
        jediConfig = game.Config?.Jedi;
    }

    /// <summary>
    /// Projects one status target into display-ready domain information.
    /// </summary>
    /// <param name="target">The status target to project.</param>
    /// <returns>The projected status information, or null for an unsupported target.</returns>
    public StrategyStatusInfo Build(StrategyStatusTarget target)
    {
        if (target == null)
            return null;

        if (target.ManufacturingType.HasValue)
            return BuildManufacturingManagerStatusInfo(target);

        if (target.Item is Planet planet)
            return BuildPlanetStatusInfo(target.Planet ?? FindGalaxyMapPlanet(planet), planet);

        if (target.Item is Building building)
            return BuildBuildingStatusInfo(target, building);

        if (target.Item is Starfighter starfighter)
            return BuildStarfighterStatusInfo(target, starfighter);

        if (target.Item is Regiment regiment)
            return BuildRegimentStatusInfo(target, regiment);

        if (target.Item is SpecialForces specialForces)
            return BuildSpecialForcesStatusInfo(target, specialForces);

        if (target.Item is Officer officer)
            return BuildOfficerStatusInfo(target, officer);

        if (target.Item is Fleet fleet)
            return BuildFleetStatusInfo(target, fleet);

        if (target.Item is CapitalShip capitalShip)
            return BuildCapitalShipStatusInfo(target, capitalShip);

        if (target.Item is Mission mission)
            return BuildMissionStatusInfo(target, mission);

        if (target.Planet != null)
            return BuildPlanetStatusInfo(target.Planet, target.Planet.Planet);

        return null;
    }

    /// <summary>
    /// Builds status information for one planet manufacturing category.
    /// </summary>
    /// <param name="target">The manufacturing status target.</param>
    /// <returns>The projected manufacturing status information.</returns>
    private StrategyStatusInfo BuildManufacturingManagerStatusInfo(StrategyStatusTarget target)
    {
        ManufacturingType type = target.ManufacturingType.Value;
        IReadOnlyList<IManufacturable> queue = GetQueue(target.Planet.Planet, type);
        int facilityCount = GetFacilityCountForManufacturingType(target.Planet.Planet, type);
        string status =
            facilityCount == 0 ? "No Facilities"
            : queue.Count > 0
                ? type == ManufacturingType.Troop ? "Training"
                    : "Building"
            : "Idle";

        StrategyStatusInfo info = new StrategyStatusInfo
        {
            OwnerFactionId = target.Planet.OwnerFactionId,
            Header = type switch
            {
                ManufacturingType.Ship => "Ship Construction",
                ManufacturingType.Troop => "Troops in Training",
                _ => "Facilities Under Construction",
            },
            Label = type switch
            {
                ManufacturingType.Ship => "Shipyards",
                ManufacturingType.Troop => "Training Facilities",
                _ => "Construction Yards",
            },
            CenterImage = false,
        };
        info.Images.Add(
            type switch
            {
                ManufacturingType.Ship => StatusWindowImage.Shipyard,
                ManufacturingType.Troop => StatusWindowImage.Training,
                _ => StatusWindowImage.Construction,
            }
        );
        info.Rows.Add(new StrategyStatusRow("Location:", target.Planet.Planet.GetDisplayName()));
        info.Rows.Add(new StrategyStatusRow("Status:", status));
        if (queue.Count > 0)
        {
            info.Rows.Add(new StrategyStatusRow("Items to Build:", queue.Count.ToString()));
            int? completionTicks = ManufacturingSystem.EstimateQueueCompletionTicks(
                target.Planet.Planet,
                type
            );
            if (completionTicks.HasValue)
            {
                info.Rows.Add(
                    new StrategyStatusRow(
                        "Estimated Day of Completion:",
                        ((long)currentTick + completionTicks.Value).ToString()
                    )
                );
            }
        }

        return info;
    }

    /// <summary>
    /// Builds status information for one planet.
    /// </summary>
    /// <param name="strategyPlanet">The strategy-map projection for the planet.</param>
    /// <param name="planet">The planet domain object.</param>
    /// <returns>The projected planet status information.</returns>
    private StrategyStatusInfo BuildPlanetStatusInfo(GalaxyMapPlanet strategyPlanet, Planet planet)
    {
        StrategyStatusInfo info = new StrategyStatusInfo
        {
            OwnerFactionId = strategyPlanet?.OwnerFactionId ?? planet.OwnerInstanceID,
            Header = "Planet Status",
            Label = planet.GetDisplayName(),
            CenterImage = true,
        };
        info.ImageItems.Add(planet);
        info.Rows.Add(
            new StrategyStatusRow(
                "Location:",
                planet.GetParentOfType<PlanetSystem>()?.GetDisplayName() ?? planet.GetDisplayName()
            )
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Status:",
                string.IsNullOrEmpty(info.OwnerFactionId) ? "Neutral" : "Active"
            )
        );
        info.Rows.Add(
            new StrategyStatusRow("Popular Support:", GetPlayerSupport(planet).ToString())
        );
        info.Rows.Add(new StrategyStatusRow("Energy:", planet.GetAvailableEnergy().ToString()));
        return info;
    }

    /// <summary>
    /// Gets the player's popular support value for one planet.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <returns>The player's popular support value.</returns>
    private int GetPlayerSupport(Planet planet)
    {
        return !string.IsNullOrEmpty(playerFactionId)
            ? planet.GetPopularSupport(playerFactionId)
            : 50;
    }

    /// <summary>
    /// Builds status information for one building.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    /// <param name="building">The selected building.</param>
    /// <returns>The projected building status information.</returns>
    private StrategyStatusInfo BuildBuildingStatusInfo(
        StrategyStatusTarget target,
        Building building
    )
    {
        bool defense = building.GetBuildingType() is BuildingType.Defense or BuildingType.Weapon;
        StrategyStatusInfo info = CreateItemStatusInfo(
            target,
            building,
            defense ? "Defense Facility Status" : "Manufacturing Status",
            building.GetDisplayName()
        );
        info.Rows.Add(new StrategyStatusRow("Location:", GetStatusLocationName(target, building)));
        AddEtaDestinationRow(info, building);
        info.Rows.Add(new StrategyStatusRow("Status:", GetManufacturingStatusText(building)));
        info.Rows.Add(
            new StrategyStatusRow("Maintenance Cost:", building.MaintenanceCost.ToString())
        );
        if (!defense)
        {
            info.Rows.Add(
                new StrategyStatusRow("Standard Processing Rate:", building.ProcessRate.ToString())
            );
            info.Rows.Add(
                new StrategyStatusRow("Bombardment Value:", building.Bombardment.ToString())
            );
        }
        else
        {
            info.Rows.Add(
                new StrategyStatusRow("Weapons Rating:", building.WeaponStrength.ToString())
            );
            info.Rows.Add(
                new StrategyStatusRow("Shield Strength:", building.ShieldStrength.ToString())
            );
            info.Rows.Add(
                new StrategyStatusRow("Bombardment Defense:", building.Bombardment.ToString())
            );
        }
        return info;
    }

    /// <summary>
    /// Builds status information for one starfighter squadron.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    /// <param name="starfighter">The selected starfighter squadron.</param>
    /// <returns>The projected starfighter status information.</returns>
    private StrategyStatusInfo BuildStarfighterStatusInfo(
        StrategyStatusTarget target,
        Starfighter starfighter
    )
    {
        StrategyStatusInfo info = CreateItemStatusInfo(
            target,
            starfighter,
            "Fighter Squadron Status",
            starfighter.GetDisplayName()
        );
        int laserRating = starfighter.LaserCannon * Math.Max(starfighter.CurrentSquadronSize, 0);
        int maxLaserRating = starfighter.LaserCannon * Math.Max(starfighter.MaxSquadronSize, 0);
        int ionRating = starfighter.IonCannon * Math.Max(starfighter.CurrentSquadronSize, 0);
        int maxIonRating = starfighter.IonCannon * Math.Max(starfighter.MaxSquadronSize, 0);
        int torpedoRating = starfighter.Torpedoes * Math.Max(starfighter.CurrentSquadronSize, 0);
        int maxTorpedoRating = starfighter.Torpedoes * Math.Max(starfighter.MaxSquadronSize, 0);

        info.Rows.Add(
            new StrategyStatusRow("Attached:", GetStatusLocationName(target, starfighter))
        );
        AddEtaDestinationRow(info, starfighter);
        info.Rows.Add(
            new StrategyStatusRow("Maintenance Cost:", starfighter.MaintenanceCost.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Squadron Size:",
                FormatStatusRatio(starfighter.CurrentSquadronSize, starfighter.MaxSquadronSize)
            )
        );
        info.Rows.Add(
            new StrategyStatusRow("Hyperdrive Rating:", starfighter.Hyperdrive.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Max Shield Strength:", starfighter.ShieldStrength.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Sublight Engine Rate:", starfighter.SublightSpeed.ToString())
        );
        info.Rows.Add(new StrategyStatusRow("Maneuverability:", starfighter.Agility.ToString()));
        info.Rows.Add(
            new StrategyStatusRow("Detection Rating:", starfighter.DetectionRating.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Bombardment Value:", starfighter.Bombardment.ToString())
        );
        info.Rows.Add(new StrategyStatusRow("Weapons Rating:", " "));
        info.Rows.Add(
            new StrategyStatusRow("Laser Rating:", FormatStatusRatio(laserRating, maxLaserRating))
        );
        info.Rows.Add(
            new StrategyStatusRow("Ion Cannon:", FormatStatusRatio(ionRating, maxIonRating))
        );
        info.Rows.Add(
            new StrategyStatusRow("Torpedoes:", FormatStatusRatio(torpedoRating, maxTorpedoRating))
        );
        return info;
    }

    /// <summary>
    /// Builds status information for one regiment.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    /// <param name="regiment">The selected regiment.</param>
    /// <returns>The projected regiment status information.</returns>
    private StrategyStatusInfo BuildRegimentStatusInfo(
        StrategyStatusTarget target,
        Regiment regiment
    )
    {
        StrategyStatusInfo info = CreateItemStatusInfo(
            target,
            regiment,
            "Trooper Regiment Status",
            regiment.GetDisplayName()
        );
        info.Rows.Add(new StrategyStatusRow("Attached:", GetStatusLocationName(target, regiment)));
        info.Rows.Add(new StrategyStatusRow("Status:", GetManufacturingStatusText(regiment)));
        AddEtaDestinationRow(info, regiment);
        info.Rows.Add(
            new StrategyStatusRow("Maintenance Cost:", regiment.MaintenanceCost.ToString())
        );
        info.Rows.Add(new StrategyStatusRow("Attack Strength:", regiment.AttackRating.ToString()));
        info.Rows.Add(
            new StrategyStatusRow("Defense Strength:", regiment.DefenseRating.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Bombardment Value:", regiment.BombardmentDefense.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Detection Value:", regiment.DetectionRating.ToString())
        );
        return info;
    }

    /// <summary>
    /// Builds status information for one special-forces unit.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    /// <param name="specialForces">The selected special-forces unit.</param>
    /// <returns>The projected special-forces status information.</returns>
    private StrategyStatusInfo BuildSpecialForcesStatusInfo(
        StrategyStatusTarget target,
        SpecialForces specialForces
    )
    {
        StrategyStatusInfo info = CreateItemStatusInfo(
            target,
            specialForces,
            "Trooper Regiment Status",
            specialForces.GetDisplayName()
        );
        info.Rows.Add(
            new StrategyStatusRow("Attached:", GetStatusLocationName(target, specialForces))
        );
        info.Rows.Add(new StrategyStatusRow("Status:", GetManufacturingStatusText(specialForces)));
        AddEtaDestinationRow(info, specialForces);
        info.Rows.Add(
            new StrategyStatusRow("Maintenance Cost:", specialForces.MaintenanceCost.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Diplomacy Rating:",
                GetRatingText(specialForces, OfficerRating.Diplomacy)
            )
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Espionage Rating:",
                GetRatingText(specialForces, OfficerRating.Espionage)
            )
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Combat Rating:",
                GetRatingText(specialForces, OfficerRating.Combat)
            )
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Leadership Rating:",
                GetRatingText(specialForces, OfficerRating.Leadership)
            )
        );
        return info;
    }

    /// <summary>
    /// Builds status information for one officer.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    /// <param name="officer">The selected officer.</param>
    /// <returns>The projected officer status information.</returns>
    private StrategyStatusInfo BuildOfficerStatusInfo(StrategyStatusTarget target, Officer officer)
    {
        StrategyStatusInfo info = CreateItemStatusInfo(
            target,
            officer,
            "Character Status",
            officer.GetDisplayName()
        );
        info.Rows.Add(new StrategyStatusRow("Commanding:", GetOfficerCommandingText(officer)));
        info.Rows.Add(new StrategyStatusRow("Attached:", GetStatusLocationName(target, officer)));
        info.Rows.Add(new StrategyStatusRow("Status:", GetOfficerStatusText(officer)));
        AddEtaDestinationRow(info, officer);
        info.Rows.Add(new StrategyStatusRow("Force Ranking:", GetForceRankText(officer)));
        info.Rows.Add(
            new StrategyStatusRow(
                "Diplomacy Rating:",
                GetRatingText(officer, OfficerRating.Diplomacy)
            )
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Espionage Rating:",
                GetRatingText(officer, OfficerRating.Espionage)
            )
        );
        info.Rows.Add(
            new StrategyStatusRow("Combat Rating:", GetRatingText(officer, OfficerRating.Combat))
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Leadership Rating:",
                GetRatingText(officer, OfficerRating.Leadership)
            )
        );
        info.Rows.Add(new StrategyStatusRow("Research Capabilities:", " "));
        info.Rows.Add(
            new StrategyStatusRow("Ship Design:", officer.ShipResearch > 0 ? "Yes" : "No")
        );
        info.Rows.Add(
            new StrategyStatusRow("Troop Training:", officer.TroopResearch > 0 ? "Yes" : "No")
        );
        info.Rows.Add(
            new StrategyStatusRow("Facility Design:", officer.FacilityResearch > 0 ? "Yes" : "No")
        );
        info.Rows.Add(new StrategyStatusRow("Possible Ranks:", " "));
        info.Rows.Add(
            new StrategyStatusRow(
                "Admiral:",
                officer.AllowedRanks?.Contains(OfficerRank.Admiral) == true ? "Yes" : "No"
            )
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "General:",
                officer.AllowedRanks?.Contains(OfficerRank.General) == true ? "Yes" : "No"
            )
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Commander:",
                officer.AllowedRanks?.Contains(OfficerRank.Commander) == true ? "Yes" : "No"
            )
        );
        return info;
    }

    /// <summary>
    /// Builds status information for one fleet.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    /// <param name="fleet">The selected fleet.</param>
    /// <returns>The projected fleet status information.</returns>
    private StrategyStatusInfo BuildFleetStatusInfo(StrategyStatusTarget target, Fleet fleet)
    {
        StrategyStatusInfo info = CreateItemStatusInfo(
            target,
            fleet,
            "Fleet Status",
            fleet.GetDisplayName()
        );
        info.Images.Clear();
        if (fleet.Movement != null)
            info.Images.Add(StatusWindowImage.FleetBannerEnroute);
        if (fleet.CapitalShips.Any(ship => ship.IsDamaged()))
            info.Images.Add(StatusWindowImage.FleetBannerDamaged);
        info.Images.Add(StatusWindowImage.FleetBanner);
        info.Rows.Add(
            new StrategyStatusRow("Status:", fleet.Movement != null ? "Enroute" : "Awaiting Orders")
        );
        AddEtaDestinationRow(info, fleet);
        info.Rows.Add(
            new StrategyStatusRow("Admiral:", FindFleetOfficerByRank(fleet, OfficerRank.Admiral))
        );
        info.Rows.Add(
            new StrategyStatusRow("General:", FindFleetOfficerByRank(fleet, OfficerRank.General))
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Commander:",
                FindFleetOfficerByRank(fleet, OfficerRank.Commander)
            )
        );
        info.Rows.Add(
            new StrategyStatusRow("Number of Ships:", fleet.CapitalShips.Count.ToString())
        );
        info.Rows.Add(new StrategyStatusRow("Capacity:", " "));
        info.Rows.Add(
            new StrategyStatusRow("Fighter Squadrons:", fleet.GetStarfighterCapacity().ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Trooper Regiments:", fleet.GetRegimentCapacity().ToString())
        );
        info.Rows.Add(new StrategyStatusRow("Embarked:", " "));
        info.Rows.Add(
            new StrategyStatusRow(
                "Fighter Squadrons:",
                fleet.GetCurrentStarfighterCount().ToString()
            )
        );
        info.Rows.Add(
            new StrategyStatusRow("Trooper Regiments:", fleet.GetCurrentRegimentCount().ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Personnel:",
                (fleet.GetOfficers().Count() + fleet.GetSpecialForces().Count()).ToString()
            )
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Damaged Ships:",
                fleet.CapitalShips.Count(ship => ship.IsDamaged()).ToString()
            )
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Hyperdrive Rating:",
                fleet.CapitalShips.Any(ship => ship.Hyperdrive > 0) ? "Yes" : "No"
            )
        );
        return info;
    }

    /// <summary>
    /// Builds status information for one capital ship.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    /// <param name="capitalShip">The selected capital ship.</param>
    /// <returns>The projected capital-ship status information.</returns>
    private StrategyStatusInfo BuildCapitalShipStatusInfo(
        StrategyStatusTarget target,
        CapitalShip capitalShip
    )
    {
        StrategyStatusInfo info = CreateItemStatusInfo(
            target,
            capitalShip,
            "Capital Ship Status",
            capitalShip.GetDisplayName()
        );
        Fleet fleet = capitalShip.GetParentOfType<Fleet>();
        info.Rows.Add(new StrategyStatusRow("Class:", capitalShip.GetDisplayName()));
        info.Rows.Add(new StrategyStatusRow("Fleet:", fleet?.GetDisplayName() ?? "None"));
        info.Rows.Add(new StrategyStatusRow("Status:", GetManufacturingStatusText(capitalShip)));
        AddEtaDestinationRow(info, capitalShip);
        info.Rows.Add(
            new StrategyStatusRow("Maintenance Cost:", capitalShip.MaintenanceCost.ToString())
        );
        info.Rows.Add(new StrategyStatusRow("Capacity:", " "));
        info.Rows.Add(
            new StrategyStatusRow("Fighter Squadrons:", capitalShip.StarfighterCapacity.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Trooper Regiments:", capitalShip.RegimentCapacity.ToString())
        );
        info.Rows.Add(new StrategyStatusRow("Embarked:", " "));
        info.Rows.Add(
            new StrategyStatusRow("Fighter Squadrons:", capitalShip.Starfighters.Count.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Trooper Regiments:", capitalShip.Regiments.Count.ToString())
        );
        info.Rows.Add(new StrategyStatusRow("Personnel:", capitalShip.Officers.Count.ToString()));
        info.Rows.Add(
            new StrategyStatusRow("Ship Damaged:", capitalShip.IsDamaged() ? "Yes" : "No")
        );
        info.Rows.Add(
            new StrategyStatusRow("Ship Hyperdrive Rating:", capitalShip.Hyperdrive.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Hull Value:",
                FormatStatusRatio(capitalShip.CurrentHullStrength, capitalShip.MaxHullStrength)
            )
        );
        info.Rows.Add(
            new StrategyStatusRow("Damage Control Rating:", capitalShip.DamageControl.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow(
                "Shield Recharge Rate:",
                capitalShip.ShieldRechargeRate.ToString()
            )
        );
        info.Rows.Add(
            new StrategyStatusRow("Max Shield Strength:", capitalShip.MaxShieldStrength.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Tractor Beam Power:", capitalShip.TractorBeamPower.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Sub Light Engine Rating:", capitalShip.SublightSpeed.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Maneuverability:", capitalShip.Maneuverability.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Detection Rating:", capitalShip.DetectionRating.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Weapons Recharge Rate:", capitalShip.WeaponRecharge.ToString())
        );
        info.Rows.Add(
            new StrategyStatusRow("Bombardment Modifier:", capitalShip.Bombardment.ToString())
        );
        return info;
    }

    /// <summary>
    /// Builds status information for one active mission.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    /// <param name="mission">The selected mission.</param>
    /// <returns>The projected mission status information.</returns>
    private StrategyStatusInfo BuildMissionStatusInfo(StrategyStatusTarget target, Mission mission)
    {
        StrategyStatusInfo info = CreateItemStatusInfo(
            target,
            mission,
            "Mission Status",
            mission.GetDisplayName()
        );
        ISceneNode missionTarget = ResolveMissionTarget(mission);
        int teamSize =
            (mission.MainParticipants?.Count ?? 0) + (mission.DecoyParticipants?.Count ?? 0);
        info.Rows.Add(
            new StrategyStatusRow("Target:", missionTarget?.GetDisplayName() ?? "Unknown")
        );
        AddMissionEtaDestinationRow(info, mission);
        info.Rows.Add(new StrategyStatusRow("Team Size:", teamSize.ToString()));
        info.Rows.Add(
            new StrategyStatusRow("Decoys:", (mission.DecoyParticipants?.Count ?? 0).ToString())
        );
        return info;
    }

    /// <summary>
    /// Creates the shared status information and image stack for one scene node.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    /// <param name="item">The selected scene node.</param>
    /// <param name="header">The status header text.</param>
    /// <param name="label">The item label text.</param>
    /// <returns>The initialized item status information.</returns>
    private StrategyStatusInfo CreateItemStatusInfo(
        StrategyStatusTarget target,
        ISceneNode item,
        string header,
        string label
    )
    {
        string ownerFactionId = item.OwnerInstanceID;
        if (string.IsNullOrEmpty(ownerFactionId))
            ownerFactionId = (target.Planet?.Planet ?? GetStatusPlanet(item))?.OwnerInstanceID;

        StrategyStatusInfo info = new StrategyStatusInfo
        {
            OwnerFactionId = ownerFactionId,
            Header = header,
            Label = label,
            CenterImage = true,
        };

        MovementState transitMovement = GetTransitMovement(item);
        if (HasEntityStatusImage(item, transitMovement))
            info.StatusImageItems.Add(item);
        else if (transitMovement != null && item is not Fleet)
            info.Images.Add(StatusWindowImage.Enroute);

        info.ImageItems.Add(item);
        if (
            item is Officer { IsCaptured: true }
            && !string.IsNullOrEmpty(item.CapturedOverlayImagePath)
        )
            info.OverlayImageItems.Add(item);
        return info;
    }

    /// <summary>
    /// Determines whether one entity supplies a state-specific status image.
    /// </summary>
    /// <param name="item">The scene node to inspect.</param>
    /// <param name="transitMovement">The movement state physically carrying the entity.</param>
    /// <returns><see langword="true"/> when a state-specific image is available.</returns>
    private static bool HasEntityStatusImage(ISceneNode item, MovementState transitMovement)
    {
        if (item == null)
            return false;

        if (item is Officer { InjuryPoints: > 0 } && !string.IsNullOrEmpty(item.InjuredImagePath))
            return true;

        if (transitMovement != null && !string.IsNullOrEmpty(item.InTransitImagePath))
            return true;

        if (item is CapitalShip capitalShip && capitalShip.IsDamaged())
            return !string.IsNullOrEmpty(item.DamagedImagePath);

        if (item is Starfighter starfighter && starfighter.HasLosses())
            return !string.IsNullOrEmpty(item.DamagedImagePath);

        return false;
    }

    /// <summary>
    /// Counts completed facilities that serve one manufacturing category.
    /// </summary>
    /// <param name="planet">The planet whose facilities are inspected.</param>
    /// <param name="type">The manufacturing category.</param>
    /// <returns>The number of matching completed facilities.</returns>
    private static int GetFacilityCountForManufacturingType(Planet planet, ManufacturingType type)
    {
        return planet.Buildings.Count(building =>
            building.GetManufacturingStatus() == ManufacturingStatus.Complete
            && building.GetProductionType() == type
        );
    }

    /// <summary>
    /// Finds or creates the strategy-map projection for one planet.
    /// </summary>
    /// <param name="planet">The planet to resolve.</param>
    /// <returns>The matching strategy-map planet projection.</returns>
    private GalaxyMapPlanet FindGalaxyMapPlanet(Planet planet)
    {
        foreach (GalaxyMapSector sector in sectors)
        {
            GalaxyMapPlanet match = sector.Planets.FirstOrDefault(p => p.Planet == planet);
            if (match != null)
                return match;
        }

        PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
        return new GalaxyMapPlanet(system, planet, planet.GetPlanetIconPath());
    }

    /// <summary>
    /// Resolves the planet that directly contains one status item.
    /// </summary>
    /// <param name="item">The status item.</param>
    /// <returns>The containing planet, or <see langword="null"/>.</returns>
    private static Planet GetStatusPlanet(ISceneNode item)
    {
        if (item is Planet planet)
            return planet;

        return item?.GetParentOfType<Planet>();
    }

    /// <summary>
    /// Resolves the displayed location name for one status item.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    /// <param name="item">The selected scene node.</param>
    /// <returns>The displayed location name.</returns>
    private static string GetStatusLocationName(StrategyStatusTarget target, ISceneNode item)
    {
        Planet planet = target.Planet?.Planet ?? GetStatusPlanet(item);
        if (planet != null)
            return planet.GetDisplayName();

        ISceneNode parent = item?.GetParent();
        return parent?.GetDisplayName() ?? "Unknown";
    }

    /// <summary>
    /// Resolves the displayed operational status for a manufacturable item.
    /// </summary>
    /// <param name="item">The manufacturable item.</param>
    /// <returns>The displayed status text.</returns>
    private static string GetManufacturingStatusText(IManufacturable item)
    {
        if (item.GetManufacturingStatus() == ManufacturingStatus.Building)
            return item is Regiment or SpecialForces ? "Training" : "Under Construction";

        if (item is IMovable movable && movable.GetTransitMovement() != null)
            return "Enroute";

        return item.GetManufacturingStatus() == ManufacturingStatus.Complete
            ? item is Regiment or SpecialForces
                ? "Awaiting Orders"
                : "Active"
            : "Idle";
    }

    /// <summary>
    /// Formats one effective mission-participant rating.
    /// </summary>
    /// <param name="participant">The mission participant.</param>
    /// <param name="rating">The requested rating category.</param>
    /// <returns>The displayed effective rating.</returns>
    private static string GetRatingText(IMissionParticipant participant, OfficerRating rating)
    {
        return participant.GetEffectiveRating(rating).ToString();
    }

    /// <summary>
    /// Resolves the displayed operational status for one officer.
    /// </summary>
    /// <param name="officer">The officer to inspect.</param>
    /// <returns>The displayed officer status.</returns>
    private static string GetOfficerStatusText(Officer officer)
    {
        if (officer.GetTransitMovement() != null)
            return "Enroute";
        if (officer.IsCaptured)
            return "Captured";
        if (officer.InjuryPoints > 0)
            return "Injured";
        if (officer.IsOnMission())
            return "On Mission";
        return "Awaiting Orders";
    }

    /// <summary>
    /// Appends the arrival day for a moving status entity.
    /// </summary>
    /// <param name="info">The status information receiving the ETA row.</param>
    /// <param name="item">The status entity whose effective movement should be displayed.</param>
    private void AddEtaDestinationRow(StrategyStatusInfo info, ISceneNode item)
    {
        AddEtaDestinationRow(info, GetTransitMovement(item));
    }

    /// <summary>
    /// Appends the arrival day represented by the first moving mission participant.
    /// </summary>
    /// <param name="info">The status information receiving the ETA row.</param>
    /// <param name="mission">The mission whose participants should be inspected.</param>
    private void AddMissionEtaDestinationRow(StrategyStatusInfo info, Mission mission)
    {
        IMissionParticipant participant = mission
            ?.GetAllParticipants()
            .FirstOrDefault(candidate => candidate?.Movement != null);
        AddEtaDestinationRow(info, participant?.Movement);
    }

    /// <summary>
    /// Appends an arrival-day row for an active movement state.
    /// </summary>
    /// <param name="info">The status information receiving the ETA row.</param>
    /// <param name="movement">The active movement state.</param>
    private void AddEtaDestinationRow(StrategyStatusInfo info, MovementState movement)
    {
        if (movement == null)
            return;

        long arrivalDay = (long)currentTick + Math.Max(movement.TicksRemaining(), 0);
        info.Rows.Add(new StrategyStatusRow("ETA Destination:", $"Day {arrivalDay}"));
    }

    /// <summary>
    /// Resolves the movement state that physically carries one status entity.
    /// </summary>
    /// <param name="item">The status entity to inspect.</param>
    /// <returns>The active movement state, or null when the entity is stationary.</returns>
    private static MovementState GetTransitMovement(ISceneNode item)
    {
        return item is IMovable movable ? movable.GetTransitMovement() : null;
    }

    /// <summary>
    /// Resolves the command assignment displayed for one officer.
    /// </summary>
    /// <param name="officer">The officer to inspect.</param>
    /// <returns>The displayed command assignment.</returns>
    private static string GetOfficerCommandingText(Officer officer)
    {
        if (officer.CurrentRank == OfficerRank.None)
            return "None";

        ISceneNode parent = officer.GetParent();
        if (parent is CapitalShip ship)
            return ship.GetParentOfType<Fleet>()?.GetDisplayName() ?? ship.GetDisplayName();

        return parent?.GetDisplayName() ?? "None";
    }

    /// <summary>
    /// Resolves the configured Force-rank label for one officer.
    /// </summary>
    /// <param name="officer">The officer to inspect.</param>
    /// <returns>The displayed Force-rank label.</returns>
    private string GetForceRankText(Officer officer)
    {
        if (jediConfig == null || !officer.IsJedi)
            return "None";

        if (officer.ForceRank >= jediConfig.RankLabelForceMaster)
            return "Jedi Master";
        if (officer.ForceRank >= jediConfig.RankLabelForceKnight)
            return "Jedi Knight";
        if (officer.ForceRank >= jediConfig.RankLabelForceStudent)
            return "Jedi Student";
        if (officer.ForceRank >= jediConfig.RankLabelTrainee)
            return "Trainee";
        if (officer.ForceRank >= jediConfig.RankLabelNovice)
            return "Novice";
        return "None";
    }

    /// <summary>
    /// Finds the displayed name of a fleet officer holding one command rank.
    /// </summary>
    /// <param name="fleet">The fleet to inspect.</param>
    /// <param name="rank">The command rank to find.</param>
    /// <returns>The assigned officer name, or a not-assigned label.</returns>
    private static string FindFleetOfficerByRank(Fleet fleet, OfficerRank rank)
    {
        Officer officer = fleet.GetOfficers().FirstOrDefault(o => o.CurrentRank == rank);
        return officer?.GetDisplayName() ?? "Not Assigned";
    }

    /// <summary>
    /// Resolves the visible target or location for one mission.
    /// </summary>
    /// <param name="mission">The mission to inspect.</param>
    /// <returns>The visible mission target, location, or parent node.</returns>
    private ISceneNode ResolveMissionTarget(Mission mission)
    {
        string targetInstanceId = GetMissionTargetInstanceId(mission);
        if (!string.IsNullOrEmpty(targetInstanceId))
            return findVisibleNode(targetInstanceId);

        if (!string.IsNullOrEmpty(mission.LocationInstanceID))
            return findVisibleNode(mission.LocationInstanceID);

        return mission.GetParent();
    }

    /// <summary>
    /// Resolves the explicit target identifier carried by one mission type.
    /// </summary>
    /// <param name="mission">The mission to inspect.</param>
    /// <returns>The explicit target identifier, or <see langword="null"/>.</returns>
    private static string GetMissionTargetInstanceId(Mission mission)
    {
        return mission switch
        {
            SabotageMission sabotage => sabotage.SabotageTargetInstanceID,
            RecruitmentMission recruitment => recruitment.TargetOfficerInstanceID,
            AbductionMission abduction => abduction.TargetOfficerInstanceID,
            AssassinationMission assassination => assassination.TargetOfficerInstanceID,
            RescueMission rescue => rescue.TargetOfficerInstanceID,
            _ => null,
        };
    }

    /// <summary>
    /// Formats a nonnegative current-to-maximum status value.
    /// </summary>
    /// <param name="current">The current value.</param>
    /// <param name="maximum">The maximum value.</param>
    /// <returns>The displayed ratio.</returns>
    private static string FormatStatusRatio(int current, int maximum)
    {
        return Math.Max(current, 0) + ":" + Math.Max(maximum, 0);
    }

    /// <summary>
    /// Gets one planet manufacturing queue.
    /// </summary>
    /// <param name="planet">The planet whose queue is requested.</param>
    /// <param name="type">The manufacturing category.</param>
    /// <returns>The matching queue or an empty collection.</returns>
    private static IReadOnlyList<IManufacturable> GetQueue(Planet planet, ManufacturingType type)
    {
        return planet.ManufacturingQueue.TryGetValue(type, out List<IManufacturable> queue)
            ? queue
            : Array.Empty<IManufacturable>();
    }
}
