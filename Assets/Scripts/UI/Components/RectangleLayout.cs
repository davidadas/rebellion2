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

        /// <summary>
        /// Captures a feasible projection and its displacement cost.
        /// </summary>
        /// <param name="constraints">The directional constraints defining this placement.</param>
        /// <param name="positions">The projected integer anchors.</param>
        /// <param name="cost">The squared visual displacement.</param>
        public Placement(Separation[] constraints, Vector2Int[] positions, double cost)
        {
            Constraints = constraints;
            Positions = positions;
            Cost = cost;
        }
    }

    /// <summary>
    /// Searches directional separation choices, preferring the least-displacing feasible choice at each collision.
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
        Placement initial = Project(
            items,
            bounds,
            coordinateScale,
            Array.Empty<Separation>(),
            cancellationToken
        );
        if (initial != null && FindCollision(items, initial.Positions, gap) == null)
            return initial.Positions;
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
            placement = FindPlacement(envelopes, bounds, gap, coordinateScale, cancellationToken);
        placement ??= FindPlacement(items, bounds, gap, coordinateScale, cancellationToken);
        if (placement == null)
            throw new ArgumentException(
                "Not enough room to arrange these items. Move items or shorten their labels."
            );
        return Refine(items, bounds, gap, coordinateScale, placement, cancellationToken);
    }

    /// <summary>
    /// Finds a feasible directional placement using displacement-ranked backtracking.
    /// </summary>
    /// <param name="items">The rigid occupied shapes.</param>
    /// <param name="bounds">The usable frame.</param>
    /// <param name="gap">The minimum clearance.</param>
    /// <param name="scale">The visual coordinate scales.</param>
    /// <param name="token">The cancellation request.</param>
    /// <returns>A complete placement, or null if no directional choices can fit.</returns>
    private static Placement FindPlacement(
        IReadOnlyList<RectangleLayoutItem> items,
        Rect bounds,
        Vector2 gap,
        Vector2 scale,
        CancellationToken token
    )
    {
        Stack<Placement> pending = new();
        Placement initial = Project(items, bounds, scale, CreateSymmetryConstraints(items), token);
        if (initial != null)
            pending.Push(initial);
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            Placement current = pending.Pop();
            Separation[] alternatives = FindCollision(items, current.Positions, gap);
            if (alternatives == null)
                return current;
            List<Placement> candidates = new();
            foreach (Separation alternative in alternatives)
            {
                Separation[] constraints = AddConstraint(current.Constraints, alternative);
                if (constraints == null)
                    continue;
                Placement candidate = Project(items, bounds, scale, constraints, token);
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
    /// <param name="items">The rigid shapes.</param>
    /// <param name="positions">Their current anchors.</param>
    /// <param name="gap">The required clearance.</param>
    /// <returns>Four alternatives, or null when the entire placement is clear.</returns>
    private static Separation[] FindCollision(
        IReadOnlyList<RectangleLayoutItem> items,
        Vector2Int[] positions,
        Vector2 gap
    )
    {
        double largest = 0;
        Separation[] result = null;
        for (int i = 0; i < items.Count; i++)
        for (int j = i + 1; j < items.Count; j++)
            foreach (Rect first in items[i].Footprints)
            foreach (Rect second in items[j].Footprints)
            {
                double overlapX =
                    Math.Min(
                        (double)first.xMax + positions[i].x,
                        (double)second.xMax + positions[j].x
                    )
                    - Math.Max(
                        (double)first.xMin + positions[i].x,
                        (double)second.xMin + positions[j].x
                    )
                    + gap.x;
                double overlapY =
                    Math.Min(
                        (double)first.yMax + positions[i].y,
                        (double)second.yMax + positions[j].y
                    )
                    - Math.Max(
                        (double)first.yMin + positions[i].y,
                        (double)second.yMin + positions[j].y
                    )
                    + gap.y;
                if (
                    overlapX <= _tolerance
                    || overlapY <= _tolerance
                    || overlapX * overlapY <= largest
                )
                    continue;
                largest = overlapX * overlapY;
                result = new[]
                {
                    new Separation(i, j, Ceiling((double)first.xMax - second.xMin + gap.x), true),
                    new Separation(j, i, Ceiling((double)second.xMax - first.xMin + gap.x), true),
                    new Separation(i, j, Ceiling((double)first.yMax - second.yMin + gap.y), false),
                    new Separation(j, i, Ceiling((double)second.yMax - first.yMin + gap.y), false),
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
    /// <returns>The projected placement, or null for contradictory constraints.</returns>
    private static Placement Project(
        IReadOnlyList<RectangleLayoutItem> items,
        Rect bounds,
        Vector2 scale,
        Separation[] constraints,
        CancellationToken token
    )
    {
        int[] x = ProjectAxis(
            items,
            bounds.xMin,
            bounds.xMax,
            constraints.Where(c => c.Horizontal).ToArray(),
            true,
            token
        );
        if (x == null)
            return null;
        int[] y = ProjectAxis(
            items,
            bounds.yMin,
            bounds.yMax,
            constraints.Where(c => !c.Horizontal).ToArray(),
            false,
            token
        );
        if (y == null)
            return null;
        Vector2Int[] positions = new Vector2Int[items.Count];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = new Vector2Int(x[i], y[i]);
        return new Placement(constraints, positions, GetCost(items, positions, scale));
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
    /// <returns>Feasible integer coordinates, or null for an infeasible axis.</returns>
    private static int[] ProjectAxis(
        IReadOnlyList<RectangleLayoutItem> items,
        float minimum,
        float maximum,
        Separation[] constraints,
        bool horizontal,
        CancellationToken token
    )
    {
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
    /// <param name="gap">The minimum clearance.</param>
    /// <param name="scale">The visual coordinate scales.</param>
    /// <param name="current">The complete initial placement.</param>
    /// <param name="token">The cancellation request.</param>
    /// <returns>A collision-free layout whose squared displacement cannot be reduced by these axis projections.</returns>
    private static Vector2Int[] Refine(
        IReadOnlyList<RectangleLayoutItem> items,
        Rect bounds,
        Vector2 gap,
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
                    items,
                    current.Positions,
                    gap,
                    horizontal
                );
                int[] axis = ProjectAxis(
                    items,
                    horizontal ? bounds.xMin : bounds.yMin,
                    horizontal ? bounds.xMax : bounds.yMax,
                    constraints,
                    horizontal,
                    token
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
                if (
                    cost + _tolerance < current.Cost
                    && FindCollision(items, positions, gap) == null
                )
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
    /// <param name="items">The rigid shapes.</param>
    /// <param name="positions">Their current collision-free anchors.</param>
    /// <param name="gap">The minimum clearance.</param>
    /// <param name="horizontal">The axis to relax.</param>
    /// <returns>The exact-shape separations that keep the placement clear on this axis.</returns>
    private static Separation[] CreateAxisConstraints(
        IReadOnlyList<RectangleLayoutItem> items,
        Vector2Int[] positions,
        Vector2 gap,
        bool horizontal
    )
    {
        Separation[] constraints = Array.Empty<Separation>();
        for (int i = 0; i < items.Count; i++)
        for (int j = i + 1; j < items.Count; j++)
            foreach (Rect first in items[i].Footprints)
            foreach (Rect second in items[j].Footprints)
            {
                double perpendicularGap = horizontal ? gap.y : gap.x;
                double overlap = horizontal
                    ? Math.Min(
                        (double)first.yMax + positions[i].y,
                        (double)second.yMax + positions[j].y
                    )
                        - Math.Max(
                            (double)first.yMin + positions[i].y,
                            (double)second.yMin + positions[j].y
                        )
                        + perpendicularGap
                    : Math.Min(
                        (double)first.xMax + positions[i].x,
                        (double)second.xMax + positions[j].x
                    )
                        - Math.Max(
                            (double)first.xMin + positions[i].x,
                            (double)second.xMin + positions[j].x
                        )
                        + perpendicularGap;
                if (overlap <= _tolerance)
                    continue;
                double firstMax = horizontal ? first.xMax : first.yMax;
                double secondMin = horizontal ? second.xMin : second.yMin;
                double axisGap = horizontal ? gap.x : gap.y;
                bool forward =
                    (horizontal ? positions[j].x - positions[i].x : positions[j].y - positions[i].y)
                    >= firstMax - secondMin + axisGap - _tolerance;
                Separation constraint = forward
                    ? new Separation(i, j, Ceiling(firstMax - secondMin + axisGap), horizontal)
                    : new Separation(
                        j,
                        i,
                        Ceiling(
                            (horizontal ? second.xMax - first.xMin : second.yMax - first.yMin)
                                + axisGap
                        ),
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
