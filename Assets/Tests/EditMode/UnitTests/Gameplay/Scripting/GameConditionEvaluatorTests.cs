using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Messages;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.Scripting;
using Rebellion.SceneGraph;
using UnityEngine;

namespace Rebellion.Tests.Gameplay.Scripting
{
    [TestFixture]
    public class GameConditionEvaluatorTests
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

        [TestCase("All", false)]
        [TestCase("Any", true)]
        [TestCase("Not", true)]
        public void IsMet_DecisiveFirstOperand_DoesNotRollFollowingCondition(
            string operation,
            bool firstMatches
        )
        {
            GameRoot game = BuildConditionGame(out Planet planet, out _);
            planet.SetPopularSupport("empire", 50);
            game.Random = new QueueRNG(0.25, 0.75);
            List<GameConditional> conditions = new List<GameConditional>
            {
                new TickCountConditional { Ticks = firstMatches ? 0 : 1 },
                new RollAgainstPopularSupportConditional
                {
                    FactionInstanceID = "empire",
                    PlanetInstanceID = planet.InstanceID,
                },
            };
            GameConditional conditional = operation switch
            {
                "All" => new AllConditional { Conditionals = conditions },
                "Any" => new AnyConditional { Conditionals = conditions },
                "Not" => new NotConditional { Conditionals = conditions },
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
            GameConditionEvaluator.IsMet(conditional, game);

            Assert.AreEqual(0.25, game.Random.NextDouble());
        }

        [Test]
        public void IsMet_XorAlreadyHasTwoMatches_StillRollsFollowingCondition()
        {
            GameRoot game = BuildConditionGame(out Planet planet, out _);
            planet.SetPopularSupport("empire", 50);
            game.Random = new QueueRNG(0.25, 0.75);
            XorConditional conditional = new XorConditional
            {
                Conditionals = new List<GameConditional>
                {
                    new TickCountConditional { Ticks = 0 },
                    new TickCountConditional { Ticks = 0 },
                    new RollAgainstPopularSupportConditional
                    {
                        FactionInstanceID = "empire",
                        PlanetInstanceID = planet.InstanceID,
                    },
                },
            };

            bool result = GameConditionEvaluator.IsMet(conditional, game);

            Assert.IsFalse(result);
            Assert.AreEqual(0.75, game.Random.NextDouble());
        }

        [Test]
        public void IsMet_EvaluateEventVariableVariableTarget_UsesCurrentVariableValues()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            game.EventRuntime.SetVariable("bounty.roll", 42);
            game.EventRuntime.SetVariable("bounty.chance", 50);
            EvaluateEventVariableConditional conditional = new EvaluateEventVariableConditional
            {
                Key = "bounty.roll",
                Comparison = ComparisonOperator.LessThanOrEqual,
                CompareToVariable = "bounty.chance",
            };

            bool result = GameConditionEvaluator.IsMet(conditional, game);

            Assert.IsTrue(result);
        }

        [Test]
        public void IsMet_EvaluateEventVariableMultipleTargets_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateEventVariableConditional conditional = new EvaluateEventVariableConditional
            {
                Key = "bounty.roll",
                Comparison = ComparisonOperator.LessThanOrEqual,
                CompareTo = 30,
                CompareToVariable = "bounty.chance",
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameConditionEvaluator.IsMet(conditional, game)
            );

            StringAssert.Contains("exactly one CompareTo or CompareToVariable", exception.Message);
        }

        [Test]
        public void IsMet_EvaluateEventVariableMissingTarget_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateEventVariableConditional conditional = new EvaluateEventVariableConditional
            {
                Key = "bounty.roll",
                Comparison = ComparisonOperator.LessThanOrEqual,
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameConditionEvaluator.IsMet(conditional, game)
            );

            StringAssert.Contains("exactly one CompareTo or CompareToVariable", exception.Message);
        }

        [Test]
        public void IsMet_EvaluateBindingNullBinding_ReturnsFalse()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "sourceEventInstanceID",
                Comparison = ComparisonOperator.Equal,
                CompareTo = "expected-event",
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("sourceEventInstanceID", null);

            bool result = GameConditionEvaluator.IsMet(conditional, game, context);

            Assert.IsFalse(result);
        }

        [Test]
        public void IsMet_EvaluateBindingObjectBinding_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out Planet empirePlanet, out _);
            Officer emperor = EntityFactory.CreateOfficer("emperor", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(fleet, empirePlanet);
            game.AttachNode(ship, fleet);
            game.AttachNode(emperor, ship);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "unit",
                Comparison = ComparisonOperator.Equal,
                CompareTo = emperor.InstanceID,
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("unit", fleet);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameConditionEvaluator.IsMet(conditional, game, context)
            );

            StringAssert.Contains("cannot be compared", exception.Message);
        }

        [Test]
        public void IsMet_EvaluateBindingDifferentObjectBinding_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out Planet empirePlanet, out Planet rebelPlanet);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "destination",
                Comparison = ComparisonOperator.Equal,
                CompareTo = empirePlanet.InstanceID,
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("destination", rebelPlanet);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameConditionEvaluator.IsMet(conditional, game, context)
            );

            StringAssert.Contains("cannot be compared", exception.Message);
        }

        [Test]
        public void IsMet_EvaluateBindingCompatibleBindingComparison_ReturnsTrue()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "first",
                Comparison = ComparisonOperator.GreaterThan,
                CompareToBinding = "second",
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("first", 80);
            context.Bind("second", 60);

            bool result = GameConditionEvaluator.IsMet(conditional, game, context);

            Assert.IsTrue(result);
        }

        [Test]
        public void IsMet_EvaluateBindingIncompatibleBindingComparison_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "first",
                Comparison = ComparisonOperator.Equal,
                CompareToBinding = "second",
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("first", 80);
            context.Bind("second", "80");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameConditionEvaluator.IsMet(conditional, game, context)
            );

            StringAssert.Contains("incompatible value types", exception.Message);
        }

        [Test]
        public void IsMet_EvaluateBindingEnumLiteralComparison_ReturnsTrue()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "comparison",
                Comparison = ComparisonOperator.Equal,
                CompareTo = "GreaterThan",
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("comparison", ComparisonOperator.GreaterThan);

            Assert.IsTrue(GameConditionEvaluator.IsMet(conditional, game, context));
        }

        [Test]
        public void IsMet_EvaluateBindingEnumAndStringBindings_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "first",
                Comparison = ComparisonOperator.Equal,
                CompareToBinding = "second",
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("first", ComparisonOperator.GreaterThan);
            context.Bind("second", "GreaterThan");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameConditionEvaluator.IsMet(conditional, game, context)
            );

            StringAssert.Contains("incompatible value types", exception.Message);
        }

        [Test]
        public void IsMet_EvaluateBindingOrderedEnumComparison_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "comparison",
                Comparison = ComparisonOperator.GreaterThan,
                CompareTo = "Equal",
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("comparison", ComparisonOperator.GreaterThan);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameConditionEvaluator.IsMet(conditional, game, context)
            );

            StringAssert.Contains("ordered comparisons only for numeric values", exception.Message);
        }

        [TestCase(ComparisonOperator.Equal, false)]
        [TestCase(ComparisonOperator.NotEqual, true)]
        [TestCase(ComparisonOperator.GreaterThan, false)]
        [TestCase(ComparisonOperator.GreaterThanOrEqual, false)]
        [TestCase(ComparisonOperator.LessThan, false)]
        [TestCase(ComparisonOperator.LessThanOrEqual, false)]
        public void IsMet_EvaluateBindingNullOptionalBinding_UsesPredicateSemantics(
            ComparisonOperator comparison,
            bool expected
        )
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "sourceEventInstanceID",
                Comparison = comparison,
                CompareTo = "EXPECTED_SOURCE",
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("sourceEventInstanceID", null);

            Assert.AreEqual(expected, GameConditionEvaluator.IsMet(conditional, game, context));
        }

        [Test]
        public void IsMet_HasEventActivatedActivationRecorded_ReturnsTrue()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            game.EventRuntime.GetState("activated").ActivationCount = 1;
            HasEventActivatedConditional conditional = new HasEventActivatedConditional
            {
                EventInstanceID = "activated",
            };

            Assert.IsTrue(GameConditionEvaluator.IsMet(conditional, game));
        }

        [Test]
        public void IsMet_IsEventCompletePersistedCompletionState_ReturnsTrue()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            game.EventRuntime.GetState("limited").IsComplete = true;
            IsEventCompleteConditional conditional = new IsEventCompleteConditional
            {
                EventInstanceID = "limited",
            };

            bool isComplete = GameConditionEvaluator.IsMet(conditional, game);

            Assert.IsTrue(isComplete);
        }

        [Test]
        public void IsMet_IsEventCompleteLoadedUnlimitedEvent_ReturnsFalse()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            game.GetEventPool().Add(new GameEvent { InstanceID = "unlimited" });
            game.EventRuntime.GetState("unlimited").ActivationCount = 10;
            IsEventCompleteConditional conditional = new IsEventCompleteConditional
            {
                EventInstanceID = "unlimited",
            };

            bool isComplete = GameConditionEvaluator.IsMet(conditional, game);

            Assert.IsFalse(isComplete);
        }

        [Test]
        public void IsMet_RollAgainstPopularSupportRollBelowSupport_ReturnsTrue()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            planet.PopularSupport["faction"] = 20;
            game.Random = new FixedRandomProvider(new[] { 0.19 });
            RollAgainstPopularSupportConditional conditional =
                new RollAgainstPopularSupportConditional
                {
                    FactionInstanceID = "faction",
                    PlanetBinding = "target",
                };
            GameEvaluationContext context = new GameEvaluationContext(
                "INFORMANTS",
                sourceEventInstanceID: "INFORMANTS",
                sourceKind: "Event"
            );
            context.Bind("target", planet);

            bool result = GameConditionEvaluator.IsMet(
                conditional,
                new GameConditionContext(game, context)
            );

            Assert.IsTrue(result);
        }

        [Test]
        public void IsMet_ShareParentDifferentImmediateParents_DoesNotMatch()
        {
            GameRoot game = BuildHierarchy(
                out Planet planet,
                out Fleet fleet,
                out CapitalShip ship
            );
            Officer planetOfficer = EntityFactory.CreateOfficer("planet-officer", "faction");
            Officer shipOfficer = EntityFactory.CreateOfficer("ship-officer", "faction");
            game.AttachNode(planetOfficer, planet);
            game.AttachNode(shipOfficer, ship);
            ShareParentConditional condition = new ShareParentConditional
            {
                Units = References(planetOfficer, shipOfficer),
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsFalse(isMet);
            Assert.AreSame(planet, fleet.GetParent());
        }

        [Test]
        public void IsMet_ShareAncestorSamePlanetWithDifferentImmediateParents_Matches()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out CapitalShip ship);
            Officer planetOfficer = EntityFactory.CreateOfficer("planet-officer", "faction");
            Officer shipOfficer = EntityFactory.CreateOfficer("ship-officer", "faction");
            game.AttachNode(planetOfficer, planet);
            game.AttachNode(shipOfficer, ship);
            ShareAncestorConditional condition = new ShareAncestorConditional
            {
                Type = SceneAncestorType.Planet,
                Units = References(planetOfficer, shipOfficer),
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        [Test]
        public void IsMet_HasParent_MatchingImmediateParent_ReturnsTrue()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, planet);
            HasParentConditional condition = new HasParentConditional
            {
                UnitInstanceID = officer.InstanceID,
                ParentInstanceID = planet.InstanceID,
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        [Test]
        public void IsMet_HasParent_NestedUnderCapitalShip_ReturnsFalse()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out CapitalShip ship);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, ship);
            HasParentConditional condition = new HasParentConditional
            {
                UnitInstanceID = officer.InstanceID,
                ParentInstanceID = planet.InstanceID,
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsFalse(isMet);
        }

        [Test]
        public void IsMet_HasParent_MissingReferences_ReturnsFalse()
        {
            HasParentConditional condition = new HasParentConditional
            {
                UnitInstanceID = "missing-unit",
                ParentInstanceID = "missing-parent",
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, _game);

            Assert.IsFalse(isMet);
        }

        [Test]
        public void IsMet_IsCapturedWithCaptor_UncapturedOfficerWithStaleCaptorDoesNotMatch()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.IsCaptured = false;
            officer.CaptorInstanceID = "captor";
            game.AttachNode(officer, planet);
            IsCapturedConditional condition = new IsCapturedConditional
            {
                OfficerInstanceID = officer.InstanceID,
                CaptorFactionInstanceID = "captor",
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsFalse(isMet);
        }

        [Test]
        public void IsMet_IsCapturedOptionalCaptor_QualifiesCapturedOfficerWhenProvided()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "captor";
            game.AttachNode(officer, planet);

            Assert.IsTrue(
                GameConditionEvaluator.IsMet(
                    new IsCapturedConditional { OfficerInstanceID = officer.InstanceID },
                    game
                )
            );
            Assert.IsFalse(
                GameConditionEvaluator.IsMet(
                    new IsCapturedConditional
                    {
                        OfficerInstanceID = officer.InstanceID,
                        CaptorFactionInstanceID = "other",
                    },
                    game
                )
            );
        }

        [Test]
        public void IsMet_IsKilledInactiveKilledOfficer_MatchesByRegisteredIdentity()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, planet);
            new PersonnelCommands(game, new PersonnelQueries(game)).KillOfficer(officer);
            IsKilledConditional condition = new IsKilledConditional
            {
                OfficerInstanceID = officer.InstanceID,
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        [Test]
        public void IsMet_IsActiveInactiveOfficer_ReturnsFalseWithoutLosingIdentity()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, planet);
            officer.IsEnabled = false;
            IsActiveConditional condition = new IsActiveConditional
            {
                NodeInstanceID = officer.InstanceID,
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsFalse(isMet);
            Assert.AreSame(
                officer,
                game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID, includeDisabled: true)
            );
        }

        [Test]
        public void IsMet_IsActiveActiveOfficer_ReturnsTrue()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, planet);
            IsActiveConditional condition = new IsActiveConditional
            {
                NodeInstanceID = officer.InstanceID,
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        [Test]
        public void IsMet_IsInTransitOfficerCarriedByMovingFleet_ReturnsTrue()
        {
            GameRoot game = BuildHierarchy(out _, out Fleet fleet, out CapitalShip ship);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, ship);
            fleet.Movement = new MovementState();
            IsInTransitConditional condition = new IsInTransitConditional
            {
                UnitInstanceID = officer.InstanceID,
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        [Test]
        public void IsMet_HasBuildingTypeInactivePlanetWithEnabledBuilding_ReturnsTrue()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            planet.EnergyCapacity = 1;
            Building building = new Building
            {
                InstanceID = "building",
                OwnerInstanceID = "faction",
                BuildingType = BuildingType.Defense,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            planet.IsEnabled = false;
            HasBuildingTypeConditional condition = new HasBuildingTypeConditional
            {
                PlanetInstanceID = planet.InstanceID,
                Type = BuildingType.Defense,
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        [Test]
        public void IsMet_HasBuildingTypeDisabledBuilding_ReturnsFalse()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            planet.EnergyCapacity = 1;
            Building building = new Building
            {
                InstanceID = "building",
                OwnerInstanceID = "faction",
                BuildingType = BuildingType.Defense,
                ManufacturingStatus = ManufacturingStatus.Complete,
                IsEnabled = false,
            };
            game.AttachNode(building, planet);
            HasBuildingTypeConditional condition = new HasBuildingTypeConditional
            {
                PlanetInstanceID = planet.InstanceID,
                Type = BuildingType.Defense,
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsFalse(isMet);
        }

        [Test]
        public void IsMet_HasForceRankConfiguredSemanticRank_UsesConfiguredMinimum()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.ForceValue = game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight);
            game.AttachNode(officer, planet);
            HasForceRankConditional condition = new HasForceRankConditional
            {
                OfficerInstanceID = officer.InstanceID,
                Comparison = ComparisonOperator.GreaterThanOrEqual,
                Rank = ForceRankLabel.ForceKnight,
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        [Test]
        public void IsMet_HasForceRankInactiveOfficer_UsesConfiguredMinimum()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.ForceValue = game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight);
            game.AttachNode(officer, planet);
            officer.IsEnabled = false;
            HasForceRankConditional condition = new HasForceRankConditional
            {
                OfficerInstanceID = officer.InstanceID,
                Comparison = ComparisonOperator.GreaterThanOrEqual,
                Rank = ForceRankLabel.ForceKnight,
            };

            bool isMet = GameConditionEvaluator.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        /// <summary>
        /// Builds game.
        /// </summary>
        /// <param name="empirePlanet">Receives the empire planet.</param>
        /// <param name="rebelPlanet">Receives the rebel planet.</param>
        /// <returns>The constructed game.</returns>
        private static GameRoot BuildConditionGame(out Planet empirePlanet, out Planet rebelPlanet)
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            empirePlanet = new Planet
            {
                InstanceID = "empire-planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            rebelPlanet = new Planet
            {
                InstanceID = "rebel-planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(empirePlanet, sector);
            game.AttachNode(rebelPlanet, sector);
            return game;
        }

        /// <summary>
        /// Builds hierarchy.
        /// </summary>
        /// <param name="planet">Receives the planet.</param>
        /// <param name="fleet">Receives the fleet.</param>
        /// <param name="ship">Receives the ship.</param>
        /// <returns>The constructed hierarchy.</returns>
        private static GameRoot BuildHierarchy(
            out Planet planet,
            out Fleet fleet,
            out CapitalShip ship
        )
        {
            GameConfig config = new GameConfig();
            config.Jedi.RankLabelByMinimumForceRank[100] = (int)ForceRankLabel.ForceKnight;
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "faction" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "faction",
                IsColonized = true,
            };
            fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "faction" };
            ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "faction",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return game;
        }

        /// <summary>
        /// Executes references.
        /// </summary>
        /// <param name="nodes">The nodes.</param>
        /// <returns>The result of references.</returns>
        private static List<GameUnitReference> References(params ISceneNode[] nodes) =>
            new List<GameUnitReference>(
                System.Array.ConvertAll(
                    nodes,
                    node => new GameUnitReference { UnitInstanceID = node.InstanceID }
                )
            );
    }
}
