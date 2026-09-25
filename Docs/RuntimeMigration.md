# Runtime migration map

## Status and scope

This is the source-based migration map for [the agreed decisions](ArchitectureDecisions.md),
not a claim that the replacement runtime is implemented or behaviorally equivalent.
The baseline is commit `c7fc3011`. The scan includes production callers and test
callers, not just declarations. No result payload or gameplay algorithm has been
changed. The first implementation slice replaces result delivery, as recorded below.

The latest instruction takes precedence: preserve existing behavior. In particular,
do not use this refactor to standardize publication timing, failure handling,
acceptance semantics, or listener order where those currently differ.

[The method inventory](RuntimeMethods.md) records all 995 method/constructor
declarations in the scanned manager, runtime coordinators, combat resolvers,
authored-event definitions, requests, and results. It records direct callers and
direct test references; a missing direct reference is **not** proof of missing
indirect coverage. Query-helper destinations require inspecting their transitive
calls, not guessing from names such as `Get` or `Resolve`.

## Existing ownership to replacement responsibility

The command columns below refer to ordinary method-owning classes, not wrappers,
command payloads, or classes containing one operation each. Existing method names
and overload semantics are retained during extraction. Renaming every method to
`TryStart` or `AdvanceTick` is not a prerequisite for this move.

Queries are extracted only where there is actual rule-based read behavior. Private
helpers remain with their implementation owner. A helper used by both a query and
a command is moved once to the read implementation if it is genuinely read-only;
it is not duplicated. RNG consumption is a state change.

| Existing owner | Action/tick destination | Read-only extraction | Reaction responsibility |
| --- | --- | --- | --- |
| `BlockadeSystem` | `BlockadeCommands`: detection, evacuation losses | None required by current public surface | Produces blockade facts |
| `BombardmentSystem` | `BombardmentCommands`: both execution paths and damage | `BombardmentQueries`: eligibility, strength, shields, active targets | Publishes the completed bombardment batch |
| `CaptiveSystem` | `CaptiveCommands`: custody, release, escape attempts | No new query class required | Capture/ownership callbacks select officers and invoke the operations; capture ordering gate below |
| `DuelSystem` | `DuelCommands`: existing duel resolution | None | Authored executor invokes it; remove request routing only after containment/order coverage |
| `FactionAutomationSystem` | `FactionAutomationCommands`: per-faction and tick automation | Uses manufacturing and garrison queries | No per-tick subscriber; scheduled explicitly |
| `FleetSystem` | `FleetCommands`: create and remove fleets | None | No new observer class required |
| `FogOfWarSystem` | `FogOfWarCommands`: snapshot capture, refresh, removal | `FogOfWarQueries`: visibility and faction-view construction | Intelligence and settled sabotage callbacks preserve their different delivery phases |
| `HeadquartersSystem` | `HeadquartersCommands`: relocation, arrival/loss effects | `HeadquartersQueries.CanRelocate` | Arrival and ownership callbacks delegate relocation/loss operations |
| `JediSystem` | `JediCommands`: discovery/growth and tick progression | None | Mission-success callback invokes `ApplyForceGrowth` |
| `MaintenanceSystem` | `MaintenanceCommands`: upkeep/scrapping | Direct `Faction.GetTotalProjectedMaintenanceCost`; the forwarding query has no external callers | Scrapping publishes its completed batch immediately, as today |
| `ManufacturingSystem` | `ManufacturingCommands`: start, enqueue, cancel, retarget, advance, rebuild | `ManufacturingQueries`: eligibility and completion estimates | Destruction/scrap/bombardment/assault callbacks identify affected queues and invoke cancellation/invalidation |
| `MessageSystem` | `MessageCommands`: deliver and expire messages | None | Automatic message generation consumes settled batches, not individual facts |
| `MissionSystem` | `MissionCommands`: start, abort, update, teardown, execution-runtime operations | `MissionQueries`: creation previews, options and odds | Capture callback identifies affected missions and invokes interruption; membership/order gate below |
| `MovementSystem` | `MovementCommands`: move, routes, mission returns, evacuation, custody placement | `MovementQueries`: route eligibility, travel estimates, evacuation availability, return-destination selection | Blockade callback invokes relocation; preserve overload-specific publication boundaries |
| `NamingSystem` | `NamingCommands`: per-faction/tick naming | None | None |
| `OfficerLoyaltySystem` | `OfficerLoyaltyCommands`: loyalty shifts and betrayal rolls | None | Ownership callback selects affected officers and delegates shifts |
| `PersonnelSystem` | `PersonnelCommands`: kill/retire | `PersonnelQueries.CanRetire` | None |
| `PlanetaryAssaultSystem` | `PlanetaryAssaultCommands`: execute assault and apply outcome | `PlanetaryAssaultQueries.CanExecute`; existing resolver estimates remain with resolver initially | Publishes completed assault batch |
| `PlanetaryControlSystem` | `PlanetaryControlCommands`: reconciliation, transfer, neutrality, support changes | `PlanetaryControlQueries`: controller selection, active regiment owners and core support resistance | Garrison/support callbacks delegate reconciliation/support operations |
| `RecoverySystem` | `RecoveryCommands`: existing recovery progression | None | None |
| `ResearchSystem` | `ResearchCommands`: existing research progression | None | None |
| `ResourceProductionSystem` | `ResourceProductionCommands`: facility cycles and material delivery | None | Retains explicit smuggling-before-production sequencing |
| `SmugglingSystem` | `SmugglingCommands`: state refresh and output diversion | None: `ResolveProductionRecipient` consumes RNG | Do not add a second tick call; it currently runs inside resource production |
| `SpaceCombatSystem` | `SpaceCombatCommands`: encounter detection, pending resolution, retreat | `SpaceCombatQueries`: fleet-contestation and active-unit predicates | Pending decision state stays with its owner; do not create a second pending-combat store |
| `UprisingSystem` | `UprisingCommands`: progression, garrison reconciliation, mission execution | `UprisingQueries.CalculateGarrisonRequirement` | Garrison callback invokes reconciliation |
| `VictorySystem` | `VictoryCommands`: victory detection/effects | None | Headquarters-loss callback invokes the existing resolution |
| `AISystem` | Move faction selection/cadence into existing `AIDirector` | AI consumes the extracted query APIs | Preserve the existing phase sequence, yields and result accumulation; no extra `AICommands` facade |
| `GameEventSystem` | `GameEventExecutor`: validation, scheduling and activation | Authored evaluation moves out of definitions into runtime | Remains a result subscriber as well as scheduled work |
| `GameResultProcessor` | `GameResultBus` | None | Replace it rather than keep two delivery mechanisms |
| `GameRequestDispatcher` / `IGameRequestHandler` | Remove after event execution is moved | None | Preserve deferred execution and per-request failure containment; see gates |

Observer **methods** in the table are identified responsibilities, not approval to
create one class per result type or to put all gameplay reactions in GameSession.
Separating an observer from its command must leave the mutation in the command;
the observer only interprets the fact and selects the operation. Final observer
class grouping must be checked against the concrete method inventory during each
slice; the table does not pretend that grouping is already implemented.

`PlanetaryAssaultResolver`, `SpaceCombatAutoResolver`, their nested calculation
types, and `SpaceCombatDecision` retain their current algorithms and contracts.
They move to the runtime side with their owners. No new encounter, resolution,
or replacement result payload is required.

## GameManager extraction, method by method

| Current members | Destination / retained responsibility |
| --- | --- |
| `_tickInterval`, `_tickTimer`, `GetGameSpeed`, `SetGameSpeed`, `TryAdvanceTickTimer`, elapsed-time input | Stay in GameManager. Tick-busy/pending checks read the tick processor's existing state, not a new fault state. |
| `InitializeSystems`, `InitializeResultProcessing`, dependency fields | GameSession constructs components and stores subscription handles. No factory. |
| `InitializeGame`, `RebuildDerivedState`, `CopyTemplates` | Session initialization owns scene/index/catalog/queue rebuilding; GameManager retains its clock reset. Preserve current ordering and RNG identity. |
| `ProcessTick`, `ProcessTickIncrementally`, `ProcessTickCore`, `ProcessRemainingTickPhases` | GameTickProcessor owns the one execution sequence. The synchronous entry exhausts that same sequence. |
| `ResolveCombat`, `ResolveCombatRetreat`, `CompleteCombatResolution`, `ReconcileLoadedCombatState`, `ProcessAvailableWaypointContinuations` | Combat commands own battle mutations; GameTickProcessor owns suspension/resumption and waypoint/next-encounter sequencing. |
| `StoreDeferredMessageResults`, `TakeDeferredMessageResults`, `_deferredMessageResults`, `BeginPendingCombatDecision` | GameTickProcessor owns pending-combat scheduling and its existing message-release boundary. |
| `ProcessResults`, `HandleSystemResultsProduced` | Bus delivery plus registered presentation callbacks. Preserve the currently distinct notification paths; do not broadcast extra notifications as cleanup. |
| `ProcessMessageReactions` | Message delivery callback and bus follow-up processing, preserving batch/provenance filters and combat delay. No separate result pipeline. |
| `ProcessFactionAutomation` | Caller invokes the automation operation; preserve its existing automation-then-naming order when options change. |
| `GetGame`, `GetCurrentTick`, `GetPlayerFaction`, `GetPlayerUIState` | Read the existing game/player data at the application composition boundary; don't inject the entire session into each UI controller. |
| `ReplaceGame`, `GameReplaced`, `ReconcileLoadedState` | GameRuntime replaces runtime ownership and rebinds presentation. Existing hot-load behavior and save gating must pass before deleting forwarding APIs. |
| `CombineResults` | Local batch-combination helper at the remaining owning delivery/tick operation; not another service. |

`GameSession` must not inherit these gameplay bodies merely because they leave
GameManager. Its responsibility is construction, wiring and lifetime. GameManager
remains for the explicitly deferred clock work, not as a second command router.

## Tick sequence that must survive extraction

Source: `Managers/GameManager.cs`, `ProcessTickCore`,
`ProcessRemainingTickPhases`, and `CompleteCombatResolution`.

1. Increment the tick, expire messages, run faction automation.
2. Resource production (including smuggling), then manufacturing; deliver each
   returned batch through its existing reaction/message boundary.
3. Run faction automation again to refill completed production lanes.
4. Maintenance, recovery, captivity, in that order with their result boundaries.
5. Movement, then space combat, then eligible waypoint continuations. Their
   gameplay reactions run; combine their results for message handling.
6. If combat is pending, retain that message batch and suspend. Do not increment
   again, run later phases, or announce tick completion.
7. Each resolution processes combat reactions, waypoint continuation and the next
   encounter check, retaining messages until no further decision is pending.
8. Release the combined message batch; run missions, scheduled authored events,
   naming, incremental AI, blockade, planetary control, uprising, research, Jedi
   progression and victory, in that order.
9. Mark the tick idle and notify completion. Autosaving observes this settled point.

Current `finally` blocks clear `_tickInProgress` and reset Processing to Idle;
AwaitingCombatDecision remains awaiting. Preserve disposal/exception behavior.
Combat resumption currently exhausts remaining incremental work synchronously;
changing that to new frame scheduling would be a separate behavior change.

## Result delivery order and ownership

Source: `GameResultProcessor.Process` and
`GameManager.InitializeResultProcessing`. Current delivery is **batch-based**:
every handler processes its matching subset of the current wave; all returned
reactions become the next wave together. Settled observers run after every wave.

Preserve this registration order until the affected operation's ordering dependency
has been removed and independently tested:

1. Authored-event execution (`GameResult`).
2. Movement (`BlockadeChangedResult`).
3. Headquarters (arrival, then ownership).
4. Officer loyalty (ownership), then captivity (ownership).
5. Victory (headquarters loss).
6. Planetary control (garrison, then support shift).
7. Uprising (garrison).
8. Jedi growth (mission completed).
9. Mission interruption (capture), then custody placement (capture).
10. Fog of war (intelligence).
11. Manufacturing (destroyed, scrapped, bombardment, assault).
12. Settled sabotage observation refreshes fog of war.

Presentation is not presently one uniform final broadcast. `ProcessResults`
announces resolved results, assault/victory collections, processes messages, then
announces individual headquarters/victory notifications. Message-delivery reaction
batches go through the processor but do not repeat every one of those callbacks.
Preserve these distinctions before considering consolidation.

Commands publish once, at the boundary that currently releases their results.
Returning a list for existing callers must not cause that same list to be
published again. Nested publication may enqueue follow-ups, but must preserve the
current wave's full batch and not interrupt another subscriber halfway through it.

## Authored-event execution and request removal

The executor must own the execution currently spread across:

- `GameAction.ExecuteAll` and the concrete action bodies in `GameActions.cs`.
- `GameConditional.IsMet` and concrete conditional evaluation.
- Selector execution, binding resolution, trigger matching/binding and schedule
  evaluation in the corresponding `Game/Events` files.
- `GameEvent.AreConditionsMet` / `ExecuteActions` and `GameEventSystem` activation.

Serialized definitions and their persistence names remain in Game. Runtime
evaluation contexts move with execution; serialized event state remains in Game.
Random selection/conditions stay in the executor, not in supposedly read-only
query methods. Preserve enumeration and short-circuit order so RNG consumption
does not shift.

The five routed request types map to existing owning operations:

| Request | Runtime operation |
| --- | --- |
| `UnitMovementRequest` | Movement command, preserving destination fallback and deferred batch release |
| `UnitPlacementRequest` | Movement placement operation, preserving no-transit semantics |
| `OwnershipChangeRequest` | Planetary control transition / existing unit ownership operation |
| `DuelRequest` | Duel execution |
| `MessageDeliveryRequest` | Message delivery, also used by automatic message construction |

`MessageDeliveryRequest` is **not exclusively an authored-event request**:
`MessageFactory.CreateMessages` and `MessageTemplateBuilder` also produce/use it.
Removing that type requires mapping those builders as well; merely moving it into
Simulation would make Game depend on Simulation. Do not hide this dependency with
a new DTO of the same shape or silently move unrelated builders without resolving
their ownership.

## Behavior-preservation gates before implementation

### 1. Capture ordering is an existing dependency, not yet solved

`MissionSystem.HandleResults` discovers the affected mission from the officer's
current parent. `CaptiveSystem.HandleResults` subsequently moves that officer into
custody. Reversing them loses the mission association. Existing
`OfficerCaptureStateResult.MissionInstanceID` cannot simply be repurposed: it can
identify the mission that caused a capture, not the mission the captive belonged to.

Keep the current order while extracting. Removing the dependency requires proven
membership tracking without altering result classes. Do not announce that the bus
alone fixes it, and do not add result fields contrary to the explicit instruction.

Custody extraction also has a smaller failure-order boundary: the capture handler
resolves the officer's owning faction before checking release state, capture
eligibility or duplicate IDs. `GameRoot.GetFactionByOwnerInstanceID` throws for a
missing owner. Moving that lookup behind a new early return would silently change
failure behavior. Preserve that order, the per-batch deduplication set, the capture
result's observation tick and the existing escape-schedule initialization when
separating the custody listener from its operations.

### 2. Immediate publication must not erase existing deferred behavior

`MissionSystem.AbortMission` and several movement/manufacturing methods append to
`_pendingResults`, consumed at their next tick processing boundary. Selection
movement, scrapping and attack commands instead invoke `ResultsProduced` immediately.
Changing all of them to immediate delivery changes observable behavior.

The latest preservation instruction overrides making these timings uniform. Keep
these boundaries explicit in migrated command implementations. Do not make nested
operations publish before their enclosing operation is complete.

### 3. Removing requests must not reorder authored actions

`GameEventSystem.TryActivateEvent` currently executes the full action list, then
dispatches requests, then increments activation history. Directly executing a
movement command where an action currently enqueues a request moves units earlier.
Later selectors/conditions can observe a different scene. Publishing at that point
also allows other authored events to run against a half-completed activation.

Preserve an activation-local deferred execution phase and the original target
selection timing; replacing requests does not authorize eager execution. Both
action and deferred-request exceptions log and continue locally. Bus-handler
exceptions propagate outside those boundaries. Moving a drain into those catches
would silently suppress errors that currently propagate.

### 4. Queries must preserve information boundaries and return meaning

`MissionQueries` must retain observed-target/detector inputs and exclusion of hidden
betrayal information. `FogOfWarSystem.BuildFactionView` returns copied locations
but some owned units remain live references; do not silently replace this with a
new snapshot model. `ManufacturingSystem.StartManufacturing` can return `true`
after partially fulfilling a multi-item order; retain that meaning.

### 5. Hot loading must rebind consumers

`GameRuntime.HotReloadGame` currently replaces the game inside the existing manager.
StrategyController uses a mixture of direct manager access and delegates returning
the current collaborators. Replacing only the session object leaves cached commands,
queries and subscriptions attached to the old game. Rebind all those consumers,
dispose old subscriptions and preserve `GameReplaced` behavior before removing old
accessors. Do not change save content identity checks or permit unsettled saves.

### 6. Failed replacement is not currently atomic

`GameManager.InitializeGame` assigns `_game` before rebuilding its collaborators.
`GameEventSystem.ValidateEvents` can subsequently throw, leaving the replacement
game assigned with a partially rebuilt runtime. The constructor-first session
replacement decision would instead keep the old session intact on that failure.
That is observably different, not just a field relocation.

The constructor-first ownership draft was set aside rather than silently changing
this failure behavior. `GameManagerTests.ReplaceGame_InvalidEvent_LeavesReplacementGameAssigned`
records the current behavior through the public API. The user's reiterated
preservation requirement rules out atomic replacement in this refactor. The
construction/lifetime design must retain in-place initialization, including the
partial state on failure; this is not an outstanding request to change behavior.
The initial characterization passed against the preserved implementation. That
`./build.sh all` run passed with **4,974 tests**, zero failures/skips. Three more
replacement tests passed before the ownership extraction: null rejection leaves
the active runtime untouched, invalid events leave earlier components replaced,
and failure does not announce a completed replacement. The subsequent
`GameSession` extraction therefore rebuilds component properties in place rather
than replacing the session only after successful construction.

## Verification and migration sequence

1. Capture the baseline and add characterization coverage at existing public
   contracts. Keep test names/locations aligned with the current owner until it moves.
2. Consolidate result delivery into the bus, preserving batching, reaction order,
   exceptions and observer timing. Add subscription-disposal and nested-publication
   tests; no new failure policy. Remove the old processor only when callers migrate.
3. Extract tick scheduling and session wiring with the existing behavior locked by
   manager/runtime tests. Leave the clock in GameManager. No second execution path.
4. Migrate gameplay areas in dependency order, splitting actual query behavior,
   moving implementation bodies and updating all production/test callers together.
   No permanent facade around the old coordinator. Preserve pending-result ownership.
5. Move authored execution only after the action/request timing gates have explicit
   tests. Remove requests and their dispatcher after all callers are accounted for.
6. Rebind application/UI/AI dependencies and verify hot-load disposal, save gating,
   immediate automation refresh and pending-combat resumption.
7. Update architecture checks to cover Simulation without weakening Game, SceneGraph
   or utility boundaries. Remove obsolete source files with their Unity metadata;
   regenerate projects rather than manually maintaining generated file lists.
8. Run formatting, lint, the complete build/test suite and matched before/after
   simulations from identical saves/RNG state. Verify Unity assembly freshness first.
   Compare state, ordered results, messages and tick boundaries, not just final wins.

### Existing regression anchors

- Baseline `Systems/GameResultProcessorTests` (now `Simulation/GameResultBusTests`):
  ordered subscribers, breadth-first waves and
  observers after reactions. Added characterization covers whole batches, combined
  next waves, propagated handler/observer exceptions and a clean subsequent invocation.
- `Systems/GameRequestDispatcherTests`: source-event stamping and continuing after
  a request exception.
- `Game/Events/GameActionTests`: continuing after an action exception.
- `Systems/GameEventSystemTests`: nested actions observing earlier local results,
  result-trigger activation, scheduled/triggered separation and message suppression.
- `Systems/MissionSystemTests`: observed mission odds and captured-participant teardown.
- `Systems/ManufacturingSystemTests`: cancel/refund behavior, replacement orders,
  existing-fleet delivery and queue/capacity invariants.
- `Managers/GameManagerTests`: immediate garrison effects, automation refill,
  capture lifecycle, movement/combat messages, pending combat, waypoints and iterator disposal.
- `App/GameRuntimeTests`: pending-combat save deferral and hot loading.

These are anchors, not a claim of full coverage. Missing cases include explicit
action/request interleaving, subscription lifetime across replacement, and exactly-once
publication after the bus replaces both returned-list routing and `ResultsProduced`.

## Baseline evidence

- Initial `./build.sh all` failed before gameplay tests because Unity's generated
  `GameAssembly.csproj` referenced missing files from the abandoned refactor.
- Regenerated projects using the installed Rider integration's
  `Packages.Rider.Editor.RiderScriptEditor.SyncSolution`; no build-script workaround
  or missing source stubs were added.
- Repeated `./build.sh all` on unchanged production sources: passed, including
  lint/architecture checks and **4,958 / 4,958 EditMode tests**, zero failures/skips.
- Semantic audit: 784 source files, 257 cataloged types, 995 methods/constructors,
  zero compilation diagnostics in that audit. Its unresolved invocation entries
  were `nameof` expressions, not unresolved gameplay calls.
- No before/after simulation equivalence claim has been made. The refactor and
  its complete verification remain outstanding.

## Implementation progress

- Completed the baseline source/method/caller map and added six characterization
  tests before replacing the processor. `./build.sh all` passed with **4,964 tests**.
- Replaced `Systems/GameResultProcessor` with `Simulation/GameResultBus`; there is
  no parallel processor or second reaction pipeline. Existing result classes are unchanged.
- Migrated GameManager's subscriptions in their existing order and preserved its
  message/tick boundaries. Existing returned-reaction callbacks use the bus's
  typed callback overload while those gameplay owners await their command migration.
- Added direct publication callbacks, disposable registrations and queued nested
  publication. The return-list overload preserves the existing operation contracts;
  publishers must not both publish a fact and return it for a second publication.
- Moved the processor tests to the matching Simulation directory/namespace,
  preserving their expectations, and added nested-publication, disposal, isolation
  and failure-cleanup coverage. Existing movement tests now use the same bus.
- Extended the presentation-dependency architecture check to include Simulation.
- Verified this bus slice with `./build.sh all`: **4,973 / 4,973 tests passed**, no
  failures/skips; line coverage **81.4%** and method coverage **91.2%**, both above
  the configured gates. The original 4,958 tests remain represented after fixture
  relocation; the increase is six pre-migration characterization tests and nine
  bus-specific cases.
- Replaced `PersonnelSystem` with `Simulation/PersonnelCommands` and
  `Simulation/PersonnelQueries`. The commands own kill/retire mutations; queries
  own eligibility and authoritative selection resolution. Neither forwards to
  the deleted system, and validation is shared rather than duplicated.
- Kept all five personnel method bodies unchanged except for qualifying the
  shared validation call. Retirement still validates the entire selection before
  mutation. Killing retains identity/parent references and clears movement as
  before. Mission/bombardment callers retain ownership of their death-result
  batches; no new publication or result payload was introduced.
- Migrated the strategy retirement controller to separate command/query dependencies,
  keeping deferred dependency lookup for game replacement. Added coverage for
  capture between dialog opening and confirmation, and replacement while the
  dialog is open. Existing UI retirement tests retain their assertions.
- Ran **15 personnel tests against the original implementation** before extraction,
  including seven new characterization cases. Moved those tests to the respective
  command/query fixtures and added the command dependency-constructor test.
- Verified the personnel extraction with `./build.sh all`: **4,984 / 4,984 tests
  passed**, zero failures/skips; formatting, lint, all 25 analyzer tests and all
  eight architecture cases passed. Coverage remains **81.4%** line / **91.2%**
  method. This is regression evidence for the current work, not proof that the
  outstanding migration or matched-simulation verification is complete.
- Moved component construction, derived-catalog/queue rebuilding, and result
  subscription wiring into `Simulation/GameSession`. It owns subscription teardown
  on successful replacement and disposal, without a factory or gameplay methods.
  Failed replacement retains the existing partial reconstruction and connected
  previous bus until reconstruction reaches the corresponding successful boundary.
- Kept scene preparation, tick execution, and clock behavior in GameManager for
  this intermediate ownership move. The session's two-argument constructor takes
  a prepared graph; moving scene preparation and application ownership is still
  outstanding. The temporary immediate-result event preserves the current
  synchronous manager delivery boundary until message/presentation subscriptions
  move out. This event is not a second bus or the completed publication design.
- Compared the clock, tick, combat-resumption and message-processing method bodies
  before and after ownership extraction: only component-reference qualification
  changed. Added session lifecycle tests for connected reactions, immediate
  results, successful replacement, failed validation and disposal. GameRuntime
  disposes the manager's session when ending the game.
- Verified the ownership extraction with `./build.sh all`: **4,996 / 4,996 tests
  passed**, zero failures/skips, including the four pre-extraction replacement
  characterizations and all nine session tests. Formatting, lint, 25 analyzer
  tests, eight architecture cases and coverage gates passed; coverage is **81.4%**
  line / **91.3%** method. No simulation-equivalence claim is made yet.
- Extracted the single synchronous/incremental tick path, pending-combat state,
  combat resumption, loaded-combat reconciliation and deferred-message batch into
  `Simulation/GameTickProcessor`. The thirteen moved method bodies preserve their
  sequence and exception boundaries; only dependency references and the clock-reset
  callback changed. There is no second tick loop and no broadcast tick subscription.
- Kept the elapsed-time clock in GameManager. Save eligibility reads `IsSettled`,
  while clock advancement reads `IsBusy`: completion observers may save while a
  nested tick remains blocked until the iterator exits. Replacement still clears
  pending scheduling without clearing the guard of an active iterator.
- Ran all **37 manager tests before extraction**, including four new characterizations
  for completion timing, observer failure, an unstarted iterator, and replacement
  during a suspended iterator. Added direct processor tests alongside the existing
  manager/runtime integration coverage.
- Tick dependencies are explicit typed providers, matching current-component lookup
  during in-place replacement; the processor does not receive GameSession or look
  up arbitrary dependencies. Its construction remains temporarily in GameManager
  because result presentation/message release still lives there. Moving that
  wiring into GameSession and removing those callback bridges remains outstanding.
- Verified tick extraction with `./build.sh all`: **5,007 / 5,007 tests passed**,
  zero failures/skips. The existing pending-combat, waypoint, message, autosave and
  hot-load tests pass alongside the new processor tests. Formatting, lint, 25
  analyzer tests, eight architecture cases and coverage gates passed (**81.4%**
  line / **91.3%** method). GameManager no longer contains tick phase bodies.

- Moved fleet formation, naming and recovery operations into `FleetCommands`,
  `NamingCommands` and `RecoveryCommands`. Their executable source is unchanged
  after normalizing type/namespace names and whitespace; no forwarding wrappers,
  additional query objects or result publication points were introduced.
- Split headquarters eligibility into `HeadquartersQueries`, relocation and
  ownership/arrival mutations into `HeadquartersCommands`, and typed batch
  callbacks into `HeadquartersObserver`. Query/relocation bodies are unchanged
  apart from the query receiver. Ownership reactions still accumulate in input
  order and return one batch at the existing listener boundary. The session
  retains the original arrival/ownership registration positions.
- Ran **65 tests against the four original implementations** before migration,
  including eight new headquarters characterization cases. Relocated their
  fixtures alongside the extracted implementations and added explicit arrival
  and ownership batch-order checks, including the existing current-tick stamp
  rather than the incoming result's tick.
- Verified these four operation migrations with `./build.sh all`: **5,018 / 5,018
  tests passed**, zero failures/skips. Formatting, lint, 25 analyzer tests,
  eight architecture cases and coverage gates passed (**81.4%** line / **91.3%**
  method). No existing result class or serialized game field changed.

- Split officer-loyalty ownership callbacks into `OfficerLoyaltyObserver` and
  moved loyalty mutation/betrayal operations into `OfficerLoyaltyCommands`.
  `ApplyControlShift` is the existing mutation body, not a query; betrayal still
  returns its results to mission resolution at the existing release boundary.
  The session retains the original ownership subscription position. No new
  notification is emitted for the existing silent loyalty changes.
- Moved research progression into `ResearchCommands` without changing executable
  code beyond type/namespace names. Constructor timer initialization and its RNG
  position in session construction are unchanged.
- Ran **18 original loyalty/research tests before extraction**, including two
  new ownership-batch characterizations. They establish sequential clamping,
  one roll per incoming faction, no roll for neutral/null entries, and no roll
  for an absent batch. Moved those checks into the listener fixture and kept
  mutation/betrayal assertions in the command fixture.
- Verified the loyalty/research extraction with `./build.sh all`: **5,020 / 5,020
  tests passed**, zero failures/skips. Formatting, lint, 25 analyzer tests,
  eight architecture cases and coverage gates passed (**81.4%** line / **91.3%**
  method). The complete research source and loyalty operation bodies compare
  unchanged after normalizing extraction names, namespaces and documentation.

- Split victory and Jedi result listeners into `VictoryObserver` and `JediObserver`.
  Their command classes own the existing tick and mutation implementations.
  Victory retains one declaration flag shared by tick detection and loss reactions;
  Force growth retains its successful-mission filtering, participant order and
  returned batch. Both listeners retain their original subscription positions.
- Ran **43 victory/Jedi tests before extraction**, including six new characterizations
  covering first eligible victory, later conquest eligibility, null batches,
  sequential mission growth and the existing null-entry exception behavior.
  Moved listener tests into matching observer fixtures without weakening assertions.
- Verified these extractions with `./build.sh all`: **5,026 / 5,026 tests passed**,
  zero failures/skips. Formatting, lint, 25 analyzer tests, eight architecture
  cases and coverage gates passed (**81.4%** line / **91.3%** method).

- Removed the `AISystem` wrapper and moved its faction scheduling into the
  existing `AIDirector`. No `AICommands` facade was added. The director now owns
  the existing cadence check, AI-controlled faction selection and faction-view
  construction before its unchanged phase sequence. Its synchronous entry still
  exhausts the same incremental iterator; results still join the aggregate only
  after the faction's phases finish.
- Ran **seven scheduler tests before consolidation**, including disabled intervals,
  human-controlled faction exclusion and disposal after the initial context yield.
  Moved those tests to `AI/Director/AIDirectorTests`. Source comparison confirms
  both faction-execution bodies are unchanged and the two moved tick bodies differ
  only by removing the wrapper's director receiver.
- Verified AI consolidation with `./build.sh all`: **5,030 / 5,030 tests passed**,
  zero failures/skips. Formatting, lint, 25 analyzer tests, eight architecture
  cases and coverage gates passed (**81.4%** line / **91.3%** method). No changes
  were made to AI planning/scoring/selection/execution algorithms or phase order.

- Split custody result selection into `CaptiveObserver` and custody/release/escape
  operations into `CaptiveCommands`. Kept owner resolution before release and
  eligibility checks, duplicate suppression per batch, incoming observation ticks,
  and mission-before-custody subscription order. The observer returns the complete
  reaction batch at the existing boundary; the extraction does not add publication.
- Ran **38 custody tests against the original implementation**, including seven
  new cases for duplicate batches, mixed release/capture results, missing-owner
  failure order, null input, and release tick provenance. Moved tick tests to the
  command fixture and result-selection tests to the observer fixture, then added
  direct checks that release preserves movement and does not apply escape loyalty.
- Verified custody extraction with `./build.sh all`: **5,039 / 5,039 tests passed**,
  zero failures/skips. Formatting, lint, analyzer/architecture tests and coverage
  gates passed. Source comparison confirms constructor and 16 operation/helper
  bodies unchanged; destination selection differs only in receiving the context
  and capturing unit directly rather than extracting them from a result.

- Moved maintenance operations into `MaintenanceCommands` with their existing
  immediate-scrap event and deferred tick batch boundaries. Removed the unused
  forwarding query: its only caller now invokes the same faction calculation
  directly. No query class or observer is needed for this area's current surface.
  AI, UI, session lifetime connections and tick callers now use the command owner.
- Ran **21 maintenance tests before migration**, including complete-selection
  validation, single post-removal batch delivery, and listener-exception propagation
  after committed removal. Moved three faction-capacity-only cases to `FactionTests`
  and the remaining cases to `MaintenanceCommandsTests`. Source comparison confirms
  constructor and operation bodies unchanged except inlining the forwarding query.
- Verified maintenance migration with `./build.sh all`: **5,042 / 5,042 tests passed**,
  zero failures/skips. Formatting, lint, 25 analyzer tests, eight architecture cases
  and coverage gates passed (**81.4%** line / **91.3%** method). Compared all 13
  affected caller files: executable changes are limited to the migrated type and
  property references. Immediate scrap delivery, session detachment/replacement,
  and last-garrison control reconciliation remain covered by the full suite.

- Split planetary control into `PlanetaryControlCommands`, `PlanetaryControlQueries`
  and `PlanetaryControlObserver`. The command owns ownership/support mutations and
  tick progression; the query owns controller selection and support resistance;
  the listener delegates garrison/support batches at the existing boundary. The
  ownership-request interface remains until authored execution is migrated.
- Ran **123 control/bombardment/uprising tests before extraction**, including eleven
  new control cases covering sequential support-result order/ticks, ineffective
  resistance-adjusted shifts, null batches, malformed later garrison entries after
  an earlier mutation, and signed integer resistance calculations. Moved the cases
  into corresponding command, query and observer fixtures. Added six direct query
  checks for faction-order ties, no ownership mutation, active-garrison precedence,
  inactive regiments, contested garrisons and outer-rim resistance bypass.
- Compared 29 retained operation bodies, five query bodies, the garrison callback
  and the ownership-request implementation against their pre-extraction source.
  They are unchanged apart from query qualification. Support-result mutation was
  extracted into `ApplySupportShift`; its observer still preserves sequential
  processing and stat-before-ownership result order. The session keeps control
  reactions before uprising reactions. No result class or serialized state changed.
- Verified planetary-control extraction with `./build.sh all`: **5,059 / 5,059
  tests passed**, zero failures/skips. Formatting, lint, 25 analyzer tests, eight
  architecture cases and coverage gates passed (**81.4%** line / **91.3%** method).
  The first verification attempt stopped on an unused query import; it was removed
  before the complete successful run. Simulation equivalence remains outstanding.

- Extracted uprising progression and mission operations into `UprisingCommands`,
  the existing stateless garrison calculation into `UprisingQueries`, and garrison
  result selection into `UprisingObserver`. Updated mission, tick, automation, AI,
  defense presentation and editor reporting callers. The session registers the
  observer in its existing position after planetary-control reactions.
- Ran **43 uprising/garrison tests before extraction**, including six new cases
  covering no constructor RNG consumption, clear/support/incident initialization
  order, saved timer-order tie breaking, overdue pulse catch-up, absent batches,
  and an invalid later result throwing after an earlier mutation. All 35 test
  methods (43 cases) remain in the matching command/query/observer fixtures.
- Compared all 52 constructor/method bodies against the pre-extraction source;
  only moved-reference qualification changed. Timer state, RNG order, mission
  outcomes, result payloads and serialization remain unchanged. The first Unity
  compile exposed a missing automation import; the subsequent compile passed.
  Full validation then exposed an obsolete editor import, which was removed.
- Verified uprising extraction with `./build.sh all`: **5,065 / 5,065 tests
  passed**, zero failures/skips; formatting, lint, analyzer/architecture checks
  and coverage gates passed (**81.4%** line / **91.3%** method).

- Moved blockade transition detection and evacuation-loss operations into
  `BlockadeCommands`, including its existing transition cache. The session still
  constructs one instance and passes it to movement; the tick processor still
  detects blockade transitions after AI and before planetary control. Movement
  still checks evacuation losses before calculating or assigning transit.
- Ran **17 blockade tests before extraction**, including three new cases for
  starts-before-ends ordering with current-tick stamps, deletion before returning
  an evacuation-loss result, and officer evacuation without an RNG roll. All seven
  operation/constructor bodies are unchanged. The fixture and metadata moved with
  the implementation; no extra query or observer class was introduced.
- Verified blockade extraction with `./build.sh all`: **5,068 / 5,068 tests
  passed**, zero failures/skips; formatting, lint, analyzer/architecture checks
  and coverage gates passed (**81.4%** line / **91.3%** method).

- Moved resource cycles and material delivery into `ResourceProductionCommands`,
  with its existing owned smuggling collaborator moved into `SmugglingCommands`.
  Kept constructor validation/cache initialization, smuggling-before-production,
  faction/facility/request iteration order, same-tick material delivery and RNG
  ownership intact. Recipient selection remains an operation because it rolls RNG.
  There is no second scheduled smuggling call and no new query/observer class.
- Ran **27 resource/smuggling tests before extraction**, including four new cases
  for detached/no-smuggling output avoiding RNG and the strict diversion threshold.
  Compared both complete production files after normalizing type/namespace moves;
  the implementations are otherwise unchanged. Fixtures and Unity GUIDs moved
  with their owners, and the session/tick/test callers reference the replacements.
- Verified resource/smuggling extraction with `./build.sh all`: **5,072 / 5,072
  tests passed**, zero failures/skips; formatting, lint, 25 analyzer tests, eight
  architecture cases and coverage gates passed (**81.4%** line / **91.3%** method).
  Latest verification log: `/tmp/reb2-runtime-map.63ZpfP/resources-build.log`.
  This proves the current regression gates, not matched simulation equivalence.

- Extracted manufacturing queue mutations, progression, reconstruction and its
  pending-result buffer into `ManufacturingCommands`. Moved fourteen eligibility,
  capacity and estimate methods into `ManufacturingQueries`; its shared carrier
  predicate serves both validation and enqueue without duplicated rules. Moved
  four typed loss-result selectors into `ManufacturingObserver`, delegating to the
  unchanged unsupported-production cancellation operation.
- Updated 95 construction sites and direct command consumers. Construction UI
  obtains eligibility rules separately from mutations, status estimates use the
  queries, and AI structural acceptance calls the stateless query with its existing
  inputs. Session subscriptions retain their original positions. Enqueue still
  buffers results until manufacturing ticks; facility-loss reactions still mutate
  immediately and return no follow-up results. No result/save contract changed.
- Ran **131 manufacturing tests before extraction**, including three new cases
  covering pending creation results surviving cancellation, assault cancellation
  without new reactions, and read-only eligibility preserving existing work.
  Moved query and listener cases into matching fixtures. Rehomed two tests of
  scene attachment/planet queue initialization with their actual subjects and
  corrected moved enqueue test prefixes to name their public operation.
- Compared all 61 existing non-constructor method bodies against the pre-extraction
  implementation: unchanged after qualifying calls to the moved query/cancellation
  methods. The constructor retains its game/fleet guard order and existing
  assignments, then stores the required query dependency. Unity compilation passed.
- Initial manufacturing verification passed `./build.sh all` with **5,075 / 5,075
  tests**, zero failures/skips and all format/lint/analyzer/architecture/coverage
  gates. Added direct coverage afterward for the extracted query constructor,
  required command query dependency, absent queue estimates and estimates that
  must not alter queue membership or facility/item progress.
- Final manufacturing verification passed `./build.sh all`: **5,079 / 5,079
  tests**, zero failures/skips; formatting, lint, 25 analyzer tests, eight
  architecture cases and coverage gates passed (**81.5%** line / **91.3%** method).
  Log: `/tmp/reb2-runtime-map.63ZpfP/manufacturing-build-2.log`.
  No matched simulation comparison has been run, so full behavioral equivalence
  remains unproven and the overall migration remains incomplete.

- Extracted mission execution, interruption, teardown, recruitment exhaustion and
  pending abort consequences into `MissionCommands`. Moved the existing creation
  previews, observed-target eligibility and probability calculations into
  `MissionQueries`, without an RNG dependency. Shared detector/evasion rules now
  live once in the query owner and are also used by the execution operations.
- Moved capture selection into `MissionObserver`. It still snapshots distinct
  missions from participants' current parents before invoking interruption. Its
  session subscription remains immediately before captivity's capture listener;
  it returns reactions in the current pass, whereas explicit aborts retain their
  next-mission-tick buffer. Result payloads and persistence types are unchanged.
- Ran **117 mission/capture lifecycle cases before extraction**, including new
  checks for explicit empty detector observations, previews leaving participants
  and the graph unchanged, and aborted stranded missions releasing their original
  tick-stamped capture result exactly once on the next mission tick. The new
  abort fixture required correcting its initial ownership and registering its
  captor faction; no production change was made to make the baseline test pass.
- Rehomed existing mission cases into command, query and observer fixtures with
  matching namespaces. Query fixtures now construct only query dependencies, not
  the movement/uprising runtime. Added direct query validation and observer
  release/null-entry/constructor cases. Tests of private teardown/start behavior
  continue through `UpdateMission`/`InitiateMission` and are named accordingly.
- Compared **52 existing method bodies** (including the three explicit execution
  runtime implementations and detector-rating switch): unchanged apart from
  moved-method qualification and formatting. Manually audited the constructor,
  capture selector/delegation, and folding `CreateAndBeginMission` into
  `InitiateMission`: the existing query performs the same context resolution and
  factory validation before resolving the live location and starting travel.
- Rebound mission UI and AI consumers to separate command/query dependencies;
  session/tick consumers retain execution ownership. Unity compilation passed.
- Verified the mission extraction with `./build.sh all`: **5,087 / 5,087 tests
  passed**, zero failures/skips; formatting, lint, 25 analyzer tests, eight
  architecture cases and coverage gates passed (**81.5%** line / **91.3%** method).
  Log: `/tmp/reb2-runtime-map.63ZpfP/mission-build-2.log`; test run finished at
  `2026-09-21 05:21:47Z`. The first build stopped at five unused imports from the
  caller rewiring; those were removed before this successful full run.
  No matched simulation comparison has been run; full behavioral equivalence
  and completion of the overall architecture remain unproven.

- Moved delegated production/garrison automation into `FactionAutomationCommands`
  and duel execution into `DuelCommands`. Whole-file executable-code comparisons
  match their pre-move implementations after namespace/type-name normalization;
  no algorithms, guards, RNG calls, result stamping or queue behavior changed.
- Automation retains both scheduled passes around resource/manufacturing work,
  plus the immediate faction path followed by naming. There is no extra query or
  observer class for automation. Duel execution retains its deferred request
  handler until authored execution and request removal are migrated together;
  the dispatcher registration position and per-request failure boundary remain.
- Ran **21 baseline cases** before these moves: 13 automation cases, seven duel
  cases, and the manager's immediate naming case. Seven new characterizations
  cover immediate queueing without tick advancement, repeated passes preserving
  queued orders, null faction rejection, invalid duel requests without rolls,
  capture invalidating subsequent same-pair requests, result order/current-tick
  stamping, and null request batches. Rehomed both fixtures and their metadata;
  corrected duel test prefixes to `HandleRequests` and documented test methods.
- Verified these moves with `./build.sh all`: **5,094 / 5,094 tests passed**, zero
  failures/skips. Formatting, lint, 25 analyzer tests, eight architecture cases and
  coverage gates passed (**81.5%** line / **91.3%** method).
  Log: `/tmp/reb2-runtime-map.63ZpfP/automation-duel-build.log`.
  No matched simulation equivalence is claimed; the overall migration remains
  incomplete.

- Extracted assault eligibility into `PlanetaryAssaultQueries` and execution/state
  application into `PlanetaryAssaultCommands`. Moved `PlanetaryAssaultResolver`
  and its fixture under `Simulation/Combat`; the entire resolver file matches
  after its namespace change. All seven non-constructor coordinator method bodies
  match after qualifying the moved eligibility methods. The constructor retains
  its game/ownership/resolver validation order, then stores its query dependency.
- Preserved the separate assault entry contracts: `Execute` returns its outcome
  without publishing; `TryExecute` filters null fleet entries, validates the whole
  action, then publishes result/events/ownership in that order. Shield rejection
  still returns a blocked result through direct execution and null through the
  immediate command. Listener exceptions still propagate after state mutation
  and combat cleanup. Session producer forwarding and AI deferred accumulation
  remain in their existing positions.
- Ran **34 assault/resolver/fleet-controller cases before extraction**, including
  five new cases for direct execution without publication, shield rejection
  without publication/RNG, listener failure after successful state application,
  direct eligibility rejecting null fleet entries, and the immediate command
  filtering those same entries. Split the mixed eligibility/execution test into
  query and command cases without dropping its assertions; added query-constructor
  coverage and rehomed/documented the affected fixtures.
- Rebound UI and AI eligibility consumers to the shared query dependency while
  retaining their distinct execution paths. Unity compilation passed after adding
  the moved command fixture's missing namespace import.
- Verified the assault extraction with `./build.sh all`: **5,101 / 5,101 tests
  passed**, zero failures/skips; formatting, lint, 25 analyzer tests, eight
  architecture cases and coverage gates passed (**81.5%** line / **91.3%** method).
  Log: `/tmp/reb2-runtime-map.63ZpfP/assault-build.log`.
  This remains regression evidence, not proof of matched simulation equivalence
  or completion of the overall runtime migration.

- Extracted bombardment eligibility, strength, shield and target queries into
  `BombardmentQueries`, and the existing damage/state execution into
  `BombardmentCommands`. Shared active-unit/condition/leadership rules remain
  single implementations in the query owner. The execution-only target builders
  and strike ordering remain with the commands; no separate resolver or observer
  was introduced for this area.
- Compared all **50 non-constructor method bodies**: unchanged after moved-method
  qualification and whitespace normalization. The command constructor retains
  movement/ownership validation and personnel fallback before accepting its new
  query dependency. Queries add no game-null rejection that the old coordinator
  did not have.
- Ran **57 bombardment/fleet-controller/AI-attack cases before extraction**,
  including seven new characterizations of deferred direct execution, immediate
  rejection without RNG/publication, listener failure after completed mutation,
  combat cleanup without waypoint restoration when RNG throws, null fleet
  handling, and exact publication order after combat cleanup.
- Preserved UI immediate publication and AI result accumulation as separate
  existing execution contracts. Session forwarding/subscription positions remain
  unchanged. UI and AI query consumers now receive the query dependency; AI
  observed-state assessments retain their existing inputs and caches. No result
  payload, serialized game type, authored data, or scene/prefab was changed.
- Rehomed and documented bombardment tests in matching command/query fixtures.
  Query tests construct only the query owner and scene data. Split the mixed
  planet-destruction rejection assertion into a dedicated query case and retained
  the command outcome assertions; added a query-state-preservation case.
- Unity compilation passed. The first two full builds stopped at unused imports
  left by caller rewiring; those imports were removed before the successful run.
- Verified the bombardment extraction with `./build.sh all`: **5,110 / 5,110 tests
  passed**, zero failures/skips. Formatting, lint, analyzer/architecture checks
  and coverage gates passed (**81.5%** line / **91.3%** method).
  Log: `/tmp/reb2-runtime-map.63ZpfP/bombardment-build-3.log`; test run finished at
  `2026-09-21 05:47:41Z`. Every pre-move bombardment test method remains in the
  matching command or query fixture. This is regression evidence, not proof of
  matched simulation equivalence or completion of the overall migration.

- Extracted space-combat encounter/withdrawal queries into `SpaceCombatQueries`
  and retained detection transitions, pending-decision ownership, resolution,
  retreats, and damage application in `SpaceCombatCommands`. The pending decision
  and `SpaceCombatDecision` shape remain unchanged; presentation is rebuilt from
  live state on every query rather than cached in another state holder.
- Compared all **51 non-constructor method bodies**: unchanged after qualifying
  moved methods and normalizing whitespace. Moved the tactical auto-resolver to
  `Simulation/Combat`; its whole file matches after the namespace replacement.
  Resolver iteration/damage/withdrawal algorithms and result payloads are untouched.
- Ran **223 space-combat/resolver/tick/manager/runtime cases before extraction**,
  including six new characterizations for missing-pending rejection, unrelated
  retreat rejection, failure retaining the pending decision after combat-flag
  cleanup, the current manual-resolution placeholder, and refreshing pending
  retreat availability/tick information from live state without moving fleets.
- Preserved command return-list delivery and the tick processor's existing
  combat suspension/resumption/message buffering. The constructor still validates
  game and movement and captures the game RNG in the resolver before accepting
  its new query dependency. Query construction adds no early validation boundary.
- Rehomed/documented the command and resolver fixtures and the shared combat
  test base. Renamed the two obsolete `EvacuateOfficers` test prefixes to `Resolve`,
  the existing entry point they exercise. Added four direct withdrawal-query
  cases covering nonmutation, absent destinations, gravity wells, and stranded
  planetary fighters. Removed the now-empty former resolver folders and metadata.
- Unity compilation passed after correcting the moved command test's namespace
  import. All 74 pre-move command test bodies match after type-name/whitespace
  normalization; their assertions were not removed or rewritten.
- Verified the space-combat extraction with `./build.sh all`: **5,120 / 5,120
  tests passed**, zero failures/skips. Formatting, lint, analyzer/architecture
  checks and coverage gates passed (**81.5%** line / **91.3%** method).
  Log: `/tmp/reb2-runtime-map.63ZpfP/space-combat-build-2.log`; test run finished at
  `2026-09-21 05:58:38Z`. The full run was repeated after correcting the second
  obsolete test prefix. No matched simulation equivalence or overall completion
  is claimed.

- Extracted fog-of-war snapshot operations into `FogOfWarCommands`, visibility
  and faction-view projection into `FogOfWarQueries`, and the two result callbacks
  into `FogOfWarObserver`. The existing `FogOfWarRecorder` continues to own its
  snapshot algorithms. No duplicate recorder implementation or retained
  catch-all fog coordinator was introduced.
- Compared all **25 non-constructor method bodies**: unchanged after qualifying
  moved calls and delegating intelligence recording to the command method.
  Query construction adds no early game-null validation. Existing movement
  constructor guards retain their order and parameter names before validation
  of the newly supplied query dependency.
- Preserved intelligence recording in the reaction phase and sabotage snapshot
  invalidation in the settled-observer phase, at their existing subscription
  positions. Captivity, arrival and ownership changes still record observations
  directly at their existing points. UI and AI view consumers receive queries;
  the session shares its query instance with movement and planetary control.
- Ran **408 cases before extraction**, including four new characterizations of
  recipient/result-tick isolation, partial-batch failure without rollback,
  sabotage invalidating only the actor's knowledge, and friendly views retaining
  live unit identity without reparenting or recording intelligence. The first
  characterization run exposed a test-arrangement mistake: a faction's own
  buildings are not stored as enemy intelligence. The isolation test was corrected
  to use another observing faction; production behavior was not changed.
- Rehomed all **106 fog-related cases** into matching command, query, observer,
  recorder and snapshot fixtures, with shared scene setup under test helpers.
  All 106 test bodies match after dependency rebinding; no assertions were
  removed. Documented the moved test methods and retained Unity metadata GUIDs.
- Unity compilation passed after correcting four read callers missed during the
  initial rebind. Lint identified two obsolete editor imports, which were removed.
- Verified the initial extraction with `./build.sh all`: **5,124 / 5,124 tests
  passed**, zero failures/skips; formatting, lint, analyzer/architecture checks
  and coverage gates passed (**81.5%** line / **91.3%** method).
  Log: `/tmp/reb2-runtime-map.63ZpfP/fog-build.log`; test run finished at
  `2026-09-21 06:13:04Z`. Repeated the full run after unused test-import cleanup:
  **5,124 / 5,124 passed**, zero failures/skips, with the same lint and coverage
  gates green. Final log: `/tmp/reb2-runtime-map.63ZpfP/fog-build-2.log`; test run
  finished at `2026-09-21 06:15:02Z`. No matched simulation equivalence is claimed.

- Extracted movement orders, arrivals, evacuation and pending-result ownership into
  `MovementCommands`, read-only eligibility/destination/travel rules into
  `MovementQueries`, and blockade reaction selection into `MovementObserver`.
  Compared all **101 non-constructor method bodies** after qualifying moved calls;
  their logic is unchanged. Headquarters registers its completed-building policy
  on the same query instance used by movement commands and other session consumers.
- Ran **361 cases before extraction**, including four new characterizations of
  single-unit deferred publication, selection immediate publication without
  duplicate queuing, accepted movement surviving a listener exception, and
  wrong-faction rejection without publication. Retained all **142 movement test
  bodies** after dependency rebinding in matching command/query/observer fixtures.
- Rebound construction estimates, move-confirmation/waypoint eligibility, mission
  returns, combat evacuation queries, headquarters and all movement callers.
  Kept blockade subscription order, command exception boundaries and the existing
  pending-result versus immediate-result paths. No result payload was changed.
- Unity compilation passed. The first full build stopped at 13 obsolete imports;
  removed those imports and reran `./build.sh all`: **5,128 / 5,128 passed**, zero
  failures/skips. Formatting, lint, analyzer/architecture checks and coverage gates
  passed (**81.5%** line / **91.3%** method). Final log:
  `/tmp/reb2-runtime-map.63ZpfP/movement-build-2.log`; test run ended
  `2026-09-21 06:32:51Z`. This is not matched simulation-equivalence evidence.

- Extracted durable message delivery/expiration into `MessageCommands` and
  automatic result-batch selection into `MessageObserver`. The session constructs
  one shared `MessageFactory` at the existing initialization point. Automatic
  message provenance filtering, batch materialization, authored template
  resolution before persistence, message identity and retention rules are unchanged.
- Compared all **six non-constructor method bodies** and all **nine message test
  bodies** after dependency rebinding. Ran **252 message/factory/event/manager/tick/
  runtime cases before extraction**, including four new characterizations of
  authored-result filtering, null authored input, template failure before delivery,
  and delivery provenance/current tick. Moved tests to matching runtime fixtures.
- Kept the existing combat message-release boundary and follow-up loop; this
  extraction does not subscribe message delivery to every bus wave or change its
  notification timing. Request removal and final pipeline ownership remain pending.
- Unity compilation passed. Removed the obsolete GameManager import identified
  by the first lint run, then verified with `./build.sh all`: **5,132 / 5,132
  passed**, zero failures/skips; formatting, lint, analyzer/architecture checks and
  coverage gates passed (**81.6%** line / **91.3%** method). Log:
  `/tmp/reb2-runtime-map.63ZpfP/messages-build-2.log`; tests ended
  `2026-09-21 06:39:14Z`. No matched simulation equivalence is claimed.

- Moved authored scheduling, validation, trigger matching and activation ownership
  from `GameEventSystem` into `GameEventExecutor`. Moved `GameEvent`'s activation
  predicate, condition loop and action-context construction into private executor
  methods; its serialized fields, attributes and constructors are unchanged.
  All **15 scheduler/constructor bodies** and the **three moved definition
  methods** match after qualifying their new inputs/calls. The bus subscription
  remains first, and tick scheduling remains at its existing position.
- Ran **152 event/request/manager/session/tick cases before extraction**, including
  three new characterizations: deferred templates observe later action mutations;
  a missing dispatcher leaves those mutations without recording activation;
  a failed deferred request does not stop the following request or activation history.
  No catch, deferred dispatch, activation increment or RNG boundary was moved.
- Rehomed and documented the executor fixture. Existing test bodies match after
  type rebinding except the nested-action test, which now exercises `ProcessEvents`
  instead of a removed internal definition method; its assertion is unchanged.
  The two activation-limit tests now exercise the executor's public processing
  contract. Serialization tests remain with `GameEvent`. Removed the unused
  `IGameResultHandler` interface and its Unity metadata; subscription callbacks
  already use the bus directly.
- Verified with Unity compilation and `./build.sh all`: **5,135 / 5,135 passed**,
  zero failures/skips. Formatting, lint, all **25 analyzer / 8 architecture tests**
  and coverage gates passed (**81.6%** line / **91.3%** method). Log:
  `/tmp/reb2-runtime-map.63ZpfP/events-build.log`; tests ended
  `2026-09-21 06:44:44Z`. Result payloads, serializer implementation, scenes and
  prefabs remain untouched. This is still not matched simulation-equivalence evidence.

- Moved individual authored action execution into `GameEventExecutor.Actions.cs`
  and its activation context into `Simulation/GameActionContext.cs`. `GameAction`
  and its action subclasses now contain authored data, not execution methods.
  Existing numeric, targeting, media and result-construction helpers moved with
  the interpreter; no separate handler class or command payload was added per action.
  Shared planet-value adjustment still uses the same calculation, with the two
  concrete field accessors supplied by the interpreter rather than the definitions.
- Compared all **34 extracted action/helper method bodies** after rebinding and
  the moved spawn body. All **148 authored properties across 35 retained types**
  are unchanged. Removed only the internal computed planet-category properties;
  their category values remain in the interpreter. Result payloads and serializer
  implementation remain untouched. Roll definitions and the spawn selector's
  rejection method still await the binding/selector execution move.
- Replaced four fabricated manager-test actions with real operations: authored
  injury, immediate assault, tick-produced victory and mission sabotage. The
  assault test now exercises the command's actual notification path rather than
  an impossible authored assault result. Ran **285 cases before extraction** with
  these arrangements, then the same **285 cases after extraction**, all passing.
  Rehomed **62 action-execution cases** with identical bodies after renaming into
  `GameEventExecutorTests`, plus two batch-execution cases. Serialization cases
  remain with their definitions; shared test invocation helpers moved under Helpers.
- Preserved action-local result visibility, nested log-and-continue behavior and
  the later deferred-request phase. Added regressions for a null action continuing
  to the following action and a failed later target retaining an earlier mutation.
  No rollback, retry, publication-timing change or new failure policy was introduced.
- After removing an obsolete import found by lint, `./build.sh all` passed
  **5,135 / 5,135** cases. Repeated the full build with the two additional failure
  regressions: **5,137 / 5,137 passed**, zero failures/skips; formatting, lint,
  analyzer/architecture checks and coverage gates passed (**81.6%** line / **91.3%**
  method). Final log: `/tmp/reb2-runtime-map.63ZpfP/actions-build-3.log`; tests ended
  `2026-09-21 07:30:18Z`. This is not matched simulation-equivalence evidence.

- Moved binding-source evaluation and integer/double rolls into
  `GameEventExecutor.Bindings.cs`. Binding and roll definitions retain all authored
  properties; their execution no longer lives in Game. Compared all eight moved
  method bodies after call qualification and all 15 binding properties.
- Added pre-extraction regressions for binding evaluation before a future activation,
  duplicate aliases consuming a roll before rejection, and binding exceptions
  preventing subsequent event execution. The same **200 cases passed before and
  after extraction**. Existing binding and roll tests moved with their implementation;
  serialization cases stayed with the definitions.
- `./build.sh all` passed **5,140 / 5,140**, zero failures/skips, after removing
  unused parameters from the three typed sources that do not use randomness.
  Formatting, lint, analyzer/architecture checks and coverage gates passed
  (**81.6%** line / **91.3%** method). Log:
  `/tmp/reb2-runtime-map.63ZpfP/bindings-build-2.log`; tests ended
  `2026-09-21 07:50:30Z`. This is not matched simulation-equivalence evidence.

- Moved condition evaluation and its context into
  `GameEventExecutor.Conditions.cs` and `Simulation/GameConditionContext.cs`.
  Serialized condition definitions, attributes, fields and all **37 properties**
  remain unchanged. Compared **27 extracted method bodies** after call qualification
  and the two moved helper classes. The three simple officer-state predicates
  retain their original expressions in the interpreter's type dispatch.
- Replaced the last fabricated trigger condition with real bindings and comparisons.
  Added pre-extraction coverage for All/Any/Not short-circuiting and XOR evaluating
  all operands. The same **213 cases passed before and after extraction**.
  Rehomed **26 condition test methods**, preserving their bodies after rebinding;
  kept distinct scene fixtures rather than substituting different test identities.
- Kept condition randomness on `game.Random`, existing deferred enumeration,
  event-state access, typed scalar comparisons and exception propagation. No
  result payload, serializer implementation or publication boundary was changed.
- After correcting an identifier typo caught by compilation, `./build.sh all`
  passed **5,144 / 5,144**, zero failures/skips. Formatting, lint, analyzer/
  architecture checks and coverage gates passed (**81.6%** line / **91.4%** method).
  Log: `/tmp/reb2-runtime-map.63ZpfP/conditions-build.log`; tests ended
  `2026-09-21 08:21:06Z`. No matched simulation equivalence is claimed.

- Moved selector execution and its shared ancestor resolver into
  `GameEventExecutor.Selectors.cs`. All **24 moved method bodies** match after
  rebinding, and all **29 selector properties** are unchanged. `SpawnUnits` retains
  its rejection outside placement; its authored fields are unchanged.
- Added pre-extraction regressions for lazy filtering, first-match short-circuiting,
  eager random draws in sorted distinct-candidate order, and spawn-source rejection
  before randomness. The same **227 targeted cases passed before and after**.
  Rehomed **14 selector test methods** with identical bodies after rebinding.
- Preserved destination selection's full-candidate path and binding cardinality
  checks. No new eager materialization or random sampling was introduced.
- `./build.sh all` passed **5,148 / 5,148**, zero failures/skips; formatting, lint,
  analyzer/architecture checks and coverage gates passed (**81.6%** line / **91.4%**
  method). Log: `/tmp/reb2-runtime-map.63ZpfP/selectors-build.log`; tests ended
  `2026-09-21 08:27:54Z`. No matched simulation equivalence is claimed.

- Moved all 23 trigger matchers, trigger type selection and binding execution into
  `GameEventExecutor.Triggers.cs`. Compared all **28 moved method bodies**, both
  argument-accessor helper classes and all **86 retained authored properties**.
  Existing argument names, type mappings and result accessors are unchanged.
- Moved `GameEventEvaluationContext` and its tests to Simulation with their Unity
  identities preserved. Its constructor still applies trigger bindings at the same
  point. Added baseline regressions for retaining an earlier binding after a later
  invalid argument or duplicate alias. The same **247 targeted cases passed before
  and after extraction**; **11 trigger test methods** retained their bodies after
  rebinding, and the constructor-binding test moved to the context fixture.
- Removed two obsolete imports reported by lint after the namespace move.
  `./build.sh all` passed **5,150 / 5,150**, zero failures/skips; formatting, lint,
  analyzer/architecture checks and coverage gates passed (**81.6%** line / **91.6%**
  method). Log: `/tmp/reb2-runtime-map.63ZpfP/triggers-build-2.log`; tests ended
  `2026-09-21 08:34:24Z`. No matched simulation equivalence is claimed.

- Moved schedule range calculation, recurrence detection and terminal-condition
  selection into the executor. Its four range methods retain their bodies after
  qualification; all **21 authored schedule properties** match the baseline.
  Removed only the ignored computed properties from the serialized definition.
- Replaced four direct range-helper tests with six `ProcessEvents` cases covering
  absolute timing, initial fixed delay and both inclusive random-range endpoints
  for first and repeated activations. These passed before extraction alongside the
  old helper tests (**253 targeted cases**). After removing the redundant helper
  tests, **249 targeted cases passed**. Serialization tests remain with the data.
- Compilation and property comparison caught two authored `Until` collections
  accidentally removed during extraction; restored both unchanged before rerunning
  validation. `./build.sh all` then passed **5,152 / 5,152**, zero failures/skips;
  formatting, lint, analyzer/architecture checks and coverage gates passed
  (**81.6%** line / **91.6%** method). Log:
  `/tmp/reb2-runtime-map.63ZpfP/schedules-build.log`; tests ended
  `2026-09-21 08:39:42Z`. No matched simulation equivalence is claimed.

- Characterized the remaining deferred execution boundary before changing its
  command APIs. Four new executor cases cover a later capture invalidating an
  earlier queued duel without drawing randomness, ownership retaining a selection
  made before a later disable action, local facts preceding deferred message facts,
  and placement completing before a following transit order chooses its departure.
  The targeted baseline passed **366 / 366** cases. The initial test compilation
  caught three array arguments where `ProcessEvents` requires a list; corrected
  those arrangements before recording the passing baseline.
- Extracted ordinary `DuelCommands.Resolve`,
  `PlanetaryControlCommands.ChangeOwnership`, and
  `MovementCommands.TryPlaceUnits` operations from their request adapters; exposed
  the existing grouped `TryRequestMove` operation used by the adapter. These own
  the existing implementation rather than forwarding to retained systems. The
  adapters remain temporarily so deferred execution, failure handling, and result
  release stay at the existing boundary until executor wiring is replaced.
- Compared **91 retained method bodies**, allowing only the intentional duel
  parameter rebinding. Also compared the assembled duel resolution, including its
  inlined outcome construction, and the extracted ownership loops. The comparisons
  match; capture/injury rolls, result order, and current-tick use remain unchanged.
  Moved five movement tests and five duel tests onto the ordinary command API while
  retaining the dispatcher-level executor coverage. Added two null-opponent cases
  and three ownership cases covering index updates, unchanged ownership, and the
  existing partial-mutation behavior when a later target throws.
- `./build.sh all` passed **5,161 / 5,161**, zero failures/skips; formatting, lint,
  analyzer/architecture tests, and coverage gates passed (**81.6%** line / **91.6%**
  method). Log: `/tmp/reb2-runtime-map.63ZpfP/request-commands-build.log`.
  No result payloads, serializer implementation, scenes, or prefabs changed.
  This remains an intermediate step: the request adapters and shared message
  request data are not yet removed, and no simulation equivalence is claimed.

- Removed `GameRequestDispatcher`, `IGameRequestHandler`, the request base class,
  and the movement, placement, ownership, and duel request payloads. Removed their
  obsolete Unity metadata and the now-empty Systems directories. `GameSession`
  supplies the four concrete command dependencies directly to `GameEventExecutor`.
  The executor retains resolved inputs in deferred calls rather than dispatching
  instruction objects. No additional command framework or per-action handler was
  introduced.
- Preserved the complete-action-list boundary, resolved target snapshots, original
  command order, per-operation log-and-continue handling, source-event stamping,
  null-result filtering, and activation bookkeeping. Missing all execution
  dependencies still throws after local actions but before activation is recorded;
  a missing individual command logs `InvalidOperationException` and allows later
  operations to execute. Diagnostics now name the authored deferred action rather
  than a deleted request type. Deferred results do not become action-local facts.
- Reworked **15 tests** to exercise actual event processing, delivered messages,
  movement, placement, ownership, or duel outcomes instead of inspecting transient
  request payloads. Ran these arrangements before removing the dispatcher:
  **236 / 236** targeted cases passed. After removing the obsolete duel null-batch
  API case, **235 / 235** passed on the direct-call path. Dispatcher source stamping
  and continuation are covered by executor cases; replaced its fake-handler tests
  with real missing-command and partially failing duel scenarios. The latter keeps
  the captured state, withholds the failed operation's partial facts, and delivers
  the following message.
- All four command files match their preceding implementations after removing
  request adapters and renaming authored message delivery. All **46 unaffected
  action/helper method bodies** match. The five deferred actions retain their
  selection, validation, media evaluation, and resolved-input timing.
- `./build.sh all` passed twice. Final run: **5,159 / 5,159**, zero failures/skips;
  formatting, lint, **25 analyzer / 8 architecture tests**, and coverage gates passed
  (**81.6%** line / **91.6%** method). Log:
  `/tmp/reb2-runtime-map.63ZpfP/deferred-build-2.log`; tests ended
  `2026-09-21 09:06:42Z`. The count includes removal of the three obsolete dispatcher
  tests and one null-batch test, plus two new executor failure cases. Result payloads,
  serializer implementation, scenes, and prefabs remain untouched.
- `MessageDeliveryRequest` remains temporarily as the shared message builder data,
  no longer a routed request. Its former inherited tick and source fields remain
  on the type. `MessageCommands.DeliverAuthored` retains the existing eager template
  resolution and delivery behavior. This does **not** finish message-data ownership
  or request cleanup; no renamed replacement DTO was introduced to hide that work.

- Traced runtime message construction to its three production consumers:
  `GameSession`, `MessageObserver`, and `MessageCommands`, all in Simulation.
  Moved `MessageFactory` and `MessageTemplateBuilder` there with their Unity GUIDs
  preserved. Serialized `Message`, `MessageDefinition`, advisor definitions, and
  combat report data remain in Game. There are no remaining Game callers of the
  moved builders, so this establishes ownership without a Game-to-Simulation
  dependency or a renamed replacement request DTO.
- Added pre-move regressions for automatic batch template failure withholding
  earlier messages and authored batch failure leaving a supplied combat report
  untouched. Corrected an invalid manufacturing enum name caught by the initial
  test compilation; the passing baseline and post-move run each passed **335 / 335**
  targeted cases. These are preservation constraints for the remaining delivery
  data change, not permission to create or mutate reports at an earlier boundary.
- Rehomed `MessageFactoryTests`, documented its 95 test methods under the current
  repository instructions, and corrected the authored-message test's member prefix.
  Compared all **145 factory / 3 template-builder / 110 test-and-helper bodies**;
  every body matches. Tests and production files retain their Unity identities.
- Lint identified two obsolete imports in the builder consumers; removed them.
  `./build.sh all` then passed **5,161 / 5,161**, zero failures/skips; formatting,
  lint, analyzer/architecture tests, and coverage gates passed (**81.6%** line /
  **91.6%** method). Log: `/tmp/reb2-runtime-map.63ZpfP/message-builders-build-2.log`;
  tests ended `2026-09-21 09:16:48Z`. Protected result payloads, serializer code,
  scenes, and prefabs remain unchanged. No matched simulation equivalence is claimed.

- Moved final message materialization into the existing `MessageFactory` and
  removed the copy of those statements from its test helper. `MessageCommands`
  still invokes materialization only during delivery, after batch template
  preparation; it then assigns the current tick and attaches the message in the
  original order. No new class, request replacement, earlier report mutation, or
  serialization change was introduced. All materialization statements match the
  prior implementation; only the timestamp assignment remains in the command.
- Added five factory cases covering resolved presentation, no attachment during
  construction, supplied-report identity/combat data, unchanged timestamps, and
  clearing stale presentation. Added a command case showing that a failed first
  attachment leaves a later supplied report untouched. This extends the existing
  template-failure boundary coverage and constrains the remaining request-data
  migration; eager materialization of the full delivery batch is not equivalent.
- The targeted run passed **303 / 303** cases before the attachment-failure case
  was added. `./build.sh all` subsequently passed **5,167 / 5,167**, zero failures
  or skips, with formatting, lint, analyzer/architecture tests, and coverage gates
  passing (**81.6%** line / **91.6%** method). Log:
  `/tmp/reb2-runtime-map.63ZpfP/message-construction-build.log`; tests ended
  `2026-09-21 09:27:34Z`. Result payloads, serializer implementation, scenes, and
  prefabs remain unchanged. No simulation-equivalence claim is made.

- Removed the last `MessageDeliveryRequest`, its Unity metadata, and the empty
  `Game/Requests` directory. No production or test references remain to that type,
  the request namespace, `GameRequestDispatcher`, or `IGameRequestHandler`.
  Authored events now snapshot the existing `MessageDefinition` data and selected
  references, then call `MessageCommands.DeliverAuthored` at the same deferred
  boundary. Template values still read live display names at that boundary;
  media/voice selection remains in action interpretation.
- Separated resolved factory output from authored input. `MessageDelivery`, next
  to the factory in Simulation, holds actual message content, the recipient,
  advisor metadata, provenance, and an optional existing report. It does not
  duplicate the old text/media/navigation fields or retain unresolved subjects,
  locations, or a request tick. Preparing content does not consume an instance
  identifier. `CreateMessage` applies that content to an existing report only
  during delivery, preserving report identity and the previously characterized
  mutation boundary.
- Moved the eager batch-preparation boundary into `MessageCommands.Deliver`.
  Factory output may be enumerated lazily, but the entire batch is prepared before
  the first attachment. Reworked the existing batch-failure tests around that
  public command; their observable requirements remain unchanged. The authored
  command takes ordinary definition/context arguments and uses the same delivery
  path as automatic messages, without a second instruction DTO or dispatcher.
- Compared **142 retained factory method bodies** after explicit delivery-field
  rebinding; all match. The first targeted run found three tests inspecting the
  former report field; changed those assertions to inspect the materialized combat
  reports. The subsequent targeted run passed **306 / 306**. Added coverage for
  prepared-message reuse and unchanged deterministic identifier allocation.
- `./build.sh all` passed **5,169 / 5,169**, zero failures/skips; formatting, lint,
  analyzer/architecture tests, and coverage gates passed (**81.6%** line / **91.6%**
  method). Log: `/tmp/reb2-runtime-map.63ZpfP/message-request-removal-build.log`;
  tests ended `2026-09-21 09:42:46Z`. Protected result payloads, serializer code,
  scenes, and prefabs remain unchanged. This is not matched simulation evidence.

- Added four manager characterization cases for an immediate assault capturing a
  fixed headquarters. The confirmed callback sequence is general settled results,
  assault summaries, victory summaries, message delivery, headquarters loss, then
  victory declaration. A settled-result callback failure prevents message delivery;
  a message callback failure leaves already attached messages intact and prevents
  the later headquarters/victory callbacks. No production routing changed.
- The first fixture omitted the required success/owned selectors on its assault
  message definition, so no messages were eligible. Corrected the test data, not
  the implementation. The subsequent manager run passed **41 / 41** cases; log:
  `/tmp/reb2-runtime-map.63ZpfP/result-boundaries-tests-2.log`.
- Full `./build.sh all` passed **5,173 / 5,173**, zero failures/skips, including
  formatting, lint, analyzer/architecture tests, and coverage gates (**81.6%** line /
  **91.6%** method). Log: `/tmp/reb2-runtime-map.63ZpfP/result-boundaries-build.log`;
  tests ended `2026-09-21 09:52:08Z`. No simulation-equivalence claim is made.

### Result-pipeline decision (superseded by the completion below)

The remaining manager result flow interleaves bus reaction draining, presentation
callbacks, and message generation. Moving those callbacks directly into ordinary
bus subscriptions would change their existing order. Requested approval for one
`Simulation/GameResultPipeline` owning the existing ordered flow, keeping message
and presentation rules out of the bus. This class is **not yet approved or added**.
The alternative presented is retaining that flow in GameManager for now.

Still outstanding: complete session/application ownership and scene preparation;
complete command publication/message/presentation pipeline ownership and consumer
rebinding; complete regression and matched simulation comparison. This intermediate
state is not the finished architecture.

### Matched simulation verification checkpoints

- Created an isolated baseline worktree at commit
  `c7fc30113e1255c8fbef1eae9dc6175b40b1604d`, using the same content as the current
  worktree. Temporary verification files and outputs live under
  `/tmp/reb2-parity.KYiBS6`; the identical temporary editor harness in each project
  must be removed after verification. It is not a runtime feature.
- Before scheduling either simulation, Unity recorded the loaded game, editor,
  and test assembly paths, timestamps, hashes, and module identities. Each loaded
  assembly was newer than its relevant C# sources, and compilation/import were
  inactive. Each simulation repeats that check before loading the shared save.
- Generated one large, easy, all-AI game with seed 173, serialized it, and loaded
  that exact input in both versions. Both runs use the same runtime entity-ID seed,
  restored game RNG position, incremental tick path, and per-tick serialization.
  No normal user save is written or overwritten.
- The initial raw comparison differed at tick 1. Stopped both long runs and
  reproduced one tick in each version: the complete XML diff contained only
  `MovementGroupID` values. Both implementations generate these through
  `Guid.NewGuid()`, independently of the seeded game RNG. No gameplay fix was
  warranted by that evidence.
- Adjusted only the temporary comparator to assign movement-group IDs unique,
  stable labels by first occurrence across the run. Group membership and identity
  relationships remain compared; no other XML values are normalized. Raw hashes
  and raw final XML are retained alongside the comparable form.
- Restarted after fresh Unity compilation checks. At the checkpoint through tick
  75, all 76 state/RNG records and all 364 callback/provenance trace records match.
  The two 2,000-tick runs are **still running**; this is not a completed comparison.
  Trace records cover callback categories, result types, ticks, and provenance,
  not every transient result field. Durable state is compared in full.
- A later checkpoint matches through tick 625. Both baseline and refactored runs
  logged the same `HAN_BOUNTY_HUNTERS` / `SetNodeStateAction` exception for Han
  being on active mission `02da80ac9047eed6b1591a9a03b4357a`; both continued.
  This exercises existing authored-action failure containment. It is an existing
  behavior observed in the baseline, not a refactor fix or a clean-log claim.
- The error-level log comparison also found the same earlier diagnostic in both
  versions: captured Leia had no valid custody destination for FNEMP1. The matching
  code paths are baseline `CaptiveSystem.HandleResults` and the extracted
  `CaptiveCommands.EstablishCustody`. Through tick 825 the normalized durable state
  and trace prefix still match, and both error-message sequences are identical
  after removing wall-clock timestamps. Neither gameplay issue was changed.
- At the halfway checkpoint, all 1,001 state/RNG records through tick 1,000 and
  the shared 8,491 callback/provenance trace records match. Both processes remain
  live; do not treat this checkpoint as the final comparison.
- Production gameplay and result routing were not changed during this check.
  The result-pipeline ownership decision above remains pending.

### Remaining application-lifetime preservation checks

Fresh inspection of `App/GameRuntime.cs`, `Managers/GameManager.cs`,
`Simulation/GameSession.cs`, and their corresponding fixtures confirms two
different replacement boundaries that must not be consolidated accidentally:

- `GameRuntime.ReplaceSession` calls `EndGame` **before** validating new content.
  An invalid new-game content identity therefore does not preserve the old active
  session. No constructor-first or rollback behavior is authorized here.
- `GameRuntime.HotReloadGame` deserializes and validates content **before** calling
  `GameManager.ReplaceGame` on the existing manager. Successful hot loading keeps
  that manager identity and its presentation subscriptions. Invalid content must
  not dispose or replace that active manager.
- `GameManager.InitializeGame` assigns/configures/rebuilds the graph before
  resetting tick scheduling and replacing runtime components. Its invalid-event
  cases already cover partial component replacement and withheld presentation
  notification; the suspended-iterator case protects the existing tick guard.
- `GameRuntimeTests` currently covers quick-load state, pending-combat save
  gating, and content validation, but lacks direct cases distinguishing the two
  application replacement failure boundaries above. Add those preservation cases
  before moving application/session ownership. This is a test gap, not evidence
  of a production defect or authorization to change the failure behavior.

During the matched runs, formatting passed. The additional lint run passed game
compilation and all eight architecture cases, then stopped at an explicit-type
style violation in the temporary editor harness. Remove that temporary harness
after verification and rerun the complete checks; do not call the lint run passing.

Five additional application-lifetime test cases are drafted outside the watched
Unity projects in `/tmp/reb2-parity.KYiBS6/runtime-lifetime-tests.insert`. They are
not applied or tested yet. Apply them to the corresponding GameRuntime fixture
after the active simulations finish, then verify against the baseline and current
runtime before performing the remaining ownership migration.

### Completing session and application ownership

The follow-up request to finish authorized the remaining pipeline and ownership
work described above. `GameResultPipeline` now owns the existing ordered flow;
it is not a second gameplay bus. `GameResultBus` still owns queued reactions.

- `GameSession` owns graph preparation, component composition, subscriptions,
  the stable tick processor and the stable result pipeline. Component getters
  still resolve the current components after in-place replacement.
- `GameManager` owns speed and elapsed-time accumulation only. Its old command,
  query, result, tick, replacement and state-forwarding APIs are removed.
- `GameRuntime` owns the active session and clock. Hot loading still validates
  content, replaces components in place, resets the clock, notifies presentation,
  then reconciles loaded combat, in that order. New-game replacement still ends
  the previous game before content validation.
- The strategy screen is a composition boundary. It supplies feature controllers
  their actual command/query/state dependencies. The advisor receives a faction
  provider, and galaxy-map projection receives the faction and fog-of-war queries;
  neither depends on GameManager or GameSession.
- Headless execution drives the session's same tick processor. Existing result
  presentation callbacks now come from its pipeline. No gameplay rules moved
  into the clock, application lifecycle, or bus.
- The combat clock reset remains after deferred messages and remaining tick
  phases finish, before clearing the tick's busy guard. A failed completion
  callback still prevents that reset.
- Result payloads, serialized state and gameplay algorithms are unchanged by this
  ownership step. Existing partial-initialization and exception behavior remain
  regression constraints, not newly introduced recovery policies.

Verification completed before this last ownership step:

- Both matched 2,000-tick processes exited successfully. All 2,001 durable-state /
  RNG records, 12,349 ordered callback records and final canonical XML matched.
  The normalization and pre-existing error limitations above still apply.
- Five application-lifetime characterizations passed on the baseline: all 15
  GameRuntime cases passed in `/tmp/reb2-parity.KYiBS6/baseline-lifetime-tests.xml`.
- The initial pipeline/session/tick extraction passed all 5,184 tests. The final
  removal of manager forwarding APIs requires the subsequent full build and
  replay; the earlier comparison is not evidence for edits made after that run.

### Final verification

- Final matched replay: both baseline and final session-based runtime completed
  2,000 ticks with exit code zero, using the same serialized input and content.
  All 2,001 tick/RNG records, all 16,105 ordered callback/provenance records and
  the final serialized state matched. Only random movement-group labels were
  canonicalized, retaining their one-to-one identity and membership relationships.
- This replay serialized at the beginning and end, unlike the earlier every-tick
  snapshot run. It was compared with a fresh baseline using the **same** schedule.
  This matters: `BaseGameEntity.InstanceID` lazily allocates identifiers, and the
  serializer invokes its getter. Comparing different snapshot schedules changes
  ID allocation and is not a valid refactor comparison. Neither that getter nor
  the serializer was modified.
- Unity compilation/import readiness and actual loaded assembly freshness were
  verified before launching each replay and again inside each run. Evidence:
  `/tmp/reb2-parity.KYiBS6/final-ready.txt`, `baseline-fast-ready.txt`, and each
  run's `.assemblies` file. The final game assembly MVID was
  `a58ce0b7-1f1d-4c34-b49d-1ad35a4a31be`.
- Final replay evidence: `final-173.*`, `baseline-fast-173.*` and
  `compare-final.js` in `/tmp/reb2-parity.KYiBS6`. The two runs also logged the
  same existing Han event failure and missing-custody-destination diagnostic;
  the comparison does not claim those gameplay errors were fixed.
- Removed the temporary editor harness and its metadata from the repository.
  Kept a copy with the verification evidence, outside the Unity project.
- Clean-tree `./build.sh` passed formatting, lint, all 25 analyzer tests, all
  eight architecture cases, and **5,190 / 5,190** game tests with zero failures
  or skips. Coverage remained **81.6% line / 91.6% method**. Log:
  `/tmp/reb2-parity.KYiBS6/clean-build.log`; tests ended
  `2026-09-21 11:47:46Z`. The explicit CSharpier check also passed.
- Result payloads, serializer code, scenes and prefabs have no diff. No commit,
  push or pull request was performed. The pre-existing `Assets/Mods.meta` was
  left untouched.

The agreed structural migration is complete. Earlier pending checkpoints above
are historical. Behavioral changes explicitly deferred in ArchitectureDecisions,
including capture ordering, return contracts and failure recovery, remain deferred.
