# Creating Game Events

Game events let content authors react to campaign time and simulation results without adding new C# code. An event can select game objects, test current game state, perform ordered actions, and persist its activation history across saves.

This guide explains the event lifecycle, where events are authored, and how each top-level part of a `GameEvent` works.

## How events work

Event definitions are loaded with the rest of the game data when a campaign begins. Before play, the game validates event IDs, schedules, trigger contracts, bindings, and dependencies. Runtime state is stored by `InstanceID`, including how many times an event has activated and when it may activate again.

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

### Bindings

Optionally select scene nodes and expose them throughout the current event evaluation. Scheduled
events declare bindings before `Schedule`, allowing recurring `Until` conditionals to consume them.
Triggered events declare `Triggers` first, allowing selections to consume trigger-result bindings.
Top-level conditionals and actions may consume either kind. See [Bindings](Bindings.md) and
[Selectors](Selectors.md).

### Schedule

Optionally activates the event according to campaign time or another event. See
[Schedules](Schedules.md).

### Triggers

Optionally activates the event in response to gameplay results and exposes result data. See
[Triggers and bindings](Triggers.md). An event cannot combine `Triggers` with `Schedule`.

### Conditionals

Optional XML collection containing the conditionals that gate an activation against current game
state and bindings. The collection is named `Conditionals`; the individual elements are called
conditionals. See [Conditionals](Conditionals.md).

### Actions

Optional collection of game changes performed in authored order. An event without actions can still
serve as a persistent timing or completion marker. See [Actions](Actions.md).

---

<p align="center"><a href="Selectors.md">Selectors →</a></p>
