using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    public enum PlanetStat
    {
        RawResourceNodes,
        EnergyCapacity,
    }

    /// <summary>
    /// Applies one explicit signed resource adjustment to the scoped planet.
    /// </summary>
    [PersistableObject(Name = "AdjustPlanetStat")]
    public sealed class AdjustPlanetStatAction : GameAction
    {
        [PersistableAttribute]
        public PlanetStat Stat { get; set; }

        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        public int? Amount { get; set; }
        public int? PercentOfCurrent { get; set; }

        [PersistableInlineCollection]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            if ((Amount.HasValue ? 1 : 0) + (PercentOfCurrent.HasValue ? 1 : 0) != 1)
                throw new InvalidOperationException(
                    "AdjustPlanetStat requires exactly one adjustment value."
                );
            IEnumerable<ISceneNode> selected = Selectors.SelectMany(selector =>
                selector.Select(game, context.Random, context.Activation)
            );
            Planet explicitPlanet = !string.IsNullOrWhiteSpace(PlanetBinding)
                ? context.Activation?.GetBindingReference<Planet>(PlanetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
            explicitPlanet ??= context.Activation?.GetTarget<Planet>();
            if (explicitPlanet != null)
                selected = new ISceneNode[] { explicitPlanet }.Concat(selected);
            List<ISceneNode> nodes = selected.Distinct().ToList();
            if (nodes.Count == 0)
                throw new InvalidOperationException(
                    "AdjustPlanetStat requires a planet, planet binding, target, or matching selector."
                );
            if (nodes.Any(node => node is not Planet))
                throw new InvalidOperationException(
                    "AdjustPlanetStat selectors may return only planets."
                );

            List<GameResult> results = new List<GameResult>();
            foreach (Planet planet in nodes.Cast<Planet>())
            {
                int oldValue = GetValue(planet, Stat);
                int adjustment = Amount ?? checked(oldValue * PercentOfCurrent.Value / 100);
                int newValue = Math.Max(0, checked(oldValue + adjustment));
                PlanetStatType resultStat;
                if (Stat == PlanetStat.RawResourceNodes)
                {
                    resultStat = PlanetStatType.RawMaterial;
                    planet.NumRawResourceNodes = newValue;
                }
                else
                {
                    resultStat = PlanetStatType.Energy;
                    planet.EnergyCapacity = newValue;
                }
                Faction faction = FindOwner(game, planet);
                results.Add(
                    new PlanetStatChangedResult
                    {
                        Planet = planet,
                        Faction = faction,
                        Stat = resultStat,
                        OldValue = oldValue,
                        NewValue = newValue,
                        Tick = game.CurrentTick,
                    }
                );
            }
            return results;
        }

        internal static int GetValue(Planet planet, PlanetStat stat) =>
            stat switch
            {
                PlanetStat.RawResourceNodes => planet.NumRawResourceNodes,
                PlanetStat.EnergyCapacity => planet.EnergyCapacity,
                _ => throw new InvalidOperationException($"Unsupported planet stat '{stat}'."),
            };

        /// <summary>
        /// Resolves the faction that currently owns the planet.
        /// </summary>
        private static Faction FindOwner(GameRoot game, Planet planet) =>
            game.GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == planet.OwnerInstanceID);
    }

    /// <summary>
    /// Reduces selected planet stats by independently rolling once for each current point.
    /// </summary>
    [PersistableObject(Name = "ReducePlanetStats")]
    public sealed class ReducePlanetStatsAction : GameAction
    {
        [PersistableAttribute(Name = "LossProbabilityPerResource")]
        public double LossProbabilityPerResource { get; set; } = 0.05;

        [PersistableAttribute(Name = "MinimumTotalLoss")]
        public int MinimumTotalLoss { get; set; } = 1;

        [PersistableInlineCollection]
        public List<PlanetStatReference> Stats { get; set; } = new List<PlanetStatReference>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Planet planet = context.Activation?.GetTarget<Planet>();
            if (planet == null)
                throw new InvalidOperationException("ReducePlanetStats requires a planet target.");

            List<PlanetStat> selectedStats = Stats.Select(stat => stat.Stat).Distinct().ToList();
            if (selectedStats.Count == 0)
                throw new InvalidOperationException(
                    "ReducePlanetStats requires at least one planet stat."
                );
            Dictionary<PlanetStat, int> oldValues = selectedStats.ToDictionary(
                stat => stat,
                stat => AdjustPlanetStatAction.GetValue(planet, stat)
            );
            if (oldValues.Values.Sum() == 0)
                return new List<GameResult>();

            if (LossProbabilityPerResource < 0 || LossProbabilityPerResource > 1)
                throw new InvalidOperationException(
                    "ReducePlanetStats.LossProbabilityPerResource must be between zero and one."
                );
            if (MinimumTotalLoss < 0)
                throw new InvalidOperationException(
                    "ReducePlanetStats.MinimumTotalLoss cannot be negative."
                );

            Dictionary<PlanetStat, int> losses = selectedStats.ToDictionary(stat => stat, _ => 0);
            foreach (PlanetStat stat in selectedStats)
            {
                for (int iteration = 0; iteration < oldValues[stat]; iteration++)
                {
                    if (RollProbability(context.Random, LossProbabilityPerResource))
                        losses[stat]++;
                }
            }

            int requiredLoss = Math.Min(MinimumTotalLoss, oldValues.Values.Sum());
            while (losses.Values.Sum() < requiredLoss)
            {
                PlanetStat? available = selectedStats
                    .Where(stat => oldValues[stat] - losses[stat] > 0)
                    .Cast<PlanetStat?>()
                    .FirstOrDefault();
                if (!available.HasValue)
                    break;
                losses[available.Value]++;
            }

            List<GameResult> results = new List<GameResult>();
            foreach (PlanetStat stat in selectedStats)
            {
                int newValue = oldValues[stat] - losses[stat];
                if (stat == PlanetStat.RawResourceNodes)
                    planet.NumRawResourceNodes = newValue;
                else
                    planet.EnergyCapacity = newValue;
                AddStatChange(
                    results,
                    game,
                    planet,
                    stat == PlanetStat.RawResourceNodes
                        ? PlanetStatType.RawMaterial
                        : PlanetStatType.Energy,
                    oldValues[stat],
                    newValue
                );
            }
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

    [PersistableObject(Name = "Stat")]
    public sealed class PlanetStatReference
    {
        [PersistableAttribute(Name = "Name")]
        public PlanetStat Stat { get; set; }
    }

    [PersistableObject(Name = "RecordPlanetIncident")]
    public sealed class RecordPlanetIncidentAction : GameAction
    {
        [PersistableAttribute(Name = "Type")]
        public IncidentType IncidentType { get; set; }

        public override List<GameResult> Execute(GameActionContext context)
        {
            Planet planet = context.Activation?.GetTarget<Planet>();
            if (planet == null)
                throw new InvalidOperationException(
                    "RecordPlanetIncident requires a planet target."
                );

            List<PlanetStatChangedResult> statChanges = context
                .Activation.Results.OfType<PlanetStatChangedResult>()
                .Where(result => result.Planet == planet)
                .ToList();
            List<IGameEntity> destroyed = context
                .Activation.Results.OfType<GameObjectDestroyedResult>()
                .Where(result => result.Context == planet)
                .Select(result => result.DestroyedObject)
                .Where(result => result != null)
                .ToList();
            int severity =
                statChanges.Sum(change => Math.Abs(change.NewValue - change.OldValue))
                + destroyed.Count;
            if (severity == 0)
                return new List<GameResult>();

            return new List<GameResult>
            {
                new PlanetIncidentResult
                {
                    Planet = planet,
                    IncidentType = IncidentType,
                    Severity = severity,
                    DestroyedObjects = destroyed,
                    Tick = context.Game.CurrentTick,
                },
            };
        }
    }
}
