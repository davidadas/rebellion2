using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Tactical;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.SpaceCombat
{
    [TestFixture]
    public sealed class TacticalCombatOutcomeTests
    {
        [Test]
        public void Serialize_DetachedOutcome_ContainsNoCampaignGraphAndRoundTripsDetachedState()
        {
            TacticalCombatOutcome outcome = CreateOutcome();
            GameSerializer serializer = new(typeof(TacticalCombatOutcome));
            using StringWriter writer = new();

            serializer.Serialize(writer, outcome);

            string payload = writer.ToString();
            XmlDocument document = new();
            document.LoadXml(payload);
            Assert.That(
                document.SelectSingleNode("/TacticalCombatOutcome/Ships/TacticalCombatShipOutcome"),
                Is.Not.Null
            );
            Assert.That(
                document.SelectSingleNode(
                    "/TacticalCombatOutcome/Fighters/TacticalCombatFighterOutcome"
                ),
                Is.Not.Null
            );
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
        }

        /// <summary>
        /// Creates a detached outcome with every persisted component and terminal field populated.
        /// </summary>
        /// <returns>The serialization fixture.</returns>
        private static TacticalCombatOutcome CreateOutcome() =>
            new()
            {
                Winner = CombatSide.Attacker,
                AttackerOutcome = SpaceCombatSideOutcome.Withdrawn,
                DefenderOutcome = SpaceCombatSideOutcome.Destroyed,
                IterationsCompleted = 19,
                Ships = new[]
                {
                    new TacticalCombatOutcome.ShipOutcome
                    {
                        UnitID = "capital",
                        Hull = 57,
                        Withdrawn = true,
                        SystemDamage = new CapitalShipSystemDamage
                        {
                            Shields = 1,
                            Weapons = 2,
                            TractorBeam = 3,
                            Engines = 4,
                            PrimaryHyperdrive = 5,
                            BackupHyperdrive = 6,
                        },
                    },
                },
                Fighters = new[]
                {
                    new TacticalCombatOutcome.FighterOutcome
                    {
                        UnitID = "fighter",
                        SquadronSize = 7,
                        CarrierID = "capital",
                        Withdrawn = true,
                    },
                },
            };
    }
}
