using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatSessionTests
    {
        [Test]
        public void AdvanceClocked_MissingClock_RejectsImplicitTimeProvider()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );

            Assert.Throws<InvalidOperationException>(() => session.AdvanceClocked(16));
        }

        [Test]
        public void ConfigureClockedAdvance_DeltaDrivenTimeAlreadyUsed_RejectsMixedOwners()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            session.Advance(16);

            Assert.Throws<InvalidOperationException>(() =>
                session.ConfigureClockedAdvance(() => 1000)
            );
        }

        [Test]
        public void Advance_ClockBoundEncounter_RejectsTimedLegacyAdvancement()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            session.ConfigureClockedAdvance(() => 1000);

            Assert.Throws<InvalidOperationException>(() => session.Advance(16));
        }

        [Test]
        public void AdvanceClocked_FrozenProvider_FrameMagnitudeCannotAdvanceSystemClock()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            session.ConfigureClockedAdvance(() => 1000);
            session.AdvanceClocked(0);
            session.AdvanceClocked(0);
            Assert.AreEqual(4, session.SystemTime);

            session.AdvanceClocked(5000);
            session.AdvanceClocked(5000);

            Assert.AreEqual(4, session.SystemTime);
        }

        [Test]
        public void AdvanceClocked_ZeroFrame_AdmitsWholeSecondClockPhase()
        {
            uint now = 1000;
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            session.ConfigureClockedAdvance(() => now);
            session.AdvanceClocked(0);
            session.AdvanceClocked(0);
            now += 1000;

            session.AdvanceClocked(0);
            session.AdvanceClocked(0);

            Assert.AreEqual(5, session.SystemTime);
        }

        [Test]
        public void AdvanceClocked_InitiallyPaused_InitializesOnlyAfterResume()
        {
            uint now = 1000;
            int reads = 0;
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            session.ConfigureClockedAdvance(() =>
            {
                reads++;
                return now;
            });
            session.IsPaused = true;
            session.AdvanceClocked(50);
            now += 5000;
            Assert.AreEqual(0, reads);

            session.IsPaused = false;
            session.AdvanceClocked(0);

            Assert.AreEqual(3, session.SystemTime);
            Assert.AreEqual(1, reads);
            session.AdvanceClocked(0);
            Assert.AreEqual(4, session.SystemTime);
        }

        [Test]
        public void AdvanceClocked_PausedAfterStartup_RetainsCoarseBaseAcrossResume()
        {
            uint now = 1000;
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            session.ConfigureClockedAdvance(() => now);
            session.AdvanceClocked(0);
            session.AdvanceClocked(0);
            session.IsPaused = true;
            now += 2500;
            session.AdvanceClocked(50);
            Assert.AreEqual(4, session.SystemTime);

            session.IsPaused = false;
            session.AdvanceClocked(0);
            session.AdvanceClocked(0);

            Assert.AreEqual(5, session.SystemTime);
        }

        [Test]
        public void AdvanceClocked_ArrivalOwnsFrameTime_DoesNotSampleSchedulerUntilEntryCompletes()
        {
            uint now = 1000;
            int reads = 0;
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0),
                enteringSide: CombatSide.Attacker
            );
            session.ConfigureClockedAdvance(() =>
            {
                reads++;
                return now;
            });
            session.BeginArrival(new ArrivalPresentation());
            for (int frame = 0; frame < 8; frame++)
            {
                now += 500;
                session.AdvanceClocked(500);
            }
            Assert.IsTrue(session.IsArriving);
            Assert.AreEqual(0, reads);

            now += 500;
            session.AdvanceClocked(500);

            Assert.IsFalse(session.IsArriving);
            Assert.AreEqual(3, session.SystemTime);
            Assert.Greater(reads, 0);
        }

        [Test]
        public void AdvanceClocked_LongFrame_UsesSameContinuousIntervalAsSmallerFrameAtSameClock()
        {
            TacticalCombatSession first = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatSession second = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            SchedulerClock firstClock = new(1000);
            SchedulerClock secondClock = new(1000);
            first.SetWaypoint(first.Groups[0], new Vector3(0, 0, -10));
            second.SetWaypoint(second.Groups[0], new Vector3(0, 0, -10));
            StartClockedSession(first, firstClock);
            StartClockedSession(second, secondClock);
            Vector3 before = first.Groups[0].Formation.Position;
            firstClock.Advance(2500);
            secondClock.Advance(2500);

            first.AdvanceClocked(2500);
            second.AdvanceClocked(500);

            Assert.AreNotEqual(before, first.Groups[0].Formation.Position);
            Assert.AreEqual(second.Units[0].Position, first.Units[0].Position);
            Assert.AreEqual(
                second.Groups[0].Formation.Position,
                first.Groups[0].Formation.Position
            );
        }

        [TestCase(CombatSide.Attacker, 0)]
        [TestCase(CombatSide.Defender, 1000000)]
        public void BeginArrival_CapturedEnteringForce_PresentsOnlyItsMembers(
            CombatSide enteringSide,
            int expectedIndex
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0),
                enteringSide: enteringSide
            );
            ArrivalPresentation presentation = new();
            session.BeginArrival(presentation);

            session.Advance(50);
            Assert.IsEmpty(presentation.ResetIndices);
            session.Advance(50);

            CollectionAssert.AreEqual(new[] { expectedIndex }, presentation.ResetIndices);
        }

        [Test]
        public void Advance_ArrivalInProgress_PreservesSimulationClockAndPositions()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0),
                enteringSide: CombatSide.Attacker
            );
            Vector3 before = session.Units[0].Position;
            session.BeginArrival(new ArrivalPresentation());

            for (int frame = 0; frame < 8; frame++)
                session.Advance(500);

            Assert.IsTrue(session.IsArriving);
            Assert.AreEqual(3, session.SystemTime);
            Assert.AreEqual(before, session.Units[0].Position);
        }

        [Test]
        public void Advance_ArrivalFinishes_ReleasesDirectControl()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0),
                enteringSide: CombatSide.Attacker
            );
            ArrivalPresentation presentation = new();
            session.BeginArrival(presentation);
            Assert.IsFalse(session.IsDirectControlEnabled);

            for (int frame = 0; frame < 9; frame++)
                session.Advance(500);

            Assert.IsFalse(session.IsArriving);
            Assert.IsTrue(session.IsDirectControlEnabled);
            Assert.IsFalse(presentation.Strobe);
        }

        [Test]
        public void Advance_PausedDuringArrival_CompletesPresentationWhileRetainingPause()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0),
                enteringSide: CombatSide.Attacker
            );
            session.IsPaused = true;
            session.BeginArrival(new ArrivalPresentation());

            for (int frame = 0; frame < 9; frame++)
                session.Advance(500);

            Assert.IsFalse(session.IsArriving);
            Assert.IsTrue(session.IsPaused);
            Assert.AreEqual(3, session.SystemTime);
        }

        [Test]
        public void BeginArrival_UncapturedEnteringForce_RejectsPresentation()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );

            Assert.Throws<InvalidOperationException>(() =>
                session.BeginArrival(new ArrivalPresentation())
            );
        }

        [TestCase(TacticalWeaponType.LaserCannon, true)]
        [TestCase(TacticalWeaponType.IonCannon, true)]
        [TestCase(TacticalWeaponType.Turbolaser, true)]
        [TestCase(TacticalWeaponType.Torpedo, true)]
        [TestCase(TacticalWeaponType.LaserCannon, false)]
        [TestCase(TacticalWeaponType.IonCannon, false)]
        [TestCase(TacticalWeaponType.Turbolaser, false)]
        [TestCase(TacticalWeaponType.Torpedo, false)]
        public void Advance_QueuedDischargeActorReachesZeroHull_StillAnnouncesFire(
            TacticalWeaponType weapon,
            bool sourceReachesZero
        )
        {
            TacticalCombatSession session = CreateDischargeSession(weapon);
            int shots = 0;
            session.ShotFired += shot =>
            {
                if (shot.Weapon == weapon)
                    shots++;
            };
            WaitForQueuedDischarge(session);
            Assert.AreEqual(0, shots);
            session.GetUnit(sourceReachesZero ? "a" : "b").Systems.Destroy();

            AdvanceSeconds(session, 12);

            Assert.That(shots, Is.GreaterThan(0));
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        [TestCase(float.NaN)]
        public void Advance_QueuedImpactEntersWithNonpositiveHull_ClampsHullWithoutDeactivation(
            float entryHull
        )
        {
            TacticalCombatSession session = CreateDischargeSession(TacticalWeaponType.Turbolaser);
            TacticalCombatUnit target = session.GetUnit("b");
            int shots = 0;
            int effects = 0;
            int deaths = 0;
            session.ShotFired += _ =>
            {
                shots++;
                target.Systems.RestorePools(entryHull, 37);
            };
            session.ImpactApplied += (_, _, _) => effects++;
            session.UnitDestroyed += _ => deaths++;

            AdvanceSeconds(session, 20);

            Assert.That(shots, Is.GreaterThan(0));
            Assert.AreEqual(0, target.Systems.Hull);
            Assert.AreEqual(37, target.Systems.Shields);
            Assert.AreEqual(TacticalCombatUnitStatus.Active, target.Status);
            Assert.IsNotNull(session.GetGroup(target));
            Assert.AreEqual(0, effects);
            Assert.AreEqual(0, deaths);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Advance_QueuedDischargeActorEntersHyperspace_RejectsFire(bool sourceLeaves)
        {
            TacticalCombatSession session = CreateDischargeSession(TacticalWeaponType.Turbolaser);
            int shots = 0;
            session.ShotFired += _ => shots++;
            WaitForQueuedDischarge(session);
            session.GetUnit(sourceLeaves ? "a" : "b").Status =
                TacticalCombatUnitStatus.EnteringHyperspace;

            AdvanceSeconds(session, 12);

            Assert.AreEqual(0, shots);
        }

        [Test]
        public void Advance_QueuedImpactSourceLeavesAfterDischarge_StillDamagesActiveTarget()
        {
            TacticalCombatSession session = CreateDischargeSession(TacticalWeaponType.Turbolaser);
            TacticalCombatUnit source = session.GetUnit("a");
            TacticalCombatUnit target = session.GetUnit("b");
            session.ShotFired += _ => source.Status = TacticalCombatUnitStatus.Withdrawn;

            AdvanceSeconds(session, 20);

            Assert.That(target.Systems.Hull, Is.LessThan(1000));
        }

        [TestCase(TacticalCombatUnitStatus.Active)]
        [TestCase(TacticalCombatUnitStatus.EnteringHyperspace)]
        [TestCase(TacticalCombatUnitStatus.Destroyed)]
        public void Advance_QueuedComponentDamageAfterParticipationChanges_StillUpdatesRetainedUnit(
            TacticalCombatUnitStatus status
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0)
            );
            TacticalCombatUnit target = session.GetUnit("b");
            target.Systems.ApplyHit(
                TacticalWeaponType.Turbolaser,
                1,
                1,
                1,
                new FixedRandom(100),
                "a"
            );
            target.Status = status;
            target.Systems.Destroy();

            AdvanceSeconds(session, 6);

            Assert.AreEqual(1, target.Systems.GetDamage(TacticalShipSystem.Hyperdrive));
        }

        [TestCase(TacticalCombatUnitStatus.Active)]
        [TestCase(TacticalCombatUnitStatus.EnteringHyperspace)]
        [TestCase(TacticalCombatUnitStatus.Destroyed)]
        public void Advance_QueuedIonDisruptionAfterParticipationChanges_AppliesKnownBatteriesWithoutTimerChanges(
            TacticalCombatUnitStatus status
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0)
            );
            TacticalCombatUnit target = session.GetUnit("b");
            target.Systems.ApplyDisruption(11, 17);
            TacticalSystemEvent? applied = null;
            session.SystemChanged += (unit, change) =>
            {
                if (
                    ReferenceEquals(unit, target)
                    && change.Kind == TacticalSystemEventKind.Disruption
                )
                    applied = change;
            };
            target.Systems.ApplyHit(TacticalWeaponType.IonCannon, 1, 1, 1, new FixedRandom(2), "a");
            target.Status = status;
            target.Systems.Destroy();

            AdvanceSeconds(session, 6);

            Assert.IsTrue(applied.HasValue);
            Assert.IsNull(applied.Value.AttackDelay);
            Assert.IsNull(applied.Value.MovementDelay);
            Assert.AreEqual(11, target.Systems.AttackDelay);
            Assert.AreEqual(17, target.Systems.MovementDelay);
            Assert.IsFalse(target.Weapons.IsReady(TacticalWeaponArc.Aft));
            Assert.IsTrue(target.Weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.IsTrue(target.Weapons.IsReady(TacticalWeaponArc.Starboard));
            Assert.IsTrue(target.Weapons.IsReady(TacticalWeaponArc.Port));
        }

        [TestCase(TacticalCombatUnitStatus.Active)]
        [TestCase(TacticalCombatUnitStatus.EnteringHyperspace)]
        [TestCase(TacticalCombatUnitStatus.Destroyed)]
        public void Advance_QueuedComponentRepairAfterParticipationChanges_StillUpdatesRetainedUnit(
            TacticalCombatUnitStatus status
        )
        {
            CapitalShip victim = CreateShip("b", 1000, 0);
            victim.DamageControl = 100;
            TacticalCombatSession session = CreateSession(CreateShip("a", 1000, 0), victim);
            TacticalCombatUnit target = session.GetUnit("b");
            target.Systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);
            target.Systems.Advance(1, new FixedRandom(0));
            target.Status = status;
            target.Systems.Destroy();

            AdvanceSeconds(session, 6);

            Assert.AreEqual(0, target.Systems.GetDamage(TacticalShipSystem.Engines));
        }

        [Test]
        public void Advance_QueuedTractorRepairWithZeroHull_RetainsItsLock()
        {
            CapitalShip source = CreateShip("a", 1000, 1);
            source.TractorBeamPower = 10;
            source.TractorBeamnRange = 1000;
            source.DamageControl = 100;
            TacticalCombatSession session = CreateSession(source, CreateShip("b", 1000000, 0));
            TacticalCombatUnit unit = session.GetUnit("a");
            AdvanceSeconds(session, 20);
            Assert.AreEqual("b", unit.TractorTargetID);
            unit.Systems.ApplyComponentEvent(TacticalShipSystem.TractorBeam, false);
            unit.Systems.Advance(1, new FixedRandom(0));
            unit.Systems.Destroy();

            AdvanceSeconds(session, 12);

            Assert.AreEqual(0, unit.Systems.GetDamage(TacticalShipSystem.TractorBeam));
            Assert.AreEqual("b", unit.TractorTargetID);
        }

        [Test]
        public void Advance_LethalQueuedWeaponImpact_ReportsRegisteredShooterBeforeExplosion()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 10000),
                CreateShip("b", 1000, 0),
                NotificationBank(17, 86, 9)
            );
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };
            session.ImpactApplied += (victim, weapon, impact) =>
            {
                Assert.AreSame(session.GetUnit("b"), victim);
                Assert.AreEqual(TacticalWeaponType.Turbolaser, weapon);
                Assert.AreEqual(TacticalCombatImpactKind.Destroyed, impact);
                Assert.AreEqual(0, victim.Systems.Hull);
                Assert.AreEqual(TacticalCombatUnitStatus.Active, victim.Status);
                output.Add("effect");
            };
            session.UnitDestroyed += _ => output.Add("destroyed");
            session.DestructionEffectRequested += _ =>
                Assert.Fail("A weapon impact already owns its destruction effect.");

            AdvanceSeconds(session, 20);

            Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, session.GetUnit("b").Status);
            CollectionAssert.AreEqual(new[] { "17", "86", "effect", "9", "destroyed" }, output);
        }

        [TestCase(CombatSide.Attacker, 86)]
        [TestCase(CombatSide.Defender, 208)]
        public void Audio_PlayerKill_UsesFiringCapitalCategoryBeforeExplosion(
            CombatSide player,
            int reportID
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0),
                NotificationBank(17, 9, 86, 208),
                player
            );
            TacticalCombatUnit attacker =
                session.Units[0].Side == player ? session.Units[0] : session.Units[1];
            TacticalCombatUnit victim =
                session.Units[0].Side != player ? session.Units[0] : session.Units[1];
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            victim.Systems.ApplyHit(
                TacticalWeaponType.Turbolaser,
                2000,
                1,
                1,
                new SystemRandomProvider(1),
                attacker.InstanceID
            );

            CollectionAssert.AreEqual(new[] { "17", reportID.ToString(), "9" }, output);
        }

        [TestCase(CombatSide.Attacker, 134, 99)]
        [TestCase(CombatSide.Defender, 260, 221)]
        public void Audio_PlayerCapitalLost_ReportsEveryCapturedPersonnelHostBeforeCategory(
            CombatSide player,
            int personnelID,
            int reportID
        )
        {
            string victimID = player == CombatSide.Attacker ? "a" : "b";
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0),
                NotificationBank(134, 260, 99, 221),
                player,
                new[] { victimID, "unrelated-host", victimID }
            );
            TacticalCombatUnit victim = session.GetUnit(victimID);
            TacticalCombatUnit attacker = session.GetUnit(victimID == "a" ? "b" : "a");
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            victim.Systems.ApplyHit(
                TacticalWeaponType.Turbolaser,
                2000,
                1,
                1,
                new SystemRandomProvider(1),
                attacker.InstanceID
            );

            CollectionAssert.AreEqual(
                new[] { personnelID.ToString(), personnelID.ToString(), reportID.ToString() },
                output
            );
        }

        [Test]
        public void Audio_PendingGroupTransfer_UsesAppliedOwnerAndCurrentPlayerSlot()
        {
            TacticalCombatSession session = CreateMembershipSession(NotificationBank(86, 89, 91));
            TacticalCombatUnit attacker = session.GetUnit("first");
            TacticalCombatGroup applied = session.GetGroup(attacker);
            TacticalCombatGroup requested = session.CreateGroup(new[] { attacker });
            session.SetObjectGroup(CombatSide.Attacker, 0, null);
            session.SetObjectGroup(CombatSide.Attacker, 3, requested);
            session.SetObjectGroup(CombatSide.Attacker, 5, applied);
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            session
                .GetUnit("enemy")
                .Systems.ApplyHit(
                    TacticalWeaponType.Turbolaser,
                    200,
                    1,
                    1,
                    new SystemRandomProvider(1),
                    attacker.InstanceID
                );

            Assert.AreSame(applied, session.GetGroup(attacker));
            Assert.AreSame(requested, session.GetRequestedGroup(attacker));
            CollectionAssert.AreEqual(new[] { "91" }, output);
        }

        [TestCase(0, 94)]
        [TestCase(3, 97)]
        [TestCase(4, 98)]
        public void Audio_FighterCategorySlotCleared_UsesRetainedDeploymentCategory(
            int category,
            int reportID
        )
        {
            TacticalCombatSession session = CreateMembershipSession(
                NotificationBank(94, 97, 98),
                fighterCategory: category
            );
            TacticalCombatUnit attacker = session.GetUnit("fighter");
            session.DeleteGroup(session.GetGroup(attacker));
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            session
                .GetUnit("enemy")
                .Systems.ApplyHit(
                    TacticalWeaponType.Turbolaser,
                    200,
                    1,
                    1,
                    new SystemRandomProvider(1),
                    attacker.InstanceID
                );

            CollectionAssert.AreEqual(new[] { reportID.ToString() }, output);
        }

        [TestCase(null)]
        [TestCase("unregistered")]
        public void Audio_LethalHitWithoutRegisteredAttacker_DoesNotReportDestruction(
            string attackerID
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0),
                NotificationBank(85, 86, 98, 99)
            );
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            session
                .GetUnit("b")
                .Systems.ApplyHit(
                    TacticalWeaponType.Turbolaser,
                    2000,
                    1,
                    1,
                    new SystemRandomProvider(1),
                    attackerID
                );

            Assert.IsEmpty(output);
        }

        [Test]
        public void Audio_FiringUnitWithZeroHull_StillCreditsRegisteredShooter()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0),
                NotificationBank(86)
            );
            session.GetUnit("a").Systems.Destroy();
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            session
                .GetUnit("b")
                .Systems.ApplyHit(
                    TacticalWeaponType.Turbolaser,
                    2000,
                    1,
                    1,
                    new SystemRandomProvider(1),
                    "a"
                );

            CollectionAssert.AreEqual(new[] { "86" }, output);
        }

        [Test]
        public void Advance_WeaponImpacts_LeaveCampaignHullUnchanged()
        {
            CapitalShip attacker = CreateShip("attacker", 1000, 100);
            CapitalShip defender = CreateShip("defender", 1000, 0);
            TacticalCombatSession session = CreateSession(attacker, defender);
            AdvanceSeconds(session, 20);

            Assert.Less(session.GetUnit("defender").Systems.Hull, 1000);
            Assert.AreEqual(1000, defender.CurrentHullStrength);
        }

        [Test]
        public void Advance_SameSeedAndCommands_ProducesIdenticalBattleResults()
        {
            TacticalCombatSession first = CreateSession(
                CreateShip("a", 100, 100),
                CreateShip("b", 100, 100)
            );
            TacticalCombatSession second = CreateSession(
                CreateShip("a", 100, 100),
                CreateShip("b", 100, 100)
            );
            AdvanceSeconds(first, 60);
            AdvanceSeconds(second, 60);

            Assert.AreEqual(first.IsComplete, second.IsComplete);
            for (int index = 0; index < first.Units.Count; index++)
            {
                Assert.AreEqual(first.Units[index].Systems.Hull, second.Units[index].Systems.Hull);
                Assert.AreEqual(first.Units[index].Status, second.Units[index].Status);
                Assert.AreEqual(first.Units[index].Position, second.Units[index].Position);
            }
        }

        [Test]
        public void Advance_DischargeBeforeImpact_DelaysHullDamage()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 100),
                CreateShip("b", 1000, 0)
            );
            bool observedDischargeBeforeImpact = false;
            session.ShotFired += shot =>
                observedDischargeBeforeImpact |= shot.Target.Systems.Hull == 1000;
            AdvanceSeconds(session, 20);

            Assert.IsTrue(observedDischargeBeforeImpact);
        }

        [Test]
        public void Advance_DestroyedSide_CompletesEncounter()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 1000),
                CreateShip("b", 10, 0)
            );
            AdvanceSeconds(session, 30);

            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, session.GetUnit("b").Status);
            Assert.IsNull(session.GetGroup(session.GetUnit("b")));
        }

        [Test]
        public void Advance_PausedEncounter_DoesNotAdvanceSystems()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 10),
                CreateShip("b", 100, 10)
            );
            session.IsPaused = true;
            AdvanceSeconds(session, 20);

            Assert.AreEqual(3, session.SystemTime);
            Assert.AreEqual(100, session.GetUnit("b").Systems.Hull);
        }

        [Test]
        public void CanWithdraw_OpposingGravityWell_PreventsDeparture()
        {
            CapitalShip interdictor = CreateShip("b", 100, 0);
            interdictor.HasGravityWell = true;
            TacticalCombatSession session = CreateSession(CreateShip("a", 100, 0), interdictor);

            Assert.IsFalse(session.CanWithdraw(CombatSide.Attacker));
        }

        [Test]
        public void CanWithdraw_DisabledHyperdrive_PreventsDeparture()
        {
            CapitalShip ship = CreateShip("a", 100, 0);
            ship.SystemDamage.PrimaryHyperdrive = 1;
            TacticalCombatSession session = CreateSession(ship, CreateShip("b", 100, 0));

            Assert.IsFalse(session.CanWithdraw(CombatSide.Attacker));
        }

        [Test]
        public void BeginWithdrawal_EligibleSide_StartsThirtySecondMinimumCountdown()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );

            Assert.IsTrue(session.BeginWithdrawal(CombatSide.Attacker));
            Assert.AreEqual(30000, session.GetSide(CombatSide.Attacker).WithdrawalMilliseconds);
            Assert.AreEqual(TacticalCombatOrder.Withdraw, session.Groups[0].Order);
        }

        [Test]
        public void Advance_WithdrawnShip_CompletesWithoutCampaignMutation()
        {
            CapitalShip ship = CreateShip("a", 100, 0);
            TacticalCombatSession session = CreateSession(ship, CreateShip("b", 100, 0));
            session.BeginWithdrawal(CombatSide.Attacker);
            AdvanceSeconds(session, 100);

            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual(TacticalCombatUnitStatus.Withdrawn, session.GetUnit("a").Status);
            Assert.IsNull(session.GetGroup(session.GetUnit("a")));
            Assert.AreEqual(100, ship.CurrentHullStrength);
        }

        [Test]
        public void SetOrder_BeforeCommandDispatch_PreservesActiveBehavior()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.SetOrder(group, TacticalCombatOrder.HoldFire);
            Assert.AreEqual(TacticalCombatOrder.HoldFire, group.Order);
            Assert.AreEqual(TacticalCombatOrder.None, group.ActiveOrder);
        }

        [Test]
        public void SetOrder_AfterCommandDispatch_AppliesActiveBehavior()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.SetOrder(group, TacticalCombatOrder.HoldFire);
            AdvanceSeconds(session, 8);
            Assert.AreEqual(TacticalCombatOrder.HoldFire, group.ActiveOrder);
        }

        [Test]
        public void CreateGroup_DuplicateMember_LeavesPreviousFormationIntact()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatUnit unit = session.GetUnit("a");
            TacticalCombatGroup group = session.GetGroup(unit);
            Assert.Throws<ArgumentException>(() => session.CreateGroup(new[] { unit, unit }));
            Assert.AreSame(group, session.GetGroup(unit));
            Assert.AreEqual(1, group.Members.Count);
        }

        [TestCase(CombatSide.Attacker)]
        [TestCase(CombatSide.Defender)]
        public void CreateGroup_LowInitialIDs_RetainsFactionRegistrationBase(
            CombatSide factionOneSide
        )
        {
            CombatSide factionZeroSide =
                factionOneSide == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker;
            TacticalCombatUnit first = new(CreateShip("first", 100, 0), factionZeroSide, 1);
            TacticalCombatUnit selected = new(CreateShip("selected", 100, 0), factionOneSide, 2);
            TacticalCombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                },
                factionZeroSide,
                3
            );
            TacticalCombatGroup firstGroup = new(101, new[] { first }, Vector3.UnitZ);
            TacticalCombatGroup previous = new(104, new[] { selected }, -Vector3.UnitZ);
            TacticalCombatGroup fighterGroup = new(105, new[] { fighter }, Vector3.UnitZ);
            TacticalCombatSession session = new(
                new[] { first, selected, fighter },
                new[] { firstGroup, previous, fighterGroup },
                CreateSide(
                    CombatSide.Attacker,
                    factionOneSide == CombatSide.Attacker ? 1 : 0,
                    Vector3.UnitZ
                ),
                CreateSide(
                    CombatSide.Defender,
                    factionOneSide == CombatSide.Defender ? 1 : 0,
                    -Vector3.UnitZ
                ),
                factionOneSide,
                100,
                new SystemRandomProvider(42)
            );

            TacticalCombatGroup requested = session.CreateGroup(new[] { selected });

            Assert.AreEqual(1000001, requested.ID);
            Assert.AreSame(previous, session.GetGroup(selected));
            Assert.AreSame(requested, session.GetRequestedGroup(selected));
            Assert.AreSame(fighterGroup, session.GetGroup(fighter));
            AdvanceSeconds(session, 8);
            Assert.AreSame(requested, session.GetGroup(selected));
            Assert.AreSame(fighterGroup, session.GetGroup(fighter));
            Assert.AreEqual(105, fighter.GroupID);
            Assert.IsEmpty(previous.Members);
            CollectionAssert.DoesNotContain(session.Groups, previous);
        }

        [Test]
        public void CreateGroup_QueuedOrderForReplacedGroup_DoesNotAffectReplacement()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatUnit member = session.GetUnit("a");
            TacticalCombatGroup previous = session.GetGroup(member);
            session.SetOrder(previous, TacticalCombatOrder.Hammer);

            TacticalCombatGroup replacement = session.CreateGroup(new[] { member });
            AdvanceSeconds(session, 8);

            CollectionAssert.DoesNotContain(session.Groups, previous);
            Assert.AreSame(replacement, session.GetGroup(member));
            Assert.AreEqual(TacticalCombatOrder.None, replacement.ActiveOrder);
            Assert.IsEmpty(replacement.RequestedWaypoints);
        }

        [Test]
        public void CreateGroup_BeforeDispatch_PreservesAppliedOwnership()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatGroup previous = session.GetGroup(first);

            TacticalCombatGroup requested = session.CreateGroup(new[] { first });

            Assert.AreSame(previous, session.GetGroup(first));
            Assert.AreSame(requested, session.GetRequestedGroup(first));
            CollectionAssert.AreEqual(new[] { first }, previous.Members);
            Assert.IsEmpty(previous.RequestedMembers);
            Assert.IsEmpty(requested.Members);
            CollectionAssert.DoesNotContain(session.Groups, requested);
        }

        [Test]
        public void CreateGroup_PausedCommands_PreservesAppliedRosterUntilResume()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatGroup previous = session.GetGroup(first);
            session.IsPaused = true;
            TacticalCombatGroup requested = session.CreateGroup(new[] { first });

            AdvanceSeconds(session, 8);

            Assert.AreSame(previous, session.GetGroup(first));
            Assert.AreSame(requested, session.GetRequestedGroup(first));
            session.IsPaused = false;
            AdvanceSeconds(session, 8);
            Assert.AreSame(requested, session.GetGroup(first));
        }

        [Test]
        public void CreateGroup_OrderedMixedSelection_PreservesLeaderAndCommandGroupKind()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit[] ordered =
            {
                session.GetUnit("fighter"),
                session.GetUnit("first"),
                session.GetUnit("second"),
            };

            TacticalCombatGroup requested = session.CreateGroup(ordered);
            TacticalCombatUnit[] registryOrder = { ordered[2], ordered[0], ordered[1] };
            CollectionAssert.AreEqual(registryOrder, requested.RequestedMembers);
            Assert.AreSame(ordered[0], requested.RequestedLeader);
            AdvanceSeconds(session, 8);

            CollectionAssert.AreEqual(registryOrder, requested.Members);
            Assert.AreSame(ordered[0], requested.PrimaryMember);
            Assert.AreEqual(TacticalCombatGroupKind.Command, requested.Kind);
            Assert.AreEqual(TacticalCombatFormationKind.Line, requested.Formation.Kind);
        }

        [Test]
        public void CreateGroup_TwoReassignmentsBeforeDispatch_AppliesMembershipCommandsInOrder()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatGroup transient = session.CreateGroup(new[] { first });
            TacticalCombatGroup final = session.CreateGroup(new[] { first });

            AdvanceSeconds(session, 8);

            Assert.AreSame(final, session.GetGroup(first));
            Assert.AreSame(final, session.GetRequestedGroup(first));
            CollectionAssert.DoesNotContain(session.Groups, transient);
            Assert.IsEmpty(transient.Members);
            Assert.IsEmpty(transient.RequestedMembers);
        }

        [Test]
        public void AddGroupMember_ExistingDestination_AppendsAfterDelayedRemoval()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatUnit second = session.GetUnit("second");
            TacticalCombatGroup previous = session.GetGroup(second);
            TacticalCombatGroup destination = session.GetGroup(first);

            session.AddGroupMember(destination, second);
            Assert.AreSame(previous, session.GetGroup(second));
            CollectionAssert.AreEqual(new[] { second, first }, destination.RequestedMembers);
            AdvanceSeconds(session, 8);

            CollectionAssert.AreEqual(new[] { second, first }, destination.Members);
            Assert.AreSame(destination, session.GetGroup(second));
            CollectionAssert.DoesNotContain(session.Groups, previous);
        }

        [Test]
        public void SetTarget_FriendlyDesignation_PreservesRequestedAndAppliedLanes()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("first"));
            TacticalCombatUnit friendly = session.GetUnit("second");
            session.SetTarget(group, friendly);
            session.SetOrder(group, TacticalCombatOrder.HoldFire);
            CollectionAssert.AreEqual(new[] { friendly }, group.RequestedTargets);
            Assert.IsNull(group.PrimaryTarget);

            AdvanceSeconds(session, 8);

            Assert.AreSame(friendly, group.PrimaryTarget);
            Assert.AreEqual(TacticalCombatOrder.HoldFire, group.ActiveOrder);
        }

        [Test]
        public void RemoveGroupMember_DesignatedLeader_PromotesFirstRemainingRegistryMember()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatUnit second = session.GetUnit("second");
            TacticalCombatUnit fighter = session.GetUnit("fighter");
            TacticalCombatGroup group = session.CreateGroup(new[] { first, fighter, second });
            AdvanceSeconds(session, 8);

            session.RemoveGroupMember(group, first);
            Assert.AreSame(second, group.RequestedLeader);
            Assert.AreSame(first, group.PrimaryMember);
            AdvanceSeconds(session, 8);

            Assert.AreSame(second, group.PrimaryMember);
        }

        [Test]
        public void SetObjectGroup_PreviousOwnerRemoved_KeepsReplacementCategory()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatGroup replacement = session.CreateGroup(new[] { first });
            Assert.IsNull(session.GetObjectGroup(CombatSide.Attacker, 0));
            session.SetObjectGroup(CombatSide.Attacker, 0, replacement);

            AdvanceSeconds(session, 8);

            Assert.AreSame(replacement, session.GetObjectGroup(CombatSide.Attacker, 0));
        }

        [TestCase(TacticalCombatFormationKind.Line)]
        [TestCase(TacticalCombatFormationKind.Surround)]
        public void SetFormation_FighterGroup_AppliesRequestedLayout(
            TacticalCombatFormationKind kind
        )
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("fighter"));

            session.SetFormation(group, kind);
            Assert.AreEqual(kind, group.RequestedFormation);
            Assert.AreEqual(TacticalCombatFormationKind.Direct, group.Formation.Kind);
            AdvanceSeconds(session, 8);

            Assert.AreEqual(kind, group.Formation.Kind);
            Assert.AreEqual(TacticalCombatGroupKind.Fighter, group.Kind);
        }

        [Test]
        public void SetFormation_CapitalDirectRequest_AppliesLineLayout()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("first"));

            session.SetFormation(group, TacticalCombatFormationKind.Direct);
            AdvanceSeconds(session, 8);

            Assert.AreEqual(TacticalCombatFormationKind.Line, group.Formation.Kind);
        }

        [Test]
        public void DeleteGroup_BeforeDispatch_PreservesAppliedRoster()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit unit = session.GetUnit("first");
            TacticalCombatGroup previous = session.GetGroup(unit);

            session.DeleteGroup(previous);

            Assert.AreSame(previous, session.GetGroup(unit));
            Assert.IsNull(session.GetObjectGroup(CombatSide.Attacker, 0));
            AdvanceSeconds(session, 8);
            Assert.IsNull(session.GetGroup(unit));
            CollectionAssert.DoesNotContain(session.Groups, previous);
        }

        [Test]
        public void DeleteGroup_QueuedReplacement_PreservesNewRequestedOwner()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit unit = session.GetUnit("first");
            TacticalCombatGroup previous = session.GetGroup(unit);
            session.DeleteGroup(previous);
            TacticalCombatGroup replacement = session.CreateGroup(new[] { unit });

            AdvanceSeconds(session, 8);

            Assert.AreSame(replacement, session.GetGroup(unit));
            Assert.AreSame(replacement, session.GetRequestedGroup(unit));
        }

        [Test]
        public void CreateResult_UnfinishedEncounter_RejectsPrematureCommit()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            Assert.Throws<InvalidOperationException>(() => session.CreateResult());
        }

        [Test]
        public void CreateResult_CompletedEncounter_CapturesLossesWithoutChangingCampaignShips()
        {
            CapitalShip defender = CreateShip("b", 10, 0);
            TacticalCombatSession session = CreateSession(CreateShip("a", 1000, 1000), defender);
            AdvanceSeconds(session, 30);
            SpaceCombatResult result = session.CreateResult();
            Assert.AreEqual(0, result.ShipDamage.Find(damage => damage.Ship == defender).HullAfter);
            Assert.AreEqual(10, defender.CurrentHullStrength);
        }

        [TestCase(0.5f, 1)]
        [TestCase(0f, 1)]
        [TestCase(-8f, 1)]
        [TestCase(1.9f, 1)]
        [TestCase(42.9f, 42)]
        [TestCase(float.NegativeInfinity, 1)]
        [TestCase(float.NaN, 0)]
        [TestCase(float.PositiveInfinity, 0)]
        [TestCase(2147483648f, int.MinValue)]
        [TestCase(4294967296f, 0)]
        [TestCase(9223372036854775808f, 0)]
        [TestCase(float.MaxValue, 0)]
        public void CreateResult_RetainedCapital_ExportsHullThroughFinalSnapshotConversion(
            float hull,
            int expected
        )
        {
            TacticalCombatSession session = CreateCompletedExportSession();
            TacticalCombatUnit unit = session.GetUnit("exported");
            unit.Systems.RestorePools(hull, 0);

            SpaceCombatResult result = session.CreateResult();

            Assert.AreEqual(
                expected,
                result.ShipDamage.Find(damage => damage.Ship == unit.Source).HullAfter
            );
            Assert.AreEqual(hull, unit.Systems.Hull);
            Assert.AreEqual(100, ((CapitalShip)unit.Source).CurrentHullStrength);
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(50f)]
        [TestCase(float.NaN)]
        public void CreateResult_DestroyedCapital_ExportsZeroRegardlessOfRetainedPool(float hull)
        {
            TacticalCombatSession session = CreateCompletedExportSession();
            TacticalCombatUnit unit = session.GetUnit("exported");
            unit.Status = TacticalCombatUnitStatus.Destroyed;
            unit.Systems.RestorePools(hull, 0);

            SpaceCombatResult result = session.CreateResult();

            Assert.AreEqual(
                0,
                result.ShipDamage.Find(damage => damage.Ship == unit.Source).HullAfter
            );
            Assert.AreEqual(hull, unit.Systems.Hull);
        }

        [TestCase(0.5f, 1)]
        [TestCase(0f, 1)]
        [TestCase(-0.5f, 1)]
        [TestCase(-8f, -1)]
        [TestCase(7.9f, 1)]
        [TestCase(19.9f, 2)]
        [TestCase(float.NaN, 1)]
        [TestCase(float.PositiveInfinity, 1)]
        [TestCase(float.NegativeInfinity, 1)]
        [TestCase(17179869184f, int.MinValue)]
        [TestCase(34359738368f, 1)]
        public void CreateResult_RetainedFighter_ExportsCountThroughFinalSnapshotConversion(
            float hull,
            int expected
        )
        {
            TacticalCombatSession session = CreateCompletedExportSession(fighter: true);
            TacticalCombatUnit unit = session.GetUnit("exported");
            unit.Systems.RestorePools(hull, 0);

            SpaceCombatResult result = session.CreateResult();

            Assert.AreEqual(
                expected,
                result.FighterLosses.Find(loss => loss.Fighter == unit.Source).SquadsAfter
            );
            Assert.AreEqual(hull, unit.Systems.Hull);
            Assert.AreEqual(12, ((Starfighter)unit.Source).CurrentSquadronSize);
        }

        [TestCase(0f)]
        [TestCase(4f)]
        [TestCase(96f)]
        [TestCase(float.NaN)]
        public void CreateResult_DestroyedFighter_ExportsZeroRegardlessOfRetainedPool(float hull)
        {
            TacticalCombatSession session = CreateCompletedExportSession(fighter: true);
            TacticalCombatUnit unit = session.GetUnit("exported");
            unit.Status = TacticalCombatUnitStatus.Destroyed;
            unit.Systems.RestorePools(hull, 0);

            SpaceCombatResult result = session.CreateResult();

            Assert.AreEqual(
                0,
                result.FighterLosses.Find(loss => loss.Fighter == unit.Source).SquadsAfter
            );
            Assert.AreEqual(hull, unit.Systems.Hull);
        }

        [TestCase(0.5f, 0)]
        [TestCase(0f, 0)]
        [TestCase(1.9f, 1)]
        [TestCase(float.NaN, 0)]
        [TestCase(float.PositiveInfinity, 0)]
        public void CreateResult_BattleStation_ExportsStoredHullWithoutOrdinaryCapitalMinimum(
            float hull,
            int expected
        )
        {
            TacticalCombatSession session = CreateCompletedExportSession(station: true);
            TacticalCombatUnit unit = session.GetUnit("exported");
            unit.Systems.RestorePools(hull, 0);

            SpaceCombatResult result = session.CreateResult();

            Assert.AreEqual(
                expected,
                result.ShipDamage.Find(damage => damage.Ship == unit.Source).HullAfter
            );
            Assert.AreEqual(hull, unit.Systems.Hull);
        }

        [Test]
        public void SetAttackMode_QueuedMode_LeavesActiveEngagementUntilCommandPhase()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0)
            );
            TacticalCombatGroup group = session.Groups[0];
            group.SetAttackMode(true);
            Assert.IsTrue(group.CloseAttack);
            Assert.IsFalse(group.ActiveCloseAttack);
            AdvanceSeconds(session, 5);
            Assert.IsTrue(group.ActiveCloseAttack);
        }

        [Test]
        public void SetFormation_QueuedFormation_ChangesOnlyDuringCommandPhase()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0)
            );
            TacticalCombatGroup group = session.Groups[0];
            session.SetFormation(group, TacticalCombatFormationKind.Surround);
            Assert.AreEqual(TacticalCombatFormationKind.Line, group.Formation.Kind);
            AdvanceSeconds(session, 5);
            Assert.AreEqual(TacticalCombatFormationKind.Surround, group.Formation.Kind);
        }

        [Test]
        public void RemoveTarget_BeforeDispatch_ChangesRequestedListOnly()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.RemoveTarget(group, session.GetUnit("b"));
            Assert.IsEmpty(group.RequestedTargets);
            Assert.AreSame(session.GetUnit("b"), group.PrimaryTarget);
        }

        [Test]
        public void RemoveTarget_AddThenRemoveBeforeDispatch_LeavesNoActiveTarget()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.SetTarget(group, session.GetUnit("b"), true);
            session.RemoveTarget(group, session.GetUnit("b"));
            AdvanceSeconds(session, 5);
            Assert.IsEmpty(group.RequestedTargets);
            Assert.IsEmpty(group.Targets);
        }

        [Test]
        public void RemoveTarget_RemoveThenAddBeforeDispatch_PreservesLatestRequest()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            TacticalCombatUnit target = session.GetUnit("b");
            session.RemoveTarget(group, target);
            session.SetTarget(group, target, true);
            AdvanceSeconds(session, 5);
            CollectionAssert.AreEqual(new[] { target }, group.RequestedTargets);
            CollectionAssert.AreEqual(new[] { target }, group.Targets);
        }

        [Test]
        public void RemoveWaypoint_AddThenRemoveBeforeDispatch_CancelsRoute()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            Vector3 waypoint = new(10000, 0, 0);
            session.SetWaypoint(group, waypoint, true);
            session.RemoveWaypoint(group, waypoint);
            AdvanceSeconds(session, 5);
            Assert.IsEmpty(group.RequestedWaypoints);
            Assert.IsFalse(group.HasWaypoints);
        }

        [Test]
        public void SetWaypoint_RepeatedAppend_StoresOneNavigationPoint()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            Vector3 waypoint = new(10000, 0, 0);
            session.SetWaypoint(group, waypoint, true);
            session.SetWaypoint(group, waypoint, true);
            AdvanceSeconds(session, 5);
            CollectionAssert.AreEqual(new[] { waypoint }, group.RequestedWaypoints);
        }

        [Test]
        public void SetOrder_HoldFire_PreservesDesignatedTarget()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.SetOrder(group, TacticalCombatOrder.HoldFire);
            AdvanceSeconds(session, 5);
            CollectionAssert.AreEqual(new[] { session.GetUnit("b") }, group.RequestedTargets);
            Assert.AreSame(session.GetUnit("b"), group.PrimaryTarget);
        }

        [Test]
        public void SetOrder_None_ClearsRequestedTargetsBeforeDispatch()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.SetOrder(group, TacticalCombatOrder.None);
            Assert.IsEmpty(group.RequestedTargets);
            Assert.AreSame(session.GetUnit("b"), group.PrimaryTarget);
        }

        [Test]
        public void ActiveAttackRun_SuccessMovie_RemovesStationBeforeLaunchAndResetsOrderAfterward()
        {
            TacticalCombatSession session = CreateAttackSession();
            TacticalCombatOrder? orderAtLaunch = null;
            TacticalCombatUnitStatus? stationAtLaunch = null;
            session.AttackRunMovieRequested += request =>
            {
                orderAtLaunch = request.AttackRun.Group.Order;
                stationAtLaunch = session.BattleStation.Unit.Status;
            };

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, stationAtLaunch);
            Assert.AreEqual(TacticalCombatOrder.AttackBattleStation, orderAtLaunch);
            Assert.AreEqual(TacticalCombatOrder.None, session.ActiveAttackRun.Group.Order);
        }

        [Test]
        public void ActiveAttackRun_FailureMovie_DestroysSquadronAfterLaunch()
        {
            TacticalCombatSession session = CreateAttackSession();
            TacticalCombatUnitStatus? atLaunch = null;
            session.AttackRunMovieRequested += _ => atLaunch = session.GetUnit("fighter").Status;

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);

            Assert.AreEqual(TacticalCombatUnitStatus.AttackingBattleStation, atLaunch);
            Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, session.GetUnit("fighter").Status);
        }

        [Test]
        public void SetOrder_FailedAttackGroupRemoved_AllowsNextEligibleAttack()
        {
            TacticalCombatSession session = CreateAttackSession(reserveFighter: true);
            TacticalCombatAttackRun failed = session.ActiveAttackRun;
            TacticalCombatGroup reserve = session.GetGroup(session.GetUnit("reserve"));

            failed.ApplyState(TacticalCombatAttackRunState.Failed);
            session.PendingAttackRunMovie.Complete();

            CollectionAssert.DoesNotContain(session.Groups, failed.Group);
            Assert.IsNull(session.GetGroup(session.GetUnit("fighter")));
            Assert.IsNull(session.GetUnit("fighter").GroupID);
            Assert.IsTrue(session.CanAttackBattleStation(CombatSide.Attacker));
            session.SetOrder(reserve, TacticalCombatOrder.AttackBattleStation);
            AdvanceSeconds(session, 5);

            Assert.AreNotSame(failed, session.ActiveAttackRun);
            Assert.AreSame(reserve, session.ActiveAttackRun.Group);
        }

        [Test]
        public void Advance_AttackConclusionQueuedAfterRegrouping_IgnoresRemovedRun()
        {
            TacticalCombatSession session = CreateAttackSession();
            TacticalCombatAttackRun removed = session.ActiveAttackRun;
            TacticalCombatGroup replacement = session.CreateGroup(
                new[] { session.GetUnit("fighter") }
            );
            removed.Advance(120000);

            AdvanceSeconds(session, 5);

            Assert.IsNull(session.PendingAttackRunMovie);
            Assert.IsTrue(session.BattleStation.IsOperational);
            Assert.AreSame(replacement, session.GetGroup(session.GetUnit("fighter")));
            Assert.AreEqual(TacticalCombatOrder.None, replacement.ActiveOrder);
        }

        [Test]
        public void Advance_MovieWithoutPresentationListener_RemainsPausedUntilAcknowledged()
        {
            TacticalCombatSession session = CreateAttackSession();
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);
            int time = session.SystemTime;

            session.Advance(10000);

            Assert.AreEqual(time, session.SystemTime);
            Assert.IsFalse(session.PendingAttackRunMovie.IsComplete);
        }

        [Test]
        public void Advance_PausedElapsed_ResumesOneModuloReducedTick()
        {
            TacticalCombatSession session = CreateAttackSession();
            session.IsPaused = true;
            int before = session.SystemTime;
            session.Advance(20500);
            session.IsPaused = false;

            session.Advance(1);
            session.Advance(1);

            Assert.AreEqual(before + 1, session.SystemTime);
        }

        [Test]
        public void ActiveAttackRun_EnabledNotifications_SuspendsSchedulingOnlyDuringMovieLaunch()
        {
            TacticalCombatSession session = CreateAttackSession();
            bool? enabledDuringLaunch = null;
            session.AttackRunMovieRequested += _ =>
                enabledDuringLaunch = session.Audio.Notifications.Enabled;

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            Assert.AreEqual(false, enabledDuringLaunch);
            Assert.IsTrue(session.Audio.Notifications.Enabled);
        }

        [Test]
        public void ActiveAttackRun_DisabledNotifications_PreservesDisabledSchedulingAfterLaunch()
        {
            TacticalCombatSession session = CreateAttackSession();
            session.Audio.Notifications.Enabled = false;

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            Assert.IsFalse(session.Audio.Notifications.Enabled);
        }

        [TestCase(CombatSide.Attacker, true)]
        [TestCase(CombatSide.Defender, false)]
        public void FireSuperlaser_LiveTarget_ReportsSoundAndPlayerAdvisorBeforeVisual(
            CombatSide player,
            bool allianceAdvisor
        )
        {
            CapitalShip station = CreateShip("station", 1000, 0);
            station.CanDestroyPlanets = true;
            TacticalCombatSession session = CreateSession(
                CreateShip("target", 1000, 0),
                station,
                NotificationBank(20, 145),
                player
            );
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };
            session.SuperlaserFired += (_, _) => output.Add("visual");
            Assert.IsTrue(session.FireSuperlaser(session.GetUnit("target")));
            AdvanceSeconds(session, 5);
            CollectionAssert.AreEqual(
                allianceAdvisor ? new[] { "20", "145", "visual" } : new[] { "20", "visual" },
                output
            );
        }

        [Test]
        public void FireSuperlaser_TargetDestroyedBeforeDispatch_StillDispatchesVisualWithoutSound()
        {
            CapitalShip station = CreateShip("station", 1000, 0);
            station.CanDestroyPlanets = true;
            TacticalCombatSession session = CreateSession(
                CreateShip("target", 1000, 0),
                station,
                NotificationBank(20, 145)
            );
            TacticalCombatUnit target = session.GetUnit("target");
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };
            session.SuperlaserFired += (_, _) => output.Add("visual");
            Assert.IsTrue(session.FireSuperlaser(target));
            target.Systems.Destroy();
            AdvanceSeconds(session, 5);
            CollectionAssert.AreEqual(new[] { "visual" }, output);
        }

        [Test]
        public void FireSuperlaser_AdmittedTarget_RequestsExplosionThenEffectBeforeDeactivation()
        {
            CapitalShip station = CreateShip("station", 1000, 0);
            station.CanDestroyPlanets = true;
            TacticalCombatSession session = CreateSession(
                CreateShip("target", 1000, 0),
                station,
                NotificationBank(9, 86, 99, 134)
            );
            TacticalCombatUnit target = session.GetUnit("target");
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                Assert.AreEqual(1000, target.Systems.Hull);
                Assert.IsNotNull(session.GetGroup(target));
                output.Add(clip.Path);
                return null;
            };
            session.DestructionEffectRequested += unit =>
            {
                Assert.AreSame(target, unit);
                Assert.AreEqual(1000, unit.Systems.Hull);
                Assert.AreEqual(TacticalCombatUnitStatus.Active, unit.Status);
                Assert.IsNotNull(session.GetGroup(unit));
                output.Add("effect");
            };
            session.UnitDestroyed += unit =>
            {
                Assert.AreSame(target, unit);
                Assert.AreEqual(0, unit.Systems.Hull);
                Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, unit.Status);
                Assert.IsNull(session.GetGroup(unit));
                output.Add("destroyed");
            };
            session.ImpactApplied += (_, _, _) =>
                Assert.Fail("A direct command does not apply a weapon impact.");

            Assert.IsTrue(session.FireSuperlaser(target));
            AdvanceSeconds(session, 5);

            CollectionAssert.AreEqual(new[] { "9", "effect", "destroyed" }, output);
        }

        [Test]
        public void BeginWithdrawal_NewlyUnreadyCapital_RequestsScuttleEffectBeforeDeactivation()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("target", 1000, 0),
                CreateShip("enemy", 1000, 0),
                NotificationBank(9, 86, 99, 134)
            );
            TacticalCombatUnit target = session.GetUnit("target");
            Assert.IsTrue(session.BeginWithdrawal(CombatSide.Attacker));
            target.Systems.ApplyComponentEvent(TacticalShipSystem.Hyperdrive, false);
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                Assert.AreEqual(1000, target.Systems.Hull);
                Assert.IsTrue(target.IsActiveDisplayObject);
                output.Add(clip.Path);
                return null;
            };
            session.DestructionEffectRequested += unit =>
            {
                Assert.AreSame(target, unit);
                Assert.AreEqual(1000, unit.Systems.Hull);
                Assert.IsTrue(unit.IsActiveDisplayObject);
                Assert.IsNotNull(session.GetGroup(unit));
                output.Add("effect");
            };
            session.UnitDestroyed += unit =>
            {
                Assert.AreSame(target, unit);
                Assert.AreEqual(0, unit.Systems.Hull);
                Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, unit.Status);
                Assert.IsNull(session.GetGroup(unit));
                output.Add("destroyed");
            };

            for (
                int frame = 0;
                frame < 2400 && target.Status != TacticalCombatUnitStatus.Destroyed;
                frame++
            )
                session.Advance(50);

            CollectionAssert.AreEqual(new[] { "9", "effect", "destroyed" }, output);
        }

        [TestCase(CombatSide.Attacker, false)]
        [TestCase(CombatSide.Defender, true)]
        public void Advance_StationChargeCrossing_AnnouncesOnlyToEmpireBeforeChargeStore(
            CombatSide player,
            bool shouldAnnounce
        )
        {
            CapitalShip station = CreateShip("station", 1000, 0);
            station.CanDestroyPlanets = true;
            TacticalCombatSession session = CreateSession(
                CreateShip("target", 1000, 0),
                station,
                NotificationBank(275),
                player
            );
            float announcedCharge = -1;
            session.Audio.Playback = (_, _) =>
            {
                announcedCharge = session.BattleStation.Charge;
                return null;
            };
            session.BattleStation.Fire();
            session.BattleStation.Advance(300000);
            Assert.AreEqual(shouldAnnounce ? 0 : -1, announcedCharge);
            Assert.AreEqual(100, session.BattleStation.Charge);
        }

        [TestCase(CombatSide.Attacker)]
        [TestCase(CombatSide.Defender)]
        public void Advance_EnteringHyperspace_RetainsPositionAndFacesOppositeSideLane(
            CombatSide side
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0)
            );
            AdvanceSeconds(session, 3);
            TacticalCombatUnit unit = session.Units[(int)side];
            Vector3 position = unit.Position;
            unit.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            unit.Heading = Vector3.UnitX;

            session.Advance(16);

            Assert.AreEqual(-session.GetSide(side).Forward, unit.Heading);
            Assert.AreEqual(position, unit.Position);
            Assert.Greater(unit.HyperspaceMilliseconds, 0);
        }

        [Test]
        public void Advance_DepartureTimerCompletes_ResetsBeforeAwaitingWithdrawal()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0)
            );
            AdvanceSeconds(session, 3);
            TacticalCombatUnit unit = session.Units[0];
            unit.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            unit.HyperspaceMilliseconds = 3990;

            session.Advance(16);

            Assert.AreEqual(TacticalCombatUnitStatus.AwaitingWithdrawal, unit.Status);
            Assert.AreEqual(0, unit.HyperspaceMilliseconds);
        }

        [TestCase(0f, true)]
        [TestCase(float.NaN, true)]
        [TestCase(1f, false)]
        public void Advance_EnteringHyperspaceTimer_AnnouncesOnlyAtZeroOrUnorderedValue(
            float elapsed,
            bool shouldAnnounce
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0),
                NotificationBank(31)
            );
            AdvanceSeconds(session, 3);
            TacticalCombatUnit unit = session.GetUnit("a");
            unit.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            unit.HyperspaceMilliseconds = elapsed;
            List<float> observedTimers = new();
            session.Audio.Playback = (_, _) =>
            {
                observedTimers.Add(unit.HyperspaceMilliseconds);
                return null;
            };
            session.Advance(16);
            Assert.AreEqual(shouldAnnounce ? 1 : 0, observedTimers.Count);
            if (shouldAnnounce)
                Assert.AreEqual(elapsed, observedTimers[0]);
        }

        [Test]
        public void Advance_EnteringHyperspaceFighter_UsesFighterAnimationSound()
        {
            TacticalCombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                },
                CombatSide.Attacker,
                0
            );
            TacticalCombatUnit enemy = new(
                CreateShip("enemy", 1000, 0),
                CombatSide.Defender,
                1000000
            );
            TacticalCombatSession session = CreateEncounter(
                fighter,
                enemy,
                NotificationBank(29),
                CombatSide.Attacker
            );
            AdvanceSeconds(session, 3);
            fighter.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };
            session.Advance(16);
            CollectionAssert.AreEqual(new[] { "29" }, output);
        }

        [TestCase(CombatSide.Attacker, 32)]
        [TestCase(CombatSide.Defender, 154)]
        public void Advance_PausedStartup_QueuesSelectedFactionBriefing(
            CombatSide player,
            int messageID
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0),
                NotificationBank(32, 154),
                player
            );
            session.IsPaused = true;
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            session.Advance(16);

            CollectionAssert.AreEqual(new[] { messageID.ToString() }, output);
            Assert.AreEqual(3, session.SystemTime);
        }

        [Test]
        public void Advance_ZeroHullActiveParticipant_DoesNotCaptureTerminalExtinction()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            AdvanceSeconds(session, 3);
            session.GetUnit("b").Systems.Destroy();

            session.Advance(16);

            CollectionAssert.AreEqual(new bool[7], session.TerminalFlags);
            Assert.IsFalse(session.IsComplete);
        }

        [TestCase(CombatSide.Attacker, 152)]
        [TestCase(CombatSide.Defender, 283)]
        public void Advance_EmpireExtinction_QueuesSelectedFactionTerminalCue(
            CombatSide player,
            int messageID
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0),
                NotificationBank(152, 283),
                player
            );
            AdvanceSeconds(session, 3);
            session.GetUnit("b").Status = TacticalCombatUnitStatus.Destroyed;
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            session.Advance(16);

            CollectionAssert.AreEqual(new[] { messageID.ToString() }, output);
            CollectionAssert.AreEqual(
                new[] { false, true, false, false, false, false, false },
                session.TerminalFlags
            );
        }

        [Test]
        public void Advance_TerminalAssessment_SuspendsDirectCommandsBeforeCompletion()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            AdvanceSeconds(session, 3);
            Assert.IsTrue(session.IsDirectControlEnabled);
            session.GetUnit("b").Status = TacticalCombatUnitStatus.Destroyed;

            session.Advance(16);

            Assert.IsTrue(session.TerminalFlags[1]);
            Assert.IsFalse(session.IsComplete);
            Assert.IsFalse(session.IsDirectControlEnabled);
        }

        [Test]
        public void Advance_MutualExtinction_ClearsBothWinnerFlags()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            AdvanceSeconds(session, 3);
            session.GetUnit("a").Status = TacticalCombatUnitStatus.Destroyed;
            session.GetUnit("b").Status = TacticalCombatUnitStatus.Destroyed;

            session.Advance(16);

            CollectionAssert.AreEqual(new bool[7], session.TerminalFlags);
            Assert.AreEqual(CombatSide.Draw, session.Winner);
        }

        [Test]
        public void Advance_TerminalAssignmentStationRegisteredFirst_PrefersOrdinaryCarrier()
        {
            TacticalCombatSession session = CreateSurvivorAssignmentSession();

            AdvanceSeconds(session, 10);

            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual("carrier", session.GetUnit("fighter").CarrierID);
        }

        [Test]
        public void Advance_TerminalAssignmentUnsortedCarriers_UsesRegistrationOrder()
        {
            TacticalCombatSession session = CreateSurvivorAssignmentSession(secondCarrier: true);

            AdvanceSeconds(session, 10);

            Assert.AreEqual("second-carrier", session.GetUnit("fighter").CarrierID);
        }

        [Test]
        public void Advance_TerminalAssignmentNoCapitalCapacity_FallsBackToStation()
        {
            TacticalCombatSession session = CreateSurvivorAssignmentSession(carrierCapacity: 0);

            AdvanceSeconds(session, 10);

            Assert.AreEqual("station", session.GetUnit("fighter").CarrierID);
        }

        [Test]
        public void Advance_TerminalAssignmentFactionZero_DoesNotUseStationFallback()
        {
            TacticalCombatSession session = CreateSurvivorAssignmentSession(
                carrierCapacity: 0,
                fighterFaction: 0
            );

            AdvanceSeconds(session, 10);

            Assert.IsNull(session.GetUnit("fighter").CarrierID);
        }

        [Test]
        public void Advance_TerminalAssignmentUnavailableStation_DoesNotUseStationFallback()
        {
            TacticalCombatSession session = CreateSurvivorAssignmentSession(carrierCapacity: 0);
            session.BattleStation.Unit.Systems.Destroy();

            AdvanceSeconds(session, 10);

            Assert.IsNull(session.GetUnit("fighter").CarrierID);
        }

        [Test]
        public void Advance_TerminalAssignmentDepartedStation_RetainsStationFallback()
        {
            TacticalCombatSession session = CreateSurvivorAssignmentSession(carrierCapacity: 0);
            Assert.IsTrue(session.BattleStation.TryWithdraw());
            session.BattleStation.Unit.Systems.Destroy();

            AdvanceSeconds(session, 10);

            Assert.AreEqual("station", session.GetUnit("fighter").CarrierID);
        }

        [TestCase(TacticalCombatUnitStatus.Docked, true)]
        [TestCase(TacticalCombatUnitStatus.UnderConstruction, false)]
        [TestCase(TacticalCombatUnitStatus.Active, true)]
        [TestCase(TacticalCombatUnitStatus.Recovered, true)]
        [TestCase(TacticalCombatUnitStatus.PreparingHyperspace, true)]
        [TestCase(TacticalCombatUnitStatus.EnteringHyperspace, false)]
        [TestCase(TacticalCombatUnitStatus.AwaitingWithdrawal, false)]
        [TestCase(TacticalCombatUnitStatus.Withdrawn, true)]
        [TestCase(TacticalCombatUnitStatus.Destroyed, false)]
        [TestCase(TacticalCombatUnitStatus.AttackingBattleStation, false)]
        public void Advance_TerminalAssignmentFighterParticipation_UsesRetainedState(
            TacticalCombatUnitStatus status,
            bool attached
        )
        {
            TacticalCombatSession session = CreateSurvivorAssignmentSession();
            session.GetUnit("fighter").Status = status;

            AdvanceSeconds(session, 10);

            Assert.AreEqual(attached ? "carrier" : null, session.GetUnit("fighter").CarrierID);
        }

        [TestCase("fighter")]
        [TestCase("carrier")]
        public void Advance_TerminalAssignmentZeroHull_DoesNotReplaceParticipationGate(string id)
        {
            TacticalCombatSession session = CreateSurvivorAssignmentSession();
            session.GetUnit(id).Systems.Destroy();

            AdvanceSeconds(session, 10);

            Assert.AreEqual("carrier", session.GetUnit("fighter").CarrierID);
        }

        [Test]
        public void CreateResult_RetainedZeroHullFighter_ExportsFinalCarrier()
        {
            TacticalCombatSession session = CreateSurvivorAssignmentSession();
            TacticalCombatUnit fighter = session.GetUnit("fighter");
            fighter.Systems.Destroy();
            AdvanceSeconds(session, 10);

            var result = session.CreateResult();

            Assert.AreSame(
                session.GetUnit("carrier").Source,
                result.FighterRecovery.Find(recovery => recovery.Fighter == fighter.Source)?.Carrier
            );
        }

        [Test]
        public void CreateResult_DestroyedFighter_ExportsNoCarrier()
        {
            TacticalCombatSession session = CreateSurvivorAssignmentSession();
            AdvanceSeconds(session, 10);
            TacticalCombatUnit fighter = session.GetUnit("fighter");
            fighter.Status = TacticalCombatUnitStatus.Destroyed;

            var result = session.CreateResult();

            Assert.IsNull(
                result.FighterRecovery.Find(recovery => recovery.Fighter == fighter.Source)?.Carrier
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CreateResult_RetainedZeroHullDeparture_ExportsDeparture(bool fighter)
        {
            TacticalCombatUnit departed = fighter
                ? new TacticalCombatUnit(
                    new Starfighter
                    {
                        InstanceID = "departed",
                        MaxSquadronSize = 12,
                        CurrentSquadronSize = 12,
                        Hyperdrive = 1,
                    },
                    CombatSide.Attacker,
                    1
                )
                : new TacticalCombatUnit(CreateShip("departed", 100, 0), CombatSide.Attacker, 1);
            TacticalCombatUnit enemy = new(
                CreateShip("enemy", 100, 0),
                CombatSide.Defender,
                1000001
            );
            TacticalCombatSession session = CreateEncounter(
                departed,
                enemy,
                null,
                CombatSide.Attacker
            );
            departed.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            departed.HyperspaceMilliseconds = 3999;
            AdvanceSeconds(session, 20);
            Assert.IsTrue(session.IsComplete);
            Assert.IsTrue(session.HasDeparted(departed));
            departed.Systems.RestorePools(0, 0);

            var result = session.CreateResult();

            CollectionAssert.Contains(result.WithdrawnUnits, departed.Source);
        }

        [TestCase(TacticalCombatUnitStatus.Active, false)]
        [TestCase(TacticalCombatUnitStatus.PreparingHyperspace, false)]
        [TestCase(TacticalCombatUnitStatus.EnteringHyperspace, false)]
        [TestCase(TacticalCombatUnitStatus.AwaitingWithdrawal, false)]
        [TestCase(TacticalCombatUnitStatus.Withdrawn, true)]
        [TestCase(TacticalCombatUnitStatus.Destroyed, true)]
        public void CanWithdraw_OpposingGravityWellParticipation_UsesRetainedCapitalChain(
            TacticalCombatUnitStatus status,
            bool expected
        )
        {
            CapitalShip interdictor = CreateShip("b", 100, 0);
            interdictor.HasGravityWell = true;
            TacticalCombatSession session = CreateSession(CreateShip("a", 100, 0), interdictor);
            session.GetUnit("b").Status = status;

            Assert.AreEqual(expected, session.CanWithdraw(CombatSide.Attacker));
        }

        [Test]
        public void CanWithdraw_ZeroHullOpposingGravityWell_RetainsCapitalChainBlock()
        {
            CapitalShip interdictor = CreateShip("b", 100, 0);
            interdictor.HasGravityWell = true;
            TacticalCombatSession session = CreateSession(CreateShip("a", 100, 0), interdictor);
            session.GetUnit("b").Systems.Destroy();

            Assert.IsFalse(session.CanWithdraw(CombatSide.Attacker));
        }

        [Test]
        public void CanWithdraw_UnorderedMovementDelay_PreservesAvailableMovementBudget()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            session.GetUnit("a").Systems.ApplyDisruption(0, float.NaN);

            Assert.IsTrue(session.CanWithdraw(CombatSide.Attacker));
        }

        [Test]
        public void CanWithdraw_IncompleteZeroHullStation_AllowsEmpireDeparture()
        {
            CapitalShip station = CreateShip("station", 100, 0);
            station.CanDestroyPlanets = true;
            station.CurrentHullStrength = 0;
            station.ManufacturingStatus = ManufacturingStatus.Building;
            TacticalCombatSession session = CreateSession(CreateShip("a", 100, 0), station);

            Assert.IsTrue(session.CanWithdraw(CombatSide.Defender));
        }

        [TestCase(CombatSide.Attacker, 143)]
        [TestCase(CombatSide.Defender, 269)]
        public void BeginWithdrawal_PlayerSide_QueuesSelectedFactionWithdrawalCue(
            CombatSide side,
            int messageID
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0),
                NotificationBank(143, 269),
                side
            );
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            Assert.IsTrue(session.BeginWithdrawal(side));
            CollectionAssert.AreEqual(new[] { messageID.ToString() }, output);
            Assert.AreEqual(
                TacticalCombatWithdrawalPhase.Waiting,
                session.GetSide(side).WithdrawalPhase
            );
        }

        [Test]
        public void BeginWithdrawal_OpposingSide_DoesNotQueuePlayerWithdrawalCue()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0),
                NotificationBank(143, 269)
            );
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            Assert.IsTrue(session.BeginWithdrawal(CombatSide.Defender));
            Assert.IsEmpty(output);
        }

        [Test]
        public void Advance_DestroyedTractorContributor_RetainsLinkUntilQueuedUnlock()
        {
            CapitalShip source = CreateShip("a", 100, 1);
            source.MaxShieldStrength = 10000;
            source.TractorBeamPower = 10;
            source.TractorBeamnRange = 1000;
            TacticalCombatSession session = CreateSession(source, CreateShip("b", 1000, 1000));
            string targetAtDestruction = null;
            session.UnitDestroyed += unit =>
            {
                if (unit.InstanceID == "a")
                    targetAtDestruction = unit.TractorTargetID;
            };

            AdvanceSeconds(session, 120);

            Assert.AreEqual("b", targetAtDestruction);
        }

        [Test]
        public void PreparePlayerWithdrawal_UnreadyCapital_QueuesWarningWithoutStartingDeparture()
        {
            TacticalCombatSession session = CreateMembershipSession(NotificationBank(149));
            session
                .GetUnit("first")
                .Systems.ApplyComponentEvent(TacticalShipSystem.Hyperdrive, false);
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            Assert.IsTrue(session.PreparePlayerWithdrawal());
            CollectionAssert.AreEqual(new[] { "149" }, output);
            Assert.IsFalse(session.GetSide(session.PlayerSide).IsWithdrawing);
        }

        [Test]
        public void PreparePlayerWithdrawal_UnreadyFighter_DoesNotQueueCapitalWarning()
        {
            TacticalCombatSession session = CreateMembershipSession(NotificationBank(149));
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            Assert.IsTrue(session.PreparePlayerWithdrawal());
            Assert.IsEmpty(output);
        }

        [Test]
        public void PreparePlayerWithdrawal_GravityBlocked_ReturnsFalseWithoutWarning()
        {
            CapitalShip interdictor = CreateShip("b", 100, 0);
            interdictor.HasGravityWell = true;
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                interdictor,
                NotificationBank(149)
            );
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };

            Assert.IsFalse(session.PreparePlayerWithdrawal());
            Assert.IsEmpty(output);
        }

        [Test]
        public void ConfirmPlayerWithdrawal_EligiblePlayer_QueuesDepartureUntilInputDrains()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );

            Assert.IsTrue(session.ConfirmPlayerWithdrawal());
            Assert.IsFalse(session.GetSide(session.PlayerSide).IsWithdrawing);
            session.Selection.ProcessPendingCommands();
            Assert.IsTrue(session.GetSide(session.PlayerSide).IsWithdrawing);
        }

        [Test]
        public void ConfirmPlayerWithdrawal_GravityBecomesBlocking_RejectsConfirmation()
        {
            CapitalShip interdictor = CreateShip("b", 100, 0);
            interdictor.HasGravityWell = true;
            TacticalCombatSession session = CreateSession(CreateShip("a", 100, 0), interdictor);
            session.GetUnit("b").Status = TacticalCombatUnitStatus.Withdrawn;
            Assert.IsTrue(session.PreparePlayerWithdrawal());
            session.GetUnit("b").Status = TacticalCombatUnitStatus.Active;

            Assert.IsFalse(session.ConfirmPlayerWithdrawal());
            session.Selection.ProcessPendingCommands();
            Assert.IsFalse(session.GetSide(session.PlayerSide).IsWithdrawing);
        }

        [Test]
        public void ConfirmPlayerWithdrawal_GravityBlocksBeforeDispatch_RejectsQueuedDeparture()
        {
            CapitalShip interdictor = CreateShip("b", 100, 0);
            interdictor.HasGravityWell = true;
            TacticalCombatSession session = CreateSession(CreateShip("a", 100, 0), interdictor);
            session.GetUnit("b").Status = TacticalCombatUnitStatus.Withdrawn;
            Assert.IsTrue(session.ConfirmPlayerWithdrawal());
            session.GetUnit("b").Status = TacticalCombatUnitStatus.Active;

            session.Selection.ProcessPendingCommands();

            Assert.IsFalse(session.GetSide(session.PlayerSide).IsWithdrawing);
        }

        [Test]
        public void BeginWithdrawal_GroupedCapitals_RequestsSeparateDepartureGroups()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatUnit second = session.GetUnit("second");
            TacticalCombatGroup previous = session.CreateGroup(new[] { first, second });
            AdvanceSeconds(session, 5);

            Assert.IsTrue(session.BeginWithdrawal(CombatSide.Attacker));

            TacticalCombatGroup firstDeparture = session.GetRequestedGroup(first);
            TacticalCombatGroup secondDeparture = session.GetRequestedGroup(second);
            Assert.AreNotSame(firstDeparture, secondDeparture);
            CollectionAssert.AreEqual(new[] { first }, firstDeparture.RequestedMembers);
            CollectionAssert.AreEqual(new[] { second }, secondDeparture.RequestedMembers);
            Assert.AreSame(previous, session.GetGroup(first));
            Assert.AreSame(previous, session.GetGroup(second));
        }

        [Test]
        public void BeginWithdrawal_PlayerCapitalCategory_ClearsRequestedCategoryOwnership()
        {
            TacticalCombatSession session = CreateMembershipSession();
            Assert.IsNotNull(session.GetObjectGroup(session.PlayerSide, 0));

            Assert.IsTrue(session.BeginWithdrawal(session.PlayerSide));

            Assert.IsNull(session.GetObjectGroup(session.PlayerSide, 0));
        }

        [Test]
        public void BeginWithdrawal_ReadyFighterGroup_ClearsPlayerFighterCategory()
        {
            TacticalCombatSession session = CreateMembershipSession(
                fighterHyperdrive: true,
                fighterCategory: 0
            );
            Assert.IsNotNull(session.GetFighterGroup(session.PlayerSide, 0));

            Assert.IsTrue(session.BeginWithdrawal(session.PlayerSide));

            Assert.IsNull(session.GetFighterGroup(session.PlayerSide, 0));
        }

        [Test]
        public void BeginWithdrawal_PendingCapitalRegrouping_PreservesLatestDepartureOwnerAtDispatch()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit unit = session.GetUnit("first");
            session.CreateGroup(new[] { unit });
            Assert.IsTrue(session.BeginWithdrawal(session.PlayerSide));
            TacticalCombatGroup departure = session.GetRequestedGroup(unit);

            AdvanceSeconds(session, 5);

            Assert.AreSame(departure, session.GetGroup(unit));
            CollectionAssert.AreEqual(new[] { unit }, departure.Members);
            Assert.AreEqual(TacticalCombatOrder.Withdraw, departure.ActiveOrder);
        }

        [TestCase(true, false, TacticalCombatOrder.Withdraw)]
        [TestCase(true, true, TacticalCombatOrder.Withdraw)]
        [TestCase(false, false, TacticalCombatOrder.Withdraw)]
        [TestCase(false, true, TacticalCombatOrder.Recover)]
        public void SetOrder_ProtectedRequestedOrder_UsesLiveFactionControlPolicy(
            bool playerOwnsGroup,
            bool automated,
            TacticalCombatOrder expected
        )
        {
            TacticalCombatSession session = CreateMembershipSession(
                player: playerOwnsGroup ? CombatSide.Attacker : CombatSide.Defender
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("fighter"));
            session.SetOrder(group, TacticalCombatOrder.Recover);
            session.SetAutomated(CombatSide.Attacker, automated);

            session.SetOrder(group, TacticalCombatOrder.Withdraw);

            Assert.AreEqual(expected, group.Order);
        }

        [Test]
        public void SetOrder_ProtectedAppliedOrder_RejectsAutomatedOpponentNonzeroChange()
        {
            TacticalCombatSession session = CreateMembershipSession(player: CombatSide.Defender);
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("fighter"));
            session.SetOrder(group, TacticalCombatOrder.Recover);
            for (
                int frame = 0;
                frame < 100 && group.ActiveOrder != TacticalCombatOrder.Recover;
                frame++
            )
                session.Advance(50);
            Assert.AreEqual(TacticalCombatOrder.Recover, group.ActiveOrder);
            session.SetOrder(group, TacticalCombatOrder.None);
            session.SetAutomated(CombatSide.Attacker, true);

            session.SetOrder(group, TacticalCombatOrder.Withdraw);

            Assert.AreEqual(TacticalCombatOrder.None, group.Order);
        }

        [Test]
        public void SetOrder_ProtectedAutomatedOpponent_AllowsClearOrder()
        {
            TacticalCombatSession session = CreateMembershipSession(player: CombatSide.Defender);
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("fighter"));
            session.SetOrder(group, TacticalCombatOrder.Recover);
            session.SetAutomated(CombatSide.Attacker, true);

            session.SetOrder(group, TacticalCombatOrder.None);

            Assert.AreEqual(TacticalCombatOrder.None, group.Order);
        }

        [Test]
        public void ActiveAttackRun_CancelledAutomatedOpponent_UsesSharedOrderRequestGate()
        {
            TacticalCombatSession session = CreateAttackSession(player: CombatSide.Defender);
            session.SetAutomated(CombatSide.Attacker, true);

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Cancelled);

            Assert.AreEqual(
                TacticalCombatOrder.AttackBattleStation,
                session.ActiveAttackRun.Group.Order
            );
        }

        [Test]
        public void BeginWithdrawal_RecoveringAutomatedOpponent_RetainsProtectedFighterOrder()
        {
            TacticalCombatSession session = CreateMembershipSession(
                fighterHyperdrive: true,
                player: CombatSide.Defender
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("fighter"));
            session.SetOrder(group, TacticalCombatOrder.Recover);
            session.SetAutomated(CombatSide.Attacker, true);

            Assert.IsTrue(session.BeginWithdrawal(CombatSide.Attacker));

            Assert.AreEqual(TacticalCombatOrder.Recover, group.Order);
        }

        [Test]
        public void Advance_StationWithdrawalCrossing_ClosesStationControlsBeforeChargeCue()
        {
            CapitalShip station = CreateShip("station", 1000, 0);
            station.CanDestroyPlanets = true;
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                station,
                NotificationBank(275),
                CombatSide.Defender
            );
            TacticalCombatSide side = session.GetSide(CombatSide.Defender);
            Assert.IsTrue(session.BeginWithdrawal(side.Side));
            for (int frame = 0; frame < 4000 && side.WithdrawalMilliseconds > 1000; frame++)
                session.Advance(50);
            Assert.AreEqual(1000, side.WithdrawalMilliseconds);
            session.BattleStation.Fire();
            session.BattleStation.Advance(299000);
            List<string> effects = new();
            session.Selection.StationControlsClosed += () => effects.Add("close");
            session.Audio.Playback = (_, _) =>
            {
                effects.Add("charge");
                return null;
            };

            for (
                int frame = 0;
                frame < 100 && side.WithdrawalPhase == TacticalCombatWithdrawalPhase.Waiting;
                frame++
            )
                session.Advance(50);

            CollectionAssert.AreEqual(new[] { "close", "charge" }, effects);
        }

        /// <summary>
        /// Creates explicit shared notification records for observing a public encounter transition.
        /// </summary>
        /// <param name="ids">The notification message IDs to author.</param>
        /// <returns>The addressed media bank.</returns>
        private static TacticalCombatDefinition NotificationBank(params int[] ids)
        {
            TacticalCombatDefinition definition = new();
            foreach (int id in ids)
                definition.Notifications.Add(
                    new()
                    {
                        MessageID = id,
                        Variants = new() { new() { Path = id.ToString() } },
                    }
                );
            return definition;
        }

        /// <summary>
        /// Captures one designated weapon lane with a passive capital target.
        /// </summary>
        /// <param name="weapon">The queued discharge category to observe.</param>
        /// <returns>The targeted encounter with no automated orders.</returns>
        private static TacticalCombatSession CreateDischargeSession(TacticalWeaponType weapon)
        {
            if (weapon == TacticalWeaponType.Torpedo)
            {
                TacticalCombatUnit source = new(
                    new Starfighter
                    {
                        InstanceID = "a",
                        MaxSquadronSize = 12,
                        CurrentSquadronSize = 12,
                        Hyperdrive = 1,
                        LaserCannon = 5,
                        LaserRange = 100,
                        Torpedoes = 3,
                        TorpedoRange = 100,
                        SublightSpeed = 4,
                        Agility = 1,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    CombatSide.Attacker,
                    0
                )
                {
                    Position = new Vector3(0, 0, -40),
                };
                TacticalCombatUnit target = new(
                    CreateShip("b", 1000, 0),
                    CombatSide.Defender,
                    1000000
                )
                {
                    Position = new Vector3(0, 0, 40),
                    Heading = -Vector3.UnitZ,
                    DesiredHeading = -Vector3.UnitZ,
                };
                return CreateEncounter(source, target, null, CombatSide.Attacker);
            }
            CapitalShip ship = CreateShip("a", 1000, 0);
            ship.WeaponRecharge = 10;
            PrimaryWeaponType lane = weapon switch
            {
                TacticalWeaponType.LaserCannon => PrimaryWeaponType.LaserCannon,
                TacticalWeaponType.IonCannon => PrimaryWeaponType.IonCannon,
                _ => PrimaryWeaponType.Turbolaser,
            };
            ship.PrimaryWeapons[lane] = new[] { 10, 10, 10, 10, 100 };
            return CreateSession(ship, CreateShip("b", 1000, 0));
        }

        /// <summary>
        /// Advances until a battery discharges while its animation command remains queued.
        /// </summary>
        /// <param name="session">The encounter with one armed firing unit.</param>
        private static void WaitForQueuedDischarge(TacticalCombatSession session)
        {
            TacticalCombatUnit unit = session.GetUnit("a");
            for (int frame = 0; frame < 1200; frame++)
            {
                session.Advance(1000f / 60);
                for (int arc = 0; arc < 4; arc++)
                    if (
                        unit.Weapons.GetCharge((TacticalWeaponArc)arc)
                        < unit.Weapons.GetMaximumCharge((TacticalWeaponArc)arc)
                    )
                        return;
            }
            Assert.Fail("The designated battery did not queue a discharge.");
        }

        /// <summary>
        /// Creates a terminal encounter with an early station and a retained fighter for carrier assignment.
        /// </summary>
        /// <param name="carrierCapacity">The ordinary carrier's configured berth count.</param>
        /// <param name="fighterFaction">The fighter side's explicit faction index.</param>
        /// <param name="secondCarrier">Whether a lower-index ordinary carrier follows the first in input order.</param>
        /// <returns>The unadvanced encounter with the opposing force already destroyed.</returns>
        private static TacticalCombatSession CreateSurvivorAssignmentSession(
            int carrierCapacity = 1,
            int fighterFaction = 1,
            bool secondCarrier = false
        )
        {
            CapitalShip stationSource = CreateShip("station", 100, 0);
            stationSource.CanDestroyPlanets = true;
            stationSource.StarfighterCapacity = 2;
            TacticalCombatUnit station = new(stationSource, CombatSide.Defender, 1000000);
            CapitalShip carrierSource = CreateShip("carrier", 100, 0);
            carrierSource.StarfighterCapacity = carrierCapacity;
            TacticalCombatUnit carrier = new(carrierSource, CombatSide.Defender, 1000002);
            TacticalCombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    Hyperdrive = 1,
                },
                CombatSide.Defender,
                1000003
            );
            TacticalCombatUnit enemy = new(CreateShip("enemy", 100, 0), CombatSide.Attacker, 0)
            {
                Status = TacticalCombatUnitStatus.Destroyed,
            };
            List<TacticalCombatUnit> units = new() { station, carrier, fighter, enemy };
            if (secondCarrier)
            {
                CapitalShip secondSource = CreateShip("second-carrier", 100, 0);
                secondSource.StarfighterCapacity = 1;
                units.Add(new TacticalCombatUnit(secondSource, CombatSide.Defender, 1000001));
            }
            return new TacticalCombatSession(
                units,
                Array.Empty<TacticalCombatGroup>(),
                CreateSide(CombatSide.Attacker, 1 - fighterFaction, Vector3.UnitZ, true),
                CreateSide(CombatSide.Defender, fighterFaction, -Vector3.UnitZ, true),
                CombatSide.Defender,
                100,
                new SystemRandomProvider(42),
                presentationClock: () => 0
            );
        }

        /// <summary>
        /// Completes an unarmed encounter around a retained participant for final-pool export checks.
        /// </summary>
        /// <param name="fighter">Whether the retained participant is a fighter squadron.</param>
        /// <param name="station">Whether the retained capital is a battle station.</param>
        /// <returns>The completed encounter whose retained unit has identity exported.</returns>
        private static TacticalCombatSession CreateCompletedExportSession(
            bool fighter = false,
            bool station = false
        )
        {
            TacticalCombatUnit first;
            if (fighter)
                first = new TacticalCombatUnit(
                    new Starfighter
                    {
                        InstanceID = "exported",
                        MaxSquadronSize = 12,
                        CurrentSquadronSize = 12,
                    },
                    CombatSide.Attacker,
                    1
                );
            else
            {
                CapitalShip ship = CreateShip("exported", 100, 0);
                ship.CanDestroyPlanets = station;
                first = new TacticalCombatUnit(ship, CombatSide.Attacker, 1);
            }
            first.Status = TacticalCombatUnitStatus.Withdrawn;
            TacticalCombatUnit second = new(
                CreateShip("enemy", 100, 0),
                CombatSide.Defender,
                1000001
            )
            {
                Status = TacticalCombatUnitStatus.Destroyed,
            };
            TacticalCombatSession session = CreateEncounter(
                first,
                second,
                null,
                CombatSide.Attacker
            );
            AdvanceSeconds(session, 10);
            Assert.IsTrue(session.IsComplete);
            return session;
        }

        /// <summary>
        /// Creates a completed armed or unarmed mobile capital ship.
        /// </summary>
        /// <param name="id">The campaign instance identifier.</param>
        /// <param name="hull">The initial hull strength.</param>
        /// <param name="strength">The forward turbolaser strength.</param>
        /// <returns>The campaign ship.</returns>
        private static CapitalShip CreateShip(string id, int hull, int strength)
        {
            CapitalShip ship = new()
            {
                InstanceID = id,
                MaxHullStrength = hull,
                CurrentHullStrength = hull,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Hyperdrive = 1,
                SublightSpeed = 4,
                Maneuverability = 1,
                WeaponRecharge = strength,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[]
            {
                strength,
                strength,
                strength,
                strength,
                100,
            };
            return ship;
        }

        /// <summary>
        /// Creates two opposing groups that target each other without tactical automation.
        /// </summary>
        /// <param name="attacker">The attacking campaign ship.</param>
        /// <param name="defender">The defending campaign ship.</param>
        /// <param name="definition">The optional authored notification bank.</param>
        /// <param name="player">The player-controlled side.</param>
        /// <param name="personnelHostInstanceIDs">The captured host identity for each available embarked officer.</param>
        /// <param name="enteringSide">The explicitly captured entering force, when arrival is exercised.</param>
        /// <returns>The isolated tactical encounter.</returns>
        private static TacticalCombatSession CreateSession(
            CapitalShip attacker,
            CapitalShip defender,
            TacticalCombatDefinition definition = null,
            CombatSide player = CombatSide.Attacker,
            IReadOnlyList<string> personnelHostInstanceIDs = null,
            CombatSide? enteringSide = null
        )
        {
            TacticalCombatUnit first = new(
                attacker,
                CombatSide.Attacker,
                0,
                new TacticalCombatUnitDefinition { PresentationIndex = 1 }
            )
            {
                Position = new Vector3(0, 0, -40),
            };
            TacticalCombatUnit second = new(
                defender,
                CombatSide.Defender,
                1000000,
                new TacticalCombatUnitDefinition { PresentationIndex = 2 }
            )
            {
                Position = new Vector3(0, 0, 40),
                Heading = -Vector3.UnitZ,
                DesiredHeading = -Vector3.UnitZ,
            };
            return CreateEncounter(
                first,
                second,
                definition,
                player,
                personnelHostInstanceIDs,
                enteringSide
            );
        }

        /// <summary>
        /// Creates two opposing groups around captured units and a controlled replay clock.
        /// </summary>
        /// <param name="first">The attacking captured unit.</param>
        /// <param name="second">The defending captured unit.</param>
        /// <param name="definition">The optional authored notification bank.</param>
        /// <param name="player">The player-controlled side.</param>
        /// <param name="personnelHostInstanceIDs">The captured host identity for each available embarked officer.</param>
        /// <param name="enteringSide">The explicitly captured entering force, when arrival is exercised.</param>
        /// <returns>The isolated tactical encounter.</returns>
        private static TacticalCombatSession CreateEncounter(
            TacticalCombatUnit first,
            TacticalCombatUnit second,
            TacticalCombatDefinition definition,
            CombatSide player,
            IReadOnlyList<string> personnelHostInstanceIDs = null,
            CombatSide? enteringSide = null
        )
        {
            PrepareBatteries(first);
            PrepareBatteries(second);
            TacticalCombatGroup firstGroup = new(1, new[] { first }, Vector3.UnitZ);
            TacticalCombatGroup secondGroup = new(2, new[] { second }, -Vector3.UnitZ);
            firstGroup.SetTarget(second);
            secondGroup.SetTarget(first);
            return new TacticalCombatSession(
                new[] { first, second },
                new[] { firstGroup, secondGroup },
                new TacticalCombatSide(
                    CombatSide.Attacker,
                    0,
                    Vector3.UnitZ,
                    true,
                    0,
                    0,
                    null,
                    null,
                    0,
                    0,
                    false
                ),
                new TacticalCombatSide(
                    CombatSide.Defender,
                    1,
                    -Vector3.UnitZ,
                    true,
                    0,
                    0,
                    null,
                    null,
                    0,
                    0,
                    false
                ),
                player,
                100,
                new SystemRandomProvider(42),
                definition,
                presentationClock: () => 0,
                personnelHostInstanceIDs: personnelHostInstanceIDs,
                enteringSide: enteringSide
            );
        }

        /// <summary>
        /// Creates ordered friendly capital and fighter members with an unarmed opposing group.
        /// </summary>
        /// <param name="definition">The optional shared notification bank.</param>
        /// <param name="fighterHyperdrive">Whether the deployed fighter can leave independently.</param>
        /// <param name="fighterCategory">The deployed fighter's initial retained category.</param>
        /// <param name="player">The faction side controlled by the player.</param>
        /// <returns>The encounter used to observe membership command transitions.</returns>
        private static TacticalCombatSession CreateMembershipSession(
            TacticalCombatDefinition definition = null,
            bool fighterHyperdrive = false,
            int fighterCategory = 4,
            CombatSide player = CombatSide.Attacker
        )
        {
            TacticalCombatUnit first = new(CreateShip("first", 100, 0), CombatSide.Attacker, 8);
            TacticalCombatUnit second = new(CreateShip("second", 100, 0), CombatSide.Attacker, 2);
            TacticalCombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    Hyperdrive = fighterHyperdrive ? 1 : 0,
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                },
                CombatSide.Attacker,
                5
            );
            TacticalCombatUnit enemy = new(
                CreateShip("enemy", 100, 0),
                CombatSide.Defender,
                1000001
            );
            return new TacticalCombatSession(
                new[] { first, second, fighter, enemy },
                new[]
                {
                    new TacticalCombatGroup(10, new[] { first }, Vector3.UnitZ),
                    new TacticalCombatGroup(11, new[] { second }, Vector3.UnitZ),
                    new TacticalCombatGroup(12, new[] { fighter }, Vector3.UnitZ)
                    {
                        FighterCategory = fighterCategory,
                    },
                    new TacticalCombatGroup(1000010, new[] { enemy }, -Vector3.UnitZ),
                },
                CreateSide(CombatSide.Attacker, 0, Vector3.UnitZ, hasWithdrawalDestination: true),
                CreateSide(CombatSide.Defender, 1, -Vector3.UnitZ, hasWithdrawalDestination: true),
                player,
                100,
                new SystemRandomProvider(42),
                definition
            );
        }

        /// <summary>
        /// Advances at sixty presentation frames per second for a bounded battle duration.
        /// </summary>
        /// <param name="session">The encounter to advance.</param>
        /// <param name="seconds">The elapsed battle duration.</param>
        private static void AdvanceSeconds(TacticalCombatSession session, int seconds)
        {
            for (int frame = 0; frame < seconds * 60 && !session.IsComplete; frame++)
                session.Advance(1000f / 60);
        }

        /// <summary>
        /// Establishes reset-and-recharged batteries for scenarios that exercise queued firing.
        /// </summary>
        /// <param name="unit">The participant whose firing precondition is required.</param>
        private static void PrepareBatteries(TacticalCombatUnit unit)
        {
            for (int arc = 0; arc < 4; arc++)
                unit.Weapons.ClearBattery((TacticalWeaponArc)arc);
            unit.Weapons.Advance(1, 1);
        }

        private sealed class ArrivalPresentation : ITacticalCombatArrivalPresentation
        {
            public readonly List<int> ResetIndices = new();
            public bool Strobe;

            /// <summary>
            /// Records the requested arrival indicator state.
            /// </summary>
            /// <param name="active">Whether the indicator is active.</param>
            public void SetPauseStrobe(bool active) => Strobe = active;

            /// <summary>
            /// Records each unit whose arrival presentation is reset.
            /// </summary>
            /// <param name="unitIndex">The registered unit index.</param>
            public void Reset(int unitIndex) => ResetIndices.Add(unitIndex);

            /// <summary>
            /// Accepts an arrival pose without creating scene objects.
            /// </summary>
            /// <param name="unitIndex">The registered unit index.</param>
            /// <param name="position">The requested presentation position.</param>
            /// <param name="direction">The requested presentation direction.</param>
            public void SetTransform(int unitIndex, Vector4 position, Vector4 direction) { }

            /// <summary>
            /// Accepts a presentation refresh without creating scene objects.
            /// </summary>
            /// <param name="unitIndex">The registered unit index.</param>
            public void Refresh(int unitIndex) { }
        }

        /// <summary>
        /// Selects a repeatable offset within every requested random interval.
        /// </summary>
        private sealed class FixedRandom : IRandomNumberProvider
        {
            private readonly int _offset;

            /// <summary>
            /// Captures the deterministic interval offset.
            /// </summary>
            /// <param name="offset">The nonnegative offset reduced to the requested interval.</param>
            public FixedRandom(int offset) => _offset = offset;

            /// <summary>
            /// Supplies a deterministic fractional draw when requested.
            /// </summary>
            /// <returns>The fixed fractional value zero.</returns>
            public double NextDouble() => 0;

            /// <summary>
            /// Selects the fixed offset within the requested half-open interval.
            /// </summary>
            /// <param name="min">The inclusive lower bound.</param>
            /// <param name="max">The exclusive upper bound.</param>
            /// <returns>The selected value within the requested interval.</returns>
            public int NextInt(int min, int max) => min + _offset % (max - min);
        }

        /// <summary>
        /// Runs the explicit clock through its initial one-second progress intervals.
        /// </summary>
        /// <param name="session">The encounter that owns the scheduled operations.</param>
        /// <param name="clock">Its manually advanced millisecond provider.</param>
        private static void StartClockedSession(TacticalCombatSession session, SchedulerClock clock)
        {
            session.ConfigureClockedAdvance(clock.Read);
            session.AdvanceClocked(0);
            session.AdvanceClocked(0);
            for (int tick = 0; tick < 3; tick++)
            {
                clock.Advance(1000);
                session.AdvanceClocked(1);
                session.AdvanceClocked(0);
            }
        }

        private sealed class SchedulerClock
        {
            private uint _now;

            /// <summary>
            /// Starts at the explicitly provided millisecond epoch.
            /// </summary>
            /// <param name="now">The initial timestamp.</param>
            public SchedulerClock(uint now) => _now = now;

            /// <summary>
            /// Samples the controlled timestamp.
            /// </summary>
            /// <returns>The current wrapping timestamp.</returns>
            public uint Read() => _now;

            /// <summary>
            /// Advances time independently of the encounter's frame argument.
            /// </summary>
            /// <param name="elapsed">The unsigned elapsed milliseconds.</param>
            public void Advance(uint elapsed) => _now = unchecked(_now + elapsed);
        }

        /// <summary>
        /// Creates a registered station attack with one surviving fighter squadron.
        /// </summary>
        /// <param name="reserveFighter">Whether to deploy another eligible squadron in a separate group.</param>
        /// <param name="player">The faction side controlled by the player.</param>
        /// <returns>The battle after its attack run has entered the two-minute sequence.</returns>
        private static TacticalCombatSession CreateAttackSession(
            bool reserveFighter = false,
            CombatSide player = CombatSide.Attacker
        )
        {
            TacticalCombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    Agility = 20,
                    Torpedoes = 100,
                },
                CombatSide.Attacker,
                1
            );
            TacticalCombatUnit station = new(
                new CapitalShip
                {
                    InstanceID = "station",
                    CanDestroyPlanets = true,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaxHullStrength = 1000,
                    CurrentHullStrength = 1000,
                },
                CombatSide.Defender,
                1000001
            );
            TacticalCombatGroup group = new(2, new[] { fighter }, Vector3.UnitZ);
            List<TacticalCombatUnit> units = new() { fighter, station };
            List<TacticalCombatGroup> groups = new() { group };
            if (reserveFighter)
            {
                TacticalCombatUnit reserve = new(
                    new Starfighter
                    {
                        InstanceID = "reserve",
                        MaxSquadronSize = 12,
                        CurrentSquadronSize = 12,
                        Agility = 20,
                        Torpedoes = 100,
                    },
                    CombatSide.Attacker,
                    2
                );
                units.Add(reserve);
                groups.Add(new TacticalCombatGroup(3, new[] { reserve }, Vector3.UnitZ));
            }
            TacticalCombatSession session = new(
                units,
                groups,
                CreateSide(CombatSide.Attacker, 0, Vector3.UnitZ),
                CreateSide(CombatSide.Defender, 1, -Vector3.UnitZ),
                player,
                100,
                new SystemRandomProvider(42),
                new TacticalCombatDefinition
                {
                    AttackRunSuccessMoviePath = "Pack/Videos/success",
                    AttackRunFailureMoviePath = "Pack/Videos/failure",
                }
            );
            session.SetOrder(group, TacticalCombatOrder.AttackBattleStation);
            for (int index = 0; index < 100 && session.ActiveAttackRun == null; index++)
                session.Advance(50);
            Assert.IsNotNull(session.ActiveAttackRun);
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Starting);
            return session;
        }

        /// <summary>
        /// Captures an unautomated side without command officers and with an explicit retreat-route choice.
        /// </summary>
        /// <param name="side">The deployment side.</param>
        /// <param name="faction">The authored faction index.</param>
        /// <param name="forward">The deployment heading.</param>
        /// <param name="hasWithdrawalDestination">Whether the fixture captures a legal withdrawal route.</param>
        /// <returns>The side's command state.</returns>
        private static TacticalCombatSide CreateSide(
            CombatSide side,
            int faction,
            Vector3 forward,
            bool hasWithdrawalDestination = false
        )
        {
            return new TacticalCombatSide(
                side,
                faction,
                forward,
                hasWithdrawalDestination,
                0,
                0,
                null,
                null,
                0,
                0,
                false
            );
        }
    }
}
