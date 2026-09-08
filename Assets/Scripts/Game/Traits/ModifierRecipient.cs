namespace Rebellion.Game.Traits
{
    /// <summary>
    /// Optionally restricts which kind of entity inside a target scope receives a modifier.
    /// </summary>
    public enum ModifierRecipient
    {
        Any,
        Officer,
        CapitalShip,
        Fleet,
        Planet,
        Building,
    }
}
