using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    public sealed partial class TacticalCombatNetworkSessionTests
    {
        [Test]
        public void ApplyNetworkEffects_SeparateState_ReplaysOnlyAfterReliableBatch()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatNetworkFrame state = authority.CaptureNetworkFrame(1);
            state.Effects = Array.Empty<TacticalCombatNetworkEffect>();
            int shots = 0;
            replica.ShotFired += _ => shots++;

            replica.ApplyNetworkState(state);

            Assert.Zero(shots);
            replica.ApplyNetworkEffects(new[] { NetworkShot() });
            Assert.AreEqual(1, shots);
            Assert.AreEqual(
                authority.GetUnit("empire").Position,
                replica.GetUnit("empire").Position
            );
            Assert.AreEqual(
                authority.GetUnit("alliance").Systems.Hull,
                replica.GetUnit("alliance").Systems.Hull
            );
        }

        [Test]
        public void ApplyNetworkEffects_InvalidLaterParticipant_RejectsEntireBatchBeforePlayback()
        {
            TacticalCombatSession replica = CreateReplica();
            int shots = 0;
            replica.ShotFired += _ => shots++;
            TacticalCombatNetworkEffect invalid = NetworkShot();
            invalid.TargetID = "unknown-unit";

            Assert.Throws<ArgumentException>(() =>
                replica.ApplyNetworkEffects(new[] { NetworkShot(), invalid })
            );

            Assert.Zero(shots);
        }

        [Test]
        public void ApplyNetworkState_EmbeddedEffects_RejectsWithoutChangingParticipant()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatNetworkFrame state = authority.CaptureNetworkFrame(1);
            state.Units[0].Position.X += 100;
            state.Effects = new[] { NetworkShot() };
            var position = replica.Units[0].Position;

            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkState(state));

            Assert.AreEqual(position, replica.Units[0].Position);
        }

        [Test]
        public void ApplyNetworkState_FailedAttack_DefersMovieAndPreservesReliableCueOrdering()
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
            TacticalCombatNetworkFrame state = authority.CaptureNetworkFrame(1);
            TacticalCombatNetworkEffect[] effects = state.Effects;
            state.Effects = Array.Empty<TacticalCombatNetworkEffect>();

            replica.ApplyNetworkState(state);
            Assert.IsEmpty(observed);
            Assert.IsNotNull(replica.PendingAttackRunMovie);
            replica.ApplyNetworkEffects(effects);

            CollectionAssert.AreEqual(new[] { "cue", "movie", "destroyed" }, observed);
            replica.ApplyNetworkState(authority.CaptureNetworkFrame(2));
            CollectionAssert.AreEqual(new[] { "cue", "movie", "destroyed" }, observed);
        }

        /// <summary>
        /// Creates a valid presentation edge between the fixture's opposing participants.
        /// </summary>
        /// <returns>The detached effect without a gameplay mutation.</returns>
        private static TacticalCombatNetworkEffect NetworkShot() =>
            new()
            {
                Kind = TacticalCombatNetworkEffectKind.Shot,
                SourceID = "empire",
                TargetID = "alliance",
                Weapon = TacticalWeaponType.LaserCannon,
                Strength = 1,
            };
    }
}
