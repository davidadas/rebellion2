namespace Rebellion.Gameplay
{
    /// <summary>
    /// Supplies one configured side's support weights and bounds for either force ordering.
    /// </summary>
    internal readonly struct SpaceCombatSupportWeights
    {
        public int LeadingFlat { get; }
        public int LeadingGapScale { get; }
        public int TrailingFlat { get; }
        public int TrailingGapScale { get; }
        public int MagnitudeCap { get; }
        public uint SupportThreshold { get; }

        /// <summary>
        /// Captures the side's configured support calculation inputs.
        /// </summary>
        /// <param name="leadingFlat">The flat weight when this side leads.</param>
        /// <param name="leadingGapScale">The gap weight when this side leads.</param>
        /// <param name="trailingFlat">The flat weight when the other side leads.</param>
        /// <param name="trailingGapScale">The gap weight when this side trails.</param>
        /// <param name="magnitudeCap">The upper magnitude bound when this side benefits.</param>
        /// <param name="supportThreshold">The threshold when this side is the comparison side.</param>
        public SpaceCombatSupportWeights(
            int leadingFlat,
            int leadingGapScale,
            int trailingFlat,
            int trailingGapScale,
            int magnitudeCap,
            uint supportThreshold
        )
        {
            LeadingFlat = leadingFlat;
            LeadingGapScale = leadingGapScale;
            TrailingFlat = trailingFlat;
            TrailingGapScale = trailingGapScale;
            MagnitudeCap = magnitudeCap;
            SupportThreshold = supportThreshold;
        }
    }
}
