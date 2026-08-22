# Triggers and Bindings

Triggers activate events from typed simulation results. A trigger does not poll game state: it receives a result at the moment gameplay produces that result, then gives the event one opportunity to execute.

An event uses `Triggers` instead of `Schedule`. Multiple triggers on the same event are alternatives—any one matching result may activate it.

## Trigger structure

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

`Event` chooses a supported result contract. Each `Bind` reads one public argument from that result and stores it under the authored alias in `As`.

## Using bindings

Binding references begin with `$`. A binding can be consumed by conditions, selectors, targets, and actions that accept that value type.

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

Bind the object when another element needs the actual scene node; bind its `InstanceID` argument when string comparison is sufficient.

## Multiple triggers

Multiple triggers are alternative routes into one event. Every route must expose the same aliases with compatible types, because the rest of the event must be valid regardless of which trigger activated it.

```xml
<Triggers>
  <Trigger Event="core:unit.destroyed">
    <Bindings>
      <Bind Argument="Unit" As="destroyedUnit"/>
      <Bind Argument="UnitInstanceID" As="destroyedUnitInstanceID"/>
    </Bindings>
  </Trigger>
  <Trigger Event="core:unit.sabotaged">
    <Bindings>
      <Bind Argument="Unit" As="destroyedUnit"/>
      <Bind Argument="UnitInstanceID" As="destroyedUnitInstanceID"/>
    </Bindings>
  </Trigger>
</Triggers>
```

Overlapping result types are rejected, so one simulation result cannot activate the same event through two trigger routes.

## Trigger catalog

### Planet

| Trigger | Available arguments |
| --- | --- |
| `core:planet.owner-changed` | `Planet`, `PlanetInstanceID`, `PreviousOwner`, `PreviousOwnerInstanceID`, `NewOwner`, `NewOwnerInstanceID`, `Reason`, `ObserverFactionInstanceIDs`, `SourceEventInstanceID` |
| `core:planet.stat-changed` | `Planet`, `PlanetInstanceID`, `Faction`, `FactionInstanceID`, `Category`, `OldValue`, `NewValue`, `SourceEventInstanceID` |
| `core:smuggling.changed` | `Planet`, `PlanetInstanceID`, `ControllerFaction`, `ControllerFactionInstanceID`, `BeneficiaryFaction`, `BeneficiaryFactionInstanceID`, `OldPercent`, `NewPercent`, `SourceEventInstanceID` |
| `core:blockade.changed` | `Planet`, `PlanetInstanceID`, `BlockadingFleet`, `BlockadingFleetInstanceID`, `IsBlockaded`, `SourceEventInstanceID` |
| `core:uprising.started` | `Planet`, `PlanetInstanceID`, `InstigatorFaction`, `InstigatorFactionInstanceID`, `SourceEventInstanceID` |
| `core:uprising.nearing` | `Planet`, `PlanetInstanceID`, `SourceEventInstanceID` |
| `core:uprising.ended` | `Planet`, `PlanetInstanceID`, `Faction`, `FactionInstanceID`, `SourceEventInstanceID` |
| `core:headquarters.destroyed` | `Headquarters`, `HeadquartersInstanceID`, `Planet`, `PlanetInstanceID`, `DefenderFaction`, `DefenderFactionInstanceID`, `AttackerFaction`, `AttackerFactionInstanceID`, `SourceEventInstanceID` |
| `core:planet.garrison-changed` | `Planet`, `PlanetInstanceID`, `SourceEventInstanceID` |
| `core:planet.incident` | `Planet`, `PlanetInstanceID`, `IncidentType`, `Severity`, `ChangedStat`, `OldValue`, `NewValue`, `DestroyedObjects`, `SourceEventInstanceID` |

### Faction

| Trigger | Available arguments |
| --- | --- |
| `core:intelligence.revealed` | `RecipientFaction`, `RecipientFactionInstanceID`, `Observations`, `SourceEventInstanceID` |
| `core:maintenance.required` | `Faction`, `FactionInstanceID`, `Planet`, `PlanetInstanceID`, `Amount`, `SourceEventInstanceID` |
| `core:research.completed` | `Faction`, `FactionInstanceID`, `Discipline`, `ResearchOrder`, `Technology`, `TechnologyTypeID`, `SourceEventInstanceID` |
| `core:research.exhausted` | `Faction`, `FactionInstanceID`, `Discipline`, `SourceEventInstanceID` |
| `core:recruitment.exhausted` | `Faction`, `FactionInstanceID`, `Planet`, `PlanetInstanceID`, `SourceEventInstanceID` |
| `core:game.completed` | `WinnerFaction`, `WinnerFactionInstanceID`, `LoserFaction`, `LoserFactionInstanceID`, `GameMode`, `Description`, `SourceEventInstanceID` |

### Mission

| Trigger | Available arguments |
| --- | --- |
| `core:mission.completed` | `Mission`, `Outcome`, `CompletionReason`, `Participants`, `Location`, `ReturnDestination`, `SourceEventInstanceID` |
| `core:planet-sectors.revealed` | `PlanetSectors`, `SourceEventInstanceID` |

### Officer

| Trigger | Available arguments |
| --- | --- |
| `core:force.discovered` | `Officer`, `Discoverer`, `ForceRank`, `SourceEventInstanceID` |
| `core:officer.recruited` | `Officer`, `OfficerInstanceID`, `Faction`, `FactionInstanceID`, `Planet`, `PlanetInstanceID`, `SourceEventInstanceID` |
| `core:officer.capture-changed` | `Officer`, `OfficerInstanceID`, `LinkedOfficer`, `Context`, `IsCaptured`, `SourceEventInstanceID` |
| `core:officer.killed` | `Officer`, `OfficerInstanceID`, `Assassin`, `Context`, `SourceEventInstanceID` |
| `core:officer.injured` | `Officer`, `OfficerInstanceID`, `Severity`, `Detail`, `SourceEventInstanceID` |
| `core:officer.rescued` | `Officer`, `OfficerInstanceID`, `RescuingFaction`, `RescuingFactionInstanceID`, `Planet`, `PlanetInstanceID`, `SourceEventInstanceID` |
| `core:officer.command-changed` | `Officer`, `OfficerInstanceID`, `CommandKind`, `Detail`, `SourceEventInstanceID` |
| `core:officer.command-assigned` | `Officer`, `OfficerInstanceID`, `CommandTarget`, `CommandTargetInstanceID`, `Context`, `SourceEventInstanceID` |
| `core:officer.traitor-discovered` | `Officer`, `OfficerInstanceID`, `DiscoveredBy`, `Context`, `SourceEventInstanceID` |
| `core:force.training-completed` | `Officer`, `OfficerInstanceID`, `Progress`, `Detail`, `SourceEventInstanceID` |
| `core:force.experience-gained` | `Officer`, `OfficerInstanceID`, `ExperienceGained`, `PreviousForceRank`, `CurrentForceRank`, `Detail`, `SourceEventInstanceID` |

### Unit lifecycle

| Trigger | Available arguments |
| --- | --- |
| `core:unit.owner-changed` | `Unit`, `UnitInstanceID`, `PreviousOwner`, `PreviousOwnerInstanceID`, `NewOwner`, `NewOwnerInstanceID`, `SourceEventInstanceID` |
| `core:unit.created` | `Unit`, `UnitInstanceID`, `SourceEventInstanceID` |
| `core:unit.deployed` | `Unit`, `UnitInstanceID`, `SourceEventInstanceID` |
| `core:unit.movement-started` | `Unit`, `UnitInstanceID`, `SourceEventInstanceID` |
| `core:unit.arrived` | `Unit`, `UnitInstanceID`, `Destination`, `DestinationInstanceID`, `SourceEventInstanceID` |
| `core:unit.damaged` | `Unit`, `UnitInstanceID`, `Damage`, `SourceEventInstanceID` |
| `core:unit.destroyed` | `Unit`, `UnitInstanceID`, `DestroyedBy`, `Context`, `SourceEventInstanceID` |
| `core:unit.destroyed-on-arrival` | `Unit`, `UnitInstanceID`, `Reference`, `Context`, `SourceEventInstanceID` |
| `core:unit.autoscrapped` | `Unit`, `UnitInstanceID`, `Reference`, `Context`, `SourceEventInstanceID` |
| `core:unit.sabotaged` | `Unit`, `UnitInstanceID`, `Saboteur`, `Context`, `SourceEventInstanceID` |

### Combat

| Trigger | Available arguments |
| --- | --- |
| `core:duel.completed` | `Officer`, `OfficerInstanceID`, `Opponent`, `OpponentInstanceID`, `Location`, `OfficerCaptured`, `OfficerInjury`, `OpponentInjury`, `ImagePath`, `AudioPath`, `SourceEventInstanceID` |
| `core:combat.completed` | `AttackerFleet`, `DefenderFleet`, `AttackerFactionInstanceID`, `DefenderFactionInstanceID`, `Planet`, `PlanetInstanceID`, `Winner`, `AttackerOutcome`, `DefenderOutcome`, `SourceEventInstanceID` |
| `core:bombardment.completed` | `Planet`, `PlanetInstanceID`, `AttackingFaction`, `AttackerFactionInstanceID`, `DefenderFactionInstanceID`, `Type`, `SuccessfulStrikes`, `HeadquartersDestroyed`, `PlanetDestroyed`, `SourceEventInstanceID` |
| `core:planetary-assault.completed` | `Planet`, `PlanetInstanceID`, `AttackingFaction`, `AttackerFactionInstanceID`, `DefenderFactionInstanceID`, `Success`, `BlockedByShields`, `SourceEventInstanceID` |
| `core:evacuation.completed` | `Faction`, `FactionInstanceID`, `Planet`, `PlanetInstanceID`, `LostCapitalShips`, `LostStarfighters`, `LostRegiments`, `SourceEventInstanceID` |

### Manufacturing

| Trigger | Available arguments |
| --- | --- |
| `core:manufacturing.completed` | `Faction`, `FactionInstanceID`, `Unit`, `UnitInstanceID`, `Location`, `LocationInstanceID`, `SourceEventInstanceID` |
| `core:manufacturing.idle` | `Planet`, `PlanetInstanceID`, `Faction`, `FactionInstanceID`, `ManufacturingType`, `SourceEventInstanceID` |

`core:unit.destroyed` represents ordinary unit destruction. Destruction during arrival, autoscrapping, and sabotage are distinct simulation outcomes and use their corresponding specialized triggers.

`SourceEventInstanceID` identifies an authored event that caused the result when one exists. It is empty for results produced entirely by normal gameplay.

---

<p align="center"><a href="Schedules.md">← Schedules</a> · <a href="README.md">Event guide</a> · <a href="Conditions.md">Conditions →</a></p>
