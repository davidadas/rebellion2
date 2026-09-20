# Strategic AI Structure Migration Plan

## Objective

Refactor the strategic AI into one enforceable pipeline without changing gameplay:

```text
Turn facts -> Plan proposals -> Score proposals -> Allocate proposals -> Execute proposals
```

Medium seed `12345`, large galaxy, tick `1000` must finish in exactly the same persisted game
state before and after the migration. Structural work is not allowed to tune weights, change
eligibility, alter iteration order, consume a different number of random values, or change proposal
execution order.

## Current Evidence

- `AIDirector` constructs one `AITurnContext`, then runs special-forces intent, planning, scoring,
  selection, decoy assignment, and execution.
- `AIPlanningPhase` invokes six top-level planners in a fixed order.
- `AIScoringPhase` scores only proposals without a score. Mission and new-attack planning already
  perform bounded scoring while enumerating candidates.
- `AISelectionPhase` globally orders proposals and delegates claims and reservations to
  `AIProposalSelectionPolicy`.
- `AIProposalSelectionPolicy` also changes production producer, demand, and quantity after the
  proposal has been scored.
- `AIAssessment` is a turn-scoped cache and index, but also owns weighted diplomacy utility,
  attack requirements, readiness policy, and defense policy.
- `AIDemand` is used only for production. It is rebuilt every turn and is not a persistent AI goal.
- `AIProductionDemandGenerator` is 2,712 lines and mixes economy, infrastructure, fleet,
  colonization, garrison, planetary-defense, and special-forces requirements.
- `AIProductionPlanner` is 1,704 lines and mixes technology choice, producer choice, batching,
  queue allocation, and proposal construction.
- `AIReinforcementArrivalForecast` has one operational consumer: producer ordering in
  `AIProductionPlanner`. The planner already caches the resulting producer ordering.
- Existing utility architecture documentation references the removed
  `AIInfrastructureAllocationScorer` and therefore is not authoritative for the current tree.

## Non-Negotiable Contracts

### Turn facts

- `AIAssessment` may cache and expose observed or projected facts.
- It may not apply utility weights, choose actions, or answer whether an action is strategically
  desirable.
- A fact query may call a game system calculation, but it must not encode AI preference.
- A scene-graph collection is resolved once per turn or once per lazy indexed fact, never once per
  candidate.

### Planning

- An `IAIProposalPlanner` enumerates feasible action proposals for one domain.
- High-cardinality enumeration may score and prune in the planner only through the same canonical
  scorer used by the scoring phase and only with a proven upper bound.
- Planning must preserve the present planner order, candidate order, proposal order, and RNG calls
  until behavioral changes are separately authorized.

### Proposals

- A proposal's action identity is immutable after construction.
- Action identity includes actor, target, product, producer, destination, and requested quantity
  whenever those values affect scoring or execution.
- `Score` may be attached after construction; it is decision metadata, not action identity.
- `CanSelect` and `CanExecute` revalidate rules. They do not redesign the action.
- A proposal may execute itself. A second command hierarchy is not introduced unless execution is
  proven to have an independent lifecycle.

### Scoring

- A scorer evaluates the exact action represented by the proposal.
- Scorers do not mutate action identity.
- Utility weights and curves live with the domain scorer, not in `AIAssessment`.
- Feasibility remains a typed rule, not an oversized score weight.

### Allocation

- Global allocation may accept or reject a proposal and reserve claims, capacity, energy, and
  maintenance.
- Allocation may not replace the producer, demand, destination, target, or product.
- Quantity reduction is permitted only if quantity is explicitly modeled as an allocatable range
  and the score is quantity-invariant. Otherwise each selectable quantity is a concrete proposal.
- Equal-score randomization must consume exactly the same RNG calls during behavior-preserving
  slices.

### Execution

- Execution occurs in selected-proposal order.
- Execution revalidates live game rules and emits the same requests and results as before.
- Structural migration cannot change system APIs or game rules.

## Target Runtime Types

The target deliberately reuses the existing pipeline. It does not create parallel goal, task,
command, decision-source, or behavior-tree hierarchies.

### Core pipeline

- `AIDirector`: constructs the turn and runs phases.
- `AITurnContext`: turn dependencies, assessment, proposals, selections, and results.
- `AIAssessment`: cached factual read model only.
- `IAITurnPhase`, `IAIIncrementalTurnPhase`: phase contracts.
- `AIPlanningPhase`, `AIScoringPhase`, `AISelectionPhase`,
  `AIMissionDecoyAssignmentPhase`, `AIExecutionPhase`: pipeline.
- `AIProposalAllocator`: renamed `AIProposalSelectionPolicy`; claims and resource reservations only.
- `AIProposal`, `AIProposalPriority`, `AIClaimKeys`: common proposal contract.
- `IAIProposalPlanner`, `IAIProposalScorer`: extension contracts.
- `AIUtility`, `AIUtilityScore`, `AIUtilityDomain`: shared utility math.

### Fleet

- `AIFleetPlanner`: owns fleet-order proposal enumeration and preserves current emission order.
- `AIFleetDefensePlanner`: retains sequential scarce-fleet allocation.
- `AIFleetAttackPlanner`: extracted attack-candidate enumeration, bounded scoring, and pruning used
  by the fleet planner.
- `AIFleetProposalScorer`: all fleet-proposal utility.
- `AIAttackRequirements`: attack combat, regiment, bombardment, and readiness policy currently in
  `AIAssessment`.
- Existing fleet proposal types remain; their action fields become immutable.
- `AIColonizationTargetScorer`, `AIFleetReinforcementUtility`, and
  `AIFleetProductionAllocationScorer` remain only while each has multiple real callers or an
  independently tested policy.

### Missions

- `AIMissionPlanner`: mission candidate enumeration.
- `AIMissionCandidatePool`: renamed bounded mission-candidate collection.
- `AIMissionProposalScorer`: mission odds and utility, including diplomacy target utility moved
  from `AIAssessment`.
- `AIAbortMissionPlanner`, `AIMissionProposal`, and `AIAbortMissionProposal` remain.
- `AISpecialForcesIntentPhase` remains a separate pre-planning phase only while mission planning
  consumes its turn-scoped intent before production requirements are built.

### Production

- `AIProductionRequirement`: renamed `AIDemand`; an immutable description of a production deficit.
- `AIProductionRequirementKind`: renamed `AIDemandKind`.
- `AIProductionRequirements`: coordinates requirement collection in the current deterministic order.
- `AIEconomyRequirements`: colony bootstrap, mines, and refineries.
- `AIInfrastructureRequirements`: production facilities, upgrades, static defenses, planetary
  fighters, and garrisons. The same target calculations are consumed by facility removal.
- `AIForceRequirements`: fleet seeds, colonization fleet seeds, and fleet reinforcement.
- `AISpecialForcesRequirements`: special-forces supply.
- `AIProductionPlanner`: maps requirements to exact product/producer/count proposals.
- `AIProductionProposalScorer`: scores exact manufacturing proposals.
- `AIManufactureProposal`: immutable exact manufacturing action.
- `AICleanupProposalScorer` remains for mandatory cleanup proposals.

Four production requirement components are justified by different inputs and fulfillment rules.
No common `AIDemandSource` base class is retained; `AIProductionRequirements` calls the four
components directly. This avoids both a 2,700-line generator and a hierarchy of one-method source
classes.

## Current-Type Disposition

### Keep with responsibility unchanged

- `AIDirector`
- `AITurnContext`
- Pipeline phase interfaces and phase classes
- `IAIProposalPlanner`, `IAIProposalScorer`
- Common utility types
- Concrete non-production proposal types
- `AIAbortMissionPlanner`, `AIOrbitalEngagementPlanner`
- Canonical fleet, mission, production, cleanup, colonization, infrastructure-placement, and fleet
  allocation scorers

### Rename without behavior change

- `AIProposalSelectionPolicy` -> `AIProposalAllocator`
- `AIDemand` -> `AIProductionRequirement`
- `AIDemandKind` -> `AIProductionRequirementKind`
- `AIProductionDemandGenerator` -> `AIProductionRequirements`
- `AIFleetAttackCandidateSelector` -> `AIFleetAttackPlanner`
- `AIMissionCandidateSelector` -> `AIMissionCandidatePool`

Renames occur only after responsibility changes are complete, so names describe final behavior and
git history remains reviewable during extraction.

### Extract

- Weighted diplomacy utility from `AIAssessment` to `AIMissionProposalScorer`.
- Attack-package and launch/readiness policy from `AIAssessment` to `AIAttackRequirements`.
- Economy, infrastructure, force, and special-forces requirement calculation from
  `AIProductionDemandGenerator` into the four production components above.
- Facility target calculation shared by construction and removal into
  `AIInfrastructureRequirements`.

### Delete after callers migrate

- `AIDemandSource`
- `AIColonyDemandSource`
- `AISpecialForcesDemandSource`
- `AIInfrastructureDemandPlanner`
- `AIManufactureOption`
- `AIReinforcementArrivalForecast`
- Mutable `SelectOption`, `SelectProducer`, and `SelectManufacturingCount` APIs
- Stale architecture-document references to deleted types

### Retain until a later proven seam

- `AIStrategicPlan`: do not expand it into a universal goal object. It currently owns fleet scale
  and defense commitments. Rename or split it only after those consumers migrate.
- `AIFleetPlanner` and `AIMissionPlanner`: do not split solely because of line count. Extract only
  independently owned policy or bounded candidate state.
- Proposal execution methods: do not create parallel command classes merely for layering purity.

## Migration Sequence

Every pull request below must be independently reviewable, preserve compilation, and pass the
equivalence gate before the next begins.

### PR 0: Capture the authoritative baseline

1. Verify Unity has finished compiling and loaded assemblies are newer than every modified C# file.
2. Record game, media, content/config, and Unity revisions.
3. Run Medium seed `12345`, large galaxy, to tick `1000`, saving as `FNALL1`.
4. Repeat from a clean generated game to prove the current build is deterministic.
5. Require both runs to match after normalizing save metadata timestamps, output paths, and
   `MovementGroupID` values. Movement group identifiers currently use nondeterministic GUIDs;
   canonicalize them by first occurrence while preserving every equality relationship.
6. Store:
   - Complete simulation JSON.
   - Simulation log and timing percentiles.
   - Saved game.
   - Canonical save-state SHA-256.
   - `GameRoot.RandomIndex`.
7. If the two current-build runs differ, stop. Determinism must be fixed or the nondeterministic
   fields explicitly identified before refactoring.

### PR 1: Add the equivalence verifier and architecture contract tests

1. Add an editor/test-only save-state fingerprint utility. Do not add it to the AI hot path.
2. Load a save, normalize only:
   - `Metadata.LastSavedUtc`.
   - Save display/file naming fields with no gameplay meaning.
   - `MovementGroupID` identity by first occurrence. The canonicalizer must preserve which units
     share a movement group and may not remove movement groups, reorder them, or ignore movement
     timing and position.
3. Serialize the complete `GameRoot` through `GameSerializer` and hash the resulting canonical XML.
4. Compare baseline and candidate:
   - Canonical save hash.
   - Current tick.
   - `RandomIndex`.
   - Complete normalized simulation JSON.
5. Add contract tests proving:
   - Scoring cannot change proposal action identity.
   - Allocation cannot change non-allocation proposal fields.
   - Execution preserves selected order.
   - Pre-scored bounded candidates are not rescored.
6. This PR changes no AI decisions and must match the baseline exactly.

### PR 2: Make production resolution explicit without changing decisions

1. Characterize every current production-option fallback with tests before editing:
   - First producer accepted.
   - First producer blocked by capacity.
   - First producer blocked by claims.
   - Producer-specific adjusted demand fallback.
   - Count reduced by capacity.
   - Count reduced by maintenance.
2. Record candidate counts and the selected producer/count for representative late-game saves.
3. Introduce exact immutable manufacturing proposals while preserving current producer ordering.
4. Emit alternatives in the same preference order and give alternatives for one requirement the
   same production-demand claim.
5. Preserve the number and order of proposals entering every equal-score group. If exact candidates
   would change tie-group cardinality, perform deterministic domain allocation before global
   proposal insertion instead of relying on global random tie resolution.
6. Remove producer/demand/count mutation from `AIProposalAllocator` only after all characterization
   tests pass.
7. Re-score and select the exact proposal that will execute.
8. Delete `AIManufactureOption` and mutable selection methods.
9. Require exact seed-12345 state, report, and RNG-index equality.

The implementation choice in steps 4-5 is measurement-driven. Candidate expansion is accepted only
if late-game proposal counts and p99 remain within the performance gates. Traversal-order caps are
forbidden.

### PR 3: Clarify the production requirement model

1. Rename `AIDemand` and `AIDemandKind` to production-specific names.
2. Make every requirement field immutable, including replacement identity.
3. Keep the existing stable ID format during this structural migration.
4. Move reserve classification into named requirement-kind policy functions if it is shared;
   otherwise keep it beside the single consumer.
5. Do not introduce persistent goal state. Requirements remain turn-scoped deficits.
6. Require exact equivalence.

### PR 4: Split production requirement ownership

Extract in the current `Generate` call order:

1. Economy requirements.
2. Planetary and infrastructure requirements.
3. Fleet and colonization force requirements.
4. Special-forces requirements.

For each extraction:

- Move code without changing predicates, sorting, pressure calculations, IDs, or emitted order.
- Move corresponding tests from the monolithic fixture to `<Subject>Tests`.
- Pass a destination collection to avoid concatenation allocations and preserve order.
- Reuse `AIAssessment` indexes; do not add scene queries.
- Delete the old method immediately after its callers migrate.
- Run the exact equivalence gate before extracting the next component.

After all four extractions, delete `AIDemandSource`, its two subclasses, and the monolithic generator
shell if it no longer owns coordination.

### PR 5: Unify infrastructure creation and removal policy

1. Characterize current desired facility counts and removal choices.
2. Make `AIInfrastructureRequirements` the single owner of desired facility counts.
3. Production consumes deficits; facility removal consumes surpluses from the same calculation.
4. Preserve current build/removal thresholds and ordering exactly.
5. Delete `AIInfrastructureDemandPlanner` after both callers migrate.
6. Require exact equivalence.

### PR 6: Remove policy and scoring from `AIAssessment`

Perform separate commits, each with moved tests and exact equivalence:

1. Move diplomacy weighted utility to mission scoring.
2. Move attack requirements and readiness to `AIAttackRequirements`.
3. Move defense commitments to their existing strategic-plan owner or a fleet-specific requirements
   owner only if the existing owner cannot remain cohesive.
4. Leave raw counts, indexed collections, combat projections, intelligence age, production rates,
   and travel facts in `AIAssessment`.
5. Rename factual methods only after all policy extraction is complete.

No wrapper method remains in `AIAssessment` merely to preserve an obsolete API. Callers and tests
move in the same commit.

### PR 7: Remove redundant services and tidy names

1. Preserve reinforcement arrival as a required production-ranking calculation:
   `appended queue completion ticks + manufactured transit ticks`, saturating at `int.MaxValue`
   exactly as it does now.
2. Move that calculation beside production producer ranking and continue using
   `ManufacturingSystem.EstimateAppendedCompletionTicks` and
   `MovementSystem.TryEstimateManufacturedTransitTicks`.
3. Reuse the production planner's existing ordered-producer cache. Measure before removing the
   forecast's inner cache; retain a production-local cache if removing it causes repeated
   calculations or a performance regression.
4. Delete only the public `AIReinforcementArrivalForecast` wrapper and its lazy `AITurnContext`
   property after tests prove identical producer ordering, arrival values, and seed-12345 state.
5. Apply the final type renames listed above.
6. Rename methods and variables using these rules:
   - Methods use terse verb-object names: `BuildRequirements`, `ScoreAttack`, `CanDepart`.
   - Boolean names state the condition without narrating the implementation.
   - Collections use domain nouns, not `items`, `data`, or `things`.
   - Avoid `Manager`, `Helper`, `Utils`, `Processor`, `Source`, and `Context` unless the type truly
     owns that established role.
   - Avoid method names that encode an entire scenario sentence.
7. Remove dead overloads, forwarding wrappers, stale comments, and obsolete documentation in the
   same domain commit.
8. Require exact equivalence.

### PR 8: Align folders with the proven ownership boundaries

Only after behavior and responsibilities are stable, move files without changing namespaces or
logic in the same commit:

```text
AI/
  Core/          director, turn state, assessment, phases, allocation, utility
  Fleet/         fleet planning, requirements, scoring, and fleet proposals
  Missions/      mission planning, scoring, intent, and mission proposals
  Production/    requirements, planning, scoring, and manufacturing proposals
```

Do not create top-level `Colonization`, `Defense`, `Assessment`, `Candidates`, or `Commands`
directories unless later code proves an independent lifecycle and multiple owners.

Move tests to mirror production paths. Require exact equivalence after the move to detect accidental
assembly, serialization, or reflection effects.

## Exact-Equivalence Gate

A structural PR passes only when all of the following hold against the current PR-0 baseline:

- Medium, large-galaxy seed `12345` completes the same number of ticks.
- Victory state is identical.
- Canonical persisted `GameRoot` SHA-256 is identical.
- `GameRoot.RandomIndex` is identical.
- Complete normalized simulation JSON is identical, excluding only output-path fields.
- Fleet history, assaults, missions, manufacturing, personnel outcomes, ownership, and final faction
  totals therefore match exactly.
- The Alliance and Empire inspection saves load successfully.
- No new warning/error category appears in the simulation log.

An aggregate metric match is insufficient. A different instance ID, queue order, movement state,
mission state, fog snapshot, officer state, random index, or event-pool state fails the gate.

If a slice fails:

1. Stop the next slice.
2. Compare the first divergent tick or persisted node.
3. Identify the exact changed ordering, mutation, or RNG call.
4. Fix the structural implementation; do not tune configuration to conceal the difference.
5. Re-run from tick zero.

## Performance Gate

For every hot-path slice:

- Record execution frequency per tick and faction.
- Record candidate and exact-score counts.
- Record scene traversals, sorts, materializations, and allocations introduced or removed.
- Run the established late-game save in addition to the tick-zero seed run.
- Compare AI faction-turn median, p90, and p99.
- Reject regressions above 10%.
- Preserve the target of 150 ms per faction turn and the hard 300 ms p99 ceiling; do not claim this
  migration fixes existing over-budget baselines unless measurements prove it.
- Never add a scene query inside an entity or candidate loop.

## Clean-Code Gate

Each PR must satisfy all of the following:

- One authoritative owner for each policy calculation.
- No forwarding compatibility wrapper after all in-repository callers move.
- No new one-method abstraction unless it owns independently testable policy or bounds expensive
  candidate work.
- No generic base class used by only one concrete implementation.
- No new service exposed through `AITurnContext` for a single consumer.
- No class split solely to reduce line count; every extraction must own a coherent decision.
- No mixed structural and gameplay changes.
- Every C# method and constructor has required XML documentation.
- Tests mirror production directories and use the required naming convention.
- `dotnet csharpier check Assets/` passes.
- `./build.sh lint` passes.
- Relevant tests and the full build pass.
- Unity's loaded assemblies are newer than all modified C# files before simulations run.

## Completion Criteria

The migration is complete only when:

- Every proposal action is immutable after planning.
- Every score describes the exact action executed.
- Global allocation reserves or rejects without redesigning actions.
- `AIAssessment` exposes facts rather than weighted preferences or strategic decisions.
- Production requirements, product choice, producer allocation, scoring, and execution have distinct
  owners.
- Infrastructure construction and removal share one target calculation.
- Redundant demand-source and reinforcement-forecast abstractions are gone.
- Current AI behavior, RNG state, persisted game state, and complete seed-12345 report match the
  authoritative baseline exactly.
- The final type and method names describe actual responsibilities.
- Architecture documentation matches the code and contains no deleted types.
- Performance measurements satisfy the regression gate.
