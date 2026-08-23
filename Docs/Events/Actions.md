# Actions

Actions change game state. They execute from top to bottom against one shared context, so later
actions observe earlier state changes. Specialized actions, such as `RecordPlanetIncident`, can
also inspect results recorded earlier in the same activation.

Requests and results produced by event actions retain the source event's `InstanceID`. They can
activate other result-triggered events, but they do not generate automatic strategy messages.
Use `SendMessage` when the event should communicate with a player.

Every action belongs inside an event's `Actions` collection:

```xml
<Actions>
  <SetEventVariable>
    <Key>example.activations</Key>
    <Operation>Add</Operation>
    <Operand>1</Operand>
  </SetEventVariable>
  <SendMessage RecipientFactionInstanceID="FNALL1" Type="Advice">
    <Subject>Event executed</Subject>
    <Body>The action list ran in authored order.</Body>
  </SendMessage>
</Actions>
```

## Flow and event state

### If

`If` evaluates its nested conditions when execution reaches it. It runs `Actions` when every condition passes and otherwise runs the optional `Else` list.

**Options**

- `Conditionals` — required condition collection.
- `Actions` — required action collection executed when the conditions pass.
- `Else` — optional action collection executed when the conditions fail.

```xml
<If>
  <Conditionals>
    <IsCaptured OfficerInstanceID="HAN_SOLO"/>
  </Conditionals>
  <Actions>
    <SendMessage RecipientFactionInstanceID="FNALL1" Type="Mission">
      <Subject>Han is a captive</Subject>
      <Body>A rescue may now be attempted.</Body>
    </SendMessage>
  </Actions>
  <Else>
    <SetDisplayStatus TargetInstanceID="HAN_SOLO" Status="Available"/>
  </Else>
</If>
```

### RollRandom

`RollRandom` first removes outcomes whose `Conditionals` fail, then selects one remaining outcome by
`Weight`. Weights are relative: weights `30` and `70` produce a 30/70 split. If no outcome remains
eligible, the action does nothing.

**Options**

- `Outcomes` — required child containing one or more `Outcome` elements.
- `Weight` — required positive attribute on each `Outcome`.
- `Conditionals` — optional conditions for making an outcome eligible.
- `Actions` — required actions for each outcome.

```xml
<RollRandom>
  <Outcomes>
    <Outcome Weight="30">
      <Actions>
        <ChangePlanetStat PlanetInstanceID="NABOO" Stat="RawResourceNodes">
          <Amount>1</Amount>
        </ChangePlanetStat>
      </Actions>
    </Outcome>
    <Outcome Weight="70">
      <Actions>
        <SendMessage RecipientFactionInstanceID="FNALL1" Type="Resource">
          <Subject>No discovery</Subject>
          <Body>The survey found no useful deposits.</Body>
        </SendMessage>
      </Actions>
    </Outcome>
  </Outcomes>
</RollRandom>
```

### PerformSkillCheck

`PerformSkillCheck` looks up the named mission probability table using the officer's effective rating multiplied by `RatingMultiplier`. It executes exactly one of `OnSuccess` or `OnFailure`; it does not emit a separate skill-check result.

**Options**

- `OfficerInstanceID` — required officer ID.
- `Rating` — required officer rating.
- `ProbabilityTable` — required probability-table name.
- `RatingMultiplier` — optional nonzero multiplier; defaults to `1`.
- `OnSuccess` — optional actions run after a successful check.
- `OnFailure` — optional actions run after a failed check.

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

Officer ratings are `Diplomacy`, `Espionage`, `Combat`, `Leadership`, `ShipResearch`, `TroopResearch`, and `FacilityResearch`.

### SetEventVariable

Event variables are saved integers shared by all events. Operations are `Set`, `Add`, `Minimum`, and `Maximum`.

**Options**

- `Key` — required variable key.
- `Operand` — required integer input.
- `Operation` — optional `Set`, `Add`, `Minimum`, or `Maximum`; defaults to `Set`.

```xml
<SetEventVariable>
  <Key>naboo.attacks</Key>
  <Operation>Add</Operation>
  <Operand>1</Operand>
</SetEventVariable>
```

Use [`EvaluateEventVariable`](Conditions.md) to read the value from a condition.

## Information and messages

### RevealToFaction

`RevealToFaction` records current observations of every selected target for one recipient faction. The selectors determine whether the faction learns about planets, fleets, missions, units, buildings, or manufacturing.

**Options**

- `FactionInstanceID` — required recipient faction.
- `Targets` — required selector collection containing the objects to reveal.

```xml
<RevealToFaction FactionInstanceID="FNALL1">
  <Targets>
    <SelectCapitalShips PlanetInstanceID="CORUSCANT"
                        OwnerFactionInstanceID="FNEMP1"
                        ManufacturingStatus="Complete"/>
    <SelectOfficers PlanetInstanceID="CORUSCANT"
                    OwnerFactionInstanceID="FNEMP1"/>
  </Targets>
</RevealToFaction>
```

If the selectors resolve no observations, the action does nothing.

Selection granularity controls what is recorded. A planet records its current planet-level state,
and a planet sector records that state for each of its planets. A fleet records its complete ship
and cargo hierarchy. Selecting one capital ship records that ship without unselected cargo;
selecting a carried officer, regiment, special-forces unit, or starfighter records that subject and
only the parent structure needed to locate it. Buildings, missions, and manufacturing orders are
recorded individually.

### SendMessage

`SendMessage` delivers a normal strategy message to an explicitly identified faction. Subject and location may use instance IDs or trigger bindings.

**Options**

- `RecipientFactionInstanceID` — required recipient faction.
- `SubjectInstanceID` or `SubjectBinding` — optional message subject. When both are present,
  `SubjectBinding` takes precedence.
- `RelatedSubjectInstanceID` — optional secondary subject.
- `LocationInstanceID` or `LocationBinding` — optional location. When both are present,
  `LocationBinding` takes precedence.
- `Type` — optional message type; defaults to `Advice`.
- `Subject`, `Body`, and `ConditionalBodies` — optional authored text.
- `BackgroundImage`, `OverlayImage`, `BackgroundAudio`, `OfficerVoice`, and
  `AdvisorNotification` — optional presentation.
```xml
<SendMessage RecipientFactionInstanceID="FNALL1"
             SubjectInstanceID="LUKE_SKYWALKER"
             LocationInstanceID="YAVIN"
             Type="Mission">
  <Subject>Luke Returns</Subject>
  <Body>Luke has completed his training.</Body>
  <BackgroundImage Key="mission_report"/>
  <OverlayImage Path="Pack/Factions/Alliance/Units/Officers/OFAL003/message"/>
  <BackgroundAudio Path="Pack/Factions/Alliance/Strategy/Messages/Audio/message-faction-report"/>
  <OfficerVoice Path="Pack/Factions/Alliance/Units/Officers/OFAL003/Voice/dagobah-completed-01"/>
  <AdvisorNotification Preset="SubjectReport"/>
</SendMessage>
```

Message types are `PopularSupport`, `Fleet`, `Mission`, `Resource`, `Manufacturing`, `Defense`, `Conflict`, `Chat`, and `Advice`.

`ConditionalBodies` contains zero or more `ConditionalBody` entries. Each entry accepts optional
`Conditionals`, `Body`, and `ElseBody` children and appends the selected text. Message text supports
context tokens such as `{subject}` and `{location}`.

Presentation sources have distinct contracts:

- `BackgroundImage` — exactly one `Key`, `Path`, or string-valued `Binding`.
- `OverlayImage` — optional `Path`. When omitted, an officer subject supplies its current message
  image.
- `BackgroundAudio` — exactly one `Path` or string-valued `Binding`.
- `OfficerVoice` — optional explicit `Path` or officer voice-line `Preset`; do not combine them. A
  preset requires an officer subject, and an empty element produces no voice line.
- `AdvisorNotification` — optional `Preset`, `LifetimeTicks`, `Droid`, and `Protocol`. `Droid` and
  `Protocol` accept `Animation`, `AnimationPath`, `FrameCount`, `Audio`, `AudioPath`,
  `DelayBeforeSeconds`, and `RequiresAnnouncementsEnabled` overrides.

XML permits presentation properties as attributes or child elements where the schema declares
both forms. Do not provide the same property in both forms.

Officer voice presets are `Order`, `PersonnelArrived`, `MissionSuccess`, `MissionFailure`,
`MissionAbort`, `Released`, `Recovered`, `EnemyDetected`, `ForceGrowth`, `ForceUserDiscovered`,
`TraitorDiscovered`, and `RescueAttempt`. Advisor presets are `None`, `PositivePopularSupport`,
`NegativePopularSupport`, `Manufacturing`, `Research`, `FleetArrived`, `UnitsArrived`,
`CapitalShipRepaired`, `StarfighterRepaired`, `Maintenance`, `BlockadeInitiated`,
`BlockadeDetected`, `FieldPersonnel`, `AgentReport`, `PlanetaryStatus`, `PrisonerEscaped`,
`InterceptedCommunication`, `Bombardment`, `PlanetaryAssault`, `SubjectReport`,
`SubjectCaptured`, and `SubjectReleased`.

## Planets and resources

### ChangePlanetStat

Choose exactly one adjustment mode: signed `Amount` or signed `PercentOfCurrent`. The action can address a planet by instance ID, binding, or selectors.

**Options**

- `Stat` — required planet stat.
- `PlanetInstanceID` or `PlanetBinding` — optional direct planet source. When both are present,
  `PlanetBinding` takes precedence.
- `Planets` — optional planet selector collection.
- `Amount` or `PercentOfCurrent` — exactly one signed adjustment is required.

```xml
<ChangePlanetStat PlanetInstanceID="NABOO" Stat="RawResourceNodes">
  <Amount>5</Amount>
</ChangePlanetStat>
```

Supported stats are `RawResourceNodes` and `EnergyCapacity`. Values cannot fall below zero.

### ReducePlanetStats

This action requires a planet instance ID or planet binding. It independently rolls `LossProbabilityPerResource` once for every current point in the selected stats, then enforces `MinimumTotalLoss`.
The minimum loss is capped by the total number of available points, and stats never fall below
zero.

**Options**

- `PlanetInstanceID` or `PlanetBinding` — required planet source. When both are present,
  `PlanetBinding` takes precedence.
- `LossProbabilityPerResource` — required probability from `0` through `1`, applied independently
  to each existing point.
- `MinimumTotalLoss` — required minimum combined loss.
- `Stats` — required child containing one or more `Stat` elements.
- `Name` — required planet-stat attribute on each `Stat`.

```xml
<ReducePlanetStats PlanetInstanceID="NABOO"
                   LossProbabilityPerResource="0.05"
                   MinimumTotalLoss="1">
  <Stats>
    <Stat Name="RawResourceNodes"/>
    <Stat Name="EnergyCapacity"/>
  </Stats>
</ReducePlanetStats>
```

### RecordPlanetIncident

This action requires a planet instance ID or planet binding. It records an incident from planet-stat
changes and destroyed units already produced earlier in the same evaluation. It records nothing
when those earlier actions made no change.

Incident severity is the sum of absolute planet-stat changes plus the number of destroyed objects
recorded for that planet.

**Options**

- `Type` — required `Uprising`, `Intelligence`, `Disaster`, or `Resource` incident type.
- `PlanetInstanceID` or `PlanetBinding` — required incident location. When both are present,
  `PlanetBinding` takes precedence.

```xml
<Actions>
  <DestroyUnits PlanetBinding="$planet">
    <Units>
      <SelectRandom Count="1">
        <From>
          <SelectBuildings PlanetBinding="$planet" Category="PlanetaryDefense"/>
        </From>
      </SelectRandom>
    </Units>
  </DestroyUnits>
  <RecordPlanetIncident Type="Disaster" PlanetBinding="$planet"/>
</Actions>
```

Incident types are `Uprising`, `Intelligence`, `Disaster`, and `Resource`.

## Units and ownership

### DestroyUnits

`DestroyUnits` permanently deletes every selected unit. Selecting a container such as a fleet or
capital ship also deletes its contained subtree. Selecting both a parent and its child does not
double-delete the child.

**Options**

- `Units` — required unit selector collection.
- `PlanetInstanceID` or `PlanetBinding` — optional result context. When both are present,
  `PlanetBinding` takes precedence. These values
  do not filter the selected units.

```xml
<DestroyUnits>
  <Units>
    <SelectRandom ChancePercent="25" MinimumCount="1" MaximumCount="3">
      <From>
        <SelectBuildings PlanetBinding="$planet" Category="PlanetaryDefense"/>
        <SelectRegiments PlanetBinding="$planet"/>
      </From>
    </SelectRandom>
  </Units>
</DestroyUnits>
```

### ChangeOwner

Choose exactly one ownership domain: `Planets` or `Units`. Planet transfers use planetary-control
rules. Unit transfers update the ownership indexes while leaving the unit at its current scene
location. Only selected nodes change owner; contained descendants retain their existing owners
unless they are also selected.

**Options**

- `FactionInstanceID` — required new owner.
- `Planets` or `Units` — exactly one selector collection is required.

```xml
<ChangeOwner FactionInstanceID="FNALL1">
  <Units>
    <SelectCapitalShips InstanceID="STAR_DESTROYER_12"/>
    <SelectOfficers InstanceID="OFFICER_7"/>
  </Units>
</ChangeOwner>
```

### PlaceUnits

`PlaceUnits` bypasses transit and requests immediate placement at one valid destination. It accepts existing-unit selectors and any number of `SpawnUnits` sources. Each spawned unit receives a new runtime instance ID and starts complete and stationary.

**Options**

- `UnitInstanceID` or `Units` — at least one direct unit, selected unit, or `SpawnUnits` source must resolve.
- `DestinationInstanceID` or `Destination` — exactly one valid destination must resolve.

```xml
<PlaceUnits DestinationInstanceID="NABOO">
  <Units>
    <SelectOfficers InstanceID="LUKE_SKYWALKER"/>
    <SpawnUnits TypeID="SFAL02" Count="3" OwnerFactionInstanceID="FNALL1"/>
    <SpawnUnits TypeID="REAL002" Count="2" OwnerFactionInstanceID="FNALL1"/>
  </Units>
</PlaceUnits>
```

`SpawnUnits` requires a `TypeID` from the unit data. `ManufacturingFactionInstanceIDs` controls which factions may manufacture a definition; an empty collection leaves it available to scenarios and events without adding it to production. Officer recruitment uses `RecruitingFactionInstanceIDs`. Neither collection restricts later ownership transfers.

### SendUnits

`SendUnits` moves active, already-placed units through normal validation and transit. It cannot spawn units.

**Options**

- `UnitInstanceID` or `Units` — at least one direct or selected existing unit must resolve.
- `DestinationInstanceID` or `Destination` — exactly one valid destination must resolve.

```xml
<SendUnits UnitInstanceID="DARTH_VADER" DestinationInstanceID="YAVIN"/>
```

Both `PlaceUnits` and `SendUnits` accept direct `UnitInstanceID` and `DestinationInstanceID` attributes or typed `Units` and `Destination` selector collections. A destination collection must resolve exactly one destination unless it explicitly uses `SelectFirst`.

Transfer actions submit requests to the normal movement rules. If no destination accepts the whole
group, existing units remain where they were and newly spawned units are not retained; the event
activation itself is still consumed. Use `SelectFirst` to provide ordered fallback destinations.

### SetNodeActive

`SetNodeActive` controls whether retained scene nodes participate in gameplay. Inactive nodes remain
attached to the scene graph and are preserved in saves, but ordinary gameplay queries ignore them.
Reactivate a returning unit before placing or sending it.

**Options**

- `IsActive` — required state.
- `InstanceID` or `Nodes` — at least one direct or selected scene node must resolve.

```xml
<SetNodeActive InstanceID="LUKE_SKYWALKER" IsActive="false"/>

<SetNodeActive InstanceID="LUKE_SKYWALKER" IsActive="true"/>
<PlaceUnits UnitInstanceID="LUKE_SKYWALKER">
  <Destination>
    <SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
  </Destination>
</PlaceUnits>
```

## Officers

### SetCaptureStatus

Capturing requires `CaptorFactionInstanceID`. Releasing must omit it. This action changes capture
properties only; it does not move or deactivate the officer.

**Options**

- `IsCaptured` — required state.
- `OfficerInstanceID` or `Officers` — at least one direct or selected officer must resolve.
- `CaptorFactionInstanceID` — required when capturing and forbidden when releasing.
- `CanEscape` — optional state used when capturing; defaults to `true`. Releasing always resets it
  to `true`.

```xml
<SetCaptureStatus OfficerInstanceID="HAN_SOLO"
                  IsCaptured="true"
                  CaptorFactionInstanceID="FNEMP1"
                  CanEscape="true"/>
```

### ChangeOfficerRating

Choose exactly one of `Amount`, `PercentOfStored`, `PercentOfEffective`, or `PercentOfPositiveGap`. Percentage-of-gap changes also require `ReferenceOfficerInstanceID`.

**Options**

- `Rating` — required officer rating.
- `OfficerInstanceID` or `Officers` — at least one direct or selected officer must resolve.
- `ReferenceOfficerInstanceID` — required by `PercentOfPositiveGap`.
- `MinimumAmount` — optional non-negative lower bound used only with `PercentOfPositiveGap`.
- `Amount`, `PercentOfStored`, `PercentOfEffective`, or `PercentOfPositiveGap` — exactly one adjustment mode is required.

```xml
<ChangeOfficerRating OfficerInstanceID="LUKE_SKYWALKER" Rating="Combat">
  <Amount>5</Amount>
</ChangeOfficerRating>
```

`Amount` adds a signed integer to the stored rating. `PercentOfStored` calculates a signed change
from the stored rating. `PercentOfEffective` calculates it from the current effective rating, then
applies that change to the stored rating. `PercentOfPositiveGap` adds a non-negative percentage of
the effective-rating gap between the reference officer and each target.

### IncreaseOfficerForce

This uses the same calculation modes as `ChangeOfficerRating`, but every configured and calculated change must increase Force progression.

**Options**

- `OfficerInstanceID` or `Officers` — at least one direct or selected officer must resolve.
- `ReferenceOfficerInstanceID` — required by `PercentOfPositiveGap`.
- `MinimumAmount` — optional non-negative lower bound used only with `PercentOfPositiveGap`.
- `Amount`, `PercentOfStored`, `PercentOfEffective`, or `PercentOfPositiveGap` — exactly one positive adjustment mode is required.

```xml
<IncreaseOfficerForce OfficerInstanceID="LUKE_SKYWALKER"
                      ReferenceOfficerInstanceID="DARTH_VADER"
                      MinimumAmount="1">
  <PercentOfPositiveGap>25</PercentOfPositiveGap>
</IncreaseOfficerForce>
```

For Force, `PercentOfStored` uses `ForceValue`, `PercentOfEffective` uses the current effective
`ForceRank`, and `PercentOfPositiveGap` uses the positive effective-rank gap to the reference
officer. The calculated increase must be greater than zero.

### SetForceSensitive

Marks an officer as having latent Force potential.

**Options**

- `OfficerInstanceID` — required officer ID.

```xml
<SetForceSensitive OfficerInstanceID="LEIA_ORGANA"/>
```

### SetForceEligible

Marks a Force-sensitive officer's potential as discovered and initializes Force progression.
Eligibility requires sensitivity, so use both actions when revealing a previously unknown candidate.
On the first eligibility change, `ForceValue` is raised to at least `JediLevel` plus a uniformly
rolled value from zero through `JediLevelVariance`.

**Options**

- `OfficerInstanceID` — required officer ID.

```xml
<SetForceEligible OfficerInstanceID="LEIA_ORGANA"/>
```

### ApplyOfficerInjury

The action rolls an inclusive injury value and records the standard officer-injured result.

**Options**

- `OfficerInstanceID` — required officer ID.
- `MinimumInjury` — required non-negative minimum injury.
- `MaximumInjury` — required non-negative maximum injury and cannot be lower than the minimum.

```xml
<ApplyOfficerInjury OfficerInstanceID="LUKE_SKYWALKER">
  <MinimumInjury>1</MinimumInjury>
  <MaximumInjury>5</MaximumInjury>
</ApplyOfficerInjury>
```

### TriggerDuel

`TriggerDuel` requests normal duel resolution between two active officers. `ImagePath` and `AudioPath` optionally override the presentation used for this duel.

**Options**

- `FirstOfficerInstanceID` — required first officer.
- `SecondOfficerInstanceID` — required second officer.
- `ImagePath` — optional presentation-image override.
- `AudioPath` — optional presentation-audio override.

```xml
<TriggerDuel FirstOfficerInstanceID="LUKE_SKYWALKER"
             SecondOfficerInstanceID="DARTH_VADER">
  <ImagePath>Pack/Factions/Alliance/Strategy/Messages/Images/luke-encounters-vader-trained</ImagePath>
  <AudioPath>Pack/Shared/Events/JediConfrontation/Audio/luke-vader-dagobah-complete</AudioPath>
</TriggerDuel>
```

The duel produces a result consumed by the typed [`DuelCompleted`](Triggers.md#duelcompleted) trigger.

### SetOfficerImages

Image paths are merged into the officer's active image set, so omitted paths remain unchanged.

**Options**

- `OfficerInstanceID` — required officer ID.
- `DisplayImagePath`, `SmallDisplayImagePath`, `MessageImagePath`, and
  `EncyclopediaImagePath` — optional paths. An empty action makes no image changes.

```xml
<SetOfficerImages OfficerInstanceID="LUKE_SKYWALKER">
  <DisplayImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-display</DisplayImagePath>
  <SmallDisplayImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-small-display</SmallDisplayImagePath>
  <MessageImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/message</MessageImagePath>
  <EncyclopediaImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-encyclopedia</EncyclopediaImagePath>
</SetOfficerImages>
```

### SetOfficerVoiceSet

Replaces authored categories in the officer's active voice set. Omitted categories remain unchanged.

**Options**

- `OfficerInstanceID` — required officer ID.
- Each optional voice-category child contains `Path` elements. A category with no paths makes no
  change to that category.

Voice categories are `Order`, `PersonnelArrived`, `MissionSuccess`, `MissionFailure`, `MissionAbort`,
`Released`, `Recovered`, `EnemyDetected`, `ForceGrowth`, `ForceUserDiscovered`,
`TraitorDiscovered`, and `RescueAttempt`.

```xml
<SetOfficerVoiceSet OfficerInstanceID="LUKE_SKYWALKER">
  <PersonnelArrived>
    <Path>Pack/Factions/Alliance/Units/Officers/OFAL003/Voice/advanced-personnel-arrived-01</Path>
    <Path>Pack/Factions/Alliance/Units/Officers/OFAL003/Voice/advanced-personnel-arrived-02</Path>
  </PersonnelArrived>
  <MissionSuccess>
    <Path>Pack/Factions/Alliance/Units/Officers/OFAL003/Voice/mission-success-01</Path>
  </MissionSuccess>
</SetOfficerVoiceSet>
```

## Display metadata

These actions accept one direct `TargetInstanceID` or a `Targets` selector collection.
At least one direct or selected target must resolve.

### SetDisplayName

Sets a node's authored display name.

**Options**

- `Name` — required display name.
- `TargetInstanceID` — optional direct target.
- `Targets` — optional selector collection.

```xml
<SetDisplayName TargetInstanceID="LUKE_SKYWALKER" Name="Luke Skywalker (Jedi)"/>
```

### SetDisplayStatus

Sets supplemental status text without changing gameplay state.

**Options**

- `Status` — required display text.
- `TargetInstanceID` — optional direct target.
- `Targets` — optional selector collection.

```xml
<SetDisplayStatus TargetInstanceID="LUKE_SKYWALKER" Status="On Mission (Dagobah)"/>
```

### ClearDisplayStatus

Clears supplemental status text.

**Options**

- `TargetInstanceID` — optional direct target.
- `Targets` — optional selector collection.

```xml
<ClearDisplayStatus TargetInstanceID="LUKE_SKYWALKER"/>
```

---

<p align="center"><a href="Conditions.md">← Conditions</a> · <a href="Index.md">Event guide</a> · <a href="Examples.md">Examples →</a></p>
