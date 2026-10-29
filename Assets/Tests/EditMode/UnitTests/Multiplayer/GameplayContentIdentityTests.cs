using System.IO;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class GameplayContentIdentityTests
    {
        [Test]
        public void Compute_EqualDefinitions_ReturnsSameIdentity()
        {
            string first = GameplayContentIdentity.Compute(
                CreateData(new CapitalShip { TypeID = "ship" })
            );
            string second = GameplayContentIdentity.Compute(
                CreateData(new CapitalShip { TypeID = "ship" })
            );

            Assert.AreEqual(first, second);
            Assert.AreEqual(64, first.Length);
        }

        [Test]
        public void Compute_CombatStatisticChanged_RejectsContentWithSameSelection()
        {
            string first = GameplayContentIdentity.Compute(
                CreateData(new CapitalShip { TypeID = "ship", DamageControl = 5 })
            );
            string second = GameplayContentIdentity.Compute(
                CreateData(new CapitalShip { TypeID = "ship", DamageControl = 6 })
            );

            Assert.AreNotEqual(first, second);
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerCompatibility.ValidateContent(first, second)
            );
        }

        [Test]
        public void Compute_UnassignedPrototypeIdentity_DoesNotGenerateIdentifier()
        {
            CapitalShip prototype = new() { TypeID = "ship" };

            GameplayContentIdentity.Compute(CreateData(prototype));

            Assert.IsNull(prototype.GetRecoveryInstanceID());
        }

        /// <summary>
        /// Composes the same installed rules with an independently supplied ship definition.
        /// </summary>
        /// <param name="ship">The definition whose actual data is being compared.</param>
        /// <returns>The complete content catalog used by the identity contract.</returns>
        private static GameDataCatalog CreateData(CapitalShip ship)
        {
            GameDataCatalog data = TestContent.Data;
            return new GameDataCatalog(
                data.GameConfig,
                data.GenerationConfig,
                data.Factions,
                data.PlanetSectors,
                data.Buildings,
                new[] { ship },
                data.Starfighters,
                data.Regiments,
                data.SpecialForces,
                data.Officers,
                data.GameEvents,
                data.MessageDefinitions,
                data.EncyclopediaEntries,
                data.FactionThemes,
                data.OfficerTraits
            );
        }
    }
}
