using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatAttackRunTests
    {
        [Test]
        public void ApplyState_StartingSuccessfulSelection_BeginsTwoMinuteSequence()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            Assert.AreEqual(TacticalCombatAttackRunState.Succeeding, run.State);
            Assert.AreEqual(120000, run.RemainingMilliseconds);
        }

        [Test]
        public void Advance_TwoMinuteSuccessSequence_RequestsQueuedSuccess()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            TacticalCombatAttackRunState? requested = null;
            run.StateChangeRequested += (_, state) => requested = state;
            run.Advance(120000);
            Assert.AreEqual(TacticalCombatAttackRunState.Succeeded, requested);
            Assert.AreEqual(TacticalCombatAttackRunState.Succeeding, run.State);
        }

        [Test]
        public void ApplyState_SuccessWithThreeSquadrons_DestroysFirstSquadron()
        {
            TacticalCombatAttackRun run = CreateRun(3);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            List<TacticalCombatUnit> destroyed = new();
            run.UnitDestroyed += destroyed.Add;
            run.ApplyState(TacticalCombatAttackRunState.Succeeded);
            Assert.AreEqual(1, destroyed.Count);
            Assert.AreEqual("fighter0", destroyed[0].InstanceID);
        }

        [Test]
        public void ApplyState_SuccessWithOneSquadron_PreservesSurvivor()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            List<TacticalCombatUnit> destroyed = new();
            run.UnitDestroyed += destroyed.Add;
            run.ApplyState(TacticalCombatAttackRunState.Succeeded);
            Assert.IsEmpty(destroyed);
            Assert.AreEqual(TacticalCombatUnitStatus.Active, run.Group.Members[0].Status);
        }

        [Test]
        public void ApplyState_FailedAttack_DestroysEverySquadron()
        {
            TacticalCombatAttackRun run = CreateRun(3);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            List<TacticalCombatUnit> destroyed = new();
            run.UnitDestroyed += destroyed.Add;
            run.ApplyState(TacticalCombatAttackRunState.Failed);
            Assert.AreEqual(3, destroyed.Count);
        }

        [Test]
        public void ApplyState_CancelledAttack_RestoresParticipationAndRequestsWithdrawal()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            run.ApplyState(TacticalCombatAttackRunState.Cancelled);
            Assert.AreEqual(TacticalCombatUnitStatus.Active, run.Group.Members[0].Status);
            Assert.AreEqual(TacticalCombatOrder.Withdraw, run.Group.Order);
        }

        /// <summary>
        /// Creates deterministic highly maneuverable attack squadrons.
        /// </summary>
        /// <param name="count">The number of squadrons.</param>
        /// <returns>The unstarted attack.</returns>
        private static TacticalCombatAttackRun CreateRun(int count)
        {
            List<TacticalCombatUnit> fighters = new();
            for (int index = 0; index < count; index++)
                fighters.Add(
                    new TacticalCombatUnit(
                        new Starfighter
                        {
                            InstanceID = "fighter" + index,
                            MaxSquadronSize = 12,
                            CurrentSquadronSize = 12,
                            Agility = 20,
                            Torpedoes = 100,
                        },
                        CombatSide.Attacker,
                        index + 1
                    )
                );
            TacticalCombatGroup group = new(10, fighters, Vector3.UnitZ);
            return new TacticalCombatAttackRun(group, new SystemRandomProvider(42), () => 1);
        }
    }
}
