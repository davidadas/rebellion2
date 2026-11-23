using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay.Scripting;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay.Scripting
{
    [TestFixture]
    public class GameBindingResolverTests
    {
        [Test]
        public void Bind_DuplicateAlias_ConsumesRollBeforeRejectingAlias()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("count", 42);
            QueueRNG random = new QueueRNG(0.25, 0.75);
            GameBinding binding = new GameBinding
            {
                As = "count",
                RollInteger = new RollInteger { Minimum = 0, Maximum = 3 },
            };

            Assert.Throws<InvalidOperationException>(() =>
                GameBindingResolver.Bind(binding, game, random, context)
            );

            Assert.AreEqual(0.75, random.NextDouble());
            Assert.AreEqual(42, context.GetBinding<int>("count"));
        }

        [Test]
        public void Bind_NumericRanges_StoresRolledValues()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEvent gameEvent = new GameEvent();
            GameEvaluationContext context = new GameEvaluationContext(
                gameEvent?.InstanceID,
                sourceEventInstanceID: gameEvent?.InstanceID,
                sourceKind: "Event"
            );
            GameBinding integerBinding = new GameBinding
            {
                As = "count",
                RollInteger = new RollInteger { Minimum = 1, Maximum = 5 },
            };
            GameBinding doubleBinding = new GameBinding
            {
                As = "probability",
                RollDouble = new RollDouble { Minimum = 0.1, Maximum = 0.9 },
            };
            IRandomNumberProvider random = new FixedRandomProvider(new[] { 0.5, 0.5 });
            GameBindingResolver.Bind(integerBinding, game, random, context);
            GameBindingResolver.Bind(doubleBinding, game, random, context);

            Assert.AreEqual(3, context.GetBinding<int>("count"));
            Assert.AreEqual(0.5, context.GetBinding<double>("probability"), 0.0001);
        }

        [Test]
        public void Bind_TypedSources_StoresResolvedValues()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "faction" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                NumRawResourceNodes = 7,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", faction.InstanceID);
            officer.SetBaseRating(SkillRating.Combat, 82);
            officer.ForceValue = 41;
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(officer, planet);
            int expectedCombatRating = officer.GetEffectiveRating(SkillRating.Combat);
            int expectedForceRank = officer.ForceRank;
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            IRandomNumberProvider random = new FixedRandomProvider(new[] { 0.5 });
            GameBindingResolver.Bind(
                new GameBinding
                {
                    As = "combat",
                    Sources = new List<GameBindingSource>
                    {
                        new SkillRatingBindingSource
                        {
                            OfficerInstanceID = officer.InstanceID,
                            Rating = SkillRating.Combat,
                        },
                    },
                },
                game,
                random,
                context
            );
            GameBindingResolver.Bind(
                new GameBinding
                {
                    As = "force",
                    Sources = new List<GameBindingSource>
                    {
                        new OfficerForceBindingSource { OfficerInstanceID = officer.InstanceID },
                    },
                },
                game,
                random,
                context
            );
            GameBindingResolver.Bind(
                new GameBinding
                {
                    As = "resources",
                    Sources = new List<GameBindingSource>
                    {
                        new PlanetStatBindingSource
                        {
                            PlanetInstanceID = planet.InstanceID,
                            Stat = PlanetStat.RawResourceNodes,
                        },
                    },
                },
                game,
                random,
                context
            );
            GameBindingResolver.Bind(
                new GameBinding
                {
                    As = "officerCount",
                    Sources = new List<GameBindingSource>
                    {
                        new SelectionCountBindingSource
                        {
                            Selectors = new List<GameSelector>
                            {
                                new SelectOfficers { PlanetInstanceID = planet.InstanceID },
                            },
                        },
                    },
                },
                game,
                random,
                context
            );

            Assert.AreEqual(expectedCombatRating, context.GetBinding<int>("combat"));
            Assert.AreEqual(expectedForceRank, context.GetBinding<int>("force"));
            Assert.AreEqual(7, context.GetBinding<int>("resources"));
            Assert.AreEqual(1, context.GetBinding<int>("officerCount"));
        }

        [Test]
        public void Bind_TypedOfficerSourcesWithInactiveOfficer_StoresResolvedValues()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "faction" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", faction.InstanceID);
            officer.SetBaseRating(SkillRating.Combat, 82);
            officer.ForceValue = 41;
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(officer, planet);
            officer.IsEnabled = false;
            int expectedCombatRating = officer.GetEffectiveRating(SkillRating.Combat);
            int expectedForceRank = officer.ForceRank;
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            IRandomNumberProvider random = new FixedRandomProvider(new[] { 0.5 });
            GameBindingResolver.Bind(
                new GameBinding
                {
                    As = "combat",
                    Sources = new List<GameBindingSource>
                    {
                        new SkillRatingBindingSource
                        {
                            OfficerInstanceID = officer.InstanceID,
                            Rating = SkillRating.Combat,
                        },
                    },
                },
                game,
                random,
                context
            );
            GameBindingResolver.Bind(
                new GameBinding
                {
                    As = "force",
                    Sources = new List<GameBindingSource>
                    {
                        new OfficerForceBindingSource { OfficerInstanceID = officer.InstanceID },
                    },
                },
                game,
                random,
                context
            );

            Assert.AreEqual(expectedCombatRating, context.GetBinding<int>("combat"));
            Assert.AreEqual(expectedForceRank, context.GetBinding<int>("force"));
        }

        [Test]
        public void Bind_PlanetStatWithInactivePlanet_StoresResolvedValue()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet", NumRawResourceNodes = 7 };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            planet.IsEnabled = false;
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            GameBinding binding = new GameBinding
            {
                As = "resources",
                Sources = new List<GameBindingSource>
                {
                    new PlanetStatBindingSource
                    {
                        PlanetInstanceID = planet.InstanceID,
                        Stat = PlanetStat.RawResourceNodes,
                    },
                },
            };
            GameBindingResolver.Bind(
                binding,
                game,
                new FixedRandomProvider(new[] { 0.5 }),
                context
            );

            Assert.AreEqual(7, context.GetBinding<int>("resources"));
        }

        [Test]
        public void Roll_ExtremeFiniteDoubleRange_ReturnsFiniteValue()
        {
            RollDouble roll = new RollDouble
            {
                Minimum = -double.MaxValue,
                Maximum = double.MaxValue,
            };

            double result = GameBindingResolver.Roll(roll, new FixedRNG(0.5));

            Assert.IsFalse(double.IsNaN(result));
            Assert.IsFalse(double.IsInfinity(result));
            Assert.AreEqual(0, result);
        }

        [Test]
        public void Bind_LaterArgumentInvalid_RetainsEarlierBinding()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            DuelResult result = new DuelResult { EncounteredOfficer = officer };
            DuelCompletedTrigger trigger = new DuelCompletedTrigger
            {
                Bindings =
                {
                    new GameBinding { Argument = "FirstOfficer", As = "officer" },
                    new GameBinding { Argument = "Missing", As = "missing" },
                },
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );

            Assert.Throws<InvalidOperationException>(() =>
                GameBindingResolver.Bind(trigger, context, result)
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("officer"));
        }

        [Test]
        public void Bind_DuplicateAlias_RetainsFirstArgument()
        {
            Officer first = new Officer { InstanceID = "first" };
            Officer second = new Officer { InstanceID = "second" };
            DuelResult result = new DuelResult
            {
                EncounteredOfficer = first,
                OpposingOfficer = second,
            };
            DuelCompletedTrigger trigger = new DuelCompletedTrigger
            {
                Bindings =
                {
                    new GameBinding { Argument = "FirstOfficer", As = "officer" },
                    new GameBinding { Argument = "SecondOfficer", As = "officer" },
                },
            };
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );

            Assert.Throws<InvalidOperationException>(() =>
                GameBindingResolver.Bind(trigger, context, result)
            );

            Assert.AreSame(first, context.GetBinding<Officer>("officer"));
        }
    }
}
