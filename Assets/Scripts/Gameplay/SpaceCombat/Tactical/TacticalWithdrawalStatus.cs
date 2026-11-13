namespace Rebellion.Gameplay
{
    /// <summary>
    /// Why a side can or cannot withdraw from a tactical battle at this moment.
    /// </summary>
    public enum TacticalWithdrawalStatus
    {
        // The side may order a withdrawal.
        Available,

        // No unit still in the battle has a hyperdrive to jump with.
        NoHyperdrive,

        // The side has no safe system to jump to.
        NoEscapeRoute,

        // An enemy ship still in the battle projects a gravity well over the system.
        Interdicted,
    }
}
