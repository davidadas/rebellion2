# Actions

Actions change game state. They execute from top to bottom against one shared context, so a later action can observe changes and results produced by an earlier action.

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

## Action index

**Flow and event state**

- [`If`](#if) — choose between two action lists.
- [`Random`](#random) — choose one weighted eligible outcome.
- [`PerformSkillCheck`](#performskillcheck) — branch using an officer rating and probability table.
- [`SetEventVariable`](#seteventvariable) — store or update a persistent integer.

**Information and messages**

- [`RevealToFaction`](#revealtofaction) — provide current observations to a faction.
- [`SendMessage`](#sendmessage) — deliver an authored strategy message.

**Planets and resources**

- [`ChangePlanetStat`](#changeplanetstat) — apply a fixed or percentage adjustment.
- [`ReducePlanetStats`](#reduceplanetstats) — roll losses across selected planet statistics.
- [`RecordPlanetIncident`](#recordplanetincident) — summarize changes already produced by this activation.

**Units and ownership**

- [`DestroyUnits`](#destroyunits) — permanently delete selected units.
- [`ChangeOwner`](#changeowner) — transfer selected planets or units.
- [`PlaceUnits`](#placeunits) — place existing or newly spawned units immediately.
- [`SendUnits`](#sendunits) — move existing units through normal transit.
- [`SetActive`](#setactive) — include or exclude units from active gameplay.

**Officers**

- [`SetCaptureStatus`](#setcapturestatus)
- [`ChangeOfficerRating`](#changeofficerrating)
- [`IncreaseOfficerForce`](#increaseofficerforce)
- [`SetForceSensitive` and `SetForceEligible`](#setforcesensitive-and-setforceeligible)
- [`ApplyOfficerInjury`](#applyofficerinjury)
- [`TriggerDuel`](#triggerduel)
- [`SetOfficerImages` and `SetOfficerVoiceSet`](#setofficerimages-and-setofficervoiceset)

**Display metadata**

- [`SetDisplayName`, `SetDisplayStatus`, and `ClearDisplayStatus`](#display-metadata)

## Flow and event state

### If

`If` evaluates its nested conditions when execution reaches it. It runs `Actions` when every condition passes and otherwise runs the optional `Else` list.

```xml
<If>
  <Conditions>
    <IsCaptured OfficerInstanceID="HAN_SOLO"/>
  </Conditions>
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

### Random

`Random` first removes outcomes whose `When` conditions fail, then selects one remaining outcome by `Weight`. Weights are relative: weights `30` and `70` produce a 30/70 split.

```xml
<Random>
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
</Random>
```

### PerformSkillCheck

`PerformSkillCheck` looks up the named mission probability table using the officer's effective rating multiplied by `RatingMultiplier`. It executes exactly one of `OnSuccess` or `OnFailure`; it does not emit a separate skill-check result.

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

`RevealToFaction` records current observations of every selected subject for one recipient faction. The selectors determine whether the faction learns about planets, fleets, missions, units, buildings, or manufacturing.

```xml
<RevealToFaction FactionInstanceID="FNALL1">
  <Subjects>
    <SelectCapitalShips PlanetInstanceID="CORUSCANT"
                        OwnerFactionInstanceID="FNEMP1"
                        ManufacturingStatus="Complete"/>
    <SelectOfficers PlanetInstanceID="CORUSCANT"
                    OwnerFactionInstanceID="FNEMP1"/>
  </Subjects>
</RevealToFaction>
```

### SendMessage

`SendMessage` delivers a normal strategy message. The recipient can be explicit or inferred from a recipient unit or subject. Subject and location may use instance IDs or trigger bindings.

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

Message types are `PopularSupport`, `Fleet`, `Mission`, `Resource`, `Manufacturing`, `Defense`, `Conflict`, `Chat`, and `Advice`.

`ConditionalBodies` can append alternate `Body` or `ElseBody` text based on conditions. Message text supports context tokens such as `{subject}` and `{location}`. Presentation elements accept the source forms supported by that element: authored paths, configured keys or presets, and trigger bindings where applicable.

## Planets and resources

### ChangePlanetStat

Choose exactly one adjustment mode: signed `Amount` or signed `PercentOfCurrent`. The action can address a planet by instance ID, binding, top-level `$target`, or selectors.

```xml
<ChangePlanetStat PlanetInstanceID="NABOO" Stat="RawResourceNodes">
  <Amount>5</Amount>
</ChangePlanetStat>
```

Supported stats are `RawResourceNodes` and `EnergyCapacity`. Values cannot fall below zero.

### ReducePlanetStats

This action requires a planet top-level target. It independently rolls `LossProbabilityPerResource` once for every current point in the selected stats, then enforces `MinimumTotalLoss`.

```xml
<ReducePlanetStats LossProbabilityPerResource="0.05" MinimumTotalLoss="1">
  <Stats>
    <Stat Name="RawResourceNodes"/>
    <Stat Name="EnergyCapacity"/>
  </Stats>
</ReducePlanetStats>
```

### RecordPlanetIncident

This action requires a planet top-level target. It records an incident from planet-stat changes and destroyed units already produced earlier in the same activation. It records nothing when those earlier actions made no change.

```xml
<Actions>
  <DestroyUnits>
    <Units>
      <SelectRandom Count="1">
        <From>
          <SelectBuildings PlanetBinding="$target" Category="PlanetaryDefense"/>
        </From>
      </SelectRandom>
    </Units>
  </DestroyUnits>
  <RecordPlanetIncident Type="Disaster"/>
</Actions>
```

Incident types are `Uprising`, `Intelligence`, `Disaster`, and `Resource`.

## Units and ownership

### DestroyUnits

`DestroyUnits` permanently deletes every selected unit. Selecting a parent and its child does not double-delete the child.

```xml
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
```

### ChangeOwner

Choose exactly one ownership domain: `Planets` or `Units`. Planet transfers use planetary-control rules; unit transfers retain valid containment while ownership indexes are updated.

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

```xml
<PlaceUnits DestinationInstanceID="NABOO">
  <Units>
    <SelectOfficers InstanceID="LUKE_SKYWALKER"/>
    <SpawnUnits TypeID="X_WING" Count="3" OwnerFactionInstanceID="FNALL1"/>
    <SpawnUnits TypeID="ALLIANCE_REGIMENT" Count="2" OwnerFactionInstanceID="FNALL1"/>
  </Units>
</PlaceUnits>
```

`SpawnUnits` requires a `TypeID` from the unit data. `ManufacturingFactionInstanceIDs` controls which factions may manufacture a definition; an empty collection leaves it available to scenarios and events without adding it to production. Officer recruitment uses `RecruitingFactionInstanceIDs`. Neither collection restricts later ownership transfers.

### SendUnits

`SendUnits` moves active, already-placed units through normal validation and transit. It cannot spawn units.

```xml
<SendUnits UnitInstanceID="DARTH_VADER" DestinationInstanceID="YAVIN"/>
```

Both `PlaceUnits` and `SendUnits` accept direct `UnitInstanceID` and `DestinationInstanceID` attributes or typed `Units` and `Destination` selector collections. A destination collection must resolve exactly one destination unless it explicitly uses `SelectFirst`.

### SetActive

`SetActive` controls whether retained units participate in gameplay. Inactive units remain attached to the scene graph and save normally, but ordinary gameplay queries ignore them. Set `IsActive="true"` before placing or sending a returning unit.

```xml
<SetActive UnitInstanceID="LUKE_SKYWALKER" IsActive="false"/>

<SetActive UnitInstanceID="LUKE_SKYWALKER" IsActive="true"/>
<PlaceUnits UnitInstanceID="LUKE_SKYWALKER">
  <Destination>
    <SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
  </Destination>
</PlaceUnits>
```

## Officers

### SetCaptureStatus

Capturing requires `CaptorFactionInstanceID`. Releasing must omit it. `CanEscape` defaults to `true`.

```xml
<SetCaptureStatus OfficerInstanceID="HAN_SOLO"
                  IsCaptured="true"
                  CaptorFactionInstanceID="FNEMP1"
                  CanEscape="true"/>
```

### ChangeOfficerRating

Choose exactly one of `Amount`, `PercentOfStored`, `PercentOfEffective`, or `PercentOfPositiveGap`. Percentage-of-gap changes also require `ReferenceOfficerInstanceID`.

```xml
<ChangeOfficerRating OfficerInstanceID="LUKE_SKYWALKER" Rating="Combat">
  <Amount>5</Amount>
</ChangeOfficerRating>
```

### IncreaseOfficerForce

This uses the same calculation modes as `ChangeOfficerRating`, but every configured and calculated change must increase Force progression.

```xml
<IncreaseOfficerForce OfficerInstanceID="LUKE_SKYWALKER"
                      ReferenceOfficerInstanceID="DARTH_VADER"
                      MinimumAmount="1">
  <PercentOfPositiveGap>25</PercentOfPositiveGap>
</IncreaseOfficerForce>
```

### SetForceSensitive and SetForceEligible

Force sensitivity is latent potential. Force eligibility means that potential has been discovered and initialized. Eligibility requires sensitivity, so use both actions when revealing a previously unknown candidate.

```xml
<SetForceSensitive OfficerInstanceID="LEIA_ORGANA"/>
<SetForceEligible OfficerInstanceID="LEIA_ORGANA"/>
```

### ApplyOfficerInjury

The action rolls an inclusive injury value and records the standard officer-injured result.

```xml
<ApplyOfficerInjury OfficerInstanceID="LUKE_SKYWALKER">
  <MinimumInjury>1</MinimumInjury>
  <MaximumInjury>5</MaximumInjury>
</ApplyOfficerInjury>
```

### TriggerDuel

`TriggerDuel` requests normal duel resolution between two active officers. `ImagePath` and `AudioPath` optionally override the presentation used for this duel.

When activated by `core:mission.completed`, exactly one configured officer must have participated in the completed mission. That participant becomes the encountered officer; the other becomes the opposing officer.

```xml
<TriggerDuel FirstOfficerInstanceID="LUKE_SKYWALKER"
             SecondOfficerInstanceID="DARTH_VADER">
  <ImagePath>Pack/Shared/Events/MessageBackgrounds/luke-encounters-vader</ImagePath>
  <AudioPath>Pack/Shared/Events/JediConfrontation/Audio/luke-vader</AudioPath>
</TriggerDuel>
```

The duel produces `core:duel.completed`. A later event can [trigger from that result and bind its outcome details](Triggers.md).

### SetOfficerImages and SetOfficerVoiceSet

Image paths are merged into the officer's active image set, so omitted paths remain unchanged.

```xml
<SetOfficerImages OfficerInstanceID="LUKE_SKYWALKER">
  <DisplayImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-display</DisplayImagePath>
  <SmallDisplayImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-small-display</SmallDisplayImagePath>
  <MessageImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-message</MessageImagePath>
  <EncyclopediaImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-encyclopedia</EncyclopediaImagePath>
</SetOfficerImages>

<SetOfficerVoiceSet OfficerInstanceID="LUKE_SKYWALKER">
  <PersonnelArrived>
    <Path>Pack/Factions/Alliance/Units/Officers/OFAL003/Voice/advanced-personnel-arrived-01</Path>
    <Path>Pack/Factions/Alliance/Units/Officers/OFAL003/Voice/advanced-personnel-arrived-02</Path>
  </PersonnelArrived>
  <MissionSuccess>
    <Path>Pack/Factions/Alliance/Units/Officers/OFAL003/Voice/advanced-mission-success-01</Path>
  </MissionSuccess>
</SetOfficerVoiceSet>
```

Voice categories are `Order`, `PersonnelArrived`, `MissionSuccess`, `MissionFailure`, `MissionAbort`, `Released`, `Recovered`, `EnemyDetected`, `ForceGrowth`, `ForceUserDiscovered`, `TraitorDiscovered`, and `RescueAttempt`.

## Display metadata

These actions accept one direct `TargetInstanceID` or a `Targets` selector collection.

```xml
<SetDisplayName TargetInstanceID="LUKE_SKYWALKER" Name="Luke Skywalker (Jedi)"/>
<SetDisplayStatus TargetInstanceID="LUKE_SKYWALKER" Status="On Mission (Dagobah)"/>
<ClearDisplayStatus TargetInstanceID="LUKE_SKYWALKER"/>
```

---

[← Targets](Targets.md) · [Event guide](README.md)

<p align="right"><a href="Examples.md">Examples →</a></p>
