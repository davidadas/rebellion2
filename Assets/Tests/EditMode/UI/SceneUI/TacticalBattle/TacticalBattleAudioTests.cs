using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public sealed class TacticalBattleAudioTests
    {
        private const string _attackerArrival = "attacker-arrival";
        private const string _attackerWithdrawal = "attacker-withdrawal";
        private const string _defenderArrival = "defender-arrival";
        private const string _defenderEnergyPenetration = "defender-energy-penetration";
        private const string _defenderEnergyHit = "defender-energy-hit";
        private const string _defenderIonPenetration = "defender-ion-penetration";
        private const string _defenderIonHit = "defender-ion-hit";
        private const string _defenderProjectilePenetration = "defender-projectile-penetration";
        private const string _defenderProjectileHit = "defender-projectile-hit";
        private const string _defenderSuperlaser = "defender-superlaser";
        private readonly List<string> played = new List<string>();
        private TacticalBattleAudio audio;

        [SetUp]
        public void SetUp()
        {
            played.Clear();
            audio = new TacticalBattleAudio(
                new Dictionary<TacticalBattleSide, TacticalBattleTheme>
                {
                    [TacticalBattleSide.Attacker] = new TacticalBattleTheme
                    {
                        ArrivalAudioPath = _attackerArrival,
                        WithdrawalAudioPath = _attackerWithdrawal,
                    },
                    [TacticalBattleSide.Defender] = new TacticalBattleTheme
                    {
                        ArrivalAudioPath = _defenderArrival,
                        EnergyShieldPenetrationAudioPath = _defenderEnergyPenetration,
                        EnergyShieldHitAudioPath = _defenderEnergyHit,
                        IonShieldPenetrationAudioPath = _defenderIonPenetration,
                        IonShieldHitAudioPath = _defenderIonHit,
                        ProjectileShieldPenetrationAudioPath = _defenderProjectilePenetration,
                        ProjectileShieldHitAudioPath = _defenderProjectileHit,
                        SuperlaserAudioPath = _defenderSuperlaser,
                    },
                },
                played.Add,
                _ => 1f
            );
        }

        [Test]
        public void Advance_QueuedArrivals_StartsFirstCueImmediately()
        {
            audio.QueueArrival(TacticalBattleSide.Attacker);
            audio.QueueArrival(TacticalBattleSide.Defender);

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { _attackerArrival }, played);
        }

        [Test]
        public void Advance_ActiveCueCompletes_StartsNextQueuedCue()
        {
            audio.QueueArrival(TacticalBattleSide.Attacker);
            audio.QueueArrival(TacticalBattleSide.Defender);
            audio.Advance(0f);

            audio.Advance(1f);

            CollectionAssert.AreEqual(new[] { _attackerArrival, _defenderArrival }, played);
        }

        [Test]
        public void QueueEvents_Withdrawal_QueuesSourceFactionCue()
        {
            TacticalUnitState attacker = CreateUnit(TacticalBattleSide.Attacker);
            audio.QueueEvents(
                new[]
                {
                    TacticalCombatEvent.UnitLifecycle(
                        TacticalCombatEventKind.UnitWithdrawn,
                        attacker
                    ),
                }
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { _attackerWithdrawal }, played);
        }

        [Test]
        public void QueueEvents_Superlaser_QueuesSourceFactionCue()
        {
            TacticalUnitState attacker = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);
            audio.QueueEvents(new[] { TacticalCombatEvent.SuperlaserFired(defender, attacker) });

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { _defenderSuperlaser }, played);
        }

        [TestCase(TacticalWeaponType.Turbolaser, false, _defenderEnergyHit)]
        [TestCase(TacticalWeaponType.LaserCannon, true, _defenderEnergyPenetration)]
        [TestCase(TacticalWeaponType.Torpedo, false, _defenderProjectileHit)]
        [TestCase(TacticalWeaponType.Torpedo, true, _defenderProjectilePenetration)]
        [TestCase(TacticalWeaponType.IonCannon, false, _defenderIonHit)]
        [TestCase(TacticalWeaponType.IonCannon, true, _defenderIonPenetration)]
        public void QueueEvents_WeaponImpact_QueuesTargetFactionDamageCue(
            TacticalWeaponType weaponType,
            bool penetratedShields,
            string expectedPath
        )
        {
            TacticalUnitState attacker = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);

            audio.QueueEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        attacker,
                        defender,
                        weaponType,
                        penetratedShields
                    ),
                }
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { expectedPath }, played);
        }

        /// <summary>
        /// Creates one minimal capital-ship state on the requested tactical side.
        /// </summary>
        /// <param name="side">The tactical side assigned to the state.</param>
        /// <returns>The initialized tactical state.</returns>
        private static TacticalUnitState CreateUnit(TacticalBattleSide side)
        {
            return TacticalUnitState.FromCapitalShip(
                new CapitalShip { CurrentHullStrength = 1, MaxHullStrength = 1 },
                side
            );
        }
    }
}
