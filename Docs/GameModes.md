# Game mode architecture

## Scope and migration status

This document records the mode ownership review and the migration criteria. The tactical separation
is in progress. A namespace or folder change does not establish that a responsibility has been
separated.

The review follows `GameSession`, `GameServiceRegistration`, `GameTickProcessor`,
`SpaceCombatCommands`, `TacticalCombatFactory`, `TacticalCombatSession`, both AI planners, the scene
controllers, and the campaign and tactical multiplayer protocols. The compiled dependency inventory
identifies 144 tactical-related types across 74 source files before separation.

The first enforced boundaries are now present:

- `Rebellion.Util` owns shared mechanisms and cannot reference the application assembly.
- `Rebellion.Combat.Contracts` owns the participation enums and component damage exchanged between
  the campaign and battle. Existing type names and XML members remain unchanged.
- `Rebellion.Tactical.Contracts` owns tactical content and detached outcomes.
- `Rebellion.Tactical.Core` owns the encounter model and execution, with engine references disabled.
  It references the contracts and utilities, not `GameAssembly`.
- `Rebellion.Tactical.Runtime` implements notification scheduling and scenery initialization through
  core-owned interfaces. It also compiles without engine or campaign references.

`TacticalCombatUnitCapture` reads campaign hardware and planetary facilities before constructing
participants. The battle retains values and identifiers. `SpaceCombatCommands` retains campaign
references outside the battle and resolves completed outcomes at its existing import boundary.
Local completion preserves its existing result conversions; remote completion retains its stricter
input validation before campaign mutation.

The initial extraction compiled in Unity and matched six deterministic traces across three seeds
and two fleet sizes. The comparison includes each authoritative frame, ordered presentation edges,
random draw counts, and completed outcomes. One large encounter remains unfinished at the same
6000-frame limit before and after extraction. These checks do not replace the complete validation
required before publishing the migration.

The extracted core and campaign integration subsequently pass 881 focused Unity cases and 47
architecture cases. Repository formatting, compilation, lint, and analyzer checks also pass at this
checkpoint. Complete application, rendering, player, and live multiplayer validation remains required
after the remaining migration work.

The combat content branch incorporates the campaign updates required by the merged source. The
checked-in media revision identifies that combined content; local staged content must match it.
Missing authored recharge parameters are not replaced with code defaults.

Notification composition is explicit: callers supply the audio factory, and the encounter passes
its existing random stream, faction, live pause callback, and presentation clock. Queue refresh
points remain unchanged. Scenery uses its supplied interface and retains lazy clock acquisition,
reseed timing, and draw order in the runtime implementation. The renderer reads that interface.

Six further active-audio battle comparisons retain identical frames, encoded network payloads,
random draw counts, and outcomes after runtime extraction. The focused Unity verification passes
888 cases, with formatting, compilation, lint, analyzer, and architecture checks passing as well.

Mandatory targeting assessment and replication admission still need further separation within the
mode. Their inclusion in an engine-independent assembly does not establish that their ownership is
finished.

## Mode ownership

| Area | Strategic campaign | Tactical battle | Shared responsibility |
| --- | --- | --- | --- |
| State | Persistent galaxy, ownership, units, orders, research | Encounter participants, groups, motion, weapons, damage, battle lifecycle | Stable participant identities and explicit battle setup/outcome contracts |
| Execution | Ordered campaign ticks and command/result boundaries | Frame advancement and ordered battle commands | Application admission and cancellation infrastructure |
| AI | Faction planning against a campaign assessment | Automated battle decisions against the encounter state | Random interfaces and arithmetic with matching semantics |
| Presentation | Galaxy map and campaign windows | Battle scene, inspection, camera, effects, and battle notifications | Content caches, window controls, input routing, and audio devices |
| Networking | Host-ordered campaign actions and settled-state agreement | Authoritative host simulation and passive replica presentation | Authentication, lobby membership, connection transport, and bounded delivery mechanisms |
| Integration | Suspending the campaign and committing a completed result | Producing a detached completed battle outcome | A single validated handoff in each direction |

Similar names do not imply interchangeable behavior. Campaign and battle AI have different state and
execution frequencies. Campaign synchronization and tactical replication have different authority and
delivery contracts. These policies remain owned by their modes.

## Inspected boundary problems

| Current implementation | Coupling to remove | Required retained behavior |
| --- | --- | --- |
| `TacticalCombatUnit` constructors | Reading campaign entities and retaining their live `ISceneNode` references inside battle state | Capturing the same identities, hardware, payload counts, readiness, and initial values |
| `TacticalCombatSession.CreateResult` | Producing campaign-object results inside the encounter implementation | Applying the same hull conversion, damage, fighter recovery, and withdrawal facts |
| `TacticalCombatBattleStation` | Casting a participant back to a campaign capital ship | Retaining its captured maximum hull and protection/construction gates |
| `TacticalCombatPlanner` | Combining automated fleet policy with mandatory target assessment used by human-controlled groups | Retaining candidate order, duplicate assessments, grouping, and call order |
| `TacticalCombatSession` and `TacticalCombatSelection` | Constructing audio services and refreshing playback-backed notification queues | Retaining notification requests, queue refresh points, status observations, and random draw order |
| `TacticalCombatSceneryRandom` | Lazily reseeding the battle stream through renderer-driven initialization | Making initialization ownership explicit while retaining the same reseed and four draws |
| Session network partials and network-shaped state records | Mixing transport sequencing, semantic input admission, and battle state projection | Preserving authenticated-side validation, replica mutation gates, state validation, and wire values |
| `CombatOwnershipTests` | Treating selected namespaces as evidence of appropriate ownership | Testing actual dependency direction and forbidden coupling |

## Target boundaries

The tactical mode will have a cohesive source area with contracts, simulation/model code, AI policy,
presentation, networking, and a campaign integration adapter. Its simulation must compile without
depending on the campaign assembly, scene controllers, audio playback implementations, or transport
implementations. Mode composition supplies the required external services explicitly.

The campaign adapter captures battle inputs from campaign objects. The battle operates on its own
participants. A completed outcome contains stable identities and values; the campaign validates and
imports it through its existing result boundary. The tactical mode does not mutate the campaign graph.

The existing campaign domain and application code retain their public and serialization contracts
during migration. Strategic and tactical ownership will be made explicit without replacing either
mode's clock, AI policy, or synchronization protocol with an artificial common implementation.

Shared utilities remain in `Util`. Their assembly boundary prevents dependencies on either mode.
The existing XML preservation attribute requires Unity's scripting metadata, so the utility and
persisted contract assemblies retain that metadata reference. The tactical core has engine
references disabled and cannot perform engine operations directly.
Save I/O and dependency-injection helpers keep their current internal application access; splitting
an assembly is not a reason to broaden those APIs publicly.

File logging requires an explicit destination on first enable. Null retains an already configured
destination. The logger no longer chooses an application profile directory: both simulation runners
already supply their log paths. Invalid configuration is rejected before replacing the current path.

The native tactical vector operations retain their precision and storage boundaries. They are not
replaced with the general saturating integer helpers: wrapped multiplication and saturation have
different observable results.

`NextBits` bounds a draw from an existing provider. It does not change the provider's algorithm,
increase its entropy, or justify selecting a particular stream for a mode. Stream selection,
initialization, and reseeding belong to the battle's explicit initialization policy.

## Verification requirements

- Preserving XML element names, save fields, content addresses, Unity asset GUIDs, and existing wire
  values and field ordering.
- Recording deterministic battle baselines before changing simulation or AI execution; comparing
  state, outcomes, ordered events, random consumption, and timings afterward with identical inputs.
- Proving the battle model does not retain campaign entities and its core assembly cannot reference
  campaign, presentation, or networking implementations.
- Exercising actual dependencies, including negative fixtures, instead of relying on namespace
  equality tests.
- Loading all production assemblies in architecture checks so moving types cannot make a rule pass
  vacuously.
- Including every production assembly definition in the simulation build fingerprint and checking
  that changes to those boundaries invalidate the compiled identity.
- Running save/content round trips, relevant deterministic and lifecycle tests, complete Unity and
  repository checks, and live two-account EOS cases after the final network integration changes.

Unknown gameplay behavior remains unresolved rather than receiving a new fallback during cleanup.
Automatic resolution keeps its upstream implementation; changes to shared handoff code must preserve
both its existing path and the interactive battle path.

## Design references

Unity assembly definitions provide explicit compilation dependencies and reject cycles; source
organization alone does not provide that enforcement. See the
[Unity assembly documentation](https://docs.unity3d.com/6000.4/Documentation/Manual/assembly-definitions-intro.html).

The two modes have different models and lifecycles despite sharing participant identities. This
uses the boundary principle described in Microsoft's
[domain model guidance](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model);
it does not introduce separate processes or network services.
