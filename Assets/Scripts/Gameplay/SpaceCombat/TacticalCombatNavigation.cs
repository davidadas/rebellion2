using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Owns the battle's navigation lattice and resolves the four group maneuvers.
    /// </summary>
    public sealed class TacticalCombatNavigation
    {
        private readonly List<Vector3> _waypoints = new(105);
        public IReadOnlyList<Vector3> Waypoints { get; }

        /// <summary>
        /// Creates four concentric navigation grids around the battle origin.
        /// </summary>
        /// <param name="battleDepth">The encounter's deployment span.</param>
        public TacticalCombatNavigation(float battleDepth)
        {
            Waypoints = _waypoints.AsReadOnly();
            float spacing = battleDepth * 0.125f;
            for (int ring = 1; ring <= 4; ring++)
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
                if (ring == 1 || x != 0 || y != 0 || z != 0)
                {
                    float radius = ring * spacing;
                    _waypoints.Add(new Vector3(x * radius, y * radius, z * radius));
                }
        }

        /// <summary>
        /// Finds the closest authored lattice point with registration order resolving equal distances.
        /// </summary>
        /// <param name="position">The requested navigation position.</param>
        /// <returns>The nearest lattice position.</returns>
        public Vector3 GetNearestWaypoint(Vector3 position)
        {
            float best = float.MaxValue;
            Vector3 selected = Vector3.Zero;
            foreach (Vector3 point in _waypoints)
            {
                Vector3 delta = point - position;
                float distance = (float)(
                    (double)delta.Y * delta.Y
                    + (double)delta.Z * delta.Z
                    + (double)delta.X * delta.X
                );
                if (distance < best)
                {
                    best = distance;
                    selected = point;
                }
            }
            return selected;
        }

        /// <summary>
        /// Resolves a maneuver from designated targets before falling back to opposing capital ships and fighters.
        /// </summary>
        /// <param name="group">The maneuvering group.</param>
        /// <param name="units">The ordered battle roster.</param>
        /// <param name="opposingCenter">The preceding frame's opposing center.</param>
        /// <param name="waypoint">Receives the resolved maneuver point.</param>
        /// <returns>Whether a directional target was available.</returns>
        public bool TryGetManeuver(
            TacticalCombatGroup group,
            IReadOnlyList<TacticalCombatUnit> units,
            Vector3 opposingCenter,
            out Vector3 waypoint
        )
        {
            Vector3 origin = group.Formation.Position;
            Vector3 delta = opposingCenter - origin;
            Vector3 direction = group.ActiveOrder switch
            {
                TacticalCombatOrder.LeftHook => new Vector3(-delta.Z, delta.Y, delta.X),
                TacticalCombatOrder.RightHook => new Vector3(delta.Z, delta.Y, -delta.X),
                TacticalCombatOrder.Hammer => new Vector3(delta.X, delta.Z, -delta.Y),
                TacticalCombatOrder.Anvil => new Vector3(delta.X, delta.Z, delta.Y),
                _ => throw new InvalidOperationException("The selected order is not a maneuver."),
            };
            TacticalCombatUnit selected = SelectDirectional(group.Targets, group, direction, null);
            selected ??= SelectDirectional(units, group, direction, false);
            selected ??= SelectDirectional(units, group, direction, true);
            waypoint = default;
            if (selected == null)
                return false;
            delta = selected.Position - origin;
            float cosine = BitConverter.Int32BitsToSingle(0x3f6c835e);
            float sine = BitConverter.Int32BitsToSingle(0x3ec3ef16);
            if (group.ActiveOrder is TacticalCombatOrder.RightHook or TacticalCombatOrder.Hammer)
                sine = -sine;
            Vector3 offset;
            if (group.ActiveOrder is TacticalCombatOrder.LeftHook or TacticalCombatOrder.RightHook)
                offset = new Vector3(
                    (float)(((double)cosine * delta.X - (double)sine * delta.Z) * 0.75f),
                    delta.Y * 0.75f,
                    (float)(((double)sine * delta.X + (double)cosine * delta.Z) * 0.75f)
                );
            else
                offset = new Vector3(
                    delta.X * 0.75f,
                    (float)(((double)cosine * delta.Y + (double)sine * delta.Z) * 0.75f),
                    (float)(((double)cosine * delta.Z - (double)sine * delta.Y) * 0.75f)
                );
            waypoint = GetNearestWaypoint(origin + offset);
            return true;
        }

        /// <summary>
        /// Selects the furthest positive projection along a maneuver direction.
        /// </summary>
        /// <param name="candidates">The ordered target candidates.</param>
        /// <param name="group">The requesting group.</param>
        /// <param name="direction">The maneuver's directional projection.</param>
        /// <param name="fighters">The required category, or null for designated targets.</param>
        /// <returns>The directional target, or null when no projection is positive.</returns>
        private static TacticalCombatUnit SelectDirectional(
            IReadOnlyList<TacticalCombatUnit> candidates,
            TacticalCombatGroup group,
            Vector3 direction,
            bool? fighters
        )
        {
            TacticalCombatUnit selected = null;
            float best = 0;
            if (direction == Vector3.Zero)
                direction = group.SideLane;
            direction = TacticalCombatGeometry.Normalize(direction);
            foreach (TacticalCombatUnit candidate in candidates)
            {
                if (
                    !candidate.IsTargetable
                    || candidate.Side == group.Side
                    || fighters.HasValue && candidate.IsFighter != fighters.Value
                )
                    continue;
                float score = (float)(
                    (double)candidate.Position.X * direction.X
                    + (double)candidate.Position.Y * direction.Y
                    + (double)candidate.Position.Z * direction.Z
                );
                if (score > best)
                {
                    best = score;
                    selected = candidate;
                }
            }
            return selected;
        }
    }
}
