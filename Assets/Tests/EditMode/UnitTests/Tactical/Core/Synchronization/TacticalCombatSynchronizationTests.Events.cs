using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Tests.Tactical
{
    public sealed partial class TacticalCombatSynchronizationTests
    {
        [Test]
        public void ApplySynchronizedEvents_SeparateState_ReplaysOnlyAfterReliableBatch()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatSnapshot state = authority.CaptureSnapshot(1);
            state.Effects = Array.Empty<TacticalCombatEvent>();
            int shots = 0;
            replica.ShotFired += _ => shots++;

            replica.ApplySnapshot(state);

            Assert.Zero(shots);
            replica.ApplySynchronizedEvents(new[] { NetworkShot() });
            Assert.AreEqual(1, shots);
            Assert.AreEqual(
                authority.GetUnit("empire").Position,
                replica.GetUnit("empire").Position
            );
            Assert.AreEqual(authority.GetUnit("alliance").Hull, replica.GetUnit("alliance").Hull);
        }

        [Test]
        public void ApplySynchronizedEvents_InvalidLaterParticipant_RejectsEntireBatchBeforePlayback()
        {
            TacticalCombatSession replica = CreateReplica();
            int shots = 0;
            replica.ShotFired += _ => shots++;
            TacticalCombatEvent invalid = NetworkShot();
            invalid.TargetID = "unknown-unit";

            Assert.Throws<ArgumentException>(() =>
                replica.ApplySynchronizedEvents(new[] { NetworkShot(), invalid })
            );

            Assert.Zero(shots);
        }

        [Test]
        public void ApplySnapshot_EmbeddedEffects_RejectsWithoutChangingParticipant()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatSnapshot state = authority.CaptureSnapshot(1);
            state.Units[0].Position.X += 100;
            state.Effects = new[] { NetworkShot() };
            var position = replica.Units[0].Position;

            Assert.Throws<ArgumentException>(() => replica.ApplySnapshot(state));

            Assert.AreEqual(position, replica.Units[0].Position);
        }

        [Test]
        public void ApplySnapshot_FailedAttack_DefersMovieAndPreservesReliableCueOrdering()
        {
            TacticalCombatSession authority = CreateStationSession(CombatSide.Defender);
            TacticalCombatSession replica = CreateStationSession(CombatSide.Attacker);
            authority.ConfigureSynchronizedAuthority();
            replica.ConfigureSynchronizedReplica();
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
            TacticalCombatSnapshot state = authority.CaptureSnapshot(1);
            TacticalCombatEvent[] effects = state.Effects;
            state.Effects = Array.Empty<TacticalCombatEvent>();

            replica.ApplySnapshot(state);
            Assert.IsEmpty(observed);
            Assert.IsNotNull(replica.PendingAttackRunMovie);
            replica.ApplySynchronizedEvents(effects);

            CollectionAssert.AreEqual(new[] { "cue", "movie", "destroyed" }, observed);
            replica.ApplySnapshot(authority.CaptureSnapshot(2));
            CollectionAssert.AreEqual(new[] { "cue", "movie", "destroyed" }, observed);
        }

        /// <summary>
        /// Creates a valid presentation edge between the fixture's opposing participants.
        /// </summary>
        /// <returns>The detached effect without a gameplay mutation.</returns>
        private static TacticalCombatEvent NetworkShot() =>
            new()
            {
                Kind = TacticalCombatEventKind.Shot,
                SourceID = "empire",
                TargetID = "alliance",
                Weapon = HardpointWeaponType.LaserCannon,
                Strength = 1,
            };
    }
}
