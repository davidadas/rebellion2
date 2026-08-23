# Bindings

Bindings assign names to values that schedules, selectors, conditionals, and actions can reuse
during one event evaluation. A binding may contain either a scene node selected by the event or the
complete gameplay result matched by a trigger.

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

Every trigger accepts an optional `As` attribute. It binds the complete matched gameplay result,
including the result's typed properties, before top-level selection bindings and conditionals are
evaluated.

**Optional options**

- `As` **[Optional]:** The unique name assigned to the complete result matched by the trigger.

```xml
<Triggers>
  <UnitArrived UnitInstanceID="EMPEROR_PALPATINE" As="arrival"/>
</Triggers>
```

Multiple triggers are alternatives. If they use `As`, every trigger must use the same name and
expose the same result type so later XML always receives the same contract.

## Binding references

Prefix a binding name with `$` wherever an option accepts a binding reference:

```xml
<SelectBinding Binding="$planet"/>
```

Trigger bindings expose result objects rather than individual scalar values. Append public property
names to traverse the result:

```xml
<Conditionals>
  <EvaluateBinding Binding="$arrival.Destination.InstanceID"
                   Comparison="Equal"
                   CompareTo="CORUSCANT"/>
</Conditionals>
```

The example resolves the `Destination` property from the bound `UnitArrivedResult`, then resolves
that destination's `InstanceID`. An unknown property name raises a runtime authoring error.

Binding names must be unique within one evaluation. A trigger binding and a selection binding
cannot use the same name.

---

<p align="center"><a href="Selectors.md">← Selectors</a> · <a href="Index.md">Event guide</a> · <a href="Schedules.md">Schedules →</a></p>
