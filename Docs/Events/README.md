# Creating Game Events

Game events let content authors react to campaign time and simulation results without adding new C# code. An event can select game objects, test the current game state, perform ordered actions, and persist its own execution history across saves.

This guide explains the event lifecycle, where events are authored, and how each top-level part of a `GameEvent` works.

## How events work

Event definitions are loaded with the rest of the game data when a campaign begins. Before play, the game validates event IDs, schedules, trigger contracts, bindings, and dependencies. Runtime state is stored by `InstanceID`, including how many times an event has executed and when it may execute again.

An event activation follows this pipeline:

1. A schedule becomes eligible, a gameplay trigger receives a matching result, or an unscheduled event is checked during the current tick.
2. `TriggerCount` and `Until` determine whether the event is permanently exhausted.
3. The event waits until its scheduled tick, if it has a schedule.
4. `Target` resolves one game object and exposes it as `$target`, if a target is declared.
5. Every top-level conditional must pass.
6. Actions execute from top to bottom.
7. The execution count, last execution tick, and next eligible tick are persisted.

An event with a `Schedule` is driven by campaign time. An event with `Triggers` reacts to typed simulation results. An event with neither is evaluated once per tick. An event cannot combine `Schedule` and `Triggers`.

## Adding an event

Open the standard game-event catalog:

```text
Assets/Content/Packs/ClassicGalacticCivilWar/Shared/Data/game-events.xml
```

Add a `GameEvent` inside the existing `GameEvents` root:

```xml
<GameEvent TriggerCount="1">
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
<GameEvent TriggerCount="1">
  <InstanceID>UNIQUE_EVENT_ID</InstanceID>

  <!-- Choose Schedule or Triggers, never both. -->
  <Schedule>...</Schedule>
  <Triggers>...</Triggers>

  <!-- Optional execution controls. -->
  <Until>...</Until>
  <Target>...</Target>
  <Conditionals>...</Conditionals>

  <!-- Required behavior. -->
  <Actions>...</Actions>
</GameEvent>
```

| Domain | Purpose |
| --- | --- |
| `InstanceID` | Stable identity used by runtime state, saves, and dependent events. |
| `TriggerCount` | Optional maximum number of successful activations. Omission means unlimited. |
| [`Schedule`](Schedules.md) | Activates the event according to campaign time or another event. |
| [`Triggers`](Triggers.md) | Activates the event in response to gameplay results and exposes result data. |
| [`Until`](Conditions.md#stopping-an-event-with-until) | Permanently exhausts an event when its stop conditions pass. |
| [`Target`](Targets.md) | Selects exactly one scene node and binds it as `$target`. |
| [`Conditionals`](Conditions.md) | Gates an activation against current game state and bindings. |
| [`Actions`](Actions.md) | Performs game changes in authored order. |

## Guide

1. [Schedules](Schedules.md)
2. [Triggers and bindings](Triggers.md)
3. [Conditions and Until](Conditions.md)
4. [Targets and selectors](Targets.md)
5. [Actions and messages](Actions.md)
6. [Complete examples](Examples.md)
7. [Testing and troubleshooting](Testing.md)

---

[Schedules →](Schedules.md)
