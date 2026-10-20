using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;

namespace Rebellion.Simulation.Tactical
{
    /// <summary>
    /// Assigns formation-preserving destinations and advances saved battle positions without presentation dependencies.
    /// </summary>
    public static class TacticalMovement
    {
        private const float _turnRadiansPerSpeedPoint = 10f * (float)Math.PI / 180f;
        private const float _capitalShipTurnSpeedMultiplier = 0.5f;

        /// <summary>
        /// Translates the selected friendly formation toward a destination inside the playable volume.
        /// </summary>
        /// <param name="battle">The battle owning the ordered units.</param>
        /// <param name="bounds">The playable battle volume.</param>
        /// <param name="selection">The units selected by the player.</param>
        /// <param name="factionInstanceID">The faction issuing the order.</param>
        /// <param name="destination">The requested formation center.</param>
        public static void MoveTo(
            ActiveBattle battle,
            BattleMapBounds bounds,
            IReadOnlyList<CombatUnit> selection,
            string factionInstanceID,
            BattleVector3 destination
        )
        {
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));
            if (bounds == null)
                throw new ArgumentNullException(nameof(bounds));
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            Vector3 target = ToVector(destination);
            if (!IsFinite(target))
                throw new ArgumentException(
                    "Move destinations must be finite.",
                    nameof(destination)
                );

            List<CombatUnit> ordered = GetControlledUnits(battle, selection, factionInstanceID);
            if (ordered.Count == 0)
                return;
            Vector3 center = Vector3.Zero;
            Vector3 minimum = new Vector3(float.PositiveInfinity);
            Vector3 maximum = new Vector3(float.NegativeInfinity);
            foreach (CombatUnit unit in ordered)
            {
                Vector3 position = ToVector(unit.Position);
                center += position;
                minimum = Vector3.Min(minimum, position);
                maximum = Vector3.Max(maximum, position);
            }
            center /= ordered.Count;
            Vector3 offset = target - center;
            offset = new Vector3(
                ClampOffset(offset.X, bounds.MinimumX - minimum.X, bounds.MaximumX - maximum.X),
                ClampOffset(offset.Y, bounds.MinimumY - minimum.Y, bounds.MaximumY - maximum.Y),
                ClampOffset(offset.Z, bounds.MinimumZ - minimum.Z, bounds.MaximumZ - maximum.Z)
            );
            foreach (CombatUnit unit in ordered)
                unit.MoveDestination = FromVector(ToVector(unit.Position) + offset);
        }

        /// <summary>
        /// Cancels pending orders only for selected units controlled by the issuing faction.
        /// </summary>
        /// <param name="battle">The battle owning the ordered units.</param>
        /// <param name="selection">The units selected by the player.</param>
        /// <param name="factionInstanceID">The faction issuing the order.</param>
        public static void Stop(
            ActiveBattle battle,
            IReadOnlyList<CombatUnit> selection,
            string factionInstanceID
        )
        {
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            foreach (CombatUnit unit in GetControlledUnits(battle, selection, factionInstanceID))
                unit.MoveDestination = null;
        }

        /// <summary>
        /// Moves forward at sublight speed while fighters turn at ten degrees per speed point per
        /// second and capital ships turn at five degrees per speed point per second.
        /// Follows a circular turn onto a tangent approach to reach the destination without orbiting it.
        /// </summary>
        /// <param name="unit">The persistent battle unit.</param>
        /// <param name="deltaTime">The non-negative elapsed battle time in seconds.</param>
        public static void Advance(CombatUnit unit, float deltaTime)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (
                unit.MoveDestination == null
                || unit.HasRetreated
                || deltaTime == 0f
                || (
                    unit.FlightState != FighterFlightState.Deployed
                    && unit.FlightState != FighterFlightState.Returning
                )
            )
                return;
            float speed = unit.GetUnit() switch
            {
                CapitalShip ship when ship.MaxHullStrength > 0 && ship.CurrentHullStrength <= 0 =>
                    0f,
                Starfighter fighter
                    when fighter.CurrentSquadronSize <= 0
                        || (fighter.MaxHullStrength > 0 && fighter.CurrentHullStrength <= 0) => 0f,
                CapitalShip ship => ship.GetSublightSpeed(),
                Starfighter fighter => fighter.GetSublightSpeed(),
                _ => 0f,
            };
            if (speed <= 0f)
                return;
            Vector3 position = ToVector(unit.Position);
            Vector3 destination = ToVector(unit.MoveDestination);
            Vector3 displacement = destination - position;
            float distance = displacement.Length();
            if (distance == 0f)
            {
                unit.MoveDestination = null;
                return;
            }
            Vector3 forward = displacement / distance;
            Vector3 currentForward = Vector3.Normalize(ToVector(unit.Forward));
            Vector3 axis = Vector3.Cross(currentForward, forward);
            double travel = (double)speed * deltaTime;
            if (axis.LengthSquared() == 0f && Vector3.Dot(currentForward, forward) > 0f)
            {
                MoveStraight(unit, position, destination, travel);
                return;
            }

            // Opposing headings have no cross-product axis. Prefer a level turn where possible.
            if (axis.LengthSquared() == 0f)
            {
                axis = Vector3.UnitY - currentForward * currentForward.Y;
                if (axis.LengthSquared() == 0f)
                    axis = Vector3.UnitX;
            }
            Vector3 tangent = Vector3.Normalize(
                Vector3.Cross(Vector3.Normalize(axis), currentForward)
            );
            float turnRadiansPerSpeedPoint =
                unit.GetUnit() is CapitalShip
                    ? _turnRadiansPerSpeedPoint * _capitalShipTurnSpeedMultiplier
                    : _turnRadiansPerSpeedPoint;
            float radius = 1f / turnRadiansPerSpeedPoint;
            (double angle, double length) = GetTurnArc(
                displacement,
                currentForward,
                tangent,
                radius
            );
            (double oppositeAngle, double oppositeLength) = GetTurnArc(
                displacement,
                currentForward,
                -tangent,
                radius
            );
            if (oppositeLength < length)
            {
                tangent = -tangent;
                angle = oppositeAngle;
            }

            double arcTravel = radius * angle;
            double stepAngle = Math.Min(angle, travel / radius);
            float sine = (float)Math.Sin(stepAngle);
            float cosine = (float)Math.Cos(stepAngle);
            double halfSine = Math.Sin(stepAngle / 2d);
            position +=
                radius * (currentForward * sine + tangent * (float)(2d * halfSine * halfSine));
            SetVector(unit.Position, position);
            SetVector(unit.Forward, Vector3.Normalize(currentForward * cosine + tangent * sine));
            if (travel > arcTravel)
                MoveStraight(unit, position, destination, travel - arcTravel);
        }

        /// <summary>
        /// Finds the forward tangent from a fixed-radius turning circle to the destination.
        /// </summary>
        /// <param name="displacement">The destination relative to the starting position.</param>
        /// <param name="forward">The starting unit heading.</param>
        /// <param name="tangent">The direction toward the center of the turning circle.</param>
        /// <param name="radius">The turning radius implied by movement speed and angular speed.</param>
        /// <returns>The required turn angle and total arc-plus-straight distance, or an infinite distance when no tangent exists.</returns>
        private static (double Angle, double Length) GetTurnArc(
            Vector3 displacement,
            Vector3 forward,
            Vector3 tangent,
            float radius
        )
        {
            double ahead = Vector3.Dot(displacement, forward);
            double side = (double)Vector3.Dot(displacement, tangent) - radius;
            double straightSquared = ahead * ahead + side * side - (double)radius * radius;
            if (straightSquared < 0d)
                return (0d, double.PositiveInfinity);
            double straight = Math.Sqrt(straightSquared);
            double angle = Math.Atan2(
                radius * ahead + side * straight,
                -radius * side + ahead * straight
            );
            if (angle < 0d)
                angle += 2d * Math.PI;
            return (angle, radius * angle + straight);
        }

        /// <summary>
        /// Advances along an aligned straight approach, stopping exactly at the destination.
        /// </summary>
        /// <param name="unit">The persistent battle unit.</param>
        /// <param name="position">The start of the straight approach.</param>
        /// <param name="destination">The requested destination.</param>
        /// <param name="travel">The available travel distance.</param>
        private static void MoveStraight(
            CombatUnit unit,
            Vector3 position,
            Vector3 destination,
            double travel
        )
        {
            Vector3 displacement = destination - position;
            float distance = displacement.Length();
            if (distance > 0f)
                SetVector(unit.Forward, displacement / distance);
            if (travel >= distance)
            {
                SetVector(unit.Position, destination);
                unit.MoveDestination = null;
            }
            else
                SetVector(unit.Position, position + displacement / distance * (float)travel);
        }

        /// <summary>
        /// Resolves selected non-retreated members through the battle's participant ownership boundary.
        /// </summary>
        /// <param name="battle">The authoritative battle roster.</param>
        /// <param name="selection">The requested selection.</param>
        /// <param name="factionInstanceID">The issuing faction identifier.</param>
        /// <returns>The unique controlled battle units.</returns>
        private static List<CombatUnit> GetControlledUnits(
            ActiveBattle battle,
            IReadOnlyList<CombatUnit> selection,
            string factionInstanceID
        )
        {
            HashSet<CombatUnit> selected = new HashSet<CombatUnit>(selection);
            List<CombatUnit> units = new List<CombatUnit>();
            if (string.IsNullOrWhiteSpace(factionInstanceID))
                return units;
            foreach (BattleParticipant participant in battle.GetParticipants())
            {
                if (
                    !string.Equals(
                        participant.FactionInstanceID,
                        factionInstanceID,
                        StringComparison.Ordinal
                    )
                )
                    continue;
                foreach (CombatUnit unit in participant.GetCombatants())
                    if (
                        !unit.HasRetreated
                        && unit.FlightState == FighterFlightState.Deployed
                        && selected.Remove(unit)
                    )
                        units.Add(unit);
            }
            return units;
        }

        /// <summary>
        /// Limits formation translation without compressing the formation at map edges.
        /// </summary>
        /// <param name="value">The requested translation.</param>
        /// <param name="minimum">The smallest translation keeping unit centers inside the map.</param>
        /// <param name="maximum">The largest translation keeping unit centers inside the map.</param>
        /// <returns>The bounded translation.</returns>
        private static float ClampOffset(float value, float minimum, float maximum) =>
            Math.Clamp(value, minimum, maximum);

        /// <summary>
        /// Checks a destination for non-finite coordinates.
        /// </summary>
        /// <param name="value">The requested destination.</param>
        /// <returns>Whether every coordinate is finite.</returns>
        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.X)
            && !float.IsInfinity(value.X)
            && !float.IsNaN(value.Y)
            && !float.IsInfinity(value.Y)
            && !float.IsNaN(value.Z)
            && !float.IsInfinity(value.Z);

        /// <summary>
        /// Converts persistent coordinates into simulation coordinates.
        /// </summary>
        /// <param name="value">The persistent coordinates.</param>
        /// <returns>The simulation vector.</returns>
        private static Vector3 ToVector(BattleVector3 value) =>
            new Vector3(value.X, value.Y, value.Z);

        /// <summary>
        /// Updates existing saved coordinates without allocating during movement updates.
        /// </summary>
        /// <param name="destination">The saved vector to update.</param>
        /// <param name="value">The new coordinates.</param>
        private static void SetVector(BattleVector3 destination, Vector3 value)
        {
            destination.X = value.X;
            destination.Y = value.Y;
            destination.Z = value.Z;
        }

        /// <summary>
        /// Converts simulation coordinates into independently owned persistent coordinates.
        /// </summary>
        /// <param name="value">The simulation coordinates.</param>
        /// <returns>The persistent vector.</returns>
        private static BattleVector3 FromVector(Vector3 value) =>
            new BattleVector3
            {
                X = value.X,
                Y = value.Y,
                Z = value.Z,
            };
    }
}
