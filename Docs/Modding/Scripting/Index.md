# Game scripting

Game scripts select game objects, bind values, test conditions, match simulation results, and perform ordered actions.

- [Bindings](Bindings.md) name selected objects and values.
- [Selectors](Selectors.md) find game objects.
- [Conditionals](Conditionals.md) test game state and bindings.
- [Triggers](Triggers.md) match simulation results and expose their arguments.
- [Actions](Actions.md) apply ordered changes.

[Game events](Events/Index.md) combine these scripting components with schedules and persistent activation history. [Officer traits](Traits.md) combine passive modifiers with optional tick and result-driven definition scripts without event schedules or activation history.

Shared XML types are defined in `Content/Application/Schemas/game-scripting.xsd`. The event schema includes those types and defines event schedules and the `GameEvents` document.
