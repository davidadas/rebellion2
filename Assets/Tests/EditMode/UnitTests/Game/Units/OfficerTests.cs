using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Missions;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public class OfficerTests
    {
        [TestCase(79, false)]
        [TestCase(80, true)]
        [TestCase(81, true)]
        public void ForceValue_RankCrossesDiscoveryThreshold_UpdatesBeforeNextTick(
            int rank,
            bool expectedDiscovery
        )
        {
            (GameRoot game, Officer officer) = RegisterDiscoveryOfficer(50);

            officer.ForceValue = rank;

            Assert.AreEqual(expectedDiscovery, officer.IsDiscoveringForceUser);
            Assert.AreEqual(
                expectedDiscovery ? game.CurrentTick + 1 : 0,
                officer.NextForceDiscoveryTick
            );
        }

        [TestCase(80, true)]
        [TestCase(50, false)]
        [TestCase(-1, false)]
        public void ForceValue_ZeroRank_RetainsDiscoveryState(int previousRank, bool expected)
        {
            (_, Officer officer) = RegisterDiscoveryOfficer(previousRank);
            int previousDueTick = officer.NextForceDiscoveryTick;

            officer.ForceValue = 0;

            Assert.AreEqual(expected, officer.IsDiscoveringForceUser);
            Assert.AreEqual(previousDueTick, officer.NextForceDiscoveryTick);
        }

        [Test]
        public void ForceTrainingAdjustment_ZeroEffectiveRank_RetainsDiscoveryTimer()
        {
            (_, Officer officer) = RegisterDiscoveryOfficer(80);
            int previousDueTick = officer.NextForceDiscoveryTick;

            officer.ForceTrainingAdjustment = -80;

            Assert.IsTrue(officer.IsDiscoveringForceUser);
            Assert.AreEqual(previousDueTick, officer.NextForceDiscoveryTick);
        }

        [Test]
        public void IsCaptured_ZeroRankScanner_CancelsDiscoveryImmediately()
        {
            (_, Officer officer) = RegisterDiscoveryOfficer(80);
            officer.ForceValue = 0;

            officer.IsCaptured = true;

            Assert.IsFalse(officer.IsDiscoveringForceUser);
            Assert.Zero(officer.NextForceDiscoveryTick);
        }

        [Test]
        public void IsCaptured_ReleasedScanner_ArmsFromActualReleaseTick()
        {
            (GameRoot game, Officer officer) = RegisterDiscoveryOfficer(80);
            officer.IsCaptured = true;
            game.CurrentTick = 70;

            officer.IsCaptured = false;

            Assert.IsTrue(officer.IsDiscoveringForceUser);
            Assert.AreEqual(71, officer.NextForceDiscoveryTick);
        }

        [Test]
        public void Movement_CompletedBetweenTicks_ArmsFromActualCompletionTick()
        {
            (GameRoot game, Officer officer) = RegisterDiscoveryOfficer(80);
            officer.Movement = new MovementState();
            game.CurrentTick = 70;

            officer.Movement = null;

            Assert.IsTrue(officer.IsDiscoveringForceUser);
            Assert.AreEqual(71, officer.NextForceDiscoveryTick);
        }

        [Test]
        public void IsEnabled_ChangedAtZeroRank_ReevaluatesDiscoveryEligibility()
        {
            (_, Officer officer) = RegisterDiscoveryOfficer(80);
            officer.ForceValue = 0;

            officer.IsEnabled = false;
            officer.IsEnabled = true;

            Assert.IsFalse(officer.IsDiscoveringForceUser);
            Assert.Zero(officer.NextForceDiscoveryTick);
        }

        [Test]
        public void SetParent_AssignedToMission_CancelsDiscoveryImmediately()
        {
            (_, Officer officer) = RegisterDiscoveryOfficer(80);
            Mission mission = new SabotageMission { InstanceID = "mission" };

            mission.AddChild(officer);
            officer.SetParent(mission);

            Assert.IsFalse(officer.IsDiscoveringForceUser);
            Assert.Zero(officer.NextForceDiscoveryTick);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Movement_ContainingTransportCompletes_ArmsPassengerDiscovery(bool fleetTransit)
        {
            (GameRoot game, Officer officer) = RegisterDiscoveryOfficer(80);
            Fleet fleet = new() { InstanceID = "fleet" };
            CapitalShip ship = new()
            {
                InstanceID = "ship",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            ship.AddChild(officer);
            officer.SetParent(ship);
            IMovable transport = fleetTransit ? fleet : ship;
            transport.Movement = new MovementState();
            Assert.IsFalse(officer.IsDiscoveringForceUser);
            game.CurrentTick = 70;

            transport.Movement = null;

            Assert.IsTrue(officer.IsDiscoveringForceUser);
            Assert.AreEqual(71, officer.NextForceDiscoveryTick);
        }

        [Test]
        public void IsEnabled_ContainingShipBecomesInactive_CancelsPassengerDiscovery()
        {
            (_, Officer officer) = RegisterDiscoveryOfficer(80);
            CapitalShip ship = new()
            {
                InstanceID = "ship",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.AddChild(officer);
            officer.SetParent(ship);

            ship.IsEnabled = false;

            Assert.IsFalse(officer.IsDiscoveringForceUser);
            Assert.Zero(officer.NextForceDiscoveryTick);
        }

        [Test]
        public void Serialize_ZeroRankScanner_RetainsDiscoveryStateAfterRegistration()
        {
            (_, Officer officer) = RegisterDiscoveryOfficer(80);
            officer.ForceValue = 0;
            Officer restored = SerializationHelper.Deserialize<Officer>(
                SerializationHelper.Serialize(officer)
            );
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Config.Jedi.DiscoveringForceUserThreshold = 80;

            game.AddSceneNodeByInstanceID(restored);

            Assert.IsTrue(restored.IsDiscoveringForceUser);
            Assert.AreEqual(43, restored.NextForceDiscoveryTick);
        }

        [TestCase(false, 79, false)]
        [TestCase(false, 80, true)]
        [TestCase(false, 81, true)]
        [TestCase(true, 79, false)]
        [TestCase(true, 80, true)]
        [TestCase(true, -1, false)]
        public void HealsFast_ForceRankThreshold_DoesNotRequireSensitivity(
            bool sensitive,
            int forceRank,
            bool expected
        )
        {
            Officer officer = RegisterHealingOfficer(
                new Officer { IsForceSensitive = sensitive, ForceValue = forceRank }
            );

            Assert.AreEqual(expected, officer.HealsFast());
        }

        [TestCase(80, true)]
        [TestCase(79, false)]
        [TestCase(-1, false)]
        public void HealsFast_RankFallsToZero_PreservesPreviousEligibility(int rank, bool expected)
        {
            Officer officer = RegisterHealingOfficer(new Officer { ForceValue = rank });

            officer.ForceValue = 0;

            Assert.AreEqual(expected, officer.HealsFast());
        }

        [Test]
        public void HealsFast_TrainingAdjustmentFallsToZero_PreservesPreviousEligibility()
        {
            Officer officer = RegisterHealingOfficer(new Officer { ForceTrainingAdjustment = 80 });

            officer.ForceTrainingAdjustment = 0;

            Assert.IsTrue(officer.HealsFast());
        }

        [Test]
        public void HealsFast_NewZeroRankWithZeroThreshold_RemainsDisabled()
        {
            Officer officer = RegisterHealingOfficer(new Officer(), threshold: 0);

            Assert.IsFalse(officer.HealsFast());
        }

        [Test]
        public void HealsFast_NonzeroRankFallsBelowThreshold_ClearsEligibility()
        {
            Officer officer = RegisterHealingOfficer(new Officer { ForceValue = 80 });

            officer.ForceValue = 79;

            Assert.IsFalse(officer.HealsFast());
        }

        [Test]
        public void HealsFast_BaseRatingAndInjuryChangeAtZero_PreserveEligibility()
        {
            Officer officer = RegisterHealingOfficer(new Officer { ForceValue = 80 });
            officer.ForceValue = 0;

            officer.SetBaseRating(SkillRating.Diplomacy, 50);
            officer.ApplyInjury(10, 100);
            officer.Heal(5);

            Assert.IsTrue(officer.HealsFast());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Deserialize_ZeroRankWithRetainedHealing_PreservesEligibilityInAnyElementOrder(
            bool reverseElementOrder
        )
        {
            Officer officer = RegisterHealingOfficer(new Officer { ForceValue = 80 });
            officer.ForceValue = 0;
            XElement xml = XElement.Parse(SerializationHelper.Serialize(officer));
            if (reverseElementOrder)
                xml.ReplaceNodes(xml.Elements().Reverse().ToArray());

            Officer restored = RegisterHealingOfficer(
                SerializationHelper.Deserialize<Officer>(xml.ToString())
            );

            Assert.Zero(restored.ForceRank);
            Assert.IsTrue(restored.HealsFast());
        }

        [Test]
        public void Deserialize_NonzeroRankWithoutHealingState_InitializesBeforeRankTransition()
        {
            XElement xml = XElement.Parse(
                SerializationHelper.Serialize(new Officer { ForceValue = 80 })
            );
            xml.Elements("FastHealing").Remove();
            Officer restored = RegisterHealingOfficer(
                SerializationHelper.Deserialize<Officer>(xml.ToString())
            );

            restored.ForceValue = 0;

            Assert.IsTrue(restored.HealsFast());
        }

        [Test]
        public void CreateCopy_ZeroRankWithRetainedHealing_PreservesIndependentState()
        {
            Officer officer = RegisterHealingOfficer(new Officer { ForceValue = 80 });
            officer.ForceValue = 0;

            Officer copy = (Officer)officer.CreateCopy();
            Assert.IsTrue(copy.HealsFast());
            copy.ForceValue = 1;

            Assert.IsFalse(copy.HealsFast());
            Assert.IsTrue(officer.HealsFast());
        }

        [Test]
        public void HealsFast_ConfigurationInjectedAfterRegistration_UsesInjectedRules()
        {
            GameRoot game = new();
            Officer officer = new() { InstanceID = "officer", ForceValue = 80 };
            game.AddSceneNodeByInstanceID(officer);
            GameConfig config = TestConfig.Create();
            config.Jedi.FastHealThreshold = 80;

            game.SetConfig(config);

            Assert.IsTrue(officer.HealsFast());
        }

        [Test]
        public void HealsFast_ConfigurationRebound_RetainsStateUntilRankChanges()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Config.Jedi.FastHealThreshold = 80;
            Officer officer = new() { InstanceID = "officer", ForceValue = 79 };
            game.AddSceneNodeByInstanceID(officer);
            Assert.IsFalse(officer.HealsFast());
            GameConfig changed = TestConfig.Create();
            changed.Jedi.FastHealThreshold = 60;

            game.SetConfig(changed);

            Assert.IsFalse(officer.HealsFast());
            officer.ForceValue = 80;
            Assert.IsTrue(officer.HealsFast());
        }

        [TestCase(32767, 32767)]
        [TestCase(32768, -32768)]
        [TestCase(65536, 0)]
        [TestCase(-32769, 32767)]
        public void ForceValue_SignedStorageBoundary_PreservesLowSixteenBits(
            int value,
            int expected
        )
        {
            Officer officer = new Officer { ForceValue = value };

            Assert.AreEqual(expected, officer.ForceValue);
        }

        [TestCase(32768, -32768)]
        [TestCase(-32769, 32767)]
        public void ForceTrainingAdjustment_SignedStorageBoundary_PreservesLowSixteenBits(
            int value,
            int expected
        )
        {
            Officer officer = new Officer { ForceTrainingAdjustment = value };

            Assert.AreEqual(expected, officer.ForceTrainingAdjustment);
        }

        [TestCase(32767, 1, -32768)]
        [TestCase(-32768, -1, 32767)]
        [TestCase(40, 10, 50)]
        public void ForceRank_TrainingAdjustedValue_UsesSignedSixteenBitSum(
            int experience,
            int training,
            int expected
        )
        {
            Officer officer = new Officer
            {
                ForceValue = experience,
                ForceTrainingAdjustment = training,
            };

            Assert.AreEqual(expected, officer.ForceRank);
        }

        [Test]
        public void Deserialize_HoldPositionConfiguration_ReadsSharedConditions()
        {
            Officer officer = SerializationHelper.Deserialize<Officer>(
                "<Officer><AIBehavior Type=\"HoldPosition\"><Conditionals>"
                    + "<HasParent UnitInstanceID=\"officer\" ParentInstanceID=\"planet\"/>"
                    + "</Conditionals></AIBehavior></Officer>"
            );

            Assert.AreEqual(OfficerAIBehaviorType.HoldPosition, officer.AIBehavior.Type);
            Assert.IsInstanceOf<HasParentConditional>(officer.AIBehavior.Conditionals[0]);
        }

        [Test]
        public void Serialize_AIBehaviorConfiguration_ExcludesContentFromSavedOfficer()
        {
            Officer officer = new() { AIBehavior = new OfficerAIBehavior() };

            string xml = SerializationHelper.Serialize(officer);

            StringAssert.DoesNotContain("AIBehavior", xml);
            Assert.IsNull(SerializationHelper.Deserialize<Officer>(xml).AIBehavior);
        }

        [TestCase(OfficerRank.None, "Han Solo")]
        [TestCase(OfficerRank.Commander, "Commander Solo")]
        [TestCase(OfficerRank.Admiral, "Admiral Solo")]
        [TestCase(OfficerRank.General, "General Solo")]
        public void GetDisplayName_CommandRank_ReturnsCommandQualifiedName(
            OfficerRank rank,
            string expected
        )
        {
            IGameEntity officer = new Officer { DisplayName = "Han Solo", CurrentRank = rank };

            Assert.AreEqual(expected, officer.GetDisplayName());
        }

        [Test]
        public void GetDisplayName_NameAlreadyIncludesCommandTitle_DoesNotRepeatTitle()
        {
            Officer officer = new Officer
            {
                DisplayName = "Admiral Ackbar",
                CurrentRank = OfficerRank.Admiral,
            };

            Assert.AreEqual("Admiral Ackbar", officer.GetDisplayName());
        }

        [Test]
        public void GetDisplayName_MultipartSurname_PreservesCompleteCommandName()
        {
            Officer officer = new Officer
            {
                DisplayName = "Garm Bel Iblis",
                CurrentRank = OfficerRank.General,
            };

            Assert.AreEqual("General Bel Iblis", officer.GetDisplayName());
        }

        [Test]
        public void GetDisplayName_WedgeAntilles_UsesSurnameCommandName()
        {
            Officer officer = new Officer
            {
                DisplayName = "Wedge Antilles",
                CurrentRank = OfficerRank.Commander,
            };

            Assert.AreEqual("Commander Antilles", officer.GetDisplayName());
        }

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
        public void ForceValue_ChangedToZero_PreservesEnhancedRatings()
        {
            Officer officer = CreateForceRatingOfficer();

            officer.ForceValue = 0;

            Assert.AreEqual(75, officer.GetEffectiveRating(SkillRating.Diplomacy));
            Assert.AreEqual(60, officer.GetEffectiveRating(SkillRating.Espionage));
            Assert.AreEqual(65, officer.GetEffectiveRating(SkillRating.Combat));
        }

        [Test]
        public void ForceTrainingAdjustment_CancelsExperience_PreservesEnhancedRatings()
        {
            Officer officer = CreateForceRatingOfficer();

            officer.ForceTrainingAdjustment = -50;

            Assert.AreEqual(0, officer.ForceRank);
            Assert.AreEqual(75, officer.GetEffectiveRating(SkillRating.Diplomacy));
            Assert.AreEqual(60, officer.GetEffectiveRating(SkillRating.Espionage));
            Assert.AreEqual(65, officer.GetEffectiveRating(SkillRating.Combat));
        }

        [TestCase(SkillRating.Diplomacy, 51)]
        [TestCase(SkillRating.Espionage, 41)]
        [TestCase(SkillRating.Combat, 41)]
        public void SetBaseRating_ChangedAtZeroForce_RefreshesOnlyChangedRating(
            SkillRating rating,
            int expected
        )
        {
            Officer officer = CreateForceRatingOfficer();
            officer.ForceValue = 0;

            officer.SetBaseRating(rating, officer.GetBaseRating(rating) + 1);

            Assert.AreEqual(expected, officer.GetEffectiveRating(rating));
            if (rating != SkillRating.Diplomacy)
                Assert.AreEqual(75, officer.GetEffectiveRating(SkillRating.Diplomacy));
            if (rating != SkillRating.Espionage)
                Assert.AreEqual(60, officer.GetEffectiveRating(SkillRating.Espionage));
            if (rating != SkillRating.Combat)
                Assert.AreEqual(65, officer.GetEffectiveRating(SkillRating.Combat));
        }

        [Test]
        public void SetBaseRating_UnchangedAtZeroForce_PreservesEnhancedRating()
        {
            Officer officer = CreateForceRatingOfficer();
            officer.ForceValue = 0;

            officer.SetBaseRating(SkillRating.Diplomacy, 50);

            Assert.AreEqual(75, officer.GetEffectiveRating(SkillRating.Diplomacy));
        }

        [Test]
        public void Heal_AtZeroForce_RefreshesCombatWithoutRefreshingOtherRatings()
        {
            Officer officer = CreateForceRatingOfficer();
            officer.ForceValue = 0;

            officer.Heal(1);

            Assert.AreEqual(41, officer.GetEffectiveRating(SkillRating.Combat));
            Assert.AreEqual(75, officer.GetEffectiveRating(SkillRating.Diplomacy));
            Assert.AreEqual(60, officer.GetEffectiveRating(SkillRating.Espionage));
        }

        [Test]
        public void InjuryPoints_UnchangedAtZeroForce_PreservesEnhancedCombat()
        {
            Officer officer = CreateForceRatingOfficer();
            officer.ForceValue = 0;

            officer.InjuryPoints = 10;

            Assert.AreEqual(65, officer.GetEffectiveRating(SkillRating.Combat));
        }

        [TestCase(SkillRating.Diplomacy)]
        [TestCase(SkillRating.Espionage)]
        [TestCase(SkillRating.Combat)]
        public void GetEffectiveRating_EnhancedValueExceedsSignedStorage_WrapsToSixteenBits(
            SkillRating rating
        )
        {
            Officer officer = new();
            officer.SetBaseRating(rating, 100);
            officer.ForceValue = short.MaxValue;

            Assert.AreEqual(-32669, officer.GetEffectiveRating(rating));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Deserialize_ZeroForceWithRetainedRatings_PreservesValuesInAnyElementOrder(
            bool reverseElementOrder
        )
        {
            Officer officer = CreateForceRatingOfficer();
            officer.ForceValue = 0;
            XElement xml = XElement.Parse(SerializationHelper.Serialize(officer));
            if (reverseElementOrder)
                xml.ReplaceNodes(xml.Elements().Reverse().ToArray());

            Officer restored = SerializationHelper.Deserialize<Officer>(xml.ToString());

            Assert.AreEqual(0, restored.ForceRank);
            Assert.AreEqual(10, restored.InjuryPoints);
            Assert.AreEqual(75, restored.GetEffectiveRating(SkillRating.Diplomacy));
            Assert.AreEqual(60, restored.GetEffectiveRating(SkillRating.Espionage));
            Assert.AreEqual(65, restored.GetEffectiveRating(SkillRating.Combat));
        }

        [Test]
        public void Deserialize_NonzeroForceWithoutStoredRatings_InitializesBeforeForceTransition()
        {
            XElement xml = XElement.Parse(
                SerializationHelper.Serialize(CreateForceRatingOfficer())
            );
            xml.Elements().Where(element => element.Name.LocalName.StartsWith("Enhanced")).Remove();
            Officer restored = SerializationHelper.Deserialize<Officer>(xml.ToString());

            restored.ForceValue = 0;

            Assert.AreEqual(75, restored.GetEffectiveRating(SkillRating.Diplomacy));
            Assert.AreEqual(60, restored.GetEffectiveRating(SkillRating.Espionage));
            Assert.AreEqual(65, restored.GetEffectiveRating(SkillRating.Combat));
        }

        [Test]
        public void CreateCopy_ZeroForceWithRetainedRatings_PreservesIndependentValues()
        {
            Officer officer = CreateForceRatingOfficer();
            officer.ForceValue = 0;

            Officer copy = (Officer)officer.CreateCopy();
            officer.Heal(1);

            Assert.AreEqual(75, copy.GetEffectiveRating(SkillRating.Diplomacy));
            Assert.AreEqual(60, copy.GetEffectiveRating(SkillRating.Espionage));
            Assert.AreEqual(65, copy.GetEffectiveRating(SkillRating.Combat));
        }

        [Test]
        public void GetEffectiveRating_Leadership_DoesNotApplyForceRankBonus()
        {
            Officer officer = new Officer { ForceValue = 50, ForceTrainingAdjustment = 50 };
            officer.SetBaseRating(SkillRating.Leadership, 40);

            Assert.AreEqual(40, officer.GetEffectiveRating(SkillRating.Leadership));
        }

        [Test]
        public void GetEffectiveRating_LeadershipTrait_AppliesConfiguredBonus()
        {
            Officer officer = CreateOfficerWithTrait(
                new OfficerTraitDefinition { ID = "leadership", LeadershipBonusPercent = 50 },
                "leadership"
            );
            officer.SetBaseRating(SkillRating.Leadership, 41);

            Assert.AreEqual(61, officer.GetEffectiveRating(SkillRating.Leadership));
        }

        [Test]
        public void GrantTrait_MultipleSources_DoesNotStackSameTrait()
        {
            Officer officer = CreateOfficerWithTrait(
                new OfficerTraitDefinition { ID = "leadership", LeadershipBonusPercent = 50 }
            );
            officer.SetBaseRating(SkillRating.Leadership, 40);

            Assert.IsTrue(officer.GrantTrait("leadership", "first-event"));
            Assert.IsTrue(officer.GrantTrait("leadership", "second-event"));

            Assert.AreEqual(60, officer.GetEffectiveRating(SkillRating.Leadership));
        }

        [Test]
        public void RevokeTrait_RemainingSource_PreservesTraitEffect()
        {
            Officer officer = CreateOfficerWithTrait(
                new OfficerTraitDefinition { ID = "leadership", LeadershipBonusPercent = 50 }
            );
            officer.SetBaseRating(SkillRating.Leadership, 40);
            officer.GrantTrait("leadership", "first-event");
            officer.GrantTrait("leadership", "second-event");

            Assert.IsTrue(officer.RevokeTrait("leadership", "first-event"));

            Assert.AreEqual(60, officer.GetEffectiveRating(SkillRating.Leadership));
        }

        [Test]
        public void GetPersonnelTravelTimePercent_TravelTrait_ReturnsConfiguredPercentage()
        {
            Officer officer = CreateOfficerWithTrait(
                new OfficerTraitDefinition { ID = "travel", PersonnelTravelTimePercent = 50 },
                "travel"
            );

            Assert.AreEqual(50, officer.GetPersonnelTravelTimePercent());
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

            bool captured = officer.TryCapture(
                "captor",
                TestGame.Create(TestConfig.Create()),
                new SequenceRNG(intValues: new[] { 48 }),
                canEscape: false
            );

            Assert.IsTrue(captured);
            Assert.IsTrue(officer.IsCaptured);
            Assert.AreEqual("captor", officer.CaptorInstanceID);
            Assert.IsFalse(officer.CanEscape);
        }

        [Test]
        public void TryCapture_NewNonEscapingCapture_ConsumesInitialIntervalBeforeCancellation()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Officer officer = new();
            FixedRandomProvider random = new(new[] { 0.25, 0.75 });

            officer.TryCapture("captor", game, random, canEscape: false);

            Assert.IsTrue(officer.IsCaptured);
            Assert.IsFalse(officer.CanEscape);
            Assert.AreEqual(0, officer.NextEscapeAttemptTick);
            Assert.AreEqual(0.75, random.NextDouble());
        }

        [Test]
        public void TryCapture_EscapableOfficer_ArmsTimerBeforeReturning()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.CurrentTick = 100;
            game.Config.Captive.EscapeAttemptInterval.Minimum = 48;
            game.Config.Captive.EscapeAttemptInterval.Maximum = 108;
            Officer officer = new Officer();

            officer.TryCapture("captor", game, new SequenceRNG(intValues: new[] { 108 }));

            Assert.AreEqual(208, officer.NextEscapeAttemptTick);
        }

        [Test]
        public void TryCapture_FixedEscapeInterval_DoesNotAdvanceRandomStream()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.CurrentTick = 100;
            game.Config.Captive.EscapeAttemptInterval.Minimum = 48;
            game.Config.Captive.EscapeAttemptInterval.Maximum = 48;
            Officer officer = new Officer();

            officer.TryCapture("captor", game, new ThrowingRNG());

            Assert.AreEqual(148, officer.NextEscapeAttemptTick);
        }

        [Test]
        public void TryCapture_ExistingEscapePermission_PreservesPendingTimer()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Officer officer = new Officer
            {
                IsCaptured = true,
                CanEscape = true,
                NextEscapeAttemptTick = 180,
            };

            officer.TryCapture("captor", game, new ThrowingRNG());

            Assert.AreEqual(180, officer.NextEscapeAttemptTick);
        }

        [Test]
        public void TryCapture_DisabledEscapePermission_CancelsPendingTimer()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Officer officer = new Officer { CanEscape = true, NextEscapeAttemptTick = 180 };

            officer.TryCapture("captor", game, new ThrowingRNG(), canEscape: false);

            Assert.AreEqual(0, officer.NextEscapeAttemptTick);
        }

        [Test]
        public void TryCapture_AfterRelease_ArmsNewEscapeTimer()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Config.Captive.EscapeAttemptInterval.Minimum = 48;
            game.Config.Captive.EscapeAttemptInterval.Maximum = 108;
            Officer officer = new Officer();
            SequenceRNG random = new SequenceRNG(intValues: new[] { 48, 108 });
            officer.TryCapture("captor", game, random);
            officer.ReleaseFromCaptivity();
            game.CurrentTick = 100;

            officer.TryCapture("captor", game, random);

            Assert.AreEqual(208, officer.NextEscapeAttemptTick);
        }

        [Test]
        public void TryCapture_WithDirectMovement_RejectsCapture()
        {
            Officer officer = new Officer
            {
                DisplayName = "Test Officer",
                Movement = new MovementState(),
            };
            bool captured = officer.TryCapture(
                "captor",
                TestGame.Create(TestConfig.Create()),
                new ThrowingRNG()
            );

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
            bool captured = officer.TryCapture(
                "captor",
                TestGame.Create(TestConfig.Create()),
                new ThrowingRNG()
            );

            Assert.IsFalse(captured);
            Assert.IsFalse(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
        }

        [Test]
        public void SerializeDeserialize_Officer_PreservesAllData()
        {
            Officer originalOfficer = EntityFactory.CreateOfficer(
                "officer",
                "faction",
                canBetray: false,
                loyalty: 75
            );
            originalOfficer.IsMain = true;
            originalOfficer.RosterOrder = 7;
            originalOfficer.CurrentRank = OfficerRank.Admiral;
            originalOfficer.Ratings = new Dictionary<SkillRating, int>
            {
                { SkillRating.Espionage, 15 },
                { SkillRating.Leadership, 25 },
            };
            originalOfficer.Movement = null;
            originalOfficer.IsForceSensitive = true;
            originalOfficer.IsForceEligible = true;
            originalOfficer.ForceValue = 75;
            originalOfficer.ForceGrowthRequiresEligibility = true;
            originalOfficer.ForceTrainingAdjustment = 10;
            originalOfficer.NextForceDiscoveryTick = 713;
            originalOfficer.ForceDiscoveryOrder = 4294967300L;
            originalOfficer.NextEscapeAttemptTick = 725;
            originalOfficer.MissionReturnParentInstanceID = "return-parent";
            originalOfficer.MissionReturnLocationInstanceID = "return-location";
            originalOfficer.Traits.Add("smuggler");
            originalOfficer.TraitGrants.Add(
                new OfficerTraitGrant { TraitID = "leadership", SourceID = "event" }
            );

            string xml = SerializationHelper.Serialize(originalOfficer);
            Officer deserializedOfficer = SerializationHelper.Deserialize<Officer>(xml);

            StringAssert.Contains("<CanBetray>False</CanBetray>", xml);
            StringAssert.Contains("<Loyalty>75</Loyalty>", xml);
            Assert.AreEqual(713, deserializedOfficer.NextForceDiscoveryTick);
            Assert.AreEqual(4294967300L, deserializedOfficer.ForceDiscoveryOrder);
            Assert.AreEqual(true, deserializedOfficer.ForceGrowthRequiresEligibility);
            CollectionAssert.AreEqual(originalOfficer.Traits, deserializedOfficer.Traits);
            Assert.AreEqual(1, deserializedOfficer.TraitGrants.Count);
            Assert.AreEqual("leadership", deserializedOfficer.TraitGrants[0].TraitID);
            Assert.AreEqual("event", deserializedOfficer.TraitGrants[0].SourceID);
            Assert.AreEqual(originalOfficer.IsMain, deserializedOfficer.IsMain, "IsMain mismatch");
            Assert.AreEqual(originalOfficer.RosterOrder, deserializedOfficer.RosterOrder);
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
            Officer officer = EntityFactory.CreateOfficer(
                "officer",
                "faction",
                canBetray: true,
                loyalty: 75
            );

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
            Officer officer = EntityFactory.CreateOfficer(
                "officer",
                "faction",
                canBetray: false,
                loyalty: 75
            );

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

        /// <summary>
        /// Registers an officer with the configured game rules used by healing eligibility.
        /// </summary>
        /// <param name="officer">The officer whose saved state is retained.</param>
        /// <param name="threshold">The configured minimum rank for fast healing.</param>
        /// <returns>The registered officer.</returns>
        private static Officer RegisterHealingOfficer(Officer officer, int threshold = 80)
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Config.Jedi.FastHealThreshold = threshold;
            officer.InstanceID ??= "healing-officer";
            game.AddSceneNodeByInstanceID(officer);
            return officer;
        }

        /// <summary>
        /// Registers a trainer with a configured discovery threshold and a known campaign clock.
        /// </summary>
        /// <param name="rank">The trainer's initial Force rank.</param>
        /// <returns>The game and its registered trainer.</returns>
        private static (GameRoot Game, Officer Officer) RegisterDiscoveryOfficer(int rank)
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Config.Jedi.DiscoveringForceUserThreshold = 80;
            game.CurrentTick = 42;
            Officer officer = new()
            {
                InstanceID = "discovery-officer",
                IsJediTrainer = true,
                ForceValue = rank,
            };
            game.AddSceneNodeByInstanceID(officer);
            return (game, officer);
        }

        /// <summary>
        /// Creates an officer whose three enhanced ratings differ from their base values.
        /// </summary>
        /// <returns>The officer with a nonzero Force rank and an injury.</returns>
        private static Officer CreateForceRatingOfficer()
        {
            Officer officer = new();
            officer.SetBaseRating(SkillRating.Diplomacy, 50);
            officer.SetBaseRating(SkillRating.Espionage, 40);
            officer.SetBaseRating(SkillRating.Combat, 50);
            officer.ForceValue = 50;
            officer.InjuryPoints = 10;
            return officer;
        }

        /// <summary>
        /// Creates an officer connected to one trait definition for focused behavior tests.
        /// </summary>
        /// <param name="definition">The trait definition.</param>
        /// <param name="inherentTraitID">The optional inherent trait identifier.</param>
        /// <returns>The configured officer.</returns>
        private static Officer CreateOfficerWithTrait(
            OfficerTraitDefinition definition,
            string inherentTraitID = null
        )
        {
            Officer officer = new Officer();
            if (!string.IsNullOrEmpty(inherentTraitID))
                officer.Traits.Add(inherentTraitID);
            officer.BindTraitDefinitions(
                new Dictionary<string, OfficerTraitDefinition> { [definition.ID] = definition }
            );
            return officer;
        }
    }
}
