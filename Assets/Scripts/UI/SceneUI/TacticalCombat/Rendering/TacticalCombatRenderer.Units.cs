using System;
using Rebellion.Game.Combat;
using Rebellion.UI;
using UnityEngine;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tactical.Rendering
{
    public sealed partial class TacticalCombatRenderer
        : MonoBehaviour,
            ITacticalCombatArrivalPresentation
    {
        /// <summary>
        /// Resizes the camera target only when the viewport's pixel size changes.
        /// </summary>
        /// <param name="width">The viewport width in pixels.</param>
        /// <param name="height">The viewport height in pixels.</param>
        public void Resize(int width, int height)
        {
            width = Mathf.Max(width, 1);
            height = Mathf.Max(height, 1);
            if (
                _renderTarget != null
                && _renderTarget.width == width
                && _renderTarget.height == height
            )
                return;
            ReleaseTarget();
            _renderTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Tactical combat",
                antiAliasing = 1,
            };
            _renderTarget.Create();
            _battleCamera.targetTexture = _renderTarget;
            _battleCamera.aspect = (float)width / height;
            UpdateFieldOfView();
        }

        /// <summary>
        /// Applies current battle positions and presentation visibility.
        /// </summary>
        public void RenderUnits()
        {
            UpdateCamera();
            RefreshSelectionBoxes();
            if (_planet != null)
                _planet.rotation = _battleCamera.transform.rotation;
            foreach (Visual visual in _visuals)
                RefreshVisual(visual);
        }

        /// <summary>
        /// Retains arrival transforms until the final snap and announces presentation ownership.
        /// </summary>
        /// <param name="active">Whether arrival presentation owns the affected transforms.</param>
        public void SetArrivalActive(bool active)
        {
            IsPresentingArrival = active;
            if (!active)
                foreach (Visual visual in _visuals)
                {
                    visual.ArrivalPosition = null;
                    visual.ArrivalDirection = null;
                    visual.ArrivalVisible = false;
                }
            ArrivalPresentationChanged?.Invoke(active);
        }

        /// <summary>
        /// Restores an addressed arrival member's base presentation before its first transform.
        /// </summary>
        /// <param name="unitIndex">The battle registration identifier.</param>
        public void ResetArrival(int unitIndex)
        {
            if (!_visualsByIndex.TryGetValue(unitIndex, out Visual visual))
                return;
            visual.ArrivalVisible = true;
            visual.Root.SetActive(true);
            visual.SelectionBox?.SetState(1, false);
            _impacts.ClearLocks(visual.Unit);
        }

        /// <summary>
        /// Applies an encounter-local arrival transform without changing simulation state.
        /// </summary>
        /// <param name="unitIndex">The battle registration identifier.</param>
        /// <param name="position">The presentation position.</param>
        /// <param name="direction">The participant's presentation direction.</param>
        public void SetTransform(
            int unitIndex,
            System.Numerics.Vector4 position,
            System.Numerics.Vector4 direction
        )
        {
            if (!_visualsByIndex.TryGetValue(unitIndex, out Visual visual))
                return;
            visual.ArrivalPosition = new Vector3(position.X, position.Y, position.Z);
            visual.ArrivalDirection = new Vector3(direction.X, direction.Y, direction.Z);
            ApplyTransform(visual);
        }

        /// <summary>
        /// Refreshes an addressed member's current visibility, selection state, and detail level.
        /// </summary>
        /// <param name="unitIndex">The battle registration identifier.</param>
        public void Refresh(int unitIndex)
        {
            if (_visualsByIndex.TryGetValue(unitIndex, out Visual visual))
                RefreshVisual(visual);
        }

        /// <summary>
        /// Projects one participant's owned presentation state without enumerating its hierarchy.
        /// </summary>
        /// <param name="visual">The participant's presentation.</param>
        private void RefreshVisual(Visual visual)
        {
            CombatUnit unit = visual.Unit;
            CombatUnitStatus status = unit.Status;
            bool isAlive = unit.IsAlive;
            if (
                _replicaPlayback != null
                && _replicaPlayback.TryGetLifecycle(
                    unit.InstanceID,
                    out CombatUnitStatus presentationStatus,
                    out bool presentationAlive
                )
            )
            {
                status = presentationStatus;
                isAlive = presentationAlive;
            }
            bool visible =
                visual.ArrivalVisible
                || _cameraState.IsCategoryView && _cameraState.TrackedObjectIndex == unit.Index
                || isAlive
                    && status
                        is CombatUnitStatus.Active
                            or CombatUnitStatus.PreparingHyperspace
                            or CombatUnitStatus.EnteringHyperspace
                            or CombatUnitStatus.UnderConstruction
                || _impacts.KeepsModelVisible(unit);
            visual.Root.SetActive(visible);
            if (visual.SelectionBox != null)
            {
                bool engaged =
                    status
                    is CombatUnitStatus.Active
                        or CombatUnitStatus.PreparingHyperspace
                        or CombatUnitStatus.EnteringHyperspace
                        or CombatUnitStatus.AwaitingWithdrawal
                        or CombatUnitStatus.AttackingBattleStation;
                int state =
                    !engaged ? 1
                    : _yellowBoxes.Contains(unit) ? 2
                    : _whiteBoxes.Contains(unit) ? 4
                    : _factionBoxes.Contains(unit)
                    || _factionHighlight[_session.GetSide(unit.Side).FactionIndex]
                        ? 3
                    : 1;
                visual.SelectionBox.SetState(state, _cameraState.TrackedObjectIndex == unit.Index);
            }
            if (!visible)
            {
                _impacts.ClearLocks(unit);
                return;
            }
            ApplyTransform(visual);
            _impacts.SetLockPosition(unit, visual.Root.transform.localPosition);
            if (visual.Billboard != null)
            {
                visual.Billboard.rotation = _battleCamera.transform.rotation;
                if (unit.IsFighter)
                    UpdateFighter(visual);
            }
            else if (visual.ModelDetails != null)
                UpdateModelDetail(visual);
        }

        /// <summary>
        /// Chooses the arrival transform while it is owned, or the participant's ordinary pose.
        /// </summary>
        /// <param name="visual">The participant's presentation.</param>
        private void ApplyTransform(Visual visual)
        {
            Vector3N position = visual.Unit.Position;
            Vector3N direction = visual.Unit.Heading;
            float hyperspaceMilliseconds = visual.Unit.HyperspaceMilliseconds;
            CombatUnitStatus status = visual.Unit.Status;
            if (
                _replicaPlayback != null
                && _replicaPlayback.TryGetPose(
                    visual.Unit.InstanceID,
                    out Vector3N presentationPosition,
                    out Vector3N presentationHeading,
                    out float presentationHyperspaceMilliseconds,
                    out CombatUnitStatus presentationStatus,
                    out _
                )
            )
            {
                position = presentationPosition;
                direction = presentationHeading;
                hyperspaceMilliseconds = presentationHyperspaceMilliseconds;
                status = presentationStatus;
            }
            if (status == CombatUnitStatus.EnteringHyperspace)
            {
                TacticalCombatDeparturePose departure = TacticalCombatDeparturePose.Evaluate(
                    visual.Unit.Index,
                    GetUnitDefinition(visual.Unit)?.PresentationIndex ?? 0,
                    position,
                    _session.GetSide(visual.Unit.Side).Forward,
                    hyperspaceMilliseconds
                );
                position = departure.Position;
                direction = departure.Heading;
            }
            visual.Root.transform.localPosition =
                visual.ArrivalPosition ?? visual.StationPosition ?? ToUnity(position);
            Vector3 heading = visual.ArrivalDirection ?? ToUnity(direction);
            if (heading.sqrMagnitude > 0)
                visual.Root.transform.localRotation = Quaternion.LookRotation(-heading, Vector3.up);
        }

        /// <summary>
        /// Resolves logical actor positions while retaining temporary arrival and departure movement.
        /// </summary>
        /// <param name="unit">The registered encounter participant.</param>
        /// <returns>Its current position in the encounter's rendering frame.</returns>
        private Vector3 GetPresentationPosition(CombatUnit unit)
        {
            Visual visual = _visualsByIndex[unit.Index];
            ApplyTransform(visual);
            return unit.IsBattleStation ? Vector3.zero : visual.Root.transform.localPosition;
        }

        /// <summary>
        /// Changes one faction's capital bounding-box visibility independently of the command selection.
        /// </summary>
        /// <param name="factionIndex">The authored faction index.</param>
        /// <param name="enabled">Whether that faction's capital boxes are requested.</param>
        public void SetFactionHighlight(int factionIndex, bool enabled)
        {
            if ((uint)factionIndex >= 2)
                throw new ArgumentOutOfRangeException(nameof(factionIndex));
            _factionHighlight[factionIndex] = enabled;
        }

        /// <summary>
        /// Captures the current context's selection and ordered target colors once for the render frame.
        /// </summary>
        private void RefreshSelectionBoxes()
        {
            _yellowBoxes.Clear();
            _factionBoxes.Clear();
            _whiteBoxes.Clear();
            TacticalCombatSelection selection = _session.Selection;
            foreach (CombatUnit unit in selection.Highlights)
            {
                bool yellow =
                    selection.Kind == TacticalCombatSelectionKind.MultipleObjects
                    || selection.Kind
                        is TacticalCombatSelectionKind.SingleObject
                            or TacticalCombatSelectionKind.ObjectGroup
                        && ReferenceEquals(selection.PrimaryUnit, unit);
                (yellow ? _yellowBoxes : _factionBoxes).Add(unit);
            }
            bool first = true;
            foreach (CombatUnit target in selection.Targets)
            {
                if (!selection.IsDisplayObject(target))
                    continue;
                if (first)
                {
                    first = false;
                    if (!target.IsFighter)
                        _whiteBoxes.Add(target);
                }
                else
                    _factionBoxes.Add(target);
            }
        }

        /// <summary>
        /// Selects capital geometry from the camera-scaled depth thresholds while retaining its current level on exact boundaries.
        /// </summary>
        /// <param name="visual">The active capital visual.</param>
        private void UpdateModelDetail(Visual visual)
        {
            float distance = GetDetailDistance(visual);
            int selected = TacticalCombatDetailLevels.SelectCapital(
                distance,
                _field,
                _highDetail,
                visual.DetailLevel
            );
            int previous = visual.DetailLevel;
            visual.DetailLevel = Mathf.Min(selected, visual.ModelDetails.Length - 1);
            if (previous != visual.DetailLevel)
                _impacts.RestartLocks(visual.Unit);
            for (int index = 0; index < visual.ModelDetails.Length; index++)
                visual.ModelDetails[index].gameObject.SetActive(index == visual.DetailLevel);
        }

        /// <summary>
        /// Selects the fighter's depth-dependent sprite and faces its billboard toward the camera.
        /// </summary>
        /// <param name="visual">The active fighter visual.</param>
        private void UpdateFighter(Visual visual)
        {
            visual.Billboard.rotation = _battleCamera.transform.rotation;
            int lod = _detailProjection.SelectFighter(
                GetDetailDistance(visual),
                _highDetail,
                visual.DetailLevel
            );
            visual.DetailLevel = lod;
            Texture2D texture = visual.FighterTextures[lod];
            if (!_fighterMaterials.TryGetValue(texture, out Material material))
            {
                material = new Material(_fighterShader) { mainTexture = texture };
                _fighterMaterials.Add(texture, material);
            }
            visual.FighterRenderer.sharedMaterial = material;
        }

        /// <summary>
        /// Projects the camera-relative position onto the camera's direction in the battle frame.
        /// </summary>
        /// <param name="visual">The participant's owned presentation.</param>
        /// <returns>The rounded nonnegative detail distance, retaining unordered values.</returns>
        private float GetDetailDistance(Visual visual)
        {
            Vector3 position = _battleCamera.transform.InverseTransformPoint(
                visual.Root.transform.position
            );
            Vector3 direction = _modelRoot.InverseTransformDirection(
                _battleCamera.transform.forward
            );
            double dot = (double)position.z * direction.z + (double)position.y * direction.y;
            dot += (double)position.x * direction.x;
            float distance = (float)dot;
            return !(dot >= 0) ? -distance : distance;
        }
    }
}
