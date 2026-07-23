using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class PlanetaryAssaultSystemTests : CombatTestBase
    {
        [Test]
        public void Execute_TwoShieldGenerators_BlockAssault()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            AddDefenseBuilding(game, planet, "shield1", DefenseFacilityClass.Shield);
            AddDefenseBuilding(game, planet, "shield2", DefenseFacilityClass.Shield);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            PlanetaryAssaultResult result = MakePlanetaryAssault(game, new SequenceRNG())
                .Execute(new List<Fleet> { fleet }, planet);

            Assert.IsTrue(result.BlockedByShields);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("alliance", planet.GetOwnerInstanceID());
        }

        [Test]
        public void Execute_DeathStarShield_DoesNotBlockAssault()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            AddDefenseBuilding(game, planet, "shield", DefenseFacilityClass.Shield);
            AddDefenseBuilding(
                game,
                planet,
                "death-star-shield",
                DefenseFacilityClass.DeathStarShield
            );
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            PlanetaryAssaultResult result = MakePlanetaryAssault(game, new SequenceRNG())
                .Execute(new List<Fleet> { fleet }, planet);

            Assert.IsFalse(result.BlockedByShields);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void CanExecute_TwoReadyAndSixMovingRegiments_UsesReadyRegiments()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 8);
            foreach (Regiment regiment in fleet.CapitalShips[0].Regiments.Skip(2))
                regiment.Movement = new MovementState();
            PlanetaryAssaultSystem system = MakePlanetaryAssault(game, new SequenceRNG());

            Assert.IsTrue(system.CanExecute(new List<Fleet> { fleet }, planet));
            PlanetaryAssaultResult result = system.Execute(new List<Fleet> { fleet }, planet);

            Assert.AreEqual(2, result.InitialAttackerRegimentCount);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void CanExecute_ShieldedTargetOrNoReadyRegiments_ReturnsFalse()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            AddDefenseBuilding(game, planet, "shield1", DefenseFacilityClass.Shield);
            AddDefenseBuilding(game, planet, "shield2", DefenseFacilityClass.Shield);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            PlanetaryAssaultSystem system = MakePlanetaryAssault(game, new SequenceRNG());

            bool shielded = system.CanExecute(new List<Fleet> { fleet }, planet);
            foreach (Building building in planet.GetAllBuildings())
                building.ManufacturingStatus = ManufacturingStatus.Building;
            fleet.CapitalShips[0].Regiments[0].Movement = new MovementState();
            bool noReadyRegiments = system.CanExecute(new List<Fleet> { fleet }, planet);

            Assert.IsFalse(shielded);
            Assert.IsFalse(noReadyRegiments);
        }

        [Test]
        public void CanExecute_NeutralPlanetWithReadyRegiment_ReturnsTrue()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: null, energy: 10);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            bool canExecute = MakePlanetaryAssault(game, new SequenceRNG())
                .CanExecute(new List<Fleet> { fleet }, planet);

            Assert.IsTrue(canExecute);
        }

        [Test]
        public void TryExecute_ValidCommand_PublishesCompletedResultBatch()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            PlanetaryAssaultSystem system = MakePlanetaryAssault(game, new SequenceRNG());
            IReadOnlyList<GameResult> publishedResults = null;
            system.ResultsProduced += results => publishedResults = results;

            PlanetaryAssaultResult result = system.TryExecute(new List<Fleet> { fleet }, planet);

            Assert.IsNotNull(publishedResults);
            Assert.AreSame(result, publishedResults[0]);
            Assert.IsTrue(publishedResults.OfType<PlanetGarrisonChangedResult>().Any());
            Assert.Contains(result.OwnershipChange, publishedResults.ToList());
        }

        [Test]
        public void Execute_DefenseFire_UsesInitialAttackerIndexRange()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Building first = AddDefenseBuilding(game, planet, "kdy", DefenseFacilityClass.KDY);
            first.WeaponPower = 500;
            Building second = AddDefenseBuilding(game, planet, "lnr", DefenseFacilityClass.LNR);
            second.WeaponPower = 500;
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 2);
            Regiment attacker = fleet.CapitalShips[0].Regiments[0];

            PlanetaryAssaultResult result = MakePlanetaryAssault(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 0, 0, 1 })
                )
                .Execute(new List<Fleet> { fleet }, planet);

            Assert.AreEqual(1, result.DestroyedAttackerRegiments.Count);
            Assert.AreEqual(1, result.RemainingAttackerRegimentCount);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("empire", result.AttackerOwnerInstanceID);
            Assert.AreEqual("alliance", result.DefenderOwnerInstanceID);
            CollectionAssert.Contains(
                result.AttackingUnits.Select(unit => unit.Unit.GetInstanceID()),
                attacker.GetInstanceID()
            );
            CollectionAssert.Contains(
                result.DefendingUnits.Select(unit => unit.Unit.GetInstanceID()),
                first.GetInstanceID()
            );
            CollectionAssert.Contains(
                result.DefendingUnits.Select(unit => unit.Unit.GetInstanceID()),
                second.GetInstanceID()
            );
            Assert.IsTrue(
                result
                    .AttackingUnits.Single(unit =>
                        unit.Unit.GetInstanceID() == attacker.GetInstanceID()
                    )
                    .Destroyed
            );
        }

        [TestCase(4, true, false)]
        [TestCase(5, false, false)]
        [TestCase(6, false, true)]
        public void Execute_ContestScore_UsesSourceThresholds(
            int contestRoll,
            bool defenderWins,
            bool attackerWins
        )
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Regiment defender = AddDefender(game, planet, "defender");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            Regiment attacker = fleet.CapitalShips[0].Regiments[0];

            PlanetaryAssaultResult result = MakePlanetaryAssault(
                    game,
                    new SequenceRNG(intValues: new[] { 0, contestRoll, 99 })
                )
                .Execute(new List<Fleet> { fleet }, planet);

            Assert.AreEqual(defenderWins, result.DestroyedAttackerRegiments.Contains(attacker));
            Assert.AreEqual(attackerWins, result.DestroyedDefenderRegiments.Contains(defender));
            Assert.AreEqual(attackerWins, result.Success);
        }

        [Test]
        public void Execute_EachTroopUsesGeneralFromItsOwnFleet()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Regiment firstDefender = AddDefender(game, planet, "defender1");
            Regiment secondDefender = AddDefender(game, planet, "defender2");
            Fleet uncommandedFleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            Fleet commandedFleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            Officer general = new Officer
            {
                InstanceID = "general",
                OwnerInstanceID = "empire",
                CurrentRank = OfficerRank.General,
            };
            general.SetBaseRating(OfficerRating.Leadership, 60);
            game.AttachNode(general, commandedFleet.CapitalShips[0]);

            PlanetaryAssaultResult result = MakePlanetaryAssault(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 4, 0, 4, 99, 99 })
                )
                .Execute(new List<Fleet> { uncommandedFleet, commandedFleet }, planet);

            Assert.AreEqual(1, result.DestroyedAttackerRegiments.Count);
            Assert.AreEqual(1, result.DestroyedDefenderRegiments.Count);
            CollectionAssert.Contains(result.DestroyedDefenderRegiments, firstDefender);
            CollectionAssert.DoesNotContain(result.DestroyedDefenderRegiments, secondDefender);
        }

        [Test]
        public void Execute_CollateralDamage_CanDestroyCivilianFacilityAndExcludesHeadquarters()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 1);
            planet.IsHeadquarters = true;
            Building mine = AddCollateralBuilding(game, planet, "mine");
            AddDefender(game, planet, "defender");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            PlanetaryAssaultResult result = MakePlanetaryAssault(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 5, 0, 0 })
                )
                .Execute(new List<Fleet> { fleet }, planet);

            Assert.IsTrue(planet.IsHeadquarters);
            CollectionAssert.Contains(result.CollateralDestroyedBuildings, mine);
            Assert.AreEqual(1, planet.EnergyCapacity);
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Execute_CollateralDamage_RollsAllTrialsBeforeSelectingTargets()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 1);
            AddDefender(game, planet, "defender1");
            AddDefender(game, planet, "defender2");
            Building mine = AddCollateralBuilding(game, planet, "mine");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 2);

            PlanetaryAssaultResult result = MakePlanetaryAssault(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 5, 0, 5, 0, 99, 0 })
                )
                .Execute(new List<Fleet> { fleet }, planet);

            CollectionAssert.Contains(result.CollateralDestroyedBuildings, mine);
            Assert.AreEqual(1, planet.EnergyCapacity);
        }

        [Test]
        public void Execute_Capture_LandsAtMostRequiredGarrison()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 7);

            PlanetaryAssaultResult result = MakePlanetaryAssault(game, new SequenceRNG())
                .Execute(new List<Fleet> { fleet }, planet);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            Assert.AreEqual(6, result.LandedRegiments.Count);
            Assert.AreEqual(6, planet.GetAllRegiments().Count);
            Assert.AreEqual(1, fleet.CapitalShips[0].Regiments.Count);
            Assert.AreSame(
                planet,
                result.Events.OfType<PlanetGarrisonChangedResult>().Single().Planet
            );
        }

        [Test]
        public void Execute_CaptureWithFewerTroops_LandsEverySurvivor()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 2);

            PlanetaryAssaultResult result = MakePlanetaryAssault(game, new SequenceRNG())
                .Execute(new List<Fleet> { fleet }, planet);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.LandedRegiments.Count);
            Assert.AreEqual(2, planet.GetAllRegiments().Count);
        }

        [Test]
        public void Execute_AttackersDestroyed_DoesNotCapturePlanet()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            AddDefender(game, planet, "defender");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            PlanetaryAssaultResult result = MakePlanetaryAssault(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 4, 99 })
                )
                .Execute(new List<Fleet> { fleet }, planet);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("alliance", planet.GetOwnerInstanceID());
            Assert.IsNull(result.OwnershipChange);
        }

        [Test]
        public void Execute_RngFailure_ClearsFleetCombatState()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            AddDefender(game, planet, "defender");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            Fleet defenderFleet = AddAssaultFleet(game, planet, "alliance", regimentCount: 0);

            Assert.Throws<InvalidOperationException>(() =>
                MakePlanetaryAssault(game, new ThrowingRNG())
                    .Execute(new List<Fleet> { fleet }, planet)
            );

            Assert.IsFalse(fleet.IsInCombat);
            Assert.IsFalse(defenderFleet.IsInCombat);
        }

        private static Fleet AddAssaultFleet(
            GameRoot game,
            Planet planet,
            string ownerId,
            int regimentCount
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
                OwnerInstanceID = ownerId,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                RegimentCapacity = regimentCount,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);

            for (int index = 0; index < regimentCount; index++)
            {
                game.AttachNode(
                    new Regiment
                    {
                        InstanceID = Guid.NewGuid().ToString(),
                        OwnerInstanceID = ownerId,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    ship
                );
            }

            return fleet;
        }

        private static Regiment AddDefender(GameRoot game, Planet planet, string instanceId)
        {
            Regiment regiment = new Regiment
            {
                InstanceID = instanceId,
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planet);
            return regiment;
        }

        private static Building AddDefenseBuilding(
            GameRoot game,
            Planet planet,
            string instanceId,
            DefenseFacilityClass defenseClass
        )
        {
            Building building = new Building
            {
                InstanceID = instanceId,
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                BuildingType = BuildingType.Defense,
                DefenseFacilityClass = defenseClass,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            return building;
        }

        private static Building AddCollateralBuilding(
            GameRoot game,
            Planet planet,
            string instanceId
        )
        {
            Building building = new Building
            {
                InstanceID = instanceId,
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            return building;
        }
    }
}
