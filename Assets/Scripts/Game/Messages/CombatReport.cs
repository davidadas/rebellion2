using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Identifies the completed encounter represented by a combat report.
    /// </summary>
    public enum CombatReportType
    {
        SpaceBattle,
        Bombardment,
        PlanetaryAssault,
    }

    /// <summary>
    /// Identifies the result-window category of a captured combat unit.
    /// </summary>
    public enum CombatReportUnitCategory
    {
        CapitalShip,
        Starfighter,
        ManufacturingFacility,
        DefenseFacility,
        Troops,
        Personnel,
    }

    /// <summary>
    /// Stores immutable presentation state for one unit involved in a completed encounter.
    /// </summary>
    [PersistableObject]
    public sealed class CombatReportUnit
    {
        public string InstanceID { get; set; }
        public string TypeID { get; set; }
        public string DisplayName { get; set; }
        public string OwnerInstanceID { get; set; }
        public CombatReportUnitCategory Category { get; set; }
        public string DisplayImagePath { get; set; }
        public string SmallDisplayImagePath { get; set; }
        public string ResultImagePath { get; set; }
        public string DamagedImagePath { get; set; }
        public string DamagedSmallImagePath { get; set; }
        public string ResultDamagedImagePath { get; set; }
        public string InTransitImagePath { get; set; }
        public string InTransitSmallImagePath { get; set; }
        public string ResultInTransitImagePath { get; set; }
        public string CapturedOverlayImagePath { get; set; }
        public bool WasOperational { get; set; }
        public bool Damaged { get; set; }
        public bool Destroyed { get; set; }
        public bool Captured { get; set; }
    }

    /// <summary>
    /// Stores hull damage recorded for one capital ship in a completed space battle.
    /// </summary>
    [PersistableObject]
    public sealed class CombatReportShipDamage
    {
        public string UnitInstanceID { get; set; }
        public string UnitName { get; set; }
        public int HullBefore { get; set; }
        public int HullAfter { get; set; }
    }

    /// <summary>
    /// Stores squadron losses recorded for one starfighter unit in a completed space battle.
    /// </summary>
    [PersistableObject]
    public sealed class CombatReportFighterLoss
    {
        public string UnitInstanceID { get; set; }
        public string UnitName { get; set; }
        public int SquadsBefore { get; set; }
        public int SquadsAfter { get; set; }
    }

    /// <summary>
    /// Represents a durable fleet-engagement, bombardment, or planetary-assault report.
    /// </summary>
    [PersistableObject]
    public sealed class CombatReport : Message
    {
        public CombatReportType CombatType { get; set; }
        public string PerspectiveOwnerInstanceID { get; set; }
        public string PlanetInstanceID { get; set; }
        public string PlanetName { get; set; }
        public string PlanetOwnerInstanceID { get; set; }
        public string AttackerOwnerInstanceID { get; set; }
        public string DefenderOwnerInstanceID { get; set; }
        public CombatSide Winner { get; set; }
        public SpaceCombatSideOutcome AttackerOutcome { get; set; }
        public SpaceCombatSideOutcome DefenderOutcome { get; set; }
        public string AttackerRetreatPlanetInstanceID { get; set; }
        public string DefenderRetreatPlanetInstanceID { get; set; }
        public bool Success { get; set; }
        public bool BlockedByShields { get; set; }
        public int InitialAttackerRegimentCount { get; set; }
        public int RemainingAttackerRegimentCount { get; set; }
        public int InitialDefenderRegimentCount { get; set; }
        public int RemainingDefenderRegimentCount { get; set; }
        public BombardmentType BombardmentType { get; set; }
        public int BombardmentStrength { get; set; }
        public int ShieldStrength { get; set; }
        public int StrikeAttempts { get; set; }
        public int SuccessfulStrikes { get; set; }
        public int EnergyCapacityDamage { get; set; }
        public int AllocatedEnergyDamage { get; set; }
        public bool HeadquartersDestroyed { get; set; }
        public bool PlanetDestroyed { get; set; }
        public List<CombatReportUnit> AttackingUnits { get; set; } = new List<CombatReportUnit>();
        public List<CombatReportUnit> DefendingUnits { get; set; } = new List<CombatReportUnit>();
        public List<CombatReportShipDamage> ShipDamage { get; set; } =
            new List<CombatReportShipDamage>();
        public List<CombatReportFighterLoss> FighterLosses { get; set; } =
            new List<CombatReportFighterLoss>();

        /// <summary>
        /// Captures a completed combat result as a durable report for faction message history.
        /// </summary>
        /// <param name="result">The completed result to capture.</param>
        /// <param name="perspectiveOwnerInstanceID">The faction receiving the message.</param>
        /// <param name="title">The resolved message title.</param>
        /// <param name="summary">The resolved outcome summary.</param>
        /// <returns>The combat report, or null for an unsupported result.</returns>
        public static CombatReport Capture(
            GameResult result,
            string perspectiveOwnerInstanceID,
            string title,
            string summary
        )
        {
            CombatReport report = result switch
            {
                SpaceCombatResult space => CaptureSpaceBattle(space),
                BombardmentResult bombardment => CaptureBombardment(bombardment),
                PlanetaryAssaultResult assault => CapturePlanetaryAssault(assault),
                _ => null,
            };
            if (report == null)
                return null;

            report.PerspectiveOwnerInstanceID = perspectiveOwnerInstanceID;
            report.Type = MessageType.Conflict;
            report.Title = title;
            report.Body = summary;
            return report;
        }

        /// <summary>
        /// Captures a completed result while retaining only units visible to one faction.
        /// </summary>
        /// <param name="result">The completed authoritative result.</param>
        /// <param name="perspectiveOwnerInstanceID">The faction receiving the report.</param>
        /// <param name="visibleUnitInstanceIDs">The opposing units visible before execution.</param>
        /// <returns>The detached faction-visible report, or null for an unsupported result.</returns>
        public static CombatReport CaptureVisible(
            GameResult result,
            string perspectiveOwnerInstanceID,
            IEnumerable<string> visibleUnitInstanceIDs
        )
        {
            CombatReport report = Capture(result, perspectiveOwnerInstanceID, null, null);
            if (report == null || string.IsNullOrEmpty(perspectiveOwnerInstanceID))
                return null;

            HashSet<string> visibleIds = new HashSet<string>(
                (visibleUnitInstanceIDs ?? Enumerable.Empty<string>()).Where(id =>
                    !string.IsNullOrEmpty(id)
                ),
                StringComparer.Ordinal
            );
            report.AttackingUnits = FilterVisibleUnits(
                report.AttackingUnits,
                perspectiveOwnerInstanceID,
                visibleIds
            );
            report.DefendingUnits = FilterVisibleUnits(
                report.DefendingUnits,
                perspectiveOwnerInstanceID,
                visibleIds
            );

            HashSet<string> retainedUnitIds = report
                .AttackingUnits.Concat(report.DefendingUnits)
                .Select(unit => unit.InstanceID)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet(StringComparer.Ordinal);
            report.ShipDamage = report
                .ShipDamage.Where(damage =>
                    damage != null && retainedUnitIds.Contains(damage.UnitInstanceID)
                )
                .ToList();
            report.FighterLosses = report
                .FighterLosses.Where(loss =>
                    loss != null && retainedUnitIds.Contains(loss.UnitInstanceID)
                )
                .ToList();

            if (
                report.CombatType == CombatReportType.PlanetaryAssault
                && report.DefenderOwnerInstanceID != perspectiveOwnerInstanceID
            )
            {
                report.InitialDefenderRegimentCount = CountOperationalRegiments(
                    report.DefendingUnits,
                    includeDestroyed: true
                );
                report.RemainingDefenderRegimentCount = CountOperationalRegiments(
                    report.DefendingUnits,
                    includeDestroyed: false
                );
            }

            return report;
        }

        /// <summary>
        /// Captures descendant unit identifiers from detached faction-visible roots.
        /// </summary>
        /// <param name="roots">The visible scene roots.</param>
        /// <returns>The visible descendant identifiers.</returns>
        internal static HashSet<string> CaptureVisibleUnitInstanceIDs(IEnumerable<ISceneNode> roots)
        {
            return (roots ?? Enumerable.Empty<ISceneNode>())
                .Where(root => root != null)
                .SelectMany(root => root.GetChildren<ISceneNode>(recursive: true))
                .Where(unit => unit != null && !string.IsNullOrEmpty(unit.InstanceID))
                .Select(unit => unit.InstanceID)
                .ToHashSet(StringComparer.Ordinal);
        }

        /// <summary>
        /// Retains report units owned by the viewer or present in the supplied visible set.
        /// </summary>
        /// <param name="units">The captured report units.</param>
        /// <param name="perspectiveOwnerInstanceID">The viewing faction identifier.</param>
        /// <param name="visibleIds">The visible unit identifiers.</param>
        /// <returns>The permitted report units.</returns>
        private static List<CombatReportUnit> FilterVisibleUnits(
            IEnumerable<CombatReportUnit> units,
            string perspectiveOwnerInstanceID,
            ISet<string> visibleIds
        )
        {
            return (units ?? Enumerable.Empty<CombatReportUnit>())
                .Where(unit =>
                    unit != null
                    && (
                        unit.OwnerInstanceID == perspectiveOwnerInstanceID
                        || visibleIds.Contains(unit.InstanceID)
                    )
                )
                .ToList();
        }

        /// <summary>
        /// Counts visible operational regiments before or after recorded destruction.
        /// </summary>
        /// <param name="units">The visible report units.</param>
        /// <param name="includeDestroyed">Whether destroyed regiments remain in the count.</param>
        /// <returns>The visible regiment count.</returns>
        private static int CountOperationalRegiments(
            IEnumerable<CombatReportUnit> units,
            bool includeDestroyed
        )
        {
            return (units ?? Enumerable.Empty<CombatReportUnit>()).Count(unit =>
                unit != null
                && unit.Category == CombatReportUnitCategory.Troops
                && unit.WasOperational
                && (includeDestroyed || !unit.Destroyed)
            );
        }

        /// <summary>
        /// Captures a completed fleet engagement.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>The result of capture space battle.</returns>
        private static CombatReport CaptureSpaceBattle(SpaceCombatResult result)
        {
            return new CombatReport
            {
                CombatType = CombatReportType.SpaceBattle,
                PlanetInstanceID = result.Planet?.InstanceID,
                PlanetName = result.Planet?.GetDisplayName(),
                PlanetOwnerInstanceID = result.PlanetOwnerInstanceID,
                AttackerOwnerInstanceID = FirstNonBlank(
                    result.AttackerOwnerInstanceID,
                    result.AttackerFleet?.GetOwnerInstanceID()
                ),
                DefenderOwnerInstanceID = FirstNonBlank(
                    result.DefenderOwnerInstanceID,
                    result.DefenderFleet?.GetOwnerInstanceID()
                ),
                Winner = result.Winner,
                AttackerOutcome = result.AttackerOutcome,
                DefenderOutcome = result.DefenderOutcome,
                AttackerRetreatPlanetInstanceID = result.AttackerRetreatPlanetInstanceID,
                DefenderRetreatPlanetInstanceID = result.DefenderRetreatPlanetInstanceID,
                AttackingUnits = CaptureUnits(result.AttackingUnits),
                DefendingUnits = CaptureUnits(result.DefendingUnits),
                ShipDamage = (result.ShipDamage ?? new List<ShipDamageResult>())
                    .Where(damage => damage != null)
                    .Select(damage => new CombatReportShipDamage
                    {
                        UnitInstanceID = damage.Ship?.InstanceID,
                        UnitName = damage.Ship?.GetDisplayName(),
                        HullBefore = damage.HullBefore,
                        HullAfter = damage.HullAfter,
                    })
                    .ToList(),
                FighterLosses = (result.FighterLosses ?? new List<FighterLossResult>())
                    .Where(loss => loss != null)
                    .Select(loss => new CombatReportFighterLoss
                    {
                        UnitInstanceID = loss.Fighter?.InstanceID,
                        UnitName = loss.Fighter?.GetDisplayName(),
                        SquadsBefore = loss.SquadsBefore,
                        SquadsAfter = loss.SquadsAfter,
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// Captures a completed orbital bombardment.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>The result of capture bombardment.</returns>
        private static CombatReport CaptureBombardment(BombardmentResult result)
        {
            return new CombatReport
            {
                CombatType = CombatReportType.Bombardment,
                PlanetInstanceID = result.Planet?.InstanceID,
                PlanetName = result.Planet?.GetDisplayName(),
                PlanetOwnerInstanceID = result.Planet?.OwnerInstanceID,
                AttackerOwnerInstanceID = FirstNonBlank(
                    result.AttackerOwnerInstanceID,
                    result.AttackingFaction?.InstanceID
                ),
                DefenderOwnerInstanceID = result.DefenderOwnerInstanceID,
                BombardmentType = result.Type,
                BombardmentStrength = result.BombardmentStrength,
                ShieldStrength = result.ShieldStrength,
                StrikeAttempts = result.StrikeAttempts,
                SuccessfulStrikes = result.SuccessfulStrikes,
                EnergyCapacityDamage = result.EnergyCapacityDamage,
                AllocatedEnergyDamage = result.AllocatedEnergyDamage,
                HeadquartersDestroyed = result.HeadquartersDestroyed,
                PlanetDestroyed = result.PlanetDestroyed,
                AttackingUnits = CaptureUnits(result.AttackingUnits),
                DefendingUnits = CaptureUnits(result.DefendingUnits),
                ShipDamage = (result.AttackerShipDamage ?? new List<ShipDamageResult>())
                    .Where(damage => damage != null)
                    .Select(damage => new CombatReportShipDamage
                    {
                        UnitInstanceID = damage.Ship?.InstanceID,
                        UnitName = damage.Ship?.GetDisplayName(),
                        HullBefore = damage.HullBefore,
                        HullAfter = damage.HullAfter,
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// Captures a completed planetary assault.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>The result of capture planetary assault.</returns>
        private static CombatReport CapturePlanetaryAssault(PlanetaryAssaultResult result)
        {
            return new CombatReport
            {
                CombatType = CombatReportType.PlanetaryAssault,
                PlanetInstanceID = result.Planet?.InstanceID,
                PlanetName = result.Planet?.GetDisplayName(),
                PlanetOwnerInstanceID =
                    result.OwnershipChange?.PreviousOwner?.InstanceID
                    ?? result.Planet?.OwnerInstanceID,
                AttackerOwnerInstanceID = FirstNonBlank(
                    result.AttackerOwnerInstanceID,
                    result.AttackingFaction?.InstanceID
                ),
                DefenderOwnerInstanceID = result.DefenderOwnerInstanceID,
                Success = result.Success,
                BlockedByShields = result.BlockedByShields,
                InitialAttackerRegimentCount = result.InitialAttackerRegimentCount,
                RemainingAttackerRegimentCount = result.RemainingAttackerRegimentCount,
                InitialDefenderRegimentCount = result.InitialDefenderRegimentCount,
                RemainingDefenderRegimentCount = result.RemainingDefenderRegimentCount,
                EnergyCapacityDamage = result.EnergyCapacityDamage,
                AllocatedEnergyDamage = result.AllocatedEnergyDamage,
                AttackingUnits = CaptureUnits(result.AttackingUnits),
                DefendingUnits = CaptureUnits(result.DefendingUnits),
            };
        }

        /// <summary>
        /// Converts detached combat snapshots into persistable report rows.
        /// </summary>
        /// <param name="snapshots">The snapshots.</param>
        /// <returns>The result of capture units.</returns>
        private static List<CombatReportUnit> CaptureUnits(
            IEnumerable<CombatUnitSnapshot> snapshots
        )
        {
            return (snapshots ?? Enumerable.Empty<CombatUnitSnapshot>())
                .Where(snapshot => snapshot?.Unit != null)
                .Select(CaptureUnit)
                .Where(unit => unit != null)
                .ToList();
        }

        /// <summary>
        /// Captures the presentation fields required to reproduce one result-table entry.
        /// </summary>
        /// <param name="snapshot">The snapshot.</param>
        /// <returns>The result of capture unit.</returns>
        private static CombatReportUnit CaptureUnit(CombatUnitSnapshot snapshot)
        {
            ISceneNode unit = snapshot.Unit;
            CombatReportUnitCategory? category = GetCategory(unit);
            if (!category.HasValue)
                return null;

            string resultImagePath = null;
            string resultDamagedImagePath = null;
            string resultInTransitImagePath = null;
            if (unit is CapitalShip capitalShip)
            {
                resultImagePath = capitalShip.BattleResultImagePath;
                resultDamagedImagePath = capitalShip.BattleResultDamagedImagePath;
                resultInTransitImagePath = capitalShip.BattleResultInTransitImagePath;
            }
            else if (unit is Starfighter starfighter)
            {
                resultImagePath = starfighter.BattleResultImagePath;
                resultDamagedImagePath = starfighter.BattleResultDamagedImagePath;
                resultInTransitImagePath = starfighter.BattleResultInTransitImagePath;
            }

            return new CombatReportUnit
            {
                InstanceID = unit.GetInstanceID(),
                TypeID = unit.GetTypeID(),
                DisplayName = unit.GetDisplayName(),
                OwnerInstanceID = unit.GetOwnerInstanceID(),
                Category = category.Value,
                DisplayImagePath = unit.GetDisplayImagePath(),
                SmallDisplayImagePath = unit.SmallDisplayImagePath,
                ResultImagePath = resultImagePath,
                DamagedImagePath = unit.DamagedImagePath,
                DamagedSmallImagePath = unit.DamagedSmallImagePath,
                ResultDamagedImagePath = resultDamagedImagePath,
                InTransitImagePath = unit.InTransitImagePath,
                InTransitSmallImagePath = unit.InTransitSmallImagePath,
                ResultInTransitImagePath = resultInTransitImagePath,
                CapturedOverlayImagePath = unit.CapturedOverlayImagePath,
                WasOperational = snapshot.WasOperational,
                Damaged = snapshot.Damaged,
                Destroyed = snapshot.Destroyed,
                Captured = snapshot.Captured,
            };
        }

        /// <summary>
        /// Maps one scene-node type to its original battle-result category.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <returns>The requested category.</returns>
        private static CombatReportUnitCategory? GetCategory(ISceneNode unit)
        {
            return unit switch
            {
                CapitalShip => CombatReportUnitCategory.CapitalShip,
                Starfighter => CombatReportUnitCategory.Starfighter,
                Building building when IsManufacturingFacility(building) =>
                    CombatReportUnitCategory.ManufacturingFacility,
                Building building when IsDefenseFacility(building) =>
                    CombatReportUnitCategory.DefenseFacility,
                Regiment => CombatReportUnitCategory.Troops,
                Officer or SpecialForces => CombatReportUnitCategory.Personnel,
                _ => null,
            };
        }

        /// <summary>
        /// Returns whether a building belongs to a manufacturing result category.
        /// </summary>
        /// <param name="building">The building.</param>
        /// <returns>True when the manufacturing facility condition is met; otherwise false.</returns>
        private static bool IsManufacturingFacility(Building building)
        {
            return building.BuildingType
                is BuildingType.Mine
                    or BuildingType.Refinery
                    or BuildingType.Shipyard
                    or BuildingType.TrainingFacility
                    or BuildingType.ConstructionFacility;
        }

        /// <summary>
        /// Returns whether a building belongs to a defensive result category.
        /// </summary>
        /// <param name="building">The building.</param>
        /// <returns>True when the defense facility condition is met; otherwise false.</returns>
        private static bool IsDefenseFacility(Building building)
        {
            return building.BuildingType is BuildingType.Defense or BuildingType.Weapon
                || building.IsDefenseFacility();
        }

        /// <summary>
        /// Returns the first nonblank identifier from an ordered fallback list.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns>The result of first non blank.</returns>
        private static string FirstNonBlank(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }
    }
}
