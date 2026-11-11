using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Advances capital ships and fighter squadrons with their distinct steering and collision rules.
    /// </summary>
    public sealed class TacticalCombatMotion
    {
        private readonly List<TacticalCombatUnit> _neighbors = new();
        private readonly List<TacticalCombatUnit> _equalHeight = new();
        private float _lower;
        private float _upper;
        private bool _hasLower;
        private bool _hasUpper;
        private bool _moveUp;
        private bool _moveDown;

        /// <summary>
        /// Advances one active unit, then clamps it to the battle volume and updates its index.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="group">The unit's active group.</param>
        /// <param name="recoveryCarrier">The selected recovery carrier, when recovering.</param>
        /// <param name="battleDepth">The battle deployment span.</param>
        /// <param name="spatialIndex">The battle's spatial indexes.</param>
        /// <param name="elapsedMilliseconds">The continuous movement interval.</param>
        public void Advance(
            TacticalCombatUnit unit,
            TacticalCombatGroup group,
            TacticalCombatUnit recoveryCarrier,
            float battleDepth,
            TacticalCombatSpatialIndex spatialIndex,
            float elapsedMilliseconds
        )
        {
            if (unit.Status != TacticalCombatUnitStatus.Active)
                return;
            if (unit.IsFighter)
                AdvanceFighter(
                    unit,
                    group,
                    recoveryCarrier,
                    battleDepth,
                    spatialIndex,
                    elapsedMilliseconds
                );
            else
                AdvanceCapitalShip(
                    unit,
                    group,
                    recoveryCarrier,
                    battleDepth,
                    spatialIndex,
                    elapsedMilliseconds
                );
            float bound = battleDepth * 0.6666667f;
            Vector3 position = unit.Position;
            position.X = ClampCoordinate(position.X, bound);
            position.Y = ClampCoordinate(position.Y, bound);
            position.Z = ClampCoordinate(position.Z, bound);
            unit.Position = position;
            spatialIndex.Update(unit);
        }

        /// <summary>
        /// Applies the lower coordinate bound before the upper bound, including unordered positions.
        /// </summary>
        /// <param name="position">The coordinate after movement.</param>
        /// <param name="bound">The positive-side coordinate boundary.</param>
        /// <returns>The coordinate admitted by the ordered boundary comparisons.</returns>
        private static float ClampCoordinate(float position, float bound)
        {
            float limited = -bound < position ? position : -bound;
            if (limited >= bound)
                limited = bound;
            return limited;
        }

        /// <summary>
        /// Applies capital-ship vertical avoidance, horizontal arrival, and delayed heading steering.
        /// </summary>
        /// <param name="unit">The moving capital ship.</param>
        /// <param name="group">The active group.</param>
        /// <param name="carrier">The selected recovery carrier.</param>
        /// <param name="depth">The deployment span.</param>
        /// <param name="index">The battle spatial index.</param>
        /// <param name="elapsed">The movement interval in milliseconds.</param>
        private void AdvanceCapitalShip(
            TacticalCombatUnit unit,
            TacticalCombatGroup group,
            TacticalCombatUnit carrier,
            float depth,
            TacticalCombatSpatialIndex index,
            float elapsed
        )
        {
            unit.HasCollision = false;
            float speed = unit.GetSpeed();
            if (speed == 0 || float.IsNaN(speed))
                return;
            Vector3 desired = group.GetMemberMovement(unit, depth, carrier);
            speed = Math.Min(speed, group.Formation.MovementSpeed);
            _neighbors.Clear();
            if (desired != Vector3.Zero)
                index.QueryCollision(unit, (float)((double)speed * elapsed * 0.001f), _neighbors);
            ResolveVerticalBounds(unit);
            desired = group.GetMemberMovement(unit, depth, carrier);
            float verticalSpeed = (float)((double)speed * 0.4f);
            float verticalStep = (float)((double)verticalSpeed * elapsed * 0.001f);
            float originalY = unit.Position.Y;
            bool collision = false;
            if (_moveDown && _moveUp)
            {
                double gap = (double)_upper - _lower;
                if ((double)unit.HalfHeight + unit.HalfHeight <= gap)
                    MoveY(unit, gap * 0.5f - unit.Position.Y, verticalStep);
                else
                    collision = true;
            }
            else if (_moveDown)
                MoveY(unit, (double)_upper - unit.Position.Y, verticalStep);
            else if (_moveUp)
                MoveY(unit, (double)_lower - unit.Position.Y, verticalStep);
            if (desired.Y != 0 && !_moveUp && !_moveDown)
            {
                double step = Math.Min(Math.Abs((double)desired.Y), verticalStep);
                if (desired.Y > 0)
                {
                    if (_hasUpper)
                        step = Math.Min(step, (double)_upper - unit.Position.Y);
                    SetY(unit, (float)(unit.Position.Y + step));
                }
                else
                {
                    if (_hasLower)
                        step = Math.Min(step, (double)unit.Position.Y - _lower);
                    SetY(unit, (float)(unit.Position.Y - step));
                }
                if (step == 0)
                    collision = true;
            }
            if (
                unit.Position.Y != originalY
                && TacticalCombatSpatialIndex.Overlaps(unit, _neighbors)
            )
            {
                collision = true;
                SetY(unit, originalY);
            }
            float dot = (float)(
                (double)unit.Heading.Z * unit.DesiredHeading.Z
                + (double)unit.DesiredHeading.X * unit.Heading.X
            );
            if (dot < 0 || float.IsNaN(dot))
                dot = 0;
            Vector3 previous = unit.Position;
            bool horizontal = false;
            if (desired.X != 0 || desired.Z != 0)
            {
                double length = Math.Sqrt(
                    (double)desired.X * desired.X + (double)desired.Z * desired.Z
                );
                double budget = (double)speed * elapsed * 0.001f;
                if (length <= budget && dot >= 0.7070000171661377f)
                {
                    Vector3 position = unit.Position;
                    position.X += desired.X;
                    position.Z += desired.Z;
                    unit.Position = position;
                    horizontal = true;
                    group.GetMemberMovement(unit, depth, carrier);
                }
            }
            unit.DesiredHeading = group.GetMemberHeading(unit);
            float turnAngle = TacticalCombatGeometry.GetHeadingAngle(
                unit.Heading,
                unit.DesiredHeading
            );
            float threshold = (float)((double)3.1415927f * 0.020833334f);
            if (unit.GetSpeed() != 0 && elapsed != 0 && turnAngle > threshold)
                unit.Heading = TacticalCombatGeometry.TurnTowards(
                    unit.Heading,
                    unit.DesiredHeading,
                    unit.TurnRate,
                    elapsed,
                    out _
                );
            if (dot != 0 && unit.DesiredMovement != Vector3.Zero)
            {
                unit.Velocity = new Vector3(
                    (float)((double)unit.Heading.X * verticalSpeed * dot),
                    0,
                    (float)((double)unit.Heading.Z * verticalSpeed * dot)
                );
                unit.Position = TacticalCombatGeometry.Integrate(
                    unit.Position,
                    unit.Velocity,
                    elapsed
                );
                horizontal = true;
            }
            if (horizontal && TacticalCombatSpatialIndex.Overlaps(unit, _neighbors))
                unit.Position = previous;
            unit.HasCollision = collision;
        }

        /// <summary>
        /// Applies fighter pursuit steering and vertical separation from capital ships.
        /// </summary>
        /// <param name="unit">The moving fighter squadron.</param>
        /// <param name="group">The active group.</param>
        /// <param name="carrier">The selected recovery carrier.</param>
        /// <param name="depth">The deployment span.</param>
        /// <param name="index">The battle spatial index.</param>
        /// <param name="elapsed">The movement interval in milliseconds.</param>
        private void AdvanceFighter(
            TacticalCombatUnit unit,
            TacticalCombatGroup group,
            TacticalCombatUnit carrier,
            float depth,
            TacticalCombatSpatialIndex index,
            float elapsed
        )
        {
            Vector3 desired = group.GetMemberMovement(unit, depth, carrier);
            if (desired == Vector3.Zero && group.ActiveOrder == TacticalCombatOrder.Withdraw)
                return;
            unit.DesiredHeading = group.GetMemberHeading(unit);
            unit.Heading = TurnFighter(unit, elapsed);
            float dot = (float)(
                (double)unit.DesiredHeading.X * unit.Heading.X
                + (double)unit.DesiredHeading.Z * unit.Heading.Z
                + (double)unit.DesiredHeading.Y * unit.Heading.Y
            );
            if (dot < 0.25f || float.IsNaN(dot))
                dot = 0.25f;
            double speed = unit.Systems.GetAvailableMovement(
                unit.CommandBudget,
                unit.TractorRestraint
            );
            float distance = (float)(speed * dot * elapsed * 0.001f);
            index.QueryCollision(unit, distance, _neighbors);
            ResolveVerticalBounds(unit);
            float verticalSpeed = (float)(speed * 0.4f);
            float step = (float)((double)verticalSpeed * elapsed * 0.001f);
            if (_moveDown)
            {
                if (_moveUp)
                {
                    double gap = (double)_upper - _lower;
                    if ((double)unit.HalfHeight + unit.HalfHeight <= gap)
                        MoveY(unit, gap * 0.5f - unit.Position.Y, step);
                }
                else
                    MoveY(unit, (double)_upper - unit.Position.Y, step);
            }
            else if (_moveUp)
                MoveY(unit, (double)_lower - unit.Position.Y, step);
            unit.Velocity = new Vector3(
                (float)(speed * unit.Heading.X * dot),
                (float)(speed * unit.Heading.Y * dot),
                (float)(speed * unit.Heading.Z * dot)
            );
            unit.Position = TacticalCombatGeometry.Integrate(unit.Position, unit.Velocity, elapsed);
        }

        /// <summary>
        /// Turns a fighter in the horizontal plane while interpolating its vertical heading.
        /// </summary>
        /// <param name="unit">The steering squadron.</param>
        /// <param name="elapsed">The movement interval in milliseconds.</param>
        /// <returns>The normalized heading after steering.</returns>
        private static Vector3 TurnFighter(TacticalCombatUnit unit, float elapsed)
        {
            float angle = TacticalCombatGeometry.GetHeadingAngle(unit.Heading, unit.DesiredHeading);
            if (
                unit.Systems.GetAvailableMovement(unit.CommandBudget, unit.TractorRestraint) == 0
                || angle == 0
                || float.IsNaN(angle)
                || elapsed == 0
                || float.IsNaN(elapsed)
            )
                return unit.Heading;
            double ratio = (double)unit.TurnRate * elapsed / ((double)angle * 1000);
            if (ratio >= 1)
                return unit.DesiredHeading;
            float roundedRatio = (float)ratio;
            float dot = (float)(
                (double)unit.DesiredHeading.Z * unit.Heading.Z
                + (double)unit.DesiredHeading.X * unit.Heading.X
            );
            if (float.IsNaN(dot) || dot > 1)
                dot = 1;
            if (dot < -1)
                dot = -1;
            double turn = Math.Acos(dot) * roundedRatio;
            double side =
                -(double)unit.Heading.Z * unit.DesiredHeading.X
                + (double)unit.DesiredHeading.Z * unit.Heading.X;
            if (side < 0 || double.IsNaN(side))
                turn = -turn;
            double cosine = Math.Cos(turn);
            double sine = Math.Sin(turn);
            return TacticalCombatGeometry.NormalizeMovement(
                new Vector3(
                    (float)((double)(float)cosine * unit.Heading.X - sine * unit.Heading.Z),
                    (float)(
                        (double)unit.Heading.Y
                        + ((double)unit.DesiredHeading.Y - unit.Heading.Y) * roundedRatio
                    ),
                    (float)(sine * unit.Heading.X + cosine * unit.Heading.Z)
                )
            );
        }

        /// <summary>
        /// Resolves upper and lower separation bounds, including equal-height tie handling.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        private void ResolveVerticalBounds(TacticalCombatUnit unit)
        {
            _lower = -float.MaxValue;
            _upper = float.MaxValue;
            _hasLower = false;
            _hasUpper = false;
            _moveUp = false;
            _moveDown = false;
            _equalHeight.Clear();
            for (int index = 0; index < _neighbors.Count; index++)
            {
                TacticalCombatUnit candidate = _neighbors[index];
                double delta = (double)candidate.Position.Y - unit.Position.Y;
                if (delta > 0)
                    UpdateUpper(unit, candidate);
                else if (delta < 0)
                    UpdateLower(unit, candidate);
                else
                    _equalHeight.Add(candidate);
            }
            for (int index = 0; index < _equalHeight.Count; index++)
            {
                TacticalCombatUnit candidate = _equalHeight[index];
                if (unit.Index < candidate.Index)
                {
                    if (!_hasLower)
                        UpdateUpper(unit, candidate);
                    else if (
                        !_hasUpper
                        || (double)unit.Position.Y - _lower <= (double)_upper - unit.Position.Y
                    )
                        UpdateLower(unit, candidate);
                    else
                        UpdateUpper(unit, candidate);
                }
                else if (!_hasUpper)
                    UpdateLower(unit, candidate);
                else if (!_hasLower)
                    UpdateUpper(unit, candidate);
                else if ((double)unit.Position.Y - _lower <= (double)_upper - unit.Position.Y)
                    UpdateLower(unit, candidate);
                else
                    UpdateUpper(unit, candidate);
            }
        }

        /// <summary>
        /// Adds the vertical exclusion bound below a higher ship.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="candidate">The higher capital ship.</param>
        private void UpdateUpper(TacticalCombatUnit unit, TacticalCombatUnit candidate)
        {
            _hasUpper = true;
            double value = (double)candidate.Position.Y - candidate.HalfHeight - unit.HalfHeight;
            _upper = (float)Math.Min(value, _upper);
            if (value < unit.Position.Y)
                _moveDown = true;
        }

        /// <summary>
        /// Adds the vertical exclusion bound above a lower ship.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="candidate">The lower capital ship.</param>
        private void UpdateLower(TacticalCombatUnit unit, TacticalCombatUnit candidate)
        {
            _hasLower = true;
            double value = (double)candidate.Position.Y + candidate.HalfHeight + unit.HalfHeight;
            _lower = (float)Math.Max(value, _lower);
            if (unit.Position.Y < value)
                _moveUp = true;
        }

        /// <summary>
        /// Moves vertically by the available signed distance.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="delta">The requested signed vertical displacement.</param>
        /// <param name="maximumStep">The available vertical movement.</param>
        private static void MoveY(TacticalCombatUnit unit, double delta, float maximumStep)
        {
            if (delta > 0)
                SetY(unit, (float)(unit.Position.Y + Math.Min(delta, maximumStep)));
            else if (delta < 0)
                SetY(unit, (float)(unit.Position.Y - Math.Min(-delta, maximumStep)));
        }

        /// <summary>
        /// Updates a position's vertical component without changing its horizontal coordinates.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="y">The new vertical coordinate.</param>
        private static void SetY(TacticalCombatUnit unit, float y)
        {
            Vector3 position = unit.Position;
            position.Y = y;
            unit.Position = position;
        }
    }
}
