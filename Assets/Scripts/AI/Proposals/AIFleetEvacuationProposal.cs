using Rebellion.AI.Demands;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Returns a fleet that cannot act at an unsafe location to friendly territory.
    /// </summary>
    public sealed class AIFleetEvacuationProposal : AIProposal
    {
        public Fleet Fleet { get; }

        public Planet CurrentPlanet { get; }

        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        /// <summary>
        /// Creates a proposal to evacuate a fleet from its current planet.
        /// </summary>
        /// <param name="fleet">Fleet to evacuate.</param>
        /// <param name="currentPlanet">Planet currently containing the fleet.</param>
        public AIFleetEvacuationProposal(Fleet fleet, Planet currentPlanet)
        {
            Fleet = fleet;
            CurrentPlanet = currentPlanet;
        }

        /// <summary>
        /// Returns a stable sort key for this evacuation.
        /// </summary>
        /// <returns>Stable evacuation key.</returns>
        public override string GetSortKey()
        {
            return $"fleet-evacuation:{Fleet?.InstanceID}:{CurrentPlanet?.InstanceID}";
        }

        /// <summary>
        /// Returns whether the fleet remains stationary at the recorded planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when evacuation remains valid.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether the fleet remains stationary at the recorded planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when evacuation remains valid.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Clears the fleet's stale assignment and evacuates it to friendly territory.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            Fleet.Order = null;
            context.Movement?.EvacuateToNearestFriendlyPlanet(Fleet);
        }

        /// <summary>
        /// Returns whether a fleet must leave its current planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="currentPlanet">The planet currently containing the fleet.</param>
        /// <returns>
        /// True when the stationary fleet cannot act at hostile territory or has no operational
        /// capital ships beneath an opposing fleet at friendly territory.
        /// </returns>
        internal static bool IsRequired(AITurnContext context, Fleet fleet, Planet currentPlanet)
        {
            if (
                context?.Faction == null
                || fleet?.GetOwnerInstanceID() != context.Faction.InstanceID
                || currentPlanet == null
                || fleet.Movement != null
                || fleet.IsInCombat
                || fleet.GetParentOfType<Planet>()?.InstanceID != currentPlanet.InstanceID
            )
                return false;

            string planetOwnerId = currentPlanet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(planetOwnerId))
                return false;

            int strongestHostileFleetStrength = context.Assessment.GetStrongestHostileFleetStrength(
                currentPlanet
            );
            bool hasHostileOwnership = planetOwnerId != context.Faction.InstanceID;
            if (!hasHostileOwnership)
            {
                return strongestHostileFleetStrength > 0 && !fleet.HasOperationalCapitalShips();
            }

            return !CanAct(context, fleet, currentPlanet, strongestHostileFleetStrength > 0);
        }

        /// <summary>
        /// Returns whether the proposal still matches live fleet and planet state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the fleet still requires evacuation.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            return IsRequired(context, Fleet, CurrentPlanet);
        }

        /// <summary>
        /// Returns whether the fleet has a viable immediate action at the hostile planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="hostilePlanet">The hostile planet currently containing the fleet.</param>
        /// <param name="hasHostileFleet">Whether a stationary hostile fleet is present.</param>
        /// <returns>True when the fleet can win orbit, bombard, or assault.</returns>
        private static bool CanAct(
            AITurnContext context,
            Fleet fleet,
            Planet hostilePlanet,
            bool hasHostileFleet
        )
        {
            if (hasHostileFleet)
            {
                int required = context.GetAttackDemand(hostilePlanet)?.OrbitalStrength ?? 0;
                return required > 0
                    && context.Assessment.GetProjectedFleetCombatValueAgainstCapitalShips(fleet)
                        >= required;
            }

            if (
                context.Assessment.GetFleetBombardmentStrength(fleet)
                    > context.Assessment.GetBombardmentShieldStrength(hostilePlanet)
                && context.Assessment.HasBombardmentTargets(hostilePlanet)
            )
                return true;

            AIAttackDemand demand = context.GetAttackDemand(hostilePlanet);
            if (demand?.IsAssaultBlockedByShields != false)
                return false;
            bool canBombardDefenders =
                context.Assessment.GetDefendingRegimentCount(hostilePlanet) > 0
                && context.Assessment.GetFleetBombardmentStrength(fleet)
                    > context.Assessment.GetBombardmentShieldStrength(hostilePlanet);
            int requiredRegiments = canBombardDefenders
                ? demand.OccupationRegimentCount
                : demand.RegimentCount;
            int requiredStrength = canBombardDefenders ? 0 : demand.RegimentStrength;
            return context.Assessment.GetReadyFleetRegimentCount(fleet) >= requiredRegiments
                && context.Assessment.GetReadyFleetRegimentAttackStrength(fleet) >= requiredStrength
                && context.Assessment.GetPlanetaryAssaultSuccessPercent(fleet, hostilePlanet)
                    >= context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent;
        }
    }
}
