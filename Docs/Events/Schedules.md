# Schedules

Schedules activate events from campaign time. A `Schedule` contains exactly one mode and cannot be combined with gameplay `Triggers`.

| Mode | Use it when |
| --- | --- |
| [`At`](#at) | The event must become eligible on one absolute campaign tick. |
| [`Every`](#every) | The event should repeat at a fixed interval. |
| [`Random`](#random) | Each activation should wait for a newly rolled delay. |
| [`After`](#after) | The event depends on one earlier event. |
| [`AfterAll`](#afterall-and-afterany) | Every listed event must have executed. |
| [`AfterAny`](#afterall-and-afterany) | At least one listed event must have executed. |

## Element reference

| Element | Attributes | Child elements | Rules |
| --- | --- | --- | --- |
| `Schedule` | None | Exactly one of `At`, `Every`, `Random`, `After`, `AfterAll`, or `AfterAny` | Cannot appear with `Triggers`. |
| `At` | `Tick` — required non-negative integer | None | One-time; requires `TriggerCount="1"`. |
| `Every` | `Ticks` — required positive integer; `InitialDelayTicks` — optional non-negative integer, default `0` | None | Repeats after successful activations. |
| `Random` | `MinimumTicks` — required positive integer; `MaximumTicks` — required integer greater than or equal to the minimum | None | Rolls a new inclusive delay after successful activations. |
| `After` | `EventInstanceID`, `DelayTicks` — required; delay must be non-negative | None | One-time; referenced event must exist. |
| `AfterAll` | `DelayTicks` — required non-negative integer | `Events` containing one or more `Event` elements | One-time; every dependency must execute. |
| `AfterAny` | `DelayTicks` — required non-negative integer | `Events` containing one or more `Event` elements | One-time; the first completed dependency is sufficient. |
| `Event` | `EventInstanceID` — required | None | IDs in one dependency list must be unique. |

## At

`At` becomes eligible on an absolute campaign tick. It is a one-time schedule and therefore requires `TriggerCount="1"`.

```xml
<GameEvent TriggerCount="1">
  <InstanceID>EVENT_AT_TICK_200</InstanceID>
  <Schedule>
    <At Tick="200"/>
  </Schedule>
  <Actions>...</Actions>
</GameEvent>
```

Conditions may delay actual execution beyond tick 200. Once eligible, the event continues to be checked until its conditions pass or `Until` exhausts it.

## Every

`Every` repeats at a fixed interval. `InitialDelayTicks` controls the first eligible tick; when omitted, the event is eligible immediately.

```xml
<Schedule>
  <Every Ticks="50" InitialDelayTicks="10"/>
</Schedule>
```

After a successful activation, the next eligible tick is the current tick plus `Ticks`. Failed conditions do not consume an activation or move the schedule forward.

## Random

`Random` rolls an inclusive delay for the first activation and rolls a fresh delay after every successful activation.

```xml
<Schedule>
  <Random MinimumTicks="25" MaximumTicks="75"/>
</Schedule>
```

Both values are delays, not absolute tick numbers. The minimum must be positive and the maximum cannot be lower than the minimum.

## After

`After` waits for one event to execute, then applies a delay from that event's last execution tick. It is a one-time schedule and requires `TriggerCount="1"`.

```xml
<Schedule>
  <After EventInstanceID="EVENT_A" DelayTicks="20"/>
</Schedule>
```

The referenced event must exist in the same loaded event pool.

## AfterAll and AfterAny

Use `AfterAll` when every dependency must have executed. Its delay starts from the latest dependency execution. Use `AfterAny` when the first completed dependency is sufficient; its delay starts from the earliest completed dependency.

```xml
<Schedule>
  <AfterAll DelayTicks="20">
    <Events>
      <Event EventInstanceID="EVENT_A"/>
      <Event EventInstanceID="EVENT_B"/>
    </Events>
  </AfterAll>
</Schedule>
```

Both modes are one-time schedules and require `TriggerCount="1"`. Dependencies must be present, unique, and valid.

## Limiting and stopping repetition

`TriggerCount` limits successful activations. Omit it for unlimited activations, or set a positive number:

```xml
<GameEvent TriggerCount="5">
  <InstanceID>EVENT_RUNS_FIVE_TIMES</InstanceID>
  <Schedule>
    <Every Ticks="50"/>
  </Schedule>
  <Actions>...</Actions>
</GameEvent>
```

Use [`Until`](Conditions.md#stopping-an-event-with-until) when game state, rather than a fixed count, decides when a repeating event permanently stops.

---

<p align="center"><a href="Index.md">← Event guide</a> · <a href="Triggers.md">Triggers →</a></p>
