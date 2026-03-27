using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Util.Extensions;

namespace Rebellion.Generation
{
    /// <summary>
    /// Responsible for generating and deploying factions for the scene graph.
    /// </summary>
    public class FactionGenerator : UnitGenerator<Faction>
    {
        /// <summary>
        /// Constructs a FactionGenerator object.
        /// </summary>
        /// <param name="summary">The GameSummary options selected by the player.</param>
        /// <param name="resourceManager">The resource manager from which to load game data.</param>
        /// <param name="randomProvider">Random number provider for deterministic generation.</param>
        public FactionGenerator(
            GameSummary summary,
            IResourceManager resourceManager,
            IRandomNumberProvider randomProvider
        )
            : base(summary, resourceManager, randomProvider) { }

        /// <summary>
        /// Assigns each faction's headquarters to their designated planet.
        /// </summary>
        /// <param name="factions">The factions to assign.</param>
        /// <param name="planetSystems">The planet systems containing the faction's designated HQ.</param>
        private void SetFactionHQs(Faction[] factions, PlanetSystem[] planetSystems)
        {
            Dictionary<string, Faction> hqs = factions.ToDictionary(f => f.HQInstanceID);
            HashSet<string> filledHQs = new HashSet<string>();

            if (planetSystems.Length == 0)
            {
                throw new GameException(
                    "Cannot assign faction headquarters. No planet systems available."
                );
            }

            foreach (PlanetSystem planetSystem in planetSystems)
            {
                foreach (Planet planet in planetSystem.Planets)
                {
                    if (hqs.TryGetValue(planet.InstanceID, out Faction faction))
                    {
                        planet.IsHeadquarters = true;
                        planet.OwnerInstanceID = faction.InstanceID;
                        planet.SetPopularSupport(faction.InstanceID, 100, 100);
                        planet.AddVisitor(faction.InstanceID);
                        filledHQs.Add(faction.InstanceID);

                        if (filledHQs.Count == factions.Length)
                        {
                            return;
                        }
                    }
                }
            }

            throw new GameException("Invalid planet designated as headquarters.");
        }

        /// <summary>
        /// Assigns starting planets to each faction.
        /// </summary>
        /// <param name="factions">The factions to assign planets to.</param>
        /// <param name="planetSystems">The planet systems to choose from.</param>
        private void SetStartingPlanets(Faction[] factions, PlanetSystem[] planetSystems)
        {
            GameGenerationRules rules = GetRules();
            PlanetSizeProfile startProfile = rules.Planets.NumInitialPlanets;

            int numStartingPlanets = 0;

            if (GetGameSummary().GalaxySize == GameSize.Small)
            {
                numStartingPlanets = startProfile.Small;
            }
            else if (GetGameSummary().GalaxySize == GameSize.Medium)
            {
                numStartingPlanets = startProfile.Medium;
            }
            else
            {
                numStartingPlanets = startProfile.Large;
            }

            // Select and shuffle starting planets.
            Queue<Planet> startingPlanets = new Queue<Planet>(
                planetSystems
                    .SelectMany(ps => ps.Planets)
                    .Where(planet => planet.IsColonized && !planet.IsHeadquarters)
                    .Shuffle()
                    .Take(numStartingPlanets * factions.Length)
            );

            // Assign planets to factions.
            foreach (Faction faction in factions)
            {
                for (int i = 0; i < numStartingPlanets; i++)
                {
                    if (startingPlanets.TryDequeue(out Planet planet))
                    {
                        planet.OwnerInstanceID = faction.InstanceID;
                        planet.SetPopularSupport(faction.InstanceID, 100, 100);
                    }
                    else
                    {
                        throw new GameException("Not enough planets to assign to factions.");
                    }
                }
            }
        }

        /// <summary>
        /// Selects the units for each faction. As all factions are deployed
        /// at the start of the game, this method does nothing.
        /// </summary>
        /// <param name="factions">The factions to select.</param>
        /// <returns>The unchanged factions array.</returns>
        public override Faction[] SelectUnits(Faction[] factions)
        {
            // No selection necessary, return as-is.
            return factions;
        }

        /// <summary>
        /// Decorates the units of each faction with additional properties.
        /// </summary>
        /// <param name="factions">The factions to decorate.</param>
        /// <returns>The decorated factions.</returns>
        public override Faction[] DecorateUnits(Faction[] factions)
        {
            int researchLevel = GetGameSummary().StartingResearchLevel;

            // Set the research level for each faction and manufacturing type.
            foreach (Faction faction in factions)
            {
                faction.SetResearchLevel(ManufacturingType.Building, researchLevel);
                faction.SetResearchLevel(ManufacturingType.Ship, researchLevel);
                faction.SetResearchLevel(ManufacturingType.Troop, researchLevel);
            }

            return factions;
        }

        /// <summary>
        /// Assigns initial planets to each faction. This method does NOT assign units.
        /// </summary>
        /// <param name="factions">The factions to deploy.</param>
        /// <param name="destinations">The planet systems to deploy to.</param>
        /// <returns>The deployed factions.</returns>
        public override Faction[] DeployUnits(Faction[] factions, PlanetSystem[] destinations)
        {
            SetFactionHQs(factions, destinations);
            SetStartingPlanets(factions, destinations);

            return factions;
        }
    }
}
