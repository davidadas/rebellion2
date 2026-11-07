using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatAITests
    {
        [Test]
        public void Advance_AutomatedSide_AssignsOpposingCapitalTarget()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 1);
            Advance(session);
            TacticalCombatUnit defender = session.GetUnit("defender-0");
            Assert.AreSame(session.GetUnit("attacker"), session.GetGroup(defender).PrimaryTarget);
        }

        [Test]
        public void Advance_PlayerSide_LeavesDesignatedTargetListUnchanged()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 1);
            Advance(session);
            Assert.IsEmpty(session.GetGroup(session.GetUnit("attacker")).RequestedTargets);
        }

        [Test]
        public void Advance_UnarmedAutomatedSide_StartsAvailableWithdrawal()
        {
            TacticalCombatSession session = CreateSession(20, 0, false, 1);
            Advance(session);
            Assert.IsTrue(session.GetSide(CombatSide.Defender).IsWithdrawing);
        }

        [Test]
        public void Advance_OpposingGravityWell_PreventsAutomatedWithdrawal()
        {
            TacticalCombatSession session = CreateSession(20, 0, true, 1);
            Advance(session);
            Assert.IsFalse(session.GetSide(CombatSide.Defender).IsWithdrawing);
        }

        [Test]
        public void Advance_FourEquivalentCapitalShips_CreatesTwoBalancedGroups()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 4);
            Advance(session);
            TacticalCombatGroup[] groups = session
                .Groups.Where(group => group.Side == CombatSide.Defender)
                .ToArray();
            Assert.AreEqual(2, groups.Length);
            Assert.IsTrue(groups.All(group => group.Members.Count == 2));
        }

        /// <summary>
        /// Builds one player-controlled ship opposite an automated capital-ship deployment.
        /// </summary>
        /// <param name="attackerStrength">The player's battery strength.</param>
        /// <param name="defenderStrength">The automated side's battery strength.</param>
        /// <param name="gravityWell">Whether the player blocks enemy withdrawal.</param>
        /// <param name="defenderCount">The automated deployment count.</param>
        /// <returns>The isolated encounter.</returns>
        private static TacticalCombatSession CreateSession(
            int attackerStrength,
            int defenderStrength,
            bool gravityWell,
            int defenderCount
        )
        {
            List<TacticalCombatUnit> units = new();
            units.Add(
                CreateUnit("attacker", CombatSide.Attacker, 1, attackerStrength, gravityWell)
            );
            for (int index = 0; index < defenderCount; index++)
                units.Add(
                    CreateUnit(
                        "defender-" + index,
                        CombatSide.Defender,
                        1000001 + index,
                        defenderStrength,
                        false
                    )
                );
            List<TacticalCombatGroup> groups = units.ConvertAll(unit => new TacticalCombatGroup(
                unit.Index + 100,
                new[] { unit },
                unit.Side == CombatSide.Attacker ? Vector3.UnitZ : -Vector3.UnitZ
            ));
            TacticalCombatSide attacker = new(
                CombatSide.Attacker,
                0,
                Vector3.UnitZ,
                true,
                0,
                .9f,
                null,
                null,
                0,
                0,
                false
            );
            TacticalCombatSide defender = new(
                CombatSide.Defender,
                1,
                -Vector3.UnitZ,
                true,
                0,
                .9f,
                null,
                null,
                0,
                0,
                true
            );
            return new TacticalCombatSession(
                units,
                groups,
                attacker,
                defender,
                CombatSide.Attacker,
                100,
                new TacticalCombatRandom(42)
            );
        }

        /// <summary>
        /// Captures a durable capital ship with one directional weapon category.
        /// </summary>
        /// <param name="id">The campaign identity.</param>
        /// <param name="side">The deployed side.</param>
        /// <param name="index">The battle registration index.</param>
        /// <param name="strength">The battery and recharge strength.</param>
        /// <param name="gravityWell">Whether the ship projects a gravity well.</param>
        /// <returns>The deployed tactical unit.</returns>
        private static TacticalCombatUnit CreateUnit(
            string id,
            CombatSide side,
            int index,
            int strength,
            bool gravityWell
        )
        {
            CapitalShip ship = new()
            {
                InstanceID = id,
                MaxHullStrength = 100000,
                CurrentHullStrength = 100000,
                Hyperdrive = 1,
                SublightSpeed = 4,
                Maneuverability = 1,
                WeaponRecharge = strength,
                HasGravityWell = gravityWell,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[]
            {
                strength,
                strength,
                strength,
                strength,
                100,
            };
            return new TacticalCombatUnit(ship, side, index)
            {
                Position = new Vector3(0, 0, side == CombatSide.Attacker ? -40 : 40),
                Heading = side == CombatSide.Attacker ? Vector3.UnitZ : -Vector3.UnitZ,
            };
        }

        /// <summary>
        /// Advances through initial deployment, automation, and delayed command application.
        /// </summary>
        /// <param name="session">The encounter under test.</param>
        private static void Advance(TacticalCombatSession session)
        {
            for (int frame = 0; frame < 200; frame++)
                session.Advance(50);
        }
    }
}
