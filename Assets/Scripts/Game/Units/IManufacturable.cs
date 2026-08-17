using System;
using System.Collections.Generic;

namespace Rebellion.Game.Units
{
    public enum ManufacturingStatus
    {
        Building,
        Complete,
    }

    public enum ManufacturingType
    {
        None,
        Ship,
        Building,
        Troop,
    }

    /// <summary>
    /// Interface for manufacturable objects with default properties and methods.
    /// <remarks>
    /// Manufacturable objects can be constructed, maintained, and researched and include
    /// objects like ships, buildings, and troops.
    /// </remarks>
    /// </summary>
    public interface IManufacturable : IMovable
    {
        // Construction Info.
        public int ConstructionCost { get; set; }
        public int MaintenanceCost { get; set; }
        public int BaseBuildSpeed { get; set; }
        public List<string> ManufacturingFactionInstanceIDs { get; set; }

        // Research Info.
        public int ResearchOrder { get; set; }
        public int ResearchDifficulty { get; set; }

        // Manufacturing Info.
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public int ManufacturingProgress { get; set; }
        public ManufacturingStatus ManufacturingStatus { get; set; }

        /// <summary>
        /// Returns the construction cost of the manufacturable.
        /// </summary>
        /// <returns>The construction cost of the manufacturable.</returns>
        public int GetConstructionCost()
        {
            return ConstructionCost;
        }

        /// <summary>
        /// Returns the maintenance cost of the manufacturable.
        /// </summary>
        /// <returns>The maintenance cost of the manufacturable.</returns>
        public int GetMaintenanceCost()
        {
            return MaintenanceCost;
        }

        /// <summary>
        /// Returns the position in the research unlock sequence for this unit's manufacturing type.
        /// 0 = available at game start without research.
        /// </summary>
        /// <returns>Research unlock order index (0 = no research needed).</returns>
        public int GetResearchOrder()
        {
            return ResearchOrder;
        }

        /// <summary>
        /// Returns the accumulated research capacity cost required to unlock this unit.
        /// </summary>
        /// <returns>Total research capacity cost to unlock.</returns>
        public int GetResearchDifficulty()
        {
            return ResearchDifficulty;
        }

        /// <summary>
        /// Returns the manufacturing progress of the manufacturable.
        /// </summary>
        /// <returns>The manufacturing progress of the manufacturable.</returns>
        public int GetManufacturingProgress()
        {
            return ManufacturingProgress;
        }

        /// <summary>
        /// Increments the manufacturing progress of the manufacturable.
        /// </summary>
        /// <param name="progress">The amount to increment the progress by.</param>
        /// <returns>The new manufacturing progress of the manufacturable.</returns>
        public int IncrementManufacturingProgress(int progress)
        {
            ManufacturingProgress += progress;
            return ManufacturingProgress;
        }

        /// <summary>
        /// Returns the base build speed of the manufacturable.
        /// </summary>
        /// <returns></returns>
        public bool IsManufacturingComplete()
        {
            return GetConstructionCost() <= GetManufacturingProgress();
        }

        /// <summary>
        /// Returns the manufacturing type of the manufacturable.
        /// </summary>
        /// <returns>The manufacturing type of the manufacturable.</returns>
        public ManufacturingType GetManufacturingType();

        /// <summary>
        /// Returns the manufacturing status of the manufacturable.
        /// </summary>
        /// <returns>The manufacturing status of the manufacturable.</returns>
        public ManufacturingStatus GetManufacturingStatus()
        {
            return ManufacturingStatus;
        }

        /// <summary>
        /// Sets the manufacturing status of the manufacturable.
        /// </summary>
        /// <param name="status">The new manufacturing status.</param>
        /// <exception cref="InvalidOperationException">Thrown when the status is invalid.</exception>
        public void SetManufacturingStatus(ManufacturingStatus status)
        {
            // Check for invalid status.
            if (
                GetManufacturingStatus() == ManufacturingStatus.Complete
                && status == ManufacturingStatus.Building
            )
            {
                throw new InvalidOperationException(
                    "Invalid manufacturing status. Cannot set to 'Building' once 'Complete'."
                );
            }

            ManufacturingStatus = status;
        }
    }

    /// <summary>Provides acquisition rules shared by all manufacturable definitions.</summary>
    public static class ManufacturableExtensions
    {
        /// <summary>Returns whether the specified faction may manufacture this definition.</summary>
        public static bool CanBeManufacturedBy(
            this IManufacturable manufacturable,
            string factionInstanceID
        ) => manufacturable.ManufacturingFactionInstanceIDs?.Contains(factionInstanceID) != false;
    }
}
