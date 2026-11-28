using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatSideTests
    {
        [TestCase(false, 4f)]
        [TestCase(true, 4f)]
        public void GetCommandBudget_ZeroHullActiveHost_RetainsOfficerSupport(
            bool fighter,
            float expected
        )
        {
            CombatUnit host = CreateHost();
            TacticalCombatSide side = CreateSide(host, 50, 60);
            host.Destroy();

            Assert.AreEqual(expected, side.GetCommandBudget(fighter));
        }

        [TestCase(CombatUnitStatus.PreparingHyperspace, 4f)]
        [TestCase(CombatUnitStatus.EnteringHyperspace, 1f)]
        [TestCase(CombatUnitStatus.AwaitingWithdrawal, 1f)]
        [TestCase(CombatUnitStatus.Withdrawn, 1f)]
        [TestCase(CombatUnitStatus.Destroyed, 1f)]
        public void GetCommandBudget_HostParticipation_UsesOrdinaryActiveDisplayDomain(
            CombatUnitStatus status,
            float expected
        )
        {
            CombatUnit host = CreateHost();
            TacticalCombatSide side = CreateSide(host, 50, 60);
            host.Status = status;

            Assert.AreEqual(expected, side.GetCommandBudget(false));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetCommandBudget_UnorderedOfficerRating_UsesLowerClamp(bool fighter)
        {
            TacticalCombatSide side = CreateSide(CreateHost(), float.NaN, float.NaN);

            Assert.AreEqual(1, side.GetCommandBudget(fighter));
        }

        [TestCase(0f, 0, false, 30000f)]
        [TestCase(0f, 0, true, 30000f)]
        [TestCase(0f, 1, false, 30000f)]
        [TestCase(0f, 1, true, 150000f)]
        [TestCase(30000f, 0, false, 30000f)]
        [TestCase(150000f, 1, true, 150000f)]
        [TestCase(45000f, 0, false, 45000f)]
        [TestCase(200000f, 1, true, 200000f)]
        [TestCase(float.NaN, 0, false, 30000f)]
        [TestCase(float.NaN, 1, true, 150000f)]
        [TestCase(float.NegativeInfinity, 0, false, 30000f)]
        [TestCase(float.PositiveInfinity, 1, true, float.PositiveInfinity)]
        public void CalculateInitialDelay_RouteAndStationFacts_SelectsApplicableMinimum(
            float route,
            int faction,
            bool station,
            float expected
        )
        {
            TacticalCombatSide side = CreateSide(CreateHost(), 0, 0, faction, route);
            side.BeginWithdrawal();
            side.SelectWithdrawalDelay(station);
            Assert.AreEqual(expected, side.WithdrawalMilliseconds);
        }

        [Test]
        public void AdvanceCountdown_PositiveDelay_RemainsWaiting()
        {
            TacticalCombatSide side = CreateSide(CreateHost(), 0, 0, route: 1001);
            side.BeginWithdrawal();
            Assert.IsFalse(side.AdvanceWithdrawal(1000));
            Assert.AreEqual(CombatWithdrawalPhase.Waiting, side.WithdrawalPhase);
            Assert.AreEqual(1, side.WithdrawalMilliseconds);
        }

        [TestCase(1000f)]
        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void AdvanceCountdown_DelayExpiresOrIsUnordered_EntersDeparture(float delay)
        {
            TacticalCombatSide side = CreateSide(CreateHost(), 0, 0, route: delay);
            side.BeginWithdrawal();
            Assert.IsTrue(side.AdvanceWithdrawal(1000));
            Assert.IsFalse(side.AdvanceWithdrawal(1000));
            Assert.AreEqual(CombatWithdrawalPhase.Departing, side.WithdrawalPhase);
        }

        /// <summary>
        /// Creates an active mobile capital host with completed campaign construction.
        /// </summary>
        /// <returns>The captured host used by both command-officer lanes.</returns>
        private static CombatUnit CreateHost() =>
            TacticalCombatDeployment.CreateUnit(
                new CapitalShip
                {
                    InstanceID = "host",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                    Hyperdrive = 1,
                },
                CombatSide.Attacker,
                0
            );

        /// <summary>
        /// Captures both selected officers on one active host.
        /// </summary>
        /// <param name="host">The registered officer host.</param>
        /// <param name="leadership">The admiral's captured effective leadership.</param>
        /// <param name="combat">The commander's captured effective combat rating.</param>
        /// <returns>The supported side state.</returns>
        /// <param name="faction">The authored faction index.</param>
        /// <param name="route">The withdrawal route duration in milliseconds.</param>
        private static TacticalCombatSide CreateSide(
            CombatUnit host,
            float leadership,
            float combat,
            int faction = 0,
            float route = 0
        ) =>
            new(
                CombatSide.Attacker,
                faction,
                Vector3.UnitZ,
                true,
                route,
                0,
                host,
                host,
                leadership,
                combat,
                false
            );
    }
}
