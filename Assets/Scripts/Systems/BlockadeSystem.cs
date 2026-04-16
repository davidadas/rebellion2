using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages blockade detection, transition events, and evacuation losses.
    /// </summary>
    public class BlockadeSystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly HashSet<string> _blockadedPlanets;

        /// <summary>
        /// Creates a new BlockadeSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for evacuation rolls.</param>
        public BlockadeSystem(GameRoot game, IRandomNumberProvider provider)
        {
            _game = game;
            _provider = provider;
            _blockadedPlanets = new HashSet<string>();
        }

        /// <summary>
        /// Detects blockade start/end transitions and emits results.
        /// </summary>
        /// <returns>Blockade transition results generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            HashSet<string> currentBlockades = DetectBlockadedPlanets();

            ApplyBlockadeStatus(currentBlockades, results);
            ClearBlockadeStatus(currentBlockades, results);

            _blockadedPlanets.Clear();
            _blockadedPlanets.UnionWith(currentBlockades);

            return results;
        }

        /// <summary>
        /// Rolls to determine if a regiment is destroyed while evacuating through a blockade.
        /// </summary>
        /// <returns>True if the regiment is destroyed.</returns>
        public bool RollEvacuationLoss()
        {
            int threshold = _game.Config.Blockade.EvacuationLossPercent;
            return _provider.NextInt(0, 100) < threshold;
        }

        /// <summary>
        /// Applies evacuation losses when a unit departs a blockaded planet.
        /// Only regiments are subject to losses — a friendly fleet at a planet
        /// breaks the blockade, so fleets never evacuate through one.
        /// </summary>
        /// <param name="unit">The unit attempting to leave.</param>
        /// <param name="originPlanet">The planet the unit is departing from.</param>
        /// <returns>Result describing the loss, or null if the unit survived.</returns>
        public EvacuationLossesResult ApplyEvacuationLosses(IMovable unit, Planet originPlanet)
        {
            if (!originPlanet.IsBlockaded())
                return null;

            if (unit is Regiment regiment && RollEvacuationLoss())
            {
                Faction faction = _game.Factions.FirstOrDefault(f =>
                    f.InstanceID == unit.GetOwnerInstanceID()
                );
                _game.DetachNode((ISceneNode)unit);
                GameLogger.Log(
                    $"{unit.GetDisplayName()} destroyed running blockade at {originPlanet.GetDisplayName()}"
                );
                return new EvacuationLossesResult
                {
                    Faction = faction,
                    Location = originPlanet,
                    LostRegiments = new List<Regiment> { regiment },
                    Tick = _game.CurrentTick,
                };
            }

            return null;
        }

        /// <summary>
        /// Scans all planets and returns the set currently under blockade.
        /// </summary>
        /// <returns>Instance IDs of all currently blockaded planets.</returns>
        private HashSet<string> DetectBlockadedPlanets()
        {
            HashSet<string> blockaded = new HashSet<string>();
            foreach (PlanetSystem system in _game.GetGalaxyMap().PlanetSystems)
            {
                foreach (Planet planet in system.Planets)
                {
                    if (planet.IsBlockaded())
                        blockaded.Add(planet.InstanceID);
                }
            }
            return blockaded;
        }

        /// <summary>
        /// Emits results for blockades that started since the last tick.
        /// </summary>
        /// <param name="currentBlockades">Planets blockaded this tick.</param>
        /// <param name="results">Results list to append transitions to.</param>
        private void ApplyBlockadeStatus(HashSet<string> currentBlockades, List<GameResult> results)
        {
            foreach (string planetId in currentBlockades)
            {
                if (_blockadedPlanets.Contains(planetId))
                    continue;

                Planet planet = _game.GetSceneNodeByInstanceID<Planet>(planetId);
                if (planet == null)
                    continue;

                results.Add(
                    new BlockadeChangedResult
                    {
                        Planet = planet,
                        BlockadingFleet = planet.Fleets.FirstOrDefault(f =>
                            f.OwnerInstanceID != planet.OwnerInstanceID
                        ),
                        Blockaded = true,
                        Tick = _game.CurrentTick,
                    }
                );
            }
        }

        /// <summary>
        /// Emits results for blockades that ended since the last tick.
        /// </summary>
        /// <param name="currentBlockades">Planets blockaded this tick.</param>
        /// <param name="results">Results list to append transitions to.</param>
        private void ClearBlockadeStatus(HashSet<string> currentBlockades, List<GameResult> results)
        {
            foreach (string planetId in _blockadedPlanets)
            {
                if (currentBlockades.Contains(planetId))
                    continue;

                Planet planet = _game.GetSceneNodeByInstanceID<Planet>(planetId);
                if (planet == null)
                    continue;

                results.Add(
                    new BlockadeChangedResult
                    {
                        Planet = planet,
                        BlockadingFleet = null,
                        Blockaded = false,
                        Tick = _game.CurrentTick,
                    }
                );
            }
        }
    }
}
