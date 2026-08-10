# Creating custom game events

Game events combine four independent pieces of data:

- A schedule or simulation-result trigger decides when the event is evaluated.
- Conditions decide whether the current game state permits it to execute.
- An optional target selects the planet used by target-aware actions.
- Actions change game state and emit results that can activate other events or produce messages.

Events are stored in the XML file referenced by `GameEventsPath` in the content pack's `pack.xml`.
The standard pack uses `Shared/Data/game-events.xml`. Every event needs a unique `InstanceID`.

## Minimal event

This event runs once at campaign tick 100:

```xml
<GameEvents>
  <GameEvent>
    <DisplayName>Anniversary Supplies</DisplayName>
    <InstanceID>MOD_ANNIVERSARY_SUPPLIES</InstanceID>
    <Schedule>
      <At Tick="100"/>
    </Schedule>
    <Actions>
      <AddMessage>
        <RecipientFactionInstanceID>FNALL1</RecipientFactionInstanceID>
        <MessageType>Resource</MessageType>
        <Title>Anniversary Supplies</Title>
        <Body>Additional supplies have arrived.</Body>
      </AddMessage>
    </Actions>
  </GameEvent>
</GameEvents>
```

`DisplayName` is used for diagnostics and tooling; it is not a player-facing message title. An
event without `IsRepeatable` is removed after its first successful execution. Failed conditions
leave the event pending.

## Scheduling

`Schedule` accepts exactly one mode:

```xml
<Schedule>
  <At Tick="200"/>
</Schedule>
```

`At` supplies an absolute campaign tick and cannot repeat.

```xml
<IsRepeatable>true</IsRepeatable>
<Schedule>
  <Every Ticks="50" InitialDelayTicks="10"/>
</Schedule>
```

`Every` waits for the optional initial delay and then uses the fixed interval after each successful
execution.

```xml
<IsRepeatable>true</IsRepeatable>
<Schedule>
  <Random MinimumTicks="25" MaximumTicks="75"/>
</Schedule>
```

`Random` selects an inclusive delay in the authored range. A repeatable event rolls a new delay
after every successful execution. All event randomness uses the saved simulation random stream.

`ScheduleEvent` can arm another pending event relative to the current tick:

```xml
<ScheduleEvent>
  <EventInstanceID>MOD_FOLLOW_UP</EventInstanceID>
  <DelayTicks>20</DelayTicks>
</ScheduleEvent>
```

## Result-triggered events

Use `Trigger` instead of `Schedule` to react to a simulation result. Set `IsRepeatable` to `true`
when the event must react to every match rather than only the first.

```xml
<GameEvent>
  <DisplayName>Arrival Ceremony</DisplayName>
  <InstanceID>MOD_ARRIVAL_CEREMONY</InstanceID>
  <IsRepeatable>true</IsRepeatable>
  <Trigger>core:unit.arrived</Trigger>
  <Conditionals>
    <UnitArrival>
      <UnitInstanceID>MON_MOTHMA</UnitInstanceID>
      <DestinationInstanceID>CHANDRILA</DestinationInstanceID>
    </UnitArrival>
  </Conditionals>
  <Actions>
    <AddMessage>
      <RecipientFactionInstanceID>FNALL1</RecipientFactionInstanceID>
      <SubjectInstanceID>MON_MOTHMA</SubjectInstanceID>
      <LocationInstanceID>CHANDRILA</LocationInstanceID>
      <MessageType>Mission</MessageType>
      <Title>Mon Mothma Arrives</Title>
      <Body>Mon Mothma has arrived on Chandrila.</Body>
      <AdvisorCue>SubjectReport</AdvisorCue>
    </AddMessage>
  </Actions>
</GameEvent>
```

Stable core triggers are:

- `core:dagobah.completed`
- `core:force.discovered`
- `core:mission.completed`
- `core:officer.capture-changed`
- `core:officer.encountered`
- `core:story-capture.resolved`
- `core:story-final-battle.completed`
- `core:story-pickup.completed`
- `core:unit.arrived`

`SuppressTriggerMessage` suppresses the automatic message for the exact activating result.
`SuppressSourceMessages` suppresses all automatic messages emitted by the same source event. Use
these only when the event supplies a deliberate replacement report.

## Conditions

Sibling conditions are an implicit AND. Logical groups contain their own `Conditionals` collection:

```xml
<Conditionals>
  <IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
  <Not>
    <Conditionals>
      <IsOnMission Value="LEIA_ORGANA"/>
    </Conditionals>
  </Not>
</Conditionals>
```

`IsOwned` requires the named planet to have an owner. When `FactionInstanceID` is present, that
specific faction must own it. Omitting the faction matches any non-neutral owner.

General conditions include `And`, `Or`, `Not`, `Xor`, `AreOnSamePlanet`,
`AreOnOpposingFactions`, `IsOnMission`, `IsMovable`, `AreOnPlanet`, `TickCount`,
`IsEventComplete`, `EventVariable`, `IsAtLocation`, and `IsOwned`. Result-triggered events can also
use `OfficerEncounterParticipants`, `OfficerPairArrival`, `UnitArrival`, `OfficerCaptureState`,
`ResultSourceEvent`, `StoryCaptureOutcome`, `StoryPickupCollector`, and
`StoryFinalBattleOutcome`. Officer checks include `OfficerState`, `OfficerCaptor`, and
`OfficerForceRank`.

## Planet scopes and targets

Use `EachPlanet` when every eligible planet needs an independent persisted schedule:

```xml
<Scope>EachPlanet</Scope>
<PlanetScopeOwnership>Owned</PlanetScopeOwnership>
<PlanetScopeSystemType>CoreSystem</PlanetScopeSystemType>
<FilterPlanetScopeSystemType>true</FilterPlanetScopeSystemType>
```

`PlanetScopeOwnership` accepts `Any`, `Owned`, or `Neutral`. A planet that stops qualifying has its
schedule disarmed and receives a fresh schedule if it becomes eligible again.

A global event can select one explicit or random target:

```xml
<Target>
  <Planet InstanceID="NABOO"/>
</Target>
```

```xml
<Target>
  <RandomPlanets Count="1" SystemType="CoreSystem"/>
</Target>
```

`RandomPlanets` currently requires `Count="1"`. Destroyed planets are never selected. Target-aware
actions share the selected planet for the complete execution.

## Actions

Actions execute in authored order. Later actions observe state and results produced earlier in the
same execution. Available actions are grouped below:

- Flow: `Conditional`, `Chance`, `RandomChoice`, `RandomOutcome`, `TriggerEvent`, and
  `ScheduleEvent`.
- State: `SetEventVariable`, `RequestMovement`, `AddToVoid`, `SetStatus`, `ReturnFromVoid`,
  `UpdateOfficerPresentation`, `RevealOfficerForcePotential`, `AddForceExperience`,
  `IncreaseOfficerForce`, and `ApplyOfficerInjury`.
- Planet incidents: `InformantIntelligence`, `ChangeResources`, `ReduceResources`, and
  `DestroyUnits`.
- Encounters and story sequences: `TriggerDuel`, `ReportForceDetection`, `StartStoryCapture`,
  `BountyAttack`, `StartStoryRescue`, `StartStoryPickup`, and `StartStoryFinalBattle`.
- Presentation: `AddMessage`.

The schema is the authoritative list of fields and allowed nesting for each action. Prefer small,
composable actions over embedding unrelated state changes in one action.

## Recurring planet incident

This event independently rolls every 100–200 ticks for each owned core-system planet and damages
eligible defenses:

```xml
<GameEvent>
  <DisplayName>Wildlife Attacks Planetary Defenses</DisplayName>
  <InstanceID>MOD_WILDLIFE_ATTACK</InstanceID>
  <IsRepeatable>true</IsRepeatable>
  <Scope>EachPlanet</Scope>
  <PlanetScopeOwnership>Owned</PlanetScopeOwnership>
  <PlanetScopeSystemType>CoreSystem</PlanetScopeSystemType>
  <FilterPlanetScopeSystemType>true</FilterPlanetScopeSystemType>
  <Schedule>
    <Random MinimumTicks="100" MaximumTicks="200"/>
  </Schedule>
  <Actions>
    <DestroyUnits ChancePerUnit="0.25" MinimumCount="1" MaximumCount="3">
      <Candidates>
        <Buildings>
          <BuildingTypes>
            <BuildingType>Defense</BuildingType>
            <BuildingType>Weapon</BuildingType>
          </BuildingTypes>
        </Buildings>
        <Regiments/>
      </Candidates>
    </DestroyUnits>
  </Actions>
</GameEvent>
```

## Validation and compatibility

`Content/Application/Schemas/game-events.xsd` validates the catalog when the pack loads. Run the
content repository's `./build.sh` before distributing a pack. Malformed XML, invalid nesting,
missing required fields, and invalid numeric ranges should fail before runtime.

Event instance IDs and variable keys become save-game state. Keep them stable after release.
Removing or renaming an event can invalidate a pending follow-up or discard scheduling history.
Increment the content pack version for incompatible changes, then test a new campaign and a
save/load cycle.
