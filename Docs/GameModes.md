# Game mode architecture

## Scope and validation status

Strategic campaign play and tactical battles have separate state, clocks, AI, presentation, and
synchronization policy. They exchange captured encounter inputs and completed outcomes. Similar
feature names do not make their execution rules interchangeable.

The ownership migration is implemented locally. Complete repository, graphics, save/content, and
live two-account network validation remains required before publication. Focused deterministic
comparisons do not establish overall completion or Internet performance.

## Mode ownership

| Area | Strategic campaign | Tactical battle | Shared mechanism |
| --- | --- | --- | --- |
| State | Persistent galaxy, ownership, units, research, and orders | Captured participants, groups, motion, weapons, damage, and encounter lifecycle | Stable identities and explicit setup/outcome contracts |
| Execution | Ordered campaign ticks and command/result boundaries | Frame advancement and ordered battle input | Application cancellation and admission infrastructure |
| AI | Faction planning against a campaign assessment | Deployment, automatic targeting, and retreat policy | Random interfaces and arithmetic with matching semantics |
| Presentation | Galaxy map and campaign windows | Battle scene, inspection, camera, effects, and notifications | Asset caches, window controls, input routing, and audio devices |
| Networking | Host-ordered campaign actions and settled-state agreement | Authoritative host simulation and passive replica playback | Authentication, lobby membership, transport, and bounded delivery |
| Integration | Suspending campaign time and committing battle results | Producing detached outcomes | One validated handoff in each direction |

The campaign retains its established `Game`, `Gameplay`, `Sessions`, and strategic UI areas. Its
turn planner is explicitly owned by `Campaign/AI`; tactical battle automation is a separate policy.
Its existing model names and serialization contracts remain stable. The tactical mode has its own
cohesive area rather than extending those campaign feature folders. Shared application composition
still connects the two modes; no common simulation clock, AI superclass, or synchronization protocol
is imposed on them.

## Tactical source ownership

| Source area | Responsibility |
| --- | --- |
| `Assets/Scripts/Campaign/AI` | Strategic faction assessment, planning, scoring, selection, and turn execution |
| `Assets/Scripts/Combat/Contracts` | Participation enums and component damage exchanged between the modes |
| `Assets/Scripts/Tactical/Contracts` | Authored battle content and detached completed outcomes |
| `Assets/Scripts/Tactical/Core` | Encounter participants, lifecycle, motion, weapons, damage, commands, and completion |
| `Tactical/Core/Targeting` | Target and force assessment shared by player orders and automated policy |
| `Tactical/Core/Automation` | Autonomous deployment, assignment, regrouping, retreat, and battle-station policy |
| `Tactical/Core/Synchronization` | Transport-agnostic commands, complete snapshots, ordered presentation events, and validation/application |
| `Tactical/Core/Mathematics` | Battle vector and formation calculations |
| `Tactical/Runtime/Audio` | Notification scheduling and playback-backed queue maintenance |
| `Tactical/Runtime/Scenery` | Encounter calendar seeding and lazy scenery initialization |
| `Tactical/Runtime/Presentation` | Bounded delayed pose history and interpolation |
| `Tactical/Presentation` | Unity views, camera, input, inspection, rendering, effects, and movie presentation |
| `Multiplayer/Tactical` | Battle driver, protocol, replica playback adapter, and binary snapshot codec |
| `Tactical/Integration/Campaign` | Capturing campaign hardware and facilities, constructing encounters, and the campaign-result presentation bridge |

`SpaceCombatCommands` remains the campaign owner of a pending encounter and its result import.
The shared multiplayer coordinator owns connection lifetime and admits a battle driver after the
ordered campaign begin request. Shared `Multiplayer/Transport` code owns packet contracts, framing, fragment assembly, and bounded delivery;
it does not choose battle targets or mutate encounter participants.

Tests mirror their subject under `Assets/Tests/EditMode/UnitTests/Tactical`. Unity asset GUIDs move
with scripts. Existing persisted contract namespaces and XML members remain unchanged.

## Enforced dependencies

- `Rebellion.Util` cannot reference application or mode implementations.
- `Rebellion.Networking.Transport` owns peer/packet contracts, bounded sends, byte framing, and
  snapshot fragmentation. It compiles without engine, application, utility, or mode references.
  Wire size limits belong to its packet contract; encoders consume them without a reverse dependency.
- `Rebellion.Combat.Contracts` and `Rebellion.Tactical.Contracts` do not reference either mode's
  implementation. Their existing XML preservation metadata retains its Unity scripting reference.
- `Rebellion.Tactical.Core` compiles with engine references disabled. It references contracts and
  utilities, excluding campaign objects, application logging/I/O, scene controllers, runtime service
  implementations, and transport implementations.
- `Rebellion.Tactical.Runtime` also compiles without engine or campaign references. Composition
  supplies its concrete services through core-owned interfaces.
- Presentation, networking, and campaign adapters currently compile in `GameAssembly` because they
  use existing application infrastructure. Architecture rules reject presentation-to-transport,
  transport-to-presentation, and adapter-to-presentation/transport dependencies there.
- Campaign runtime rules reject UI and transport implementations, including their tactical
  namespaces. Production assembly loading and nonempty source checks prevent moves from bypassing
  those rules; deliberately invalid fixtures exercise rejection.

Every production assembly definition contributes to the simulation build identity. Changes to an
assembly boundary invalidate the identity alongside source and package changes.

## Campaign handoff

`TacticalCombatUnitCapture` reads campaign hardware, readiness, payload counts, component damage,
weapons, carrier ownership, and planetary facilities before constructing participants. The encounter
copies values and identifiers; it retains no live campaign entity or scene-graph reference.

`TacticalCombatSession.CaptureOutcome` produces identifiers, final pools, component damage, recovery,
withdrawal, and terminal flags. `SpaceCombatCommands` resolves these against the captured campaign
roster at its result boundary. Local completion retains its result conversions. Remote completion
retains stricter input validation before campaign mutation. Automatic resolution keeps its existing
implementation; shared handoff changes must preserve both completion paths.

`SpaceCombatObserver` applies manual battle aftermath after ordinary campaign reactions settle.
It reads accepted destruction from the encounter ledger using each retained unit's current owner
and value, and reads the current global military roster for force weighting. Loyalty reads the existing recruitment-faction
flags, so a change of current owner does not change allegiance. Sector support requests use the saved
launch profile, retained planet eligibility, population, and counterpart support threshold; the
existing planetary-control pipeline applies support and ownership reactions. Automatic results
retain their existing aftermath path.

Manual battle entry checks required support configuration and retained planet history before
deployment. Missing history remains explicit and is rejected without selecting an eligibility
value. Neither rendering nor transport chooses campaign consequences.

## Assessment and external services

Mandatory target assessment is independent of whether a faction is automated. Player attack orders
and automatic fleet policy consume the same retained candidate ordering and force calculations.
The planner owns policy and its existing cadence; the assessment owns candidate caches and scoring.
No additional campaign graph traversal or per-candidate graph query is introduced.

The core requests notification behavior through interfaces. Runtime scheduling retains playback
activity, refresh points, variant selection before capacity checks, retries, and shared random draw
order. Unity presentation supplies actual playback. Queue completion remains observable to battle
lifecycle; removing that relationship would change behavior.

Scenery initialization remains lazy: its runtime service receives the encounter stream and clock,
then performs the same reseed and four draws when scenery first needs a position. Rendering does not
choose a replacement random stream. `RandomExtensions.NextBits` requests a width from an existing
provider; it neither changes the provider algorithm nor increases its entropy.

File logging requires an explicit destination on first enable. Null retains an already configured
destination. The utility logger does not choose an application profile directory; its application
callers supply paths. Invalid configuration is rejected before replacing the active destination.

## Model projection and delivery policy

The core owns faction command eligibility, independent selection contexts, authoritative/passive
mutation gates, and validation/application of complete model projections. These operations contain no
EOS identities, lobby state, packet encoding, fragment handling, retry timers, or transport queues.
The projection ordinal prevents stale model replacement; it does not acknowledge packet delivery.

The networking protocol establishes the authenticated side, validates battle identity, orders and
acknowledges commands/effects, publishes snapshots, recovers checkpoints, and enforces deadlines.
Its binary codec owns wire field order, versioning, compression, and bounds. These policies remain
outside the core. Source terminology consistently calls complete model projections snapshots; the
existing wire numeric values and field order are retained.

Replica playback observes validated incoming projections and converts them to pose snapshots.
The runtime buffer owns only bounded pose history, presentation time, interpolation, and lifecycle
discontinuities. The guest driver owns the adapter lifetime and detaches it when disposed. Rendering
reads the timeline without advancing simulation, outcomes, or random state.

## Verification requirements

- Preserve save fields, XML members, content addresses, script GUIDs, wire values, and field ordering.
- Compare deterministic state, ordered effects, random consumption, outcomes, and timings with
  identical fleets, seeds, clocks, and content before and after execution changes.
- Verify actual compiled dependencies and negative architecture fixtures.
- Run relevant lifecycle, save/content, rendering, network impairment, and completion tests.
- Run formatting, compilation, static analysis, architecture checks, and the complete Unity suite
  against the exact final worktree before pushing.
- Rebuild the headless integration player from that worktree and exercise two-account EOS recovery,
  natural completion, and large-fleet forced-relay completion. Local tests do not establish Internet
  connectivity, latency, or rendered frame rate.

Unknown gameplay behavior remains unresolved rather than receiving a new default during cleanup.
Vector calculations use double-precision intermediates and retain their float storage boundaries; wrapped multiplication and
general saturating arithmetic have different results and are not substituted for one another.

## Design references

Unity assembly definitions enforce compilation dependencies and reject cycles; folders alone do not.
See the [Unity assembly documentation](https://docs.unity3d.com/6000.4/Documentation/Manual/assembly-definitions-intro.html).

The modes retain separate models and lifecycles while exchanging explicit data, following the boundary
principle in Microsoft's [domain model guidance](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model).
This does not introduce separate processes or network services.
