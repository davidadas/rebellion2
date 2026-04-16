using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Periodically decays per-planet energy and raw materials, suspends facilities,
    /// and applies a random resource walk.
    ///
    /// Two independent timers per faction:
    /// 1. Rebalance timer: picks a random planet, applies probabilistic decay to
    ///    energy and raw materials, and rolls suspension chance on active facilities.
    /// 2. Resource walk timer: picks a random planet and randomly adjusts
    ///    energy or raw materials by ±1.
    /// </summary>
    public class ResourceRebalanceSystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly Dictionary<string, int> _nextRebalanceTick = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _nextResourceWalkTick =
            new Dictionary<string, int>();

        /// <summary>
        /// Creates a new ResourceRebalanceSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for timer offsets and resource rolls.</param>
        public ResourceRebalanceSystem(GameRoot game, IRandomNumberProvider provider)
        {
            _game = game;
            _provider = provider;

            // Arm initial timers for each faction
            GameConfig.ResourceRebalanceConfig config = _game.GetConfig().ResourceRebalance;
            foreach (Faction faction in _game.GetFactions())
            {
                _nextRebalanceTick[faction.InstanceID] =
                    _game.CurrentTick
                    + config.RebalanceTimerBase
                    + provider.NextInt(0, config.RebalanceTimerSpread);
                _nextResourceWalkTick[faction.InstanceID] =
                    _game.CurrentTick
                    + config.ResourceWalkTimerBase
                    + provider.NextInt(0, config.ResourceWalkTimerSpread);
            }
        }

        /// <summary>
        /// Checks each faction's timers and fires rebalance/walk when due.
        /// </summary>
        public List<GameResult> ProcessTick()
        {
            GameConfig.ResourceRebalanceConfig config = _game.GetConfig().ResourceRebalance;

            foreach (Faction faction in _game.GetFactions())
            {
                string fid = faction.InstanceID;

                // Rebalance timer
                if (
                    _nextRebalanceTick.TryGetValue(fid, out int rebalTick)
                    && _game.CurrentTick >= rebalTick
                )
                {
                    RebalanceRandomPlanet(faction, config);
                    _nextRebalanceTick[fid] =
                        _game.CurrentTick
                        + config.RebalanceTimerBase
                        + _provider.NextInt(0, config.RebalanceTimerSpread);
                }

                // Resource walk timer
                if (
                    _nextResourceWalkTick.TryGetValue(fid, out int walkTick)
                    && _game.CurrentTick >= walkTick
                )
                {
                    ResourceWalkRandomPlanet(faction, config);
                    _nextResourceWalkTick[fid] =
                        _game.CurrentTick
                        + config.ResourceWalkTimerBase
                        + _provider.NextInt(0, config.ResourceWalkTimerSpread);
                }
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Picks one random owned planet and applies resource decay + facility suspension.
        /// </summary>
        private void RebalanceRandomPlanet(
            Faction faction,
            GameConfig.ResourceRebalanceConfig config
        )
        {
            List<Planet> planets = faction
                .GetOwnedUnitsByType<Planet>()
                .Where(p => p.NumRawResourceNodes > 0 || p.EnergyCapacity > 0)
                .ToList();

            if (planets.Count == 0)
                return;

            Planet target = planets[_provider.NextInt(0, planets.Count)];

            ApplyResourceDecay(target, config);
        }

        /// <summary>
        /// Probabilistic decay of raw materials and energy. At least 1 unit is always
        /// lost. After decay, raw materials are clamped to not exceed energy.
        /// </summary>
        private void ApplyResourceDecay(Planet planet, GameConfig.ResourceRebalanceConfig config)
        {
            int rawMaterials = planet.NumRawResourceNodes;
            int energy = planet.EnergyCapacity;
            int maxChannel = Math.Max(rawMaterials, energy);

            int rawLosses = 0;
            int energyLosses = 0;

            for (int i = 0; i < maxChannel; i++)
            {
                // Raw materials decay roll
                if (i < rawMaterials)
                {
                    int remaining = (energy - rawLosses - energyLosses) + rawMaterials;
                    int probability = remaining * config.DecayMultiplier;
                    if (_provider.NextInt(0, 100) < probability)
                        rawLosses++;
                }

                // Energy decay roll
                if (i < energy)
                {
                    int remaining = (rawMaterials - rawLosses - energyLosses) + energy;
                    int probability = remaining * config.DecayMultiplier;
                    if (_provider.NextInt(0, 100) < probability)
                        energyLosses++;
                }
            }

            // Guarantee at least 1 loss
            if (rawLosses == 0 && energyLosses == 0)
            {
                if (rawMaterials > 0)
                    rawLosses = 1;
                else if (energy > 0)
                    energyLosses = 1;
            }

            planet.NumRawResourceNodes -= rawLosses;
            planet.EnergyCapacity -= energyLosses;

            // Clamp: raw materials cannot exceed energy
            if (planet.EnergyCapacity < planet.NumRawResourceNodes)
                planet.NumRawResourceNodes = planet.EnergyCapacity;

            // Floor at 0
            planet.NumRawResourceNodes = Math.Max(0, planet.NumRawResourceNodes);
            planet.EnergyCapacity = Math.Max(0, planet.EnergyCapacity);

            if (rawLosses > 0 || energyLosses > 0)
            {
                GameLogger.Debug(
                    $"Resource decay at {planet.GetDisplayName()}: "
                        + $"raw -{rawLosses} (now {planet.NumRawResourceNodes}), "
                        + $"energy -{energyLosses} (now {planet.EnergyCapacity})"
                );
            }
        }

        /// <summary>
        /// Picks one random planet and applies a ±1 adjustment to energy or raw materials.
        /// Each quartile of a 0-99 roll maps to: decrement raw materials, decrement energy,
        /// increment raw materials, or increment energy (with capacity/clamp guards).
        /// </summary>
        private void ResourceWalkRandomPlanet(
            Faction faction,
            GameConfig.ResourceRebalanceConfig config
        )
        {
            List<Planet> planets = faction.GetOwnedUnitsByType<Planet>();
            if (planets.Count == 0)
                return;

            Planet target = planets[_provider.NextInt(0, planets.Count)];
            int roll = _provider.NextInt(0, 100);

            if (roll < 25)
            {
                // Decrement raw materials (if mines exist and above 0)
                if (
                    target.NumRawResourceNodes > 0
                    && target.GetBuildingTypeCount(BuildingType.Mine) > 0
                )
                {
                    target.NumRawResourceNodes--;
                }
            }
            else if (roll < 50)
            {
                // Decrement energy (if facilities exist and above 0)
                if (target.EnergyCapacity > 0 && target.GetAllBuildings().Count > 0)
                {
                    target.EnergyCapacity--;
                    // Maintain clamp: raw materials cannot exceed energy
                    if (target.NumRawResourceNodes > target.EnergyCapacity)
                        target.NumRawResourceNodes = target.EnergyCapacity;
                }
            }
            else if (roll < 75)
            {
                // Increment raw materials (if below max and below energy)
                if (
                    target.NumRawResourceNodes < config.MaxRawMaterials
                    && target.NumRawResourceNodes < target.EnergyCapacity
                )
                {
                    target.NumRawResourceNodes++;
                }
            }
            else
            {
                // Increment energy (if below max)
                if (target.EnergyCapacity < config.MaxEnergy)
                {
                    target.EnergyCapacity++;
                }
            }
        }
    }
}
