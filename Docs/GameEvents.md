# Creating custom game events

Game events compose reusable schedules, result triggers, conditions, selectors, and actions. This
guide explains how those pieces execute. The generated [XML API reference](GameEvents.API.md)
lists every accepted element, attribute, payload type, trigger argument, and enum value.

The content pack's `pack.xml` identifies the event catalog through `GameEventsPath`. The standard
catalog is `Packs/ClassicGalacticCivilWar/Shared/Data/game-events.xml`, and its root is:

```xml
<GameEvents>
  <GameEvent TriggerCount="1">
    <InstanceID>MOD_EVENT_ID</InstanceID>
    <!-- schedule or triggers, conditions, target, and actions -->
  </GameEvent>
</GameEvents>
```

Every `InstanceID` must be unique and stable. It is runtime and save-state identity, not a
player-facing title. Put text shown to the player in `SendMessage`.

## Execution lifecycle

An event becomes eligible in one of two ways:

- A scheduled event is checked at its scheduled campaign tick.
- A triggered event is checked whenever a matching game result is processed.

An event cannot have both `Schedule` and `Triggers`. An event with neither is eligible from tick
zero and is checked every tick. When eligible, the runtime resolves its optional `Target`, evaluates
all top-level `Conditionals` as an implicit AND, and executes `Actions` in authored order.

Omitting `TriggerCount` permits unlimited activations. `TriggerCount="1"` permits one activation;
any other positive value sets that exact upper limit. The count advances only when the event reaches
its actions. `Until` permanently exhausts the event as soon as all of its conditions are true,
regardless of the remaining trigger count.

Event schedules, activation counts, exhaustion, and event variables are saved. Preserve event IDs
and variable keys after publishing a content pack.

## Scheduling

`Schedule` accepts exactly one mode:

```xml
<Schedule><At Tick="200"/></Schedule>
<Schedule><Every Ticks="50" InitialDelayTicks="10"/></Schedule>
<Schedule><Random MinimumTicks="25" MaximumTicks="75"/></Schedule>
<Schedule><After EventInstanceID="MOD_PREDECESSOR" DelayTicks="20"/></Schedule>
<Schedule>
  <AfterAll DelayTicks="20">
    <Events>
      <Event EventInstanceID="MOD_FIRST_PREDECESSOR"/>
      <Event EventInstanceID="MOD_SECOND_PREDECESSOR"/>
    </Events>
  </AfterAll>
</Schedule>
```

`At` is an absolute campaign tick and can activate only once. `Every` uses a fixed recurring delay.
`Random` rolls a new inclusive delay after each activation. `After` waits for one event. `AfterAll`
waits for every listed event and anchors its delay to the most recent activation; `AfterAny` anchors
to the first listed event that activates. Non-recurring schedules require `TriggerCount="1"`.

## Result triggers and bindings

A trigger reacts to an existing simulation result. `Bind` gives selected result arguments local
names; references to those names begin with `$`:

```xml
<GameEvent>
  <InstanceID>MOD_EMPEROR_REACHES_CORUSCANT</InstanceID>
  <Triggers>
    <Trigger Event="core:unit.arrived">
      <Bindings>
        <Bind Argument="UnitInstanceID" As="unitInstanceID"/>
        <Bind Argument="DestinationInstanceID" As="destinationInstanceID"/>
      </Bindings>
    </Trigger>
  </Triggers>
  <Conditionals>
    <EvaluateBinding Binding="$unitInstanceID" Comparison="Equal"
                     CompareTo="EMPEROR_PALPATINE"/>
    <EvaluateBinding Binding="$destinationInstanceID" Comparison="Equal"
                     CompareTo="CORUSCANT"/>
  </Conditionals>
  <Actions>
    <SendMessage RecipientFactionInstanceID="FNEMP1"
                 SubjectInstanceID="EMPEROR_PALPATINE"
                 LocationInstanceID="CORUSCANT"
                 Type="Mission">
      <Subject>Emperor Arrives at Coruscant</Subject>
      <Body>The Emperor has returned to Coruscant.</Body>
      <AdvisorNotification Preset="SubjectReport"/>
    </SendMessage>
  </Actions>
</GameEvent>
```

Multiple triggers are alternatives: any matching result may activate the event. All triggers on one
event must expose the same aliases with compatible argument types. The generated reference lists
the arguments offered by each trigger contract.

Bindings retain their runtime type. Use scalar bindings with `EvaluateBinding`; use object and
collection bindings through compatible conditions or `SelectBinding`. The runtime validates unknown
arguments, missing aliases, duplicate aliases, and incompatible bindings while loading the catalog.

## Conditions

Siblings inside `Conditionals`, `Until`, `When`, or `Conditions` are an implicit AND. `All`, `Any`,
`Not`, and `Xor` provide explicit boolean composition:

```xml
<Conditionals>
  <IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
  <Not>
    <Conditionals>
      <Any>
        <Conditionals>
          <IsCaptured OfficerInstanceID="LEIA_ORGANA"/>
          <IsOnMission UnitInstanceID="LEIA_ORGANA"/>
          <IsInTransit UnitInstanceID="LEIA_ORGANA"/>
        </Conditionals>
      </Any>
    </Conditionals>
  </Not>
</Conditionals>
```

`ShareParent` requires the exact same immediate parent. `ShareAncestor` compares the nearest ancestor
of the requested scene-node type. `HasEventTriggered` means an event has activated at least once;
`IsEventExhausted` means its trigger count or `Until` condition prevents future activation.

## Targets and selectors

`Target` must resolve exactly one scene node and exposes it as `$target` for that activation:

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

Selectors return typed collections. Filters narrow a selector; wrapper selectors combine or reshape
their candidates:

- `SelectRandom` deduplicates candidates, orders them by instance ID for deterministic selection,
  applies `ChancePercent`, and then applies its count bounds.
- `SelectFirst` returns the first destination candidate.
- `SelectBinding` returns a bound object or collection.
- `SelectAncestors` returns the requested ancestor type for its candidates.
- `SelectPreviousLocation` returns the location recorded when a retained unit left active play.

Each action admits only compatible selector types in the schema. Dynamic bindings receive an
additional runtime type check.

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

## Actions and control flow

Actions execute in authored order. `If` evaluates one branch. `Random` first discards outcomes whose
optional `When` conditions fail, then selects one remaining outcome by weight:

```xml
<Random>
  <Outcomes>
    <Outcome Weight="30">
      <Actions>
        <ChangePlanetStat PlanetBinding="$target" Stat="RawResourceNodes">
          <Amount>1</Amount>
        </ChangePlanetStat>
      </Actions>
    </Outcome>
    <Outcome Weight="70">
      <Actions>
        <SendMessage LocationBinding="$target" Type="Resource">
          <Subject>No discovery</Subject>
          <Body>No useful deposits were found.</Body>
        </SendMessage>
      </Actions>
    </Outcome>
  </Outcomes>
</Random>
```

`PerformSkillCheck` similarly executes its nested `OnSuccess` or `OnFailure` actions immediately; it
does not publish a separate result that another event must catch.

`ChangeOfficerRating` supports signed `Amount`, `PercentOfStored`, `PercentOfEffective`, or
`PercentOfPositiveGap`. `IncreaseOfficerForce` uses the same positive calculation modes but cannot
decrease Force growth. Exactly one calculation mode is required. `ChangePlanetStat` accepts either
signed `Amount` or `PercentOfCurrent`.

`PlaceUnits` immediately reparents units to a valid destination. `SendUnits` uses normal movement and
transit rules. `AddToVoid` records the previous location, detaches the unit, and retains it outside
active play. `RemoveFromVoid` only releases that retention; it does not choose or restore a parent.
Compose it with `PlaceUnits` and `SelectPreviousLocation` when restoration is desired:

```xml
<RemoveFromVoid UnitInstanceID="LUKE_SKYWALKER"/>
<PlaceUnits UnitInstanceID="LUKE_SKYWALKER">
  <Destination>
    <SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
  </Destination>
</PlaceUnits>
```

## Messages

Messages keep gameplay references separate from optional presentation:

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

The recipient may be a faction or a unit. Subject and location may be supplied by instance ID or a
compatible binding. Media fields accept the `Key`, `Path`, `Binding`, or `Preset` forms allowed by
their generated payload definition. `SendMessage` requests authored delivery directly; automatic
messages remain the responsibility of the gameplay result pipeline.

## Generate and validate the reference

The reference is generated from two authoritative sources:

- `Assets/Content/Application/Schemas/game-events.xsd` supplies XML elements, nesting, attributes,
  occurrence rules, constraints, and enum values.
- `Assets/Scripts/Game/Events/GameEventTrigger.cs` supplies registered trigger IDs, result types, and
  trigger arguments.

Run `./build.sh docs` after changing either contract. `./build.sh lint` fails when the committed
reference is stale. The schema and runtime catalog validation remain authoritative for semantic
rules that XSD cannot express, such as exclusive modes, compatible bindings, and valid references.
