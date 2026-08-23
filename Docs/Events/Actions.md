# Actions

Actions change game state. They execute from top to bottom against one shared context, so later
actions observe earlier state changes.

Requests and results produced by event actions retain the source event's `InstanceID`. They can
activate other result-triggered events, but they do not generate automatic strategy messages.
Use `SendMessage` when the event should communicate with a player.

## Contents

- [Flow and event state](#flow-and-event-state)
  - [`If`](#if)
  - [`RollRandom`](#rollrandom)
  - [`PerformSkillCheck`](#performskillcheck)
  - [`SetEventVariable`](#seteventvariable)
- [Information and messages](#information-and-messages)
  - [`RevealToFaction`](#revealtofaction)
  - [`SendMessage`](#sendmessage)
- [Planets and resources](#planets-and-resources)
  - [`ChangePlanetStat`](#changeplanetstat)
  - [`ReducePlanetStats`](#reduceplanetstats)
- [Units and ownership](#units-and-ownership)
  - [`DestroyUnits`](#destroyunits)
  - [`ChangeOwner`](#changeowner)
  - [`PlaceUnits`](#placeunits)
  - [`SendUnits`](#sendunits)
  - [`SetNodeActive`](#setnodeactive)
- [Officers](#officers)
  - [`SetCaptureStatus`](#setcapturestatus)
  - [`ChangeOfficerRating`](#changeofficerrating)
  - [`IncreaseForceRank`](#increaseforcerank)
  - [`SetForceSensitive`](#setforcesensitive)
  - [`SetForceEligible`](#setforceeligible)
  - [`ApplyOfficerInjury`](#applyofficerinjury)
  - [`TriggerDuel`](#triggerduel)
  - [`SetOfficerImages`](#setofficerimages)
  - [`SetOfficerVoiceSet`](#setofficervoiceset)
- [Display metadata](#display-metadata)
  - [`SetDisplayName`](#setdisplayname)
  - [`SetDisplayStatus`](#setdisplaystatus)
  - [`ClearDisplayStatus`](#cleardisplaystatus)

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

Conditionally executes one action collection.

**Required options**

- `Conditionals` **[Required]:** The conditionals that determine which branch executes.
- `Actions` **[Required]:** The actions to execute when every conditional passes.

**Optional options**

- `Else` **[Optional]:** The actions to execute when any conditional fails.

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

Executes one randomly selected eligible outcome.

**Required options**

- `Outcomes` **[Required]:** One or more outcomes from which to choose.
- `Weight` **[Required]:** The positive relative selection weight assigned to an `Outcome`.
- `Actions` **[Required]:** The actions to execute when an `Outcome` is selected.

**Optional options**

- `Conditionals` **[Optional]:** The conditionals an `Outcome` must satisfy to be eligible.

Outcomes whose conditionals fail are excluded. Weights are relative, so weights `30` and `70` produce
a 30/70 split. The action does nothing when no outcome remains eligible.

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

Executes success or failure actions from an officer rating check.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer whose rating is checked.
- `Rating` **[Required]:** The officer rating used for the check: `Diplomacy`, `Espionage`, `Combat`, `Leadership`, `ShipResearch`, `TroopResearch`, or `FacilityResearch`.
- `ProbabilityTable` **[Required]:** The name of the mission probability table used for the check.

**Optional options**

- `RatingMultiplier` **[Optional]:** The nonzero multiplier applied to the effective rating; defaults to `1`.
- `OnSuccess` **[Optional]:** The actions to execute when the check succeeds.
- `OnFailure` **[Optional]:** The actions to execute when the check fails.

The check looks up the named mission probability table using the officer's effective rating
multiplied by `RatingMultiplier`. It does not emit a separate result.

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

### SetEventVariable

Updates a saved integer shared by all events.

**Required options**

- `Key` **[Required]:** The persistent key identifying the event variable.
- `Operand` **[Required]:** The integer used by the selected operation.

**Optional options**

- `Operation` **[Optional]:** The operation applied to the variable: `Set`, `Add`, `Minimum`, or `Maximum`; defaults to `Set`.

```xml
<SetEventVariable>
  <Key>naboo.attacks</Key>
  <Operation>Add</Operation>
  <Operand>1</Operand>
</SetEventVariable>
```

Use [`EvaluateEventVariable`](Conditionals.md) to read the value from a conditional.

## Information and messages

### RevealToFaction

Provides a faction with current intelligence about selected game objects.

**Required options**

- `FactionInstanceID` **[Required]:** The `InstanceID` of the faction receiving the intelligence.
- `Targets` **[Required]:** The selectors identifying the game objects to reveal.

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

Delivers a strategy message to an explicitly identified faction.

**Required options**

- `RecipientFactionInstanceID` **[Required]:** The `InstanceID` of the faction receiving the message.

**Optional options**

- `SubjectInstanceID` **[Optional]:** The `InstanceID` of the message subject; `SubjectBinding` takes precedence when both are provided.
- `SubjectBinding` **[Optional]:** A binding that resolves the message subject; takes precedence over `SubjectInstanceID`.
- `RelatedSubjectInstanceID` **[Optional]:** The `InstanceID` of a secondary subject associated with the message.
- `LocationInstanceID` **[Optional]:** The `InstanceID` of the message location; `LocationBinding` takes precedence when both are provided.
- `LocationBinding` **[Optional]:** A binding that resolves the message location; takes precedence over `LocationInstanceID`.
- `Type` **[Optional]:** The message category: `PopularSupport`, `Fleet`, `Mission`, `Resource`, `Manufacturing`, `Defense`, `Conflict`, `Chat`, or `Advice`; defaults to `Advice`.
- `Subject` **[Optional]:** The message title; supports context tokens such as `{subject}` and `{location}`.
- `Body` **[Optional]:** The message body; supports context tokens such as `{subject}` and `{location}`.
- `BackgroundImage` **[Optional]:** The message background, supplied by exactly one `Key`, `Path`, or string-valued `Binding`.
- `OverlayImage` **[Optional]:** The `Path` to the message overlay; when omitted, an officer subject supplies its current message image.
- `BackgroundAudio` **[Optional]:** The message background audio, supplied by exactly one `Path` or string-valued `Binding`.
- `OfficerVoice` **[Optional]:** Explicit `Path` or `Preset`; presets are `Order`, `PersonnelArrived`, `MissionSuccess`, `MissionFailure`, `MissionAbort`, `Released`, `Recovered`, `EnemyDetected`, `ForceGrowth`, `ForceUserDiscovered`, `TraitorDiscovered`, and `RescueAttempt`, and require an officer subject.
- `AdvisorNotification` **[Optional]:** Optional `LifetimeTicks`, `Droid`, and `Protocol` settings plus a `Preset` of `None`, `PositivePopularSupport`, `NegativePopularSupport`, `Manufacturing`, `Research`, `FleetArrived`, `UnitsArrived`, `CapitalShipRepaired`, `StarfighterRepaired`, `Maintenance`, `BlockadeInitiated`, `BlockadeDetected`, `FieldPersonnel`, `AgentReport`, `PlanetaryStatus`, `PrisonerEscaped`, `InterceptedCommunication`, `Bombardment`, `PlanetaryAssault`, `SubjectReport`, `SubjectCaptured`, or `SubjectReleased`.

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

## Planets and resources

### ChangePlanetStat

Adjusts one stat on one or more planets.

**Required options**

- `Stat` **[Required]:** The planet stat to adjust: `RawResourceNodes` or `EnergyCapacity`.
- `PlanetInstanceID` **[Required]:** The `InstanceID` of a planet to adjust; at least one planet source must be provided, and `PlanetBinding` takes precedence when both attributes are present.
- `PlanetBinding` **[Required]:** A binding that resolves a planet to adjust; at least one planet source must be provided, and this takes precedence over `PlanetInstanceID`.
- `Planets` **[Required]:** The selectors identifying planets to adjust; at least one planet source must be provided, and these may be combined with the direct or bound planet.
- `Amount` **[Required]:** Signed fixed adjustment; use either this or `PercentOfCurrent`.
- `PercentOfCurrent` **[Required]:** Signed percentage adjustment; use either this or `Amount`.

```xml
<ChangePlanetStat PlanetInstanceID="NABOO" Stat="RawResourceNodes">
  <Amount>5</Amount>
</ChangePlanetStat>
```

Values cannot fall below zero.

### ReducePlanetStats

Randomly removes points from selected planet stats without reducing them below zero.

**Required options**

- `PlanetInstanceID` **[Required]:** The `InstanceID` of the affected planet; use either this or `PlanetBinding`.
- `PlanetBinding` **[Required]:** A binding that resolves the affected planet; use either this or `PlanetInstanceID`.
- `LossProbabilityPerResource` **[Required]:** Probability from `0` through `1`, rolled independently for each current point.
- `MinimumTotalLoss` **[Required]:** Minimum combined loss, capped by the total available points.
- `Stats` **[Required]:** One or more planet stats from which points may be removed.
- `Name` **[Required]:** The stat selected by each `Stat` element: `RawResourceNodes` or `EnergyCapacity`.

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

## Units and ownership

### DestroyUnits

Permanently deletes every selected unit and the contents of selected containers.

**Required options**

- `Units` **[Required]:** The selectors identifying the units to destroy.

**Optional options**

- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet recorded as the destruction context; does not filter selected units.
- `PlanetBinding` **[Optional]:** A binding that resolves the planet recorded as the destruction context; takes precedence over `PlanetInstanceID` and does not filter selected units.

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

Transfers selected planets or units to another faction.

**Required options**

- `FactionInstanceID` **[Required]:** The `InstanceID` of the faction receiving ownership.
- `Planets` **[Required]:** The selectors identifying planets to transfer; use either this or `Units`.
- `Units` **[Required]:** The selectors identifying units to transfer; use either this or `Planets`.

Planet transfers use planetary-control rules. Unit transfers leave units at their current scene
locations. Only selected nodes change owner.

```xml
<ChangeOwner FactionInstanceID="FNALL1">
  <Units>
    <SelectCapitalShips InstanceID="STAR_DESTROYER_12"/>
    <SelectOfficers InstanceID="OFFICER_7"/>
  </Units>
</ChangeOwner>
```

### PlaceUnits

Immediately places existing or newly spawned units at one valid destination.

**Required options**

- `UnitInstanceID` **[Required]:** The `InstanceID` of an existing unit to place; at least one unit source must be provided and this may be combined with `Units`.
- `Units` **[Required]:** The selectors and `SpawnUnits` entries producing units to place; at least one unit source must be provided and this may be combined with `UnitInstanceID`.
- `DestinationInstanceID` **[Required]:** The `InstanceID` of the destination; use either this or `Destination`.
- `Destination` **[Required]:** The selectors that must resolve exactly one destination; use either this or `DestinationInstanceID`.

```xml
<PlaceUnits DestinationInstanceID="NABOO">
  <Units>
    <SelectOfficers InstanceID="LUKE_SKYWALKER"/>
    <SpawnUnits TypeID="SFAL02" Count="3" OwnerFactionInstanceID="FNALL1"/>
    <SpawnUnits TypeID="REAL002" Count="2" OwnerFactionInstanceID="FNALL1"/>
  </Units>
</PlaceUnits>
```

Each spawned unit receives a new runtime instance ID and starts complete and stationary.

### SendUnits

Moves active, already-placed units through normal validation and transit.

**Required options**

- `UnitInstanceID` **[Required]:** The `InstanceID` of an existing unit to send; at least one unit source must be provided and this may be combined with `Units`.
- `Units` **[Required]:** The selectors identifying existing units to send; at least one unit source must be provided and this may be combined with `UnitInstanceID`.
- `DestinationInstanceID` **[Required]:** The `InstanceID` of the destination; use either this or `Destination`.
- `Destination` **[Required]:** The selectors that must resolve exactly one destination; use either this or `DestinationInstanceID`.

```xml
<SendUnits UnitInstanceID="DARTH_VADER" DestinationInstanceID="YAVIN"/>
```

Transfer actions submit requests to the normal movement rules. If no destination accepts the whole
group, existing units remain where they were and newly spawned units are not retained; the event
activation itself is still consumed. Use `SelectFirst` to provide ordered fallback destinations.

### SetNodeActive

Activates or deactivates retained scene nodes. Inactive nodes remain
attached to the scene graph and are preserved in saves, but ordinary gameplay queries ignore them.
Reactivate a returning unit before placing or sending it.

**Required options**

- `IsActive` **[Required]:** Whether the selected nodes are active (`true`) or inactive (`false`).
- `InstanceID` **[Required]:** The `InstanceID` of a scene node to update; at least one node source must be provided and this may be combined with `Nodes`.
- `Nodes` **[Required]:** The selectors identifying scene nodes to update; at least one node source must be provided and this may be combined with `InstanceID`.

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

Captures or releases one or more officers without moving them.

**Required options**

- `IsCaptured` **[Required]:** Whether the selected officers are captured (`true`) or released (`false`).
- `OfficerInstanceID` **[Required]:** The `InstanceID` of an officer to capture or release; at least one officer source must be provided and this may be combined with `Officers`.
- `Officers` **[Required]:** The selectors identifying officers to capture or release; at least one officer source must be provided and this may be combined with `OfficerInstanceID`.
- `CaptorFactionInstanceID` **[Required]:** The `InstanceID` of the capturing faction when `IsCaptured` is `true`; omit when releasing.

**Optional options**

- `CanEscape` **[Optional]:** Whether captured officers may escape; defaults to `true`. Releasing always resets it
  to `true`.

```xml
<SetCaptureStatus OfficerInstanceID="HAN_SOLO"
                  IsCaptured="true"
                  CaptorFactionInstanceID="FNEMP1"
                  CanEscape="true"/>
```

### ChangeOfficerRating

Adjusts one rating for one or more officers.

**Required options**

- `Rating` **[Required]:** The officer rating to adjust: `Diplomacy`, `Espionage`, `Combat`, `Leadership`, `ShipResearch`, `TroopResearch`, or `FacilityResearch`.
- `OfficerInstanceID` **[Required]:** The `InstanceID` of an officer to adjust; at least one officer source must be provided and this may be combined with `Officers`.
- `Officers` **[Required]:** The selectors identifying officers to adjust; at least one officer source must be provided and this may be combined with `OfficerInstanceID`.
- `ReferenceOfficerInstanceID` **[Required]:** The `InstanceID` of the comparison officer when using `PercentOfPositiveGap`.
- `Amount` **[Required]:** Signed integer added to the stored rating; use exactly one adjustment option.
- `PercentOfStored` **[Required]:** Signed change calculated from the stored rating; use exactly one adjustment option.
- `PercentOfEffective` **[Required]:** Signed change calculated from the effective rating and applied to the stored rating; use exactly one adjustment option.
- `PercentOfPositiveGap` **[Required]:** Non-negative percentage of the effective-rating gap to `ReferenceOfficerInstanceID`; use exactly one adjustment option.

**Optional options**

- `MinimumAmount` **[Optional]:** Non-negative lower bound used only with `PercentOfPositiveGap`.

```xml
<ChangeOfficerRating OfficerInstanceID="LUKE_SKYWALKER" Rating="Combat">
  <Amount>5</Amount>
</ChangeOfficerRating>
```

### IncreaseForceRank

Increases Force progression for one or more officers.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of an officer whose Force progression will increase; at least one officer source must be provided and this may be combined with `Officers`.
- `Officers` **[Required]:** The selectors identifying officers whose Force progression will increase; at least one officer source must be provided and this may be combined with `OfficerInstanceID`.
- `ReferenceOfficerInstanceID` **[Required]:** The `InstanceID` of the comparison officer when using `PercentOfPositiveGap`.
- `Amount` **[Required]:** Positive fixed adjustment; use exactly one adjustment option.
- `PercentOfStored` **[Required]:** Positive change calculated from `ForceValue`; use exactly one adjustment option.
- `PercentOfEffective` **[Required]:** Positive change calculated from the effective `ForceRank`; use exactly one adjustment option.
- `PercentOfPositiveGap` **[Required]:** Positive percentage of the effective-rank gap to `ReferenceOfficerInstanceID`; use exactly one adjustment option.

**Optional options**

- `MinimumAmount` **[Optional]:** Non-negative lower bound used only with `PercentOfPositiveGap`.

```xml
<IncreaseForceRank OfficerInstanceID="LUKE_SKYWALKER"
                      ReferenceOfficerInstanceID="DARTH_VADER"
                      MinimumAmount="1">
  <PercentOfPositiveGap>25</PercentOfPositiveGap>
</IncreaseForceRank>
```

### SetForceSensitive

Marks an officer as having latent Force potential.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer whose Force sensitivity will be enabled.

```xml
<SetForceSensitive OfficerInstanceID="LEIA_ORGANA"/>
```

### SetForceEligible

Makes a Force-sensitive officer eligible to develop Force ability.
Eligibility requires sensitivity, so use both actions when revealing a previously unknown candidate.
On the first eligibility change, `ForceValue` is raised to at least `JediLevel` plus a uniformly
rolled value from zero through `JediLevelVariance`.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer whose Force eligibility will be enabled.

```xml
<SetForceEligible OfficerInstanceID="LEIA_ORGANA"/>
```

### ApplyOfficerInjury

Applies a randomly selected amount of injury to an officer.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer to injure.
- `MinimumInjury` **[Required]:** The minimum injury amount that may be applied; cannot be negative.
- `MaximumInjury` **[Required]:** The maximum injury amount that may be applied; cannot be lower than `MinimumInjury`.

```xml
<ApplyOfficerInjury OfficerInstanceID="LUKE_SKYWALKER">
  <MinimumInjury>1</MinimumInjury>
  <MaximumInjury>5</MaximumInjury>
</ApplyOfficerInjury>
```

### TriggerDuel

Requests normal duel resolution between two active officers.

**Required options**

- `FirstOfficerInstanceID` **[Required]:** The `InstanceID` of the first officer in the duel.
- `SecondOfficerInstanceID` **[Required]:** The `InstanceID` of the second officer in the duel.

**Optional options**

- `ImagePath` **[Optional]:** The image shown when presenting this duel instead of the default image.
- `AudioPath` **[Optional]:** The audio played when presenting this duel instead of the default audio.

```xml
<TriggerDuel FirstOfficerInstanceID="LUKE_SKYWALKER"
             SecondOfficerInstanceID="DARTH_VADER">
  <ImagePath>Pack/Factions/Alliance/Strategy/Messages/Images/luke-encounters-vader-trained</ImagePath>
  <AudioPath>Pack/Shared/Events/JediConfrontation/Audio/luke-vader-dagobah-complete</AudioPath>
</TriggerDuel>
```

The duel produces a result consumed by the typed [`DuelCompleted`](Triggers.md#duelcompleted) trigger.

### SetOfficerImages

Updates the images used to present an officer.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer whose images will be updated.

**Optional options**

- `DisplayImagePath` **[Optional]:** The path to the officer's large display image; omission preserves the current path.
- `SmallDisplayImagePath` **[Optional]:** The path to the officer's small display image; omission preserves the current path.
- `MessageImagePath` **[Optional]:** The path to the officer image shown in messages; omission preserves the current path.
- `EncyclopediaImagePath` **[Optional]:** The path to the officer's encyclopedia image; omission preserves the current path.

```xml
<SetOfficerImages OfficerInstanceID="LUKE_SKYWALKER">
  <DisplayImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-display</DisplayImagePath>
  <SmallDisplayImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-small-display</SmallDisplayImagePath>
  <MessageImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/message</MessageImagePath>
  <EncyclopediaImagePath>Pack/Factions/Alliance/Units/Officers/OFAL003/jedi-encyclopedia</EncyclopediaImagePath>
</SetOfficerImages>
```

### SetOfficerVoiceSet

Updates the voice lines used to present an officer.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer whose voice lines will be updated.

**Optional options**

- `Order` **[Optional]:** The paths to voice lines played when the officer receives an order.
- `PersonnelArrived` **[Optional]:** The paths to voice lines played when the officer arrives.
- `MissionSuccess` **[Optional]:** The paths to voice lines played after mission success.
- `MissionFailure` **[Optional]:** The paths to voice lines played after mission failure.
- `MissionAbort` **[Optional]:** The paths to voice lines played after mission cancellation.
- `Released` **[Optional]:** The paths to voice lines played when the officer is released.
- `Recovered` **[Optional]:** The paths to voice lines played when the officer recovers.
- `EnemyDetected` **[Optional]:** The paths to voice lines played when the officer detects an enemy.
- `ForceGrowth` **[Optional]:** The paths to voice lines played when the officer's Force ability grows.
- `ForceUserDiscovered` **[Optional]:** The paths to voice lines played when another Force user is discovered.
- `TraitorDiscovered` **[Optional]:** The paths to voice lines played when a traitor is discovered.
- `RescueAttempt` **[Optional]:** The paths to voice lines played during a rescue attempt.

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

### SetDisplayName

Changes the display name of one or more scene nodes.

**Required options**

- `Name` **[Required]:** The display name assigned to every selected node.
- `TargetInstanceID` **[Required]:** The `InstanceID` of a node to rename; at least one target source must be provided and this may be combined with `Targets`.
- `Targets` **[Required]:** The selectors identifying nodes to rename; at least one target source must be provided and this may be combined with `TargetInstanceID`.

```xml
<SetDisplayName TargetInstanceID="LUKE_SKYWALKER" Name="Luke Skywalker (Jedi)"/>
```

### SetDisplayStatus

Changes the supplemental status text shown for one or more scene nodes.

**Required options**

- `Status` **[Required]:** The supplemental status text assigned to every selected node.
- `TargetInstanceID` **[Required]:** The `InstanceID` of a node whose status will change; at least one target source must be provided and this may be combined with `Targets`.
- `Targets` **[Required]:** The selectors identifying nodes whose status will change; at least one target source must be provided and this may be combined with `TargetInstanceID`.

```xml
<SetDisplayStatus TargetInstanceID="LUKE_SKYWALKER" Status="On Mission (Dagobah)"/>
```

### ClearDisplayStatus

Removes the supplemental status text shown for one or more scene nodes.

**Required options**

- `TargetInstanceID` **[Required]:** The `InstanceID` of a node whose status will be cleared; at least one target source must be provided and this may be combined with `Targets`.
- `Targets` **[Required]:** The selectors identifying nodes whose status will be cleared; at least one target source must be provided and this may be combined with `TargetInstanceID`.

```xml
<ClearDisplayStatus TargetInstanceID="LUKE_SKYWALKER"/>
```

---

<p align="center"><a href="Conditionals.md">← Conditionals</a> · <a href="Index.md">Event guide</a> · <a href="Examples.md">Examples →</a></p>
