namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures a calculated support shift using selectors for the configured first and second sides.
    /// </summary>
    internal readonly struct SpaceCombatSupportAdjustment
    {
        public int BeneficiarySide { get; }
        public uint Magnitude { get; }
        public int ComparisonSide { get; }
        public uint SupportThreshold { get; }

        /// <summary>
        /// Captures the calculated support rule without selecting destination planets.
        /// </summary>
        /// <param name="beneficiarySide">The benefiting configured side, numbered one or two.</param>
        /// <param name="magnitude">The raw magnitude bits after signed lower and upper bounds are applied.</param>
        /// <param name="comparisonSide">The opposing configured side used for support comparison.</param>
        /// <param name="supportThreshold">The comparison side's configured support threshold.</param>
        public SpaceCombatSupportAdjustment(
            int beneficiarySide,
            uint magnitude,
            int comparisonSide,
            uint supportThreshold
        )
        {
            BeneficiarySide = beneficiarySide;
            Magnitude = magnitude;
            ComparisonSide = comparisonSide;
            SupportThreshold = supportThreshold;
        }
    }
}
