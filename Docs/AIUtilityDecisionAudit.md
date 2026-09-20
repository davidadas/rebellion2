# Strategic AI Decision Audit

This audit defines the end of the structural utility migration. A LINQ ordering is not by itself a
utility decision: the question is whether it suppresses otherwise feasible strategic proposals.

| Decision domain | Architectural owner | Status |
| --- | --- | --- |
| Fleet attacks, defense, bombardment, return, colonization, and unit transfers | `AIFleetProposalScorer` plus global proposal selection | Configured utility |
| Attack candidate pruning | `AIFleetAttackCandidateSelector` | Proven score upper bound; does not prune a candidate that can beat the retained set |
| Mission objectives and aborts | `AIMissionProposalScorer` plus global proposal selection | Configured utility |
| Mission candidate retention | Mission score upper bounds and per-actor claim keys | Bounded alternatives preserve the strongest candidates |
| Production demands | `AIProductionRequirements` pressure calculations | Configured utility |
| Production proposal choice | `AIProductionProposalScorer` plus shared resource claims | Configured utility |
| Fleet production routing | `AIFleetProductionAllocationScorer` | Configured sequential allocation |
| Production-facility placement | `AIInfrastructurePlacementScorer` | Configured utility |
| Sector production-hub assignment | `AIInfrastructureAllocationScorer` | Configured turn-scoped allocation |
| Capital-ship reinforcement need | `AIFleetReinforcementUtility` | Shared configured utility |
| Colonization target choice | `AIColonizationTargetScorer` | Configured utility |
| Unit technology choice other than capital-ship hulls | Production technology scorers | Configured utility |
| Capital-ship hull choice | Seeded eligible-template selection | Deliberately excluded from this migration |

The remaining orderings do not suppress competing strategic proposals:

- Stable identifier orderings make assessment indexes and proposal sort keys deterministic.
- Production-lane orderings assign already-selected work to compatible producers by backlog,
  throughput, and travel cost. They are execution allocation, not demand selection.
- Payload orderings choose the least valuable colonization carrier or regiment, the strongest
  transferable regiment, and the fastest surviving facility after an action has already won.
- Colonization campaign distance ordering constructs a route inside an already-selected system.
- Mission participant ordering fills roles after the mission proposal has been selected.
- Defense allocation remains sequential because fleets are scarce, mutually exclusive actors; its
  target, travel, force-efficiency, and reinforcement preferences are configured.

Hard constraints remain typed configuration rather than weights. Counts, safety floors, capacity,
ownership, legal mission types, manufacturing compatibility, and combat-readiness requirements
decide feasibility or define a target. Changing one does not require changing planner control flow.

No normal strategic-AI decision path uses an unseeded random source. Seeded shuffling is retained
only to vary equal mission alternatives, and deterministic identifiers resolve equal final utility.
