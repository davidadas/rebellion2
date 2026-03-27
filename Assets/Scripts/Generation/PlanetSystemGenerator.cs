using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Generation
{
    /// <summary>
    /// Responsible for generating and configuring planet systems.
    /// Applies galaxy size filtering, resource allocation, and colonization
    /// rules based on GameSummary and strongly-typed GameGenerationRules.
    /// </summary>
    public class PlanetSystemGenerator : UnitGenerator<PlanetSystem>
    {
        /// <summary>
        /// Constructs a PlanetSystemGenerator.
        /// </summary>
        /// <param name="summary">The GameSummary options selected by the player.</param>
        /// <param name="resourceManager">The resource manager used to load game data and configuration.</param>
        /// <param name="randomProvider">Random number provider for deterministic generation.</param>
        public PlanetSystemGenerator(
            GameSummary summary,
            IResourceManager resourceManager,
            IRandomNumberProvider randomProvider
        )
            : base(summary, resourceManager, randomProvider) { }

        /// <summary>
        /// Assigns resource slots and raw resource nodes to a planet
        /// using the selected GameResourceAvailability setting.
        /// </summary>
        /// <param name="parentSystem">The parent planet system.</param>
        /// <param name="planet">The planet being configured.</param>
        private void SetResources(PlanetSystem parentSystem, Planet planet)
        {
            GameGenerationRules rules = GetRules();

            ResourceAvailabilityProfile availabilityProfile = rules.Planets.ResourceAvailability;

            ResourceSystemProfile selectedProfile = GetGameSummary().ResourceAvailability switch
            {
                GameResourceAvailability.Limited => availabilityProfile.Limited,
                GameResourceAvailability.Normal => availabilityProfile.Normal,
                GameResourceAvailability.Abundant => availabilityProfile.Abundant,
                _ => throw new System.Exception("Invalid resource availability."),
            };

            ResourceRanges ranges =
                parentSystem.SystemType == PlanetSystemType.CoreSystem
                    ? selectedProfile.CoreSystem
                    : selectedProfile.OuterRim;

            planet.OrbitSlots = Random.Range(ranges.OrbitSlotRange.Min, ranges.OrbitSlotRange.Max);

            planet.GroundSlots = Random.Range(
                ranges.GroundSlotRange.Min,
                ranges.GroundSlotRange.Max
            );

            planet.NumRawResourceNodes = Random.Range(
                ranges.ResourceRange.Min,
                ranges.ResourceRange.Max
            );
        }

        /// <summary>
        /// Determines the initial colonization state of a planet.
        /// Core systems are always colonized.
        /// Outer systems use the configured initial colonization probability,
        /// unless overridden directly by planet data.
        /// </summary>
        /// <param name="parentSystem">The parent planet system.</param>
        /// <param name="planet">The planet being evaluated.</param>
        private void SetColonizationStatus(PlanetSystem parentSystem, Planet planet)
        {
            GameGenerationRules rules = GetRules();
            double colonizationRate = rules.Planets.InitialColonizationRate;

            if (parentSystem.SystemType == PlanetSystemType.CoreSystem)
            {
                planet.IsColonized = true;
                return;
            }

            if (!planet.IsColonized)
            {
                if (Random.value < colonizationRate)
                {
                    planet.IsColonized = true;
                }
            }
        }

        /// <summary>
        /// Initializes equal popular support for all player factions.
        /// Ensures total support equals 100.
        /// </summary>
        private void SetPopularSupport(Planet planet)
        {
            string[] factionIds = GetGameSummary().StartingFactionIDs;

            if (factionIds == null || factionIds.Length == 0)
                return;

            planet.PopularSupport.Clear();

            int factionCount = factionIds.Length;

            int baseShare = 100 / factionCount;
            int remainder = 100 % factionCount;

            for (int i = 0; i < factionCount; i++)
            {
                int value = baseShare;

                // Deterministically distribute remainder to first factions
                if (i < remainder)
                    value += 1;

                planet.PopularSupport[factionIds[i]] = value;
            }
        }

        /// <summary>
        /// Sets the initial visitor status for a planet.
        /// Core systems have all starting factions as visitors.
        /// </summary>
        /// <param name="system">The planet system containing the planet.</param>
        /// <param name="planet">The planet to set visitor status for.</param>
        private void SetVisitorStatus(PlanetSystem system, Planet planet)
        {
            if (system.GetSystemType() == PlanetSystemType.CoreSystem)
            {
                string[] factionIds = GetGameSummary().StartingFactionIDs;
                foreach (string factionId in factionIds)
                {
                    planet.AddVisitor(factionId);
                }
            }
        }

        /// <summary>
        /// Filters planet systems based on the selected galaxy size.
        /// Systems whose visibility exceeds the selected size are excluded.
        /// </summary>
        /// <param name="units">All available planet systems.</param>
        /// <returns>The filtered set of planet systems included in the game.</returns>
        public override PlanetSystem[] SelectUnits(PlanetSystem[] units)
        {
            int galaxySize = (int)GetGameSummary().GalaxySize;
            List<PlanetSystem> galaxyMap = new List<PlanetSystem>();

            foreach (PlanetSystem system in units)
            {
                if ((int)system.Visibility <= galaxySize)
                {
                    galaxyMap.Add(system);
                }
            }

            return galaxyMap.ToArray();
        }

        /// <summary>
        /// Applies runtime configuration to selected planet systems,
        /// including resource slot generation and colonization state.
        /// </summary>
        /// <param name="units">The selected planet systems.</param>
        /// <returns>The decorated planet systems.</returns>
        public override PlanetSystem[] DecorateUnits(PlanetSystem[] units)
        {
            foreach (PlanetSystem system in units)
            {
                foreach (Planet planet in system.Planets)
                {
                    SetResources(system, planet);
                    SetColonizationStatus(system, planet);
                    SetPopularSupport(planet);
                    SetVisitorStatus(system, planet);
                }
            }

            return units;
        }

        /// <summary>
        /// Planet systems do not require deployment into other nodes.
        /// They already exist at the galaxy root.
        /// </summary>
        /// <param name="units">The configured planet systems.</param>
        /// <param name="destinations">Unused.</param>
        /// <returns>The unchanged planet systems.</returns>
        public override PlanetSystem[] DeployUnits(
            PlanetSystem[] units,
            PlanetSystem[] destinations = default
        )
        {
            return units;
        }
    }
}
