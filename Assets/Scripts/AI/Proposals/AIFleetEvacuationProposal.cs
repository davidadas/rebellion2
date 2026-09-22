using Rebellion.AI.Demands;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Returns a fleet that cannot act at its hostile location to friendly territory.
    /// </summary>
    public sealed class AIFleetEvacuationProposal : AIProposal
    {
        public Fleet Fleet { get; }

        public Planet HostilePlanet { get; }

        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        /// <summary>
        /// Creates a proposal to evacuate a fleet from its current hostile planet.
        /// </summary>
        /// <param name="fleet">Fleet to evacuate.</param>
        /// <param name="hostilePlanet">Hostile planet currently containing the fleet.</param>
        public AIFleetEvacuationProposal(Fleet fleet, Planet hostilePlanet)
        {
            Fleet = fleet;
            HostilePlanet = hostilePlanet;
        }

        /// <summary>
        /// Returns a stable sort key for this evacuation.
        /// </summary>
        /// <returns>Stable evacuation key.</returns>
        public override string GetSortKey()
        {
            return $"fleet-evacuation:{Fleet?.InstanceID}:{HostilePlanet?.InstanceID}";
        }

        /// <summary>
        /// Returns whether the fleet remains stationary at the recorded hostile planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when evacuation remains valid.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether the fleet remains stationary at the recorded hostile planet.
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
        /// Returns whether the proposal still matches live fleet and planet state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the fleet still requires evacuation.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            return context?.Faction != null
                && Fleet?.GetOwnerInstanceID() == context.Faction.InstanceID
                && HostilePlanet != null
                && Fleet.Movement == null
                && !Fleet.IsInCombat
                && Fleet.GetParentOfType<Planet>()?.InstanceID == HostilePlanet.InstanceID
                && !string.IsNullOrEmpty(HostilePlanet.GetOwnerInstanceID())
                && HostilePlanet.GetOwnerInstanceID() != context.Faction.InstanceID
                && !CanAct(context);
        }

        /// <summary>
        /// Returns whether the fleet has a viable immediate action at the hostile planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the fleet can win orbit, bombard, or assault.</returns>
        private bool CanAct(AITurnContext context)
        {
            if (context.Assessment.GetStrongestHostileFleetStrength(HostilePlanet) > 0)
            {
                int required = context.GetAttackDemand(HostilePlanet)?.OrbitalStrength ?? 0;
                return required > 0
                    && context.Assessment.GetProjectedFleetCombatValue(Fleet) >= required;
            }

            if (
                context.Assessment.GetFleetBombardmentStrength(Fleet)
                    > context.Assessment.GetBombardmentShieldResistance(HostilePlanet)
                && context.Assessment.HasBombardmentTargets(HostilePlanet)
            )
                return true;

            AIAttackDemand demand = context.GetAttackDemand(HostilePlanet);
            if (demand?.IsAssaultBlockedByShields != false)
                return false;
            bool canBombardDefenders =
                context.Assessment.GetDefendingRegimentCount(HostilePlanet) > 0
                && context.Assessment.GetFleetBombardmentStrength(Fleet)
                    > context.Assessment.GetBombardmentShieldResistance(HostilePlanet);
            int requiredRegiments = canBombardDefenders
                ? demand.OccupationRegimentCount
                : demand.RegimentCount;
            int requiredStrength = canBombardDefenders ? 0 : demand.RegimentStrength;
            return context.Assessment.GetReadyFleetRegimentCount(Fleet) >= requiredRegiments
                && context.Assessment.GetReadyFleetRegimentAttackStrength(Fleet) >= requiredStrength
                && context.Assessment.GetPlanetaryAssaultSuccessPercent(Fleet, HostilePlanet)
                    >= context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent;
        }
    }
}
