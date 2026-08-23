using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Advisor;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
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

    public enum PlanetOwnershipChangeReason
    {
        None,
        PopularSupport,
    }

    #endregion

    #region Planet

    /// <summary>
    /// A numeric attribute of a planet changed for a faction (energy, loyalty, raw materials, etc.).
    /// </summary>
    public class PlanetStatChangedResult : GameResult
    {
        public Planet Planet { get; set; }
        public Faction Faction { get; set; }
        public PlanetChangeCategory Category { get; set; }
        public int OldValue { get; set; }
        public int NewValue { get; set; }
    }

    /// <summary>
    /// A system began or ended diverting produced resources through smuggling.
    /// </summary>
    public class SmugglingChangedResult : GameResult
    {
        public Planet Planet { get; set; }
        public Faction Controller { get; set; }
        public Faction Beneficiary { get; set; }
        public int OldPercent { get; set; }
        public int NewPercent { get; set; }
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
    /// A mobile headquarters was destroyed when its planet fell to an enemy faction.
    /// </summary>
    public class HeadquartersDestroyedResult : GameResult
    {
        public Building Headquarters { get; set; }
        public Planet Planet { get; set; }
        public Faction Defender { get; set; }
        public Faction Attacker { get; set; }
    }

    /// <summary>
    /// The active regiment garrison at a planet changed.
    /// </summary>
    public class PlanetGarrisonChangedResult : GameResult
    {
        public Planet Planet { get; set; }
    }

    #endregion

    #region Faction

    /// <summary>
    /// Current observations about selected game objects were supplied to a faction.
    /// </summary>
    public class IntelligenceRevealedResult : GameResult
    {
        public Faction Recipient { get; set; }
        public List<ISceneNode> Observations { get; set; } = new List<ISceneNode>();
    }

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

    #region Mission

    /// <summary>
    /// A mission completed with a recorded outcome.
    /// </summary>
    public class MissionCompletedResult : GameResult
    {
        public Mission Mission { get; set; }
        public string MissionName { get; set; }
        public string MissionTypeID { get; set; }
        public string TargetName { get; set; }
        public Planet Location { get; set; }
        public ContainerNode ReturnDestination { get; set; }
        public List<IMissionParticipant> Participants { get; set; } =
            new List<IMissionParticipant>();
        public MissionOutcome Outcome { get; set; }
        public MissionCompletionReason CompletionReason { get; set; }
        public bool CanContinue { get; set; }
    }

    /// <summary>
    /// An espionage mission revealed intelligence about sectors beyond its primary target.
    /// </summary>
    public class PlanetSectorsRevealedResult : GameResult
    {
        public List<PlanetSector> AdditionalSectors { get; set; } = new List<PlanetSector>();
    }

    #endregion

    #region Officer

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
    /// A recruitment mission completed (successfully or not).
    /// </summary>
    public class OfficerRecruitedResult : GameResult
    {
        public Officer Officer { get; set; }
        public Faction Faction { get; set; }
        public Planet Planet { get; set; }
    }

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
    /// A traitor was discovered.
    /// </summary>
    public class TraitorDiscoveredResult : GameResult
    {
        public Officer Officer { get; set; }
        public IGameEntity DiscoveredBy { get; set; }
        public IGameEntity Context { get; set; }
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
    /// A message was created and delivered to a faction.
    /// </summary>
    public sealed class MessageDeliveredResult : GameResult
    {
        public Faction Recipient { get; set; }
        public Message Message { get; set; }
        public AdvisorNotificationType NotificationType { get; set; }
        public AdvisorSubjectNotification AdvisorSubjectNotification { get; set; }
        public string AdvisorSubjectTypeID { get; set; }
        public AdvisorNotification AdvisorNotification { get; set; }
    }

    #endregion

    #region Unit Lifecycle

    /// <summary>
    /// Ownership of one unit changed hands.
    /// </summary>
    public sealed class UnitOwnershipChangedResult : GameResult
    {
        public ISceneNode Unit { get; set; }
        public Faction PreviousOwner { get; set; }
        public Faction NewOwner { get; set; }
    }

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

    #endregion

    #region Combat

    /// <summary>
    /// Records the complete outcome of a linked-officer encounter.
    /// </summary>
    public class DuelResult : GameResult
    {
        public Officer EncounteredOfficer { get; set; }
        public Officer OpposingOfficer { get; set; }
        public Planet Location { get; set; }
        public bool EncounteredOfficerCaptured { get; set; }
        public int EncounteredOfficerInjury { get; set; }
        public int OpposingOfficerInjury { get; set; }
        public string ImagePath { get; set; }
        public string AudioPath { get; set; }
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
    /// Detached presentation state for one unit present when combat began.
    /// </summary>
    public class CombatUnitSnapshot
    {
        public ISceneNode Unit { get; set; }
        public bool WasOperational { get; set; }
        public bool Damaged { get; set; }
        public bool Destroyed { get; set; }
        public bool Captured { get; set; }

        /// <summary>
        /// Captures one unit without retaining its live scene-graph identity.
        /// </summary>
        /// <param name="unit">The unit to capture.</param>
        public CombatUnitSnapshot(ISceneNode unit)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));

            Unit = unit.CreateCopy();
            Unit.ParentInstanceID = unit.ParentInstanceID;
            Unit.LastParentInstanceID = unit.LastParentInstanceID;
            WasOperational =
                unit is not IManufacturable manufacturable
                || (
                    manufacturable.ManufacturingStatus == ManufacturingStatus.Complete
                    && manufacturable.Movement == null
                );
            Captured = unit is Officer { IsCaptured: true };
        }

        /// <summary>
        /// Captures every unit carried by the supplied fleets.
        /// </summary>
        /// <param name="fleets">The fleets whose units will be captured.</param>
        /// <returns>The detached unit snapshots in scene-graph order.</returns>
        public static List<CombatUnitSnapshot> CaptureFleetUnits(IEnumerable<Fleet> fleets)
        {
            return (fleets ?? Enumerable.Empty<Fleet>())
                .Where(fleet => fleet != null)
                .SelectMany(fleet => fleet.GetChildren<ISceneNode>(recursive: true))
                .Distinct()
                .Select(unit => new CombatUnitSnapshot(unit))
                .ToList();
        }

        /// <summary>
        /// Captures one owner's units stationed on a planet.
        /// </summary>
        /// <param name="planet">The planet whose units will be captured.</param>
        /// <param name="ownerInstanceId">The owner whose units will be captured.</param>
        /// <returns>The detached unit snapshots in scene-graph order.</returns>
        public static List<CombatUnitSnapshot> CapturePlanetUnits(
            Planet planet,
            string ownerInstanceId
        )
        {
            return planet
                    ?.GetChildren<ISceneNode>(recursive: true)
                    .Where(unit => unit.GetOwnerInstanceID() == ownerInstanceId)
                    .Distinct()
                    .Select(unit => new CombatUnitSnapshot(unit))
                    .ToList()
                ?? new List<CombatUnitSnapshot>();
        }

        /// <summary>
        /// Applies completed combat damage and destruction to captured unit state.
        /// </summary>
        /// <param name="units">The captured units to update.</param>
        /// <param name="damagedUnits">The units damaged during combat.</param>
        /// <param name="destroyedUnits">The units destroyed during combat.</param>
        public static void RecordOutcomes(
            IEnumerable<CombatUnitSnapshot> units,
            IEnumerable<ISceneNode> damagedUnits,
            IEnumerable<ISceneNode> destroyedUnits
        )
        {
            HashSet<string> damagedIds = GetInstanceIDs(damagedUnits);
            HashSet<string> destroyedIds = GetInstanceIDs(destroyedUnits);

            foreach (CombatUnitSnapshot unit in units ?? Enumerable.Empty<CombatUnitSnapshot>())
            {
                string instanceId = unit?.Unit?.GetInstanceID();
                if (string.IsNullOrEmpty(instanceId))
                    continue;

                unit.Damaged |= damagedIds.Contains(instanceId);
                unit.Destroyed |= destroyedIds.Contains(instanceId);
            }
        }

        /// <summary>
        /// Collects the stable identifiers of non-null scene nodes.
        /// </summary>
        /// <param name="units">The scene nodes whose identifiers will be collected.</param>
        /// <returns>The nonblank scene-node identifiers.</returns>
        private static HashSet<string> GetInstanceIDs(IEnumerable<ISceneNode> units)
        {
            return (units ?? Enumerable.Empty<ISceneNode>())
                .Where(unit => unit != null && !string.IsNullOrEmpty(unit.GetInstanceID()))
                .Select(unit => unit.GetInstanceID())
                .ToHashSet();
        }
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
        public string PlanetOwnerInstanceID { get; set; }
        public string AttackerRetreatPlanetInstanceID { get; set; }
        public string DefenderRetreatPlanetInstanceID { get; set; }
        public CombatSide Winner { get; set; }
        public SpaceCombatSideOutcome AttackerOutcome { get; set; }
        public SpaceCombatSideOutcome DefenderOutcome { get; set; }
        public List<ShipDamageResult> ShipDamage { get; set; } = new List<ShipDamageResult>();
        public List<FighterLossResult> FighterLosses { get; set; } = new List<FighterLossResult>();
        public List<CombatUnitSnapshot> AttackingUnits { get; set; } =
            new List<CombatUnitSnapshot>();
        public List<CombatUnitSnapshot> DefendingUnits { get; set; } =
            new List<CombatUnitSnapshot>();
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
        public List<CombatUnitSnapshot> AttackingUnits { get; set; } =
            new List<CombatUnitSnapshot>();
        public List<CombatUnitSnapshot> DefendingUnits { get; set; } =
            new List<CombatUnitSnapshot>();
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
        public List<CombatUnitSnapshot> AttackingUnits { get; set; } =
            new List<CombatUnitSnapshot>();
        public List<CombatUnitSnapshot> DefendingUnits { get; set; } =
            new List<CombatUnitSnapshot>();
        public List<GameResult> Events { get; set; } = new List<GameResult>();
        public PlanetOwnershipChangedResult OwnershipChange { get; set; }
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
    /// A manufactured item was deployed to its destination.
    /// </summary>
    public class ManufacturingDeployedResult : GameResult
    {
        public Faction Faction { get; set; }
        public IGameEntity DeployedObject { get; set; }
        public IGameEntity Location { get; set; }
    }

    #endregion
}
