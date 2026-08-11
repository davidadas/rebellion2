using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public sealed class TacticalBattleAudioTests
    {
        private const string _attackerCapitalShipArrival = "attacker-capital-arrival";
        private const string _attackerCapitalShipWithdrawal = "attacker-capital-withdrawal";
        private const string _attackerFighterArrival = "attacker-fighter-arrival";
        private const string _attackerFighterWithdrawal = "attacker-fighter-withdrawal";
        private const string _defenderCapitalShipArrival = "defender-capital-arrival";
        private const string _defenderFighterArrival = "defender-fighter-arrival";
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
                        CapitalShipArrivalAudioPath = _attackerCapitalShipArrival,
                        CapitalShipWithdrawalAudioPath = _attackerCapitalShipWithdrawal,
                        FighterArrivalAudioPath = _attackerFighterArrival,
                        FighterWithdrawalAudioPath = _attackerFighterWithdrawal,
                    },
                    [TacticalBattleSide.Defender] = new TacticalBattleTheme
                    {
                        CapitalShipArrivalAudioPath = _defenderCapitalShipArrival,
                        FighterArrivalAudioPath = _defenderFighterArrival,
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
            audio.QueueArrival(CreateUnit(TacticalBattleSide.Attacker));
            audio.QueueArrival(CreateUnit(TacticalBattleSide.Defender, TacticalUnitKind.Fighters));

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { _attackerCapitalShipArrival }, played);
        }

        [Test]
        public void Advance_ActiveCueCompletes_StartsNextQueuedCue()
        {
            audio.QueueArrival(CreateUnit(TacticalBattleSide.Attacker));
            audio.QueueArrival(CreateUnit(TacticalBattleSide.Defender, TacticalUnitKind.Fighters));
            audio.Advance(0f);

            audio.Advance(1f);

            CollectionAssert.AreEqual(
                new[] { _attackerCapitalShipArrival, _defenderFighterArrival },
                played
            );
        }

        [Test]
        public void QueueEvents_CapitalShipWithdrawal_QueuesCapitalShipCue()
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

            CollectionAssert.AreEqual(new[] { _attackerCapitalShipWithdrawal }, played);
        }

        [Test]
        public void QueueEvents_FighterWithdrawal_QueuesFighterCue()
        {
            TacticalUnitState attacker = CreateUnit(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.Fighters
            );
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

            CollectionAssert.AreEqual(new[] { _attackerFighterWithdrawal }, played);
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
                        penetratedShields ? TacticalImpactState.Hull : TacticalImpactState.Shield
                    ),
                }
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { expectedPath }, played);
        }

        /// <summary>
        /// Creates one minimal tactical unit of the requested class and side.
        /// </summary>
        /// <param name="side">The tactical side assigned to the state.</param>
        /// <param name="kind">The tactical unit class to create.</param>
        /// <returns>The initialized tactical state.</returns>
        private static TacticalUnitState CreateUnit(
            TacticalBattleSide side,
            TacticalUnitKind kind = TacticalUnitKind.CapitalShip
        )
        {
            if (kind == TacticalUnitKind.Fighters)
            {
                return TacticalUnitState.FromFighters(
                    new Starfighter { CurrentSquadronSize = 1 },
                    side
                );
            }

            return TacticalUnitState.FromCapitalShip(
                new CapitalShip { CurrentHullStrength = 1, MaxHullStrength = 1 },
                side
            );
        }
    }
}
