using Rebellion.Core.Simulation;
using Rebellion.Game;

namespace Rebellion.Generation
{
    /// <summary>
    ///
    /// </summary>
    public class GameEventGenerator : UnitGenerator<GameEvent>
    {
        /// <summary>
        /// Constructs an EventGenerator object.
        /// </summary>
        /// <param name="summary">The GameSummary options selected by the player.</param>
        /// <param name="resourceManager">The resource manager from which to load game data.</param>
        /// <param name="randomProvider">Random number provider for deterministic generation.</param>
        public GameEventGenerator(
            GameSummary summary,
            IResourceManager resourceManager,
            IRandomNumberProvider randomProvider
        )
            : base(summary, resourceManager, randomProvider) { }

        /// <summary>
        ///
        /// </summary>
        /// <param name="events"></param>
        /// <returns></returns>
        public override GameEvent[] DecorateUnits(GameEvent[] events)
        {
            return events;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="events"></param>
        /// <param name="destinations"></param>
        /// <returns></returns>
        public override GameEvent[] DeployUnits(GameEvent[] events, PlanetSystem[] destinations)
        {
            return events;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="events"></param>
        /// <returns></returns>
        public override GameEvent[] SelectUnits(GameEvent[] events)
        {
            return events;
        }
    }
}
