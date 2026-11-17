namespace Rebellion.Game.Units
{
    /// <summary>
    /// Identifies the authored faction allegiance used by battle loyalty changes.
    /// None reverses either faction's change. First and second faction refer to
    /// combat indices zero and one.
    /// </summary>
    public enum OfficerLoyaltyAllegiance
    {
        None = 0,
        FirstFaction = 1,
        SecondFaction = 2,
    }
}
