using NUnit.Framework;
using Rebellion.AI.Combat;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.AI.Combat
{
    [TestFixture]
    public class AISpaceCombatPolicyTests
    {
        [Test]
        public void CanWithdraw_NonHeadquartersDefender_ReturnsTrue()
        {
            (AISpaceCombatPolicy policy, Fleet fleet, Planet planet, GameRoot _) = CreateScenario(
                isHeadquarters: false,
                isMobile: false
            );

            bool canWithdraw = policy.CanWithdraw(fleet, planet);

            Assert.IsTrue(canWithdraw);
        }

        [Test]
        public void CanWithdraw_FixedHeadquartersDefender_ReturnsFalse()
        {
            (AISpaceCombatPolicy policy, Fleet fleet, Planet planet, GameRoot _) = CreateScenario(
                isHeadquarters: true,
                isMobile: false
            );

            bool canWithdraw = policy.CanWithdraw(fleet, planet);

            Assert.IsFalse(canWithdraw);
        }

        [Test]
        public void CanWithdraw_MobileHeadquartersDefender_ReturnsTrue()
        {
            (AISpaceCombatPolicy policy, Fleet fleet, Planet planet, GameRoot _) = CreateScenario(
                isHeadquarters: true,
                isMobile: true
            );

            bool canWithdraw = policy.CanWithdraw(fleet, planet);

            Assert.IsTrue(canWithdraw);
        }

        [Test]
        public void CanWithdraw_CapturedFixedHeadquartersDefender_ReturnsFalse()
        {
            (AISpaceCombatPolicy policy, Fleet fleet, Planet planet, GameRoot game) =
                CreateScenario(isHeadquarters: true, isMobile: false);
            Faction currentOwner = new Faction
            {
                InstanceID = "alliance",
                Settings = new FactionSettings
                {
                    Headquarters = new HeadquartersSettings { IsMobile = true },
                },
            };
            game.GetFactions().Add(currentOwner);
            planet.OwnerInstanceID = currentOwner.InstanceID;
            planet.IsHeadquarters = false;
            fleet.OwnerInstanceID = currentOwner.InstanceID;

            bool canWithdraw = policy.CanWithdraw(fleet, planet);

            Assert.IsFalse(canWithdraw);
        }

        /// <summary>
        /// Creates a fleet defending a configured headquarters scenario.
        /// </summary>
        /// <param name="isHeadquarters">Whether the planet is the faction headquarters.</param>
        /// <param name="isMobile">Whether the faction headquarters is mobile.</param>
        /// <returns>The policy and scenario objects under test.</returns>
        private static (
            AISpaceCombatPolicy policy,
            Fleet fleet,
            Planet planet,
            GameRoot game
        ) CreateScenario(bool isHeadquarters, bool isMobile)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction
            {
                InstanceID = "empire",
                HQInstanceID = isHeadquarters ? "planet" : null,
                Settings = new FactionSettings
                {
                    Headquarters = new HeadquartersSettings { IsMobile = isMobile },
                },
            };
            game.GetFactions().Add(faction);

            PlanetSector system = new PlanetSector { InstanceID = "system" };
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = faction.InstanceID,
                IsHeadquarters = isHeadquarters,
            };
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = faction.InstanceID };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);
            game.AttachNode(fleet, planet);

            return (new AISpaceCombatPolicy(game), fleet, planet, game);
        }
    }
}
