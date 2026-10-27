# Game rules

Game rules select game objects, bind values, test conditions, match simulation results, and perform ordered actions.

- [Bindings](Bindings.md) name selected objects and values.
- [Selectors](Selectors.md) find game objects.
- [Conditionals](Conditionals.md) test game state and bindings.
- [Triggers](Triggers.md) match simulation results and expose their arguments.
- [Actions](Actions.md) apply ordered changes.

[Game events](../Events/Index.md) combine these rules with schedules and persistent activation history. This reference documents the existing event XML; other rule consumers are not yet exposed in content definitions.

Shared XML types are defined in `Content/Application/Schemas/game-rules.xsd`. The event schema includes those types and defines event schedules and the `GameEvents` document.
