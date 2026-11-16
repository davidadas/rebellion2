using System;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.SpaceCombat
{
    [TestFixture]
    public sealed class TacticalCombatOutcomeTests
    {
        [Test]
        public void Capture_CompletedEncounter_RetainsDetachedIdentitiesAndFinalPools()
        {
            SpaceCombatResult result = CreateResult();
            TacticalCombatOutcome outcome = TacticalCombatOutcome.Capture(result, CreateFlags());

            result.ShipDamage[0].Ship.InstanceID = "changed-capital";
            result.ShipDamage[0].HullAfter = 0;
            result.FighterLosses[0].Fighter.InstanceID = "changed-fighter";
            result.FighterLosses[0].SquadsAfter = 0;
            result.ShipDamage.Clear();
            result.FighterLosses.Clear();
            result.FighterRecovery.Clear();
            result.WithdrawnUnits.Clear();
            result.Winner = CombatSide.Draw;

            Assert.AreEqual(CombatSide.Attacker, outcome.Winner);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, outcome.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, outcome.DefenderOutcome);
            Assert.AreEqual(19, outcome.IterationsCompleted);
            Assert.AreEqual("capital", outcome.Ships.Single().UnitID);
            Assert.AreEqual(57, outcome.Ships.Single().Hull);
            Assert.AreEqual("fighter", outcome.Fighters.Single().UnitID);
            Assert.AreEqual(7, outcome.Fighters.Single().SquadronSize);
            Assert.AreEqual("capital", outcome.Fighters.Single().CarrierID);
            Assert.IsTrue(outcome.Ships.Single().Withdrawn);
            Assert.IsTrue(outcome.Fighters.Single().Withdrawn);
        }

        [Test]
        public void Capture_ComponentDamage_CopiesEveryPoolAndRepairTimerIndependently()
        {
            SpaceCombatResult result = CreateResult();
            CapitalShipSystemDamage source = result.ShipDamage[0].SystemDamage;

            TacticalCombatOutcome outcome = TacticalCombatOutcome.Capture(result, CreateFlags());

            CapitalShipSystemDamage captured = outcome.Ships.Single().SystemDamage;
            Assert.AreNotSame(source, captured);
            Assert.AreEqual(1, captured.Shields);
            Assert.AreEqual(2, captured.Weapons);
            Assert.AreEqual(3, captured.TractorBeam);
            Assert.AreEqual(4, captured.Engines);
            Assert.AreEqual(5, captured.PrimaryHyperdrive);
            Assert.AreEqual(6, captured.BackupHyperdrive);
            Assert.AreEqual(123, captured.RepairDueTick);
            Assert.AreEqual(24, captured.RepairInterval);
            source.Engines = 99;
            captured.Weapons = 77;
            Assert.AreEqual(4, captured.Engines);
            Assert.AreEqual(2, source.Weapons);
        }

        [Test]
        public void Capture_TerminalFlags_CopiesAllFlagsWithoutSharingMutations()
        {
            bool[] flags = CreateFlags();

            TacticalCombatOutcome outcome = TacticalCombatOutcome.Capture(CreateResult(), flags);

            CollectionAssert.AreEqual(flags, outcome.TerminalFlags);
            Assert.AreNotSame(flags, outcome.TerminalFlags);
            flags[0] = false;
            outcome.TerminalFlags[1] = true;
            Assert.IsTrue(outcome.TerminalFlags[0]);
            Assert.IsFalse(flags[1]);
        }

        [TestCase("capital", true)]
        [TestCase("CAPITAL", false)]
        public void Capture_WithdrawalIdentity_UsesOrdinalKeysInsteadOfObjectReferences(
            string withdrawnID,
            bool expected
        )
        {
            SpaceCombatResult result = CreateResult();
            result.WithdrawnUnits.Clear();
            result.WithdrawnUnits.Add(new CapitalShip { InstanceID = withdrawnID });

            TacticalCombatOutcome outcome = TacticalCombatOutcome.Capture(result, CreateFlags());

            Assert.AreEqual(expected, outcome.Ships.Single().Withdrawn);
            Assert.IsFalse(outcome.Fighters.Single().Withdrawn);
        }

        [TestCase(0)]
        [TestCase(7)]
        public void Capture_ExplicitPlanetRecovery_PreservesNullCarrierAndExactSquadronSize(
            int size
        )
        {
            SpaceCombatResult result = CreateResult();
            result.FighterRecovery[0].Carrier = null;
            result.FighterLosses[0].SquadsAfter = size;

            TacticalCombatOutcome outcome = TacticalCombatOutcome.Capture(result, CreateFlags());

            Assert.IsNull(outcome.Fighters.Single().CarrierID);
            Assert.AreEqual(size, outcome.Fighters.Single().SquadronSize);
            Assert.IsTrue(outcome.Fighters.Single().Withdrawn);
        }

        [Test]
        public void Capture_ReorderedRecoveryRecords_ResolvesCarrierByFighterIdentity()
        {
            SpaceCombatResult result = CreateResult();
            result.FighterLosses.Add(
                new FighterLossResult
                {
                    Fighter = new Starfighter { InstanceID = "second-fighter" },
                    SquadsBefore = 12,
                    SquadsAfter = 3,
                }
            );
            result.FighterRecovery[0].Fighter = new Starfighter { InstanceID = "fighter" };
            result.FighterRecovery.Insert(
                0,
                new FighterRecoveryResult
                {
                    Fighter = new Starfighter { InstanceID = "second-fighter" },
                    Carrier = null,
                }
            );

            TacticalCombatOutcome outcome = TacticalCombatOutcome.Capture(result, CreateFlags());

            Assert.AreEqual("fighter", outcome.Fighters[0].UnitID);
            Assert.AreEqual("capital", outcome.Fighters[0].CarrierID);
            Assert.IsTrue(outcome.Fighters[0].Withdrawn);
            Assert.AreEqual("second-fighter", outcome.Fighters[1].UnitID);
            Assert.IsNull(outcome.Fighters[1].CarrierID);
            Assert.AreEqual(3, outcome.Fighters[1].SquadronSize);
            Assert.IsFalse(outcome.Fighters[1].Withdrawn);
        }

        [Test]
        public void Capture_SerializedOutcome_ContainsNoCampaignGraphAndRoundTripsDetachedState()
        {
            TacticalCombatOutcome outcome = TacticalCombatOutcome.Capture(
                CreateResult(),
                CreateFlags()
            );
            GameSerializer serializer = new(typeof(TacticalCombatOutcome));
            using StringWriter writer = new();

            serializer.Serialize(writer, outcome);

            string payload = writer.ToString();
            XmlDocument document = new();
            document.LoadXml(payload);
            Assert.AreEqual(
                0,
                document
                    .SelectNodes(
                        "//Ship|//Fighter|//Carrier|//Planet|//AttackerFleet|//DefenderFleet"
                    )
                    .Count
            );
            Assert.That(payload, Does.Not.Contain("Live capital details"));
            Assert.That(payload, Does.Not.Contain("Live fighter details"));
            Assert.That(payload, Does.Not.Contain("OwnerInstanceID"));
            Assert.That(payload, Does.Not.Contain("ParentInstanceID"));
            using StringReader reader = new(payload);
            TacticalCombatOutcome restored = (TacticalCombatOutcome)serializer.Deserialize(reader);
            Assert.AreEqual("capital", restored.Ships.Single().UnitID);
            Assert.AreEqual("fighter", restored.Fighters.Single().UnitID);
            Assert.AreEqual("capital", restored.Fighters.Single().CarrierID);
            Assert.AreEqual(57, restored.Ships.Single().Hull);
            Assert.AreEqual(7, restored.Fighters.Single().SquadronSize);
            Assert.AreEqual(4, restored.Ships.Single().SystemDamage.Engines);
            Assert.AreEqual(123, restored.Ships.Single().SystemDamage.RepairDueTick);
            CollectionAssert.AreEqual(outcome.TerminalFlags, restored.TerminalFlags);
        }

        [Test]
        public void Capture_MissingResult_RejectsMissingEncounter()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                TacticalCombatOutcome.Capture(null, CreateFlags())
            );
            Assert.AreEqual("result", exception.ParamName);
        }

        [TestCase(-1)]
        [TestCase(6)]
        [TestCase(8)]
        public void Capture_IncompleteTerminalFlags_RejectsMissingOrPartialState(int count)
        {
            bool[] flags = count < 0 ? null : new bool[count];

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                TacticalCombatOutcome.Capture(CreateResult(), flags)
            );

            Assert.AreEqual("terminalFlags", exception.ParamName);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Capture_MissingShipOrComponents_RejectsInsteadOfCreatingReplacementState(
            bool missingShip
        )
        {
            SpaceCombatResult result = CreateResult();
            if (missingShip)
                result.ShipDamage[0].Ship = null;
            else
                result.ShipDamage[0].SystemDamage = null;

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                TacticalCombatOutcome.Capture(result, CreateFlags())
            );

            Assert.AreEqual("result", exception.ParamName);
        }

        [Test]
        public void Capture_MissingFighter_RejectsInsteadOfUsingRecoveryIdentity()
        {
            SpaceCombatResult result = CreateResult();
            result.FighterLosses[0].Fighter = null;

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                TacticalCombatOutcome.Capture(result, CreateFlags())
            );

            Assert.AreEqual("result", exception.ParamName);
        }

        [TestCase(0)]
        [TestCase(7)]
        public void Capture_MissingRecovery_RejectsSurvivingAndDestroyedSquadrons(int size)
        {
            SpaceCombatResult result = CreateResult();
            result.FighterLosses[0].SquadsAfter = size;
            result.FighterRecovery.Clear();

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                TacticalCombatOutcome.Capture(result, CreateFlags())
            );

            Assert.AreEqual("result", exception.ParamName);
        }

        /// <summary>
        /// Creates a complete ending state with distinguishable enabled and disabled flags.
        /// </summary>
        /// <returns>The seven independent ending flags.</returns>
        private static bool[] CreateFlags() =>
            new[] { true, false, true, false, true, false, false };

        /// <summary>
        /// Captures damaged ships, recovered fighters, and withdrawals using mutable campaign objects.
        /// </summary>
        /// <returns>The final encounter data used to verify detached outcomes.</returns>
        private static SpaceCombatResult CreateResult()
        {
            CapitalShip ship = new()
            {
                InstanceID = "capital",
                DisplayName = "Live capital details",
                OwnerInstanceID = "campaign-owner",
                ParentInstanceID = "campaign-fleet",
                MaxHullStrength = 900,
            };
            Starfighter fighter = new()
            {
                InstanceID = "fighter",
                DisplayName = "Live fighter details",
                OwnerInstanceID = "campaign-owner",
                ParentInstanceID = "campaign-carrier",
                MaxSquadronSize = 12,
            };
            return new SpaceCombatResult
            {
                Planet = new Planet { InstanceID = "campaign-planet" },
                AttackerFleet = new Fleet { InstanceID = "campaign-fleet" },
                Winner = CombatSide.Attacker,
                AttackerOutcome = SpaceCombatSideOutcome.Withdrawn,
                DefenderOutcome = SpaceCombatSideOutcome.Destroyed,
                IterationsCompleted = 19,
                ShipDamage = new()
                {
                    new()
                    {
                        Ship = ship,
                        HullBefore = 900,
                        HullAfter = 57,
                        SystemDamage = new()
                        {
                            Shields = 1,
                            Weapons = 2,
                            TractorBeam = 3,
                            Engines = 4,
                            PrimaryHyperdrive = 5,
                            BackupHyperdrive = 6,
                            RepairDueTick = 123,
                            RepairInterval = 24,
                        },
                    },
                },
                FighterLosses = new()
                {
                    new()
                    {
                        Fighter = fighter,
                        SquadsBefore = 12,
                        SquadsAfter = 7,
                    },
                },
                FighterRecovery = new()
                {
                    new() { Fighter = fighter, Carrier = ship },
                },
                WithdrawnUnits = new() { ship, fighter },
            };
        }
    }
}
