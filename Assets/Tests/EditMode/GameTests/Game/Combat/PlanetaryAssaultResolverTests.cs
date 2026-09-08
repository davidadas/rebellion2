using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Combat
{
    [TestFixture]
    public class PlanetaryAssaultResolverTests
    {
        [Test]
        public void Resolve_CompletedAssault_DoesNotModifyGameState()
        {
            GameConfig.PlanetaryAssaultConfig config = new GameConfig.PlanetaryAssaultConfig
            {
                DefenseFireDivisor = 5,
                CollateralDamagePercent = 10,
                GeneralLeadershipDivisor = 10,
                ContestRollMaximum = 10,
                DefenderWinsMaximum = 4,
                AttackerWinsMinimum = 6,
                CaptureGarrisonCount = 6,
            };
            GameRoot game = new GameRoot(new GameConfig());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "alliance" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "alliance",
                EnergyCapacity = 2,
                AllocatedEnergy = 1,
            };
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
            };
            Regiment attacker = new Regiment
            {
                InstanceID = "attacker",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Regiment defender = new Regiment
            {
                InstanceID = "defender",
                OwnerInstanceID = "alliance",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(attacker, ship);
            game.AttachNode(defender, planet);
            PlanetaryAssaultResolver resolver = new PlanetaryAssaultResolver(
                config,
                new SequenceRNG(intValues: new[] { 0, 6, 99 })
            );

            PlanetaryAssaultResult result = resolver.Resolve(new List<Fleet> { fleet }, planet);

            Assert.IsTrue(result.Success);
            CollectionAssert.Contains(result.DestroyedDefenderRegiments, defender);
            Assert.AreSame(ship, attacker.GetParent());
            Assert.AreSame(planet, defender.GetParent());
            Assert.AreEqual("alliance", planet.GetOwnerInstanceID());
            Assert.AreEqual(2, planet.EnergyCapacity);
            Assert.AreEqual(1, planet.AllocatedEnergy);
        }
    }
}
