using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventBindingTests
    {
        [Test]
        public void Bind_NumericRanges_StoresRolledValues()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            GameEvent gameEvent = new GameEvent();
            GameEventEvaluationContext context = new GameEventEvaluationContext(gameEvent, null);
            GameEventBinding integerBinding = new GameEventBinding
            {
                As = "count",
                RollInteger = new RollInteger { Minimum = 1, Maximum = 5 },
            };
            GameEventBinding doubleBinding = new GameEventBinding
            {
                As = "probability",
                RollDouble = new RollDouble { Minimum = 0.1, Maximum = 0.9 },
            };
            IRandomNumberProvider random = new FixedRandomProvider(new[] { 0.5, 0.5 });

            integerBinding.Bind(game, random, context);
            doubleBinding.Bind(game, random, context);

            Assert.AreEqual(3, context.GetBinding<int>("count"));
            Assert.AreEqual(0.5, context.GetBinding<double>("probability"), 0.0001);
        }

        [Test]
        public void Bind_TypedSources_StoresResolvedValues()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
            officer.SetBaseRating(OfficerRating.Combat, 82);
            officer.ForceValue = 41;
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(officer, planet);
            int expectedCombatRating = officer.GetEffectiveRating(OfficerRating.Combat);
            int expectedForceRank = officer.ForceRank;
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            IRandomNumberProvider random = new FixedRandomProvider(new[] { 0.5 });

            new GameEventBinding
            {
                As = "combat",
                Sources = new List<GameEventBindingSource>
                {
                    new OfficerRatingBindingSource
                    {
                        OfficerInstanceID = officer.InstanceID,
                        Rating = OfficerRating.Combat,
                    },
                },
            }.Bind(game, random, context);
            new GameEventBinding
            {
                As = "force",
                Sources = new List<GameEventBindingSource>
                {
                    new OfficerForceBindingSource { OfficerInstanceID = officer.InstanceID },
                },
            }.Bind(game, random, context);
            new GameEventBinding
            {
                As = "resources",
                Sources = new List<GameEventBindingSource>
                {
                    new PlanetStatBindingSource
                    {
                        PlanetInstanceID = planet.InstanceID,
                        Stat = PlanetStat.RawResourceNodes,
                    },
                },
            }.Bind(game, random, context);
            new GameEventBinding
            {
                As = "officerCount",
                Sources = new List<GameEventBindingSource>
                {
                    new SelectionCountBindingSource
                    {
                        Selectors = new List<GameEventSelector>
                        {
                            new SelectOfficers { PlanetInstanceID = planet.InstanceID },
                        },
                    },
                },
            }.Bind(game, random, context);

            Assert.AreEqual(expectedCombatRating, context.GetBinding<int>("combat"));
            Assert.AreEqual(expectedForceRank, context.GetBinding<int>("force"));
            Assert.AreEqual(7, context.GetBinding<int>("resources"));
            Assert.AreEqual(1, context.GetBinding<int>("officerCount"));
        }

        [Test]
        public void RoundTrip_NumericRanges_RestoresConcreteRolls()
        {
            GameEvent gameEvent = new GameEvent
            {
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "count",
                        RollInteger = new RollInteger { Minimum = 1, Maximum = 5 },
                    },
                    new GameEventBinding
                    {
                        As = "probability",
                        RollDouble = new RollDouble { Minimum = 0.1, Maximum = 0.9 },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            StringAssert.Contains("<RollInteger Minimum=\"1\" Maximum=\"5\" />", xml);
            StringAssert.Contains("<RollDouble Minimum=\"0.1\" Maximum=\"0.9\" />", xml);
            Assert.AreEqual(1, restored.Bindings[0].RollInteger.Minimum);
            Assert.AreEqual(5, restored.Bindings[0].RollInteger.Maximum);
            Assert.AreEqual(0.1, restored.Bindings[1].RollDouble.Minimum);
            Assert.AreEqual(0.9, restored.Bindings[1].RollDouble.Maximum);
        }

        [Test]
        public void RoundTrip_TypedSources_RestoresConcreteSources()
        {
            GameEvent gameEvent = new GameEvent
            {
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "combat",
                        Sources = new List<GameEventBindingSource>
                        {
                            new OfficerRatingBindingSource
                            {
                                OfficerInstanceID = "officer",
                                Rating = OfficerRating.Combat,
                            },
                        },
                    },
                    new GameEventBinding
                    {
                        As = "unitCount",
                        Sources = new List<GameEventBindingSource>
                        {
                            new SelectionCountBindingSource
                            {
                                Selectors = new List<GameEventSelector>
                                {
                                    new SelectOfficers { OwnerFactionInstanceID = "faction" },
                                },
                            },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            StringAssert.Contains(
                "<OfficerRating OfficerInstanceID=\"officer\" Rating=\"Combat\" />",
                xml
            );
            StringAssert.Contains("<SelectionCount>", xml);
            Assert.IsInstanceOf<OfficerRatingBindingSource>(restored.Bindings[0].Sources[0]);
            Assert.IsInstanceOf<SelectionCountBindingSource>(restored.Bindings[1].Sources[0]);
        }
    }
}
