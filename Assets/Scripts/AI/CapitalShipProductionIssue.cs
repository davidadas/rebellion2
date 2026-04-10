using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

/// <summary>
/// Strike target types for capital ship assault evaluation (FUN_0058b660).
/// </summary>
public enum StrikeTargetType
{
    Troop,
    Fighter,
    SystemEnergy,
    AllocatedEnergy,
}

/// <summary>
/// Represents a strike target at a system during capital ship assault evaluation.
/// </summary>
public class StrikeTarget
{
    public StrikeTargetType Type { get; set; }
    public ISceneNode Target { get; set; }
    public Planet Planet { get; set; }

    public int GetResistance(GameConfig config)
    {
        return Type switch
        {
            StrikeTargetType.Troop => ((Regiment)Target).DefenseRating,
            StrikeTargetType.Fighter => ((Starfighter)Target).ShieldStrength,
            StrikeTargetType.SystemEnergy => config.AI.CapitalShipProduction.EnergyStrikeResistance,
            StrikeTargetType.AllocatedEnergy => config
                .AI
                .CapitalShipProduction
                .AllocatedEnergyStrikeResistance,
            _ => int.MaxValue,
        };
    }
}

/// <summary>
/// Implements the original game's 4-variant capital ship production issue system
/// (strategy row package table entries 0x220-0x223).
///
/// All 4 variants execute concurrently every AI tick for each system with active production.
/// Each variant's flags modify behavior WITHIN stages, not whether stages execute.
/// ALL 4 stages always run unconditionally; results are AND-chained for return value only:
///   Stage 1 (Setup): Ship enumeration + strike target building + minor char processing
///   Stage 2 (KDY/LNR): Facility contribution to ship construction via two-resource model
///   Stage 3 (Assault): Probabilistic strike evaluation against enemy targets
///   Stage 4 (Finalize): Notification + fleet cleanup (support shifts are Death Star only)
///
/// Two-resource model (FUN_0058bb60):
///   Dimension 1: Primary shortage (ConstructionCost = refined_material_cost)
///   Dimension 2: Capacity (ProductionCapacity = production, always 40)
///   KDY fills primary only. LNR fills primary first, then overflows into capacity.
///   Ship completes when capacity is full (ProductionCapacityUsed >= ProductionCapacity).
///   KDY/LNR pools persist on the ship object across ticks.
///
/// See docs/generation-audit/03-ai-manufacturing.md and 03a-capital-ship-production-disassembly-findings.md.
/// </summary>
public class CapitalShipProductionIssue
{
    /// <summary>
    /// The 4 capital ship production variants from the strategy row package table.
    /// Each variant has different pipeline stage flags.
    /// </summary>
    public enum Variant
    {
        /// <summary>0x220: uVar3=1, local_34=1 — setup + minor character processing only.</summary>
        SetupMinorChars = 0x220,

        /// <summary>0x221: local_3c=1 — KDY/LNR contribution only.</summary>
        ContributionOnly = 0x221,

        /// <summary>0x222: uVar3=1, local_3c=1, local_38=1 — setup + contribution + assault.</summary>
        SetupContributionAssault = 0x222,

        /// <summary>0x223: uVar3=1, local_3c=1, local_38=1, local_30=1 — full pipeline.</summary>
        FullPipeline = 0x223,
    }

    // Pipeline stage flags (derived from variant, see Section 1.3)
    private readonly bool _enableSetup; // uVar3 / this+0x44: Stage 1 main body
    private readonly bool _enableContribution; // local_3c / this+0x3c: Stage 2 main body
    private readonly bool _enableAssault; // local_38 / this+0x38: Stage 3 main body
    private readonly bool _enableMinorChars; // local_34 / this+0x40: minor char processing in Stage 1
    private readonly bool _enableFinalize; // local_30 / this+0x34: Stage 4 + Death Star path in Stage 1

    // Context
    private readonly Variant _variant;
    private readonly GameRoot _game;
    private readonly Faction _faction;
    private readonly PlanetSystem _system;
    private readonly IRandomNumberProvider _provider;

    // State built during pipeline
    private List<CapitalShip> _shipsInProgress;
    private List<StrikeTarget> _strikeTargets;
    private int _initialStrikeTargetCount; // this+0x20: stored during setup, used as roll denominator in assault
    private bool _productionComplete; // this+0x30: set when capacity full or targets exhausted

    public CapitalShipProductionIssue(
        Variant variant,
        GameRoot game,
        Faction faction,
        PlanetSystem system,
        IRandomNumberProvider provider
    )
    {
        _variant = variant;
        _game = game;
        _faction = faction;
        _system = system;
        _provider = provider;

        switch (variant)
        {
            case Variant.SetupMinorChars:
                _enableSetup = true;
                _enableMinorChars = true;
                break;
            case Variant.ContributionOnly:
                _enableContribution = true;
                break;
            case Variant.SetupContributionAssault:
                _enableSetup = true;
                _enableContribution = true;
                _enableAssault = true;
                break;
            case Variant.FullPipeline:
                _enableSetup = true;
                _enableContribution = true;
                _enableAssault = true;
                _enableFinalize = true;
                break;
        }
    }

    /// <summary>
    /// Executes all 4 variants for a system, matching the original game's
    /// concurrent queue processing where all registered issues run every tick.
    /// There is no variant selection logic — all 4 execute independently.
    /// </summary>
    public static void ExecuteAllVariants(
        GameRoot game,
        Faction faction,
        PlanetSystem system,
        IRandomNumberProvider provider
    )
    {
        foreach (
            Variant v in new[]
            {
                Variant.SetupMinorChars,
                Variant.ContributionOnly,
                Variant.SetupContributionAssault,
                Variant.FullPipeline,
            }
        )
        {
            CapitalShipProductionIssue issue = new CapitalShipProductionIssue(
                v,
                game,
                faction,
                system,
                provider
            );
            issue.Execute();
        }
    }

    /// <summary>
    /// Runs the 4-stage AND-chained pipeline (FUN_0058bd90).
    /// ALL stages always run unconditionally — the variant flags modify behavior
    /// WITHIN stages, not whether stages execute. The AND-chain only affects the
    /// overall return value; no stage is short-circuited by a prior stage's failure.
    ///
    /// Original pre-check: (this+0x10 != 0 &amp;&amp; this+0x14 != 0) — validates
    /// system and faction pointers are non-null. In our code these are readonly
    /// constructor args that are always non-null.
    /// </summary>
    public bool Execute()
    {
        if (_system == null || _faction == null)
            return false;

        // All 4 stages run unconditionally, results AND-chained (FUN_0058bd90):
        //   iVar2 = setup(this, param_1);
        //   bVar1 = kdy_lnr(this, param_1);
        //   bVar1 = bVar1 && iVar2;
        //   iVar2 = assault(this, param_1);
        //   bVar1 = bVar1 && iVar2;
        //   iVar2 = finalize(this, param_1);
        //   return bVar1 && iVar2;
        bool setupResult = ExecuteSetup();
        bool contributionResult = ExecuteContribution();
        bool result = contributionResult && setupResult;
        bool assaultResult = ExecuteAssault();
        result = result && assaultResult;
        bool finalizeResult = ExecuteFinalize();
        return result && finalizeResult;
    }

    /// <summary>
    /// Stage 1: Setup and Minor Character processing (FUN_0058bfb0).
    /// Always runs for all variants. The enableSetup flag (this+0x44) gates the
    /// main body — variants without it return true immediately.
    /// If enableFinalize is set (variant 223), checks for Death Star-class ships
    /// and sets enableFinalizePackage (this+0x28) which gates finalize support shifts.
    /// If enableMinorChars is set (variant 220), processes minor characters.
    /// </summary>
    private bool ExecuteSetup()
    {
        if (!_enableSetup)
            return true;

        _shipsInProgress = EnumerateShipsInProgress();
        if (_shipsInProgress.Count == 0)
            return false;

        // Death Star special handling (variant 223 / enableFinalize flag)
        // Original: FUN_0058ba60 checks family IDs 0x18-0x1B (Death Star-class).
        // If found AND enableFinalize is set, sets this+0x28 (enableFinalizePackage) = 1
        // and this+0x2c (systemScan) = 1. These gate the finalize stage's support
        // shift logic. No Death Star ship type exists in our data yet; when added,
        // enableFinalizePackage will be set here and finalize will apply support shifts.

        // Build strike target list (FUN_0058b1e0_enumerate_capital_strike_target_lanes)
        _strikeTargets = EnumerateStrikeTargets();
        _initialStrikeTargetCount = _strikeTargets.Count; // Stored at this+0x20 for assault roll denominator

        // Minor character processing (variant 220 / enableMinorChars flag)
        if (_enableMinorChars)
        {
            // Original: FUN_0056db30 finds minor characters (type 0x38) at the system,
            // then FUN_0055d1b0 rolls GENERAL_PARAM_1542 (50%) chance for each
            // non-incapacitated one. This is a probabilistic gate only — no actual
            // injury is applied. If any roll fails while the chain is still true,
            // it breaks the AND-chain (iVar1 = 0).
            // Minor characters map to SpecialForces in our codebase but are not
            // children of Planet. Since we have no minor char entities at planets,
            // this is a no-op (equivalent to "no minor chars found" path in original).
        }

        return true;
    }

    /// <summary>
    /// Stage 2: KDY/LNR Facility Contribution (FUN_0058c230).
    /// Always runs for all variants. The enableContribution flag (this+0x3c) gates
    /// the main body — variants without it return true immediately.
    ///
    /// Each KDY and LNR facility independently selects a random ship under construction
    /// and contributes (personnel_skill / divisor + 1) * facility.ProductionModifier
    /// to that ship's persistent KDY or LNR pool.
    ///
    /// After all facilities contribute, pools are consumed per-ship (FUN_0058bb60):
    ///   1. KDY pool → primary shortage (refined_material_cost)
    ///   2. LNR pool → remaining primary shortage
    ///   3. LNR pool → capacity (production=40)
    /// Remaining pool values persist on the ship for the next tick.
    /// Ship completes when ProductionCapacityUsed >= ProductionCapacity.
    /// </summary>
    private bool ExecuteContribution()
    {
        if (!_enableContribution)
            return true;

        // Variant 221 skips setup; enumerate ships directly
        if (_shipsInProgress == null)
            _shipsInProgress = EnumerateShipsInProgress();

        if (_shipsInProgress.Count == 0)
            return true;

        string factionId = _faction.InstanceID;
        int divisor = _game.Config.AI.CapitalShipProduction.FacilityPersonnelDivisor;

        // Personnel lookup is system-wide, first-match (FUN_005084a0 with type=3).
        // Original calls this per-facility but always returns the same character
        // since it searches the whole system in stable order. Compute once.
        int personnelSkill = GetPersonnelSkill();

        // Phase 1: KDY facilities contribute to ship KDY pools (FUN_00526700 loop)
        // Each facility independently picks a random ship and adds its contribution
        // to that ship's persistent KDY pool (FUN_004ff8c0 read, FUN_00500d60 write).
        foreach (Planet planet in _system.Planets)
        {
            if (planet.GetOwnerInstanceID() != factionId)
                continue;

            foreach (Building building in planet.GetAllBuildings())
            {
                if (building.GetManufacturingStatus() != ManufacturingStatus.Complete)
                    continue;
                if (building.DefenseFacilityClass == DefenseFacilityClass.KDY)
                {
                    CapitalShip target = _shipsInProgress[
                        _provider.NextInt(0, _shipsInProgress.Count)
                    ];
                    // FUN_0055d100: (personnel / GENERAL_PARAM_1536 + 1) * facility_value
                    int contribution = (personnelSkill / divisor + 1) * building.ProductionModifier;
                    target.KdyPool += contribution;
                }
            }
        }

        // Phase 2: LNR facilities contribute to ship LNR pools (FUN_00526490 loop)
        foreach (Planet planet in _system.Planets)
        {
            if (planet.GetOwnerInstanceID() != factionId)
                continue;

            foreach (Building building in planet.GetAllBuildings())
            {
                if (building.GetManufacturingStatus() != ManufacturingStatus.Complete)
                    continue;
                if (building.DefenseFacilityClass == DefenseFacilityClass.LNR)
                {
                    CapitalShip target = _shipsInProgress[
                        _provider.NextInt(0, _shipsInProgress.Count)
                    ];
                    int contribution = (personnelSkill / divisor + 1) * building.ProductionModifier;
                    target.LnrPool += contribution;
                }
            }
        }

        // Phase 3: Apply accumulated pools per-ship using two-resource consumption
        // (FUN_0058bb60 called via FUN_0058b0b0 iterator with PTR_00667490 callback)
        bool anyShipProcessed = false;
        foreach (CapitalShip ship in _shipsInProgress)
        {
            ApplyContributions(ship);
            anyShipProcessed = true;

            // Check completion (FUN_00500ca0): ship completes when capacity is full
            if (ship.ProductionCapacityUsed >= ship.ProductionCapacity)
            {
                _productionComplete = true;
            }
        }

        return anyShipProcessed;
    }

    /// <summary>
    /// Applies accumulated KDY/LNR pools to a ship using the two-resource model
    /// (FUN_0058bb60_apply_capital_ship_production_contributions).
    ///
    /// Consumption order via FUN_0055d170:
    ///   1. KDY pool → primary shortage (ConstructionCost - RefinedMaterialProgress)
    ///   2. LNR pool → remaining primary shortage
    ///   3. LNR pool → remaining capacity (ProductionCapacity - ProductionCapacityUsed)
    /// Remaining pools are written back to the ship for next tick.
    /// </summary>
    private void ApplyContributions(CapitalShip ship)
    {
        int kdyPool = ship.KdyPool;
        int lnrPool = ship.LnrPool;

        int remainingPrimary = ship.ConstructionCost - ship.RefinedMaterialProgress;
        int remainingCapacity = ship.ProductionCapacity - ship.ProductionCapacityUsed;

        // FUN_0055d170: consume(kdy_pool, remaining_primary)
        ConsumeFromPool(ref kdyPool, ref remainingPrimary);

        // FUN_0055d170: consume(lnr_pool, remaining_primary)
        ConsumeFromPool(ref lnrPool, ref remainingPrimary);

        // FUN_0055d170: consume(lnr_pool, remaining_capacity)
        ConsumeFromPool(ref lnrPool, ref remainingCapacity);

        // Write back remaining pools (FUN_00500d60, FUN_00500d80)
        ship.KdyPool = kdyPool;
        ship.LnrPool = lnrPool;

        // Calculate primary covered this pass (FUN_00500da0)
        int originalRemainingPrimary = ship.ConstructionCost - ship.RefinedMaterialProgress;
        int primaryCoveredThisPass = originalRemainingPrimary - remainingPrimary;
        ship.RefinedMaterialProgress += primaryCoveredThisPass;

        // Calculate capacity advancement (FUN_00500ca0)
        int originalRemainingCapacity = ship.ProductionCapacity - ship.ProductionCapacityUsed;
        int capacityAdvancement = originalRemainingCapacity - remainingCapacity;
        if (capacityAdvancement > 0)
        {
            ship.ProductionCapacityUsed = Math.Min(
                ship.ProductionCapacityUsed + capacityAdvancement,
                ship.ProductionCapacity
            );
        }
    }

    /// <summary>
    /// Pool consumption algorithm (FUN_0055d170_consume_requirement_from_available_pool).
    /// If available >= required: pool -= required, required = 0
    /// Else: required -= available, pool = 0
    /// </summary>
    private static void ConsumeFromPool(ref int availablePool, ref int requiredAmount)
    {
        if (requiredAmount < availablePool)
        {
            availablePool -= requiredAmount;
            requiredAmount = 0;
        }
        else
        {
            requiredAmount -= availablePool;
            availablePool = 0;
        }
    }

    /// <summary>
    /// Finds the first matching personnel at the system and returns their leadership skill.
    /// Original: FUN_005084a0 searches the SYSTEM (not per-planet) with type=3,
    /// returns the FIRST matching character, then reads vtable offset 500 (leadership skill).
    /// Called once per facility but always returns the same character since list order is stable.
    /// </summary>
    private int GetPersonnelSkill()
    {
        string factionId = _faction.InstanceID;
        foreach (Planet planet in _system.Planets)
        {
            foreach (Officer officer in planet.GetAllOfficers())
            {
                if (officer.GetOwnerInstanceID() == factionId && !officer.IsCaptured)
                {
                    return officer.GetSkillValue(MissionParticipantSkill.Leadership);
                }
            }
        }
        return 0;
    }

    /// <summary>
    /// Stage 3: Assault Evaluation (FUN_0058c580).
    /// Always runs for all variants. The enableAssault flag (this+0x38) gates
    /// the main body — variants without it return true immediately.
    /// Only proceeds if productionComplete (this+0x30) is NOT set.
    /// Sums fleet assault strength vs enemy defensive values, then applies
    /// probabilistic strikes against enemy targets at the system.
    /// </summary>
    private bool ExecuteAssault()
    {
        if (!_enableAssault)
            return true;

        // Original: if (*(int *)((int)this + 0x30) == 0) — skip if production complete
        if (_productionComplete)
            return true;

        if (_strikeTargets == null)
        {
            _strikeTargets = EnumerateStrikeTargets();
            _initialStrikeTargetCount = _strikeTargets.Count;
        }

        if (_strikeTargets.Count == 0)
            return true;

        string factionId = _faction.InstanceID;

        // 1. Sum fleet assault strength (FUN_004fc870 + FUN_004fc950)
        // Original checks fleet 0x40 flag (bit 6 of offset 0x58) and only sums combat
        // from fleets with that construction flag. In our model, ships under construction
        // are in planet manufacturing queues, not in fleets, so there's no direct equivalent
        // of "production fleet." We sum all friendly fleets at the system instead.
        // Formula: FUN_0055d120: (commander_skill / GENERAL_PARAM_1537 + 1) * fleet_combat_value
        int assaultDivisor = _game.Config.Combat.AssaultPersonnelDivisor;
        int totalAssaultStrength = 0;
        foreach (Planet planet in _system.Planets)
        {
            foreach (Fleet fleet in planet.GetFleets())
            {
                if (fleet.GetOwnerInstanceID() != factionId)
                    continue;

                int fleetCombat = fleet.GetCombatValue();
                Officer commander = fleet.GetOfficers().FirstOrDefault();
                int commanderSkill =
                    commander?.GetSkillValue(MissionParticipantSkill.Leadership) ?? 0;
                totalAssaultStrength += (commanderSkill / assaultDivisor + 1) * fleetCombat;
            }
        }

        // 2. Sum enemy defensive core values (FUN_00526b00 + FUN_0051fd40)
        int totalDefense = 0;
        foreach (Planet planet in _system.Planets)
        {
            if (planet.GetOwnerInstanceID() == null || planet.GetOwnerInstanceID() == factionId)
                continue;
            totalDefense += planet.GetDefenseStrength(EntityStateFilter.All);
        }

        // 3. Calculate net strength
        int netStrength = totalAssaultStrength - totalDefense;
        if (netStrength <= 0)
            return true;

        int thresholdLow = _game.Config.AI.CapitalShipProduction.StrikeThresholdLow;
        int thresholdHigh = _game.Config.AI.CapitalShipProduction.StrikeThresholdHigh;

        // 4. First strike gate (effectively dead code — see below)
        // Original: roll_dice(this+0x20 - 1 + minor_chars_flag) < minor_chars_flag
        // Uses _initialStrikeTargetCount (this+0x20), NOT current count.
        // When _enableMinorChars=0: roll < 0, always false.
        // When _enableMinorChars=1: no variant has both _enableAssault and _enableMinorChars.
        // So this gate never fires. Included for structural completeness.
        if (_enableMinorChars && netStrength > 0 && _strikeTargets.Count > 0)
        {
            int minorFlag = 1;
            int roll = _provider.NextInt(0, _initialStrikeTargetCount - 1 + minorFlag + 1);
            if (roll < minorFlag)
            {
                int targetIndex = _provider.NextInt(0, _strikeTargets.Count);
                StrikeTarget target = _strikeTargets[targetIndex];
                int resistance = target.GetResistance(_game.Config);
                // FUN_0055d140: random_in_range(PARAM_1538, PARAM_1539)
                int threshold = _provider.NextInt(thresholdLow, thresholdHigh + 1);
                if (resistance < threshold)
                {
                    ApplyStrike(target);
                    _strikeTargets = EnumerateStrikeTargets();
                }
            }
        }

        // 5. Additional strikes loop (FUN_0058c580 lines 112-152)
        // Original loop condition: iVar3 != 0 && this+0x30 == 0 && i < netStrength
        //
        // Each iteration:
        //   1. Roll index using STORED count from setup (this+0x20), NOT current count
        //   2. Re-enumerate targets (fresh list every iteration, not just after strikes)
        //   3. Select Nth target from new list — if index >= current count, target is NULL → skip
        //   This creates probability dampening: as targets are destroyed, some rolls
        //   exceed the shrinking list and become no-ops.
        for (int i = 0; i < netStrength && !_productionComplete; i++)
        {
            // Roll using stored count from setup (FUN_0053c9f0_roll_dice(this+0x20 - 1))
            int targetIndex = _provider.NextInt(0, _initialStrikeTargetCount);

            // Re-enumerate targets every iteration (FUN_0058b1e0 called at line 121)
            _strikeTargets = EnumerateStrikeTargets();

            // If rolled index >= current count, target is NULL → skip (FUN_0058b990 returns NULL)
            if (_strikeTargets.Count == 0)
            {
                _productionComplete = true;
                break;
            }
            if (targetIndex >= _strikeTargets.Count)
                continue;

            StrikeTarget target = _strikeTargets[targetIndex];
            int resistance = target.GetResistance(_game.Config);
            // FUN_0055d140: strike_allowed = resistance < random_in_range(PARAM_1538, PARAM_1539)
            int threshold = _provider.NextInt(thresholdLow, thresholdHigh + 1);

            if (resistance < threshold)
            {
                ApplyStrike(target);
                // Re-enumerate after strike to check if targets exhausted (line 136)
                _strikeTargets = EnumerateStrikeTargets();
                // Original sets this+0x30 = 1 when re-enumeration finds no targets (line 143-144)
                if (_strikeTargets.Count == 0)
                    _productionComplete = true;
            }
        }

        return true;
    }

    /// <summary>
    /// Stage 4: Finalize Package (FUN_0058c940).
    /// Always runs for all variants. The enableFinalize flag (this+0x34) gates
    /// the main body — variants without it return true immediately.
    ///
    /// Support shift logic is gated by enableFinalizePackage (this+0x28), which is
    /// ONLY set in setup's Death Star path (when Death Star ships are found AND
    /// enableFinalize flag is set). For all standard capital ship production,
    /// finalize only does notification (FUN_005097d0) + fleet cleanup (FUN_004fd620).
    ///
    /// When Death Star IS present (future):
    ///   - Orbital strike: GENERAL_PARAM_7705 = -20, applied to PRODUCING faction's
    ///     own support (negative shift = military disruption)
    ///   - Local rules (no system scan): SDPR 5128-5131 applied to local system group
    ///   - Global rules (system scan): GNPR 5121-5124 applied globally
    ///
    /// Our manufacturing queue handles fleet cleanup via ManufacturingSystem completion.
    /// </summary>
    private bool ExecuteFinalize()
    {
        if (!_enableFinalize)
            return true;

        // enableFinalizePackage is set in ExecuteSetup only when Death Star is found.
        // Currently always false since we have no Death Star ship type.
        // When Death Star is added, support shift logic will go here:
        //   - Apply OrbitalStrikeSupportShift (-20) to producing faction's own support
        //   - Apply local or global support shift rules based on systemScan flag

        return true;
    }

    /// <summary>
    /// Enumerates all capital ships under construction at this system.
    /// Filters to ships owned by this faction that have not completed capacity.
    /// </summary>
    private List<CapitalShip> EnumerateShipsInProgress()
    {
        string factionId = _faction.InstanceID;
        List<CapitalShip> ships = new List<CapitalShip>();

        foreach (Planet planet in _system.Planets)
        {
            if (planet.GetOwnerInstanceID() != factionId)
                continue;

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            if (queue.TryGetValue(ManufacturingType.Ship, out List<IManufacturable> shipQueue))
            {
                ships.AddRange(
                    shipQueue
                        .OfType<CapitalShip>()
                        .Where(s => s.ProductionCapacityUsed < s.ProductionCapacity)
                );
            }
        }

        return ships;
    }

    /// <summary>
    /// Builds the strike target list for assault evaluation (FUN_0058b1e0).
    /// 4 target types: enemy troops, enemy fighters, enemy system energy, enemy allocated energy.
    /// </summary>
    private List<StrikeTarget> EnumerateStrikeTargets()
    {
        string factionId = _faction.InstanceID;
        List<StrikeTarget> targets = new List<StrikeTarget>();

        foreach (Planet planet in _system.Planets)
        {
            string owner = planet.GetOwnerInstanceID();
            if (owner == null || owner == factionId)
                continue;

            // Enemy troops at this planet
            foreach (Regiment regiment in planet.GetAllRegiments())
            {
                if (regiment.GetOwnerInstanceID() != factionId)
                {
                    targets.Add(
                        new StrikeTarget
                        {
                            Type = StrikeTargetType.Troop,
                            Target = regiment,
                            Planet = planet,
                        }
                    );
                }
            }

            // Enemy fighters at this planet
            foreach (Starfighter fighter in planet.GetAllStarfighters())
            {
                if (fighter.GetOwnerInstanceID() != factionId)
                {
                    targets.Add(
                        new StrikeTarget
                        {
                            Type = StrikeTargetType.Fighter,
                            Target = fighter,
                            Planet = planet,
                        }
                    );
                }
            }

            // System energy target
            if (planet.EnergyCapacity > 0)
            {
                targets.Add(
                    new StrikeTarget { Type = StrikeTargetType.SystemEnergy, Planet = planet }
                );
            }

            // Allocated energy target (energy used by buildings)
            if (planet.GetEnergyUsed() > 0)
            {
                targets.Add(
                    new StrikeTarget { Type = StrikeTargetType.AllocatedEnergy, Planet = planet }
                );
            }
        }

        return targets;
    }

    /// <summary>
    /// Applies a strike to the selected target (FUN_0058b660).
    /// Troops/fighters: removed from the scene graph via FUN_0058b4b0.
    /// Energy: decremented by 1 via FUN_0058b560.
    /// </summary>
    private void ApplyStrike(StrikeTarget target)
    {
        switch (target.Type)
        {
            case StrikeTargetType.Troop:
                _game.DetachNode((ISceneNode)target.Target);
                break;
            case StrikeTargetType.Fighter:
                _game.DetachNode((ISceneNode)target.Target);
                break;
            case StrikeTargetType.SystemEnergy:
                if (target.Planet.EnergyCapacity > 0)
                    target.Planet.EnergyCapacity--;
                break;
            case StrikeTargetType.AllocatedEnergy:
                if (target.Planet.EnergyCapacity > 0)
                    target.Planet.EnergyCapacity--;
                break;
        }
    }
}
