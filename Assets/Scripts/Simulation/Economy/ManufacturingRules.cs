using System;
using System.Collections.Generic;
using System.Linq;
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
                string itemTypeID = GetProductTypeID(item);
                if (string.IsNullOrEmpty(itemTypeID))
                    return null;

                productTypeID ??= itemTypeID;
                if (!string.Equals(productTypeID, itemTypeID, StringComparison.Ordinal))
                    return null;
            }

            return productTypeID;
        }

        /// <summary>
        /// Returns whether queued items represent the same ordinary product or rebuild type.
        /// </summary>
        /// <param name="items">The active manufacturing items.</param>
        /// <param name="template">The requested product or destroyed building.</param>
        /// <returns>True when the request matches the active project.</returns>
        internal static bool MatchesProduct(
            IReadOnlyCollection<IManufacturable> items,
            IManufacturable template
        )
        {
            if (items == null || items.Count == 0 || template == null)
                return false;

            if (template is Building { IsDestroyed: true } rebuild)
            {
                return items.All(item =>
                    item is Building { IsDestroyed: true } queued
                    && string.Equals(queued.TypeID, rebuild.TypeID, StringComparison.Ordinal)
                    && ReferenceEquals(queued.GetParent(), rebuild.GetParent())
                    && string.Equals(
                        queued.OwnerInstanceID,
                        rebuild.OwnerInstanceID,
                        StringComparison.Ordinal
                    )
                );
            }

            if (template is Building requestedUpgrade && requestedUpgrade.IsUpgrading())
            {
                return IsBuildingUpgradeProject(items, requestedUpgrade.UpgradeTargetTypeID);
            }

            if (items.Any(item => item is Building upgrading && upgrading.IsUpgrading()))
                return false;

            return items.All(item =>
                item is not Building { IsDestroyed: true }
                && string.Equals(
                    GetProductTypeID(item),
                    GetProductTypeID(template),
                    StringComparison.Ordinal
                )
            );
        }

        /// <summary>
        /// Returns whether a lane contains only in-place upgrades to one target type.
        /// </summary>
        /// <param name="items">The active manufacturing items.</param>
        /// <param name="targetTypeID">The authored upgrade target type.</param>
        /// <returns>True when every queued item is an upgrade to the requested target.</returns>
        internal static bool IsBuildingUpgradeProject(
            IReadOnlyCollection<IManufacturable> items,
            string targetTypeID
        )
        {
            return items?.Count > 0
                && !string.IsNullOrEmpty(targetTypeID)
                && items.All(item =>
                    item is Building queuedUpgrade
                    && queuedUpgrade.IsUpgrading()
                    && string.Equals(
                        queuedUpgrade.UpgradeTargetTypeID,
                        targetTypeID,
                        StringComparison.Ordinal
                    )
                );
        }

        /// <summary>
        /// Gets the product represented by one ordinary order or in-place upgrade.
        /// </summary>
        /// <param name="item">The manufacturing item.</param>
        /// <returns>The product type identifier.</returns>
        private static string GetProductTypeID(IManufacturable item)
        {
            return item is Building upgrading && upgrading.IsUpgrading()
                ? upgrading.UpgradeTargetTypeID
                : item?.GetTypeID();
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
