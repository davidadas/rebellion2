using System;
using System.Collections.Generic;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Maintains separate capital-ship and fighter indexes ordered by horizontal position.
    /// Queries reuse caller-owned lists and return results in stable battle order.
    /// </summary>
    public sealed class TacticalCombatSpatialIndex
    {
        private readonly List<TacticalCombatUnit> _capitalShips = new();
        private readonly List<TacticalCombatUnit> _fighters = new();
        private readonly Dictionary<TacticalCombatUnit, float> _indexedPositions = new();
        private static readonly Comparison<TacticalCombatUnit> _compareBattleOrder = CompareIndices;

        /// <summary>
        /// Builds the spatial indexes from the battle's active units.
        /// </summary>
        /// <param name="units">The captured battle roster.</param>
        public TacticalCombatSpatialIndex(IReadOnlyList<TacticalCombatUnit> units)
        {
            if (units == null)
                throw new ArgumentNullException(nameof(units));
            HashSet<int> identifiers = new();
            for (int index = 0; index < units.Count; index++)
            {
                if (!identifiers.Add(units[index].Index))
                    throw new ArgumentException(
                        "Tactical units must have unique battle identifiers.",
                        nameof(units)
                    );
                Update(units[index]);
            }
        }

        /// <summary>
        /// Removes a unit from its previous position and inserts its current active position.
        /// </summary>
        /// <param name="unit">The unit whose position or participation changed.</param>
        public void Update(TacticalCombatUnit unit)
        {
            List<TacticalCombatUnit> index = unit.IsFighter ? _fighters : _capitalShips;
            if (_indexedPositions.TryGetValue(unit, out float previousX))
            {
                int previous = Find(index, previousX, unit.Index);
                if (previous < index.Count && ReferenceEquals(index[previous], unit))
                    index.RemoveAt(previous);
                _indexedPositions.Remove(unit);
            }
            if (!unit.IsTargetable)
                return;
            index.Insert(Find(index, unit.Position.X, unit.Index), unit);
            _indexedPositions.Add(unit, unit.Position.X);
        }

        /// <summary>
        /// Finds capital ships that constrain vertical separation during movement.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="movementDistance">The distance available during this movement update.</param>
        /// <param name="results">Receives neighbors in stable battle order.</param>
        public void QueryCollision(
            TacticalCombatUnit unit,
            float movementDistance,
            List<TacticalCombatUnit> results
        )
        {
            results.Clear();
            int center = Find(_capitalShips, unit.Position.X, unit.Index);
            float verticalRange = (float)((double)movementDistance * 0.4f + unit.HalfHeight);
            for (int index = center - 1; index >= 0; index--)
            {
                TacticalCombatUnit candidate = _capitalShips[index];
                float horizontalRange = (float)((double)candidate.Proximity + unit.Proximity);
                if ((double)unit.Position.X - candidate.Position.X > horizontalRange)
                    break;
                AddCollisionCandidate(unit, candidate, horizontalRange, verticalRange, results);
            }
            for (int index = center; index < _capitalShips.Count; index++)
            {
                TacticalCombatUnit candidate = _capitalShips[index];
                if (ReferenceEquals(candidate, unit))
                    continue;
                float horizontalRange = (float)((double)candidate.Proximity + unit.Proximity);
                if ((double)candidate.Position.X - unit.Position.X > horizontalRange)
                    break;
                AddCollisionCandidate(unit, candidate, horizontalRange, verticalRange, results);
            }
            results.Sort(_compareBattleOrder);
        }

        /// <summary>
        /// Finds opposing fighter squadrons inside a weapon-range bounding box.
        /// Firing control applies the spherical weapon ranges to these candidates.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="range">The largest active battery range.</param>
        /// <param name="results">Receives eligible opposing squadrons.</param>
        public void QueryFighters(
            TacticalCombatUnit unit,
            float range,
            List<TacticalCombatUnit> results
        )
        {
            results.Clear();
            int center = Find(_fighters, unit.Position.X, unit.Index);
            for (int index = center - 1; index >= 0; index--)
            {
                TacticalCombatUnit candidate = _fighters[index];
                if ((double)unit.Position.X - candidate.Position.X > range)
                    break;
                AddFighterCandidate(unit, candidate, range, results);
            }
            for (int index = center; index < _fighters.Count; index++)
            {
                TacticalCombatUnit candidate = _fighters[index];
                if ((double)candidate.Position.X - unit.Position.X > range)
                    break;
                AddFighterCandidate(unit, candidate, range, results);
            }
            results.Sort(_compareBattleOrder);
        }

        /// <summary>
        /// Compares stable battle identifiers for query-result ordering.
        /// </summary>
        /// <param name="left">The first unit.</param>
        /// <param name="right">The second unit.</param>
        /// <returns>The identifier comparison.</returns>
        private static int CompareIndices(TacticalCombatUnit left, TacticalCombatUnit right)
        {
            return left.Index.CompareTo(right.Index);
        }

        /// <summary>
        /// Tests cylindrical hull overlap against the supplied capital-ship neighbors.
        /// </summary>
        /// <param name="unit">The moved unit.</param>
        /// <param name="neighbors">The previously resolved collision candidates.</param>
        /// <returns>Whether any hull overlaps the moved unit.</returns>
        public static bool Overlaps(
            TacticalCombatUnit unit,
            IReadOnlyList<TacticalCombatUnit> neighbors
        )
        {
            for (int index = 0; index < neighbors.Count; index++)
            {
                TacticalCombatUnit candidate = neighbors[index];
                if (
                    Math.Abs((double)candidate.Position.Y - unit.Position.Y)
                    >= (double)candidate.HalfHeight + unit.HalfHeight
                )
                    continue;
                double deltaX = (double)unit.Position.X - candidate.Position.X;
                double deltaZ = (double)unit.Position.Z - candidate.Position.Z;
                double radius = ((double)unit.CollisionRadius + candidate.CollisionRadius) * 0.5f;
                if (deltaX * (float)deltaX + deltaZ * (float)deltaZ < radius * (float)radius)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a vertically and horizontally reachable collision candidate.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="candidate">The neighboring capital ship.</param>
        /// <param name="horizontalRange">The combined proximity envelope.</param>
        /// <param name="verticalRange">The moving unit's vertical envelope.</param>
        /// <param name="results">The reusable query result list.</param>
        private static void AddCollisionCandidate(
            TacticalCombatUnit unit,
            TacticalCombatUnit candidate,
            float horizontalRange,
            float verticalRange,
            List<TacticalCombatUnit> results
        )
        {
            if (
                Math.Abs((double)candidate.Position.Y - unit.Position.Y)
                    <= (double)candidate.HalfHeight + verticalRange
                && Math.Abs((double)candidate.Position.Z - unit.Position.Z) <= horizontalRange
            )
                results.Add(candidate);
        }

        /// <summary>
        /// Adds an active opposing squadron inside the query's remaining dimensions.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="candidate">The candidate squadron.</param>
        /// <param name="range">The weapon-range bound.</param>
        /// <param name="results">The reusable query result list.</param>
        private static void AddFighterCandidate(
            TacticalCombatUnit unit,
            TacticalCombatUnit candidate,
            float range,
            List<TacticalCombatUnit> results
        )
        {
            if (
                candidate.Side != unit.Side
                && candidate.Status == TacticalCombatUnitStatus.Active
                && Math.Abs((double)candidate.Position.Y - unit.Position.Y) <= range
                && Math.Abs((double)candidate.Position.Z - unit.Position.Z) <= range
            )
                results.Add(candidate);
        }

        /// <summary>
        /// Finds the insertion point for a position and stable battle index.
        /// </summary>
        /// <param name="index">The ordered unit collection.</param>
        /// <param name="x">The indexed horizontal position.</param>
        /// <param name="unitIndex">The stable battle index used for ties.</param>
        /// <returns>The zero-based insertion point.</returns>
        private int Find(List<TacticalCombatUnit> index, float x, int unitIndex)
        {
            int lower = 0;
            int upper = index.Count;
            while (lower < upper)
            {
                int middle = lower + (upper - lower) / 2;
                TacticalCombatUnit candidate = index[middle];
                float candidateX = _indexedPositions[candidate];
                if (candidateX < x || candidateX == x && candidate.Index < unitIndex)
                    lower = middle + 1;
                else
                    upper = middle;
            }
            return lower;
        }
    }
}
