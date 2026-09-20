using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.AI.Scoring;
using FacilityPortfolio = Rebellion.AI.Planners.AIInfrastructureRequirements.FacilityPortfolio;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds production requirements from faction state and current force needs.
    /// </summary>
    public sealed class AIProductionRequirements
    {
        private static readonly AIEconomyRequirements _economyRequirements = new();
        private static readonly AIForceRequirements _forceRequirements = new();
        private static readonly AISpecialForcesRequirements _specialForcesRequirements = new();
        private readonly AIInfrastructureRequirements _infrastructureRequirements = new();

        /// <summary>
        /// Builds production requirements for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Production requirements generated for this faction.</returns>
        public List<AIProductionRequirement> BuildRequirements(AITurnContext context)
        {
            List<AIProductionRequirement> requirements = new List<AIProductionRequirement>();

            if (context?.Game == null || context.Faction == null || context.Assessment == null)
                return requirements;

            FacilityPortfolio facilityPortfolio = _infrastructureRequirements.BuildPortfolio(
                context
            );
            _economyRequirements.AddColonyRequirements(context, requirements);
            _economyRequirements.AddResourceRequirements(context, requirements);
            _infrastructureRequirements.AddPlanetaryDefenseRequirements(
                context,
                requirements,
                facilityPortfolio
            );
            _infrastructureRequirements.AddPlanetaryStarfighterRequirements(context, requirements);
            _forceRequirements.AddFleetSeedRequirements(context, requirements);
            _forceRequirements.AddColonizationFleetSeedRequirements(context, requirements);
            _forceRequirements.AddFleetReinforcementRequirements(context, requirements);
            _infrastructureRequirements.AddGarrisonRequirements(context, requirements);
            _specialForcesRequirements.AddRequirements(context, requirements);
            _infrastructureRequirements.AddProductionFacilityRequirements(
                context,
                requirements,
                new AIInfrastructurePlacementScorer(context),
                facilityPortfolio
            );
            _infrastructureRequirements.AddProductionFacilityUpgradeRequirements(
                context,
                requirements
            );
            _infrastructureRequirements.AddIdleShipyardRequirements(context, requirements);

            return requirements;
        }
    }
}
