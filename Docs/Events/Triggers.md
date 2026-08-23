# Triggers

Triggers activate events from typed gameplay results. An event uses either `Triggers` or `Schedule`, never both. Multiple triggers are alternatives: any matching result gives the event one activation opportunity.

```xml
<Triggers>
  <UnitArrived UnitInstanceID="LUKE_SKYWALKER"/>
  <UnitArrived UnitInstanceID="DARTH_VADER"/>
</Triggers>
```

When multiple triggers use `As`, every trigger on that event must use the same alias and expose a compatible result type.

## MissionCompleted

Activates when a mission completes and all authored filters match.

**Options**

- `MissionTypeID` — optional mission type filter.
- `Outcome` — optional mission outcome filter.
- `CompletionReason` — optional completion-reason filter.
- `SourceEventInstanceID` — optional source-event filter.
- `As` — optional alias exposing the complete `MissionCompletedResult`.
- `Participants` — optional participant filter with `Match="Any"` or `Match="All"`.

```xml
<Triggers>
  <MissionCompleted MissionTypeID="SABOTAGE" Outcome="Success" As="mission">
    <Participants Match="Any">
      <Units>
        <Unit UnitInstanceID="HAN_SOLO"/>
        <Unit UnitInstanceID="LEIA_ORGANA"/>
      </Units>
    </Participants>
  </MissionCompleted>
</Triggers>
```

## UnitArrived

Activates when one unit finishes movement and all authored filters match.

**Options**

- `UnitInstanceID` — optional arriving-unit filter.
- `DestinationInstanceID` — optional destination filter.
- `SourceEventInstanceID` — optional source-event filter.
- `As` — optional alias exposing the complete `UnitArrivedResult`.

Use multiple triggers when any of several units should activate the event:

```xml
<Triggers>
  <UnitArrived UnitInstanceID="LUKE_SKYWALKER" As="arrival"/>
  <UnitArrived UnitInstanceID="DARTH_VADER" As="arrival"/>
</Triggers>
```

Multiple triggers express the OR relationship directly.

## DuelCompleted

Activates when a duel completes and all authored filters match.

**Options**

- `FirstOfficerInstanceID` — optional first-officer filter.
- `SecondOfficerInstanceID` — optional second-officer filter.
- `SourceEventInstanceID` — optional source-event filter.
- `As` — optional alias exposing the complete `DuelResult`.

```xml
<Triggers>
  <DuelCompleted FirstOfficerInstanceID="LUKE_SKYWALKER"
                 SecondOfficerInstanceID="DARTH_VADER"
                 As="duel"/>
</Triggers>
```

## Trigger bindings

`As` binds the complete matched result for use by conditionals and actions. Prefix the alias with `$` when referring to it:

```xml
<Triggers>
  <UnitArrived UnitInstanceID="EMPEROR_PALPATINE" As="arrival"/>
</Triggers>
<Conditionals>
  <EvaluateBinding Binding="$arrival.Destination.InstanceID"
                   Comparison="Equal"
                   CompareTo="CORUSCANT"/>
</Conditionals>
```

Use top-level [`Bindings`](Bindings.md#bindings) when an event must select and retain a scene node independently of a gameplay result.

---

<p align="center"><a href="Schedules.md">← Schedules</a> · <a href="Index.md">Event guide</a> · <a href="Conditions.md">Conditions →</a></p>
