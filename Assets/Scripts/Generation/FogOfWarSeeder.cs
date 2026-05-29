using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Systems;

namespace Rebellion.Generation
{
    /// <summary>
    /// Seeds initial fog-of-war visibility snapshots for every faction. Each faction
    /// receives a starting snapshot of any planet sitting in a core system it does not
    /// own, plus any planet explicitly granted to it by a
    /// <see cref="StartingPlanet.VisibleToFactionIDs"/> override.
    /// </summary>
    public sealed class FogOfWarSeeder : IGameSeeder
    {
        /// <summary>
        /// Seeds initial fog-of-war visibility against the assembled game in the
        /// generation context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        public void Seed(GenerationContext ctx)
        {
            SeedVisibility(ctx.Game, ctx.Config);
        }

        /// <summary>
        /// Seeds initial fog-of-war visibility against an assembled game.
        /// </summary>
        /// <param name="game">The game whose visibility is seeded.</param>
        /// <param name="config">The generation config (read for visibility overrides).</param>
        private void SeedVisibility(GameRoot game, GameGenerationConfig config)
        {
            FogOfWarSystem fog = new FogOfWarSystem(game);
            HashSet<(string PlanetID, string ViewerFactionID)> visibilityOverrides =
                CollectStartingVisibilityOverrides(config);

            foreach (PlanetSystem system in game.Galaxy.PlanetSystems)
            {
                foreach (Faction faction in game.Factions)
                {
                    foreach (Planet planet in system.Planets)
                    {
                        if (IsForeignCorePlanet(system, planet, faction))
                        {
                            fog.CaptureSnapshot(faction, planet, system, currentTick: 0);
                        }

                        if (visibilityOverrides.Contains((planet.InstanceID, faction.InstanceID)))
                        {
                            fog.CaptureSnapshot(faction, planet, system, currentTick: 0);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns true when the planet sits in a core system and is owned by some
        /// faction other than the viewer.
        /// </summary>
        /// <param name="system">The system containing the planet.</param>
        /// <param name="planet">The candidate planet.</param>
        /// <param name="viewer">The faction whose visibility is being decided.</param>
        /// <returns>True when the viewer should see the planet as a foreign core planet.</returns>
        private bool IsForeignCorePlanet(PlanetSystem system, Planet planet, Faction viewer)
        {
            return system.SystemType == PlanetSystemType.CoreSystem
                && planet.OwnerInstanceID != viewer.InstanceID;
        }

        /// <summary>
        /// Walks the configured faction setups and collects every (planet, viewer-faction)
        /// pair that should grant starting visibility regardless of system type.
        /// </summary>
        /// <param name="config">The generation config.</param>
        /// <returns>The set of (planet, viewer) overrides used to seed fog of war.</returns>
        private HashSet<(
            string PlanetID,
            string ViewerFactionID
        )> CollectStartingVisibilityOverrides(GameGenerationConfig config)
        {
            HashSet<(string, string)> overrides = new HashSet<(string, string)>();
            foreach (FactionSetup factionSetup in config.GalaxyClassification.FactionSetups)
            {
                if (factionSetup.StartingPlanets == null)
                {
                    continue;
                }

                foreach (StartingPlanet startingPlanet in factionSetup.StartingPlanets)
                {
                    if (
                        string.IsNullOrEmpty(startingPlanet.PlanetInstanceID)
                        || startingPlanet.VisibleToFactionIDs == null
                    )
                    {
                        continue;
                    }

                    foreach (string viewerFactionId in startingPlanet.VisibleToFactionIDs)
                    {
                        overrides.Add((startingPlanet.PlanetInstanceID, viewerFactionId));
                    }
                }
            }
            return overrides;
        }
    }
}
