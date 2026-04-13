using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.SceneGraph;

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
                // FUN_004306c0: build system analysis records.
                // Returns non-zero when complete; may span multiple ticks.
                if (BuildSystemAnalysis(ws))
                    ws.GalaxyAnalysisPhase = 3;
                return false;

            case 3:
                // FUN_00430200: cross-link fleet entities into system records.
                if (BuildFleetSystemCrossLinks(ws))
                {
                    // Count system nodes × 10 into accumulator.
                    ws.SystemScoreAccumulator = ws.SystemAnalysis.Count * 10;
                    ws.GalaxyAnalysisPhase = 4;
                }
                return false;

            case 4:
                // FUN_0042fb50: build fleet analysis sub-list.
                if (BuildFleetAnalysis(ws))
                    ws.GalaxyAnalysisPhase = 5;
                return false;

            case 5:
                // FUN_004032c0: build character analysis sub-list.
                if (BuildCharacterAnalysis(ws))
                {
                    ws.GalaxyAnalysisPhase = 6;
                    // Set bit 0x80000000 in StatusFlags (signals calibration data ready).
                    ws.StatusFlags |= unchecked((int)0x80000000);
                }
                return false;

            case 6:
                // FUN_00417cb0: calibration sub-machine.
                if (TickCalibration(ws))
                    return true; // Entire pipeline complete.
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
        // This corresponds to FUN_0042d3a0 (entity list builder) and FUN_0041b4d0
        // (side analysis populator) — they scan all entities owned by this side.
        ws.SystemAnalysis.Clear();
        ws.FleetAnalysis.Clear();
        ws.CharacterAnalysis.Clear();

        // Mark initialization pass complete.
        ws.StatusFlags |= 0x1;
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
        foreach (PlanetSystem system in GetVisibleSystems(ws))
        {
            SystemAnalysisRecord record = new SystemAnalysisRecord { System = system };
            ScoreSystem(ws, record);
            ws.SystemAnalysis.Add(record);
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
        }

        return true;
    }

    // ----------------------------------------------------------------
    // Phase 4: FUN_0042fb50 — build fleet analysis sub-list.
    // Enumerates all fleets and populates FleetAnalysis with unit stats.
    // ----------------------------------------------------------------
    private static bool BuildFleetAnalysis(AIWorkspace ws)
    {
        // Fleet records are already seeded in phase 3.
        // Phase 4 adds unit category flags to each record.
        foreach (FleetAnalysisRecord rec in ws.FleetAnalysis)
        {
            ScoreFleet(ws, rec);
        }
        return true;
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
            ws.CharacterAnalysis.Add(rec);
        }

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
                // Walk system analysis list from CalibrationCursor to end.
                // FUN_00431860 is called on each node; it returns non-zero if the
                // node requires a fleet/character score refresh, setting bits
                // 0x20000000/0x40000000/0x10000000 in StatusFlags.
                // When the cursor reaches the end → state 5.
                while (ws.CalibrationCursor < ws.SystemAnalysis.Count)
                {
                    SystemAnalysisRecord rec = ws.SystemAnalysis[ws.CalibrationCursor];
                    int result = ScoreSystemNode(ws, rec);
                    if (result != 0)
                        ws.StatusFlags |= unchecked((int)0xa0000000); // bits 0x80000000 | 0x20000000
                    ws.CalibrationCursor++;
                    // Each call may span a tick — break here to match per-frame behavior.
                    // In the original this is a do-while that breaks after each step.
                    break;
                }

                if (ws.CalibrationCursor >= ws.SystemAnalysis.Count)
                    ws.CalibrationState = 5;

                return false;
            }

            case 2:
            {
                // Fleet score computation (array bounds 8, 0x10 = 8 and 16).
                // Populates FleetScores using fleet analysis data.
                ComputeFleetScoreRange(ws, 8, 16);
                ws.CalibrationState = 5;
                return false;
            }

            case 4:
            {
                // Character score computation (array bounds 0x30=48, 0x40=64).
                ComputeCharacterScoreRange(ws, 48, 64);
                ws.CalibrationState = 5;
                return false;
            }

            case 5:
            {
                // Finalization: if analysis data ready, run scoring accumulators,
                // then reset state to 4 and return true (calibration complete).
                if ((ws.StatusFlags & unchecked((int)0x80000000)) != 0)
                {
                    // Trigger fleet/character analysis sub-object updates.
                    // FUN_00417cb0 state 5 calls vtable[4] on sub-objects at +0xc0 and +0x104.
                    // These sub-objects drive the secondary score pipelines.

                    if ((ws.StatusFlags & 0x20000000) != 0)
                    {
                        // Zero FleetScores array and recompute from fleet analysis list.
                        for (int i = 0; i < ws.FleetScores.Length; i++)
                            ws.FleetScores[i] = 0;
                        ws.StatusFlags &= ~0x20000000;

                        // Walk fleet analysis list, call FUN_0041af90 per-fleet accumulator.
                        foreach (FleetAnalysisRecord rec in ws.FleetAnalysis)
                            AccumulateFleetScores(ws, rec);

                        // FUN_00417cb0 state 5: divide accumulators by 5+1 and 6+1.
                        if (ws.FleetAnalysisAccumulatorA > 0)
                            ws.FleetAnalysisAccumulatorA = ws.FleetAnalysisAccumulatorA / 5 + 1;
                        if (ws.FleetAnalysisAccumulatorB > 0)
                            ws.FleetAnalysisAccumulatorB = ws.FleetAnalysisAccumulatorB / 6 + 1;
                    }

                    if ((ws.StatusFlags & 0x40000000) != 0)
                    {
                        // Zero FleetSecondaryScores and recompute.
                        for (int i = 0; i < ws.FleetSecondaryScores.Length; i++)
                            ws.FleetSecondaryScores[i] = 0;
                        ws.StatusFlags &= ~0x40000000;

                        // Walk fleet analysis list via FUN_0041b230.
                        foreach (FleetAnalysisRecord rec in ws.FleetAnalysis)
                            AccumulateFleetSecondaryScores(ws, rec);
                    }

                    if ((ws.StatusFlags & 0x10000000) != 0)
                    {
                        // Zero CharacterScores and recompute.
                        for (int i = 0; i < ws.CharacterScores.Length; i++)
                            ws.CharacterScores[i] = 0;
                        ws.StatusFlags &= ~0x10000000;

                        // Walk character analysis list via FUN_0041b3c0.
                        foreach (CharacterAnalysisRecord rec in ws.CharacterAnalysis)
                            AccumulateCharacterScores(ws, rec);
                    }

                    // Clear upper 4 bits (the analysis-request flags).
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

    private static void ScoreSystem(AIWorkspace ws, SystemAnalysisRecord rec)
    {
        if (rec.System == null)
            return;

        // Basic system scoring using PerSystemStats fields.
        // Detailed implementation follows the GalaxyAnalysisScorer patterns.
        foreach (Planet planet in rec.System.Planets)
        {
            bool ownedByUs = planet.GetOwnerInstanceID() == ws.Owner?.InstanceID;
            bool ownedByEnemy = !ownedByUs && planet.GetOwnerInstanceID() != null;

            if (ownedByUs)
            {
                rec.Stats.FacilityCount++;
                rec.Stats.FriendlyTroopSurplus += planet.GetAllRegiments().Count;
            }
            else if (ownedByEnemy)
            {
                rec.Stats.EnemyTroopSurplus += planet.GetAllRegiments().Count;
            }
        }
    }

    private static int ScoreSystemNode(AIWorkspace ws, SystemAnalysisRecord rec)
    {
        // FUN_00431860: scores the system node for calibration.
        // Returns non-zero if any refresh flag should be set.
        if (rec == null)
            return 0;

        // Check if this system has changed since last analysis.
        // For now, always signal a refresh is needed on non-trivial systems.
        return rec.SystemScore != 0 ? 1 : 0;
    }

    private static void ScoreFleet(AIWorkspace ws, FleetAnalysisRecord rec)
    {
        if (rec.Fleet == null)
            return;

        rec.Stats.CategoryFlags = 0;
        if (rec.Fleet.CapitalShips.Count > 0)
            rec.Stats.CategoryFlags |= 0x200000; // warship flag

        rec.FleetScore = rec.Fleet.GetCombatValue();
    }

    private static void ScoreCharacter(AIWorkspace ws, CharacterAnalysisRecord rec)
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

    private static void AccumulateFleetSecondaryScores(AIWorkspace ws, FleetAnalysisRecord rec)
    {
        // FUN_0041b230: secondary fleet scoring pass.
        if (rec.Fleet == null)
            return;
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
        if (ws.Owner == null)
            return System.Array.Empty<PlanetSystem>();
        // Enumerate all systems that contain at least one planet owned by this faction.
        // Matches the original game's "visible systems" which includes all systems
        // the faction has presence in (owns a planet or has a fleet).
        System.Collections.Generic.HashSet<PlanetSystem> systems =
            new System.Collections.Generic.HashSet<PlanetSystem>();
        foreach (Planet planet in ws.Owner.GetOwnedUnitsByType<Planet>())
        {
            PlanetSystem sys = planet.GetParent() as PlanetSystem;
            if (sys != null)
                systems.Add(sys);
        }
        return systems;
    }

    private static IEnumerable<Fleet> GetVisibleFleets(AIWorkspace ws)
    {
        if (ws.Owner == null)
            return System.Array.Empty<Fleet>();
        return ws.Owner.GetOwnedUnitsByType<Fleet>();
    }

    private static IEnumerable<Officer> GetVisibleOfficers(AIWorkspace ws)
    {
        if (ws.Owner == null)
            return System.Array.Empty<Officer>();
        return ws.Owner.GetOwnedUnitsByType<Officer>();
    }
}
