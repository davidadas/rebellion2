using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Missions;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Selects non-conflicting proposals for execution.
    /// </summary>
    public sealed class AISelectionPhase : IAITurnPhase
    {
        private readonly bool _continueSelection;

        /// <summary>
        /// Creates the first selection stage for a faction turn.
        /// </summary>
        public AISelectionPhase()
            : this(false) { }

        /// <summary>
        /// Creates a selection stage that can retain earlier accepted assignments.
        /// </summary>
        /// <param name="continueSelection">Whether to consider newly added proposals using the turn's existing reservations.</param>
        internal AISelectionPhase(bool continueSelection)
        {
            _continueSelection = continueSelection;
        }

        /// <summary>
        /// Selects proposals and stores the result on the turn context.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            if (context == null)
                return;

            AISelectionState state = _continueSelection
                ? context.SelectionState
                    ?? throw new System.InvalidOperationException(
                        "Selection must begin before it can continue."
                    )
                : new AISelectionState();
            AIProductionSelector production = _continueSelection
                ? context.ProductionSelector
                : new AIProductionSelector(state);
            IEnumerable<AIProposal> candidates = _continueSelection
                ? context.Proposals.Skip(context.SelectionProposalCount)
                : context.Proposals;
            List<AIProposal> selected = _continueSelection
                ? context.SelectedProposals.ToList()
                : new List<AIProposal>();
            context.SetSelectedProposals(Select(context, state, production, candidates, selected));
            if (!_continueSelection)
            {
                new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);
                foreach (AIProposal proposal in context.SelectedProposals)
                    state.Reserve(AIMissionSelector.GetReservations(proposal));
            }
            context.SelectionState = state;
            context.ProductionSelector = production;
            context.SelectionProposalCount = context.Proposals.Count;
        }

        /// <summary>
        /// Returns selected proposals after score ordering and claim checks.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The selected proposals.</returns>
        public List<AIProposal> Select(AITurnContext context)
        {
            AISelectionState state = new AISelectionState();
            return Select(
                context,
                state,
                new AIProductionSelector(state),
                context?.Proposals,
                new List<AIProposal>()
            );
        }

        /// <summary>
        /// Extends a turn's accepted actions using shared reservations and production budgets.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="selectionState">Reservations retained across selection stages.</param>
        /// <param name="productionSelector">The production budgets and claims retained across stages.</param>
        /// <param name="candidates">The proposals not yet considered for selection.</param>
        /// <param name="selectedProposals">The actions already accepted for this turn.</param>
        /// <returns>The accepted actions including feasible new proposals.</returns>
        private List<AIProposal> Select(
            AITurnContext context,
            AISelectionState selectionState,
            AIProductionSelector productionSelector,
            IEnumerable<AIProposal> candidates,
            List<AIProposal> selectedProposals
        )
        {
            if (context?.Proposals == null || candidates == null)
                return selectedProposals;

            AIFleetSelector fleetSelector = new AIFleetSelector(selectionState);
            AIMissionSelector missionSelector = new AIMissionSelector(selectionState);
            float minimumSelectableScore = GetMinimumSelectableScore(context);
            LinkedList<AIProposal> pending = new(
                productionSelector.OrderInfrastructureProduction(
                    context,
                    GetSortedProposals(context, candidates, true)
                )
            );
            while (pending.First != null)
            {
                AIProposal proposal = pending.First.Value;
                pending.RemoveFirst();
                if (!HasSelectableScore(proposal, minimumSelectableScore))
                    continue;

                if (proposal is AIMissionProposal missionProposal)
                {
                    if (
                        !missionSelector.TryResolve(
                            context,
                            missionProposal,
                            out var resolvedMission
                        )
                    )
                        continue;
                    if (!ReferenceEquals(resolvedMission, proposal))
                    {
                        pending.AddLast(resolvedMission);
                        pending = new LinkedList<AIProposal>(
                            productionSelector.OrderInfrastructureProduction(
                                context,
                                GetSortedProposals(context, pending, false)
                            )
                        );
                        continue;
                    }
                }

                AIProposal selectedProposal = proposal;
                if (proposal is AIMaintenanceRecoveryProposal recovery)
                {
                    if (!productionSelector.TryResolve(context, recovery, out var selectedRecovery))
                        continue;
                    selectedProposals.Add(selectedRecovery);
                    continue;
                }
                if (proposal is AIManufactureProposal manufactureProposal)
                {
                    if (
                        !productionSelector.TryResolve(
                            context,
                            manufactureProposal,
                            out AIManufactureProposal selectedManufactureProposal
                        )
                    )
                        continue;
                    selectedProposal = selectedManufactureProposal;
                }

                if (
                    proposal is AIManufactureProposal
                    && selectedProposal is AIManufactureProposal acceptedManufactureProposal
                )
                {
                    if (
                        !selectedProposal.CanSelect(context)
                        || !productionSelector.CanReserve(context, acceptedManufactureProposal)
                    )
                        continue;

                    productionSelector.Reserve(acceptedManufactureProposal);
                    productionSelector.ScoreResolved(context, proposal, selectedProposal);
                }
                else if (
                    selectedProposal is AIFacilityRemovalProposal removalProposal
                        ? !productionSelector.TrySelect(context, removalProposal)
                    : selectedProposal is AIHeadquartersRelocationProposal relocationProposal
                        ? !productionSelector.TrySelect(context, relocationProposal)
                    : selectedProposal
                        is AIMissionProposal
                            or AIAbortMissionProposal
                            or AIMissionStagingProposal
                        ? !missionSelector.TrySelect(context, selectedProposal)
                    : IsFleetDomainProposal(selectedProposal)
                        ? !fleetSelector.TrySelect(context, selectedProposal)
                    : !selectedProposal.CanSelect(context)
                )
                    continue;

                selectedProposals.Add(selectedProposal);
                if (
                    selectedProposal is AIMissionProposal
                    {
                        MissionTypeID: MissionTypeIDs.Diplomacy
                    }
                )
                {
                    missionSelector.RefreshDiplomacyScores(context, pending);
                    pending = new LinkedList<AIProposal>(
                        productionSelector.OrderInfrastructureProduction(
                            context,
                            GetSortedProposals(context, pending, false)
                        )
                    );
                }
                else if (
                    selectedProposal is AIManufactureProposal
                    {
                        Demand.Kind: AIProductionDemandKind.ConstructionFacility
                            or AIProductionDemandKind.Shipyard
                    }
                )
                    pending = new LinkedList<AIProposal>(
                        productionSelector.OrderInfrastructureProduction(context, pending)
                    );
                if (
                    proposal is AIManufactureProposal fleetRequest
                    && productionSelector.TryCreateNextFleetProposal(
                        context,
                        fleetRequest,
                        out AIManufactureProposal next
                    )
                )
                {
                    pending.AddLast(next);
                    pending = new LinkedList<AIProposal>(
                        productionSelector.OrderInfrastructureProduction(
                            context,
                            GetSortedProposals(context, pending, false)
                        )
                    );
                }
            }

            return selectedProposals;
        }

        /// <summary>
        /// Returns whether a proposal belongs to fleet-domain contention.
        /// </summary>
        /// <param name="proposal">Proposal to classify.</param>
        /// <returns>True for fleet and unit-transfer proposals.</returns>
        private static bool IsFleetDomainProposal(AIProposal proposal)
        {
            return proposal
                is AIClearFleetOrderProposal
                    or AIFleetAttackProposal
                    or AIFleetDefenseProposal
                    or AIFleetEvacuationProposal
                    or AIFleetReinforcementRedirectionProposal
                    or AIColonizationProposal
                    or AIColonizationCampaignProposal
                    or AIOrbitalEngagementProposal
                    or AIFleetRoleProposal
                    or AITransferUnitProposal
                    or AIOfficerCommandProposal;
        }

        /// <summary>
        /// Returns the minimum score required for proposal selection.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The minimum selectable score.</returns>
        private static float GetMinimumSelectableScore(AITurnContext context)
        {
            return context.Game?.Config?.AI?.Selection?.MinimumSelectableScore
                ?? new GameConfig.AISelectionConfig().MinimumSelectableScore;
        }

        /// <summary>
        /// Returns whether a scored proposal meets its priority category's selection threshold.
        /// </summary>
        /// <param name="proposal">The proposal whose score is inspected.</param>
        /// <param name="minimumScore">The exclusive score floor for optional work.</param>
        /// <returns>True when domain selection may consider the proposal.</returns>
        private static bool HasSelectableScore(AIProposal proposal, float minimumScore)
        {
            return proposal.HasScore
                && (
                    proposal.Priority >= AIProposalPriority.Mandatory
                    || proposal.Score > minimumScore
                );
        }

        /// <summary>
        /// Orders proposals by strategic value and randomizes eligible score ties.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="candidates">The proposals still awaiting selection.</param>
        /// <param name="shuffleTies">Whether to initialize random tie order.</param>
        /// <returns>Sorted proposals.</returns>
        private static IEnumerable<AIProposal> GetSortedProposals(
            AITurnContext context,
            IEnumerable<AIProposal> candidates,
            bool shuffleTies
        )
        {
            List<AIProposal> proposals = candidates
                .Where(proposal => proposal != null)
                .OrderByDescending(proposal => proposal.Priority)
                .ThenByDescending(proposal => proposal.Score)
                .ThenByDescending(proposal => proposal.HasScore)
                .ToList();
            float minimumScore = GetMinimumSelectableScore(context);
            int groupStart = 0;
            while (shuffleTies && groupStart < proposals.Count)
            {
                int groupEnd = groupStart + 1;
                while (
                    groupEnd < proposals.Count
                    && proposals[groupEnd].Priority == proposals[groupStart].Priority
                    && proposals[groupEnd].Score == proposals[groupStart].Score
                    && proposals[groupEnd].HasScore == proposals[groupStart].HasScore
                )
                {
                    groupEnd++;
                }

                if (HasSelectableScore(proposals[groupStart], minimumScore))
                    ShuffleRange(proposals, groupStart, groupEnd, context.Random);
                groupStart = groupEnd;
            }

            AIProductionSelector.OrderFleetProduction(context, proposals);
            return proposals;
        }

        /// <summary>
        /// Randomizes one tied proposal range using the persisted game random stream.
        /// </summary>
        /// <param name="proposals">The proposal list to update.</param>
        /// <param name="start">The inclusive tied-range start.</param>
        /// <param name="end">The exclusive tied-range end.</param>
        /// <param name="random">The persisted random provider.</param>
        private static void ShuffleRange(
            IList<AIProposal> proposals,
            int start,
            int end,
            Rebellion.Util.Random.IRandomNumberProvider random
        )
        {
            if (random == null)
                return;

            for (int index = end - 1; index > start; index--)
            {
                int otherIndex = random.NextInt(start, index + 1);
                (proposals[index], proposals[otherIndex]) = (
                    proposals[otherIndex],
                    proposals[index]
                );
            }
        }
    }
}
