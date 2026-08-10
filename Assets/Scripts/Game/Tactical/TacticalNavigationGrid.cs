using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Owns the four concentric waypoint-marker sets available in tactical space.
    /// </summary>
    public sealed class TacticalNavigationGrid
    {
        private const int _setCount = 4;
        private const float _setSpacing = 0.25f;
        private readonly ReadOnlyCollection<TacticalNavPoint>[] pointSets;
        private readonly bool[] visibility = new bool[_setCount];

        /// <summary>
        /// Gets the number of fixed waypoint-marker sets.
        /// </summary>
        public int SetCount => pointSets.Length;

        /// <summary>
        /// Initializes the tactical waypoint lattice for a battlefield scale.
        /// </summary>
        /// <param name="battlefieldScale">The full tactical battlefield scale.</param>
        public TacticalNavigationGrid(float battlefieldScale)
        {
            if (battlefieldScale <= 0f)
                throw new ArgumentOutOfRangeException(nameof(battlefieldScale));

            pointSets = new ReadOnlyCollection<TacticalNavPoint>[_setCount];
            for (int setIndex = 0; setIndex < pointSets.Length; setIndex++)
                pointSets[setIndex] = BuildSet(battlefieldScale, setIndex);
        }

        /// <summary>
        /// Gets one waypoint-marker set from the innermost to the outermost shell.
        /// </summary>
        /// <param name="setIndex">The zero-based internal shell index.</param>
        /// <returns>The immutable waypoint-marker set.</returns>
        public IReadOnlyList<TacticalNavPoint> GetPoints(int setIndex)
        {
            ValidateSetIndex(setIndex);
            return pointSets[setIndex];
        }

        /// <summary>
        /// Gets whether one waypoint-marker set is visible.
        /// </summary>
        /// <param name="setIndex">The zero-based internal shell index.</param>
        /// <returns>True when the set is visible.</returns>
        public bool IsVisible(int setIndex)
        {
            ValidateSetIndex(setIndex);
            return visibility[setIndex];
        }

        /// <summary>
        /// Reverses the visibility of one waypoint-marker set.
        /// </summary>
        /// <param name="setIndex">The zero-based internal shell index.</param>
        /// <returns>The new visibility state.</returns>
        public bool ToggleVisibility(int setIndex)
        {
            ValidateSetIndex(setIndex);
            visibility[setIndex] = !visibility[setIndex];
            return visibility[setIndex];
        }

        /// <summary>
        /// Maps the left-to-right HUD button order to the outer-to-inner marker shells.
        /// </summary>
        /// <param name="buttonIndex">The zero-based HUD button index.</param>
        /// <returns>The corresponding internal shell index.</returns>
        public int GetSetIndexForButton(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= pointSets.Length)
                throw new ArgumentOutOfRangeException(nameof(buttonIndex));

            return pointSets.Length - buttonIndex - 1;
        }

        /// <summary>
        /// Builds one 3 by 3 by 3 shell at its fixed battlefield interval.
        /// </summary>
        /// <param name="battlefieldScale">The full tactical battlefield scale.</param>
        /// <param name="setIndex">The zero-based internal shell index.</param>
        /// <returns>The immutable waypoint-marker set.</returns>
        private static ReadOnlyCollection<TacticalNavPoint> BuildSet(
            float battlefieldScale,
            int setIndex
        )
        {
            float interval = battlefieldScale * _setSpacing * (setIndex + 1);
            List<TacticalNavPoint> points = new List<TacticalNavPoint>(27);
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (setIndex > 0 && x == 0 && y == 0 && z == 0)
                            continue;

                        points.Add(new TacticalNavPoint(x * interval, y * interval, z * interval));
                    }
                }
            }

            return points.AsReadOnly();
        }

        /// <summary>
        /// Rejects indices outside the four fixed marker sets.
        /// </summary>
        /// <param name="setIndex">The zero-based internal shell index.</param>
        private void ValidateSetIndex(int setIndex)
        {
            if (setIndex < 0 || setIndex >= pointSets.Length)
                throw new ArgumentOutOfRangeException(nameof(setIndex));
        }
    }
}
