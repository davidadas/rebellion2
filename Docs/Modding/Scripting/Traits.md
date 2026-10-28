# Officer traits

The pack's existing `OfficerTraitsPath` file defines shared traits. Officers select passive modifiers
by referencing a definition's ID in `Traits`, or by receiving an `ApplyOfficerTrait` grant.

```xml
<Trait ID="smuggler">
  <PersonnelTravelTimePercent>50</PersonnelTravelTimePercent>
  <SharesPersonnelTravelTimeModifier>true</SharesPersonnelTravelTimeModifier>
</Trait>
```

## Trait scripts

A trait definition may directly contain the existing `Triggers`, `Bindings`, `Conditionals`, and
`Actions` collections. Scripts belong to the shared definition and execute once for that definition,
not once for every officer carrying it. Their targets come from authored IDs, selectors, and trigger
argument bindings. There is no implicit holder binding.

**Options**

- `Triggers` **[Optional]:** Existing result triggers. Any matching trigger gives the definition one
  activation opportunity for that incoming result. Overlapping triggers do not duplicate execution.
- `Bindings` **[Optional]:** Existing bindings resolved before script conditions and actions.
- `Conditionals` **[Optional]:** Require every condition to pass before executing the action list.
- `Actions` **[Required for scripts]:** Existing actions executed in authored order. Script inputs
  without any actions are rejected.

A script without `Triggers` is evaluated once each normal game tick, after scheduled events and
before AI planning. A script with `Triggers` runs only for matching results. Plain modifier
traits without actions do not execute scripts.

```xml
<Trait ID="smuggler">
  <PersonnelTravelTimePercent>50</PersonnelTravelTimePercent>
  <SharesPersonnelTravelTimeModifier>true</SharesPersonnelTravelTimeModifier>
  <Triggers>
    <UnitArrived UnitInstanceID="HAN_SOLO"/>
  </Triggers>
  <Actions>
    <SetDisplayStatus TargetInstanceID="HAN_SOLO" Status="Arrived"/>
  </Actions>
</Trait>
```

This is an authoring example, not a change to Han's shipped trait. It reuses `UnitArrived` and
`SetDisplayStatus` directly. Trigger bindings and conditions use the same contracts as game events.
Every trigger path must expose compatible binding names and value types.

## Emperor leadership

The `leadership-bonus` definition owns the Emperor's script. Its passive leadership percentage stays
at 50. The script retains the existing conditions checking captivity, death, travel, direct placement
on Coruscant, and Imperial ownership of Coruscant.

An `If` action grants the leadership trait to Imperial officers and issues `ReserveOfficer` for
Palpatine when those conditions hold. Its `Else` removes that script's leadership grants. Keeping the
cleanup branch inside the definition lets it execute even after Palpatine leaves or is removed.
The separate `EMPEROR_LEADERSHIP_BONUS` event is removed from authored game-event content.

`ReserveOfficer` publishes a current-tick request through the result bus. AI excludes that officer
from its candidate personnel and selection reservations, including decoy assignment, and rechecks
reservations before starting a mission. Requests expire when the game tick changes. This action
neither cancels an existing mission nor changes player-issued orders.

## Execution and loading

Definitions execute in catalog order; result-driven scripts also preserve incoming-result order.
Each activation gets fresh bindings and uses the current game random provider. Actions retain their
existing direct changes, deferred command timing, error handling, and result contracts.

Script conditions gate the action list. They do not conditionally enable passive modifier fields.
Conditional bonuses must use explicit grant and removal actions, as the Emperor definition does.
Removing an arbitrary trait grant does not automatically undo unrelated changes made by scripts.

Each definition owns grants under `trait/` followed by its URI-escaped ID. Repeated grants from that
source are idempotent, while grants from other sources stay independently owned.

The result bus tracks reaction origins transiently using weak references. A definition cannot react
to results descended from its own activation, including reactions passing through another subscriber.
Independent results remain eligible; there is no once-per-tick cap on result-driven scripts. This
tracking adds no fields to game objects or saved games.

Definitions and subscriptions are rebuilt from the active catalog when a session is created or
loaded. Loading does not execute actions or replay historical results. The next normal trait phase
renews any AI reservation and reconciles the Emperor's script-owned grants.

Existing saves retain their saved event pool. A save made before the migration can still contain
the older Emperor event. Its grants retain their own source ID; the modifier counts a trait once
regardless of how many sources grant it, and each script removes only its own grant.

Trait XML and actions are validated by the existing `game-scripting.xsd`. Session construction
validates script activation and binding contracts.
