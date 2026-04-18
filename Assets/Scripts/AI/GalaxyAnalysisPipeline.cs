using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

// Implements FUN_00417a50 (galaxy analysis pipeline) and FUN_00417cb0 (calibration
// sub-machine), both of which operate on the AIWorkspace (scratchBlock).
//
// FUN_00417a50 — 6-state pipeline:
//   State 1: Init galaxy helper; call entity list builder; compute production manager;
//            call FUN_0041b4d0 (populate side analysis); find production manager entity.
//   State 2: FUN_004306c0 — build system analysis records (one per PlanetSystem).
//   State 3: FUN_00430200 — cross-link fleet entities into system records; count nodes.
//   State 4: FUN_0042fb50 — build fleet analysis sub-list.
//   State 5: FUN_004032c0 — build character analysis sub-list; set bit 0x80000000 in +0x4.
//   State 6: FUN_00417cb0 — calibration sub-machine; when done, return true.
//
// FUN_00417cb0 — calibration sub-machine (state variable at workspace+0x180):
//   Default/invalid → state 4
//   State 1: walk system analysis list; call FUN_00431860 scoring on each node;
//            accumulate flag bits 0xa0000000; when list exhausted → state 5.
//   State 2: fleet score computation arrays (loads 8, 0x10 bounds; iterates).
//   State 4: character score computation arrays (loads 0x30, 0x40 bounds; iterates).
//   State 5: if bit 0x80000000 in workspace.StatusFlags:
//              - vtable[4] on fleet sub-objects
//              - if bit 0x20000000: zero FleetScores[40]; walk fleet list; call FUN_0041af90
//              - if bit 0x40000000: zero FleetSecondaryScores[30]; call FUN_0041b230
//              - if bit 0x10000000: zero CharacterScores[20]; call FUN_0041b3c0
//              clear upper bits
//            set state 4, return true.
public static class GalaxyAnalysisPipeline
{
    // ----------------------------------------------------------------
    // FUN_00417a50: galaxy analysis pipeline tick.
    // Returns true when state 6 (calibration) completes.
    // ----------------------------------------------------------------
    public static bool Tick(AIWorkspace ws)
    {
        switch (ws.GalaxyAnalysisPhase)
        {
            case 1:
                ExecutePhase1(ws);
                ws.GalaxyAnalysisPhase = 2;
                return false;

            case 2:
                if (BuildSystemAnalysis(ws))
                    ws.GalaxyAnalysisPhase = 3;
                return false;

            case 3:
                if (BuildFleetSystemCrossLinks(ws))
                {
                    ws.SystemScoreAccumulator = ws.SystemAnalysis.Count * 10;
                    ws.GalaxyAnalysisPhase = 4;
                }
                return false;

            case 4:
                if (BuildFleetAnalysis(ws))
                    ws.GalaxyAnalysisPhase = 5;
                return false;

            case 5:
                if (BuildCharacterAnalysis(ws))
                {
                    ws.GalaxyAnalysisPhase = 6;
                    ws.StatusFlags |= unchecked((int)0x80000000);
                }
                return false;

            case 6:
                if (TickCalibration(ws))
                    return true;
                return false;

            default:
                ws.GalaxyAnalysisPhase = 1;
                return false;
        }
    }

    // ----------------------------------------------------------------
    // Phase 1 (state 1): initialize galaxy helper sub-object; call entity list
    // builder; read production manager entity.
    //
    // FUN_00417a50 case 1:
    //   FUN_0051b930_init_galaxy_helper_0x58(local_20)    — init helper on stack
    //   FUN_0042d3a0(entity_list, *scratchBlock)           — rebuild entity list
    //   scratchBlock+0xa4 = scratchBlock+0x14c            — copy production mgr ID
    //   FUN_004fc010(local_20)                             — populate helper from clock
    //   if local_18 == 1: scratchBlock+0x4 |= 1           — set initialization flag
    //   FUN_0041b4d0(scratchBlock)                         — populate side analysis
    //   FUN_004f3150(*scratchBlock, *scratchBlock)         — find production manager
    //   if found: FUN_0041b590 + FUN_00418120              — side maintenance summary
    // ----------------------------------------------------------------
    private static void ExecutePhase1(AIWorkspace ws)
    {
        if (ws.Owner == null)
            return;

        // Rebuild entity analysis lists from the current game state.
        ws.SystemAnalysis.Clear();
        ws.FleetAnalysis.Clear();
        ws.CharacterAnalysis.Clear();

        // Reset scorers for a fresh pass.
        ws.FleetAnalysisSubObject.Clear();
        ws.FleetAnalysisSubObjectB.Clear();

        // Mark initialization pass complete (FUN_00417a50 case 1: StatusFlags |= 1).
        ws.StatusFlags |= 0x1;

        // Populate supply analysis constants (FUN_0041b4d0 in original — static values
        // written once at the start of each galaxy analysis cycle).
        // These are already initialized to the correct defaults in AIWorkspace properties,
        // so no writes needed here unless workspace was reset.

        // Compute agent and fleet capacity from current game state.
        // These are used by shortage generators to determine how many missions to create.
        // Original: set externally via vtable slots 25/26; here we derive from game entities.
        ComputeCapacityFields(ws);

        // Populate CapitalShipNamingFlags for all unnamed owned ships.
        // Corresponds to the entity list scan in FUN_0042d3a0 that tracks
        // which ships need names. Ships with no DisplayName get bit 0x4000 (unnamed)
        // plus bit 0x10 (use pool-1 first) as a baseline.
        foreach (CapitalShip ship in ws.Owner.GetOwnedUnitsByType<CapitalShip>())
        {
            if (ship.InstanceID == null)
                continue;
            if (!ws.CapitalShipNamingFlags.ContainsKey(ship.InstanceID))
            {
                // Unnamed ship not yet tracked — mark it for naming.
                // Bit 0x4000 = needs name; bit 0x10 = prefer pool-1 (class-based preference
                // normally comes from the ship's base record; 0x10 is the safe default).
                ws.CapitalShipNamingFlags[ship.InstanceID] = 0x4000 | 0x10;
            }
        }
    }

    // ----------------------------------------------------------------
    // Phase 2: FUN_004306c0 — build system analysis records.
    // Scans all PlanetSystem objects visible to this faction and creates
    // SystemAnalysisRecord entries. Returns true when the scan is complete.
    // ----------------------------------------------------------------
    private static bool BuildSystemAnalysis(AIWorkspace ws)
    {
        if (ws.Owner == null)
            return true;

        ws.SystemAnalysis.Clear();
        string ownerFactionId = ws.Owner?.InstanceID ?? string.Empty;
        int ownerSide = 1;
        if (ws.Owner != null && ws.GameRoot != null)
        {
            int idx = 0;
            foreach (Faction f in ws.GameRoot.Factions)
            {
                if (f.InstanceID == ws.Owner.InstanceID)
                {
                    ownerSide = idx + 1;
                    break;
                }
                idx++;
            }
        }

        foreach (PlanetSystem system in GetVisibleSystems(ws))
        {
            SystemAnalysisRecord record = new SystemAnalysisRecord { System = system };

            // Initialise the 10 planet sub-objects (one per planet slot in the system).
            // FUN_00433190 inits them; FUN_00431860 calls FUN_004334c0 to refresh each.
            int slotIndex = 0;
            foreach (Planet planet in system.Planets)
            {
                if (slotIndex >= 10)
                    break;
                var sub = new PlanetSubobject
                {
                    OwnerSide = ownerSide,
                    DirtyFlag = 1, // mark for refresh
                };
                record.PlanetSubobjects[slotIndex++] = sub;

                // FUN_004334c0 lines 164-174: entity DataId-based flag setup (runs at the
                // start of RefreshPlanetSubobject, before ownership checks).
                // Binary checks *(planet_entity + 0x18) & 0xffffff against specific DataIds.
                // In C# we use system.DataId (Planet has no DataId property).
                if (system.DataId == 0x109)
                {
                    sub.CapabilityFlags |= 0x80000000u; // CapabilityFlags |= 0x80000000
                    sub.StatusFlags |= 0x40000000u; // StatusFlags |= 0x40000000
                }
                if (system.DataId == 0x121)
                    sub.StatusFlags |= 0x8000000u; // StatusFlags |= 0x8000000

                // FUN_004334c0: refresh the sub-object from the planet game entity.
                RefreshPlanetSubobject(sub, planet, ownerFactionId, ownerSide, ws);

                // FUN_004319d0: accumulate planet data into the system record.
                AccumulatePlanetIntoSystemRecord(record, sub);
            }

            // Legacy ScoreSystem for backward compatibility with remaining paths.
            // After FUN_004334c0/FUN_004319d0 run, DispositionFlags/FlagA/FlagB are set
            // correctly. ScoreSystem now only fills in fields the planet accumulator doesn't.
            ScoreSystem(ws, record);
            ws.SystemAnalysis.Add(record);

            // FUN_004334c0 lines 178-182: sub_525c30 counts entity families 0x20 (Alliance HQ)
            // and 0x22 (Empire HQ) at this system. If any HQ entity is present, three flag bits
            // are set: CapabilityFlags |= 0x80000000, StatusFlags |= 0x80000000, ExtraFlags |= 0x2000000.
            // C# equivalent: GalaxyClassifier.cs:235 stores faction.HQInstanceID = planet.InstanceID
            // when a planet is the faction headquarters. So the check is: does any planet in this
            // system have an InstanceID matching the owner faction's HQ planet?
            if (
                ws.Owner?.HQInstanceID != null
                && system.Planets.Any(p => p.InstanceID == ws.Owner.HQInstanceID)
            )
            {
                record.FlagA |= unchecked((int)0x80000000u); // CapabilityFlags |= 0x80000000
                record.PresenceFlags |= unchecked((int)0x80000000u); // StatusFlags |= 0x80000000
                record.FlagB |= 0x2000000; // ExtraFlags |= 0x2000000
            }

            // Build a scorer node for GalaxyAnalysisScorer.
            SystemAnalysisNode node = BuildSystemNode(ws, system, record);
            ws.FleetAnalysisSubObject.AddSystemNode(node);
        }

        ws.FleetAnalysisSubObject.MarkSystemsReady();

        // Seed the SelectedTargetTable (workspace+0x11c) for Type 9 attack/mission pipeline.
        //
        // In the original binary, a seeder function (not present in the disassembly set)
        // allocates 0x50-byte SelectedTargetEntry objects via FUN_00473420 and inserts them
        // into workspace+0x11c via FUN_005f39b0. The entry's EntityTypePacked (+0x30) is set
        // to a SystemAnalysisRecord.InternalId so the HIBYTE [0x90,0x98) check in
        // FUN_00473900 (CheckTargetPrecondition) passes. FactionContext (+0x4c) receives a
        // workspace-level reference (*(container+0x10)).
        //
        // C# translation: EntityTypePacked (integer HIBYTE-encoded ID) → SystemRef (typed reference).
        // The HIBYTE category check becomes a null check on SystemRef; the identity lookup becomes
        // reference equality. The seeder sets SystemRef = rec directly.
        //
        // Seeding criteria: own-faction presence (PresenceFlags & 0x1) marks a system as a
        // valid mission-dispatch point. The Type 9 pipeline then identifies attack/scout
        // targets within or adjacent to that system.
        ws.SelectedTargetTable.Clear();
        foreach (SystemAnalysisRecord rec in ws.SystemAnalysis)
        {
            // Require own-faction presence: the AI must have forces here to issue missions.
            if (!rec.HasFactionPresence)
                continue;
            if (rec.System == null)
                continue;

            ws.SelectedTargetTable.Add(
                new SelectedTargetEntry
                {
                    Workspace = ws,
                    Id = ws.NextMissionId++,
                    SystemRef = rec,
                    OwnerSide = ownerSide,
                    FactionContext = ws, // mirrors *(container+0x10) which holds the workspace reference
                }
            );
        }

        return true;
    }

    // ----------------------------------------------------------------
    // Phase 3: FUN_00430200 — cross-link fleet entities into system records.
    // For each fleet entity visible to this faction, find the system it's in
    // and attach a FleetAnalysisRecord to the appropriate SystemAnalysisRecord.
    // ----------------------------------------------------------------
    private static bool BuildFleetSystemCrossLinks(AIWorkspace ws)
    {
        if (ws.Owner == null)
            return true;

        string ownerId = ws.Owner?.InstanceID;

        foreach (Fleet fleet in GetVisibleFleets(ws))
        {
            PlanetSystem system = fleet.GetParentOfType<PlanetSystem>();
            if (system == null)
                continue;

            SystemAnalysisRecord sysRecord = ws.SystemAnalysis.FirstOrDefault(r =>
                r.System == system
            );
            if (sysRecord == null)
                continue;

            FleetAnalysisRecord fleetRecord = new FleetAnalysisRecord { Fleet = fleet };
            ws.FleetAnalysis.Add(fleetRecord);

            // A fleet in this system means there's entity presence (field_0x30 in binary).
            // Set PresenceFlags so shortage generators find this system as a candidate.
            if (fleet.GetOwnerInstanceID() == ownerId)
                sysRecord.PresenceFlags |= 0x1;
        }

        return true;
    }

    // Computes AgentTotalCapacity, AgentAssignedCapacity, FleetTotalCapacity, and
    // FleetAssignedCapacity from the current game state. Called during Phase 1.
    //
    // In the original, FleetTotalCapacity and FleetAssignedCapacity are written via
    // HeavyAIWorker vtable slots 25 and 26 (called externally by fleet management).
    // AgentTotalCapacity and AgentAssignedCapacity are not explicitly set in the
    // disassembly's Phase 1 — they must be maintained by the mission scheduling system.
    // Here we derive them from owned entity counts so shortage generators have real data.
    private static void ComputeCapacityFields(AIWorkspace ws)
    {
        if (ws.Owner == null)
            return;

        // Agent capacity: total officers owned vs. those currently on missions.
        // An officer is "assigned" when its parent is a Mission node.
        int totalAgents = 0;
        int assignedAgents = 0;
        foreach (Officer officer in ws.Owner.GetOwnedUnitsByType<Officer>())
        {
            totalAgents++;
            if (officer.IsOnMission())
                assignedAgents++;
        }
        ws.AgentTotalCapacity = totalAgents;
        ws.AgentAssignedCapacity = assignedAgents;

        // Fleet capacity: total capital ships vs. those committed to a fleet on a mission.
        // Approximation — the original tracks this via fleet deployment records.
        int totalShips = 0;
        int assignedShips = 0;
        foreach (CapitalShip ship in ws.Owner.GetOwnedUnitsByType<CapitalShip>())
        {
            totalShips++;
            // A ship is "assigned" (not available) when it is in transit or in a fleet
            // that is currently on an attack/patrol order.
            if (!ship.IsMovable())
                assignedShips++;
        }
        ws.FleetTotalCapacity = totalShips;
        ws.FleetAssignedCapacity = assignedShips;
    }

    // Builds a SystemAnalysisNode for the scorer from the current game state of a PlanetSystem.
    // Computes the key status and disposition flag bits that strategy records check.
    private static SystemAnalysisNode BuildSystemNode(
        AIWorkspace ws,
        PlanetSystem system,
        SystemAnalysisRecord record
    )
    {
        string ownerId = ws.Owner?.InstanceID;
        bool hasOwned = false;
        int ownedPlanetCount = 0;
        int enemyTroops = 0;
        int friendlyTroops = 0;
        int facilityCount = 0;

        foreach (Planet planet in system.Planets)
        {
            string owner = planet.GetOwnerInstanceID();
            if (owner == ownerId)
            {
                hasOwned = true;
                ownedPlanetCount++;
                facilityCount += planet.GetAllBuildings().Count;
                friendlyTroops += planet.GetAllRegiments().Count;
            }
            else if (owner != null)
            {
                enemyTroops += planet.GetAllRegiments().Count;
            }
            // else: unowned neutral planet — no accumulation needed here
        }

        // DispositionFlags (node+0x24) = record.DispositionFlags from ScoreSystem.
        //   Also set bit 0x40000000 when faction has controlled planets here.
        // CapabilityFlags (node+0x28) = record.FlagA.
        // StatusFlags (node+0x2c) = record.FlagB.
        uint dispositionFlags = record.DispositionFlags;
        if (hasOwned)
            dispositionFlags |= 0x40000000u;
        uint capabilityFlags = (uint)record.FlagA;
        uint statusFlags = (uint)record.FlagB;

        var stats = record.Stats;
        stats.FacilityCount = facilityCount;
        stats.FriendlyTroopSurplus = friendlyTroops;
        stats.EnemyTroopSurplus = enemyTroops;
        stats.FacilityCountOwned = ownedPlanetCount;

        return new SystemAnalysisNode
        {
            DispositionFlags = dispositionFlags,
            StatusFlags = statusFlags,
            CapabilityFlags = capabilityFlags,
            Stats = stats,
        };
    }

    // ----------------------------------------------------------------
    // Phase 4: FUN_0042fb50 — build fleet analysis sub-list.
    // Enumerates all fleets and populates FleetAnalysis with unit stats.
    // ----------------------------------------------------------------
    private static bool BuildFleetAnalysis(AIWorkspace ws)
    {
        // Fleet records are already seeded in phase 3.
        // Phase 4 adds unit category flags to each record and builds scorer nodes.
        foreach (FleetAnalysisRecord rec in ws.FleetAnalysis)
        {
            ScoreFleet(ws, rec);

            FleetAnalysisNode node = BuildFleetNode(ws, rec);
            ws.FleetAnalysisSubObject.AddFleetNode(node);
        }

        ws.FleetAnalysisSubObject.MarkFleetsReady();
        return true;
    }

    private static FleetAnalysisNode BuildFleetNode(AIWorkspace ws, FleetAnalysisRecord rec) // ws used for owner ID
    {
        string ownerId = ws.Owner?.InstanceID;
        bool isOwn = rec.Fleet?.GetOwnerInstanceID() == ownerId;
        bool hasCapShips = rec.Fleet?.CapitalShips?.Count > 0;

        // OwnershipFlags: bit 0x1 = own side; bit 0x4 = has capital ships.
        uint ownershipFlags = 0;
        if (isOwn)
            ownershipFlags |= 0x1;
        if (hasCapShips)
            ownershipFlags |= 0x4;

        return new FleetAnalysisNode
        {
            OwnershipFlags = ownershipFlags,
            Stats = new FleetUnitStats
            {
                CategoryFlags = rec.Stats.CategoryFlags,
                CombatStrength = rec.FleetScore,
                FactionAlignment = isOwn ? 1 : 2,
            },
        };
    }

    // ----------------------------------------------------------------
    // Phase 5: FUN_004032c0 — build character analysis sub-list.
    // Enumerates officers/agents and creates CharacterAnalysisRecord entries.
    // ----------------------------------------------------------------
    private static bool BuildCharacterAnalysis(AIWorkspace ws)
    {
        if (ws.Owner == null)
            return true;

        ws.CharacterAnalysis.Clear();
        foreach (Officer officer in GetVisibleOfficers(ws))
        {
            CharacterAnalysisRecord rec = new CharacterAnalysisRecord { Officer = officer };
            ScoreCharacter(ws, rec);

            // bit 0x10: workspace+0x138 FieldAt18 HIBYTE [0x90,0x98) — system entity type.
            // C# proxy: officer not currently on a mission (stationed with fleet at system).
            // bit 0x20: workspace+0x138 FieldAt18 HIBYTE [0xa0,0xa2) — character entity type.
            // C# proxy: officer currently on a mission (targeting an entity).
            if (!officer.IsOnMission())
                rec.CapabilityFlags |= 0x10;
            else
                rec.CapabilityFlags |= 0x20;

            // Associate with the fleet analysis record whose fleet contains this officer.
            foreach (FleetAnalysisRecord fleetRec in ws.FleetAnalysis)
            {
                if (fleetRec.Fleet != null && fleetRec.Fleet.GetOfficers().Contains(officer))
                {
                    rec.FleetRef = fleetRec;
                    break;
                }
            }

            ws.CharacterAnalysis.Add(rec);
            // HIBYTE 0xa0 (character type, range [0xa0,0xa2)) — FUN_004032c0 iterator.
            rec.InternalId =
                unchecked((int)0xa0000000u) | ((ws.CharacterAnalysis.Count - 1) & 0xFFFFFF);

            CharacterAnalysisNode node = new CharacterAnalysisNode
            {
                EngagementFlags = 0, // not in combat — available for missions
                CategoryFlags = 0,
                Stats = new CombatEngagementStats { CombatScore = rec.CharacterScore },
            };
            ws.FleetAnalysisSubObject.AddCharacterNode(node);
        }

        ws.FleetAnalysisSubObject.MarkCharactersReady();
        return true;
    }

    // ----------------------------------------------------------------
    // FUN_00417cb0: calibration sub-machine tick.
    // State variable: ws.CalibrationState.
    // Returns true when calibration completes (state 5 finishes).
    // ----------------------------------------------------------------
    public static bool TickCalibration(AIWorkspace ws)
    {
        switch (ws.CalibrationState)
        {
            default:
                ws.CalibrationState = 4;
                return false;

            case 1:
            {
                while (ws.CalibrationCursor < ws.SystemAnalysis.Count)
                {
                    SystemAnalysisRecord rec = ws.SystemAnalysis[ws.CalibrationCursor];
                    int result = ScoreSystemNode(ws, rec);
                    if (result != 0)
                        ws.StatusFlags |= unchecked((int)0xa0000000);
                    ws.CalibrationCursor++;
                    break;
                }

                if (ws.CalibrationCursor >= ws.SystemAnalysis.Count)
                    ws.CalibrationState = 5;

                return false;
            }

            case 2:
            {
                ComputeFleetScoreRange(ws, 8, 16);
                ws.CalibrationState = 5;
                return false;
            }

            case 4:
            {
                ComputeCharacterScoreRange(ws, 48, 64);
                ws.CalibrationState = 5;
                return false;
            }

            case 5:
            {
                if ((ws.StatusFlags & unchecked((int)0x80000000)) != 0)
                {
                    // Invoke both scorer instances (vtable[4] calls in original).
                    ws.FleetAnalysisSubObject.Score();
                    ws.FleetAnalysisSubObjectB.Score();

                    if ((ws.StatusFlags & 0x20000000) != 0)
                    {
                        for (int i = 0; i < ws.FleetScores.Length; i++)
                            ws.FleetScores[i] = 0;
                        ws.StatusFlags &= ~0x20000000;

                        foreach (FleetAnalysisRecord rec in ws.FleetAnalysis)
                            AccumulateFleetScores(ws, rec);

                        if (ws.FleetAnalysisAccumulatorA > 0)
                            ws.FleetAnalysisAccumulatorA = ws.FleetAnalysisAccumulatorA / 5 + 1;
                        if (ws.FleetAnalysisAccumulatorB > 0)
                            ws.FleetAnalysisAccumulatorB = ws.FleetAnalysisAccumulatorB / 6 + 1;
                    }

                    if ((ws.StatusFlags & 0x40000000) != 0)
                    {
                        for (int i = 0; i < ws.FleetSecondaryScores.Length; i++)
                            ws.FleetSecondaryScores[i] = 0;
                        ws.StatusFlags &= ~0x40000000;

                        foreach (FleetAnalysisRecord rec in ws.FleetAnalysis)
                            AccumulateFleetSecondaryScores(ws, rec);
                    }

                    if ((ws.StatusFlags & 0x10000000) != 0)
                    {
                        for (int i = 0; i < ws.CharacterScores.Length; i++)
                            ws.CharacterScores[i] = 0;
                        ws.StatusFlags &= ~0x10000000;

                        foreach (CharacterAnalysisRecord rec in ws.CharacterAnalysis)
                            AccumulateCharacterScores(ws, rec);
                    }

                    ws.StatusFlags &= 0x0fffffff;
                }

                ws.CalibrationState = 4;
                ws.CalibrationCursor = 0;
                return true;
            }
        }
    }

    // ----------------------------------------------------------------
    // Scoring helpers — implement the specific accumulator functions.
    // These correspond to FUN_0041af90, FUN_0041b230, FUN_0041b3c0,
    // FUN_00431860, etc. referenced throughout the calibration pipeline.
    // ----------------------------------------------------------------

    // Scores a system analysis record from the current game state.
    // Sets the key flag bits that shortage generators and calibration check:
    //   FlagA (+0x28): bit 0x1 = friendly planet, bit 0x2 = enemy planet,
    //                  bit 0x8 = has troops.
    //   FlagB (+0x2c): bit 0x4 = own planet, bit 0x8 = enemy planet,
    //                  bit 0x10 = neutral planet.
    //   PresenceFlags (+0x30): 1 if faction has a fleet/character here.
    // Corresponds to FUN_004319d0 (per-planet accumulator) and FUN_00431860 (node scorer).
    private static void ScoreSystem(AIWorkspace ws, SystemAnalysisRecord rec)
    {
        if (rec.System == null)
            return;

        string ownerId = ws.Owner?.InstanceID;

        foreach (Planet planet in rec.System.Planets)
        {
            string owner = planet.GetOwnerInstanceID();
            bool ownedByUs = owner == ownerId;
            bool ownedByEnemy = !ownedByUs && owner != null;
            bool isNeutral = owner == null;

            int troops = planet.GetAllRegiments().Count;

            if (ownedByUs)
            {
                // FUN_004319d0: *param_3 & 0x1 set → field35_0x2c |= 0x4
                rec.FlagB |= 0x4; // own planet (FlagB bit 2)
                // NOTE: FlagA bits 0-1 (0x1, 0x2) are set ONLY from planet ExtraFlags & 0x4/0x8
                // via AccumulatePlanetIntoSystemRecord — they represent garrison unit type flags,
                // NOT faction ownership. Do NOT set FlagA |= 0x1 here: that would incorrectly
                // block all own-faction systems from PreconditionCheck1 (which requires FlagA & 0x3 == 0).

                // FlagA bit 0x1000: set when param_4 & 0x20000 (regiment capacity available).
                // Proxy: own planet with buildings = regiment support capable.
                int buildings = planet.GetAllBuildings().Count;
                if (buildings > 0)
                    rec.FlagA |= 0x1000;
                rec.Stats.FacilityCount += buildings;
                rec.Stats.FriendlyTroopSurplus += troops;
                if (troops > 0)
                    rec.FlagA |= 0x8;

                // DispositionFlags bit 0x80: set when planet+0x2c & 0x3800000 == 0.
                // Proxy: own planet without mission-blocking flags = character available.
                // This is the flag queried by UpdateShortageFleet (FUN_004191b0 param_1=0x80).
                rec.DispositionFlags |= 0x80;

                // DispositionFlags bit 0x2000: driven by CapabilityFlags & 0x800 in
                // AccumulatePlanetIntoSystemRecord (FUN_004319d0 line 142-143).
                // CapabilityFlags & 0x800 is now set by RefreshPlanetSubobject when the planet
                // has active construction yards. No proxy needed here.

                // SystemScore: positive for shortage candidate eligibility (+0x60 in binary).
                rec.SystemScore += 1;
            }
            else if (ownedByEnemy)
            {
                // FUN_004319d0: *param_3 & 0x4 → field35_0x2c |= 0x8 (enemy planet)
                // param_4 & 0x4 → field34_0x28 |= 0x1 (shortage gen requires 0x3 clear)
                rec.FlagB |= 0x8;
                rec.Stats.EnemyTroopSurplus += troops;
            }
            else if (isNeutral)
            {
                // FUN_004319d0: neither owned nor enemy → field35_0x2c |= 0x10
                rec.FlagB |= 0x10;
            }
        }

        // PresenceFlags (+0x30): multi-bit flag.
        //   bit 0x1 = faction has presence (own planets or fleet).
        //   bit 0x10000000 = selected for shortage resolution (set by FinalizeShortageRecord).
        if ((rec.FlagB & 0x4) != 0)
            rec.PresenceFlags |= 0x1; // own planets = presence

        rec.Stats.FacilityCountOwned = rec.System.Planets.Count(p =>
            p.GetOwnerInstanceID() == ownerId
        );
    }

    // FUN_00431860: scores a system node during calibration state 1.
    // Reads the system analysis record and sets bits on the workspace StatusFlags
    // (bits 0x20000000/0x40000000/0x10000000) to signal which score arrays need refresh.
    //
    // Returns non-zero when the system has own-faction planets (bit 0x4 in FlagB),
    // which triggers the fleet/character analysis refresh.
    private static int ScoreSystemNode(AIWorkspace _, SystemAnalysisRecord rec)
    {
        if (rec == null)
            return 0;

        // Systems with own-controlled planets signal that fleet/character refresh is needed.
        // FUN_00431860: sets bit 0xa0000000 in workspace when scoring produces useful data.
        bool hasOwnPlanets = (rec.FlagB & 0x4) != 0;
        bool hasEnemyPresence = (rec.FlagB & 0x8) != 0;

        // Set contested flag if system has enemy presence alongside own (field35_0x2c |= 0x4000000).
        if (hasEnemyPresence && rec.PresenceFlags != 0)
            rec.FlagB |= 0x4000000;

        // Return 1 to signal refresh needed when own-faction presence exists.
        return (hasOwnPlanets || rec.PresenceFlags != 0) ? 1 : 0;
    }

    private static void ScoreFleet(AIWorkspace _, FleetAnalysisRecord rec)
    {
        if (rec.Fleet == null)
            return;

        rec.Stats.CategoryFlags = 0;
        if (rec.Fleet.CapitalShips.Count > 0)
            rec.Stats.CategoryFlags |= 0x200000; // warship flag

        rec.FleetScore = rec.Fleet.GetCombatValue();
    }

    private static void ScoreCharacter(AIWorkspace _, CharacterAnalysisRecord rec)
    {
        if (rec.Officer == null)
            return;

        rec.CharacterScore = rec.Officer.GetSkillValue(MissionParticipantSkill.Leadership);
    }

    private static void AccumulateFleetScores(AIWorkspace ws, FleetAnalysisRecord rec)
    {
        // FUN_0041af90: per-system fleet score accumulator.
        // Writes into FleetScores array indexed by system.
        if (rec.Fleet == null)
            return;

        int score = rec.Fleet.GetCombatValue();
        if (ws.FleetAnalysisAccumulatorA < score)
            ws.FleetAnalysisAccumulatorA = score;
    }

    // -------------------------------------------------------------------------
    // FUN_004334c0 — Planet Sub-Object Refresh
    //
    // Gate: if sub.DirtyFlag == 0 → return immediately (already current).
    // If DirtyFlag != 0:
    //   1. Clear DirtyFlag.
    //   2. Resolve the planet game entity via the planet reference at sub.EntityRef.
    //   3. Clear all data fields (0x3c dwords from param_1 start region).
    //   4. Reset flag fields with masks (keep only persistent bits):
    //      CapabilityFlags &= 0x3e00000; ExtraFlags &= 0x60000000; StatusFlags &= 0x35ff0000.
    //   5. Compute ownership code from entity.OwnerField >> 6 & 3.
    //   6. Set StatusFlags bits based on ownership comparison:
    //      own faction → StatusFlags |= 0x1 | 0x20
    //      enemy faction → StatusFlags |= 0x4 | 0x20
    //      neutral → StatusFlags LOBYTE |= 0x2
    //   7. Iterate all unit types at this planet (regiments, capital ships, starfighters,
    //      fighters, officers) setting capability and surplus fields.
    //   8. Compute urgency score (0-6), garrison deficit, and shortage flags.
    //
    // In C# the game entity iteration uses the game's entity APIs. The entity iterators
    // (sub_52b900, sub_52bc60, sub_52b600, sub_526a80, sub_51b460, etc.) are replaced by
    // faction.GetOwnedUnitsByType<T>() filtered by the planet's associated fleet.
    // -------------------------------------------------------------------------
    private static void RefreshPlanetSubobject(
        PlanetSubobject sub,
        Planet planet,
        string ownerFactionId,
        int ownerSide,
        AIWorkspace ws
    )
    {
        if (sub == null || sub.DirtyFlag == 0)
            return;

        sub.DirtyFlag = 0;

        // Determine enemy side code (opposite of owner side).
        // ownerSide: 1=Alliance, 2=Empire. enemySide = 3-ownerSide.
        int enemySide = ownerSide == 1 ? 2 : 1;

        // Get the planet's owner.
        string planetOwner = planet.GetOwnerInstanceID();

        // Reset flag fields preserving only the persistent bits (assembly masks):
        sub.CapabilityFlags &= 0x3e00000u; // keep bits 21-25
        sub.ExtraFlags &= 0x60000000u; // keep bits 29-30
        sub.StatusFlags &= 0x35ff0000u; // keep specific bits

        // Compute ownership code: entity.OwnerField >> 6 & 3.
        // In C# proxy: 1=Alliance owns, 2=Empire owns, 0=neutral.
        int ownCode;
        if (planetOwner == ownerFactionId)
            ownCode = ownerSide; // own faction
        else if (planetOwner != null)
            ownCode = enemySide; // enemy faction
        else
            ownCode = 0; // neutral / unclaimed

        sub.OwnershipCode = ownCode;

        // FUN_004334c0 lines 123-144: ownership → StatusFlags:
        if (ownCode == ownerSide)
        {
            sub.StatusFlags |= 0x1u | 0x20u; // own faction (bit 0 + bit 5)
        }
        else if (ownCode == enemySide)
        {
            sub.StatusFlags |= 0x4u | 0x20u; // enemy faction (bit 2 + bit 5)
        }
        else
        {
            sub.StatusFlags |= 0x2u; // neutral (LOBYTE bit 1)
        }

        // FUN_004334c0 line 156-158: fleet.+0x88 & 0x20 → CapabilityFlags |= 0x2 (warship facility).
        // fleet.+0x88 bit 5 is the capital-ship construction capability flag on the fleet entity.
        // C# proxy: planet has a completed Shipyard building (BuildingType.Shipyard).
        // Note: in the original this is gated on fleet presence (fleet.+0x88 & 0x2 != 0); we
        // skip that gate since we have no per-planet fleet entity — the capability is present
        // regardless of whether a fleet is stationed there.
        if (planet.GetAllBuildings().Any(b => b.BuildingType == BuildingType.Shipyard))
            sub.CapabilityFlags |= 0x2u;

        // FUN_004334c0 lines 178-182: sub_525c30(fleet, ownerCode, 0x3) != 0 → HQ buildings present.
        // sub_525c30 counts entity family types 0x20 (Alliance HQ) and 0x22 (Empire HQ).
        // C# equivalent: this specific planet is the owner faction's HQ planet.
        // (GalaxyClassifier.cs line 235 stores faction.HQInstanceID = planet.InstanceID for HQ planets.)
        if (ws.Owner?.HQInstanceID != null && planet.InstanceID == ws.Owner.HQInstanceID)
        {
            sub.CapabilityFlags |= 0x80000000u; // CapabilityFlags |= 0x80000000
            sub.StatusFlags |= 0x80000000u; // StatusFlags |= 0x80000000
            sub.ExtraFlags |= 0x2000000u; // ExtraFlags |= 0x2000000
        }

        // Compute available capacity from the planet entity fields.
        // In the binary entity+0x5c = garrison capacity, +0x60 = used, +0x64/+0x68 = fighter capacity.
        // C# proxy: use building count * 5 as garrison capacity (Planet has no direct capacity field).
        // TODO: add Planet.GarrisonCapacity when the planet capacity system is implemented.
        int buildingCountForCap = planet.GetAllBuildings().Count;
        sub.EntityCapacity = buildingCountForCap * 5;
        sub.AvailableCapacityA = sub.EntityCapacity - (planet.GetAllRegiments().Count);
        sub.AvailableCapacityB = 0; // second capacity pair — requires entity+0x64/0x68

        // FUN_004334c0 line 182-184: if own planet AND AvailableCapacityA > 0 → set +0x20
        if ((sub.StatusFlags & 0x1u) != 0 && sub.AvailableCapacityA > 0)
        {
            sub.CapabilityFlags |= 0x20u; // own faction has available capacity
            if (sub.AvailableCapacityB > 0)
                sub.CapabilityFlags |= 0x40u;
        }

        // Iterate stationed units. In the original, each unit type has its own iterator.
        // In C#, regiments on the planet are directly accessible.
        int regimentCount = planet.GetAllRegiments().Count;
        int buildingCount = planet.GetAllBuildings().Count;

        sub.TroopCount = regimentCount;
        sub.RegimentCount = regimentCount;

        // FUN_004334c0 line 572: if regiments present (ebx != 0) → CapabilityFlags HIBYTE |= 0x10
        if (regimentCount > 0)
            sub.CapabilityFlags |= 0x1000u; // HIBYTE(CapabilityFlags) bit 4 = 0x1000

        // FUN_004334c0 line 578: if regiment count > 0 AND no enemy (own planet) → may set more flags
        if (regimentCount > 0 && (sub.StatusFlags & 0x1u) != 0 && (sub.CapabilityFlags & 0x3u) == 0)
        {
            // FUN_004334c0 line 575: sub_5087e0(0) check (fleet capacity).
            // Proxy: if entity has fleet capacity → CapabilityFlags HIBYTE |= 0x2
            if (sub.EntityCapacity > 0)
                sub.CapabilityFlags |= 0x200u; // HIBYTE(0x2) = bit 9 = 0x200
        }

        // Iterate capital ships at the planet via fleets.
        // In C# capital ships are in fleets, not directly on planets.
        // Find fleets at this planet owned by any faction.
        if (ws.GameRoot != null)
        {
            int capShipCount = 0;
            int starfighterCount = 0;
            int enemyCapShipCount = 0;

            foreach (Fleet fleet in ws.GameRoot.GetSceneNodesByType<Fleet>())
            {
                Planet fleetPlanet = fleet.GetParentOfType<Planet>();
                if (fleetPlanet != planet)
                    continue;

                bool isOwnFleet = fleet.GetOwnerInstanceID() == ownerFactionId;

                foreach (
                    CapitalShip ship in fleet.CapitalShips
                        ?? new System.Collections.Generic.List<CapitalShip>()
                )
                {
                    if (isOwnFleet)
                    {
                        capShipCount++;
                        sub.CapShipStrength += ship.HullStrength;
                    }
                    else
                    {
                        enemyCapShipCount++;
                    }
                }

                foreach (
                    Starfighter sf in fleet.GetStarfighters()
                        ?? new System.Collections.Generic.List<Starfighter>()
                )
                {
                    if (isOwnFleet)
                        starfighterCount++;
                }
            }

            sub.CapitalShipCount = capShipCount;
            sub.StarfighterCount = starfighterCount;

            // FUN_004334c0 line 568: if capital ships present → CapabilityFlags HIBYTE |= 0x10
            if (capShipCount > 0)
            {
                sub.CapabilityFlags |= 0x1000u;
                if (
                    capShipCount > 0
                    && (sub.StatusFlags & 0x1u) != 0
                    && (sub.CapabilityFlags & 0x3u) == 0
                )
                    sub.CapabilityFlags |= 0x100u; // HIBYTE(0x1)
            }

            // FUN_004334c0 line 724: if unit count (this+0x64 = UnitTypeDCount) >= 2 → StatusFlags |= 0x100
            if (capShipCount + starfighterCount >= 2)
                sub.StatusFlags |= 0x100u;

            // Fleet presence check: if any own fleet at this planet → DispositionFlags upstream
            if (capShipCount > 0 || starfighterCount > 0 || regimentCount > 0)
                sub.StatusFlags |= 0x80u; // character/mission slot (line 762-763: starfighter flag)
        }

        // FUN_004334c0 lines 526-530: mine count (sub_52c4d0) and refinery count (sub_52c0f0).
        // Both are computed unconditionally at the planet, independent of faction.
        // ExtraFlags |= 0x10000 requires mines > 0 || refineries > 0 (checked later in enemy section).
        sub.MineCount = planet.GetBuildingTypeCount(BuildingType.Mine, EntityStateFilter.Active);
        sub.RefineryCount = planet.GetBuildingTypeCount(
            BuildingType.Refinery,
            EntityStateFilter.Active
        );

        // FUN_004334c0 lines 631-688: construction yard availability (sub_52b600 / entity type 0x2a).
        // Binary: iterates construction yards; if any available (*(yard+0x50) & 0x1):
        //   HIBYTE(CapabilityFlags) |= 0x10 (= 0x1000): construction yard present.
        // Then if count > 0 AND own faction (LOBYTE & 0x3 == 0) AND manufacturing manager type 1
        // available (sub_5087e0(0x1)) AND manager not busy (*(mgr+0x58) == 0):
        //   HIBYTE(CapabilityFlags) |= 0x8 (= 0x800): construction yard deployment condition.
        // CapabilityFlags bit 0x800 is the sole driver of DispositionFlags |= 0x2000 in
        // AccumulatePlanetIntoSystemRecord (FUN_004319d0 lines 142-143), which is the primary
        // filter for PreconditionCheck2 (FUN_004191b0 param_1 = 0x2000).
        int constructionYardCount = planet.GetBuildingTypeCount(
            BuildingType.ConstructionFacility,
            EntityStateFilter.Active
        );
        if (constructionYardCount > 0)
        {
            sub.CapabilityFlags |= 0x1000u; // HIBYTE |= 0x10: any construction yard present
            if ((sub.StatusFlags & 0x1u) != 0 && (sub.CapabilityFlags & 0x3u) == 0)
                sub.CapabilityFlags |= 0x800u; // HIBYTE |= 0x8: own planet, construction yard available
        }

        // FUN_004334c0 line 858-868: entity field +0x88 bit 0x1 set → specific ship class
        // This requires reading the entity field directly — proxy: if planet has a manufacturing facility
        // for capital ships → StatusFlags |= 0x8 (line 870)
        if (buildingCount > 0 && (sub.StatusFlags & 0x1u) != 0)
        {
            sub.StatusFlags |= 0x8u; // own planet with facilities
            sub.StatusFlags |= 0x20u; // mission condition flag
        }

        // FUN_004334c0 line 980-990: if own planet and no specific restrictions → CapabilityFlags |= 0x4000000
        if (
            (sub.StatusFlags & 0x1u) != 0
            && (sub.CapabilityFlags & 0x3u) == 0
            && (sub.CapabilityFlags & 0x3e00000u) == 0
        )
        {
            sub.CapabilityFlags |= 0x4000000u;

            // Compute troop surplus/deficit vs capacity thresholds.
            // FUN_004334c0 line 999-1007: if unit_sum < this+0x4c → CapabilityFlags |= 0x100
            int unitSum = sub.CapitalShipCount + sub.StarfighterCount + sub.RegimentCount;
            if (unitSum < sub.EntityCapacity)
            {
                sub.CapabilityFlags |= 0x100u;
                if (sub.AvailableCapacityA > 0)
                    sub.CapabilityFlags |= 0x80u;
            }
        }

        // FUN_004334c0 lines 1016-1033: own planet urgency score computation.
        // urgency = function of (capacity - urgency_base) clamped 0-6.
        // Proxy: use unit count vs capacity as urgency indicator.
        if ((sub.StatusFlags & 0x1u) != 0 && sub.EntityCapacity > 0)
        {
            int deficit = sub.EntityCapacity - (sub.CapitalShipCount + sub.RegimentCount);
            if (deficit > 0)
            {
                // Urgency formula from assembly: (0x46 - capacity) * 0x66666667 / 2^34
                // Simplified: scale deficit to 0-6.
                sub.UrgencyScore = System.Math.Min(
                    6,
                    deficit / System.Math.Max(1, sub.EntityCapacity / 7)
                );
            }
            sub.UrgencyScore =
                (sub.StatusFlags & 0x1u) != 0 ? sub.UrgencyScore * 2 : sub.UrgencyScore;
            sub.UrgencyScore = System.Math.Min(6, sub.UrgencyScore);
        }

        // FUN_004334c0 lines 875-880: popular support for own planets.
        // Line 184: initial *(esi+0x48) = 0x64 (100) when entering own-planet path.
        // Line 877: *(esi+0x48) overwritten with sub_5063f0(raw entity, ownerSide) = actual support.
        // Line 878-880: if support < 0x46 (70) → ExtraFlags |= 0x10000000 (bit 28).
        // ExtraFlags bit 28 propagates to FlagA & 0x8000000 in AccumulatePlanetIntoSystemRecord,
        // which is the gate for PreconditionCheck2 (FUN_004da280) to return 1.
        if ((sub.StatusFlags & 0x1u) != 0)
        {
            int popularSupport = planet.GetPopularSupport(ownerFactionId);
            sub.CapacityThreshold = popularSupport;
            if (popularSupport < 70)
                sub.ExtraFlags |= 0x10000000u;
        }

        // FUN_004334c0 character capability count for own-faction planets.
        // In the binary, this is incremented during character/officer iteration when
        // a character has certain capability flags set. Checked by FUN_004319d0 line 115:
        //   if (cf & 3 == 0 AND sf & 0x8000000 == 0 AND param_1->field111_0xcc > 0):
        //     set DispositionFlags |= 0x20 | 0x40 | 0x80 (unit capacity available bits).
        // These bits are queried by Types 5/6/7 strategy records to find deployment targets.
        // Proxy: own-faction planet with buildings (EntityCapacity > 0) → CharCapabilityCount = 1.
        if ((sub.StatusFlags & 0x1u) != 0 && sub.EntityCapacity > 0)
            sub.CharCapabilityCount = 1;

        // FUN_004334c0 lines 283-289: AuxCount (*(esi+0x90)) incremented per sub_503dc0 unit
        // with *(unit+0x50) & 1 set. sub_503dc0 iterates characters/officers at this planet.
        // Checked at lines 1224/1232 as part of the regiment/starfighter deployment gate.
        // Proxy: own-faction planet with capacity → AuxCount = 1 (at least one available character).
        if ((sub.StatusFlags & 0x1u) != 0 && sub.EntityCapacity > 0)
            sub.AuxCount = 1;

        // FUN_004334c0 enemy planet path (line 1407-1573):
        // Enemy planet: compute urgency and set flags.
        if ((sub.StatusFlags & 0x4u) != 0)
        {
            // FUN_004334c0 line 1427: *(esi+0x2c) |= 0x100 if *(esi+0x48) > 0x32
            if (sub.EntityCapacity > 0x32)
                sub.ExtraFlags |= 0x100u;

            // FUN_004334c0 line 1436-1458: various capacity-driven flag bits.
            if (sub.StrengthAccum > 0)
                sub.ExtraFlags |= 0x40000u;
            if ((sub.StatusFlags & 0x80u) != 0)
                sub.ExtraFlags |= 0x80000u;
            if (sub.UnitStrengthSum > 0)
                sub.ExtraFlags |= 0x100000u;
            if (sub.AuxCount > 0)
                sub.ExtraFlags |= 0x200000u;
            if (sub.CharCapabilityCount > 0)
                sub.ExtraFlags |= 0x400000u;

            // FUN_004334c0 lines 1543-1572: unit presence and mine/refinery flags.
            // 0x20000: enemy planet has ships or regiments (CapitalShipCount/RegimentCount/StarfighterCount > 0).
            // 0x10000: enemy planet has mines or refineries (MineCount/RefineryCount > 0).
            //   → FUN_004319d0 line 329: FlagA |= 0x800 when ExtraFlags & 0x10000.
            //   → PreconditionCheck2 final gate: FlagA & 0x800 on the candidate system.
            if (sub.StarfighterCount > 0 || sub.CapitalShipCount > 0 || sub.RegimentCount > 0)
                sub.ExtraFlags |= 0x20000u;
            if (sub.MineCount > 0 || sub.RefineryCount > 0)
                sub.ExtraFlags |= 0x10000u;
        }

        // Neutral planet path (line 1574-1603):
        if ((sub.StatusFlags & 0x2u) != 0)
        {
            int neutralUrgency = sub.UnitTypeDCount > 0 ? sub.UnitTypeDCount : 0;
            sub.UrgencyScore = System.Math.Min(6, neutralUrgency);
            if ((sub.CapabilityFlags & unchecked((uint)0x80000000)) != 0)
                sub.CharCapabilityCount++;
        }

        // FUN_004334c0 line 1659-1664: final flag (loc_434c2b):
        // If condition fails entirely: ExtraFlags LOBYTE |= 0x80
        // (This path triggers when entity+0x88 bit 0x2 is clear — no ship at planet)
        // Since we can't easily check this without the entity data, leave it as-is.

        // FUN_004334c0 lines 1215-1236: regiment and starfighter deployment-in-progress flags.
        // Gate: no enemy presence at this planet (CapabilityFlags bits 0–1 clear).
        // CapabilityFlags bits 0x200000 and 0x400000 are set by strategy records when they
        // initiate a deployment mission to this planet (sub_4334b0 is called immediately after
        // the strategy record writes the bit). Until C# strategy records propagate these bits,
        // ExtraFlags 0x4 and 0x8 remain unset, and NeedsUnitDeployment stays true for all systems.
        // Once strategy records are implemented, this code activates the assignment-blocking gate.
        if ((sub.CapabilityFlags & 0x3u) == 0)
        {
            // Regiment deployment in progress: ExtraFlags & 0x4 → FlagA |= 0x1 (NeedsUnitDeployment = false).
            // var_184 (any regiment available) proxied by AuxCount > 0.
            // sub_5f2ef0() & 0x2000000 (active mission check) omitted — not yet tracked in C#.
            if ((sub.CapabilityFlags & 0x200000u) != 0 && sub.AuxCount > 0)
                sub.ExtraFlags |= 0x4u;

            // Starfighter deployment in progress: ExtraFlags & 0x8 → FlagA |= 0x2.
            if ((sub.CapabilityFlags & 0x400000u) != 0 && sub.AuxCount > 0)
                sub.ExtraFlags |= 0x8u;
        }
    }

    // -------------------------------------------------------------------------
    // FUN_004319d0 — Per-Planet Accumulator
    //
    // Accumulates data from a planet sub-object into the system analysis record.
    // param_1 = planet_subobj+0x48 (data fields)
    // param_2 = &planet_subobj+0x28 (CapabilityFlags) → read-only
    // param_3 = &planet_subobj+0x30 (StatusFlags) → read-only
    // param_4 = &planet_subobj+0x2c (ExtraFlags) → read-only
    //
    // Branch on param_3 bit 0x1 (own planet):
    //   Own: field35_0x2c |= 4, accumulate many fields, set DispositionFlags bits.
    //   Not own, enemy (param_3 & 4): field35_0x2c |= 8, accumulate enemy fields.
    //   Not own, not enemy: field35_0x2c |= 0x10 (neutral), minimal accumulation.
    //   All paths converge at LAB_004321b8.
    //
    // Returns void. Sets DispositionFlags (+0x24), CapabilityFlags (+0x28),
    // StatusFlags (+0x2c) on the system record.
    // -------------------------------------------------------------------------
    private static void AccumulatePlanetIntoSystemRecord(
        SystemAnalysisRecord sysRec,
        PlanetSubobject sub
    )
    {
        if (sub == null)
            return;

        uint cf = sub.CapabilityFlags; // param_2 = &subobj+0x28
        uint sf = sub.StatusFlags; // param_3 = &subobj+0x30
        uint ef = sub.ExtraFlags; // param_4 = &subobj+0x2c

        if ((sf & 0x1u) == 0)
        {
            // NOT own faction planet.
            if ((sf & 0x4u) == 0)
            {
                // Neither own nor enemy (neutral/unowned), param_3 bit 2 also clear:
                // FUN_004319d0 line 14-26 (Type 0 path):
                sysRec.FlagB |= 0x10; // field35_0x2c |= 0x10 (neutral planet)
                sysRec.Stats.FacilityCount += 1; // field105_0x84 += 1
                sysRec.Stats.FriendlyTroopSurplus += sub.CharStrengthA; // field187 += param_1->field75_0x9c proxy
                sysRec.Stats.NetFighterSurplus += sub.CharStrengthB; // field188
                sysRec.Stats.SystemPriority += sub.CharStrengthC; // field189

                // field152-154 += param_1->field4-6
                // (proxied by unit counts since exact field mappings require full sub-object)

                if ((cf & 0x80000000u) != 0)
                    sysRec.DispositionFlags |= 0x40000000u; // field33 |= 0x40000000

                if ((sf & 0x20u) != 0)
                    sysRec.FlagB |= 0x40; // field35 |= 0x40

                // Accumulate common fields at LAB_004321b8 (all paths):
            }
            else
            {
                // Enemy faction planet (param_3 bit 2 set):
                // FUN_004319d0 lines 29-72 (Type 1 path, enemy):
                sysRec.FlagB |= 0x8; // field35_0x2c |= 0x8 (enemy planet)
                sysRec.Stats.EnemyTroopSurplus += 1; // field104_0x80 += 1
                sysRec.Stats.FriendlyTroopSurplus += sub.UnitCountAccumA; // field150 += param_1->field2_0x8
                sysRec.Stats.NetCapitalShipSurplus += sub.UnitCountAccumB; // field151 += param_1->field3_0xc

                // field149_0x110 += param_1->field1_0x4 - param_1->field24_0x3c
                sysRec.Stats.FleetSurplus += (sub.EntityCapacity - sub.AvailableCapacityA);
                sysRec.Stats.FightersAboveThreshold += sub.CharStrengthA; // field187
                sysRec.Stats.NetFighterSurplus += sub.CharStrengthB;
                sysRec.Stats.SystemPriority += sub.CharStrengthC;
                sysRec.Stats.ThreatenedCount += sub.CombinedStrength; // field112_0xa0
                sysRec.Stats.AlignedEntityCount += sub.FleetStrengthA; // field157_0x130

                // Max-track fields:
                if (sysRec.Stats.FleetFacilityCombatPower < sub.CombinedStrength)
                    sysRec.Stats.FleetFacilityCombatPower = sub.CombinedStrength;
                if (sysRec.Stats.FighterPower < sub.AvailableCapacityA)
                    sysRec.Stats.FighterPower = (int)sub.CapacityThreshold;
                if (sysRec.Stats.OwnShipyardStrength < sub.UrgencyScore)
                    sysRec.Stats.OwnShipyardStrength = sub.UrgencyScore;

                // Availability flag (param_3 bit 0x800):
                if ((sf & 0x800u) != 0)
                {
                    sysRec.FlagB |= 0x100; // field35 |= 0x100
                    if (sub.CapacityThreshold < sysRec.SystemScore)
                        sysRec.SystemScore = sub.CapacityThreshold;
                }
                if ((sf & 0x100u) == 0)
                    sysRec.FlagB |= 0x400; // field35 |= 0x400
                if ((sf & 0x2000u) == 0)
                    sysRec.FlagB |= 0x400000; // field35 |= 0x400000
                if ((ef & 0x2f70000u) != 0)
                    sysRec.FlagB |= 0x80000; // field35 |= 0x80000

                // param_2 & 0x80000000 → field33 |= 0x80000000
                if ((cf & 0x80000000u) != 0)
                    sysRec.DispositionFlags |= 0x80000000u;
            }
        }
        else
        {
            // Own faction planet (param_3 bit 0x1 set):
            // FUN_004319d0 lines 77-165:
            sysRec.FlagB |= 0x4; // field35_0x2c |= 4
            sysRec.Stats.FacilityCount += 1; // field103_0x7c += 1

            // Accumulate own-planet stats into system record:
            sysRec.Stats.FriendlyTroopSurplus += sub.TroopStrength; // field107_0x8c
            sysRec.Stats.EnemyShipCount += sub.CapShipStrength; // field108_0x90 (repurposed)
            sysRec.Stats.FightersAboveThreshold += sub.UnitCountAccumA; // field117_0xa8
            sysRec.Stats.AvailableFighters += sub.UnitCountAccumB; // field118_0xac
            sysRec.Stats.EnemyStrengthAccum += sub.CombinedStrength; // field112_0xa0 (repurposed)
            sysRec.Stats.SummaryScore += sub.FleetStrengthA; // field157_0x130

            // Max-trackers (own planet):
            if (sysRec.Stats.FleetFacilityCombatPower < sub.CombinedStrength)
                sysRec.Stats.FleetFacilityCombatPower = sub.CombinedStrength;
            if (sysRec.Stats.FighterPower < sub.AvailableCapacityA)
                sysRec.Stats.FighterPower = sub.AvailableCapacityA;
            if (sysRec.Stats.OwnShipyardStrength < sub.UrgencyScore)
                sysRec.Stats.OwnShipyardStrength = sub.UrgencyScore;

            // Shortage flags from param_3:
            if ((sf & 0x40000u) != 0)
            {
                sysRec.FlagB |= 0x800000; // field35 |= 0x800000 (garrison shortage)
                if ((sf & 0x10000u) != 0)
                    sysRec.FlagB |= 0x1000000;
                else if ((sf & 0x20000u) != 0)
                    sysRec.FlagB |= 0x2000000;
            }

            // param_2 (CapabilityFlags) bit operations → field33 (DispositionFlags):
            if ((cf & 0x2u) == 0)
                sysRec.DispositionFlags |= 0x8u;
            if (
                (cf & 0x3u) == 0
                && (sf & 0x8000000u) == 0
                && sub.CharCapabilityCount > 0
            )
            {
                if ((cf & 0x200000u) == 0)
                    sysRec.DispositionFlags |= 0x20u;
                if ((cf & 0x400000u) == 0)
                    sysRec.DispositionFlags |= 0x40u;
                if ((cf & 0x3800000u) == 0)
                    sysRec.DispositionFlags |= 0x80u;
            }
            if ((cf & 0x80u) != 0 && (cf & 0x3u) == 0)
                sysRec.DispositionFlags |= 0x100u;
            if ((cf & 0x100u) != 0 && (cf & 0x3u) == 0)
                sysRec.DispositionFlags |= 0x200u;
            if ((cf & 0x20u) != 0 && (cf & 0x3u) == 0)
                sysRec.DispositionFlags |= 0x400u;
            if ((cf & 0x40u) != 0 && (cf & 0x3u) == 0)
                sysRec.DispositionFlags |= 0x800u;
            if ((cf & unchecked((uint)0x800000)) != 0 && (cf & 0x800u) != 0 && (cf & 0x3u) == 0)
                sysRec.DispositionFlags |= 0x1000u;
            if ((cf & 0x800u) != 0 && (cf & 0x3800000u) == 0)
                sysRec.DispositionFlags |= 0x2000u;

            // FUN_004319d0 lines 145-172: per-type unit count accumulators.
            // DispositionFlags |= 0x20000000 and the SumA fields are gated on cf & 0x3e00000.
            // SumB fields apply when cf lacks the corresponding bit (lines 164-172, outside the block).
            if ((cf & 0x3e00000u) != 0)
            {
                sysRec.DispositionFlags |= 0x20000000u;
                if ((cf & 0x200000u) != 0)
                {
                    if ((cf & 0x2u) == 0)
                        sysRec.FlagB |= 0x20000; // field35 |= 0x20000 (cf & 0x200000, cf & 0x2 clear)
                    sysRec.Stats.CapShipAtTarget += sub.CapitalShipCount;
                }
                if ((cf & 0x400000u) != 0)
                    sysRec.Stats.RegimentAtTarget += sub.RegimentCount;
                if ((cf & 0x3800000u) != 0)
                    sysRec.Stats.StarfighterAtTarget += sub.StarfighterCount;
            }
            if ((cf & 0x3800000u) == 0)
                sysRec.Stats.StarfighterNotAtTarget += sub.StarfighterCount; // statIndex 0x15
            if ((cf & 0x400000u) == 0)
                sysRec.Stats.RegimentNotAtTarget += sub.RegimentCount;
            if ((cf & 0x200000u) == 0)
                sysRec.Stats.CapShipNotAtTarget += sub.CapitalShipCount;
            if ((cf & unchecked((uint)0x80000000)) != 0)
            {
                sysRec.DispositionFlags |= 0x40000000u;
                if ((ef & 0x40000000u) != 0)
                    sysRec.FlagB |= 0x200;
                if ((cf & 0x2u) == 0)
                    sysRec.Stats.StandardCharCount++;
            }
        }

        // --- LAB_004321b8: common ending for all paths ---
        sysRec.Stats.EnemyStrengthAccum += sub.CharStrengthA; // field190_0x19c += param_1->field110_0xc8

        // param_4 (ExtraFlags) bit operations → field100/101/102/106/34_0x28/35_0x2c:
        if ((ef & 0x8000000u) != 0)
            sysRec.Stats.UrgencyScore++; // field100_0x70 (count0)
        if ((sf & 0x8u) != 0)
            sysRec.Stats.ThreatenedCount++; // field101_0x74 (count1)
        if ((sf & 0x20u) != 0)
            sysRec.Stats.AlignedEntityCount++; // field102_0x78 (count2)
        if ((sf & 0x10u) != 0)
            sysRec.Stats.FleetSurplus++; // field106_0x88 (count6)
        if ((sf & 0x40u) != 0)
            sysRec.FlagB |= 0x20; // field35 |= 0x20
        if ((sf & 0x40000000u) != 0)
            sysRec.FlagB |= 0x40000000; // field35 |= 0x40000000
        if ((sf & 0x80000000u) != 0)
            sysRec.FlagB |= unchecked((int)0x80000000); // field35 |= 0x80000000
        if ((sf & 0x200u) != 0)
            sysRec.FlagB |= 0x40000; // field35 |= 0x40000
        if ((ef & 0x1u) != 0)
            sysRec.Stats.CharGarrisonCountA++; // field171_0x150
        if ((ef & 0x2u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x8;
        } // field175+field28 |= 8
        if ((ef & 0x4u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x1;
        } // field172+field28 |= 1
        if ((ef & 0x8u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x2;
        } // field173+field28 |= 2
        if ((ef & 0x10u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x4;
        } // field174+field28 |= 4
        if ((ef & 0x20u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x10;
        } // field176+field28 |= 0x10
        if ((ef & 0x40u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= unchecked((int)(0x20u | 0x4000020u));
        }
        if ((ef & 0x80u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x40;
        }
        if ((ef & 0xc00u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x80;
        }
        if ((ef & 0x3000u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x100;
        }
        if ((ef & 0x100u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x200;
        }
        if ((ef & 0x200u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x400;
        }
        if ((ef & 0x10000u) != 0)
            sysRec.FlagA |= 0x800;
        if ((ef & 0x20000u) != 0)
            sysRec.FlagA |= 0x1000;
        if ((ef & unchecked((uint)0x800000)) != 0)
            sysRec.FlagA |= 0x2000;
        if ((ef & 0xc000u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x4000;
        }
        if ((ef & 0x1000000u) != 0)
        {
            sysRec.Stats.CharGarrisonCountA++;
            sysRec.FlagA |= 0x8000;
        }
        // FUN_004319d0 lines 249-251: ExtraFlags bit 28 (0x10000000) → FlagA bit 27 (0x8000000).
        // Set when own planet popular support < 70. Gate for PreconditionCheck2 return value.
        if ((ef & 0x10000000u) != 0)
            sysRec.FlagA |= 0x8000000;
    }

    // FUN_0041b230: secondary fleet score accumulator called during calibration state 5.
    //
    // Parameters (from FUN_00417cb0 call site):
    //   this (workspace) — receives accumulations into FleetSecondaryScores array (+0x24c)
    //   param_1 — unused in function body (assembly confirms no read of arg_0)
    //   param_2 — fleet stats sub-object (astruct_391) at fleet node+0x70
    //   param_3 — OwnershipFlags pointer from fleet node+0x38
    //
    // Branch on *param_3 & 0x1 (own-side flag):
    //   Own-side: accumulates into FleetSecondaryScores[0..6] and [14..20] max-trackers.
    //   Enemy:    accumulates into FleetSecondaryScores[14..20] counters and max-tracker.
    //
    // FleetSecondaryScores array indices (base workspace+0x24c):
    //   [0] +0x24c <- stats+0x38 ScaledCount (own-side)
    //   [1] +0x250 <- stats+0x3c SizeTier (own-side)
    //   [2] +0x254 <- stats+0x40 WarshipAttribute (own-side)
    //   [3] +0x258 <- stats+0x64 CombatStrength (own-side)
    //   [4] +0x25c <- stats+0x2c CombatModifier (own-side)
    //   [5] +0x260 <- stats+0x6c TertiaryValue (own-side)
    //   [6] +0x264 <- stats+0x44 CapacityField (own-side)
    //   [7] +0x268 <- stats+0x48 CapacityProduct (own-side capital ships only)
    //   [8] +0x26c <- stats+0x4c WarshipCount (own-side capital ships only)
    //   [9] +0x270 <- stats+0x50 Val_0x50 (own-side capital ships only)
    //  [10] +0x274 <- stats+0x64 CombatStrength (own-side capital ships only)
    //  [11] +0x278 <- stats+0x68 SecondaryStrength (own-side capital ships only)
    //  [12] +0x27c <- stats+0x28 FactionAlignment (own-side capital ships only)
    //  [13] +0x280 <- stats+0x18 CategoryFlags (own-side capital ships only)
    //  [14] +0x284 enemy count
    //  [15] +0x288 <- stats+0x64 CombatStrength (enemy)
    //  [16] +0x28c <- stats+0x28 FactionAlignment (enemy)
    //  [17] +0x290 <- stats+0x68 SecondaryStrength (enemy)
    //  [18] +0x294 max enemy CombatStrength
    //  [19] +0x298 paired SecondaryStrength
    //  [20] +0x29c paired FactionAlignment
    //
    // StatusFlags bits set by own-side flags:
    //   *param_3 & 0x10 → StatusFlags |= 0x4
    //   *param_3 & 0x20 → StatusFlags |= 0x8
    private static void AccumulateFleetSecondaryScores(AIWorkspace ws, FleetAnalysisRecord rec)
    {
        if (rec.Fleet == null)
            return;

        string ownerId = ws.Owner?.InstanceID;
        bool isOwn = rec.Fleet.GetOwnerInstanceID() == ownerId;
        bool hasCapShips = rec.Fleet.CapitalShips?.Count > 0;

        // OwnershipFlags: bit 0x1 = own-side, bit 0x4 = capital ships present.
        // Bits 0x10 / 0x20 are fleet-specific flags from the analysis pipeline;
        // not yet computed (requires FUN_0042fb50 full implementation).
        uint ownershipFlags = 0;
        if (isOwn)
            ownershipFlags |= 0x1;
        if (hasCapShips)
            ownershipFlags |= 0x4;

        FleetUnitStats s = rec.Stats;
        int[] fss = ws.FleetSecondaryScores;

        if ((ownershipFlags & 0x1) != 0)
        {
            // Own-side capital ships (bit 0x4):
            if ((ownershipFlags & 0x4) != 0)
            {
                fss[7] += s.CapacityProduct; // +0x268 <- stats+0x48
                fss[8] += s.WarshipCount; // +0x26c <- stats+0x4c
                fss[9] += s.Val_0x50; // +0x270 <- stats+0x50
                fss[10] += s.CombatStrength; // +0x274 <- stats+0x64
                fss[11] += s.SecondaryStrength; // +0x278 <- stats+0x68
                fss[12] += s.FactionAlignment; // +0x27c <- stats+0x28
                fss[13] += s.CategoryFlags; // +0x280 <- stats+0x18
            }

            // All own-side units:
            fss[4] += s.CombatModifier; // +0x25c <- stats+0x2c
            fss[5] += s.TertiaryValue; // +0x260 <- stats+0x6c
            fss[3] += s.CombatStrength; // +0x258 <- stats+0x64
            fss[0] += s.ScaledCount; // +0x24c <- stats+0x38
            fss[1] += s.SizeTier; // +0x250 <- stats+0x3c
            fss[2] += s.WarshipAttribute; // +0x254 <- stats+0x40

            if ((ownershipFlags & 0x10) != 0)
                ws.StatusFlags |= 0x4; // own fleet sub-flag A
            if ((ownershipFlags & 0x20) != 0)
                ws.StatusFlags |= 0x8; // own fleet sub-flag B

            fss[6] += s.CapacityField; // +0x264 <- stats+0x44
        }
        else
        {
            // Enemy unit:
            fss[14] += 1; // +0x284 enemy count
            fss[15] += s.CombatStrength; // +0x288 <- stats+0x64
            fss[16] += s.FactionAlignment; // +0x28c <- stats+0x28
            fss[17] += s.SecondaryStrength; // +0x290 <- stats+0x68

            // Max-track by CombatStrength (signed comparison matches assembly jg):
            if (s.CombatStrength > fss[18])
            {
                fss[18] = s.CombatStrength; // +0x294
                fss[19] = s.SecondaryStrength; // +0x298
                fss[20] = s.FactionAlignment; // +0x29c
            }
        }
    }

    private static void AccumulateCharacterScores(AIWorkspace ws, CharacterAnalysisRecord rec)
    {
        // FUN_0041b3c0: per-character score accumulator.
        if (rec.Officer == null)
            return;

        int idx = ws.CharacterAnalysis.IndexOf(rec);
        if (idx >= 0 && idx < ws.CharacterScores.Length)
            ws.CharacterScores[idx] = rec.CharacterScore;
    }

    private static void ComputeFleetScoreRange(AIWorkspace ws, int start, int end)
    {
        // Iterates FleetScores within [start, end) and applies derived scoring.
        for (int i = start; i < end && i < ws.FleetScores.Length; i++)
        {
            if (ws.FleetScores[i] > 0)
                ws.FleetScores[i] = ws.FleetScores[i] / 2 + 1;
        }
    }

    private static void ComputeCharacterScoreRange(AIWorkspace ws, int start, int end)
    {
        for (int i = start; i < end && i < ws.CharacterScores.Length; i++)
        {
            if (ws.CharacterScores[i] > 0)
                ws.CharacterScores[i] = ws.CharacterScores[i] / 2 + 1;
        }
    }

    // ----------------------------------------------------------------
    // Entity enumeration helpers.
    // ----------------------------------------------------------------

    private static IEnumerable<PlanetSystem> GetVisibleSystems(AIWorkspace ws)
    {
        if (ws.GameRoot == null)
            return System.Array.Empty<PlanetSystem>();
        // Include all systems in the galaxy. The AI needs to analyze every system
        // to find where shortages exist, where enemies are, and where to send fleets.
        // Matches the original which walks the full game entity list.
        return ws.GameRoot.GetSceneNodesByType<PlanetSystem>();
    }

    private static IEnumerable<Fleet> GetVisibleFleets(AIWorkspace ws)
    {
        if (ws.GameRoot == null)
            return System.Array.Empty<Fleet>();
        // Include all fleets — own AND enemy — so the analysis has threat data.
        return ws.GameRoot.GetSceneNodesByType<Fleet>();
    }

    private static IEnumerable<Officer> GetVisibleOfficers(AIWorkspace ws)
    {
        if (ws.Owner == null)
            return System.Array.Empty<Officer>();
        return ws.Owner.GetOwnedUnitsByType<Officer>();
    }
}
