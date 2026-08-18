# Creating Game Events

## Add an Event

Open the standard game-event catalog:

```text
Assets/Content/Packs/ClassicGalacticCivilWar/Shared/Data/game-events.xml
```

Add the event inside the existing `GameEvents` root:

```xml
<GameEvent TriggerCount="1">
  <InstanceID>MY_FIRST_EVENT</InstanceID>
  <Schedule>
    <At Tick="10"/>
  </Schedule>
  <Actions>
    <SendMessage RecipientFactionInstanceID="FNALL1" Type="Advice">
      <Subject>My First Event</Subject>
      <Body>The event is working.</Body>
    </SendMessage>
  </Actions>
</GameEvent>
```

`InstanceID` is the event's permanent runtime and save-game identity. Keep it unique and do not
rename it after saves contain the event. Player-facing names belong in messages.

## Available Options

An event may contain these pieces:

| Option | Purpose |
| --- | --- |
| `Schedule` | Runs according to campaign time or another event. |
| `Triggers` | Reacts to a gameplay result such as an arrival or completed mission. |
| `Conditionals` | Prevents the event from running unless every listed condition passes. |
| `Until` | Permanently stops the event once every listed condition passes. |
| `Target` | Selects exactly one node and makes it available as `$target`. |
| `Actions` | Changes the game in authored order. |
| `TriggerCount` | Limits activations to a positive number. If omitted, activations are unlimited. |

Use either `Schedule` or `Triggers`, never both. With neither, the event is checked every tick.

**Schedules**

```xml
<!-- At an absolute tick. -->
<Schedule><At Tick="200"/></Schedule>

<!-- Every 50 ticks, after an initial 10-tick delay. -->
<Schedule><Every Ticks="50" InitialDelayTicks="10"/></Schedule>

<!-- After a newly rolled delay of 25–75 ticks. -->
<Schedule><Random MinimumTicks="25" MaximumTicks="75"/></Schedule>

<!-- After another event. -->
<Schedule><After EventInstanceID="EVENT_A" DelayTicks="20"/></Schedule>

<!-- After all listed events. Use AfterAny to wait for the first one instead. -->
<Schedule>
  <AfterAll DelayTicks="20">
    <Events>
      <Event EventInstanceID="EVENT_A"/>
      <Event EventInstanceID="EVENT_B"/>
    </Events>
  </AfterAll>
</Schedule>
```

`At`, `After`, `AfterAll`, and `AfterAny` are one-time schedules and require
`TriggerCount="1"`. `Every` and `Random` can repeat. `TriggerCount="5"` permits five activations;
omitting it permits unlimited activations.

Use `Until` when game state decides when a repeating event ends:

```xml
<Until>
  <IsCapturedBy OfficerInstanceID="HAN_SOLO" CaptorFactionInstanceID="FNEMP1"/>
</Until>
```

**Gameplay triggers and bindings**

Triggers expose result information through named bindings. Binding references begin with `$`.

```xml
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
```

| Trigger | Available arguments |
| --- | --- |
| `core:unit.arrived` | `Unit`, `UnitInstanceID`, `Destination`, `DestinationInstanceID`, `SourceEventInstanceID` |
| `core:mission.completed` | `Mission`, `Outcome`, `CompletionReason`, `Participants`, `Location`, `ReturnDestination`, `SourceEventInstanceID` |
| `core:duel.completed` | `Officer`, `OfficerInstanceID`, `Opponent`, `OpponentInstanceID`, `Location`, `OfficerCaptured`, `OfficerInjury`, `OpponentInjury`, `ImagePath`, `AudioPath`, `SourceEventInstanceID` |
| `core:officer.capture-changed` | `Officer`, `OfficerInstanceID`, `LinkedOfficer`, `Context`, `IsCaptured`, `SourceEventInstanceID` |
| `core:force.discovered` | `Officer`, `Discoverer`, `ForceRank`, `SourceEventInstanceID` |

Multiple triggers are alternatives. Each trigger on one event must expose the same aliases with
compatible types.

**Conditions**

Sibling conditions are ANDed. Use `Any` for OR, `Not` for negation, `All` for an explicit AND, and
`Xor` when exactly one nested condition must pass:

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

| Condition | What it checks |
| --- | --- |
| `All`, `Any`, `Not`, `Xor` | Nested boolean logic. |
| `TickCount` | Current tick using `Comparison` and `Ticks`. |
| `HasEventTriggered`, `IsEventExhausted` | Whether another event has run or can ever run again. |
| `EvaluateEventVariable` | A saved integer using `Key`, `Comparison`, and `CompareTo`. |
| `EvaluateBinding`, `BindingIncludesUnit` | A scalar binding or collection binding. |
| `IsOwned`, `RollAgainstPopularSupport` | Planet ownership or a support roll for a faction. |
| `IsAtLocation`, `ShareParent`, `ShareAncestor` | Unit location and scene relationships. |
| `AreOnOpposingFactions` | Whether listed units belong to opposing factions. |
| `IsOnMission`, `IsInTransit` | Unit activity. |
| `IsCaptured`, `IsCapturedBy`, `IsKilled`, `IsInjured` | Officer state. |
| `IsForceEligible`, `HasForceRank` | Force eligibility or configured rank label. |
| `CompareOfficerRating`, `CompareOfficerForce` | Numeric officer values. |
| `ComparePlanetStat`, `HasBuildingType` | Planet stats and facilities. |

Comparisons are `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, and
`LessThanOrEqual`. Officer ratings are `Diplomacy`, `Espionage`, `Combat`, `Leadership`,
`ShipResearch`, `TroopResearch`, and `FacilityResearch`. Planet stats are `RawResourceNodes` and
`EnergyCapacity`. Force ranks are `None`, `Novice`, `Trainee`, `ForceStudent`, `ForceKnight`, and
`ForceMaster`.

`ShareParent` checks the exact immediate parent. `ShareAncestor` checks a shared nearest `Galaxy`,
`PlanetSystem`, `Planet`, `Fleet`, `Mission`, or `CapitalShip` ancestor:

```xml
<ShareAncestor Type="Planet">
  <Units>
    <Unit UnitInstanceID="LUKE_SKYWALKER"/>
    <Unit UnitInstanceID="DARTH_VADER"/>
  </Units>
</ShareAncestor>
```

Building types are `Mine`, `Refinery`, `Shipyard`, `TrainingFacility`, `ConstructionFacility`,
`Defense`, `Weapon`, and `Headquarters`.

**Targets and selectors**

`Target` selects exactly one node and exposes it as `$target`:

```xml
<Target>
  <From>
    <SelectRandom Count="1">
      <From>
        <SelectPlanets SystemType="CoreSystem"/>
      </From>
    </SelectRandom>
  </From>
</Target>
```

| Selector | Filters or behavior |
| --- | --- |
| `SelectPlanets` | `InstanceID`, `OwnerFactionInstanceID`, `SystemType` |
| `SelectPlanetSystems` | `InstanceID`, `SystemType` |
| `SelectOfficers` | ID, planet, owner, capture state, and whether retained officers are included |
| `SelectSpecialForces`, `SelectFleets`, `SelectMissions` | ID, planet, and owner |
| `SelectCapitalShips`, `SelectStarfighters`, `SelectRegiments` | ID, planet, owner, `TypeID`, and `ManufacturingStatus` |
| `SelectBuildings` | The same filters plus `Category` |
| `SelectManufacturingOrders` | Planet, owner, and `ManufacturingType` |
| `SelectRandom` | Samples its combined candidates by chance and count. |
| `SelectFirst` | Returns the first valid destination candidate. |
| `SelectBinding` | Returns the object or collection in a binding. |
| `SelectAncestors` | Maps candidates to their nearest ancestor of `Type`. |
| `SelectPreviousLocation` | Returns a retained unit's recorded previous location. |
| `SpawnUnits` | Creates `Count` detached units from a catalog `TypeID` for immediate use by `PlaceUnits`. |

Planet location filters use `PlanetInstanceID` or `PlanetBinding`. System types are `CoreSystem` and
`OuterRim`. Manufacturing statuses are `Building` and `Complete`. Manufacturing types are `Ship`,
`Building`, and `Troop`. Building categories are `Any`, `PlanetaryDefense`, and
`ManufacturingFacility`.

`SelectRandom` accepts `ChancePercent`, exact `Count`, or `MinimumCount` and `MaximumCount`:

```xml
<SelectRandom ChancePercent="25" MinimumCount="1" MaximumCount="3">
  <From>
    <SelectBuildings PlanetBinding="$target" Category="PlanetaryDefense"/>
    <SelectRegiments PlanetBinding="$target"/>
  </From>
</SelectRandom>
```

**Actions**

Actions run from top to bottom. Later actions see changes and results produced by earlier actions.

| Action | What it does |
| --- | --- |
| `SendMessage` | Sends an authored strategy message. |
| `If` | Runs `Actions` or optional `Else` based on `Conditions`. |
| `Random` | Chooses one weighted outcome whose optional `When` passes. |
| `PerformSkillCheck` | Uses an officer rating and probability table, then runs `OnSuccess` or `OnFailure`. |
| `SetEventVariable` | Applies `Set`, `Add`, `Minimum`, or `Maximum` to a saved integer. |
| `RevealToFaction` | Reveals selected planets, systems, fleets, missions, units, buildings, or manufacturing orders. |
| `ChangePlanetStat` | Changes a planet stat by signed `Amount` or `PercentOfCurrent`. |
| `ReducePlanetStats` | Applies probabilistic resource losses to selected planet stats. |
| `RecordPlanetIncident` | Records `Uprising`, `Intelligence`, `Disaster`, or `Resource` from results already produced against `$target`. |
| `DestroyUnits` | Permanently deletes selected units. |
| `ChangeOwner` | Transfers either selected planets or selected units to a faction. |
| `PlaceUnits` | Immediately places selected units or newly spawned units at a valid destination. |
| `SendUnits` | Sends units using normal movement and transit. |
| `AddToVoid`, `RemoveFromVoid` | Retains units outside active play or releases that retention. |
| `SetDisplayName`, `SetDisplayStatus`, `ClearDisplayStatus` | Changes display metadata for selected nodes. |
| `SetCaptureStatus` | Captures or releases selected officers. |
| `ChangeOfficerRating` | Changes an officer rating by a flat or percentage calculation. |
| `IncreaseOfficerForce` | Increases an officer's Force value. |
| `SetForceSensitive`, `SetForceEligible` | Adds latent Force sensitivity or reveals and initializes it. |
| `ApplyOfficerInjury` | Applies a random injury in an inclusive range. |
| `TriggerDuel` | Requests a duel between two officers. |
| `SetOfficerImages`, `SetOfficerVoiceSet` | Replaces supplied officer presentation assets. |

`If`, weighted `Random`, and `PerformSkillCheck` contain actions directly:

```xml
<PerformSkillCheck OfficerInstanceID="HAN_SOLO"
                   Rating="Combat"
                   ProbabilityTable="Abduction"
                   RatingMultiplier="-1">
  <OnSuccess>
    <SetCaptureStatus OfficerInstanceID="HAN_SOLO"
                      IsCaptured="true"
                      CaptorFactionInstanceID="FNEMP1"/>
  </OnSuccess>
  <OnFailure>
    <SendMessage RecipientFactionInstanceID="FNALL1"
                 SubjectInstanceID="HAN_SOLO"
                 Type="Mission">
      <Subject>Han evades capture</Subject>
      <Body>The attackers failed to capture Han Solo.</Body>
    </SendMessage>
  </OnFailure>
</PerformSkillCheck>
```

`ChangeOfficerRating` accepts exactly one of `Amount`, `PercentOfStored`, `PercentOfEffective`, or
`PercentOfPositiveGap`. Percentage-of-gap changes require `ReferenceOfficerInstanceID`.
`IncreaseOfficerForce` offers the same calculations but only permits positive growth.

```xml
<ChangeOfficerRating OfficerInstanceID="LUKE_SKYWALKER" Rating="Combat">
  <Amount>5</Amount>
</ChangeOfficerRating>

<IncreaseOfficerForce OfficerInstanceID="LUKE_SKYWALKER"
                      ReferenceOfficerInstanceID="DARTH_VADER"
                      MinimumAmount="1">
  <PercentOfPositiveGap>25</PercentOfPositiveGap>
</IncreaseOfficerForce>
```

`PlaceUnits` accepts existing-unit selectors and any number of `SpawnUnits` sources in the same
`Units` collection. Each spawned unit receives a new runtime instance ID and starts complete and
stationary:

```xml
<PlaceUnits DestinationInstanceID="NABOO">
  <Units>
    <SelectOfficers InstanceID="LUKE_SKYWALKER"/>
    <SpawnUnits TypeID="X_WING" Count="3" OwnerFactionInstanceID="FNALL1"/>
    <SpawnUnits TypeID="ALLIANCE_REGIMENT" Count="2" OwnerFactionInstanceID="FNALL1"/>
  </Units>
</PlaceUnits>
```

`SpawnUnits` requires a `TypeID` defined in the appropriate unit data file. A definition's optional
`ManufacturingFactionInstanceIDs` controls which faction research and production catalogs include it;
an empty collection makes the definition available only to scenarios and events.

Faction collections use the serializer's standard `String` item elements:

```xml
<ManufacturingFactionInstanceIDs>
  <String>FNALL1</String>
</ManufacturingFactionInstanceIDs>

<RecruitingFactionInstanceIDs>
  <String>FNALL1</String>
</RecruitingFactionInstanceIDs>
```

Ownership does not restrict how a unit was acquired. `ManufacturingFactionInstanceIDs` controls who may
manufacture a definition, while an officer's `RecruitingFactionInstanceIDs` controls who may recruit
that officer. Once acquired or transferred, any faction may own either.

`ChangeOwner` accepts exactly one of `Planets` or `Units` per action. Planet transfers use planetary
control rules; unit transfers preserve containment while updating faction ownership indexes:

```xml
<ChangeOwner FactionInstanceID="FNALL1">
  <Planets>
    <SelectPlanets InstanceID="NABOO"/>
  </Planets>
</ChangeOwner>

<ChangeOwner FactionInstanceID="FNALL1">
  <Units>
    <SelectCapitalShips InstanceID="STAR_DESTROYER_12"/>
    <SelectOfficers InstanceID="OFFICER_7"/>
  </Units>
</ChangeOwner>
```

`PlaceUnits` and `SendUnits` accept direct `UnitInstanceID` and `DestinationInstanceID` attributes or
typed `Units` and `Destination` selectors. `RemoveFromVoid` only releases retention; it does not
choose a destination. Return a retained unit explicitly:

```xml
<RemoveFromVoid UnitInstanceID="LUKE_SKYWALKER"/>
<PlaceUnits UnitInstanceID="LUKE_SKYWALKER">
  <Destination>
    <SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
  </Destination>
</PlaceUnits>
```

`SetOfficerImages` supports `DisplayImagePath`, `SmallDisplayImagePath`, `MessageImagePath`, and
`EncyclopediaImagePath`. `SetOfficerVoiceSet` supports `Order`, `PersonnelArrived`, `MissionSuccess`,
`MissionFailure`, `MissionAbort`, `Released`, `Recovered`, `EnemyDetected`, `ForceGrowth`,
`ForceUserDiscovered`, `TraitorDiscovered`, and `RescueAttempt`; each contains one or more `Path`
elements.

**Messages**

```xml
<SendMessage RecipientFactionInstanceID="FNALL1"
             SubjectInstanceID="LUKE_SKYWALKER"
             LocationInstanceID="YAVIN"
             Type="Mission">
  <Subject>Luke Returns</Subject>
  <Body>Luke has completed his training.</Body>
  <BackgroundImage Path="Pack/Shared/Events/MessageBackgrounds/luke-returns"/>
  <OverlayImage Path="Pack/Factions/Alliance/Units/Officers/OFAL003/message"/>
  <BackgroundAudio Path="Pack/Factions/Alliance/Strategy/Audio/Messages/message-faction-report"/>
  <OfficerVoice Preset="MissionSuccess"/>
  <AdvisorNotification Preset="SubjectReport"/>
</SendMessage>
```

Recipients use `RecipientFactionInstanceID` or `RecipientUnitInstanceID`. Subject and location use
an instance ID or binding. `RelatedSubjectInstanceID` supplies a secondary subject. Message types
are `PopularSupport`, `Fleet`, `Mission`, `Resource`, `Manufacturing`, `Defense`, `Conflict`, `Chat`,
and `Advice`.

`ConditionalBodies` can select alternate `Body` and `ElseBody` text with conditions. Message text
may use supported context tokens such as `{subject}` and `{location}`.

Presentation options are `BackgroundImage`, `OverlayImage`, `BackgroundAudio`, `OfficerVoice`, and
`AdvisorNotification`. Images and audio accept the `Path`, `Key`, `Binding`, or `Preset` form allowed
by that element. Officer voice presets are `Order`, `PersonnelArrived`, `MissionSuccess`,
`MissionFailure`, `MissionAbort`, `Released`, `Recovered`, `EnemyDetected`, `ForceGrowth`,
`ForceUserDiscovered`, `TraitorDiscovered`, and `RescueAttempt`. Advisor notifications may use a
preset or explicit droid and protocol animation, audio, frame-count, delay, and announcement fields.

## Complete Examples

This repeating event selects one owned core planet, destroys a random sample of defensive units,
records the incident, and sends a message:

```xml
<GameEvent>
  <InstanceID>MOD_PLANETARY_ATTACK</InstanceID>
  <Schedule>
    <Random MinimumTicks="100" MaximumTicks="300"/>
  </Schedule>
  <Target>
    <From>
      <SelectRandom Count="1">
        <From>
          <SelectPlanets SystemType="CoreSystem"/>
        </From>
      </SelectRandom>
    </From>
  </Target>
  <Conditionals>
    <IsOwned PlanetBinding="$target"/>
  </Conditionals>
  <Actions>
    <DestroyUnits>
      <Units>
        <SelectRandom ChancePercent="25" MinimumCount="1" MaximumCount="3">
          <From>
            <SelectBuildings PlanetBinding="$target" Category="PlanetaryDefense"/>
            <SelectRegiments PlanetBinding="$target"/>
          </From>
        </SelectRandom>
      </Units>
    </DestroyUnits>
    <RecordPlanetIncident Type="Uprising"/>
    <SendMessage LocationBinding="$target" Type="Defense">
      <Subject>Planetary defenses attacked</Subject>
      <Body>Hostile forces attacked defenses on {location}.</Body>
      <BackgroundImage Path="Pack/Shared/Events/MessageBackgrounds/planetary-attack"/>
      <AdvisorNotification Preset="SubjectReport"/>
    </SendMessage>
  </Actions>
</GameEvent>
```

This one-time event reacts when a particular unit reaches a particular destination:

```xml
<GameEvent TriggerCount="1">
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
      <Body>The Emperor has returned to the seat of power.</Body>
      <BackgroundImage Path="Pack/Shared/Events/MessageBackgrounds/emperor-arrives-at-coruscant"/>
      <OfficerVoice Path="Pack/Factions/Empire/Units/Officers/OFEM001/Voice/seat-of-power-01"/>
      <AdvisorNotification Preset="SubjectReport"/>
    </SendMessage>
  </Actions>
</GameEvent>
```

A multi-stage chain uses stable event IDs and dependent schedules:

```xml
<GameEvent TriggerCount="1">
  <InstanceID>MOD_OFFICER_LEAVES</InstanceID>
  <Schedule><At Tick="300"/></Schedule>
  <Actions>
    <AddToVoid UnitInstanceID="LUKE_SKYWALKER"/>
    <SetDisplayStatus TargetInstanceID="LUKE_SKYWALKER" Status="Away on assignment"/>
  </Actions>
</GameEvent>

<GameEvent TriggerCount="1">
  <InstanceID>MOD_OFFICER_RETURNS</InstanceID>
  <Schedule><After EventInstanceID="MOD_OFFICER_LEAVES" DelayTicks="100"/></Schedule>
  <Actions>
    <RemoveFromVoid UnitInstanceID="LUKE_SKYWALKER"/>
    <PlaceUnits UnitInstanceID="LUKE_SKYWALKER">
      <Destination>
        <SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
      </Destination>
    </PlaceUnits>
    <ClearDisplayStatus TargetInstanceID="LUKE_SKYWALKER"/>
  </Actions>
</GameEvent>
```

## Testing Your Events

Run the content repository's build first. It validates the XML against `game-events.xsd` and catches
invalid elements, attributes, nesting, and enum values.

```bash
cd /path/to/rebellion2-media
./build.sh
```

Copy or install the updated content into `rebellion2/Assets/Content`, open the project in Unity, and
start a new campaign using that content pack. For quick testing, temporarily use a small `At` tick or
short `Random` range, then restore the intended schedule before committing.

If an event does not run:

- Check the Unity log for content-load or runtime validation errors.
- Confirm `pack.xml` points to the correct `GameEventsPath`.
- Confirm every `InstanceID`, referenced event, faction, planet, unit, and media path exists.
- Confirm the event does not combine `Schedule` with `Triggers`.
- Confirm trigger argument names and `$binding` aliases match exactly.
- Confirm the target selector returns exactly one node.
- Confirm conditions can pass in the tested game state.
- Confirm the event has not reached `TriggerCount` or matched `Until`.

Finally, save and reload after the event has run. Verify recurring schedules, activation counts,
event variables, retained units, display changes, and multi-stage chains still behave correctly.
Event IDs and variable keys are persisted, so changing them can invalidate existing event state.
