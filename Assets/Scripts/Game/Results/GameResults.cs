using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
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

    public enum MissionCompletionReason
    {
        None,
        Success,
        Failure,
        Foiled,
        TargetUnavailable,
        NoResearchFacilities,
        ResearchProgress,
        ResearchBreakthrough,
    }

    public enum ForceEventType
    {
        DiscoveringForceUser,
        ForceUserDiscovered,
    }

    public enum CombatSide
    {
        Attacker,
        Defender,
        Draw,
    }

    public enum SpaceCombatSideOutcome
    {
        Unknown,
        Active,
        Destroyed,
        Withdrawn,
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

    public enum PlanetOwnershipChangeReason
    {
        None,
        PopularSupport,
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
    /// </summary>
    public class PlanetStatChangedResult : GameResult
    {
        public Planet Planet { get; set; }
        public Faction Faction { get; set; }
        public PlanetStatType Stat { get; set; }
        public int OldValue { get; set; }
        public int NewValue { get; set; }
    }

    /// <summary>
    /// Combat at a system started or ended (battle, bombardment, or assault).
    /// </summary>
    public class SystemCombatStateResult : GameResult
    {
        public Planet Planet { get; set; }
        public Fleet Fleet { get; set; }
        public SystemCombatStateType CombatState { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// A blockade at a system started or ended.
    /// </summary>
    public class BlockadeChangedResult : GameResult
    {
        public Planet Planet { get; set; }
        public Fleet BlockadingFleet { get; set; }
        public bool Blockaded { get; set; }
    }

    /// <summary>
    /// A system was explored (or exploration ended) by a fleet.
    /// </summary>
    public class SystemExploredResult : GameResult
    {
        public Planet Planet { get; set; }
        public Fleet Fleet { get; set; }
        public bool IsNewlyExplored { get; set; }
    }

    /// <summary>
    /// A Force discovery state changed — either an officer began scanning for Force users,
    /// or a hidden Force user was discovered by a scanner.
    /// </summary>
    public class ForceDiscoveryResult : GameResult
    {
        public ForceEventType EventType { get; set; }
        public Officer Officer { get; set; }
        public Officer Discoverer { get; set; }
        public int ForceRank { get; set; }
    }

    /// <summary>
    /// A system's population changed or was depopulated.
    /// </summary>
    public class SystemPopulationChangedResult : GameResult
    {
        public Planet Planet { get; set; }
        public bool IsDepopulated { get; set; }
        public int Population { get; set; }
    }

    /// <summary>
    /// A system was controlled for the first time ever.
    /// </summary>
    public class SystemFirstControlResult : GameResult
    {
        public Planet Planet { get; set; }
        public bool IsFirstControl { get; set; }
        public IGameEntity ControlRef { get; set; }
    }

    /// <summary>
    /// An uprising began on a planet.
    /// </summary>
    public class PlanetUprisingStartedResult : GameResult
    {
        public Planet Planet { get; set; }
        public Faction InstigatorFaction { get; set; }
    }

    /// <summary>
    /// A planet is approaching an uprising.
    /// </summary>
    public class PlanetNearUprisingResult : GameResult
    {
        public Planet Planet { get; set; }
    }

    /// <summary>
    /// An uprising ended on a planet.
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
        public PlanetOwnershipChangeReason Reason { get; set; }
        public List<string> ObserverFactionInstanceIDs { get; set; } = new List<string>();
    }

    /// <summary>
    /// A scripted incident occurred at a planet.
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
    /// </summary>
    public class MaintenanceRequiredResult : GameResult
    {
        public Faction Faction { get; set; }
        public Planet Planet { get; set; }
        public int Amount { get; set; }
    }

    /// <summary>
    /// A side research order advanced for one discipline.
    /// </summary>
    public class ResearchOrderedResult : GameResult
    {
        public Faction Faction { get; set; }
        public ResearchDiscipline Discipline { get; set; }
        public int ResearchOrder { get; set; }
        public int Capacity { get; set; }
        public Technology Technology { get; set; }
    }

    /// <summary>
    /// A side research discipline became exhausted and has no further advances available.
    /// </summary>
    public class ResearchExhaustedResult : GameResult
    {
        public Faction Faction { get; set; }
        public ResearchDiscipline Discipline { get; set; }
        public int PreviousState { get; set; }
        public int NewState { get; set; }
    }

    /// <summary>
    /// A faction's victory condition state changed.
    /// </summary>
    public class VictoryConditionChangedResult : GameResult
    {
        public Faction Faction { get; set; }
        public int ConditionId { get; set; }
        public int Value { get; set; }
    }

    /// <summary>
    /// A recruitment mission completed (successfully or not).
    /// </summary>
    public class OfficerRecruitedResult : GameResult
    {
        public Officer Officer { get; set; }
        public Faction Faction { get; set; }
        public Planet Planet { get; set; }
    }

    /// <summary>
    /// A side has no remaining officers available for recruitment.
    /// </summary>
    public class RecruitmentExhaustedResult : GameResult
    {
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
    /// </summary>
    public class MissionCompletedResult : GameResult
    {
        public Mission Mission { get; set; }
        public string MissionName { get; set; }
        public string MissionTypeID { get; set; }
        public string TargetName { get; set; }
        public List<IMissionParticipant> Participants { get; set; } =
            new List<IMissionParticipant>();
        public MissionOutcome Outcome { get; set; }
        public MissionCompletionReason CompletionReason { get; set; }
        public bool CanContinue { get; set; }
    }

    /// <summary>
    /// A mission participant's en-route-to-mission active state changed.
    /// </summary>
    public class RoleEnrouteActiveResult : GameResult
    {
        public IMissionParticipant Participant { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// A mission key was assigned to a role.
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
    /// </summary>
    public class OfficerInjuredResult : GameResult
    {
        public Officer Officer { get; set; }
        public int Severity { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// A character's command type changed.
    /// </summary>
    public class CommandKindChangedResult : GameResult
    {
        public Officer Officer { get; set; }
        public int CommandKind { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// A character is now commanding a target.
    /// </summary>
    public class OfficerCommandingResult : GameResult
    {
        public Officer Officer { get; set; }
        public IGameEntity CommandTarget { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// Two factions' characters encountered each other.
    /// </summary>
    public class OfficerEncounterResult : GameResult
    {
        public Officer Officer { get; set; }
        public Faction FactionA { get; set; }
        public Faction FactionB { get; set; }
    }

    /// <summary>
    /// A traitor was discovered.
    /// </summary>
    public class TraitorDiscoveredResult : GameResult
    {
        public Officer Officer { get; set; }
        public IGameEntity DiscoveredBy { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A character's Force level changed.
    /// </summary>
    public class ForceChangedResult : GameResult
    {
        public Officer Officer { get; set; }
        public int ForceLevel { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// A character's Force training progress changed.
    /// </summary>
    public class ForceTrainingResult : GameResult
    {
        public Officer Officer { get; set; }
        public int Progress { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// A character gained Force experience.
    /// </summary>
    public class ForceExperienceResult : GameResult
    {
        public Officer Officer { get; set; }
        public int ExperienceGained { get; set; }
        public int PreviousForceRank { get; set; }
        public int CurrentForceRank { get; set; }
        public int Detail { get; set; }
    }

    /// <summary>
    /// Luke completed Dagobah training.
    /// </summary>
    public class DagobahCompletedResult : GameResult
    {
        public Officer Officer { get; set; }
    }

    /// <summary>
    /// Luke learned about his heritage.
    /// </summary>
    public class HeritageRevealedResult : GameResult
    {
        public Officer Officer { get; set; }
    }

    /// <summary>
    /// Han Solo was attacked by a bounty hunter.
    /// </summary>
    public class BountyAttackResult : GameResult
    {
        public Officer Officer { get; set; }
    }

    /// <summary>
    /// A character's seat-of-power status changed.
    /// </summary>
    public class SeatOfPowerChangedResult : GameResult
    {
        public Officer Officer { get; set; }
        public bool IsAtSeat { get; set; }
    }

    /// <summary>
    /// A character pickup/retrieval operation changed state.
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
    /// </summary>
    public class GameObjectCreatedResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
    }

    /// <summary>
    /// A game object was deployed.
    /// </summary>
    public class GameObjectDeployedResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
    }

    /// <summary>
    /// A game object's usable state changed.
    /// </summary>
    public class GameObjectUsableResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
        public bool IsUsable { get; set; }
    }

    /// <summary>
    /// A game object began moving toward a destination.
    /// </summary>
    public class GameObjectEnrouteResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
    }

    /// <summary>
    /// A game object's en-route active state changed.
    /// </summary>
    public class GameObjectEnrouteActiveResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// A unit completed transit and arrived at its destination planet.
    /// </summary>
    public class UnitArrivedResult : GameResult
    {
        public IGameEntity Unit { get; set; }
        public Planet Destination { get; set; }
        public string MovementGroupID { get; set; }
    }

    /// <summary>
    /// A game object was damaged.
    /// </summary>
    public class GameObjectDamagedResult : GameResult
    {
        public IGameEntity GameObject { get; set; }
        public int DamageValue { get; set; }
    }

    /// <summary>
    /// The controlling faction of a game object changed.
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
    /// </summary>
    public class GameObjectDestroyedResult : GameResult
    {
        public IGameEntity DestroyedObject { get; set; }
        public IGameEntity DestroyedBy { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A game object was destroyed on arrival at its destination.
    /// </summary>
    public class GameObjectDestroyedOnArrivalResult : GameResult
    {
        public IGameEntity DestroyedObject { get; set; }
        public IGameEntity Ref { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A game object was automatically scrapped.
    /// </summary>
    public class GameObjectAutoscrappedResult : GameResult
    {
        public IGameEntity DestroyedObject { get; set; }
        public IGameEntity Ref { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A game object was sabotaged and destroyed.
    /// </summary>
    public class GameObjectSabotagedResult : GameResult
    {
        public IGameEntity SabotagedObject { get; set; }
        public IGameEntity Saboteur { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A game object's name changed.
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
    /// </summary>
    public class FleetStateChangedResult : GameResult
    {
        public Fleet Fleet { get; set; }
        public Planet Planet { get; set; }
        public FleetStateType State { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// A fighter squadron took casualties during combat.
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
        public string AttackerOwnerInstanceID { get; set; }
        public string DefenderOwnerInstanceID { get; set; }
        public Planet Planet { get; set; }
        public CombatSide Winner { get; set; }
        public SpaceCombatSideOutcome AttackerOutcome { get; set; }
        public SpaceCombatSideOutcome DefenderOutcome { get; set; }
        public List<ShipDamageResult> ShipDamage { get; set; } = new List<ShipDamageResult>();
        public List<FighterLossResult> FighterLosses { get; set; } = new List<FighterLossResult>();
        public List<GameResult> Events { get; set; } = new List<GameResult>();
    }

    /// <summary>
    /// Emitted when a combat encounter requires player input before the tick can continue.
    /// GameManager holds this as the pending combat decision until the player resolves it.
    /// </summary>
    public class PendingCombatResult : GameResult
    {
        public Fleet AttackerFleet { get; set; }
        public Fleet DefenderFleet { get; set; }
        public string AttackerOwnerInstanceID { get; set; }
        public string DefenderOwnerInstanceID { get; set; }
        public Planet Planet { get; set; }
        public bool AttackerCanRetreat { get; set; }
        public bool DefenderCanRetreat { get; set; }
    }

    /// <summary>
    /// Describes one target affected by an orbital bombardment strike.
    /// </summary>
    public class BombardmentStrikeEvent
    {
        public BombardmentTargetType TargetType { get; set; }
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
        public string AttackerOwnerInstanceID { get; set; }
        public string DefenderOwnerInstanceID { get; set; }
        public BombardmentType Type { get; set; }
        public int BombardmentStrength { get; set; }
        public int ShieldStrength { get; set; }
        public int StrikeAttempts { get; set; }
        public int SuccessfulStrikes { get; set; }
        public int EnergyCapacityDamage { get; set; }
        public int AllocatedEnergyDamage { get; set; }
        public bool HeadquartersDestroyed { get; set; }
        public bool PlanetDestroyed { get; set; }
        public List<BombardmentStrikeEvent> Strikes { get; set; } =
            new List<BombardmentStrikeEvent>();
        public List<Regiment> DestroyedRegiments { get; set; } = new List<Regiment>();
        public List<Building> DestroyedBuildings { get; set; } = new List<Building>();
        public List<CapitalShip> DestroyedCapitalShips { get; set; } = new List<CapitalShip>();
        public List<ISceneNode> AttackingUnits { get; set; } = new List<ISceneNode>();
        public List<ISceneNode> DefendingUnits { get; set; } = new List<ISceneNode>();
        public List<ShipDamageResult> AttackerShipDamage { get; set; } =
            new List<ShipDamageResult>();
        public List<GameResult> Events { get; set; } = new List<GameResult>();
        public PlanetOwnershipChangedResult OwnershipChange { get; set; }
    }

    /// <summary>
    /// Outcome of a ground assault on a planet.
    /// </summary>
    public class PlanetaryAssaultResult : GameResult
    {
        public Planet Planet { get; set; }
        public Faction AttackingFaction { get; set; }
        public string AttackerOwnerInstanceID { get; set; }
        public string DefenderOwnerInstanceID { get; set; }
        public bool Success { get; set; }
        public bool BlockedByShields { get; set; }
        public int InitialAttackerRegimentCount { get; set; }
        public int RemainingAttackerRegimentCount { get; set; }
        public int InitialDefenderRegimentCount { get; set; }
        public int RemainingDefenderRegimentCount { get; set; }
        public int EnergyCapacityDamage { get; set; }
        public int AllocatedEnergyDamage { get; set; }
        public List<Regiment> DestroyedAttackerRegiments { get; set; } = new List<Regiment>();
        public List<Regiment> DestroyedDefenderRegiments { get; set; } = new List<Regiment>();
        public List<Building> CollateralDestroyedBuildings { get; set; } = new List<Building>();
        public List<Regiment> LandedRegiments { get; set; } = new List<Regiment>();
        public List<ISceneNode> AttackingUnits { get; set; } = new List<ISceneNode>();
        public List<ISceneNode> DefendingUnits { get; set; } = new List<ISceneNode>();
        public PlanetOwnershipChangedResult OwnershipChange { get; set; }
    }

    /// <summary>
    /// A lightsaber duel was triggered between Force users.
    /// </summary>
    public class DuelTriggeredResult : GameResult
    {
        public List<Officer> Attackers { get; set; } = new List<Officer>();
        public List<Officer> Defenders { get; set; } = new List<Officer>();
    }

    /// <summary>
    /// Units were lost during an evacuation.
    /// </summary>
    public class EvacuationLossesResult : GameResult
    {
        public Faction Faction { get; set; }
        public Planet Location { get; set; }
        public List<CapitalShip> LostShips { get; set; } = new List<CapitalShip>();
        public List<Starfighter> LostStarfighters { get; set; } = new List<Starfighter>();
        public List<Regiment> LostRegiments { get; set; } = new List<Regiment>();
    }

    #endregion

    #region Manufacturing

    /// <summary>
    /// A manufacturing queue became idle.
    /// </summary>
    public class ManufacturingIdleResult : GameResult
    {
        public Planet ProductionPlanet { get; set; }
        public Faction Faction { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
    }

    /// <summary>
    /// The remaining item count for a manufacturing queue changed.
    /// </summary>
    public class ManufacturingRemainingResult : GameResult
    {
        public Faction Faction { get; set; }
        public int RemainingCount { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// The required production point count for a manufacturing queue changed.
    /// </summary>
    public class ManufacturingPointsRequiredResult : GameResult
    {
        public Faction Faction { get; set; }
        public int RequiredPoints { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// The completed production point count for a manufacturing queue changed.
    /// </summary>
    public class ManufacturingPointsCompletedResult : GameResult
    {
        public Faction Faction { get; set; }
        public int Points { get; set; }
        public IGameEntity Context { get; set; }
    }

    /// <summary>
    /// A manufacturing slot was reserved or released.
    /// </summary>
    public class ManufacturingReservedResult : GameResult
    {
        public Faction Faction { get; set; }
        public bool IsReserved { get; set; }
    }

    /// <summary>
    /// A manufactured item was deployed to its destination.
    /// </summary>
    public class ManufacturingDeployedResult : GameResult
    {
        public Faction Faction { get; set; }
        public IGameEntity DeployedObject { get; set; }
        public IGameEntity Location { get; set; }
    }

    /// <summary>
    /// The name of a product being manufactured was set or changed.
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
    /// </summary>
    public class HeavyOutcomeResult : GameResult
    {
        public IGameEntity Subject { get; set; }
        public IGameEntity Target { get; set; }
    }

    /// <summary>
    /// A minor (light) scripted outcome was dispatched.
    /// </summary>
    public class LightOutcomeResult : GameResult
    {
        public IGameEntity Subject { get; set; }
        public IGameEntity Target { get; set; }
    }

    #endregion
}
