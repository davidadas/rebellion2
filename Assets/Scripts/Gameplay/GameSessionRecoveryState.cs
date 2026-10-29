using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures a writable simulation boundary and the runtime facts required to continue it.
    /// References in this envelope belong to one object graph and must retain their identity.
    /// </summary>
    [PersistableObject]
    internal sealed class GameSessionRecoveryState
    {
        public GameRoot Game { get; set; }
        public bool UseDeterministicRuntimeIdentifiers { get; set; }
        public int RandomSeed { get; set; }
        public GameTickRecoveryState Tick { get; set; }
        public SpaceCombatDecision PendingCombat { get; set; }
        public PlanetaryControlRecoveryState PlanetaryControl { get; set; }
    }

    /// <summary>
    /// Identifies the writable continuation points of the existing tick scheduler.
    /// </summary>
    internal enum GameTickRecoveryPhase
    {
        Idle,
        AwaitingCombatDecision,
    }

    /// <summary>
    /// Preserves the scheduler's release boundaries without serializing an executing iterator.
    /// </summary>
    [PersistableObject]
    internal sealed class GameTickRecoveryState
    {
        public GameTickRecoveryPhase Phase { get; set; }
        public List<GameResult> DeferredMessageResults { get; set; }
        public List<BlockadeChangedResult> DeferredBlockadeResults { get; set; }
    }

    /// <summary>
    /// Preserves the factions whose control-loss support shift was already applied in the current tick.
    /// </summary>
    [PersistableObject]
    internal sealed class PlanetaryControlRecoveryState
    {
        public int ControlShiftTick { get; set; }
        public List<string> ControlShiftedOwners { get; set; }
    }
}
