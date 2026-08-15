# Creating custom game events

Game events compose a schedule or typed result triggers, optional iteration, conditions, and
actions. The event catalog is referenced by `GameEventsPath` in a content pack's `pack.xml`; the
standard pack stores it at `Shared/Data/game-events.xml`. Every event requires a stable, unique
`InstanceID`. Player-facing names belong to messages, not event definitions.

An event executes once by default. Use `MinimumRuns` by itself for an exact number of runs,
`MaximumRuns` for a bounded recurring event, or `UnlimitedRuns="true"` for an unbounded one. When
both bounds are present, `MinimumRuns` declares the lower valid bound and must not exceed the
maximum. Run counts advance only after at least one activation executes its actions.

## Scheduling

`Schedule` accepts exactly one mode:

```xml
<Schedule><At Tick="200"/></Schedule>
<Schedule><Every Ticks="50" InitialDelayTicks="10"/></Schedule>
<Schedule><Random MinimumTicks="25" MaximumTicks="75"/></Schedule>
<Schedule><After EventInstanceID="MOD_PREDECESSOR" DelayTicks="20"/></Schedule>
<Schedule>
  <AfterAll DelayTicks="20">
    <Event EventInstanceID="MOD_FIRST_PREDECESSOR"/>
    <Event EventInstanceID="MOD_SECOND_PREDECESSOR"/>
  </AfterAll>
</Schedule>
```

`At` is an absolute campaign tick. `After` begins after the named event first executes.
`AfterAll` anchors to the last listed dependency to execute; `AfterAny` anchors to the first.
`Every` and `Random` provide the next delay for recurring events. Events without a schedule are
immediately eligible. Each event definition owns one persisted schedule and execution count,
including events that iterate over multiple targets.

## Result triggers and bindings

`Triggers` reacts to simulation results. Each trigger exposes selected result arguments under
event-local binding names:

```xml
<GameEvent UnlimitedRuns="true">
  <InstanceID>MOD_ARRIVAL_CEREMONY</InstanceID>
  <Triggers>
    <Trigger Event="core:unit.arrived">
      <Bindings>
        <Bind Argument="Unit" As="unit"/>
        <Bind Argument="Destination" As="destination"/>
      </Bindings>
    </Trigger>
  </Triggers>
  <Conditionals>
    <EvaluateBinding Binding="$unit" Comparison="Equal" ExpectedValue="MON_MOTHMA"/>
    <EvaluateBinding Binding="$destination" Comparison="Equal" ExpectedValue="CHANDRILA"/>
  </Conditionals>
  <Actions>
    <SendMessage RecipientFactionInstanceID="FNALL1"
                 SubjectInstanceID="MON_MOTHMA"
                 LocationInstanceID="CHANDRILA"
                 Type="Mission">
      <Subject>Mon Mothma Arrives</Subject>
      <Body>Mon Mothma has arrived on Chandrila.</Body>
      <AdvisorNotification Preset="SubjectReport"/>
    </SendMessage>
  </Actions>
</GameEvent>
```

Supported contracts include `core:unit.arrived`, `core:mission.completed`,
`core:duel.completed`, `core:officer.capture-changed`, and `core:force.discovered`. A typed,
non-reflective registry defines each contract and its available arguments. Binding references use
a `$` prefix. Multiple triggers are alternatives; each matching result can activate the event.

## Conditions

Sibling conditions are an implicit AND. Composite conditions wrap their child conditions explicitly:

```xml
<Conditionals>
  <IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
  <Not>
    <Conditionals>
      <Any>
        <Conditionals>
          <IsOnMission UnitInstanceID="LEIA_ORGANA"/>
          <IsCaptured OfficerInstanceID="LEIA_ORGANA"/>
          <IsInTransit UnitInstanceID="LEIA_ORGANA"/>
        </Conditionals>
      </Any>
    </Conditionals>
  </Not>
</Conditionals>
```

`IsOwned` always names a planet or planet binding; its optional faction restricts the owner.
`ShareParent` compares immediate parents. `ShareAncestor Type="Planet"` compares the nearest
ancestor of that type. Other conditions include `All`, `Any`, `Not`, `Xor`, `IsAtLocation`,
`IsOnMission`, `IsInTransit`, `IsCaptured`, `IsCapturedBy`, `IsKilled`, `IsInjured`,
`IsForceEligible`, `HasForceRank`, `CompareOfficerStat`, `ComparePlanetStat`,
`IsEventComplete`, `EvaluateEventVariable`, and `EvaluateBinding`.

## Iteration and selectors

`Target` selects exactly one activation target and binds it as `$target`:

```xml
<Target>
  <Candidates>
    <SelectRandom Count="1">
      <Candidates>
        <SelectPlanets SystemType="CoreSystem"/>
      </Candidates>
    </SelectRandom>
  </Candidates>
</Target>
```

Typed selectors include planets, planet systems, fleets, missions, officers, special forces,
capital ships, starfighters, regiments, buildings, manufacturing orders, and bindings.
`SelectRandom` deduplicates the union of its children, uses deterministic instance-ID ordering,
and applies its probability and count bounds. Actions accept only domain-compatible selectors;
the schema rejects statically incompatible combinations and runtime bindings are type checked.

```xml
<DestroyUnits>
  <Units>
    <SelectRandom ChancePercent="25" MinimumCount="1" MaximumCount="3">
      <Candidates>
        <SelectBuildings PlanetBinding="$target" Category="PlanetaryDefense"/>
        <SelectRegiments PlanetBinding="$target"/>
      </Candidates>
    </SelectRandom>
  </Units>
</DestroyUnits>
```

Destroyed objects are deleted from game state. `AddToVoid` instead retains a faction-owned node
outside the active galaxy, and `RemoveFromVoid` attempts to restore it to its previous valid
parent. A void pool records no status or reason.

## Actions

Actions execute in authored order, and later actions can consume results produced earlier in the
same activation. Principal actions include:

- Composite: `If` with `Conditions`, `Actions`, and `Else`; and weighted `Random` outcomes with
  optional `When` guards.
- Event state: `SetEventVariable`.
- Units: `DestroyUnits`, `RequestMovement`, `AddToVoid`, and `RemoveFromVoid`.
- Officers: `SetCaptureStatus`, `SetOfficerStatus`, `AdjustOfficerStat`,
  `SetOfficerJediState`, `ApplyOfficerInjury`, `SetOfficerImageSet`, `SetOfficerVoiceSet`, and
  `TriggerDuel`.
- Planets and intelligence: `AdjustPlanetStat`, `ReducePlanetStats`, `RecordPlanetIncident`, and
  `RevealToFaction`.
- Missions and messages: `CreateMission`, `SendMessage`, and
  `SuppressNextAutomaticMessage`.

`AdjustOfficerStat` and `AdjustPlanetStat` each require exactly one signed calculation mode, such
as `Amount`, `PercentOfBase`, or `PercentOfCurrent` where supported. Their matching comparators use
the same closed stat vocabulary. The schema is authoritative for required attributes, children,
and nesting.

## Messages

`SendMessage` keeps presentation payloads explicit:

```xml
<SendMessage SubjectInstanceID="LUKE_SKYWALKER" Type="Mission">
  <Subject>Luke Returns</Subject>
  <Body>Luke has completed his training.</Body>
  <BackgroundImage Path="Pack/Shared/Events/MessageBackgrounds/luke-returns"/>
  <OverlayImage Path="Pack/Factions/Alliance/Units/Officers/OFAL003/message"/>
  <BackgroundAudio Path="Pack/Factions/Alliance/Strategy/Audio/Messages/message-faction-report"/>
  <OfficerVoice Preset="MissionSuccess"/>
  <AdvisorNotification Preset="SubjectReport"/>
</SendMessage>
```

Media references accept the schema-defined `Key`, `Path`, `Binding`, or `Preset` forms. Advisor
metadata remains transient delivery state rather than persisted message state.
`SuppressNextAutomaticMessage` removes one automatic candidate matching its source result,
message result type, and optional recipient; it never suppresses authored messages.

## Validation and compatibility

`Content/Application/Schemas/game-events.xsd` validates the catalog while the pack loads. Run the
content repository's `./build.sh` before distribution. Event IDs and variable keys become save
state, so keep them stable after release and test both a new campaign and a save/load cycle when
changing event content.
