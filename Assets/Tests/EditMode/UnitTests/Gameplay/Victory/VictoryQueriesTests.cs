using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class VictoryQueriesTests
    {
        [Test]
        public void HasDefeatedEveryOpponent_EnemyHeadquartersStillOwned_ReturnsFalse()
        {
            (GameRoot game, Faction defender, Faction attacker, Planet headquarters) = BuildScene();
            game.ChangeOwnership(headquarters, defender.InstanceID);

            bool result = new VictoryQueries(game).HasDefeatedEveryOpponent(attacker);

            Assert.IsFalse(result);
        }

        [Test]
        public void HasDefeatedEveryOpponent_EnemyHeadquartersLost_ReturnsTrue()
        {
            (GameRoot game, _, Faction attacker, _) = BuildScene();

            bool result = new VictoryQueries(game).HasDefeatedEveryOpponent(attacker);

            Assert.IsTrue(result);
        }

        [Test]
        public void HasDefeatedEveryOpponent_CandidateHeadquartersLost_ReturnsFalse()
        {
            (GameRoot game, _, Faction attacker, _) = BuildScene();
            attacker.HasLostHeadquarters = true;

            bool result = new VictoryQueries(game).HasDefeatedEveryOpponent(attacker);

            Assert.IsFalse(result);
        }

        [Test]
        public void HasDefeatedEveryOpponent_CandidateHeadquartersNotOwned_ReturnsFalse()
        {
            (GameRoot game, Faction defender, Faction attacker, _) = BuildScene();
            Planet attackerHeadquarters = game.GetSceneNodeByInstanceID<Planet>(
                attacker.HQInstanceID
            );
            game.ChangeOwnership(attackerHeadquarters, defender.InstanceID);

            bool result = new VictoryQueries(game).HasDefeatedEveryOpponent(attacker);

            Assert.IsFalse(result);
        }

        [Test]
        public void GetSoleOpponent_OneActiveAndOneDefeatedOpponent_ReturnsActiveOpponent()
        {
            (GameRoot game, Faction defender, Faction attacker, _) = BuildScene();
            Faction defeated = new Faction
            {
                InstanceID = "defeated",
                HQInstanceID = "hq-defeated",
                HasLostHeadquarters = true,
            };
            game.GetFactions().Add(defeated);

            Faction result = new VictoryQueries(game).GetSoleOpponent(defender);

            Assert.AreSame(attacker, result);
        }

        [Test]
        public void HasDefeatedEveryOpponent_ConquestOfficerRemainsFree_ReturnsFalse()
        {
            (GameRoot game, Faction defender, Faction attacker, Planet headquarters) = BuildScene(
                GameVictoryCondition.Conquest
            );
            defender.Settings.ConquestOfficerInstanceIDs.Add("leader");
            Planet defenderPlanet = AddOwnedPlanet(game, headquarters, defender, "defender-home");
            game.AttachNode(
                new Officer { InstanceID = "leader", OwnerInstanceID = defender.InstanceID },
                defenderPlanet
            );

            bool result = new VictoryQueries(game).HasDefeatedEveryOpponent(attacker);

            Assert.IsFalse(result);
        }

        [Test]
        public void HasDefeatedEveryOpponent_ConfiguredConquestOfficerCaptured_ReturnsTrue()
        {
            (GameRoot game, Faction defender, Faction attacker, Planet headquarters) = BuildScene(
                GameVictoryCondition.Conquest
            );
            defender.Settings.ConquestOfficerInstanceIDs.Add("leader");
            game.AttachNode(
                new Officer
                {
                    InstanceID = "leader",
                    OwnerInstanceID = defender.InstanceID,
                    IsCaptured = true,
                    CaptorInstanceID = attacker.InstanceID,
                },
                headquarters
            );
            Planet defenderPlanet = AddOwnedPlanet(game, headquarters, defender, "defender-home");
            game.AttachNode(
                new Officer
                {
                    InstanceID = "other-officer",
                    OwnerInstanceID = defender.InstanceID,
                    IsMain = true,
                },
                defenderPlanet
            );

            bool result = new VictoryQueries(game).HasDefeatedEveryOpponent(attacker);

            Assert.IsTrue(result);
        }

        [Test]
        public void HasDefeatedEveryOpponent_AnotherEnemyHeadquartersRemains_ReturnsFalse()
        {
            (GameRoot game, _, Faction attacker, Planet headquarters) = BuildScene();
            Faction thirdFaction = new Faction { InstanceID = "third", HQInstanceID = "hq-third" };
            game.GetFactions().Add(thirdFaction);
            game.AttachNode(
                new Planet
                {
                    InstanceID = thirdFaction.HQInstanceID,
                    OwnerInstanceID = thirdFaction.InstanceID,
                    IsColonized = true,
                },
                headquarters.GetParent()
            );

            bool result = new VictoryQueries(game).HasDefeatedEveryOpponent(attacker);

            Assert.IsFalse(result);
        }

        [Test]
        public void HasDefeatedEveryOpponent_ActiveMobileHeadquartersRemains_ReturnsFalse()
        {
            (GameRoot game, Faction defender, Faction attacker, Planet headquarters) = BuildScene();
            defender.Settings.Headquarters = new HeadquartersSettings
            {
                FacilityTypeID = "mobile-hq-type",
                IsMobile = true,
            };
            Building mobileHeadquarters = new Building
            {
                InstanceID = "mobile-hq",
                TypeID = "mobile-hq-type",
                OwnerInstanceID = defender.InstanceID,
                BuildingType = BuildingType.Headquarters,
            };
            Planet defenderPlanet = AddOwnedPlanet(game, headquarters, defender, "defender-home");
            game.AttachNode(mobileHeadquarters, defenderPlanet);
            defender.HeadquartersUnitInstanceID = mobileHeadquarters.InstanceID;

            bool result = new VictoryQueries(game).HasDefeatedEveryOpponent(attacker);

            Assert.IsFalse(result);
        }

        /// <summary>
        /// Adds a colonized planet owned by the specified faction.
        /// </summary>
        /// <param name="game">The game receiving the planet.</param>
        /// <param name="sibling">A planet in the destination sector.</param>
        /// <param name="owner">The planet owner.</param>
        /// <param name="instanceId">The planet instance identifier.</param>
        /// <returns>The attached planet.</returns>
        private static Planet AddOwnedPlanet(
            GameRoot game,
            Planet sibling,
            Faction owner,
            string instanceId
        )
        {
            Planet planet = new Planet
            {
                InstanceID = instanceId,
                OwnerInstanceID = owner.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(planet, sibling.GetParent());
            return planet;
        }

        /// <summary>
        /// Creates a two-faction victory scene.
        /// </summary>
        /// <param name="victoryCondition">The active victory condition.</param>
        /// <returns>The game, factions, and lost headquarters planet.</returns>
        private static (
            GameRoot game,
            Faction defender,
            Faction attacker,
            Planet defenderHeadquarters
        ) BuildScene(GameVictoryCondition victoryCondition = GameVictoryCondition.Headquarters)
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Summary = new GameSummary { VictoryCondition = victoryCondition };
            Faction defender = new Faction { InstanceID = "defender" };
            Faction attacker = new Faction { InstanceID = "attacker" };
            game.GetFactions().Add(defender);
            game.GetFactions().Add(attacker);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet defenderHeadquarters = new Planet
            {
                InstanceID = "hq-defender",
                OwnerInstanceID = attacker.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(defenderHeadquarters, sector);
            defender.HQInstanceID = defenderHeadquarters.InstanceID;
            Planet attackerHeadquarters = new Planet
            {
                InstanceID = "hq-attacker",
                OwnerInstanceID = attacker.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(attackerHeadquarters, sector);
            attacker.HQInstanceID = attackerHeadquarters.InstanceID;
            return (game, defender, attacker, defenderHeadquarters);
        }
    }
}
