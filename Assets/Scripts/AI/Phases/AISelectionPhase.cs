using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Selects non-conflicting proposals for execution.
    /// </summary>
    public sealed class AISelectionPhase : IAITurnPhase
    {
        private static readonly OfficerRank[] _attackCommandRanks =
        {
            OfficerRank.General,
            OfficerRank.Admiral,
            OfficerRank.Commander,
        };

        /// <summary>
        /// Selects proposals and stores the result on the turn context.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            if (context == null)
                return;

            List<AIProposal> selected = Select(context);
            context.SetSelectedProposals(selected);
            AssignAttackCommands(context);
            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);
            RestoreEssentialMissionsWhenCommandConflicts(context, selected);
        }

        /// <summary>
        /// Gives the original selected mission set precedence when provisional command staffing
        /// would remove the faction's last espionage or sabotage mission.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="selected">The selected proposals before command reservations.</param>
        private static void RestoreEssentialMissionsWhenCommandConflicts(
            AITurnContext context,
            IReadOnlyCollection<AIProposal> selected
        )
        {
            if (
                context.CommandAssignments.Count == 0
                || RetainsMissionCapability(context, selected, MissionTypeIDs.Espionage)
                    && RetainsMissionCapability(context, selected, MissionTypeIDs.Sabotage)
            )
                return;

            context.SetCommandAssignments(null);
            context.SetSelectedProposals(selected);
            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);
        }

        /// <summary>
        /// Returns whether a mission capability remains selected when it existed before command
        /// reservations were applied.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="selected">The selected proposals before command reservations.</param>
        /// <param name="missionTypeId">The mission capability to inspect.</param>
        /// <returns>True when the capability was absent originally or remains selected.</returns>
        private static bool RetainsMissionCapability(
            AITurnContext context,
            IEnumerable<AIProposal> selected,
            string missionTypeId
        )
        {
            bool wasSelected = selected
                .OfType<AIMissionProposal>()
                .Any(proposal => proposal.MissionTypeID == missionTypeId);
            return !wasSelected
                || context
                    .SelectedProposals.OfType<AIMissionProposal>()
                    .Any(proposal => proposal.MissionTypeID == missionTypeId);
        }

        /// <summary>
        /// Reserves one useful command officer for each selected stationary attack fleet without
        /// displacing diplomacy, research, or the last selected espionage or sabotage mission.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        private static void AssignAttackCommands(AITurnContext context)
        {
            List<AIProposal> selected = context.SelectedProposals.ToList();
            List<AIFleetAttackProposal> attacks = selected
                .OfType<AIFleetAttackProposal>()
                .Where(proposal =>
                    proposal.OrderType == FleetOrderType.Attack
                    && proposal.Status != FleetOrderStatus.Returning
                    && proposal.Fleet != null
                    && proposal.Fleet.Movement == null
                    && proposal.Fleet.IsInCombat == false
                )
                .ToList();
            if (attacks.Count == 0 || context.Assessment == null)
            {
                context.SetCommandAssignments(null);
                return;
            }

            HashSet<string> protectedOfficerIds = GetProtectedMissionOfficerIds(selected);
            HashSet<string> attackFleetIds = attacks
                .Select(proposal => proposal.Fleet.InstanceID)
                .ToHashSet(StringComparer.Ordinal);
            Dictionary<string, List<Officer>> officersByPlanetId = BuildCommandOfficerIndex(
                context,
                attackFleetIds
            );
            HashSet<string> assignedOfficerIds = new HashSet<string>(StringComparer.Ordinal);
            List<(Officer Officer, Fleet Fleet, OfficerRank Rank)> assignments = new();

            foreach (AIFleetAttackProposal attack in attacks)
            {
                Fleet fleet = attack.Fleet;
                Planet planet = fleet.GetParentOfType<Planet>();
                if (
                    planet == null
                    || !officersByPlanetId.TryGetValue(
                        planet.InstanceID,
                        out List<Officer> localOfficers
                    )
                )
                    continue;

                (Officer officer, OfficerRank rank) = SelectCommandOfficer(
                    context,
                    fleet,
                    localOfficers,
                    protectedOfficerIds,
                    assignedOfficerIds
                );
                if (officer == null)
                    continue;

                assignments.Add((officer, fleet, rank));
                assignedOfficerIds.Add(officer.InstanceID);
            }

            context.SetCommandAssignments(assignments);
            if (assignments.Count == 0)
                return;

            context.SetSelectedProposals(
                selected.Where(proposal => !UsesReservedCommandOfficer(context, proposal))
            );
        }

        /// <summary>
        /// Returns officers whose selected mission takes precedence over attack-fleet command.
        /// </summary>
        /// <param name="selected">The selected proposal set.</param>
        /// <returns>Protected officer identifiers.</returns>
        private static HashSet<string> GetProtectedMissionOfficerIds(
            IReadOnlyCollection<AIProposal> selected
        )
        {
            HashSet<string> protectedOfficerIds = new HashSet<string>(StringComparer.Ordinal);
            List<AIMissionProposal> missions = selected.OfType<AIMissionProposal>().ToList();
            foreach (
                AIMissionProposal mission in missions.Where(proposal =>
                    proposal.MissionTypeID == MissionTypeIDs.Diplomacy
                    || proposal.MissionTypeID == MissionTypeIDs.Research
                )
            )
                AddOfficerIds(protectedOfficerIds, mission);

            ProtectHighestPriorityMission(protectedOfficerIds, missions, MissionTypeIDs.Espionage);
            ProtectHighestPriorityMission(protectedOfficerIds, missions, MissionTypeIDs.Sabotage);
            return protectedOfficerIds;
        }

        /// <summary>
        /// Protects the strongest selected mission of one type so command staffing cannot remove
        /// that capability completely.
        /// </summary>
        /// <param name="protectedOfficerIds">The protected officer set to update.</param>
        /// <param name="missions">Selected missions to inspect.</param>
        /// <param name="missionTypeId">The mission capability to preserve.</param>
        private static void ProtectHighestPriorityMission(
            ISet<string> protectedOfficerIds,
            IEnumerable<AIMissionProposal> missions,
            string missionTypeId
        )
        {
            AIMissionProposal mission = missions
                .Where(proposal => proposal.MissionTypeID == missionTypeId)
                .OrderByDescending(proposal => proposal.Score)
                .ThenBy(proposal => proposal.GetSortKey(), StringComparer.Ordinal)
                .FirstOrDefault();
            AddOfficerIds(protectedOfficerIds, mission);
        }

        /// <summary>
        /// Adds the officer participants from one mission to a protected identifier set.
        /// </summary>
        /// <param name="officerIds">The identifier set to update.</param>
        /// <param name="mission">The mission whose participants are protected.</param>
        private static void AddOfficerIds(ISet<string> officerIds, AIMissionProposal mission)
        {
            if (mission == null)
                return;

            foreach (Officer officer in mission.Participants.OfType<Officer>())
                officerIds.Add(officer.InstanceID);
        }

        /// <summary>
        /// Indexes available command-qualified officers by planet once for the selection pass.
        /// Officers assigned to unrelated fleets remain with those fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="attackFleetIds">Selected attack-fleet identifiers.</param>
        /// <returns>Available officers keyed by current planet identifier.</returns>
        private static Dictionary<string, List<Officer>> BuildCommandOfficerIndex(
            AITurnContext context,
            HashSet<string> attackFleetIds
        )
        {
            Dictionary<string, List<Officer>> officersByPlanetId = new Dictionary<
                string,
                List<Officer>
            >(StringComparer.Ordinal);
            foreach (
                Officer officer in context.Assessment.AvailableMissionParticipants.OfType<Officer>()
            )
            {
                Fleet currentFleet = officer.GetParentOfType<Fleet>();
                Planet planet = officer.GetParentOfType<Planet>();
                if (
                    !IsAvailableCommandOfficer(context, officer)
                    || planet == null
                    || currentFleet != null && !attackFleetIds.Contains(currentFleet.InstanceID)
                )
                    continue;

                if (
                    !officersByPlanetId.TryGetValue(
                        planet.InstanceID,
                        out List<Officer> localOfficers
                    )
                )
                {
                    localOfficers = new List<Officer>();
                    officersByPlanetId.Add(planet.InstanceID, localOfficers);
                }

                localOfficers.Add(officer);
            }

            return officersByPlanetId;
        }

        /// <summary>
        /// Returns whether an officer can receive a command appointment during this turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="officer">The officer to inspect.</param>
        /// <returns>True when the officer is healthy, stationary, active, and rank-qualified.</returns>
        private static bool IsAvailableCommandOfficer(AITurnContext context, Officer officer)
        {
            return officer != null
                && officer.GetOwnerInstanceID() == context.Faction?.InstanceID
                && !officer.IsCaptured
                && !officer.IsKilled
                && !officer.IsRetired
                && officer.InjuryPoints <= 0
                && officer.Movement == null
                && !officer.IsOnMission()
                && officer.AllowedRanks?.Any(rank => rank != OfficerRank.None) == true;
        }

        /// <summary>
        /// Selects the officer and post that provide the largest direct benefit to one attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet receiving command staff.</param>
        /// <param name="officers">Available officers at the fleet's planet.</param>
        /// <param name="protectedOfficerIds">Officers retained for essential missions.</param>
        /// <param name="assignedOfficerIds">Officers already assigned to another attack fleet.</param>
        /// <returns>The selected officer and rank, or an empty assignment.</returns>
        private static (Officer Officer, OfficerRank Rank) SelectCommandOfficer(
            AITurnContext context,
            Fleet fleet,
            IEnumerable<Officer> officers,
            HashSet<string> protectedOfficerIds,
            HashSet<string> assignedOfficerIds
        )
        {
            int regimentCount = context.Assessment.GetReadyFleetRegimentCount(fleet);
            int capitalShipCount = fleet.GetOperationalCapitalShipCount();
            int starfighterCount = fleet
                .GetStarfighters()
                .Count(starfighter =>
                    starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                    && starfighter.Movement == null
                );
            Officer selectedOfficer = null;
            OfficerRank selectedRank = OfficerRank.None;
            double selectedBenefit = 0;

            foreach (Officer officer in officers)
            {
                if (
                    protectedOfficerIds.Contains(officer.InstanceID)
                    || assignedOfficerIds.Contains(officer.InstanceID)
                )
                    continue;

                Fleet currentFleet = officer.GetParentOfType<Fleet>();
                if (currentFleet != null && currentFleet != fleet)
                    continue;

                foreach (OfficerRank rank in _attackCommandRanks)
                {
                    if (officer.AllowedRanks?.Contains(rank) != true)
                        continue;

                    double benefit = GetCommandBenefit(
                        context,
                        officer,
                        rank,
                        regimentCount,
                        capitalShipCount,
                        starfighterCount
                    );
                    if (
                        benefit > 0
                        && IsPreferredCommandAssignment(
                            officer,
                            rank,
                            benefit,
                            fleet,
                            selectedOfficer,
                            selectedRank,
                            selectedBenefit
                        )
                    )
                    {
                        selectedOfficer = officer;
                        selectedRank = rank;
                        selectedBenefit = benefit;
                    }
                }
            }

            return (selectedOfficer, selectedRank);
        }

        /// <summary>
        /// Estimates the direct number of fleet elements improved by one command appointment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="officer">The proposed command officer.</param>
        /// <param name="rank">The proposed command post.</param>
        /// <param name="regimentCount">Ready regiments carried by the fleet.</param>
        /// <param name="capitalShipCount">Operational capital ships in the fleet.</param>
        /// <param name="starfighterCount">Ready starfighters carried by the fleet.</param>
        /// <returns>A positive relative benefit when the appointment affects the fleet.</returns>
        private static double GetCommandBenefit(
            AITurnContext context,
            Officer officer,
            OfficerRank rank,
            int regimentCount,
            int capitalShipCount,
            int starfighterCount
        )
        {
            GameConfig.CombatConfig combat = context.Game?.Config?.Combat;
            if (combat == null)
                return 0;

            if (rank == OfficerRank.General)
            {
                int divisor = combat.PlanetaryAssault.GeneralLeadershipDivisor;
                return divisor > 0
                    ? officer.GetEffectiveRating(SkillRating.Leadership) / divisor * regimentCount
                    : 0;
            }

            if (rank == OfficerRank.Admiral)
            {
                int divisor = combat.SpaceCombat.AdmiralLeadershipDivisor;
                return divisor > 0
                    ? (double)officer.GetEffectiveRating(SkillRating.Leadership)
                        / divisor
                        * capitalShipCount
                    : 0;
            }

            if (rank == OfficerRank.Commander)
            {
                int divisor = combat.SpaceCombat.CommanderCombatDivisor;
                return divisor > 0
                    ? (double)officer.GetEffectiveRating(SkillRating.Combat)
                        / divisor
                        * starfighterCount
                    : 0;
            }

            return 0;
        }

        /// <summary>
        /// Compares a command assignment with the best assignment found so far.
        /// </summary>
        /// <param name="officer">The candidate officer.</param>
        /// <param name="rank">The candidate rank.</param>
        /// <param name="benefit">The candidate benefit.</param>
        /// <param name="fleet">The fleet receiving the appointment.</param>
        /// <param name="selectedOfficer">The currently selected officer.</param>
        /// <param name="selectedRank">The currently selected rank.</param>
        /// <param name="selectedBenefit">The currently selected benefit.</param>
        /// <returns>True when the candidate should replace the current selection.</returns>
        private static bool IsPreferredCommandAssignment(
            Officer officer,
            OfficerRank rank,
            double benefit,
            Fleet fleet,
            Officer selectedOfficer,
            OfficerRank selectedRank,
            double selectedBenefit
        )
        {
            int benefitComparison = benefit.CompareTo(selectedBenefit);
            if (selectedOfficer == null || benefitComparison != 0)
                return selectedOfficer == null || benefitComparison > 0;

            bool officerAlreadyAboard = officer.GetParentOfType<Fleet>() == fleet;
            bool selectedOfficerAlreadyAboard = selectedOfficer.GetParentOfType<Fleet>() == fleet;
            if (officerAlreadyAboard != selectedOfficerAlreadyAboard)
                return officerAlreadyAboard;

            int rankComparison = rank.CompareTo(selectedRank);
            return rankComparison != 0
                ? rankComparison > 0
                : string.Compare(
                    officer.InstanceID,
                    selectedOfficer.InstanceID,
                    StringComparison.Ordinal
                ) < 0;
        }

        /// <summary>
        /// Returns whether a mission conflicts with an officer reserved for attack-fleet command.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True when the proposal uses a reserved command officer.</returns>
        private static bool UsesReservedCommandOfficer(AITurnContext context, AIProposal proposal)
        {
            return proposal is AIMissionProposal mission
                && mission
                    .Participants.OfType<Officer>()
                    .Any(officer => context.IsOfficerReservedForCommand(officer.InstanceID));
        }

        /// <summary>
        /// Returns selected proposals after score ordering and claim checks.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The selected proposals.</returns>
        public List<AIProposal> Select(AITurnContext context)
        {
            List<AIProposal> selectedProposals = new List<AIProposal>();
            if (context?.Proposals == null)
                return selectedProposals;

            AISelectionState selectionState = new AISelectionState();
            AIFleetSelector fleetSelector = new AIFleetSelector(selectionState);
            AIMissionSelector missionSelector = new AIMissionSelector(selectionState);
            AIProductionSelector productionSelector = new AIProductionSelector(selectionState);
            float minimumSelectableScore = GetMinimumSelectableScore(context);
            foreach (AIProposal proposal in GetSortedProposals(context))
            {
                if (
                    !proposal.HasScore
                    || proposal.Priority != AIProposalPriority.Mandatory
                        && proposal.Score <= minimumSelectableScore
                )
                    continue;

                AIProposal selectedProposal = proposal;
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
                    : selectedProposal is AIMissionProposal or AIAbortMissionProposal
                        ? !missionSelector.TrySelect(context, selectedProposal)
                    : IsFleetDomainProposal(selectedProposal)
                        ? !fleetSelector.TrySelect(context, selectedProposal)
                    : !selectedProposal.CanSelect(context)
                )
                    continue;

                selectedProposals.Add(selectedProposal);
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
                    or AIColonizationProposal
                    or AIColonizationCampaignProposal
                    or AIOrbitalEngagementProposal
                    or AIFleetRoleProposal
                    or AITransferUnitProposal;
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
        /// Returns proposals ordered by strategic value with seed-faithful random tie resolution.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Sorted proposals.</returns>
        private static IEnumerable<AIProposal> GetSortedProposals(AITurnContext context)
        {
            List<AIProposal> proposals = context
                .Proposals.Where(proposal => proposal != null)
                .OrderByDescending(proposal => proposal.Priority)
                .ThenByDescending(proposal => proposal.Score)
                .ToList();
            int groupStart = 0;
            while (groupStart < proposals.Count)
            {
                int groupEnd = groupStart + 1;
                while (
                    groupEnd < proposals.Count
                    && proposals[groupEnd].Priority == proposals[groupStart].Priority
                    && proposals[groupEnd].Score == proposals[groupStart].Score
                )
                {
                    groupEnd++;
                }

                ShuffleRange(proposals, groupStart, groupEnd, context.Random);
                groupStart = groupEnd;
            }

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
