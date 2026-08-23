# Bindings

Bindings assign names to values that schedules, selectors, conditionals, and actions can reuse
during one event evaluation. A binding contains either a scene node selected by the event or one
explicitly documented argument exposed by a matched trigger.

Binding values are temporary. They are rebuilt whenever the event is evaluated and are not stored
in the save game.

## Selection bindings

Each `Bind` selects exactly one scene node and exposes it under its `As` name for the complete event
evaluation. Bindings are resolved before schedules, so recurring `Until` conditionals can consume
them. In a triggered event, selection bindings are resolved after the trigger binding and may use
that result. See [Selectors](Selectors.md) for the available selection operations.

The selector must resolve exactly one scene node. Resolving no nodes or multiple nodes raises a
runtime authoring error.

**Required options**

- `As` **[Required]:** The unique name assigned to the selected scene node.
- `From` **[Required]:** Contains exactly one supported selector. The schema currently accepts
  direct planet, officer, special-forces, fleet, mission, ship, regiment, building, and
  manufacturing-order selectors, plus `SelectRandom` and `SelectBinding`.

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
