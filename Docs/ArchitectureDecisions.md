# Runtime architecture decisions

These notes record agreed decisions, not an implemented architecture. Earlier
replacement class inventories are not approved designs.

## Result publication and observation

Agreed on September 20, 2026.

- Use `GameResultBus` as the shared publication and subscription point for game
  results within one running game. It is not a global static instance.
- Reuse `GameResult` and its concrete result types as notification payloads.
  Player-facing messages and advisor notifications are distinct concepts.
- Publishers announce completed changes. Observers subscribe to the result types
  they need. Publishers do not reference their observers directly.
- Publishing and observing are roles. One object may perform either role or both;
  do not create a mandatory publisher/observer class pair for each gameplay area.
- Register observer methods through typed callbacks. The currently agreed API
  does not require a new observer interface or a separate class per listener.
- Return a disposable subscription handle so listeners can detach when their
  window or game session ends.
- Consolidate existing result-delivery machinery rather than introduce a second
  parallel delivery mechanism. The bus contains no gameplay rules.
- Required steps of an operation must finish before its completed result is
  announced. Subscriber registration order must not be responsible for completing
  that operation.
- Do not use result publication to answer synchronous questions or disguise an
  instruction as an announcement of something that already happened.

This decision does not approve retaining the existing catch-all gameplay
coordinators, renaming them, or wrapping them.

## Grouped command methods and read-only queries

Agreed on September 20, 2026.

- Use the existing gameplay areas as responsibility buckets: movement,
  manufacturing, missions, captivity, combat, and the other existing areas.
- Implement actions as ordinary methods grouped by gameplay area. Examples are
  `MovementCommands.TryMove`, `MissionCommands.TryStart`, and
  `ManufacturingCommands.TryCancel`.
- These classes own action implementation. They do not forward to retained
  catch-all coordinators.
- UI, AI, console, and the authored-event executor call the same command methods.
- Command methods validate authoritative state, perform the operation, and return
  acceptance directly. Accepting a movement order does not mean arrival has occurred.
- Publish completed changes through the shared `GameResultBus`. Callers do not
  route the returned acceptance through GameManager to finish the operation.
- Reject the proposed `GameCommands.Execute(GameCommand)` dispatcher and the
  mandatory payload class for every operation. Do not introduce a parallel
  command-object framework alongside ordinary command methods.
- Group rule-based questions into read-only query APIs where needed, such as
  `MissionQueries.GetMissionOdds`. Simple property reads remain direct reads.
- Queries respect the caller's permitted information. Command execution validates
  authoritative state regardless of an earlier query answer.
- Separate query, command, reaction, and tick responsibilities without requiring
  four classes for every gameplay bucket.
- Runtime query and command implementations belong under `Simulation`, not `Game`.
- Detailed rejection reporting and the complete class inventory remain to be decided.

The source-based [migration map](RuntimeMigration.md) and [method inventory](RuntimeMethods.md)
record the implementation responsibilities and compatibility gates. They are not
proof of completed migration or permission to change current behavior.

## Authored events and dependency direction

Agreed on September 20, 2026.

- Keep serialized action and condition definitions in `Game` as data.
- Move their execution and evaluation out of those definitions and into the
  runtime under `Simulation`.
- `GameEventExecutor` interprets the definitions, asks query APIs questions when
  rules must be evaluated, and calls command methods to perform operations.
- An authored action object does not call back into the runtime. Remove that
  responsibility from `GameAction.Execute` and `GameEvent.ExecuteActions`.
- Execution methods can be grouped; do not require a separate handler class for
  each authored action.
- Preserve the one-way package dependency: `Simulation` depends on `Game`;
  `Game` does not depend on `Simulation`.

## Cross-bucket reactions through the result bus

Agreed on September 20, 2026.

- Use subscriptions for consequences in other gameplay buckets. Do not introduce
  a separate coordinator merely because a change affects multiple areas.
- Capture establishes captivity and publishes the capture result. The mission
  listener reacts by cancelling the affected mission and producing its own results.
- Mission cancellation is a downstream reaction, not a prerequisite that the
  capture command must coordinate before publishing the capture result.
- Listeners must not depend on subscriber registration order to recover facts
  about what happened. Preserve the relevant historical information in the result,
  or maintain reliable membership information in the owning gameplay area.
- In particular, the current mission capture handler discovers the mission from
  the officer's current parent, but custody placement can change that parent.
  Replace that dependency with preserved mission-at-capture information or reliable
  mission membership tracking. The precise representation remains to be chosen.
- The bus supplies communication; it does not make mutable live references into
  historical snapshots or automatically remove ordering dependencies.

## Queued result delivery

Agreed on September 20, 2026.

- Finish notifying listeners of the current result before delivering results
  published by those listeners.
- Append follow-up results to the pending queue in publication order. Nested
  publication must not recursively interrupt the current notification.
- Drain the queue, including further results produced by reactions, in the same
  processing pass. Do not defer those reactions to the next tick or frame.
- This determines follow-up delivery order. Publication timing is specified below;
  new reaction-loop detection is out of scope for this refactor.

## Immediate gameplay reactions

Agreed on September 20, 2026.

- Once an operation has made its change and publishes the corresponding result,
  process the gameplay consequences immediately rather than waiting for the next
  tick or an end-of-tick flush.
- A top-level command drains its resulting gameplay reactions before returning.
  If publication occurs while the bus is already delivering, append to the active
  queue and drain within that same pass; do not recursively interrupt delivery.
- For example, scrapping the last defending troop must immediately cause the
  planet's control rules to be reevaluated. If those rules require neutrality,
  apply it in that processing pass. This does not change the rules determining
  whether a garrison is required.
- Player-facing message buffering during combat is separate from gameplay result
  delivery. Delaying messages must not defer the underlying gameplay reactions.

## Combat message timing

Agreed on September 20, 2026.

- Preserve the existing buffering of movement/combat player-facing messages until
  pending combat decisions are resolved and combat processing has settled.
- Continue processing gameplay results and their reactions immediately. Do not
  pause the gameplay result bus merely because player-facing messages are buffered.
- Preserve the existing release point across successive pending combat decisions;
  do not change message timing as part of the architectural refactor.

## Failure handling and refactor safety

Agreed on September 20, 2026.

- Preserve existing failure, exception propagation, recovery, and continuation
  behavior during the refactor. This supersedes the earlier proposed fault-stop
  policy; do not implement that policy as part of this work.
- The preservation instruction also takes precedence where earlier decisions
  would otherwise change timing or ordering. Keep existing deferred publication
  and capture-listener order until an explicitly reviewed, behavior-preserving
  replacement has been verified. Do not claim those dependencies are solved by
  renaming the delivery mechanism.
- Keep existing expected-rejection return behavior. Do not turn normal rejection
  into an exception or unexpected exceptions into normal rejection.
- Preserve existing catch boundaries: authored-action/request failures currently
  log and continue, while result-handler exceptions propagate. Do not unify these
  paths under a new blanket catch or fault policy.
- Do not introduce new fault states, forced reload requirements, automatic retries,
  rollback, reaction-count limits, or loop guards during this refactor.
- Preserve existing authored-action failure containment rather than silently
  removing or broadening it.
- That containment is intentional: commit `af2c0dad` added log-and-continue behavior
  to authored actions and requests. Existing tests explicitly require later work
  to execute after a failure, but do not establish safety after partial mutation.
- As existing request execution is replaced, account for that tested containment
  explicitly rather than allowing the architectural move to change it accidentally.
- Cover existing rejection, propagation, and continuation behavior with regression
  tests. Do not replace those expectations with the abandoned fault-stop proposal.
- Structural moves, synchronous publication, and new subscription wiring must not
  accidentally relocate exception handling. If preserving a boundary conflicts
  with the proposed structure, stop and explain before changing behavior.
- Preserve existing regression coverage and verify each migrated slice before
  proceeding. Architectural agreement is not proof of behavior preservation.

## Tick execution

Agreed on September 20, 2026.

- Use `Simulation/GameTickProcessor.cs` to own tick execution and its ordered
  schedule. Do not name this processing object `GameTick`, which suggests data
  representing a tick.
- Gameplay buckets own their recurring state changes alongside their command
  methods. For example, `ManufacturingCommands.AdvanceTick` progresses production
  and `MovementCommands.AdvanceTick` progresses travel.
- Do not introduce an additional updater class for every gameplay bucket.
- GameTickProcessor calls the recurring work in explicit order. Do not use a
  broadcast tick event whose subscriber registration determines gameplay order.
- Keep gameplay rules in the owning buckets, not in GameTickProcessor.
- Allow incremental AI work to yield between frames. Suspend for pending combat
  decisions and resume the same logical tick after resolution.
- Use one execution path driven by the application, tests, and headless simulations.
- Announce tick completion only after its required work and result reactions finish.
- Preserve existing phase order and combat/message timing during extraction.
  In particular, existing pending-combat tests require tick completion and further
  ticks to wait for combat resolution. Do not treat this move as permission to
  redesign gameplay timing.
- The elapsed-time/speed clock remains in GameManager for now, as specified below.

## Session ownership and construction

Agreed on September 20, 2026.

- Use `Simulation/GameSession.cs` to construct, connect, own, and dispose the
  runtime components for one game: commands, queries, result bus, event executor,
  and tick processor, together with the associated game state.
- Construct it directly with the game state and composed content data, for example
  `new GameSession(game, gameData)`. Do not add a separate GameSessionFactory.
- Session construction and lifetime management are one responsibility. GameSession
  must not acquire gameplay rules, tick phases, or forwarding methods for every
  command. Its components perform the work.
- Give consumers the specific dependencies they need rather than the entire
  session as a general-purpose lookup object.
- Keep the existing GameRuntime in App responsible for the active session and
  application-level starting, loading, saving, and replacement.
- Preserve the existing in-place replacement sequence, including its partial
  state if initialization throws. The later behavior-preservation instruction
  supersedes the earlier constructor-first replacement proposal. Do not make
  replacement atomic as part of this refactor. Reconnect presentation and dispose
  superseded subscriptions only at equivalent successful replacement boundaries.
- Account explicitly for hot-load presentation rebinding: the current runtime
  replaces the contents of the existing manager rather than simply replacing
  that manager reference. Preserve hot loading and settled-state save behavior
  with regression coverage during migration.

## Clock ownership deferred

Agreed on September 20, 2026.

- Leave elapsed-time accumulation, game speed, and pause timing in GameManager
  for now. Do not introduce a clock class or move this responsibility during the
  current refactor.
- Preserve the existing timing behavior and frame elapsed-time input from
  GameFlowController.
- GameTickProcessor still owns tick execution. Leaving the clock in GameManager
  does not move gameplay rules or tick phases back into GameManager.
- GameManager therefore remains for this responsibility; the current plan must
  not assume its complete removal.

## Result classes remain unchanged

Agreed on September 20, 2026.

- Do not modify GameResult or any existing result subclass as part of this work.
  Do not add, remove, or change their fields, properties, or structure.
- Defer the proposed historical-payload changes. The earlier capture discussion
  does not authorize changing OfficerCaptureStateResult or the base result class.
- If safe migration of a listener depends on a result-contract change, stop and
  explain that dependency rather than changing the contract or claiming the
  ordering problem is solved.

## Observer delegation and explicit publication

Agreed on September 20, 2026.

- An observer handles a result and invokes the appropriate command method when a
  further gameplay operation is needed. The command owns that operation's implementation.
- Do not merge observer and command responsibilities merely because both need the
  same operation. Delegating to the command shares that implementation without
  duplication. Final grouping depends on the concrete responsibilities, not a
  mandatory separate class for each result type.
- GameSession creates one GameResultBus for the session and passes that same
  instance to the command objects that publish results.
- A command explicitly publishes its completed change, conceptually with
  `_bus.Publish(result)`, using the existing result classes unchanged.
- Publish regardless of whether the caller was UI, AI, the authored-event executor,
  or another observer. No scene-graph watcher or GameManager forwarding is required.
- Results produced by observer-initiated commands join the active queue and reach
  all matching subscribers, including message handling and authored-event execution.
  The calling observer does not manually notify those other subscribers.
- Preserve existing message eligibility/filtering and combat buffering. Receiving
  a result does not mean it necessarily produces a player-facing message.

## Command return contracts remain unchanged

Agreed on September 20, 2026.

- Preserve each operation's existing return contract during the refactor, including
  boolean acceptance where that is what callers currently consume.
- Do not introduce a universal command response object or add rejection-reason
  contracts as part of this work. Richer rejection reporting is deferred.
- Preserve the meaning of existing return values, not merely their types.
  Publishing through the bus does not authorize changing acceptance semantics.

## Next: concrete mapping and migration verification

The main decision list is settled or explicitly deferred. Result-contract changes,
richer command rejection reporting, and new failure/recovery/loop-protection
behavior are out of scope. This does not mean implementation details are complete
or that gameplay behavior preservation has been verified.

Map existing methods, dependencies, exception boundaries, subscriptions, and tick
phases to the agreed classes. Plan regression tests and behavior comparisons for
each migrated slice. Stop for review if the mapping requires changing behavior or
any contract explicitly protected above.
