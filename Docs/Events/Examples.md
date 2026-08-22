# Complete Examples

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
          <SelectPlanets SectorType="Core"/>
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
    <SetActive UnitInstanceID="LUKE_SKYWALKER" IsActive="false"/>
    <SetDisplayStatus TargetInstanceID="LUKE_SKYWALKER" Status="Away on assignment"/>
  </Actions>
</GameEvent>

<GameEvent TriggerCount="1">
  <InstanceID>MOD_OFFICER_RETURNS</InstanceID>
  <Schedule><After EventInstanceID="MOD_OFFICER_LEAVES" DelayTicks="100"/></Schedule>
  <Actions>
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

<p align="center"><a href="Actions.md">← Actions</a> · <a href="README.md">Event guide</a> · <a href="Testing.md">Testing →</a></p>
