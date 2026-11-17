namespace Rebellion.Game.Units
{
    /// <summary>
    /// Identifies the authored faction allegiance used by battle loyalty changes.
    /// </summary>
    public enum OfficerLoyaltyAllegiance
    {
        /// <summary>
        /// Reverses a loyalty change favored by either campaign faction.
        /// </summary>
        None = 0,

        /// <summary>
        /// Follows loyalty changes favored by the faction at combat index zero.
        /// </summary>
        FirstFaction = 1,

        /// <summary>
        /// Follows loyalty changes favored by the faction at combat index one.
        /// </summary>
        SecondFaction = 2,
    }
}
