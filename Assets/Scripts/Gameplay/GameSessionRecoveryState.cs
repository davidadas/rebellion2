using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
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
        public List<GameResult> PendingMovementResults { get; set; }
        public List<GameResult> PendingManufacturingResults { get; set; }
        public List<SmugglingRecoveryState> Smuggling { get; set; }
        public List<PlanetIntegerRecoveryState> GarrisonSurplus { get; set; }
        public PlanetaryControlRecoveryState PlanetaryControl { get; set; }
        public List<PlanetOwnershipChangedResult> ObservedOwnershipChanges { get; set; }
        public bool VictoryDeclared { get; set; }
        public List<FactionOwnershipRecoveryState> Ownership { get; set; }
        public List<MissionParticipantRecoveryState> Missions { get; set; }
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
    /// Preserves the previous relationship against which a planet's next smuggling change is evaluated.
    /// </summary>
    [PersistableObject]
    internal sealed class SmugglingRecoveryState
    {
        public string PlanetID { get; set; }
        public int DiversionPercent { get; set; }
        public string ControllerInstanceID { get; set; }
        public string BeneficiaryInstanceID { get; set; }
    }

    /// <summary>
    /// Preserves a prior numeric fact associated with the exact authoritative planet object.
    /// </summary>
    [PersistableObject]
    internal sealed class PlanetIntegerRecoveryState
    {
        public Planet Planet { get; set; }
        public int Value { get; set; }
    }

    /// <summary>
    /// Preserves per-tick control-shift suppression and ownership changes whose immediate reactions were applied.
    /// </summary>
    [PersistableObject]
    internal sealed class PlanetaryControlRecoveryState
    {
        public int ControlShiftTick { get; set; }
        public List<string> ControlShiftedOwners { get; set; }
        public List<OwnershipTransitionRecoveryState> Transitions { get; set; }
    }

    /// <summary>
    /// Keeps an ownership fact associated with its already performed displacement results.
    /// </summary>
    [PersistableObject]
    internal sealed class OwnershipTransitionRecoveryState
    {
        public PlanetOwnershipChangedResult Change { get; set; }
        public List<GameResult> Results { get; set; }
    }

    /// <summary>
    /// Preserves the raw ordered per-type ownership index for a faction.
    /// </summary>
    [PersistableObject]
    internal sealed class FactionOwnershipRecoveryState
    {
        public Faction Faction { get; set; }
        public List<ISceneNode> Units { get; set; }
    }

    /// <summary>
    /// Preserves a mission's original primary roster for its existing change detection.
    /// </summary>
    [PersistableObject]
    internal sealed class MissionParticipantRecoveryState
    {
        public Mission Mission { get; set; }
        public List<string> ParticipantIDs { get; set; }
        public bool WasCaptured { get; set; }
    }
}
