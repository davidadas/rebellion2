/Users/davidadams/.zshenv:.:1: no such file or directory: /tmp/reb2-rust.Fq5aMf/cargo/env
/Users/davidadams/.zshenv:.:1: no such file or directory: /tmp/reb2-rust.Fq5aMf/cargo/env
/Users/davidadams/.zshenv:.:1: no such file or directory: /tmp/reb2-rust.Fq5aMf/cargo/env
/Users/davidadams/.zshenv:.:1: no such file or directory: /tmp/reb2-rust.Fq5aMf/cargo/env
/Users/davidadams/.zshenv:.:1: no such file or directory: /tmp/reb2-rust.Fq5aMf/cargo/env
/Users/davidadams/.zshenv:.:1: no such file or directory: /tmp/reb2-rust.Fq5aMf/cargo/env
# Triggers and Bindings

Triggers activate events from typed simulation results. A trigger does not poll game state: it receives
a result when gameplay produces one, then gives the event one opportunity to execute. An event uses
`Triggers` instead of `Schedule`. Multiple triggers are alternatives, so any matching result may
activate the event.

## Triggers

`Triggers` contains one or more `Trigger` elements. It cannot appear with `Schedule`. When an
event declares multiple triggers, every route must expose the same binding aliases with compatible
types.

```xml
<Triggers>
  <Trigger Event="core:unit.arrived">
    <Bindings>
      <Bind Argument="UnitInstanceID" As="unitInstanceID"/>
      <Bind Argument="DestinationInstanceID" As="destinationInstanceID"/>
    </Bindings>
  </Trigger>
</Triggers>
```

## Trigger

`Trigger` selects one supported gameplay-result contract.

**Options**

- `Event` — required trigger ID.
- `Bindings` — optional collection of values to expose to the event.
- Overlapping result types are rejected, so one result cannot activate the same event through two
  routes.

## Bindings

`Bindings` contains one or more `Bind` elements. Each trigger route on the event must expose the
same aliases and compatible types.

## Bind

`Bind` copies one public argument from the triggering result into the event context.

**Options**

- `Argument` — required argument name listed by that trigger below.
- `As` — required unique alias. It cannot be `target`.
- Refer to the resulting value as `$alias` in conditions, selectors, targets, and actions.

```xml
<Bind Argument="UnitInstanceID" As="unitInstanceID"/>
```

Bind the object when another element needs the scene node itself. Bind its instance-ID argument when
a scalar string comparison is sufficient.

## Available triggers

### Planet

#### core:planet.owner-changed

Activates when the game produces the **planet owner-changed** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `PreviousOwner`, `PreviousOwnerInstanceID`, `NewOwner`, `NewOwnerInstanceID`, `Reason`, `ObserverFactionInstanceIDs`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:planet.owner-changed"/>
```

#### core:planet.stat-changed

Activates when the game produces the **planet stat-changed** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `Faction`, `FactionInstanceID`, `Category`, `OldValue`, `NewValue`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:planet.stat-changed"/>
```

#### core:smuggling.changed

Activates when the game produces the **smuggling changed** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `ControllerFaction`, `ControllerFactionInstanceID`, `BeneficiaryFaction`, `BeneficiaryFactionInstanceID`, `OldPercent`, `NewPercent`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:smuggling.changed"/>
```

#### core:blockade.changed

Activates when the game produces the **blockade changed** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `BlockadingFleet`, `BlockadingFleetInstanceID`, `IsBlockaded`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:blockade.changed"/>
```

#### core:uprising.started

Activates when the game produces the **uprising started** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `InstigatorFaction`, `InstigatorFactionInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:uprising.started"/>
```

#### core:uprising.nearing

Activates when the game produces the **uprising nearing** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:uprising.nearing"/>
```

#### core:uprising.ended

Activates when the game produces the **uprising ended** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `Faction`, `FactionInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:uprising.ended"/>
```

#### core:headquarters.destroyed

Activates when the game produces the **headquarters destroyed** result.

**Options**

- Available binding arguments: `Headquarters`, `HeadquartersInstanceID`, `Planet`, `PlanetInstanceID`, `DefenderFaction`, `DefenderFactionInstanceID`, `AttackerFaction`, `AttackerFactionInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:headquarters.destroyed"/>
```

#### core:planet.garrison-changed

Activates when the game produces the **planet garrison-changed** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:planet.garrison-changed"/>
```

#### core:planet.incident

Activates when the game produces the **planet incident** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `IncidentType`, `Severity`, `ChangedStat`, `OldValue`, `NewValue`, `DestroyedObjects`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:planet.incident"/>
```

### Faction

#### core:intelligence.revealed

Activates when the game produces the **intelligence revealed** result.

**Options**

- Available binding arguments: `RecipientFaction`, `RecipientFactionInstanceID`, `Observations`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:intelligence.revealed"/>
```

#### core:maintenance.required

Activates when the game produces the **maintenance required** result.

**Options**

- Available binding arguments: `Faction`, `FactionInstanceID`, `Planet`, `PlanetInstanceID`, `Amount`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:maintenance.required"/>
```

#### core:research.completed

Activates when the game produces the **research completed** result.

**Options**

- Available binding arguments: `Faction`, `FactionInstanceID`, `Discipline`, `ResearchOrder`, `Technology`, `TechnologyTypeID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:research.completed"/>
```

#### core:research.exhausted

Activates when the game produces the **research exhausted** result.

**Options**

- Available binding arguments: `Faction`, `FactionInstanceID`, `Discipline`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:research.exhausted"/>
```

#### core:recruitment.exhausted

Activates when the game produces the **recruitment exhausted** result.

**Options**

- Available binding arguments: `Faction`, `FactionInstanceID`, `Planet`, `PlanetInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:recruitment.exhausted"/>
```

#### core:game.completed

Activates when the game produces the **game completed** result.

**Options**

- Available binding arguments: `WinnerFaction`, `WinnerFactionInstanceID`, `LoserFaction`, `LoserFactionInstanceID`, `GameMode`, `Description`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:game.completed"/>
```

### Mission

#### core:mission.completed

Activates when the game produces the **mission completed** result.

**Options**

- Available binding arguments: `Mission`, `Outcome`, `CompletionReason`, `Participants`, `Location`, `ReturnDestination`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:mission.completed"/>
```

#### core:planet-sectors.revealed

Activates when the game produces the **planet-sectors revealed** result.

**Options**

- Available binding arguments: `PlanetSectors`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:planet-sectors.revealed"/>
```

### Officer

#### core:force.discovered

Activates when the game produces the **force discovered** result.

**Options**

- Available binding arguments: `Officer`, `Discoverer`, `ForceRank`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:force.discovered"/>
```

#### core:officer.recruited

Activates when the game produces the **officer recruited** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `Faction`, `FactionInstanceID`, `Planet`, `PlanetInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:officer.recruited"/>
```

#### core:officer.capture-changed

Activates when the game produces the **officer capture-changed** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `LinkedOfficer`, `Context`, `IsCaptured`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:officer.capture-changed"/>
```

#### core:officer.killed

Activates when the game produces the **officer killed** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `Assassin`, `Context`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:officer.killed"/>
```

#### core:officer.injured

Activates when the game produces the **officer injured** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `Severity`, `Detail`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:officer.injured"/>
```

#### core:officer.rescued

Activates when the game produces the **officer rescued** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `RescuingFaction`, `RescuingFactionInstanceID`, `Planet`, `PlanetInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:officer.rescued"/>
```

#### core:officer.command-changed

Activates when the game produces the **officer command-changed** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `CommandKind`, `Detail`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:officer.command-changed"/>
```

#### core:officer.command-assigned

Activates when the game produces the **officer command-assigned** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `CommandTarget`, `CommandTargetInstanceID`, `Context`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:officer.command-assigned"/>
```

#### core:officer.traitor-discovered

Activates when the game produces the **officer traitor-discovered** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `DiscoveredBy`, `Context`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:officer.traitor-discovered"/>
```

#### core:force.training-completed

Activates when the game produces the **force training-completed** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `Progress`, `Detail`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:force.training-completed"/>
```

#### core:force.experience-gained

Activates when the game produces the **force experience-gained** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `ExperienceGained`, `PreviousForceRank`, `CurrentForceRank`, `Detail`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:force.experience-gained"/>
```

### Unit lifecycle

#### core:unit.owner-changed

Activates when the game produces the **unit owner-changed** result.

**Options**

- Available binding arguments: `Unit`, `UnitInstanceID`, `PreviousOwner`, `PreviousOwnerInstanceID`, `NewOwner`, `NewOwnerInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:unit.owner-changed"/>
```

#### core:unit.created

Activates when the game produces the **unit created** result.

**Options**

- Available binding arguments: `Unit`, `UnitInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:unit.created"/>
```

#### core:unit.deployed

Activates when the game produces the **unit deployed** result.

**Options**

- Available binding arguments: `Unit`, `UnitInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:unit.deployed"/>
```

#### core:unit.movement-started

Activates when the game produces the **unit movement-started** result.

**Options**

- Available binding arguments: `Unit`, `UnitInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:unit.movement-started"/>
```

#### core:unit.arrived

Activates when the game produces the **unit arrived** result.

**Options**

- Available binding arguments: `Unit`, `UnitInstanceID`, `Destination`, `DestinationInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:unit.arrived"/>
```

#### core:unit.damaged

Activates when the game produces the **unit damaged** result.

**Options**

- Available binding arguments: `Unit`, `UnitInstanceID`, `Damage`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:unit.damaged"/>
```

#### core:unit.destroyed

Activates when the game produces the **unit destroyed** result.

**Options**

- Available binding arguments: `Unit`, `UnitInstanceID`, `DestroyedBy`, `Context`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:unit.destroyed"/>
```

#### core:unit.destroyed-on-arrival

Activates when the game produces the **unit destroyed-on-arrival** result.

**Options**

- Available binding arguments: `Unit`, `UnitInstanceID`, `Reference`, `Context`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:unit.destroyed-on-arrival"/>
```

#### core:unit.autoscrapped

Activates when the game produces the **unit autoscrapped** result.

**Options**

- Available binding arguments: `Unit`, `UnitInstanceID`, `Reference`, `Context`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:unit.autoscrapped"/>
```

#### core:unit.sabotaged

Activates when the game produces the **unit sabotaged** result.

**Options**

- Available binding arguments: `Unit`, `UnitInstanceID`, `Saboteur`, `Context`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:unit.sabotaged"/>
```

### Combat

#### core:duel.completed

Activates when the game produces the **duel completed** result.

**Options**

- Available binding arguments: `Officer`, `OfficerInstanceID`, `Opponent`, `OpponentInstanceID`, `Location`, `OfficerCaptured`, `OfficerInjury`, `OpponentInjury`, `ImagePath`, `AudioPath`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:duel.completed"/>
```

#### core:combat.completed

Activates when the game produces the **combat completed** result.

**Options**

- Available binding arguments: `AttackerFleet`, `DefenderFleet`, `AttackerFactionInstanceID`, `DefenderFactionInstanceID`, `Planet`, `PlanetInstanceID`, `Winner`, `AttackerOutcome`, `DefenderOutcome`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:combat.completed"/>
```

#### core:bombardment.completed

Activates when the game produces the **bombardment completed** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `AttackingFaction`, `AttackerFactionInstanceID`, `DefenderFactionInstanceID`, `Type`, `SuccessfulStrikes`, `HeadquartersDestroyed`, `PlanetDestroyed`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:bombardment.completed"/>
```

#### core:planetary-assault.completed

Activates when the game produces the **planetary-assault completed** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `AttackingFaction`, `AttackerFactionInstanceID`, `DefenderFactionInstanceID`, `Success`, `BlockedByShields`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:planetary-assault.completed"/>
```

#### core:evacuation.completed

Activates when the game produces the **evacuation completed** result.

**Options**

- Available binding arguments: `Faction`, `FactionInstanceID`, `Planet`, `PlanetInstanceID`, `LostCapitalShips`, `LostStarfighters`, `LostRegiments`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:evacuation.completed"/>
```

### Manufacturing

#### core:manufacturing.completed

Activates when the game produces the **manufacturing completed** result.

**Options**

- Available binding arguments: `Faction`, `FactionInstanceID`, `Unit`, `UnitInstanceID`, `Location`, `LocationInstanceID`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:manufacturing.completed"/>
```

#### core:manufacturing.idle

Activates when the game produces the **manufacturing idle** result.

**Options**

- Available binding arguments: `Planet`, `PlanetInstanceID`, `Faction`, `FactionInstanceID`, `ManufacturingType`, `SourceEventInstanceID`.

```xml
<Trigger Event="core:manufacturing.idle"/>
```

## Using bindings

Binding references begin with `$`:

```xml
<Conditionals>
  <EvaluateBinding Binding="$unitInstanceID"
                   Comparison="Equal"
                   CompareTo="EMPEROR_PALPATINE"/>
  <EvaluateBinding Binding="$destinationInstanceID"
                   Comparison="Equal"
                   CompareTo="CORUSCANT"/>
</Conditionals>
```

`core:unit.destroyed` represents ordinary unit destruction. Destruction during arrival,
autoscrapping, and sabotage use their corresponding specialized triggers.

`SourceEventInstanceID` identifies the authored event that caused the result when one exists. It is
empty for results produced entirely by normal gameplay.

---

<p align="center"><a href="Schedules.md">← Schedules</a> · <a href="Index.md">Event guide</a> · <a href="Conditions.md">Conditions →</a></p>
