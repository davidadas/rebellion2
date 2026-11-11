using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatInspectionTests
    {
        [TestCase(0d, 0)]
        [TestCase(0.009d, 0)]
        [TestCase(0.01d, 1)]
        [TestCase(0.249d, 1)]
        [TestCase(0.25d, 2)]
        [TestCase(0.499d, 2)]
        [TestCase(0.5d, 3)]
        [TestCase(0.749d, 3)]
        [TestCase(0.75d, 4)]
        [TestCase(2d, 4)]
        [TestCase(-0.01d, 4)]
        [TestCase(double.NaN, 0)]
        public void Level_RatioAcrossUnsignedTruncatedThresholds_SelectsAuthoredImage(
            double ratio,
            int expected
        )
        {
            Assert.AreEqual(expected, TacticalCombatInspection.Level(ratio));
        }

        [Test]
        public void ReadUnit_PendingRegroup_UsesRequestedAssignmentAndAppliedRouting()
        {
            TacticalCombatUnit unit = Capital("first", CombatSide.Attacker, 1);
            TacticalCombatUnit target = Capital("target", CombatSide.Defender, 1000001);
            TacticalCombatSession session = Encounter(unit, target);
            TacticalCombatGroup applied = session.GetGroup(unit);
            applied.SetTarget(target);
            TacticalCombatGroup requested = session.CreateGroup(new[] { unit });
            requested.SetOrder(TacticalCombatOrder.LeftHook);
            session.SetObjectGroup(CombatSide.Attacker, 3, requested);

            TacticalCombatInspectionData data = new TacticalCombatInspection(session).ReadUnit(
                unit
            );

            Assert.AreEqual("Task Force # 4", data.Assignment);
            Assert.AreEqual("Attacking target", data.Orders);
            Assert.AreSame(applied, session.GetGroup(unit));
            Assert.AreSame(requested, session.GetRequestedGroup(unit));
        }

        [Test]
        public void ReadUnit_EnemyCategory_DoesNotExposeOpponentAssignmentOrOverwriteTactics()
        {
            TacticalCombatUnit unit = Capital("enemy", CombatSide.Defender, 1000001);
            TacticalCombatSession session = Encounter(unit);

            TacticalCombatInspectionData data = new TacticalCombatInspection(session).ReadUnit(
                unit
            );

            Assert.AreEqual("Task Force Unassigned", data.Assignment);
            Assert.AreEqual("Orders Unavailable", data.Orders);
            Assert.IsNull(data.Tactics);
        }

        [Test]
        public void ReadUnit_RequestedWaypointAndTarget_PrioritizesNavigationBeforeDispatch()
        {
            TacticalCombatUnit unit = Capital("first", CombatSide.Attacker, 1);
            TacticalCombatUnit target = Capital("target", CombatSide.Defender, 1000001);
            TacticalCombatSession session = Encounter(unit, target);
            TacticalCombatGroup group = session.GetGroup(unit);
            session.SetTarget(group, target);
            session.SetWaypoint(group, new Vector3(30, 0, 20));

            TacticalCombatInspectionData data = new TacticalCombatInspection(session).ReadUnit(
                unit
            );

            Assert.AreEqual("Navigating", data.Orders);
            Assert.IsFalse(group.HasWaypoints);
        }

        [Test]
        public void ReadUnit_DisruptedNavigation_DisplaysDisabled()
        {
            TacticalCombatUnit unit = Capital("first", CombatSide.Attacker, 1);
            TacticalCombatSession session = Encounter(unit);
            session.SetWaypoint(session.GetGroup(unit), new Vector3(30, 0, 20));
            unit.Systems.ApplyDisruption(0, 1);

            TacticalCombatInspectionData data = new TacticalCombatInspection(session).ReadUnit(
                unit
            );

            Assert.AreEqual("Disabled", data.Orders);
            Assert.AreEqual(0, data.Movement);
        }

        [Test]
        public void ReadUnit_EscortTarget_RetainsEscortLabel()
        {
            TacticalCombatUnit unit = Capital("first", CombatSide.Attacker, 1);
            TacticalCombatUnit target = Capital("target", CombatSide.Attacker, 2);
            TacticalCombatSession session = Encounter(unit, target);
            TacticalCombatGroup group = session.GetGroup(unit);
            group.SetOrder(TacticalCombatOrder.HoldFire);
            session.SetTarget(group, target);

            TacticalCombatInspectionData data = new TacticalCombatInspection(session).ReadUnit(
                unit
            );

            Assert.AreEqual("Escort target", data.Orders);
        }

        [Test]
        public void ReadFighters_MixedTypesAndZeroHull_CountsRequestedTypeAndEveryAppliedMember()
        {
            TacticalCombatUnit laterType = Fighter("later", 1, 31, 12, 8);
            TacticalCombatUnit firstType = Fighter("first", 2, 29, 12, 6);
            TacticalCombatUnit lost = Fighter("lost", 3, 29, 12, 12);
            TacticalCombatGroup group = new(
                10,
                new[] { laterType, firstType, lost },
                Vector3.UnitZ
            );
            TacticalCombatSession session = EncounterWithGroups(
                new[] { laterType, firstType, lost },
                new[] { group }
            );
            lost.Systems.Destroy();

            TacticalCombatInspectionData data = new TacticalCombatInspection(session).ReadFighters(
                group,
                2,
                0
            );

            Assert.AreEqual("Green Group", data.Name);
            Assert.AreEqual(29, data.PreviewIndex);
            Assert.AreEqual("X-wings: 2 15/36", data.FighterCounts);
        }

        [Test]
        public void ReadFighters_PendingMemberTransfer_SeparatesRequestedTypeFromAppliedTotals()
        {
            TacticalCombatUnit first = Fighter("first", 1, 29, 12, 6);
            TacticalCombatUnit second = Fighter("second", 2, 29, 12, 8);
            TacticalCombatGroup group = new(10, new[] { first, second }, Vector3.UnitZ);
            TacticalCombatSession session = EncounterWithGroups(
                new[] { first, second },
                new[] { group }
            );
            session.CreateGroup(new[] { first });

            TacticalCombatInspectionData data = new TacticalCombatInspection(session).ReadFighters(
                group,
                0,
                0
            );

            Assert.AreEqual("X-wings: 1 14/24", data.FighterCounts);
            Assert.AreEqual(2, group.Members.Count);
            Assert.AreEqual(1, group.RequestedMembers.Count);
        }

        [Test]
        public void ReadFighters_NoAddressedFactionType_LeavesPreviewAndCountsUnchanged()
        {
            TacticalCombatUnit unit = Fighter("first", 1, 29, 12, 12);
            TacticalCombatGroup group = new(10, new[] { unit }, Vector3.UnitZ);
            TacticalCombatSession session = EncounterWithGroups(new[] { unit }, new[] { group });

            TacticalCombatInspectionData data = new TacticalCombatInspection(session).ReadFighters(
                group,
                0,
                1
            );

            Assert.IsNull(data.PreviewIndex);
            Assert.IsNull(data.FighterCounts);
        }

        [Test]
        public void ReadFighters_RecoverOrder_DisplaysRecoverWithoutMovementDisabledOverride()
        {
            TacticalCombatUnit unit = Fighter("first", 1, 29, 12, 12);
            TacticalCombatGroup group = new(10, new[] { unit }, Vector3.UnitZ);
            TacticalCombatSession session = EncounterWithGroups(new[] { unit }, new[] { group });
            group.SetOrder(TacticalCombatOrder.Recover);
            unit.Systems.ApplyDisruption(0, 1);

            TacticalCombatInspectionData data = new TacticalCombatInspection(session).ReadFighters(
                group,
                0,
                0
            );

            Assert.AreEqual("Recover", data.Orders);
        }

        /// <summary>
        /// Captures an explicitly named capital with movement and system budgets.
        /// </summary>
        /// <param name="name">The participant's identity and display label.</param>
        /// <param name="side">The participating side.</param>
        /// <param name="index">The tactical registration key.</param>
        /// <returns>The captured participant.</returns>
        private static TacticalCombatUnit Capital(string name, CombatSide side, int index)
        {
            CapitalShip ship = new()
            {
                InstanceID = name,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                SublightSpeed = 10,
                Hyperdrive = 1,
            };
            ship.AssignName(name);
            return new(
                ship,
                side,
                index,
                new TacticalCombatUnitDefinition { PresentationIndex = 1 }
            );
        }

        /// <summary>
        /// Captures a fighter with explicit authored type and complete and remaining fighter pools.
        /// </summary>
        /// <param name="id">The squadron identity.</param>
        /// <param name="index">The registration key.</param>
        /// <param name="type">The presentation index.</param>
        /// <param name="maximum">The complete fighter count.</param>
        /// <param name="current">The remaining fighter count.</param>
        /// <returns>The captured squadron.</returns>
        private static TacticalCombatUnit Fighter(
            string id,
            int index,
            int type,
            int maximum,
            int current
        ) =>
            new(
                new Starfighter
                {
                    InstanceID = id,
                    MaxSquadronSize = maximum,
                    CurrentSquadronSize = current,
                },
                CombatSide.Attacker,
                index,
                new TacticalCombatUnitDefinition { PresentationIndex = type }
            );

        /// <summary>
        /// Builds one separate applied command group for each capital.
        /// </summary>
        /// <param name="units">The ordered participants.</param>
        /// <returns>The encounter retaining their applied ownership.</returns>
        private static TacticalCombatSession Encounter(params TacticalCombatUnit[] units)
        {
            TacticalCombatGroup[] groups = new TacticalCombatGroup[units.Length];
            for (int index = 0; index < units.Length; index++)
                groups[index] = new(10 + index, new[] { units[index] }, Vector3.UnitZ);
            return EncounterWithGroups(units, groups);
        }

        /// <summary>
        /// Builds an encounter with caller-specified applied groups and no command officers.
        /// </summary>
        /// <param name="units">The captured participants.</param>
        /// <param name="groups">Their deployed applied groups.</param>
        /// <returns>The manually controlled encounter.</returns>
        private static TacticalCombatSession EncounterWithGroups(
            TacticalCombatUnit[] units,
            TacticalCombatGroup[] groups
        ) =>
            new(
                units,
                groups,
                new(CombatSide.Attacker, 0, Vector3.UnitZ, true, 0, 0, null, null, 0, 0, false),
                new(CombatSide.Defender, 1, -Vector3.UnitZ, true, 0, 0, null, null, 0, 0, false),
                CombatSide.Attacker,
                100,
                new SystemRandomProvider(1)
            );
    }
}
