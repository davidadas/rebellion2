using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Applies completed-result knowledge changes to faction intelligence.
    /// </summary>
    public sealed class FogOfWarObserver : IResultObserver, IDisposable
    {
        private readonly GameRoot _game;
        private readonly FogOfWarCommands _commands;
        private readonly FogOfWarQueries _queries;
        private IDisposable[] _subscriptions;

        /// <summary>
        /// Creates the observer that updates faction intelligence.
        /// </summary>
        /// <param name="game">The game containing the observing factions.</param>
        /// <param name="commands">The commands that record and invalidate observations.</param>
        /// <param name="queries">The visibility rules used to identify informed factions.</param>
        public FogOfWarObserver(GameRoot game, FogOfWarCommands commands, FogOfWarQueries queries)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>Registers the settled-result callback with the result bus.</summary>
        /// <param name="results">The bus that delivers completed game results.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscriptions != null)
                throw new InvalidOperationException("Fog of war observer is already connected.");
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[] { results.Observe<GameResult>(ProcessResults) };
        }

        /// <summary>Stops receiving completed game results.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
                subscription.Dispose();
        }

        /// <summary>
        /// Applies category-limited planet intelligence emitted by a simulation event.
        /// </summary>
        /// <param name="results">The intelligence results to record.</param>
        /// <returns>No reactions; snapshots are updated directly.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<IntelligenceRevealedResult> results)
        {
            foreach (IntelligenceRevealedResult result in results)
                _commands.RecordObservations(result.Recipient, result.Observations, result.Tick);

            return new List<GameResult>();
        }

        /// <summary>
        /// Applies completed game results to the knowledge of factions informed by those results.
        /// </summary>
        /// <param name="results">The game results to process.</param>
        public void ProcessResults(IReadOnlyList<GameResult> results)
        {
            if (results == null)
                return;

            HandleResults(results.OfType<IntelligenceRevealedResult>().ToList());

            foreach (
                GameObjectDestroyedResult result in results.OfType<GameObjectDestroyedResult>()
            )
                RecordKnownDestruction(result);
            foreach (OfficerKilledResult result in results.OfType<OfficerKilledResult>())
                RecordKnownOfficerDeath(result);
            foreach (SpaceCombatResult result in results.OfType<SpaceCombatResult>())
                RecordCombatLosses(
                    result.AttackerOwnerInstanceID,
                    result.DefenderOwnerInstanceID,
                    result.AttackingUnits.Concat(result.DefendingUnits)
                );
            foreach (BombardmentResult result in results.OfType<BombardmentResult>())
                RecordCombatLosses(
                    result.AttackerOwnerInstanceID,
                    result.DefenderOwnerInstanceID,
                    result.AttackingUnits.Concat(result.DefendingUnits)
                );
            foreach (PlanetaryAssaultResult result in results.OfType<PlanetaryAssaultResult>())
                RecordCombatLosses(
                    result.AttackerOwnerInstanceID,
                    result.DefenderOwnerInstanceID,
                    result.AttackingUnits.Concat(result.DefendingUnits)
                );
            foreach (EvacuationLossesResult result in results.OfType<EvacuationLossesResult>())
                RecordEvacuationLosses(result);
        }

        /// <summary>
        /// Removes a destroyed object from the knowledge of informed factions.
        /// </summary>
        /// <param name="result">The destruction result to process.</param>
        private void RecordKnownDestruction(GameObjectDestroyedResult result)
        {
            if (result?.DestroyedObject == null)
                return;

            string entityId = result.DestroyedObject.GetInstanceID();
            if (string.IsNullOrEmpty(entityId))
                return;

            foreach (
                Faction faction in GetInformedFactions(
                    result.DestroyedObject,
                    result.DestroyedBy,
                    result.Context
                )
            )
            {
                _commands.RemoveEntityFromSnapshots(faction, entityId);
            }
        }

        /// <summary>
        /// Removes a dead officer from the knowledge of factions informed of the death.
        /// </summary>
        /// <param name="result">The officer death result.</param>
        private void RecordKnownOfficerDeath(OfficerKilledResult result)
        {
            if (result?.TargetOfficer == null)
                return;

            foreach (
                Faction faction in GetInformedFactions(
                    result.TargetOfficer,
                    result.Assassin,
                    result.Context
                )
            )
            {
                _commands.RemoveEntityFromSnapshots(faction, result.TargetOfficer.InstanceID);
            }
        }

        /// <summary>
        /// Removes combatants confirmed destroyed to both participating factions.
        /// </summary>
        /// <param name="attackerFactionId">The attacking faction identifier.</param>
        /// <param name="defenderFactionId">The defending faction identifier.</param>
        /// <param name="units">The detached combat unit outcomes.</param>
        private void RecordCombatLosses(
            string attackerFactionId,
            string defenderFactionId,
            IEnumerable<CombatUnitSnapshot> units
        )
        {
            List<Faction> informedFactions = new[]
            {
                FindFaction(attackerFactionId),
                FindFaction(defenderFactionId),
            }
                .Where(faction => faction != null)
                .Distinct()
                .ToList();
            foreach (
                string entityId in (units ?? Enumerable.Empty<CombatUnitSnapshot>())
                    .Where(unit => unit?.Destroyed == true)
                    .Select(unit => unit.Unit?.InstanceID)
                    .Where(entityId => !string.IsNullOrEmpty(entityId))
                    .Distinct()
            )
            {
                foreach (Faction faction in informedFactions)
                    _commands.RemoveEntityFromSnapshots(faction, entityId);
            }
        }

        /// <summary>
        /// Removes evacuation casualties from the affected faction's remembered state.
        /// </summary>
        /// <param name="result">The evacuation-loss result.</param>
        private void RecordEvacuationLosses(EvacuationLossesResult result)
        {
            if (result?.Faction == null)
                return;

            IEnumerable<ISceneNode> lostUnits = result
                .LostShips.Cast<ISceneNode>()
                .Concat(result.LostStarfighters)
                .Concat(result.LostRegiments);
            foreach (ISceneNode lostUnit in lostUnits.Where(unit => unit != null))
                _commands.RemoveEntityFromSnapshots(result.Faction, lostUnit.InstanceID);
        }

        /// <summary>
        /// Gets every faction that necessarily knows about a completed entity destruction.
        /// </summary>
        /// <param name="destroyedObject">The destroyed object.</param>
        /// <param name="destroyedBy">The object responsible for the destruction.</param>
        /// <param name="context">The location context of the destruction.</param>
        /// <returns>The informed factions.</returns>
        private HashSet<Faction> GetInformedFactions(
            IGameEntity destroyedObject,
            IGameEntity destroyedBy,
            IGameEntity context
        )
        {
            HashSet<Faction> factions = new HashSet<Faction>();
            AddFaction(factions, (destroyedObject as ISceneNode)?.GetOwnerInstanceID());
            AddFaction(factions, (destroyedBy as ISceneNode)?.GetOwnerInstanceID());

            ISceneNode contextNode = context as ISceneNode;
            Planet planet = contextNode as Planet ?? contextNode?.GetParentOfType<Planet>();
            if (planet != null)
            {
                foreach (
                    Faction faction in _game
                        .GetFactions()
                        .Where(faction => _queries.IsPlanetVisible(planet, faction))
                )
                {
                    factions.Add(faction);
                }
            }

            return factions;
        }

        /// <summary>
        /// Adds a faction to a set when its identifier resolves in the active game.
        /// </summary>
        /// <param name="factions">The destination set.</param>
        /// <param name="factionId">The faction identifier.</param>
        private void AddFaction(ISet<Faction> factions, string factionId)
        {
            Faction faction = FindFaction(factionId);
            if (faction != null)
                factions.Add(faction);
        }

        /// <summary>
        /// Resolves a faction without throwing when an optional owner identifier is absent.
        /// </summary>
        /// <param name="factionId">The faction identifier.</param>
        /// <returns>The matching faction, or null.</returns>
        private Faction FindFaction(string factionId)
        {
            return string.IsNullOrEmpty(factionId)
                ? null
                : _game.GetFactions().FirstOrDefault(faction => faction.InstanceID == factionId);
        }
    }
}
