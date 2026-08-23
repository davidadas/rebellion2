# Schedules

Schedules activate events from campaign time. A `Schedule` contains exactly one scheduling option and cannot be combined with `Triggers`.

Top-level bindings are resolved before a schedule is evaluated. Consequently, `Until` may use the
same bindings as the event's top-level conditionals and actions.

Bindings are evaluation-scoped, not persistent. Their selectors run whenever the event is
evaluated, including while a recurring schedule is waiting for its next eligible tick. A random
binding can therefore resolve a different node on successive evaluations.

## Contents

- [`At`](#at)
- [`RandomDelay`](#randomdelay)
- [`Every`](#every)
- [`RandomInterval`](#randominterval)
- [`After`](#after)
- [`AfterAll`](#afterall)
- [`AfterAny`](#afterany)

## At

`At` makes an event eligible at one absolute campaign tick. It is inherently one-shot.

**Required options**

- `Tick` **[Required]:** Non-negative campaign tick.

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

**Required options**

- `MinimumTicks` **[Required]:** Positive minimum first eligible campaign tick.
- `MaximumTicks` **[Required]:** Maximum first eligible campaign tick; cannot be lower than
  `MinimumTicks`.

```xml
<Schedule>
  <RandomDelay MinimumTicks="300" MaximumTicks="400"/>
</Schedule>
```

## Every

`Every` repeats at a fixed interval. `InitialDelayTicks` sets its first eligible campaign tick,
measured from tick zero. Later intervals are measured from the event's previous activation.

**Required options**

- `Ticks` **[Required]:** Positive interval between activations.

**Optional options**

- `InitialDelayTicks` **[Optional]:** Non-negative initial delay; defaults to `0`.
- `Until` **[Optional]:** Conditionals that permanently complete the schedule when all pass.

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

**Required options**

- `MinimumTicks` **[Required]:** Positive minimum interval.
- `MaximumTicks` **[Required]:** Maximum interval; cannot be lower than `MinimumTicks`.

**Optional options**

- `Until` **[Optional]:** Conditionals that permanently complete the schedule when all pass.

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

**Required options**

- `EventInstanceID` **[Required]:** ID of an event in the loaded catalog.
- `DelayTicks` **[Required]:** Non-negative delay after that activation.

```xml
<Schedule>
  <After EventInstanceID="EVENT_A" DelayTicks="20"/>
</Schedule>
```

## AfterAll

`AfterAll` waits for every listed event to activate. Its delay begins at the latest activation.

**Required options**

- `DelayTicks` **[Required]:** Non-negative delay.
- `Events` **[Required]:** Collection containing one or more unique event IDs.

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

**Required options**

- `DelayTicks` **[Required]:** Non-negative delay.
- `Events` **[Required]:** Collection containing one or more unique event IDs.

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
