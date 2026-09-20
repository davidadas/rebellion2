# AI Production Selection Trace

## Purpose

This document records the current manufacturing-selection behavior that must be understood before
changing proposal ownership. It describes observed code paths and characterization tests; it does
not prescribe a replacement architecture.

## Current flow

1. `AIProductionPlanner.AddManufactureProposal` chooses one product and ranks producer planets.
2. Non-distributed work becomes one `AIManufactureProposal` containing a primary producer and
   ordered `ProducerAlternatives`.
3. `AIProductionProposalScorer` scores only the primary producer and requested quantity.
4. `AISelectionPhase` globally orders and tie-shuffles that composite proposal using the primary
   score. Embedded alternatives consume no additional random values.
5. At that fixed global position, `AIProposalAllocator.TryResolveManufactureProposal` tries the
   primary producer, then its alternatives.
6. `TrySelectManufacturePrefix` may replace the requested quantity with the largest currently
   affordable prefix.
7. When an alternative producer or reduced quantity is accepted, the allocator scores the newly
   created exact proposal. Global ordering is not repeated.
8. `AIExecutionPhase` executes the selected exact proposal in the position inherited from the
   original composite proposal.

## Proven fallback behavior

`AISelectionPhaseTests.Select_WithUnavailablePreferredManufacturingProducer_PreservesPrimaryOrdering`
proves that:

- An earlier proposal can claim the preferred producer.
- The composite manufacturing proposal then selects its fallback producer.
- The fallback receives its own exact score.
- The fallback still executes before an independent proposal whose score is higher than that exact
  fallback score.

`AISelectionPhaseTests.Select_WithPartiallyAffordableFacilityBatch_PreservesFullBatchOrdering`
proves the equivalent quantity behavior:

- A full batch receives the global selection position.
- Maintenance availability reduces it to an affordable prefix.
- The prefix receives its own exact score.
- The prefix still executes before an independent proposal whose score is higher than the prefix
  score.

The focused `AISelectionPhaseTests` fixture passes with both characterizations.

## Constraint established by the trace

The following requirements cannot all describe the current behavior:

1. Preserve current proposal ordering and fallback behavior exactly.
2. Globally order every executed action by that exact action's score.
3. Prevent global allocation from replacing producer or quantity.

The current implementation satisfies the first requirement by assigning a composite proposal's
global position before the exact action is known. Enumerating exact producer and quantity actions
and globally sorting them by their own scores would satisfy the second and third requirements, but
the characterized outcomes above would change. Resolving producers before global selection would
also change which already-selected claims, capacity, energy, and maintenance are visible during
resolution.

This is a specification conflict, not an unlocated class seam. An implementation choice requires
an explicit decision about which behavior contract changes.
