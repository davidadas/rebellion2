# Creating custom game events

Game events combine four independent pieces of data:

- A schedule or one or more typed result triggers decide when the event is evaluated.
- Conditions decide whether the current game state permits execution.
- An optional target supplies a planet to target-aware actions.
- Actions change game state and emit ordinary game results.

The event catalog is referenced by `GameEventsPath` in the content pack's `pack.xml`. The standard
pack stores it at `Shared/Data/game-events.xml`. Every event needs a stable, unique `InstanceID`.
`DisplayName` is for diagnostics and tooling; messages provide their own player-facing subject.

## Scheduling

`Schedule` accepts exactly one mode:

```xml
<Schedule><At Tick="200"/></Schedule>
<Schedule><Every Ticks="50" InitialDelayTicks="10"/></Schedule>
<Schedule><Random MinimumTicks="25" MaximumTicks="75"/></Schedule>
<Schedule><After EventInstanceID="MOD_PREDECESSOR" DelayTicks="20"/></Schedule>
```

`At` uses an absolute campaign tick. `After` starts after the named event completes. `Every` and
`Random` repeat after each successful execution unless the event declares `RunsOnce="true"`.
Events without a schedule are immediately eligible. Failed conditions leave an event pending.

## Typed result triggers and bindings

Use `Triggers` to react to simulation results. Each trigger declares which result values should be
published and the binding name each value receives:

```xml
<GameEvent>
  <DisplayName>Arrival Ceremony</DisplayName>
  <InstanceID>MOD_ARRIVAL_CEREMONY</InstanceID>
  <Triggers>
    <UnitArrived Unit="unit" Destination="destination"/>
  </Triggers>
  <Conditionals>
    <EvaluateBinding Name="unit" Comparison="Equal" Value="MON_MOTHMA"/>
    <EvaluateBinding Name="destination" Comparison="Equal" Value="CHANDRILA"/>
  </Conditionals>
  <Actions>
    <SendMessage RecipientFactionInstanceID="FNALL1" SubjectInstanceID="MON_MOTHMA"
                 LocationInstanceID="CHANDRILA" Type="Mission">
      <Subject>Mon Mothma Arrives</Subject>
      <Body>Mon Mothma has arrived on Chandrila.</Body>
      <AdvisorNotification Preset="SubjectReport"/>
    </SendMessage>
  </Actions>
</GameEvent>
```

Supported typed triggers are `UnitArrived`, `MissionCompleted`, `DuelCompleted`,
`OfficerCaptureChanged`, and `ForceDiscovered`. Their allowed bindings are declared directly in
`game-events.xsd`; no reflection or string event registry is involved. A result-triggered event
reacts to every match unless it declares `RunsOnce="true"`.

## Conditions

Sibling conditions are an implicit AND. Composite conditions contain their children directly:

```xml
<Conditionals>
  <IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
  <Not><IsOnMission UnitInstanceID="LEIA_ORGANA"/></Not>
  <Any>
    <IsInjured OfficerInstanceID="LEIA_ORGANA"/>
    <IsCaptured OfficerInstanceID="LEIA_ORGANA"/>
  </Any>
</Conditionals>
```

`IsOwned` requires the explicit planet or planet binding to have a non-neutral faction owner.
Adding `FactionInstanceID` requires that specific owner. Other conditions include `All`, `Any`,
`Not`, `Xor`, `AreOnSamePlanet`, `AreOnOpposingFactions`, `AreOnPlanet`, `IsAtLocation`,
`IsOnMission`, `IsInTransit`, `IsCaptured`, `IsKilled`, `IsInjured`, `IsForceEligible`,
`IsCapturedBy`, `HasForceRank`, `IsEventComplete`, `EvaluateEventVariable`, and
`EvaluateBinding`.

## Targets and selectors

Targets establish the scope used by target-aware actions:

```xml
<Target><Planet InstanceID="NABOO"/></Target>
<Target><RandomPlanets Count="1" SystemType="CoreSystem"/></Target>
<Target><EachPlanet/></Target>
```

`EachPlanet` maintains independent persisted schedule state for every surviving planet.
`RandomPlanets` currently requires `Count="1"`; the schema rejects unsupported counts instead of
silently applying incomplete behavior.

Unit actions use reusable selectors. `SelectUnits` returns every match, while
`SelectRandomUnits` applies probability and count limits to the union of its queries:

```xml
<DestroyUnits>
  <SelectRandomUnits ChancePercent="25" MinimumCount="1" MaximumCount="3">
    <SelectUnits PlanetInstanceID="NABOO" UnitCategory="PlanetaryDefense"/>
    <SelectUnits PlanetInstanceID="NABOO" UnitCategory="Regiment"/>
  </SelectRandomUnits>
</DestroyUnits>
```

Destroyed units enter their owning faction's void pool with a `Destroyed` status. They remain
registered for save data and historical references but are no longer attached to active play.

## Actions

Actions run in authored order. Later actions see state and results produced earlier in the same
event activation. The principal actions are:

- Composite: `If` and weighted `Random` outcomes.
- Event state: `SetEventVariable`.
- Units: `DestroyUnits`, `RequestMovement`, `AddToVoid`, `SetMissionReturnDestination`,
  `SetStatus`, and `ActivateFromVoid`.
- Officers: `SetCaptivity`, `AdjustOfficerRating`, `SetOfficerJediState`,
  `ApplyOfficerInjury`, `SetOfficerImages`, `SetOfficerVoiceSet`, and `TriggerDuel`.
- Resources and intelligence: `AdjustPlanetResource`, `ReduceResources`, and
  `GatherInformantIntelligence`.
- Missions and messages: `CreateMission`, `SendMessage`, and
  `SuppressNextAutomaticMessage`.

The schema is the authoritative list of required attributes, child elements, and allowed nesting.

## Messages

`SendMessage` uses `Subject` and `Body`. Media is explicit and consistently structured:

```xml
<SendMessage SubjectInstanceID="LUKE_SKYWALKER" Type="Mission">
  <Subject>Luke Returns</Subject>
  <Body>Luke has completed his training.</Body>
  <BackgroundImage Path="Pack/Shared/Events/MessageBackgrounds/luke-returns"/>
  <OverlayImage Path="Pack/Factions/Alliance/Units/Officers/OFAL003/message"/>
  <AmbientAudio Path="Pack/Factions/Alliance/Strategy/Audio/Messages/message-faction-report"/>
  <OfficerVoice Preset="MissionSuccess"/>
  <AdvisorNotification Preset="SubjectReport"/>
</SendMessage>
```

`BackgroundImage` accepts either a theme `Key` or an explicit content `Path`; an explicit path wins
if both are supplied. `OfficerVoice` similarly accepts either an explicit `Path` or a voice-set
`Preset`, with the explicit path taking precedence. Advisor animation overrides belong to the
transient delivery presentation and are never persisted on `Message`.

## Definition-backed missions

Reusable event missions live in the catalog referenced by `MissionDefinitionsPath`. Definitions
own duration, cancellation policy, and success rules. Events create an instance with the normal
target, participant, and decoy collections:

```xml
<CreateMission MissionDefinitionID="MOD_OFFICER_CAPTURE">
  <Target UnitInstanceID="HAN_SOLO"/>
  <Participants>
    <Participant UnitInstanceID="BOBA_FETT"/>
  </Participants>
</CreateMission>
```

Mission instances persist their definition ID and concrete assignments, then reconnect to the
content definition after loading. Mission completion emits a normal `MissionCompletedResult`; a
separate result-triggered event applies story-specific consequences.

## Validation and compatibility

`Content/Application/Schemas/game-events.xsd` validates the catalog while the pack loads. Run the
content repository's `./build.sh` before distribution. Event instance IDs and variable keys become
save-game state, so keep them stable after release and test both a new campaign and a save/load
cycle when changing event content.
