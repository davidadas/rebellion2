# Bindings

Bindings assign a name to one selected scene node so schedules, conditionals, and actions can reuse
it during the same event evaluation. See [Selectors](Selectors.md) for the available selection
operations.

## Bind

Each `Bind` selects exactly one scene node and exposes it under its `As` name for the complete event
evaluation. Bindings are resolved before schedules, so recurring `Until` conditionals can consume
them. In a triggered event, bindings follow `Triggers` and may consume the matched result. Zero
results or multiple results raise a runtime authoring error.

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

---

<p align="center"><a href="Selectors.md">← Selectors</a> · <a href="Index.md">Event guide</a> · <a href="Schedules.md">Schedules →</a></p>
