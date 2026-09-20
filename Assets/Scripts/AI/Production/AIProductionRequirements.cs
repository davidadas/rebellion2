using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;
using FacilityPortfolio = Rebellion.AI.Planners.AIInfrastructureRequirements.FacilityPortfolio;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds production demand from faction state and current force needs.
    /// </summary>
    public sealed class AIProductionRequirements
    {
        private static readonly AIEconomyRequirements _economyRequirements = new();
        private static readonly AIForceRequirements _forceRequirements = new();
        private static readonly AISpecialForcesRequirements _specialForcesRequirements = new();
        private readonly AIInfrastructureRequirements _infrastructureRequirements = new();

        /// <summary>
        /// Returns production demand for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Production demand generated for this faction.</returns>
        public List<AIProductionRequirement> Generate(AITurnContext context)
        {
            List<AIProductionRequirement> demands = new List<AIProductionRequirement>();

            if (context?.Game == null || context.Faction == null || context.Assessment == null)
                return demands;

            FacilityPortfolio facilityPortfolio = _infrastructureRequirements.BuildPortfolio(
                context
            );
            _economyRequirements.AddColonyRequirements(context, demands);
            _economyRequirements.AddResourceRequirements(context, demands);
            _infrastructureRequirements.AddPlanetaryDefenseRequirements(
                context,
                demands,
                facilityPortfolio
            );
            _infrastructureRequirements.AddPlanetaryStarfighterRequirements(context, demands);
            _forceRequirements.AddFleetSeedDemand(context, demands);
            _forceRequirements.AddColonizationFleetSeedDemand(context, demands);
            _forceRequirements.AddFleetReinforcementDemands(context, demands);
            _infrastructureRequirements.AddGarrisonRequirements(context, demands);
            _specialForcesRequirements.AddRequirements(context, demands);
            _infrastructureRequirements.AddProductionFacilityDemands(
                context,
                demands,
                new AIInfrastructurePlacementScorer(context),
                facilityPortfolio
            );
            _infrastructureRequirements.AddProductionFacilityUpgradeDemands(context, demands);
            _infrastructureRequirements.AddIdleShipyardRequirements(context, demands);

            return demands;
        }
    }
}
