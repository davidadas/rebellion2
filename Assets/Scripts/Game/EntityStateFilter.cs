namespace Rebellion.Game
{
    /// <summary>
    /// Controls which entities are included when querying counts or derived values
    /// (e.g. building counts, defense strength, garrison strength).
    /// </summary>
    public enum EntityStateFilter
    {
        /// <summary>
        /// Includes all entities regardless of state (under construction, in transit, etc.).
        /// Use this for planning and UI display where total potential matters.
        /// </summary>
        All,

        /// <summary>
        /// Includes only entities that are currently operational and contributing at their location.
        ///
        /// General rules:
        /// - Manufacturing must be complete (if applicable).
        /// - Must not be in transit.
        /// - Must not be destroyed or otherwise inactive.
        ///
        /// Exact interpretation may vary by entity type.
        /// </summary>
        Active,
    }
}
