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

`RollRandom` first removes outcomes whose `Conditionals` fail, then selects one remaining outcome by `Weight`. Weights are relative: weights `30` and `70` produce a 30/70 split.

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
- `RatingMultiplier` — optional multiplier; defaults to `1`.
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

### SendMessage

`SendMessage` delivers a normal strategy message. The recipient can be explicit or inferred from a recipient unit or subject. Subject and location may use instance IDs or trigger bindings.

**Options**

- `RecipientFactionInstanceID` — optional explicit recipient faction.
- `RecipientUnitInstanceID` — optional unit from which to infer the recipient.
- `SubjectInstanceID` or `SubjectBinding` — optional, mutually exclusive message subject.
- `RelatedSubjectInstanceID` — optional secondary subject.
- `LocationInstanceID` or `LocationBinding` — optional, mutually exclusive location.
- `Type` — optional message type; defaults to `Advice`.
- `Subject`, `Body`, and `ConditionalBodies` — optional authored text.
- `BackgroundImage`, `OverlayImage`, `BackgroundAudio`, `OfficerVoice`, and
  `AdvisorNotification` — optional presentation.
- Recipient resolution must identify exactly one faction.

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

Choose exactly one adjustment mode: signed `Amount` or signed `PercentOfCurrent`. The action can address a planet by instance ID, binding, or selectors.

**Options**

- `Stat` — required planet stat.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive direct planet source.
- `Planets` — optional planet selector collection.
- Exactly one of `Amount` or `PercentOfCurrent` is required.

```xml
<ChangePlanetStat PlanetInstanceID="NABOO" Stat="RawResourceNodes">
  <Amount>5</Amount>
</ChangePlanetStat>
```

Supported stats are `RawResourceNodes` and `EnergyCapacity`. Values cannot fall below zero.

### ReducePlanetStats

This action requires a planet instance ID or planet binding. It independently rolls `LossProbabilityPerResource` once for every current point in the selected stats, then enforces `MinimumTotalLoss`.

**Options**

- `LossProbabilityPerResource` — required probability applied to each existing point.
- `MinimumTotalLoss` — required minimum combined loss.
- `Stats` — required child containing one or more `Stat` elements.
- `Name` — required planet-stat attribute on each `Stat`.

```xml
<ReducePlanetStats LossProbabilityPerResource="0.05" MinimumTotalLoss="1">
  <Stats>
    <Stat Name="RawResourceNodes"/>
    <Stat Name="EnergyCapacity"/>
  </Stats>
</ReducePlanetStats>
```

### RecordPlanetIncident

This action requires a planet instance ID or planet binding. It records an incident from planet-stat changes and destroyed units already produced earlier in the same activation. It records nothing when those earlier actions made no change.

**Options**

- `Type` — required `Uprising`, `Intelligence`, `Disaster`, or `Resource` incident type.

```xml
<Actions>
  <DestroyUnits>
    <Units>
      <SelectRandom Count="1">
        <From>
          <SelectBuildings PlanetBinding="$planet" Category="PlanetaryDefense"/>
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

**Options**

- `Units` — required unit selector collection.

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

Choose exactly one ownership domain: `Planets` or `Units`. Planet transfers use planetary-control rules; unit transfers retain valid containment while ownership indexes are updated.

**Options**

- `FactionInstanceID` — required new owner.
- Exactly one of `Planets` or `Units` is required, containing matching selectors.

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

- `UnitInstanceID` — optional direct unit source.
- `Units` — optional selectors and `SpawnUnits` sources.
- `DestinationInstanceID` — optional direct destination.
- `Destination` — optional destination selector.
- At least one unit and one destination must resolve.

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

**Options**

- `UnitInstanceID` — optional direct unit source.
- `Units` — optional existing-unit selector collection.
- `DestinationInstanceID` — optional direct destination.
- `Destination` — optional destination selector.
- At least one unit and one destination must resolve.

```xml
<SendUnits UnitInstanceID="DARTH_VADER" DestinationInstanceID="YAVIN"/>
```

Both `PlaceUnits` and `SendUnits` accept direct `UnitInstanceID` and `DestinationInstanceID` attributes or typed `Units` and `Destination` selector collections. A destination collection must resolve exactly one destination unless it explicitly uses `SelectFirst`.

### SetNodeActive

`SetNodeActive` controls whether retained scene nodes participate in gameplay. Inactive nodes remain
attached to the scene graph and are preserved in saves, but ordinary gameplay queries ignore them.
Reactivate a returning unit before placing or sending it.

**Options**

- `InstanceID` — optional direct scene-node ID.
- `IsActive` — required state.
- `Nodes` — optional selector collection.
- At least one direct node or selected node must resolve.

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

Capturing requires `CaptorFactionInstanceID`. Releasing must omit it. `CanEscape` defaults to `true`.

**Options**

- `IsCaptured` — required state.
- `OfficerInstanceID` — optional direct officer ID.
- `Officers` — optional officer selector collection.
- `CaptorFactionInstanceID` — required when capturing and forbidden when releasing.
- `CanEscape` — optional escape state; defaults to `true`.

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
- `OfficerInstanceID` — optional direct officer ID.
- `Officers` — optional officer selector collection.
- `ReferenceOfficerInstanceID` — required by `PercentOfPositiveGap`.
- `MinimumAmount` — optional lower bound on the calculated change.
- Exactly one of `Amount`, `PercentOfStored`, `PercentOfEffective`, or `PercentOfPositiveGap` is
  required.

```xml
<ChangeOfficerRating OfficerInstanceID="LUKE_SKYWALKER" Rating="Combat">
  <Amount>5</Amount>
</ChangeOfficerRating>
```

### IncreaseOfficerForce

This uses the same calculation modes as `ChangeOfficerRating`, but every configured and calculated change must increase Force progression.

**Options**

- `OfficerInstanceID` — optional direct officer ID.
- `Officers` — optional officer selector collection.
- `ReferenceOfficerInstanceID` — required by `PercentOfPositiveGap`.
- `MinimumAmount` — optional positive lower bound.
- Exactly one positive `Amount`, `PercentOfStored`, `PercentOfEffective`, or
  `PercentOfPositiveGap` is required.

```xml
<IncreaseOfficerForce OfficerInstanceID="LUKE_SKYWALKER"
                      ReferenceOfficerInstanceID="DARTH_VADER"
                      MinimumAmount="1">
  <PercentOfPositiveGap>25</PercentOfPositiveGap>
</IncreaseOfficerForce>
```

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

**Options**

- `OfficerInstanceID` — required officer ID.

```xml
<SetForceEligible OfficerInstanceID="LEIA_ORGANA"/>
```

### ApplyOfficerInjury

The action rolls an inclusive injury value and records the standard officer-injured result.

**Options**

- `OfficerInstanceID` — required officer ID.
- `MinimumInjury` — required minimum injury.
- `MaximumInjury` — required maximum injury and cannot be lower than the minimum.

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
  <ImagePath>Pack/Shared/Events/MessageBackgrounds/luke-encounters-vader</ImagePath>
  <AudioPath>Pack/Shared/Events/JediConfrontation/Audio/luke-vader</AudioPath>
</TriggerDuel>
```

The duel produces a result consumed by the typed [`DuelCompleted`](Triggers.md#duelcompleted) trigger.

### SetOfficerImages

Image paths are merged into the officer's active image set, so omitted paths remain unchanged.

**Options**

- `OfficerInstanceID` — required officer ID.
- `DisplayImagePath`, `SmallDisplayImagePath`, `MessageImagePath`, and
  `EncyclopediaImagePath` — optional paths; supply at least one.

```xml
<SetOfficerImages OfficerInstanceID="LUKE_SKYWALKER">
  <DisplayImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-display</DisplayImagePath>
  <SmallDisplayImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-small-display</SmallDisplayImagePath>
  <MessageImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-message</MessageImagePath>
  <EncyclopediaImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-encyclopedia</EncyclopediaImagePath>
</SetOfficerImages>
```

### SetOfficerVoiceSet

Replaces authored categories in the officer's active voice set. Omitted categories remain unchanged.

**Options**

- `OfficerInstanceID` — required officer ID.
- Each optional voice-category child contains one or more `Path` elements.

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
    <Path>Pack/Factions/Alliance/Units/Officers/OFAL003/Voice/advanced-mission-success-01</Path>
  </MissionSuccess>
</SetOfficerVoiceSet>
```

## Display metadata

These actions accept one direct `TargetInstanceID` or a `Targets` selector collection.

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
