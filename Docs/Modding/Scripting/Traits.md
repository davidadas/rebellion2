# Officer traits

Officer traits are defined in the content pack's `OfficerTraitsPath` document. Officers receive them
through authored `Traits` or the `ApplyOfficerTrait` action. Existing leadership and travel modifiers
remain passive effects; the optional `Behaviors` collection adds scripts owned by each trait holder.

## Behavior

A `Behavior` contains shared [triggers](Triggers.md), [bindings](Bindings.md),
[conditionals](Conditionals.md), and [actions](Actions.md).

**Required options**

- `ID` **[Required]:** A nonblank identifier unique within the trait.
- Activation **[Required]:** Choose exactly one:
  - `EveryTick="true"`: Run after scheduled events and before AI planning during each tick.
  - `Triggers`: React to matching results through the existing gameplay result pipeline.
- `Actions` **[Required]:** One or more actions executed in authored order.

**Optional options**

- `IncludeInactive`: Defaults to `false`. Set to `true` to allow this behavior on registered officers
  disabled directly or through their containing node. This includes unrecruited officers; use
  explicit conditions when only a particular inactive state should qualify.
- `Bindings`: Resolve additional values before conditions and actions.
- `Conditionals`: Require every condition to pass before executing actions.

Every activation supplies `$officer`, bound to the officer holding the trait. This name is reserved:
trigger arguments and authored bindings cannot replace it. Use `SelectBinding` to target that officer
without hardcoding an officer identifier. All triggers of one behavior must expose the same binding
names and value types.

```xml
<OfficerTraits>
  <Trait ID="training">
    <Behaviors>
      <Behavior ID="leadership-practice" EveryTick="true">
        <Bindings>
          <Bind As="$leadership">
            <SkillRating OfficerBinding="$officer" Rating="Leadership"/>
          </Bind>
        </Bindings>
        <Conditionals>
          <EvaluateBinding Binding="$leadership" Comparison="LessThan" CompareTo="50"/>
        </Conditionals>
        <Actions>
          <ChangeOfficerRating Rating="Leadership">
            <Amount>1</Amount>
            <Officers><SelectBinding Binding="$officer"/></Officers>
          </ChangeOfficerRating>
        </Actions>
      </Behavior>
    </Behaviors>
  </Trait>
</OfficerTraits>
```

This example increases each active holder's stored leadership once per tick while effective
leadership remains below 50. It does not change any shipped trait definition.

For a result-driven behavior, omit `EveryTick` and provide `Triggers` instead:

```xml
<Behavior ID="arrival-status">
  <Triggers>
    <UnitArrived UnitInstanceID="OFFICER"/>
  </Triggers>
  <Actions>
    <SetDisplayStatus Status="Arrived">
      <Targets><SelectBinding Binding="$officer"/></Targets>
    </SetDisplayStatus>
  </Actions>
</Behavior>
```

Triggers listen to game results, including results about other units. Use their filters, bindings,
and conditions to specify which changes matter. The second example deliberately filters one named
officer's arrivals. Each matching behavior runs once per holder per incoming result, even if several
of its triggers match. Distinct results in the same tick each receive their own activation.

## Execution and loading

Behaviors run only while the officer still exists in the authoritative game graph and holds the
trait. Multiple grants of the same trait do not multiply behavior execution. Removing a trait or an
officer prevents their subsequent activations. Each activation rechecks eligibility after preceding
scripts have run.

Officers are processed in ordinal instance-ID order. Tick behaviors follow trait-definition and
behavior order. Each activation gets fresh bindings. The shared action executor preserves immediate
changes, deferred command ordering, and result delivery. Conditions, bindings, and actions use the
current game random provider.

A behavior owns grants under a stable source identifier made from its officer, trait, and behavior
IDs. Repeating `ApplyOfficerTrait` from the same behavior does not duplicate its grant. Another
behavior's grants remain independently owned. The activation does not pretend to be a game event:
results do not receive a fabricated `SourceEventInstanceID`.

No activation counts, cooldowns, completion flags, subscriptions, or execution contexts are saved.
The catalog supplies definitions again on load; the session recreates its trigger index and observer.
Authored traits and existing saved trait grants determine which behaviors are eligible. Loading a
save does not execute actions or replay historical results. Tick scripts run at the next normal trait
phase, and triggered scripts wait for newly published results.

Recurring behaviors should author any required completion conditions. A script that produces a
result which activates itself can keep producing reactions; use conditions and idempotent actions to
ensure its reactions settle. Removing a trait does not automatically undo changes previously made by
its actions.

The `officer-traits.xsd` schema includes the shared `game-scripting.xsd` declarations. Content loading
validates trait XML against that schema, and session construction validates activation modes,
behavior IDs, and binding contracts.
