using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatNavigationTests
    {
        [Test]
        public void GetNearestWaypoint_StoredThresholdTie_RetainsDistanceUntilComparison()
        {
            TacticalCombatNavigation navigation = new(8);
            Vector3 position = new(
                BitConverter.Int32BitsToSingle(0x3f00000e),
                BitConverter.Int32BitsToSingle(0x3fbffffa),
                3.5f
            );

            Vector3 selected = navigation.GetNearestWaypoint(position);

            Assert.AreEqual(new Vector3(0, 0, 4), selected);
        }

        [Test]
        public void GetNearestWaypoint_UnorderedDistance_SelectsFinalRegisteredPoint()
        {
            TacticalCombatNavigation navigation = new(8);

            Vector3 selected = navigation.GetNearestWaypoint(new Vector3(float.NaN, 0, 0));

            Assert.AreEqual(new Vector3(4, 4, 4), selected);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryGetManeuver_NegativeCapitalProjection_SelectsCapitalWithoutStation(
            bool stationPresent
        )
        {
            TacticalCombatUnit own = CreateUnit("own", CombatSide.Attacker, 1);
            TacticalCombatUnit enemy = CreateUnit("enemy", CombatSide.Defender, 1000001);
            TacticalCombatGroup group = new(1, new[] { own }, Vector3.UnitZ);
            List<TacticalCombatUnit> units = new() { own, enemy };
            List<TacticalCombatGroup> groups = new()
            {
                group,
                new TacticalCombatGroup(2, new[] { enemy }, -Vector3.UnitZ),
            };
            TacticalCombatUnit station = null;
            if (stationPresent)
            {
                station = new TacticalCombatUnit(
                    new CapitalShip
                    {
                        InstanceID = "station",
                        CanDestroyPlanets = true,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                        MaxHullStrength = 100,
                        CurrentHullStrength = 100,
                    },
                    CombatSide.Defender,
                    1000002
                );
                units.Add(station);
                groups.Add(new TacticalCombatGroup(3, new[] { station }, -Vector3.UnitZ));
            }
            TacticalCombatSession session = new(
                units,
                groups,
                CreateSide(CombatSide.Attacker),
                CreateSide(CombatSide.Defender),
                CombatSide.Attacker,
                80,
                new TacticalCombatRandom(42)
            );
            session.SetOrder(group, TacticalCombatOrder.LeftHook);
            for (
                int frame = 0;
                frame < 240 && group.ActiveOrder != TacticalCombatOrder.LeftHook;
                frame++
            )
                session.Advance(1000f / 60);
            Assert.AreEqual(TacticalCombatOrder.LeftHook, group.ActiveOrder);
            group.Formation.SetPosition(Vector3.Zero);
            enemy.Position = new Vector3(20, 0, 0);
            if (station != null)
                station.Position = new Vector3(-20, 0, 0);

            bool found = session.Navigation.TryGetManeuver(
                group,
                units,
                Vector3.UnitZ,
                out Vector3 waypoint
            );

            Assert.IsTrue(found);
            Assert.AreEqual(new Vector3(10, 0, 10), waypoint);
        }

        /// <summary>
        /// Creates an active unarmed capital for isolated navigation selection.
        /// </summary>
        /// <param name="id">The battle participant's identity.</param>
        /// <param name="side">The participant's side.</param>
        /// <param name="index">The registration ordering.</param>
        /// <returns>The captured participant.</returns>
        private static TacticalCombatUnit CreateUnit(string id, CombatSide side, int index) =>
            new(
                new CapitalShip
                {
                    InstanceID = id,
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                },
                side,
                index
            );

        /// <summary>
        /// Creates a manually controlled side with no retreat destination.
        /// </summary>
        /// <param name="side">The tactical side.</param>
        /// <returns>The side's command state.</returns>
        private static TacticalCombatSide CreateSide(CombatSide side) =>
            new(
                side,
                (int)side,
                side == CombatSide.Attacker ? Vector3.UnitZ : -Vector3.UnitZ,
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
