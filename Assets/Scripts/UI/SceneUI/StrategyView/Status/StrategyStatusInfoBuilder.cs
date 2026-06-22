using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

public sealed class StrategyStatusInfoBuilder
{
    private readonly GameManager gameManager;
    private readonly IReadOnlyList<GalaxyMapSector> sectors;
    private readonly string playerFactionId;
    private readonly Func<string, ISceneNode> findVisibleNode;

    public StrategyStatusInfoBuilder(StrategyWindowRenderContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        gameManager =
            context.GameManager ?? throw new ArgumentNullException(nameof(context.GameManager));
        sectors = context.Sectors ?? throw new ArgumentNullException(nameof(context.Sectors));
        playerFactionId = context.PlayerFactionId;
        findVisibleNode = context.FindVisibleNode;
    }

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

    public StatusWindowRenderData CreateRenderData(UIWindow window)
    {
        StatusWindowView view = null;
        if (window != null)
        {
            StatusWindowView statusWindowView = null;
            if (window.TryGetContent(out statusWindowView))
                view = statusWindowView;
        }

        StrategyStatusInfo info = Build(view?.StatusTarget);
        if (info == null)
            return null;

        return new StatusWindowRenderData
        {
            X = window.X,
            Y = window.Y,
            OwnerFactionId = info.OwnerFactionId,
            CenterImage = info.CenterImage,
            InfoDisabled = view?.InfoDisabled == true,
            Header = info.Header,
            Label = info.Label,
            SourceRows = info.Rows,
            Images = info.Images,
            SourceStatusImageItems = info.StatusImageItems,
            SourceImageItems = info.ImageItems,
            SourceOverlayImageItems = info.OverlayImageItems,
        };
    }

    private StrategyStatusInfo BuildManufacturingManagerStatusInfo(StrategyStatusTarget target)
    {
        ManufacturingType type = target.ManufacturingType.Value;
        List<IManufacturable> queue = GetQueue(target.Planet.Planet, type);
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
            info.Rows.Add(new StrategyStatusRow("Build ETA:", GetQueueEta(queue).ToString()));
        }

        return info;
    }

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

    private int GetPlayerSupport(Planet planet)
    {
        return !string.IsNullOrEmpty(playerFactionId)
            ? planet.GetPopularSupport(playerFactionId)
            : 50;
    }

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
            new StrategyStatusRow("Status:", fleet.Movement != null ? "Enroute" : "Awaiting Order")
        );
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
        info.Rows.Add(new StrategyStatusRow("Team Size:", teamSize.ToString()));
        info.Rows.Add(
            new StrategyStatusRow("Decoys:", (mission.DecoyParticipants?.Count ?? 0).ToString())
        );
        return info;
    }

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

        if (
            item is IManufacturable manufacturable
            && manufacturable.GetManufacturingStatus() == ManufacturingStatus.Building
        )
            info.Images.Add(StatusWindowImage.FactionConstruction);
        else if (HasEntityStatusImage(item))
            info.StatusImageItems.Add(item);
        else if (item is IMovable movable && movable.Movement != null && item is not Fleet)
            info.Images.Add(StatusWindowImage.Enroute);
        else if (item is Regiment or SpecialForces)
            info.Images.Add(StatusWindowImage.PersonnelBackground);

        info.ImageItems.Add(item);
        if (
            item is Officer { IsCaptured: true }
            && !string.IsNullOrEmpty(item.CapturedOverlayImagePath)
        )
            info.OverlayImageItems.Add(item);
        return info;
    }

    private static bool HasEntityStatusImage(ISceneNode item)
    {
        if (item == null)
            return false;

        if (item is Officer { InjuryPoints: > 0 } && !string.IsNullOrEmpty(item.InjuredImagePath))
            return true;

        if (
            item is IMovable { Movement: not null }
            && !string.IsNullOrEmpty(item.InTransitImagePath)
        )
            return true;

        if (item is CapitalShip capitalShip && capitalShip.IsDamaged())
            return !string.IsNullOrEmpty(item.DamagedImagePath);

        if (item is Starfighter starfighter && starfighter.HasLosses())
            return !string.IsNullOrEmpty(item.DamagedImagePath);

        return false;
    }

    private static int GetFacilityCountForManufacturingType(Planet planet, ManufacturingType type)
    {
        return planet.Buildings.Count(building =>
            building.GetManufacturingStatus() == ManufacturingStatus.Complete
            && building.GetProductionType() == type
        );
    }

    private static int GetQueueEta(List<IManufacturable> queue)
    {
        return queue.Sum(item =>
            Math.Max(item.GetConstructionCost(), 0) - Math.Max(item.GetManufacturingProgress(), 0)
        );
    }

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

    private static Planet GetStatusPlanet(ISceneNode item)
    {
        if (item is Planet planet)
            return planet;

        return item?.GetParentOfType<Planet>();
    }

    private static string GetStatusLocationName(StrategyStatusTarget target, ISceneNode item)
    {
        Planet planet = target.Planet?.Planet ?? GetStatusPlanet(item);
        if (planet != null)
            return planet.GetDisplayName();

        ISceneNode parent = item?.GetParent();
        return parent?.GetDisplayName() ?? "Unknown";
    }

    private static string GetManufacturingStatusText(IManufacturable item)
    {
        if (item.GetManufacturingStatus() == ManufacturingStatus.Building)
            return item is Regiment or SpecialForces ? "Training" : "Under Construction";

        if (item is IMovable movable && movable.Movement != null)
            return "Enroute";

        return item.GetManufacturingStatus() == ManufacturingStatus.Complete
            ? item is Regiment or SpecialForces
                ? "Awaiting Order"
                : "Active"
            : "Idle";
    }

    private static string GetRatingText(IMissionParticipant participant, OfficerRating rating)
    {
        return participant.GetEffectiveRating(rating).ToString();
    }

    private static string GetOfficerStatusText(Officer officer)
    {
        if (officer.Movement != null)
            return "Enroute";
        if (officer.IsCaptured)
            return "Captured";
        if (officer.InjuryPoints > 0)
            return "Injured";
        if (officer.IsOnMission())
            return "On Mission";
        return "Awaiting Order";
    }

    private static string GetOfficerCommandingText(Officer officer)
    {
        if (officer.CurrentRank == OfficerRank.None)
            return "None";

        ISceneNode parent = officer.GetParent();
        if (parent is CapitalShip ship)
            return ship.GetParentOfType<Fleet>()?.GetDisplayName() ?? ship.GetDisplayName();

        return parent?.GetDisplayName() ?? "None";
    }

    private string GetForceRankText(Officer officer)
    {
        GameConfig.JediConfig config = gameManager.GetGame()?.Config?.Jedi;
        if (config == null || !officer.IsJedi)
            return "None";

        if (officer.ForceRank >= config.RankLabelForceMaster)
            return "Jedi Master";
        if (officer.ForceRank >= config.RankLabelForceKnight)
            return "Jedi Knight";
        if (officer.ForceRank >= config.RankLabelForceStudent)
            return "Jedi Student";
        if (officer.ForceRank >= config.RankLabelTrainee)
            return "Trainee";
        if (officer.ForceRank >= config.RankLabelNovice)
            return "Novice";
        return "None";
    }

    private static string FindFleetOfficerByRank(Fleet fleet, OfficerRank rank)
    {
        Officer officer = fleet.GetOfficers().FirstOrDefault(o => o.CurrentRank == rank);
        return officer?.GetDisplayName() ?? "Not Assigned";
    }

    private ISceneNode ResolveMissionTarget(Mission mission)
    {
        if (string.IsNullOrEmpty(mission.TargetInstanceID))
            return mission.GetParent();

        return findVisibleNode(mission.TargetInstanceID);
    }

    private static string FormatStatusRatio(int current, int maximum)
    {
        return Math.Max(current, 0) + ":" + Math.Max(maximum, 0);
    }

    private static List<IManufacturable> GetQueue(Planet planet, ManufacturingType type)
    {
        return planet.ManufacturingQueue.TryGetValue(type, out List<IManufacturable> queue)
            ? queue
            : new List<IManufacturable>();
    }
}
