using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatBattleStationTests
    {
        [Test]
        public void TryFire_NewOperationalStation_ConsumesInitialFullCharge()
        {
            TacticalCombatBattleStation station = CreateStation();
            Assert.IsTrue(station.TryFire());
            Assert.AreEqual(0, station.Charge);
        }

        [Test]
        public void TryFire_ConsumedCharge_RejectsSecondShot()
        {
            TacticalCombatBattleStation station = CreateStation();
            station.TryFire();
            Assert.IsFalse(station.TryFire());
        }

        [Test]
        public void Advance_FiveMinutes_RechargesSuperlaser()
        {
            TacticalCombatBattleStation station = CreateStation();
            station.TryFire();
            station.Advance(300000);
            Assert.IsTrue(station.TryFire());
        }

        [Test]
        public void CanReceiveAttackRun_PlanetaryProtection_RejectsAttack()
        {
            Assert.IsFalse(CreateStation(protectedByPlanet: true).CanReceiveAttackRun);
        }

        [Test]
        public void TryFire_PlanetaryProtection_StillAllowsSuperlaser()
        {
            Assert.IsTrue(CreateStation(protectedByPlanet: true).TryFire());
        }

        [Test]
        public void TryFire_ConstructionIncomplete_RejectsShot()
        {
            Assert.IsFalse(CreateStation(underConstruction: true).TryFire());
        }

        [Test]
        public void Advance_ChargeCrossing_NotifiesBeforeStoringCompletedCharge()
        {
            TacticalCombatBattleStation station = CreateStation();
            station.TryFire();
            float observedCharge = -1;
            bool couldFireInCallback = true;
            station.ChargeCompleted += () =>
            {
                observedCharge = station.Charge;
                couldFireInCallback = station.TryFire();
            };
            station.Advance(300000);
            Assert.AreEqual(0, observedCharge);
            Assert.IsFalse(couldFireInCallback);
            Assert.AreEqual(100, station.Charge);
        }

        [Test]
        public void Advance_AlreadyCharged_DoesNotRepeatCompletionNotification()
        {
            TacticalCombatBattleStation station = CreateStation();
            station.TryFire();
            int notifications = 0;
            station.ChargeCompleted += () => notifications++;
            station.Advance(300000);
            station.Advance(300000);
            Assert.AreEqual(1, notifications);
        }

        /// <summary>
        /// Creates a station with known operational and protection state.
        /// </summary>
        /// <param name="protectedByPlanet">Whether planetary facilities protect it.</param>
        /// <param name="underConstruction">Whether manufacturing is unfinished.</param>
        /// <returns>The captured station.</returns>
        private static TacticalCombatBattleStation CreateStation(
            bool protectedByPlanet = false,
            bool underConstruction = false
        )
        {
            CapitalShip ship = new()
            {
                InstanceID = "station",
                CanDestroyPlanets = true,
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
                ManufacturingStatus = underConstruction
                    ? ManufacturingStatus.Building
                    : ManufacturingStatus.Complete,
            };
            return new TacticalCombatBattleStation(
                new TacticalCombatUnit(ship, CombatSide.Defender, 1000001),
                protectedByPlanet
            );
        }
    }
}
