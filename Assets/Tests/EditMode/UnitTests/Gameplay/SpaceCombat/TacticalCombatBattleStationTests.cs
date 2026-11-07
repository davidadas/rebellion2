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
        public void Fire_AdmittedTargetCommand_ConsumesInitialFullCharge()
        {
            TacticalCombatBattleStation station = CreateStation();
            station.Fire();
            Assert.AreEqual(0, station.Charge);
        }

        [Test]
        public void CanBeginTargetSelection_ConsumedCharge_RejectsContext()
        {
            TacticalCombatBattleStation station = CreateStation();
            station.Fire();
            Assert.IsFalse(station.CanBeginTargetSelection);
        }

        [Test]
        public void Advance_FiveMinutes_RechargesSuperlaser()
        {
            TacticalCombatBattleStation station = CreateStation();
            station.Fire();
            station.Advance(300000);
            Assert.IsTrue(station.CanBeginTargetSelection);
        }

        [Test]
        public void CanReceiveAttackRun_PlanetaryProtection_RejectsAttack()
        {
            Assert.IsFalse(CreateStation(protectedByPlanet: true).CanReceiveAttackRun);
        }

        [Test]
        public void CanBeginTargetSelection_PlanetaryProtection_AllowsContext()
        {
            Assert.IsTrue(CreateStation(protectedByPlanet: true).CanBeginTargetSelection);
        }

        [Test]
        public void CanKeepTargetSelection_ConstructionIncomplete_RejectsContext()
        {
            Assert.IsFalse(
                CreateStation(underConstruction: true).CanKeepTargetSelection(false, true)
            );
        }

        [Test]
        public void Advance_ChargeCrossing_NotifiesBeforeStoringCompletedCharge()
        {
            TacticalCombatBattleStation station = CreateStation();
            station.Fire();
            float observedCharge = -1;
            bool couldFireInCallback = true;
            station.ChargeCompleted += () =>
            {
                observedCharge = station.Charge;
                couldFireInCallback = station.CanBeginTargetSelection;
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
            station.Fire();
            int notifications = 0;
            station.ChargeCompleted += () => notifications++;
            station.Advance(300000);
            station.Advance(300000);
            Assert.AreEqual(1, notifications);
        }

        [Test]
        public void CanBeginTargetSelection_UnfinishedStationWithMaximumHull_AllowsInitialContext()
        {
            Assert.IsTrue(CreateStation(underConstruction: true).CanBeginTargetSelection);
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void CanKeepTargetSelection_CommandAuthorityUnavailable_RejectsContext(
            bool withdrawing,
            bool directControl
        )
        {
            Assert.IsFalse(CreateStation().CanKeepTargetSelection(withdrawing, directControl));
        }

        [Test]
        public void TryWithdraw_OperationalStation_PreservesHull()
        {
            TacticalCombatBattleStation station = CreateStation();

            Assert.IsTrue(station.TryWithdraw());

            Assert.IsTrue(station.HasDeparted);
            Assert.IsFalse(station.IsOperational);
            Assert.AreEqual(1000, station.Unit.Systems.Hull);
        }

        [Test]
        public void Advance_DepartedStationWithPositiveHull_ContinuesCharging()
        {
            TacticalCombatBattleStation station = CreateStation();
            station.Fire();
            station.TryWithdraw();

            station.Advance(300000);

            Assert.AreEqual(100, station.Charge);
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
