using System;
using UnityEngine;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tactical.Presentation
{
    public sealed partial class TacticalCombatRenderer
        : MonoBehaviour,
            ITacticalCombatArrivalPresentation
    {
        /// <summary>
        /// Applies one semantic camera operation independently of simulation pause.
        /// </summary>
        /// <param name="command">The requested primary-camera operation.</param>
        public void ApplyCameraCommand(TacticalCombatCameraCommand command)
        {
            _cameraState.Apply(command);
            UpdateFieldOfView();
            UpdateCamera();
        }

        /// <summary>
        /// Opens an occupied object or fighter category without changing its command selection.
        /// </summary>
        /// <param name="category">The object category from zero through seven, or fighter category from eight through eleven.</param>
        /// <returns>Whether the category has a registered owner.</returns>
        public bool ShowCategory(int category)
        {
            if ((uint)category >= 12)
                throw new ArgumentOutOfRangeException(nameof(category));
            if (GetCameraGroup(category) == null)
                return false;
            _cameraState.OpenCategory(category, () => ResolveCameraFocus(category));
            UpdateCamera();
            return true;
        }

        /// <summary>
        /// Resolves the current owner of one camera category independently of its prior slot contents.
        /// </summary>
        /// <param name="category">The camera category from zero through eleven.</param>
        /// <returns>The current category owner, or null for an empty slot.</returns>
        private TacticalCombatGroup GetCameraGroup(int category) =>
            category < 8
                ? _session.GetObjectGroup(_session.PlayerSide, category)
                : _session.GetFighterGroup(_session.PlayerSide, category - 8);

        /// <summary>
        /// Resolves the live category participant independently of the last camera refresh.
        /// </summary>
        /// <param name="category">The current object or fighter category.</param>
        /// <param name="group">The category's current owner, or null for an empty slot.</param>
        /// <returns>The inspected object or applied group leader.</returns>
        private TacticalCombatUnit GetCameraPrimary(int category, TacticalCombatGroup group) =>
            group == null ? null
            : category < 8 && _session.Selection.Kind == TacticalCombatSelectionKind.ObjectGroup
                ? _session.Selection.PrimaryUnit ?? group.PrimaryMember
            : group.PrimaryMember;

        /// <summary>
        /// Identifies only a category close-up's primary participant for effect attachment decisions.
        /// </summary>
        /// <returns>The current registered primary identifier, or null for the ordinary camera.</returns>
        private int? GetCloseUpPrimary()
        {
            if (!_cameraState.IsCategoryView || !_cameraState.Category.HasValue)
                return null;
            int category = _cameraState.Category.Value;
            TacticalCombatUnit primary = GetCameraPrimary(category, GetCameraGroup(category));
            return primary != null && _visualsByIndex.ContainsKey(primary.Index)
                ? primary.Index
                : null;
        }

        /// <summary>
        /// Resolves applied members, route points, and targets against the screen's cached presentations.
        /// </summary>
        /// <param name="category">The live category slot.</param>
        /// <returns>The current close-up references, or null when no primary render object resolves.</returns>
        private TacticalCombatCameraFocus? ResolveCameraFocus(int category)
        {
            TacticalCombatGroup group = GetCameraGroup(category);
            if (group == null)
                return null;
            TacticalCombatUnit primary = GetCameraPrimary(category, group);
            if (primary == null || !_visualsByIndex.TryGetValue(primary.Index, out Visual visual))
                return null;
            Vector3? secondaryPosition = null;
            TacticalCombatUnit secondary = null;
            if (group.PrimaryWaypoint is Vector3N waypoint)
            {
                if (_navigation.TryGetWorldPosition(waypoint, out Vector3 markerPosition))
                    secondaryPosition = markerPosition;
            }
            else if (group.Targets.Count > 0)
                secondary = group.Targets[0];
            if (
                category >= 8
                && !group.PrimaryWaypoint.HasValue
                && secondary == null
                && group.Order == TacticalCombatOrder.Recover
                && group.Members.Count > 0
            )
                secondary = _session.GetUnit(group.Members[0].CarrierID);
            if (
                secondary != null
                && !ReferenceEquals(primary, secondary)
                && _visualsByIndex.TryGetValue(secondary.Index, out Visual secondaryVisual)
            )
            {
                ApplyTransform(secondaryVisual);
                secondaryPosition = GetCameraPosition(secondaryVisual);
            }
            ApplyTransform(visual);
            visual.Root.SetActive(true);
            visual.SelectionBox?.SetState(1, false);
            Vector3 span = visual.SelectionBox?.Bounds.size ?? Vector3.zero;
            return new TacticalCombatCameraFocus(
                primary.Index,
                GetCameraPosition(visual),
                primary.IsBattleStation ? _modelRoot.rotation : visual.Root.transform.rotation,
                span.x > span.y ? span.x : span.y,
                secondaryPosition
            );
        }

        /// <summary>
        /// Tracks the selected participant's live render position without translating the camera orbit.
        /// </summary>
        /// <param name="unit">The selected participant, or null to clear tracking.</param>
        public void Focus(TacticalCombatUnit unit)
        {
            if (unit == null || !_visualsByIndex.TryGetValue(unit.Index, out Visual visual))
                _cameraState.Track(null);
            else
            {
                _cameraState.Track(
                    () =>
                    {
                        ApplyTransform(visual);
                        return GetCameraPosition(visual);
                    },
                    unit.Index
                );
            }
            UpdateCamera();
        }

        /// <summary>
        /// Resolves the logical object frame independently of any offset background billboard.
        /// </summary>
        /// <param name="visual">The registered object's presentation.</param>
        /// <returns>The logical render frame's world position.</returns>
        private Vector3 GetCameraPosition(Visual visual) =>
            visual.Unit.IsBattleStation ? _modelRoot.position : visual.Root.transform.position;

        /// <summary>
        /// Aims the primary camera at an addressed navigation marker without moving its orbit.
        /// </summary>
        /// <param name="point">The marker's fixed encounter-local position.</param>
        public void Focus(Vector3N point)
        {
            _cameraState.Track(() => _modelRoot.TransformPoint(ToUnity(point)));
            UpdateCamera();
        }

        /// <summary>
        /// Projects an encounter-local point into normalized viewport coordinates.
        /// </summary>
        /// <param name="position">The encounter-local position.</param>
        /// <returns>The normalized projection and signed camera depth.</returns>
        public Vector3 Project(Vector3N position) =>
            _battleCamera.WorldToViewportPoint(_modelRoot.TransformPoint(ToUnity(position)));

        /// <summary>
        /// Updates the isolated camera's orbit transform.
        /// </summary>
        private void UpdateCamera()
        {
            _cameraState.GetPose(
                _modelRoot.position,
                out Vector3 position,
                out Quaternion rotation
            );
            _battleCamera.transform.SetPositionAndRotation(position, rotation);
            _battleCamera.nearClipPlane = _cameraState.NearPlane;
            UpdateFieldOfView();
        }
    }
}
