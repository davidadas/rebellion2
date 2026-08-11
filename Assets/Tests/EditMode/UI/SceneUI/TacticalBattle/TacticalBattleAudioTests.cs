using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Results;
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
        private const string _attackerFighterIonFire = "attacker-fighter-ion-fire";
        private const string _attackerFighterLaserFire = "attacker-fighter-laser-fire";
        private const string _attackerIonFire = "attacker-ion-fire";
        private const string _attackerLaserFire = "attacker-laser-fire";
        private const string _attackerTorpedoFire = "attacker-torpedo-fire";
        private const string _attackerTurbolaserFire = "attacker-turbolaser-fire";
        private const string _attackerVoiceRoot = "attacker-voice";
        private const string _defenderCapitalShipArrival = "defender-capital-arrival";
        private const string _defenderFighterArrival = "defender-fighter-arrival";
        private const string _defenderEnergyPenetration = "defender-energy-penetration";
        private const string _defenderEnergyHit = "defender-energy-hit";
        private const string _defenderIonPenetration = "defender-ion-penetration";
        private const string _defenderIonHit = "defender-ion-hit";
        private const string _defenderProjectilePenetration = "defender-projectile-penetration";
        private const string _defenderProjectileHit = "defender-projectile-hit";
        private const string _defenderSuperlaser = "defender-superlaser";
        private const string _defenderSmallDestruction = "defender-small-destruction";
        private const string _defenderMediumDestruction = "defender-medium-destruction";
        private const string _defenderLargeDestruction = "defender-large-destruction";
        private const string _defenderTractorLock = "defender-tractor-lock";
        private const string _defenderTractorRelease = "defender-tractor-release";
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
                        CapitalShipArrivalAudio = Cue(_attackerCapitalShipArrival),
                        CapitalShipWithdrawalAudio = Cue(_attackerCapitalShipWithdrawal),
                        FighterArrivalAudio = Cue(_attackerFighterArrival),
                        FighterWithdrawalAudio = Cue(_attackerFighterWithdrawal),
                        LaserCannonFireAudio = Cue(_attackerLaserFire),
                        FighterLaserCannonFireAudio = Cue(_attackerFighterLaserFire),
                        TurbolaserFireAudio = Cue(_attackerTurbolaserFire),
                        IonCannonFireAudio = Cue(_attackerIonFire),
                        FighterIonCannonFireAudio = Cue(_attackerFighterIonFire),
                        TorpedoFireAudio = Cue(_attackerTorpedoFire),
                        Voice = new TacticalVoiceTheme
                        {
                            AudioRoot = _attackerVoiceRoot,
                            FleetReady = "fleet-ready",
                            WithdrawalPreparing = "withdrawal-preparing",
                            WithdrawalBlocked = "withdrawal-blocked",
                            DeathStar = new TacticalDeathStarVoiceTheme
                            {
                                Approaching = "death-star-approaching",
                                AttackWindowOpen = "death-star-attack-window-open",
                                FighterScreen = "death-star-fighter-screen",
                                Shielded = "death-star-shielded",
                                SuperlaserFiring = "superlaser-firing",
                                SuperlaserReady = "superlaser-ready",
                                SuperlaserWarning = "superlaser-warning",
                                AttackReports = new List<string>
                                {
                                    "attack-report-1",
                                    "attack-report-2",
                                },
                                AttackGroups = CreateDeathStarAttackGroups(),
                            },
                            OrdersRequested = CreateGroupVoice("orders-requested"),
                            ManeuverAcknowledged = CreateGroupVoice("maneuver"),
                            AttackAcknowledged = CreateGroupVoice("attack"),
                            FormationAcknowledged = CreateGroupVoice("formation"),
                            MissionAcknowledged = CreateGroupVoice("mission"),
                            FightersLaunched = CreateGroupVoice("fighters-launched"),
                            FightersRecovered = CreateGroupVoice("fighters-recovered"),
                            UnitLost = CreateGroupVoice("unit-lost"),
                            TargetDestroyed = CreateGroupVoice("target-destroyed"),
                            Outcome = new TacticalOutcomeVoiceTheme
                            {
                                WithdrawalComplete = "withdrawal-complete",
                                EnemyWithdrew = "victory-withdrawal",
                                EnemyDestroyed = "victory-destruction",
                                FleetDestroyed = "defeat",
                            },
                        },
                    },
                    [TacticalBattleSide.Defender] = new TacticalBattleTheme
                    {
                        CapitalShipArrivalAudio = Cue(_defenderCapitalShipArrival),
                        FighterArrivalAudio = Cue(_defenderFighterArrival),
                        EnergyShieldPenetrationAudio = Cue(_defenderEnergyPenetration),
                        EnergyShieldHitAudio = Cue(_defenderEnergyHit),
                        IonShieldPenetrationAudio = Cue(_defenderIonPenetration),
                        IonShieldHitAudio = Cue(_defenderIonHit),
                        ProjectileShieldPenetrationAudio = Cue(_defenderProjectilePenetration),
                        ProjectileShieldHitAudio = Cue(_defenderProjectileHit),
                        SuperlaserAudio = Cue(_defenderSuperlaser),
                        SmallShipDestructionAudio = Cue(_defenderSmallDestruction),
                        MediumShipDestructionAudio = Cue(_defenderMediumDestruction),
                        LargeShipDestructionAudio = Cue(_defenderLargeDestruction),
                        TractorLockAudio = Cue(_defenderTractorLock),
                        TractorReleaseAudio = Cue(_defenderTractorRelease),
                        Voice = new TacticalVoiceTheme
                        {
                            AudioRoot = "defender-voice",
                            DeathStar = new TacticalDeathStarVoiceTheme
                            {
                                InsufficientFighterScreen =
                                    "death-star-insufficient-fighter-screen",
                                UnderAttack = "death-star-under-attack",
                                AttackContinuing = "death-star-attack-continuing",
                                AttackBrokenOff = "death-star-attack-broken-off",
                                Destroyed = "death-star-destroyed",
                            },
                        },
                    },
                },
                played.Add,
                _ => 1f,
                _ => 0
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
        public void Advance_UnitAndCombatCuesQueued_StartsBothChannelsImmediately()
        {
            TacticalUnitState attacker = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);
            audio.QueueArrival(attacker);
            audio.QueueEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        attacker,
                        defender,
                        TacticalWeaponType.Turbolaser,
                        TacticalImpactState.Shield
                    ),
                }
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { _attackerCapitalShipArrival, _attackerTurbolaserFire },
                played
            );
        }

        [Test]
        public void QueueFleetReady_ConfiguredFactionVoice_QueuesFleetReadyCue()
        {
            audio.QueueFleetReady(TacticalBattleSide.Attacker);

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/fleet-ready" }, played);
        }

        [Test]
        public void QueueManeuverAcknowledged_TaskForce_QueuesNumberedTaskForceCue()
        {
            audio.QueueManeuverAcknowledged(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.CapitalShip,
                1
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/task-force-2-maneuver" }, played);
        }

        [Test]
        public void QueueAttackAcknowledged_FighterGroup_QueuesNamedFighterGroupCue()
        {
            audio.QueueAttackAcknowledged(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.Fighters,
                2
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "attacker-voice/fighter-group-green-attack" },
                played
            );
        }

        [Test]
        public void QueueFormationAcknowledged_TaskForce_QueuesNumberedTaskForceCue()
        {
            audio.QueueFormationAcknowledged(TacticalBattleSide.Attacker, 0);

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/task-force-1-formation" }, played);
        }

        [Test]
        public void QueueMissionAcknowledged_FighterGroup_QueuesNamedFighterGroupCue()
        {
            audio.QueueMissionAcknowledged(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.Fighters,
                3
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "attacker-voice/fighter-group-gold-mission" },
                played
            );
        }

        [Test]
        public void QueueOutcome_EnemyWithdrew_QueuesWithdrawalVictoryCue()
        {
            audio.QueueOutcome(
                TacticalBattleSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Withdrawn
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/victory-withdrawal" }, played);
        }

        [Test]
        public void QueueUnitLost_FighterGroup_QueuesNamedLossReport()
        {
            audio.QueueUnitLost(TacticalBattleSide.Attacker, TacticalUnitKind.Fighters, 1);

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "attacker-voice/fighter-group-blue-unit-lost" },
                played
            );
        }

        [Test]
        public void QueueOrdersRequested_FighterGroup_QueuesNamedSelectionReport()
        {
            audio.QueueOrdersRequested(TacticalBattleSide.Attacker, TacticalUnitKind.Fighters, 2);

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "attacker-voice/fighter-group-green-orders-requested" },
                played
            );
        }

        [Test]
        public void QueueFightersLaunched_FighterGroup_QueuesNamedLaunchReport()
        {
            audio.QueueFightersLaunched(TacticalBattleSide.Attacker, 0);

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "attacker-voice/fighter-group-red-fighters-launched" },
                played
            );
        }

        [Test]
        public void QueueFightersLaunched_ConfiguredLaunchEffect_QueuesLaunchEffect()
        {
            TacticalBattleAudio launchAudio = new TacticalBattleAudio(
                new Dictionary<TacticalBattleSide, TacticalBattleTheme>
                {
                    [TacticalBattleSide.Attacker] = new TacticalBattleTheme
                    {
                        FighterLaunchAudio = Cue("fighter-launch"),
                    },
                    [TacticalBattleSide.Defender] = new TacticalBattleTheme(),
                },
                played.Add,
                _ => 1f,
                _ => 0
            );

            launchAudio.QueueFightersLaunched(TacticalBattleSide.Attacker, 0);
            launchAudio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "fighter-launch" }, played);
        }

        [Test]
        public void QueueFightersRecovered_FighterGroup_QueuesNamedRecoveryReport()
        {
            audio.QueueFightersRecovered(TacticalBattleSide.Attacker, 3);

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "attacker-voice/fighter-group-gold-fighters-recovered" },
                played
            );
        }

        [Test]
        public void QueueTargetDestroyed_TaskForce_QueuesNumberedDestructionReport()
        {
            audio.QueueTargetDestroyed(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.CapitalShip,
                2
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "attacker-voice/task-force-3-target-destroyed" },
                played
            );
        }

        [Test]
        public void QueueWithdrawalPreparing_ConfiguredFactionVoice_QueuesPreparingReport()
        {
            audio.QueueWithdrawalPreparing(TacticalBattleSide.Attacker);

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/withdrawal-preparing" }, played);
        }

        [Test]
        public void QueueSuperlaserReports_PlayedSideFires_QueuesFiringReport()
        {
            TacticalUnitState source = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Defender);

            audio.QueueSuperlaserReports(
                TacticalBattleSide.Attacker,
                new[] { TacticalCombatEvent.SuperlaserFired(source, target) }
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/superlaser-firing" }, played);
        }

        [Test]
        public void QueueSuperlaserReports_OpposingSideFires_QueuesWarningReport()
        {
            TacticalUnitState source = CreateUnit(TacticalBattleSide.Defender);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Attacker);

            audio.QueueSuperlaserReports(
                TacticalBattleSide.Attacker,
                new[] { TacticalCombatEvent.SuperlaserFired(source, target) }
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/superlaser-warning" }, played);
        }

        [Test]
        public void QueueSuperlaserReports_PlayedSideBecomesReady_QueuesReadinessReport()
        {
            TacticalUnitState source = CreateUnit(TacticalBattleSide.Attacker);

            audio.QueueSuperlaserReports(
                TacticalBattleSide.Attacker,
                new[]
                {
                    TacticalCombatEvent.UnitLifecycle(
                        TacticalCombatEventKind.SuperlaserReady,
                        source
                    ),
                }
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/superlaser-ready" }, played);
        }

        [Test]
        public void QueueDeathStarAttackBegin_NumberedFighterGroup_QueuesBeginReport()
        {
            audio.QueueDeathStarAttackBegin(TacticalBattleSide.Attacker, 1);

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "attacker-voice/fighter-group-blue-death-star-begin" },
                played
            );
        }

        [Test]
        public void QueueDeathStarAvailability_OpposingExposedDeathStar_QueuesApproachThenAttackWindow()
        {
            audio.QueueDeathStarAvailability(
                TacticalBattleSide.Attacker,
                TacticalBattleSide.Defender,
                TacticalDeathStarAttackAvailability.Available,
                true
            );

            audio.Advance(0f);
            audio.Advance(1f);

            CollectionAssert.AreEqual(
                new[]
                {
                    "attacker-voice/death-star-approaching",
                    "attacker-voice/death-star-attack-window-open",
                },
                played
            );
        }

        [Test]
        public void QueueDeathStarAvailability_ShieldedOpposingDeathStar_QueuesShieldReport()
        {
            audio.QueueDeathStarAvailability(
                TacticalBattleSide.Attacker,
                TacticalBattleSide.Defender,
                TacticalDeathStarAttackAvailability.Shielded,
                false
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/death-star-shielded" }, played);
        }

        [Test]
        public void QueueDeathStarAvailability_DefendingFighterScreen_QueuesScreenReport()
        {
            audio.QueueDeathStarAvailability(
                TacticalBattleSide.Attacker,
                TacticalBattleSide.Defender,
                TacticalDeathStarAttackAvailability.FighterScreen,
                false
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/death-star-fighter-screen" }, played);
        }

        [Test]
        public void QueueDeathStarAvailability_FriendlyDeathStarExposed_QueuesInsufficientScreenReport()
        {
            audio.QueueDeathStarAvailability(
                TacticalBattleSide.Defender,
                TacticalBattleSide.Defender,
                TacticalDeathStarAttackAvailability.Available,
                true
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "defender-voice/death-star-insufficient-fighter-screen" },
                played
            );
        }

        [Test]
        public void QueueDeathStarAttackReports_AttackingGroupStarts_QueuesRunningReport()
        {
            TacticalUnitState attacker = CreateUnit(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.Fighters
            );
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);

            audio.QueueDeathStarAttackReports(
                TacticalBattleSide.Attacker,
                new[]
                {
                    TacticalCombatEvent.DeathStarAttackPhase(
                        TacticalCombatEventKind.DeathStarAttackStarted,
                        attacker,
                        defender
                    ),
                },
                _ => 1,
                _ => TacticalDeathStarAttackAvailability.Available
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "attacker-voice/fighter-group-blue-death-star-running" },
                played
            );
        }

        [Test]
        public void QueueDeathStarAttackReports_TimedReport_QueuesConfiguredChatter()
        {
            TacticalUnitState attacker = CreateUnit(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.Fighters
            );
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);

            audio.QueueDeathStarAttackReports(
                TacticalBattleSide.Attacker,
                new[] { TacticalCombatEvent.DeathStarAttackReport(attacker, defender, 1) },
                _ => 0,
                _ => TacticalDeathStarAttackAvailability.Available
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/attack-report-2" }, played);
        }

        [Test]
        public void QueueDeathStarAttackReports_DefendingDeathStarAttacked_QueuesWarning()
        {
            TacticalUnitState attacker = CreateUnit(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.Fighters
            );
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);

            audio.QueueDeathStarAttackReports(
                TacticalBattleSide.Defender,
                new[]
                {
                    TacticalCombatEvent.DeathStarAttackPhase(
                        TacticalCombatEventKind.DeathStarAttackStarted,
                        attacker,
                        defender
                    ),
                },
                _ => -1,
                _ => TacticalDeathStarAttackAvailability.FighterScreen
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "defender-voice/death-star-under-attack" }, played);
        }

        [Test]
        public void QueueDeathStarAttackReports_FailedEnemyRun_QueuesBrokenOffReport()
        {
            TacticalUnitState attacker = CreateUnit(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.Fighters
            );
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);

            audio.QueueDeathStarAttackReports(
                TacticalBattleSide.Defender,
                new[]
                {
                    TacticalCombatEvent.DeathStarAttackPhase(
                        TacticalCombatEventKind.DeathStarAttackFailed,
                        attacker,
                        defender
                    ),
                },
                _ => -1,
                _ => TacticalDeathStarAttackAvailability.FighterScreen
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "defender-voice/death-star-attack-broken-off" },
                played
            );
        }

        [Test]
        public void QueueDeathStarAttackReports_FailedEnemyRunWithAnotherRunAvailable_QueuesContinuingReport()
        {
            TacticalUnitState attacker = CreateUnit(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.Fighters
            );
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);

            audio.QueueDeathStarAttackReports(
                TacticalBattleSide.Defender,
                new[]
                {
                    TacticalCombatEvent.DeathStarAttackPhase(
                        TacticalCombatEventKind.DeathStarAttackFailed,
                        attacker,
                        defender
                    ),
                },
                _ => -1,
                _ => TacticalDeathStarAttackAvailability.Available
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { "defender-voice/death-star-attack-continuing" },
                played
            );
        }

        [Test]
        public void QueueDeathStarAttackReports_DestroyedDeathStar_QueuesDestroyedReport()
        {
            TacticalUnitState attacker = CreateUnit(
                TacticalBattleSide.Attacker,
                TacticalUnitKind.Fighters
            );
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);

            audio.QueueDeathStarAttackReports(
                TacticalBattleSide.Defender,
                new[]
                {
                    TacticalCombatEvent.DeathStarAttackPhase(
                        TacticalCombatEventKind.DeathStarAttackSucceeded,
                        attacker,
                        defender
                    ),
                },
                _ => -1,
                _ => TacticalDeathStarAttackAvailability.NoTarget
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "defender-voice/death-star-destroyed" }, played);
        }

        [Test]
        public void QueueWithdrawalBlocked_ConfiguredFactionVoice_QueuesBlockedReport()
        {
            audio.QueueWithdrawalBlocked(TacticalBattleSide.Attacker);

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/withdrawal-blocked" }, played);
        }

        [Test]
        public void QueueOutcome_PlayedFleetWithdrew_QueuesWithdrawalCompleteCue()
        {
            audio.QueueOutcome(
                TacticalBattleSide.Attacker,
                SpaceCombatSideOutcome.Withdrawn,
                SpaceCombatSideOutcome.Active
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/withdrawal-complete" }, played);
        }

        [Test]
        public void QueueOutcome_EnemyDestroyed_QueuesDestructionVictoryCue()
        {
            audio.QueueOutcome(
                TacticalBattleSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Destroyed
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/victory-destruction" }, played);
        }

        [Test]
        public void QueueOutcome_PlayedFleetDestroyed_QueuesDefeatCue()
        {
            audio.QueueOutcome(
                TacticalBattleSide.Attacker,
                SpaceCombatSideOutcome.Destroyed,
                SpaceCombatSideOutcome.Active
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "attacker-voice/defeat" }, played);
        }

        [Test]
        public void IsVoiceIdle_ActiveOutcomeCue_ReturnsFalseUntilCueCompletes()
        {
            audio.QueueOutcome(
                TacticalBattleSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Destroyed
            );
            audio.Advance(0f);

            bool active = audio.IsVoiceIdle;
            audio.Advance(1f);

            Assert.IsFalse(active);
            Assert.IsTrue(audio.IsVoiceIdle);
        }

        [Test]
        public void Advance_VoiceAndCombatCuesQueued_StartsBothChannelsImmediately()
        {
            TacticalUnitState attacker = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);
            audio.QueueFleetReady(TacticalBattleSide.Attacker);
            audio.QueueEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        attacker,
                        defender,
                        TacticalWeaponType.Turbolaser,
                        TacticalImpactState.Shield
                    ),
                }
            );

            audio.Advance(0f);

            CollectionAssert.AreEqual(
                new[] { _attackerTurbolaserFire, "attacker-voice/fleet-ready" },
                played
            );
        }

        [Test]
        public void Advance_CombatCueWaitsPastLifetime_DiscardsStaleCue()
        {
            TacticalUnitState attacker = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);
            TacticalCombatEvent[] impacts = new TacticalCombatEvent[5];
            for (int index = 0; index < impacts.Length; index++)
            {
                impacts[index] = TacticalCombatEvent.WeaponImpact(
                    attacker,
                    defender,
                    TacticalWeaponType.Turbolaser,
                    TacticalImpactState.Shield
                );
            }
            audio.QueueEvents(impacts);
            audio.Advance(0f);

            audio.Advance(4f);

            CollectionAssert.AreEqual(
                new[]
                {
                    _attackerTurbolaserFire,
                    _defenderEnergyHit,
                    _attackerTurbolaserFire,
                    _defenderEnergyHit,
                },
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

        [TestCase(TacticalUnitKind.Fighters, 1, _defenderSmallDestruction)]
        [TestCase(TacticalUnitKind.CapitalShip, 1099, _defenderSmallDestruction)]
        [TestCase(TacticalUnitKind.CapitalShip, 1100, _defenderMediumDestruction)]
        [TestCase(TacticalUnitKind.CapitalShip, 2000, _defenderMediumDestruction)]
        [TestCase(TacticalUnitKind.CapitalShip, 2001, _defenderLargeDestruction)]
        public void QueueEvents_UnitDestroyed_QueuesHullClassCue(
            TacticalUnitKind kind,
            int maximumHull,
            string expectedPath
        )
        {
            TacticalUnitState unit = CreateUnit(TacticalBattleSide.Defender, kind, maximumHull);

            audio.QueueEvents(
                new[]
                {
                    TacticalCombatEvent.UnitLifecycle(TacticalCombatEventKind.UnitDestroyed, unit),
                }
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { expectedPath }, played);
        }

        [TestCase(TacticalCombatEventKind.TractorLock, _defenderTractorLock)]
        [TestCase(TacticalCombatEventKind.TractorRelease, _defenderTractorRelease)]
        public void QueueEvents_TractorLifecycle_QueuesTargetFactionCue(
            TacticalCombatEventKind kind,
            string expectedPath
        )
        {
            TacticalUnitState attacker = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);

            audio.QueueEvents(new[] { TacticalCombatEvent.TractorLock(kind, attacker, defender) });
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { expectedPath }, played);
        }

        [TestCase(TacticalWeaponType.Turbolaser, false, _defenderEnergyHit)]
        [TestCase(TacticalWeaponType.LaserCannon, true, _defenderProjectilePenetration)]
        [TestCase(TacticalWeaponType.Torpedo, false, _defenderEnergyHit)]
        [TestCase(TacticalWeaponType.Torpedo, true, _defenderEnergyPenetration)]
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
            audio.Advance(1f);

            Assert.AreEqual(expectedPath, played[1]);
        }

        [TestCase(TacticalWeaponType.LaserCannon, TacticalUnitKind.CapitalShip, _attackerLaserFire)]
        [TestCase(
            TacticalWeaponType.LaserCannon,
            TacticalUnitKind.Fighters,
            _attackerFighterLaserFire
        )]
        [TestCase(
            TacticalWeaponType.Turbolaser,
            TacticalUnitKind.CapitalShip,
            _attackerTurbolaserFire
        )]
        [TestCase(TacticalWeaponType.IonCannon, TacticalUnitKind.CapitalShip, _attackerIonFire)]
        [TestCase(TacticalWeaponType.IonCannon, TacticalUnitKind.Fighters, _attackerFighterIonFire)]
        [TestCase(TacticalWeaponType.Torpedo, TacticalUnitKind.Fighters, _attackerTorpedoFire)]
        public void QueueEvents_WeaponImpact_QueuesSourceWeaponFireCueBeforeDamageCue(
            TacticalWeaponType weaponType,
            TacticalUnitKind sourceKind,
            string expectedPath
        )
        {
            TacticalUnitState attacker = CreateUnit(TacticalBattleSide.Attacker, sourceKind);
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);

            audio.QueueEvents(
                new[] { TacticalCombatEvent.WeaponImpact(attacker, defender, weaponType) }
            );
            audio.Advance(0f);

            CollectionAssert.AreEqual(new[] { expectedPath }, played);
        }

        [Test]
        public void QueueEvents_WeaponCueHasVariants_QueuesSelectedVariant()
        {
            TacticalUnitState attacker = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState defender = CreateUnit(TacticalBattleSide.Defender);
            TacticalBattleAudio selectingAudio = new TacticalBattleAudio(
                new Dictionary<TacticalBattleSide, TacticalBattleTheme>
                {
                    [TacticalBattleSide.Attacker] = new TacticalBattleTheme
                    {
                        LaserCannonFireAudio = new TacticalAudioCueTheme
                        {
                            Paths = new List<string> { "first", "second" },
                        },
                    },
                    [TacticalBattleSide.Defender] = new TacticalBattleTheme(),
                },
                played.Add,
                _ => 1f,
                _ => 1
            );

            selectingAudio.QueueEvents(
                new[]
                {
                    TacticalCombatEvent.WeaponImpact(
                        attacker,
                        defender,
                        TacticalWeaponType.LaserCannon
                    ),
                }
            );
            selectingAudio.Advance(0f);

            CollectionAssert.AreEqual(new[] { "second" }, played);
        }

        /// <summary>
        /// Creates one minimal tactical unit of the requested class and side.
        /// </summary>
        /// <param name="side">The tactical side assigned to the state.</param>
        /// <param name="kind">The tactical unit class to create.</param>
        /// <returns>The initialized tactical state.</returns>
        private static TacticalUnitState CreateUnit(
            TacticalBattleSide side,
            TacticalUnitKind kind = TacticalUnitKind.CapitalShip,
            int maximumHull = 1
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
                new CapitalShip
                {
                    CurrentHullStrength = maximumHull,
                    MaxHullStrength = maximumHull,
                },
                side
            );
        }

        /// <summary>
        /// Creates a tactical sound event with one predictable variant.
        /// </summary>
        /// <param name="path">The configured sound path.</param>
        /// <returns>The configured tactical sound event.</returns>
        private static TacticalAudioCueTheme Cue(string path)
        {
            return new TacticalAudioCueTheme { Paths = new List<string> { path } };
        }

        /// <summary>
        /// Creates predictable numbered command responses for tactical audio tests.
        /// </summary>
        /// <param name="category">The response category included in every audio name.</param>
        /// <returns>The configured command-group response set.</returns>
        private static TacticalGroupVoiceTheme CreateGroupVoice(string category)
        {
            return new TacticalGroupVoiceTheme
            {
                Ship = $"ship-{category}",
                TaskForces = new List<string>
                {
                    $"task-force-1-{category}",
                    $"task-force-2-{category}",
                    $"task-force-3-{category}",
                    $"task-force-4-{category}",
                    $"task-force-5-{category}",
                    $"task-force-6-{category}",
                    $"task-force-7-{category}",
                    $"task-force-8-{category}",
                },
                FighterGroups = new List<string>
                {
                    $"fighter-group-red-{category}",
                    $"fighter-group-blue-{category}",
                    $"fighter-group-green-{category}",
                    $"fighter-group-gold-{category}",
                },
            };
        }

        /// <summary>
        /// Creates predictable named fighter-group reports for Death Star attack tests.
        /// </summary>
        /// <returns>The configured attack-group reports.</returns>
        private static List<TacticalDeathStarAttackGroupVoiceTheme> CreateDeathStarAttackGroups()
        {
            string[] groupNames = { "red", "blue", "green", "gold" };
            List<TacticalDeathStarAttackGroupVoiceTheme> groups =
                new List<TacticalDeathStarAttackGroupVoiceTheme>();
            foreach (string groupName in groupNames)
            {
                groups.Add(
                    new TacticalDeathStarAttackGroupVoiceTheme
                    {
                        Begin = $"fighter-group-{groupName}-death-star-begin",
                        Running = $"fighter-group-{groupName}-death-star-running",
                        Failed = $"fighter-group-{groupName}-death-star-failed",
                        Succeeded = $"fighter-group-{groupName}-death-star-succeeded",
                    }
                );
            }

            return groups;
        }
    }
}
