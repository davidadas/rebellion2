using System;
using System.Collections.Generic;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Owns manufacturing rules shared by commands and queries.
    /// </summary>
    internal static class ManufacturingRules
    {
        /// <summary>
        /// Gets the common product identifier from a homogeneous manufacturing lane.
        /// </summary>
        /// <param name="items">The unfinished items assigned to the lane.</param>
        /// <returns>The common product identifier, or null when the lane is empty or mixed.</returns>
        internal static string GetProductTypeID(IReadOnlyCollection<IManufacturable> items)
        {
            if (items == null || items.Count == 0)
                return null;

            string productTypeID = null;
            foreach (IManufacturable item in items)
            {
                string itemTypeID = item?.GetTypeID();
                if (string.IsNullOrEmpty(itemTypeID))
                    return null;

                productTypeID ??= itemTypeID;
                if (!string.Equals(productTypeID, itemTypeID, StringComparison.Ordinal))
                    return null;
            }

            return productTypeID;
        }

        /// <summary>
        /// Returns whether an active order contains only the requested product.
        /// </summary>
        /// <param name="order">The active manufacturing order.</param>
        /// <param name="template">The requested product template.</param>
        /// <returns>True when both values identify the same non-empty product.</returns>
        internal static bool MatchesProduct(ManufacturingOrder order, IManufacturable template)
        {
            string requestedTypeID = template?.GetTypeID();
            return order != null
                && !string.IsNullOrEmpty(order.ProductTypeID)
                && !string.IsNullOrEmpty(requestedTypeID)
                && string.Equals(order.ProductTypeID, requestedTypeID, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns whether a capital ship can receive manufacturing output.
        /// </summary>
        /// <param name="ship">The destination ship to inspect.</param>
        /// <returns>True when the ship is complete and not in transit.</returns>
        internal static bool IsCarrierAvailable(CapitalShip ship)
        {
            return ship?.ManufacturingStatus == ManufacturingStatus.Complete
                && ((IMovable)ship).GetTransitMovement() == null;
        }

        /// <summary>
        /// Returns whether an item expands the resource facilities that supply maintenance.
        /// </summary>
        /// <param name="item">The prospective manufacturing item.</param>
        /// <returns>True for mines and refineries.</returns>
        internal static bool IsResourceFacility(IManufacturable item)
        {
            return item is Building { BuildingType: BuildingType.Mine or BuildingType.Refinery };
        }
    }
}
