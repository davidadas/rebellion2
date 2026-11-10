namespace Rebellion.Gameplay
{
    /// <summary>
    /// Why a side can or cannot withdraw from a tactical battle at this moment.
    /// </summary>
    public enum TacticalWithdrawalStatus
    {
        /// <summary>The side may order a withdrawal.</summary>
        Available,

        /// <summary>No unit still in the battle has a hyperdrive to jump with.</summary>
        NoHyperdrive,

        /// <summary>The side has no safe system to jump to.</summary>
        NoEscapeRoute,

        /// <summary>An enemy ship still in the battle projects a gravity well over the system.</summary>
        Interdicted,
    }
}
