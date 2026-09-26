using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public class OfficerTests
    {
        [Test]
        public void IsMovable_OnActiveMission_ReturnsFalse()
        {
            Officer officer = new Officer { OwnerInstanceID = "rebels" };
            StubMission mission = new StubMission();
            mission.MaxProgress = 5;
            mission.CurrentProgress = 0; // IsComplete() == false
            officer.SetParent(mission);

            Assert.IsFalse(
                officer.IsMovable(),
                "Officer on an active mission should not be movable"
            );
        }

        [Test]
        public void IsMovable_OnCompletedMission_ReturnsTrue()
        {
            Officer officer = new Officer { OwnerInstanceID = "rebels" };
            StubMission mission = new StubMission();
            mission.MaxProgress = 1;
            mission.CurrentProgress = 1; // IsComplete() == true
            officer.SetParent(mission);

            Assert.IsTrue(officer.IsMovable(), "Officer on a completed mission should be movable");
        }

        [Test]
        public void IsMovable_WhenIdleAndNotOnMission_ReturnsTrue()
        {
            Officer officer = new Officer { Movement = null };
            bool isMovable = officer.IsMovable();
            Assert.IsTrue(isMovable);
        }

        [Test]
        public void IsMovable_WhenOnMission_ReturnsFalse()
        {
            StubMission mission = new StubMission();
            mission.MaxProgress = 5;
            mission.CurrentProgress = 0;
            Officer officer = new Officer { Movement = null };
            officer.SetParent(mission);
            bool isMovable = officer.IsMovable();
            Assert.IsFalse(isMovable);
        }

        [Test]
        public void GetBaseRating_ValidRating_ReturnsCorrectValue()
        {
            Officer officer = new Officer();
            officer.SetBaseRating(SkillRating.Diplomacy, 10);
            int ratingValue = officer.GetBaseRating(SkillRating.Diplomacy);
            Assert.AreEqual(10, ratingValue);
        }

        [Test]
        public void SetBaseRating_ValidRating_UpdatesValue()
        {
            Officer officer = new Officer();
            int updatedValue = officer.SetBaseRating(SkillRating.Combat, 15);
            Assert.AreEqual(15, updatedValue);
            Assert.AreEqual(15, officer.GetBaseRating(SkillRating.Combat));
        }

        [Test]
        public void GetEffectiveRating_Diplomacy_AppliesForceRankBonus()
        {
            Officer officer = new Officer { ForceValue = 20, ForceTrainingAdjustment = 10 };
            officer.SetBaseRating(SkillRating.Diplomacy, 50);

            Assert.AreEqual(65, officer.GetEffectiveRating(SkillRating.Diplomacy));
            Assert.AreEqual(50, officer.GetBaseRating(SkillRating.Diplomacy));
        }

        [Test]
        public void GetEffectiveRating_Espionage_AppliesForceRankBonus()
        {
            Officer officer = new Officer { ForceValue = 20, ForceTrainingAdjustment = 10 };
            officer.SetBaseRating(SkillRating.Espionage, 40);

            Assert.AreEqual(52, officer.GetEffectiveRating(SkillRating.Espionage));
            Assert.AreEqual(40, officer.GetBaseRating(SkillRating.Espionage));
        }

        [Test]
        public void GetEffectiveRating_Combat_AppliesForceRankBonusAndInjury()
        {
            Officer officer = new Officer
            {
                ForceValue = 20,
                ForceTrainingAdjustment = 10,
                InjuryPoints = 10,
            };
            officer.SetBaseRating(SkillRating.Combat, 50);

            Assert.AreEqual(55, officer.GetEffectiveRating(SkillRating.Combat));
            Assert.AreEqual(50, officer.GetBaseRating(SkillRating.Combat));
        }

        [Test]
        public void GetEffectiveRating_Combat_InjuryCannotGoBelowZero()
        {
            Officer officer = new Officer { InjuryPoints = 90 };
            officer.SetBaseRating(SkillRating.Combat, 50);

            Assert.AreEqual(0, officer.GetEffectiveRating(SkillRating.Combat));
        }

        [Test]
        public void GetEffectiveRating_Leadership_DoesNotApplyForceRankBonus()
        {
            Officer officer = new Officer { ForceValue = 50, ForceTrainingAdjustment = 50 };
            officer.SetBaseRating(SkillRating.Leadership, 40);

            Assert.AreEqual(40, officer.GetEffectiveRating(SkillRating.Leadership));
        }

        [Test]
        public void GetEffectiveRating_ShipResearch_DoesNotApplyForceRankBonus()
        {
            Officer officer = new Officer { ForceValue = 50, ForceTrainingAdjustment = 50 };
            officer.SetBaseRating(SkillRating.ShipResearch, 40);

            Assert.AreEqual(40, officer.GetEffectiveRating(SkillRating.ShipResearch));
        }

        [Test]
        public void IncrementBaseRating_WithForceBonus_IncrementsBaseRatingOnly()
        {
            Officer officer = new Officer { ForceValue = 50 };
            officer.SetBaseRating(SkillRating.Diplomacy, 40);

            officer.IncrementBaseRating(SkillRating.Diplomacy);

            Assert.AreEqual(41, officer.GetBaseRating(SkillRating.Diplomacy));
            Assert.AreEqual(61, officer.GetEffectiveRating(SkillRating.Diplomacy));
        }

        [Test]
        public void IsOnMission_WhenAssignedToMission_ReturnsTrue()
        {
            StubMission mission = new StubMission();
            Officer officer = new Officer();
            officer.SetParent(mission);
            bool isOnMission = officer.IsOnMission();
            Assert.IsTrue(isOnMission);
        }

        [Test]
        public void IsOnMission_WhenNotAssignedToMission_ReturnsFalse()
        {
            Officer officer = new Officer();
            bool isOnMission = officer.IsOnMission();
            Assert.IsFalse(isOnMission);
        }

        [Test]
        public void TryCapture_WhenStationary_SetsCaptureState()
        {
            Officer officer = new Officer();

            bool captured = officer.TryCapture("captor", canEscape: false);

            Assert.IsTrue(captured);
            Assert.IsTrue(officer.IsCaptured);
            Assert.AreEqual("captor", officer.CaptorInstanceID);
            Assert.IsFalse(officer.CanEscape);
        }

        [Test]
        public void TryCapture_WithDirectMovement_RejectsCapture()
        {
            Officer officer = new Officer
            {
                DisplayName = "Test Officer",
                Movement = new MovementState(),
            };
            bool captured = officer.TryCapture("captor");

            Assert.IsFalse(captured);
            Assert.IsFalse(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
        }

        [Test]
        public void TryCapture_AboardMovingFleet_RejectsCapture()
        {
            Officer officer = new Officer
            {
                DisplayName = "Test Officer",
                OwnerInstanceID = "owner",
            };
            CapitalShip ship = new CapitalShip { OwnerInstanceID = "owner" };
            Fleet fleet = new Fleet { OwnerInstanceID = "owner", Movement = new MovementState() };
            officer.SetParent(ship);
            ship.SetParent(fleet);
            bool captured = officer.TryCapture("captor");

            Assert.IsFalse(captured);
            Assert.IsFalse(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
        }

        [Test]
        public void SerializeDeserialize_Officer_PreservesAllData()
        {
            Officer originalOfficer = new Officer(canBetray: false, loyalty: 75)
            {
                IsMain = true,
                CurrentRank = OfficerRank.Admiral,
                Ratings = new Dictionary<SkillRating, int>
                {
                    { SkillRating.Espionage, 15 },
                    { SkillRating.Leadership, 25 },
                },
                Movement = null,
                IsForceSensitive = true,
                IsForceEligible = true,
                ForceValue = 75,
                ForceTrainingAdjustment = 10,
                NextEscapeAttemptTick = 725,
                MissionReturnParentInstanceID = "return-parent",
                MissionReturnLocationInstanceID = "return-location",
            };

            string xml = SerializationHelper.Serialize(originalOfficer);
            Officer deserializedOfficer = SerializationHelper.Deserialize<Officer>(xml);

            StringAssert.Contains("<CanBetray>False</CanBetray>", xml);
            StringAssert.Contains("<Loyalty>75</Loyalty>", xml);
            Assert.AreEqual(originalOfficer.IsMain, deserializedOfficer.IsMain, "IsMain mismatch");
            Assert.AreEqual(
                originalOfficer.CurrentRank,
                deserializedOfficer.CurrentRank,
                "CurrentRank mismatch"
            );
            Assert.AreEqual(
                originalOfficer.Movement,
                deserializedOfficer.Movement,
                "MovementStatus mismatch"
            );
            Assert.AreEqual(
                originalOfficer.IsForceSensitive,
                deserializedOfficer.IsForceSensitive,
                "IsForceSensitive mismatch"
            );
            Assert.AreEqual(
                originalOfficer.IsForceEligible,
                deserializedOfficer.IsForceEligible,
                "IsForceEligible mismatch"
            );
            Assert.AreEqual(
                originalOfficer.NextEscapeAttemptTick,
                deserializedOfficer.NextEscapeAttemptTick,
                "NextEscapeAttemptTick mismatch"
            );
            Assert.AreEqual(
                originalOfficer.ForceValue,
                deserializedOfficer.ForceValue,
                "ForceValue mismatch"
            );
            Assert.AreEqual(
                originalOfficer.ForceTrainingAdjustment,
                deserializedOfficer.ForceTrainingAdjustment,
                "ForceTrainingAdjustment mismatch"
            );
            Assert.AreEqual(
                originalOfficer.CanBetray,
                deserializedOfficer.CanBetray,
                "CanBetray mismatch"
            );
            Assert.AreEqual(
                originalOfficer.Loyalty,
                deserializedOfficer.Loyalty,
                "Loyalty mismatch"
            );
            Assert.AreEqual(
                originalOfficer.MissionReturnParentInstanceID,
                deserializedOfficer.MissionReturnParentInstanceID
            );
            Assert.AreEqual(
                originalOfficer.MissionReturnLocationInstanceID,
                deserializedOfficer.MissionReturnLocationInstanceID
            );
            Assert.AreEqual(25, deserializedOfficer.GetEffectiveRating(SkillRating.Leadership));
            Assert.AreEqual(
                originalOfficer.GetBaseRating(SkillRating.Espionage),
                deserializedOfficer.GetBaseRating(SkillRating.Espionage),
                "Espionage rating mismatch"
            );
            Assert.AreEqual(
                originalOfficer.GetBaseRating(SkillRating.Leadership),
                deserializedOfficer.GetBaseRating(SkillRating.Leadership),
                "Leadership rating mismatch"
            );
        }

        [Test]
        public void ShipResearch_SetAndGet_ReturnsCorrectValue()
        {
            Officer officer = new Officer();
            officer.ShipResearch = 50;
            Assert.AreEqual(50, officer.ShipResearch);
        }

        [Test]
        public void TroopResearch_SetAndGet_ReturnsCorrectValue()
        {
            Officer officer = new Officer();
            officer.TroopResearch = 30;
            Assert.AreEqual(30, officer.TroopResearch);
        }

        [Test]
        public void FacilityResearch_SetAndGet_ReturnsCorrectValue()
        {
            Officer officer = new Officer();
            officer.FacilityResearch = 40;
            Assert.AreEqual(40, officer.FacilityResearch);
        }

        [Test]
        public void IsRecruitable_SetToTrue_ReturnsTrue()
        {
            Officer officer = new Officer();
            officer.IsRecruitable = true;
            Assert.IsTrue(officer.IsRecruitable);
        }

        [Test]
        public void IsRecruitable_SetToFalse_ReturnsFalse()
        {
            Officer officer = new Officer();
            officer.IsRecruitable = false;
            Assert.IsFalse(officer.IsRecruitable);
        }

        [Test]
        public void IsCaptured_SetToTrue_ReturnsTrue()
        {
            Officer officer = new Officer();
            officer.IsCaptured = true;
            Assert.IsTrue(officer.IsCaptured);
        }

        [Test]
        public void IsCaptured_SetToFalse_ReturnsFalse()
        {
            Officer officer = new Officer();
            officer.IsCaptured = false;
            Assert.IsFalse(officer.IsCaptured);
        }

        [Test]
        public void TryAdjustLoyalty_BetrayableOfficer_ChangesAndClampsLoyalty()
        {
            Officer officer = new Officer(canBetray: true, loyalty: 75);

            Assert.IsTrue(officer.TryAdjustLoyalty(10));
            Assert.AreEqual(85, officer.Loyalty);
            Assert.IsTrue(officer.TryAdjustLoyalty(int.MaxValue));
            Assert.AreEqual(100, officer.Loyalty);
            Assert.IsTrue(officer.TryAdjustLoyalty(int.MinValue));
            Assert.AreEqual(0, officer.Loyalty);
            Assert.IsFalse(officer.TryAdjustLoyalty(-1));
            Assert.AreEqual(0, officer.Loyalty);
        }

        [Test]
        public void TryAdjustLoyalty_NonBetrayableOfficer_DoesNotChangeLoyalty()
        {
            Officer officer = new Officer(canBetray: false, loyalty: 75);

            Assert.IsFalse(officer.TryAdjustLoyalty(-10));
            Assert.AreEqual(75, officer.Loyalty);
        }

        [Test]
        public void CanPerformMission_AnyMissionTypeID_ReturnsTrue()
        {
            Officer officer = new Officer();

            Assert.IsTrue(officer.CanPerformMission(SabotageMission.MissionTypeID));
            Assert.IsTrue(officer.CanPerformMission(EspionageMission.MissionTypeID));
            Assert.IsTrue(officer.CanPerformMission(AssassinationMission.MissionTypeID));
        }

        [Test]
        public void GetVoicePath_ConfiguredEventPool_ReturnsConfiguredPath()
        {
            Officer officer = new Officer
            {
                VoiceSet = new OfficerVoiceSet
                {
                    PersonnelArrivedPaths = new List<string> { "configured" },
                },
            };

            Assert.AreEqual(
                "configured",
                officer.GetVoicePath(OfficerVoiceLineType.PersonnelArrived, null)
            );
        }
    }
}
