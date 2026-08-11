using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public sealed class TacticalFighterDeploymentSystemTests
    {
        [Test]
        public void Advance_ScheduledLaunchDelayElapses_DeploysFightersAtCarrier()
        {
            TacticalUnitState carrier = CreateCarrier();
            TacticalUnitState fighters = CreateHeldFighters(carrier);
            carrier.Position = new Vector3(12f, 3f, -4f);
            TacticalFighterDeploymentSystem system = CreateSystem(
                carrier,
                new[] { fighters },
                0.99d
            );

            system.Advance(7.99f);

            Assert.IsFalse(fighters.IsDeployed);

            system.Advance(0.01f);

            Assert.IsTrue(fighters.IsDeployed);
            Assert.AreEqual(carrier.Position, fighters.Position);
        }

        [Test]
        public void Advance_MultipleHeldSquadrons_StaggersLaunches()
        {
            TacticalUnitState carrier = CreateCarrier();
            TacticalUnitState firstFighters = CreateHeldFighters(carrier, "first");
            TacticalUnitState secondFighters = CreateHeldFighters(carrier, "second");
            TacticalFighterDeploymentSystem system = CreateSystem(
                carrier,
                new[] { firstFighters, secondFighters },
                0d,
                0.99d
            );

            system.Advance(0f);

            Assert.IsTrue(firstFighters.IsDeployed);
            Assert.IsFalse(secondFighters.IsDeployed);

            system.Advance(8f);

            Assert.IsTrue(secondFighters.IsDeployed);
        }

        [Test]
        public void DrainEvents_FightersDeploys_ReturnsDeploymentEvent()
        {
            TacticalUnitState carrier = CreateCarrier();
            TacticalUnitState fighters = CreateHeldFighters(carrier);
            TacticalFighterDeploymentSystem system = CreateSystem(carrier, new[] { fighters }, 0d);
            system.Advance(0f);

            TacticalCombatEvent combatEvent = system.DrainEvents().Single();

            Assert.AreEqual(TacticalCombatEventKind.FightersDeployed, combatEvent.Kind);
            Assert.AreSame(fighters, combatEvent.Source);
        }

        [Test]
        public void Advance_CarrierIsDestroyed_DestroysHeldFighters()
        {
            TacticalUnitState carrier = CreateCarrier();
            TacticalUnitState fighters = CreateHeldFighters(carrier);
            TacticalFighterDeploymentSystem system = CreateSystem(
                carrier,
                new[] { fighters },
                0.99d
            );
            carrier.Hull = 0;

            system.Advance(0f);

            Assert.AreEqual(0, fighters.Hull);
            Assert.IsFalse(fighters.IsActive);
        }

        [Test]
        public void Advance_CarrierHasWithdrawn_WithdrawsHeldFightersWithCarrier()
        {
            TacticalUnitState carrier = CreateCarrier();
            TacticalUnitState fighters = CreateHeldFighters(carrier);
            TacticalFighterDeploymentSystem system = CreateSystem(
                carrier,
                new[] { fighters },
                0.99d
            );
            carrier.BeginWithdrawal();
            carrier.CompleteWithdrawal();

            system.Advance(0f);

            Assert.IsTrue(fighters.HasWithdrawn);
            Assert.IsFalse(fighters.IsDeployed);
            Assert.AreEqual(12, fighters.Hull);
        }

        private static TacticalFighterDeploymentSystem CreateSystem(
            TacticalUnitState carrier,
            TacticalUnitState[] fighters,
            params double[] randomValues
        )
        {
            return new TacticalFighterDeploymentSystem(
                new[] { carrier }.Concat(fighters).ToArray(),
                new FixedRandomProvider(randomValues)
            );
        }

        private static TacticalUnitState CreateCarrier()
        {
            return TacticalUnitState.FromCapitalShip(
                new CapitalShip
                {
                    CurrentHullStrength = 600,
                    MaxHullStrength = 600,
                    Hyperdrive = 100,
                    SublightSpeed = 10,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                TacticalBattleSide.Attacker
            );
        }

        private static TacticalUnitState CreateHeldFighters(
            TacticalUnitState carrier,
            string typeId = null
        )
        {
            return TacticalUnitState.FromFighters(
                new Starfighter
                {
                    TypeID = typeId,
                    CurrentSquadronSize = 12,
                    MaxSquadronSize = 12,
                    Hyperdrive = 0,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                TacticalBattleSide.Attacker,
                carrier
            );
        }
    }
}
