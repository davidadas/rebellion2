using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class SpaceCombatObserverTests
    {
        private const string _firstID = "FNALL1";
        private const string _secondID = "FNEMP1";
        private GameRoot _game;
        private GameSession _session;
        private PlanetSector _sector;
        private Planet _planet;
        private Officer _firstOfficer;
        private Officer _defectedOfficer;
        private CapitalShip _lostShip;
        private SpaceCombatResult _result;

        private SpaceCombatSupportProfile Profile =>
            _game.Config.SupportShift.BattleSupport.Profiles[
                SpaceCombatSupportContext.FirstFactionEasy
            ];

        /// <summary>
        /// Creates a connected campaign with equal remaining forces and one accepted destruction.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TacticalTestData.ConfigureCampaignRules(TestConfig.Create()));
            _game.Summary.PlayerFactionID = _firstID;
            _game
                .GetFactions()
                .Add(
                    new Faction
                    {
                        InstanceID = _firstID,
                        Settings = new FactionSettings { BattleLossLoyaltyDivisor = 80 },
                    }
                );
            _game
                .GetFactions()
                .Add(
                    new Faction
                    {
                        InstanceID = _secondID,
                        Settings = new FactionSettings { BattleLossLoyaltyDivisor = 80 },
                    }
                );
            SpaceCombatSupportProfile profile = Profile;
            profile.Divisor = 10;
            profile.FirstSide = CreateSide();
            profile.SecondSide = CreateSide();
            _sector = new PlanetSector { InstanceID = "battle-sector" };
            _game.AttachNode(_sector, _game.Galaxy);
            _planet = AddPlanet("battle", _sector);
            AddShip("first-survivor", _firstID, 1000);
            AddShip("second-reserve", _secondID, 1000);
            _firstOfficer = AddOfficer("first-officer", _firstID);
            _defectedOfficer = AddOfficer("defected-officer", _secondID);
            _lostShip = new CapitalShip
            {
                InstanceID = "destroyed-ship",
                OwnerInstanceID = _secondID,
                UprisingDefense = 80,
            };
            _result = new SpaceCombatResult
            {
                IsTacticalCombat = true,
                Planet = _planet,
                AttackerOwnerInstanceID = _firstID,
                DefenderOwnerInstanceID = _secondID,
                AttackerOutcome = SpaceCombatSideOutcome.Active,
                DefenderOutcome = SpaceCombatSideOutcome.Destroyed,
                Tick = 42,
            };
            _result.AcceptedLosses.RecordDestroyedUnit(_lostShip);
            _session = new GameSession(_game, TacticalTestData.CreateCatalog(_game.Config));
        }

        /// <summary>
        /// Releases the session's result subscriptions.
        /// </summary>
        [TearDown]
        public void TearDown() => _session?.Dispose();

        [Test]
        public void HandleResults_ManualBattle_AppliesLoyaltyOnceAndPublishesSectorSupport()
        {
            List<GameResult> results = Publish();

            Assert.AreEqual(52, _firstOfficer.Loyalty);
            Assert.AreEqual(48, _defectedOfficer.Loyalty);
            Assert.AreEqual(_firstID, _defectedOfficer.OwnerInstanceID);
            Assert.AreEqual(86, _planet.GetPopularSupport(_firstID));
            PopularSupportShiftResult shift = results.OfType<PopularSupportShiftResult>().Single();
            Assert.AreSame(_planet, shift.Planet);
            Assert.AreEqual(16, shift.Shift);
            Assert.AreEqual(42, shift.Tick);
            Assert.That(
                results.OfType<PlanetStatChangedResult>().Any(item => item.Planet == _planet)
            );
        }

        [Test]
        public void HandleResults_SettledLossValue_UsesCurrentValueInsteadOfCombatSnapshots()
        {
            _result.DefendingUnits.Add(new CombatUnitSnapshot(_lostShip) { Destroyed = true });
            using IDisposable reaction = _session.Results.Subscribe<SpaceCombatResult>(_ =>
                _lostShip.UprisingDefense = 40
            );

            Publish();

            Assert.AreEqual(50, _firstOfficer.Loyalty);
            Assert.AreEqual(50, _defectedOfficer.Loyalty);
            Assert.AreEqual(78, _planet.GetPopularSupport(_firstID));
        }

        [Test]
        public void HandleResults_SettledLossOwnership_UsesTheFinalOwner()
        {
            using IDisposable reaction = _session.Results.Subscribe<SpaceCombatResult>(_ =>
                _lostShip.OwnerInstanceID = _firstID
            );

            Publish();

            Assert.AreEqual(49, _firstOfficer.Loyalty);
            Assert.AreEqual(51, _defectedOfficer.Loyalty);
            Assert.AreEqual(62, _planet.GetPopularSupport(_firstID));
        }

        [Test]
        public void HandleResults_CurrentMilitaryRoster_WeightsLossesAfterOrdinaryReactions()
        {
            Profile.FirstSide.LeadingGapScale = 100;
            _result.AcceptedLosses.RecordDestroyedUnit(
                new CapitalShip { OwnerInstanceID = _firstID, UprisingDefense = 20 }
            );
            using IDisposable reaction = _session.Results.Subscribe<SpaceCombatResult>(_ =>
                AddShip("late-survivor", _firstID, 2000)
            );

            Publish();

            Assert.AreEqual(82, _planet.GetPopularSupport(_firstID));
        }

        [Test]
        public void HandleResults_SectorCandidates_ExcludesIneligibleUnpopulatedDestroyedAndDisabledPlanets()
        {
            Planet eligible = AddPlanet("eligible", _sector);
            Planet ineligible = AddPlanet("ineligible", _sector);
            ineligible.InitializeSectorSupportEligibility(false);
            Planet unpopulated = AddPlanet("unpopulated", _sector);
            unpopulated.IsColonized = false;
            Planet destroyed = AddPlanet("destroyed", _sector);
            destroyed.IsDestroyed = true;
            Planet disabled = AddPlanet("disabled", _sector);
            disabled.IsEnabled = false;
            PlanetSector otherSector = new PlanetSector { InstanceID = "other-sector" };
            _game.AttachNode(otherSector, _game.Galaxy);
            Planet other = AddPlanet("other-sector-planet", otherSector);

            List<GameResult> results = Publish();

            Assert.AreEqual(86, eligible.GetPopularSupport(_firstID));
            foreach (
                Planet excluded in new[] { ineligible, unpopulated, destroyed, disabled, other }
            )
                Assert.AreEqual(70, excluded.GetPopularSupport(_firstID));
            Assert.AreEqual(2, results.OfType<PopularSupportShiftResult>().Count());
        }

        [Test]
        public void HandleResults_ComparisonSupportAtThreshold_ExcludesPlanetWithoutSuppressingLoyalty()
        {
            Profile.SecondSide.SupportThreshold = 30;

            List<GameResult> results = Publish();

            Assert.AreEqual(70, _planet.GetPopularSupport(_firstID));
            Assert.AreEqual(52, _firstOfficer.Loyalty);
            Assert.IsEmpty(results.OfType<PopularSupportShiftResult>());
        }

        [Test]
        public void HandleResults_SecondSideSurvives_SignsSupportRelativeToTheFirstFaction()
        {
            _result.AttackerOutcome = SpaceCombatSideOutcome.Destroyed;
            _result.DefenderOutcome = SpaceCombatSideOutcome.Active;
            _result.AcceptedLosses = new SpaceCombatLossLedger();
            _result.AcceptedLosses.RecordDestroyedUnit(
                new CapitalShip { OwnerInstanceID = _firstID, UprisingDefense = 20 }
            );

            List<GameResult> results = Publish();

            Assert.AreEqual(66, _planet.GetPopularSupport(_firstID));
            Assert.AreEqual(-4, results.OfType<PopularSupportShiftResult>().Single().Shift);
        }

        [Test]
        public void HandleResults_CarriedCompletedRegiment_AddsAcceptedLossAndAppliesMagnitudeCap()
        {
            _result.AcceptedLosses.RecordDestroyedUnit(
                new Regiment
                {
                    OwnerInstanceID = _secondID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    UprisingDefense = 40,
                }
            );

            Publish();

            Assert.AreEqual(52, _firstOfficer.Loyalty);
            Assert.AreEqual(90, _planet.GetPopularSupport(_firstID));
        }

        [TestCase(SpaceCombatSideOutcome.Active, SpaceCombatSideOutcome.Active)]
        [TestCase(SpaceCombatSideOutcome.Withdrawn, SpaceCombatSideOutcome.Withdrawn)]
        [TestCase(SpaceCombatSideOutcome.Destroyed, SpaceCombatSideOutcome.Destroyed)]
        public void HandleResults_NoExclusiveSurvivingSide_DoesNotApplyAftermath(
            SpaceCombatSideOutcome attacker,
            SpaceCombatSideOutcome defender
        )
        {
            _result.AttackerOutcome = attacker;
            _result.DefenderOutcome = defender;

            List<GameResult> results = Publish();

            Assert.AreEqual(50, _firstOfficer.Loyalty);
            Assert.AreEqual(50, _defectedOfficer.Loyalty);
            Assert.AreEqual(70, _planet.GetPopularSupport(_firstID));
            Assert.IsEmpty(results.OfType<PopularSupportShiftResult>());
        }

        [Test]
        public void HandleResults_AutomaticResult_KeepsExistingOwnershipRelativeLoyaltyAndSupport()
        {
            _result.IsTacticalCombat = false;
            _result.DefendingUnits.Add(new CombatUnitSnapshot(_lostShip) { Destroyed = true });

            List<GameResult> results = Publish();

            Assert.AreEqual(52, _firstOfficer.Loyalty);
            Assert.AreEqual(52, _defectedOfficer.Loyalty);
            Assert.AreEqual(70, _planet.GetPopularSupport(_firstID));
            Assert.IsEmpty(results.OfType<PopularSupportShiftResult>());
        }

        [Test]
        public void HandleResults_ProtectedOfficer_DoesNotChangeProtectedLoyalty()
        {
            Officer protectedOfficer = EntityFactory.CreateOfficer(
                "protected-officer",
                _firstID,
                canBetray: false,
                loyalty: 50
            );
            protectedOfficer.RecruitingFactionInstanceIDs.Add(_firstID);
            _game.AttachNode(protectedOfficer, _planet);

            Publish();

            Assert.AreEqual(50, protectedOfficer.Loyalty);
            Assert.AreEqual(48, _defectedOfficer.Loyalty);
        }

        [Test]
        public void HandleResults_NoAcceptedDestruction_DoesNotTreatHullDamageAsLostValue()
        {
            _result.AcceptedLosses = new SpaceCombatLossLedger();
            _result.ShipDamage.Add(
                new ShipDamageResult
                {
                    Ship = _lostShip,
                    HullBefore = 100,
                    HullAfter = 1,
                }
            );

            List<GameResult> results = Publish();

            Assert.AreEqual(50, _firstOfficer.Loyalty);
            Assert.AreEqual(70, _planet.GetPopularSupport(_firstID));
            Assert.IsEmpty(results.OfType<PopularSupportShiftResult>());
        }

        [Test]
        public void BeginTacticalCombat_MissingSupportProfile_RejectsBeforeDeployment()
        {
            SpaceCombatCommands commands = CreatePendingEncounter();
            _game.Config.SupportShift.BattleSupport = null;

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                commands.BeginTacticalCombat()
            );

            StringAssert.Contains("support profile", error.Message);
            Assert.IsNull(commands.ActiveTacticalCombat);
            Assert.IsTrue(commands.HasPendingDecision);
            Assert.AreEqual(50, _firstOfficer.Loyalty);
            Assert.AreEqual(70, _planet.GetPopularSupport(_firstID));
        }

        [Test]
        public void BeginTacticalCombat_MissingPlanetHistory_RejectsBeforeDeployment()
        {
            SpaceCombatCommands commands = CreatePendingEncounter();
            Planet unknown = new Planet { InstanceID = "old-save-planet", IsColonized = true };
            _game.AttachNode(unknown, _sector);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                commands.BeginTacticalCombat()
            );

            StringAssert.Contains("old-save-planet", error.Message);
            Assert.IsNull(commands.ActiveTacticalCombat);
            Assert.IsTrue(commands.HasPendingDecision);
            Assert.IsNull(unknown.IsSectorSupportEligible);
        }

        [Test]
        public void BeginTacticalCombat_CompleteAftermathInputs_DeploysWithoutApplyingConsequences()
        {
            SpaceCombatCommands commands = CreatePendingEncounter();

            Assert.IsNotNull(commands.BeginTacticalCombat());

            Assert.AreEqual(50, _firstOfficer.Loyalty);
            Assert.AreEqual(70, _planet.GetPopularSupport(_firstID));
        }

        /// <summary>
        /// Publishes the encounter through the campaign's ordinary and settled reaction phases.
        /// </summary>
        /// <returns>The encounter and all of its campaign reactions.</returns>
        private List<GameResult> Publish() =>
            _session.Pipeline.ProcessResults(new[] { _result }, false);

        /// <summary>
        /// Detects the prepared hostile fleets as a human encounter awaiting a command choice.
        /// </summary>
        /// <returns>The session's pending encounter owner.</returns>
        private SpaceCombatCommands CreatePendingEncounter()
        {
            _game.SetFactionController(_firstID, "player", PlayerControllerType.Human);
            SpaceCombatCommands commands = _session.GetService<SpaceCombatCommands>();
            Assert.IsNotEmpty(new SpaceCombatTickProcessor(commands).ProcessTick(_game));
            Assert.IsTrue(commands.HasPendingDecision);
            return commands;
        }

        /// <summary>
        /// Attaches a populated, eligible world whose initial support preserves its current owner.
        /// </summary>
        /// <param name="id">The world's unique identity.</param>
        /// <param name="sector">The containing sector.</param>
        /// <returns>The attached world.</returns>
        private Planet AddPlanet(string id, PlanetSector sector)
        {
            Planet planet = new Planet
            {
                InstanceID = id,
                OwnerInstanceID = _firstID,
                IsColonized = true,
            };
            planet.SetPopularSupport(_firstID, 70);
            planet.SetPopularSupport(_secondID, 30);
            planet.InitializeSectorSupportEligibility(true);
            _game.AttachNode(planet, sector);
            return planet;
        }

        /// <summary>
        /// Registers a completed military value in an independent fleet at the combat location.
        /// </summary>
        /// <param name="id">The ship's unique identity.</param>
        /// <param name="owner">The faction supplying its current ownership.</param>
        /// <param name="value">Its full military value.</param>
        private void AddShip(string id, string owner, int value)
        {
            Fleet fleet = new Fleet { InstanceID = id + "-fleet", OwnerInstanceID = owner };
            _game.AttachNode(fleet, _planet);
            _game.AttachNode(
                new CapitalShip
                {
                    InstanceID = id,
                    TypeID = owner == _firstID ? "ALCS008" : "CSEM006",
                    OwnerInstanceID = owner,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    UprisingDefense = value,
                    CurrentHullStrength = 100,
                    MaxHullStrength = 100,
                },
                fleet
            );
        }

        /// <summary>
        /// Attaches a mutable officer whose authored allegiance is independent of current ownership.
        /// </summary>
        /// <param name="id">The officer's unique identity.</param>
        /// <param name="recruitingFactionID">The retained recruitment-faction flag.</param>
        /// <returns>The attached officer.</returns>
        private Officer AddOfficer(string id, string recruitingFactionID)
        {
            Officer officer = EntityFactory.CreateOfficer(
                id,
                _firstID,
                canBetray: true,
                loyalty: 50
            );
            officer.RecruitingFactionInstanceIDs.Add(recruitingFactionID);
            _game.AttachNode(officer, _planet);
            return officer;
        }

        /// <summary>
        /// Creates a flat-weight profile for observable campaign aftermath assertions.
        /// </summary>
        /// <returns>The complete authored calculation inputs.</returns>
        private static SpaceCombatSupportSideConfig CreateSide() =>
            new()
            {
                LeadingFlat = 100,
                LeadingGapScale = 0,
                TrailingFlat = 100,
                TrailingGapScale = 0,
                MagnitudeCap = 20,
                SupportThreshold = 101,
            };
    }
}
