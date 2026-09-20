# Strategic AI Structure Recovery Plan

## Purpose

This document replaces the extraction-first migration plan. Its purpose is to define the target
structure completely enough that implementation does not invent new architecture while moving
code.

The target preserves the established runtime pipeline:

```text
AIDirector
  -> build AITurnContext and AIAssessment
  -> run intent phases
  -> planners enumerate proposals
  -> scorers assign comparable utility
  -> selection resolves shared claims and capacity
  -> proposals execute through game systems
```

Requirements are production-planning inputs. They are not a second planning pipeline.

Folder and namespace boundaries are domain-first and identical:

```text
AI/Core        -> Rebellion.AI.Core
AI/Fleet       -> Rebellion.AI.Fleet
AI/Missions    -> Rebellion.AI.Missions
AI/Production -> Rebellion.AI.Production
```

The current domain-first folders with legacy layer-first namespaces (`Director`, `Phases`,
`Planners`, `Proposals`, and `Scoring`) are an incomplete migration and are not the target.

## Evidence

The pre-migration revision `7c15cfeb` and current revision `5058f9c2` both contain 64 AI types.
The migration did not produce a class-count explosion; it replaced types. It did, however, replace
one 2,712-line production generator with broad bucket classes:

| Current type | Lines | Problem |
| --- | ---: | --- |
| `AIProductionRequirements` | 61 | Sequences collaborators but owns no decision |
| `AIInfrastructureRequirements` | 1,406 | Combines planetary defense, garrisons, fighters, facility portfolio, expansion, and upgrades |
| `AIForceRequirements` | 1,011 | Combines fleet creation, colonization creation, reinforcement allocation, composition, and pressure |
| `AIEconomyRequirements` | 480 | Combines colony foundations and resource-balance requirements |

`AIProductionRequirements` has two non-test callers: `AIProductionPlanner` and the simulation
summary builder. The report reads the same ordered requirements for diagnostics; it does not create
a second runtime owner. Both callers can use `AIProductionPlanner.BuildRequirements`, while tests
move to the calculator that owns each policy. The coordinator therefore remains a forwarding shell
rather than an independent domain owner.

The migration also produced valid ownership changes:

- `AIAttackRequirements` owns attack and assault requirements consumed across fleet planning,
  fleet scoring, execution validation, and force production.
- `AIStrategicPlan` owns one turn's defense commitments shared by fleet planning and production.
- `AIProductionSelector` owns the production-specific fallback that must occur at the production
  decision's existing global selection position.
- Reinforcement-arrival ranking now lives in production rather than behind a turn-context service.

These types perform domain calculations; they do not merely forward calls.

## Class Admission Rules

A new or surviving class must satisfy all applicable rules:

1. It owns a named domain decision, immutable domain value, pipeline stage, or shared factual index.
2. Its public API exposes that responsibility directly rather than forwarding another object's API.
3. Moving its implementation into its only caller would make that caller responsible for a second
   independently describable decision.
4. It does not exist only to preserve an obsolete call site, test seam, folder, or naming pattern.
5. A coordinator is allowed only at an established runtime boundary (`AIDirector`, a turn phase, or
   a top-level proposal planner). Internal coordinator shells are forbidden.
6. A single-method class is acceptable only when that method is the pipeline contract or implements
   a substantial independent domain decision. Method count alone neither admits nor rejects a type.
7. Shared policy has one owner. Consumers may not retain forwarding wrappers around that owner.
8. Facts and policy stay separate: `AIAssessment` indexes facts; domain decision sources interpret
   those facts.

## Ownership Contract

### Core

- `AIDirector`: constructs a turn and runs phases.
- `AITurnContext`: holds turn-scoped services, indexes, shared strategic decisions, proposals, and
  results. It does not calculate domain policy itself.
- `AIAssessment`: constructs and exposes factual indexes and projections. It does not score,
  prioritize, choose, or declare strategic readiness.
- `AIPlanningPhase`: registers and runs top-level proposal planners. Registration is orchestration,
  not domain policy; injected planners remain available for focused tests.
- `AIScoringPhase`: registers and dispatches proposal scorers. It does not enumerate or select
  proposals; injected scorers remain available for focused tests and tooling.
- `AISelectionPhase`: orders scored decisions and coordinates selection. Production-specific
  alternative resolution is delegated to `AIProductionSelector`.
- `AIProposalAllocator`: owns only cross-proposal claim keys. It has no production-type dependency.
- `AIExecutionPhase`: executes selected proposals in order.
- `AIProposal`: represents a proposed action and its execution contract.
- Utility types normalize and combine configured utility; they own no gameplay policy.

### Fleet

- `AIFleetPlanner`: top-level planner for strategic fleet orders, transfers, evacuations, and role
  assignment.
- `AIOrbitalEngagementPlanner`: top-level planner for immediate encounters between fleets already
  occupying the same planet. It remains separate because encounter resolution and strategic fleet
  movement are different decisions and both are registered with `AIPlanningPhase`.
- Attack, defense, and orbital-engagement planners enumerate their named proposal family. Helper
  planners remain internal to the fleet domain and do not appear as additional turn phases.
- `AIAttackRequirements`: owns target force, assault, bombardment, departure-readiness, and combat
  survival requirements. These are one cohesive decision because every consumer must agree on what
  makes an attack viable.
- `AIStrategicPlan`: caches shared turn-level fleet commitments such as planetary defense. It does
  not become a general policy bucket.
- Fleet scorers compare fleet proposals. They do not mutate proposals or allocate units.
- Fleet proposal types validate and execute their exact selected actions through game systems.

### Missions

- Mission planners enumerate mission proposals.
- `AIMissionCandidatePool` owns bounded retention and safe scoring/pruning of mission candidates;
  it is not a second general selector.
- Mission scorers calculate mission utility.
- The mission scorer assigns the neutral score to mandatory abort proposals; Core does not inspect
  mission proposal types.
- Mission proposals validate and execute exact mission actions.
- Decoy assignment remains a post-selection mission phase because it allocates participants among
  already selected missions.

### Production

- `AIProductionPlanner` is the sole top-level production planner. It owns the ordered production
  planning workflow: collect requirements, resolve eligible technologies and producers, and emit
  manufacture proposals.
- `AIProductionPlanner.BuildRequirements` is a real query used by both proposal generation and
  simulation reporting. Reporting must include unsatisfied requirements that emit no proposal, so
  it cannot reconstruct this data from `Plan` output.
- `AIProductionRequirement` is an immutable description of a production deficit and its pressure.
- Requirement calculators own independent production questions and append results to the planner's
  ordered collection. They do not coordinate one another or emit proposals.
- `AIProductionProposalScorer` compares production proposals.
- `AIProductionSelector` resolves the exact producer and quantity for the currently ranked
  production decision. It owns producer-capacity, production-stream, destination-energy, and
  maintenance reservations because those constraints apply only to production.
- `AIProposalAllocator` accepts or rejects the exact resolved action's generic claim keys; it does
  not inspect manufacturing proposals.
- The production scorer assigns the neutral score to mandatory facility-removal proposals; Core
  does not inspect production proposal types.
- `AIManufactureProposal` carries production alternatives before selection and the exact action
  afterward, then validates and executes that action. It does not generate requirements or rank
  producers.

## Exact Type Disposition

### Delete

- `AIProductionRequirements`: forwarding coordinator with no policy. Move its ordered calls into
  `AIProductionPlanner`, the established production-planning boundary.
- `AICleanupProposalScorer`: cross-domain type switch with no cleanup algorithm. Mission aborts are
  scored by the mission scorer and facility removals by the production scorer.

### Retain

- `AIAttackRequirements`: cohesive shared attack policy with multiple production and fleet
  consumers.
- `AIStrategicPlan`: cohesive shared turn-level commitments with multiple consumers.
- `AIProductionSelector`: cohesive production selection algorithm required at a specific pipeline
  boundary.
- `AIProductionRequirement`: immutable domain value.
- Existing pipeline phases, top-level planners, scorers, proposals, and factual assessment indexes.

All current Core, Fleet, and Missions types are retained unless explicitly listed below. Their
large implementations still require method-level cohesion review, but none is replaced merely to
reduce line count. This recovery does not create one class per proposal subtype or mission type.

### Replace the broad requirement buckets

The current buckets are divided by independent production question, not by vague domain noun:

| Replacement | Exact responsibility |
| --- | --- |
| `AIColonyRequirements` | Founding infrastructure for newly claimed planets |
| `AIResourceRequirements` | Refined-material and maintenance-economy balancing requirements |
| `AIPlanetDefenseRequirements` | Planetary fighters, garrisons, shields, planetary weapons, and idle-shipyard fighter reserves |
| `AIProductionCapacityRequirements` | Facility targets, facility expansion, and upgrades |
| `AIFleetFormationRequirements` | Creation and initial composition of battle and colonization fleets |
| `AIFleetReinforcementRequirements` | Reinforcement deficits, readiness pressure, and composition of existing fleets |
| `AISpecialForcesRequirements` | Special-forces supply for mission demand |

These are not registered planners and do not form a second framework. `AIProductionPlanner` invokes
them directly in the existing emission order. Each must contain meaningful policy and helpers, not
just delegate to another requirement calculator.

`FacilityPortfolio` becomes an immutable production value, not a class with behavior. The
production planner constructs it once per faction turn from `AIAssessment`'s indexed planet-building
collections and passes it to the requirement calculations that need it. The current assessment does
not cache every faction-wide facility-category total, so deleting the snapshot would either repeat
enumeration or require expanding the assessment without evidence. Neither is allowed during the
structural cleanup.

This replacement changes the AI type-declaration count from 64 to 65: three broad requirement types
become six cohesive calculators, the coordinator is deleted, and the existing nested portfolio
value remains one nested value type. The cross-domain cleanup scorer is also deleted. No other new
production framework types are admitted.

### Assessment policy disposition

The assessment audit uses callers, not method names, to place remaining methods:

| Current assessment API | Disposition |
| --- | --- |
| Planet/fleet/unit collections and indexed counts | Retain as facts |
| Combat, bombardment, capacity, production-rate, backlog, distance, and intelligence projections | Retain as factual projections |
| Planet and system value/support estimates shared by several domains | Retain as shared strategic estimates; they do not select an action |
| `IsPriorityDefensePlanet` | Retain; it combines indexed headquarters-ownership facts and is consumed by fleet planning, fleet validation, strategic allocation, and production |
| `IsPlanetThreatened` | Retain; it caches the shared known-contact/hostile-sector classification used by production and defense policy |
| `IsAttackPreparationTarget` | Retain; it exposes the indexed set of active attack-order targets to fleet, mission, and production consumers |
| `IsGarrisonSabotageCritical` | Move into mission scoring; it has one mission-policy consumer |
| Unused public policy queries such as `IsIdleBattleFleet` | Delete after reference verification |
| Private helpers used only to construct retained facts | Keep private in assessment |

No new assessment wrapper is created for these moves.

## Operation Coverage

Every current strategic operation has one path through the target:

| Operation | Enumeration | Utility | Shared allocation | Execution |
| --- | --- | --- | --- | --- |
| Abort mission | `AIAbortMissionPlanner` | Mission scorer | Mission/personnel claims | `AIAbortMissionProposal` |
| Start mission | `AIMissionPlanner` | Mission scorer/candidate pool | Mission/personnel claims, decoy phase | `AIMissionProposal` |
| Remove facility | `AIFacilityRemovalPlanner` | Production scorer | Planet/facility claims | `AIFacilityRemovalProposal` |
| Engage orbitally | `AIOrbitalEngagementPlanner` | Fleet scorer | Fleet/target claims | `AIOrbitalEngagementProposal` |
| Attack planet | Fleet attack planner | Fleet scorer | Fleet/target claims | `AIFleetAttackProposal` |
| Defend planet | Fleet defense planner | Fleet scorer | Fleet/target claims | `AIFleetDefenseProposal` |
| Colonize | `AIFleetPlanner` | Fleet scorer | Fleet/target claims | Colonization proposals |
| Transfer/evacuate/clear/assign role | `AIFleetPlanner` | Fleet scorer | Unit/fleet claims | Corresponding proposal |
| Manufacture | `AIProductionPlanner` | Production scorer | Production selector plus shared allocator | `AIManufactureProposal` |

No current operation requires another framework or coordinator.

## Dependency Rules

Allowed direction:

```text
Core planning/scoring phases -> registered domain planners/scorers
Domain planners -> assessment facts, domain policy, proposal types
Domain scorers -> assessment facts, domain policy, proposal types
Selection -> allocator and domain selector
Proposals -> game systems
```

Forbidden direction:

- Assessment to planner, scorer, selector, or proposal.
- Requirement calculator to proposal selection or execution.
- Scorer to allocator or execution.
- Proposal to planner.
- One domain folder to another domain's concrete planner.
- Tests as the only reason for a production type or public method to exist.

`AISelectionPhase` has one explicit domain dependency: it delegates unresolved manufacturing
decisions to `AIProductionSelector` at their existing global rank. This exception is required by
the measured ordering contract and is not generalized into a one-implementation resolver framework.

## Reconstruction Sequence

1. Preserve the current revision as a recovery reference.
2. Delete `AIProductionRequirements`; move its exact call order into `AIProductionPlanner`.
3. Split each broad requirement bucket by the disposition table using move-only commits. Delete the
   bucket immediately when its last method moves.
4. Keep requirement behavior tests on `AIProductionPlanner.BuildRequirements`, the public query
   consumed by production planning and simulation reporting. Group them separately from proposal
   generation tests without inventing a test-only coordinator or widening calculator visibility.
5. Audit `AIAssessment` methods: factual calculations remain; policy methods move only to an owner
   named above. Do not create additional categories during implementation.
6. Move production-only reservation state out of `AIProposalAllocator` and into
   `AIProductionSelector`; keep only generic claims in Core.
7. Delete `AICleanupProposalScorer` and route its two proposal types to their domain scorers.
8. Audit every remaining AI type against the class admission rules. Delete forwarding APIs and dead
   types; do not invent replacements.
9. Update the old migration document to point to this plan and remove steps that contradict it.
10. Align namespaces with the four established domain folders in one mechanical commit after type
   ownership is stable. Do not mix namespace edits with behavior or responsibility changes.

Every step preserves proposal emission order, equal-score order, RNG consumption, and runtime
behavior.

## Verification Gates

Each implementation commit must satisfy:

1. The complete test suite passes.
2. CSharpier and lint pass.
3. No new AI type exists unless it is already named in this plan and satisfies the admission rules.
4. No surviving class is an internal forwarding coordinator.
5. The normalized seed-12345 tick-1000 report, canonical save hash, and `RandomIndex` match the
   established baseline exactly.
6. Late-game median, p90, and p99 AI faction-turn timings stay within the repository limits and do
   not regress by more than 10 percent.
7. A class-disposition diff records every added, deleted, retained, and renamed AI type.

If an implementation step cannot meet all seven gates, it is rejected rather than patched with a
new wrapper or exception.

## Complete Type Audit

This is the authoritative disposition of the current type inventory. Nested enums and immutable
values remain with their owning subject unless separately named.

### Core inventory

| Type | Disposition | Responsibility |
| --- | --- | --- |
| `AIDirector` | Retain | Composition root and faction-turn runner |
| `AITurnContext` | Retain | Turn-scoped shared state and services |
| `AIAssessment` | Retain and narrow | Factual indexes and projections |
| `AIPlanningPhase` | Retain | Registers and runs proposal planners |
| `AIScoringPhase` | Retain | Registers and runs proposal scorers |
| `AISelectionPhase` | Retain | Global ordering and selection orchestration |
| `AIProposalAllocator` | Retain and narrow | Generic claim ledger only |
| `AIExecutionPhase` | Retain | Ordered proposal execution |
| `AISpecialForcesIntentPhase` | Move to Missions | Pre-planning mission-resource intent |
| `AIMissionDecoyAssignmentPhase` | Move to Missions | Post-selection mission participant allocation |
| `AIProposal` / `AIProposalPriority` | Retain | Proposal contract and mandatory priority |
| `AIClaimKeys` | Retain | Stable cross-proposal claim identities |
| Turn/planner/scorer interfaces | Retain | Pipeline contracts |
| `AIUtility`, `AIUtilityScore`, `AIUtilityDomain` | Retain | Configured utility normalization and composition |
| `AICleanupProposalScorer` | Delete | Cross-domain type switch with no independent policy |

### Fleet inventory

| Type | Disposition | Responsibility |
| --- | --- | --- |
| `AIFleetPlanner` | Retain | Strategic fleet orders, transfers, evacuation, and roles |
| `AIFleetAttackPlanner` | Retain | Bounded attack-candidate enumeration |
| `AIFleetDefensePlanner` | Retain | Defense-candidate enumeration |
| `AIOrbitalEngagementPlanner` | Retain | Immediate local fleet encounters |
| `AIAttackRequirements` | Retain | Shared attack viability requirements |
| `AIStrategicPlan` | Retain | Turn-level defense commitments |
| `AIFleetProposalScorer` | Retain | Comparable utility for fleet proposals |
| `AIFleetProductionAllocationScorer` | Retain | Fleet competition for production priority |
| `AIFleetReinforcementUtility` | Retain | Shared reinforcement value calculation |
| `AIColonizationTargetScorer` | Retain | Colonization-target utility |
| Fleet proposal types | Retain | Exact selected fleet actions and execution |

`AIFleetProposalScorer` is large, but its methods share one configured utility model and produce one
comparable score domain. It is not split during recovery. A later change may split its private
implementation only if measurement proves a maintenance or performance benefit without creating
multiple competing score scales.

### Missions inventory

| Type | Disposition | Responsibility |
| --- | --- | --- |
| `AIMissionPlanner` | Retain | Mission candidate enumeration |
| `AIAbortMissionPlanner` | Retain | Invalid active-mission cleanup enumeration |
| `AIMissionCandidatePool` | Retain | Bounded mission candidate retention and safe pruning |
| `AIMissionProposalScorer` | Retain and extend | Mission utility, including mandatory abort score |
| `AIMissionProposal` | Retain | Exact mission start action and execution |
| `AIAbortMissionProposal` | Retain | Exact mission abort action and execution |

`AIMissionPlanner` remains one planner because mission candidates compete through one participant
pool and one pruning process. Splitting by mission type would duplicate that shared enumeration
state or introduce another coordinator.

### Production inventory

| Type | Disposition | Responsibility |
| --- | --- | --- |
| `AIProductionPlanner` | Retain | Production workflow and proposal enumeration |
| `AIProductionRequirement` and enums | Retain | Immutable production deficit |
| `AIProductionProposalScorer` | Retain and extend | Production utility, including mandatory removal score |
| `AIProductionSelector` | Retain and expand | Exact production fallback and reservations |
| `AIManufactureProposal` | Retain | Production alternatives/exact action and execution |
| `AIInfrastructurePlacementScorer` | Retain | Facility destination ranking |
| `AIFacilityRemovalPlanner` | Retain | Surplus-facility removal enumeration |
| `AIFacilityRemovalProposal` | Retain | Exact facility removal and execution |
| `AISpecialForcesRequirements` | Retain | Mission-driven special-forces supply |
| `AIProductionRequirements` | Delete | Forwarding coordinator |
| `AIEconomyRequirements` | Replace | Mixed colony and resource policy |
| `AIInfrastructureRequirements` | Replace | Mixed defense and production-capacity policy |
| `AIForceRequirements` | Replace | Mixed formation and reinforcement policy |

The only admitted replacement types are the six requirement calculators named earlier. Any need
for another type stops implementation and reopens this audit before code is added.

### Requirement method move map

The split boundary is fixed before implementation:

- `AIColonyRequirements`: `AddColonyRequirements`, `SelectInitialColonyBuildingType`, and
  `RequiresFoundingFacility`.
- `AIResourceRequirements`: `AddResourceRequirements` and all mine/refinery deficit, destination,
  batch, maintenance-pressure, and refined-material-pressure helpers.
- `AIPlanetDefenseRequirements`: planetary starfighter, shield, weapon, garrison, static-defense,
  defense-pressure, local production-infrastructure predicates, and idle-shipyard fighter work.
- `AIProductionCapacityRequirements`: facility portfolio construction, desired facility counts,
  facility expansion, facility upgrades, and facility placement pressure.
- `AIFleetFormationRequirements`: battle- and colonization-fleet seed requirements,
  `HasColonizationOpportunity`, and shared fleet-assembly helpers.
- `AIFleetReinforcementRequirements`: existing-fleet priority, deficits, composition, readiness
  pressure, and reinforcement eligibility.
- `AISpecialForcesRequirements`: unchanged.

No helper is duplicated across these calculators. A helper needed by two calculators either uses an
existing factual assessment API or remains a private calculation on the single policy owner that
both consumers call.
