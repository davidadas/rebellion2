using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects the comparison applied to a persistent event variable.
    /// </summary>
    public enum EventVariableComparison
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }

    /// <summary>
    /// Selects one boolean officer state for a data-defined condition.
    /// </summary>
    public enum OfficerStateKind
    {
        Available,
        Captured,
        Killed,
        Injured,
        ForceEligible,
    }

    /// <summary>
    /// Tests whether an authored planet has any owner or one specific faction owner.
    /// </summary>
    [PersistableObject(Name = "IsOwned")]
    public sealed class IsOwnedConditional : GameConditional
    {
        [PersistableAttribute(Name = "PlanetInstanceID")]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute(Name = "FactionInstanceID")]
        public string FactionInstanceID { get; set; }

        public override bool IsMet(GameRoot game)
        {
            Planet planet = game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
            if (planet?.IsDestroyed != false)
                return false;

            Faction owner = game.GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == planet.OwnerInstanceID);
            return owner != null
                && (
                    string.IsNullOrWhiteSpace(FactionInstanceID)
                    || owner.InstanceID == FactionInstanceID
                );
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when all child conditions are met.
    /// </summary>
    [PersistableObject(Name = "And")]
    public class AndConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public AndConditional()
            : base() { }

        /// <summary>
        /// Evaluates the AND composition: all child conditions must be met.
        /// </summary>
        /// <param name="game">The game state to evaluate against.</param>
        /// <returns>True if every child condition is met; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            return Conditionals.All(conditional => conditional.IsMet(game));
        }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return Conditionals.All(conditional => conditional.IsMet(game, triggerResult));
        }

        public override bool IsMet(GameRoot game, GameEventExecutionContext context) =>
            Conditionals.All(conditional => conditional.IsMet(game, context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when any child condition is met.
    /// </summary>
    [PersistableObject(Name = "Or")]
    public class OrConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public OrConditional()
            : base() { }

        /// <summary>
        /// Evaluates the OR composition: at least one child condition must be met.
        /// </summary>
        /// <param name="game">The game state to evaluate against.</param>
        /// <returns>True if any child condition is met; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            return Conditionals.Any(conditional => conditional.IsMet(game));
        }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return Conditionals.Any(conditional => conditional.IsMet(game, triggerResult));
        }

        public override bool IsMet(GameRoot game, GameEventExecutionContext context) =>
            Conditionals.Any(conditional => conditional.IsMet(game, context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when none of the child conditions are met.
    /// </summary>
    [PersistableObject(Name = "Not")]
    public class NotConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public NotConditional()
            : base() { }

        /// <summary>
        /// Evaluates the NOT composition: no child condition may be met.
        /// </summary>
        /// <param name="game">The game state to evaluate against.</param>
        /// <returns>True if every child condition is unmet; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            return Conditionals.All(conditional => !conditional.IsMet(game));
        }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return Conditionals.All(conditional => !conditional.IsMet(game, triggerResult));
        }

        public override bool IsMet(GameRoot game, GameEventExecutionContext context) =>
            Conditionals.All(conditional => !conditional.IsMet(game, context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when exactly one child condition is met.
    /// </summary>
    [PersistableObject(Name = "Xor")]
    public class XorConditional : GameConditional
    {
        [PersistableMember(Name = "Conditionals")]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public XorConditional()
            : base() { }

        /// <summary>
        /// Evaluates the XOR composition: exactly one child condition must be met.
        /// </summary>
        /// <param name="game">The game state to evaluate against.</param>
        /// <returns>True if precisely one child condition is met; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            return Conditionals.Count(conditional => conditional.IsMet(game)) == 1;
        }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return Conditionals.Count(conditional => conditional.IsMet(game, triggerResult)) == 1;
        }

        public override bool IsMet(GameRoot game, GameEventExecutionContext context) =>
            Conditionals.Count(conditional => conditional.IsMet(game, context)) == 1;
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when all specified units are located on the same planet.
    /// </summary>
    [PersistableObject(Name = "AreOnSamePlanet")]
    public class AreOnSamePlanetConditional : GameConditional
    {
        [PersistableMember(Name = "UnitInstanceIDs")]
        public List<string> UnitInstanceIDs { get; set; }

        public AreOnSamePlanetConditional()
            : base() { }

        /// <summary>
        /// Checks whether every referenced unit is parented to the same planet.
        /// </summary>
        /// <param name="game">The game state used to resolve unit references.</param>
        /// <returns>True if all referenced units share a planet parent; false if any are missing or on a different planet.</returns>
        public override bool IsMet(GameRoot game)
        {
            List<ISceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(UnitInstanceIDs);
            if (sceneNodes.Count != UnitInstanceIDs.Count)
                return false;

            Planet comparator = null;

            // Check if all units are on the same planet.
            foreach (ISceneNode node in sceneNodes)
            {
                if (node == null)
                {
                    return false;
                }

                Planet planet = node.GetParentOfType<Planet>();
                comparator ??= planet;

                if (comparator != planet)
                {
                    return false;
                }
            }

            return comparator != null;
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when exactly two units belong to different factions.
    /// </summary>
    [PersistableObject(Name = "AreOnOpposingFactions")]
    public class AreOnOpposingFactionsConditional : GameConditional
    {
        [PersistableMember(Name = "UnitInstanceIDs")]
        public List<string> UnitInstanceIDs { get; set; } = new List<string>();

        public AreOnOpposingFactionsConditional()
            : base() { }

        /// <summary>
        /// Checks whether the two referenced units belong to different owners.
        /// </summary>
        /// <param name="game">The game state used to resolve unit references.</param>
        /// <returns>True if exactly two units are referenced and their owner instance IDs differ.</returns>
        public override bool IsMet(GameRoot game)
        {
            // Get the scene nodes for the units.
            List<ISceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(UnitInstanceIDs);

            // Check if the units are on opposing factions.
            return sceneNodes.Count == 2
                && sceneNodes[0].OwnerInstanceID != sceneNodes[1].OwnerInstanceID;
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when the specified unit is currently assigned to a mission.
    /// </summary>
    [PersistableObject(Name = "IsOnMission")]
    public class IsOnMissionConditional : GameConditional
    {
        public IsOnMissionConditional()
            : base() { }

        /// <summary>
        /// Checks whether the referenced unit is parented to a <see cref="Mission"/> node.
        /// </summary>
        /// <param name="game">The game state used to resolve the unit.</param>
        /// <returns>True if the unit exists and its direct parent is a mission; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            string instanceId = this.GetConditionalValue();
            ISceneNode sceneNode = game.GetSceneNodeByInstanceID<ISceneNode>(instanceId);
            // Check if the unit is on a mission.
            return sceneNode?.GetParent() is Mission;
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when the specified unit implements <see cref="IMovable"/> and is currently movable.
    /// </summary>
    [PersistableObject(Name = "IsMovable")]
    public class IsMovableConditional : GameConditional
    {
        public IsMovableConditional()
            : base() { }

        /// <summary>
        /// Checks whether the referenced unit implements <see cref="IMovable"/> and is currently free to move.
        /// </summary>
        /// <param name="game">The game state used to resolve the unit.</param>
        /// <returns>True if the unit is resolvable, movable, and not currently in transit; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            string instanceId = this.GetConditionalValue();
            ISceneNode sceneNode = game.GetSceneNodeByInstanceID<ISceneNode>(instanceId);

            // Check if the ISceneNode implements IMovable and is movable.
            if (sceneNode is IMovable movable)
            {
                return movable.IsMovable();
            }

            return false;
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when all specified units are located on any planet.
    /// </summary>
    [PersistableObject(Name = "AreOnPlanet")]
    public class AreOnPlanetConditional : GameConditional
    {
        public List<string> UnitInstanceIDs { get; set; }

        public AreOnPlanetConditional()
            : base() { }

        /// <summary>
        /// Checks whether every referenced unit has a planet somewhere in its ancestry.
        /// </summary>
        /// <param name="game">The game state used to resolve unit references.</param>
        /// <returns>True if every referenced unit is on some planet; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            // Get the instance IDs of the units to check.
            List<ISceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(UnitInstanceIDs);

            // Check if all units are on a planet.
            return sceneNodes.All(node => node.GetParentOfType<Planet>() != null);
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when the current tick count satisfies a comparison against a target value.
    /// </summary>
    [PersistableObject(Name = "TickCount")]
    public class TickCountConditional : GameConditional
    {
        private enum ComparisonType
        {
            EqualTo,
            GreaterThan,
            LessThan,
        }

        /// <summary>
        /// Creates an empty tick condition for content deserialization.
        /// </summary>
        public TickCountConditional()
            : base() { }

        /// <summary>
        /// Compares the current tick against the stored target value using the comparison
        /// type selected by <see cref="GameConditional.GetConditionalType"/>. Unknown types fall back to EqualTo.
        /// </summary>
        /// <param name="game">The game state providing the current tick.</param>
        /// <returns>True when the tick comparison holds; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            ComparisonType comparison = Enum.TryParse(
                this.GetConditionalType(),
                out ComparisonType result
            )
                ? result
                : ComparisonType.EqualTo;

            return comparison switch
            {
                ComparisonType.EqualTo => game.CurrentTick
                    == Convert.ToInt32(this.GetConditionalValue()),
                ComparisonType.GreaterThan => game.CurrentTick
                    > Convert.ToInt32(this.GetConditionalValue()),
                ComparisonType.LessThan => game.CurrentTick
                    < Convert.ToInt32(this.GetConditionalValue()),
                _ => throw new InvalidOperationException(
                    $"Invalid comparison type \"{comparison}\" for TickCountConditional."
                ),
            };
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when the specified game event has been completed.
    /// </summary>
    [PersistableObject(Name = "IsEventComplete")]
    public class IsEventCompleteConditional : GameConditional
    {
        public IsEventCompleteConditional()
            : base() { }

        /// <summary>
        /// Checks whether the event with the configured instance ID has been marked complete.
        /// </summary>
        /// <param name="game">The game state tracking completed events.</param>
        /// <returns>True if the event is complete; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            string eventInstanceId = this.GetConditionalValue();

            // Check if the event is complete.
            return game.IsEventComplete(eventInstanceId);
        }
    }

    /// <summary>
    /// Compares a persistent, data-defined event variable with an authored value.
    /// </summary>
    [PersistableObject(Name = "EventVariable")]
    public class EventVariableConditional : GameConditional
    {
        public string Key { get; set; }
        public EventVariableComparison Comparison { get; set; }
        public int Value { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            int current = game.GetEventVariable(Key);
            return Comparison switch
            {
                EventVariableComparison.Equal => current == Value,
                EventVariableComparison.NotEqual => current != Value,
                EventVariableComparison.GreaterThan => current > Value,
                EventVariableComparison.GreaterThanOrEqual => current >= Value,
                EventVariableComparison.LessThan => current < Value,
                EventVariableComparison.LessThanOrEqual => current <= Value,
                _ => throw new InvalidOperationException(
                    $"Unsupported event variable comparison '{Comparison}'."
                ),
            };
        }
    }

    /// <summary>
    /// Tests whether a scene node is contained by a specific location node.
    /// </summary>
    [PersistableObject(Name = "IsAtLocation")]
    public class IsAtLocationConditional : GameConditional
    {
        public string UnitInstanceID { get; set; }
        public string LocationInstanceID { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            ISceneNode location = game.GetSceneNodeByInstanceID<ISceneNode>(LocationInstanceID);
            for (ISceneNode current = unit; current != null; current = current.GetParent())
            {
                if (current == location)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Matches the ordered participants of the officer encounter that triggered an event.
    /// </summary>
    [PersistableObject(Name = "OfficerEncounterParticipants")]
    public class OfficerEncounterParticipantsConditional : GameConditional
    {
        public string EncounteredOfficerInstanceID { get; set; }
        public string OpposingOfficerInstanceID { get; set; }
        public bool MatchEitherOrder { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            return false;
        }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            if (triggerResult is not OfficerEncounterResult encounter)
                return false;

            bool authoredOrder =
                encounter.EncounteredOfficer?.InstanceID == EncounteredOfficerInstanceID
                && encounter.OpposingOfficer?.InstanceID == OpposingOfficerInstanceID;
            return authoredOrder
                || (
                    MatchEitherOrder
                    && encounter.EncounteredOfficer?.InstanceID == OpposingOfficerInstanceID
                    && encounter.OpposingOfficer?.InstanceID == EncounteredOfficerInstanceID
                );
        }
    }

    /// <summary>
    /// Matches an arrival containing exactly one member of an authored officer pair.
    /// </summary>
    [PersistableObject(Name = "OfficerPairArrival")]
    public sealed class OfficerPairArrivalConditional : GameConditional
    {
        public string FirstOfficerInstanceID { get; set; }
        public string SecondOfficerInstanceID { get; set; }

        public override bool IsMet(GameRoot game) => false;

        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            if (
                triggerResult is not UnitArrivedResult arrival
                || arrival.Unit is not ISceneNode unit
            )
                return false;

            Officer first = game.GetSceneNodeByInstanceID<Officer>(FirstOfficerInstanceID);
            Officer second = game.GetSceneNodeByInstanceID<Officer>(SecondOfficerInstanceID);
            bool firstArrived =
                unit == first || unit.GetChildren<Officer>(officer => officer == first).Any();
            bool secondArrived =
                unit == second || unit.GetChildren<Officer>(officer => officer == second).Any();
            return firstArrived != secondArrived;
        }
    }

    /// <summary>
    /// Matches an authored unit contained by an arrival at an authored destination.
    /// </summary>
    [PersistableObject(Name = "UnitArrival")]
    public sealed class UnitArrivalConditional : GameConditional
    {
        public string UnitInstanceID { get; set; }
        public string DestinationInstanceID { get; set; }

        public override bool IsMet(GameRoot game) => false;

        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            if (
                triggerResult is not UnitArrivedResult arrival
                || arrival.Unit is not ISceneNode arrivingUnit
                || arrival.Destination?.InstanceID != DestinationInstanceID
            )
                return false;

            ISceneNode expectedUnit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            return arrivingUnit == expectedUnit
                || arrivingUnit.GetChildren<ISceneNode>(node => node == expectedUnit).Any();
        }
    }

    /// <summary>
    /// Matches an authored officer and capture state on the result that triggered an event.
    /// </summary>
    [PersistableObject(Name = "OfficerCaptureState")]
    public sealed class OfficerCaptureStateConditional : GameConditional
    {
        public string OfficerInstanceID { get; set; }
        public bool IsCaptured { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game) => false;

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return triggerResult is OfficerCaptureStateResult capture
                && capture.TargetOfficer?.InstanceID == OfficerInstanceID
                && capture.IsCaptured == IsCaptured;
        }
    }

    /// <summary>
    /// Matches the authored event that produced the result currently triggering an event.
    /// </summary>
    [PersistableObject(Name = "ResultSourceEvent")]
    public sealed class ResultSourceEventConditional : GameConditional
    {
        public string SourceEventInstanceID { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game) => false;

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return triggerResult?.SourceEventInstanceID == SourceEventInstanceID;
        }
    }

    /// <summary>
    /// Matches the target and outcome of a content-authored capture attempt.
    /// </summary>
    [PersistableObject(Name = "StoryCaptureOutcome")]
    public sealed class StoryCaptureOutcomeConditional : GameConditional
    {
        public string TargetOfficerInstanceID { get; set; }
        public bool WasCaptured { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game) => false;

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return triggerResult is StoryCaptureResolvedResult capture
                && capture.Target?.InstanceID == TargetOfficerInstanceID
                && capture.WasCaptured == WasCaptured;
        }
    }

    /// <summary>
    /// Matches the collector that completed a story prisoner pickup.
    /// </summary>
    [PersistableObject(Name = "StoryPickupCollector")]
    public sealed class StoryPickupCollectorConditional : GameConditional
    {
        public string CollectorOfficerInstanceID { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game) => false;

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return triggerResult is StoryPickupCompletedResult pickup
                && pickup.Collector?.InstanceID == CollectorOfficerInstanceID;
        }
    }

    /// <summary>
    /// Matches the authored outcome of the scripted final battle.
    /// </summary>
    [PersistableObject(Name = "StoryFinalBattleOutcome")]
    public sealed class StoryFinalBattleOutcomeConditional : GameConditional
    {
        public bool LukeVictorious { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game) => false;

        /// <inheritdoc />
        public override bool IsMet(GameRoot game, GameResult triggerResult) =>
            triggerResult is StoryFinalBattleCompletedResult finalBattle
            && finalBattle.LukeVictorious == LukeVictorious;
    }

    /// <summary>
    /// Tests a data-selected runtime state on one officer.
    /// </summary>
    [PersistableObject(Name = "OfficerState")]
    public class OfficerStateConditional : GameConditional
    {
        public string OfficerInstanceID { get; set; }
        public OfficerStateKind State { get; set; }
        public bool Expected { get; set; } = true;

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                return false;

            bool current = State switch
            {
                OfficerStateKind.Available => !officer.IsKilled && !officer.IsCaptured,
                OfficerStateKind.Captured => officer.IsCaptured,
                OfficerStateKind.Killed => officer.IsKilled,
                OfficerStateKind.Injured => officer.InjuryPoints > 0,
                OfficerStateKind.ForceEligible => officer.IsForceEligible,
                _ => throw new InvalidOperationException($"Unsupported officer state '{State}'."),
            };
            return current == Expected;
        }
    }

    /// <summary>
    /// Tests which faction currently holds a captured officer.
    /// </summary>
    [PersistableObject(Name = "OfficerCaptor")]
    public sealed class OfficerCaptorConditional : GameConditional
    {
        public string OfficerInstanceID { get; set; }
        public string FactionInstanceID { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            return officer?.IsCaptured == true && officer.CaptorInstanceID == FactionInstanceID;
        }
    }

    /// <summary>
    /// Compares one officer's effective Force rank with an authored threshold.
    /// </summary>
    [PersistableObject(Name = "OfficerForceRank")]
    public class OfficerForceRankConditional : GameConditional
    {
        public string OfficerInstanceID { get; set; }
        public EventVariableComparison Comparison { get; set; }
        public int Value { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                return false;

            int current = officer.ForceRank;
            return Comparison switch
            {
                EventVariableComparison.Equal => current == Value,
                EventVariableComparison.NotEqual => current != Value,
                EventVariableComparison.GreaterThan => current > Value,
                EventVariableComparison.GreaterThanOrEqual => current >= Value,
                EventVariableComparison.LessThan => current < Value,
                EventVariableComparison.LessThanOrEqual => current <= Value,
                _ => throw new InvalidOperationException(
                    $"Unsupported Force-rank comparison '{Comparison}'."
                ),
            };
        }
    }
}
