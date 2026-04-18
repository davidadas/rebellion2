using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Drives the AI for all AI-controlled factions.
    ///
    /// Each faction gets one HeavyAIWorker (LargeSelectionRecord) which implements
    /// the original game's two-phase execution model:
    ///
    ///   Phase 1 — Startup (AtStartSetupMissions):
    ///     State 1: run GalaxyAnalysisPipeline until complete.
    ///     State 2: create all 14 strategy records; set batch limit; transition to state 4.
    ///
    ///   Phase 2 — Per-tick (Tick):
    ///     State 4: run CalibrationSubMachine on scratchBlock.
    ///     State 5: walk record list, call Reset() on each record to prime it.
    ///     State 3: call Tick() on each active record; dispatch work items.
    ///
    /// Corresponds to the sequence:
    ///   constructor (FUN_00484bf0) → slot 4 (FUN_00484ea0) → slot 9 (FUN_00484f90)
    /// </summary>
    public class AISystem
    {
        private readonly GameRoot _game;
        private readonly MissionSystem _missionManager;
        private readonly MovementSystem _movementManager;
        private readonly ManufacturingSystem _manufacturingManager;
        private readonly IRandomNumberProvider _randomProvider;

        // One HeavyAIWorker per AI-controlled faction, keyed by faction InstanceID.
        private readonly Dictionary<string, HeavyAIWorker> _workers =
            new Dictionary<string, HeavyAIWorker>();

        /// <summary>
        /// Creates a new AISystem.
        /// </summary>
        public AISystem(
            GameRoot game,
            MissionSystem missionManager,
            MovementSystem movementManager,
            ManufacturingSystem manufacturingManager,
            IRandomNumberProvider randomProvider
        )
        {
            _game = game;
            _missionManager = missionManager;
            _movementManager = movementManager;
            _manufacturingManager = manufacturingManager;
            _randomProvider = randomProvider;
        }

        /// <summary>
        /// Advances AI logic for all AI-controlled factions by one tick.
        /// Only runs every TickInterval ticks, matching the original game.
        /// </summary>
        public List<GameResult> ProcessTick()
        {
            if (_game.CurrentTick % _game.Config.AI.TickInterval != 0)
                return new List<GameResult>();

            var aiFactions = _game.Factions.Where(f => f.IsAIControlled()).ToList();
            foreach (Faction faction in aiFactions)
                UpdateFaction(faction);

            return new List<GameResult>();
        }

        /// <summary>
        /// Runs the AI decision cycle for one faction via its HeavyAIWorker.
        ///
        /// On first call for a faction: creates the worker and begins startup.
        /// While in startup: calls AtStartSetupMissions() each tick until done.
        /// After startup complete: calls Tick() each tick (batch work cycle).
        /// </summary>
        private void UpdateFaction(Faction faction)
        {
            if (!_workers.TryGetValue(faction.InstanceID, out HeavyAIWorker worker))
            {
                int ownerSide = GetFactionSide(faction);
                worker = new HeavyAIWorker(
                    faction,
                    ownerSide,
                    _game,
                    _manufacturingManager,
                    _movementManager,
                    _missionManager,
                    _randomProvider
                );
                _workers[faction.InstanceID] = worker;
            }

            if (worker.IsInStartup)
                worker.AtStartSetupMissions();
            else
                worker.Tick();
        }

        // Returns the faction's 0-based index within _game.Factions (its side ID).
        // Rebel = 0, Empire = 1 in the original game's two-faction model.
        private int GetFactionSide(Faction faction)
        {
            int i = 0;
            foreach (Faction f in _game.Factions)
            {
                if (f.InstanceID == faction.InstanceID)
                    return i;
                i++;
            }
            return 0;
        }
    }
}
