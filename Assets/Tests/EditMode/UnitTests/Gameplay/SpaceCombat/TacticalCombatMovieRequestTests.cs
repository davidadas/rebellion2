using System;
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
    public sealed class TacticalCombatMovieRequestTests
    {
        [Test]
        public void Complete_PendingMovie_ReleasesSimulationPause()
        {
            TacticalCombatSession session = CreateSession();
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);
            TacticalCombatMovieRequest request = session.PendingAttackRunMovie;

            request.Complete();

            Assert.IsFalse(session.IsPaused);
            Assert.IsNull(session.PendingAttackRunMovie);
        }

        [Test]
        public void Complete_UserPausedBeforeMovie_PreservesUserPause()
        {
            TacticalCombatSession session = CreateSession();
            session.IsPaused = true;
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            session.PendingAttackRunMovie.Complete();

            Assert.IsTrue(session.IsPaused);
        }

        [Test]
        public void Complete_RepeatedAcknowledgement_DoesNotReleaseUserPause()
        {
            TacticalCombatSession session = CreateSession();
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);
            TacticalCombatMovieRequest request = session.PendingAttackRunMovie;
            request.Complete();
            session.IsPaused = true;

            request.Complete();

            Assert.IsTrue(session.IsPaused);
        }

        /// <summary>
        /// Creates a registered station attack with one surviving fighter squadron.
        /// </summary>
        /// <returns>The battle after its attack run has entered the two-minute sequence.</returns>
        private static TacticalCombatSession CreateSession()
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
            TacticalCombatSession session = new(
                new[] { fighter, station },
                new[] { group },
                CreateSide(CombatSide.Attacker, 0, Vector3.UnitZ),
                CreateSide(CombatSide.Defender, 1, -Vector3.UnitZ),
                CombatSide.Attacker,
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
        /// Captures an unautomated side without command officers or retreat destinations.
        /// </summary>
        /// <param name="side">The deployment side.</param>
        /// <param name="faction">The authored faction index.</param>
        /// <param name="forward">The deployment heading.</param>
        /// <returns>The side's command state.</returns>
        private static TacticalCombatSide CreateSide(CombatSide side, int faction, Vector3 forward)
        {
            return new TacticalCombatSide(
                side,
                faction,
                forward,
                false,
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
