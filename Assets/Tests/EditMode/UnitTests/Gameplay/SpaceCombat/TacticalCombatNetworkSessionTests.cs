using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed partial class TacticalCombatNetworkSessionTests
    {
        [Test]
        public void ApplyNetworkCommand_OpposingHuman_SelectsAndTargetsOnlyItsOwnForce()
        {
            TacticalCombatSession authority = CreateAuthority();

            Assert.IsTrue(authority.ApplyNetworkCommand(CombatSide.Defender, Object("alliance")));
            Assert.IsTrue(authority.ApplyNetworkCommand(CombatSide.Defender, Object("empire")));

            TacticalCombatGroup commanded = authority.GetRequestedGroup(
                authority.GetUnit("alliance")
            );
            Assert.AreSame(authority.GetUnit("empire"), commanded.RequestedTargets.Single());
            Assert.IsEmpty(
                authority.GetRequestedGroup(authority.GetUnit("empire")).RequestedTargets
            );
            Assert.AreEqual(TacticalCombatSelectionKind.Empty, authority.Selection.Kind);
        }

        [Test]
        public void ApplyNetworkCommand_EnemyInspection_CannotIssueEnemyOrders()
        {
            TacticalCombatSession authority = CreateAuthority();
            authority.ApplyNetworkCommand(CombatSide.Attacker, Object("alliance"));

            bool accepted = authority.ApplyNetworkCommand(
                CombatSide.Attacker,
                new()
                {
                    Kind = TacticalCombatNetworkCommandKind.Order,
                    Order = TacticalCombatOrder.HoldFire,
                }
            );

            Assert.IsFalse(accepted);
            Assert.AreEqual(
                TacticalCombatOrder.None,
                authority.GetGroup(authority.GetUnit("alliance")).Order
            );
        }

        [Test]
        public void ApplyNetworkCommand_InvalidIdentityAndSide_DoNotReplaceSelection()
        {
            TacticalCombatSession authority = CreateAuthority();

            Assert.IsFalse(authority.ApplyNetworkCommand(CombatSide.Draw, Object("empire")));
            Assert.IsFalse(authority.ApplyNetworkCommand(CombatSide.Attacker, Object("missing")));
            Assert.IsFalse(
                authority.ApplyNetworkCommand(
                    CombatSide.Attacker,
                    new() { Kind = TacticalCombatNetworkCommandKind.ObjectCategory, Category = 8 }
                )
            );

            Assert.AreEqual(TacticalCombatSelectionKind.Empty, authority.Selection.Kind);
        }

        [Test]
        public void QueueObject_Replica_RequestsAuthorityWithoutApplyingLocalSelection()
        {
            TacticalCombatSession replica = CreateReplica();
            List<TacticalCombatNetworkCommand> inputs = new();
            replica.NetworkCommandRequested += inputs.Add;

            replica.Selection.QueueObject(replica.GetUnit("alliance"));
            replica.AdvanceLocalSelection();

            Assert.AreEqual(TacticalCombatNetworkCommandKind.Object, inputs.Single().Kind);
            Assert.AreEqual("alliance", inputs[0].UnitID);
            Assert.AreEqual(TacticalCombatSelectionKind.Empty, replica.Selection.Kind);
            Assert.AreEqual(3, replica.SystemTime);
        }

        [Test]
        public void ApplyNetworkState_RemoteSelection_RestoresLocalFactionContextWithoutAdvancingRandom()
        {
            TacticalCombatSession authority = CreateAuthority();
            SystemRandomProvider random = new(12);
            TacticalCombatSession replica = CreateReplica(random);
            authority.ApplyNetworkCommand(CombatSide.Defender, Object("alliance"));
            long draws = random.CallCount;

            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(1));

            Assert.AreEqual(CombatSide.Defender, replica.PlayerSide);
            Assert.AreEqual(TacticalCombatSelectionKind.SingleObject, replica.Selection.Kind);
            Assert.AreSame(replica.GetUnit("alliance"), replica.Selection.PrimaryUnit);
            Assert.AreEqual(draws, random.CallCount);
            Assert.AreEqual(3, replica.SystemTime);
        }

        [Test]
        public void ApplyNetworkState_JsonRoundTrip_NormalizesAbsentOptionalIdentitiesAndRejectsInvalidReferences()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatNetworkFrame captured = authority.CaptureNetworkFrame(1);
            Assert.IsNull(captured.Units[0].CarrierID);
            Assert.IsNull(captured.Units[0].TargetID);
            string json = UnityEngine.JsonUtility.ToJson(captured);
            TacticalCombatNetworkFrame restored =
                UnityEngine.JsonUtility.FromJson<TacticalCombatNetworkFrame>(json);
            Assert.AreEqual(string.Empty, restored.Units[0].CarrierID);
            Assert.AreEqual(string.Empty, restored.Units[0].TargetID);

            ApplyCapturedProjection(replica, restored);

            Assert.IsNull(replica.GetUnit(restored.Units[0].UnitID).CarrierID);
            Assert.IsNull(replica.GetUnit(restored.Units[0].UnitID).TargetID);
            TacticalCombatNetworkFrame next =
                UnityEngine.JsonUtility.FromJson<TacticalCombatNetworkFrame>(
                    UnityEngine.JsonUtility.ToJson(authority.CaptureNetworkFrame(2))
                );
            next.Units[0].TargetID = "missing";
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkState(next));
            next.Units[0].TargetID = string.Empty;
            next.Groups[0].Members[0] = string.Empty;
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkState(next));
            Assert.AreEqual(captured.SystemTime, replica.SystemTime);
        }

        [Test]
        public void ApplyNetworkState_SameKindDifferentSelection_NotifiesContextReplacementOnce()
        {
            TacticalCombatSession authority = CreateAuthority(additionalDefender: true);
            TacticalCombatSession replica = CreateReplica(additionalDefender: true);
            int replacements = 0;
            replica.Selection.ContextReplaced += _ => replacements++;
            authority.ApplyNetworkCommand(CombatSide.Defender, Object("alliance"));
            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(1));
            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(2));
            Assert.AreEqual(1, replacements);
            authority.ApplyNetworkCommand(
                CombatSide.Defender,
                new() { Kind = TacticalCombatNetworkCommandKind.Clear }
            );
            authority.ApplyNetworkCommand(CombatSide.Defender, Object("alliance-wing"));

            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(3));

            Assert.AreEqual(2, replacements);
            Assert.AreEqual(TacticalCombatSelectionKind.SingleObject, replica.Selection.Kind);
            Assert.AreSame(replica.GetUnit("alliance-wing"), replica.Selection.PrimaryUnit);
        }

        [Test]
        public void ApplyNetworkState_QueuedGroupCreation_PreservesRequestedAndAppliedOwnership()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            authority.ApplyNetworkCommand(CombatSide.Defender, Object("alliance"));
            authority.ApplyNetworkCommand(CombatSide.Defender, Object("empire"));

            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(1));

            TacticalCombatUnit source = replica.GetUnit("alliance");
            Assert.AreNotSame(replica.GetGroup(source), replica.GetRequestedGroup(source));
            Assert.AreSame(
                replica.GetUnit("empire"),
                replica.GetRequestedGroup(source).RequestedTargets.Single()
            );
            Assert.IsFalse(replica.Groups.Contains(replica.GetRequestedGroup(source)));
        }

        [Test]
        public void ApplyNetworkState_ReplacedPendingGroup_RetainsEmptyIdentityUntilAuthoritativeDispatch()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            authority.ApplyNetworkCommand(CombatSide.Defender, Object("alliance"));
            authority.ApplyNetworkCommand(CombatSide.Defender, Object("empire"));
            authority.ApplyNetworkCommand(
                CombatSide.Defender,
                new()
                {
                    Kind = TacticalCombatNetworkCommandKind.Order,
                    Order = TacticalCombatOrder.HoldFire,
                }
            );
            TacticalCombatNetworkFrame state = authority.CaptureNetworkFrame(1);

            ApplyCapturedProjection(replica, state);

            Assert.IsTrue(
                state.Groups.Any(group =>
                    !group.Applied
                    && group.Members.Length == 0
                    && group.RequestedMembers.Length == 0
                )
            );
            Assert.AreEqual(
                TacticalCombatOrder.HoldFire,
                replica.GetRequestedGroup(replica.GetUnit("alliance")).Order
            );
        }

        [Test]
        public void Advance_Replica_RejectsLocalSimulationAndDirectMutations()
        {
            TacticalCombatSession replica = CreateReplica();
            Vector3 initial = replica.GetUnit("alliance").Position;

            Assert.Throws<InvalidOperationException>(() => replica.Advance(50));
            Assert.Throws<InvalidOperationException>(() =>
                replica.ConfigureClockedAdvance(() => 1)
            );
            Assert.Throws<InvalidOperationException>(() =>
                replica.CreateGroup(new[] { replica.GetUnit("alliance") })
            );
            Assert.Throws<InvalidOperationException>(() =>
                replica.SetTarget(
                    replica.GetGroup(replica.GetUnit("alliance")),
                    replica.GetUnit("empire")
                )
            );
            Assert.Throws<InvalidOperationException>(() =>
                replica.SetObjectGroup(CombatSide.Defender, 0, null)
            );

            Assert.AreEqual(initial, replica.GetUnit("alliance").Position);
            Assert.AreEqual(3, replica.SystemTime);
        }

        [Test]
        public void ApplyNetworkEffects_NaturalFire_ReplaysCapturedEffectsAndRestoresState()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            int authoritativeShots = 0;
            int projectedShots = 0;
            authority.ShotFired += _ => authoritativeShots++;
            replica.ShotFired += shot =>
            {
                projectedShots++;
                Assert.AreSame(replica.GetUnit(shot.Source.InstanceID), shot.Source);
                Assert.AreSame(replica.GetUnit(shot.Target.InstanceID), shot.Target);
            };
            authority.ApplyNetworkCommand(CombatSide.Attacker, Object("empire"));
            authority.ApplyNetworkCommand(CombatSide.Attacker, Object("alliance"));
            authority.ApplyNetworkCommand(CombatSide.Defender, Object("alliance"));
            authority.ApplyNetworkCommand(CombatSide.Defender, Object("empire"));
            for (int frame = 0; frame < 600; frame++)
                authority.Advance(50);
            TacticalCombatNetworkFrame state = authority.CaptureNetworkFrame(1);

            ApplyCapturedProjection(replica, state);

            Assert.Greater(authoritativeShots, 0);
            Assert.AreEqual(authoritativeShots, projectedShots);
            foreach (TacticalCombatUnit unit in authority.Units)
            {
                TacticalCombatUnit projected = replica.GetUnit(unit.InstanceID);
                Assert.AreEqual(unit.Position, projected.Position);
                Assert.AreEqual(unit.Systems.Hull, projected.Systems.Hull);
                Assert.AreEqual(unit.Systems.Shields, projected.Systems.Shields);
                Assert.AreEqual(
                    unit.Weapons.GetCharge(TacticalWeaponArc.Forward),
                    projected.Weapons.GetCharge(TacticalWeaponArc.Forward)
                );
            }
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkState(state));
            Assert.AreEqual(authoritativeShots, projectedShots);
            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(2));
            Assert.AreEqual(authoritativeShots, projectedShots);
        }

        [Test]
        public void ApplyNetworkState_UnknownGroupParticipant_RejectsBeforeMutatingPose()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatNetworkFrame state = authority.CaptureNetworkFrame(1);
            Vector3 initial = replica.GetUnit(state.Units[0].UnitID).Position;
            state.Units[0].Position = new(new Vector3(40, 40, 40));
            state.Groups[0].Members[0] = "missing";

            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkState(state));

            Assert.AreEqual(initial, replica.GetUnit(state.Units[0].UnitID).Position);
            Assert.AreEqual(3, replica.SystemTime);
        }

        [Test]
        public void ApplyNetworkCommand_Withdrawal_SuspendsOnlyItsAuthenticatedFaction()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            int guestWithdrawals = 0;
            replica.Selection.WithdrawalDispatched += () => guestWithdrawals++;

            Assert.IsTrue(
                authority.ApplyNetworkCommand(
                    CombatSide.Defender,
                    new() { Kind = TacticalCombatNetworkCommandKind.Withdrawal }
                )
            );
            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(1));

            Assert.IsTrue(authority.GetSide(CombatSide.Defender).IsWithdrawing);
            Assert.IsFalse(authority.GetSide(CombatSide.Attacker).IsWithdrawing);
            Assert.IsTrue(authority.IsDirectControlEnabled);
            Assert.IsFalse(replica.IsDirectControlEnabled);
            Assert.AreEqual(1, guestWithdrawals);
        }

        [Test]
        public void IsPaused_Replica_RequestsSharedPauseWithoutChangingItsLocalProjection()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatNetworkCommand submitted = null;
            replica.NetworkCommandRequested += command => submitted = command;

            replica.IsPaused = true;

            Assert.IsFalse(replica.IsPaused);
            Assert.AreEqual(TacticalCombatNetworkCommandKind.Pause, submitted.Kind);
            Assert.IsTrue(authority.ApplyNetworkCommand(CombatSide.Defender, submitted));
            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(1));
            Assert.IsTrue(replica.IsPaused);
            Assert.IsTrue(authority.IsPaused);
        }

        [Test]
        public void RequestImmediateResult_NetworkCombat_PreservesLiveBattleAndTerminalFlags()
        {
            TacticalCombatSession authority = CreateAuthority();

            Assert.IsFalse(authority.CanRequestImmediateResult);
            Assert.IsFalse(authority.RequestImmediateResult());
            Assert.IsFalse(
                authority.ApplyNetworkCommand(
                    CombatSide.Defender,
                    new() { Kind = TacticalCombatNetworkCommandKind.ImmediateResult }
                )
            );

            Assert.IsFalse(authority.IsComplete);
            Assert.IsFalse(authority.TerminalFlags[6]);
        }

        [Test]
        public void CaptureNetworkFrame_Briefing_AddressesEachFactionsExistingCue()
        {
            TacticalCombatSession authority = CreateAuthority();

            authority.Advance(50);
            TacticalCombatNetworkEffect[] cues = authority.CaptureNetworkFrame(1).Effects;

            Assert.IsTrue(
                cues.Any(cue =>
                    cue.Kind == TacticalCombatNetworkEffectKind.Notification
                    && cue.HasRecipient
                    && cue.Recipient == CombatSide.Attacker
                    && cue.MessageID == 154
                )
            );
            Assert.IsTrue(
                cues.Any(cue =>
                    cue.Kind == TacticalCombatNetworkEffectKind.Notification
                    && cue.HasRecipient
                    && cue.Recipient == CombatSide.Defender
                    && cue.MessageID == 32
                )
            );
        }

        [Test]
        public void ApplyNetworkEffects_AddressedBriefing_QueuesOnlyTheLocalFactionsCue()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            List<int> requested = new();
            replica.Audio.NotificationRequested += (message, _) => requested.Add(message);
            authority.Advance(50);

            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(1));

            CollectionAssert.Contains(requested, 32);
            CollectionAssert.DoesNotContain(requested, 154);
        }

        [Test]
        public void ApplyNetworkState_TractorRestraint_RestoresLiveInspectionMovement()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatNetworkFrame state = authority.CaptureNetworkFrame(1);
            state.Units.Single(unit => unit.UnitID == "alliance").TractorRestraint = 100;
            state.Units.Single(unit => unit.UnitID == "alliance").HasTractorSources = true;

            ApplyCapturedProjection(replica, state);

            Assert.AreEqual(0, replica.GetMovementBudget(replica.GetUnit("alliance")));
            Assert.AreEqual(0, replica.GetMovementRatio(replica.GetUnit("alliance")));
            Assert.IsFalse(replica.CanWithdraw(CombatSide.Defender));
        }

        [Test]
        public void ApplyNetworkCommand_CapitalRecovery_RejectsWithoutCreatingARequestedGroup()
        {
            TacticalCombatSession authority = CreateAuthority();
            authority.ApplyNetworkCommand(CombatSide.Defender, Object("alliance"));
            TacticalCombatGroup owner = authority.GetRequestedGroup(authority.GetUnit("alliance"));

            bool accepted = authority.ApplyNetworkCommand(
                CombatSide.Defender,
                new()
                {
                    Kind = TacticalCombatNetworkCommandKind.Order,
                    Order = TacticalCombatOrder.Recover,
                }
            );

            Assert.IsFalse(accepted);
            Assert.AreSame(owner, authority.GetRequestedGroup(authority.GetUnit("alliance")));
        }

        [Test]
        public void ConfigureNetworkAuthority_CapturedAutomationPreference_PreservesBothSidesAndGroups()
        {
            TacticalCombatSession authority = CreateSession(
                CombatSide.Attacker,
                new SystemRandomProvider(12)
            );
            authority.SetAutomated(CombatSide.Defender, true);

            authority.ConfigureNetworkAuthority();

            Assert.IsFalse(authority.GetSide(CombatSide.Attacker).IsAutomated);
            Assert.IsTrue(authority.GetSide(CombatSide.Defender).IsAutomated);
            Assert.IsTrue(authority.GetGroup(authority.GetUnit("alliance")).IsAutomated);
            Assert.IsFalse(authority.IsSideDirectControlEnabled(CombatSide.Defender));
            Assert.IsTrue(
                authority.ApplyNetworkCommand(
                    CombatSide.Defender,
                    new() { Kind = TacticalCombatNetworkCommandKind.ToggleAutomation }
                )
            );
            Assert.IsTrue(authority.IsSideDirectControlEnabled(CombatSide.Defender));
        }

        [Test]
        public void ApplyNetworkEffects_RemovedAttackGroup_RetainsMovieWithoutRestoringLiveOwnership()
        {
            TacticalCombatSession authority = CreateStationSession(CombatSide.Attacker);
            TacticalCombatSession replica = CreateStationSession(CombatSide.Defender);
            authority.ConfigureNetworkAuthority();
            replica.ConfigureNetworkReplica();
            authority.SetOrder(
                authority.GetGroup(authority.GetUnit("fighter")),
                TacticalCombatOrder.AttackBattleStation
            );
            for (int frame = 0; frame < 100 && authority.ActiveAttackRun == null; frame++)
                authority.Advance(50);
            Assert.IsNotNull(authority.ActiveAttackRun);
            authority.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);
            TacticalCombatNetworkFrame state = authority.CaptureNetworkFrame(1);
            int movieRequests = 0;
            replica.AttackRunMovieRequested += _ => movieRequests++;

            ApplyCapturedProjection(replica, state);

            Assert.IsEmpty(state.Groups);
            Assert.AreEqual(
                TacticalCombatAttackRunState.Failed,
                replica.PendingAttackRunMovie.AttackRun.State
            );
            Assert.IsNull(replica.GetGroup(replica.GetUnit("fighter")));
            Assert.IsNull(replica.GetRequestedGroup(replica.GetUnit("fighter")));
            Assert.AreEqual(1, movieRequests);
            Assert.IsTrue(replica.IsPaused);
            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(2));
            Assert.AreEqual(1, movieRequests);
            Assert.IsTrue(authority.CompleteNetworkMovie(state.MovieID));
            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(3));
            Assert.IsNull(replica.PendingAttackRunMovie);
            Assert.IsFalse(replica.IsPaused);
        }

        [Test]
        public void CaptureNetworkFrame_RemoteSelectedGroupDestroyed_ClearsStaleSelectionBeforeProjection()
        {
            TacticalCombatSession authority = CreateStationSession(CombatSide.Defender);
            TacticalCombatSession replica = CreateStationSession(CombatSide.Attacker);
            authority.ConfigureNetworkAuthority();
            replica.ConfigureNetworkReplica();
            authority.ApplyNetworkCommand(
                CombatSide.Attacker,
                new() { Kind = TacticalCombatNetworkCommandKind.FighterCategory, Category = 0 }
            );
            authority.SetOrder(
                authority.GetGroup(authority.GetUnit("fighter")),
                TacticalCombatOrder.AttackBattleStation
            );
            for (int frame = 0; frame < 100 && authority.ActiveAttackRun == null; frame++)
                authority.Advance(50);
            authority.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);

            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(1));

            Assert.AreEqual(TacticalCombatSelectionKind.Empty, replica.Selection.Kind);
            Assert.IsEmpty(replica.Selection.Groups);
        }

        [Test]
        public void CaptureNetworkFrame_RemovedAttackHelper_EmitsNoFurtherNotificationsOrMovies()
        {
            TacticalCombatSession authority = CreateStationSession(CombatSide.Attacker);
            authority.ConfigureNetworkAuthority();
            authority.SetOrder(
                authority.GetGroup(authority.GetUnit("fighter")),
                TacticalCombatOrder.AttackBattleStation
            );
            for (int frame = 0; frame < 100 && authority.ActiveAttackRun == null; frame++)
                authority.Advance(50);
            TacticalCombatAttackRun removed = authority.ActiveAttackRun;
            removed.ApplyState(TacticalCombatAttackRunState.Failed);
            TacticalCombatMovieRequest movie = authority.PendingAttackRunMovie;
            TacticalCombatNetworkFrame first = authority.CaptureNetworkFrame(1);

            removed.ApplyState(TacticalCombatAttackRunState.Failed);
            TacticalCombatNetworkFrame second = authority.CaptureNetworkFrame(2);

            Assert.IsTrue(
                first.Effects.Any(effect =>
                    effect.Kind == TacticalCombatNetworkEffectKind.Notification
                    && effect.MessageID == 315
                )
            );
            Assert.IsEmpty(second.Effects);
            Assert.AreSame(movie, authority.PendingAttackRunMovie);
            Assert.AreEqual(first.MovieID, second.MovieID);
        }

        [Test]
        public void ApplyNetworkEffects_FailedAttack_PreservesCueMovieAndDestructionOrderingOnce()
        {
            TacticalCombatSession authority = CreateStationSession(CombatSide.Defender);
            TacticalCombatSession replica = CreateStationSession(CombatSide.Attacker);
            authority.ConfigureNetworkAuthority();
            replica.ConfigureNetworkReplica();
            authority.SetOrder(
                authority.GetGroup(authority.GetUnit("fighter")),
                TacticalCombatOrder.AttackBattleStation
            );
            for (int frame = 0; frame < 100 && authority.ActiveAttackRun == null; frame++)
                authority.Advance(50);
            List<string> observed = new();
            replica.Audio.NotificationRequested += (message, _) =>
            {
                if (message == 315)
                    observed.Add("cue");
            };
            replica.AttackRunMovieRequested += _ => observed.Add("movie");
            replica.UnitDestroyed += unit =>
            {
                if (unit.InstanceID == "fighter")
                    observed.Add("destroyed");
            };
            authority.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);

            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(1));
            ApplyCapturedProjection(replica, authority.CaptureNetworkFrame(2));

            CollectionAssert.AreEqual(new[] { "cue", "movie", "destroyed" }, observed);
        }

        [TestCase(TacticalCombatNetworkCommandKind.Pause)]
        [TestCase(TacticalCombatNetworkCommandKind.Clear)]
        [TestCase(TacticalCombatNetworkCommandKind.FighterCategory)]
        [TestCase(TacticalCombatNetworkCommandKind.Waypoint)]
        public void ApplyNetworkCommand_PendingMovie_RejectsInflightInputWithoutMutatingSelectionOrPause(
            TacticalCombatNetworkCommandKind kind
        )
        {
            TacticalCombatSession authority = CreateStationSession(
                CombatSide.Attacker,
                reserveFighter: true
            );
            authority.ConfigureNetworkAuthority();
            authority.ApplyNetworkCommand(
                CombatSide.Attacker,
                new() { Kind = TacticalCombatNetworkCommandKind.FighterCategory, Category = 1 }
            );
            TacticalCombatGroup selected = authority.Selection.Group;
            authority.SetOrder(
                authority.GetGroup(authority.GetUnit("fighter")),
                TacticalCombatOrder.AttackBattleStation
            );
            for (int frame = 0; frame < 100 && authority.ActiveAttackRun == null; frame++)
                authority.Advance(50);
            authority.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);
            int movieID = authority.PendingNetworkMovieID;

            bool accepted = authority.ApplyNetworkCommand(
                CombatSide.Attacker,
                new()
                {
                    Kind = kind,
                    Paused = true,
                    Category = 1,
                    Waypoint = new(authority.Navigation.Waypoints[0]),
                }
            );

            Assert.IsFalse(accepted);
            Assert.AreSame(selected, authority.Selection.Group);
            Assert.AreEqual(TacticalCombatSelectionKind.FighterGroup, authority.Selection.Kind);
            Assert.IsEmpty(selected.RequestedWaypoints);
            Assert.AreEqual(movieID, authority.PendingNetworkMovieID);
            Assert.IsTrue(authority.CompleteNetworkMovie(movieID));
            Assert.IsFalse(authority.IsPaused);
        }

        /// <summary>
        /// Applies a captured projection through the same separated state and reliable-effect boundaries as live playback.
        /// </summary>
        /// <param name="session">The passive encounter receiving the projection.</param>
        /// <param name="frame">The captured state and its separately delivered effects.</param>
        private static void ApplyCapturedProjection(
            TacticalCombatSession session,
            TacticalCombatNetworkFrame frame
        )
        {
            TacticalCombatNetworkEffect[] effects = frame.Effects;
            frame.Effects = System.Array.Empty<TacticalCombatNetworkEffect>();
            session.ApplyNetworkState(frame);
            session.ApplyNetworkEffects(effects);
        }

        /// <summary>
        /// Creates a battle station and an eligible opposite-faction fighter group before any playback.
        /// </summary>
        /// <param name="player">The faction presented by this local view.</param>
        /// <param name="reserveFighter">Whether an independent category remains selectable after an attack loss.</param>
        /// <returns>The unadvanced encounter.</returns>
        private static TacticalCombatSession CreateStationSession(
            CombatSide player,
            bool reserveFighter = false
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
            TacticalCombatGroup group = new(2, new[] { fighter }, Vector3.UnitZ)
            {
                FighterCategory = 0,
            };
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
                groups.Add(
                    new TacticalCombatGroup(3, new[] { reserve }, Vector3.UnitZ)
                    {
                        FighterCategory = 1,
                    }
                );
            }
            return new(
                units,
                groups,
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
                new SystemRandomProvider(42)
            );
        }

        /// <summary>
        /// Creates a battle authority with two manually controlled opposing factions.
        /// </summary>
        /// <param name="additionalDefender">Whether to include another own-faction inspection choice.</param>
        /// <returns>The unadvanced shared simulation.</returns>
        private static TacticalCombatSession CreateAuthority(bool additionalDefender = false)
        {
            TacticalCombatSession session = CreateSession(
                CombatSide.Attacker,
                new SystemRandomProvider(12),
                additionalDefender
            );
            session.ConfigureNetworkAuthority();
            return session;
        }

        /// <summary>
        /// Creates the opposite faction's passive view with its own local presentation random stream.
        /// </summary>
        /// <param name="random">The optional observable local presentation stream.</param>
        /// <param name="additionalDefender">Whether to include another own-faction inspection choice.</param>
        /// <returns>The unadvanced passive view.</returns>
        private static TacticalCombatSession CreateReplica(
            SystemRandomProvider random = null,
            bool additionalDefender = false
        )
        {
            TacticalCombatSession session = CreateSession(
                CombatSide.Defender,
                random ?? new SystemRandomProvider(12),
                additionalDefender
            );
            session.ConfigureNetworkReplica();
            return session;
        }

        /// <summary>
        /// Captures two complete armed capital ships without changing campaign state or priming batteries.
        /// </summary>
        /// <param name="player">The faction whose selection is presented locally.</param>
        /// <param name="random">The encounter's local random stream.</param>
        /// <param name="additionalDefender">Whether to include another own-faction inspection choice.</param>
        /// <returns>The captured encounter.</returns>
        private static TacticalCombatSession CreateSession(
            CombatSide player,
            IRandomNumberProvider random,
            bool additionalDefender = false
        )
        {
            TacticalCombatUnit empire = CreateUnit(
                "empire",
                CombatSide.Attacker,
                1000001,
                -Vector3.UnitZ
            );
            TacticalCombatUnit alliance = CreateUnit(
                "alliance",
                CombatSide.Defender,
                1,
                Vector3.UnitZ
            );
            empire.Position = new Vector3(0, 0, 10);
            alliance.Position = new Vector3(0, 0, -10);
            List<TacticalCombatUnit> units = new() { alliance, empire };
            List<TacticalCombatGroup> groups = new()
            {
                new TacticalCombatGroup(10, new[] { alliance }, Vector3.UnitZ),
                new TacticalCombatGroup(1000010, new[] { empire }, -Vector3.UnitZ),
            };
            if (additionalDefender)
            {
                TacticalCombatUnit wing = CreateUnit(
                    "alliance-wing",
                    CombatSide.Defender,
                    2,
                    Vector3.UnitZ
                );
                wing.Position = new Vector3(12, 0, -10);
                units.Add(wing);
                groups.Add(new TacticalCombatGroup(11, new[] { wing }, Vector3.UnitZ));
            }
            return new TacticalCombatSession(
                units,
                groups,
                new TacticalCombatSide(
                    CombatSide.Attacker,
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
                new TacticalCombatSide(
                    CombatSide.Defender,
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
                player,
                100,
                random
            );
        }

        /// <summary>
        /// Creates one complete directional combat participant from runtime ship data.
        /// </summary>
        /// <param name="id">The campaign participant identity.</param>
        /// <param name="side">The encounter faction.</param>
        /// <param name="index">The stable tactical registration identity.</param>
        /// <param name="heading">The deployment heading.</param>
        /// <returns>The captured armed participant.</returns>
        private static TacticalCombatUnit CreateUnit(
            string id,
            CombatSide side,
            int index,
            Vector3 heading
        )
        {
            CapitalShip ship = new()
            {
                InstanceID = id,
                OwnerInstanceID = side == CombatSide.Attacker ? "FNEMP1" : "FNALL1",
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
                Hyperdrive = 1,
                SublightSpeed = 4,
                WeaponRecharge = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 20, 20, 20, 20, 100 };
            return new(
                ship,
                side,
                index,
                new TacticalCombatUnitDefinition { PresentationIndex = 1 }
            )
            {
                Heading = heading,
                DesiredHeading = heading,
            };
        }

        /// <summary>
        /// Addresses a registered participant through semantic selection input.
        /// </summary>
        /// <param name="unitID">The registered participant identity.</param>
        /// <returns>The semantic object command.</returns>
        private static TacticalCombatNetworkCommand Object(string unitID) =>
            new() { Kind = TacticalCombatNetworkCommandKind.Object, UnitID = unitID };
    }
}
