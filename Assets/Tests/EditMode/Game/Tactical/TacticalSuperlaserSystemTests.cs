using System;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public sealed class TacticalSuperlaserSystemTests
    {
        [Test]
        public void Constructor_ParticipatingDeathStar_BeginsFullyCharged()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);

            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(new[] { deathStar });

            Assert.AreEqual(TacticalSuperlaserSystem.MaximumCharge, system.GetCharge(deathStar));
        }

        [Test]
        public void TryFire_ChargedDeathStar_SchedulesOpposingTargetAndResetsCharge()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, target }
            );

            bool fired = system.TryFire(deathStar, target);

            Assert.IsTrue(fired);
            Assert.AreEqual(target.InitialHull, target.Hull);
            Assert.AreEqual(0f, system.GetCharge(deathStar));
            Assert.IsEmpty(system.DrainResolvedTargets());
        }

        [Test]
        public void TryFire_UnchargedDeathStar_PreservesOpposingTarget()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState firstTarget = CreateShip(TacticalBattleSide.Defender);
            TacticalUnitState secondTarget = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, firstTarget, secondTarget }
            );
            system.TryFire(deathStar, firstTarget);

            bool fired = system.TryFire(deathStar, secondTarget);

            Assert.IsFalse(fired);
            Assert.AreEqual(secondTarget.InitialHull, secondTarget.Hull);
        }

        [Test]
        public void TryFire_FriendlyTarget_PreservesTargetAndCharge()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Attacker);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, target }
            );

            bool fired = system.TryFire(deathStar, target);

            Assert.IsFalse(fired);
            Assert.AreEqual(target.InitialHull, target.Hull);
            Assert.AreEqual(TacticalSuperlaserSystem.MaximumCharge, system.GetCharge(deathStar));
        }

        [Test]
        public void TryFire_NonparticipatingTarget_PreservesTargetAndCharge()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(new[] { deathStar });

            bool fired = system.TryFire(deathStar, target);

            Assert.IsFalse(fired);
            Assert.AreEqual(target.InitialHull, target.Hull);
            Assert.AreEqual(TacticalSuperlaserSystem.MaximumCharge, system.GetCharge(deathStar));
        }

        [Test]
        public void TryFire_WithdrawingDeathStar_PreservesOpposingTargetAndCharge()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, target }
            );
            deathStar.BeginWithdrawal();

            bool fired = system.TryFire(deathStar, target);

            Assert.IsFalse(fired);
            Assert.AreEqual(target.InitialHull, target.Hull);
            Assert.AreEqual(TacticalSuperlaserSystem.MaximumCharge, system.GetCharge(deathStar));
        }

        [Test]
        public void Advance_DischargedOperationalDeathStar_RechargesToMaximum()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, target }
            );
            system.TryFire(deathStar, target);

            system.Advance(1000f);

            Assert.AreEqual(TacticalSuperlaserSystem.MaximumCharge, system.GetCharge(deathStar));
        }

        [Test]
        public void Advance_DischargedOperationalDeathStar_RechargesAtExpectedRate()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, target }
            );
            system.TryFire(deathStar, target);

            system.Advance(3f);

            Assert.AreEqual(1f, system.GetCharge(deathStar), 0.0001f);
        }

        [Test]
        public void Advance_PendingShotBeforeResolutionDelay_DoesNotResolveTarget()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, target }
            );
            system.TryFire(deathStar, target);

            system.Advance(TacticalSuperlaserSystem.ResolutionDelay - 0.01f);

            Assert.IsEmpty(system.DrainResolvedTargets());
        }

        [Test]
        public void Advance_PendingShotReachesResolutionDelay_ResolvesTarget()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, target }
            );
            system.TryFire(deathStar, target);

            system.Advance(TacticalSuperlaserSystem.ResolutionDelay);

            CollectionAssert.AreEqual(new[] { target }, system.DrainResolvedTargets());
        }

        [Test]
        public void Advance_InactivePendingTargetReachesResolutionDelay_DoesNotResolveTarget()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, target }
            );
            system.TryFire(deathStar, target);
            target.Hull = 0;

            system.Advance(TacticalSuperlaserSystem.ResolutionDelay);

            Assert.IsEmpty(system.DrainResolvedTargets());
        }

        [Test]
        public void Advance_DestroyedFiringDeathStarWithPendingShot_ResolvesActiveTarget()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, target }
            );
            system.TryFire(deathStar, target);
            deathStar.Hull = 0;

            system.Advance(TacticalSuperlaserSystem.ResolutionDelay);

            CollectionAssert.AreEqual(new[] { target }, system.DrainResolvedTargets());
        }

        [Test]
        public void Advance_DestroyedDeathStar_DoesNotRecharge()
        {
            TacticalUnitState deathStar = CreateDeathStar(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateShip(TacticalBattleSide.Defender);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(
                new[] { deathStar, target }
            );
            system.TryFire(deathStar, target);
            deathStar.Hull = 0;

            system.Advance(1000f);

            Assert.AreEqual(0f, system.GetCharge(deathStar));
        }

        [Test]
        public void GetCharge_NonDeathStar_ThrowsArgumentException()
        {
            TacticalUnitState ship = CreateShip(TacticalBattleSide.Attacker);
            TacticalSuperlaserSystem system = new TacticalSuperlaserSystem(new[] { ship });

            Assert.Throws<ArgumentException>(() => system.GetCharge(ship));
        }

        private static TacticalUnitState CreateDeathStar(TacticalBattleSide side)
        {
            CapitalShip deathStar = new CapitalShip
            {
                CurrentHullStrength = 1000,
                MaxHullStrength = 1000,
                IsDeathStar = true,
            };
            return TacticalUnitState.FromCapitalShip(deathStar, side);
        }

        private static TacticalUnitState CreateShip(TacticalBattleSide side)
        {
            CapitalShip ship = new CapitalShip { CurrentHullStrength = 100, MaxHullStrength = 100 };
            return TacticalUnitState.FromCapitalShip(ship, side);
        }
    }
}
