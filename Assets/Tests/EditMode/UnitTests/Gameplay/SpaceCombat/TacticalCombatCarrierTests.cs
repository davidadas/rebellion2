using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatCarrierTests
    {
        [Test]
        public void Launch_DockedFighter_ReleasesBayReservation()
        {
            TacticalCombatCarrier carrier = CreateCarrier(1);
            TacticalCombatUnit fighter = CreateFighter();
            carrier.AddDocked(fighter);
            Assert.IsTrue(carrier.Launch(fighter));
            Assert.AreEqual(0, carrier.Reserved);
            Assert.AreEqual(0, carrier.Occupied);
            Assert.AreEqual(TacticalCombatUnitStatus.Active, fighter.Status);
        }

        [Test]
        public void Launch_DockedFighter_PlacesSquadronAtHangar()
        {
            TacticalCombatCarrier carrier = CreateCarrier(1);
            TacticalCombatUnit fighter = CreateFighter();
            carrier.AddDocked(fighter);
            carrier.Launch(fighter);
            Assert.AreEqual(carrier.HangarPosition, fighter.Position);
        }

        [Test]
        public void Reserve_FullCarrier_RejectsAdditionalFighter()
        {
            TacticalCombatCarrier carrier = CreateCarrier(1);
            carrier.AddDocked(CreateFighter());
            Assert.IsFalse(carrier.Reserve(CreateFighter("second")));
        }

        [Test]
        public void Recover_ReservedFighter_OccupiesReservedBay()
        {
            TacticalCombatCarrier carrier = CreateCarrier(1);
            TacticalCombatUnit fighter = CreateFighter();
            Assert.IsTrue(carrier.Reserve(fighter));
            Assert.IsTrue(carrier.Recover(fighter));
            Assert.AreEqual(1, carrier.Reserved);
            Assert.AreEqual(1, carrier.Occupied);
            Assert.AreEqual(TacticalCombatUnitStatus.Recovered, fighter.Status);
        }

        [Test]
        public void Detach_ApproachingFighter_ReleasesReservation()
        {
            TacticalCombatCarrier carrier = CreateCarrier(1);
            TacticalCombatUnit fighter = CreateFighter();
            carrier.Reserve(fighter);
            carrier.Detach(fighter);
            Assert.AreEqual(0, carrier.Reserved);
        }

        [Test]
        public void Launch_DestroyedCarrier_LeavesDockedFighterInactive()
        {
            TacticalCombatCarrier carrier = CreateCarrier(1);
            TacticalCombatUnit fighter = CreateFighter();
            carrier.AddDocked(fighter);
            carrier.Unit.Status = TacticalCombatUnitStatus.Destroyed;
            Assert.IsFalse(carrier.Launch(fighter));
        }

        /// <summary>
        /// Creates a carrier with a specified number of fighter bays.
        /// </summary>
        /// <param name="capacity">The available bays.</param>
        /// <returns>The captured carrier.</returns>
        private static TacticalCombatCarrier CreateCarrier(int capacity)
        {
            CapitalShip ship = new()
            {
                InstanceID = "carrier",
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                StarfighterCapacity = capacity,
            };
            return new TacticalCombatCarrier(new TacticalCombatUnit(ship, CombatSide.Attacker, 1));
        }

        /// <summary>
        /// Creates a complete fighter squadron.
        /// </summary>
        /// <param name="id">The squadron identifier.</param>
        /// <returns>The captured squadron.</returns>
        private static TacticalCombatUnit CreateFighter(string id = "fighter") =>
            new(
                new Starfighter
                {
                    InstanceID = id,
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                },
                CombatSide.Attacker,
                2
            );
    }
}
