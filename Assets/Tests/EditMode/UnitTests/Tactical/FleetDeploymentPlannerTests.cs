using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using UnityEngine;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class FleetDeploymentPlannerTests
    {
        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void Deploy_MixedFleet_KeepsHullsInsideRegionWithoutOverlap(float yaw)
        {
            ActiveBattle battle = CreateBattle(30, 4, 12);
            BattleMap map = CreateMap(yaw);

            FleetDeploymentPlanner.Deploy(battle, map);

            AssertContainedAndSeparated(
                battle.GetParticipants().Single(),
                map.GetDeploymentRegions().Single().Bounds
            );
        }

        [TestCase(0f)]
        [TestCase(180f)]
        public void Deploy_MixedFleet_PlacesWholeFighterScreenAheadOfCapitalHulls(float yaw)
        {
            ActiveBattle battle = CreateBattle(25, 6, 12);
            BattleMap map = CreateMap(yaw);

            FleetDeploymentPlanner.Deploy(battle, map);

            CombatUnit[] capitals = GetCombatants(battle)
                .Where(unit => unit.Data.Kind == CombatUnitKind.CapitalShip)
                .ToArray();
            CombatUnit[] fighters = GetCombatants(battle)
                .Where(unit => unit.Data.Kind == CombatUnitKind.Starfighter)
                .ToArray();
            Vector3 forward = ToVector(capitals[0].Forward);
            float capitalFront = capitals.Max(unit =>
                Vector3.Dot(ToVector(unit.Position), forward) + unit.Data.ModelSize.Depth / 2f
            );
            float fighterRear = fighters.Min(unit =>
                Vector3.Dot(ToVector(unit.Position), forward) - unit.Data.ModelSize.Depth / 2f
            );
            Assert.That(fighterRear - capitalFront, Is.GreaterThanOrEqualTo(18f));
        }

        [Test]
        public void Deploy_WideMixedFleet_StacksUnitClassesIntoVerticalRanks()
        {
            ActiveBattle battle = CreateBattle(5, 4, 12);

            FleetDeploymentPlanner.Deploy(battle, CreateMap());

            CombatUnit[] capitals = GetCombatants(battle)
                .Where(unit => unit.Data.Kind == CombatUnitKind.CapitalShip)
                .ToArray();
            CombatUnit[] fighters = GetCombatants(battle)
                .Where(unit => unit.Data.Kind == CombatUnitKind.Starfighter)
                .ToArray();
            float capitalDepth = capitals[0].Position.Z;
            float fighterDepth = fighters[0].Position.Z;
            Assert.That(fighterDepth, Is.GreaterThan(capitalDepth));
            Assert.That(capitals.Select(unit => unit.Position.Y).Distinct().Count(), Is.EqualTo(2));
            Assert.That(
                fighters.Select(unit => unit.Position.Y).Distinct().Count(),
                Is.GreaterThan(1)
            );
            Assert.That(capitals.Select(unit => unit.Position.Z).Distinct().Count(), Is.EqualTo(1));
            Assert.That(fighters.Select(unit => unit.Position.Z).Distinct().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Deploy_ReorderedCombatants_PreservesPositions()
        {
            ActiveBattle battle = CreateBattle(20, 4, 12);
            BattleMap map = CreateMap();
            FleetDeploymentPlanner.Deploy(battle, map);
            Dictionary<string, Vector3> expected = GetCombatants(battle)
                .ToDictionary(unit => unit.InstanceID, unit => ToVector(unit.Position));
            battle.GetParticipants().Single().GetCombatants().Reverse();

            FleetDeploymentPlanner.Deploy(battle, map);

            foreach (CombatUnit unit in GetCombatants(battle))
                Assert.That(ToVector(unit.Position), Is.EqualTo(expected[unit.InstanceID]));
        }

        [Test]
        public void Deploy_MultipleSquadrons_KeepsEachSquadronTogether()
        {
            ActiveBattle battle = CreateBattle(2, 4, 12);
            FleetDeploymentPlanner.Deploy(battle, CreateMap());
            Bounds[] groups = GetCombatants(battle)
                .Where(unit => unit.Data.Kind == CombatUnitKind.Starfighter)
                .GroupBy(unit => unit.SourceUnitInstanceID)
                .Select(group => GetGroupBounds(group))
                .ToArray();

            foreach (Bounds group in groups)
                Assert.That(group.size.magnitude, Is.LessThan(15f));
            for (int first = 0; first < groups.Length; first++)
            for (int second = first + 1; second < groups.Length; second++)
                Assert.IsFalse(groups[first].Intersects(groups[second]));
        }

        [Test]
        public void Deploy_NarrowDeepRegion_UsesMultipleDepthLayers()
        {
            ActiveBattle battle = CreateBattle(12, 0, 0);
            BattleMap map = CreateMap();
            map.GetDeploymentRegions()[0].Bounds = new BattleMapBounds
            {
                MinimumX = -15f,
                MaximumX = 15f,
                MinimumY = -10f,
                MaximumY = 10f,
                MinimumZ = -200f,
                MaximumZ = 200f,
            };

            FleetDeploymentPlanner.Deploy(battle, map);

            Assert.That(
                GetCombatants(battle).Select(unit => unit.Position.Z).Distinct().Count(),
                Is.GreaterThan(1)
            );
            Assert.That(
                GetCombatants(battle).Select(unit => unit.Position.Y).Distinct().Count(),
                Is.GreaterThan(1)
            );
            AssertContainedAndSeparated(
                battle.GetParticipants().Single(),
                map.GetDeploymentRegions().Single().Bounds
            );
        }

        [Test]
        public void Deploy_MissingDimensions_RejectsInsteadOfInventingSize()
        {
            ActiveBattle battle = CreateBattle(1, 0, 0);
            GetCombatants(battle).Single().Data.ModelSize = null;

            Assert.Throws<ArgumentException>(() =>
                FleetDeploymentPlanner.Deploy(battle, CreateMap())
            );
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Deploy_InvalidDimensions_RejectsWithoutMutation(float width)
        {
            ActiveBattle battle = CreateBattle(2, 0, 0);
            CombatUnit unit = GetCombatants(battle).Last();
            unit.Data.ModelSize.Width = width;
            BattleVector3 initial = unit.Position;

            Assert.Throws<ArgumentException>(() =>
                FleetDeploymentPlanner.Deploy(battle, CreateMap())
            );
            Assert.AreSame(initial, unit.Position);
        }

        [Test]
        public void Deploy_SecondParticipantOverflows_LeavesAllPositionsUntouched()
        {
            ActiveBattle battle = CreateBattle(2, 0, 0);
            battle.AddCombatants(
                new CapitalShip
                {
                    OwnerInstanceID = "SECOND",
                    ModelSize = new ModelDimensions
                    {
                        Width = 2000f,
                        Height = 2f,
                        Depth = 2f,
                    },
                }
            );
            battle.GetParticipants()[1].BattleMapSlotID = "second";
            BattleMap map = CreateMap();
            map.GetDeploymentRegions()
                .Add(
                    new BattleMapDeploymentRegion
                    {
                        ParticipantSlotID = "second",
                        Bounds = map.GetDeploymentRegions()[0].Bounds,
                    }
                );
            BattleVector3[] initial = GetCombatants(battle).Select(unit => unit.Position).ToArray();

            Assert.Throws<InvalidOperationException>(() =>
                FleetDeploymentPlanner.Deploy(battle, map)
            );

            CollectionAssert.AreEqual(initial, GetCombatants(battle).Select(unit => unit.Position));
        }

        [Test]
        public void Deploy_OverlappingParticipantHulls_RejectsDeployment()
        {
            ActiveBattle battle = CreateBattle(1, 0, 0);
            battle.AddCombatants(
                new CapitalShip
                {
                    OwnerInstanceID = "SECOND",
                    ModelSize = new ModelDimensions
                    {
                        Width = 20f,
                        Height = 4f,
                        Depth = 30f,
                    },
                }
            );
            battle.GetParticipants()[1].BattleMapSlotID = "second";
            BattleMap map = CreateMap();
            map.GetDeploymentRegions()
                .Add(
                    new BattleMapDeploymentRegion
                    {
                        ParticipantSlotID = "second",
                        Bounds = map.GetDeploymentRegions()[0].Bounds,
                    }
                );

            Assert.Throws<InvalidOperationException>(() =>
                FleetDeploymentPlanner.Deploy(battle, map)
            );
        }

        [Test]
        public void Deploy_MismatchedMap_RejectsDeployment()
        {
            BattleMap map = CreateMap();
            map.InstanceID = "OTHER";

            Assert.Throws<ArgumentException>(() =>
                FleetDeploymentPlanner.Deploy(CreateBattle(1, 0, 0), map)
            );
        }

        /// <summary>
        /// Checks setup performance against an explicit fleet of fourteen hundred tactical units.
        /// </summary>
        [Test]
        public void Deploy_LargeMixedFleet_CompletesDeploymentWithinSetupBudget()
        {
            ActiveBattle battle = CreateBattle(200, 1200, 12);
            Stopwatch timer = Stopwatch.StartNew();

            FleetDeploymentPlanner.Deploy(battle, CreateMap());

            timer.Stop();
            TestContext.WriteLine(
                $"Deploying 200 capitals and 1200 squadrons: {timer.Elapsed.TotalMilliseconds:F2} ms"
            );
            Assert.That(GetCombatants(battle).Count(), Is.EqualTo(1400));
            Assert.That(timer.Elapsed.TotalSeconds, Is.LessThan(5d));
        }

        /// <summary>
        /// Creates synthetic combatants with stable identities and varied dimensions.
        /// </summary>
        /// <param name="capitals">The capital ship count.</param>
        /// <param name="squadrons">The squadron count.</param>
        /// <param name="fighters">The fighters per squadron.</param>
        /// <returns>The undeployed battle.</returns>
        private static ActiveBattle CreateBattle(int capitals, int squadrons, int fighters)
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "MAP",
                Kind = BattleKind.Space,
            };
            for (int index = 0; index < capitals; index++)
                battle.AddCombatants(
                    new CapitalShip
                    {
                        InstanceID = $"CAPITAL-{index:D4}",
                        OwnerInstanceID = "FACTION",
                        ModelSize = new ModelDimensions
                        {
                            Width = 8f + index % 3,
                            Height = 3f + index % 2,
                            Depth = 15f + index % 5,
                        },
                    }
                );
            for (int index = 0; index < squadrons; index++)
                battle.AddCombatants(
                    new Starfighter
                    {
                        InstanceID = $"SQUADRON-{index:D4}",
                        OwnerInstanceID = "FACTION",
                        CurrentSquadronSize = fighters,
                        ModelSize = new ModelDimensions
                        {
                            Width = 1f,
                            Height = 0.5f,
                            Depth = 1.5f,
                        },
                    }
                );
            battle.GetParticipants().Single().BattleMapSlotID = "fleet";
            int identity = 0;
            foreach (CombatUnit unit in GetCombatants(battle))
                unit.InstanceID = $"COMBATANT-{identity++:D5}";
            return battle;
        }

        /// <summary>
        /// Creates a bounded map for a single participant.
        /// </summary>
        /// <param name="yaw">The heading in degrees.</param>
        /// <returns>The map definition.</returns>
        private static BattleMap CreateMap(float yaw = 0f)
        {
            BattleMap map = new BattleMap
            {
                InstanceID = "MAP",
                Kind = BattleKind.Space,
                PlayableBounds = new BattleMapBounds
                {
                    MinimumX = -1000f,
                    MaximumX = 1000f,
                    MinimumY = -500f,
                    MaximumY = 500f,
                    MinimumZ = -1000f,
                    MaximumZ = 1000f,
                },
            };
            map.GetDeploymentRegions()
                .Add(
                    new BattleMapDeploymentRegion
                    {
                        ParticipantSlotID = "fleet",
                        Facing = new BattleMapVector3 { Y = yaw },
                        Bounds = new BattleMapBounds
                        {
                            MinimumX = -500f,
                            MaximumX = 500f,
                            MinimumY = -200f,
                            MaximumY = 200f,
                            MinimumZ = -200f,
                            MaximumZ = 200f,
                        },
                    }
                );
            return map;
        }

        /// <summary>
        /// Enumerates combatants without changing their participant ownership.
        /// </summary>
        /// <param name="battle">The source battle.</param>
        /// <returns>The combatants.</returns>
        private static IEnumerable<CombatUnit> GetCombatants(ActiveBattle battle) =>
            battle.GetParticipants().SelectMany(participant => participant.GetCombatants());

        /// <summary>
        /// Measures a ship in the same heading frame used by presentation.
        /// </summary>
        /// <param name="unit">The deployed combatant.</param>
        /// <returns>The world-aligned hull envelope.</returns>
        private static Bounds GetHull(CombatUnit unit)
        {
            ModelDimensions size = unit.Data.ModelSize;
            Quaternion rotation = Quaternion.LookRotation(ToVector(unit.Forward));
            Vector3 right = rotation * Vector3.right * size.Width;
            Vector3 up = rotation * Vector3.up * size.Height;
            Vector3 forward = rotation * Vector3.forward * size.Depth;
            Vector3 dimensions = new Vector3(
                Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(forward.x),
                Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(forward.y),
                Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(forward.z)
            );
            return new Bounds(ToVector(unit.Position), dimensions);
        }

        /// <summary>
        /// Measures the complete hull envelope of a squadron.
        /// </summary>
        /// <param name="units">The squadron members.</param>
        /// <returns>The combined envelope.</returns>
        private static Bounds GetGroupBounds(IEnumerable<CombatUnit> units)
        {
            Bounds result = GetHull(units.First());
            foreach (CombatUnit unit in units.Skip(1))
                result.Encapsulate(GetHull(unit));
            return result;
        }

        /// <summary>
        /// Checks containment and oriented-box separation independently of the planner.
        /// </summary>
        /// <param name="participant">The deployed participant.</param>
        /// <param name="region">The containing map region.</param>
        private static void AssertContainedAndSeparated(
            BattleParticipant participant,
            BattleMapBounds region
        )
        {
            List<CombatUnit> units = participant.GetCombatants();
            Vector3 min = new Vector3(region.MinimumX, region.MinimumY, region.MinimumZ);
            Vector3 max = new Vector3(region.MaximumX, region.MaximumY, region.MaximumZ);
            for (int first = 0; first < units.Count; first++)
            {
                Bounds hull = GetHull(units[first]);
                Assert.That(hull.min.x, Is.GreaterThanOrEqualTo(min.x - 0.001f));
                Assert.That(hull.min.y, Is.GreaterThanOrEqualTo(min.y - 0.001f));
                Assert.That(hull.min.z, Is.GreaterThanOrEqualTo(min.z - 0.001f));
                Assert.That(hull.max.x, Is.LessThanOrEqualTo(max.x + 0.001f));
                Assert.That(hull.max.y, Is.LessThanOrEqualTo(max.y + 0.001f));
                Assert.That(hull.max.z, Is.LessThanOrEqualTo(max.z + 0.001f));
                Quaternion inverse = Quaternion.Inverse(
                    Quaternion.LookRotation(ToVector(units[first].Forward))
                );
                ModelDimensions size = units[first].Data.ModelSize;
                for (int second = first + 1; second < units.Count; second++)
                {
                    Vector3 delta =
                        inverse
                        * (ToVector(units[second].Position) - ToVector(units[first].Position));
                    ModelDimensions other = units[second].Data.ModelSize;
                    Assert.IsTrue(
                        Mathf.Abs(delta.x) >= (size.Width + other.Width) / 2f - 0.001f
                            || Mathf.Abs(delta.y) >= (size.Height + other.Height) / 2f - 0.001f
                            || Mathf.Abs(delta.z) >= (size.Depth + other.Depth) / 2f - 0.001f,
                        $"Hull overlap: {units[first].InstanceID}, {units[second].InstanceID}"
                    );
                }
            }
        }

        /// <summary>
        /// Converts saved battle coordinates for geometric assertions.
        /// </summary>
        /// <param name="value">The saved vector.</param>
        /// <returns>The Unity vector.</returns>
        private static Vector3 ToVector(BattleVector3 value) =>
            new Vector3(value.X, value.Y, value.Z);
    }
}
