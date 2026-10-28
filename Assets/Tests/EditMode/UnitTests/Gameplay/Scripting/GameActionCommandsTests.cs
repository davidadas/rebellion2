using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Scripting.Events;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.Scripting;
using Rebellion.SceneGraph;
using UnityEngine;

namespace Rebellion.Tests.Gameplay.Scripting
{
    [TestFixture]
    public class GameActionCommandsTests
    {
        private GameRoot _game;

        /// <summary>
        /// Creates a fresh game for each scripting case.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
        }

        [Test]
        public void Execute_PlaceUnitsInactiveExistingUnit_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet destination, out _);
            Officer officer = new Officer
            {
                InstanceID = "inactive-officer",
                OwnerInstanceID = "empire",
                IsEnabled = false,
            };
            game.AttachNode(officer, destination);
            PlaceUnitsAction action = new PlaceUnitsAction
            {
                UnitInstanceID = officer.InstanceID,
                DestinationInstanceID = destination.InstanceID,
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("requires existing units to be active", exception.Message);
        }

        [Test]
        public void Execute_ChangeOwnerWithPlanetsAndUnits_RejectsAmbiguousRequest()
        {
            GameRoot game = BuildGame(out _, out _);
            ChangeOwnerAction action = new ChangeOwnerAction
            {
                FactionInstanceID = "rebels",
                Planets = new List<GameSelector> { new SelectPlanets() },
                Units = new List<GameSelector> { new SelectOfficers() },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("exactly one", exception.Message);
        }

        [Test]
        public void Execute_SetNodeStateInactive_DisablesOfficerWithoutDetachingIt()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            game.AttachNode(officer, rebelPlanet);

            new SetNodeStateAction
            {
                InstanceID = officer.InstanceID,
                State = SceneNodeState.Inactive,
            }.Execute(game);

            Assert.AreSame(rebelPlanet, officer.GetParent());
            Assert.IsFalse(officer.IsActive());
        }

        [Test]
        public void Execute_SetNodeStateInactiveMissionParticipant_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "mission",
                OwnerInstanceID = "rebels",
                LocationInstanceID = rebelPlanet.InstanceID,
            };
            game.AttachNode(officer, rebelPlanet);
            game.AttachNode(mission, rebelPlanet);
            game.MoveNode(officer, mission);
            mission.Initiate(100);

            TestDelegate execute = () =>
                new SetNodeStateAction
                {
                    InstanceID = officer.InstanceID,
                    State = SceneNodeState.Inactive,
                }.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
            Assert.AreSame(mission, officer.GetParent());
            Assert.IsTrue(officer.IsActive());
        }

        [Test]
        public void Execute_SetNodeStateSelector_DisablesEveryMatchingOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer first = EntityFactory.CreateOfficer("first", "rebels");
            Officer second = EntityFactory.CreateOfficer("second", "rebels");
            game.AttachNode(first, rebelPlanet);
            game.AttachNode(second, rebelPlanet);

            new SetNodeStateAction
            {
                State = SceneNodeState.Inactive,
                Selectors = new List<GameSelector>
                {
                    new SelectOfficers { PlanetInstanceID = rebelPlanet.InstanceID },
                },
            }.Execute(game);

            Assert.IsFalse(first.IsActive());
            Assert.IsFalse(second.IsActive());
        }

        [Test]
        public void Execute_SetNodeStateActive_EnablesOfficerAtExistingParent()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            game.AttachNode(officer, rebelPlanet);
            officer.IsEnabled = false;

            List<GameResult> results = new SetNodeStateAction
            {
                InstanceID = officer.InstanceID,
                State = SceneNodeState.Active,
            }.Execute(game);

            Assert.IsEmpty(results);
            Assert.AreSame(rebelPlanet, officer.GetParent());
            Assert.IsTrue(officer.IsActive());
        }

        [Test]
        public void Execute_SetNodeStatePlanet_DisablesNonMovableNode()
        {
            GameRoot game = BuildGame(out _, out Planet planet);

            new SetNodeStateAction
            {
                InstanceID = planet.InstanceID,
                State = SceneNodeState.Inactive,
            }.Execute(game);

            Assert.IsFalse(planet.IsActive());
        }

        [Test]
        public void Execute_SetNodeStateInactiveOfficerSelector_EnablesMatchingOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            officer.IsCaptured = true;
            game.AttachNode(officer, rebelPlanet);
            officer.IsEnabled = false;
            SetNodeStateAction action = new SetNodeStateAction
            {
                State = SceneNodeState.Active,
                Selectors = new List<GameSelector>
                {
                    new SelectOfficers
                    {
                        PlanetInstanceID = rebelPlanet.InstanceID,
                        IsCaptured = true,
                        IncludeInactive = true,
                    },
                },
            };

            action.Execute(game);

            Assert.AreSame(rebelPlanet, officer.GetParent());
            Assert.IsTrue(officer.IsActive());
        }

        [Test]
        public void Execute_RevealToFactionSelectedOfficer_EmitsConcreteObservation()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            game.AttachNode(officer, empirePlanet);
            RevealToFactionAction action = new RevealToFactionAction
            {
                FactionInstanceID = "rebels",
                Targets = new List<GameSelector>
                {
                    new SelectOfficers { InstanceID = officer.InstanceID },
                },
            };
            GameEvaluationContext context = new GameEvaluationContext(
                "INFORMANTS",
                sourceEventInstanceID: "INFORMANTS",
                sourceKind: "Event"
            );

            List<GameResult> results = action.Execute(game, game.Random, context);

            IntelligenceRevealedResult intelligence = results
                .OfType<IntelligenceRevealedResult>()
                .Single();
            Assert.AreEqual("rebels", intelligence.Recipient.InstanceID);
            CollectionAssert.AreEqual(new[] { officer }, intelligence.Observations);
        }

        [Test]
        public void Execute_SendMessageRecipientOmitted_ThrowsException()
        {
            GameRoot game = BuildGame(out _, out _);
            SendMessageAction action = new SendMessageAction();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            Assert.AreEqual("SendMessage requires RecipientFactionInstanceID.", exception.Message);
        }

        [Test]
        public void Execute_SendMessageMultipleBackgroundSources_ThrowsException()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", rebelPlanet.OwnerInstanceID);
            game.AttachNode(officer, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = officer.InstanceID,
                BackgroundImage = new MessageBackgroundImage
                {
                    Key = "advice",
                    Path = "custom-background",
                },
            };

            Assert.Throws<InvalidOperationException>(() => action.Execute(game));
        }

        [Test]
        public void Execute_SendUnitsValidReferences_DefersMovementUntilActivationCompletes()
        {
            GameRoot game = BuildGame(out Planet destination, out Planet origin);
            Officer officer = EntityFactory.CreateOfficer("traveler", "rebels");
            game.AttachNode(officer, origin);
            SendUnitsAction action = new SendUnitsAction
            {
                UnitInstanceID = officer.InstanceID,
                DestinationInstanceID = destination.InstanceID,
            };

            action.Execute(game);

            Assert.AreSame(origin, officer.GetParent());
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void Execute_SendUnitsIncompatibleSelector_ThrowsPreciseError()
        {
            GameRoot game = BuildGame(out Planet destination, out _);
            SendUnitsAction action = new SendUnitsAction
            {
                DestinationInstanceID = destination.InstanceID,
                Units = new List<GameSelector>
                {
                    new SelectPlanets { InstanceID = destination.InstanceID },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("only movable units", exception.Message);
        }

        [Test]
        public void Execute_SetCaptureStatusIncompatibleSelector_ThrowsPreciseError()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                IsCaptured = true,
                CaptorFactionInstanceID = "rebels",
                Selectors = new List<GameSelector>
                {
                    new SelectPlanets { InstanceID = planet.InstanceID },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("only officers", exception.Message);
        }

        [Test]
        public void Execute_SetCaptureStatusNormalCapture_AllowsEscape()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            game.AttachNode(officer, planet);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
            };

            OfficerCaptureStateResult result = action
                .Execute(game)
                .OfType<OfficerCaptureStateResult>()
                .Single();

            Assert.IsTrue(officer.IsCaptured);
            Assert.AreEqual("empire", officer.CaptorInstanceID);
            Assert.IsTrue(officer.CanEscape);
            Assert.AreSame(officer, result.TargetOfficer);
        }

        [Test]
        public void Execute_SetCaptureStatusOfficerInTransit_ReturnsNoResult()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            officer.Movement = new MovementState();
            game.AttachNode(officer, planet);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
            };

            List<GameResult> results = action.Execute(game);

            Assert.IsFalse(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
            Assert.IsEmpty(results);
        }

        [Test]
        public void Execute_SetCaptureStatusMissionParticipant_RecordsParentAtCapture()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            DiplomacyMission mission = new DiplomacyMission { InstanceID = "mission" };
            game.AttachNode(mission, planet);
            game.AttachNode(officer, mission);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
            };

            OfficerCaptureStateResult result = action
                .Execute(game)
                .OfType<OfficerCaptureStateResult>()
                .Single();

            Assert.AreSame(mission, result.ParentAtCapture);
        }

        [Test]
        public void Execute_SetCaptureStatusAuthoredNonEscapingCapture_DisablesEscape()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            game.AttachNode(officer, planet);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
                CanEscape = false,
            };

            action.Execute(game);

            Assert.IsFalse(officer.CanEscape);
        }

        [Test]
        public void Execute_SetCaptureStatusRelease_ClearsCaptorAndCaptureOnlyState()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "empire";
            officer.CanEscape = false;
            game.AttachNode(officer, planet);
            officer.IsEnabled = false;
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = false,
            };

            action.Execute(game);

            Assert.IsFalse(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
            Assert.IsTrue(officer.CanEscape);
        }

        [Test]
        public void Execute_SetCaptureStatusRecaptureAfterRelease_RestoresDefaultEscapeState()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "empire";
            officer.CanEscape = false;
            game.AttachNode(officer, planet);
            new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = false,
            }.Execute(game);

            new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
            }.Execute(game);

            Assert.IsTrue(officer.IsCaptured);
            Assert.IsTrue(officer.CanEscape);
        }

        [Test]
        public void Execute_SetDisplayNameCapitalShip_MarksNameAsAssigned()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Fleet fleet = new Fleet
            {
                InstanceID = "fleet",
                OwnerInstanceID = planet.OwnerInstanceID,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                DisplayName = "Generic Ship",
                OwnerInstanceID = planet.OwnerInstanceID,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            SetDisplayNameAction action = new SetDisplayNameAction
            {
                TargetInstanceID = ship.InstanceID,
                Name = "Named Ship",
            };

            action.Execute(game);

            Assert.AreEqual("Named Ship", ship.DisplayName);
            Assert.IsTrue(ship.HasAssignedName);
        }

        [Test]
        public void Execute_SetOfficerImagesConfiguredValues_UpdatesOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            SetOfficerImagesAction action = new SetOfficerImagesAction
            {
                OfficerInstanceID = luke.InstanceID,
                DisplayImagePath = "jedi-display",
                SmallDisplayImagePath = "jedi-small-display",
                MessageImagePath = "jedi-message",
                EncyclopediaImagePath = "jedi-encyclopedia",
            };

            Assert.IsEmpty(action.Execute(game));

            Assert.AreEqual("jedi-display", luke.DisplayImagePath);
            Assert.AreEqual("jedi-small-display", luke.SmallDisplayImagePath);
            Assert.AreEqual("jedi-message", luke.MessageImagePath);
            Assert.AreEqual("jedi-encyclopedia", luke.EncyclopediaImagePath);
        }

        [Test]
        public void Execute_SetOfficerVoiceSetConfiguredValues_ReplacesSelectedVoicePools()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.VoiceSet.PersonnelArrivedPaths.Add("old");
            game.AttachNode(luke, rebelPlanet);
            SetOfficerVoiceSetAction action = new SetOfficerVoiceSetAction
            {
                OfficerInstanceID = luke.InstanceID,
                PersonnelArrived = new List<string> { "jedi-arrived" },
            };

            Assert.IsEmpty(action.Execute(game));

            CollectionAssert.AreEqual(
                new[] { "jedi-arrived" },
                luke.VoiceSet.PersonnelArrivedPaths
            );
        }

        [Test]
        public void Execute_IncreaseForceRankPercentOfEffectiveRank_AdjustsForceRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.ForceValue = 40;
            game.AttachNode(luke, rebelPlanet);
            IncreaseForceRankAction action = new IncreaseForceRankAction
            {
                OfficerInstanceID = luke.InstanceID,
                PercentOfEffective = 25,
            };

            List<GameResult> results = action.Execute(game);

            Assert.IsEmpty(results);
            Assert.AreEqual(50, luke.ForceValue);
        }

        [Test]
        public void Execute_ChangeOfficerRatingAmount_AdjustsStoredRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(SkillRating.Diplomacy, 40);
            game.AttachNode(luke, rebelPlanet);

            Assert.IsEmpty(
                new ChangeOfficerRatingAction
                {
                    OfficerInstanceID = luke.InstanceID,
                    Rating = SkillRating.Diplomacy,
                    Amount = 5,
                }.Execute(game)
            );

            Assert.AreEqual(45, luke.GetBaseRating(SkillRating.Diplomacy));
        }

        [Test]
        public void Execute_ChangeOfficerRatingPercentOfStoredRating_AdjustsStoredRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(SkillRating.ShipResearch, 40);
            game.AttachNode(luke, rebelPlanet);

            new ChangeOfficerRatingAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = SkillRating.ShipResearch,
                PercentOfStored = -25,
            }.Execute(game);

            Assert.AreEqual(30, luke.GetBaseRating(SkillRating.ShipResearch));
        }

        [Test]
        public void Execute_ChangeOfficerRatingMultipleAdjustmentModes_Throws()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);

            Assert.Throws<InvalidOperationException>(() =>
                new ChangeOfficerRatingAction
                {
                    OfficerInstanceID = luke.InstanceID,
                    Rating = SkillRating.Combat,
                    Amount = 5,
                    PercentOfStored = 10,
                }.Execute(game)
            );
        }

        [Test]
        public void Execute_SetForceEligibleEligibilityTransition_InitializesForceOnce()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer leia = EntityFactory.CreateOfficer("leia", "rebels");
            leia.IsForceSensitive = false;
            leia.IsForceEligible = false;
            leia.JediLevel = 10;
            leia.JediLevelVariance = 5;
            game.AttachNode(leia, rebelPlanet);
            SetForceSensitiveAction sensitivity = new SetForceSensitiveAction
            {
                OfficerInstanceID = leia.InstanceID,
            };
            SetForceEligibleAction eligibility = new SetForceEligibleAction
            {
                OfficerInstanceID = leia.InstanceID,
            };

            sensitivity.Execute(game);
            List<GameResult> results = eligibility.Execute(
                game,
                new FixedRandomProvider(new[] { 0.5 })
            );

            Assert.IsTrue(leia.IsForceSensitive);
            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(13, leia.ForceValue);
            Assert.IsEmpty(results);
            eligibility.Execute(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(13, leia.ForceValue);
        }

        [Test]
        public void Execute_ApplyOfficerInjuryInclusiveRange_AppliesRolledSeverity()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            ApplyOfficerInjuryAction action = new ApplyOfficerInjuryAction
            {
                OfficerInstanceID = luke.InstanceID,
                MinimumInjury = 1,
                MaximumInjury = 100,
            };

            OfficerInjuredResult result = action
                .Execute(game, new FixedRandomProvider(new[] { 0.49 }))
                .OfType<OfficerInjuredResult>()
                .Single();

            Assert.AreEqual(50, result.Severity);
            Assert.AreEqual(50, luke.InjuryPoints);
        }

        [Test]
        public void Execute_ChangeRawResourceNodesDefault_IncreasesExplicitAmount()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            ChangeRawResourceNodesAction action = new ChangeRawResourceNodesAction
            {
                PlanetBinding = "target",
                Amount = 1,
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("target", planet);

            List<GameResult> results = action.Execute(game, new SequenceRNG(), context);

            Assert.AreEqual(5, planet.NumRawResourceNodes);
            Assert.AreEqual(
                PlanetChangeCategory.RawMaterial,
                results.OfType<PlanetStatChangedResult>().Single().Category
            );
        }

        [Test]
        public void Execute_ChangeRawResourceNodesNeutralPlanet_ReportsNoFaction()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.OwnerInstanceID = null;
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            ChangeRawResourceNodesAction action = new ChangeRawResourceNodesAction
            {
                PlanetBinding = "target",
                Amount = 1,
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("target", planet);

            PlanetStatChangedResult result = action
                .Execute(game, new SequenceRNG(), context)
                .OfType<PlanetStatChangedResult>()
                .Single();

            Assert.IsNull(result.Faction);
            Assert.AreEqual(5, planet.NumRawResourceNodes);
        }

        [Test]
        public void Execute_ChangeRawResourceNodesBoundAmount_AppliesReusedInteger()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 4;
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("change", -2);
            ChangeRawResourceNodesAction action = new ChangeRawResourceNodesAction
            {
                PlanetInstanceID = planet.InstanceID,
                AmountBinding = "change",
            };

            action.Execute(game, new ThrowingRNG(), context);

            Assert.AreEqual(2, planet.NumRawResourceNodes);
        }

        [Test]
        public void Execute_ChangeEnergyCapacityRolledAmount_AppliesInclusiveIntegerRoll()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.EnergyCapacity = 8;
            ChangeEnergyCapacityAction action = new ChangeEnergyCapacityAction
            {
                PlanetInstanceID = planet.InstanceID,
                RollInteger = new RollInteger { Minimum = -3, Maximum = -1 },
            };

            action.Execute(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(6, planet.EnergyCapacity);
        }

        [Test]
        public void Execute_ChangePopularSupportIncrease_RebalancesOtherFaction()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.SetPopularSupport("empire", 60);
            planet.SetPopularSupport("rebels", 40);
            ChangePopularSupportAction action = new ChangePopularSupportAction
            {
                PlanetInstanceID = planet.InstanceID,
                FactionInstanceID = "empire",
                Amount = 10,
            };

            List<GameResult> results = action.Execute(game);

            Assert.AreEqual(70, planet.GetPopularSupport("empire"));
            Assert.AreEqual(30, planet.GetPopularSupport("rebels"));
            Assert.AreEqual(2, results.OfType<PlanetStatChangedResult>().Count());
            Assert.IsTrue(
                results
                    .OfType<PlanetStatChangedResult>()
                    .All(result => result.Category == PlanetChangeCategory.Loyalty)
            );
        }

        [Test]
        public void Execute_SetPopularSupportAbsoluteValue_PreservesUnallocatedSupport()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.SetPopularSupport("empire", 60);
            planet.SetPopularSupport("rebels", 40);
            SetPopularSupportAction action = new SetPopularSupportAction
            {
                PlanetInstanceID = planet.InstanceID,
                FactionInstanceID = "rebels",
                Support = 20,
            };

            action.Execute(game);

            Assert.AreEqual(60, planet.GetPopularSupport("empire"));
            Assert.AreEqual(20, planet.GetPopularSupport("rebels"));
        }

        [Test]
        public void Execute_DamagePlanetResourcesMinimumLoss_GuaranteesOnePointLoss()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 3;
            planet.EnergyCapacity = 3;
            DamagePlanetResourcesAction action = new DamagePlanetResourcesAction
            {
                PlanetBinding = "target",
                LossProbabilityPerResource = 0,
                MinimumTotalLoss = 1,
            };
            GameEvent gameEvent = new GameEvent { InstanceID = "disaster" };
            GameEvaluationContext context = new GameEvaluationContext(
                gameEvent?.InstanceID,
                sourceEventInstanceID: gameEvent?.InstanceID,
                sourceKind: "Event"
            );
            context.Bind("target", planet);

            List<GameResult> results = action.Execute(game, new FixedRNG(0.99), context);

            Assert.AreEqual(2, planet.NumRawResourceNodes);
            Assert.AreEqual(3, planet.EnergyCapacity);
            Assert.AreEqual(1, results.OfType<PlanetStatChangedResult>().Count());
        }

        [Test]
        public void Execute_DamagePlanetResourcesNaNProbabilityWithNoResources_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 0;
            planet.EnergyCapacity = 0;
            DamagePlanetResourcesAction action = new DamagePlanetResourcesAction
            {
                PlanetInstanceID = planet.InstanceID,
                LossProbabilityPerResource = double.NaN,
            };

            TestDelegate execute = () => action.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
        }

        [Test]
        public void Execute_DamagePlanetResourcesNegativeMinimumLossWithNoResources_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 0;
            planet.EnergyCapacity = 0;
            DamagePlanetResourcesAction action = new DamagePlanetResourcesAction
            {
                PlanetInstanceID = planet.InstanceID,
                LossProbabilityPerResource = 0.5,
                MinimumTotalLoss = -1,
            };

            TestDelegate execute = () => action.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
        }

        [Test]
        public void Execute_DestroyUnitsSelectedUnit_DeletesUnitFromGame()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                OwnerInstanceID = planet.OwnerInstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planet);
            DestroyUnitsAction action = new DestroyUnitsAction
            {
                Selectors = new List<GameSelector>
                {
                    new SelectRegiments { InstanceID = regiment.InstanceID },
                },
            };

            action.Execute(game, new FixedRNG(0), null);

            Assert.IsNull(regiment.GetParent());
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        [Test]
        public void Execute_DestroyUnitsParentAndChildSelected_DestroysSubtreeOnce()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            DestroyUnitsAction action = new DestroyUnitsAction
            {
                Selectors = new List<GameSelector>
                {
                    new SelectFleets { InstanceID = fleet.InstanceID },
                    new SelectCapitalShips { InstanceID = ship.InstanceID },
                },
            };

            List<GameResult> results = action.Execute(game);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID));
            Assert.IsNull(game.GetSceneNodeByInstanceID<CapitalShip>(ship.InstanceID));
            CollectionAssert.AreEquivalent(
                new ISceneNode[] { fleet, ship },
                results.OfType<GameObjectDestroyedResult>().Select(result => result.DestroyedObject)
            );
        }

        [Test]
        public void Execute_DestroyLastBlockadingFleet_ReportsBlockadeEnd()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Fleet fleet = EntityFactory.CreateFleet("blockading-fleet", "rebels");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "blockading-ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            DestroyUnitsAction action = new DestroyUnitsAction
            {
                Selectors = new List<GameSelector>
                {
                    new SelectFleets { InstanceID = fleet.InstanceID },
                },
            };

            List<GameResult> results = action.Execute(game);

            Assert.IsFalse(planet.IsBlockaded());
            BlockadeChangedResult blockade = results.OfType<BlockadeChangedResult>().Single();
            Assert.AreSame(planet, blockade.Planet);
            Assert.IsNull(blockade.BlockadingFleet);
            Assert.IsFalse(blockade.Blockaded);
        }

        [Test]
        public void Execute_OfficerTraitActions_UseEventOwnedAssignment()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "faction" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "faction" };
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            OfficerTraitDefinition trait = new OfficerTraitDefinition
            {
                ID = "leadership",
                LeadershipBonusPercent = 50,
            };
            officer.BindTraitDefinitions(
                new Dictionary<string, OfficerTraitDefinition> { [trait.ID] = trait }
            );
            officer.SetBaseRating(SkillRating.Leadership, 40);
            _game.AttachNode(sector, _game.GetGalaxyMap());
            _game.AttachNode(planet, sector);
            _game.AttachNode(officer, planet);
            GameEvent gameEvent = new GameEvent { InstanceID = "trait-event" };
            GameActionContext context = new GameActionContext(
                _game,
                _game.Random,
                new GameEvaluationContext(
                    gameEvent?.InstanceID,
                    sourceEventInstanceID: gameEvent?.InstanceID,
                    sourceKind: "Event"
                )
            );

            GameActionInterpreter.ExecuteAction(
                new ApplyOfficerTraitAction
                {
                    TraitID = trait.ID,
                    OfficerInstanceID = officer.InstanceID,
                },
                context,
                new GameActionCommands(context.Game, context.Random)
            );
            Assert.AreEqual(60, officer.GetEffectiveRating(SkillRating.Leadership));

            GameActionInterpreter.ExecuteAction(
                new RemoveOfficerTraitAction
                {
                    TraitID = trait.ID,
                    OfficerInstanceID = officer.InstanceID,
                },
                context,
                new GameActionCommands(context.Game, context.Random)
            );
            Assert.AreEqual(40, officer.GetEffectiveRating(SkillRating.Leadership));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Execute_TraitSelectorMatchesNoOfficers_PerformsNoChanges(bool apply)
        {
            GameAction action = CreateTraitAction(apply);
            if (action is ApplyOfficerTraitAction grant)
                grant.Selectors.Add(new SelectOfficers());
            else
                ((RemoveOfficerTraitAction)action).Selectors.Add(new SelectOfficers());
            GameActionContext context = CreateTraitContext();

            Assert.DoesNotThrow(() =>
                new GameActionCommands(_game, _game.Random).Execute(action, context)
            );
            Assert.IsEmpty(context.Results);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Execute_TraitActionHasNoOfficerSource_RejectsAction(bool apply)
        {
            GameAction action = CreateTraitAction(apply);

            Assert.Throws<InvalidOperationException>(() =>
                new GameActionCommands(_game, _game.Random).Execute(action, CreateTraitContext())
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Execute_TraitActionNamesMissingOfficer_RejectsAction(bool apply)
        {
            GameAction action = CreateTraitAction(apply);
            if (action is ApplyOfficerTraitAction grant)
                grant.OfficerInstanceID = "missing";
            else
                ((RemoveOfficerTraitAction)action).OfficerInstanceID = "missing";

            Assert.Throws<InvalidOperationException>(() =>
                new GameActionCommands(_game, _game.Random).Execute(action, CreateTraitContext())
            );
        }

        /// <summary>
        /// Creates either operation for the shared trait-targeting contract.
        /// </summary>
        /// <param name="apply">Whether to grant rather than remove the trait.</param>
        /// <returns>The trait action with no authored targets.</returns>
        private static GameAction CreateTraitAction(bool apply) =>
            apply
                ? new ApplyOfficerTraitAction { TraitID = "bonus" }
                : new RemoveOfficerTraitAction { TraitID = "bonus" };

        /// <summary>
        /// Creates a generic script source for trait action execution.
        /// </summary>
        /// <returns>The current action context.</returns>
        private GameActionContext CreateTraitContext() =>
            new(_game, _game.Random, new GameEvaluationContext("trait/source"));

        /// <summary>
        /// Builds game.
        /// </summary>
        /// <param name="empirePlanet">Receives the empire planet.</param>
        /// <param name="rebelPlanet">Receives the rebel planet.</param>
        /// <returns>The constructed game.</returns>
        private GameRoot BuildGame(out Planet empirePlanet, out Planet rebelPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.Galaxy);
            empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(empirePlanet, sector);
            rebelPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(rebelPlanet, sector);
            return game;
        }
    }
}
