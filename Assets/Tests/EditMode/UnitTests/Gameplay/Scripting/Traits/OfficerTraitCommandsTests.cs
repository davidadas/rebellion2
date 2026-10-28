using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.Scripting;
using Rebellion.Gameplay.Scripting.Traits;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Gameplay.Scripting.Traits
{
    [TestFixture]
    public sealed class OfficerTraitCommandsTests
    {
        private GameRoot _game;
        private Faction _owner;
        private Planet _planet;
        private Officer _officer;
        private GameResultBus _results;

        /// <summary>
        /// Creates a synthetic graph without loading content-pack definitions.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.CurrentTick = 3;
            _owner = new Faction { InstanceID = "owner" };
            _game.GetFactions().Add(_owner);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _owner.InstanceID,
                IsColonized = true,
            };
            _game.AttachNode(_planet, sector);
            _officer = AddOfficer("officer");
            _results = new GameResultBus();
        }

        [Test]
        public void ProcessTick_ReservationAction_ProducesCurrentTickRequest()
        {
            OfficerTraitCommands commands = CreateCommands(CreateReservationTrait());

            OfficerReservedForAIResult result = commands
                .ProcessTick()
                .OfType<OfficerReservedForAIResult>()
                .Single();

            Assert.AreEqual(
                (_officer.InstanceID, _owner.InstanceID, _game.CurrentTick),
                (result.OfficerInstanceID, result.FactionInstanceID, result.Tick)
            );
        }

        [Test]
        public void ProcessTick_UnmetCondition_DoesNotReserveOfficer()
        {
            OfficerTraitDefinition trait = CreateReservationTrait();
            trait.Conditionals.Add(
                new HasParentConditional
                {
                    UnitInstanceID = _officer.InstanceID,
                    ParentInstanceID = "elsewhere",
                }
            );

            List<GameResult> results = CreateCommands(trait).ProcessTick();

            Assert.IsEmpty(results);
        }

        [Test]
        public void ProcessTick_SharedDefinition_ExecutesOnceRegardlessOfHolderCount()
        {
            OfficerTraitDefinition trait = CreateRatingTrait();
            _officer.Traits.Add(trait.ID);
            AddOfficer("second").Traits.Add(trait.ID);

            CreateCommands(trait).ProcessTick();

            Assert.AreEqual(41, _officer.GetBaseRating(SkillRating.Leadership));
        }

        [Test]
        public void ProcessTick_TriggeredDefinition_WaitsForMatchingResult()
        {
            OfficerTraitDefinition trait = CreateRatingTrait();
            trait.Triggers.Add(new UnitArrivedTrigger { UnitInstanceID = _officer.InstanceID });

            CreateCommands(trait).ProcessTick();

            Assert.AreEqual(40, _officer.GetBaseRating(SkillRating.Leadership));
        }

        [Test]
        public void ProcessResults_ExistingArrivalTrigger_ExecutesBoundAction()
        {
            OfficerTraitDefinition trait = CreateRatingTrait();
            trait.Triggers.Add(
                new UnitArrivedTrigger
                {
                    Bindings = new List<GameBinding>
                    {
                        new GameBinding { Argument = "Unit", As = "arriving" },
                    },
                }
            );
            trait.Actions.Clear();
            trait.Actions.Add(
                new ChangeOfficerRatingAction
                {
                    Rating = SkillRating.Leadership,
                    Amount = 1,
                    Selectors = new List<GameSelector>
                    {
                        new SelectBinding { Binding = "arriving" },
                    },
                }
            );

            CreateCommands(trait).ProcessResults(new[] { CreateArrival(_officer) });

            Assert.AreEqual(41, _officer.GetBaseRating(SkillRating.Leadership));
        }

        [Test]
        public void ProcessResults_UnrelatedArrival_DoesNotExecuteAction()
        {
            OfficerTraitDefinition trait = CreateRatingTrait();
            trait.Triggers.Add(new UnitArrivedTrigger { UnitInstanceID = _officer.InstanceID });

            CreateCommands(trait).ProcessResults(new[] { CreateArrival(AddOfficer("other")) });

            Assert.AreEqual(40, _officer.GetBaseRating(SkillRating.Leadership));
        }

        [Test]
        public void ProcessResults_OverlappingTriggers_ExecutesDefinitionOnce()
        {
            OfficerTraitDefinition trait = CreateRatingTrait();
            trait.Triggers.Add(new UnitArrivedTrigger());
            trait.Triggers.Add(new UnitArrivedTrigger { UnitInstanceID = _officer.InstanceID });

            CreateCommands(trait).ProcessResults(new[] { CreateArrival(_officer) });

            Assert.AreEqual(41, _officer.GetBaseRating(SkillRating.Leadership));
        }

        [Test]
        public void ProcessResults_IndependentResultsInSameTick_ExecutesForEach()
        {
            OfficerTraitDefinition trait = CreateRatingTrait();
            trait.Triggers.Add(new UnitArrivedTrigger());

            CreateCommands(trait)
                .ProcessResults(new[] { CreateArrival(_officer), CreateArrival(_officer) });

            Assert.AreEqual(42, _officer.GetBaseRating(SkillRating.Leadership));
        }

        [Test]
        public void ProcessResults_SelfProducedResult_StopsFeedback()
        {
            using OfficerTraitObserver observer = new(CreateCommands(CreateRevealTrait("reactor")));
            observer.Connect(_results);

            List<GameResult> settled = PublishWithLimit(2, CreateReveal());

            Assert.AreEqual(2, settled.Count);
        }

        [Test]
        public void ProcessResults_TwoDefinitionsProduceMatchingResults_StopsMutualFeedback()
        {
            using OfficerTraitObserver observer = new(
                CreateCommands(CreateRevealTrait("first"), CreateRevealTrait("second"))
            );
            observer.Connect(_results);

            List<GameResult> settled = PublishWithLimit(5, CreateReveal());

            Assert.AreEqual(5, settled.Count);
        }

        [Test]
        public void ProcessResults_SeparateRootResults_RemainIndependentlyEligible()
        {
            using OfficerTraitObserver observer = new(CreateCommands(CreateRevealTrait("reactor")));
            observer.Connect(_results);

            List<GameResult> settled = PublishWithLimit(4, CreateReveal(), CreateReveal());

            Assert.AreEqual(4, settled.Count);
        }

        [Test]
        public void ProcessResults_ReactionPassesThroughAnotherSubscriber_RetainsLoopProtection()
        {
            OfficerTraitDefinition trait = CreateReservationTrait();
            trait.Triggers.Add(new IntelligenceRevealedTrigger());
            using OfficerTraitObserver observer = new(CreateCommands(trait));
            observer.Connect(_results);
            using IDisposable intermediary = _results.Subscribe<OfficerReservedForAIResult>(
                _ => new List<GameResult> { CreateReveal() }
            );

            List<GameResult> settled = PublishWithLimit(3, CreateReveal());

            CollectionAssert.AreEqual(
                new[]
                {
                    typeof(IntelligenceRevealedResult),
                    typeof(OfficerReservedForAIResult),
                    typeof(IntelligenceRevealedResult),
                },
                settled.Select(result => result.GetType())
            );
        }

        [Test]
        public void ProcessTick_ProviderRemoved_RemovesGrantedModifier()
        {
            Officer recipient = AddOfficer("recipient");
            OfficerTraitDefinition bonus = new() { ID = "bonus", LeadershipBonusPercent = 25 };
            OfficerTraitCommands commands = CreateCommands(
                CreateConditionalGrant(recipient),
                bonus
            );
            commands.ProcessTick();
            Assert.AreEqual(50, recipient.GetEffectiveRating(SkillRating.Leadership));
            _game.DeleteNode(_officer);

            commands.ProcessTick();

            Assert.AreEqual(40, recipient.GetEffectiveRating(SkillRating.Leadership));
        }

        [Test]
        public void ProcessTick_ProviderRemoved_PreservesOtherSourcesGrant()
        {
            Officer recipient = AddOfficer("recipient");
            OfficerTraitDefinition bonus = new() { ID = "bonus", LeadershipBonusPercent = 25 };
            OfficerTraitCommands commands = CreateCommands(
                CreateConditionalGrant(recipient),
                bonus
            );
            commands.ProcessTick();
            recipient.GrantTrait(bonus.ID, "another-source");
            _game.DeleteNode(_officer);

            commands.ProcessTick();

            CollectionAssert.AreEqual(
                new[] { "another-source" },
                recipient.TraitGrants.Select(grant => grant.SourceID)
            );
        }

        [Test]
        public void ProcessTick_LoadedGrantWithMissingProvider_RemovesModifier()
        {
            Officer recipient = AddOfficer("recipient");
            OfficerTraitDefinition bonus = new() { ID = "bonus", LeadershipBonusPercent = 25 };
            OfficerTraitDefinition driver = CreateConditionalGrant(recipient);
            CreateCommands(driver, bonus).ProcessTick();
            _game.DeleteNode(_officer);
            GameRoot restored = SerializationHelper.Deserialize<GameRoot>(
                SerializationHelper.Serialize(_game)
            );
            using GameSession session = new(
                restored,
                TestGameData.Create(_game.Config, officerTraits: new[] { driver, bonus })
            );
            Officer restoredRecipient = restored.GetSceneNodeByInstanceID<Officer>(
                recipient.InstanceID
            );
            Assert.AreEqual(50, restoredRecipient.GetEffectiveRating(SkillRating.Leadership));

            session.GetService<OfficerTraitCommands>().ProcessTick();

            Assert.AreEqual(40, restoredRecipient.GetEffectiveRating(SkillRating.Leadership));
        }

        [Test]
        public void Constructor_TriggerWithoutActions_RejectsScript()
        {
            OfficerTraitDefinition trait = new()
            {
                ID = "invalid",
                Triggers = new List<GameTrigger> { new UnitArrivedTrigger() },
            };

            Assert.Throws<InvalidOperationException>(() => CreateCommands(trait));
        }

        [Test]
        public void Constructor_DuplicateBindingAliases_RejectsScript()
        {
            OfficerTraitDefinition trait = CreateRatingTrait();
            trait.Bindings.Add(
                new GameBinding
                {
                    As = "same",
                    RollInteger = new RollInteger { Minimum = 1, Maximum = 1 },
                }
            );
            trait.Bindings.Add(
                new GameBinding
                {
                    As = "same",
                    RollInteger = new RollInteger { Minimum = 1, Maximum = 1 },
                }
            );

            Assert.Throws<InvalidOperationException>(() => CreateCommands(trait));
        }

        /// <summary>
        /// Adds an ordinary fixture officer to the authoritative graph.
        /// </summary>
        /// <param name="id">The officer identity.</param>
        /// <returns>The attached officer.</returns>
        private Officer AddOfficer(string id)
        {
            Officer officer = EntityFactory.CreateOfficer(id, _owner.InstanceID);
            officer.SetBaseRating(SkillRating.Leadership, 40);
            _game.AttachNode(officer, _planet);
            return officer;
        }

        /// <summary>
        /// Creates production script execution with a synthetic catalog.
        /// </summary>
        /// <param name="traits">The fixture definitions.</param>
        /// <returns>The trait operations using the session bus.</returns>
        private OfficerTraitCommands CreateCommands(params OfficerTraitDefinition[] traits)
        {
            GameDataCatalog data = TestGameData.Create(_game.Config, officerTraits: traits);
            data.BindOfficerTraits(
                _game.GetRegisteredSceneNodesByType<Officer>(includeDisabled: true)
            );
            return new OfficerTraitCommands(
                _game,
                data,
                new GameActionCommands(_game, _game.Random),
                _results
            );
        }

        /// <summary>
        /// Creates a script requesting reservation of the fixture officer.
        /// </summary>
        /// <returns>The synthetic trait definition.</returns>
        private OfficerTraitDefinition CreateReservationTrait() =>
            new()
            {
                ID = "reservation",
                Actions = new List<GameAction>
                {
                    new ReserveOfficerAction { OfficerInstanceID = _officer.InstanceID },
                },
            };

        /// <summary>
        /// Creates an observable rating adjustment without deferred services.
        /// </summary>
        /// <returns>The synthetic trait definition.</returns>
        private OfficerTraitDefinition CreateRatingTrait() =>
            new()
            {
                ID = "rating",
                Actions = new List<GameAction>
                {
                    new ChangeOfficerRatingAction
                    {
                        OfficerInstanceID = _officer.InstanceID,
                        Rating = SkillRating.Leadership,
                        Amount = 1,
                    },
                },
            };

        /// <summary>
        /// Creates an arrival fact for a registered fixture officer.
        /// </summary>
        /// <param name="officer">The arriving officer.</param>
        /// <returns>The arrival result.</returns>
        private UnitArrivedResult CreateArrival(Officer officer) =>
            new()
            {
                Unit = officer,
                Destination = _planet,
                Tick = _game.CurrentTick,
            };

        /// <summary>
        /// Creates a reveal fact with a stable authoritative observation.
        /// </summary>
        /// <returns>The reveal result.</returns>
        private IntelligenceRevealedResult CreateReveal() =>
            new()
            {
                Recipient = _owner,
                Observations = new List<ISceneNode> { _officer },
                Tick = _game.CurrentTick,
            };

        /// <summary>
        /// Creates a script that emits the same kind of fact it consumes.
        /// </summary>
        /// <param name="id">The independent definition identity.</param>
        /// <returns>The synthetic trait definition.</returns>
        private OfficerTraitDefinition CreateRevealTrait(string id) =>
            new()
            {
                ID = id,
                Triggers = new List<GameTrigger> { new IntelligenceRevealedTrigger() },
                Actions = new List<GameAction>
                {
                    new RevealToFactionAction
                    {
                        FactionInstanceID = _owner.InstanceID,
                        Targets = new List<GameSelector>
                        {
                            new SelectOfficers { InstanceID = _officer.InstanceID },
                        },
                    },
                },
            };

        /// <summary>
        /// Builds an explicit grant-and-removal script controlled by a provider's current parent.
        /// </summary>
        /// <param name="recipient">The officer receiving the conditional bonus.</param>
        /// <returns>The synthetic trait definition.</returns>
        private OfficerTraitDefinition CreateConditionalGrant(Officer recipient) =>
            new()
            {
                ID = "provider",
                Actions = new List<GameAction>
                {
                    new IfAction
                    {
                        Conditionals = new List<GameConditional>
                        {
                            new HasParentConditional
                            {
                                UnitInstanceID = _officer.InstanceID,
                                ParentInstanceID = _planet.InstanceID,
                            },
                        },
                        Actions = new List<GameAction>
                        {
                            new ApplyOfficerTraitAction
                            {
                                TraitID = "bonus",
                                OfficerInstanceID = recipient.InstanceID,
                            },
                        },
                        Else = new List<GameAction>
                        {
                            new RemoveOfficerTraitAction
                            {
                                TraitID = "bonus",
                                OfficerInstanceID = recipient.InstanceID,
                            },
                        },
                    },
                },
            };

        /// <summary>
        /// Fails a feedback regression promptly rather than letting an unbounded publication hang the runner.
        /// </summary>
        /// <param name="maximumResults">The expected upper bound on delivered facts.</param>
        /// <param name="roots">The independent initiating facts.</param>
        /// <returns>The settled result sequence.</returns>
        private List<GameResult> PublishWithLimit(int maximumResults, params GameResult[] roots)
        {
            int delivered = 0;
            using IDisposable guard = _results.Subscribe<GameResult>(batch =>
            {
                delivered += batch.Count;
                Assert.LessOrEqual(
                    delivered,
                    maximumResults,
                    "Reaction publication did not settle."
                );
            });
            return _results.Publish(roots);
        }
    }
}
