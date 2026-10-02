using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class BombardmentCommandsTests : CombatTestBase
    {
        [Test]
        public void TryExecute_ValidCommand_PublishesCompletedResultBatch()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            BombardmentCommands system = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 1, 0, 10 })
            );
            IReadOnlyList<GameResult> publishedResults = null;
            system.ResultsProduced += results => publishedResults = results;

            BombardmentResult result = system.TryExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Military
            );

            Assert.IsNotNull(publishedResults);
            Assert.AreSame(result, publishedResults[0]);
            Assert.IsTrue(publishedResults.OfType<PlanetGarrisonChangedResult>().Any());
        }

        [Test]
        public void TryExecute_MilitaryBombardment_TargetsDefendersOnly()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Regiment regiment = AddRegiment(game, planet, "defender", "empire");
            Building mine = AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            CollectionAssert.Contains(result.DestroyedRegiments, regiment);
            CollectionAssert.DoesNotContain(result.DestroyedBuildings, mine);
            Assert.AreEqual("alliance", result.AttackerOwnerInstanceID);
            Assert.AreEqual("empire", result.DefenderOwnerInstanceID);
            CollectionAssert.Contains(
                result.AttackingUnits.Select(unit => unit.Unit.GetInstanceID()),
                fleet.GetChildren<CapitalShip>()[0].GetInstanceID()
            );
            CollectionAssert.Contains(
                result.DefendingUnits.Select(unit => unit.Unit.GetInstanceID()),
                regiment.GetInstanceID()
            );
            CollectionAssert.Contains(
                result.DefendingUnits.Select(unit => unit.Unit.GetInstanceID()),
                mine.GetInstanceID()
            );
            Assert.AreNotSame(
                fleet.GetChildren<CapitalShip>()[0],
                result
                    .AttackingUnits.Single(unit =>
                        unit.Unit.GetInstanceID()
                        == fleet.GetChildren<CapitalShip>()[0].GetInstanceID()
                    )
                    .Unit
            );
            Assert.AreSame(
                planet,
                result.Events.OfType<PlanetGarrisonChangedResult>().Single().Planet
            );
            Assert.AreEqual(10, planet.EnergyCapacity);
        }

        [Test]
        public void TryExecute_AttackingFleetWithWaypoints_ClearsRoute()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            fleet.Waypoints.Add("next-planet");

            MakeBombardment(game, new SequenceRNG(intValues: new[] { 1, 0, 10 }))
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.IsEmpty(fleet.Waypoints);
        }

        [Test]
        public void TryExecute_CivilianTargetDestroyed_DoesNotApplyPoliticalReaction()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSector sector) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.PopularSupport["alliance"] = 30;
            planet.PopularSupport["empire"] = 70;
            Planet secondPlanet = AddPlanet(game, sector, "p2", "empire");
            secondPlanet.PopularSupport["alliance"] = 30;
            secondPlanet.PopularSupport["empire"] = 70;
            AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 10 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.Civilian);

            Assert.AreEqual(30, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(30, secondPlanet.GetPopularSupport("alliance"));
            Assert.IsNull(result.OwnershipChange);
        }

        [Test]
        public void TryExecute_CivilianBombardment_AppliesCoreSupportPenalties()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSector planetSector) = CreatePlanet(
                game,
                "p1",
                "empire",
                energy: 10
            );
            planet.PopularSupport["alliance"] = 30;
            planet.PopularSupport["empire"] = 70;
            Planet secondPlanet = AddPlanet(game, planetSector, "p2", "empire");
            secondPlanet.PopularSupport["alliance"] = 30;
            secondPlanet.PopularSupport["empire"] = 70;
            Building mine = AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = TryExecuteBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0, 10 }),
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Civilian
            );

            CollectionAssert.Contains(result.DestroyedBuildings, mine);
            Assert.AreEqual(6, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(94, planet.GetPopularSupport("empire"));
            Assert.AreEqual(26, secondPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual(74, secondPlanet.GetPopularSupport("empire"));
        }

        [Test]
        public void TryExecute_CivilianBombardment_SupportFlipCarriesNotificationContext()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: null, energy: 10);
            planet.PopularSupport["alliance"] = 61;
            planet.PopularSupport["empire"] = 39;
            AddBuilding(game, planet, "mine", ownerId: null, BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = TryExecuteBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0, 10 }),
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Civilian
            );

            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            Assert.AreEqual(
                PlanetOwnershipChangeReason.PopularSupport,
                result.OwnershipChange.Reason
            );
            CollectionAssert.Contains(
                result.OwnershipChange.ObserverFactionInstanceIDs,
                "alliance"
            );
        }

        [Test]
        public void TryExecute_CivilianBombardment_ShiftsSectorSupport()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSector planetSector) = CreatePlanet(
                game,
                "p1",
                owner: null,
                energy: 10
            );
            planet.PopularSupport["alliance"] = 61;
            planet.PopularSupport["empire"] = 39;
            Planet secondPlanet = AddPlanet(game, planetSector, "p2", "empire");
            secondPlanet.PopularSupport["alliance"] = 30;
            secondPlanet.PopularSupport["empire"] = 70;
            AddBuilding(game, planet, "mine", ownerId: null, BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            TryExecuteBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0, 10 }),
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Civilian
            );

            Assert.AreEqual(25, secondPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual(75, secondPlanet.GetPopularSupport("empire"));
        }

        [Test]
        public void TryExecute_EmpireCivilianBombardment_HalvesEveryCorePenalty()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            planet.PopularSupport["alliance"] = 70;
            planet.PopularSupport["empire"] = 30;
            AddBuilding(game, planet, "mine", "alliance", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "empire", bombardment: 1);

            TryExecuteBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0, 10 }),
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Civilian
            );

            Assert.AreEqual(19, planet.GetPopularSupport("empire"));
            Assert.AreEqual(81, planet.GetPopularSupport("alliance"));
        }

        [TestCase("alliance", "empire", 8, 28)]
        [TestCase("empire", "alliance", 9, 29)]
        public void TryExecute_CivilianBombardment_AppliesOuterRimSupportPenalties(
            string attackerId,
            string defenderId,
            int expectedTargetSupport,
            int expectedPlanetSupport
        )
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSector planetSector) = CreatePlanet(
                game,
                "p1",
                defenderId,
                energy: 10
            );
            planetSector.SectorType = PlanetSectorType.OuterRim;
            planet.PopularSupport[attackerId] = 30;
            planet.PopularSupport[defenderId] = 70;
            Planet secondPlanet = AddPlanet(game, planetSector, "p2", defenderId);
            secondPlanet.PopularSupport[attackerId] = 30;
            secondPlanet.PopularSupport[defenderId] = 70;
            AddBuilding(game, planet, "mine", defenderId, BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, attackerId, bombardment: 1);

            TryExecuteBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0, 10 }),
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Civilian
            );

            Assert.AreEqual(expectedTargetSupport, planet.GetPopularSupport(attackerId));
            Assert.AreEqual(expectedPlanetSupport, secondPlanet.GetPopularSupport(attackerId));
        }

        [Test]
        public void TryExecute_GeneralBombardment_CanDamageBothEnergyPools()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            planet.AllocatedEnergy = 1;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 2);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 10, 0, 10 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            Assert.AreEqual(1, result.EnergyCapacityDamage);
            Assert.AreEqual(1, result.AllocatedEnergyDamage);
            Assert.Zero(planet.EnergyCapacity);
            Assert.Zero(planet.AllocatedEnergy);
        }

        [Test]
        public void TryExecute_TwoPlanetaryShields_AbsorbTheirFullCombinedStrength()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            AddRegiment(game, planet, "defender", "empire");
            Building firstShield = AddBuilding(
                game,
                planet,
                "shield-1",
                "empire",
                BuildingType.Defense
            );
            firstShield.ShieldStrength = 40;
            Building secondShield = AddBuilding(
                game,
                planet,
                "shield-2",
                "empire",
                BuildingType.Defense
            );
            secondShield.ShieldStrength = 40;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 5);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.AreEqual(5, result.BombardmentStrength);
            Assert.AreEqual(80, result.ShieldStrength);
            Assert.Zero(result.StrikeAttempts);
        }

        [Test]
        public void TryExecute_DeathStarShield_DoesNotReduceBombardment()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building shield = AddBuilding(
                game,
                planet,
                "death-star-shield",
                "empire",
                BuildingType.Defense
            );
            shield.ProtectedUnitTypeIDs.Add("CSEM015");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.Zero(result.ShieldStrength);
            Assert.AreEqual(1, result.StrikeAttempts);
        }

        [Test]
        public void TryExecute_DamagedShipAndFighter_UseEffectiveBombardment()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 10,
                currentHull: 50
            );
            CapitalShip ship = fleet.GetChildren<CapitalShip>()[0];
            ship.StarfighterCapacity = 1;
            Starfighter fighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "alliance",
                Bombardment = 10,
                MaxSquadronSize = 10,
                CurrentSquadronSize = 5,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fighter, ship);
            Officer admiral = new Officer
            {
                InstanceID = "admiral",
                OwnerInstanceID = "alliance",
                CurrentRank = OfficerRank.Admiral,
            };
            admiral.SetBaseRating(SkillRating.Leadership, 40);
            game.AttachNode(admiral, ship);

            BombardmentResult result = MakeBombardment(game, new SequenceRNG())
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            Assert.AreEqual(20, result.BombardmentStrength);
        }

        [Test]
        public void TryExecute_KdyAndLnr_ResolveShieldBeforeHullDamage()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building lnr = AddBuilding(game, planet, "lnr", "empire", BuildingType.Weapon);
            lnr.WeaponPower = 30;
            Building kdy = AddBuilding(game, planet, "kdy", "empire", BuildingType.Weapon);
            kdy.DefenseWeaponEffect = DefenseWeaponEffect.ShieldDamage;
            kdy.WeaponPower = 30;
            Officer captive = new Officer
            {
                InstanceID = "captive",
                OwnerInstanceID = "alliance",
                IsMain = true,
                IsCaptured = true,
                CurrentRank = OfficerRank.General,
            };
            captive.SetBaseRating(SkillRating.Leadership, 400);
            game.AttachNode(captive, planet);
            Officer general = AddOfficer(game, planet, "general", "empire", isMain: true);
            general.CurrentRank = OfficerRank.General;
            general.SetBaseRating(SkillRating.Leadership, 40);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            CapitalShip ship = fleet.GetChildren<CapitalShip>()[0];
            ship.MaxShieldStrength = 100;

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 0 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            Assert.AreEqual(80, ship.CurrentHullStrength);
            Assert.AreEqual(1, result.AttackerShipDamage.Count);
            Assert.AreEqual(
                20,
                result.AttackerShipDamage[0].HullBefore - result.AttackerShipDamage[0].HullAfter
            );
        }

        [Test]
        public void TryExecute_DefenseFire_DeterminesSurvivingBombardmentStrength()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            Building lnr = AddBuilding(game, planet, "lnr", "empire", BuildingType.Weapon);
            lnr.WeaponPower = 100;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            CapitalShip destroyedShip = fleet.GetChildren<CapitalShip>()[0];
            CapitalShip survivingShip = new CapitalShip
            {
                InstanceID = "survivor",
                OwnerInstanceID = "alliance",
                Bombardment = 1,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(survivingShip, fleet);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 1, 10 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            CollectionAssert.Contains(result.DestroyedCapitalShips, destroyedShip);
            Assert.AreEqual(1, result.BombardmentStrength);
            Assert.AreEqual(1, result.StrikeAttempts);
            Assert.AreEqual(1, result.EnergyCapacityDamage);
        }

        [Test]
        public void TryExecute_GroundCannonDestroysCarrier_DestroysEmbarkedCombatUnits()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            CreatePlanet(game, "fallback", "alliance", energy: 1);
            Building groundCannon = AddBuilding(
                game,
                planet,
                "ground-cannon",
                "empire",
                BuildingType.Weapon
            );
            groundCannon.WeaponPower = 100;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            CapitalShip carrier = fleet.GetChildren<CapitalShip>().Single();
            carrier.StarfighterCapacity = 1;
            carrier.RegimentCapacity = 1;
            Starfighter starfighter = EntityFactory.CreateStarfighter(
                "embarked-starfighter",
                "alliance"
            );
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            starfighter.Hyperdrive = 1;
            Regiment regiment = EntityFactory.CreateRegiment("embarked-regiment", "alliance");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "embarked-special-forces",
                OwnerInstanceID = "alliance",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(starfighter, carrier);
            game.AttachNode(regiment, carrier);
            game.AttachNode(specialForces, carrier);

            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            GameResultBus resultBus = new GameResultBus();
            new MovementObserver(movement).Connect(resultBus);
            BombardmentCommands system = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0 })
            );
            system.ResultsProduced += results => resultBus.Publish(results);

            BombardmentResult result = system.TryExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.General
            );

            CollectionAssert.Contains(result.DestroyedCapitalShips, carrier);
            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Starfighter>(
                    starfighter.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID, includeDisabled: true)
            );
            Assert.IsNull(
                game.GetSceneNodeByInstanceID<SpecialForces>(
                    specialForces.InstanceID,
                    includeDisabled: true
                )
            );
            CollectionAssert.Contains(
                result
                    .Events.OfType<GameObjectDestroyedResult>()
                    .Select(destruction => destruction.DestroyedObject)
                    .ToList(),
                specialForces
            );
        }

        [Test]
        public void TryExecute_DefenseFireDestroysCarrier_RelocatesInactiveOfficer()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            (Planet fallback, _) = CreatePlanet(game, "fallback", "alliance", energy: 1);
            Building lnr = AddBuilding(game, planet, "lnr", "empire", BuildingType.Weapon);
            lnr.WeaponPower = 100;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            CapitalShip carrier = fleet.GetChildren<CapitalShip>().Single();
            Officer officer = new Officer
            {
                InstanceID = "officer",
                OwnerInstanceID = "alliance",
                IsEnabled = false,
            };
            game.AttachNode(officer, carrier);
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            GameResultBus resultBus = new GameResultBus();
            new MovementObserver(movement).Connect(resultBus);
            BombardmentCommands system = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0 })
            );
            system.ResultsProduced += results => resultBus.Publish(results);

            system.TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            Assert.AreSame(fallback, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
            Assert.IsFalse(officer.IsEnabled);
        }

        [Test]
        public void TryExecute_DefenseFireDestroysCarrier_RelocatesActiveOfficer()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            (Planet fallback, _) = CreatePlanet(game, "fallback", "alliance", energy: 1);
            Building lnr = AddBuilding(game, planet, "lnr", "empire", BuildingType.Weapon);
            lnr.WeaponPower = 100;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            CapitalShip carrier = fleet.GetChildren<CapitalShip>().Single();
            Officer officer = new Officer { InstanceID = "officer", OwnerInstanceID = "alliance" };
            game.AttachNode(officer, carrier);
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            GameResultBus resultBus = new GameResultBus();
            new MovementObserver(movement).Connect(resultBus);
            BombardmentCommands system = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0 })
            );
            system.ResultsProduced += results => resultBus.Publish(results);

            system.TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            Assert.AreSame(
                officer,
                game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID, includeDisabled: true)
            );
            Assert.AreSame(fallback, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
            Assert.IsTrue(officer.IsEnabled);
        }

        [Test]
        public void TryExecute_StrikeResistance_MustBeLowerThanRoll()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building mine = AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            mine.Bombardment = 9;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 2);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 9, 0, 10 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.Civilian);

            Assert.AreEqual(1, result.SuccessfulStrikes);
            CollectionAssert.Contains(result.DestroyedBuildings, mine);
        }

        [Test]
        public void TryExecute_MilitaryCollateral_CanDestroyCivilianTarget()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Regiment regiment = AddRegiment(game, planet, "defender", "empire");
            Building mine = AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = TryExecuteBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0, 0, 10, 0, 10 }),
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Military
            );

            CollectionAssert.Contains(result.DestroyedRegiments, regiment);
            CollectionAssert.Contains(result.DestroyedBuildings, mine);
            Assert.AreEqual(2, result.SuccessfulStrikes);
            Assert.Less(planet.GetPopularSupport("alliance"), 50);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            Assert.IsNull(result.OwnershipChange);
        }

        [Test]
        public void TryExecute_AllianceHeadquarters_CanBeDestroyed()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            planet.IsHeadquarters = true;
            game.GetFactionByOwnerInstanceID("alliance").HQInstanceID = planet.InstanceID;
            Fleet fleet = AddBombardmentFleet(game, planet, "empire", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.IsTrue(result.HeadquartersDestroyed);
            Assert.IsFalse(planet.IsHeadquarters);
            Assert.IsNull(game.GetFactionByOwnerInstanceID("alliance").HQInstanceID);
            Assert.IsFalse(planet.IsDestroyed);
        }

        [Test]
        public void TryExecute_EmpireHeadquarters_IsNotAMilitaryTarget()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.IsHeadquarters = true;
            game.GetFactionByOwnerInstanceID("empire").HQInstanceID = planet.InstanceID;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(game, new SequenceRNG())
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.IsFalse(result.HeadquartersDestroyed);
            Assert.IsTrue(planet.IsHeadquarters);
            Assert.Zero(result.SuccessfulStrikes);
        }

        [Test]
        public void TryExecute_DestroyPlanetWithDeathStar_DestroysPlanetAndMinorPersonnel()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSector planetSector) = CreatePlanet(
                game,
                "p1",
                "empire",
                energy: 10
            );
            planet.PopularSupport["alliance"] = 30;
            planet.PopularSupport["empire"] = 70;
            Planet secondPlanet = AddPlanet(game, planetSector, "p2", "empire");
            secondPlanet.PopularSupport["alliance"] = 30;
            secondPlanet.PopularSupport["empire"] = 70;
            Officer minor = AddOfficer(game, planet, "minor", "empire", isMain: false);
            Officer main = AddOfficer(game, planet, "main", "empire", isMain: true);
            Officer killedMinor = AddOfficer(game, planet, "killed-minor", "empire", isMain: false);
            killedMinor.IsKilled = true;
            killedMinor.InjuryPoints = 2;
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 0,
                typeId: "CSEM015"
            );
            fleet.GetChildren<CapitalShip>()[0].CanDestroyPlanets = true;

            BombardmentResult result = TryExecuteBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0, 0 }),
                new List<Fleet> { fleet },
                planet,
                BombardmentType.DestroyPlanet
            );

            Assert.IsTrue(result.PlanetDestroyed);
            Assert.IsTrue(planet.IsDestroyed);
            Assert.IsTrue(minor.IsKilled);
            Assert.AreSame(planet, minor.GetParent());
            Assert.IsFalse(minor.IsActive());
            Assert.AreSame(
                minor,
                game.GetSceneNodeByInstanceID<Officer>(minor.InstanceID, includeDisabled: true)
            );
            Assert.IsFalse(main.IsKilled);
            Assert.AreEqual(2, killedMinor.InjuryPoints);
            Assert.AreEqual(planet, killedMinor.GetParent());
            Assert.AreEqual(10, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(20, secondPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual(1, result.Events.OfType<OfficerInjuredResult>().Count());
            Assert.AreEqual(1, result.Events.OfType<OfficerKilledResult>().Count());
            Assert.IsEmpty(result.Events.OfType<OfficerAssassinatedResult>());
        }

        [Test]
        public void TryExecute_DestroyPlanetMinorPersonnelSurvivesDeathRoll_RemainsInjured()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Officer minor = AddOfficer(game, planet, "minor", "empire", isMain: false);
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 0,
                typeId: "planet-destroyer"
            );
            fleet.GetChildren<CapitalShip>()[0].CanDestroyPlanets = true;

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 99 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.DestroyPlanet);

            Assert.AreEqual(1, minor.InjuryPoints);
            Assert.IsFalse(minor.IsKilled);
            Assert.AreSame(planet, minor.GetParent());
            Assert.AreEqual(1, result.Events.OfType<OfficerInjuredResult>().Count());
            Assert.IsEmpty(result.Events.OfType<OfficerKilledResult>());
        }

        [Test]
        public void TryExecute_DestroyPlanet_DefenseFireCannotPreventDestruction()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building lnr = AddBuilding(game, planet, "lnr", "empire", BuildingType.Weapon);
            lnr.WeaponPower = 100;
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 0,
                typeId: "CSEM015"
            );
            CapitalShip deathStar = fleet.GetChildren<CapitalShip>()[0];
            deathStar.CanDestroyPlanets = true;

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0 })
                )
                .TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.DestroyPlanet);

            Assert.IsTrue(result.PlanetDestroyed);
            Assert.IsTrue(planet.IsDestroyed);
            CollectionAssert.Contains(result.DestroyedCapitalShips, deathStar);
            Assert.Zero(result.StrikeAttempts);
        }

        [Test]
        public void TryExecute_DestroyPlanet_PenalizesOuterRimSupportBelowThreshold()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSector planetSector) = CreatePlanet(
                game,
                "p1",
                "empire",
                energy: 10
            );
            planetSector.SectorType = PlanetSectorType.OuterRim;
            planet.PopularSupport["alliance"] = 30;
            planet.PopularSupport["empire"] = 70;
            (Planet lowSupportPlanet, PlanetSector affectedSector) = CreatePlanet(
                game,
                "p2",
                "alliance"
            );
            affectedSector.SectorType = PlanetSectorType.OuterRim;
            lowSupportPlanet.PopularSupport["alliance"] = 89;
            lowSupportPlanet.PopularSupport["empire"] = 11;
            Planet thresholdPlanet = AddPlanet(game, affectedSector, "p3", "alliance");
            thresholdPlanet.PopularSupport["alliance"] = 90;
            thresholdPlanet.PopularSupport["empire"] = 10;
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 0,
                typeId: "CSEM015"
            );
            fleet.GetChildren<CapitalShip>()[0].CanDestroyPlanets = true;

            TryExecuteBombardment(
                game,
                new SequenceRNG(),
                new List<Fleet> { fleet },
                planet,
                BombardmentType.DestroyPlanet
            );

            Assert.AreEqual(87, lowSupportPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual(90, thresholdPlanet.GetPopularSupport("alliance"));
        }

        [Test]
        public void TryExecute_DestroyPlanetWithoutDeathStar_DoesNotBombard()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            BombardmentCommands system = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0, 10 })
            );

            BombardmentResult result = system.TryExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.DestroyPlanet
            );

            Assert.IsNull(result);
            Assert.AreEqual(1, planet.EnergyCapacity);
        }

        [Test]
        public void TryExecute_DestroyedGarrison_ReportsGarrisonRemovedOwnershipChange()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSector planetSector) = CreatePlanet(
                game,
                "p1",
                "empire",
                energy: 10
            );
            planet.PopularSupport["alliance"] = 60;
            planet.PopularSupport["empire"] = 40;
            Planet secondPlanet = AddPlanet(game, planetSector, "p2", ownerId: null);
            Regiment regiment = AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentCommands commands = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 1, 0, 10 })
            );
            GameResultBus results = new GameResultBus();
            ConnectPlanetaryControl(game, results);
            commands.ResultsProduced += produced => results.Publish(produced);

            BombardmentResult result = commands.TryExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Military
            );

            CollectionAssert.Contains(result.DestroyedRegiments, regiment);
            Assert.AreEqual("alliance", planet.GetOwnerInstanceID());
            Assert.AreEqual(65, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(55, secondPlanet.GetPopularSupport("alliance"));
            Assert.IsNull(secondPlanet.GetOwnerInstanceID());
            Assert.IsEmpty(result.Events.OfType<PlanetOwnershipChangedResult>());
            Assert.AreEqual("empire", result.OwnershipChange.PreviousOwner.InstanceID);
            Assert.AreEqual("alliance", result.OwnershipChange.NewOwner.InstanceID);
            Assert.AreEqual(
                PlanetOwnershipChangeReason.GarrisonRemoved,
                result.OwnershipChange.Reason
            );
        }

        [Test]
        public void TryExecute_DestroyedGarrison_CanLeavePlanetNeutral()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSector planetSector) = CreatePlanet(
                game,
                "p1",
                "empire",
                energy: 10
            );
            planet.PopularSupport["alliance"] = 49;
            planet.PopularSupport["empire"] = 51;
            Planet secondPlanet = AddPlanet(game, planetSector, "p2", "empire");
            secondPlanet.PopularSupport["alliance"] = 20;
            secondPlanet.PopularSupport["empire"] = 80;
            AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentCommands commands = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 1, 0, 10 })
            );
            GameResultBus results = new GameResultBus();
            ConnectPlanetaryControl(game, results);
            commands.ResultsProduced += produced => results.Publish(produced);

            BombardmentResult result = commands.TryExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Military
            );

            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.AreEqual(54, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(25, secondPlanet.GetPopularSupport("alliance"));
            Assert.IsNull(result.OwnershipChange.NewOwner);
            Assert.AreEqual(
                PlanetOwnershipChangeReason.GarrisonRemoved,
                result.OwnershipChange.Reason
            );
        }

        [Test]
        public void TryExecute_DestroyedGarrison_ReportsNeutralOwnershipChange()
        {
            GameRoot game = CreateGame();
            game.Config.SupportShift.GarrisonRemovalSupportShift = 0;
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentCommands commands = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 1, 0, 10 })
            );
            GameResultBus results = new GameResultBus();
            ConnectPlanetaryControl(game, results);
            commands.ResultsProduced += produced => results.Publish(produced);

            BombardmentResult result = commands.TryExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Military
            );

            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.AreEqual(50, planet.GetPopularSupport("alliance"));
            Assert.AreEqual("empire", result.OwnershipChange.PreviousOwner.InstanceID);
            Assert.IsNull(result.OwnershipChange.NewOwner);
            Assert.AreEqual(
                PlanetOwnershipChangeReason.GarrisonRemoved,
                result.OwnershipChange.Reason
            );
        }

        [Test]
        public void TryExecute_GarrisonReactionChangesControlAgain_PreservesEventsAndSummarizesReport()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSector sector) = CreatePlanet(game, "p1", "empire", energy: 10);
            sector.SectorType = PlanetSectorType.OuterRim;
            AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            BombardmentCommands commands = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 1, 0, 10 })
            );
            GameResultBus results = new GameResultBus();
            ConnectPlanetaryControl(game, results);
            IReadOnlyList<GameResult> settled = null;
            commands.ResultsProduced += produced => settled = results.Publish(produced);

            BombardmentResult result = commands.TryExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Military
            );

            List<PlanetOwnershipChangedResult> ownershipChanges = settled
                .OfType<PlanetOwnershipChangedResult>()
                .Where(change => change.Planet == planet)
                .ToList();
            Assert.AreEqual(2, ownershipChanges.Count);
            Assert.AreEqual("empire", ownershipChanges[0].PreviousOwner.InstanceID);
            Assert.IsNull(ownershipChanges[0].NewOwner);
            Assert.IsNull(ownershipChanges[1].PreviousOwner);
            Assert.AreEqual("alliance", ownershipChanges[1].NewOwner.InstanceID);
            Assert.AreEqual("empire", result.OwnershipChange.PreviousOwner.InstanceID);
            Assert.AreEqual("alliance", result.OwnershipChange.NewOwner.InstanceID);
        }

        [Test]
        public void TryExecute_RemoteOrMixedFleets_DoNotAttack()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            (Planet remote, _) = CreatePlanet(game, "p2", "empire", energy: 10);
            Fleet remoteFleet = AddBombardmentFleet(game, remote, "alliance", bombardment: 10);
            Fleet localEnemyFleet = AddBombardmentFleet(game, planet, "empire", bombardment: 10);

            BombardmentResult remoteResult = MakeBombardment(game, new SequenceRNG())
                .TryExecute(new List<Fleet> { remoteFleet }, planet, BombardmentType.General);
            BombardmentResult mixedResult = MakeBombardment(game, new SequenceRNG())
                .TryExecute(
                    new List<Fleet> { remoteFleet, localEnemyFleet },
                    planet,
                    BombardmentType.General
                );

            Assert.IsNull(remoteResult);
            Assert.IsNull(mixedResult);
        }

        [Test]
        public void TryExecute_NoBombardmentStrength_ReturnsNullWithoutPublishing()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "target", "empire", energy: 1);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 0);
            BombardmentCommands system = MakeBombardment(game, new ThrowingRNG());
            int publications = 0;
            system.ResultsProduced += _ => publications++;

            BombardmentResult result = system.TryExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.General
            );

            Assert.IsNull(result);
            Assert.Zero(publications);
            Assert.AreEqual(1, planet.EnergyCapacity);
        }

        [Test]
        public void TryExecute_ListenerThrows_PreservesCompletedBombardment()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "target", "empire", energy: 1);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            BombardmentCommands system = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0, 10 })
            );
            InvalidOperationException failure = new InvalidOperationException("Listener failure");
            system.ResultsProduced += _ => throw failure;

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                system.TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.General)
            );

            Assert.AreSame(failure, thrown);
            Assert.Zero(planet.EnergyCapacity);
            Assert.IsFalse(fleet.IsInCombat);
        }

        [Test]
        public void TryExecute_RandomProviderThrows_ClearsCombatState()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "target", "empire", energy: 1);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            fleet.Waypoints.Add("next-planet");
            BombardmentCommands system = MakeBombardment(game, new ThrowingRNG());

            Assert.Throws<InvalidOperationException>(() =>
                system.TryExecute(new List<Fleet> { fleet }, planet, BombardmentType.General)
            );

            Assert.IsFalse(fleet.IsInCombat);
            Assert.IsEmpty(fleet.Waypoints);
            Assert.AreEqual(1, planet.EnergyCapacity);
        }

        [Test]
        public void TryExecute_NullFleetAlongsideReadyFleet_ExecutesBombardment()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "target", "empire", energy: 1);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 10 })
                )
                .TryExecute(new List<Fleet> { null, fleet }, planet, BombardmentType.General);

            Assert.AreEqual(1, result.EnergyCapacityDamage);
        }

        [Test]
        public void TryExecute_GarrisonDestroyed_PublishesOrderedCompletedBatch()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 42;
            game.Config.SupportShift.GarrisonRemovalSupportShift = 0;
            (Planet planet, _) = CreatePlanet(game, "target", "empire", energy: 10);
            AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            BombardmentCommands system = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 1, 0, 10 })
            );
            GameResultBus resultBus = new GameResultBus();
            ConnectPlanetaryControl(game, resultBus);
            IReadOnlyList<GameResult> published = null;
            IReadOnlyList<GameResult> settled = null;
            bool inCombatAtPublication = true;
            system.ResultsProduced += results =>
            {
                published = results;
                inCombatAtPublication = fleet.IsInCombat;
                settled = resultBus.Publish(results);
            };

            BombardmentResult result = system.TryExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.Military
            );

            Assert.IsNotNull(result.OwnershipChange);
            PlanetOwnershipChangedResult ownershipChange = settled
                .OfType<PlanetOwnershipChangedResult>()
                .Single();
            CollectionAssert.AreEqual(new GameResult[] { result }.Concat(result.Events), published);
            CollectionAssert.AreEqual(published.Append(ownershipChange), settled);
            Assert.AreEqual(ownershipChange.PreviousOwner, result.OwnershipChange.PreviousOwner);
            Assert.AreEqual(ownershipChange.NewOwner, result.OwnershipChange.NewOwner);
            Assert.AreEqual(ownershipChange.Reason, result.OwnershipChange.Reason);
            Assert.AreEqual(42, result.Tick);
            Assert.IsFalse(inCombatAtPublication);
        }

        /// <summary>
        /// Executes bombardment with the production planetary-control reactions connected.
        /// </summary>
        /// <param name="game">The game containing the combatants.</param>
        /// <param name="provider">The deterministic random-number provider.</param>
        /// <param name="fleets">The fleets performing the bombardment.</param>
        /// <param name="planet">The bombardment target.</param>
        /// <param name="type">The bombardment target profile.</param>
        /// <returns>The completed bombardment result.</returns>
        private BombardmentResult TryExecuteBombardment(
            GameRoot game,
            IRandomNumberProvider provider,
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            BombardmentType type
        )
        {
            BombardmentCommands commands = MakeBombardment(game, provider);
            GameResultBus results = new GameResultBus();
            ConnectPlanetaryControl(game, results);
            commands.ResultsProduced += produced => results.Publish(produced);
            return commands.TryExecute(fleets, planet, type);
        }

        /// <summary>
        /// Adds bombardment fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <param name="bombardment">The bombardment.</param>
        /// <param name="currentHull">The current hull.</param>
        /// <param name="typeId">The type id.</param>
        /// <returns>The result of add bombardment fleet.</returns>
        private static Fleet AddBombardmentFleet(
            GameRoot game,
            Planet planet,
            string ownerId,
            int bombardment,
            int currentHull = 100,
            string typeId = null
        )
        {
            Fleet fleet = new Fleet
            {
                InstanceID = Guid.NewGuid().ToString(),
                OwnerInstanceID = ownerId,
            };
            game.AttachNode(fleet, planet);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = Guid.NewGuid().ToString(),
                TypeID = typeId,
                OwnerInstanceID = ownerId,
                Bombardment = bombardment,
                MaxHullStrength = 100,
                CurrentHullStrength = currentHull,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);
            return fleet;
        }

        /// <summary>
        /// Adds building.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="id">The id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <param name="type">The type.</param>
        /// <param name="weaponEffect">The weapon effect.</param>
        /// <returns>The result of add building.</returns>
        private static Building AddBuilding(
            GameRoot game,
            Planet planet,
            string id,
            string ownerId,
            BuildingType type,
            DefenseWeaponEffect weaponEffect = DefenseWeaponEffect.HullDamage
        )
        {
            Building building = new Building
            {
                InstanceID = id,
                OwnerInstanceID = ownerId,
                BuildingType = type,
                DefenseWeaponEffect = weaponEffect,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            return building;
        }

        /// <summary>
        /// Adds regiment.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="id">The id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>The result of add regiment.</returns>
        private static Regiment AddRegiment(GameRoot game, Planet planet, string id, string ownerId)
        {
            Regiment regiment = new Regiment
            {
                InstanceID = id,
                OwnerInstanceID = ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planet);
            return regiment;
        }

        /// <summary>
        /// Adds officer.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="id">The id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <param name="isMain">Whether is main.</param>
        /// <returns>The result of add officer.</returns>
        private static Officer AddOfficer(
            GameRoot game,
            Planet planet,
            string id,
            string ownerId,
            bool isMain
        )
        {
            Officer officer = new Officer
            {
                InstanceID = id,
                OwnerInstanceID = ownerId,
                IsMain = isMain,
            };
            game.AttachNode(officer, planet);
            return officer;
        }

        /// <summary>
        /// Adds planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planetSector">The planet sector.</param>
        /// <param name="id">The id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>The result of add planet.</returns>
        private static Planet AddPlanet(
            GameRoot game,
            PlanetSector planetSector,
            string id,
            string ownerId
        )
        {
            Planet planet = new Planet
            {
                InstanceID = id,
                OwnerInstanceID = ownerId,
                IsColonized = true,
                EnergyCapacity = 10,
                PopularSupport = new Dictionary<string, int>
                {
                    { "empire", 50 },
                    { "alliance", 50 },
                },
            };
            game.AttachNode(planet, planetSector);
            return planet;
        }
    }
}
