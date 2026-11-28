using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;

namespace Rebellion.Tactical
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
        public CombatUnit CapitalCommandCarrier { get; }
        public CombatUnit FighterCommandCarrier { get; }
        public float AdmiralLeadership { get; }
        public float CommanderCombat { get; }
        public bool IsAutomated { get; internal set; }
        internal bool CapitalGroupsPartitioned { get; set; }
        internal int AutomationCountdown { get; set; }
        internal IReadOnlyList<(
            CombatUnit Unit,
            int Category
        )> InitialCapitalDeployment { get; set; }
        public bool IsWithdrawing { get; private set; }
        public float AttackBudget { get; internal set; }
        public float WithdrawalMilliseconds { get; private set; }
        public CombatWithdrawalPhase? WithdrawalPhase { get; private set; }

        /// <summary>
        /// Captures the support and withdrawal state independently of the campaign roster.
        /// </summary>
        /// <returns>The side's saved state.</returns>
        internal ActiveBattle.SideState CaptureState() =>
            new()
            {
                Side = Side,
                FactionIndex = FactionIndex,
                Forward = Forward,
                HasWithdrawalDestination = HasWithdrawalDestination,
                InitialWithdrawalMilliseconds = InitialWithdrawalMilliseconds,
                RetreatStrengthRatio = RetreatStrengthRatio,
                CapitalCommandCarrierID = CapitalCommandCarrier?.InstanceID,
                FighterCommandCarrierID = FighterCommandCarrier?.InstanceID,
                AdmiralLeadership = AdmiralLeadership,
                CommanderCombat = CommanderCombat,
                Automated = IsAutomated,
                CapitalGroupsPartitioned = CapitalGroupsPartitioned,
                AutomationCountdown = AutomationCountdown,
                InitialCapitalDeployment =
                    InitialCapitalDeployment
                        ?.Select(entry => new ActiveBattle.SideState.CapitalDeployment
                        {
                            UnitID = entry.Unit.InstanceID,
                            Category = entry.Category,
                        })
                        .ToList()
                    ?? new(),
                AttackBudget = AttackBudget,
                WithdrawalPhase = WithdrawalPhase,
                WithdrawalMilliseconds = WithdrawalMilliseconds,
            };

        /// <summary>
        /// Restores the side's support and departure progress from the saved encounter.
        /// </summary>
        /// <param name="state">The saved side state.</param>
        /// <param name="getUnit">Resolves identities into the loaded battle roster.</param>
        /// <returns>The side ready to resume its existing orders.</returns>
        internal static TacticalCombatSide RestoreState(
            ActiveBattle.SideState state,
            Func<string, CombatUnit> getUnit
        ) =>
            new(
                state.Side,
                state.FactionIndex,
                state.Forward,
                state.HasWithdrawalDestination,
                state.InitialWithdrawalMilliseconds,
                state.RetreatStrengthRatio,
                getUnit(state.CapitalCommandCarrierID),
                getUnit(state.FighterCommandCarrierID),
                state.AdmiralLeadership,
                state.CommanderCombat,
                state.Automated
            )
            {
                CapitalGroupsPartitioned = state.CapitalGroupsPartitioned,
                AutomationCountdown = state.AutomationCountdown,
                InitialCapitalDeployment = state
                    .InitialCapitalDeployment.Select(entry =>
                        (getUnit(entry.UnitID), entry.Category)
                    )
                    .ToArray(),
                IsWithdrawing = state.WithdrawalPhase != null,
                AttackBudget = state.AttackBudget,
                WithdrawalPhase = state.WithdrawalPhase,
                WithdrawalMilliseconds = state.WithdrawalMilliseconds,
            };

        /// <summary>
        /// Starts the side's withdrawal countdown once.
        /// </summary>
        internal void BeginWithdrawal()
        {
            if (FactionIndex is not 0 and not 1)
                throw new ArgumentOutOfRangeException(nameof(FactionIndex));

            IsWithdrawing = true;
            WithdrawalPhase = CombatWithdrawalPhase.Waiting;
            WithdrawalMilliseconds = InitialWithdrawalMilliseconds;
        }

        /// <summary>
        /// Raises the route countdown to the applicable station or fleet departure minimum.
        /// </summary>
        /// <param name="stationOperational">Whether the battle station contributes to the withdrawal delay.</param>
        internal void SelectWithdrawalDelay(bool stationOperational)
        {
            float minimum = FactionIndex == 1 && stationOperational ? 150000 : 30000;
            if (!(WithdrawalMilliseconds > minimum))
                WithdrawalMilliseconds = minimum;
        }

        // Returns true once, on the tick that admits the side's departures.
        /// <summary>
        /// Advances the waiting phase and admits departure when the countdown elapses.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed interval in milliseconds.</param>
        /// <returns>Whether this interval opened the departure phase.</returns>
        internal bool AdvanceWithdrawal(float elapsedMilliseconds)
        {
            if (WithdrawalPhase != CombatWithdrawalPhase.Waiting)
                return false;

            double remaining = (double)WithdrawalMilliseconds - elapsedMilliseconds;
            WithdrawalMilliseconds = (float)remaining;
            if (remaining > 0)
                return false;

            WithdrawalPhase = CombatWithdrawalPhase.Departing;
            return true;
        }

        /// <summary>
        /// Records that this side may now scuttle fighters that cannot recover.
        /// </summary>
        internal void StartScuttling() => WithdrawalPhase = CombatWithdrawalPhase.Scuttling;

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
            CombatUnit capitalCommandCarrier,
            CombatUnit fighterCommandCarrier,
            float admiralLeadership,
            float commanderCombat,
            bool isAutomated
        )
        {
            if (side is not CombatSide.Attacker and not CombatSide.Defender)
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
            CombatUnit host = isFighter ? FighterCommandCarrier : CapitalCommandCarrier;
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
