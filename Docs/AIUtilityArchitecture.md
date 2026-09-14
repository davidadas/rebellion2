# Strategic AI Utility Architecture

Strategic decisions follow one data flow:

1. `AITurnContext` and `AIAssessment` capture reusable facts once per faction turn.
2. Planners enumerate feasible proposals without discarding a stronger candidate by traversal order.
3. Scorers normalize proposal considerations and evaluate configured response curves.
4. Selection orders proposals by priority and utility, then enforces shared claims and resource
   reservations.
5. Proposals revalidate game rules before execution.

## Decision Boundaries

Use a response curve when a measured fact changes how desirable an otherwise valid option is.
Examples include strategic value, readiness, travel efficiency, risk, production deficit, and
technology capability. Each consideration owns its input range, curve, and contribution weight in
game configuration.

Keep hard constraints outside utility scoring. Ownership, manufacturing compatibility, available
energy, fleet capacity, mission legality, and minimum force requirements determine whether an
option is feasible. A low score must not make an illegal action legal, and an arbitrary score gate
must not replace a game rule.

Keep domain calculations outside utility scoring. Percent bounds, combat totals, resource flow,
travel distance, build duration, and fulfillment ratios describe game state. Feed their normalized
results into considerations when they influence preference.

Use deterministic identifiers only to resolve equal utility. A tie-break must not silently act as
a second preference model. If a gameplay attribute consistently decides between otherwise valid
options, represent it as a configured consideration.

## Candidate Generation

A planner may prune candidates only with a feasibility rule or a proven upper bound that cannot
discard the best-scoring result. Greedy preselection is allocation policy, not candidate generation;
it belongs behind a documented shared boundary or must be represented by proposals with mutually
exclusive claim keys.

Candidate generation and scoring reuse the turn assessment. They must not introduce per-candidate
scene-graph scans, nested materialization, or repeated sorting of the same source collection.

## Configuration

Policy targets and limits remain explicit typed values. Preferences use `AIConsiderationConfig`:

- `InputMaximum` converts a raw domain value to the normalized interval.
- `Curve` controls the response shape.
- `Weight` controls the consideration's contribution relative to other considerations.

Default linear curves preserve established behavior during migration. Behavioral tuning begins only
after the structural migration passes the deterministic baseline and held-seed validation.

## Verification

During migration, run Medium seed `12345` for 1000 ticks and compare the complete normalized JSON
with `SimulationResults/current-medium-seed-12345-tick-1000.json`. Structural slices require an
exact match unless a deliberate behavior change is separately justified. Run held seeds only after
the complete migration, as specified in `Docs/AIUtilityBaseline.md`.
