using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects how a persistent integer event variable is updated.
    /// </summary>
    public enum EventVariableOperation
    {
        Set,
        Add,
        Minimum,
        Maximum,
    }

    /// <summary>
    /// Resolves deterministic Force-awareness checks shared by encounter actions.
    /// </summary>
    internal static class ForceEncounterDetection
    {
        /// <summary>
        /// Rolls whether two known Force ranks produce a detectable encounter.
        /// </summary>
        /// <param name="first">The first officer.</param>
        /// <param name="second">The second officer.</param>
        /// <param name="chanceModifier">The authored percentage-point modifier.</param>
        /// <param name="provider">The deterministic simulation random source.</param>
        /// <returns>True when the detection roll succeeds.</returns>
        public static bool Succeeds(
            Officer first,
            Officer second,
            int chanceModifier,
            IRandomNumberProvider provider
        )
        {
            if (first == null || second == null || provider == null)
                return false;

            int firstRank = first.ForceRank;
            int secondRank = second.ForceRank;
            if (firstRank == 0 || secondRank == 0)
                return false;

            int chance = Math.Min(100, Math.Max(0, firstRank + secondRank + chanceModifier));
            return chance > 0 && provider.NextInt(0, 100) < chance;
        }
    }

    /// <summary>
    /// Routes intelligence from a planet controller to one recipient faction.
    /// </summary>
    [PersistableObject]
    public sealed class InformantFactionRoute
    {
        public string ControllerFactionInstanceID { get; set; }
        public string RecipientFactionInstanceID { get; set; }
    }

    /// <summary>
    /// Identifies an unordered officer pair that a generic encounter action must ignore.
    /// </summary>
    [PersistableObject]
    public sealed class OfficerPairReference
    {
        public string FirstOfficerInstanceID { get; set; }
        public string SecondOfficerInstanceID { get; set; }

        /// <summary>
        /// Returns whether two officers match the authored pair in either order.
        /// </summary>
        /// <param name="first">The first officer to compare.</param>
        /// <param name="second">The second officer to compare.</param>
        /// <returns>True when both authored instance IDs are present.</returns>
        public bool Matches(Officer first, Officer second)
        {
            return (
                    first?.InstanceID == FirstOfficerInstanceID
                    && second?.InstanceID == SecondOfficerInstanceID
                )
                || (
                    first?.InstanceID == SecondOfficerInstanceID
                    && second?.InstanceID == FirstOfficerInstanceID
                );
        }
    }

    /// <summary>
    /// Resolves a controlled-world informant check with data-defined faction routing
    /// and uniformly weighted intelligence categories.
    /// </summary>
    [PersistableObject(Name = "InformantIntelligence")]
    public sealed class InformantIntelligenceAction : GameAction
    {
        public int MaximumPopularSupport { get; set; } = 100;
        public string Title { get; set; }
        public string Body { get; set; }
        public MessageType MessageType { get; set; } = MessageType.Advice;
        public string DetailImageKey { get; set; }
        public string VoicePath { get; set; }
        public AdvisorCue AdvisorCue { get; set; }
        public List<InformantFactionRoute> FactionRoutes { get; set; } =
            new List<InformantFactionRoute>();
        public List<PlanetIntelligenceCategory> IntelligenceChoices { get; set; } =
            new List<PlanetIntelligenceCategory>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            throw new InvalidOperationException(
                "InformantIntelligence must execute from a planet-scoped game event."
            );
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            Planet planet = context?.GetScopeTarget<Planet>();
            if (planet == null)
                return Execute(game);

            InformantFactionRoute route = FactionRoutes.FirstOrDefault(candidate =>
                candidate.ControllerFactionInstanceID == planet.OwnerInstanceID
            );
            if (route == null)
                return new List<GameResult>();

            int support = Math.Max(
                0,
                Math.Min(MaximumPopularSupport, planet.GetPopularSupport(planet.OwnerInstanceID))
            );
            if (provider.NextInt(0, MaximumPopularSupport) < support)
                return new List<GameResult>();

            PlanetIntelligenceCategory categories = IntelligenceChoices[
                provider.NextInt(0, IntelligenceChoices.Count)
            ];
            Faction recipient = game.GetFactionByOwnerInstanceID(route.RecipientFactionInstanceID);
            return new List<GameResult>
            {
                new PlanetIntelligenceResult
                {
                    Recipient = recipient,
                    Planet = planet,
                    Categories = categories,
                    Tick = game.CurrentTick,
                },
                new NarrativeMessageResult
                {
                    Recipient = recipient,
                    Subject = planet,
                    Location = planet,
                    MessageType = MessageType,
                    TitleTemplate = Title,
                    BodyTemplate = Body,
                    DetailImageKey = DetailImageKey,
                    VoicePath = VoicePath,
                    AdvisorCue = AdvisorCue,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Changes one resource capacity on the planet selected by the event target.
    /// Limits and affected facility types are supplied by content.
    /// </summary>
    [PersistableObject(Name = "ChangeResources")]
    public sealed class ChangeResourcesAction : GameAction
    {
        public int MinimumRawMaterials { get; set; }
        public int MaximumRawMaterials { get; set; } = 15;
        public int MinimumEnergy { get; set; }
        public int MaximumEnergy { get; set; } = 15;
        public List<BuildingType> EnergyFacilityTypes { get; set; } = new List<BuildingType>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            throw new InvalidOperationException("ChangeResources requires a planet target.");
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            Planet planet = context?.GetScopeTarget<Planet>();
            if (planet == null)
                return Execute(game);

            return ApplyResourceChange(game, planet, provider);
        }

        /// <summary>
        /// Applies one eligible resource change and reports the resulting planet state.
        /// </summary>
        private List<GameResult> ApplyResourceChange(
            GameRoot game,
            Planet planet,
            IRandomNumberProvider provider
        )
        {
            int oldRaw = planet.NumRawResourceNodes;
            int oldEnergy = planet.EnergyCapacity;
            PlanetStatType stat;
            int oldValue;
            int newValue;

            switch (provider.NextInt(0, 4))
            {
                case 0
                    when HasCompletedFacility(planet, BuildingType.Mine)
                        && oldRaw > MinimumRawMaterials:
                    stat = PlanetStatType.RawMaterial;
                    oldValue = oldRaw;
                    newValue = oldRaw - 1;
                    planet.NumRawResourceNodes = newValue;
                    break;
                case 1
                    when HasAnyCompletedFacility(planet, EnergyFacilityTypes)
                        && oldEnergy > MinimumEnergy:
                    stat = PlanetStatType.Energy;
                    oldValue = oldEnergy;
                    newValue = oldEnergy - 1;
                    planet.EnergyCapacity = newValue;
                    break;
                case 2 when oldRaw < MaximumRawMaterials && oldRaw < oldEnergy:
                    stat = PlanetStatType.RawMaterial;
                    oldValue = oldRaw;
                    newValue = oldRaw + 1;
                    planet.NumRawResourceNodes = newValue;
                    break;
                case 3 when oldEnergy < MaximumEnergy:
                    stat = PlanetStatType.Energy;
                    oldValue = oldEnergy;
                    newValue = oldEnergy + 1;
                    planet.EnergyCapacity = newValue;
                    break;
                default:
                    return new List<GameResult>();
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
        /// Returns whether the planet contains a completed facility of the requested type.
        /// </summary>
        private static bool HasCompletedFacility(Planet planet, BuildingType type) =>
            planet.Buildings.Any(building =>
                building.BuildingType == type
                && building.ManufacturingStatus == ManufacturingStatus.Complete
            );

        /// <summary>
        /// Returns whether the planet contains a completed facility of any requested type.
        /// </summary>
        private static bool HasAnyCompletedFacility(
            Planet planet,
            IReadOnlyCollection<BuildingType> types
        ) =>
            planet.Buildings.Any(building =>
                types.Contains(building.BuildingType)
                && building.ManufacturingStatus == ManufacturingStatus.Complete
            );

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

    /// <summary>
    /// Selects candidate buildings by their general gameplay type.
    /// </summary>
    [PersistableObject]
    public sealed class BuildingCandidates
    {
        public List<BuildingType> BuildingTypes { get; set; } = new List<BuildingType>();
    }

    /// <summary>
    /// Includes all eligible regiments on the selected planet.
    /// </summary>
    [PersistableObject]
    public sealed class RegimentCandidates { }

    /// <summary>
    /// Combines the unit categories eligible for a destructive incident.
    /// </summary>
    [PersistableObject]
    public sealed class DestroyUnitCandidates
    {
        public BuildingCandidates Buildings { get; set; }
        public RegimentCandidates Regiments { get; set; }
    }

    /// <summary>
    /// Destroys a bounded random subset of eligible units on the selected planet.
    /// </summary>
    [PersistableObject(Name = "DestroyUnits")]
    public sealed class DestroyUnitsAction : GameAction
    {
        [PersistableAttribute(Name = "ChancePerUnit")]
        public double ChancePerUnit { get; set; } = 0.1;

        [PersistableAttribute(Name = "MinimumCount")]
        public int MinimumCount { get; set; }

        [PersistableAttribute(Name = "MaximumCount")]
        public int MaximumCount { get; set; } = int.MaxValue;

        public DestroyUnitCandidates Candidates { get; set; } = new DestroyUnitCandidates();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game) =>
            throw new InvalidOperationException("DestroyUnits requires a planet target.");

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

            List<ISceneNode> eligible = new List<ISceneNode>();
            if (Candidates?.Buildings != null)
            {
                eligible.AddRange(
                    planet.Buildings.Where(building =>
                        building.ManufacturingStatus == ManufacturingStatus.Complete
                        && building.OwnerInstanceID == planet.OwnerInstanceID
                        && Candidates.Buildings.BuildingTypes.Contains(building.BuildingType)
                    )
                );
            }
            if (Candidates?.Regiments != null)
                eligible.AddRange(
                    planet.Regiments.Where(regiment =>
                        regiment.OwnerInstanceID == planet.OwnerInstanceID
                    )
                );

            eligible = eligible.OrderBy(unit => unit.InstanceID, StringComparer.Ordinal).ToList();
            List<ISceneNode> destroyed = eligible
                .Where(_ => provider.NextDouble() < Math.Clamp(ChancePerUnit, 0.0, 1.0))
                .ToList();
            List<ISceneNode> remaining = eligible.Except(destroyed).ToList();
            while (destroyed.Count < Math.Min(MinimumCount, eligible.Count))
            {
                int index = provider.NextInt(0, remaining.Count);
                destroyed.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            while (destroyed.Count > Math.Max(0, MaximumCount))
                destroyed.RemoveAt(provider.NextInt(0, destroyed.Count));

            foreach (ISceneNode unit in destroyed)
                game.DetachNode(unit);

            PlanetIncidentResult incident = context
                .Results.OfType<PlanetIncidentResult>()
                .LastOrDefault(result =>
                    result.Planet == planet && result.IncidentType == IncidentType.Disaster
                );
            if (incident != null)
            {
                incident.DestroyedObjects.AddRange(destroyed.Cast<IGameEntity>());
                incident.Severity += destroyed.Count;
            }

            return destroyed.ConvertAll<GameResult>(unit => new GameObjectDestroyedResult
            {
                DestroyedObject = unit,
                Context = planet,
                Tick = game.CurrentTick,
            });
        }
    }

    /// <summary>
    /// Executes one authored action list when its probability roll succeeds.
    /// </summary>
    [PersistableObject(Name = "RandomOutcome")]
    public class RandomOutcomeAction : GameAction
    {
        [PersistableAttribute(Name = "Value")]
        public double Probability { get; set; }

        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        public RandomOutcomeAction()
            : base() { }

        /// <summary>
        /// Rolls against the configured probability; on success, executes a uniformly-chosen
        /// child action and returns its results. Otherwise returns no results.
        /// </summary>
        /// <param name="game">The game state passed to the chosen child action.</param>
        /// <returns>The results produced by the chosen action, or an empty list if the roll failed.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (provider.NextDouble() < Probability)
            {
                return Actions[provider.NextInt(0, Actions.Count)].Execute(game, provider);
            }

            return new List<GameResult>();
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (provider.NextDouble() >= Probability)
                return new List<GameResult>();
            return Actions[provider.NextInt(0, Actions.Count)].Execute(game, provider, context);
        }
    }

    /// <summary>
    /// Executes every child action when one probability roll succeeds.
    /// </summary>
    [PersistableObject(Name = "Chance")]
    public sealed class ChanceAction : GameAction
    {
        [PersistableAttribute(Name = "Value")]
        public double Probability { get; set; }

        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game) => Execute(game, game.Random);

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider) =>
            Execute(game, provider, null);

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (provider.NextDouble() >= Probability)
                return new List<GameResult>();

            List<GameResult> results = new List<GameResult>();
            foreach (GameAction action in Actions)
                results.AddRange(action.Execute(game, provider, context));
            return results;
        }
    }

    /// <summary>
    /// Defines one weighted action list within a random choice.
    /// </summary>
    [PersistableObject(Name = "Choice")]
    public sealed class RandomChoice
    {
        public int Weight { get; set; } = 1;
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    /// <summary>
    /// Selects one weighted outcome and executes every action belonging to that outcome.
    /// </summary>
    [PersistableObject(Name = "RandomChoice")]
    public sealed class RandomChoiceAction : GameAction
    {
        public List<RandomChoice> Choices { get; set; } = new List<RandomChoice>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game) => Execute(game, game.Random);

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider) =>
            Execute(game, provider, null);

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            int totalWeight = Choices.Sum(choice => choice.Weight);
            int roll = provider.NextInt(0, totalWeight);
            RandomChoice selected = null;
            foreach (RandomChoice choice in Choices)
            {
                roll -= choice.Weight;
                if (roll < 0)
                {
                    selected = choice;
                    break;
                }
            }

            List<GameResult> results = new List<GameResult>();
            foreach (GameAction action in selected.Actions)
                results.AddRange(action.Execute(game, provider, context));
            return results;
        }
    }

    /// <summary>
    /// Requests authoritative resolution of an encounter between two opposing officers.
    /// </summary>
    [PersistableObject(Name = "TriggerDuel")]
    public class TriggerDuelAction : GameAction
    {
        public string EncounteredOfficerInstanceID { get; set; }
        public string OpposingOfficerInstanceID { get; set; }
        public bool EncounteredOfficerIsArrivingParticipant { get; set; }
        public bool UseForceRankDetectionChance { get; set; }
        public int ForceRankDetectionChanceModifier { get; set; }
        public string ImagePath { get; set; }
        public string VoicePath { get; set; }

        /// <summary>
        /// Creates an empty action for content deserialization.
        /// </summary>
        public TriggerDuelAction()
            : base() { }

        /// <summary>
        /// Requests authoritative resolution of a linked-officer encounter.
        /// </summary>
        /// <param name="game">The game state used to resolve the officers.</param>
        /// <returns>The encounter request, or no result when either officer is unavailable.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, null, null);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            Officer encountered = game.GetSceneNodeByInstanceID<Officer>(
                EncounteredOfficerInstanceID
            );
            Officer opposing = game.GetSceneNodeByInstanceID<Officer>(OpposingOfficerInstanceID);
            if (encountered == null || opposing == null)
                return new List<GameResult>();

            IRandomNumberProvider random = provider ?? game.Random;
            if (
                UseForceRankDetectionChance
                && !ForceEncounterDetection.Succeeds(
                    encountered,
                    opposing,
                    ForceRankDetectionChanceModifier,
                    random
                )
            )
                return new List<GameResult>();

            if (EncounteredOfficerIsArrivingParticipant)
            {
                ISceneNode arrivingUnit =
                    (context?.TriggerResult as UnitArrivedResult)?.Unit as ISceneNode;
                bool encounteredArrived =
                    arrivingUnit == encountered
                    || arrivingUnit?.GetChildren<Officer>(officer => officer == encountered).Any()
                        == true;
                bool opposingArrived =
                    arrivingUnit == opposing
                    || arrivingUnit?.GetChildren<Officer>(officer => officer == opposing).Any()
                        == true;
                if (encounteredArrived == opposingArrived)
                    return new List<GameResult>();
                if (opposingArrived)
                    (encountered, opposing) = (opposing, encountered);
            }

            return new List<GameResult>
            {
                new OfficerEncounterRequestedResult
                {
                    EncounteredOfficer = encountered,
                    OpposingOfficer = opposing,
                    ImagePath = ImagePath,
                    VoicePath = VoicePath,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Reports opposing, revealed Force users brought together by a unit arrival.
    /// Presentation and named-pair exclusions remain content-authored so other packs can extend
    /// the mechanic without adding code.
    /// </summary>
    [PersistableObject(Name = "ReportForceDetection")]
    public sealed class ReportForceDetectionAction : GameAction
    {
        public bool RequireForceEligible { get; set; } = true;
        public bool UseForceRankDetectionChance { get; set; }
        public int ForceRankDetectionChanceModifier { get; set; }
        public MessageType MessageType { get; set; } = MessageType.Mission;
        public string Title { get; set; }
        public string Body { get; set; }
        public string DetailImageKey { get; set; }
        public string VoicePath { get; set; }
        public Dictionary<string, string> VoicePaths { get; set; } =
            new Dictionary<string, string>();
        public AdvisorCue AdvisorCue { get; set; }
        public List<OfficerPairReference> ExcludedPairs { get; set; } =
            new List<OfficerPairReference>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game) => new List<GameResult>();

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (
                context?.TriggerResult is not UnitArrivedResult arrival
                || arrival.Unit is not ISceneNode arrivingUnit
                || arrival.Destination == null
            )
                return new List<GameResult>();

            List<Officer> arrivingOfficers = GetOfficers(arrivingUnit)
                .Where(IsEligible)
                .OrderBy(officer => officer.InstanceID, StringComparer.Ordinal)
                .ToList();
            if (arrivingOfficers.Count == 0)
                return new List<GameResult>();

            List<Officer> presentOfficers = arrival
                .Destination.GetChildren<Officer>(_ => true, recurse: true)
                .Except(arrivingOfficers)
                .Where(IsEligible)
                .OrderBy(officer => officer.InstanceID, StringComparer.Ordinal)
                .ToList();

            List<GameResult> results = new List<GameResult>();
            foreach (Officer arriving in arrivingOfficers)
            {
                foreach (Officer present in presentOfficers)
                {
                    if (
                        arriving.OwnerInstanceID == present.OwnerInstanceID
                        || ExcludedPairs.Any(pair => pair.Matches(arriving, present))
                    )
                        continue;

                    if (
                        UseForceRankDetectionChance
                        && !ForceEncounterDetection.Succeeds(
                            arriving,
                            present,
                            ForceRankDetectionChanceModifier,
                            provider ?? game.Random
                        )
                    )
                        continue;

                    AddReport(game, provider, results, arriving, present, arrival.Destination);
                    AddReport(game, provider, results, present, arriving, arrival.Destination);
                }
            }

            return results;
        }

        /// <summary>
        /// Returns whether an officer may participate in Force-detection reporting.
        /// </summary>
        private bool IsEligible(Officer officer)
        {
            return officer.IsJedi
                && (!RequireForceEligible || officer.IsForceEligible)
                && !officer.IsCaptured
                && !officer.IsKilled;
        }

        /// <summary>
        /// Enumerates an officer node or the officers contained beneath a composite node.
        /// </summary>
        private static IEnumerable<Officer> GetOfficers(ISceneNode node)
        {
            if (node is Officer officer)
                yield return officer;

            foreach (Officer descendant in node.GetChildren<Officer>(_ => true, recurse: true))
                yield return descendant;
        }

        /// <summary>
        /// Adds one faction-routed Force-detection report.
        /// </summary>
        private void AddReport(
            GameRoot game,
            IRandomNumberProvider provider,
            ICollection<GameResult> results,
            Officer detector,
            Officer detected,
            Planet location
        )
        {
            Faction recipient = game.Factions.FirstOrDefault(faction =>
                faction.InstanceID == detector.OwnerInstanceID
            );
            if (recipient == null)
                return;

            string voicePath = VoicePaths.TryGetValue(recipient.InstanceID, out string routedVoice)
                ? routedVoice
                : VoicePath;
            results.Add(
                new NarrativeMessageResult
                {
                    Recipient = recipient,
                    Subject = detector,
                    RelatedSubject = detected,
                    Location = location,
                    MessageType = MessageType,
                    TitleTemplate = Title,
                    BodyTemplate = Body,
                    DetailImageKey = DetailImageKey,
                    OverlayImagePath = detector.MessageImagePath,
                    VoicePath = voicePath,
                    OfficerVoicePath = detector.GetVoicePath(
                        OfficerVoiceLineType.EnemyDetected,
                        provider ?? game.Random
                    ),
                    AdvisorCue = AdvisorCue,
                    Tick = game.CurrentTick,
                }
            );
        }
    }

    /// <summary>
    /// Executes another event immediately within the current deterministic result pipeline.
    /// </summary>
    [PersistableObject(Name = "TriggerEvent")]
    public class TriggerEventAction : GameAction
    {
        public string EventInstanceID { get; set; }

        public TriggerEventAction()
            : base() { }

        /// <summary>
        /// Resolves the referenced <see cref="GameEvent"/> and runs its action chain.
        /// Falls back to <see cref="GameRoot.Random"/> if no provider has been injected.
        /// </summary>
        /// <param name="game">The game state used to resolve the event.</param>
        /// <returns>The results produced by the triggered event's actions.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            GameEvent gameEvent = game.GetEventByInstanceID(EventInstanceID);
            return gameEvent.Execute(game, provider ?? game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            GameEvent gameEvent = game.GetEventByInstanceID(EventInstanceID);
            GameEventExecutionContext childContext =
                context == null
                    ? null
                    : new GameEventExecutionContext(
                        gameEvent,
                        context.State,
                        context.ScopeTarget,
                        context.TriggerResult
                    );
            return gameEvent.Execute(game, provider ?? game.Random, childContext);
        }
    }

    /// <summary>
    /// Selects one authored narrative fragment from current simulation state.
    /// </summary>
    [PersistableObject(Name = "BodySegment")]
    public sealed class NarrativeBodySegment
    {
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public string Body { get; set; }
        public string ElseBody { get; set; }

        /// <summary>
        /// Selects the primary or fallback body from the current conditions.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="triggerResult">The result that activated the containing event.</param>
        /// <returns>The body selected by the condition results.</returns>
        public string Resolve(GameRoot game, GameResult triggerResult = null)
        {
            return Conditionals.TrueForAll(condition => condition.IsMet(game, triggerResult))
                ? Body
                : ElseBody;
        }
    }

    /// <summary>
    /// Emits a normal faction message from presentation data authored with a game event.
    /// </summary>
    [PersistableObject(Name = "AddMessage")]
    public sealed class AddMessageAction : GameAction
    {
        public string RecipientFactionInstanceID { get; set; }
        public string RecipientUnitInstanceID { get; set; }
        public string SubjectInstanceID { get; set; }
        public string RelatedSubjectInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
        public MessageType MessageType { get; set; } = MessageType.Advice;
        public string Title { get; set; }
        public string Body { get; set; }
        public List<NarrativeBodySegment> BodySegments { get; set; } =
            new List<NarrativeBodySegment>();
        public string DetailImageKey { get; set; }
        public string ImagePath { get; set; }
        public bool ImagePathFromOfficerEncounter { get; set; }
        public string OverlayImagePath { get; set; }
        public string VoicePath { get; set; }
        public bool VoicePathFromOfficerEncounter { get; set; }
        public string OfficerVoicePath { get; set; }
        public AdvisorCue AdvisorCue { get; set; }

        /// <summary>
        /// Resolves the authored references and emits presentation-neutral narrative data.
        /// </summary>
        /// <param name="game">The game state used to resolve faction and scene-node IDs.</param>
        /// <returns>A single narrative message result.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            return ExecuteCore(game, null);
        }

        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            return ExecuteCore(game, context?.TriggerResult);
        }

        /// <summary>
        /// Builds the configured narrative result from the optional triggering result.
        /// </summary>
        private List<GameResult> ExecuteCore(GameRoot game, GameResult triggerResult)
        {
            ISceneNode subject = game.GetSceneNodeByInstanceID<ISceneNode>(SubjectInstanceID);
            ISceneNode relatedSubject = game.GetSceneNodeByInstanceID<ISceneNode>(
                RelatedSubjectInstanceID
            );
            ISceneNode recipientUnit = game.GetSceneNodeByInstanceID<ISceneNode>(
                RecipientUnitInstanceID
            );
            string recipientId = RecipientFactionInstanceID;
            if (string.IsNullOrWhiteSpace(recipientId))
                recipientId = recipientUnit?.OwnerInstanceID ?? subject?.OwnerInstanceID;

            if (string.IsNullOrWhiteSpace(recipientId))
                throw new InvalidOperationException(
                    "AddMessage could not resolve its recipient faction."
                );

            Faction recipient = game.GetFactionByOwnerInstanceID(recipientId);
            Planet location = game.GetSceneNodeByInstanceID<Planet>(LocationInstanceID);
            if (location == null && subject != null)
                location = subject as Planet ?? subject.GetParentOfType<Planet>();

            string bodyTemplate = Body ?? string.Empty;
            foreach (NarrativeBodySegment segment in BodySegments)
                bodyTemplate += segment.Resolve(game, triggerResult) ?? string.Empty;
            string voicePath =
                VoicePathFromOfficerEncounter && triggerResult is OfficerEncounterResult encounter
                    ? encounter.VoicePath
                    : VoicePath;
            string imagePath =
                ImagePathFromOfficerEncounter
                && triggerResult is OfficerEncounterResult encounterImage
                    ? encounterImage.ImagePath
                    : ImagePath;

            return new List<GameResult>
            {
                new NarrativeMessageResult
                {
                    Recipient = recipient,
                    Subject = subject,
                    RelatedSubject = relatedSubject,
                    Location = location,
                    MessageType = MessageType,
                    TitleTemplate = Title,
                    BodyTemplate = bodyTemplate,
                    DetailImageKey = DetailImageKey,
                    ImagePath = imagePath,
                    OverlayImagePath = OverlayImagePath,
                    VoicePath = voicePath,
                    OfficerVoicePath = OfficerVoicePath,
                    AdvisorCue = AdvisorCue,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Executes one of two authored action lists based on data-defined conditions.
    /// </summary>
    [PersistableObject(Name = "Conditional")]
    public class ConditionalAction : GameAction
    {
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
        public List<GameAction> ElseActions { get; set; } = new List<GameAction>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameAction> selected = Conditionals.TrueForAll(condition => condition.IsMet(game))
                ? Actions
                : ElseActions;
            List<GameResult> results = new List<GameResult>();
            foreach (GameAction action in selected)
                results.AddRange(action.Execute(game, provider));
            return results;
        }

        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            List<GameAction> selected = Conditionals.TrueForAll(condition =>
                condition.IsMet(game, context)
            )
                ? Actions
                : ElseActions;
            List<GameResult> results = new List<GameResult>();
            foreach (GameAction action in selected)
                results.AddRange(action.Execute(game, provider, context));
            return results;
        }
    }

    /// <summary>
    /// Mutates a persistent integer used to coordinate data-defined story stages.
    /// </summary>
    [PersistableObject(Name = "SetEventVariable")]
    public class SetEventVariableAction : GameAction
    {
        public string Key { get; set; }
        public EventVariableOperation Operation { get; set; }
        public int Value { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            int previousValue = game.GetEventVariable(Key);
            int currentValue = Operation switch
            {
                EventVariableOperation.Set => Value,
                EventVariableOperation.Add => checked(previousValue + Value),
                EventVariableOperation.Minimum => Math.Min(previousValue, Value),
                EventVariableOperation.Maximum => Math.Max(previousValue, Value),
                _ => throw new InvalidOperationException(
                    $"Unsupported event variable operation '{Operation}'."
                ),
            };
            game.SetEventVariable(Key, currentValue);
            return new List<GameResult>
            {
                new EventVariableChangedResult
                {
                    Key = Key,
                    PreviousValue = previousValue,
                    CurrentValue = currentValue,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Updates the authored presentation used for an officer.
    /// </summary>
    [PersistableObject(Name = "UpdateOfficerPresentation")]
    public sealed class UpdateOfficerPresentationAction : GameAction
    {
        public string OfficerInstanceID { get; set; }
        public string DisplayImagePath { get; set; }
        public string SmallDisplayImagePath { get; set; }
        public string MessageImagePath { get; set; }
        public string EncyclopediaImagePath { get; set; }
        public bool UsesAdvancedVoiceLines { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"UpdateOfficerPresentation could not resolve officer '{OfficerInstanceID}'."
                );

            if (!string.IsNullOrWhiteSpace(DisplayImagePath))
                officer.DisplayImagePath = DisplayImagePath;
            if (!string.IsNullOrWhiteSpace(SmallDisplayImagePath))
                officer.SmallDisplayImagePath = SmallDisplayImagePath;
            if (!string.IsNullOrWhiteSpace(MessageImagePath))
                officer.MessageImagePath = MessageImagePath;
            if (!string.IsNullOrWhiteSpace(EncyclopediaImagePath))
                officer.EncyclopediaImagePath = EncyclopediaImagePath;
            officer.UsesAdvancedVoiceLines = UsesAdvancedVoiceLines;

            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Requests authoritative movement for one movable scene node.
    /// </summary>
    [PersistableObject(Name = "RequestMovement")]
    public class RequestMovementAction : GameAction
    {
        public string UnitInstanceID { get; set; }
        public string DestinationInstanceID { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            IMovable unit = game.GetSceneNodeByInstanceID<IMovable>(UnitInstanceID);
            ContainerNode destination = game.GetSceneNodeByInstanceID<ContainerNode>(
                DestinationInstanceID
            );
            if (unit == null)
                throw new InvalidOperationException(
                    $"RequestMovement could not resolve movable unit '{UnitInstanceID}'."
                );
            if (destination == null)
                throw new InvalidOperationException(
                    $"RequestMovement could not resolve destination '{DestinationInstanceID}'."
                );

            return new List<GameResult>
            {
                new UnitMovementRequestedResult
                {
                    Unit = unit,
                    Destination = destination,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Removes one active unit from the scene graph while retaining it in faction storage.
    /// </summary>
    [PersistableObject(Name = "AddToVoid")]
    public sealed class AddToVoidAction : GameAction
    {
        [PersistableAttribute(Name = "UnitInstanceID")]
        public string UnitInstanceID { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                throw new InvalidOperationException(
                    $"AddToVoid could not resolve unit '{UnitInstanceID}'."
                );
            game.AddToVoid(unit);
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Sets the reason an off-map unit is unavailable.
    /// </summary>
    [PersistableObject(Name = "SetStatus")]
    public sealed class SetStatusAction : GameAction
    {
        public string UnitInstanceID { get; set; }
        public VoidStatus Status { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                throw new InvalidOperationException(
                    $"SetStatus could not resolve unit '{UnitInstanceID}'."
                );
            game.SetVoidStatus(unit, Status);
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Schedules another global game event relative to the current tick.
    /// </summary>
    [PersistableObject(Name = "ScheduleEvent")]
    public sealed class ScheduleEventAction : GameAction
    {
        public string EventInstanceID { get; set; }
        public int DelayTicks { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            if (!game.EventPool.Any(gameEvent => gameEvent.InstanceID == EventInstanceID))
                throw new InvalidOperationException(
                    $"ScheduleEvent could not resolve event '{EventInstanceID}'."
                );
            if (DelayTicks < 0)
                throw new InvalidOperationException("ScheduleEvent delay cannot be negative.");

            GameEventState state = game.GetEventState(EventInstanceID);
            state.IsInitialized = true;
            state.NextEligibleTick = checked(game.CurrentTick + DelayTicks);
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Adds Force experience calculated as a percentage of the officer's current rank.
    /// </summary>
    [PersistableObject(Name = "AddForceExperience")]
    public sealed class AddForceExperienceAction : GameAction
    {
        public string OfficerInstanceID { get; set; }
        public int PercentOfCurrentRank { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"AddForceExperience could not resolve officer '{OfficerInstanceID}'."
                );
            if (PercentOfCurrentRank < 0)
                throw new InvalidOperationException(
                    "AddForceExperience percentage cannot be negative."
                );

            int previousRank = officer.ForceRank;
            int gained = previousRank * PercentOfCurrentRank / 100;
            officer.ForceValue += gained;

            return new List<GameResult>
            {
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = gained,
                    PreviousForceRank = previousRank,
                    CurrentForceRank = officer.ForceRank,
                    SuppressRankChangeMessage = true,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Returns an off-map unit to its last valid attachment or a friendly fallback planet.
    /// </summary>
    [PersistableObject(Name = "ReturnFromVoid")]
    public sealed class ReturnFromVoidAction : GameAction
    {
        public string UnitInstanceID { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                throw new InvalidOperationException(
                    $"ReturnFromVoid could not resolve unit '{UnitInstanceID}'."
                );
            game.ReturnFromVoid(unit);
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Starts a persistent, timed story capture through the authoritative mission system.
    /// </summary>
    [PersistableObject(Name = "StartStoryCapture")]
    public sealed class StartStoryCaptureAction : GameAction
    {
        public string TargetOfficerInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public string CaptorFactionInstanceID { get; set; }
        public bool CanEscape { get; set; }
        public int AttackRating { get; set; }
        public OfficerRating ResistanceRating { get; set; } = OfficerRating.Combat;
        public string ProbabilityTableKey { get; set; } = AbductionMission.MissionTypeID;
        public string DisplayName { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target == null)
                throw new InvalidOperationException(
                    $"StartStoryCapture could not resolve target officer '{TargetOfficerInstanceID}'."
                );

            return new List<GameResult>
            {
                new StoryCaptureRequestedResult
                {
                    Target = target,
                    DurationTicks = DurationTicks,
                    CaptorFactionInstanceID = CaptorFactionInstanceID,
                    CanEscape = CanEscape,
                    AttackRating = AttackRating,
                    ResistanceRating = ResistanceRating,
                    ProbabilityTableKey = ProbabilityTableKey,
                    DisplayName = DisplayName,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Announces a bounty-hunter attack through the normal result pipeline.
    /// </summary>
    [PersistableObject(Name = "BountyAttack")]
    public sealed class BountyAttackAction : GameAction
    {
        public string OfficerInstanceID { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"BountyAttack could not resolve officer '{OfficerInstanceID}'."
                );

            return new List<GameResult>
            {
                new BountyAttackResult { Officer = officer, Tick = game.CurrentTick },
            };
        }
    }

    /// <summary>
    /// Starts independent rescue missions for all available content-authored rescuers.
    /// </summary>
    [PersistableObject(Name = "StartStoryRescue")]
    public sealed class StartStoryRescueAction : GameAction
    {
        public string CaptiveOfficerInstanceID { get; set; }
        public List<string> RescuerOfficerInstanceIDs { get; set; } = new List<string>();
        public int DurationTicks { get; set; }
        public int DurationRandomTicks { get; set; }
        public int RatingDivisor { get; set; } = 1;
        public int SuccessCombatBonus { get; set; }
        public int SuccessEspionageBonus { get; set; }
        public bool CaptureRescuerOnFailure { get; set; }
        public bool FailedRescuerCanEscape { get; set; }
        public string DisplayName { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer captive = game.GetSceneNodeByInstanceID<Officer>(CaptiveOfficerInstanceID);
            if (captive == null)
                throw new InvalidOperationException(
                    $"StartStoryRescue could not resolve captive officer '{CaptiveOfficerInstanceID}'."
                );

            List<Officer> rescuers = new List<Officer>();
            foreach (string rescuerId in RescuerOfficerInstanceIDs)
            {
                Officer rescuer = game.GetSceneNodeByInstanceID<Officer>(rescuerId);
                if (rescuer == null)
                    throw new InvalidOperationException(
                        $"StartStoryRescue could not resolve rescuer officer '{rescuerId}'."
                    );
                rescuers.Add(rescuer);
            }

            return new List<GameResult>
            {
                new StoryRescueRequestedResult
                {
                    Captive = captive,
                    Rescuers = rescuers,
                    DurationTicks = DurationTicks,
                    DurationRandomTicks = DurationRandomTicks,
                    RatingDivisor = RatingDivisor,
                    SuccessCombatBonus = SuccessCombatBonus,
                    SuccessEspionageBonus = SuccessEspionageBonus,
                    CaptureRescuerOnFailure = CaptureRescuerOnFailure,
                    FailedRescuerCanEscape = FailedRescuerCanEscape,
                    DisplayName = DisplayName,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Sends a content-selected collector to retrieve all matching prisoners at a story location.
    /// </summary>
    [PersistableObject(Name = "StartStoryPickup")]
    public sealed class StartStoryPickupAction : GameAction
    {
        public string CollectorOfficerInstanceID { get; set; }
        public string LocationOfficerInstanceID { get; set; }
        public string CaptiveFactionInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public bool CaptivesCanEscapeAfterPickup { get; set; }
        public string DisplayName { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer collector = game.GetSceneNodeByInstanceID<Officer>(CollectorOfficerInstanceID);
            if (collector == null)
                throw new InvalidOperationException(
                    $"StartStoryPickup could not resolve collector officer '{CollectorOfficerInstanceID}'."
                );

            Officer locationOfficer = game.GetSceneNodeByInstanceID<Officer>(
                LocationOfficerInstanceID
            );
            Planet location = locationOfficer?.GetParentOfType<Planet>();
            if (location == null)
                throw new InvalidOperationException(
                    $"StartStoryPickup could not resolve a planet for officer '{LocationOfficerInstanceID}'."
                );

            return new List<GameResult>
            {
                new OfficerPickupResult
                {
                    Officer = collector,
                    InProgress = true,
                    Tick = game.CurrentTick,
                },
                new StoryPickupRequestedResult
                {
                    Collector = collector,
                    Location = location,
                    CaptiveFactionInstanceID = CaptiveFactionInstanceID,
                    DurationTicks = DurationTicks,
                    CaptivesCanEscapeAfterPickup = CaptivesCanEscapeAfterPickup,
                    DisplayName = DisplayName,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Starts the persisted, two-leg journey that culminates in the scripted final battle.
    /// </summary>
    [PersistableObject(Name = "StartStoryFinalBattle")]
    public sealed class StartStoryFinalBattleAction : GameAction
    {
        public string LukeOfficerInstanceID { get; set; }
        public string VaderOfficerInstanceID { get; set; }
        public string PalpatineOfficerInstanceID { get; set; }
        public string CaptorFactionInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public int VictoryForceRank { get; set; }
        public int MinimumFailureInjury { get; set; }
        public int MaximumFailureInjury { get; set; }
        public bool CaptivesCanEscapeOnVictory { get; set; }
        public string DisplayName { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer luke = ResolveOfficer(
                game,
                LukeOfficerInstanceID,
                nameof(LukeOfficerInstanceID)
            );
            Officer vader = ResolveOfficer(
                game,
                VaderOfficerInstanceID,
                nameof(VaderOfficerInstanceID)
            );
            Officer palpatine = ResolveOfficer(
                game,
                PalpatineOfficerInstanceID,
                nameof(PalpatineOfficerInstanceID)
            );

            return new List<GameResult>
            {
                new StoryFinalBattleRequestedResult
                {
                    Luke = luke,
                    Vader = vader,
                    Palpatine = palpatine,
                    CaptorFactionInstanceID = CaptorFactionInstanceID,
                    DurationTicks = DurationTicks,
                    VictoryForceRank = VictoryForceRank,
                    MinimumFailureInjury = MinimumFailureInjury,
                    MaximumFailureInjury = MaximumFailureInjury,
                    CaptivesCanEscapeOnVictory = CaptivesCanEscapeOnVictory,
                    DisplayName = DisplayName,
                    Tick = game.CurrentTick,
                },
            };
        }

        /// <summary>
        /// Resolves a required officer reference for a story action.
        /// </summary>
        private static Officer ResolveOfficer(GameRoot game, string instanceId, string memberName)
        {
            return game.GetSceneNodeByInstanceID<Officer>(instanceId)
                ?? throw new InvalidOperationException(
                    $"StartStoryFinalBattle could not resolve {memberName} '{instanceId}'."
                );
        }
    }

    /// <summary>
    /// Reveals an officer's authored Force potential and initializes its starting value.
    /// </summary>
    [PersistableObject(Name = "RevealOfficerForcePotential")]
    public sealed class RevealOfficerForcePotentialAction : GameAction
    {
        public string OfficerInstanceID { get; set; }
        public bool SuppressRankChangeMessage { get; set; } = true;

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            return Execute(game, provider);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"RevealOfficerForcePotential could not resolve {nameof(OfficerInstanceID)} '{OfficerInstanceID}'."
                );
            if (officer.IsForceEligible)
                return new List<GameResult>();

            int previousRank = officer.ForceRank;
            officer.IsJedi = true;
            officer.IsForceEligible = true;
            int startingValue =
                officer.JediLevel + provider.NextInt(0, officer.JediLevelVariance + 1);
            officer.ForceValue = Math.Max(officer.ForceValue, startingValue);
            return new List<GameResult>
            {
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = Math.Max(0, officer.ForceRank - previousRank),
                    PreviousForceRank = previousRank,
                    CurrentForceRank = officer.ForceRank,
                    SuppressRankChangeMessage = SuppressRankChangeMessage,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Increases one officer's Force value using the greatest configured reward component.
    /// </summary>
    [PersistableObject(Name = "IncreaseOfficerForce")]
    public class IncreaseOfficerForceAction : GameAction
    {
        public string OfficerInstanceID { get; set; }
        public string ReferenceOfficerInstanceID { get; set; }
        public int MinimumIncrease { get; set; }
        public int CurrentRankPercent { get; set; }
        public int PositiveRankGapPercent { get; set; }
        public bool SuppressRankChangeMessage { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = ResolveOfficer(game, OfficerInstanceID, nameof(OfficerInstanceID));
            Officer reference = string.IsNullOrWhiteSpace(ReferenceOfficerInstanceID)
                ? null
                : ResolveOfficer(
                    game,
                    ReferenceOfficerInstanceID,
                    nameof(ReferenceOfficerInstanceID)
                );
            int previousRank = officer.ForceRank;
            int increase = Math.Max(MinimumIncrease, previousRank * CurrentRankPercent / 100);
            if (reference != null)
            {
                int positiveGap = Math.Max(0, reference.ForceRank - previousRank);
                increase = Math.Max(increase, positiveGap * PositiveRankGapPercent / 100);
            }

            officer.ForceValue += increase;
            return new List<GameResult>
            {
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = increase,
                    PreviousForceRank = previousRank,
                    CurrentForceRank = officer.ForceRank,
                    SuppressRankChangeMessage = SuppressRankChangeMessage,
                    Tick = game.CurrentTick,
                },
            };
        }

        /// <summary>
        /// Resolves a required officer reference for a story action.
        /// </summary>
        private static Officer ResolveOfficer(GameRoot game, string instanceId, string memberName)
        {
            return game.GetSceneNodeByInstanceID<Officer>(instanceId)
                ?? throw new InvalidOperationException(
                    $"IncreaseOfficerForce could not resolve {memberName} '{instanceId}'."
                );
        }
    }

    /// <summary>
    /// Applies a data-authored inclusive random injury range to one officer.
    /// </summary>
    [PersistableObject(Name = "ApplyOfficerInjury")]
    public class ApplyOfficerInjuryAction : GameAction
    {
        public string OfficerInstanceID { get; set; }
        public int MinimumInjury { get; set; }
        public int MaximumInjury { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"ApplyOfficerInjury could not resolve officer '{OfficerInstanceID}'."
                );

            int injury = provider.NextInt(MinimumInjury, checked(MaximumInjury + 1));
            officer.ApplyInjury(injury, game.Config.Recovery.MaxInjuryPoints);
            return new List<GameResult>
            {
                new OfficerInjuredResult
                {
                    Officer = officer,
                    Severity = injury,
                    Tick = game.CurrentTick,
                },
            };
        }
    }
}
