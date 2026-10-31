using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>
/// Removes compound-rectangle overlaps on an integer grid while anchoring objects to their desired positions.
/// </summary>
public static class RectangleLayout
{
    private const double _tolerance = 0.0000001;

    /// <summary>
    /// Stores an inclusive rectangle of forbidden integer translations.
    /// </summary>
    private readonly struct CollisionBounds
    {
        public readonly long Left;
        public readonly long Top;
        public readonly long Right;
        public readonly long Bottom;

        /// <summary>
        /// Captures the inclusive limits of a translation obstacle.
        /// </summary>
        /// <param name="l">The left limit.</param>
        /// <param name="t">The top limit.</param>
        /// <param name="r">The right limit.</param>
        /// <param name="b">The bottom limit.</param>
        public CollisionBounds(long l, long t, long r, long b)
        {
            Left = l;
            Top = t;
            Right = r;
            Bottom = b;
        }

        /// <summary>
        /// Checks whether another obstacle is fully covered.
        /// </summary>
        /// <param name="other">The candidate obstacle.</param>
        /// <returns>Whether every point of the candidate is contained.</returns>
        public bool Contains(CollisionBounds other) =>
            Left <= other.Left
            && Top <= other.Top
            && Right >= other.Right
            && Bottom >= other.Bottom;
    }

    /// <summary>
    /// Associates a forbidden translation with its ordered pair of objects.
    /// </summary>
    private readonly struct CollisionRegion
    {
        public readonly int First;
        public readonly int Second;
        public readonly CollisionBounds Bounds;

        /// <summary>
        /// Captures one pair's exact translation obstacle.
        /// </summary>
        /// <param name="i">The first object.</param>
        /// <param name="j">The second object.</param>
        /// <param name="region">The inclusive forbidden translations.</param>
        public CollisionRegion(int i, int j, CollisionBounds region)
        {
            First = i;
            Second = j;
            Bounds = region;
        }
    }

    /// <summary>
    /// Assigns complete shapes to an optimal shared-footprint layout without changing the position set.
    /// </summary>
    private sealed class PositionAssignment
    {
        private readonly IReadOnlyList<RectangleLayoutItem> items;
        private readonly Vector2Int[] slots;
        private readonly Vector2Int[] positions;
        private readonly bool[] assigned;
        private readonly bool[] used;
        private readonly bool[,] fits;
        private readonly Dictionary<(int First, int Second), CollisionBounds[]> pairs;
        private readonly CancellationToken token;

        /// <summary>
        /// Captures immutable geometry and the allowed slot bounds for each complete shape.
        /// </summary>
        /// <param name="items">The complete rigid shapes.</param>
        /// <param name="slots">The optimal shared-footprint anchors.</param>
        /// <param name="bounds">The usable frame.</param>
        /// <param name="geometry">The exact full-shape collision obstacles.</param>
        /// <param name="token">The cancellation request.</param>
        public PositionAssignment(
            IReadOnlyList<RectangleLayoutItem> items,
            Vector2Int[] slots,
            Rect bounds,
            CollisionRegion[] geometry,
            CancellationToken token
        )
        {
            this.items = items;
            this.slots = slots;
            this.token = token;
            positions = new Vector2Int[items.Count];
            assigned = new bool[items.Count];
            used = new bool[slots.Length];
            fits = new bool[items.Count, slots.Length];
            pairs = geometry
                .GroupBy(region => (region.First, region.Second))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(region => region.Bounds).ToArray()
                );
            for (int i = 0; i < items.Count; i++)
            {
                int left = Ceiling(
                    (double)bounds.xMin - items[i].Footprints.Min(rect => rect.xMin)
                );
                int right = Floor((double)bounds.xMax - items[i].Footprints.Max(rect => rect.xMax));
                int top = Ceiling((double)bounds.yMin - items[i].Footprints.Min(rect => rect.yMin));
                int bottom = Floor(
                    (double)bounds.yMax - items[i].Footprints.Max(rect => rect.yMax)
                );
                for (int slot = 0; slot < slots.Length; slot++)
                    fits[i, slot] =
                        slots[slot].x >= left
                        && slots[slot].x <= right
                        && slots[slot].y >= top
                        && slots[slot].y <= bottom;
            }
        }

        /// <summary>
        /// Finds a complete compatible assignment without moving any candidate anchor.
        /// </summary>
        /// <returns>The indexed full-shape layout, or null when these slots cannot fit all shapes.</returns>
        public Vector2Int[] Find() => Search(0) ? positions : null;

        /// <summary>
        /// Places the most constrained remaining shape and backtracks only incompatible assignments.
        /// </summary>
        /// <param name="depth">The number of assigned shapes.</param>
        /// <returns>Whether every complete shape can be placed.</returns>
        private bool Search(int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth == items.Count)
                return true;
            int selected = -1;
            int[] options = null;
            for (int i = 0; i < items.Count; i++)
                if (!assigned[i])
                {
                    int[] candidates = Enumerable
                        .Range(0, slots.Length)
                        .Where(slot => Allows(i, slot))
                        .ToArray();
                    if (candidates.Length == 0)
                        return false;
                    if (options == null || candidates.Length < options.Length)
                    {
                        selected = i;
                        options = candidates;
                    }
                }
            foreach (int slot in options)
            {
                assigned[selected] = true;
                used[slot] = true;
                positions[selected] = slots[slot];
                if (Search(depth + 1))
                    return true;
                assigned[selected] = false;
                used[slot] = false;
            }
            return false;
        }

        /// <summary>
        /// Checks the frame and every already placed full shape using the exact collision map.
        /// </summary>
        /// <param name="item">The shape being assigned.</param>
        /// <param name="slot">The candidate anchor.</param>
        /// <returns>Whether this assignment remains feasible.</returns>
        private bool Allows(int item, int slot)
        {
            if (used[slot] || !fits[item, slot])
                return false;
            for (int other = 0; other < items.Count; other++)
                if (assigned[other])
                {
                    if (
                        !pairs.TryGetValue(
                            (Math.Min(item, other), Math.Max(item, other)),
                            out CollisionBounds[] regions
                        )
                    )
                        continue;
                    long x =
                        item < other
                            ? (long)positions[other].x - slots[slot].x
                            : (long)slots[slot].x - positions[other].x;
                    long y =
                        item < other
                            ? (long)positions[other].y - slots[slot].y
                            : (long)slots[slot].y - positions[other].y;
                    foreach (CollisionBounds region in regions)
                        if (
                            x >= region.Left
                            && x <= region.Right
                            && y >= region.Top
                            && y <= region.Bottom
                        )
                            return false;
                }
            return true;
        }
    }

    private readonly struct Separation
    {
        public readonly int Before;
        public readonly int After;
        public readonly int Distance;
        public readonly bool Horizontal;

        /// <summary>
        /// Defines one directional integer separation between two object anchors.
        /// </summary>
        /// <param name="before">The first anchor index.</param>
        /// <param name="after">The second anchor index.</param>
        /// <param name="distance">The minimum anchor difference.</param>
        /// <param name="horizontal">Whether the constraint applies horizontally.</param>
        public Separation(int before, int after, int distance, bool horizontal)
        {
            Before = before;
            After = after;
            Distance = distance;
            Horizontal = horizontal;
        }
    }

    private sealed class Placement
    {
        public readonly Separation[] Constraints;
        public readonly Vector2Int[] Positions;
        public readonly double Cost;
        public readonly double LowerBound;

        /// <summary>
        /// Captures a feasible projection and its displacement cost.
        /// </summary>
        /// <param name="constraints">The directional constraints defining this placement.</param>
        /// <param name="positions">The projected integer anchors.</param>
        /// <param name="cost">The squared visual displacement.</param>
        /// <param name="lowerBound">A conservative dual lower bound for the chosen constraints.</param>
        public Placement(
            Separation[] constraints,
            Vector2Int[] positions,
            double cost,
            double lowerBound = 0
        )
        {
            Constraints = constraints;
            Positions = positions;
            Cost = cost;
            LowerBound = lowerBound;
        }
    }

    /// <summary>
    /// Reuses identical axis projections within one search over fixed geometry and frame bounds.
    /// </summary>
    private sealed class ProjectionCache : IEqualityComparer<Separation[]>
    {
        private readonly Dictionary<Separation[], (int[] Values, double Bound)> horizontal;
        private readonly Dictionary<Separation[], (int[] Values, double Bound)> vertical;

        /// <summary>
        /// Creates independent caches for the two coordinate axes.
        /// </summary>
        public ProjectionCache()
        {
            horizontal = new(this);
            vertical = new(this);
        }

        /// <summary>
        /// Solves each ordered constraint sequence once without sharing results between layouts.
        /// </summary>
        /// <param name="items">The immutable desired anchors and occupied shapes.</param>
        /// <param name="minimum">The frame's lower coordinate.</param>
        /// <param name="maximum">The frame's upper coordinate.</param>
        /// <param name="constraints">The immutable separation sequence on this axis.</param>
        /// <param name="isHorizontal">Whether this is the horizontal axis.</param>
        /// <param name="token">The cancellation request.</param>
        /// <param name="lowerBound">The projection's conservative displacement bound.</param>
        /// <returns>The immutable projected coordinates, or null for contradictory constraints.</returns>
        public int[] Project(
            IReadOnlyList<RectangleLayoutItem> items,
            float minimum,
            float maximum,
            Separation[] constraints,
            bool isHorizontal,
            CancellationToken token,
            out double lowerBound
        )
        {
            token.ThrowIfCancellationRequested();
            var cache = isHorizontal ? horizontal : vertical;
            if (!cache.TryGetValue(constraints, out var result))
            {
                int[] values = ProjectAxis(
                    items,
                    minimum,
                    maximum,
                    constraints,
                    isHorizontal,
                    token,
                    out double bound
                );
                result = (values, bound);
                cache.Add(constraints, result);
            }
            lowerBound = result.Bound;
            return result.Values;
        }

        /// <summary>
        /// Compares constraint order as well as values to preserve numerical projection behavior.
        /// </summary>
        /// <param name="first">The first immutable sequence.</param>
        /// <param name="second">The second immutable sequence.</param>
        /// <returns>Whether both sequences request the same projection.</returns>
        public bool Equals(Separation[] first, Separation[] second)
        {
            if (first.Length != second.Length)
                return false;
            for (int i = 0; i < first.Length; i++)
                if (
                    first[i].Before != second[i].Before
                    || first[i].After != second[i].After
                    || first[i].Distance != second[i].Distance
                )
                    return false;
            return true;
        }

        /// <summary>
        /// Hashes the ordered separation values within one axis.
        /// </summary>
        /// <param name="constraints">The immutable sequence.</param>
        /// <returns>The value hash used by the search-local cache.</returns>
        public int GetHashCode(Separation[] constraints)
        {
            int hash = 17;
            foreach (Separation constraint in constraints)
            {
                hash = unchecked(hash * 31 + constraint.Before);
                hash = unchecked(hash * 31 + constraint.After);
                hash = unchecked(hash * 31 + constraint.Distance);
            }
            return hash;
        }
    }

    /// <summary>
    /// Searches feasible integer placements to minimize squared visual displacement from the authored anchors.
    /// </summary>
    /// <param name="items">The desired anchors and their rigid occupied shapes.</param>
    /// <param name="bounds">The usable coordinate frame.</param>
    /// <param name="gap">The required horizontal and vertical clearance.</param>
    /// <param name="coordinateScale">The visual size of one coordinate step on each axis.</param>
    /// <param name="cancellationToken">Cancellation for expensive or impossible searches.</param>
    /// <returns>Complete non-overlapping integer anchors in input order.</returns>
    public static Vector2Int[] Arrange(
        IReadOnlyList<RectangleLayoutItem> items,
        Rect bounds,
        Vector2 gap,
        Vector2 coordinateScale,
        CancellationToken cancellationToken = default
    )
    {
        Validate(items, bounds, gap, coordinateScale);
        cancellationToken.ThrowIfCancellationRequested();
        if (items.Count == 0)
            return Array.Empty<Vector2Int>();
        double frameArea =
            ((double)bounds.xMax - bounds.xMin) * ((double)bounds.yMax - bounds.yMin);
        if (items.Sum(item => OccupiedArea(item.Footprints)) > frameArea + _tolerance)
            throw new ArgumentException("The items occupy more space than this map contains.");
        var geometry = BuildGeometry(items, gap, cancellationToken);
        Placement initial = Project(
            items,
            bounds,
            coordinateScale,
            Array.Empty<Separation>(),
            cancellationToken
        );
        if (initial != null && FindCollision(geometry, initial.Positions) == null)
            return initial.Positions;
        if (
            initial != null
            && items.All(item => item.Position == items[0].Position)
            && !items.All(item => item.Footprints.SequenceEqual(items[0].Footprints))
        )
        {
            Vector2Int[] shared = TrySharedPlacement(
                items,
                bounds,
                gap,
                coordinateScale,
                geometry,
                cancellationToken
            );
            if (shared != null)
                return shared;
        }
        RectangleLayoutItem[] envelopes = items
            .Select(item => new RectangleLayoutItem(
                item.Position,
                new[]
                {
                    Rect.MinMaxRect(
                        item.Footprints.Min(r => r.xMin),
                        item.Footprints.Min(r => r.yMin),
                        item.Footprints.Max(r => r.xMax),
                        item.Footprints.Max(r => r.yMax)
                    ),
                }
            ))
            .ToArray();
        Placement placement = null;
        if (
            envelopes.Sum(item => (double)item.Footprints[0].width * item.Footprints[0].height)
            <= frameArea + _tolerance
        )
            placement = FindPlacement(
                envelopes,
                bounds,
                BuildGeometry(envelopes, gap, cancellationToken),
                coordinateScale,
                cancellationToken
            );
        placement ??= FindPlacement(items, bounds, geometry, coordinateScale, cancellationToken);
        if (placement == null)
            throw new ArgumentException(
                "Not enough room to arrange these items. Move items or shorten their labels."
            );
        Vector2Int[] refined = Refine(
            items,
            bounds,
            geometry,
            coordinateScale,
            placement,
            cancellationToken
        );
        Placement seed = new Placement(
            placement.Constraints,
            refined,
            GetCost(items, refined, coordinateScale)
        );
        return ImprovePlacement(
            items,
            bounds,
            geometry,
            coordinateScale,
            cancellationToken,
            seed
        ).Positions;
    }

    /// <summary>
    /// Uses shared occupied shapes as a lower-bound problem when all desired anchors coincide.
    /// </summary>
    /// <param name="items">The different full shapes sharing one desired anchor.</param>
    /// <param name="bounds">The usable frame.</param>
    /// <param name="gap">The required clearance.</param>
    /// <param name="scale">The visual coordinate scales.</param>
    /// <param name="geometry">The full-shape collision obstacles.</param>
    /// <param name="token">The cancellation request.</param>
    /// <returns>A full-shape placement attaining the shared problem's lower bound, or null.</returns>
    private static Vector2Int[] TrySharedPlacement(
        IReadOnlyList<RectangleLayoutItem> items,
        Rect bounds,
        Vector2 gap,
        Vector2 scale,
        CollisionRegion[] geometry,
        CancellationToken token
    )
    {
        Rect[] common = items
            .SelectMany(item => item.Footprints)
            .Distinct()
            .Where(rect =>
                items.All(item =>
                    item.Footprints.Any(other =>
                        other.xMin <= rect.xMin
                        && other.xMax >= rect.xMax
                        && other.yMin <= rect.yMin
                        && other.yMax >= rect.yMax
                    )
                )
            )
            .ToArray();
        if (common.Length == 0)
            return null;
        RectangleLayoutItem[] relaxed = items
            .Select(item => new RectangleLayoutItem(item.Position, common))
            .ToArray();
        Vector2Int[] slots = Arrange(relaxed, bounds, gap, scale, token);
        // All desired anchors coincide, so assigning these positions to different objects preserves the objective.
        // A compatible full-shape assignment attains the relaxation's lower bound and is therefore optimal.
        return new PositionAssignment(items, slots, bounds, geometry, token).Find();
    }

    /// <summary>
    /// Combines all footprint collisions into exact integer translation obstacles once per layout.
    /// </summary>
    /// <param name="items">The rigid compound shapes.</param>
    /// <param name="gap">The required clearance.</param>
    /// <param name="token">The cancellation request.</param>
    /// <returns>Maximal rectangular obstacles for every object pair.</returns>
    private static CollisionRegion[] BuildGeometry(
        IReadOnlyList<RectangleLayoutItem> items,
        Vector2 gap,
        CancellationToken token
    )
    {
        List<CollisionRegion> result = new();
        for (int i = 0; i < items.Count; i++)
        for (int j = i + 1; j < items.Count; j++)
        {
            token.ThrowIfCancellationRequested();
            List<CollisionBounds> regions = new();
            foreach (var a in items[i].Footprints)
            foreach (var b in items[j].Footprints)
            {
                long l = 1L - Ceiling((double)b.xMax - a.xMin + gap.x),
                    r = (long)Ceiling((double)a.xMax - b.xMin + gap.x) - 1;
                long t = 1L - Ceiling((double)b.yMax - a.yMin + gap.y),
                    bottom = (long)Ceiling((double)a.yMax - b.yMin + gap.y) - 1;
                if (l <= r && t <= bottom)
                    regions.Add(new(l, t, r, bottom));
            }
            foreach (var region in MergeRegions(regions, token))
                result.Add(new(i, j, region));
        }
        return result.ToArray();
    }

    /// <summary>
    /// Intersects sorted disjoint integer intervals without filling their gaps.
    /// </summary>
    /// <param name="a">The first interval union.</param>
    /// <param name="b">The second interval union.</param>
    /// <returns>The sorted intersection intervals.</returns>
    private static List<(long L, long R)> Intersect(
        List<(long L, long R)> a,
        List<(long L, long R)> b
    )
    {
        List<(long L, long R)> result = new();
        int i = 0,
            j = 0;
        while (i < a.Count && j < b.Count)
        {
            long l = Math.Max(a[i].L, b[j].L),
                r = Math.Min(a[i].R, b[j].R);
            if (l <= r)
                result.Add((l, r));
            if (a[i].R < b[j].R)
                i++;
            else
                j++;
        }
        return result;
    }

    /// <summary>
    /// Finds maximal rectangles contained in a union of forbidden integer translations.
    /// </summary>
    /// <param name="regions">The atomic translation obstacles.</param>
    /// <param name="token">The cancellation request.</param>
    /// <returns>An exact union cover with no rectangle contained in another.</returns>
    private static CollisionBounds[] MergeRegions(
        List<CollisionBounds> regions,
        CancellationToken token
    )
    {
        long[] cuts = regions
            .SelectMany(r => new[] { r.Top, r.Bottom + 1 })
            .Distinct()
            .OrderBy(y => y)
            .ToArray();
        List<(long L, long R)>[] bands = new List<(long L, long R)>[Math.Max(0, cuts.Length - 1)];
        for (int i = 0; i < bands.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            bands[i] = new();
            foreach (
                var r in regions
                    .Where(r => r.Top <= cuts[i] && r.Bottom >= cuts[i])
                    .OrderBy(r => r.Left)
                    .ThenBy(r => r.Right)
            )
            {
                if (bands[i].Count > 0 && r.Left <= bands[i][^1].R + 1)
                {
                    var last = bands[i][^1];
                    bands[i][^1] = (last.L, Math.Max(last.R, r.Right));
                }
                else
                    bands[i].Add((r.Left, r.Right));
            }
        }
        List<CollisionBounds> result = new();
        for (int i = 0; i < bands.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            var intervals = bands[i];
            for (int j = i; j < bands.Length && intervals.Count > 0; j++)
            {
                if (j > i)
                    intervals = Intersect(intervals, bands[j]);
                foreach (var range in intervals)
                {
                    CollisionBounds added = new(range.L, cuts[i], range.R, cuts[j + 1] - 1);
                    bool contained = false;
                    for (int k = result.Count - 1; k >= 0; k--)
                    {
                        if (result[k].Contains(added))
                        {
                            contained = true;
                            break;
                        }
                        if (added.Contains(result[k]))
                            result.RemoveAt(k);
                    }
                    if (!contained)
                        result.Add(added);
                }
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// Finds a feasible directional placement using displacement-ranked backtracking.
    /// </summary>
    /// <param name="items">The rigid occupied shapes.</param>
    /// <param name="bounds">The usable frame.</param>
    /// <param name="geometry">The exact integer collision obstacles.</param>
    /// <param name="scale">The visual coordinate scales.</param>
    /// <param name="token">The cancellation request.</param>
    /// <returns>A complete placement, or null if no directional choices can fit.</returns>
    private static Placement FindPlacement(
        IReadOnlyList<RectangleLayoutItem> items,
        Rect bounds,
        CollisionRegion[] geometry,
        Vector2 scale,
        CancellationToken token
    )
    {
        ProjectionCache projections = new();
        Stack<Placement> pending = new();
        Placement initial = Project(
            items,
            bounds,
            scale,
            CreateSymmetryConstraints(items),
            token,
            projections
        );
        if (initial != null)
            pending.Push(initial);
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            Placement current = pending.Pop();
            Separation[] alternatives = FindCollision(geometry, current.Positions);
            if (alternatives == null)
                return current;
            List<Placement> candidates = new();
            foreach (Separation alternative in alternatives)
            {
                Separation[] constraints = AddConstraint(current.Constraints, alternative);
                if (constraints == null)
                    continue;
                Placement candidate = Project(
                    items,
                    bounds,
                    scale,
                    constraints,
                    token,
                    projections
                );
                if (candidate != null)
                    candidates.Add(candidate);
            }
            foreach (
                Placement candidate in candidates.OrderByDescending(candidate => candidate.Cost)
            )
                pending.Push(candidate);
        }
        return null;
    }

    /// <summary>
    /// Searches exact-shape separation choices, pruning only states whose dual bound cannot improve the incumbent.
    /// </summary>
    /// <param name="items">The desired anchors and rigid shapes.</param>
    /// <param name="bounds">The usable frame.</param>
    /// <param name="geometry">The exact integer collision obstacles.</param>
    /// <param name="scale">The visual coordinate scales.</param>
    /// <param name="token">The cancellation request.</param>
    /// <param name="best">The feasible upper bound.</param>
    /// <returns>The least-displacing completed search result.</returns>
    private static Placement ImprovePlacement(
        IReadOnlyList<RectangleLayoutItem> items,
        Rect bounds,
        CollisionRegion[] geometry,
        Vector2 scale,
        CancellationToken token,
        Placement best
    )
    {
        ProjectionCache projections = new();
        Stack<Placement> pending = new();
        Placement initial = Project(
            items,
            bounds,
            scale,
            CreateSymmetryConstraints(items),
            token,
            projections
        );
        if (initial != null)
            pending.Push(initial);
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            Placement current = pending.Pop();
            if (current.LowerBound >= best.Cost - _tolerance)
                continue;
            Separation[] alternatives = FindCollision(geometry, current.Positions);
            if (alternatives == null)
            {
                if (current.Cost < best.Cost)
                    best = current;
                continue;
            }
            List<Placement> candidates = new();
            foreach (Separation alternative in alternatives)
            {
                Separation[] constraints = AddConstraint(current.Constraints, alternative);
                if (constraints == null)
                    continue;
                Placement candidate = Project(
                    items,
                    bounds,
                    scale,
                    constraints,
                    token,
                    projections
                );
                if (candidate != null)
                    candidates.Add(candidate);
            }
            foreach (
                Placement candidate in candidates.OrderByDescending(candidate => candidate.Cost)
            )
                pending.Push(candidate);
        }
        return best;
    }

    /// <summary>
    /// Orders interchangeable shapes vertically to avoid searching permutations of the same placement.
    /// </summary>
    /// <param name="items">The desired anchors and rigid shapes.</param>
    /// <returns>Zero-distance constraints that preserve feasibility for identical shapes and anchors.</returns>
    private static Separation[] CreateSymmetryConstraints(IReadOnlyList<RectangleLayoutItem> items)
    {
        List<Separation> constraints = new();
        for (int i = 1; i < items.Count; i++)
        for (int j = i - 1; j >= 0; j--)
            if (
                items[i].Position == items[j].Position
                && items[i].Footprints.SequenceEqual(items[j].Footprints)
            )
            {
                constraints.Add(new Separation(j, i, 0, false));
                break;
            }
        return constraints.ToArray();
    }

    /// <summary>
    /// Measures a compound footprint without counting its overlapping pieces twice.
    /// </summary>
    /// <param name="shapes">The rectangles belonging to one rigid item.</param>
    /// <returns>The exact occupied union area.</returns>
    private static double OccupiedArea(IReadOnlyList<Rect> shapes)
    {
        float[] edges = shapes
            .SelectMany(rect => new[] { rect.xMin, rect.xMax })
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        double area = 0;
        for (int i = 1; i < edges.Length; i++)
        {
            Rect[] strip = shapes
                .Where(rect => rect.xMin < edges[i] && rect.xMax > edges[i - 1])
                .OrderBy(rect => rect.yMin)
                .ToArray();
            double covered = 0;
            float end = float.NegativeInfinity;
            foreach (Rect rect in strip)
            {
                covered += Math.Max(0, (double)rect.yMax - Math.Max(rect.yMin, end));
                end = Math.Max(end, rect.yMax);
            }
            area += ((double)edges[i] - edges[i - 1]) * covered;
        }
        return area;
    }

    /// <summary>
    /// Rejects invalid geometry before starting a background search.
    /// </summary>
    /// <param name="items">The movable shapes.</param>
    /// <param name="bounds">The usable frame.</param>
    /// <param name="gap">The required clearance.</param>
    /// <param name="scale">The visual coordinate scales.</param>
    private static void Validate(
        IReadOnlyList<RectangleLayoutItem> items,
        Rect bounds,
        Vector2 gap,
        Vector2 scale
    )
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (
            !IsRectangle(bounds)
            || !IsFinite(gap.x)
            || !IsFinite(gap.y)
            || gap.x < 0
            || gap.y < 0
            || !IsFinite(scale.x)
            || !IsFinite(scale.y)
            || scale.x <= 0
            || scale.y <= 0
        )
            throw new ArgumentException(
                "The layout requires valid bounds, clearance, and coordinate scales."
            );
        foreach (RectangleLayoutItem item in items)
            if (
                item == null
                || item.Footprints.Count == 0
                || item.Footprints.Any(shape => !IsRectangle(shape))
            )
                throw new ArgumentException(
                    "Every layout item requires a valid footprint.",
                    nameof(items)
                );
    }

    /// <summary>
    /// Checks a finite positive-area rectangle.
    /// </summary>
    /// <param name="rect">The rectangle to inspect.</param>
    /// <returns>Whether all edges are finite and its area is positive.</returns>
    private static bool IsRectangle(Rect rect) =>
        IsFinite(rect.xMin)
        && IsFinite(rect.xMax)
        && IsFinite(rect.yMin)
        && IsFinite(rect.yMax)
        && rect.width > 0
        && rect.height > 0;

    /// <summary>
    /// Checks that a coordinate is representable by finite arithmetic.
    /// </summary>
    /// <param name="value">The coordinate.</param>
    /// <returns>Whether the coordinate is finite.</returns>
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>
    /// Finds the largest remaining shape collision and its four separating half-spaces.
    /// </summary>
    /// <param name="positions">Their current anchors.</param>
    /// <param name="geometry">The exact integer collision obstacles.</param>
    /// <returns>Four alternatives, or null when the entire placement is clear.</returns>
    private static Separation[] FindCollision(CollisionRegion[] geometry, Vector2Int[] positions)
    {
        double largest = 0;
        Separation[] result = null;
        foreach (var pair in geometry)
        {
            long x = (long)positions[pair.Second].x - positions[pair.First].x,
                y = (long)positions[pair.Second].y - positions[pair.First].y;
            var r = pair.Bounds;
            if (x < r.Left || x > r.Right || y < r.Top || y > r.Bottom)
                continue;
            double score =
                (double)Math.Min(x - r.Left + 1, r.Right - x + 1)
                * Math.Min(y - r.Top + 1, r.Bottom - y + 1);
            if (score <= largest)
                continue;
            largest = score;
            result = new[]
            {
                new Separation(pair.First, pair.Second, checked((int)(r.Right + 1)), true),
                new Separation(pair.Second, pair.First, checked((int)(1 - r.Left)), true),
                new Separation(pair.First, pair.Second, checked((int)(r.Bottom + 1)), false),
                new Separation(pair.Second, pair.First, checked((int)(1 - r.Top)), false),
            };
        }
        return result;
    }

    /// <summary>
    /// Adds or tightens one directional constraint without retaining weaker duplicates.
    /// </summary>
    /// <param name="constraints">The preceding directional choices.</param>
    /// <param name="added">The separating choice to enforce.</param>
    /// <returns>The stronger constraint set, or null if the choice is already enforced.</returns>
    private static Separation[] AddConstraint(Separation[] constraints, Separation added)
    {
        int existing = Array.FindIndex(
            constraints,
            constraint =>
                constraint.Before == added.Before
                && constraint.After == added.After
                && constraint.Horizontal == added.Horizontal
        );
        if (existing >= 0 && constraints[existing].Distance >= added.Distance)
            return null;
        Separation[] result = new Separation[constraints.Length + (existing < 0 ? 1 : 0)];
        Array.Copy(constraints, result, constraints.Length);
        result[existing < 0 ? constraints.Length : existing] = added;
        return result;
    }

    /// <summary>
    /// Projects desired anchors onto the chosen separation and boundary constraints.
    /// </summary>
    /// <param name="items">The desired positions and occupied shapes.</param>
    /// <param name="bounds">The usable frame.</param>
    /// <param name="scale">The visual coordinate scales.</param>
    /// <param name="constraints">The selected directional separations.</param>
    /// <param name="token">The cancellation request.</param>
    /// <param name="projections">The cache owned by this fixed-geometry search, when available.</param>
    /// <returns>The projected placement, or null for contradictory constraints.</returns>
    private static Placement Project(
        IReadOnlyList<RectangleLayoutItem> items,
        Rect bounds,
        Vector2 scale,
        Separation[] constraints,
        CancellationToken token,
        ProjectionCache projections = null
    )
    {
        projections ??= new ProjectionCache();
        int[] x = projections.Project(
            items,
            bounds.xMin,
            bounds.xMax,
            constraints.Where(c => c.Horizontal).ToArray(),
            true,
            token,
            out double xBound
        );
        if (x == null)
            return null;
        int[] y = projections.Project(
            items,
            bounds.yMin,
            bounds.yMax,
            constraints.Where(c => !c.Horizontal).ToArray(),
            false,
            token,
            out double yBound
        );
        if (y == null)
            return null;
        Vector2Int[] positions = new Vector2Int[items.Count];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = new Vector2Int(x[i], y[i]);
        double bound = xBound * scale.x * scale.x + yBound * scale.y * scale.y;
        // With equal scales, every saved-coordinate objective is an integer multiple of the common weight.
        if (scale.x == scale.y)
        {
            double unit = (double)scale.x * scale.x;
            bound =
                Math.Ceiling(bound / unit - _tolerance * Math.Max(1, Math.Abs(bound / unit)))
                * unit;
        }
        return new Placement(constraints, positions, GetCost(items, positions, scale), bound);
    }

    /// <summary>
    /// Solves a bounded quadratic projection, then repairs numerical rounding on the integer lattice.
    /// </summary>
    /// <param name="items">The desired anchors and footprints.</param>
    /// <param name="minimum">The frame's lower coordinate.</param>
    /// <param name="maximum">The frame's upper coordinate.</param>
    /// <param name="constraints">The separations on this axis.</param>
    /// <param name="horizontal">Whether this is the horizontal axis.</param>
    /// <param name="token">The cancellation request.</param>
    /// <param name="lowerBound">The squared-displacement bound supplied by nonnegative dual multipliers.</param>
    /// <returns>Feasible integer coordinates, or null for an infeasible axis.</returns>
    private static int[] ProjectAxis(
        IReadOnlyList<RectangleLayoutItem> items,
        float minimum,
        float maximum,
        Separation[] constraints,
        bool horizontal,
        CancellationToken token,
        out double lowerBound
    )
    {
        lowerBound = 0;
        int count = items.Count;
        int[] lower = new int[count];
        int[] upper = new int[count];
        double[] values = new double[count];
        for (int i = 0; i < count; i++)
        {
            lower[i] = Ceiling(
                (double)minimum - items[i].Footprints.Min(r => horizontal ? r.xMin : r.yMin)
            );
            upper[i] = Floor(
                (double)maximum - items[i].Footprints.Max(r => horizontal ? r.xMax : r.yMax)
            );
            if (lower[i] > upper[i])
                return null;
            values[i] = horizontal ? items[i].Position.x : items[i].Position.y;
        }
        if (!Propagate(lower, upper, constraints, token))
            return null;
        double[] lowerDual = new double[count];
        double[] upperDual = new double[count];
        double[] separationDual = new double[constraints.Length];
        double update;
        do
        {
            token.ThrowIfCancellationRequested();
            update = 0;
            for (int i = 0; i < count; i++)
            {
                double next = Math.Max(0, lowerDual[i] + lower[i] - values[i]);
                double change = next - lowerDual[i];
                lowerDual[i] = next;
                values[i] += change;
                update = Math.Max(update, Math.Abs(change));
                next = Math.Max(0, upperDual[i] + values[i] - upper[i]);
                change = next - upperDual[i];
                upperDual[i] = next;
                values[i] -= change;
                update = Math.Max(update, Math.Abs(change));
            }
            for (int i = 0; i < constraints.Length; i++)
            {
                Separation constraint = constraints[i];
                double next = Math.Max(
                    0,
                    separationDual[i]
                        + (
                            constraint.Distance
                            - values[constraint.After]
                            + values[constraint.Before]
                        ) / 2
                );
                double change = next - separationDual[i];
                separationDual[i] = next;
                values[constraint.Before] -= change;
                values[constraint.After] += change;
                update = Math.Max(update, Math.Abs(change));
            }
        } while (update > _tolerance);
        // Nonnegative projection multipliers give a valid dual bound even before numerical convergence.
        // Recomputing the gradient from those multipliers avoids accumulating primal-update roundoff.
        double linear = 0;
        double[] gradient = new double[count];
        for (int i = 0; i < count; i++)
        {
            double desired = horizontal ? items[i].Position.x : items[i].Position.y;
            linear += lowerDual[i] * (lower[i] - desired) + upperDual[i] * (desired - upper[i]);
            gradient[i] = lowerDual[i] - upperDual[i];
        }
        for (int i = 0; i < constraints.Length; i++)
        {
            var c = constraints[i];
            double before = horizontal ? items[c.Before].Position.x : items[c.Before].Position.y;
            double after = horizontal ? items[c.After].Position.x : items[c.After].Position.y;
            linear += separationDual[i] * (c.Distance + before - after);
            gradient[c.Before] -= separationDual[i];
            gradient[c.After] += separationDual[i];
        }
        double dual = 2 * linear - gradient.Sum(value => value * value);
        lowerBound = Math.Max(0, dual - _tolerance * Math.Max(1, Math.Abs(dual)));
        int[] rounded = values
            .Select((value, i) => Math.Max(lower[i], Math.Min(upper[i], Floor(value + 0.5))))
            .ToArray();
        for (int pass = 0; pass < count; pass++)
            foreach (Separation constraint in constraints)
                rounded[constraint.After] = checked(
                    (int)
                        Math.Max(
                            rounded[constraint.After],
                            (long)rounded[constraint.Before] + constraint.Distance
                        )
                );
        return rounded;
    }

    /// <summary>
    /// Computes the componentwise feasible extremes and detects positive cycles or exhausted space.
    /// </summary>
    /// <param name="lower">The lower bounds, tightened in place.</param>
    /// <param name="upper">The upper bounds, tightened in place.</param>
    /// <param name="constraints">The required directional separations.</param>
    /// <param name="token">The cancellation request.</param>
    /// <returns>Whether all constraints fit between the bounds.</returns>
    private static bool Propagate(
        int[] lower,
        int[] upper,
        Separation[] constraints,
        CancellationToken token
    )
    {
        for (int pass = 0; pass < lower.Length; pass++)
        {
            token.ThrowIfCancellationRequested();
            bool changed = false;
            foreach (Separation constraint in constraints)
            {
                long nextLower = (long)lower[constraint.Before] + constraint.Distance;
                long nextUpper = (long)upper[constraint.After] - constraint.Distance;
                if (nextLower > upper[constraint.After] || nextUpper < lower[constraint.Before])
                    return false;
                if (nextLower > lower[constraint.After])
                {
                    lower[constraint.After] = (int)nextLower;
                    changed = true;
                }
                if (nextUpper < upper[constraint.Before])
                {
                    upper[constraint.Before] = (int)nextUpper;
                    changed = true;
                }
            }
            if (!changed)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Alternately relaxes each axis against the exact shapes while keeping the other axis fixed.
    /// </summary>
    /// <param name="items">The desired anchors and compound shapes.</param>
    /// <param name="bounds">The usable frame.</param>
    /// <param name="geometry">The exact integer collision obstacles.</param>
    /// <param name="scale">The visual coordinate scales.</param>
    /// <param name="current">The complete initial placement.</param>
    /// <param name="token">The cancellation request.</param>
    /// <returns>A collision-free layout whose squared displacement cannot be reduced by these axis projections.</returns>
    private static Vector2Int[] Refine(
        IReadOnlyList<RectangleLayoutItem> items,
        Rect bounds,
        CollisionRegion[] geometry,
        Vector2 scale,
        Placement current,
        CancellationToken token
    )
    {
        bool improved;
        do
        {
            improved = false;
            foreach (bool horizontal in new[] { true, false })
            {
                token.ThrowIfCancellationRequested();
                Separation[] constraints = CreateAxisConstraints(
                    geometry,
                    current.Positions,
                    horizontal
                );
                int[] axis = ProjectAxis(
                    items,
                    horizontal ? bounds.xMin : bounds.yMin,
                    horizontal ? bounds.xMax : bounds.yMax,
                    constraints,
                    horizontal,
                    token,
                    out _
                );
                if (axis == null)
                    throw new InvalidOperationException(
                        "The existing map layout violated its separation constraints."
                    );
                Vector2Int[] positions = current
                    .Positions.Select(
                        (point, i) =>
                            horizontal
                                ? new Vector2Int(axis[i], point.y)
                                : new Vector2Int(point.x, axis[i])
                    )
                    .ToArray();
                double cost = GetCost(items, positions, scale);
                if (cost + _tolerance < current.Cost && FindCollision(geometry, positions) == null)
                {
                    current = new Placement(constraints, positions, cost);
                    improved = true;
                }
            }
        } while (improved);
        return current.Positions;
    }

    /// <summary>
    /// Preserves only separations required by shapes that meet on the frozen perpendicular axis.
    /// </summary>
    /// <param name="positions">Their current collision-free anchors.</param>
    /// <param name="geometry">The exact integer collision obstacles.</param>
    /// <param name="horizontal">The axis to relax.</param>
    /// <returns>The exact-shape separations that keep the placement clear on this axis.</returns>
    private static Separation[] CreateAxisConstraints(
        CollisionRegion[] geometry,
        Vector2Int[] positions,
        bool horizontal
    )
    {
        Separation[] constraints = Array.Empty<Separation>();
        foreach (var pair in geometry)
        {
            long x = (long)positions[pair.Second].x - positions[pair.First].x,
                y = (long)positions[pair.Second].y - positions[pair.First].y;
            var r = pair.Bounds;
            long perpendicular = horizontal ? y : x,
                lo = horizontal ? r.Top : r.Left,
                hi = horizontal ? r.Bottom : r.Right;
            if (perpendicular < lo || perpendicular > hi)
                continue;
            bool forward = (horizontal ? x : y) > (horizontal ? r.Right : r.Bottom);
            Separation constraint = forward
                ? new Separation(
                    pair.First,
                    pair.Second,
                    checked((int)((horizontal ? r.Right : r.Bottom) + 1)),
                    horizontal
                )
                : new Separation(
                    pair.Second,
                    pair.First,
                    checked((int)(1 - (horizontal ? r.Left : r.Top))),
                    horizontal
                );
            constraints = AddConstraint(constraints, constraint) ?? constraints;
        }
        return constraints;
    }

    /// <summary>
    /// Measures squared displacement in visual units without favoring a compressed coordinate axis.
    /// </summary>
    /// <param name="items">The original desired anchors.</param>
    /// <param name="positions">The candidate anchors.</param>
    /// <param name="scale">The visual size of a coordinate step.</param>
    /// <returns>The total squared displacement.</returns>
    private static double GetCost(
        IReadOnlyList<RectangleLayoutItem> items,
        Vector2Int[] positions,
        Vector2 scale
    )
    {
        double cost = 0;
        for (int i = 0; i < positions.Length; i++)
        {
            double dx = ((double)positions[i].x - items[i].Position.x) * scale.x;
            double dy = ((double)positions[i].y - items[i].Position.y) * scale.y;
            cost += dx * dx + dy * dy;
        }
        return cost;
    }

    /// <summary>
    /// Rounds a lower lattice bound without overflowing its supported coordinate type.
    /// </summary>
    /// <param name="value">The continuous bound.</param>
    /// <returns>The first supported integer at or above the bound.</returns>
    private static int Ceiling(double value) => checked((int)Math.Ceiling(value));

    /// <summary>
    /// Rounds an upper lattice bound without overflowing its supported coordinate type.
    /// </summary>
    /// <param name="value">The continuous bound.</param>
    /// <returns>The last supported integer at or below the bound.</returns>
    private static int Floor(double value) => checked((int)Math.Floor(value));
}
