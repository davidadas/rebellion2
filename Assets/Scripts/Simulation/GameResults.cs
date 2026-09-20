using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Resolves domain reactions, message deliveries, and presentation notifications.
    /// </summary>
    public sealed class GameResults
    {
        private readonly SimulationFeatures _features;
        private readonly ResultReactions _reactions = new ResultReactions();

        /// <summary>
        /// Raised after a batch and all of its reactions have resolved.
        /// </summary>
        public event Action<IReadOnlyList<GameResult>> Resolved;

        /// <summary>
        /// Raised after planetary assaults have resolved.
        /// </summary>
        public event Action<IReadOnlyList<PlanetaryAssaultResult>> PlanetaryAssaultsResolved;

        /// <summary>
        /// Raised after victory results have resolved.
        /// </summary>
        public event Action<IReadOnlyList<VictoryResult>> VictoriesResolved;

        /// <summary>
        /// Raised when headquarters are lost.
        /// </summary>
        public event Action<HeadquartersLostResult> HeadquartersLost;

        /// <summary>
        /// Raised when victory is declared.
        /// </summary>
        public event Action<VictoryResult> VictoryDeclared;

        /// <summary>
        /// Raised when a message is delivered.
        /// </summary>
        public event Action<MessageDeliveredResult> MessageDelivered;

        /// <summary>
        /// Raised when bombardment resolves.
        /// </summary>
        public event Action<BombardmentResult> BombardmentCompleted;

        /// <summary>
        /// Creates and connects the result pipeline for one feature graph.
        /// </summary>
        /// <param name="features">The session's private feature graph.</param>
        internal GameResults(SimulationFeatures features)
        {
            _features = features ?? throw new ArgumentNullException(nameof(features));
            ConnectReactions();
        }

        /// <summary>
        /// Resolves an initial result batch and all ordered reaction waves.
        /// </summary>
        /// <param name="results">The initial factual results.</param>
        /// <param name="createMessages">Whether resolved facts should generate messages.</param>
        /// <returns>The initial results followed by every reaction result.</returns>
        internal List<GameResult> Resolve(
            IEnumerable<GameResult> results,
            bool createMessages = true
        )
        {
            List<GameResult> resolved = _reactions.Resolve(results);
            if (resolved.Count > 0)
                Resolved?.Invoke(resolved);

            List<PlanetaryAssaultResult> assaults = resolved
                .OfType<PlanetaryAssaultResult>()
                .ToList();
            if (assaults.Count > 0)
                PlanetaryAssaultsResolved?.Invoke(assaults);

            List<VictoryResult> victories = resolved.OfType<VictoryResult>().ToList();
            if (victories.Count > 0)
                VictoriesResolved?.Invoke(victories);

            if (createMessages)
                DeliverMessages(resolved);

            foreach (HeadquartersLostResult result in resolved.OfType<HeadquartersLostResult>())
                HeadquartersLost?.Invoke(result);
            foreach (VictoryResult result in victories)
                VictoryDeclared?.Invoke(result);
            return resolved;
        }

        /// <summary>
        /// Generates messages and drains reactions caused by their delivery.
        /// </summary>
        /// <param name="resolved">The already-resolved batch to extend.</param>
        internal void DeliverMessages(List<GameResult> resolved)
        {
            List<GameResult> pending = new List<GameResult>(resolved);
            while (pending.Count > 0)
            {
                List<GameResult> delivered = _features.Messages.ProcessResults(pending);
                if (delivered.Count == 0)
                    return;

                List<GameResult> reactions = _reactions.Resolve(delivered);
                resolved.AddRange(reactions);
                foreach (
                    MessageDeliveredResult result in delivered.OfType<MessageDeliveredResult>()
                )
                    MessageDelivered?.Invoke(result);
                pending = reactions;
            }
        }

        /// <summary>
        /// Connects ordered result reactions and passive observers.
        /// </summary>
        private void ConnectReactions()
        {
            Subscribe<GameResult>(_features.Events);
            Subscribe<BlockadeChangedResult>(_features.Movement);
            Subscribe<UnitArrivedResult>(_features.Headquarters);
            Subscribe<PlanetOwnershipChangedResult>(_features.Headquarters);
            Subscribe<PlanetOwnershipChangedResult>(_features.OfficerLoyalty);
            Subscribe<PlanetOwnershipChangedResult>(_features.Captives);
            Subscribe<HeadquartersLostResult>(_features.Victory);
            Subscribe<PlanetGarrisonChangedResult>(_features.PlanetaryControl);
            Subscribe<PopularSupportShiftResult>(_features.PlanetaryControl);
            Subscribe<PlanetGarrisonChangedResult>(_features.Uprisings);
            Subscribe<MissionCompletedResult>(_features.Jedi);
            Subscribe<OfficerCaptureStateResult>(_features.Missions);
            Subscribe<OfficerCaptureStateResult>(_features.Captives);
            Subscribe<IntelligenceRevealedResult>(_features.FogOfWar);
            Subscribe<GameObjectDestroyedResult>(_features.Manufacturing);
            Subscribe<GameObjectScrappedResult>(_features.Manufacturing);
            Subscribe<BombardmentResult>(_features.Manufacturing);
            Subscribe<PlanetaryAssaultResult>(_features.Manufacturing);
            Observe<GameObjectSabotagedResult>(_features.FogOfWar.ProcessResults);

            _features.Movement.ResultsProduced += HandleImmediateResults;
            _features.Maintenance.ResultsProduced += HandleImmediateResults;
            _features.Bombardment.ResultsProduced += HandleImmediateResults;
            _features.PlanetaryAssault.ResultsProduced += HandleImmediateResults;
        }

        /// <summary>
        /// Resolves facts emitted by an immediate gameplay command.
        /// </summary>
        /// <param name="results">The command's factual results.</param>
        private void HandleImmediateResults(IReadOnlyList<GameResult> results)
        {
            List<GameResult> resolved = Resolve(results);
            foreach (BombardmentResult result in resolved.OfType<BombardmentResult>())
                BombardmentCompleted?.Invoke(result);
        }

        /// <summary>
        /// Registers one ordered reaction handler.
        /// </summary>
        /// <typeparam name="T">The factual result type handled.</typeparam>
        /// <param name="handler">The handler that may produce follow-up facts.</param>
        private void Subscribe<T>(IGameResultHandler<T> handler)
            where T : GameResult
        {
            _reactions.Subscribe(handler);
        }

        /// <summary>
        /// Registers one passive result observer.
        /// </summary>
        /// <typeparam name="T">The factual result type observed.</typeparam>
        /// <param name="observer">The observer to notify after reaction processing.</param>
        private void Observe<T>(Action<IReadOnlyList<T>> observer)
            where T : GameResult
        {
            _reactions.Observe(observer);
        }
    }
}
