using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI
{
    [TestFixture]
    public class AIStrategicPlanTests
    {
        [TestCase(ManufacturingStatus.Complete)]
        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Delivering)]
        public void GetInfrastructureDevelopmentStep_ConstructionHubCommitted_PrioritizesShipyards(
            ManufacturingStatus status
        )
        {
            AITurnContext context = CreateIndustrialDevelopmentContext(5, 0, out Planet hub);
            foreach (Building building in hub.GetChildren<Building>())
                building.ManufacturingStatus = status;

            var step = context.StrategicPlan.GetInfrastructureDevelopmentStep(hub, null);

            Assert.AreEqual(BuildingType.Shipyard, step.NextType);
        }

        [Test]
        public void GetInfrastructureDevelopmentStep_ConstructionHubIncomplete_PrioritizesConstruction()
        {
            AITurnContext context = CreateIndustrialDevelopmentContext(4, 0, out Planet hub);

            var step = context.StrategicPlan.GetInfrastructureDevelopmentStep(hub, null);

            Assert.AreEqual(BuildingType.ConstructionFacility, step.NextType);
        }

        [Test]
        public void GetInfrastructureDevelopmentStep_LastConstructionYardSelected_PrioritizesShipyards()
        {
            AITurnContext context = CreateIndustrialDevelopmentContext(4, 0, out Planet hub);
            Dictionary<string, Dictionary<BuildingType, int>> selected = new()
            {
                [hub.InstanceID] = new() { [BuildingType.ConstructionFacility] = 1 },
            };

            var step = context.StrategicPlan.GetInfrastructureDevelopmentStep(hub, selected);

            Assert.AreEqual(BuildingType.Shipyard, step.NextType);
        }

        [Test]
        public void GetInfrastructureDevelopmentStep_BothPrimaryHubsCovered_ReturnsNoPreferredStep()
        {
            AITurnContext context = CreateIndustrialDevelopmentContext(5, 6, out Planet hub);

            var step = context.StrategicPlan.GetInfrastructureDevelopmentStep(hub, null);

            Assert.AreEqual(BuildingType.None, step.NextType);
        }

        [Test]
        public void GetInfrastructureDevelopmentStep_SmallerHubAllocated_DoesNotSwitchToUnassignedLargerSite()
        {
            AITurnContext initial = CreateIndustrialDevelopmentContext(3, 0, out Planet hub);
            hub.EnergyCapacity = 3;
            AITestSceneBuilder
                .AddPlanet(
                    initial.Game,
                    hub.GetParentOfType<PlanetSector>(),
                    "unassigned",
                    initial.Faction.InstanceID,
                    energyCapacity: 8
                )
                .SetPopularSupport(initial.Faction.InstanceID, 100);
            AITurnContext context = AITestSceneBuilder.CreateContext(initial.Game, initial.Faction);
            context.StrategicPlan.TryAllocateInfrastructureHub(
                hub,
                BuildingType.ConstructionFacility,
                3
            );
            context.StrategicPlan.TryAllocateInfrastructureHub(
                initial.Game.GetSceneNodeByInstanceID<Planet>("shipyard-hub"),
                BuildingType.Shipyard,
                6
            );

            var step = context.StrategicPlan.GetInfrastructureDevelopmentStep(hub, null);

            Assert.AreEqual(BuildingType.Shipyard, step.NextType);
        }

        /// <summary>
        /// Applies the headquarters multiplier without exceeding its affordable fleet share.
        /// </summary>
        /// <param name="headquartersPercent">The headquarters multiplier, or zero to use the attack setting.</param>
        /// <param name="friendlyStrength">The faction's available fleet strength.</param>
        /// <param name="expectedStrength">The resulting headquarters defense requirement.</param>
        [TestCase(110, 20000, 2200)]
        [TestCase(175, 20000, 3500)]
        [TestCase(110, 4000, 1400)]
        [TestCase(0, 20000, 3500)]
        public void GetHeadquartersDefenseStrength_ConfiguredHostileFleetMultiplier_RespectsAffordabilityLimit(
            int headquartersPercent,
            int friendlyStrength,
            int expectedStrength
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.HeadquartersDefenseCombatPercent = 35;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 175;
            game.Config.AI.FleetDeployment.HeadquartersDefensePercentOfStrongestHostileFleet =
                headquartersPercent;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "headquarters-sector");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "headquarters",
                faction.InstanceID
            );
            faction.HQInstanceID = headquarters.InstanceID;
            Fleet defender = EntityFactory.CreateFleet("defender", faction.InstanceID);
            game.AttachNode(defender, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "friendly-ship",
                    faction.InstanceID,
                    friendlyStrength
                ),
                defender
            );
            Planet hostilePlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hostile-planet",
                enemy.InstanceID
            );
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile", enemy.InstanceID);
            game.AttachNode(hostileFleet, hostilePlanet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("hostile-ship", enemy.InstanceID, 2000),
                hostileFleet
            );
            AITestSceneBuilder.RevealPlanet(game, faction, hostilePlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);

            Assert.AreEqual(
                expectedStrength,
                context.StrategicPlan.GetHeadquartersDefenseStrength(headquarters)
            );
        }

        /// <summary>
        /// Requires room for the completed industrial layout and its shield.
        /// </summary>
        /// <param name="availableSpace">The neutral planet's building capacity.</param>
        /// <param name="expectedOpportunity">Whether the layout fits.</param>
        [TestCase(3, false)]
        [TestCase(4, true)]
        public void GetInfrastructureAcquisitionValue_NeutralHubNeedsShieldSpace_RequiresFinishedLayout(
            int availableSpace,
            bool expectedOpportunity
        )
        {
            AITurnContext context = CreateIndustrialAcquisitionContext(
                0,
                availableSpace,
                out Planet candidate
            );

            double value = context.StrategicPlan.GetInfrastructureAcquisitionValue(candidate);

            Assert.AreEqual(expectedOpportunity, value > 0);
        }

        /// <summary>
        /// Avoids rewarding redundant industrial sites.
        /// </summary>
        [Test]
        public void GetInfrastructureAcquisitionValue_OwnedHubSiteAvailable_DoesNotRewardAnotherEmptySite()
        {
            AITurnContext context = CreateIndustrialAcquisitionContext(4, 4, out Planet candidate);

            Assert.Zero(context.StrategicPlan.GetInfrastructureAcquisitionValue(candidate));
        }

        /// <summary>
        /// Counts committed capacity when assessing acquisition needs.
        /// </summary>
        /// <param name="status">The committed facilities' manufacturing state.</param>
        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Delivering)]
        [TestCase(ManufacturingStatus.Complete)]
        public void GetInfrastructureAcquisitionValue_HubCapacityCommitted_RemovesAcquisitionNeed(
            ManufacturingStatus status
        )
        {
            AITurnContext initial = CreateIndustrialAcquisitionContext(4, 4, out Planet candidate);
            Planet owned = initial.Assessment.OwnedPlanets[0];
            for (int index = 0; index < 3; index++)
            {
                Building building = AITestSceneBuilder.AddProductionFacility(
                    initial.Game,
                    owned,
                    "yard-" + index,
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
                building.ManufacturingStatus = status;
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(initial.Game, initial.Faction);

            Assert.Zero(context.StrategicPlan.GetInfrastructureAcquisitionValue(candidate));
        }

        /// <summary>
        /// Does not encourage expansion into sectors without an existing presence.
        /// </summary>
        [Test]
        public void GetInfrastructureAcquisitionValue_SectorWithoutOwnedPlanets_DoesNotCreateExpansionBonus()
        {
            AITurnContext initial = CreateIndustrialAcquisitionContext(0, 4, out Planet candidate);
            PlanetSector other = AITestSceneBuilder.AddSector(initial.Game, "other-sector");
            candidate = AITestSceneBuilder.AddPlanet(
                initial.Game,
                other,
                "other-neutral",
                null,
                energyCapacity: 4
            );
            AITestSceneBuilder.RevealPlanet(initial.Game, initial.Faction, candidate);
            AITurnContext context = AITestSceneBuilder.CreateContext(initial.Game, initial.Faction);

            Assert.Zero(context.StrategicPlan.GetInfrastructureAcquisitionValue(candidate));
        }

        /// <summary>
        /// Values immediately usable construction capacity despite an existing empty hub site.
        /// </summary>
        [Test]
        public void GetInfrastructureAcquisitionValue_NeutralPlanetSuppliesNeededCapacity_ValuesExistingYards()
        {
            AITurnContext initial = CreateIndustrialAcquisitionContext(4, 2, out Planet candidate);
            AITestSceneBuilder.AddProductionFacility(
                initial.Game,
                candidate,
                "neutral-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITestSceneBuilder.RevealPlanet(initial.Game, initial.Faction, candidate);
            AITurnContext context = AITestSceneBuilder.CreateContext(initial.Game, initial.Faction);

            Assert.Greater(context.StrategicPlan.GetInfrastructureAcquisitionValue(candidate), 0);
        }

        [TestCase(1000, true)]
        [TestCase(999, false)]
        public void CanReleaseCapitalShip_HeadquartersDefender_PreservesRequiredStrength(
            int remainingStrength,
            bool expected
        )
        {
            AITurnContext context = CreateDefender(out Fleet fleet, out CapitalShip spare);
            CapitalShip anchor = fleet.GetChildren<CapitalShip>()[0];
            anchor.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = remainingStrength;

            Assert.AreEqual(expected, context.StrategicPlan.CanReleaseCapitalShip(fleet, spare));
        }

        [TestCase(FleetOrderType.Attack)]
        [TestCase(FleetOrderType.Colonize)]
        public void CanReleaseCapitalShip_ActiveOperation_ReturnsFalse(FleetOrderType orderType)
        {
            AITurnContext context = CreateDefender(out Fleet fleet, out CapitalShip spare);
            fleet.Order.OrderType = orderType;

            Assert.IsFalse(context.StrategicPlan.CanReleaseCapitalShip(fleet, spare));
        }

        [Test]
        public void CanReleaseCapitalShip_DefenderAssignedElsewhere_ReturnsFalse()
        {
            AITurnContext context = CreateDefender(out Fleet fleet, out CapitalShip spare);
            fleet.Order.TargetPlanetId = "elsewhere";

            Assert.IsFalse(context.StrategicPlan.CanReleaseCapitalShip(fleet, spare));
        }

        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Delivering)]
        public void CanReleaseCapitalShip_UnfinishedRemainingShip_ReturnsFalse(
            ManufacturingStatus status
        )
        {
            AITurnContext context = CreateDefender(out Fleet fleet, out CapitalShip spare);
            fleet.GetChildren<CapitalShip>()[0].ManufacturingStatus = status;

            Assert.IsFalse(context.StrategicPlan.CanReleaseCapitalShip(fleet, spare));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CanReleaseCapitalShip_LastInterdictor_ReturnsFalse(bool defending)
        {
            AITurnContext context = CreateDefender(out Fleet fleet, out CapitalShip spare);
            if (!defending)
                fleet.Order = null;
            spare.HasGravityWell = true;

            Assert.IsFalse(context.StrategicPlan.CanReleaseCapitalShip(fleet, spare));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CanReleaseCapitalShip_LastArmedEscort_ReturnsFalse(bool defending)
        {
            AITurnContext context = CreateDefender(out Fleet fleet, out CapitalShip spare);
            if (!defending)
                fleet.Order = null;
            CapitalShip anchor = fleet.GetChildren<CapitalShip>()[0];
            anchor.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = 0;
            context.Game.AttachNode(
                AITestSceneBuilder.CreateStarfighter(
                    "defense-fighters",
                    context.Faction.InstanceID,
                    laserCannon: 2000
                ),
                anchor
            );

            Assert.IsFalse(context.StrategicPlan.CanReleaseCapitalShip(fleet, spare));
        }

        [Test]
        public void CanReleaseCapitalShip_MovingDonor_ReturnsFalse()
        {
            AITurnContext context = CreateDefender(out Fleet fleet, out CapitalShip spare);
            fleet.Movement = new MovementState { TransitTicks = 10 };

            Assert.IsFalse(context.StrategicPlan.CanReleaseCapitalShip(fleet, spare));
        }

        [Test]
        public void CanReleaseCapitalShip_ColonizationFleet_ReturnsFalse()
        {
            AITurnContext context = CreateDefender(out Fleet fleet, out CapitalShip spare);
            fleet.RoleType = FleetRoleType.Colonization;

            Assert.IsFalse(context.StrategicPlan.CanReleaseCapitalShip(fleet, spare));
        }

        [Test]
        public void CanReleaseCapitalShip_OtherLocalDonorAlreadyCommitted_PreservesLocalDefense()
        {
            AITurnContext context = CreateDefender(out Fleet first, out CapitalShip firstSpare);
            first.GetChildren<CapitalShip>()[0].PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] =
                600;
            Fleet second = EntityFactory.CreateFleet("second", context.Faction.InstanceID);
            second.RoleType = FleetRoleType.Battle;
            context.Game.AttachNode(second, first.GetParent());
            context.Game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "second-anchor",
                    context.Faction.InstanceID,
                    600
                ),
                second
            );
            CapitalShip secondSpare = AITestSceneBuilder.CreateCapitalShip(
                "second-spare",
                context.Faction.InstanceID,
                500
            );
            context.Game.AttachNode(secondSpare, second);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            Assert.IsTrue(context.StrategicPlan.CanReleaseCapitalShip(second, secondSpare));

            bool canRelease = context.StrategicPlan.CanReleaseCapitalShip(
                second,
                secondSpare,
                new[] { firstSpare }
            );

            Assert.IsFalse(canRelease);
        }

        [TestCase(FleetOrderType.Defend, true, true)]
        [TestCase(FleetOrderType.Defend, false, false)]
        [TestCase(FleetOrderType.Attack, true, false)]
        [TestCase(FleetOrderType.Colonize, true, false)]
        [TestCase(null, true, false)]
        public void CanReleaseCapitalShip_OtherLocalFleet_CountsOnlyCommittedDefense(
            FleetOrderType? orderType,
            bool localTarget,
            bool expected
        )
        {
            AITurnContext context = CreateDefender(out Fleet donor, out CapitalShip spare);
            donor.Order = null;
            donor.GetChildren<CapitalShip>()[0].PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] =
                600;
            Fleet cover = EntityFactory.CreateFleet("cover", context.Faction.InstanceID);
            cover.RoleType = FleetRoleType.Battle;
            if (orderType.HasValue)
                cover.Order = new FleetOrder
                {
                    OrderType = orderType.Value,
                    Status = FleetOrderStatus.Ready,
                    TargetPlanetId = localTarget
                        ? donor.GetParentOfType<Planet>().InstanceID
                        : "elsewhere",
                };
            context.Game.AttachNode(cover, donor.GetParent());
            context.Game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "cover-ship",
                    context.Faction.InstanceID,
                    1000
                ),
                cover
            );
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            Assert.AreEqual(expected, context.StrategicPlan.CanReleaseCapitalShip(donor, spare));
        }

        [Test]
        public void TryAllocateInfrastructureHub_ExactIndustrialAndShieldSpace_PreservesFinishedLayout()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 7
            );
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;

            Assert.IsTrue(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            Assert.AreEqual(
                6,
                plan.GetAllocatedInfrastructureTarget(planet, BuildingType.Shipyard)
            );
            Assert.AreEqual(6, plan.GetAvailableConstructionSpace(planet, BuildingType.Shipyard));
            Assert.AreEqual(0, plan.GetAvailableConstructionSpace(planet, BuildingType.Mine));
            Assert.AreEqual(1, plan.GetAvailableConstructionSpace(planet, BuildingType.Defense));
        }

        [Test]
        public void TryAllocateInfrastructureHub_NoSpaceForShield_RejectsLayout()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 6
            );
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;

            Assert.IsFalse(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            Assert.AreEqual(
                0,
                plan.GetAllocatedInfrastructureTarget(planet, BuildingType.Shipyard)
            );
        }

        [TestCase(ManufacturingStatus.Complete)]
        [TestCase(ManufacturingStatus.Building)]
        public void GetAvailableConstructionSpace_CommittedShield_DoesNotReserveItsSlotTwice(
            ManufacturingStatus status
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 7
            );
            Building shield = AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shield",
                BuildingType.Defense,
                ManufacturingType.None
            );
            shield.ShieldStrength = 80;
            shield.ManufacturingStatus = status;
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;

            Assert.IsTrue(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            Assert.AreEqual(6, plan.GetAvailableConstructionSpace(planet, BuildingType.Shipyard));
            Assert.AreEqual(0, plan.GetAvailableConstructionSpace(planet, BuildingType.Refinery));
        }

        [Test]
        public void TryAllocateInfrastructureHub_AnotherCategoryAlreadyPlanned_PreventsOverbooking()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 10
            );
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;

            Assert.IsTrue(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            Assert.IsFalse(
                plan.TryAllocateInfrastructureHub(planet, BuildingType.TrainingFacility, 4)
            );
            Assert.AreEqual(
                3,
                plan.GetAvailableConstructionSpace(planet, BuildingType.TrainingFacility)
            );
        }

        [Test]
        public void GetAvailableConstructionSpace_IndustryQueued_ReleasesOnlyItsSatisfiedReservation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 8
            );
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;
            Assert.IsTrue(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            for (int index = 0; index < 6; index++)
            {
                Building shipyard = AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"yard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
                shipyard.ManufacturingStatus = ManufacturingStatus.Building;
            }

            Assert.AreEqual(1, plan.GetAvailableConstructionSpace(planet, BuildingType.Refinery));
            Assert.AreEqual(2, plan.GetAvailableConstructionSpace(planet, BuildingType.Defense));
            Assert.AreEqual(1, plan.GetPlanetaryShieldTarget(planet, true));
        }

        [Test]
        public void GetAvailableConstructionSpace_SelectedIndustry_DoesNotReserveItTwice()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 7
            );
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;
            Assert.IsTrue(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            var selected = new Dictionary<BuildingType, int> { [BuildingType.Shipyard] = 6 };

            Assert.AreEqual(
                1,
                plan.GetAvailableConstructionSpace(
                    planet,
                    BuildingType.Defense,
                    selectedBuildings: selected
                )
            );
            Assert.AreEqual(
                0,
                plan.GetAvailableConstructionSpace(
                    planet,
                    BuildingType.Mine,
                    selectedBuildings: selected
                )
            );
        }

        [Test]
        public void FleetCountChange_Default_RedistributesIndependentMobileStrengthTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            GameConfig.AIFleetDeploymentConfig config = game.Config.AI.FleetDeployment;
            config.MinimumBattleFleetCount = 4;
            config.PlanetsPerBattleFleet = 8;
            config.MinimumAttackStrength = 1400;
            config.MinimumMobileCombatStrength = 5600;
            config.MobileCombatStrengthPerPlanet = 175;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 40; index++)
            {
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);
            }

            AIStrategicPlan eightPlanetPlan = AITestSceneBuilder
                .CreateContext(game, empire)
                .StrategicPlan;
            config.PlanetsPerBattleFleet = 10;
            AIStrategicPlan tenPlanetPlan = AITestSceneBuilder
                .CreateContext(game, empire)
                .StrategicPlan;

            Assert.AreEqual(7000, eightPlanetPlan.TargetMobileCombatStrength);
            Assert.AreEqual(7000, tenPlanetPlan.TargetMobileCombatStrength);
            Assert.AreEqual(5, eightPlanetPlan.TargetBattleFleetCount);
            Assert.AreEqual(1400, eightPlanetPlan.AssemblyFleetCombatStrength);
            Assert.AreEqual(4, tenPlanetPlan.TargetBattleFleetCount);
            Assert.AreEqual(1750, tenPlanetPlan.AssemblyFleetCombatStrength);
        }

        [Test]
        public void MobileStrengthTarget_WithManyTargetFleets_CoversMinimumStrengthForEveryFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            GameConfig.AIFleetDeploymentConfig config = game.Config.AI.FleetDeployment;
            config.MinimumBattleFleetCount = 4;
            config.PlanetsPerBattleFleet = 8;
            config.MinimumAttackStrength = 1400;
            config.MinimumMobileCombatStrength = 5600;
            config.MobileCombatStrengthPerPlanet = 175;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 73; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(10, plan.TargetBattleFleetCount);
            Assert.AreEqual(14000, plan.TargetMobileCombatStrength);
            Assert.AreEqual(1400, plan.AssemblyFleetCombatStrength);
        }

        [Test]
        public void GetInfrastructureTarget_WithTwoShipyardPlanets_ReturnsBothHubTargets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ShipyardPrimaryHubTargetCount = 6;
            game.Config.AI.Infrastructure.ShipyardSecondaryHubTargetCount = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 2; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(10, plan.GetInfrastructureTarget(BuildingType.Shipyard));
        }

        [Test]
        public void GetInfrastructureTarget_WithOneConstructionPlanet_ReturnsPrimaryHubTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ConstructionFacilityPrimaryHubTargetCount = 5;
            game.Config.AI.Infrastructure.ConstructionFacilitySecondaryHubTargetCount = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(5, plan.GetInfrastructureTarget(BuildingType.ConstructionFacility));
        }

        [Test]
        public void GetInfrastructureTarget_WithTwoPopulatedSectors_AddsTargetsForEachSector()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ShipyardPrimaryHubTargetCount = 6;
            game.Config.AI.Infrastructure.ShipyardSecondaryHubTargetCount = 4;
            for (int sectorIndex = 0; sectorIndex < 2; sectorIndex++)
            {
                PlanetSector sector = AITestSceneBuilder.AddSector(game, $"sector-{sectorIndex}");
                for (int planetIndex = 0; planetIndex < 2; planetIndex++)
                {
                    AITestSceneBuilder.AddPlanet(
                        game,
                        sector,
                        $"planet-{sectorIndex}-{planetIndex}",
                        empire.InstanceID
                    );
                }
            }

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(20, plan.GetInfrastructureTarget(BuildingType.Shipyard));
            Assert.AreEqual(20, plan.GetInfrastructureDeficit(BuildingType.Shipyard));
        }

        [Test]
        public void GetInfrastructureTarget_WithMultipleTrainingPlanets_UsesOneSectorHub()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.TrainingFacilityHubTargetCount = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 3; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(4, plan.GetInfrastructureTarget(BuildingType.TrainingFacility));
            Assert.AreEqual(4, plan.GetInfrastructureDeficit(BuildingType.TrainingFacility));
        }

        [Test]
        public void GetInfrastructureDeficit_WithCommittedFacility_CountsFacilityTowardTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.TrainingFacilityHubTargetCount = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Building trainingFacility = AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            trainingFacility.ManufacturingStatus = ManufacturingStatus.Building;

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(1, plan.GetInfrastructureCount(BuildingType.TrainingFacility));
            Assert.AreEqual(3, plan.GetInfrastructureDeficit(BuildingType.TrainingFacility));
        }

        [Test]
        public void GetInfrastructureDeficit_WithScatteredShipyards_PreservesHubShortfall()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ShipyardPrimaryHubTargetCount = 6;
            game.Config.AI.Infrastructure.ShipyardSecondaryHubTargetCount = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int planetIndex = 0; planetIndex < 3; planetIndex++)
            {
                Planet planet = AITestSceneBuilder.AddPlanet(
                    game,
                    sector,
                    $"planet-{planetIndex}",
                    empire.InstanceID
                );
                for (int facilityIndex = 0; facilityIndex < 2; facilityIndex++)
                {
                    AITestSceneBuilder.AddProductionFacility(
                        game,
                        planet,
                        $"shipyard-{planetIndex}-{facilityIndex}",
                        BuildingType.Shipyard,
                        ManufacturingType.Ship
                    );
                }
            }

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(6, plan.GetInfrastructureCount(BuildingType.Shipyard));
            Assert.AreEqual(10, plan.GetInfrastructureTarget(BuildingType.Shipyard));
            Assert.AreEqual(6, plan.GetInfrastructureDeficit(BuildingType.Shipyard));
        }

        [Test]
        public void CanReinforce_FriendlyShipUnderOpposingFleet_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet receivingFleet = EntityFactory.CreateFleet("receiving-fleet", empire.InstanceID);
            game.AttachNode(receivingFleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("receiving-ship", empire.InstanceID),
                receivingFleet
            );
            Fleet opposingFleet = EntityFactory.CreateFleet("opposing-fleet", rebels.InstanceID);
            game.AttachNode(opposingFleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("opposing-ship", rebels.InstanceID),
                opposingFleet
            );
            AITestSceneBuilder.RevealPlanet(game, empire, planet);
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.IsFalse(plan.CanReinforce(receivingFleet));
        }

        [TestCase(FleetRoleType.Battle, ManufacturingStatus.Complete, 2)]
        [TestCase(FleetRoleType.Battle, ManufacturingStatus.Building, 1)]
        [TestCase(FleetRoleType.Colonization, ManufacturingStatus.Complete, 1)]
        public void GetPlanetaryShieldTarget_PoliticalFleetHold_MatchesOperationalBattleCommitment(
            FleetRoleType role,
            ManufacturingStatus status,
            int expectedTarget
        )
        {
            AITurnContext context = CreatePoliticalFleetHoldContext(out Fleet fleet);
            fleet.RoleType = role;
            fleet.GetChildren<CapitalShip>()[0].ManufacturingStatus = status;

            Assert.AreEqual(
                expectedTarget,
                context.StrategicPlan.GetPlanetaryShieldTarget(fleet.GetParentOfType<Planet>())
            );
        }

        [Test]
        public void CanFleetDepart_BuildingRequestedShields_ReleasesPoliticallyHeldFleet()
        {
            AITurnContext context = CreatePoliticalFleetHoldContext(out Fleet fleet);
            Planet planet = fleet.GetParentOfType<Planet>();
            Assert.IsFalse(context.StrategicPlan.CanFleetDepart(fleet));
            int target = context.StrategicPlan.GetPlanetaryShieldTarget(planet);
            for (int index = 0; index < target; index++)
            {
                Building shield = AITestSceneBuilder.AddProductionFacility(
                    context.Game,
                    planet,
                    "shield-" + index,
                    BuildingType.Defense,
                    ManufacturingType.None
                );
                shield.ShieldStrength = 80;
            }
            AITurnContext completed = AITestSceneBuilder.CreateContext(
                context.Game,
                context.Faction
            );

            Assert.IsTrue(completed.StrategicPlan.CanFleetDepart(fleet));
        }

        [TestCase(0, ManufacturingStatus.Complete, false, false)]
        [TestCase(1, ManufacturingStatus.Complete, false, false)]
        [TestCase(2, ManufacturingStatus.Complete, false, true)]
        [TestCase(2, ManufacturingStatus.Building, false, false)]
        [TestCase(2, ManufacturingStatus.Delivering, false, false)]
        [TestCase(2, ManufacturingStatus.Complete, true, false)]
        public void CanFleetDepart_PoliticalRisk_RequiresStationaryCompletedGarrisonReserve(
            int count,
            ManufacturingStatus status,
            bool inTransit,
            bool expected
        )
        {
            AITurnContext initial = CreatePoliticalFleetHoldContext(out Fleet fleet);
            Planet planet = fleet.GetParentOfType<Planet>();
            initial.Game.Config.AI.Garrison.GarrisonDivisor = 20;
            initial.Faction.Settings.GarrisonEfficiency = 1;
            for (int index = 0; index < count; index++)
            {
                Regiment regiment = AITestSceneBuilder.CreateRegiment(
                    "garrison-" + index,
                    initial.Faction.InstanceID
                );
                regiment.ManufacturingStatus = status;
                if (inTransit)
                    regiment.Movement = new MovementState { TransitTicks = 10 };
                initial.Game.AttachNode(regiment, planet);
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(initial.Game, initial.Faction);

            Assert.AreEqual(expected, context.StrategicPlan.CanFleetDepart(fleet));
        }

        [Test]
        public void CanFleetDepart_EmbarkedGarrison_DoesNotReleasePoliticalHold()
        {
            AITurnContext initial = CreatePoliticalFleetHoldContext(out Fleet fleet);
            initial.Game.Config.AI.Garrison.GarrisonDivisor = 20;
            CapitalShip carrier = fleet.GetChildren<CapitalShip>()[0];
            carrier.RegimentCapacity = 3;
            for (int index = 0; index < 3; index++)
                initial.Game.AttachNode(
                    AITestSceneBuilder.CreateRegiment(
                        "embarked-" + index,
                        initial.Faction.InstanceID
                    ),
                    carrier
                );
            AITurnContext context = AITestSceneBuilder.CreateContext(initial.Game, initial.Faction);

            Assert.IsFalse(context.StrategicPlan.CanFleetDepart(fleet));
        }

        [Test]
        public void GetPlanetaryGarrisonTarget_ThreatenedPlanet_PreservesPreferredCaptureGarrison()
        {
            AITurnContext initial = CreatePoliticalFleetHoldContext(out Fleet fleet);
            Planet planet = fleet.GetParentOfType<Planet>();
            initial.Game.Config.AI.Garrison.GarrisonDivisor = 20;
            initial.Game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 6;
            Planet enemy = AITestSceneBuilder.AddPlanet(
                initial.Game,
                planet.GetParentOfType<PlanetSector>(),
                "enemy-world",
                "rebels"
            );
            AITestSceneBuilder.RevealPlanet(initial.Game, initial.Faction, enemy);
            AITurnContext context = AITestSceneBuilder.CreateContext(initial.Game, initial.Faction);

            Assert.AreEqual(6, context.StrategicPlan.GetPlanetaryGarrisonTarget(planet));
        }

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void GetPlanetaryGarrisonReserve_HighSupport_PreservesUncolonizedOwnership(
            bool colonized,
            int expected
        )
        {
            AITurnContext initial = CreatePoliticalFleetHoldContext(out Fleet fleet);
            Planet planet = fleet.GetParentOfType<Planet>();
            planet.IsColonized = colonized;
            planet.SetPopularSupport(initial.Faction.InstanceID, 100);
            planet.SetPopularSupport("rebels", 0);
            AITurnContext context = AITestSceneBuilder.CreateContext(initial.Game, initial.Faction);

            Assert.AreEqual(expected, context.StrategicPlan.GetPlanetaryGarrisonReserve(planet));
        }

        [Test]
        public void CanFleetDepart_ActiveUprising_DoesNotReleasePoliticalHold()
        {
            AITurnContext initial = CreatePoliticalFleetHoldContext(out Fleet fleet);
            Planet planet = fleet.GetParentOfType<Planet>();
            initial.Game.Config.AI.Garrison.GarrisonDivisor = 20;
            planet.BeginUprising();
            for (int index = 0; index < 10; index++)
                initial.Game.AttachNode(
                    AITestSceneBuilder.CreateRegiment(
                        "garrison-" + index,
                        initial.Faction.InstanceID
                    ),
                    planet
                );
            AITurnContext context = AITestSceneBuilder.CreateContext(initial.Game, initial.Faction);

            Assert.IsFalse(context.StrategicPlan.CanFleetDepart(fleet));
        }

        [Test]
        public void CanFleetDepart_StableHeadquartersGarrison_PreservesSpaceDefense()
        {
            AITurnContext initial = CreatePoliticalFleetHoldContext(out Fleet fleet);
            Planet planet = fleet.GetParentOfType<Planet>();
            planet.IsHeadquarters = true;
            initial.Faction.HQInstanceID = planet.InstanceID;
            initial.Game.Config.AI.Garrison.GarrisonDivisor = 20;
            initial.Game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            for (int index = 0; index < 3; index++)
                initial.Game.AttachNode(
                    AITestSceneBuilder.CreateRegiment(
                        "garrison-" + index,
                        initial.Faction.InstanceID
                    ),
                    planet
                );
            AITurnContext context = AITestSceneBuilder.CreateContext(initial.Game, initial.Faction);

            Assert.IsFalse(context.StrategicPlan.CanFleetDepart(fleet));
        }

        /// <summary>
        /// Creates separately allocated construction and shipyard hubs with configured primary targets.
        /// </summary>
        /// <param name="constructionCount">The committed construction-yard count.</param>
        /// <param name="shipyardCount">The committed shipyard count.</param>
        /// <param name="constructionHub">Receives the construction-hub planet.</param>
        /// <returns>The faction turn containing both industrial sites.</returns>
        private static AITurnContext CreateIndustrialDevelopmentContext(
            int constructionCount,
            int shipyardCount,
            out Planet constructionHub
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            game.Config.AI.Infrastructure.ConstructionFacilityPrimaryHubTargetCount = 5;
            game.Config.AI.Infrastructure.ShipyardPrimaryHubTargetCount = 6;
            game.Config.AI.Infrastructure.IndustrialShieldTargetCount = 0;
            game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "industrial-sector");
            constructionHub = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "construction-hub",
                faction.InstanceID,
                energyCapacity: 5
            );
            Planet shipyardHub = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "shipyard-hub",
                faction.InstanceID,
                energyCapacity: 6
            );
            constructionHub.SetPopularSupport(faction.InstanceID, 100);
            shipyardHub.SetPopularSupport(faction.InstanceID, 100);
            for (int index = 0; index < constructionCount; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    constructionHub,
                    "construction-" + index,
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            for (int index = 0; index < shipyardCount; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    shipyardHub,
                    "shipyard-" + index,
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            context.StrategicPlan.TryAllocateInfrastructureHub(
                constructionHub,
                BuildingType.ConstructionFacility,
                5
            );
            context.StrategicPlan.TryAllocateInfrastructureHub(
                shipyardHub,
                BuildingType.Shipyard,
                6
            );
            return context;
        }

        /// <summary>
        /// Creates a low-support planet whose loss exposes neighboring neutral worlds.
        /// </summary>
        /// <param name="fleet">Receives the stationary operational battle fleet.</param>
        /// <returns>The faction turn containing the political defense commitment.</returns>
        private static AITurnContext CreatePoliticalFleetHoldContext(out Fleet fleet)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            game.Config.AI.Garrison.SupportThreshold = 50;
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            game.Config.SupportShift.OwnershipTransferThreshold = 50;
            game.Config.SupportShift.GarrisonRemovalSupportShift = 10;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "held", faction.InstanceID);
            planet.SetPopularSupport(faction.InstanceID, 30);
            planet.SetPopularSupport(enemy.InstanceID, 70);
            for (int index = 0; index < 2; index++)
            {
                Planet neighbor = AITestSceneBuilder.AddPlanet(
                    game,
                    sector,
                    "neutral-" + index,
                    null
                );
                neighbor.SetPopularSupport(enemy.InstanceID, 45);
                AITestSceneBuilder.RevealPlanet(game, faction, neighbor);
            }
            fleet = EntityFactory.CreateFleet("fleet", faction.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("ship", faction.InstanceID),
                fleet
            );
            return AITestSceneBuilder.CreateContext(game, faction);
        }

        /// <summary>
        /// Creates a sector with a construction-hub shortage and a known neutral candidate.
        /// </summary>
        /// <param name="ownedSpace">The owned planet's building capacity.</param>
        /// <param name="candidateSpace">The neutral planet's building capacity.</param>
        /// <param name="candidate">The neutral acquisition candidate.</param>
        /// <returns>The faction's turn context.</returns>
        private static AITurnContext CreateIndustrialAcquisitionContext(
            int ownedSpace,
            int candidateSpace,
            out Planet candidate
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.AI.Infrastructure.ConstructionFacilityPrimaryHubTargetCount = 3;
            game.Config.AI.Infrastructure.IndustrialShieldTargetCount = 1;
            game.Config.AI.Infrastructure.ConstructionFacilitySecondaryHubTargetCount = 0;
            game.Config.AI.Infrastructure.ShipyardPrimaryHubTargetCount = 0;
            game.Config.AI.Infrastructure.ShipyardSecondaryHubTargetCount = 0;
            game.Config.AI.Infrastructure.TrainingFacilityHubTargetCount = 0;
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "industry-sector");
            AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "owned",
                faction.InstanceID,
                energyCapacity: ownedSpace
            );
            candidate = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "neutral",
                null,
                energyCapacity: candidateSpace
            );
            AITestSceneBuilder.RevealPlanet(game, faction, candidate);
            return AITestSceneBuilder.CreateContext(game, faction);
        }

        /// <summary>
        /// Creates a stationary headquarters defender with a required core and one spare hull.
        /// </summary>
        /// <param name="fleet">The defending fleet.</param>
        /// <param name="spare">The hull eligible for release.</param>
        /// <returns>The faction turn context.</returns>
        private static AITurnContext CreateDefender(out Fleet fleet, out CapitalShip spare)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "hq", faction.InstanceID);
            planet.IsHeadquarters = true;
            faction.HQInstanceID = planet.InstanceID;
            fleet = EntityFactory.CreateFleet("defender", faction.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = planet.InstanceID,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("anchor", faction.InstanceID, 1000),
                fleet
            );
            spare = AITestSceneBuilder.CreateCapitalShip("spare", faction.InstanceID, 500);
            game.AttachNode(spare, fleet);
            return AITestSceneBuilder.CreateContext(game, faction);
        }
    }
}
