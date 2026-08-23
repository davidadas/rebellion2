# Actions

Actions change game state. They execute from top to bottom against one shared context, so later
actions observe earlier state changes.

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

`If` conditionally executes one action collection.

**Options**

- `Conditionals` **[Required]:** condition collection.
- `Actions` **[Required]:** action collection executed when the conditions pass.
- `Else` **[Optional]:** action collection executed when the conditions fail.

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

`RollRandom` executes one randomly selected eligible outcome.

**Options**

- `Outcomes` **[Required]:** child containing one or more `Outcome` elements.
- `Weight` **[Required]:** positive attribute on each `Outcome`.
- `Conditionals` **[Optional]:** conditions for making an outcome eligible.
- `Actions` **[Required]:** actions for each outcome.

Outcomes whose conditions fail are excluded. Weights are relative, so weights `30` and `70` produce
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

`PerformSkillCheck` executes success or failure actions from an officer rating check.

**Options**

- `OfficerInstanceID` **[Required]:** officer ID.
- `Rating` **[Required]:** `Diplomacy`, `Espionage`, `Combat`, `Leadership`, `ShipResearch`, `TroopResearch`, or `FacilityResearch`.
- `ProbabilityTable` **[Required]:** probability-table name.
- `RatingMultiplier` **[Optional]:** nonzero multiplier; defaults to `1`.
- `OnSuccess` **[Optional]:** actions run after a successful check.
- `OnFailure` **[Optional]:** actions run after a failed check.

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

`SetEventVariable` updates a saved integer shared by all events.

**Options**

- `Key` **[Required]:** variable key.
- `Operand` **[Required]:** integer input.
- `Operation` **[Optional]:** `Set`, `Add`, `Minimum`, or `Maximum`; defaults to `Set`.

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

- `FactionInstanceID` **[Required]:** recipient faction.
- `Targets` **[Required]:** selector collection containing the objects to reveal.

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

`SendMessage` delivers a strategy message to an explicitly identified faction.

**Options**

- `RecipientFactionInstanceID` **[Required]:** recipient faction.
- `SubjectInstanceID` **[Optional]:** message subject instance ID; `SubjectBinding` takes precedence when both are provided.
- `SubjectBinding` **[Optional]:** message subject binding; takes precedence over `SubjectInstanceID`.
- `RelatedSubjectInstanceID` **[Optional]:** secondary subject.
- `LocationInstanceID` **[Optional]:** message location instance ID; `LocationBinding` takes precedence when both are provided.
- `LocationBinding` **[Optional]:** message location binding; takes precedence over `LocationInstanceID`.
- `Type` **[Optional]:** `PopularSupport`, `Fleet`, `Mission`, `Resource`, `Manufacturing`, `Defense`, `Conflict`, `Chat`, or `Advice`; defaults to `Advice`.
- `Subject` **[Optional]:** message subject text; supports context tokens such as `{subject}` and `{location}`.
- `Body` **[Optional]:** message body text; supports context tokens such as `{subject}` and `{location}`.
- `BackgroundImage` **[Optional]:** exactly one `Key`, `Path`, or string-valued `Binding`.
- `OverlayImage` **[Optional]:** `Path`; when omitted, an officer subject supplies its current message image.
- `BackgroundAudio` **[Optional]:** exactly one `Path` or string-valued `Binding`.
- `OfficerVoice` **[Optional]:** explicit `Path` or `Preset`; presets are `Order`, `PersonnelArrived`, `MissionSuccess`, `MissionFailure`, `MissionAbort`, `Released`, `Recovered`, `EnemyDetected`, `ForceGrowth`, `ForceUserDiscovered`, `TraitorDiscovered`, and `RescueAttempt`, and require an officer subject.
- `AdvisorNotification` **[Optional]:** optional `LifetimeTicks`, `Droid`, and `Protocol` settings plus a `Preset` of `None`, `PositivePopularSupport`, `NegativePopularSupport`, `Manufacturing`, `Research`, `FleetArrived`, `UnitsArrived`, `CapitalShipRepaired`, `StarfighterRepaired`, `Maintenance`, `BlockadeInitiated`, `BlockadeDetected`, `FieldPersonnel`, `AgentReport`, `PlanetaryStatus`, `PrisonerEscaped`, `InterceptedCommunication`, `Bombardment`, `PlanetaryAssault`, `SubjectReport`, `SubjectCaptured`, or `SubjectReleased`.

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

`ChangePlanetStat` adjusts one stat on one or more planets.

**Options**

- `Stat` **[Required]:** `RawResourceNodes` or `EnergyCapacity`.
- `PlanetInstanceID` **[Required]:** direct planet source; at least one planet source must be provided, and `PlanetBinding` takes precedence when both attributes are present.
- `PlanetBinding` **[Required]:** bound planet source; at least one planet source must be provided, and this takes precedence over `PlanetInstanceID`.
- `Planets` **[Required]:** planet selector collection; at least one planet source must be provided, and selected planets are combined with the direct or bound planet.
- `Amount` **[Required]:** signed fixed adjustment; use either this or `PercentOfCurrent`.
- `PercentOfCurrent` **[Required]:** signed percentage adjustment; use either this or `Amount`.

```xml
<ChangePlanetStat PlanetInstanceID="NABOO" Stat="RawResourceNodes">
  <Amount>5</Amount>
</ChangePlanetStat>
```

Values cannot fall below zero.

### ReducePlanetStats

`ReducePlanetStats` randomly removes points from selected planet stats without reducing them below zero.

**Options**

- `PlanetInstanceID` **[Required]:** direct planet source; use either this or `PlanetBinding`.
- `PlanetBinding` **[Required]:** bound planet source; use either this or `PlanetInstanceID`.
- `LossProbabilityPerResource` **[Required]:** probability from `0` through `1`, rolled independently for each current point.
- `MinimumTotalLoss` **[Required]:** minimum combined loss, capped by the total available points.
- `Stats` **[Required]:** child containing one or more `Stat` elements.
- `Name` **[Required]:** planet-stat attribute on each `Stat`.

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

`DestroyUnits` permanently deletes every selected unit and the contents of selected containers.

**Options**

- `Units` **[Required]:** unit selector collection.
- `PlanetInstanceID` **[Optional]:** direct result context. Does not filter the selected units.
- `PlanetBinding` **[Optional]:** bound result context. Takes precedence over `PlanetInstanceID` and does not filter the selected units.

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

`ChangeOwner` transfers selected planets or units to another faction.

**Options**

- `FactionInstanceID` **[Required]:** new owner.
- `Planets` **[Required]:** planet selector collection; use either this or `Units`.
- `Units` **[Required]:** unit selector collection; use either this or `Planets`.

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

`PlaceUnits` immediately places existing or newly spawned units at one valid destination.

**Options**

- `UnitInstanceID` **[Required]:** direct unit source; at least one unit source must be provided and this may be combined with `Units`.
- `Units` **[Required]:** selected units and `SpawnUnits` sources; at least one unit source must be provided and this may be combined with `UnitInstanceID`.
- `DestinationInstanceID` **[Required]:** direct destination; use either this or `Destination`.
- `Destination` **[Required]:** destination selector collection; use either this or `DestinationInstanceID` and resolve exactly one destination.

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

`SendUnits` moves active, already-placed units through normal validation and transit. It cannot spawn units.

**Options**

- `UnitInstanceID` **[Required]:** direct existing-unit source; at least one unit source must be provided and this may be combined with `Units`.
- `Units` **[Required]:** existing-unit selector collection; at least one unit source must be provided and this may be combined with `UnitInstanceID`.
- `DestinationInstanceID` **[Required]:** direct destination; use either this or `Destination`.
- `Destination` **[Required]:** destination selector collection; use either this or `DestinationInstanceID` and resolve exactly one destination.

```xml
<SendUnits UnitInstanceID="DARTH_VADER" DestinationInstanceID="YAVIN"/>
```

Transfer actions submit requests to the normal movement rules. If no destination accepts the whole
group, existing units remain where they were and newly spawned units are not retained; the event
activation itself is still consumed. Use `SelectFirst` to provide ordered fallback destinations.

### SetNodeActive

`SetNodeActive` controls whether retained scene nodes participate in gameplay. Inactive nodes remain
attached to the scene graph and are preserved in saves, but ordinary gameplay queries ignore them.
Reactivate a returning unit before placing or sending it.

**Options**

- `IsActive` **[Required]:** state.
- `InstanceID` **[Required]:** direct scene-node ID; at least one node source must be provided and this may be combined with `Nodes`.
- `Nodes` **[Required]:** scene-node selector collection; at least one node source must be provided and this may be combined with `InstanceID`.

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

`SetCaptureStatus` captures or releases one or more officers without moving them.

**Options**

- `IsCaptured` **[Required]:** state.
- `OfficerInstanceID` **[Required]:** direct officer ID; at least one officer source must be provided and this may be combined with `Officers`.
- `Officers` **[Required]:** officer selector collection; at least one officer source must be provided and this may be combined with `OfficerInstanceID`.
- `CaptorFactionInstanceID` **[Required]:** capturing faction when `IsCaptured` is `true`; omit when releasing.
- `CanEscape` **[Optional]:** state used when capturing; defaults to `true`. Releasing always resets it
  to `true`.

```xml
<SetCaptureStatus OfficerInstanceID="HAN_SOLO"
                  IsCaptured="true"
                  CaptorFactionInstanceID="FNEMP1"
                  CanEscape="true"/>
```

### ChangeOfficerRating

`ChangeOfficerRating` adjusts one rating for one or more officers.

**Options**

- `Rating` **[Required]:** officer rating.
- `OfficerInstanceID` **[Required]:** direct officer ID; at least one officer source must be provided and this may be combined with `Officers`.
- `Officers` **[Required]:** officer selector collection; at least one officer source must be provided and this may be combined with `OfficerInstanceID`.
- `ReferenceOfficerInstanceID` **[Required]:** reference officer when using `PercentOfPositiveGap`.
- `MinimumAmount` **[Optional]:** non-negative lower bound used only with `PercentOfPositiveGap`.
- `Amount` **[Required]:** signed integer added to the stored rating; use exactly one adjustment option.
- `PercentOfStored` **[Required]:** signed change calculated from the stored rating; use exactly one adjustment option.
- `PercentOfEffective` **[Required]:** signed change calculated from the effective rating and applied to the stored rating; use exactly one adjustment option.
- `PercentOfPositiveGap` **[Required]:** non-negative percentage of the effective-rating gap to `ReferenceOfficerInstanceID`; use exactly one adjustment option.

```xml
<ChangeOfficerRating OfficerInstanceID="LUKE_SKYWALKER" Rating="Combat">
  <Amount>5</Amount>
</ChangeOfficerRating>
```

### IncreaseOfficerForce

`IncreaseOfficerForce` increases Force progression for one or more officers.

**Options**

- `OfficerInstanceID` **[Required]:** direct officer ID; at least one officer source must be provided and this may be combined with `Officers`.
- `Officers` **[Required]:** officer selector collection; at least one officer source must be provided and this may be combined with `OfficerInstanceID`.
- `ReferenceOfficerInstanceID` **[Required]:** reference officer when using `PercentOfPositiveGap`.
- `MinimumAmount` **[Optional]:** non-negative lower bound used only with `PercentOfPositiveGap`.
- `Amount` **[Required]:** positive fixed adjustment; use exactly one adjustment option.
- `PercentOfStored` **[Required]:** positive change calculated from `ForceValue`; use exactly one adjustment option.
- `PercentOfEffective` **[Required]:** positive change calculated from the effective `ForceRank`; use exactly one adjustment option.
- `PercentOfPositiveGap` **[Required]:** positive percentage of the effective-rank gap to `ReferenceOfficerInstanceID`; use exactly one adjustment option.

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

- `OfficerInstanceID` **[Required]:** officer ID.

```xml
<SetForceSensitive OfficerInstanceID="LEIA_ORGANA"/>
```

### SetForceEligible

Marks a Force-sensitive officer's potential as discovered and initializes Force progression.
Eligibility requires sensitivity, so use both actions when revealing a previously unknown candidate.
On the first eligibility change, `ForceValue` is raised to at least `JediLevel` plus a uniformly
rolled value from zero through `JediLevelVariance`.

**Options**

- `OfficerInstanceID` **[Required]:** officer ID.

```xml
<SetForceEligible OfficerInstanceID="LEIA_ORGANA"/>
```

### ApplyOfficerInjury

The action rolls an inclusive injury value and records the standard officer-injured result.

**Options**

- `OfficerInstanceID` **[Required]:** officer ID.
- `MinimumInjury` **[Required]:** non-negative minimum injury.
- `MaximumInjury` **[Required]:** non-negative maximum injury and cannot be lower than the minimum.

```xml
<ApplyOfficerInjury OfficerInstanceID="LUKE_SKYWALKER">
  <MinimumInjury>1</MinimumInjury>
  <MaximumInjury>5</MaximumInjury>
</ApplyOfficerInjury>
```

### TriggerDuel

`TriggerDuel` requests normal duel resolution between two active officers.

**Options**

- `FirstOfficerInstanceID` **[Required]:** first officer.
- `SecondOfficerInstanceID` **[Required]:** second officer.
- `ImagePath` **[Optional]:** presentation-image override.
- `AudioPath` **[Optional]:** presentation-audio override.

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

- `OfficerInstanceID` **[Required]:** officer ID.
- `DisplayImagePath` **[Optional]:** display image path.
- `SmallDisplayImagePath` **[Optional]:** small display image path.
- `MessageImagePath` **[Optional]:** message image path.
- `EncyclopediaImagePath` **[Optional]:** encyclopedia image path.

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

- `OfficerInstanceID` **[Required]:** officer ID.
- `Order` **[Optional]:** order voice-line paths.
- `PersonnelArrived` **[Optional]:** personnel-arrival voice-line paths.
- `MissionSuccess` **[Optional]:** mission-success voice-line paths.
- `MissionFailure` **[Optional]:** mission-failure voice-line paths.
- `MissionAbort` **[Optional]:** mission-abort voice-line paths.
- `Released` **[Optional]:** release voice-line paths.
- `Recovered` **[Optional]:** recovery voice-line paths.
- `EnemyDetected` **[Optional]:** enemy-detection voice-line paths.
- `ForceGrowth` **[Optional]:** Force-growth voice-line paths.
- `ForceUserDiscovered` **[Optional]:** Force-user-discovery voice-line paths.
- `TraitorDiscovered` **[Optional]:** traitor-discovery voice-line paths.
- `RescueAttempt` **[Optional]:** rescue-attempt voice-line paths.

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

Sets a node's authored display name.

**Options**

- `Name` **[Required]:** display name.
- `TargetInstanceID` **[Required]:** direct target; at least one target source must be provided and this may be combined with `Targets`.
- `Targets` **[Required]:** selector collection; at least one target source must be provided and this may be combined with `TargetInstanceID`.

```xml
<SetDisplayName TargetInstanceID="LUKE_SKYWALKER" Name="Luke Skywalker (Jedi)"/>
```

### SetDisplayStatus

Sets supplemental status text without changing gameplay state.

**Options**

- `Status` **[Required]:** display text.
- `TargetInstanceID` **[Required]:** direct target; at least one target source must be provided and this may be combined with `Targets`.
- `Targets` **[Required]:** selector collection; at least one target source must be provided and this may be combined with `TargetInstanceID`.

```xml
<SetDisplayStatus TargetInstanceID="LUKE_SKYWALKER" Status="On Mission (Dagobah)"/>
```

### ClearDisplayStatus

Clears supplemental status text.

**Options**

- `TargetInstanceID` **[Required]:** direct target; at least one target source must be provided and this may be combined with `Targets`.
- `Targets` **[Required]:** selector collection; at least one target source must be provided and this may be combined with `TargetInstanceID`.

```xml
<ClearDisplayStatus TargetInstanceID="LUKE_SKYWALKER"/>
```

---

<p align="center"><a href="Conditions.md">← Conditions</a> · <a href="Index.md">Event guide</a> · <a href="Examples.md">Examples →</a></p>
