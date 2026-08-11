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
      <SendMessage RecipientFactionInstanceID="FNALL1" Type="Resource">
        <Title>Anniversary Supplies</Title>
        <Body>Additional supplies have arrived.</Body>
      </SendMessage>
    </Actions>
  </GameEvent>
</GameEvents>
```

`DisplayName` is used for diagnostics and tooling; it is not a player-facing message title.
`At` and `After` schedules inherently run once. Other events repeat unless `RunsOnce="true"` is
set. Failed conditions leave an event pending.

## Scheduling

`Schedule` accepts exactly one mode:

```xml
<Schedule>
  <At Tick="200"/>
</Schedule>
```

`At` supplies an absolute campaign tick and cannot repeat.

```xml
<Schedule>
  <Every Ticks="50" InitialDelayTicks="10"/>
</Schedule>
```

`Every` waits for the optional initial delay and then uses the fixed interval after each successful
execution.

```xml
<Schedule>
  <Random MinimumTicks="25" MaximumTicks="75"/>
</Schedule>
```

`Random` selects an inclusive delay in the authored range and rolls a new delay after every
successful execution. Set `RunsOnce="true"` on the `GameEvent` to prevent another roll. All event
randomness uses the saved simulation random stream.

`After` schedules an event relative to the successful execution of another event:

```xml
<Schedule>
  <After EventInstanceID="MOD_PREDECESSOR" DelayTicks="20"/>
</Schedule>
```

## Result-triggered events

Use `Trigger` instead of `Schedule` to react to a simulation result. Result-triggered events react
to every match unless the `GameEvent` has `RunsOnce="true"`.

```xml
<GameEvent>
  <DisplayName>Arrival Ceremony</DisplayName>
  <InstanceID>MOD_ARRIVAL_CEREMONY</InstanceID>
  <Trigger>core:unit.arrived</Trigger>
  <Conditionals>
    <UnitArrived UnitInstanceID="MON_MOTHMA" DestinationInstanceID="CHANDRILA"/>
  </Conditionals>
  <Actions>
    <SendMessage RecipientFactionInstanceID="FNALL1" SubjectInstanceID="MON_MOTHMA" LocationInstanceID="CHANDRILA" Type="Mission">
      <Title>Mon Mothma Arrives</Title>
      <Body>Mon Mothma has arrived on Chandrila.</Body>
      <AdvisorNotification Preset="SubjectReport"/>
    </SendMessage>
  </Actions>
</GameEvent>
```

Stable core triggers are:

- `core:dagobah.completed`
- `core:force.discovered`
- `core:mission.completed`
- `core:officer.capture-changed`
- `core:officer.encountered`
- `core:officer.capture-attempted`
- `core:force-confrontation.completed`
- `core:prisoner-pickup.completed`
- `core:unit.arrived`

`SuppressNextMessage` suppresses one automatic message of the authored result type and optional
recipient. Authored `SendMessage` actions are never suppressed.

## Conditions

Sibling conditions are an implicit AND. Logical groups contain their own `Conditionals` collection:

```xml
<Conditionals>
  <IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
  <Not>
    <Conditionals>
      <IsOnMission UnitInstanceID="LEIA_ORGANA"/>
    </Conditionals>
  </Not>
</Conditionals>
```

`IsOwned` requires the named planet to have an owner. When `FactionInstanceID` is present, that
specific faction must own it. Omitting the faction matches any non-neutral owner.

General conditions include `And`, `Or`, `Not`, `Xor`, `AreOnSamePlanet`,
`AreOnOpposingFactions`, `IsOnMission`, `IsMovable`, `AreOnPlanet`, `TickCount`,
`IsEventComplete`, `EventVariable`, `IsAtLocation`, and `IsOwned`. Result-triggered events can also
use `DuelIncludes`, `MissionIncludes`, `UnitArrived`, `OfficerCaptured`, `TriggeredBy`,
`CaptureFailed`, `PrisonerPickupCollector`, and `ForceConfrontationOutcome`. Officer checks include
`OfficerState`, `OfficerCaptor`, and `OfficerForceRank`.

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

- Flow: `Conditional`, `Chance`, `RandomChoice`, `RandomOutcome`, and `TriggerEvent`.
- State: `SetEventVariable`, `RequestMovement`, `AddToVoid`, `SetStatus`, `ReturnFromVoid`,
  `SetOfficerImages`, `SetOfficerVoiceSet`, `RevealOfficerForcePotential`, `AddForceExperience`,
  `AdjustOfficerRating`, `IncreaseOfficerForce`, and `ApplyOfficerInjury`.
- Planet incidents: `InformantIntelligence`, `ChangeResources`, `ReduceResources`, and
  `DestroyUnits`.
- Encounters and missions: `TriggerDuel`, `BountyAttack`, and `Mission`.
- Presentation: `SendMessage` and `SuppressNextMessage`.

The schema is the authoritative list of fields and allowed nesting for each action. Prefer small,
composable actions over embedding unrelated state changes in one action.

## Definition-backed missions

Reusable event missions live in the XML file referenced by `MissionDefinitionsPath` in
`pack.xml`. A mission definition owns its duration, cancellation policy, and resolution rules.
Events start one with a concrete target and the normal main and decoy participant groups:

```xml
<Mission MissionDefinitionID="MOD_OFFICER_CAPTURE">
  <Target UnitInstanceID="HAN_SOLO"/>
  <MainParticipants>
    <Participant UnitInstanceID="BOBA_FETT"/>
  </MainParticipants>
</Mission>
```

The standard pack defines officer capture, officer rescue, prisoner pickup, and the two-stage
final confrontation in `Shared/Data/mission-definitions.xml`. Mission instances persist only the
definition ID, target, and participant IDs, then reconnect to the pack definition after loading. This keeps
event chains small while preserving normal mission travel, timing, completion, and save behavior.

Player-facing message templates live separately in the XML file referenced by
`MessageDefinitionsPath`. They use `Subject` and `Body`; an optional `BackgroundImage` contains
exactly one `Key` or `Path`.

## Recurring planet incident

This event independently rolls every 100–200 ticks for each owned core-system planet and damages
eligible defenses:

```xml
<GameEvent>
  <DisplayName>Wildlife Attacks Planetary Defenses</DisplayName>
  <InstanceID>MOD_WILDLIFE_ATTACK</InstanceID>
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
