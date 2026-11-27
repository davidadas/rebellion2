using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Generation
{
    /// <summary>
    /// Seeds starting intelligence using the selected difficulty's exploration and category rules.
    /// Explicit <see cref="StartingPlanet.VisibleToFactionIDs"/> grants take precedence.
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
            SeedVisibility(
                ctx.Game,
                ctx.Config,
                ctx.Config.GetStartingIntelligence(ctx.Summary.Difficulty)
            );
        }

        /// <summary>
        /// Seeds initial fog-of-war visibility against an assembled game.
        /// </summary>
        /// <param name="game">The game whose visibility is seeded.</param>
        /// <param name="config">The generation config (read for visibility overrides).</param>
        /// <param name="profile">The selected difficulty's starting visibility rules.</param>
        private void SeedVisibility(
            GameRoot game,
            GameGenerationConfig config,
            StartingIntelligenceProfile profile
        )
        {
            FogOfWarRecorder fog = new FogOfWarRecorder();
            HashSet<(string PlanetID, string ViewerFactionID)> visibilityOverrides =
                CollectStartingVisibilityOverrides(game, config);

            foreach (PlanetSector sector in game.Galaxy.GetChildren<PlanetSector>())
            {
                foreach (Faction faction in game.GetFactions())
                {
                    foreach (Planet planet in sector.GetChildren<Planet>())
                    {
                        if (visibilityOverrides.Contains((planet.InstanceID, faction.InstanceID)))
                        {
                            fog.RecordPlanetSnapshot(faction, planet, sector, currentTick: 0);
                            continue;
                        }

                        if (
                            profile.StartWithUnexploredPlanets
                            || !IsForeignCorePlanet(sector, planet, faction)
                        )
                            continue;

                        if (profile.RevealAgents && profile.RevealFleets && profile.RevealBuildings)
                        {
                            fog.RecordPlanetSnapshot(faction, planet, sector, currentTick: 0);
                        }
                        else
                        {
                            fog.RecordSelectedObservations(
                                game,
                                faction,
                                SelectStartingObservations(planet, faction, profile),
                                currentTick: 0
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Selects individual starting observations while keeping unrevealed fleets and cargo hidden.
        /// </summary>
        /// <param name="planet">The known foreign planet.</param>
        /// <param name="viewer">The faction receiving intelligence.</param>
        /// <param name="profile">The selected visibility rules.</param>
        /// <returns>The planet and the units revealed at game start.</returns>
        private static IEnumerable<ISceneNode> SelectStartingObservations(
            Planet planet,
            Faction viewer,
            StartingIntelligenceProfile profile
        )
        {
            yield return planet;
            foreach (ISceneNode child in planet.GetChildren(includeDisabled: true))
            {
                if (!CanObserve(child, viewer))
                    continue;
                if (child is Fleet fleet)
                {
                    if (fleet.GetChildren<CapitalShip>().Count == 0 || !profile.RevealFleets)
                        continue;
                    foreach (CapitalShip ship in fleet.GetChildren<CapitalShip>())
                    {
                        if (!FogOfWarRecorder.IsObservableAtPlanet(ship, viewer.InstanceID))
                            continue;
                        yield return ship;
                        foreach (ISceneNode cargo in ship.GetChildren())
                            if (CanObserve(cargo, viewer) && ShouldReveal(cargo, viewer, profile))
                                yield return cargo;
                    }
                }
                else if (ShouldReveal(child, viewer, profile))
                {
                    yield return child;
                }
            }
        }

        /// <summary>
        /// Uses normal stationary observation rules without recording a faction's own live units.
        /// </summary>
        /// <param name="unit">The candidate observation.</param>
        /// <param name="viewer">The observing faction.</param>
        /// <returns>Whether the unit is eligible for starting intelligence.</returns>
        private static bool CanObserve(ISceneNode unit, Faction viewer) =>
            unit is Officer or SpecialForces or Fleet or Building or Regiment or Starfighter
            && unit.IsEnabled
            && (
                unit.GetOwnerInstanceID() != viewer.InstanceID
                || unit is Officer { IsCaptured: true }
            )
            && FogOfWarRecorder.IsObservableAtPlanet(unit, viewer.InstanceID);

        /// <summary>
        /// Applies category settings to foreign units while retaining observable friendly personnel.
        /// </summary>
        /// <param name="unit">The candidate observation.</param>
        /// <param name="viewer">The faction receiving intelligence.</param>
        /// <param name="profile">The selected visibility rules.</param>
        /// <returns>Whether this unit should be recorded.</returns>
        private static bool ShouldReveal(
            ISceneNode unit,
            Faction viewer,
            StartingIntelligenceProfile profile
        ) =>
            unit.GetOwnerInstanceID() == viewer.InstanceID
            || unit switch
            {
                Officer or SpecialForces => profile.RevealAgents,
                Building => profile.RevealBuildings,
                _ => true,
            };

        /// <summary>
        /// Returns true when the planet sits in a core sector and is owned by some
        /// faction other than the viewer.
        /// </summary>
        /// <param name="sector">The sector containing the planet.</param>
        /// <param name="planet">The candidate planet.</param>
        /// <param name="viewer">The faction whose visibility is being decided.</param>
        /// <returns>True when the viewer should see the planet as a foreign core planet.</returns>
        private bool IsForeignCorePlanet(PlanetSector sector, Planet planet, Faction viewer)
        {
            return sector.SectorType == PlanetSectorType.Core
                && planet.OwnerInstanceID != viewer.InstanceID;
        }

        /// <summary>
        /// Walks the configured faction setups and collects every (planet, viewer-faction)
        /// pair that should grant starting visibility regardless of sector type.
        /// </summary>
        /// <param name="game">The game containing the live planet instances.</param>
        /// <param name="config">The generation config.</param>
        /// <returns>The set of (planet, viewer) overrides used to seed fog of war.</returns>
        private HashSet<(
            string PlanetID,
            string ViewerFactionID
        )> CollectStartingVisibilityOverrides(GameRoot game, GameGenerationConfig config)
        {
            HashSet<(string, string)> overrides = new HashSet<(string, string)>();
            Dictionary<string, Planet> planetsByTypeId = game
                .Galaxy.GetChildren<PlanetSector>()
                .SelectMany(sector => sector.GetChildren<Planet>())
                .Where(planet => !string.IsNullOrEmpty(planet.TypeID))
                .ToDictionary(planet => planet.TypeID);

            foreach (FactionSetup factionSetup in config.GalaxyClassification.FactionSetups)
            {
                if (factionSetup.StartingPlanets == null)
                {
                    continue;
                }

                foreach (StartingPlanet startingPlanet in factionSetup.StartingPlanets)
                {
                    if (
                        string.IsNullOrEmpty(startingPlanet.PlanetTypeID)
                        || startingPlanet.VisibleToFactionIDs == null
                    )
                    {
                        continue;
                    }

                    if (
                        !planetsByTypeId.TryGetValue(
                            startingPlanet.PlanetTypeID,
                            out Planet visiblePlanet
                        )
                    )
                    {
                        continue;
                    }

                    foreach (string viewerFactionId in startingPlanet.VisibleToFactionIDs)
                    {
                        overrides.Add((visiblePlanet.InstanceID, viewerFactionId));
                    }
                }
            }
            return overrides;
        }
    }
}
