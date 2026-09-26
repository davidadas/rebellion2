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
5. At that fixed global position, `AIProductionSelector.TryResolve` tries the primary producer,
   then its alternatives.
6. `AIProductionSelector.TryResolveQuantity` may replace the requested quantity with the largest
   currently affordable prefix.
7. When an alternative producer or reduced quantity is accepted, the selector applies the newly
   created exact proposal's score as metadata. Global ordering is not repeated.
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

## Controlled exact-candidate experiment

An uncommitted experiment replaced embedded producer and quantity fallback with independently
scored exact proposals. Equal-scored quantity variants were grouped so that one production
decision still consumed one random tie entry and tried the largest viable quantity first. The
complete EditMode suite passed (`5043/5043`).

The required Medium/Large seed `12345` tick-`1000` equivalence gate failed. The authoritative
baseline ended with Alliance/Empire planet counts of `46/111`; the exact-candidate experiment ended
at `35/106`. Alliance starfighters fell from `68` to `0`, and its regiments fell from `160` to `64`.
The first visible fleet-history divergence occurred at ticks `173-175`. This is controlled evidence
that the canonical run exercises production fallback ordering; the characterization difference is
not merely theoretical.

Performance did not reject the experiment: per-faction AI-turn median/p90/p99 changed from
`40.549/82.064/140.450 ms` to `39.301/65.975/95.765 ms`. Behavior equivalence rejected it.

Consequently, exact candidates cannot be globally expanded while also satisfying the required
canonical-state contract. The behavior-preserving seam is to keep one production decision at its
existing global position, move producer/count resolution into a production-owned selector, and
leave the generic allocation ledger responsible only for shared feasibility and reservations. The
selected executable action can still be immutable; its fallback score cannot retroactively change
the decision's global position without intentionally changing gameplay.

## Behavior-preserving extraction

`AIProductionSelector` now owns the characterized producer/count resolution. `AIProposalAllocator`
accepts or rejects only the exact action returned by that selector and retains the shared claim,
capacity, energy, maintenance, and production-stream reservation state. `AISelectionPhase` remains
the single orchestration boundary.

Validation on the intended compiled build produced:

- EditMode tests: `5043/5043` passed.
- Normalized seed-`12345` tick-`1000` report SHA-256:
  `24ac5c3dc666877b5f7aa880f31798ac1d953887e8b70aebbbcb7d422541d763` for baseline and
  candidate.
- Canonical saved-state SHA-256:
  `2571b48c226735f14ff4e97dcc5d462a01994f15ad6802e63b0cf0a9d9c82281` for baseline and
  candidate.
- Persisted `RandomIndex`: `729609` for baseline and candidate.
- Per-faction AI-turn median/p90/p99: baseline `40.549/82.064/140.450 ms`; candidate
  `39.497/71.767/107.693 ms`.
- Selection-phase median/p90/p99: baseline `0.523/3.187/7.938 ms`; candidate
  `0.513/3.053/6.313 ms`.

The extraction adds one selector object per faction selection phase. It adds no scene-graph query,
candidate enumeration, sorting pass, or proposal expansion. Producer-option and quantity loops are
the same loops moved from `AIProposalAllocator`.
