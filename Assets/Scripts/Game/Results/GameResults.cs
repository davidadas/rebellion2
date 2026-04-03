using System.Collections.Generic;
using Rebellion.Game;

namespace Rebellion.Game.Results
{
    public enum MissionOutcome
    {
        Success,
        Failed,
        Foiled,
    }

    public enum JediEventType
    {
        TierAdvanced,
        TrainingComplete,
        JediDiscovered,
    }

    public class MissionCompletedResult : GameResult
    {
        public string MissionInstanceID { get; set; }
        public string MissionName { get; set; }
        public string TargetName { get; set; }
        public List<string> ParticipantInstanceIDs { get; set; } = new List<string>();
        public List<string> ParticipantNames { get; set; } = new List<string>();
        public MissionOutcome Outcome { get; set; }
    }

    public class CharacterCapturedResult : GameResult
    {
        public string CharacterInstanceID { get; set; }
        public string CapturingFactionInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
    }

    public class CharacterKilledResult : GameResult
    {
        public string CharacterInstanceID { get; set; }
        public string KillingFactionInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
    }

    public class CharacterMovedResult : GameResult
    {
        public string CharacterInstanceID { get; set; }
        public string FromLocationInstanceID { get; set; }
        public string ToLocationInstanceID { get; set; }
    }

    public class CharacterSentToNarrativeResult : GameResult
    {
        public string CharacterInstanceID { get; set; }
        public string FromLocationInstanceID { get; set; }
        public string NarrativeType { get; set; }
    }

    public class OfficerRescuedResult : GameResult
    {
        public string CharacterInstanceID { get; set; }
        public string RescuingFactionInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
    }

    public class PlanetOwnershipChangedResult : GameResult
    {
        public string PlanetInstanceID { get; set; }
        public string PreviousOwnerInstanceID { get; set; }
        public string NewOwnerInstanceID { get; set; }
    }

    public class PlanetUprisingStartedResult : GameResult
    {
        public string PlanetInstanceID { get; set; }
        public string InstigatorFactionInstanceID { get; set; }
    }

    public class PlanetUprisingEndedResult : GameResult
    {
        public string PlanetInstanceID { get; set; }
        public string FactionInstanceID { get; set; }
    }

    public class BuildingSabotagedResult : GameResult
    {
        public string PlanetInstanceID { get; set; }
        public string BuildingType { get; set; }
        public string FactionInstanceID { get; set; }
    }

    public class UnitArrivedResult : GameResult
    {
        public string UnitInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
    }

    public class CombatResolvedResult : GameResult
    {
        public List<string> WinningFactionInstanceIDs { get; set; } = new List<string>();
        public List<string> LosingFactionInstanceIDs { get; set; } = new List<string>();
        public string PlanetInstanceID { get; set; }
    }

    /// <summary>
    /// Emitted when a combat encounter requires player input before the tick can continue.
    /// GameManager holds this as the pending combat decision until the player resolves it.
    /// </summary>
    public class PendingCombatResult : GameResult
    {
        public string AttackerFleetInstanceID { get; set; }
        public string DefenderFleetInstanceID { get; set; }
    }

    public class DuelTriggeredResult : GameResult
    {
        public List<string> AttackerInstanceIDs { get; set; } = new List<string>();
        public List<string> DefenderInstanceIDs { get; set; } = new List<string>();
    }

    public class JediResult : GameResult
    {
        public JediEventType EventType { get; set; }
        public Officer Officer { get; set; }
        public ForceTier OldTier { get; set; }
        public ForceTier NewTier { get; set; }
    }

    public class PlanetaryAssaultResult : GameResult
    {
        public string PlanetInstanceID { get; set; }
        public string AttackingFactionInstanceID { get; set; }
        public int AssaultStrength { get; set; }
        public int DefenseStrength { get; set; }
        public bool Success { get; set; }
        public List<string> DestroyedBuildingInstanceIDs { get; set; } = new List<string>();
        public bool OwnershipChanged { get; set; }
        public string NewOwnerInstanceID { get; set; }
    }

    public class VictoryResult : GameResult
    {
        public Faction Winner { get; set; }
        public Faction Loser { get; set; }
        public GameVictoryCondition? GameMode { get; set; }
        public string Description { get; set; }
    }
}
