using System;
using System.Numerics;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures one side's command support and strategic departure information.
    /// </summary>
    public sealed class TacticalCombatSide
    {
        public CombatSide Side { get; }
        public int FactionIndex { get; }
        public Vector3 Forward { get; }
        public bool HasWithdrawalDestination { get; }
        public float InitialWithdrawalMilliseconds { get; }
        public float RetreatStrengthRatio { get; }
        public TacticalCombatUnit CapitalCommandCarrier { get; }
        public TacticalCombatUnit FighterCommandCarrier { get; }
        public float AdmiralLeadership { get; }
        public float CommanderCombat { get; }
        public bool IsAutomated { get; internal set; }
        public bool IsWithdrawing => Withdrawal != null;
        public float AttackBudget { get; internal set; }
        public float WithdrawalMilliseconds => Withdrawal?.RemainingMilliseconds ?? 0;
        public TacticalCombatWithdrawalPhase? WithdrawalPhase => Withdrawal?.Phase;
        internal TacticalCombatWithdrawal Withdrawal { get; set; }

        /// <summary>
        /// Captures the command officers and campaign withdrawal route at battle entry.
        /// </summary>
        /// <param name="side">The side's encounter identity.</param>
        /// <param name="factionIndex">The authored faction index used by deployment and battle-station operations.</param>
        /// <param name="forward">The authored deployment heading.</param>
        /// <param name="hasWithdrawalDestination">Whether campaign movement found a legal departure route.</param>
        /// <param name="withdrawalMilliseconds">The route's initial tactical departure delay.</param>
        /// <param name="retreatStrengthRatio">The strategic controller's retreat strength threshold.</param>
        /// <param name="capitalCommandCarrier">The active unit hosting the selected admiral.</param>
        /// <param name="fighterCommandCarrier">The active unit hosting the selected commander.</param>
        /// <param name="admiralLeadership">The selected admiral's effective leadership.</param>
        /// <param name="commanderCombat">The selected commander's effective combat rating.</param>
        /// <param name="isAutomated">Whether this side is controlled by tactical automation.</param>
        public TacticalCombatSide(
            CombatSide side,
            int factionIndex,
            Vector3 forward,
            bool hasWithdrawalDestination,
            float withdrawalMilliseconds,
            float retreatStrengthRatio,
            TacticalCombatUnit capitalCommandCarrier,
            TacticalCombatUnit fighterCommandCarrier,
            float admiralLeadership,
            float commanderCombat,
            bool isAutomated
        )
        {
            if (side == CombatSide.Draw)
                throw new ArgumentOutOfRangeException(nameof(side));
            Side = side;
            FactionIndex = factionIndex;
            Forward = forward;
            HasWithdrawalDestination = hasWithdrawalDestination;
            InitialWithdrawalMilliseconds = withdrawalMilliseconds;
            RetreatStrengthRatio = retreatStrengthRatio;
            CapitalCommandCarrier = capitalCommandCarrier;
            FighterCommandCarrier = fighterCommandCarrier;
            AdmiralLeadership = admiralLeadership;
            CommanderCombat = commanderCombat;
            IsAutomated = isAutomated;
        }

        /// <summary>
        /// Calculates the supported movement budget while the command officer's host remains active.
        /// </summary>
        /// <param name="isFighter">Whether the budget is for a fighter squadron.</param>
        /// <returns>The supported budget clamped between one and nine.</returns>
        public float GetCommandBudget(bool isFighter) => (float)GetMovementSupport(isFighter);

        /// <summary>
        /// Reads the selected officer's live host and retains movement-support precision until its consumer stores a result.
        /// </summary>
        /// <param name="isFighter">Whether support belongs to the fighter command officer.</param>
        /// <returns>The ordered supported movement budget between one and nine.</returns>
        internal double GetMovementSupport(bool isFighter)
        {
            TacticalCombatUnit host = isFighter ? FighterCommandCarrier : CapitalCommandCarrier;
            if (host?.IsActiveDisplayObject != true)
                return 1;
            float scalar = isFighter ? CommanderCombat / 20 : AdmiralLeadership / 10;
            double value = isFighter ? (double)scalar + 1 : 9d - scalar;
            if (!(value > 1))
                return 1;
            return value >= 9 ? 9 : value;
        }
    }
}
