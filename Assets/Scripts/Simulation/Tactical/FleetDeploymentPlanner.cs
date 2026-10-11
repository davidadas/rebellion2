using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation.Tactical
{
    /// <summary>
    /// Places a new battle's fleets inside their assigned map regions before presentation begins.
    /// </summary>
    public static class FleetDeploymentPlanner
    {
        private const float _relativeClearance = 0.18f;
        private const float _minimumClearance = 0.12f;
        private const float _maximumClearance = 8f;
        private const float _screenClearance = 18f;

        /// <summary>
        /// Deploys all participants atomically, keeping whole fighter squadrons ahead of capital hulls.
        /// Call this only when creating a battle; existing battle positions are saved state.
        /// </summary>
        /// <param name="battle">The newly created space battle.</param>
        /// <param name="map">The map with assigned deployment regions.</param>
        public static void Deploy(ActiveBattle battle, BattleMap map)
        {
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (
                battle.Kind != BattleKind.Space
                || map.Kind != battle.Kind
                || !string.Equals(
                    battle.BattleMapInstanceID,
                    map.InstanceID,
                    StringComparison.Ordinal
                )
            )
                throw new ArgumentException(
                    "Deployment requires the battle's matching space map.",
                    nameof(map)
                );

            List<Placement> placements = new List<Placement>();
            HashSet<string> usedSlots = new HashSet<string>(StringComparer.Ordinal);
            foreach (BattleParticipant participant in battle.GetParticipants())
            {
                BattleMapDeploymentRegion region = null;
                foreach (BattleMapDeploymentRegion candidate in map.GetDeploymentRegions())
                {
                    if (
                        !string.Equals(
                            candidate.ParticipantSlotID,
                            participant.BattleMapSlotID,
                            StringComparison.Ordinal
                        )
                    )
                        continue;
                    if (region != null)
                        throw new ArgumentException(
                            "Deployment slot identifiers must be unique.",
                            nameof(map)
                        );
                    region = candidate;
                }
                if (region == null || !usedSlots.Add(region.ParticipantSlotID))
                    throw new ArgumentException(
                        "Each participant requires its own deployment region.",
                        nameof(battle)
                    );
                PlanParticipant(participant, region, map.PlayableBounds, placements);
            }

            ValidateSeparation(placements);
            foreach (Placement placement in placements)
            {
                placement.Combatant.Position = ToBattleVector(placement.Position);
                placement.Combatant.Forward = ToBattleVector(placement.Forward);
            }
        }

        /// <summary>Plans one participant in the local frame of its authored facing.</summary>
        /// <param name="participant">The participant to deploy.</param>
        /// <param name="region">The assigned region.</param>
        /// <param name="playableBounds">The map's outer playable bounds.</param>
        /// <param name="placements">The complete pending deployment.</param>
        private static void PlanParticipant(
            BattleParticipant participant,
            BattleMapDeploymentRegion region,
            BattleMapBounds playableBounds,
            List<Placement> placements
        )
        {
            ValidateBounds(region.Bounds);
            ValidateBounds(playableBounds);
            if (
                region.Facing == null
                || !IsFinite(region.Facing.X)
                || !IsFinite(region.Facing.Y)
                || !IsFinite(region.Facing.Z)
            )
                throw new ArgumentException("Deployment facing must be finite.", nameof(region));
            Vector3 minimum = Vector3.Max(GetMinimum(region.Bounds), GetMinimum(playableBounds));
            Vector3 maximum = Vector3.Min(GetMaximum(region.Bounds), GetMaximum(playableBounds));
            Vector3 worldSize = maximum - minimum;
            if (worldSize.X <= 0f || worldSize.Y <= 0f || worldSize.Z <= 0f)
                throw new ArgumentException(
                    "Deployment region must intersect the playable volume.",
                    nameof(region)
                );
            Quaternion rotation = Quaternion.CreateFromYawPitchRoll(
                region.Facing.Y * (float)Math.PI / 180f,
                region.Facing.X * (float)Math.PI / 180f,
                0f
            );
            Vector3 right = Vector3.Transform(Vector3.UnitX, rotation);
            Vector3 up = Vector3.Transform(Vector3.UnitY, rotation);
            Vector3 forward = Vector3.Transform(Vector3.UnitZ, rotation);
            // CombatUnit stores a heading, not roll. Match the renderer's heading-based orientation.
            Vector3 localSize = new Vector3(
                Vector3.Dot(Vector3.Abs(right), worldSize),
                Vector3.Dot(Vector3.Abs(up), worldSize),
                Vector3.Dot(Vector3.Abs(forward), worldSize)
            );
            Vector3 rotatedSize = RotateSize(localSize, right, up, forward);
            float fitScale = Math.Min(
                worldSize.X / rotatedSize.X,
                Math.Min(worldSize.Y / rotatedSize.Y, worldSize.Z / rotatedSize.Z)
            );
            // An inscribed local volume keeps every rotated hull within the world-aligned map region.
            Vector3 capacity = localSize * fitScale;
            List<Group> capitals = new List<Group>();
            Dictionary<string, Group> squadrons = new Dictionary<string, Group>(
                StringComparer.Ordinal
            );
            foreach (CombatUnit combatant in participant.GetCombatants())
            {
                BaseSceneNode unit = combatant?.GetUnit();
                if (unit is not CapitalShip && unit is not Starfighter)
                    throw new ArgumentException(
                        "Deployment requires capital ships or fighters.",
                        nameof(participant)
                    );
                ModelDimensions dimensions = unit.ModelSize;
                if (
                    dimensions == null
                    || !IsPositive(dimensions.Width)
                    || !IsPositive(dimensions.Height)
                    || !IsPositive(dimensions.Depth)
                )
                    throw new ArgumentException(
                        $"Unit '{unit.TypeID}' requires finite, positive ModelSize dimensions.",
                        nameof(participant)
                    );
                Vector3 size = new Vector3(dimensions.Width, dimensions.Height, dimensions.Depth);
                float clearance = Math.Clamp(
                    Math.Max(size.X, Math.Max(size.Y, size.Z)) * _relativeClearance,
                    _minimumClearance,
                    _maximumClearance
                );
                Ship ship = new Ship(combatant, size + new Vector3(clearance * 2f));
                if (unit is CapitalShip)
                    capitals.Add(new Group(combatant.InstanceID, new List<Ship> { ship }));
                else
                {
                    string squadronId = string.IsNullOrWhiteSpace(combatant.SourceUnitInstanceID)
                        ? combatant.InstanceID
                        : combatant.SourceUnitInstanceID;
                    if (!squadrons.TryGetValue(squadronId, out Group squadron))
                    {
                        squadron = new Group(squadronId, new List<Ship>());
                        squadrons.Add(squadronId, squadron);
                    }
                    squadron.Ships.Add(ship);
                }
            }

            List<Group> fighters = new List<Group>(squadrons.Values);
            PrepareGroups(capitals, capacity);
            PrepareGroups(fighters, capacity);
            Layout core = Pack(GetSizes(capitals), capacity);
            Layout screen = Pack(GetSizes(fighters), capacity);
            float gap = capitals.Count > 0 && fighters.Count > 0 ? _screenClearance : 0f;
            float totalDepth = core.Size.Z + screen.Size.Z + gap;
            if (totalDepth > capacity.Z)
                throw new InvalidOperationException(
                    $"Fleet cannot fit deployment region '{region.ParticipantSlotID}' with fighters ahead of capitals."
                );
            Vector3 center = (minimum + maximum) * 0.5f;
            AddPlacements(
                capitals,
                core,
                new Vector3(0f, 0f, (core.Size.Z - totalDepth) * 0.5f),
                center,
                right,
                up,
                forward,
                placements
            );
            AddPlacements(
                fighters,
                screen,
                new Vector3(0f, 0f, (totalDepth - screen.Size.Z) * 0.5f),
                center,
                right,
                up,
                forward,
                placements
            );
        }

        /// <summary>Packs squadron members and orders groups by size with stable identity tie breaks.</summary>
        /// <param name="groups">The groups to prepare.</param>
        /// <param name="capacity">The available local volume.</param>
        private static void PrepareGroups(List<Group> groups, Vector3 capacity)
        {
            foreach (Group group in groups)
            {
                group.Ships.Sort(
                    (left, right) =>
                        CompareSize(
                            left.Size,
                            right.Size,
                            left.Combatant.InstanceID,
                            right.Combatant.InstanceID
                        )
                );
                List<Vector3> sizes = new List<Vector3>(group.Ships.Count);
                foreach (Ship ship in group.Ships)
                    sizes.Add(ship.Size);
                group.Layout = Pack(sizes, capacity);
            }
            groups.Sort(
                (left, right) => CompareSize(left.Layout.Size, right.Layout.Size, left.Id, right.Id)
            );
        }

        /// <summary>Returns group envelopes in their stable placement order.</summary>
        /// <param name="groups">The groups to measure.</param>
        /// <returns>The occupied sizes.</returns>
        private static List<Vector3> GetSizes(List<Group> groups)
        {
            List<Vector3> sizes = new List<Vector3>(groups.Count);
            foreach (Group group in groups)
                sizes.Add(group.Layout.Size);
            return sizes;
        }

        /// <summary>
        /// Searches dimension-aware rows and depth layers, preferring shallow, balanced formations.
        /// Measures candidates without allocating positions and builds only the winning arrangement.
        /// </summary>
        /// <param name="sizes">The occupied item sizes in stable size order.</param>
        /// <param name="capacity">The available local dimensions.</param>
        /// <returns>A centered layout containing every item.</returns>
        private static Layout Pack(List<Vector3> sizes, Vector3 capacity)
        {
            if (sizes.Count == 0)
                return new Layout(Vector3.Zero, Array.Empty<Vector3>());
            int bestColumns = 0;
            int bestRows = 0;
            Vector3 bestSize = Vector3.Zero;
            float bestBalance = float.PositiveInfinity;
            for (int columns = 1; columns <= sizes.Count; columns++)
            {
                int maximumRows = (sizes.Count + columns - 1) / columns;
                for (int rows = 1; rows <= maximumRows; rows++)
                {
                    Vector3 size = Measure(sizes, columns, rows);
                    if (size.X > capacity.X || size.Y > capacity.Y || size.Z > capacity.Z)
                        continue;
                    float balance = Math.Max(size.X / capacity.X, size.Y / capacity.Y);
                    if (
                        bestColumns != 0
                        && (size.Z > bestSize.Z || (size.Z == bestSize.Z && balance >= bestBalance))
                    )
                        continue;
                    bestColumns = columns;
                    bestRows = rows;
                    bestSize = size;
                    bestBalance = balance;
                }
            }
            if (bestColumns == 0)
                throw new InvalidOperationException(
                    "Fleet or squadron cannot fit the assigned deployment volume."
                );
            return BuildLayout(sizes, bestColumns, bestRows, bestSize);
        }

        /// <summary>Measures actual row widths, row heights, and layer depths.</summary>
        /// <param name="sizes">The occupied item sizes.</param>
        /// <param name="columns">The maximum items per row.</param>
        /// <param name="rows">The maximum rows per depth layer.</param>
        /// <returns>The envelope dimensions.</returns>
        private static Vector3 Measure(List<Vector3> sizes, int columns, int rows)
        {
            Vector3 result = Vector3.Zero;
            for (int layerStart = 0; layerStart < sizes.Count; )
            {
                float layerHeight = 0f;
                float layerDepth = 0f;
                for (int row = 0; row < rows && layerStart < sizes.Count; row++)
                {
                    Vector3 rowSize = Vector3.Zero;
                    for (
                        int column = 0;
                        column < columns && layerStart < sizes.Count;
                        column++, layerStart++
                    )
                    {
                        Vector3 size = sizes[layerStart];
                        rowSize.X += size.X;
                        rowSize.Y = Math.Max(rowSize.Y, size.Y);
                        rowSize.Z = Math.Max(rowSize.Z, size.Z);
                    }
                    result.X = Math.Max(result.X, rowSize.X);
                    layerHeight += rowSize.Y;
                    layerDepth = Math.Max(layerDepth, rowSize.Z);
                }
                result.Y = Math.Max(result.Y, layerHeight);
                result.Z += layerDepth;
            }
            return result;
        }

        /// <summary>Builds the winning rows with larger items and rows nearest the center.</summary>
        /// <param name="sizes">The occupied item sizes.</param>
        /// <param name="columns">The items per row.</param>
        /// <param name="rows">The rows per depth layer.</param>
        /// <param name="size">The measured envelope.</param>
        /// <returns>The centered placements.</returns>
        private static Layout BuildLayout(List<Vector3> sizes, int columns, int rows, Vector3 size)
        {
            Vector3[] positions = new Vector3[sizes.Count];
            float z = -size.Z * 0.5f;
            for (int start = 0; start < sizes.Count; )
            {
                int count = Math.Min(sizes.Count - start, columns * rows);
                int rowCount = (count + columns - 1) / columns;
                float[] heights = new float[rowCount];
                float depth = 0f;
                for (int index = 0; index < count; index++)
                {
                    heights[index / columns] = Math.Max(
                        heights[index / columns],
                        sizes[start + index].Y
                    );
                    depth = Math.Max(depth, sizes[start + index].Z);
                }
                float[] yCenters = GetCenters(heights);
                for (int row = 0; row < rowCount; row++)
                {
                    int rowStart = start + row * columns;
                    int rowSize = Math.Min(columns, start + count - rowStart);
                    float[] widths = new float[rowSize];
                    for (int column = 0; column < rowSize; column++)
                        widths[column] = sizes[rowStart + column].X;
                    float[] xCenters = GetCenters(widths);
                    for (int column = 0; column < rowSize; column++)
                        positions[rowStart + column] = new Vector3(
                            xCenters[column],
                            yCenters[row],
                            z + depth * 0.5f
                        );
                }
                z += depth;
                start += count;
            }
            return new Layout(size, positions);
        }

        /// <summary>Places variable-width intervals from the center outward without overlap.</summary>
        /// <param name="lengths">The lengths ordered from largest priority to smallest.</param>
        /// <returns>The interval centers in the original order.</returns>
        private static float[] GetCenters(float[] lengths)
        {
            float[] centers = new float[lengths.Length];
            float minimum = -lengths[0] * 0.5f;
            float maximum = lengths[0] * 0.5f;
            for (int index = 1; index < lengths.Length; index++)
            {
                if (index % 2 == 1)
                {
                    centers[index] = maximum + lengths[index] * 0.5f;
                    maximum += lengths[index];
                }
                else
                {
                    centers[index] = minimum - lengths[index] * 0.5f;
                    minimum -= lengths[index];
                }
            }
            float center = (minimum + maximum) * 0.5f;
            for (int index = 0; index < centers.Length; index++)
                centers[index] -= center;
            return centers;
        }

        /// <summary>Transforms group and member positions into battle space.</summary>
        /// <param name="groups">The packed groups.</param>
        /// <param name="layout">The group arrangement.</param>
        /// <param name="offset">The core or screen offset.</param>
        /// <param name="center">The deployment center.</param>
        /// <param name="right">The local right axis.</param>
        /// <param name="up">The local up axis.</param>
        /// <param name="forward">The participant's heading.</param>
        /// <param name="placements">The pending placements.</param>
        private static void AddPlacements(
            List<Group> groups,
            Layout layout,
            Vector3 offset,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            Vector3 forward,
            List<Placement> placements
        )
        {
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                Group group = groups[groupIndex];
                for (int index = 0; index < group.Ships.Count; index++)
                {
                    Ship ship = group.Ships[index];
                    Vector3 local =
                        offset + layout.Positions[groupIndex] + group.Layout.Positions[index];
                    Vector3 position = center + right * local.X + up * local.Y + forward * local.Z;
                    placements.Add(
                        new Placement(
                            ship.Combatant,
                            position,
                            right,
                            up,
                            forward,
                            ship.Size * 0.5f
                        )
                    );
                }
            }
        }

        /// <summary>Rejects intersecting occupied hulls before any persistent position changes.</summary>
        /// <param name="placements">All pending placements.</param>
        private static void ValidateSeparation(List<Placement> placements)
        {
            placements.Sort(
                (left, right) =>
                    (left.Position.X - left.Extent.X).CompareTo(right.Position.X - right.Extent.X)
            );
            for (int left = 0; left < placements.Count; left++)
            {
                Placement first = placements[left];
                for (int right = left + 1; right < placements.Count; right++)
                {
                    Placement second = placements[right];
                    if (second.Position.X - second.Extent.X >= first.Position.X + first.Extent.X)
                        break;
                    Vector3 separation = Vector3.Abs(first.Position - second.Position);
                    Vector3 required = first.Extent + second.Extent;
                    if (
                        separation.X < required.X
                        && separation.Y < required.Y
                        && separation.Z < required.Z
                        && Intersects(first, second)
                    )
                        throw new InvalidOperationException(
                            "Deployment regions produce intersecting occupied hulls."
                        );
                }
            }
        }

        /// <summary>Tests oriented hulls on their face normals and cross-product separating axes.</summary>
        /// <param name="first">The first occupied hull.</param>
        /// <param name="second">The second occupied hull.</param>
        /// <returns>True when no separating axis exists.</returns>
        private static bool Intersects(Placement first, Placement second)
        {
            Vector3 delta = second.Position - first.Position;
            for (int axis = 0; axis < 3; axis++)
            {
                if (
                    IsSeparated(first, second, delta, GetAxis(first, axis))
                    || IsSeparated(first, second, delta, GetAxis(second, axis))
                )
                    return false;
                for (int other = 0; other < 3; other++)
                    if (
                        IsSeparated(
                            first,
                            second,
                            delta,
                            Vector3.Cross(GetAxis(first, axis), GetAxis(second, other))
                        )
                    )
                        return false;
            }
            return true;
        }

        /// <summary>Returns one hull orientation axis.</summary>
        /// <param name="placement">The occupied hull.</param>
        /// <param name="axis">The axis index.</param>
        /// <returns>The unit orientation axis.</returns>
        private static Vector3 GetAxis(Placement placement, int axis) =>
            axis == 0 ? placement.Right
            : axis == 1 ? placement.Up
            : placement.Forward;

        /// <summary>Checks whether projected hull intervals touch or separate on one axis.</summary>
        /// <param name="first">The first occupied hull.</param>
        /// <param name="second">The second occupied hull.</param>
        /// <param name="delta">The displacement between centers.</param>
        /// <param name="axis">The candidate separating axis.</param>
        /// <returns>True when the hull interiors do not intersect on this axis.</returns>
        private static bool IsSeparated(
            Placement first,
            Placement second,
            Vector3 delta,
            Vector3 axis
        )
        {
            if (axis.LengthSquared() < 0.000001f)
                return false;
            axis = Vector3.Normalize(axis);
            float radius = ProjectRadius(first, axis) + ProjectRadius(second, axis);
            return Math.Abs(Vector3.Dot(delta, axis)) >= radius - 0.0001f;
        }

        /// <summary>Projects an occupied hull's half dimensions onto an axis.</summary>
        /// <param name="placement">The occupied hull.</param>
        /// <param name="axis">The unit projection axis.</param>
        /// <returns>The projected half length.</returns>
        private static float ProjectRadius(Placement placement, Vector3 axis) =>
            Math.Abs(Vector3.Dot(placement.Right, axis)) * placement.LocalExtent.X
            + Math.Abs(Vector3.Dot(placement.Up, axis)) * placement.LocalExtent.Y
            + Math.Abs(Vector3.Dot(placement.Forward, axis)) * placement.LocalExtent.Z;

        /// <summary>Compares occupied size and then stable identity.</summary>
        /// <param name="left">The first size.</param>
        /// <param name="right">The second size.</param>
        /// <param name="leftId">The first identity.</param>
        /// <param name="rightId">The second identity.</param>
        /// <returns>The descending size order.</returns>
        private static int CompareSize(Vector3 left, Vector3 right, string leftId, string rightId)
        {
            int comparison = Math.Max(right.X, Math.Max(right.Y, right.Z))
                .CompareTo(Math.Max(left.X, Math.Max(left.Y, left.Z)));
            if (comparison == 0)
                comparison = (right.X * right.Y * right.Z).CompareTo(left.X * left.Y * left.Z);
            return comparison == 0
                ? string.Compare(leftId, rightId, StringComparison.Ordinal)
                : comparison;
        }

        /// <summary>Computes the world-aligned size of a rotated box.</summary>
        /// <param name="size">The local dimensions.</param>
        /// <param name="right">The right axis.</param>
        /// <param name="up">The up axis.</param>
        /// <param name="forward">The forward axis.</param>
        /// <returns>The rotated envelope dimensions.</returns>
        private static Vector3 RotateSize(Vector3 size, Vector3 right, Vector3 up, Vector3 forward)
        {
            return Vector3.Abs(right) * size.X
                + Vector3.Abs(up) * size.Y
                + Vector3.Abs(forward) * size.Z;
        }

        /// <summary>Validates a finite, positive map volume.</summary>
        /// <param name="bounds">The bounds to inspect.</param>
        private static void ValidateBounds(BattleMapBounds bounds)
        {
            if (
                bounds == null
                || !IsFinite(bounds.MinimumX)
                || !IsFinite(bounds.MinimumY)
                || !IsFinite(bounds.MinimumZ)
                || !IsFinite(bounds.MaximumX)
                || !IsFinite(bounds.MaximumY)
                || !IsFinite(bounds.MaximumZ)
                || !IsPositive(bounds.MaximumX - bounds.MinimumX)
                || !IsPositive(bounds.MaximumY - bounds.MinimumY)
                || !IsPositive(bounds.MaximumZ - bounds.MinimumZ)
            )
                throw new ArgumentException(
                    "Deployment requires finite, positive bounds.",
                    nameof(bounds)
                );
        }

        /// <summary>Returns whether a value is finite.</summary>
        /// <param name="value">The value to inspect.</param>
        /// <returns>True for finite values.</returns>
        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>Returns whether a value is finite and positive.</summary>
        /// <param name="value">The value to inspect.</param>
        /// <returns>True for positive finite values.</returns>
        private static bool IsPositive(float value) => IsFinite(value) && value > 0f;

        /// <summary>Returns the minimum corner.</summary>
        /// <param name="bounds">The source bounds.</param>
        /// <returns>The minimum corner.</returns>
        private static Vector3 GetMinimum(BattleMapBounds bounds) =>
            new Vector3(bounds.MinimumX, bounds.MinimumY, bounds.MinimumZ);

        /// <summary>Returns the maximum corner.</summary>
        /// <param name="bounds">The source bounds.</param>
        /// <returns>The maximum corner.</returns>
        private static Vector3 GetMaximum(BattleMapBounds bounds) =>
            new Vector3(bounds.MaximumX, bounds.MaximumY, bounds.MaximumZ);

        /// <summary>Converts a calculation result into persistent battle coordinates.</summary>
        /// <param name="value">The calculated vector.</param>
        /// <returns>The persistent vector.</returns>
        private static BattleVector3 ToBattleVector(Vector3 value) =>
            new BattleVector3
            {
                X = value.X,
                Y = value.Y,
                Z = value.Z,
            };

        private sealed class Group
        {
            internal readonly string Id;
            internal readonly List<Ship> Ships;
            internal Layout Layout;

            /// <summary>Creates an independently packed ship or squadron.</summary>
            /// <param name="id">The stable group identifier.</param>
            /// <param name="ships">The group members.</param>
            internal Group(string id, List<Ship> ships)
            {
                Id = id;
                Ships = ships;
            }
        }

        private readonly struct Ship
        {
            internal readonly CombatUnit Combatant;
            internal readonly Vector3 Size;

            /// <summary>Captures one combatant's occupied envelope.</summary>
            /// <param name="combatant">The persistent combatant.</param>
            /// <param name="size">The hull dimensions with clearance.</param>
            internal Ship(CombatUnit combatant, Vector3 size)
            {
                Combatant = combatant;
                Size = size;
            }
        }

        private readonly struct Layout
        {
            internal readonly Vector3 Size;
            internal readonly Vector3[] Positions;

            /// <summary>Captures one centered arrangement.</summary>
            /// <param name="size">The occupied dimensions.</param>
            /// <param name="positions">The ordered item positions.</param>
            internal Layout(Vector3 size, Vector3[] positions)
            {
                Size = size;
                Positions = positions;
            }
        }

        private readonly struct Placement
        {
            internal readonly CombatUnit Combatant;
            internal readonly Vector3 Position;
            internal readonly Vector3 Right;
            internal readonly Vector3 Up;
            internal readonly Vector3 Forward;
            internal readonly Vector3 LocalExtent;
            internal readonly Vector3 Extent;

            /// <summary>Captures a pending world-space placement.</summary>
            /// <param name="combatant">The persistent combatant.</param>
            /// <param name="position">The world position.</param>
            /// <param name="right">The right axis.</param>
            /// <param name="up">The up axis.</param>
            /// <param name="forward">The world heading.</param>
            /// <param name="extent">The occupied local extent.</param>
            internal Placement(
                CombatUnit combatant,
                Vector3 position,
                Vector3 right,
                Vector3 up,
                Vector3 forward,
                Vector3 extent
            )
            {
                Combatant = combatant;
                Position = position;
                Right = right;
                Up = up;
                Forward = forward;
                LocalExtent = extent;
                Extent = RotateSize(extent, right, up, forward);
            }
        }
    }
}
