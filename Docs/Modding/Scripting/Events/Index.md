# Creating Game Events

> **Note:** Game events are in active development and are likely to change.

Game events let content authors react to campaign time and simulation results without adding new C# code. An event can select game objects, test current game state, perform ordered actions, and persist its activation history across saves.

Events use shared [scripting components](../Index.md) for bindings, selectors, conditionals, triggers, and actions. Schedules and activation history belong to events.

This guide explains the event lifecycle, where events are authored, and how each top-level part of a `GameEvent` works.

## How events work

Event definitions are loaded with the rest of the game data when a campaign begins. Before play, the game validates event `InstanceID` values, schedules, trigger contracts, bindings, and dependencies. Runtime state is stored by `InstanceID`, including how many times an event has activated and when it may activate again.

Events with schedules are considered on each campaign tick. Events with triggers enter evaluation
after a matching simulation result is produced. Once selected, an event follows this pipeline:

1. The event confirms that it is not already complete and initializes its schedule.
2. A matched trigger exposes its result, then top-level bindings select any required scene nodes.
3. A recurring schedule's `Until` conditionals may permanently complete the event.
4. A scheduled event must have reached its next eligible tick.
5. Every top-level conditional must pass.
6. Actions execute from top to bottom.
7. The activation count, last activation tick, and next eligible tick are persisted.

An event with a `Schedule` is driven by campaign time. An event with `Triggers` reacts to typed simulation results. Every event requires exactly one of those activation sources.

## Adding an event

Open the standard game-event catalog:

```text
Assets/Content/Packs/ClassicGalacticCivilWar/Shared/Data/game-events.xml
```

Add a `GameEvent` inside the existing `GameEvents` root:

```xml
<GameEvent>
  <InstanceID>MY_FIRST_EVENT</InstanceID>
  <Schedule>
    <At Tick="10"/>
  </Schedule>
  <Actions>
    <SendMessage RecipientFactionInstanceID="FNALL1" Type="Advice">
      <Subject>My First Event</Subject>
      <Body>The event is working.</Body>
    </SendMessage>
  </Actions>
</GameEvent>
```

`InstanceID` is the event's permanent runtime and save-game identity. Keep it unique and do not rename it after a campaign has stored the event. Player-facing text belongs in messages, not in the ID.

## Event structure

A complete event uses the following high-level shape. Most events only need some of these domains:

```xml
<GameEvent MaximumActivations="3">
  <InstanceID>UNIQUE_EVENT_ID</InstanceID>

  <!-- Recurring scheduled events may scope their schedule to selected targets. -->
  <!-- <ForEach As="$planet"><From>...</From></ForEach> -->

  <!-- Scheduled events put bindings before their schedule. -->
  <Bindings>...</Bindings>
  <Schedule>...</Schedule>

  <!-- Triggered events instead put Triggers before optional Bindings. -->
  <!-- <Triggers>...</Triggers> -->
  <!-- <Bindings>...</Bindings> -->

  <!-- Optional activation gates. -->
  <Conditionals>...</Conditionals>

  <!-- Optional authored behavior. -->
  <Actions>
    <!-- Add the actions that should run on each activation. -->
  </Actions>
</GameEvent>
```

### InstanceID

Required stable identity used by runtime state, saves, and dependent events.

### MaximumActivations

Optional positive maximum number of successful activations, regardless of whether the event uses a
schedule or triggers. Omission adds no numerical cap. One-shot schedules still complete after one
activation, recurring schedules can complete through `Until`, and triggered events remain active
until their limit is reached.

```xml
<GameEvent MaximumActivations="3">
  <InstanceID>EVENT_ACTIVATES_THREE_TIMES</InstanceID>
  <Schedule>
    <Every Ticks="50"/>
  </Schedule>
  <Actions>
    <!-- Add the actions that should run on each activation. -->
  </Actions>
</GameEvent>
```

Do not add `MaximumActivations="1"` to a one-shot schedule; it is redundant. A one-shot schedule
cannot specify a value greater than one.

With `ForEach`, this remains a total across all targets, not a separate allowance for each target.
Reaching the limit permanently completes the event and stops every target's schedule.

### ForEach

Runs a recurring schedule independently for every selected scene node. Place `ForEach` immediately
after `InstanceID`, before optional `Bindings` and the required `Schedule`.

**Required options**

- `As` **[Required]:** The exact, unique binding name assigned to the current target. Ordinary
  event bindings, conditionals, and actions may consume it.
- `From` **[Required]:** Exactly one deterministic [selector](../Selectors.md): `SelectPlanets`,
  `SelectOfficers`, `SelectSpecialForces`, `SelectFleets`, `SelectMissions`, `SelectCapitalShips`,
  `SelectStarfighters`, `SelectRegiments`, `SelectBuildings`, or `SelectManufacturingOrders`.

```xml
<GameEvent>
  <InstanceID>PER_PLANET_REPORT</InstanceID>
  <ForEach As="$planet">
    <From>
      <SelectPlanets IsOwned="true"/>
    </From>
  </ForEach>
  <Schedule>
    <RandomInterval MinimumTicks="30" MaximumTicks="1000"/>
  </Schedule>
  <Actions>
    <RevealToFaction FactionInstanceID="FNALL1" PlanetBinding="$planet" Category="PlanetControl"/>
  </Actions>
</GameEvent>
```

`As` follows the same naming rules as [`Bind`](../Bindings.md). Scope selection must be independent
of activation bindings: `SelectRandom`, `SelectBinding`, composite selectors, and selectors with
`PlanetBinding` are rejected. Use literal filters in `From`; ordinary event bindings and actions
may still perform random selection after the target is bound.

Only `Every` and `RandomInterval` are supported; `ForEach` cannot accompany `Triggers` or a one-shot
schedule. Each target's first delay starts when it enters the scope, rather than at campaign tick
zero. A target that leaves loses its pending timer; returning starts a fresh delay. A change that
keeps it selected, such as changing between owners with `IsOwned="true"`, preserves the timer.
Planet ownership changes update scope membership between scheduled passes.

Targets are discovered at the start of each scheduled pass and processed in ordinal `InstanceID`
order. After an activation changes game state, remaining targets are checked against the selector
again. Newly matching targets become candidates on the next pass. Ordinary bindings are evaluated
separately for each target, after binding `As`; failed conditionals leave that target's due time
unchanged.

`Until` permanently completes only the current target's schedule. Its completion record survives
leaving, returning, and saving. An empty selection or completion of all current targets does not
complete the event, because future targets can still enter. `MaximumActivations` is the event-wide
completion limit. Event references such as `After`, `HasEventActivated`, and `IsEventComplete`
continue to use the event's aggregate history. Persistent event variables keep their existing
shared values; they do not become target-local.

### Bindings

Optionally assign names to selected scene nodes or matched trigger results and expose them throughout
the current event evaluation. Scheduled events declare selection bindings before `Schedule`,
allowing recurring `Until` conditionals to consume them. Triggered events bind their matched result
before resolving optional selection bindings. Top-level selectors, conditionals, and actions may
consume either kind. See [Bindings](../Bindings.md) and [Selectors](../Selectors.md).

### Schedule

Optionally activates the event according to campaign time or another event. See
[Schedules](Schedules.md).

### Triggers

Optionally activates the event in response to gameplay results and exposes result data. See
[Triggers and bindings](../Triggers.md). An event cannot combine `Triggers` with `Schedule`.

### Conditionals

Optional XML collection containing the conditionals that gate an activation against current game
state and bindings. The collection is named `Conditionals`; the individual elements are called
conditionals. See [Conditionals](../Conditionals.md).

### Actions

Optional collection of game changes performed in authored order. An event without actions can still
serve as a persistent timing or completion marker. See [Actions](../Actions.md).

---

<p align="center"><a href="../Selectors.md">Selectors →</a></p>
