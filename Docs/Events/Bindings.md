# Bindings

Bindings assign names to values that schedules, selectors, conditionals, and actions can reuse
during one event evaluation. A top-level binding contains one selected scene node, numeric roll,
or typed value read from the current game state. A trigger binding contains one explicitly
documented argument exposed by a matched trigger.

Binding values are temporary. They are rebuilt whenever the event is evaluated and are not stored
in the save game.

## Top-level bindings

Each top-level `Bind` resolves exactly one value and exposes it under its `As` name for the complete
event evaluation. Bindings are resolved before schedules, so recurring `Until` conditionals can
consume them. In a triggered event, top-level bindings are resolved after trigger bindings and may
use those results.

When `From` is used, its selector must resolve exactly one scene node. Resolving no nodes or
multiple nodes raises a runtime authoring error.

**Required options**

- `As` **[Required]:** The unique name assigned to the resolved value.
- Binding source **[Required]:** Provide exactly one:
  - `From`: Contains exactly one supported selector. The schema currently accepts direct planet,
    officer, special-forces, fleet, mission, ship, regiment, building, and manufacturing-order
    selectors, plus `SelectRandom` and `SelectBinding`.
  - `RollInteger`: Produces an integer from its inclusive `Minimum` and `Maximum`.
  - `RollDouble`: Produces a double from its inclusive `Minimum` and exclusive `Maximum`.
  - `OfficerRating`: Produces one officer's effective rating.
  - `OfficerForce`: Produces one officer's current Force rank.
  - `PlanetStat`: Produces one current planet statistic.
  - `SelectionCount`: Produces the number of distinct scene nodes returned by its selectors.

```xml
<Bindings>
  <Bind As="planet">
    <From>
      <SelectRandom Count="1">
        <From>
          <SelectPlanets SectorType="Core"/>
        </From>
      </SelectRandom>
    </From>
  </Bind>
</Bindings>
```

Numeric bindings let multiple actions reuse one roll:

```xml
<Bindings>
  <Bind As="resourceChange">
    <RollInteger Minimum="1" Maximum="5"/>
  </Bind>
</Bindings>
<Actions>
  <ChangeRawResourceNodes PlanetInstanceID="NABOO">
    <AmountBinding>$resourceChange</AmountBinding>
  </ChangeRawResourceNodes>
  <ChangeEnergyCapacity PlanetInstanceID="NABOO">
    <AmountBinding>$resourceChange</AmountBinding>
  </ChangeEnergyCapacity>
</Actions>
```

Typed value bindings read current game state without introducing a separate conditional for every
property that an event might compare:

```xml
<Bindings>
  <Bind As="hanCombat">
    <OfficerRating OfficerInstanceID="HAN_SOLO" Rating="Combat"/>
  </Bind>
  <Bind As="vaderForce">
    <OfficerForce OfficerInstanceID="DARTH_VADER"/>
  </Bind>
  <Bind As="nabooResources">
    <PlanetStat PlanetInstanceID="NABOO" Stat="RawResourceNodes"/>
  </Bind>
  <Bind As="imperialFleetCount">
    <SelectionCount>
      <From>
        <SelectFleets PlanetInstanceID="NABOO" OwnerFactionInstanceID="FNEMP1"/>
      </From>
    </SelectionCount>
  </Bind>
</Bindings>
```

`OfficerRating` options:

- Officer source **[Required]:** Provide exactly one:
  - `OfficerInstanceID`: The `InstanceID` of the officer to evaluate.
  - `OfficerBinding`: A binding that resolves the officer to evaluate.
- `Rating` **[Required]:** The officer rating to read.

`OfficerForce` options:

- Officer source **[Required]:** Provide exactly one:
  - `OfficerInstanceID`: The `InstanceID` of the officer to evaluate.
  - `OfficerBinding`: A binding that resolves the officer to evaluate.

`PlanetStat` options:

- Planet source **[Required]:** Provide exactly one:
  - `PlanetInstanceID`: The `InstanceID` of the planet to evaluate.
  - `PlanetBinding`: A binding that resolves the planet to evaluate.
- `Stat` **[Required]:** The planet statistic to read.

`SelectionCount` options:

- `From` **[Required]:** One or more selectors whose distinct results are counted.

## Trigger bindings

Every trigger accepts an optional `Bindings` collection. Each `Bind` selects one argument from the
trigger's documented contract and assigns it a name before top-level selection bindings and
conditionals are evaluated.

**Required options**

- `Argument` **[Required]:** The trigger argument to expose. Each trigger documents its supported arguments.
- `As` **[Required]:** The unique name assigned to that argument's value.

```xml
<Triggers>
  <UnitArrived UnitInstanceID="EMPEROR_PALPATINE">
    <Bindings>
      <Bind Argument="Unit" As="unit"/>
      <Bind Argument="Destination" As="destination"/>
    </Bindings>
  </UnitArrived>
</Triggers>
```

Multiple triggers are alternatives. Every alternative must expose the same binding names and value
types so later XML always receives the same contract.

## Binding references

Prefix a binding name with `$` wherever an option accepts a binding reference:

```xml
<SelectBinding Binding="$planet"/>
```

Trigger bindings expose the selected argument directly:

```xml
<Actions>
  <SendMessage RecipientFactionInstanceID="FNALL1"
               SubjectBinding="$unit"
               LocationBinding="$destination">
    <Subject>Unit Arrived</Subject>
    <Body>{subject} arrived at {location}.</Body>
  </SendMessage>
</Actions>
```

Binding references never traverse C# properties. An unsupported trigger argument is rejected during
event validation instead of failing later through reflection.

Binding names must be unique within one evaluation. A trigger binding and a selection binding
cannot use the same name.

---

<p align="center"><a href="Selectors.md">← Selectors</a> · <a href="Index.md">Event guide</a> · <a href="Schedules.md">Schedules →</a></p>
