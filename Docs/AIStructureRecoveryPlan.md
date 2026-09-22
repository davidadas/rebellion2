# Strategic AI Structure

## Runtime flow

```text
AIDirector
  -> AIAssessment captures turn facts
  -> domain demand generators describe unmet needs
  -> domain planners enumerate feasible proposals from demands
  -> domain scorers assign utility
  -> AISelectionPhase resolves claims and production capacity
  -> proposals execute through game systems
```

Folders and namespaces follow pipeline responsibility, matching the repository convention that
module-wide coordinators and shared state live at the module root while specialized roles live
beside their contracts:

```text
AI/              -> AIDirector, AITurnContext, AIAssessment, and shared turn-level state
AI/Phases        -> turn phases and phase interfaces
AI/Demands       -> demand records, generators, and generator interface
AI/Planners      -> proposal planners and planner interface
AI/Proposals     -> proposal base and concrete proposals
AI/Scorers       -> proposal scorers, scorer interface, and scoring utilities
AI/Selectors     -> domain selection and typed reservation state
```

Do not create a folder for a single class, retain a vague `Core` bucket, or separate contracts from
the implementations they describe.

## Ownership rules

- `AIAssessment` owns turn-scoped factual indexes and projections. It does not choose actions.
- Demand generators describe unmet needs and measured deficits. They do not score solutions.
- `AITurnContext` carries turn state and shared policy objects. It does not enumerate candidates.
- A top-level planner consumes demands and owns feasible proposal enumeration for its domain.
- A domain scorer owns comparable utility for its proposals.
- Facts and genuine feasibility constraints may gate demand or proposal creation. Preferences and
  tradeoffs must be expressed as configured scorer considerations and weights, not conditional
  exclusions.
- `AISelectionPhase` owns global ordering and the shared boundary for proven cross-domain
  reservations. Domain selectors own domain contention.
- `AIProductionSelector` owns production-only fallback and resource reservations.
- Proposal types validate and execute one exact selected action.
- Planner implementations remain in one file unless a separate type owns an independently reusable
  responsibility.
- A separate class is admitted only when it owns independently reused policy, state, or a pipeline
  stage. A single-caller helper is normally a private part of its caller.

## Current domain owners

### Turn pipeline

- `AIDirector`: composition root and faction-turn runner.
- `AIPlanningPhase`: runs registered domain planners.
- `AIScoringPhase`: dispatches registered domain scorers.
- `AISelectionPhase`: globally orders and selects proposals.
- `AISelectionState`: typed cross-domain reservation state only.
- `AIExecutionPhase`: executes selected proposals.
- `AIAssessment`: factual turn index.
- `AITurnContext`: shared turn state.

### Fleets

- `AIFleetPlanner`: defense, attack, colonization, transfer, evacuation, and role proposal
  enumeration in one implementation file.
- `AIOrbitalEngagementPlanner`: immediate encounters for fleets already occupying one planet.
- `AIAttackDemandGenerator`: factual attack-package demand generation.
- `AIStrategicPlan`: cached turn-level defense commitments.
- `AIFleetProposalScorer`: fleet-proposal utility.
- `AIFleetProductionAllocationScorer`: shared production priority and reinforcement need for fleets.
- Fleet proposal types: exact validation and execution.

### Missions

- `AIAbortMissionPlanner`: invalid active-mission cleanup at its own incremental work boundary.
- `AIMissionPlanner`: start-mission proposal enumeration.
- `AIMissionCandidatePool`: bounded candidate retention and safe pruning.
- `AIMissionProposalScorer`: mission utility.
- Mission proposal types: exact validation and execution.
- `AIMissionSelector`: mission contention and selected-team decoy finalization.
- `AIMissionPlanner`: mission enumeration and turn-scoped special-forces intent assignment.

### Production

- `AIProductionPlanner`: technology and producer resolution and manufacture-proposal enumeration
  from production demands.
- `AIProductionDemandGenerator`: score-free production-deficit generation.
- `AIProductionCapacityTargets`: neutral facility targets shared by expansion and retirement.
- `AIInfrastructurePlacementScorer`: reusable facility-destination ranking.
- `AIProductionProposalScorer`: production utility.
- `AIProductionSelector`: exact producer fallback and production-only reservations.
- `AIFacilityRemovalPlanner`: surplus-facility removal enumeration.
- Production proposal types: exact validation and execution.

Completed production separation:

1. The director runs demand generation after assessment.
2. Production needs are score-free demand records containing identity, scope, deficit, and
   capability constraints.
3. Production demands carry no proposal utility.
4. `AIProductionPlanner` resolves technologies, destinations, and producers and enumerates
   feasible manufacturing proposals from those demands.
5. Deficit severity, strategic importance, readiness benefit, threat, facility balance,
   travel, maintenance risk, and resource urgency are configured considerations owned by
   `AIProductionProposalScorer`.
6. Hard gates remain only for factual need, genuine feasibility, allocation, and final execution
   validity.

Completed selection separation:

1. `AISelectionPhase` remains responsible for global priority and score ordering.
2. Fleet, mission, and production contention rules live in their respective domain selectors.
3. Arbitrary string claims, `AIClaimKeys`, and `AIProposalAllocator` are removed.
4. Only proven cross-domain reservations remain shared, represented by
   typed state rather than string keys.
5. The characterized late scoring of a resolved production fallback remains unchanged; correcting
   it is a separate behavior change.

Completed turn-state separation:

1. `AIDirector` explicitly constructs the assessment and derived strategic policy used by a
   faction turn.
2. `AITurnContext` is a carrier of supplied turn state rather than a constructor that performs
   assessment and strategic derivation.
3. Special-forces intent belongs to mission planning; decoy finalization belongs to mission selection.

Completed attack-demand separation:

1. `AIAttackRequirements` is removed.
2. Required orbital strength, ground strength and count, bombardment, and occupation capability
   are attack-demand facts.
3. Comparative readiness and target-value decisions live in fleet and production scorers.
4. Only live execution-validity checks remain on proposals or underlying game combat rules.
5. No renamed catch-all policy class replaces it.

## Dependency rules

Allowed:

```text
Phases -> registered domain demand generators, planners, scorers, and selectors
Domain planners -> assessment facts, domain policy, proposal types
Domain scorers -> assessment facts, domain policy, proposal types
Selection -> typed shared state and domain selectors
Proposals -> game systems
```

Forbidden:

- Assessment depending on a planner, scorer, selector, or proposal.
- Core selection depending on domain-specific contention policy.
- A scorer allocating resources or executing actions.
- A proposal enumerating candidates.
- A requirement calculation selecting or executing proposals.
- A demand carrying precomputed proposal utility.
- Domain contention encoded as arbitrary global string claims.
- A selector scoring or rescoring a proposal after global ordering.
- A class existing only to forward a call to its only consumer.
- Scene-graph traversal inside entity or candidate loops.

## Verification gates

This recovery is a structural refactor only. Its completed result must leave the AI operating
precisely as it does before the migration. Architectural cleanup does not authorize tuning or
behavior correction.

Migrate one boundary at a time in this order:

1. Make assessment and derived turn state explicit inputs to `AITurnContext`.
2. Introduce score-free demands while preserving the existing generated production needs.
3. Make planners consume those demands while preserving proposal identities and enumeration order.
4. Move preference calculations into scorers while preserving score values and ordering exactly.
5. Replace string claims with typed domain contention that reproduces every existing conflict.
6. Move mission intent and decoy ownership without changing assignments or RNG consumption.

For each boundary, compare the old and migrated paths from identical state. The equivalence record
must include generated needs, proposal stable identities and order, scores, selected proposals,
executed actions, RNG consumption, and resulting game state. A difference stops the migration until
it is explained and eliminated.

Known questionable behavior—including late production-fallback scoring—must remain behaviorally
equivalent during this work. Correcting it is a separate, explicitly measured behavior change. If a
proposed architectural boundary cannot reproduce current behavior, stop and establish that conflict
before proceeding rather than silently changing the AI.

Structural changes must preserve proposal order, score order, RNG consumption, and gameplay. Before
acceptance:

1. Run the complete Unity test suite.
2. Run `dotnet csharpier check Assets/` and `./build.sh lint`.
3. Compare identical-seed reports and normalized saves against the established baseline.
4. Measure median, p90, and p99 faction-turn timing in late-game state.
5. Reject a consolidation that changes gameplay or regresses runtime by more than ten percent.
