using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class ResearchMissionTests
    {
        private GameRoot game;
        private Faction faction;
        private Planet planet;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = TestConfig.Create();
            game = new GameRoot(config);

            faction = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            game.Factions.Add(faction);

            PlanetSystem sys = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sys, game.Galaxy);

            planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 20,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 80 } },
            };
            game.AttachNode(planet, sys);
        }

        private ResearchMission CreateMission(
            Officer officer,
            ManufacturingType type = ManufacturingType.Ship
        )
        {
            MissionContext ctx = new MissionContext
            {
                Game = game,
                OwnerInstanceId = "empire",
                Target = planet,
                MainParticipants = new List<IMissionParticipant> { officer },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            ResearchMission mission = ResearchMission.TryCreate(ctx, type);
            game.AttachNode(mission, planet);
            return mission;
        }

        private Officer CreateOfficer(int shipSkill = 50, int troopSkill = 0, int facilitySkill = 0)
        {
            Officer officer = new Officer
            {
                InstanceID = "off1",
                OwnerInstanceID = "empire",
                ShipResearch = shipSkill,
                TroopResearch = troopSkill,
                FacilityResearch = facilitySkill,
            };
            game.AttachNode(officer, planet);
            return officer;
        }

        [Test]
        public void TryCreate_EnemyPlanet_ReturnsNull()
        {
            Officer officer = CreateOfficer();

            planet.OwnerInstanceID = "rebels";

            MissionContext ctx = new MissionContext
            {
                Game = game,
                OwnerInstanceId = "empire",
                Target = planet,
                MainParticipants = new List<IMissionParticipant> { officer },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            ResearchMission mission = ResearchMission.TryCreate(ctx, ManufacturingType.Ship);

            Assert.IsNull(mission, "TryCreate should return null for an enemy planet");
        }

        [Test]
        public void Execute_PositiveResearchSkillAndMinimumRoll_AwardsResearchCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 75);
            ResearchMission mission = CreateMission(officer, ManufacturingType.Ship);

            mission.Execute(game, new FixedRNG(0.0));

            Assert.Greater(faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign), 0);
        }

        [Test]
        public void Execute_Success_AwardsResearchCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 100);
            ResearchMission mission = CreateMission(officer);

            int before = faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign);
            mission.Execute(game, new FixedRNG(0.0));
            int after = faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign);

            Assert.Greater(after, before, "Successful research mission should award capacity");
        }

        [Test]
        public void Execute_Success_IncrementsMatchingResearchSkill()
        {
            Officer officer = CreateOfficer(shipSkill: 50);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(
                51,
                officer.ShipResearch,
                "Successful research should improve the acting officer's matching research stat"
            );
        }

        [Test]
        public void Execute_Failure_NoCapacityAwarded()
        {
            Officer officer = CreateOfficer(shipSkill: 10);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(game, new FixedRNG(0.99));

            Assert.AreEqual(0, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
        }

        [Test]
        public void Execute_ZeroResearchSkillAndMinimumRoll_DoesNotAwardCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 0);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(0, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
        }

        [Test]
        public void Execute_RollEqualsResearchChance_DoesNotAwardCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 50);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(game, new FixedRNG(0.5));

            Assert.AreEqual(0, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(50, officer.ShipResearch);
        }

        [Test]
        public void Execute_Failure_SkillUnchanged()
        {
            Officer officer = CreateOfficer(shipSkill: 10);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(game, new FixedRNG(0.99));

            Assert.AreEqual(10, officer.ShipResearch, "Skill should not change on failure");
        }

        [Test]
        public void Execute_SecondParticipantOnlyCouldSucceed_DoesNotAwardCapacity()
        {
            Officer firstOfficer = CreateOfficer(shipSkill: 10);
            Officer secondOfficer = new Officer
            {
                InstanceID = "off2",
                OwnerInstanceID = "empire",
                ShipResearch = 100,
            };
            game.AttachNode(secondOfficer, planet);

            MissionContext ctx = new MissionContext
            {
                Game = game,
                OwnerInstanceId = "empire",
                Target = planet,
                MainParticipants = new List<IMissionParticipant> { firstOfficer, secondOfficer },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            ResearchMission mission = ResearchMission.TryCreate(ctx, ManufacturingType.Ship);
            game.AttachNode(mission, planet);

            mission.Execute(game, new FixedRNG(0.5));

            Assert.AreEqual(0, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(10, firstOfficer.ShipResearch);
            Assert.AreEqual(100, secondOfficer.ShipResearch);
        }

        [Test]
        public void Execute_TroopResearchType_AwardsTroopCapacity()
        {
            Officer officer = CreateOfficer(troopSkill: 100);
            ResearchMission mission = CreateMission(officer, ManufacturingType.Troop);

            mission.Execute(game, new FixedRNG(0.0));

            Assert.Greater(
                faction.GetResearchCapacityRemaining(ResearchDiscipline.TroopTraining),
                0
            );
            Assert.AreEqual(0, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
        }

        [Test]
        public void Execute_MaxSkillOfficer_AwardsResearchCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 100);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(game, new FixedRNG(0.99));

            Assert.Greater(faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign), 0);
        }

        [Test]
        public void CanContinue_OwnedPlanet_ReturnsTrue()
        {
            Officer officer = CreateOfficer();
            ResearchMission mission = CreateMission(officer);

            Assert.IsTrue(mission.CanContinue(game));
        }

        [Test]
        public void CanContinue_PlanetLost_ReturnsFalse()
        {
            Officer officer = CreateOfficer();
            ResearchMission mission = CreateMission(officer);

            planet.OwnerInstanceID = "rebels";

            Assert.IsFalse(mission.CanContinue(game));
        }

        [Test]
        public void Execute_Success_DoesNotIncrementLeadership()
        {
            Officer officer = CreateOfficer(shipSkill: 100);
            int leadershipBefore = officer.GetSkillValue(MissionParticipantSkill.Leadership);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(
                leadershipBefore,
                officer.GetSkillValue(MissionParticipantSkill.Leadership),
                "Research missions should not increment Leadership"
            );
        }

        [Test]
        public void CanContinue_AfterSuccess_ReturnsTrue()
        {
            Officer officer = CreateOfficer(shipSkill: 100);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(game, new FixedRNG(0.0));

            Assert.IsTrue(
                mission.CanContinue(game),
                "Mission should continue after successful research"
            );
        }

        [Test]
        public void CanContinue_AfterFailure_ReturnsTrue()
        {
            Officer officer = CreateOfficer(shipSkill: 10);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(game, new FixedRNG(0.99));

            Assert.IsTrue(
                mission.CanContinue(game),
                "Mission should continue after failed research"
            );
        }

        [Test]
        public void SerializeAndDeserialize_ShipDesignMission_RetainsAllProperties()
        {
            ResearchMission mission = new ResearchMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Research",
                DisplayName = "Ship Design",
                TargetInstanceID = "PLANET1",
                ParticipantSkill = MissionParticipantSkill.Leadership,
                ResearchType = ManufacturingType.Ship,
                HasInitiated = true,
                MaxProgress = 15,
                CurrentProgress = 7,
            };

            string xml = SerializationHelper.Serialize(mission);
            ResearchMission deserialized = SerializationHelper.Deserialize<ResearchMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("FACTION1", deserialized.OwnerInstanceID);
            Assert.AreEqual("Research", deserialized.ConfigKey);
            Assert.AreEqual("Ship Design", deserialized.DisplayName);
            Assert.AreEqual("PLANET1", deserialized.TargetInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Leadership, deserialized.ParticipantSkill);
            Assert.AreEqual(ManufacturingType.Ship, deserialized.ResearchType);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(15, deserialized.MaxProgress);
            Assert.AreEqual(7, deserialized.CurrentProgress);
        }

        [Test]
        public void SerializeAndDeserialize_TroopTrainingMission_RetainsAllProperties()
        {
            ResearchMission mission = new ResearchMission
            {
                InstanceID = "MISSION2",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Research",
                DisplayName = "Troop Training",
                TargetInstanceID = "PLANET1",
                ResearchType = ManufacturingType.Troop,
            };

            string xml = SerializationHelper.Serialize(mission);
            ResearchMission deserialized = SerializationHelper.Deserialize<ResearchMission>(xml);

            Assert.AreEqual("Research", deserialized.ConfigKey);
            Assert.AreEqual("Troop Training", deserialized.DisplayName);
            Assert.AreEqual(ManufacturingType.Troop, deserialized.ResearchType);
        }

        [Test]
        public void SerializeAndDeserialize_FacilityDesignMission_RetainsAllProperties()
        {
            ResearchMission mission = new ResearchMission
            {
                InstanceID = "MISSION3",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Research",
                DisplayName = "Facility Design",
                TargetInstanceID = "PLANET1",
                ResearchType = ManufacturingType.Building,
            };

            string xml = SerializationHelper.Serialize(mission);
            ResearchMission deserialized = SerializationHelper.Deserialize<ResearchMission>(xml);

            Assert.AreEqual("Research", deserialized.ConfigKey);
            Assert.AreEqual("Facility Design", deserialized.DisplayName);
            Assert.AreEqual(ManufacturingType.Building, deserialized.ResearchType);
        }
    }
}
