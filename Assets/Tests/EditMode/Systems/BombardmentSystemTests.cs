using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class BombardmentSystemTests : CombatTestBase
    {
        [Test]
        public void CanExecute_NeutralPlanetWithActiveCapitalShip_ReturnsTrue()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: null, energy: 10);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            bool canExecute = MakeBombardment(game, new SequenceRNG())
                .CanExecute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            Assert.IsTrue(canExecute);
        }

        [TestCase(BombardmentType.Military)]
        [TestCase(BombardmentType.Civilian)]
        [TestCase(BombardmentType.General)]
        public void CanExecute_OrdinaryBombardmentWithoutEffectiveStrength_ReturnsFalse(
            BombardmentType type
        )
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 0);

            bool canExecute = MakeBombardment(game, new SequenceRNG())
                .CanExecute(new List<Fleet> { fleet }, planet, type);

            Assert.IsFalse(canExecute);
        }

        [Test]
        public void CanExecute_EmbarkedFighterSuppliesBombardmentStrength_ReturnsTrue()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 0);
            CapitalShip ship = fleet.CapitalShips[0];
            ship.StarfighterCapacity = 1;
            game.AttachNode(
                new Starfighter
                {
                    InstanceID = "fighter",
                    OwnerInstanceID = "alliance",
                    Bombardment = 1,
                    MaxSquadronSize = 1,
                    CurrentSquadronSize = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                ship
            );

            bool canExecute = MakeBombardment(game, new SequenceRNG())
                .CanExecute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            Assert.IsTrue(canExecute);
        }

        [Test]
        public void Execute_MilitaryBombardment_TargetsDefendersOnly()
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
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            CollectionAssert.Contains(result.DestroyedRegiments, regiment);
            CollectionAssert.DoesNotContain(result.DestroyedBuildings, mine);
            Assert.AreEqual("alliance", result.AttackerOwnerInstanceID);
            Assert.AreEqual("empire", result.DefenderOwnerInstanceID);
            CollectionAssert.Contains(
                result.AttackingUnits.Select(unit => unit.Unit.GetInstanceID()),
                fleet.CapitalShips[0].GetInstanceID()
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
                fleet.CapitalShips[0],
                result
                    .AttackingUnits.Single(unit =>
                        unit.Unit.GetInstanceID() == fleet.CapitalShips[0].GetInstanceID()
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
        public void Execute_CivilianBombardment_AppliesCoreSupportPenalties()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.PopularSupport["alliance"] = 30;
            planet.PopularSupport["empire"] = 70;
            Planet secondPlanet = AddPlanet(game, system, "p2", "empire");
            secondPlanet.PopularSupport["alliance"] = 30;
            secondPlanet.PopularSupport["empire"] = 70;
            Building mine = AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 10 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Civilian);

            CollectionAssert.Contains(result.DestroyedBuildings, mine);
            Assert.AreEqual(6, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(94, planet.GetPopularSupport("empire"));
            Assert.AreEqual(26, secondPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual(74, secondPlanet.GetPopularSupport("empire"));
        }

        [Test]
        public void Execute_CivilianBombardment_SupportFlipCarriesNotificationContext()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: null, energy: 10);
            planet.PopularSupport["alliance"] = 61;
            planet.PopularSupport["empire"] = 39;
            AddBuilding(game, planet, "mine", ownerId: null, BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 10 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Civilian);

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
        public void Execute_EmpireCivilianBombardment_HalvesCoreTargetPenalty()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            planet.PopularSupport["alliance"] = 70;
            planet.PopularSupport["empire"] = 30;
            AddBuilding(game, planet, "mine", "alliance", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "empire", bombardment: 1);

            MakeBombardment(game, new SequenceRNG(intValues: new[] { 0, 10 }))
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Civilian);

            Assert.AreEqual(17, planet.GetPopularSupport("empire"));
            Assert.AreEqual(83, planet.GetPopularSupport("alliance"));
        }

        [TestCase("alliance", "empire", 8, 28)]
        [TestCase("empire", "alliance", 9, 29)]
        public void Execute_CivilianBombardment_AppliesOuterRimSupportPenalties(
            string attackerId,
            string defenderId,
            int expectedTargetSupport,
            int expectedSystemSupport
        )
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", defenderId, energy: 10);
            system.SystemType = PlanetSystemType.OuterRim;
            planet.PopularSupport[attackerId] = 30;
            planet.PopularSupport[defenderId] = 70;
            Planet secondPlanet = AddPlanet(game, system, "p2", defenderId);
            secondPlanet.PopularSupport[attackerId] = 30;
            secondPlanet.PopularSupport[defenderId] = 70;
            AddBuilding(game, planet, "mine", defenderId, BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, attackerId, bombardment: 1);

            MakeBombardment(game, new SequenceRNG(intValues: new[] { 0, 10 }))
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Civilian);

            Assert.AreEqual(expectedTargetSupport, planet.GetPopularSupport(attackerId));
            Assert.AreEqual(expectedSystemSupport, secondPlanet.GetPopularSupport(attackerId));
        }

        [Test]
        public void Execute_GeneralBombardment_CanDamageBothEnergyPools()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            planet.AllocatedEnergy = 1;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 2);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 10, 0, 10 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            Assert.AreEqual(1, result.EnergyCapacityDamage);
            Assert.AreEqual(1, result.AllocatedEnergyDamage);
            Assert.Zero(planet.EnergyCapacity);
            Assert.Zero(planet.AllocatedEnergy);
        }

        [Test]
        public void Execute_ShieldsSubtractFromStrikeAttempts()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            AddRegiment(game, planet, "defender", "empire");
            Building shield = AddBuilding(
                game,
                planet,
                "shield",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.Shield
            );
            shield.ShieldStrength = 2;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 3);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.AreEqual(3, result.BombardmentStrength);
            Assert.AreEqual(2, result.ShieldStrength);
            Assert.AreEqual(1, result.StrikeAttempts);
        }

        [Test]
        public void Execute_DeathStarShield_DoesNotReduceBombardment()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building shield = AddBuilding(
                game,
                planet,
                "death-star-shield",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.DeathStarShield
            );
            shield.ShieldStrength = 100;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.Zero(result.ShieldStrength);
            Assert.AreEqual(1, result.StrikeAttempts);
        }

        [Test]
        public void Execute_DamagedShipAndFighter_UseEffectiveBombardment()
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
            CapitalShip ship = fleet.CapitalShips[0];
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
            admiral.SetBaseRating(OfficerRating.Leadership, 40);
            game.AttachNode(admiral, ship);

            BombardmentResult result = MakeBombardment(game, new SequenceRNG())
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            Assert.AreEqual(20, result.BombardmentStrength);
        }

        [Test]
        public void Execute_KdyAndLnr_ResolveShieldBeforeHullDamage()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building lnr = AddBuilding(
                game,
                planet,
                "lnr",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.LNR
            );
            lnr.WeaponPower = 30;
            Building kdy = AddBuilding(
                game,
                planet,
                "kdy",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.KDY
            );
            kdy.WeaponPower = 30;
            Officer captive = new Officer
            {
                InstanceID = "captive",
                OwnerInstanceID = "alliance",
                IsMain = true,
                IsCaptured = true,
                CurrentRank = OfficerRank.General,
            };
            captive.SetBaseRating(OfficerRating.Leadership, 400);
            game.AttachNode(captive, planet);
            Officer general = AddOfficer(game, planet, "general", "empire", isMain: true);
            general.CurrentRank = OfficerRank.General;
            general.SetBaseRating(OfficerRating.Leadership, 40);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            CapitalShip ship = fleet.CapitalShips[0];
            ship.MaxShieldStrength = 100;

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 0 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            Assert.AreEqual(80, ship.CurrentHullStrength);
            Assert.AreEqual(1, result.AttackerShipDamage.Count);
            Assert.AreEqual(
                20,
                result.AttackerShipDamage[0].HullBefore - result.AttackerShipDamage[0].HullAfter
            );
        }

        [Test]
        public void Execute_DefenseFire_DeterminesSurvivingBombardmentStrength()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            Building lnr = AddBuilding(
                game,
                planet,
                "lnr",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.LNR
            );
            lnr.WeaponPower = 100;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            CapitalShip destroyedShip = fleet.CapitalShips[0];
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
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.General);

            CollectionAssert.Contains(result.DestroyedCapitalShips, destroyedShip);
            Assert.AreEqual(1, result.BombardmentStrength);
            Assert.AreEqual(1, result.StrikeAttempts);
            Assert.AreEqual(1, result.EnergyCapacityDamage);
        }

        [Test]
        public void Execute_StrikeResistance_MustBeLowerThanRoll()
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
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Civilian);

            Assert.AreEqual(1, result.SuccessfulStrikes);
            CollectionAssert.Contains(result.DestroyedBuildings, mine);
        }

        [Test]
        public void Execute_MilitaryCollateral_CanDestroyCivilianTarget()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Regiment regiment = AddRegiment(game, planet, "defender", "empire");
            Building mine = AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 0, 10, 0, 10 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            CollectionAssert.Contains(result.DestroyedRegiments, regiment);
            CollectionAssert.Contains(result.DestroyedBuildings, mine);
            Assert.AreEqual(2, result.SuccessfulStrikes);
            Assert.Less(planet.GetPopularSupport("alliance"), 50);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            Assert.IsNull(result.OwnershipChange);
        }

        [Test]
        public void Execute_AllianceHeadquarters_CanBeDestroyed()
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
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.IsTrue(result.HeadquartersDestroyed);
            Assert.IsFalse(planet.IsHeadquarters);
            Assert.IsNull(game.GetFactionByOwnerInstanceID("alliance").HQInstanceID);
            Assert.IsFalse(planet.IsDestroyed);
        }

        [Test]
        public void Execute_EmpireHeadquarters_IsNotAMilitaryTarget()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.IsHeadquarters = true;
            game.GetFactionByOwnerInstanceID("empire").HQInstanceID = planet.InstanceID;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(game, new SequenceRNG())
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.IsFalse(result.HeadquartersDestroyed);
            Assert.IsTrue(planet.IsHeadquarters);
            Assert.Zero(result.SuccessfulStrikes);
        }

        [Test]
        public void Execute_DestroySystemWithDeathStar_DestroysPlanetAndMinorPersonnel()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.PopularSupport["alliance"] = 30;
            planet.PopularSupport["empire"] = 70;
            Planet secondPlanet = AddPlanet(game, system, "p2", "empire");
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

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 0 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.DestroySystem);

            Assert.IsTrue(result.PlanetDestroyed);
            Assert.IsTrue(planet.IsDestroyed);
            Assert.IsTrue(minor.IsKilled);
            Assert.IsNull(minor.GetParent());
            Assert.IsFalse(main.IsKilled);
            Assert.AreEqual(2, killedMinor.InjuryPoints);
            Assert.AreEqual(planet, killedMinor.GetParent());
            Assert.AreEqual(10, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(20, secondPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual(1, result.Events.OfType<OfficerInjuredResult>().Count());
            Assert.AreEqual(1, result.Events.OfType<OfficerKilledResult>().Count());
        }

        [Test]
        public void Execute_DestroySystem_DefenseFireCannotPreventDestruction()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building lnr = AddBuilding(
                game,
                planet,
                "lnr",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.LNR
            );
            lnr.WeaponPower = 100;
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 0,
                typeId: "CSEM015"
            );
            CapitalShip deathStar = fleet.CapitalShips[0];

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 0 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.DestroySystem);

            Assert.IsTrue(result.PlanetDestroyed);
            Assert.IsTrue(planet.IsDestroyed);
            CollectionAssert.Contains(result.DestroyedCapitalShips, deathStar);
            Assert.Zero(result.StrikeAttempts);
        }

        [Test]
        public void Execute_DestroySystem_PenalizesOuterRimSupportBelowThreshold()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "empire", energy: 10);
            system.SystemType = PlanetSystemType.OuterRim;
            planet.PopularSupport["alliance"] = 30;
            planet.PopularSupport["empire"] = 70;
            (Planet lowSupportPlanet, PlanetSystem affectedSystem) = CreatePlanet(
                game,
                "p2",
                "alliance"
            );
            affectedSystem.SystemType = PlanetSystemType.OuterRim;
            lowSupportPlanet.PopularSupport["alliance"] = 89;
            lowSupportPlanet.PopularSupport["empire"] = 11;
            Planet thresholdPlanet = AddPlanet(game, affectedSystem, "p3", "alliance");
            thresholdPlanet.PopularSupport["alliance"] = 90;
            thresholdPlanet.PopularSupport["empire"] = 10;
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 0,
                typeId: "CSEM015"
            );

            MakeBombardment(game, new SequenceRNG())
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.DestroySystem);

            Assert.AreEqual(87, lowSupportPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual(90, thresholdPlanet.GetPopularSupport("alliance"));
        }

        [Test]
        public void Execute_DestroySystemWithoutDeathStar_DoesNotBombard()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            BombardmentSystem system = MakeBombardment(
                game,
                new SequenceRNG(intValues: new[] { 0, 10 })
            );

            BombardmentResult result = system.Execute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.DestroySystem
            );

            Assert.IsFalse(
                system.CanExecute(new List<Fleet> { fleet }, planet, BombardmentType.DestroySystem)
            );
            Assert.IsFalse(result.PlanetDestroyed);
            Assert.Zero(result.EnergyCapacityDamage);
            Assert.AreEqual(1, planet.EnergyCapacity);
        }

        [Test]
        public void Execute_DestroyedGarrison_CanTransferPlanetBySupport()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.PopularSupport["alliance"] = 60;
            planet.PopularSupport["empire"] = 40;
            Planet secondPlanet = AddPlanet(game, system, "p2", ownerId: null);
            Regiment regiment = AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            CollectionAssert.Contains(result.DestroyedRegiments, regiment);
            Assert.AreEqual("alliance", planet.GetOwnerInstanceID());
            Assert.AreEqual(71, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(61, secondPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual("alliance", secondPlanet.GetOwnerInstanceID());
            Assert.IsTrue(
                result
                    .Events.OfType<PlanetOwnershipChangedResult>()
                    .Any(change =>
                        change.Planet == secondPlanet && change.NewOwner?.InstanceID == "alliance"
                    )
            );
            Assert.AreEqual("empire", result.OwnershipChange.PreviousOwner.InstanceID);
            Assert.AreEqual("alliance", result.OwnershipChange.NewOwner.InstanceID);
        }

        [Test]
        public void Execute_DestroyedGarrison_CanLeavePlanetNeutral()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.PopularSupport["alliance"] = 49;
            planet.PopularSupport["empire"] = 51;
            Planet secondPlanet = AddPlanet(game, system, "p2", "empire");
            secondPlanet.PopularSupport["alliance"] = 20;
            secondPlanet.PopularSupport["empire"] = 80;
            AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.AreEqual(59, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(30, secondPlanet.GetPopularSupport("alliance"));
            Assert.IsNull(result.OwnershipChange.NewOwner);
        }

        [Test]
        public void Execute_DestroyedGarrison_SupportShiftCanTransferPlanet()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeBombardment(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .Execute(new List<Fleet> { fleet }, planet, BombardmentType.Military);

            Assert.AreEqual("alliance", planet.GetOwnerInstanceID());
            Assert.AreEqual(61, planet.GetPopularSupport("alliance"));
            Assert.AreEqual("empire", result.OwnershipChange.PreviousOwner.InstanceID);
            Assert.AreEqual("alliance", result.OwnershipChange.NewOwner.InstanceID);
        }

        [Test]
        public void Execute_RemoteOrMixedFleets_DoNotAttack()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            (Planet remote, _) = CreatePlanet(game, "p2", "empire", energy: 10);
            Fleet remoteFleet = AddBombardmentFleet(game, remote, "alliance", bombardment: 10);
            Fleet localEnemyFleet = AddBombardmentFleet(game, planet, "empire", bombardment: 10);

            BombardmentResult remoteResult = MakeBombardment(game, new SequenceRNG())
                .Execute(new List<Fleet> { remoteFleet }, planet, BombardmentType.General);
            BombardmentResult mixedResult = MakeBombardment(game, new SequenceRNG())
                .Execute(
                    new List<Fleet> { remoteFleet, localEnemyFleet },
                    planet,
                    BombardmentType.General
                );

            Assert.Zero(remoteResult.BombardmentStrength);
            Assert.Zero(mixedResult.BombardmentStrength);
        }

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

        private static Building AddBuilding(
            GameRoot game,
            Planet planet,
            string id,
            string ownerId,
            BuildingType type,
            DefenseFacilityClass defenseClass = DefenseFacilityClass.None
        )
        {
            Building building = new Building
            {
                InstanceID = id,
                OwnerInstanceID = ownerId,
                BuildingType = type,
                DefenseFacilityClass = defenseClass,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            return building;
        }

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

        private static Planet AddPlanet(
            GameRoot game,
            PlanetSystem system,
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
            game.AttachNode(planet, system);
            return planet;
        }
    }
}
