using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public class OfficerTests
    {
        /// <summary>
        /// Verifies is movable on active mission returns false.
        /// </summary>
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

        /// <summary>
        /// Verifies is movable on completed mission returns true.
        /// </summary>
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

        /// <summary>
        /// Verifies is movable when idle and not on mission returns true.
        /// </summary>
        [Test]
        public void IsMovable_WhenIdleAndNotOnMission_ReturnsTrue()
        {
            Officer officer = new Officer { Movement = null };
            bool isMovable = officer.IsMovable();
            Assert.IsTrue(isMovable);
        }

        /// <summary>
        /// Verifies is movable when on mission returns false.
        /// </summary>
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

        /// <summary>
        /// Verifies get base rating valid rating returns correct value.
        /// </summary>
        [Test]
        public void GetBaseRating_ValidRating_ReturnsCorrectValue()
        {
            Officer officer = new Officer();
            officer.SetBaseRating(OfficerRating.Diplomacy, 10);
            int ratingValue = officer.GetBaseRating(OfficerRating.Diplomacy);
            Assert.AreEqual(10, ratingValue);
        }

        /// <summary>
        /// Verifies set base rating valid rating updates value.
        /// </summary>
        [Test]
        public void SetBaseRating_ValidRating_UpdatesValue()
        {
            Officer officer = new Officer();
            int updatedValue = officer.SetBaseRating(OfficerRating.Combat, 15);
            Assert.AreEqual(15, updatedValue);
            Assert.AreEqual(15, officer.GetBaseRating(OfficerRating.Combat));
        }

        /// <summary>
        /// Verifies get effective rating diplomacy applies force rank bonus.
        /// </summary>
        [Test]
        public void GetEffectiveRating_Diplomacy_AppliesForceRankBonus()
        {
            Officer officer = new Officer { ForceValue = 20, ForceTrainingAdjustment = 10 };
            officer.SetBaseRating(OfficerRating.Diplomacy, 50);

            Assert.AreEqual(65, officer.GetEffectiveRating(OfficerRating.Diplomacy));
            Assert.AreEqual(50, officer.GetBaseRating(OfficerRating.Diplomacy));
        }

        /// <summary>
        /// Verifies get effective rating espionage applies force rank bonus.
        /// </summary>
        [Test]
        public void GetEffectiveRating_Espionage_AppliesForceRankBonus()
        {
            Officer officer = new Officer { ForceValue = 20, ForceTrainingAdjustment = 10 };
            officer.SetBaseRating(OfficerRating.Espionage, 40);

            Assert.AreEqual(52, officer.GetEffectiveRating(OfficerRating.Espionage));
            Assert.AreEqual(40, officer.GetBaseRating(OfficerRating.Espionage));
        }

        /// <summary>
        /// Verifies get effective rating combat applies force rank bonus and injury.
        /// </summary>
        [Test]
        public void GetEffectiveRating_Combat_AppliesForceRankBonusAndInjury()
        {
            Officer officer = new Officer
            {
                ForceValue = 20,
                ForceTrainingAdjustment = 10,
                InjuryPoints = 10,
            };
            officer.SetBaseRating(OfficerRating.Combat, 50);

            Assert.AreEqual(55, officer.GetEffectiveRating(OfficerRating.Combat));
            Assert.AreEqual(50, officer.GetBaseRating(OfficerRating.Combat));
        }

        /// <summary>
        /// Verifies get effective rating combat injury cannot go below zero.
        /// </summary>
        [Test]
        public void GetEffectiveRating_Combat_InjuryCannotGoBelowZero()
        {
            Officer officer = new Officer { InjuryPoints = 90 };
            officer.SetBaseRating(OfficerRating.Combat, 50);

            Assert.AreEqual(0, officer.GetEffectiveRating(OfficerRating.Combat));
        }

        /// <summary>
        /// Verifies get effective rating leadership does not apply force rank bonus.
        /// </summary>
        [Test]
        public void GetEffectiveRating_Leadership_DoesNotApplyForceRankBonus()
        {
            Officer officer = new Officer { ForceValue = 50, ForceTrainingAdjustment = 50 };
            officer.SetBaseRating(OfficerRating.Leadership, 40);

            Assert.AreEqual(40, officer.GetEffectiveRating(OfficerRating.Leadership));
        }

        /// <summary>
        /// Verifies get effective rating ship research does not apply force rank bonus.
        /// </summary>
        [Test]
        public void GetEffectiveRating_ShipResearch_DoesNotApplyForceRankBonus()
        {
            Officer officer = new Officer { ForceValue = 50, ForceTrainingAdjustment = 50 };
            officer.SetBaseRating(OfficerRating.ShipResearch, 40);

            Assert.AreEqual(40, officer.GetEffectiveRating(OfficerRating.ShipResearch));
        }

        /// <summary>
        /// Verifies increment base rating with force bonus increments base rating only.
        /// </summary>
        [Test]
        public void IncrementBaseRating_WithForceBonus_IncrementsBaseRatingOnly()
        {
            Officer officer = new Officer { ForceValue = 50 };
            officer.SetBaseRating(OfficerRating.Diplomacy, 40);

            officer.IncrementBaseRating(OfficerRating.Diplomacy);

            Assert.AreEqual(41, officer.GetBaseRating(OfficerRating.Diplomacy));
            Assert.AreEqual(61, officer.GetEffectiveRating(OfficerRating.Diplomacy));
        }

        /// <summary>
        /// Verifies is on mission when assigned to mission returns true.
        /// </summary>
        [Test]
        public void IsOnMission_WhenAssignedToMission_ReturnsTrue()
        {
            StubMission mission = new StubMission();
            Officer officer = new Officer();
            officer.SetParent(mission);
            bool isOnMission = officer.IsOnMission();
            Assert.IsTrue(isOnMission);
        }

        /// <summary>
        /// Verifies is on mission when not assigned to mission returns false.
        /// </summary>
        [Test]
        public void IsOnMission_WhenNotAssignedToMission_ReturnsFalse()
        {
            Officer officer = new Officer();
            bool isOnMission = officer.IsOnMission();
            Assert.IsFalse(isOnMission);
        }

        /// <summary>
        /// Verifies serialize deserialize officer preserves all data.
        /// </summary>
        [Test]
        public void SerializeDeserialize_Officer_PreservesAllData()
        {
            Officer originalOfficer = new Officer
            {
                IsMain = true,
                CurrentRank = OfficerRank.Admiral,
                Ratings = new Dictionary<OfficerRating, int>
                {
                    { OfficerRating.Espionage, 15 },
                    { OfficerRating.Leadership, 25 },
                },
                Movement = null,
                IsForceSensitive = true,
                IsForceEligible = true,
                ForceValue = 75,
                ForceTrainingAdjustment = 10,
                CanBetray = false,
                NextEscapeAttemptTick = 725,
                MissionReturnParentInstanceID = "return-parent",
                MissionReturnLocationInstanceID = "return-location",
            };

            string xml = SerializationHelper.Serialize(originalOfficer);
            Officer deserializedOfficer = SerializationHelper.Deserialize<Officer>(xml);

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
                originalOfficer.MissionReturnParentInstanceID,
                deserializedOfficer.MissionReturnParentInstanceID
            );
            Assert.AreEqual(
                originalOfficer.MissionReturnLocationInstanceID,
                deserializedOfficer.MissionReturnLocationInstanceID
            );
            Assert.AreEqual(25, deserializedOfficer.GetEffectiveRating(OfficerRating.Leadership));
            Assert.AreEqual(
                originalOfficer.GetBaseRating(OfficerRating.Espionage),
                deserializedOfficer.GetBaseRating(OfficerRating.Espionage),
                "Espionage rating mismatch"
            );
            Assert.AreEqual(
                originalOfficer.GetBaseRating(OfficerRating.Leadership),
                deserializedOfficer.GetBaseRating(OfficerRating.Leadership),
                "Leadership rating mismatch"
            );
        }

        /// <summary>
        /// Verifies ship research set and get returns correct value.
        /// </summary>
        [Test]
        public void ShipResearch_SetAndGet_ReturnsCorrectValue()
        {
            Officer officer = new Officer();
            officer.ShipResearch = 50;
            Assert.AreEqual(50, officer.ShipResearch);
        }

        /// <summary>
        /// Verifies troop research set and get returns correct value.
        /// </summary>
        [Test]
        public void TroopResearch_SetAndGet_ReturnsCorrectValue()
        {
            Officer officer = new Officer();
            officer.TroopResearch = 30;
            Assert.AreEqual(30, officer.TroopResearch);
        }

        /// <summary>
        /// Verifies facility research set and get returns correct value.
        /// </summary>
        [Test]
        public void FacilityResearch_SetAndGet_ReturnsCorrectValue()
        {
            Officer officer = new Officer();
            officer.FacilityResearch = 40;
            Assert.AreEqual(40, officer.FacilityResearch);
        }

        /// <summary>
        /// Verifies is recruitable set to true returns true.
        /// </summary>
        [Test]
        public void IsRecruitable_SetToTrue_ReturnsTrue()
        {
            Officer officer = new Officer();
            officer.IsRecruitable = true;
            Assert.IsTrue(officer.IsRecruitable);
        }

        /// <summary>
        /// Verifies is recruitable set to false returns false.
        /// </summary>
        [Test]
        public void IsRecruitable_SetToFalse_ReturnsFalse()
        {
            Officer officer = new Officer();
            officer.IsRecruitable = false;
            Assert.IsFalse(officer.IsRecruitable);
        }

        /// <summary>
        /// Verifies is captured set to true returns true.
        /// </summary>
        [Test]
        public void IsCaptured_SetToTrue_ReturnsTrue()
        {
            Officer officer = new Officer();
            officer.IsCaptured = true;
            Assert.IsTrue(officer.IsCaptured);
        }

        /// <summary>
        /// Verifies is captured set to false returns false.
        /// </summary>
        [Test]
        public void IsCaptured_SetToFalse_ReturnsFalse()
        {
            Officer officer = new Officer();
            officer.IsCaptured = false;
            Assert.IsFalse(officer.IsCaptured);
        }

        /// <summary>
        /// Verifies is traitor set to true returns true.
        /// </summary>
        [Test]
        public void IsTraitor_SetToTrue_ReturnsTrue()
        {
            Officer officer = new Officer();
            officer.IsTraitor = true;
            Assert.IsTrue(officer.IsTraitor);
        }

        /// <summary>
        /// Verifies is traitor set to false returns false.
        /// </summary>
        [Test]
        public void IsTraitor_SetToFalse_ReturnsFalse()
        {
            Officer officer = new Officer();
            officer.IsTraitor = false;
            Assert.IsFalse(officer.IsTraitor);
        }

        /// <summary>
        /// Verifies loyalty set and get returns correct value.
        /// </summary>
        [Test]
        public void Loyalty_SetAndGet_ReturnsCorrectValue()
        {
            Officer officer = new Officer();
            officer.Loyalty = 75;
            Assert.AreEqual(75, officer.Loyalty);
        }

        /// <summary>
        /// Verifies can perform mission any mission type id returns true.
        /// </summary>
        [Test]
        public void CanPerformMission_AnyMissionTypeID_ReturnsTrue()
        {
            Officer officer = new Officer();

            Assert.IsTrue(officer.CanPerformMission(MissionTypeIDs.Sabotage));
            Assert.IsTrue(officer.CanPerformMission(MissionTypeIDs.Espionage));
            Assert.IsTrue(officer.CanPerformMission(MissionTypeIDs.Assassination));
        }

        /// <summary>
        /// Verifies get voice path configured event pool returns configured path.
        /// </summary>
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
