using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Selects intelligence observations and sabotage invalidations from completed results.
    /// </summary>
    public sealed class FogOfWarObserver
    {
        private readonly GameRoot _game;
        private readonly FogOfWarCommands _commands;

        /// <summary>
        /// Connects result observation to the commands that update faction intelligence.
        /// </summary>
        /// <param name="game">The game containing the observing factions.</param>
        /// <param name="commands">The commands that record and invalidate observations.</param>
        public FogOfWarObserver(GameRoot game, FogOfWarCommands commands)
        {
            _game = game;
            _commands = commands;
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
        /// Applies fog-of-war side effects for a result batch.
        /// </summary>
        /// <param name="results">The game results to process.</param>
        public void ProcessResults(IReadOnlyList<GameObjectSabotagedResult> results)
        {
            foreach (GameObjectSabotagedResult result in results)
                RemoveSabotagedObjectFromActorSnapshot(result);
        }

        /// <summary>
        /// Removes a sabotaged object from the actor faction's fog-of-war snapshots.
        /// </summary>
        /// <param name="result">The sabotage result to process.</param>
        private void RemoveSabotagedObjectFromActorSnapshot(GameObjectSabotagedResult result)
        {
            if (result?.DestroyedObject == null || result.DestroyedBy is not ISceneNode saboteur)
                return;

            Faction faction = _game
                .GetFactions()
                .FirstOrDefault(f => f.InstanceID == saboteur.GetOwnerInstanceID());
            if (faction == null)
                return;

            _commands.RemoveEntityFromSnapshots(faction, result.DestroyedObject.GetInstanceID());
        }
    }
}
