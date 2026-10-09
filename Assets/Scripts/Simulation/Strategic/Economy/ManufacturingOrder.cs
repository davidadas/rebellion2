using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Describes the product and unfinished quantity assigned to one manufacturing lane.
    /// </summary>
    public sealed class ManufacturingOrder
    {
        public ManufacturingType ManufacturingType { get; }

        public string ProductTypeID { get; }

        public int Quantity { get; }

        /// <summary>
        /// Creates an immutable manufacturing-order snapshot.
        /// </summary>
        /// <param name="manufacturingType">The lane receiving the order.</param>
        /// <param name="productTypeID">The product assigned to the lane.</param>
        /// <param name="quantity">The unfinished quantity assigned to the lane.</param>
        internal ManufacturingOrder(
            ManufacturingType manufacturingType,
            string productTypeID,
            int quantity
        )
        {
            ManufacturingType = manufacturingType;
            ProductTypeID = productTypeID;
            Quantity = quantity;
        }
    }
}
