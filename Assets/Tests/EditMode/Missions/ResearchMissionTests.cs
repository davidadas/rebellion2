using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Tests.Missions
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
            ResearchMission mission = new ResearchMission(
                "empire",
                planet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                type
            );
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
        public void GetAgentProbability_ReturnsResearchSkill()
        {
            Officer officer = CreateOfficer(shipSkill: 75);
            ResearchMission mission = CreateMission(officer, ManufacturingType.Ship);

            // Execute with RNG=0.0 guarantees success (0 <= 75)
            mission.Execute(game, new FixedRNG(0.0));

            // If success path ran, capacity should have increased
            Assert.Greater(faction.ResearchCapacity[ManufacturingType.Ship], 0);
        }

        [Test]
        public void Execute_Success_AwardsResearchCapacity()
        {
            Officer officer = CreateOfficer(shipSkill: 100);
            ResearchMission mission = CreateMission(officer);

            int before = faction.ResearchCapacity[ManufacturingType.Ship];
            mission.Execute(game, new FixedRNG(0.0));
            int after = faction.ResearchCapacity[ManufacturingType.Ship];

            Assert.Greater(after, before, "Successful research mission should award capacity");
        }

        [Test]
        public void Execute_Success_IncrementsResearchSkill()
        {
            Officer officer = CreateOfficer(shipSkill: 50);
            ResearchMission mission = CreateMission(officer);

            mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(51, officer.ShipResearch, "Research skill should increment by 1");
        }

        [Test]
        public void Execute_Failure_NoCapacityAwarded()
        {
            Officer officer = CreateOfficer(shipSkill: 10);
            ResearchMission mission = CreateMission(officer);

            // RNG=0.99 → 99 > 10 → failure
            mission.Execute(game, new FixedRNG(0.99));

            Assert.AreEqual(0, faction.ResearchCapacity[ManufacturingType.Ship]);
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
        public void Execute_TroopResearchType_AwardsTroopCapacity()
        {
            Officer officer = CreateOfficer(troopSkill: 100);
            ResearchMission mission = CreateMission(officer, ManufacturingType.Troop);

            mission.Execute(game, new FixedRNG(0.0));

            Assert.Greater(faction.ResearchCapacity[ManufacturingType.Troop], 0);
            Assert.AreEqual(0, faction.ResearchCapacity[ManufacturingType.Ship]);
        }

        [Test]
        public void Execute_NeverFoiled()
        {
            Officer officer = CreateOfficer(shipSkill: 100);
            ResearchMission mission = CreateMission(officer);

            // Even with high RNG, skill 100 should always succeed since foil = 0
            // RNG=0.99 → 99 <= 100 → success
            mission.Execute(game, new FixedRNG(0.99));

            Assert.Greater(faction.ResearchCapacity[ManufacturingType.Ship], 0);
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

            Assert.IsTrue(mission.CanContinue(game), "Mission should continue after successful research");
        }

        [Test]
        public void CanContinue_AfterFailure_ReturnsTrue()
        {
            Officer officer = CreateOfficer(shipSkill: 10);
            ResearchMission mission = CreateMission(officer);

            // RNG=0.99 → 99 > 10 → failure
            mission.Execute(game, new FixedRNG(0.99));

            Assert.IsTrue(mission.CanContinue(game), "Mission should continue after failed research");
        }

        [Test]
        public void Constructor_EnemyPlanet_Throws()
        {
            Officer officer = CreateOfficer();

            // Change ownership after officer is already placed
            planet.OwnerInstanceID = "rebels";

            Assert.Throws<System.InvalidOperationException>(() =>
            {
                new ResearchMission(
                    "empire",
                    planet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    ManufacturingType.Ship
                );
            });
        }

        [Test]
        public void ShipDesign_SerializesAndDeserializes()
        {
            ResearchMission mission = new ResearchMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                Name = "Ship Design",
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
            Assert.AreEqual("Ship Design", deserialized.Name);
            Assert.AreEqual("Ship Design", deserialized.DisplayName);
            Assert.AreEqual("PLANET1", deserialized.TargetInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Leadership, deserialized.ParticipantSkill);
            Assert.AreEqual(ManufacturingType.Ship, deserialized.ResearchType);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(15, deserialized.MaxProgress);
            Assert.AreEqual(7, deserialized.CurrentProgress);
        }

        [Test]
        public void TroopTraining_SerializesAndDeserializes()
        {
            ResearchMission mission = new ResearchMission
            {
                InstanceID = "MISSION2",
                OwnerInstanceID = "FACTION1",
                Name = "Troop Training",
                DisplayName = "Troop Training",
                TargetInstanceID = "PLANET1",
                ResearchType = ManufacturingType.Troop,
            };

            string xml = SerializationHelper.Serialize(mission);
            ResearchMission deserialized = SerializationHelper.Deserialize<ResearchMission>(xml);

            Assert.AreEqual("Troop Training", deserialized.Name);
            Assert.AreEqual("Troop Training", deserialized.DisplayName);
            Assert.AreEqual(ManufacturingType.Troop, deserialized.ResearchType);
        }

        [Test]
        public void FacilityDesign_SerializesAndDeserializes()
        {
            ResearchMission mission = new ResearchMission
            {
                InstanceID = "MISSION3",
                OwnerInstanceID = "FACTION1",
                Name = "Facility Design",
                DisplayName = "Facility Design",
                TargetInstanceID = "PLANET1",
                ResearchType = ManufacturingType.Building,
            };

            string xml = SerializationHelper.Serialize(mission);
            ResearchMission deserialized = SerializationHelper.Deserialize<ResearchMission>(xml);

            Assert.AreEqual("Facility Design", deserialized.Name);
            Assert.AreEqual("Facility Design", deserialized.DisplayName);
            Assert.AreEqual(ManufacturingType.Building, deserialized.ResearchType);
        }
    }
}
