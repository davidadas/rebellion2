using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Util.Serialization;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures a completed world and the runtime settings required to reopen it.
    /// References in this envelope belong to one object graph and must retain their identity.
    /// </summary>
    [PersistableObject]
    internal sealed class GameSessionRecoveryState
    {
        public GameRoot Game { get; set; }
        public bool UseDeterministicRuntimeIdentifiers { get; set; }
        public int RandomSeed { get; set; }
        public PlanetaryControlRecoveryState PlanetaryControl { get; set; }
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
