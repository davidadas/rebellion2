using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Combat
{
    [TestFixture]
    public sealed class CombatUnitTests
    {
        [Test]
        public void Create_CapitalShip_CreatesIndependentBattleCopy()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "CAPITAL_SHIP_1",
                OwnerInstanceID = "FACTION_1",
                CurrentHullStrength = 100,
            };
            source.GetComponents().Add(new Engine { Health = 50 });

            CombatUnit combatUnit = CombatUnit.Create(source);
            CapitalShip copy = combatUnit.GetUnit() as CapitalShip;

            Assert.IsNotNull(copy);
            Assert.AreEqual(source.InstanceID, combatUnit.SourceUnitId);
            Assert.AreNotSame(source, copy);
            Assert.AreEqual(source.InstanceID, copy.InstanceID);
            Assert.AreNotSame(source.GetComponents(), copy.GetComponents());
            Assert.AreNotSame(source.GetComponents()[0], copy.GetComponents()[0]);

            copy.CurrentHullStrength = 25;
            copy.GetComponents()[0].Health = 10;

            Assert.AreEqual(100, source.CurrentHullStrength);
            Assert.AreEqual(50, source.GetComponents()[0].Health);
        }

        [Test]
        public void Create_Starfighter_CreatesIndependentBattleCopy()
        {
            Starfighter source = new Starfighter
            {
                InstanceID = "STARFIGHTER_SQUADRON_1",
                OwnerInstanceID = "FACTION_1",
                CurrentSquadronSize = 12,
            };

            CombatUnit combatUnit = CombatUnit.Create(source);
            Starfighter copy = combatUnit.GetUnit() as Starfighter;

            Assert.IsNotNull(copy);
            Assert.AreEqual(source.InstanceID, combatUnit.SourceUnitId);
            Assert.AreNotSame(source, copy);
            Assert.AreEqual(1, copy.MaxSquadronSize);
            Assert.AreEqual(1, copy.CurrentSquadronSize);

            copy.CurrentSquadronSize = 0;

            Assert.AreEqual(12, source.CurrentSquadronSize);
        }
    }
}
