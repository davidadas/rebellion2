# Schedules

Schedules activate events from campaign time. A `Schedule` contains exactly one scheduling option and
cannot be combined with gameplay `Triggers`.

## At

`At` becomes eligible on an absolute campaign tick. It is a one-time schedule and therefore requires `TriggerCount="1"`.

- `Tick` — required non-negative campaign tick.

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

- `Ticks` — required positive interval between successful activations.
- `InitialDelayTicks` — optional non-negative delay before the first activation; defaults to `0`.

```xml
<Schedule>
  <Every Ticks="50" InitialDelayTicks="10"/>
</Schedule>
```

After a successful activation, the next eligible tick is the current tick plus `Ticks`. Failed conditions do not consume an activation or move the schedule forward.

## Random

`Random` rolls an inclusive delay for the first activation and rolls a fresh delay after every successful activation.

- `MinimumTicks` — required positive minimum delay.
- `MaximumTicks` — required maximum delay; cannot be lower than `MinimumTicks`.

```xml
<Schedule>
  <Random MinimumTicks="25" MaximumTicks="75"/>
</Schedule>
```

Both values are delays, not absolute tick numbers. The minimum must be positive and the maximum cannot be lower than the minimum.

## After

`After` waits for one event to execute, then applies a delay from that event's last execution tick. It is a one-time schedule and requires `TriggerCount="1"`.

- `EventInstanceID` — required ID of an event in the loaded catalog.
- `DelayTicks` — required non-negative delay after that event executes.

```xml
<Schedule>
  <After EventInstanceID="EVENT_A" DelayTicks="20"/>
</Schedule>
```

The referenced event must exist in the same loaded event pool.

## AfterAll

`AfterAll` waits until every listed event has executed. Its delay starts from the latest dependency
execution. It is a one-time schedule and requires `TriggerCount="1"`.

- `DelayTicks` — required non-negative delay.
- `Events` — required child containing one or more `Event` elements.
- `EventInstanceID` — required attribute on each `Event`; IDs must be unique.

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

## AfterAny

`AfterAny` waits until the first listed event executes. Its delay starts from the earliest completed
dependency. It is a one-time schedule and requires `TriggerCount="1"`.

- `DelayTicks` — required non-negative delay.
- `Events` — required child containing one or more `Event` elements.
- `EventInstanceID` — required attribute on each `Event`; IDs must be unique.

```xml
<Schedule>
  <AfterAny DelayTicks="20">
    <Events>
      <Event EventInstanceID="EVENT_A"/>
      <Event EventInstanceID="EVENT_B"/>
    </Events>
  </AfterAny>
</Schedule>
```

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
