using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Periodically decays per-planet energy and raw materials, suspends facilities,
    /// and applies a random resource walk. Faithfully reproduces the original game's
    /// FUN_00510b20_rebalance_system_resources_and_update_facilities pipeline.
    ///
    /// Two independent timers per faction:
    /// 1. Rebalance timer (PARAM_7717/7718): picks a random planet, applies probabilistic
    ///    decay to energy + raw materials, clamps raw_materials ≤ energy, rolls 10%
    ///    suspension chance on each active facility.
    /// 2. Resource walk timer (PARAM_7719/7720): picks a random planet, randomly adjusts
    ///    energy or raw materials by ±1 using the RESRC_TABLE distribution.
    /// </summary>
    public class ResourceRebalanceSystem
    {
        private readonly GameRoot _game;
        private readonly Dictionary<string, int> _nextRebalanceTick = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _nextResourceWalkTick =
            new Dictionary<string, int>();

        public ResourceRebalanceSystem(GameRoot game, IRandomNumberProvider provider)
        {
            _game = game;

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
        public void ProcessTick(IRandomNumberProvider provider)
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
                    RebalanceRandomPlanet(faction, config, provider);
                    _nextRebalanceTick[fid] =
                        _game.CurrentTick
                        + config.RebalanceTimerBase
                        + provider.NextInt(0, config.RebalanceTimerSpread);
                }

                // Resource walk timer
                if (
                    _nextResourceWalkTick.TryGetValue(fid, out int walkTick)
                    && _game.CurrentTick >= walkTick
                )
                {
                    ResourceWalkRandomPlanet(faction, config, provider);
                    _nextResourceWalkTick[fid] =
                        _game.CurrentTick
                        + config.ResourceWalkTimerBase
                        + provider.NextInt(0, config.ResourceWalkTimerSpread);
                }
            }
        }

        /// <summary>
        /// Picks one random owned planet and applies resource decay + facility suspension.
        /// Reproduces FUN_00510b20.
        /// </summary>
        private void RebalanceRandomPlanet(
            Faction faction,
            GameConfig.ResourceRebalanceConfig config,
            IRandomNumberProvider provider
        )
        {
            List<Planet> planets = faction
                .GetOwnedUnitsByType<Planet>()
                .Where(p => p.NumRawResourceNodes > 0 || p.EnergyCapacity > 0)
                .ToList();

            if (planets.Count == 0)
                return;

            Planet target = planets[provider.NextInt(0, planets.Count)];

            ApplyResourceDecay(target, config, provider);
        }

        /// <summary>
        /// Probabilistic decay of raw materials and energy.
        /// Reproduces FUN_00558590_apply_resource_rebalance_losses exactly.
        ///
        /// For each unit of each resource, probability of loss =
        /// (remaining_combined * decayMultiplier) out of 100.
        /// At least 1 unit is always lost. After decay, raw materials
        /// are clamped to not exceed energy.
        /// </summary>
        private void ApplyResourceDecay(
            Planet planet,
            GameConfig.ResourceRebalanceConfig config,
            IRandomNumberProvider provider
        )
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
                    if (provider.NextInt(0, 100) < probability)
                        rawLosses++;
                }

                // Energy decay roll
                if (i < energy)
                {
                    int remaining = (rawMaterials - rawLosses - energyLosses) + energy;
                    int probability = remaining * config.DecayMultiplier;
                    if (provider.NextInt(0, 100) < probability)
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
        /// Reproduces the RESRC_TABLE random walk:
        ///   0-24: decrement raw materials (if > 0 and mines exist)
        ///   25-49: decrement energy (if > 0 and facilities exist)
        ///   50-74: increment raw materials (if below max and below energy)
        ///   75-99: increment energy (if below max)
        /// </summary>
        private void ResourceWalkRandomPlanet(
            Faction faction,
            GameConfig.ResourceRebalanceConfig config,
            IRandomNumberProvider provider
        )
        {
            List<Planet> planets = faction.GetOwnedUnitsByType<Planet>();
            if (planets.Count == 0)
                return;

            Planet target = planets[provider.NextInt(0, planets.Count)];
            int roll = provider.NextInt(0, 100);

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
