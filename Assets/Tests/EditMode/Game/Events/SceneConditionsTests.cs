using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class SceneConditionsTests
    {
        [Test]
        public void AreOnPlanet_MissingUnit_DoesNotMatch()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "faction" });
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "faction",
                IsColonized = true,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);
            game.AttachNode(officer, planet);
            AreOnPlanetConditional condition = new AreOnPlanetConditional
            {
                UnitInstanceIDs = new List<string> { officer.InstanceID, "missing" },
            };

            bool isMet = condition.IsMet(game);

            Assert.IsFalse(isMet);
        }
    }
}
