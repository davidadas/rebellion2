using System;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures one faction's authority and withdrawal facts for its local HUD.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkSideState
    {
        public CombatSide Side;
        public bool Automated;
        public bool CommandsSuspended;
        public bool Withdrawing;
        public float WithdrawalMilliseconds;
        public bool HasWithdrawalPhase;
        public TacticalCombatWithdrawalPhase WithdrawalPhase;
        public float AttackBudget;
    }
}
