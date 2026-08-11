using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    public enum PlanetResource
    {
        RawMaterials,
        Energy,
    }

    /// <summary>
    /// Applies one explicit signed resource adjustment to the scoped planet.
    /// </summary>
    [PersistableObject(Name = "AdjustPlanetResource")]
    public sealed class AdjustPlanetResourceAction : GameAction
    {
        [PersistableAttribute]
        public PlanetResource Resource { get; set; }

        [PersistableAttribute]
        public int Amount { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            throw new InvalidOperationException("AdjustPlanetResource requires a planet target.");
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            Planet planet = context?.GetScopeTarget<Planet>();
            if (planet == null)
                return Execute(game);

            int oldValue;
            int newValue;
            PlanetStatType stat;
            switch (Resource)
            {
                case PlanetResource.RawMaterials:
                    stat = PlanetStatType.RawMaterial;
                    oldValue = planet.NumRawResourceNodes;
                    newValue = Math.Max(0, checked(oldValue + Amount));
                    planet.NumRawResourceNodes = newValue;
                    break;
                case PlanetResource.Energy:
                    stat = PlanetStatType.Energy;
                    oldValue = planet.EnergyCapacity;
                    newValue = Math.Max(0, checked(oldValue + Amount));
                    planet.EnergyCapacity = newValue;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported planet resource '{Resource}'."
                    );
            }

            Faction faction = FindOwner(game, planet);
            return new List<GameResult>
            {
                new PlanetStatChangedResult
                {
                    Planet = planet,
                    Faction = faction,
                    Stat = stat,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Tick = game.CurrentTick,
                },
                new PlanetIncidentResult
                {
                    Planet = planet,
                    IncidentType = IncidentType.Resource,
                    ChangedStat = stat,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Severity = Math.Abs(newValue - oldValue),
                    Tick = game.CurrentTick,
                },
            };
        }

        /// <summary>
        /// Resolves the faction that currently owns the planet.
        /// </summary>
        private static Faction FindOwner(GameRoot game, Planet planet) =>
            game.GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == planet.OwnerInstanceID);
    }

    /// <summary>
    /// Removes a probability-driven number of resource nodes from the selected planet.
    /// </summary>
    [PersistableObject(Name = "ReduceResources")]
    public sealed class ReduceResourcesAction : GameAction
    {
        [PersistableAttribute(Name = "LossProbabilityPerResource")]
        public double LossProbabilityPerResource { get; set; } = 0.05;

        [PersistableAttribute(Name = "MinimumTotalLoss")]
        public int MinimumTotalLoss { get; set; } = 1;

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game) =>
            throw new InvalidOperationException("ReduceResources requires a planet target.");

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            Planet planet = context?.GetScopeTarget<Planet>();
            if (planet == null)
                return Execute(game);

            int oldRaw = planet.NumRawResourceNodes;
            int oldEnergy = planet.EnergyCapacity;
            if (oldRaw == 0 && oldEnergy == 0)
                return new List<GameResult>();

            int rawLoss = 0;
            int energyLoss = 0;
            int iterations = Math.Max(oldRaw, oldEnergy);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                if (
                    iteration < oldRaw
                    && RollProbability(
                        provider,
                        ((oldEnergy - rawLoss - energyLoss) + oldRaw) * LossProbabilityPerResource
                    )
                )
                    rawLoss++;
                if (
                    iteration < oldEnergy
                    && RollProbability(
                        provider,
                        ((oldRaw - rawLoss - energyLoss) + oldEnergy) * LossProbabilityPerResource
                    )
                )
                    energyLoss++;
            }

            int requiredLoss = Math.Min(MinimumTotalLoss, oldRaw + oldEnergy);
            while (rawLoss + energyLoss < requiredLoss)
            {
                if (oldRaw - rawLoss > 0)
                    rawLoss++;
                else if (oldEnergy - energyLoss > 0)
                    energyLoss++;
                else
                    break;
            }

            planet.EnergyCapacity = oldEnergy - energyLoss;
            planet.NumRawResourceNodes = Math.Min(oldRaw - rawLoss, planet.EnergyCapacity);

            List<GameResult> results = new List<GameResult>();
            AddStatChange(
                results,
                game,
                planet,
                PlanetStatType.RawMaterial,
                oldRaw,
                planet.NumRawResourceNodes
            );
            AddStatChange(
                results,
                game,
                planet,
                PlanetStatType.Energy,
                oldEnergy,
                planet.EnergyCapacity
            );
            results.Add(
                new PlanetIncidentResult
                {
                    Planet = planet,
                    IncidentType = IncidentType.Disaster,
                    Severity = rawLoss + energyLoss,
                    OldValue = oldRaw + oldEnergy,
                    NewValue = planet.NumRawResourceNodes + planet.EnergyCapacity,
                    Tick = game.CurrentTick,
                }
            );
            return results;
        }

        /// <summary>
        /// Rolls a normalized probability against the supplied random source.
        /// </summary>
        private static bool RollProbability(IRandomNumberProvider provider, double probability) =>
            provider.NextDouble() < Math.Min(1.0, Math.Max(0.0, probability));

        /// <summary>
        /// Adds a planet-stat result when the value changed.
        /// </summary>
        private static void AddStatChange(
            ICollection<GameResult> results,
            GameRoot game,
            Planet planet,
            PlanetStatType stat,
            int oldValue,
            int newValue
        )
        {
            if (oldValue == newValue)
                return;
            results.Add(
                new PlanetStatChangedResult
                {
                    Planet = planet,
                    Faction = game.GetFactions()
                        .FirstOrDefault(faction => faction.InstanceID == planet.OwnerInstanceID),
                    Stat = stat,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Tick = game.CurrentTick,
                }
            );
        }
    }
}
