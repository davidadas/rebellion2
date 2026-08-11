using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public class TacticalTractorBeamSystemTests
    {
        [Test]
        public void UpdateLock_OpposingTargetWithinRange_ReducesMovementByTractorStrength()
        {
            TacticalTractorBeamSystem system = new TacticalTractorBeamSystem();
            TacticalUnitState source = CreateUnit(TacticalBattleSide.Attacker, 6, 20);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Defender, 0, 0);
            source.Position = Vector3.Zero;
            target.Position = new Vector3(0f, 0f, 10f);

            system.UpdateLock(source, target);

            Assert.AreEqual(target.EffectiveSublightSpeed - 6f, system.GetMovementSpeed(target));
            Assert.AreEqual(
                TacticalCombatEventKind.TractorLock,
                system.DrainEvents().Single().Kind
            );
        }

        [Test]
        public void UpdateLock_TargetLeavesRange_ReleasesMovementPenalty()
        {
            TacticalTractorBeamSystem system = new TacticalTractorBeamSystem();
            TacticalUnitState source = CreateUnit(TacticalBattleSide.Attacker, 6, 20);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Defender, 0, 0);
            system.UpdateLock(source, target);
            system.DrainEvents();
            target.Position = new Vector3(0f, 0f, 21f);

            system.UpdateLock(source, target);

            Assert.AreEqual(target.EffectiveSublightSpeed, system.GetMovementSpeed(target));
            Assert.AreEqual(
                TacticalCombatEventKind.TractorRelease,
                system.DrainEvents().Single().Kind
            );
        }

        [Test]
        public void GetMovementSpeed_CommandBudgetAndTractorLock_AppliesTractorToCombinedBudget()
        {
            TacticalTractorBeamSystem system = new TacticalTractorBeamSystem();
            TacticalUnitState source = CreateUnit(TacticalBattleSide.Attacker, 6, 20);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Defender, 0, 0);
            source.Position = Vector3.Zero;
            target.Position = new Vector3(0f, 0f, 10f);
            system.UpdateLock(source, target);

            float movement = system.GetMovementSpeed(target, 4f);

            Assert.AreEqual(target.EffectiveSublightSpeed + 4f - 6f, movement);
        }

        [Test]
        public void UpdateLock_FourExistingSources_RejectsFifthSource()
        {
            TacticalTractorBeamSystem system = new TacticalTractorBeamSystem();
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Defender, 0, 0);
            TacticalUnitState[] sources = Enumerable
                .Range(0, 5)
                .Select(_ => CreateUnit(TacticalBattleSide.Attacker, 1, 20))
                .ToArray();

            foreach (TacticalUnitState source in sources)
                system.UpdateLock(source, target);

            Assert.AreEqual(target.EffectiveSublightSpeed - 4f, system.GetMovementSpeed(target));
            Assert.AreEqual(4, system.DrainEvents().Count);
        }

        [Test]
        public void UpdateLock_SourceChangesTarget_ReleasesOldTargetBeforeLockingNewTarget()
        {
            TacticalTractorBeamSystem system = new TacticalTractorBeamSystem();
            TacticalUnitState source = CreateUnit(TacticalBattleSide.Attacker, 6, 20);
            TacticalUnitState firstTarget = CreateUnit(TacticalBattleSide.Defender, 0, 0);
            TacticalUnitState secondTarget = CreateUnit(TacticalBattleSide.Defender, 0, 0);
            system.UpdateLock(source, firstTarget);
            system.DrainEvents();

            system.UpdateLock(source, secondTarget);

            Assert.AreEqual(
                firstTarget.EffectiveSublightSpeed,
                system.GetMovementSpeed(firstTarget)
            );
            Assert.AreEqual(
                secondTarget.EffectiveSublightSpeed - 6f,
                system.GetMovementSpeed(secondTarget)
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    TacticalCombatEventKind.TractorRelease,
                    TacticalCombatEventKind.TractorLock,
                },
                system.DrainEvents().Select(combatEvent => combatEvent.Kind)
            );
        }

        private static TacticalUnitState CreateUnit(
            TacticalBattleSide side,
            int tractorPower,
            int tractorRange
        )
        {
            CapitalShip ship = new CapitalShip
            {
                CurrentHullStrength = 100,
                MaxHullStrength = 100,
                SublightSpeed = 10,
                Hyperdrive = 100,
                TractorBeamPower = tractorPower,
                TractorBeamnRange = tractorRange,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            return TacticalUnitState.FromCapitalShip(ship, side);
        }
    }
}
