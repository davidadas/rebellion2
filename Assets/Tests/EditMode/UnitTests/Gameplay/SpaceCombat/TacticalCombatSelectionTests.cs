using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatSelectionTests
    {
        private readonly List<Encounter> _encounters = new();

        /// <summary>
        /// Releases selection loops before their encounter-owned audio scheduler.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (Encounter encounter in _encounters)
                encounter.Dispose();
            _encounters.Clear();
        }

        [Test]
        public void QueueObject_BeforeInputDrain_DoesNotReplaceSelection()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObject(encounter.Friendly[0]);

            Assert.AreEqual(TacticalCombatSelectionKind.Empty, encounter.Selection.Kind);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.SingleObject, encounter.Selection.Kind);
            Assert.AreSame(encounter.Friendly[0], encounter.Selection.PrimaryUnit);
        }

        [Test]
        public void ProcessPendingCommands_CategoryBeforePointer_UsesCategoryContextForQueuedEnemy()
        {
            Encounter encounter = Create();
            TacticalCombatGroup group = encounter.Session.GetObjectGroup(CombatSide.Attacker, 0);
            encounter.Selection.QueueObject(encounter.Enemy);
            encounter.Selection.QueueObjectCategory(0);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.ObjectGroup, encounter.Selection.Kind);
            Assert.AreSame(group, encounter.Selection.Group);
            CollectionAssert.AreEqual(new[] { encounter.Enemy }, group.RequestedTargets);
        }

        [Test]
        public void QueueClear_BeforeEnemyHit_InspectsWithoutChangingFormerGroupTarget()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObjectCategory(0);
            encounter.Selection.ProcessPendingCommands();
            TacticalCombatGroup group = encounter.Selection.Group;
            encounter.Selection.QueueClear();
            encounter.Selection.QueueObject(encounter.Enemy);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.EnemyInspection, encounter.Selection.Kind);
            Assert.AreSame(encounter.Enemy, encounter.Selection.PrimaryUnit);
            Assert.IsEmpty(group.RequestedTargets);
        }

        [Test]
        public void QueueObject_SingleTrackedMember_DoesNotPromoteHighlightsIntoPrimaryMembership()
        {
            Encounter encounter = Create();
            TacticalCombatGroup group = encounter.Session.CreateGroup(encounter.Friendly);
            encounter.Session.SetObjectGroup(CombatSide.Attacker, 0, group);
            encounter.Selection.QueueObject(encounter.Friendly[1]);

            encounter.Selection.ProcessPendingCommands();

            CollectionAssert.AreEqual(new[] { encounter.Friendly[1] }, encounter.Selection.Members);
            CollectionAssert.AreEquivalent(encounter.Friendly, encounter.Selection.Highlights);
        }

        [Test]
        public void QueueObject_ExtendedFriendly_MaintainsUnsignedPrimaryOrderAndInspectsFirstKey()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObject(encounter.Friendly[1]);
            encounter.Selection.QueueObject(encounter.Friendly[0], true);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.MultipleObjects, encounter.Selection.Kind);
            CollectionAssert.AreEqual(encounter.Friendly.Take(2), encounter.Selection.Members);
            Assert.AreSame(encounter.Friendly[0], encounter.Selection.PrimaryUnit);
        }

        [Test]
        public void QueueObject_ExtendedMemberRemoval_ReturnsToSingleObjectContext()
        {
            Encounter encounter = Create();
            SelectMultiple(encounter);
            encounter.Selection.QueueObject(encounter.Friendly[0], true);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.SingleObject, encounter.Selection.Kind);
            Assert.AreSame(encounter.Friendly[1], encounter.Selection.PrimaryUnit);
        }

        [Test]
        public void QueueObject_SingleTrackedOwner_CreatesIndividualRequestedGroupBeforeAppliedTransfer()
        {
            Encounter encounter = Create();
            TacticalCombatUnit unit = encounter.Friendly[0];
            TacticalCombatGroup applied = encounter.Session.GetGroup(unit);
            encounter.Selection.QueueObject(unit);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueObject(encounter.Enemy);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreSame(applied, encounter.Session.GetGroup(unit));
            Assert.AreNotSame(applied, encounter.Session.GetRequestedGroup(unit));
            Assert.IsFalse(
                encounter.Session.Groups.Contains(encounter.Session.GetRequestedGroup(unit))
            );
            CollectionAssert.AreEqual(
                new[] { encounter.Enemy },
                encounter.Session.GetRequestedGroup(unit).RequestedTargets
            );
        }

        [Test]
        public void QueueObject_SingleUntrackedOwner_ReusesItsRequestedIdentity()
        {
            Encounter encounter = Create();
            TacticalCombatUnit unit = encounter.Friendly[0];
            TacticalCombatGroup group = encounter.Session.GetRequestedGroup(unit);
            encounter.Session.SetObjectGroup(CombatSide.Attacker, 0, null);
            encounter.Selection.QueueObject(unit);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueObject(encounter.Enemy);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreSame(group, encounter.Session.GetRequestedGroup(unit));
            CollectionAssert.AreEqual(new[] { encounter.Enemy }, group.RequestedTargets);
        }

        [Test]
        public void QueueObject_FriendlyDesignation_RequestsFriendlyTargetAndHoldFire()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueObject(encounter.Friendly[1]);

            encounter.Selection.ProcessPendingCommands();

            TacticalCombatGroup group = encounter.Session.GetRequestedGroup(encounter.Friendly[0]);
            Assert.AreEqual(TacticalCombatOrder.HoldFire, group.Order);
            CollectionAssert.AreEqual(new[] { encounter.Friendly[1] }, group.RequestedTargets);
        }

        [Test]
        public void QueueObject_ObjectCategoryExtension_TransfersRequestedMembershipOnly()
        {
            Encounter encounter = Create();
            TacticalCombatGroup old = encounter.Session.GetGroup(encounter.Friendly[1]);
            encounter.Selection.QueueObjectCategory(0);
            encounter.Selection.ProcessPendingCommands();
            TacticalCombatGroup destination = encounter.Selection.Group;
            encounter.Selection.QueueObject(encounter.Friendly[1], true);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreSame(old, encounter.Session.GetGroup(encounter.Friendly[1]));
            Assert.AreSame(destination, encounter.Session.GetRequestedGroup(encounter.Friendly[1]));
            Assert.Contains(encounter.Friendly[1], destination.RequestedMembers.ToList());
            Assert.IsFalse(destination.Members.Contains(encounter.Friendly[1]));
        }

        [Test]
        public void QueueObjectCategory_LastRequestedMemberRemoved_ClearsSelectionAndSlot()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObjectCategory(0);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueObject(encounter.Friendly[0], true);

            encounter.Selection.ProcessPendingCommands();

            Assert.IsNull(encounter.Session.GetObjectGroup(CombatSide.Attacker, 0));
            Assert.AreEqual(TacticalCombatSelectionKind.Empty, encounter.Selection.Kind);
        }

        [Test]
        public void QueueObjectCategory_EmptySlot_AssignsOrderedMixedCommandGroup()
        {
            Encounter encounter = Create(includeFighter: true);
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.QueueObject(encounter.Fighter, true);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueObjectCategory(7);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.ObjectGroup, encounter.Selection.Kind);
            Assert.AreEqual(TacticalCombatGroupKind.Command, encounter.Selection.Group.Kind);
            CollectionAssert.AreEqual(
                new[] { encounter.Friendly[0], encounter.Fighter },
                encounter.Selection.Group.RequestedMembers
            );
            Assert.AreSame(
                encounter.Selection.Group,
                encounter.Session.GetObjectGroup(CombatSide.Attacker, 7)
            );
        }

        [Test]
        public void QueueObjectCategory_AlreadySelectedSlot_TogglesEmpty()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObjectCategory(0);
            encounter.Selection.QueueObjectCategory(0);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.Empty, encounter.Selection.Kind);
        }

        [Test]
        public void QueueFighterCategory_EmptySlot_PreservesCurrentInspection()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueFighterCategory(3);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.SingleObject, encounter.Selection.Kind);
        }

        [Test]
        public void QueueObject_ObjectCategoryHeldFriendlyThenExtendedEnemy_ReplacesFriendlyDesignation()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObjectCategory(0);
            encounter.Selection.ProcessPendingCommands();
            TacticalCombatGroup group = encounter.Selection.Group;
            encounter.Selection.QueueObject(encounter.Friendly[1]);
            encounter.Selection.ProcessPendingCommands();
            Assert.AreEqual(TacticalCombatOrder.HoldFire, group.Order);

            encounter.Selection.QueueObject(encounter.Enemy, true);
            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatOrder.None, group.Order);
            CollectionAssert.AreEqual(new[] { encounter.Enemy }, group.RequestedTargets);
        }

        [Test]
        public void QueueObject_FighterCategoryHeldFriendlyThenExtendedEnemy_PreservesFriendlyDesignation()
        {
            Encounter encounter = Create(includeFighter: true);
            encounter.Selection.QueueFighterCategory(0);
            encounter.Selection.ProcessPendingCommands();
            TacticalCombatGroup group = encounter.Selection.Group;
            TacticalCombatGroup friendlyOwner = encounter.Session.GetRequestedGroup(
                encounter.Friendly[0]
            );
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueObject(encounter.Enemy, true);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatOrder.HoldFire, group.Order);
            CollectionAssert.AreEqual(
                new[] { encounter.Friendly[0], encounter.Enemy },
                group.RequestedTargets
            );
            CollectionAssert.AreEqual(new[] { encounter.Fighter }, group.RequestedMembers);
            Assert.AreSame(
                friendlyOwner,
                encounter.Session.GetRequestedGroup(encounter.Friendly[0])
            );
        }

        [Test]
        public void ProcessPendingCommands_FighterCategory_UsesFactionSelectionLoop()
        {
            Encounter encounter = Create(includeFighter: true);
            encounter.Selection.QueueFighterCategory(0);

            encounter.Selection.ProcessPendingCommands();

            Playback playback = encounter.Playbacks.Single();
            Assert.AreEqual("22", playback.Path);
            Assert.IsTrue(playback.Loop);
        }

        [Test]
        public void QueueObject_MultiSelectionAndFullCategories_AssignsIndividualTargets()
        {
            Encounter encounter = Create(9);
            SelectMultiple(encounter);
            encounter.Selection.QueueObject(encounter.Enemy);

            encounter.Selection.ProcessPendingCommands();

            TacticalCombatGroup first = encounter.Session.GetRequestedGroup(encounter.Friendly[0]);
            TacticalCombatGroup second = encounter.Session.GetRequestedGroup(encounter.Friendly[1]);
            Assert.AreNotSame(first, second);
            Assert.AreEqual(TacticalCombatGroupKind.Command, first.Kind);
            CollectionAssert.AreEqual(new[] { encounter.Enemy }, first.RequestedTargets);
            CollectionAssert.AreEqual(new[] { encounter.Enemy }, second.RequestedTargets);
            Assert.AreEqual(TacticalCombatSelectionKind.SingleObject, encounter.Selection.Kind);
        }

        [Test]
        public void QueueWaypoint_ExtendedRoute_TogglesRequestedPointWithoutApplyingIt()
        {
            Encounter encounter = Create();
            Vector3 point = encounter.Session.Navigation.Waypoints[0];
            encounter.Selection.QueueObjectCategory(0);
            encounter.Selection.ProcessPendingCommands();
            TacticalCombatGroup group = encounter.Selection.Group;
            encounter.Selection.QueueWaypoint(point, true);
            encounter.Selection.ProcessPendingCommands();
            CollectionAssert.AreEqual(new[] { point }, group.RequestedWaypoints);
            Assert.IsFalse(group.HasWaypoints);
            encounter.Selection.QueueWaypoint(point, true);

            encounter.Selection.ProcessPendingCommands();

            Assert.IsEmpty(group.RequestedWaypoints);
        }

        [Test]
        public void QueueToggleAutomation_ObserveMode_InspectsAndPreservesRequestedOrders()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObjectCategory(0);
            encounter.Selection.ProcessPendingCommands();
            TacticalCombatGroup group = encounter.Selection.Group;
            encounter.Selection.QueueToggleAutomation();
            encounter.Selection.QueueObject(encounter.Enemy);

            encounter.Selection.ProcessPendingCommands();

            Assert.IsTrue(encounter.Session.GetSide(CombatSide.Attacker).IsAutomated);
            Assert.AreEqual(TacticalCombatSelectionKind.EnemyInspection, encounter.Selection.Kind);
            Assert.IsEmpty(group.RequestedTargets);
        }

        [Test]
        public void QueueNearestTarget_CloserFighter_PrefersCapitalCategory()
        {
            Encounter encounter = Create(includeEnemyFighter: true);
            encounter.Enemy.Position = new Vector3(0, 0, 100);
            encounter.EnemyFighter.Position = Vector3.UnitZ;
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueNearestTarget();

            encounter.Selection.ProcessPendingCommands();

            CollectionAssert.AreEqual(
                new[] { encounter.Enemy },
                encounter.Session.GetRequestedGroup(encounter.Friendly[0]).RequestedTargets
            );
        }

        [Test]
        public void QueueNearestTarget_EqualCapitalDistances_RetainsFirstRegisteredObject()
        {
            Encounter encounter = Create(secondEnemy: true);
            encounter.Enemy.Position = new Vector3(0, 0, 40);
            encounter.SecondEnemy.Position = new Vector3(0, 0, -40);
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueNearestTarget();

            encounter.Selection.ProcessPendingCommands();

            CollectionAssert.AreEqual(
                new[] { encounter.Enemy },
                encounter.Session.GetRequestedGroup(encounter.Friendly[0]).RequestedTargets
            );
        }

        [Test]
        public void RequestFormation_SingleObject_DoesNotCreateRequestedGroup()
        {
            Encounter encounter = Create();
            TacticalCombatUnit unit = encounter.Friendly[0];
            TacticalCombatGroup before = encounter.Session.GetRequestedGroup(unit);
            encounter.Selection.QueueObject(unit);
            encounter.Selection.ProcessPendingCommands();

            encounter.Selection.RequestFormation(TacticalCombatFormationKind.Surround);

            Assert.AreSame(before, encounter.Session.GetRequestedGroup(unit));
        }

        [Test]
        public void RequestFormation_MultipleObjectsWithFreeSlot_CreatesUntrackedGroupWithoutApplyingFormation()
        {
            Encounter encounter = Create();
            SelectMultiple(encounter);

            encounter.Selection.RequestFormation(TacticalCombatFormationKind.Surround);

            TacticalCombatGroup group = encounter.Session.GetRequestedGroup(encounter.Friendly[0]);
            Assert.AreSame(group, encounter.Session.GetRequestedGroup(encounter.Friendly[1]));
            Assert.AreEqual(TacticalCombatFormationKind.Line, group.RequestedFormation);
            for (int slot = 0; slot < 8; slot++)
                Assert.AreNotSame(
                    group,
                    encounter.Session.GetObjectGroup(CombatSide.Attacker, slot)
                );
            Assert.AreEqual(TacticalCombatSelectionKind.MultipleObjects, encounter.Selection.Kind);
        }

        [TestCase(1099, 25)]
        [TestCase(1100, 24)]
        [TestCase(1999, 24)]
        [TestCase(2000, 23)]
        public void ProcessPendingCommands_SelectedCapital_UsesMaximumHullLoopCode(
            int maximumHull,
            int message
        )
        {
            Encounter encounter = Create(maximumHull: maximumHull, currentHull: 100);
            encounter.Selection.QueueObject(encounter.Friendly[0]);

            encounter.Selection.ProcessPendingCommands();

            Playback playback = encounter.Playbacks.Single();
            Assert.AreEqual(message.ToString(), playback.Path);
            Assert.IsTrue(playback.Loop);
        }

        [Test]
        public void ProcessPendingCommands_ContextReplacement_StopsOnlyOwnedSelectedLoop()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            Playback first = encounter.Playbacks.Single();
            encounter.Selection.QueueClear();
            encounter.Selection.QueueObject(encounter.Friendly[1]);

            encounter.Selection.ProcessPendingCommands();

            Assert.IsTrue(first.Stopped);
            Assert.AreEqual(2, encounter.Playbacks.Count);
            Assert.IsFalse(encounter.Playbacks[1].Stopped);
        }

        [Test]
        public void ProcessPendingCommands_PauseAndResume_ReleasesThenRestartsSelectedLoop()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            Playback first = encounter.Playbacks.Single();
            encounter.Session.IsPaused = true;
            encounter.Selection.ProcessPendingCommands();
            Assert.IsTrue(first.Stopped);
            Assert.AreEqual(1, encounter.Playbacks.Count);
            encounter.Session.IsPaused = false;

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(2, encounter.Playbacks.Count);
            Assert.IsFalse(encounter.Playbacks[1].Stopped);
        }

        [Test]
        public void ProcessPendingCommands_FinishedSelectedLoop_ReacquiresOwnedPlayback()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            encounter.Playbacks[0].Playing = false;

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(2, encounter.Playbacks.Count);
            Assert.IsTrue(encounter.Playbacks[1].Playing);
        }

        [Test]
        public void ProcessPendingCommands_SingleVariantSelection_DoesNotConsumeSelectorRandomDraw()
        {
            Encounter encounter = Create();
            encounter.Random.Reseed(73);
            TacticalCombatRandom expected = new(73);
            encounter.Selection.QueueObject(encounter.Friendly[0]);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(expected.NextInt(0, 10000), encounter.Random.NextInt(0, 10000));
        }

        [Test]
        public void IsDisplayObject_ActiveStatusWithZeroHull_RemainsPresentUntilStatusTransition()
        {
            Encounter encounter = Create(currentHull: 0);

            Assert.IsFalse(encounter.Friendly[0].Systems.IsAlive);
            Assert.IsTrue(encounter.Selection.IsDisplayObject(encounter.Friendly[0]));
        }

        [TestCase(TacticalCombatUnitStatus.PreparingHyperspace, true)]
        [TestCase(TacticalCombatUnitStatus.EnteringHyperspace, false)]
        [TestCase(TacticalCombatUnitStatus.AwaitingWithdrawal, false)]
        [TestCase(TacticalCombatUnitStatus.Withdrawn, false)]
        [TestCase(TacticalCombatUnitStatus.Destroyed, false)]
        public void IsDisplayObject_WithdrawalStatus_UsesDisplayPresenceDomain(
            TacticalCombatUnitStatus status,
            bool expected
        )
        {
            Encounter encounter = Create();
            encounter.Friendly[0].Status = status;

            Assert.AreEqual(expected, encounter.Selection.IsDisplayObject(encounter.Friendly[0]));
        }

        [Test]
        public void QueueObject_SingleEnemyPreparingHyperspace_InspectsWithoutRequestingTarget()
        {
            Encounter encounter = Create();
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            TacticalCombatGroup group = encounter.Session.GetRequestedGroup(encounter.Friendly[0]);
            encounter.Enemy.Status = TacticalCombatUnitStatus.PreparingHyperspace;
            encounter.Selection.QueueObject(encounter.Enemy);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.EnemyInspection, encounter.Selection.Kind);
            Assert.AreSame(encounter.Enemy, encounter.Selection.PrimaryUnit);
            Assert.IsEmpty(group.RequestedTargets);
        }

        [Test]
        public void QueueDisplayMode_NextBeforeCategoryToggle_DrainsDisplayQueueFirst()
        {
            Encounter encounter = Create();
            TacticalCombatGroup group = encounter.Session.CreateGroup(encounter.Friendly);
            encounter.Session.SetObjectGroup(CombatSide.Attacker, 0, group);
            encounter.Selection.QueueObjectCategory(0);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueDisplayMode(true);
            encounter.Selection.QueueObjectCategory(0);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.Empty, encounter.Selection.Kind);
        }

        [Test]
        public void QueueDisplayMode_CategoryPendingMembership_TraversesRequestedRoster()
        {
            Encounter encounter = Create();
            TacticalCombatGroup group = encounter.Session.CreateGroup(encounter.Friendly);
            encounter.Session.SetObjectGroup(CombatSide.Attacker, 0, group);
            encounter.Selection.QueueObjectCategory(0);
            encounter.Selection.ProcessPendingCommands();
            Assert.IsEmpty(group.Members);
            encounter.Selection.QueueDisplayMode(true);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreSame(encounter.Friendly[1], encounter.Selection.PrimaryUnit);
            Assert.AreSame(group, encounter.Selection.Group);
        }

        [Test]
        public void QueueDisplayMode_CapitalEnteringHyperspace_DoesNotSkipPastUndisplayableRosterEntry()
        {
            Encounter encounter = Create(count: 3);
            encounter.Friendly[1].Status = TacticalCombatUnitStatus.EnteringHyperspace;
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueDisplayMode(true);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(TacticalCombatSelectionKind.Empty, encounter.Selection.Kind);
        }

        [Test]
        public void QueueDisplayMode_ActiveCapitalWithZeroHull_TraversesWithoutHealthGate()
        {
            Encounter encounter = Create(currentHull: 0);
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.ProcessPendingCommands();
            encounter.Selection.QueueDisplayMode(true);

            encounter.Selection.ProcessPendingCommands();

            Assert.AreSame(encounter.Friendly[1], encounter.Selection.PrimaryUnit);
        }

        [Test]
        public void ProcessStationDeparture_PendingObjectSelection_DrainsBeforeClosingControls()
        {
            Encounter encounter = Create();
            TacticalCombatSelectionKind? whenClosed = null;
            encounter.Selection.StationControlsClosed += () =>
                whenClosed = encounter.Selection.Kind;
            encounter.Selection.QueueObject(encounter.Friendly[0]);

            encounter.Selection.ProcessStationDeparture();

            Assert.AreEqual(TacticalCombatSelectionKind.SingleObject, whenClosed);
            Assert.AreSame(encounter.Friendly[0], encounter.Selection.PrimaryUnit);
        }

        [Test]
        public void QueueWithdrawal_BeforeInputDrain_PreservesSideAndPresentation()
        {
            Encounter encounter = Create(withdrawalRoute: true);
            bool dispatched = false;
            encounter.Selection.WithdrawalDispatched += () => dispatched = true;

            encounter.Selection.QueueWithdrawal();

            Assert.IsFalse(encounter.Session.GetSide(CombatSide.Attacker).IsWithdrawing);
            Assert.IsFalse(dispatched);
            encounter.Selection.ProcessPendingCommands();
            Assert.IsTrue(encounter.Session.GetSide(CombatSide.Attacker).IsWithdrawing);
            Assert.IsTrue(dispatched);
        }

        [Test]
        public void QueueWithdrawal_AdmittedDispatch_ClosesControlsBeforeMutationAndCompletesAfter()
        {
            Encounter encounter = Create(withdrawalRoute: true);
            List<string> events = new();
            encounter.Selection.WithdrawalStarting += () =>
            {
                Assert.IsFalse(encounter.Session.GetSide(CombatSide.Attacker).IsWithdrawing);
                Assert.IsTrue(encounter.Session.IsDirectControlEnabled);
                events.Add("unlock");
            };
            encounter.Selection.StationControlsClosed += () =>
            {
                Assert.IsFalse(encounter.Session.GetSide(CombatSide.Attacker).IsWithdrawing);
                events.Add("controls");
            };
            encounter.Selection.WithdrawalDispatched += () =>
            {
                Assert.IsTrue(encounter.Session.GetSide(CombatSide.Attacker).IsWithdrawing);
                Assert.IsFalse(encounter.Session.IsDirectControlEnabled);
                events.Add("background");
            };
            encounter.Selection.QueueWithdrawal();

            encounter.Selection.ProcessPendingCommands();

            CollectionAssert.AreEqual(new[] { "unlock", "controls", "background" }, events);
        }

        [Test]
        public void QueueWithdrawal_NoLongerEligible_DoesNotCloseControlsOrDispatchAgain()
        {
            Encounter encounter = Create(withdrawalRoute: true);
            int notifications = 0;
            encounter.Selection.WithdrawalStarting += () => notifications++;
            encounter.Selection.StationControlsClosed += () => notifications++;
            encounter.Selection.WithdrawalDispatched += () => notifications++;
            encounter.Selection.QueueWithdrawal();
            Assert.IsTrue(encounter.Session.BeginWithdrawal(CombatSide.Attacker));

            encounter.Selection.ProcessPendingCommands();

            Assert.AreEqual(0, notifications);
        }

        /// <summary>
        /// Creates and retains an isolated encounter for public selection-contract assertions.
        /// </summary>
        /// <param name="count">The friendly capital count.</param>
        /// <param name="includeFighter">Whether to include a friendly fighter.</param>
        /// <param name="includeEnemyFighter">Whether to include an opposing fighter.</param>
        /// <param name="secondEnemy">Whether to include a second opposing capital.</param>
        /// <param name="maximumHull">The friendly capital's authored maximum hull.</param>
        /// <param name="currentHull">The friendly capital's initial remaining hull, or null for its maximum.</param>
        /// <param name="withdrawalRoute">Whether a strategic departure destination exists.</param>
        /// <returns>The captured selection owner and playback observations.</returns>
        private Encounter Create(
            int count = 2,
            bool includeFighter = false,
            bool includeEnemyFighter = false,
            bool secondEnemy = false,
            int maximumHull = 1000,
            int? currentHull = null,
            bool withdrawalRoute = false
        )
        {
            Encounter encounter = new(
                count,
                includeFighter,
                includeEnemyFighter,
                secondEnemy,
                maximumHull,
                currentHull,
                withdrawalRoute
            );
            _encounters.Add(encounter);
            return encounter;
        }

        /// <summary>
        /// Builds a two-object temporary context by its public queued pointer contract.
        /// </summary>
        /// <param name="encounter">The isolated encounter.</param>
        private static void SelectMultiple(Encounter encounter)
        {
            encounter.Selection.QueueObject(encounter.Friendly[0]);
            encounter.Selection.QueueObject(encounter.Friendly[1], true);
            encounter.Selection.ProcessPendingCommands();
        }

        private sealed class Encounter : IDisposable
        {
            public TacticalCombatSession Session { get; }
            public TacticalCombatSelection Selection => Session.Selection;
            public List<TacticalCombatUnit> Friendly { get; } = new();
            public TacticalCombatUnit Enemy { get; }
            public TacticalCombatUnit SecondEnemy { get; }
            public TacticalCombatUnit Fighter { get; }
            public TacticalCombatUnit EnemyFighter { get; }
            public TacticalCombatRandom Random { get; } = new(42);
            public List<Playback> Playbacks { get; } = new();

            /// <summary>
            /// Captures ordered same-side command groups and optional opposing display categories.
            /// </summary>
            /// <param name="count">The friendly capital count.</param>
            /// <param name="fighter">Whether to add a friendly fighter.</param>
            /// <param name="enemyFighter">Whether to add an opposing fighter.</param>
            /// <param name="secondEnemy">Whether to add another opposing capital.</param>
            /// <param name="maximumHull">The friendly capital's authored hull.</param>
            /// <param name="currentHull">Its initial remaining hull.</param>
            /// <param name="withdrawalRoute">Whether a strategic departure destination exists.</param>
            public Encounter(
                int count,
                bool fighter,
                bool enemyFighter,
                bool secondEnemy,
                int maximumHull,
                int? currentHull,
                bool withdrawalRoute
            )
            {
                List<TacticalCombatUnit> units = new();
                List<TacticalCombatGroup> groups = new();
                for (int index = 0; index < count; index++)
                {
                    TacticalCombatUnit unit = new(
                        CreateShip("friendly-" + index, maximumHull, currentHull ?? maximumHull),
                        CombatSide.Attacker,
                        index + 1
                    );
                    Friendly.Add(unit);
                    units.Add(unit);
                    groups.Add(new TacticalCombatGroup(100 + index, new[] { unit }, Vector3.UnitZ));
                }
                Enemy = new TacticalCombatUnit(
                    CreateShip("enemy", 1000, 1000),
                    CombatSide.Defender,
                    1000001
                );
                units.Add(Enemy);
                groups.Add(new TacticalCombatGroup(1000100, new[] { Enemy }, -Vector3.UnitZ));
                if (fighter)
                {
                    Fighter = CreateFighter("fighter", CombatSide.Attacker, count + 1);
                    units.Add(Fighter);
                    groups.Add(
                        new TacticalCombatGroup(200, new[] { Fighter }, Vector3.UnitZ)
                        {
                            FighterCategory = 0,
                        }
                    );
                }
                if (enemyFighter)
                {
                    EnemyFighter = CreateFighter("enemy-fighter", CombatSide.Defender, 1000002);
                    units.Add(EnemyFighter);
                    groups.Add(
                        new TacticalCombatGroup(1000200, new[] { EnemyFighter }, -Vector3.UnitZ)
                    );
                }
                if (secondEnemy)
                {
                    SecondEnemy = new TacticalCombatUnit(
                        CreateShip("other-enemy", 1000, 1000),
                        CombatSide.Defender,
                        1000003
                    );
                    units.Add(SecondEnemy);
                    groups.Add(
                        new TacticalCombatGroup(1000300, new[] { SecondEnemy }, -Vector3.UnitZ)
                    );
                }
                TacticalCombatDefinition definition = new();
                for (int message = 21; message < 286; message++)
                    definition.Notifications.Add(
                        new TacticalCombatNotificationDefinition
                        {
                            MessageID = message,
                            Variants = new List<TacticalCombatSoundClip>
                            {
                                new() { Path = message.ToString() },
                            },
                        }
                    );
                Session = new TacticalCombatSession(
                    units,
                    groups,
                    CreateSide(CombatSide.Attacker, 0, withdrawalRoute),
                    CreateSide(CombatSide.Defender, 1, withdrawalRoute),
                    CombatSide.Attacker,
                    100,
                    Random,
                    definition,
                    presentationClock: () => 0
                );
                Session.Audio.Playback = (clip, loop) =>
                {
                    Playback playback = new(clip.Path, loop);
                    Playbacks.Add(playback);
                    return playback;
                };
            }

            /// <summary>
            /// Releases selected playback before shutting down its shared scheduler.
            /// </summary>
            public void Dispose()
            {
                Selection.Dispose();
                Session.Audio.Dispose();
            }

            /// <summary>
            /// Creates an unarmed active capital with independently authored maximum and current hull.
            /// </summary>
            /// <param name="id">The encounter identity.</param>
            /// <param name="maximum">The authored hull.</param>
            /// <param name="current">The initial remaining hull.</param>
            /// <returns>The campaign-backed ship definition.</returns>
            private static CapitalShip CreateShip(string id, int maximum, int current) =>
                new()
                {
                    InstanceID = id,
                    MaxHullStrength = maximum,
                    CurrentHullStrength = current,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    Hyperdrive = 1,
                    SublightSpeed = 4,
                    Maneuverability = 1,
                };

            /// <summary>
            /// Creates an active fighter outside any carrier's docked roster.
            /// </summary>
            /// <param name="id">The encounter identity.</param>
            /// <param name="side">The captured side.</param>
            /// <param name="index">The registration key.</param>
            /// <returns>The captured fighter.</returns>
            private static TacticalCombatUnit CreateFighter(
                string id,
                CombatSide side,
                int index
            ) =>
                new(
                    new Starfighter
                    {
                        InstanceID = id,
                        MaxSquadronSize = 12,
                        CurrentSquadronSize = 12,
                    },
                    side,
                    index
                );

            /// <summary>
            /// Creates a manually controlled side with the requested strategic withdrawal admission.
            /// </summary>
            /// <param name="side">The captured encounter side.</param>
            /// <param name="faction">The authored faction index.</param>
            /// <param name="withdrawalRoute">Whether a strategic departure destination exists.</param>
            /// <returns>The side's command state.</returns>
            private static TacticalCombatSide CreateSide(
                CombatSide side,
                int faction,
                bool withdrawalRoute
            ) =>
                new(
                    side,
                    faction,
                    side == CombatSide.Attacker ? Vector3.UnitZ : -Vector3.UnitZ,
                    withdrawalRoute,
                    0,
                    0,
                    null,
                    null,
                    0,
                    0,
                    false
                );
        }

        private sealed class Playback : ITacticalCombatNotificationPlayback
        {
            public string Path { get; }
            public bool Loop { get; }
            public bool Stopped { get; private set; }
            public bool Playing { get; set; } = true;
            public bool IsPlaying => Playing;

            /// <summary>
            /// Captures the actual clip and loop request from the public playback boundary.
            /// </summary>
            /// <param name="path">The selected variant.</param>
            /// <param name="loop">Whether playback repeats.</param>
            public Playback(string path, bool loop)
            {
                Path = path;
                Loop = loop;
            }

            /// <summary>
            /// Records cancellation of this playback without stopping unrelated audio.
            /// </summary>
            public void Stop()
            {
                Stopped = true;
                Playing = false;
            }
        }
    }
}
