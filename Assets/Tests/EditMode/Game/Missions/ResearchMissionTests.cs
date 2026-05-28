using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class ResearchMissionTests
    {
        private GameRoot _game;
        private Faction _faction;
        private Planet _planet;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = TestConfig.Create();
            _game = new GameRoot(config);

            _faction = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            _game.Factions.Add(_faction);

            PlanetSystem sys = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(sys, _game.Galaxy);

            _planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 20,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 80 } },
            };
            _game.AttachNode(_planet, sys);
        }

        private ResearchMission CreateMission(
            Officer officer,
            ResearchDiscipline discipline = ResearchDiscipline.ShipDesign
        )
        {
            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = "empire",
                Target = _planet,
                MainParticipants = new List<IMissionParticipant> { officer },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            ResearchMission mission = ResearchMission.TryCreate(ctx, discipline);
            _game.AttachNode(mission, _planet);
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
            _game.AttachNode(officer, _planet);
            return officer;
        }

        [Test]
        public void TryCreate_EnemyPlanet_ReturnsNull()
        {
            Officer officer = CreateOfficer();

            _planet.OwnerInstanceID = "rebels";

            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = "empire",
                Target = _planet,
                MainParticipants = new List<IMissionParticipant> { officer },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            ResearchMission mission = ResearchMission.TryCreate(ctx, ResearchDiscipline.ShipDesign);

            Assert.IsNull(mission, "TryCreate should return null for an enemy planet");
        }

        [Test]
        public void Execute_PositiveResearchSkillAndMinimumRoll_AwardsResearchCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 75);
            ResearchMission mission = CreateMission(officer, ResearchDiscipline.ShipDesign);

            mission.Execute(_game, new FixedRNG(0.0));

            Assert.Greater(_faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign), 0);
        }

        [Test]
        public void Execute_Success_AwardsResearchCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 100);
            ResearchMission mission = CreateMission(officer);

            int before = _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign);
            mission.Execute(_game, new FixedRNG(0.0));
            int after = _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign);

            Assert.Greater(after, before, "Successful research mission should award capacity");
        }

        [Test]
        public void Execute_Success_IncrementsMatchingResearchSkill()
        {
            Officer officer = CreateOfficer(shipSkill: 50);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(_game, new FixedRNG(0.0));

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

            mission.Execute(_game, new FixedRNG(0.99));

            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
        }

        [Test]
        public void Execute_ZeroResearchSkillAndMinimumRoll_DoesNotAwardCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 0);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(_game, new FixedRNG(0.0));

            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
        }

        [Test]
        public void Execute_RollEqualsResearchChance_DoesNotAwardCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 50);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(_game, new FixedRNG(0.5));

            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
            Assert.AreEqual(50, officer.ShipResearch);
        }

        [Test]
        public void Execute_Failure_SkillUnchanged()
        {
            Officer officer = CreateOfficer(shipSkill: 10);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(_game, new FixedRNG(0.99));

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
            _game.AttachNode(secondOfficer, _planet);

            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = "empire",
                Target = _planet,
                MainParticipants = new List<IMissionParticipant> { firstOfficer, secondOfficer },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            ResearchMission mission = ResearchMission.TryCreate(ctx, ResearchDiscipline.ShipDesign);
            _game.AttachNode(mission, _planet);

            mission.Execute(_game, new FixedRNG(0.5));

            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
            Assert.AreEqual(10, firstOfficer.ShipResearch);
            Assert.AreEqual(100, secondOfficer.ShipResearch);
        }

        [Test]
        public void Execute_TroopTrainingDiscipline_AwardsTroopCapacity()
        {
            Officer officer = CreateOfficer(troopSkill: 100);
            ResearchMission mission = CreateMission(officer, ResearchDiscipline.TroopTraining);

            mission.Execute(_game, new FixedRNG(0.0));

            Assert.Greater(
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.TroopTraining),
                0
            );
            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
        }

        [Test]
        public void Execute_MaxSkillOfficer_AwardsResearchCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 100);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(_game, new FixedRNG(0.99));

            Assert.Greater(_faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign), 0);
        }

        [Test]
        public void CanContinue_OwnedPlanet_ReturnsTrue()
        {
            Officer officer = CreateOfficer();
            ResearchMission mission = CreateMission(officer);

            Assert.IsTrue(mission.CanContinue(_game));
        }

        [Test]
        public void CanContinue_PlanetLost_ReturnsFalse()
        {
            Officer officer = CreateOfficer();
            ResearchMission mission = CreateMission(officer);

            _planet.OwnerInstanceID = "rebels";

            Assert.IsFalse(mission.CanContinue(_game));
        }

        [Test]
        public void Execute_Success_DoesNotIncrementLeadership()
        {
            Officer officer = CreateOfficer(shipSkill: 100);
            int leadershipBefore = officer.GetSkillValue(MissionParticipantSkill.Leadership);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(_game, new FixedRNG(0.0));

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

            mission.Execute(_game, new FixedRNG(0.0));

            Assert.IsTrue(
                mission.CanContinue(_game),
                "Mission should continue after successful research"
            );
        }

        [Test]
        public void CanContinue_AfterFailure_ReturnsTrue()
        {
            Officer officer = CreateOfficer(shipSkill: 10);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(_game, new FixedRNG(0.99));

            Assert.IsTrue(
                mission.CanContinue(_game),
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
                Discipline = ResearchDiscipline.ShipDesign,
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
            Assert.AreEqual(ResearchDiscipline.ShipDesign, deserialized.Discipline);
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
                Discipline = ResearchDiscipline.TroopTraining,
            };

            string xml = SerializationHelper.Serialize(mission);
            ResearchMission deserialized = SerializationHelper.Deserialize<ResearchMission>(xml);

            Assert.AreEqual("Research", deserialized.ConfigKey);
            Assert.AreEqual("Troop Training", deserialized.DisplayName);
            Assert.AreEqual(ResearchDiscipline.TroopTraining, deserialized.Discipline);
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
                Discipline = ResearchDiscipline.FacilityDesign,
            };

            string xml = SerializationHelper.Serialize(mission);
            ResearchMission deserialized = SerializationHelper.Deserialize<ResearchMission>(xml);

            Assert.AreEqual("Research", deserialized.ConfigKey);
            Assert.AreEqual("Facility Design", deserialized.DisplayName);
            Assert.AreEqual(ResearchDiscipline.FacilityDesign, deserialized.Discipline);
        }
    }
}
