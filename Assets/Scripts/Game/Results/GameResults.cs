using System.Collections.Generic;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Game.Results
{
    #region Enums

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

    public enum CombatSide
    {
        Attacker,
        Defender,
        Draw,
    }

    public enum PlanetStatType
    {
        Energy,
        EnergyAllocated,
        Loyalty,
        ProductionModifier,
        RawMaterial,
        RawMaterialAllocated,
        Smuggling,
        TroopWithdrawPercent,
        TroopSurplus,
        TroopRequired,
        ControlUprising,
    }

    public enum IncidentType
    {
        Uprising,
        Intelligence,
        Disaster,
        Resource,
    }

    public enum SystemCombatStateType
    {
        Battle,
        Bombardment,
        Assault,
    }

    public enum FleetStateType
    {
        Battle,
        Blockade,
        Bombardment,
        Assault,
    }

    public enum ShipStatType
    {
        ShieldRechargeRate,
        WeaponRechargeRate,
        TractorBeamPower,
        Speed,
        PrimaryHyperdrive,
        BackupHyperdrive,
    }

    #endregion

    #region System / Planet

    /// <summary>
    /// A numeric attribute of a planet changed for a faction (energy, loyalty, raw materials, etc.).
    /// Covers SystemEnergyEventRecord, SystemLoyaltyEventRecord, SystemRawMaterialEventRecord,
    /// SystemProductionModifierEventRecord, SystemSmugglingPercentEventRecord,
    /// and SystemTroopRegiment* variants.
    /// </summary>
    public class PlanetStatChangedResult : GameResult
    {
        public Planet System { get; set; }
        public Faction Faction { get; set; }
        public PlanetStatType Stat { get; set; }
        public int OldValue { get; set; }
        public int NewValue { get; set; }
    }

    /// <summary>
    /// Combat at a system started or ended (battle, bombardment, or assault).
    /// Covers SystemBattleEventRecord, SystemBombardEventRecord, SystemAssaultEventRecord.
    /// </summary>
    public class SystemCombatStateResult : GameResult
    {
        public Planet System { get; set; }
        public Fleet Fleet { get; set; }
        public SystemCombatStateType CombatState { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// A blockade at a system started or ended.
    /// Covers SystemBlockadeEventRecord.
    /// </summary>
    public class BlockadeChangedResult : GameResult
    {
        public Planet System { get; set; }
        public Fleet BlockadingFleet { get; set; }
        public bool Blockaded { get; set; }
    }

    /// <summary>
    /// A system was explored (or exploration ended) by a fleet.
    /// Covers SystemExploredEventRecord.
    /// </summary>
    public class SystemExploredResult : GameResult
    {
        public Planet System { get; set; }
        public Fleet Fleet { get; set; }
        public bool IsNewlyExplored { get; set; }
    }

    /// <summary>
    /// A system's population changed or was depopulated.
    /// Covers SystemPopulatedEventRecord.
    /// </summary>
    public class SystemPopulationChangedResult : GameResult
    {
        public Planet System { get; set; }
        public bool IsDepopulated { get; set; }
        public int Population { get; set; }
    }

    /// <summary>
    /// A system was controlled for the first time ever.
    /// Covers SystemNeverBeenControlledEventRecord.
    /// </summary>
    public class SystemFirstControlResult : GameResult
    {
        public Planet System { get; set; }
        public bool IsFirstControl { get; set; }
        public IGameEntity ControlRef { get; set; }
    }

    /// <summary>
    /// An uprising began on a planet.
    /// Covers SystemUprisingEventRecord (IsActive = true).
    /// </summary>
    public class PlanetUprisingStartedResult : GameResult
    {
        public Planet Planet { get; set; }
        public Faction InstigatorFaction { get; set; }
    }

    /// <summary>
    /// An uprising ended on a planet.
    /// Covers SystemUprisingEventRecord (IsActive = false).
    /// </summary>
    public class PlanetUprisingEndedResult : GameResult
    {
        public Planet Planet { get; set; }
        public Faction Faction { get; set; }
    }

    /// <summary>
    /// Ownership of a planet changed hands.
    /// </summary>
    public class PlanetOwnershipChangedResult : GameResult
    {
        public Planet Planet { get; set; }
        public Faction PreviousOwner { get; set; }
        public Faction NewOwner { get; set; }
    }

    /// <summary>
    /// A scripted incident occurred at a planet.
    /// Covers SystemUprisingIncidentEventRecord, SystemInformationIncidentEventRecord,
    /// SystemDisasterIncidentEventRecord, SystemResourceIncidentEventRecord.
    /// </summary>
    public class PlanetIncidentResult : GameResult
    {
        public Planet Planet { get; set; }
        public IncidentType IncidentType { get; set; }
        public int Severity { get; set; }
    }

    #endregion

    #region Faction / Side

    /// <summary>
    /// A faction requires maintenance at a system.
    /// Covers SideMaintenanceRequiredEventRecord.
    /// </summary>
    public class MaintenanceRequiredResult : GameResult
    {
        public Faction Faction { get; set; }
        public Planet System { get; set; }
        public int Amount { get; set; }
    }

    /// <summary>
    /// A faction placed a research order at a facility.
    /// Covers SideShipyardResearchOrderEventRecord, SideTrainingFacilityResearchOrderEventRecord,
    /// SideConstructionYardResearchOrderEventRecord.
    /// </summary>
    public class ResearchOrderedResult : GameResult
    {
        public Faction Faction { get; set; }
        public ManufacturingType FacilityType { get; set; }
        public int ResearchOrder { get; set; }
        public int Capacity { get; set; }
    }

    /// <summary>
    /// A faction's victory condition state changed.
    /// Covers SideVictoryConditionsEventRecord.
    /// </summary>
    public class VictoryConditionChangedResult : GameResult
    {
        public Faction Faction { get; set; }
        public int ConditionId { get; set; }
        public int Value { get; set; }
    }

    /// <summary>
    /// A faction unlocked a new technology.
    /// Covers SideShipyardResearchDoneEventRecord, SideTrainingFacilityResearchDoneEventRecord,
    /// SideConstructionYardResearchDoneEventRecord.
    /// </summary>
    public class TechnologyUnlockedResult : GameResult
    {
        public Faction Faction { get; set; }
        public ManufacturingType ResearchType { get; set; }
        public string TechnologyName { get; set; }
        public int ResearchOrder { get; set; }
    }

    /// <summary>
    /// A recruitment mission completed (successfully or not).
    /// Covers SideRecruitmentDoneEventRecord.
    /// </summary>
    public class OfficerRecruitedResult : GameResult
    {
        public Officer Officer { get; set; }
        public Faction Faction { get; set; }
        public Planet Planet { get; set; }
    }

    /// <summary>
    /// A faction has won the game.
    /// </summary>
    public class VictoryResult : GameResult
    {
        public Faction Winner { get; set; }
        public Faction Loser { get; set; }
        public GameVictoryCondition? GameMode { get; set; }
        public string Description { get; set; }
    }

    #endregion

    #region Mission / Role

    /// <summary>
    /// A mission completed with a recorded outcome.
    /// Covers MissionReportEventRecord and FailedMissionReportEventRecord.
    /// </summary>
    public class MissionCompletedResult : GameResult
    {
        public Mission Mission { get; set; }
        public string MissionName { get; set; }
        public string TargetName { get; set; }
        public List<Officer> Participants { get; set; } = new List<Officer>();
        public MissionOutcome Outcome { get; set; }
    }

    /// <summary>
    /// A character's en-route-to-mission active state changed.
    /// Covers RoleEnrouteActiveEventRecord.
    /// </summary>
    public class RoleEnrouteActiveResult : GameResult
    {
        public Officer Officer { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// A mission key was assigned to a role.
    /// Covers RoleMissionKeyEventRecord.
    /// </summary>
    public class RoleMissionKeyResult : GameResult
    {
        public Officer Officer { get; set; }
        public Mission Mission { get; set; }
    }

    #endregion

    #region Officer

    /// <summary>
    /// A character's capture state changed (captured or released).
    /// Covers CharacterCaptureStateEventRecord — the one event type with extra fields
    /// beyond the standard dual-value layout.
    /// </summary>
    public class OfficerCaptureStateResult : GameResult
    {
        public Officer TargetOfficer { get; set; }
        public bool IsCaptured { get; set; }
        public Officer CapturedOfficer { get; set; }
        public Officer LinkedOfficer { get; set; }
        public IGameEntity Context { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// A character was killed.
    /// Covers GameObjectDestroyedAssassinationEventRecord.
    /// </summary>
    public class OfficerKilledResult : GameResult
    {
        public Officer TargetOfficer { get; set; }
        public IGameEntity Assassin { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A captured officer was rescued.
    /// </summary>
    public class OfficerRescuedResult : GameResult
    {
        public Officer Officer { get; set; }
        public Faction RescuingFaction { get; set; }
        public Planet Location { get; set; }
    }

    /// <summary>
    /// A character was injured.
    /// Covers CharacterInjuryEventRecord.
    /// </summary>
    public class OfficerInjuredResult : GameResult
    {
        public Officer Officer { get; set; }
        public int Severity { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// A character's command type changed.
    /// Covers CharacterCommandKindEventRecord.
    /// </summary>
    public class CommandKindChangedResult : GameResult
    {
        public Officer Officer { get; set; }
        public int CommandKind { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// A character is now commanding a target.
    /// Covers CharacterCommandingEventRecord.
    /// </summary>
    public class OfficerCommandingResult : GameResult
    {
        public Officer Officer { get; set; }
        public IGameEntity CommandTarget { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// Two factions' characters encountered each other.
    /// Covers CharacterEncounterEventRecord.
    /// </summary>
    public class OfficerEncounterResult : GameResult
    {
        public Officer Officer { get; set; }
        public Faction FactionA { get; set; }
        public Faction FactionB { get; set; }
    }

    /// <summary>
    /// A traitor was discovered.
    /// Covers CharacterTraitorDiscoveredEventRecord.
    /// </summary>
    public class TraitorDiscoveredResult : GameResult
    {
        public Officer Officer { get; set; }
        public IGameEntity DiscoveredBy { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A character's Force level changed.
    /// Covers CharacterForceEventRecord.
    /// </summary>
    public class ForceChangedResult : GameResult
    {
        public Officer Officer { get; set; }
        public int ForceLevel { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// A character's Force training progress changed.
    /// Covers CharacterForceTrainingEventRecord.
    /// </summary>
    public class ForceTrainingResult : GameResult
    {
        public Officer Officer { get; set; }
        public int Progress { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// A character gained Force experience.
    /// Covers CharacterForceExperienceEventRecord.
    /// </summary>
    public class ForceExperienceResult : GameResult
    {
        public Officer Officer { get; set; }
        public int ExperienceGained { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// A Jedi-related event: tier advancement, training completion, or discovery.
    /// </summary>
    public class JediResult : GameResult
    {
        public JediEventType EventType { get; set; }
        public Officer Officer { get; set; }
        public ForceTier OldTier { get; set; }
        public ForceTier NewTier { get; set; }
    }

    /// <summary>
    /// Luke completed Dagobah training.
    /// Covers LukeDagobahCompletedEventRecord.
    /// </summary>
    public class DagobahCompletedResult : GameResult
    {
        public Officer Officer { get; set; }
    }

    /// <summary>
    /// Luke learned about his heritage.
    /// Covers LukeKnowsHeritageEventRecord.
    /// </summary>
    public class HeritageRevealedResult : GameResult
    {
        public Officer Officer { get; set; }
    }

    /// <summary>
    /// Han Solo was attacked by a bounty hunter.
    /// Covers HanBountyAttackEventRecord.
    /// </summary>
    public class BountyAttackResult : GameResult
    {
        public Officer Officer { get; set; }
    }

    /// <summary>
    /// A character's seat-of-power status changed.
    /// Covers CharacterMgrSeatOfPowerEventRecord.
    /// </summary>
    public class SeatOfPowerChangedResult : GameResult
    {
        public Officer Officer { get; set; }
        public bool IsAtSeat { get; set; }
    }

    /// <summary>
    /// A character pickup/retrieval operation changed state.
    /// Covers CharacterMgrPickupInProgressEventRecord.
    /// </summary>
    public class OfficerPickupResult : GameResult
    {
        public Officer Officer { get; set; }
        public bool InProgress { get; set; }
    }

    #endregion

    #region GameObject Lifecycle

    /// <summary>
    /// A game object was created.
    /// Covers GameObjectCreatedEventRecord.
    /// </summary>
    public class GameObjectCreatedResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
    }

    /// <summary>
    /// A manufactured item finished production.
    /// Covers GameObjectCompletedEventRecord.
    /// </summary>
    public class ManufacturingCompletedResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
        public Planet ProductionPlanet { get; set; }
        public Faction Faction { get; set; }
        public ManufacturingType ProductType { get; set; }
        public string ProductName { get; set; }
    }

    /// <summary>
    /// A game object was deployed.
    /// Covers GameObjectDeployedEventRecord.
    /// </summary>
    public class GameObjectDeployedResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
    }

    /// <summary>
    /// A game object's usable state changed.
    /// Covers GameObjectUsableEventRecord.
    /// </summary>
    public class GameObjectUsableResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
        public bool IsUsable { get; set; }
    }

    /// <summary>
    /// A game object began moving toward a destination.
    /// Covers GameObjectEnrouteEventRecord.
    /// </summary>
    public class GameObjectEnrouteResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
    }

    /// <summary>
    /// A game object's en-route active state changed.
    /// Covers GameObjectEnrouteActiveEventRecord.
    /// </summary>
    public class GameObjectEnrouteActiveResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// A game object was damaged.
    /// Covers GameObjectDamagedEventRecord.
    /// </summary>
    public class GameObjectDamagedResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
        public int DamageValue { get; set; }
    }

    /// <summary>
    /// The controlling faction of a game object changed.
    /// Covers GameObjectControlKindEventRecord (0x100) and subtypes: battle victory,
    /// withdrawal, uprising (SystemControlKindUprisingEventRecord, 0x151), loyalty-shift.
    /// </summary>
    public class GameObjectControlChangedResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
        public Faction PreviousOwner { get; set; }
        public Faction NewOwner { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A game object was destroyed.
    /// Covers GameObjectDestroyedEventRecord.
    /// </summary>
    public class GameObjectDestroyedResult : GameResult
    {
        public IGameEntity DestroyedObject { get; set; }
        public IGameEntity DestroyedBy { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A game object was destroyed on arrival at its destination.
    /// Covers GameObjectDestroyedOnArrivalEventRecord.
    /// </summary>
    public class GameObjectDestroyedOnArrivalResult : GameResult
    {
        public IGameEntity DestroyedObject { get; set; }
        public IGameEntity Ref { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A game object was automatically scrapped.
    /// Covers GameObjectDestroyedAutoscrapEventRecord.
    /// </summary>
    public class GameObjectAutoscrappedResult : GameResult
    {
        public IGameEntity DestroyedObject { get; set; }
        public IGameEntity Ref { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A game object was sabotaged and destroyed.
    /// Covers GameObjectDestroyedSabotageEventRecord.
    /// </summary>
    public class GameObjectSabotagedResult : GameResult
    {
        public IGameEntity SabotagedObject { get; set; }
        public IGameEntity Saboteur { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A game object's name changed.
    /// Covers GameObjectNameEventRecord (TextPair).
    /// </summary>
    public class GameObjectNameChangedResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
        public string NewName { get; set; }
        public string OldName { get; set; }
    }

    #endregion

    #region Fleet / Combat

    /// <summary>
    /// A fleet's operational state changed (entering or leaving battle, blockade, bombardment, or assault).
    /// Covers FleetBattleEventRecord, FleetBlockadeEventRecord, FleetBombardEventRecord,
    /// FleetAssaultEventRecord.
    /// </summary>
    public class FleetStateChangedResult : GameResult
    {
        public Fleet Fleet { get; set; }
        public Planet System { get; set; }
        public FleetStateType State { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// A fighter squadron took casualties during combat.
    /// Covers FighterSquadSizeDamageEventRecord.
    /// </summary>
    public class FighterDamageResult : GameResult
    {
        public Starfighter Fighter { get; set; }
        public Fleet Fleet { get; set; }
        public int OldSize { get; set; }
        public int NewSize { get; set; }
    }

    /// <summary>
    /// A capital ship's hull took damage.
    /// Covers CapitalShipHullValueDamageEventRecord.
    /// </summary>
    public class ShipHullDamageResult : GameResult
    {
        public CapitalShip Ship { get; set; }
        public Fleet Fleet { get; set; }
        public int OldHull { get; set; }
        public int NewHull { get; set; }
    }

    /// <summary>
    /// A capital ship's non-hull attribute changed (shields, weapons, speed, etc.).
    /// Covers CapitalShipShieldRechargeRateEventRecord, CapitalShipWeaponRechargeRateEventRecord,
    /// CapitalShipTractorBeamPowerEventRecord, CapitalShipSpeedEventRecord,
    /// CapitalShipPrimaryHyperdriveModifierEventRecord, CapitalShipBackupHyperdriveModifierEventRecord.
    /// </summary>
    public class ShipStatChangedResult : GameResult
    {
        public CapitalShip Ship { get; set; }
        public Fleet Fleet { get; set; }
        public ShipStatType Stat { get; set; }
        public int OldValue { get; set; }
        public int NewValue { get; set; }
    }

    /// <summary>
    /// Hull damage sustained by a single capital ship during space combat (nested in SpaceCombatResult).
    /// </summary>
    public class ShipDamageResult
    {
        public CapitalShip Ship { get; set; }
        public int HullBefore { get; set; }
        public int HullAfter { get; set; }
    }

    /// <summary>
    /// Losses sustained by a single fighter squadron during space combat (nested in SpaceCombatResult).
    /// </summary>
    public class FighterLossResult
    {
        public Starfighter Fighter { get; set; }
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

    /// <summary>
    /// Summary of which faction won a combat engagement at a system.
    /// </summary>
    public class CombatResolvedResult : GameResult
    {
        public Faction WinningFaction { get; set; }
        public Faction LosingFaction { get; set; }
        public Planet Planet { get; set; }
    }

    /// <summary>
    /// Emitted when a combat encounter requires player input before the tick can continue.
    /// GameManager holds this as the pending combat decision until the player resolves it.
    /// </summary>
    public class PendingCombatResult : GameResult
    {
        public Fleet AttackerFleet { get; set; }
        public Fleet DefenderFleet { get; set; }
    }

    public class BombardmentStrikeEvent
    {
        public BombardmentLaneType Lane { get; set; }
        public IGameEntity Target { get; set; }
        public string TargetName { get; set; }
    }

    /// <summary>
    /// Full detail of a bombardment run against a planet.
    /// </summary>
    public class BombardmentResult : GameResult
    {
        public Planet Planet { get; set; }
        public Faction AttackingFaction { get; set; }
        public int FleetBombardmentStrength { get; set; }
        public int PlanetaryDefenseValue { get; set; }
        public int NetStrikes { get; set; }
        public bool ShieldBlocked { get; set; }
        public int EnergyDamage { get; set; }
        public int PopularSupportShift { get; set; }
        public List<BombardmentStrikeEvent> Strikes { get; set; } =
            new List<BombardmentStrikeEvent>();
        public List<Regiment> DestroyedRegiments { get; set; } = new List<Regiment>();
        public List<Starfighter> DestroyedStarfighters { get; set; } = new List<Starfighter>();
        public List<Building> DestroyedBuildings { get; set; } = new List<Building>();
    }

    /// <summary>
    /// Outcome of a ground assault on a planet.
    /// </summary>
    public class PlanetaryAssaultResult : GameResult
    {
        public Planet Planet { get; set; }
        public Faction AttackingFaction { get; set; }
        public int AssaultStrength { get; set; }
        public int DefenseStrength { get; set; }
        public bool Success { get; set; }
        public List<Building> DestroyedBuildings { get; set; } = new List<Building>();
        public bool OwnershipChanged { get; set; }
        public Faction NewOwner { get; set; }
    }

    /// <summary>
    /// A lightsaber duel was triggered between Force users.
    /// </summary>
    public class DuelTriggeredResult : GameResult
    {
        public List<Officer> Attackers { get; set; } = new List<Officer>();
        public List<Officer> Defenders { get; set; } = new List<Officer>();
    }

    #endregion

    #region Manufacturing

    /// <summary>
    /// The remaining item count for a manufacturing queue changed.
    /// Covers ManufacturingMgrRemainingGameObjCountEventRecord.
    /// </summary>
    public class ManufacturingRemainingResult : GameResult
    {
        public Faction Faction { get; set; }
        public int RemainingCount { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// The completed production point count for a manufacturing queue changed.
    /// Covers ManufacturingMgrCompletedPointCountEventRecord.
    /// </summary>
    public class ManufacturingPointsCompletedResult : GameResult
    {
        public Faction Faction { get; set; }
        public int Points { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// The required production point count for a manufacturing queue changed.
    /// Covers ManufacturingMgrRequiredPointCountEventRecord.
    /// </summary>
    public class ManufacturingPointsRequiredResult : GameResult
    {
        public Faction Faction { get; set; }
        public int RequiredPoints { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A manufacturing slot was reserved or released.
    /// Covers ManufacturingMgrReservedEventRecord.
    /// </summary>
    public class ManufacturingReservedResult : GameResult
    {
        public Faction Faction { get; set; }
        public bool IsReserved { get; set; }
    }

    /// <summary>
    /// A manufactured item was deployed to its destination.
    /// Covers ManufacturingMgrDeploymentKeyEventRecord.
    /// </summary>
    public class ManufacturingDeployedResult : GameResult
    {
        public Faction Faction { get; set; }
        public IGameEntity DeployedObject { get; set; }
        public IGameEntity Location { get; set; }
    }

    /// <summary>
    /// The name of a product being manufactured was set or changed.
    /// Covers ManufacturingMgrProductNameEventRecord (TextPair).
    /// </summary>
    public class ManufacturingProductNameResult : GameResult
    {
        public Faction Faction { get; set; }
        public string ProductName { get; set; }
        public string Detail { get; set; }
    }

    #endregion

    #region Outcome

    /// <summary>
    /// A major (heavy) scripted outcome was dispatched.
    /// Covers HeavyOutcomeEventRecord — has dispatchOutcome() path in the original.
    /// </summary>
    public class HeavyOutcomeResult : GameResult
    {
        public IGameEntity Subject { get; set; }
        public IGameEntity Target { get; set; }
    }

    /// <summary>
    /// A minor (light) scripted outcome was dispatched.
    /// Covers LightOutcomeEventRecord — has dispatchOutcome() path in the original.
    /// </summary>
    public class LightOutcomeResult : GameResult
    {
        public IGameEntity Subject { get; set; }
        public IGameEntity Target { get; set; }
    }

    #endregion
}
