# Game Event Examples

These complete examples show common event patterns. Replace the sample IDs, text, and media paths
with values from the content you are editing.

## Send a message on a specific tick

This event executes once when tick 10 becomes eligible.

```xml
<GameEvent TriggerCount="1">
  <InstanceID>MOD_OPENING_REPORT</InstanceID>
  <!-- At is an absolute campaign tick and can execute only once. -->
  <Schedule>
    <At Tick="10"/>
  </Schedule>
  <Actions>
    <SendMessage RecipientFactionInstanceID="FNALL1" Type="Advice">
      <Subject>Opening Report</Subject>
      <Body>Alliance command has completed its initial assessment.</Body>
    </SendMessage>
  </Actions>
</GameEvent>
```

## Repeat an event until a condition is met

This event runs every 50 ticks while Han remains free. Capturing him permanently exhausts it.

```xml
<GameEvent>
  <InstanceID>MOD_HAN_STATUS_REPORT</InstanceID>
  <!-- Omit TriggerCount to allow unlimited successful activations. -->
  <Schedule>
    <Every Ticks="50" InitialDelayTicks="10"/>
  </Schedule>
  <!-- Until permanently exhausts the event before its next activation. -->
  <Until>
    <IsCaptured OfficerInstanceID="HAN_SOLO"/>
  </Until>
  <Actions>
    <SendMessage RecipientFactionInstanceID="FNALL1"
                 SubjectInstanceID="HAN_SOLO"
                 Type="Advice">
      <Subject>Han remains active</Subject>
      <Body>Han Solo remains available for assignment.</Body>
    </SendMessage>
  </Actions>
</GameEvent>
```

## Select a random owned planet

This repeating event selects one Alliance-owned core planet and adds a raw-resource node.

```xml
<GameEvent TriggerCount="5">
  <InstanceID>MOD_RESOURCE_DISCOVERY</InstanceID>
  <Schedule>
    <Random MinimumTicks="100" MaximumTicks="300"/>
  </Schedule>
  <Target>
    <From>
      <!-- Target must resolve exactly one node, so reduce the matching planets to one. -->
      <SelectRandom Count="1">
        <From>
          <SelectPlanets OwnerFactionInstanceID="FNALL1" SectorType="Core"/>
        </From>
      </SelectRandom>
    </From>
  </Target>
  <Actions>
    <!-- With no explicit planet on the action, ChangePlanetStat uses $target. -->
    <ChangePlanetStat Stat="RawResourceNodes">
      <Amount>1</Amount>
    </ChangePlanetStat>
    <SendMessage RecipientFactionInstanceID="FNALL1"
                 LocationBinding="$target"
                 Type="Resource">
      <Subject>Resources discovered</Subject>
      <Body>New raw materials have been discovered on {location}.</Body>
    </SendMessage>
  </Actions>
</GameEvent>
```

## Choose one weighted outcome

Eligible outcomes are selected by relative weight. Here the planet receives a resource increase 30
percent of the time and a message-only outcome 70 percent of the time.

```xml
<GameEvent TriggerCount="1">
  <InstanceID>MOD_NABOO_SURVEY</InstanceID>
  <Schedule>
    <At Tick="100"/>
  </Schedule>
  <Actions>
    <Random>
      <Outcomes>
        <!-- Weights are relative; 30 and 70 form a 30/70 split. -->
        <Outcome Weight="30">
          <Actions>
            <ChangePlanetStat PlanetInstanceID="NABOO" Stat="RawResourceNodes">
              <Amount>1</Amount>
            </ChangePlanetStat>
          </Actions>
        </Outcome>
        <Outcome Weight="70">
          <Actions>
            <SendMessage RecipientFactionInstanceID="FNALL1"
                         LocationInstanceID="NABOO"
                         Type="Resource">
              <Subject>Survey completed</Subject>
              <Body>The survey of {location} found no usable deposits.</Body>
            </SendMessage>
          </Actions>
        </Outcome>
      </Outcomes>
    </Random>
  </Actions>
</GameEvent>
```

## Damage random defensive units

This repeating event selects an owned core planet, destroys a random sample of defensive units,
records the changes as an incident, and sends a message.

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
          <SelectPlanets SectorType="Core"/>
        </From>
      </SelectRandom>
    </From>
  </Target>
  <Conditionals>
    <!-- Omitting FactionInstanceID means any non-neutral owner is accepted. -->
    <IsOwned PlanetBinding="$target"/>
  </Conditionals>
  <Actions>
    <DestroyUnits>
      <Units>
        <!-- Build one candidate pool, then destroy only the random subset. -->
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

## React to a unit arriving

Trigger bindings expose the arriving unit and destination for conditions and actions.

```xml
<GameEvent TriggerCount="1">
  <InstanceID>MOD_EMPEROR_REACHES_CORUSCANT</InstanceID>
  <Triggers>
    <Trigger Event="core:unit.arrived">
      <!-- Bind only the result values needed by the rest of this event. -->
      <Bindings>
        <Bind Argument="UnitInstanceID" As="unitInstanceID"/>
        <Bind Argument="DestinationInstanceID" As="destinationInstanceID"/>
      </Bindings>
    </Trigger>
  </Triggers>
  <Conditionals>
    <!-- Binding aliases are referenced with a leading $. -->
    <EvaluateBinding Binding="$unitInstanceID"
                     Comparison="Equal"
                     CompareTo="EMPEROR_PALPATINE"/>
    <EvaluateBinding Binding="$destinationInstanceID"
                     Comparison="Equal"
                     CompareTo="CORUSCANT"/>
  </Conditionals>
  <Actions>
    <SendMessage RecipientFactionInstanceID="FNEMP1"
                 SubjectInstanceID="EMPEROR_PALPATINE"
                 LocationInstanceID="CORUSCANT"
                 Type="Mission">
      <Subject>Emperor Arrives at Coruscant</Subject>
      <Body>The Emperor has returned to the seat of power.</Body>
      <AdvisorNotification Preset="SubjectReport"/>
    </SendMessage>
  </Actions>
</GameEvent>
```

## React to a mission participant

This event starts a duel only when Luke participated in a completed mission and Vader is at the same
planet.

```xml
<GameEvent>
  <InstanceID>MOD_LUKE_MEETS_VADER</InstanceID>
  <Triggers>
    <Trigger Event="core:mission.completed">
      <Bindings>
        <!-- Participants is the collection captured by the completed mission result. -->
        <Bind Argument="Participants" As="participants"/>
      </Bindings>
    </Trigger>
  </Triggers>
  <Conditionals>
    <BindingIncludesUnit Binding="$participants" UnitInstanceID="LUKE_SKYWALKER"/>
    <!-- ShareAncestor includes units attached through a fleet or mission at that planet. -->
    <ShareAncestor Type="Planet">
      <Units>
        <Unit UnitInstanceID="LUKE_SKYWALKER"/>
        <Unit UnitInstanceID="DARTH_VADER"/>
      </Units>
    </ShareAncestor>
  </Conditionals>
  <Actions>
    <TriggerDuel FirstOfficerInstanceID="LUKE_SKYWALKER"
                 SecondOfficerInstanceID="DARTH_VADER"/>
  </Actions>
</GameEvent>
```

## Branch on an officer skill check

`PerformSkillCheck` runs one nested action list and emits no separate skill-check result.

```xml
<GameEvent TriggerCount="1">
  <InstanceID>MOD_BOUNTY_HUNTERS_FIND_HAN</InstanceID>
  <Schedule>
    <Random MinimumTicks="300" MaximumTicks="600"/>
  </Schedule>
  <Actions>
    <!-- The check performs either OnSuccess or OnFailure inline. -->
    <PerformSkillCheck OfficerInstanceID="HAN_SOLO"
                       Rating="Combat"
                       ProbabilityTable="Abduction"
                       RatingMultiplier="-1">
      <OnSuccess>
        <!-- Capture state and gameplay activity are independent changes. -->
        <SetCaptureStatus OfficerInstanceID="HAN_SOLO"
                          IsCaptured="true"
                          CaptorFactionInstanceID="FNEMP1"
                          CanEscape="false"/>
        <SetActive UnitInstanceID="HAN_SOLO" IsActive="false"/>
      </OnSuccess>
      <OnFailure>
        <SendMessage RecipientFactionInstanceID="FNALL1"
                     SubjectInstanceID="HAN_SOLO"
                     Type="Mission">
          <Subject>Han evades capture</Subject>
          <Body>Bounty hunters failed to capture Han Solo.</Body>
        </SendMessage>
      </OnFailure>
    </PerformSkillCheck>
  </Actions>
</GameEvent>
```

## Spawn and place units

`SpawnUnits` creates runtime instances from an existing definition. `PlaceUnits` immediately places
them at the destination without transit.

```xml
<GameEvent TriggerCount="1">
  <InstanceID>MOD_NABOO_X_WINGS</InstanceID>
  <Schedule>
    <At Tick="200"/>
  </Schedule>
  <Actions>
    <PlaceUnits DestinationInstanceID="NABOO">
      <Units>
        <!-- SpawnUnits creates detached instances; PlaceUnits attaches them to Naboo. -->
        <SpawnUnits TypeID="SFAL02"
                    OwnerFactionInstanceID="FNALL1"
                    Count="3"/>
      </Units>
    </PlaceUnits>
    <SendMessage RecipientFactionInstanceID="FNALL1"
                 LocationInstanceID="NABOO"
                 Type="Fleet">
      <Subject>X-Wings discovered</Subject>
      <Body>Three X-Wing squadrons are now stationed at {location}.</Body>
    </SendMessage>
  </Actions>
</GameEvent>
```

## Reveal selected intelligence

This event reveals one randomly selected Imperial subject at Coruscant to the Alliance.

```xml
<GameEvent TriggerCount="1">
  <InstanceID>MOD_CORUSCANT_INFORMANTS</InstanceID>
  <Schedule>
    <At Tick="250"/>
  </Schedule>
  <Actions>
    <RevealToFaction FactionInstanceID="FNALL1">
      <Targets>
        <!-- Reveal exactly one randomly selected subject from the combined candidates. -->
        <SelectRandom Count="1">
          <From>
            <SelectCapitalShips PlanetInstanceID="CORUSCANT"
                                OwnerFactionInstanceID="FNEMP1"/>
            <SelectOfficers PlanetInstanceID="CORUSCANT"
                            OwnerFactionInstanceID="FNEMP1"/>
            <SelectBuildings PlanetInstanceID="CORUSCANT"
                             OwnerFactionInstanceID="FNEMP1"/>
          </From>
        </SelectRandom>
      </Targets>
    </RevealToFaction>
    <SendMessage RecipientFactionInstanceID="FNALL1"
                 LocationInstanceID="CORUSCANT"
                 Type="Advice">
      <Subject>Informants provide intelligence</Subject>
      <Body>Informants revealed new information about {location}.</Body>
    </SendMessage>
  </Actions>
</GameEvent>
```

## Change ownership

`ChangeOwner` accepts either planets or units. This event transfers every selected completed
starfighter at Naboo to the Alliance.

```xml
<GameEvent TriggerCount="1">
  <InstanceID>MOD_NABOO_STARFIGHTERS_DEFECT</InstanceID>
  <Schedule>
    <At Tick="300"/>
  </Schedule>
  <Actions>
    <!-- ChangeOwner transfers existing units; it does not create or move them. -->
    <ChangeOwner FactionInstanceID="FNALL1">
      <Units>
        <SelectStarfighters PlanetInstanceID="NABOO"
                            ManufacturingStatus="Complete"/>
      </Units>
    </ChangeOwner>
  </Actions>
</GameEvent>
```

## Temporarily remove and return an officer

The first event retains Luke in the scene graph but excludes him from gameplay. The dependent event
reactivates him and attempts to place him at his recorded previous location.

```xml
<GameEvent TriggerCount="1">
  <InstanceID>MOD_OFFICER_LEAVES</InstanceID>
  <Schedule>
    <At Tick="300"/>
  </Schedule>
  <Actions>
    <!-- Inactive nodes remain saved and attached but are ignored by normal gameplay queries. -->
    <SetActive UnitInstanceID="LUKE_SKYWALKER" IsActive="false"/>
    <SetDisplayStatus TargetInstanceID="LUKE_SKYWALKER"
                      Status="Away on assignment"/>
  </Actions>
</GameEvent>

<GameEvent TriggerCount="1">
  <InstanceID>MOD_OFFICER_RETURNS</InstanceID>
  <Schedule>
    <After EventInstanceID="MOD_OFFICER_LEAVES" DelayTicks="100"/>
  </Schedule>
  <Actions>
    <!-- Reactivate before placement so normal destination validation can see the officer. -->
    <SetActive UnitInstanceID="LUKE_SKYWALKER" IsActive="true"/>
    <PlaceUnits UnitInstanceID="LUKE_SKYWALKER">
      <Destination>
        <SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
      </Destination>
    </PlaceUnits>
    <ClearDisplayStatus TargetInstanceID="LUKE_SKYWALKER"/>
  </Actions>
</GameEvent>
```

---

<p align="center"><a href="Actions.md">← Actions</a> · <a href="Index.md">Event guide</a> · <a href="Testing.md">Testing →</a></p>
