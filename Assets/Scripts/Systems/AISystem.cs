using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Util.Random;

namespace Rebellion.Systems
{
    /// <summary>
    /// Runs AI faction turns after world systems finish each tick.
    /// </summary>
    public class AISystem
    {
        private readonly GameRoot _game;
        private readonly FogOfWarSystem _fogOfWar;
        private readonly AIDirector _director;

        /// <summary>
        /// Raised immediately before one faction's strategic AI turn begins.
        /// </summary>
        public event Action<Faction> FactionTurnStarted;

        /// <summary>
        /// Raised after one faction's strategic AI turn finishes or is interrupted.
        /// </summary>
        public event Action<Faction> FactionTurnCompleted;

        /// <summary>
        /// Raised immediately before one named unit of faction-turn work begins.
        /// </summary>
        public event Action<Faction, string> FactionTurnStepStarted;

        /// <summary>
        /// Raised after one named unit of faction-turn work finishes or is interrupted.
        /// </summary>
        public event Action<Faction, string> FactionTurnStepCompleted;

        /// <summary>
        /// Creates an AI system.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="missionManager">The mission system.</param>
        /// <param name="movementManager">The movement system.</param>
        /// <param name="manufacturingManager">The manufacturing system.</param>
        /// <param name="bombardmentSystem">The bombardment system.</param>
        /// <param name="planetaryAssaultSystem">The planetary-assault system.</param>
        /// <param name="randomProvider">Random number provider for AI choices.</param>
        /// <param name="fogOfWarManager">Fog-of-war system used to limit AI knowledge.</param>
        /// <param name="maintenanceSystem">Maintenance system used to project production capacity.</param>
        public AISystem(
            GameRoot game,
            MissionSystem missionManager,
            MovementSystem movementManager,
            ManufacturingSystem manufacturingManager,
            BombardmentSystem bombardmentSystem,
            PlanetaryAssaultSystem planetaryAssaultSystem,
            IRandomNumberProvider randomProvider,
            FogOfWarSystem fogOfWarManager,
            MaintenanceSystem maintenanceSystem = null
        )
        {
            _game = game;
            _fogOfWar = fogOfWarManager;
            _director = new AIDirector(
                game,
                missionManager,
                movementManager,
                manufacturingManager,
                bombardmentSystem,
                planetaryAssaultSystem,
                randomProvider,
                maintenanceSystem
            );
            _director.FactionTurnStepStarted += (faction, stepName) =>
                FactionTurnStepStarted?.Invoke(faction, stepName);
            _director.FactionTurnStepCompleted += (faction, stepName) =>
                FactionTurnStepCompleted?.Invoke(faction, stepName);
        }

        /// <summary>
        /// Processes AI turns for all AI-controlled factions.
        /// </summary>
        /// <returns>The results produced by AI actions.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            foreach (object _ in ProcessTickIncrementally(results)) { }

            return results;
        }

        /// <summary>
        /// Processes eligible AI factions one phase at a time.
        /// </summary>
        /// <param name="results">The result list populated as faction turns complete.</param>
        /// <returns>A sequence containing one step per completed AI phase.</returns>
        internal IEnumerable<object> ProcessTickIncrementally(ICollection<GameResult> results)
        {
            int tickInterval = _game.Config.AI.TickInterval;
            if (tickInterval <= 0 || _game.CurrentTick % tickInterval != 0)
                yield break;

            foreach (Faction faction in _game.GetFactions().Where(_game.IsFactionAIControlled))
            {
                FactionTurnStarted?.Invoke(faction);
                try
                {
                    GalaxyMap factionView = _fogOfWar.BuildFactionView(faction);
                    foreach (
                        object step in _director.ProcessFactionIncrementally(
                            faction,
                            factionView,
                            results
                        )
                    )
                        yield return step;
                }
                finally
                {
                    FactionTurnCompleted?.Invoke(faction);
                }
            }
        }
    }
}
