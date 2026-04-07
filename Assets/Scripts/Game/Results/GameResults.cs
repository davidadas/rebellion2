using System.Collections.Generic;
using Rebellion.Systems;

namespace Rebellion.Game.Results
{
    public enum MissionOutcome
    {
        Success,
        Failed,
        Foiled,
    }

    public enum ForceEventType
    {
        DiscoveringForceUser,
        ForceGrowth,
        ForceUserDiscovered,
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
        public string OfficerInstanceID { get; set; }
        public string CapturingFactionInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
    }

    public class CharacterKilledResult : GameResult
    {
        public string OfficerInstanceID { get; set; }
        public string KillingFactionInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
    }

    public class CharacterMovedResult : GameResult
    {
        public string OfficerInstanceID { get; set; }
        public string FromLocationInstanceID { get; set; }
        public string ToLocationInstanceID { get; set; }
    }

    public class CharacterSentToNarrativeResult : GameResult
    {
        public string OfficerInstanceID { get; set; }
        public string FromLocationInstanceID { get; set; }
        public string NarrativeType { get; set; }
    }

    public class OfficerRescuedResult : GameResult
    {
        public string OfficerInstanceID { get; set; }
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
        public string WinningFactionInstanceID { get; set; }
        public string LosingFactionInstanceID { get; set; }
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

    public class ForceDiscoveryResult : GameResult
    {
        public ForceEventType EventType { get; set; }
        public Officer Officer { get; set; }
        public int ForceRank { get; set; }
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

    public class BombardmentStrikeEvent
    {
        public BombardmentLaneType Lane { get; set; }
        public string TargetInstanceID { get; set; }
        public string TargetName { get; set; }
    }

    public class BombardmentResult : GameResult
    {
        public string PlanetInstanceID { get; set; }
        public string AttackingFactionInstanceID { get; set; }
        public int FleetBombardmentStrength { get; set; }
        public int PlanetaryDefenseValue { get; set; }
        public int NetStrikes { get; set; }
        public bool ShieldBlocked { get; set; }
        public int EnergyDamage { get; set; }
        public int PopularSupportShift { get; set; }
        public List<BombardmentStrikeEvent> Strikes { get; set; } =
            new List<BombardmentStrikeEvent>();
        public List<string> DestroyedRegimentInstanceIDs { get; set; } = new List<string>();
        public List<string> DestroyedStarfighterInstanceIDs { get; set; } = new List<string>();
        public List<string> DestroyedBuildingInstanceIDs { get; set; } = new List<string>();
    }

    public class TechnologyUnlockedResult : GameResult
    {
        public string FactionInstanceID { get; set; }
        public ManufacturingType ResearchType { get; set; }
        public string TechnologyName { get; set; }
        public int ResearchOrder { get; set; }
    }

    public class VictoryResult : GameResult
    {
        public Faction Winner { get; set; }
        public Faction Loser { get; set; }
        public GameVictoryCondition? GameMode { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Which side won a combat engagement.
    /// </summary>
    public enum CombatSide
    {
        Attacker,
        Defender,
        Draw,
    }

    /// <summary>
    /// Hull damage sustained by a single capital ship during space combat.
    /// </summary>
    public class ShipDamageResult
    {
        public Fleet Fleet { get; set; }

        /// <summary>
        /// Index into fleet.CapitalShips at the time of combat (not a stable reference).
        /// </summary>
        public int ShipIndex { get; set; }

        public int HullBefore { get; set; }
        public int HullAfter { get; set; }
    }

    /// <summary>
    /// Losses sustained by a single fighter squadron during space combat.
    /// </summary>
    public class FighterLossResult
    {
        public Fleet Fleet { get; set; }
        public int FighterIndex { get; set; }
        public int SquadsBefore { get; set; }
        public int SquadsAfter { get; set; }
    }

    /// <summary>
    /// Outcome of space combat between two fleets.
    /// </summary>
    public class SpaceCombatResult : GameResult
    {
        public Fleet AttackerFleet { get; set; }
        public Fleet DefenderFleet { get; set; }
        public Planet System { get; set; }
        public CombatSide Winner { get; set; }
        public List<ShipDamageResult> ShipDamage { get; set; } = new List<ShipDamageResult>();
        public List<FighterLossResult> FighterLosses { get; set; } = new List<FighterLossResult>();
    }
}
