using System.Collections.Generic;
using UnityEngine;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tactical.Presentation
{
    public sealed partial class TacticalCombatRenderer
        : MonoBehaviour,
            ITacticalCombatArrivalPresentation
    {
        /// <summary>
        /// Toggles one of the encounter's four navigation rings.
        /// </summary>
        /// <param name="ring">The zero-based navigation ring.</param>
        public void ToggleNavigationRing(int ring) => _navigation.ToggleRing(ring);

        /// <summary>
        /// Projects requested routes for the current player selection.
        /// </summary>
        /// <param name="groups">The selected groups in encounter order.</param>
        public void UpdateNavigationSelection(IReadOnlyList<TacticalCombatGroup> groups) =>
            _navigation.UpdateSelection(groups);

        /// <summary>
        /// Resolves the nearest visible unit or navigation marker under the pointer.
        /// </summary>
        /// <param name="point">The normalized viewport point.</param>
        /// <param name="unit">Receives the selected participant, or null for a navigation marker.</param>
        /// <param name="waypoint">Receives the selected navigation position.</param>
        /// <returns>Whether a visible selectable object was intersected.</returns>
        public bool TryPick(Vector2 point, out TacticalCombatUnit unit, out Vector3N waypoint)
        {
            unit = null;
            waypoint = default;
            if (point.x <= 0 || point.y <= 0 || point.x >= 1 || point.y >= 1)
                return false;
            Ray ray = _battleCamera.ViewportPointToRay(point);
            float distance = _battleCamera.farClipPlane;
            unit = _picker.Pick(ray, ref distance);
            if (_navigation.TryPick(ray, distance, out waypoint))
            {
                unit = null;
                return true;
            }
            return unit != null;
        }

        /// <summary>
        /// Resolves a pointer press using the ordered pixel neighborhood associated with its button.
        /// </summary>
        /// <param name="point">The normalized viewport press.</param>
        /// <param name="secondary">Whether the secondary pointer button was pressed.</param>
        /// <param name="unit">Receives an object hit, or null for a navigation marker.</param>
        /// <param name="waypoint">Receives the addressed navigation marker.</param>
        /// <returns>Whether one of the ordered neighborhood rays hit an addressed object.</returns>
        public bool TryPickNeighborhood(
            Vector2 point,
            bool secondary,
            out TacticalCombatUnit unit,
            out Vector3N waypoint
        ) =>
            TacticalCombatPointerPicker.TryPick(
                point,
                new Vector2Int(_battleCamera.pixelWidth, _battleCamera.pixelHeight),
                secondary,
                TryPick,
                out unit,
                out waypoint
            );
    }
}
