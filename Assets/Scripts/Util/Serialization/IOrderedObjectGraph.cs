using System.Collections.Generic;

namespace Rebellion.Util.Serialization
{
    /// <summary>
    /// Supplies the logical order of objects already contained in a persisted graph.
    /// Serializers record positions on the existing object records instead of duplicating identities.
    /// </summary>
    public interface IOrderedObjectGraph
    {
        /// <summary>
        /// Captures the live objects and their positions at a stable persistence boundary.
        /// </summary>
        /// <returns>The ordered object references and their logical positions.</returns>
        IReadOnlyList<OrderedObject> CaptureObjectOrder();

        /// <summary>
        /// Captures vacant positions in their current reuse-stack order.
        /// </summary>
        /// <returns>The vacant positions, with the next reused position last.</returns>
        IReadOnlyList<int> CaptureVacantPositions();

        /// <summary>
        /// Restores order using the same instances decoded from the existing object records.
        /// </summary>
        /// <param name="objects">The decoded objects and their logical positions.</param>
        /// <param name="vacancies">The captured vacant-position reuse stack.</param>
        void RestoreObjectOrder(IReadOnlyList<OrderedObject> objects, IReadOnlyList<int> vacancies);
    }
}
