using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
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
            TacticalCombatUnit host = CreateHost();
            TacticalCombatSide side = CreateSide(host, 50, 60);
            host.Systems.Destroy();

            Assert.AreEqual(expected, side.GetCommandBudget(fighter));
        }

        [TestCase(TacticalCombatUnitStatus.PreparingHyperspace, 4f)]
        [TestCase(TacticalCombatUnitStatus.EnteringHyperspace, 1f)]
        [TestCase(TacticalCombatUnitStatus.AwaitingWithdrawal, 1f)]
        [TestCase(TacticalCombatUnitStatus.Withdrawn, 1f)]
        [TestCase(TacticalCombatUnitStatus.Destroyed, 1f)]
        public void GetCommandBudget_HostParticipation_UsesOrdinaryActiveDisplayDomain(
            TacticalCombatUnitStatus status,
            float expected
        )
        {
            TacticalCombatUnit host = CreateHost();
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

        /// <summary>
        /// Creates an active mobile capital host with completed campaign construction.
        /// </summary>
        /// <returns>The captured host used by both command-officer lanes.</returns>
        private static TacticalCombatUnit CreateHost() =>
            new(
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
        private static TacticalCombatSide CreateSide(
            TacticalCombatUnit host,
            float leadership,
            float combat
        ) =>
            new(
                CombatSide.Attacker,
                0,
                Vector3.UnitZ,
                true,
                0,
                0,
                host,
                host,
                leadership,
                combat,
                false
            );
    }
}
