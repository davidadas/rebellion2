# Schedules

Schedules activate events from campaign time. A `Schedule` contains exactly one scheduling option and cannot be combined with `Triggers`.

Top-level bindings are resolved before a schedule is evaluated. Consequently, `Until` may use the
same bindings as the event's top-level conditionals and actions.

Bindings are evaluation-scoped, not persistent. Their selectors run whenever the event is
evaluated, including while a recurring schedule is waiting for its next eligible tick. A random
binding can therefore resolve a different node on successive evaluations.

## At

`At` makes an event eligible at one absolute campaign tick. It is inherently one-shot.

**Options**

- `Tick` — required non-negative campaign tick.

```xml
<Schedule>
  <At Tick="200"/>
</Schedule>
```

If the event's conditionals fail at tick 200, it remains eligible until they pass.

## RandomDelay

`RandomDelay` rolls one inclusive campaign tick before a one-shot event becomes eligible. Initial
schedule values are measured from campaign tick zero; they are not added to the tick on which the
event catalog happens to be evaluated.

**Options**

- `MinimumTicks` — required positive minimum first eligible campaign tick.
- `MaximumTicks` — required maximum first eligible campaign tick; cannot be lower than
  `MinimumTicks`.

```xml
<Schedule>
  <RandomDelay MinimumTicks="300" MaximumTicks="400"/>
</Schedule>
```

## Every

`Every` repeats at a fixed interval. `InitialDelayTicks` sets its first eligible campaign tick,
measured from tick zero. Later intervals are measured from the event's previous activation.

**Options**

- `Ticks` — required positive interval between activations.
- `InitialDelayTicks` — optional non-negative initial delay; defaults to `0`.
- `Until` — optional conditionals that permanently complete the schedule when all pass.

```xml
<Schedule>
  <Every Ticks="50" InitialDelayTicks="10">
    <Until>
      <IsCaptured OfficerInstanceID="HAN_SOLO"/>
    </Until>
  </Every>
</Schedule>
```

## RandomInterval

`RandomInterval` rolls its first eligible campaign tick from the authored range, measured from tick
zero. After each activation, it rolls a new inclusive delay from that activation tick.

**Options**

- `MinimumTicks` — required positive minimum interval.
- `MaximumTicks` — required maximum interval; cannot be lower than `MinimumTicks`.
- `Until` — optional conditionals that permanently complete the schedule when all pass.

```xml
<Schedule>
  <RandomInterval MinimumTicks="300" MaximumTicks="600">
    <Until>
      <IsCaptured OfficerInstanceID="HAN_SOLO" CaptorFactionInstanceID="FNEMP1"/>
    </Until>
  </RandomInterval>
</Schedule>
```

Failed event conditionals do not consume an activation or roll the next interval. Once the event is
eligible, it is reconsidered on each campaign tick until its conditionals pass or `Until` completes
the schedule.

## After

`After` makes a one-shot event eligible after another event activates, plus an authored delay.

**Options**

- `EventInstanceID` — required ID of an event in the loaded catalog.
- `DelayTicks` — required non-negative delay after that activation.

```xml
<Schedule>
  <After EventInstanceID="EVENT_A" DelayTicks="20"/>
</Schedule>
```

## AfterAll

`AfterAll` waits for every listed event to activate. Its delay begins at the latest activation.

**Options**

- `DelayTicks` — required non-negative delay.
- `Events` — required collection containing one or more unique event IDs.

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

`AfterAny` initializes once any listed event has activated. If several predecessors have already
activated when it initializes, the delay uses the earliest of their recorded latest activation
ticks.

**Options**

- `DelayTicks` — required non-negative delay.
- `Events` — required collection containing one or more unique event IDs.

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

To cap how often an event can activate, use the event-level
[`MaximumActivations`](Index.md#maximumactivations) option. It is not part of `Schedule` because it
also applies to trigger-driven events.

---

<p align="center"><a href="Bindings.md">← Bindings</a> · <a href="Index.md">Event guide</a> · <a href="Triggers.md">Triggers →</a></p>
