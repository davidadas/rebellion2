# Runtime method inventory

Baseline: `c7fc3011`. Generated from a semantic scan of production and test callers.
Source links are pinned to that baseline so moved files and changed line numbers do not invalidate the evidence.
See [RuntimeMigration.md](RuntimeMigration.md) for behavior constraints and implementation gates.

This is a relocation map, not an implemented API or evidence that every private helper is safe to move. Query-root reachability identifies helpers to inspect; it does not prove purity. Existing result methods and data constructors remain unchanged. Runtime callbacks are listed separately from mutation bodies; final observer class grouping is not decided by this inventory.

Coverage: 257 types, 995 method/constructor declarations. Property accessors and fields are not counted as methods by this audit. The migration document separately accounts for state, properties and event subscriptions. Direct test references include helpers as well as test cases. An empty list does not establish lack of indirect coverage.

## Rebellion.Game.Events.GameAction

Source: [Assets/Scripts/Game/Events/GameAction.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs#L16).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs#L23) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameAction.ExecuteAll(System.Collections.Generic.IEnumerable<Rebellion.Game.Events.GameAction>, Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs#L31) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameAction.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs)
- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)
- [Assets/Scripts/Game/Events/GameEvent.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEvent.cs)

Direct test references:

- `Rebellion.Tests.Game.Events.GameActionTestExtensions.Execute`
- `Rebellion.Tests.Game.Events.GameActionTestExtensions.ExecuteRequests`
- `Rebellion.Tests.Game.Events.GameActionTests.ExecuteAll_ActionThrows_ExecutesRemainingActions`

## Rebellion.Game.Events.GameActionContext

Source: [Assets/Scripts/Game/Events/GameAction.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs#L55).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameActionContext.GameActionContext(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext, Rebellion.Game.Units.UnitFactory)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs#L71) | Public | Runtime evaluation context constructor; move with executor |
| [`Rebellion.Game.Events.GameActionContext.Request(Rebellion.Game.Requests.GameRequest)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs#L88) | Internal | Runtime activation/evaluation context; remove request ownership |
| [`Rebellion.Game.Events.GameActionContext.Record(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs#L101) | Internal | Runtime activation/evaluation context; remove request ownership |
| [`Rebellion.Game.Events.GameActionContext.Record(System.Collections.Generic.IEnumerable<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs#L115) | Internal | Runtime activation/evaluation context; remove request ownership |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameAction.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs)
- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

- `Rebellion.Tests.Managers.GameManagerTests.EmitResultAction.Execute`
- `Rebellion.Tests.Sectors.GameEventSystemTests.EmitTestResultAction.Execute`

## Rebellion.Game.Events.RollInteger

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L21).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.RollInteger.Roll(Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L36) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)
- [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.RollDouble

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L50).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.RollDouble.Roll(Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L65) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)
- [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs)

Direct test references:

- `Rebellion.Tests.Game.Events.GameActionsTests.RollDouble_ExtremeFiniteRange_ReturnsFiniteValue`

## Rebellion.Game.Events.GameActionNumericValue

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L86).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameActionNumericValue.ResolveInteger(int?, string, Rebellion.Game.Events.RollInteger, Rebellion.Game.Events.GameActionContext, string, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L98) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameActionNumericValue.ResolveDouble(double?, string, Rebellion.Game.Events.RollDouble, Rebellion.Game.Events.GameActionContext, string, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L136) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.RandomOutcome

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L171).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Events.RollOutcomeAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L185).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.RollOutcomeAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L194) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.RollChanceAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L227).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.RollChanceAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L245) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.RollChanceAction.ResolveProbability(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L261) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IfAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L277).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IfAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L288) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SetEventVariableAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L310).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SetEventVariableAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L325) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.RevealToFactionAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L356).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.RevealToFactionAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L369) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SendMessageAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L397).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SendMessageAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L436) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.MessageMediaResolver

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L492).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.MessageMediaResolver.Resolve(Rebellion.Game.Messages.MessageBackgroundImage, Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L500) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.MessageMediaResolver.Resolve(Rebellion.Game.Messages.MessageAudio, Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L523) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.MessageMediaResolver.ResolvePath(string, string, Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L544) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SetCaptureStatusAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L561).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SetCaptureStatusAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L583) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ChangeOfficerRatingAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L642).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ChangeOfficerRatingAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L675) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.ChangeOfficerRatingAction.ResolveOfficers(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L748) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IncreaseForceRankAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L783).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IncreaseForceRankAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L812) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.PerformSkillCheckAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L918).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.PerformSkillCheckAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L940) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SetForceSensitiveAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L981).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SetForceSensitiveAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L991) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SetForceEligibleAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1009).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SetForceEligibleAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1019) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ApplyOfficerInjuryAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1047).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ApplyOfficerInjuryAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1059) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SetOfficerImagesAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1087).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SetOfficerImagesAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1101) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SetOfficerVoiceSetAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1129).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SetOfficerVoiceSetAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1175) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.TriggerDuelAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1211).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.TriggerDuelAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1227) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.DisplayActionTargets

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1267).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.DisplayActionTargets.ResolveTargets(string, System.Collections.Generic.IEnumerable<Rebellion.Game.Events.GameEventSelector>, Rebellion.Game.Events.GameActionContext, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1278) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SetDisplayNameAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1329).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SetDisplayNameAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1345) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SetDisplayStatusAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1368).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SetDisplayStatusAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1384) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ClearDisplayStatusAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1402).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ClearDisplayStatusAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1415) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ChangePlanetValueAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1435).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ChangePlanetValueAction.GetValue(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1461) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.ChangePlanetValueAction.SetValue(Rebellion.Game.Galaxy.Planet, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1468) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.ChangePlanetValueAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1474) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ChangeRawResourceNodesAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1518).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ChangeRawResourceNodesAction.GetValue(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1528) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.ChangeRawResourceNodesAction.SetValue(Rebellion.Game.Galaxy.Planet, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1535) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ChangeEnergyCapacityAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1542).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ChangeEnergyCapacityAction.GetValue(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1552) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.ChangeEnergyCapacityAction.SetValue(Rebellion.Game.Galaxy.Planet, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1559) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ChangePopularSupportAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1565).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ChangePopularSupportAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1593) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SetPopularSupportAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1641).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SetPopularSupportAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1668) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.PopularSupportChange

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1699).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.PopularSupportChange.ResolveFaction(Rebellion.Game.GameRoot, string, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1708) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.PopularSupportChange.Apply(Rebellion.Game.Events.GameActionContext, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1725) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.DamagePlanetResourcesAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1760).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.DamagePlanetResourcesAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1786) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.DamagePlanetResourcesAction.RollLoss(int, double, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1862) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.PlanetActionResults

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1881).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.PlanetActionResults.Create(Rebellion.Game.GameRoot, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Galaxy.PlanetChangeCategory, int, int, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1893) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.PlanetActionTargets

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1924).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.PlanetActionTargets.Resolve(string, string, System.Collections.Generic.IEnumerable<Rebellion.Game.Events.GameEventSelector>, Rebellion.Game.Events.GameActionContext, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1935) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.DestroyUnitsAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1968).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.DestroyUnitsAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L1984) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.DestroyUnitsAction.HasSelectedAncestor(Rebellion.SceneGraph.ISceneNode, System.Collections.Generic.HashSet<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2023) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ChangeOwnerAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2037).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ChangeOwnerAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2051) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.ChangeOwnerAction.IsSupportedUnit(Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2094) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.UnitActionTargets

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2106).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.UnitActionTargets.ResolveUnits(string, System.Collections.Generic.IEnumerable<Rebellion.Game.Events.GameEventSelector>, Rebellion.Game.Events.GameActionContext, string, bool, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2118) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.UnitActionTargets.ResolveDestinations(string, System.Collections.Generic.IEnumerable<Rebellion.Game.Events.GameEventSelector>, Rebellion.Game.Events.GameActionContext, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2193) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.UnitTransferAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2243).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.UnitTransferAction.Resolve(Rebellion.Game.Events.GameActionContext, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2261) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.PlaceUnitsAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2285).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.PlaceUnitsAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2292) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SpawnUnits

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2330).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SpawnUnits.Spawn(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2347) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.SpawnUnits.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2374) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SendUnitsAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2389).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SendUnitsAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2396) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SetNodeStateAction

Source: [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2434).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SetNodeStateAction.Execute(Rebellion.Game.Events.GameActionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs#L2450) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.GameConditional

Source: [Assets/Scripts/Game/Events/GameConditional.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs#L12).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs#L20) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameConditional.IsMet(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs#L27) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameConditional.IsMet(Rebellion.Game.GameRoot, Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs#L35) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameConditional.IsMet(Rebellion.Game.GameRoot, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs#L44) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)
- [Assets/Scripts/Game/Events/GameConditional.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs)
- [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs)
- [Assets/Scripts/Game/Events/GameEvent.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEvent.cs)
- [Assets/Scripts/Systems/GameEventSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs)

Direct test references:

- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_CompatibleBindingComparison_ReturnsTrue`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_DifferentObjectBinding_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_EnumAndStringBindings_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_EnumLiteralComparison_ReturnsTrue`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_IncompatibleBindingComparison_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_NullBinding_ReturnsFalse`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_NullOptionalBinding_UsesPredicateSemantics`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_ObjectBinding_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_OrderedEnumComparison_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.HasEventActivated_ActivationRecorded_ReturnsTrue`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.IsEventComplete_LoadedUnlimitedEvent_ReturnsFalse`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.IsEventComplete_PersistedCompletionState_ReturnsTrue`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.HasBuildingType_DisabledBuilding_ReturnsFalse`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.HasBuildingType_InactivePlanetWithEnabledBuilding_ReturnsTrue`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.HasForceRank_ConfiguredSemanticRank_UsesConfiguredMinimum`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.HasForceRank_InactiveOfficer_UsesConfiguredMinimum`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.IsActive_ActiveOfficer_ReturnsTrue`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.IsActive_InactiveOfficer_ReturnsFalseWithoutLosingIdentity`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.IsCaptured_OptionalCaptor_QualifiesCapturedOfficerWhenProvided`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.IsCaptured_WithCaptor_UncapturedOfficerWithStaleCaptorDoesNotMatch`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.IsKilled_InactiveKilledOfficer_MatchesByRegisteredIdentity`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.ShareAncestor_SamePlanetWithDifferentImmediateParents_Matches`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.ShareParent_DifferentImmediateParents_DoesNotMatch`

## Rebellion.Game.Events.GameConditionContext

Source: [Assets/Scripts/Game/Events/GameConditional.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs#L51).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameConditionContext.GameConditionContext(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs#L62) | Public | Runtime evaluation context constructor; move with executor |
| [`Rebellion.Game.Events.GameConditionContext.GameConditionContext(Rebellion.Game.GameRoot, Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs#L70) | Public | Runtime evaluation context constructor; move with executor |
| [`Rebellion.Game.Events.GameConditionContext.GameConditionContext(Rebellion.Game.GameRoot, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs#L78) | Public | Runtime evaluation context constructor; move with executor |
| [`Rebellion.Game.Events.GameConditionContext.GameConditionContext(Rebellion.Game.GameRoot, Rebellion.Game.Events.GameEventEvaluationContext, Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditional.cs#L87) | Private | Runtime evaluation context constructor; move with executor |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.AllConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L19).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.AllConditional.AllConditional()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L28) | Public | Serialized definition constructor remains in Game |
| [`Rebellion.Game.Events.AllConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L36) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.AnyConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L43).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.AnyConditional.AnyConditional()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L52) | Public | Serialized definition constructor remains in Game |
| [`Rebellion.Game.Events.AnyConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L60) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.NotConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L67).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.NotConditional.NotConditional()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L76) | Public | Serialized definition constructor remains in Game |
| [`Rebellion.Game.Events.NotConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L84) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.XorConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L91).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.XorConditional.XorConditional()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L100) | Public | Serialized definition constructor remains in Game |
| [`Rebellion.Game.Events.XorConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L108) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IntegerComparison

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L130).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IntegerComparison.Evaluate(int, Rebellion.Game.Events.ComparisonOperator, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L139) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.TickCountConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L157).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.TickCountConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L171) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.HasEventActivatedConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L181).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.HasEventActivatedConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L192) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IsEventCompleteConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L201).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IsEventCompleteConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L212) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.EvaluateEventVariableConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L221).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.EvaluateEventVariableConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L238) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.EvaluateBindingConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L248).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.EvaluateBindingConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L268) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.EvaluateBindingConditional.ConvertLiteral(object, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L316) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.EvaluateBindingConditional.Compare(object, object)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L372) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.EvaluateBindingConditional.IsOrderedComparison()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L410) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.BindingIncludesUnitConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L421).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.BindingIncludesUnitConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L435) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.OfficerBooleanConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L461).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.OfficerBooleanConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L471) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.OfficerBooleanConditional.Evaluate(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L485) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IsCapturedConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L488).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IsCapturedConditional.Evaluate(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L499) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IsKilledConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L507).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IsKilledConditional.Evaluate(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L515) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IsInjuredConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L518).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IsInjuredConditional.Evaluate(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L526) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IsForceEligibleConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L529).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IsForceEligibleConditional.Evaluate(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L537) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.HasForceRankConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L543).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.HasForceRankConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L560) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.HasBuildingTypeConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L580).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.HasBuildingTypeConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L597) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IsOwnedConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L618).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IsOwnedConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L635) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.RollAgainstPopularSupportConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L657).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.RollAgainstPopularSupportConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L674) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.SceneConditionsTests.RollAgainstPopularSupport_RollBelowSupport_ReturnsTrue`

## Rebellion.Game.Events.EventUnitReference

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L690).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Events.SceneAncestors

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L707).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SceneAncestors.Resolve(Rebellion.SceneGraph.ISceneNode, Rebellion.Game.Events.SceneAncestorType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L715) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs)
- [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ShareParentConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L728).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ShareParentConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L738) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.ShareParentConditional.ResolveDistinctUnits(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L752) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ShareAncestorConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L756).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ShareAncestorConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L769) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SceneConditionUnits

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L782).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SceneConditionUnits.ResolveDistinct(Rebellion.Game.GameRoot, System.Collections.Generic.IReadOnlyCollection<Rebellion.Game.Events.EventUnitReference>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L790) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.AreOnOpposingFactionsConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L812).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.AreOnOpposingFactionsConditional.AreOnOpposingFactionsConditional()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L820) | Public | Serialized definition constructor remains in Game |
| [`Rebellion.Game.Events.AreOnOpposingFactionsConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L828) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IsOnMissionConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L846).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IsOnMissionConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L857) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IsActiveConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L871).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IsActiveConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L882) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IsInTransitConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L895).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IsInTransitConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L906) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IsAtLocationConditional

Source: [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L914).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IsAtLocationConditional.IsMet(Rebellion.Game.Events.GameConditionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs#L925) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.GameEventState

Source: [Assets/Scripts/Game/Events/GameEvent.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEvent.cs#L11).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Events.GameEvent

Source: [Assets/Scripts/Game/Events/GameEvent.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEvent.cs#L24).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameEvent.CanActivate(Rebellion.Game.Events.GameEventState)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEvent.cs#L43) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEvent.GameEvent()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEvent.cs#L50) | Public | Serialized definition constructor remains in Game |
| [`Rebellion.Game.Events.GameEvent.GameEvent(System.Collections.Generic.List<Rebellion.Game.Events.GameConditional>, System.Collections.Generic.List<Rebellion.Game.Events.GameAction>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEvent.cs#L57) | Public | Serialized definition constructor remains in Game |
| [`Rebellion.Game.Events.GameEvent.AreConditionsMet(Rebellion.Game.GameRoot, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEvent.cs#L69) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEvent.ExecuteActions(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext, Rebellion.Game.Units.UnitFactory)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEvent.cs#L87) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/GameEventSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs)

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventTests.CanActivate_MaximumActivationsReached_ReturnsFalse`
- `Rebellion.Tests.Game.Events.GameEventTests.CanActivate_UnlimitedEvent_ReturnsTrue`
- `Rebellion.Tests.Sectors.GameEventSystemTests.Execute_NestedActions_LaterActionObservesEarlierResult`

## Rebellion.Game.Events.GameEventBindingSource

Source: [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L15).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameEventBindingSource.Resolve(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L30) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SkillRatingBindingSource

Source: [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L40).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SkillRatingBindingSource.Resolve(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L63) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.OfficerForceBindingSource

Source: [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L87).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.OfficerForceBindingSource.Resolve(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L106) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.PlanetStatBindingSource

Source: [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L130).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.PlanetStatBindingSource.Resolve(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L153) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectionCountBindingSource

Source: [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L177).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectionCountBindingSource.Resolve(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L192) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.GameEventBinding

Source: [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L212).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameEventBinding.GetValueType()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L235) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventBinding.Bind(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs#L252) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/GameEventSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs)

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventBindingTests.Bind_NumericRanges_StoresRolledValues`
- `Rebellion.Tests.Game.Events.GameEventBindingTests.Bind_PlanetStatWithInactivePlanet_StoresResolvedValue`
- `Rebellion.Tests.Game.Events.GameEventBindingTests.Bind_TypedOfficerSourcesWithInactiveOfficer_StoresResolvedValues`
- `Rebellion.Tests.Game.Events.GameEventBindingTests.Bind_TypedSources_StoresResolvedValues`

## Rebellion.Game.Events.GameEventEvaluationContext

Source: [Assets/Scripts/Game/Events/GameEventEvaluationContext.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs#L11).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameEventEvaluationContext.GameEventEvaluationContext(Rebellion.Game.Events.GameEvent, Rebellion.Game.Events.GameEventState, Rebellion.Game.Results.GameResult, Rebellion.Game.Events.GameEventTrigger)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs#L30) | Public | Runtime evaluation context constructor; move with executor |
| [`Rebellion.Game.Events.GameEventEvaluationContext.Bind(string, object)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs#L48) | Public | Runtime activation/evaluation context; remove request ownership |
| [`Rebellion.Game.Events.GameEventEvaluationContext.TryGetBinding<T>(string, out T)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs#L63) | Public | Runtime activation/evaluation context; remove request ownership |
| [`Rebellion.Game.Events.GameEventEvaluationContext.GetBinding<T>(string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs#L81) | Public | Runtime activation/evaluation context; remove request ownership |
| [`Rebellion.Game.Events.GameEventEvaluationContext.TryGetBinding(string, out object)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs#L89) | Public | Runtime activation/evaluation context; remove request ownership |
| [`Rebellion.Game.Events.GameEventEvaluationContext.TryGetBindingReference<T>(string, out T)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs#L99) | Public | Runtime activation/evaluation context; remove request ownership |
| [`Rebellion.Game.Events.GameEventEvaluationContext.TryGetBindingReference(string, out object)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs#L118) | Public | Runtime activation/evaluation context; remove request ownership |
| [`Rebellion.Game.Events.GameEventEvaluationContext.GetBindingReference<T>(string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs#L131) | Public | Runtime activation/evaluation context; remove request ownership |
| [`Rebellion.Game.Events.GameEventEvaluationContext.AddResult(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs#L140) | Public | Runtime activation/evaluation context; remove request ownership |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameAction.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameAction.cs)
- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)
- [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs)
- [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs)
- [Assets/Scripts/Game/Events/GameEventEvaluationContext.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs)
- [Assets/Scripts/Game/Events/GameEventSelector.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelector.cs)
- [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs)
- [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs)

Direct test references:

- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_CompatibleBindingComparison_ReturnsTrue`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_DifferentObjectBinding_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_EnumAndStringBindings_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_EnumLiteralComparison_ReturnsTrue`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_IncompatibleBindingComparison_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_NullBinding_ReturnsFalse`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_NullOptionalBinding_UsesPredicateSemantics`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_ObjectBinding_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.EvaluateBinding_OrderedEnumComparison_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.GameActionsTests.ChangeRawResourceNodes_BoundAmount_AppliesReusedInteger`
- `Rebellion.Tests.Game.Events.GameActionsTests.ChangeRawResourceNodes_Default_IncreasesExplicitAmount`
- `Rebellion.Tests.Game.Events.GameActionsTests.ChangeRawResourceNodes_NeutralPlanet_ReportsNoFaction`
- `Rebellion.Tests.Game.Events.GameActionsTests.DamagePlanetResources_MinimumLoss_GuaranteesOnePointLoss`
- `Rebellion.Tests.Game.Events.GameEventBindingTests.Bind_NumericRanges_StoresRolledValues`
- `Rebellion.Tests.Game.Events.GameEventBindingTests.Bind_PlanetStatWithInactivePlanet_StoresResolvedValue`
- `Rebellion.Tests.Game.Events.GameEventBindingTests.Bind_TypedOfficerSourcesWithInactiveOfficer_StoresResolvedValues`
- `Rebellion.Tests.Game.Events.GameEventBindingTests.Bind_TypedSources_StoresResolvedValues`
- `Rebellion.Tests.Game.Events.GameEventEvaluationContextTests.AddResult_NullResult_DoesNotRecordResult`
- `Rebellion.Tests.Game.Events.GameEventEvaluationContextTests.Bind_BlankName_ThrowsArgumentException`
- `Rebellion.Tests.Game.Events.GameEventEvaluationContextTests.Bind_DuplicateName_ThrowsInvalidOperationException`
- `Rebellion.Tests.Game.Events.GameEventEvaluationContextTests.GetBindingReference_ExactOpaqueName_ReturnsValue`
- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectBinding_InactiveRegisteredNode_ReturnsCanonicalNode`
- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectBinding_StaleReferenceWithRegisteredInstanceID_ReturnsCanonicalNode`
- `Rebellion.Tests.Game.Events.GameEventTriggerTests.Bind_TriggerArgument_ExposesOnlyAuthoredValue`
- `Rebellion.Tests.Game.Events.SceneConditionsTests.RollAgainstPopularSupport_RollBelowSupport_ReturnsTrue`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HasArrivalBindingsConditional.IsMet`
- `Rebellion.Tests.Sectors.GameEventSystemTests.RecordScopedPlanetAction.Execute`

## Rebellion.Game.Events.GameEventRuntimeState

Source: [Assets/Scripts/Game/Events/GameEventRuntimeState.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventRuntimeState.cs#L10).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameEventRuntimeState.GetState(string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventRuntimeState.cs#L23) | Public | Persisted event-state management remains in Game |
| [`Rebellion.Game.Events.GameEventRuntimeState.GetVariable(string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventRuntimeState.cs#L43) | Public | Persisted event-state management remains in Game |
| [`Rebellion.Game.Events.GameEventRuntimeState.SetVariable(string, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventRuntimeState.cs#L55) | Public | Persisted event-state management remains in Game |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)
- [Assets/Scripts/Game/Events/GameConditionals.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameConditionals.cs)
- [Assets/Scripts/Systems/GameEventSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs)

Direct test references:

- `Rebellion.Tests.Game.Events.EventStateConditionsTests.HasEventActivated_ActivationRecorded_ReturnsTrue`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.IsEventComplete_LoadedUnlimitedEvent_ReturnsFalse`
- `Rebellion.Tests.Game.Events.EventStateConditionsTests.IsEventComplete_PersistedCompletionState_ReturnsTrue`
- `Rebellion.Tests.Game.Events.GameActionsTests.IfAction_EventVariable_SelectsBranchAndPersistsMutation`
- `Rebellion.Tests.Game.Events.GameActionsTests.PerformSkillCheck_FailedRoll_ExecutesFailureActions`
- `Rebellion.Tests.Game.Events.GameActionsTests.PerformSkillCheck_InjuredOfficer_UsesEffectiveRating`
- `Rebellion.Tests.Game.Events.GameActionsTests.PerformSkillCheck_NegativeRatingMultiplier_UsesScaledScore`
- `Rebellion.Tests.Game.Events.GameActionsTests.PerformSkillCheck_SuccessfulRoll_ExecutesSuccessActions`
- `Rebellion.Tests.Game.Events.GameActionsTests.RollChance_FailedProbability_DoesNotExecuteActions`
- `Rebellion.Tests.Game.Events.GameActionsTests.RollChance_RolledProbability_ExecutesActionsOnSuccess`
- `Rebellion.Tests.Game.Events.GameActionsTests.RollOutcome_WeightedSelection_ExecutesEveryActionInSelectedOutcome`
- `Rebellion.Tests.Game.Events.GameEventRuntimeStateTests.GetState_NewEvent_ReturnsIncompleteState`
- `Rebellion.Tests.Game.Events.GameEventRuntimeStateTests.GetState_SameEvent_ReturnsCanonicalState`
- `Rebellion.Tests.Game.Events.GameEventRuntimeStateTests.GetVariable_MissingKey_ReturnsZero`
- `Rebellion.Tests.Managers.SaveGameManagerTests.SaveAndLoadGame_GameWithEventVariables_PreservesStoryState`
- `Rebellion.Tests.Sectors.GameEventSystemTests.CreateDependentEvent`
- `Rebellion.Tests.Sectors.GameEventSystemTests.Execute_NestedActions_LaterActionObservesEarlierResult`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_MatchingEncounter_ActivatesResultTriggeredEventOnce`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_MatchingOptionalSourceBinding_ActivatesEvent`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_RepeatableEncounterEffect_ActivatesForEveryEncounter`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_SecondUnitArrivedAlternativeMatches_ActivatesOnce`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_StableTriggerId_ActivatesWithoutClrTypeName`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ObserveTestResultAction.Execute`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_EachOwnedPlanetTarget_ArmsWhenNeutralPlanetBecomesOwned`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_EachOwnedPlanetTarget_RearmsAfterNeutralInterval`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_MaximumActivationsFive_ActivatesFiveTimes`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_MaximumActivationsThree_ActivatesThreeTimes`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_MetOneShotEvent_CompletesAndLeavesPool`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_MetRepeatableEvent_CompletesAndRemainsActive`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_OneShotTarget_ActivatesTargetOnce`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_RandomDelay_WaitsUntilRolledAbsoluteTick`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_RandomTargetBeforeScheduledTick_DoesNotSelectTarget`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_RecurringScheduleUntilMet_CompletesAndRemovesEvent`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_RecurringScheduleUntilMet_UsesEvaluationBinding`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_RepeatDelay_PreventsActivationUntilCooldownExpires`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_ResultTriggeredEvent_DoesNotRunDuringScheduledPolling`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_TargetedPlanet_UsesOnePersistedSchedule`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_UnmetOneShotEvent_RemainsPending`
- `Rebellion.Tests.Sectors.GameEventSystemTests.RecordScopedPlanetAction.Execute`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ValidateEvents_DependencyCompletedAndRemovedFromPool_DoesNotThrow`

## Rebellion.Game.Events.GameEventSchedule

Source: [Assets/Scripts/Game/Events/GameEventSchedule.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L10).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameEventSchedule.GetInitialRange(out int, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L45) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventSchedule.GetRepeatRange(out int, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L79) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/GameEventSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs)

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventScheduleTests.GetInitialRange_AtSchedule_ReturnsAbsoluteTick`
- `Rebellion.Tests.Game.Events.GameEventScheduleTests.GetInitialRange_EverySchedule_ReturnsInitialDelay`
- `Rebellion.Tests.Game.Events.GameEventScheduleTests.GetInitialRange_RandomDelaySchedule_ReturnsInclusiveRange`
- `Rebellion.Tests.Game.Events.GameEventScheduleTests.GetRepeatRange_RandomIntervalSchedule_ReturnsInclusiveRange`

## Rebellion.Game.Events.AfterEvent

Source: [Assets/Scripts/Game/Events/GameEventSchedule.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L94).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Events.AfterEvents

Source: [Assets/Scripts/Game/Events/GameEventSchedule.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L107).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Events.EventDependency

Source: [Assets/Scripts/Game/Events/GameEventSchedule.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L116).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Events.AtTick

Source: [Assets/Scripts/Game/Events/GameEventSchedule.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L126).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Events.EveryTicks

Source: [Assets/Scripts/Game/Events/GameEventSchedule.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L136).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Events.RandomDelay

Source: [Assets/Scripts/Game/Events/GameEventSchedule.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L152).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.RandomDelay.GetRange(out int, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L166) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameEventSchedule.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.RandomInterval

Source: [Assets/Scripts/Game/Events/GameEventSchedule.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L176).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.RandomInterval.GetRange(out int, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs#L193) | Public | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameEventSchedule.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSchedule.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.GameEventSelector

Source: [Assets/Scripts/Game/Events/GameEventSelector.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelector.cs#L13).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameEventSelector.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelector.cs#L23) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventSelector.Active<T>(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelector.cs#L35) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventSelector.MatchesLocation(Rebellion.SceneGraph.ISceneNode, Rebellion.Game.Events.GameEventEvaluationContext, string, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelector.cs#L49) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)
- [Assets/Scripts/Game/Events/GameEventBinding.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventBinding.cs)
- [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.OwnedSceneNodeSelector<T>

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L18).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.OwnedSceneNodeSelector<T>.SelectOwned(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L35) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.OwnedSceneNodeSelector<T>.SelectOwned(System.Collections.Generic.IEnumerable<T>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L48) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.LocatedSceneNodeSelector<T>

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L64).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.LocatedSceneNodeSelector<T>.SelectLocated(Rebellion.Game.GameRoot, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L79) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ManufacturableSelector<T>

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L87).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ManufacturableSelector<T>.SelectManufacturable(Rebellion.Game.GameRoot, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L102) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectPlanets

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L121).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectPlanets.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L134) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectPlanets_DestroyedPlanet_ReturnsNothing`
- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectPlanets_MatchingInstanceID_ReturnsPlanet`
- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectPlanets_NoFilters_ReturnsEverySurvivingPlanet`

## Rebellion.Game.Events.SelectPlanetSectors

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L150).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectPlanetSectors.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L169) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectOfficers

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L193).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectOfficers.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L206) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectOfficers_IncludeInactiveAtCurrentPlanet_ReturnsOfficer`

## Rebellion.Game.Events.SelectSpecialForces

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L221).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectSpecialForces.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L231) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectFleets

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L241).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectFleets.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L251) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectMissions

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L261).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectMissions.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L271) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectCapitalShips

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L281).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectCapitalShips.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L291) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectCapitalShips_IncludeInactive_ReturnsCapitalShip`

## Rebellion.Game.Events.SelectStarfighters

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L301).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectStarfighters.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L311) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectRegiments

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L321).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectRegiments.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L331) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectBuildings

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L351).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectBuildings.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L364) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.SelectBuildings.MatchesCategory(Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L375) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectManufacturingOrders

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L393).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectManufacturingOrders.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L418) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectManufacturingOrders_MatchingPlanet_ReturnsQueuedProduct`

## Rebellion.Game.Events.SelectRandom

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L456).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectRandom.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L481) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectRandom_FilteredPlanetSet_ReturnsRequestedCount`

## Rebellion.Game.Events.SelectFirst

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L532).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectFirst.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L545) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.SelectFirst.SelectCandidates(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L558) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameActions.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameActions.cs)
- [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectBinding

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L568).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectBinding.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L581) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectBinding_InactiveRegisteredNode_ReturnsCanonicalNode`
- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectBinding_StaleReferenceWithRegisteredInstanceID_ReturnsCanonicalNode`

## Rebellion.Game.Events.SelectNearestParent

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L624).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectNearestParent.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L640) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.SelectPreviousLocation

Source: [Assets/Scripts/Game/Events/GameEventSelectors.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L655).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SelectPreviousLocation.Select(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventSelectors.cs#L671) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventSelectorTests.SelectPreviousLocation_InactiveUnit_ReturnsPreviousLocation`

## Rebellion.Game.Events.GameEventTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L18).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameEventTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L34) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventTrigger.Bind(Rebellion.Game.Events.GameEventEvaluationContext, Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L41) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventTrigger.GetBindingType(string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L58) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventTrigger.MatchesInstanceID(string, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L67) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventTrigger.MatchesSource(string, Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L77) | Protected | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameEventEvaluationContext.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventEvaluationContext.cs)
- [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs)
- [Assets/Scripts/Systems/GameEventSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.GameEventTriggerArgument

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L84).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameEventTriggerArgument.GameEventTriggerArgument(System.Type, System.Func<Rebellion.Game.Results.GameResult, object>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L96) | Private | Serialized definition constructor remains in Game |
| [`Rebellion.Game.Events.GameEventTriggerArgument.Resolve(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L107) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventTriggerArgument.Create<TResult, TValue>(System.Func<TResult, TValue>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L116) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.GameEventTriggerArguments

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L126).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.GameEventTriggerArguments.Get(System.Type, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L137) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventTriggerArguments.Build()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L153) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |
| [`Rebellion.Game.Events.GameEventTriggerArguments.Add<TResult, TValue>(System.Collections.Generic.IDictionary<(System.Type, string), Rebellion.Game.Events.GameEventTriggerArgument>, string, System.Func<TResult, TValue>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L450) | Private | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.PlanetOwnershipChangedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L464).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.PlanetOwnershipChangedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L489) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventTriggerTests.Matches_PlanetOwnershipChangedTrigger_AppliesOwnershipFilters`

## Rebellion.Game.Events.PlanetStatChangedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L501).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.PlanetStatChangedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L523) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.BlockadeChangedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L534).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.BlockadeChangedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L553) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.UprisingStartedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L563).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.UprisingStartedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L582) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.UprisingEndedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L592).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.UprisingEndedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L611) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.IntelligenceRevealedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L621).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.IntelligenceRevealedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L640) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventTriggerTests.Matches_IntelligenceRevealedTrigger_AppliesRecipientAndObservationFilters`

## Rebellion.Game.Events.MaintenanceRequiredTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L655).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.MaintenanceRequiredTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L671) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventTriggerTests.Matches_MaintenanceRequiredTrigger_AppliesFactionFilter`

## Rebellion.Game.Events.ResearchAdvancedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L684).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ResearchAdvancedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L706) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.MissionParticipantFilter

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L730).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.MissionParticipantFilter.Matches(System.Collections.Generic.IReadOnlyCollection<Rebellion.Game.Units.IMissionParticipant>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L744) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.MissionCompletedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L762).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.MissionCompletedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L786) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.OfficerCaptureChangedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L808).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.OfficerCaptureChangedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L827) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventTriggerTests.Matches_OfficerCaptureChangedTrigger_AppliesOfficerAndStateFilters`

## Rebellion.Game.Events.OfficerKilledTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L841).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.OfficerKilledTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L857) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.OfficerInjuredTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L866).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.OfficerInjuredTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L882) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.OfficerRecruitedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L891).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.OfficerRecruitedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L913) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.ForceDiscoveryChangedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L924).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ForceDiscoveryChangedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L946) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventTriggerTests.Matches_ForceDiscoveryChangedTrigger_AppliesOfficerAndEventTypeFilters`

## Rebellion.Game.Events.UnitOwnershipChangedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L961).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.UnitOwnershipChangedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L983) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.UnitCreatedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L994).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.UnitCreatedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1010) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.UnitDestroyedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1019).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.UnitDestroyedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1038) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventTriggerTests.Matches_UnitDestroyedTrigger_CoversEveryDestructionPath`

## Rebellion.Game.Events.UnitArrivedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1048).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.UnitArrivedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1067) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventTriggerTests.Matches_UnitArrivedTrigger_AppliesIdentityAndDestinationFilters`

## Rebellion.Game.Events.SpaceCombatCompletedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1084).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.SpaceCombatCompletedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1109) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.BombardmentCompletedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1121).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.BombardmentCompletedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1149) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventTriggerTests.Matches_BombardmentCompletedTrigger_AppliesOutcomeFilters`

## Rebellion.Game.Events.PlanetaryAssaultCompletedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1162).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.PlanetaryAssaultCompletedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1190) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Events.DuelCompletedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1203).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.DuelCompletedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1222) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

- `Rebellion.Tests.Game.Events.GameEventTriggerTests.Matches_DuelCompletedTrigger_AppliesOfficerAndSourceFilters`

## Rebellion.Game.Events.ManufacturingCompletedTrigger

Source: [Assets/Scripts/Game/Events/GameEventTrigger.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1239).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Events.ManufacturingCompletedTrigger.Matches(Rebellion.Game.Results.GameResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Events/GameEventTrigger.cs#L1261) | Internal | Authored runtime execution/evaluation owned by GameEventExecutor; definitions remain data |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Requests.GameRequest

Source: [Assets/Scripts/Game/Requests/GameRequests.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Requests/GameRequests.cs#L13).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Requests.OwnershipChangeRequest

Source: [Assets/Scripts/Game/Requests/GameRequests.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Requests/GameRequests.cs#L22).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Requests.DuelRequest

Source: [Assets/Scripts/Game/Requests/GameRequests.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Requests/GameRequests.cs#L32).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Requests.MessageDeliveryRequest

Source: [Assets/Scripts/Game/Requests/GameRequests.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Requests/GameRequests.cs#L43).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Requests.UnitMovementRequest

Source: [Assets/Scripts/Game/Requests/GameRequests.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Requests/GameRequests.cs#L72).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Requests.UnitPlacementRequest

Source: [Assets/Scripts/Game/Requests/GameRequests.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Requests/GameRequests.cs#L81).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.GameResult

Source: [Assets/Scripts/Game/Results/GameResult.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResult.cs#L6).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.PopularSupportShiftResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L80).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.PlanetStatChangedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L90).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.SmugglingChangedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L102).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.BlockadeChangedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L114).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.PlanetUprisingStartedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L124).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.PlanetNearUprisingResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L133).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.PlanetUprisingEndedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L141).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.PlanetOwnershipChangedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L150).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.HeadquartersLostResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L162).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.HeadquartersCapturedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L172).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.HeadquartersDestroyedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L177).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.PlanetGarrisonChangedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L185).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.IntelligenceRevealedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L197).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.MaintenanceRequiredResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L206).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ResearchOrderedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L216).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ResearchExhaustedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L228).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.RecruitmentExhaustedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L239).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.VictoryResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L248).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.MissionCompletedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L263).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.PlanetsRevealedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L281).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ForceDiscoveryResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L294).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.OfficerRecruitedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L305).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.OfficerCaptureStateResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L315).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.OfficerKilledResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L330).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.OfficerAssassinatedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L340).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.OfficerRescuedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L345).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.OfficerInjuredResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L355).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.CommandKindChangedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L365).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.OfficerCommandingResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L375).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.TraitorDiscoveredResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L385).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ForceTrainingResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L395).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ForceExperienceResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L405).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.MessageDeliveredResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L417).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.UnitOwnershipChangedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L434).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.GameObjectCreatedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L444).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.GameObjectDeployedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L452).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.GameObjectEnrouteResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L460).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.GameObjectEnrouteActiveResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L468).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.UnitArrivedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L477).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.FleetWaypointsCompletedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L487).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.GameObjectDamagedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L496).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.GameObjectDestroyedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L505).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.GameObjectScrappedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L516).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.GameObjectDestroyedOnArrivalResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L525).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Results.GameObjectDestroyedOnArrivalResult.GameObjectDestroyedOnArrivalResult()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L530) | Public | Unchanged result contract / existing helper |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Results.GameObjectAutoscrappedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L539).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Results.GameObjectAutoscrappedResult.GameObjectAutoscrappedResult()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L544) | Public | Unchanged result contract / existing helper |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Results.GameObjectSabotagedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L553).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Results.GameObjectSabotagedResult.GameObjectSabotagedResult()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L556) | Public | Unchanged result contract / existing helper |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Results.DuelResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L569).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.FighterDamageResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L584).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ShipHullDamageResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L595).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ShipDamageResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L606).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.FighterLossResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L616).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.CombatUnitSnapshot

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L626).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Game.Results.CombatUnitSnapshot.CombatUnitSnapshot(Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L638) | Public | Unchanged result contract / existing helper |
| [`Rebellion.Game.Results.CombatUnitSnapshot.CaptureFleetUnits(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L660) | Public | Unchanged result contract / existing helper |
| [`Rebellion.Game.Results.CombatUnitSnapshot.CapturePlanetUnits(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L676) | Public | Unchanged result contract / existing helper |
| [`Rebellion.Game.Results.CombatUnitSnapshot.RecordOutcomes(System.Collections.Generic.IEnumerable<Rebellion.Game.Results.CombatUnitSnapshot>, System.Collections.Generic.IEnumerable<Rebellion.SceneGraph.ISceneNode>, System.Collections.Generic.IEnumerable<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L696) | Public | Unchanged result contract / existing helper |
| [`Rebellion.Game.Results.CombatUnitSnapshot.GetInstanceIDs(System.Collections.Generic.IEnumerable<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L721) | Private | Unchanged result contract / existing helper |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Game.Results.SpaceCombatResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L733).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.PendingCombatResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L761).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.BombardmentResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L786).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.PlanetaryAssaultResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L817).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.EvacuationLossesResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L846).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ManufacturingIdleResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L862).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ManufacturingRemainingResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L872).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ManufacturingPointsRequiredResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L882).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ManufacturingPointsCompletedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L892).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Game.Results.ManufacturingDeployedResult

Source: [Assets/Scripts/Game/Results/GameResults.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Game/Results/GameResults.cs#L902).

No declared methods. Data/nested state type; no behavior extraction inferred.

## GameManager

Source: [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L20).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`GameManager.GameManager(Rebellion.Game.GameRoot, GameDataCatalog)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L152) | Public | GameManager clock/application construction + GameSession initialization |
| [`GameManager.ReplaceGame(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L162) | Public | GameRuntime / GameSession replacement; preserve GameReplaced |
| [`GameManager.ReconcileLoadedState()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L171) | Internal | GameTickProcessor loaded-combat reconciliation |
| [`GameManager.GetGame()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L180) | Public | Existing game data access at application boundary |
| [`GameManager.GetCurrentTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L186) | Public | Existing GameRoot.CurrentTick read |
| [`GameManager.GetPlayerFaction()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L192) | Public | Existing GameRoot player/faction read |
| [`GameManager.GetPlayerUIState()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L198) | Public | Existing player UI state read |
| [`GameManager.GetFogOfWarSystem()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L208) | Public | Replace caller dependency with FogOfWarQueries |
| [`GameManager.ProcessFactionAutomation(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L214) | Public | FactionAutomationCommands, then NamingCommands; preserve order |
| [`GameManager.GetGameSpeed()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L224) | Public | Retain in GameManager |
| [`GameManager.SetGameSpeed(Rebellion.Game.TickSpeed)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L230) | Public | Retain in GameManager |
| [`GameManager.AdvanceTime(float)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L263) | Public | Retain clock input; call GameTickProcessor |
| [`GameManager.TryAdvanceTickTimer(float)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L274) | Public | Retain in GameManager; use tick processor busy/pending state |
| [`GameManager.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L295) | Public | GameTickProcessor; preserve existing sequence |
| [`GameManager.ProcessTickIncrementally()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L312) | Public | GameTickProcessor; preserve existing sequence |
| [`GameManager.ProcessTickCore()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L340) | Private | GameTickProcessor; preserve existing sequence |
| [`GameManager.ProcessRemainingTickPhases()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L389) | Private | GameTickProcessor; preserve existing sequence |
| [`GameManager.ResolveCombat(bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L415) | Public | GameTickProcessor; preserve existing sequence |
| [`GameManager.ResolveCombatRetreat(string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L426) | Public | GameTickProcessor; preserve existing sequence |
| [`GameManager.InitializeGame(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L441) | Private | Split GameSession initialization from retained clock reset |
| [`GameManager.InitializeSystems()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L463) | Private | GameSession construction |
| [`GameManager.InitializeResultProcessing()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L554) | Private | GameSession typed subscription wiring |
| [`GameManager.RebuildDerivedState()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L586) | Private | GameSession initialization |
| [`GameManager.CopyTemplates<T>(System.Collections.Generic.IEnumerable<T>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L608) | Private | GameSession derived-catalog helper |
| [`GameManager.CompleteCombatResolution(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L624) | Private | GameTickProcessor; preserve existing sequence |
| [`GameManager.ReconcileLoadedCombatState()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L676) | Private | GameTickProcessor; preserve existing sequence |
| [`GameManager.ProcessAvailableWaypointContinuations()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L696) | Private | GameTickProcessor; preserve existing sequence |
| [`GameManager.ProcessResults(System.Collections.Generic.IEnumerable<Rebellion.Game.Results.GameResult>, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L714) | Private | GameResultBus delivery / existing presentation boundaries |
| [`GameManager.ProcessMessageReactions(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L749) | Private | Message delivery + GameResultBus follow-ups |
| [`GameManager.HandleSystemResultsProduced(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L773) | Private | Replace with explicit bus publication; preserve bombardment notification |
| [`GameManager.StoreDeferredMessageResults(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L784) | Private | GameTickProcessor; preserve existing sequence |
| [`GameManager.BeginPendingCombatDecision()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L794) | Private | GameTickProcessor; preserve existing sequence |
| [`GameManager.TakeDeferredMessageResults()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L804) | Private | GameTickProcessor; preserve existing sequence |
| [`GameManager.CombineResults(params System.Collections.Generic.List<Rebellion.Game.Results.GameResult>[])`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs#L816) | Private | GameTickProcessor; preserve existing sequence |

Direct production callers (including same-owner helpers):

- [Assets/Editor/Simulation/HeadlessSimulationRunner.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Editor/Simulation/HeadlessSimulationRunner.cs)
- [Assets/Scripts/App/GameRuntime.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/App/GameRuntime.cs)
- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/UI/Input/AppInputController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/Input/AppInputController.cs)
- [Assets/Scripts/UI/SceneUI/OptionsMenu/OptionsMenuController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/OptionsMenu/OptionsMenuController.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Advisor/AdvisorCommandController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Advisor/AdvisorCommandController.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/GalaxyMap/GalaxyMapController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/GalaxyMap/GalaxyMapController.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Screen/GameFlowController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Screen/GameFlowController.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Screen/StrategyController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Screen/StrategyController.cs)

Direct test references:

- `Rebellion.Tests.App.GameRuntimeTests.StartGame_PendingCombat_DefersAutosaveUntilResolution`
- `Rebellion.Tests.Managers.GameManagerTests.AdvanceTime_BelowCompletedInterval_DoesNotProcessTick`
- `Rebellion.Tests.Managers.GameManagerTests.AdvanceTime_CompletedInterval_ProcessesTickAndRaisesTickCompleted`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessFactionAutomation_ManageNaming_AssignsNameImmediately`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTickIncrementally_DisposedBeforeCompletion_AllowsNextTick`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_AdvisorOrderCompletes_RefillsReleasedLaneOnly`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_BlockadeStarts_ReroutesInboundStarfighter`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_CapturedOfficerWithDueEscapeAttempt_FreesOfficer`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_EventCapturesMissionParticipant_CompletesCaptureLifecycle`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_EventResults_DoesNotAddAutomaticMessages`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_ExpiredMessage_RemovesMessageAfterTickAdvances`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_FleetArrivesAtPlanetaryStarfighters_CreatesPendingCombat`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_FleetDestroyedAfterArrival_AddsFleetArrivalAndBattleMessages`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_FleetReachesWaypoint_StartsNextLegAfterCombatDetection`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_FullyRecoveredUnits_DeliversRecoveryMessages`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_InjuredOfficerAtFriendlyPlanet_Heals`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_LoadedConvergingMultipleFleets_ResolvesSingleCombinedCombat`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_PausedGame_DoesNotAdvanceTick`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_PendingCombat_CompletesTickAfterResolution`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_PlanetaryAssaultResult_RaisesResolvedEvent`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_SabotageResult_RemovesDestroyedObjectFromActorSnapshot`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_VictoryConditionMet_RaisesVictoryDeclaredOnce`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_VictoryResult_RaisesResolvedEvent`
- `Rebellion.Tests.Managers.GameManagerTests.ReconcileLoadedState_ContestedPlayerFleet_RestoresPendingCombat`
- `Rebellion.Tests.Managers.GameManagerTests.ResolveCombat_UnrelatedFleetReachedWaypoint_StartsDeferredNextLeg`
- `Rebellion.Tests.Managers.GameManagerTests.SetGameSpeed_ConfiguredIntervals_UpdatesTickInterval`
- `Rebellion.Tests.UI.SceneUI.OptionsMenu.OptionsMenuControllerTests.ActiveGame_OpenAndBackToGame_PausesAndRestoresSpeed`
- `Rebellion.Tests.UI.SceneUI.StrategyView.Construction.ConstructionWindowControllerTests.CreateController`
- `Rebellion.Tests.UI.SceneUI.StrategyView.Fleet.FleetWindowControllerTests.CreateFleetCommandController`
- `Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSector.PlanetSectorWindowControllerTests.CreateFleetCommandController`
- `Rebellion.Tests.UI.SceneUI.StrategyView.Shared.StrategyFleetCommandControllerTests.CreateController`
- `Rebellion.Tests.UI.SceneUI.StrategyView.Windows.StrategyWindowCommandControllerTests.Constructor_NullMissionCreateController_ThrowsArgumentNullException`
- `Rebellion.Tests.UI.SceneUI.StrategyView.Windows.StrategyWindowCommandControllerTests.SetUp`

## Rebellion.Systems.AISystem

Source: [Assets/Scripts/Systems/AISystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/AISystem.cs#L15).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.AISystem.AISystem(Rebellion.Game.GameRoot, Rebellion.Systems.MissionSystem, Rebellion.Systems.MovementSystem, Rebellion.Systems.ManufacturingSystem, Rebellion.Systems.BombardmentSystem, Rebellion.Systems.PlanetaryAssaultSystem, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Systems.FogOfWarSystem, Rebellion.Systems.MaintenanceSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/AISystem.cs#L33) | Public | Existing AIDirector faction selection/cadence; no facade |
| [`Rebellion.Systems.AISystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/AISystem.cs#L63) | Public | Existing AIDirector faction selection/cadence; no facade |
| [`Rebellion.Systems.AISystem.ProcessTickIncrementally(System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/AISystem.cs#L76) | Internal | Existing AIDirector faction selection/cadence; no facade |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/AISystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/AISystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.AISystemTests.ProcessTickIncrementally_AtConfiguredInterval_YieldsBetweenWorkUnits`
- `Rebellion.Tests.Systems.AISystemTests.ProcessTick_AtConfiguredInterval_ProcessesFaction`
- `Rebellion.Tests.Systems.AISystemTests.ProcessTick_BeforeConfiguredInterval_DoesNotProcessFaction`

## Rebellion.Systems.BlockadeSystem

Source: [Assets/Scripts/Systems/BlockadeSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BlockadeSystem.cs#L16).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.BlockadeSystem.BlockadeSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BlockadeSystem.cs#L27) | Public | BlockadeCommands implementation / local helper |
| [`Rebellion.Systems.BlockadeSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BlockadeSystem.cs#L38) | Public | BlockadeCommands implementation / local helper |
| [`Rebellion.Systems.BlockadeSystem.RollEvacuationLoss()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BlockadeSystem.cs#L56) | Public | BlockadeCommands implementation / local helper |
| [`Rebellion.Systems.BlockadeSystem.ApplyEvacuationLosses(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BlockadeSystem.cs#L69) | Public | BlockadeCommands implementation / local helper |
| [`Rebellion.Systems.BlockadeSystem.DetectBlockadedPlanets()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BlockadeSystem.cs#L102) | Private | BlockadeCommands implementation / local helper |
| [`Rebellion.Systems.BlockadeSystem.ApplyBlockadeStatus(System.Collections.Generic.HashSet<string>, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BlockadeSystem.cs#L121) | Private | BlockadeCommands implementation / local helper |
| [`Rebellion.Systems.BlockadeSystem.ClearBlockadeStatus(System.Collections.Generic.HashSet<string>, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BlockadeSystem.cs#L155) | Private | BlockadeCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/BlockadeSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BlockadeSystem.cs)
- [Assets/Scripts/Systems/MovementSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs)

Direct test references:

- `Rebellion.Tests.Sectors.BlockadeSystemTests.ApplyEvacuationLosses_NeutralPlanetBlockadingFaction_ReturnsNoLoss`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.ApplyEvacuationLosses_OperationalIonCannon_PreventsLoss`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.ProcessTick_AlreadyBlockaded_NoRepeatedEvent`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.ProcessTick_BlockadeEnds_EmitsBlockadeCleared`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.ProcessTick_HostileFleetInTransit_EmitsBlockadeOnlyAfterArrival`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.ProcessTick_MultiplePlanets_HandledIndependently`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.ProcessTick_NeverBlockaded_NoEndEvent`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.ProcessTick_NewBlockade_EmitsBlockadeStarted`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.ProcessTick_NewBlockade_InTransitDefendersSurvive`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.ProcessTick_NewNeutralPlanetBlockade_EmitsBlockadeStarted`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.RollEvacuationLoss_HundredPercent_AlwaysDestroys`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.RollEvacuationLoss_RollAboveThreshold_ReturnsFalse`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.RollEvacuationLoss_RollBelowThreshold_ReturnsTrue`
- `Rebellion.Tests.Sectors.BlockadeSystemTests.RollEvacuationLoss_ZeroPercent_NeverDestroys`
- `Rebellion.Tests.Sectors.MovementSystemTests.ProcessBlockadeStart`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_BlockadeEndsBeforeManufacturedBuildingArrival_CompletesArrival`

## Rebellion.Systems.BombardmentSystem

Source: [Assets/Scripts/Systems/BombardmentSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L18).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.BombardmentSystem.BombardmentSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Systems.MovementSystem, Rebellion.Systems.PlanetaryControlSystem, Rebellion.Systems.PersonnelSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L39) | Public | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.TryExecute(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Results.BombardmentType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L61) | Public | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.Execute(System.Collections.Generic.List<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Results.BombardmentType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L92) | Public | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.CanExecute(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Results.BombardmentType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L209) | Public | BombardmentQueries (read rule / preview) |
| [`Rebellion.Systems.BombardmentSystem.CanBombard(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L229) | Private | BombardmentQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.BombardmentSystem.RecordUnitOutcomes(Rebellion.Game.Results.BombardmentResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L254) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.SetBombardmentCombatState(System.Collections.Generic.List<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L281) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.CalculateBombardmentStrength(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L299) | Private | BombardmentQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.BombardmentSystem.GetBombardmentStrength(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Fleet>, Rebellion.Game.GameConfig.BombardmentConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L310) | Public | BombardmentQueries (read rule / preview) |
| [`Rebellion.Systems.BombardmentSystem.GetProjectedBombardmentStrength(Rebellion.Game.Units.Fleet, Rebellion.Game.GameConfig.BombardmentConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L356) | Internal | BombardmentQueries (read rule / preview) |
| [`Rebellion.Systems.BombardmentSystem.GetProjectedCapitalShipBombardmentStrength(Rebellion.Game.Units.Fleet, Rebellion.Game.Units.CapitalShip, Rebellion.Game.GameConfig.BombardmentConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L376) | Internal | BombardmentQueries (read rule / preview) |
| [`Rebellion.Systems.BombardmentSystem.GetBombardmentShieldStrength(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L394) | Public | BombardmentQueries (read rule / preview) |
| [`Rebellion.Systems.BombardmentSystem.GetBombardmentShieldResistance(int, Rebellion.Game.GameConfig.BombardmentConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L413) | Public | BombardmentQueries (read rule / preview) |
| [`Rebellion.Systems.BombardmentSystem.HasActiveDefenseFacilities(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L429) | Public | BombardmentQueries (read rule / preview) |
| [`Rebellion.Systems.BombardmentSystem.HasActiveMilitaryTargets(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L444) | Public | BombardmentQueries (read rule / preview) |
| [`Rebellion.Systems.BombardmentSystem.GetBombardmentMultiplier(Rebellion.Game.Units.Fleet, Rebellion.Game.GameConfig.BombardmentConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L464) | Private | BombardmentQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.BombardmentSystem.GetProjectedCapitalShipBombardmentStrength(Rebellion.Game.Units.CapitalShip)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L482) | Private | BombardmentQueries (read rule / preview) |
| [`Rebellion.Systems.BombardmentSystem.ResolveBombardmentDefenseFire(System.Collections.Generic.List<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Results.BombardmentResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L517) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.ResolveStrikes(Rebellion.Game.Galaxy.Planet, string, Rebellion.Game.Results.BombardmentType, Rebellion.Game.Results.BombardmentResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L600) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.TryStrikeCivilianTarget(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Results.BombardmentResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L644) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.BuildTargets(Rebellion.Game.Galaxy.Planet, string, Rebellion.Game.Results.BombardmentType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L665) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.BuildMilitaryTargets(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L704) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.BuildCivilianTargets(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L752) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.AddEnergyTargets(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Systems.BombardmentSystem.BombardmentTarget>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L774) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.RollStrike(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L804) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.ApplyStrike(Rebellion.Game.Galaxy.Planet, string, Rebellion.Systems.BombardmentSystem.BombardmentTarget, Rebellion.Game.Results.BombardmentResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L818) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.DestroyHeadquarters(Rebellion.Game.Galaxy.Planet, string, Rebellion.Game.Results.BombardmentResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L866) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.CanBombardHeadquarters(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L880) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.HasPlanetDestroyingShip(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L894) | Private | BombardmentQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.BombardmentSystem.DestroyPlanet(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Results.BombardmentResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L904) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.ApplyCivilianBombardmentPenalty(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L958) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.ApplyDirectBombardmentPenalty(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L985) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.GetCivilianBombardmentSectorPenalty(Rebellion.Game.Galaxy.PlanetSector, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1007) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.ApplyDestroyedSystemPenalty(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1019) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.ReconcileControl(Rebellion.Game.Galaxy.Planet, string, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1061) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.AddOwnershipChanges(Rebellion.Game.Results.BombardmentResult, System.Collections.Generic.IEnumerable<Rebellion.Game.Results.PlanetOwnershipChangedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1082) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.MergeOwnershipChanges(Rebellion.Game.Results.PlanetOwnershipChangedResult, Rebellion.Game.Results.PlanetOwnershipChangedResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1118) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.RollBombardmentPercent(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1147) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.GetAffectedPlanets(Rebellion.Game.Galaxy.PlanetSector)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1157) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.GetActiveCapitalShips(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1170) | Private | BombardmentQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.BombardmentSystem.GetActiveDefenseFacilities(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1183) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.GetActiveDefenderRegiments(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1199) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.GetBombardmentLeadership(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Officer>, Rebellion.Game.Units.OfficerRank, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1216) | Private | BombardmentQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.BombardmentSystem.GetEffectiveShieldStrength(Rebellion.Game.Units.CapitalShip)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1235) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.ScaleByCondition(int, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1251) | Private | BombardmentQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.BombardmentSystem.IsActiveBombardmentUnit(Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1265) | Private | BombardmentQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.BombardmentSystem.IsCommittedBombardmentUnit(Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1276) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.IsActiveBombardmentUnit(Rebellion.Game.Units.CapitalShip)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1288) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.IsActiveBombardmentUnit(Rebellion.Game.Units.Starfighter)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1298) | Private | BombardmentCommands implementation / local helper |
| [`Rebellion.Systems.BombardmentSystem.IsBombardmentDefenseFacility(Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1309) | Private | BombardmentQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.BombardmentSystem.IsCivilianBuilding(Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1319) | Private | BombardmentCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Editor/Simulation/Reporting/FleetSimulationSummaryBuilder.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Editor/Simulation/Reporting/FleetSimulationSummaryBuilder.cs)
- [Assets/Scripts/AI/Director/AIAssessment.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Director/AIAssessment.cs)
- [Assets/Scripts/AI/Proposals/AIFleetAttackProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIFleetAttackProposal.cs)
- [Assets/Scripts/Systems/BombardmentSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Shared/StrategyFleetCommandController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Shared/StrategyFleetCommandController.cs)

Direct test references:

- `Rebellion.Tests.Systems.BombardmentSystemTests.CanExecute_DamagedLowBombardmentShip_ReturnsTrue`
- `Rebellion.Tests.Systems.BombardmentSystemTests.CanExecute_EmbarkedFighterSuppliesBombardmentStrength_ReturnsTrue`
- `Rebellion.Tests.Systems.BombardmentSystemTests.CanExecute_NeutralPlanetWithActiveCapitalShip_ReturnsTrue`
- `Rebellion.Tests.Systems.BombardmentSystemTests.CanExecute_OrdinaryBombardmentWithoutEffectiveStrength_ReturnsFalse`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_AllianceHeadquarters_CanBeDestroyed`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_AttackingFleetWithWaypoints_ClearsRoute`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_CivilianBombardment_AppliesCoreSupportPenalties`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_CivilianBombardment_AppliesOuterRimSupportPenalties`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_CivilianBombardment_SupportFlipCarriesNotificationContext`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DamagedShipAndFighter_UseEffectiveBombardment`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DeathStarShield_DoesNotReduceBombardment`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DefenseFire_DeterminesSurvivingBombardmentStrength`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DestroyPlanetMinorPersonnelSurvivesDeathRoll_RemainsInjured`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DestroyPlanetWithDeathStar_DestroysPlanetAndMinorPersonnel`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DestroyPlanetWithoutDeathStar_DoesNotBombard`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DestroyPlanet_DefenseFireCannotPreventDestruction`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DestroyPlanet_PenalizesOuterRimSupportBelowThreshold`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DestroyedGarrison_CanLeavePlanetNeutral`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DestroyedGarrison_CanTransferPlanetBySupport`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_DestroyedGarrison_SupportShiftCanTransferPlanet`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_EmpireCivilianBombardment_HalvesCoreTargetPenalty`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_EmpireHeadquarters_IsNotAMilitaryTarget`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_GeneralBombardment_CanDamageBothEnergyPools`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_KdyAndLnr_ResolveShieldBeforeHullDamage`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_MilitaryBombardment_TargetsDefendersOnly`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_MilitaryCollateral_CanDestroyCivilianTarget`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_PlanetaryShieldStrength_UsesBombardmentScale`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_RemoteOrMixedFleets_DoNotAttack`
- `Rebellion.Tests.Systems.BombardmentSystemTests.Execute_StrikeResistance_MustBeLowerThanRoll`
- `Rebellion.Tests.Systems.BombardmentSystemTests.TryExecute_ValidCommand_PublishesCompletedResultBatch`

## Rebellion.Systems.BombardmentSystem.BombardmentTarget

Source: [Assets/Scripts/Systems/BombardmentSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs#L1329).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Systems.CaptiveSystem

Source: [Assets/Scripts/Systems/CaptiveSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L20).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.CaptiveSystem.CaptiveSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Systems.MovementSystem, Rebellion.Systems.FogOfWarSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L39) | Public | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.OfficerCaptureStateResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L63) | Public | Typed reaction/settled callback delegates operations to CaptiveCommands |
| [`Rebellion.Systems.CaptiveSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.PlanetOwnershipChangedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L128) | Public | Typed reaction/settled callback delegates operations to CaptiveCommands |
| [`Rebellion.Systems.CaptiveSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L164) | Public | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.ScheduleEscapeAttempt(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L210) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.ResolveCustodyDestination(Rebellion.Game.Results.OfficerCaptureStateResult, Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L226) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.GetResultPlanet(Rebellion.Game.Results.OfficerCaptureStateResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L279) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.GetCapturingUnitCustody(Rebellion.SceneGraph.ISceneNode, string, Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L292) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.GetCustodyEscort(Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L323) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.GetCaptorControlledContainer(Rebellion.SceneGraph.ContainerNode, string, Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L340) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.IsControlledBy(Rebellion.SceneGraph.ISceneNode, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L360) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.GetCustodyContext(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L385) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.RollEscapeAttempt(Rebellion.Game.Units.Officer, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L398) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.TryReleaseOfficer(Rebellion.Game.Units.Officer, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L411) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.ReleaseOfficer(Rebellion.Game.Units.Officer, Rebellion.Game.Galaxy.Planet, int, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L440) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.GetEscapeDestinations(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.Officer, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L469) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.ComputeEscapeDelta(Rebellion.Game.Units.Officer, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L509) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.GetEscapeSkillScore(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L523) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.GetAverageGuardCombat(Rebellion.SceneGraph.ContainerNode, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L535) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.CountGuardRegiments(Rebellion.SceneGraph.ContainerNode, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L560) | Private | CaptiveCommands implementation / local helper |
| [`Rebellion.Systems.CaptiveSystem.GetCustodyUnits<T>(Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs#L575) | Private | CaptiveCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/CaptiveSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_CaptureAtCaptorPlanet_RecordsImmediateCustody`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_CaptureAtUncolonizedCaptorPlanet_UsesFallbackDestination`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_CaptureByOfficerAwayFromCaptorPlanet_MovesWithEscort`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_CaptureByShipAwayFromCaptorPlanet_BoardsCapturingShip`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_CaptureInsideForeignContainerAtCaptorPlanet_MovesToPlanet`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_CaptureWithEstablishedTransfer_PreservesTransfer`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_CaptureWithoutPhysicalCaptor_PlacesAtCustodyDestination`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_CustodyTransferArrives_DoesNotRefreshCaptureSnapshot`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_InactiveCaptureAwayFromCaptorPlanet_PlacesAtCustodyDestination`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_OwnerRecapturesCaptivePlanet_ReleasesOfficer`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_ReleasedOfficer_RemovesCaptureSnapshot`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_CanEscapeFalse_SkipsEscapeAttempt`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_CaptiveAboardFleet_IgnoresPlanetGarrison`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_CaptiveAboardFleet_UsesCaptorFleetGuards`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_CaptiveInTransit_SkipsEscapeAttempt`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_CaptivesWithDifferentSchedules_EvaluatesOnlyDueCaptive`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_EscapeAttemptNotDue_SkipsEscapeRoll`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_EscapeRollFails_ReschedulesEscapeAttempt`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_EscapeRollFails_StaysCaptured`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_EscapeRollSucceeds_FreesOfficer`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_EscapeSucceedsWithFriendlyFleet_MovesOfficerToFleet`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_EscapeSucceedsWithoutFriendlyDestination_RemainsCaptured`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_EscapeSucceeds_EmitsCaptureStateResult`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_EscapeSucceeds_ShiftsLoyalty`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_FriendlyFleetFirstShipUnavailable_MovesOfficerToOperationalShip`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_KilledOfficer_SkipsEscapeAttempt`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_LoyaltyClampsToZero_DoesNotGoNegative`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_NoGarrison_HigherEscapeChance`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_StrongGarrison_LowerEscapeChance`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_UnscheduledCaptiveUsesMaximumRoll_SchedulesMaximumInterval`
- `Rebellion.Tests.Systems.CaptiveSystemTests.ProcessTick_UnscheduledCaptive_SchedulesEscapeAttempt`

## Rebellion.Systems.Combat.PlanetaryAssaultResolver

Source: [Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L15).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.PlanetaryAssaultResolver(Rebellion.Game.GameConfig.PlanetaryAssaultConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L25) | Public | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.Resolve(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L40) | Public | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.HasReadyAttackers(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L110) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.IsBlockedByShields(Rebellion.Game.Galaxy.Planet, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L121) | Public | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.GetLeadershipBonus(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Officer>, Rebellion.Game.Units.OfficerRank, string, Rebellion.Game.GameConfig.PlanetaryAssaultConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L142) | Public | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.EstimateSuccessPercent(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, Rebellion.Game.GameConfig.PlanetaryAssaultConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L168) | Public | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.ResolveDefenseFire(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Systems.Combat.PlanetaryAssaultResolver.AssaultTroop>, System.Collections.Generic.List<Rebellion.Game.Units.Regiment>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L230) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.ResolveGroundCombat(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Systems.Combat.PlanetaryAssaultResolver.AssaultTroop>, System.Collections.Generic.List<Rebellion.Game.Units.Regiment>, System.Collections.Generic.List<Rebellion.Game.Units.Regiment>, System.Collections.Generic.List<Rebellion.Game.Units.Regiment>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L270) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.CalculateContestScore(Rebellion.Systems.Combat.PlanetaryAssaultResolver.AssaultTroop, Rebellion.Game.Units.Regiment, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L320) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.ResolveCollateralDamage(Rebellion.Game.Galaxy.Planet, int, System.Collections.Generic.List<Rebellion.Game.Units.Building>, out int, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L351) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.BuildCollateralTargets(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.IReadOnlyCollection<Rebellion.Game.Units.Building>, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L403) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.SnapshotAttackers(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L437) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.GetActiveDefenders(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L456) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.GetSurvivingAttackers(System.Collections.Generic.IEnumerable<Rebellion.Systems.Combat.PlanetaryAssaultResolver.AssaultTroop>, System.Collections.Generic.IReadOnlyCollection<Rebellion.Game.Units.Regiment>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L472) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.GetSurvivingDefenders(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Regiment>, System.Collections.Generic.IReadOnlyCollection<Rebellion.Game.Units.Regiment>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L488) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.CalculateDefenseFireCasualtyProbabilities(int, System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Building>, Rebellion.Game.GameConfig.PlanetaryAssaultConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L503) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.GetMinimumContestWinProbability(Rebellion.Systems.Combat.PlanetaryAssaultResolver.AssaultTroop, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Regiment>, int, Rebellion.Game.GameConfig.PlanetaryAssaultConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L543) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.CalculateGroundSuccessProbability(System.Collections.Generic.IEnumerable<double>, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L584) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.RollPercent(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L626) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.IsActiveAssaultUnit(Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L636) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.IsActiveAssaultUnit(Rebellion.Game.Units.CapitalShip)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L647) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.PlanetaryAssaultResolver.IsAssaultDefenseFacility(Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L657) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |

Direct production callers (including same-owner helpers):

- [Assets/Editor/Simulation/Reporting/FleetSimulationSummaryBuilder.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Editor/Simulation/Reporting/FleetSimulationSummaryBuilder.cs)
- [Assets/Scripts/AI/Director/AIAssessment.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Director/AIAssessment.cs)
- [Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs)
- [Assets/Scripts/Systems/PlanetaryAssaultSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.Combat.PlanetaryAssaultResolverTests.Resolve_CompletedAssault_DoesNotModifyGameState`

## Rebellion.Systems.Combat.PlanetaryAssaultResolver.AssaultTroop

Source: [Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L662).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Systems.Combat.PlanetaryAssaultResolver.CollateralTarget

Source: [Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/PlanetaryAssaultResolver.cs#L668).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Systems.Combat.SpaceCombatAutoResolver

Source: [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L15).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.SpaceCombatAutoResolver(Rebellion.Game.GameConfig.SpaceCombatConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L25) | Public | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.Resolve(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.CapitalShip>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Starfighter>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.CapitalShip>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Starfighter>, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyCollection<Rebellion.SceneGraph.ISceneNode>>, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyCollection<Rebellion.SceneGraph.ISceneNode>>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L44) | Public | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CompleteEliminatedForces(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L130) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.WithdrawUnitsAtThreshold(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L149) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.HasReachedWithdrawalThreshold(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L165) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CompleteForce(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L178) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.QueueAttacks(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, System.Collections.Generic.IDictionary<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit, Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingDamage>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L191) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.ApplyPendingDamage(System.Collections.Generic.IReadOnlyDictionary<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit, Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingDamage>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L220) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.AdvanceTacticalState(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L282) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.GetTacticalStrength(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L299) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.GetTacticalDurability(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L322) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.ResolveStalemate(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, double, double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L340) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CompleteStalematedForce(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L368) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CreateResult(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L392) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.AddShipResults(Rebellion.Game.Results.SpaceCombatResult, System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L426) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.GetCommittedHullStrength(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L453) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.AddFighterResults(Rebellion.Game.Results.SpaceCombatResult, System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L466) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CaptureCombatUnits(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L492) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.IsDamaged(Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L518) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.DetermineWinner(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce, Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L535) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs)
- [Assets/Scripts/Systems/SpaceCombatSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve`
- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve_CarriedNonHyperdriveFighterWithdraws_PreservesFighter`
- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve_CarrierDestroyedWithSpareRecoveryCapacity_WithdrawsNonHyperdriveFighter`
- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve_CarrierDestroyedWithoutRecoveryCapacity_DestroysNonHyperdriveFighter`
- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve_FleetWithdrawalInterruptedByVictory_DoesNotWithdrawPartialFleet`
- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve_HyperdriveFighterOccupiesRecoveryCarrier_WithdrawsBothFighters`
- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve_OnlyEligibleUnitsCanWithdraw_LeavesOtherUnitsInCombat`
- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve_RecoveryCarrierBayOccupiedByInTransitFighter_DestroysNonHyperdriveFighter`
- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve_ThreeCapitalShipsWithdrawWithOneWithoutHyperdrive_DestroysStrandedShip`
- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve_TwoCarriersDestroyedWithNonHyperdriveFighters_DestroysFightersAfterTheyFight`
- `Rebellion.Tests.Systems.Combat.SpaceCombatAutoResolverTests.Resolve_WithdrawalRequiredWithoutHyperdrive_ContinuesFighting`

## Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingDamage

Source: [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L241).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingDamage.Add(double, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L252) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingHit

Source: [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L261).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingHit.PendingHit(double, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L271) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |

Direct production callers (including same-owner helpers):

None in the invocation index.

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce

Source: [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L553).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce.CombatForce(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.CapitalShip>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Starfighter>, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyCollection<Rebellion.SceneGraph.ISceneNode>>, Rebellion.Game.GameConfig.SpaceCombatConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L575) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce.GetTargetableUnits()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L601) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce.ConfigureWithdrawalGroups(System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyCollection<Rebellion.SceneGraph.ISceneNode>>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L616) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce.HasTargetableUnits<TUnit>(System.Collections.Generic.IReadOnlyList<TUnit>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L648) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce.HasWithdrawnUnit(System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L664) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CombatForce.AssignWithdrawalGroup(System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit>, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L679) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Systems.Combat.SpaceCombatAutoResolver.WithdrawalGroup

Source: [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L693).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.WithdrawalGroup.WithdrawalGroup(System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit>, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L705) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.WithdrawalGroup.BeginWithdrawal()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L714) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.WithdrawalGroup.CompleteWithdrawalWhenReady(double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L728) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.WithdrawalGroup.CompleteWithdrawal()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L744) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.WithdrawalGroup.GetRecoverableUnits()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L763) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.WithdrawalGroup.GetAvailableRecoveryCapacity(Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState, System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L820) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.WithdrawalGroup.IsAssignedToCarrier(Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState, Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L838) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit

Source: [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L853).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.QueueAvailableAttacks(System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit>, bool, double, System.Collections.Generic.IDictionary<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit, Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingDamage>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L888) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.AddPendingDamage(System.Collections.Generic.IDictionary<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit, Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingDamage>, Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit, double, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L902) | Protected | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.GetDistanceTo(Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit, double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L925) | Protected | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.GetEffectiveness(bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L942) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.ApplyDamage(double, double, Rebellion.Game.GameConfig.SpaceCombatConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L951) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.AdvanceTacticalState(Rebellion.Game.GameConfig.SpaceCombatConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L963) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.AdvanceUnitState(Rebellion.Game.GameConfig.SpaceCombatConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L986) | Protected | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.Destroy()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L994) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.BeginWithdrawal()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L999) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.CompleteWithdrawal()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1007) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.TacticalUnit(double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1016) | Protected | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.SetWithdrawalGroup(Rebellion.Systems.Combat.SpaceCombatAutoResolver.WithdrawalGroup)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1025) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.StartWithdrawal()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1033) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.FinishWithdrawal()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1041) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.CancelWithdrawal()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1054) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit.GetManeuverMultiplier(Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1064) | Protected | Existing resolver/calculation type, moved to Simulation; no algorithm change |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState

Source: [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1077).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.CapitalShipState(Rebellion.Game.Units.CapitalShip, Rebellion.Game.GameConfig.SpaceCombatConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1151) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.QueueAvailableAttacks(System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit>, bool, double, System.Collections.Generic.IDictionary<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit, Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingDamage>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1181) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.ScanForArcTargets(System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit>, double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1234) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.GetQueuedArcDamage(int, double, double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1291) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.QueueArcAttacks(int, double, double, System.Collections.Generic.IDictionary<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit, Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingDamage>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1322) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.GetEffectiveness(bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1371) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.ApplyDamage(double, double, Rebellion.Game.GameConfig.SpaceCombatConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1384) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.AdvanceUnitState(Rebellion.Game.GameConfig.SpaceCombatConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1405) | Protected | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.Destroy()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1420) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.ApplyShieldDamage(double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1431) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.ApplyComponentDamage(double, Rebellion.Game.GameConfig.SpaceCombatConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1445) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.GetComponentDelay(Rebellion.Game.GameConfig.SpaceCombatConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1475) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.ClearArc(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1489) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.DischargeArc(int, double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1503) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.QueueArcForRecharge(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1513) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.RechargeShields()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1525) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.RechargeWeapons()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1537) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.GetStrongestArcStrength(bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1562) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.GetArcStrength(int, bool, double, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1587) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.GetRawArcStrength(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1609) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.GetTargetTypeMultiplier(Rebellion.Game.Units.PrimaryWeaponType, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1630) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.GetWeaponStrength(Rebellion.Game.Units.PrimaryWeaponType, int, double, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1647) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.CapitalShipState.GetWeaponValues(Rebellion.Game.Units.CapitalShip, Rebellion.Game.Units.PrimaryWeaponType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1684) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState

Source: [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1693).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.StarfighterState(Rebellion.Game.Units.Starfighter, Rebellion.Game.GameConfig.SpaceCombatConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1728) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.QueueAvailableAttacks(System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit>, bool, double, System.Collections.Generic.IDictionary<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit, Rebellion.Systems.Combat.SpaceCombatAutoResolver.PendingDamage>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1752) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.ScanForWeaponTargets(System.Collections.Generic.IReadOnlyList<Rebellion.Systems.Combat.SpaceCombatAutoResolver.TacticalUnit>, double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1796) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.GetEffectiveness(bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1835) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.ApplyDamage(double, double, Rebellion.Game.GameConfig.SpaceCombatConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1851) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.AdvanceUnitState(Rebellion.Game.GameConfig.SpaceCombatConfig, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1869) | Protected | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.Destroy()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1883) | Internal | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.GetRemainingSquadronStrength()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1892) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.GetCombinedWeaponStrength(bool, double, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1908) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.GetWeaponStrength(int, double, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1929) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |
| [`Rebellion.Systems.Combat.SpaceCombatAutoResolver.StarfighterState.GetRangedWeaponStrength(int, int, double, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs#L1967) | Private | Existing resolver/calculation type, moved to Simulation; no algorithm change |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/Combat/SpaceCombatAutoResolver.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Systems.DuelSystem

Source: [Assets/Scripts/Systems/DuelSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L15).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.DuelSystem.DuelSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L26) | Public | DuelCommands implementation / local helper |
| [`Rebellion.Systems.DuelSystem.HandleRequests(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Requests.DuelRequest>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L41) | Public | DuelCommands operation; executor retains deferred timing and failure containment |
| [`Rebellion.Systems.DuelSystem.CanResolveDuel(Rebellion.Game.Requests.DuelRequest)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L61) | Private | DuelCommands implementation / local helper |
| [`Rebellion.Systems.DuelSystem.ResolveDuel(Rebellion.Game.Requests.DuelRequest, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L84) | Private | DuelCommands implementation / local helper |
| [`Rebellion.Systems.DuelSystem.TryCaptureEncounteredOfficer(Rebellion.Game.Units.Officer, Rebellion.Game.Units.Officer, Rebellion.Game.Galaxy.Planet, int, int, Rebellion.Game.Requests.DuelRequest, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L131) | Private | DuelCommands implementation / local helper |
| [`Rebellion.Systems.DuelSystem.CalculateEncounteredOfficerInjury(bool, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L173) | Private | DuelCommands implementation / local helper |
| [`Rebellion.Systems.DuelSystem.CalculateOpposingOfficerInjury(int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L201) | Private | DuelCommands implementation / local helper |
| [`Rebellion.Systems.DuelSystem.RecordDuelOutcome(Rebellion.Game.Requests.DuelRequest, Rebellion.Game.Galaxy.Planet, bool, int, int, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L218) | Private | DuelCommands implementation / local helper |
| [`Rebellion.Systems.DuelSystem.TryRollInjury(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L251) | Private | DuelCommands implementation / local helper |
| [`Rebellion.Systems.DuelSystem.RollPercent(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L267) | Private | DuelCommands implementation / local helper |
| [`Rebellion.Systems.DuelSystem.ApplyInjury(Rebellion.Game.Units.Officer, int, Rebellion.Game.Units.Officer, Rebellion.Game.Requests.DuelRequest, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L280) | Private | DuelCommands implementation / local helper |
| [`Rebellion.Systems.DuelSystem.Stamp<T>(T, Rebellion.Game.Requests.DuelRequest)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs#L316) | Private | DuelCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/DuelSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/DuelSystem.cs)

Direct test references:

- `Rebellion.Tests.Sectors.DuelSystemTests.HandleResults_FailedAvoidance_CapturesEncounteredOfficer`
- `Rebellion.Tests.Sectors.DuelSystemTests.HandleResults_Injuries_RewardTheOtherOfficersCombat`
- `Rebellion.Tests.Sectors.DuelSystemTests.HandleResults_OfficersOnDifferentPlanets_RejectsDuel`

## Rebellion.Systems.FactionAutomationSystem

Source: [Assets/Scripts/Systems/FactionAutomationSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L14).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.FactionAutomationSystem.FactionAutomationSystem(Rebellion.Game.GameRoot, GameDataCatalog, Rebellion.Systems.ManufacturingSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L26) | Public | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L41) | Public | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.ProcessFaction(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L51) | Public | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.FillGarrisonManufacturingCapacity(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L67) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.TryQueueGarrisonRegiment(Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Galaxy.Planet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L87) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.FillProductionManufacturingCapacity(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L123) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.TryQueueProductionFacility(Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Galaxy.Planet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L145) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.GetOwnedPlanets(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L192) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.GetGarrisonTarget(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L214) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.CountFactionRegiments(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L231) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.HasManufacturingFacilities(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L250) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.FindProducer(System.Collections.Generic.IEnumerable<Rebellion.Game.Galaxy.Planet>, Rebellion.Game.Units.ManufacturingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L268) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.TryFindResourceFacilityOrder(System.Collections.Generic.IEnumerable<Rebellion.Game.Galaxy.Planet>, Rebellion.Game.Units.BuildingType, out Rebellion.Game.Galaxy.Planet, out Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L293) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.HasActiveBuildingProject(System.Collections.Generic.IEnumerable<Rebellion.Game.Galaxy.Planet>, Rebellion.Game.Units.BuildingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L339) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.IsCompatibleBuildingProject(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.BuildingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L360) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.GetAvailableRegiment(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L381) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.GetAvailableBuilding(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.BuildingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L404) | Private | FactionAutomationCommands implementation / local helper |
| [`Rebellion.Systems.FactionAutomationSystem.CountBuildings(System.Collections.Generic.IEnumerable<Rebellion.Game.Galaxy.Planet>, Rebellion.Game.Units.BuildingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs#L424) | Private | FactionAutomationCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/FactionAutomationSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.FactionAutomationSystemTests.ProcessTick_DisabledAutomation_DoesNotQueueWork`
- `Rebellion.Tests.Systems.FactionAutomationSystemTests.ProcessTick_ManageGarrisonsWithReservedTrainingFacility_DoesNotQueueWork`
- `Rebellion.Tests.Systems.FactionAutomationSystemTests.ProcessTick_ManageGarrisons_FillsAvailableCapacityAcrossShortages`
- `Rebellion.Tests.Systems.FactionAutomationSystemTests.ProcessTick_ManageGarrisons_PrioritizesUprising`
- `Rebellion.Tests.Systems.FactionAutomationSystemTests.ProcessTick_ManageGarrisons_QueuesTroopForUnguardedPlanet`
- `Rebellion.Tests.Systems.FactionAutomationSystemTests.ProcessTick_ManageProductionWithReservedBuildingLane_DoesNotQueueWork`
- `Rebellion.Tests.Systems.FactionAutomationSystemTests.ProcessTick_ManageProductionWithoutMineCapacity_DoesNotAddRefinery`
- `Rebellion.Tests.Systems.FactionAutomationSystemTests.ProcessTick_ManageProduction_FillsLaneWithOneProject`
- `Rebellion.Tests.Systems.FactionAutomationSystemTests.ProcessTick_ManageProduction_UsesClosestAvailableResourceSlot`
- `Rebellion.Tests.Systems.FactionAutomationSystemTests.ProcessTick_ReservedDestination_RemainsAvailableForAutomatedDelivery`

## Rebellion.Systems.FleetSystem

Source: [Assets/Scripts/Systems/FleetSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FleetSystem.cs#L15).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.FleetSystem.FleetSystem(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FleetSystem.cs#L23) | Public | FleetCommands implementation / local helper |
| [`Rebellion.Systems.FleetSystem.CreateAtPlanet(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FleetSystem.cs#L34) | Public | FleetCommands implementation / local helper |
| [`Rebellion.Systems.FleetSystem.CreateFromCapitalShips(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.CapitalShip>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FleetSystem.cs#L59) | Public | FleetCommands implementation / local helper |
| [`Rebellion.Systems.FleetSystem.RemoveIfEmpty(Rebellion.Game.Units.Fleet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FleetSystem.cs#L124) | Public | FleetCommands implementation / local helper |
| [`Rebellion.Systems.FleetSystem.ResolveLiveCapitalShips(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.CapitalShip>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FleetSystem.cs#L146) | Private | FleetCommands implementation / local helper |
| [`Rebellion.Systems.FleetSystem.ResolveLivePlanet(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FleetSystem.cs#L168) | Private | FleetCommands implementation / local helper |
| [`Rebellion.Systems.FleetSystem.ResolveLiveFleet(Rebellion.Game.Units.Fleet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FleetSystem.cs#L180) | Private | FleetCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/FleetSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FleetSystem.cs)
- [Assets/Scripts/Systems/MaintenanceSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs)
- [Assets/Scripts/Systems/ManufacturingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs)
- [Assets/Scripts/Systems/MovementSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Shared/StrategyFleetCommandController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Shared/StrategyFleetCommandController.cs)

Direct test references:

- `Rebellion.Tests.Sectors.FleetSystemTests.CreateAtPlanet_InvalidOwner_DoesNotCreateFleet`
- `Rebellion.Tests.Sectors.FleetSystemTests.CreateAtPlanet_SnapshotDestination_CreatesFleetOnLivePlanet`
- `Rebellion.Tests.Sectors.FleetSystemTests.CreateFromCapitalShips_CompleteSourceSelection_RemovesSourceFleet`
- `Rebellion.Tests.Sectors.FleetSystemTests.CreateFromCapitalShips_CompletedShipInTransit_PreservesSourceGraph`
- `Rebellion.Tests.Sectors.FleetSystemTests.CreateFromCapitalShips_PartialSourceSelection_PreservesSourceFleet`
- `Rebellion.Tests.Sectors.FleetSystemTests.CreateFromCapitalShips_ShipUnderConstruction_ChangesDeliveryFleet`
- `Rebellion.Tests.Sectors.FleetSystemTests.CreateFromCapitalShips_SnapshotSelection_UsesLiveShip`
- `Rebellion.Tests.Sectors.FleetSystemTests.CreateFromCapitalShips_UnauthorizedOwner_PreservesSourceGraph`
- `Rebellion.Tests.Sectors.FleetSystemTests.RemoveIfEmpty_EmptyFleet_RemovesFleet`
- `Rebellion.Tests.Sectors.FleetSystemTests.RemoveIfEmpty_PopulatedFleet_PreservesFleet`

## Rebellion.Systems.FogOfWarSystem

Source: [Assets/Scripts/Systems/FogOfWarSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L18).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.FogOfWarSystem.FogOfWarSystem(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L27) | Public | FogOfWarCommands implementation / local helper |
| [`Rebellion.Systems.FogOfWarSystem.CaptureSnapshot(Rebellion.Game.Factions.Faction, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Galaxy.PlanetSector, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L40) | Public | FogOfWarCommands implementation / local helper |
| [`Rebellion.Systems.FogOfWarSystem.RecordObservations(Rebellion.Game.Factions.Faction, System.Collections.Generic.IEnumerable<Rebellion.SceneGraph.ISceneNode>, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L56) | Internal | FogOfWarCommands implementation / local helper |
| [`Rebellion.Systems.FogOfWarSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.IntelligenceRevealedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L70) | Public | Typed reaction/settled callback delegates operations to FogOfWarCommands |
| [`Rebellion.Systems.FogOfWarSystem.CaptureOwnershipChange(System.Collections.Generic.IEnumerable<Rebellion.Game.Factions.Faction>, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Galaxy.PlanetSector, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L90) | Internal | FogOfWarCommands implementation / local helper |
| [`Rebellion.Systems.FogOfWarSystem.RemoveEntityFromSnapshots(Rebellion.Game.Factions.Faction, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L106) | Public | FogOfWarCommands implementation / local helper |
| [`Rebellion.Systems.FogOfWarSystem.ProcessResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.GameObjectSabotagedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L115) | Public | Typed reaction/settled callback delegates operations to FogOfWarCommands |
| [`Rebellion.Systems.FogOfWarSystem.IsPlanetVisible(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L127) | Public | FogOfWarQueries (read rule / preview) |
| [`Rebellion.Systems.FogOfWarSystem.BuildFactionView(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L153) | Public | FogOfWarQueries (read rule / preview) |
| [`Rebellion.Systems.FogOfWarSystem.AddObservedMissions(Rebellion.Game.Galaxy.Planet, Rebellion.Game.FogOfWar.PlanetSnapshot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L228) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.AttachDetachedChildrenToView(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L245) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.IsOwnedBy(Rebellion.SceneGraph.ISceneNode, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L260) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.RemoveSabotagedObjectFromActorSnapshot(Rebellion.Game.Results.GameObjectSabotagedResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L269) | Private | FogOfWarCommands implementation / local helper |
| [`Rebellion.Systems.FogOfWarSystem.MergeOwnLiveUnits(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L289) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.MergeMissingByInstanceID<T>(System.Collections.Generic.IEnumerable<T>, System.Collections.Generic.IEnumerable<T>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L344) | Private | FogOfWarCommands implementation / local helper |
| [`Rebellion.Systems.FogOfWarSystem.ViewUnit<T>(T, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L363) | Private | FogOfWarCommands implementation / local helper |
| [`Rebellion.Systems.FogOfWarSystem.IsVisibleWithoutManufacturingIntelligence(Rebellion.SceneGraph.ISceneNode, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L375) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.BlankPlanetView(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L396) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.ApplyRealTimeView(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, Rebellion.Game.FogOfWar.PlanetSnapshot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L423) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.ApplySnapshotView(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Galaxy.PlanetSector, Rebellion.Game.FogOfWar.PlanetSnapshot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L501) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.HasIntelligence(Rebellion.Game.FogOfWar.PlanetSnapshot, Rebellion.Game.FogOfWar.PlanetIntelligenceCategory)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L544) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.ApplyIncomingFleetIntelligence(Rebellion.Game.Galaxy.Planet, Rebellion.Game.FogOfWar.PlanetSnapshot, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L558) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.CopyLiveManufacturingQueue(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L595) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.ApplyManufacturingIntelligence(Rebellion.Game.Galaxy.Planet, Rebellion.Game.FogOfWar.PlanetSnapshot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L611) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.ApplyManufacturingQueue(Rebellion.Game.Galaxy.Planet, Rebellion.Game.FogOfWar.PlanetSnapshot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L631) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.FogOfWarSystem.UnexploredPlanetView(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs#L651) | Private | FogOfWarQueries helper candidate; verify shared callers/purity |

Direct production callers (including same-owner helpers):

- [Assets/Editor/Simulation/Reporting/SimulationSummaryBuilder.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Editor/Simulation/Reporting/SimulationSummaryBuilder.cs)
- [Assets/Editor/Simulation/Tracking/AttackReadinessTracker.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Editor/Simulation/Tracking/AttackReadinessTracker.cs)
- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/AISystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/AISystem.cs)
- [Assets/Scripts/Systems/CaptiveSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs)
- [Assets/Scripts/Systems/FogOfWarSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FogOfWarSystem.cs)
- [Assets/Scripts/Systems/MovementSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs)
- [Assets/Scripts/Systems/PlanetaryControlSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/GalaxyMap/GalaxyMapController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/GalaxyMap/GalaxyMapController.cs)

Direct test references:

- `Rebellion.Tests.AI.Helpers.AITestSceneBuilder.CreateContext`
- `Rebellion.Tests.Game.Missions.ReconnaissanceMissionTests.ResolveObjective_UnvisitedPlanet_CapturesSnapshotWithoutSuccessRoll`
- `Rebellion.Tests.Generation.GameBuilderTests.Build_FogOfWar_OuterRimEnemyPlanetNotVisible`
- `Rebellion.Tests.Generation.GameBuilderTests.Build_FogOfWar_OuterRimOwnerCanSeeOwnPlanet`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_SabotageResult_RemovesDestroyedObjectFromActorSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_BlockadedOwnPlanet_EnemyFleetInTransit_NotVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_BlockadedOwnPlanet_ShowsOnlyPresentCompletedEnemyShips`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_CapturedFriendlyOfficerOnSnapshotPlanet_DoesNotRevealOfficer`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_CapturedFriendlyOfficerOnUnexploredPlanet_DoesNotRevealOfficer`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_CapturedFriendlyOfficerOnVisiblePlanet_ReturnsOfficer`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_EntitiesOnMultiplePlanets_PreservesInstanceIDs`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_FleetAtEnemyPlanet_EnemyMissionsStillHidden`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_FleetAtEnemyPlanet_EnemyOfficerVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_FleetAtEnemyPlanet_ManufacturingRemainsHidden`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_FleetLeaves_UsesSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_FleetMoves_FleetNotDuplicated`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_LiveEnemyFleet_InTransitManifestNotVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_LiveEnemyPlanet_EnemyUnitsInTransit_NotVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_LivePlanet_BuildingsPreserved`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_LivePlanet_EspionageMissionIntelligenceRemainsVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_LivePlanet_EspionageSnapshotEnemyMission_IsSurfaced`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_LivePlanet_ModifyingViewDoesNotAffectGame`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_LivePlanet_OrbingEnemyFleet_NotDuplicatedFromSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_LivePlanet_RemovesAbsentEnemyUnitsFromSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_LivePlanet_StaleOwnSnapshotUnits_NotVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_LivePlanet_StaleSnapshotFriendlyFleet_NotShown`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_NoSnapshotsAnywhere_AllPlanetsEmptySnapshots`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_NotVisibleWithSnapshot_UsesSnapshotData`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OuterRimSnapshot_PreservesObservedPopularSupport`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OuterRimSnapshot_PreservesObservedUprisingState`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OwnFleetArrived_DestinationUsesLive`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OwnFleetInTransitToUnexploredPlanet_ShowsFleetWithoutLivePlanetData`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OwnFleetInTransit_DestinationUsesSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OwnFleetInTransit_IsVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OwnFleet_AtEnemyPlanet_PlanetLiveWithoutSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OwnMission_OnEnemyPlanet_VisibleWithoutSnapshotOrFleet`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OwnMission_OnNeutralPlanet_VisibleWithoutSnapshotOrFleet`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OwnPlanet_EnemyMissionsNotVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OwnPlanet_ManufacturingQueueVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_OwnPlanet_OwnMissionsVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_PlanetCapturedFromEnemy_UsesOnlyLiveUnits`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_PlanetWithNoEntities_HandledCorrectly`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_PlanetsWithSharedEntities_NoDuplicateEntitiesAcrossPlanets`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_SectorWithMultiplePlanets_MixedVisibilityHandledCorrectly`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_SnapshotBuildings_Visible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_SnapshotPlanet_BuildingQueuedAfterSnapshot_NotVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_SnapshotPlanet_EntityAddedAfterSnapshot_NotVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_SnapshotPlanet_ReturnsVisitedViewPlanet`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_SnapshotQueuedBuildingOnFullLivePlanet_SkipsGhostBuilding`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_Snapshot_ModifyingViewDoesNotAffectSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_Snapshot_PreservesObservedResources`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_UnexploredOuterRimAndCore_BothHiddenWithoutSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_UnexploredOwnedPlanet_HidesStatus`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_UnexploredPlanet_EmptySnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_UnownedCorePlanet_UsesCurrentPopularSupport`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_UnownedCorePlanet_UsesCurrentUprisingState`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_VaderMovesWithoutObservation_StaleIntelPersists`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_VisiblePlanet_UsesLiveData`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_AfterEspionage_PreservesFleetContainingOnlyManufacturingShip`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_AfterEspionage_PreservesIncomingEnemyFleet`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_AfterEspionage_PreservesMissionIntelligence`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_AfterEspionage_PreservesStaleManufacturingIntel`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_AfterEspionage_RemovesAbsentCargoFromPreservedShip`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_AfterEspionage_RemovesAbsentFleetContainingOnlyManufacturingShip`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_AfterEspionage_RemovesAbsentManufacturingIntel`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_CapturedFriendlyOfficer_IncludesDetachedOfficer`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_DeepCopy_ModifyingGameDoesNotAffectSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_EmptyFleet_ExcludedFromSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_EmptyPlanet_CreatesPlanetSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_EnemyUnitsInTransit_NotRecorded`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_EntityMovesBackToOriginalPlanet_HandledCorrectly`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_EntityMoves_RemovedFromOldPlanetSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_EntityOnPlanet_UpdatesLastSeenIndex`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_EntitySeenTwiceSamePlanet_DoesNotDuplicate`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_FleetWithShips_IncludedInSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_Invalidation_RemovesOnlyTargetEntity`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_MultipleEntitiesMove_InvalidationIndependentPerEntity`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_NestedEntityObservedElsewhere_RemovesOldFleetManifestEntry`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_OrdinaryObservation_ManufacturingRemainsHidden`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_ParticipantSeenElsewhere_PreservesRecordedMissionIdentity`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_PlanetInPlanetSector_MapsPlanetToSector`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_PlanetVisible_SnapshotNotOverwrittenWithoutExplicitCall`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_PlanetWithAllEntities_CreatesAccurateSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_SingleEntity_CopiesEntityWithSameInstanceID`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_UnvisitedPlanet_MarksPlanetVisited`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.CaptureSnapshot_VaderRediscovered_RemovesFromOldPlanet`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.HandleResults_SelectedCapitalShip_RevealsPartialFleetWithoutSiblingsOrCargo`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.HandleResults_SelectedManufacturingOrder_RevealsOnlySelectedOrder`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.HandleResults_SelectedNestedOfficer_RevealsAncestryWithoutSiblings`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.HandleResults_SelectedObservation_RevealsOnlySelectedObject`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.IsPlanetVisible_CapturedFriendlyOfficerPresent_ReturnsFalse`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.IsPlanetVisible_FleetPresent_ReturnsTrue`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.IsPlanetVisible_MultipleFleetsDifferentFactions_OnlyOwnFactionCounts`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.IsPlanetVisible_NoOwnershipNoFleet_ReturnsFalse`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.IsPlanetVisible_OwnCapitalShipInTransit_DoesNotGrantVisibility`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.IsPlanetVisible_OwnFleetInTransit_DoesNotGrantVisibility`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.IsPlanetVisible_OwnFleetWithoutShips_ReturnsFalse`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.IsPlanetVisible_OwnedPlanet_ReturnsTrue`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.ProcessResults_SabotagedObject_RemovesObjectFromActorSnapshot`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.RecordEspionageSnapshot_EnemyManufacturing_RevealsManufacturing`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.RecordEspionageSnapshot_EnemyMissions_RevealsMissions`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.RecordEspionageSnapshot_IncomingEnemyFleet_RevealsFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.CapturePlanetSnapshot`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.CapturePlanetSnapshot`

## Rebellion.Systems.GameEventSystem

Source: [Assets/Scripts/Systems/GameEventSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L16).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.GameEventSystem.GameEventSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Game.Units.UnitFactory, Rebellion.Systems.GameRequestDispatcher)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L30) | Public | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.ValidateEvents(System.Collections.Generic.IReadOnlyCollection<Rebellion.Game.Events.GameEvent>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L49) | Public | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.HaveSameBindings(System.Collections.Generic.IReadOnlyDictionary<string, System.Type>, System.Collections.Generic.IReadOnlyDictionary<string, System.Type>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L182) | Private | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.ValidateSchedule(Rebellion.Game.Events.GameEvent)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L195) | Private | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.ProcessEvents(System.Collections.Generic.List<Rebellion.Game.Events.GameEvent>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L276) | Public | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L303) | Public | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.BuildTriggerIndex()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L351) | Private | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.TryActivateEvent(Rebellion.Game.Events.GameEvent, Rebellion.Game.Events.GameEventTrigger, Rebellion.Game.Results.GameResult, out System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L385) | Private | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.ShouldCompleteSchedule(Rebellion.Game.Events.GameEvent, Rebellion.Game.Events.GameEventState, Rebellion.Game.Events.GameEventEvaluationContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L467) | Private | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.HasReachedMaximumActivations(Rebellion.Game.Events.GameEvent, Rebellion.Game.Events.GameEventState)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L490) | Private | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.HasResultTrigger(Rebellion.Game.Events.GameEvent)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L503) | Private | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.InitializeSchedule(Rebellion.Game.Events.GameEvent, Rebellion.Game.Events.GameEventState)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L511) | Private | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.GetInitialRange(Rebellion.Game.Events.GameEvent, out int, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L568) | Private | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.GetRepeatRange(Rebellion.Game.Events.GameEvent, out int, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L584) | Private | GameEventExecutor activation/scheduling |
| [`Rebellion.Systems.GameEventSystem.RollRange(int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs#L600) | Private | GameEventExecutor activation/scheduling |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/GameEventSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs)

Direct test references:

- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_MatchingEncounter_ActivatesResultTriggeredEventOnce`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_MatchingOptionalSourceBinding_ActivatesEvent`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_RepeatableEncounterEffect_ActivatesForEveryEncounter`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_SecondUnitArrivedAlternativeMatches_ActivatesOnce`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_StableTriggerId_ActivatesWithoutClrTypeName`
- `Rebellion.Tests.Sectors.GameEventSystemTests.HandleResults_WithoutSuppression_PreservesTriggerAndSiblingMessages`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_AfterAllScheduleAtFinalDelay_ActivatesEvent`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_AfterAllScheduleBeforeFinalDelay_KeepsEventPending`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_AfterAnyScheduleAtFirstDelay_ActivatesEvent`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_AfterAnyScheduleBeforeFirstDelay_KeepsEventPending`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_AfterSchedule_DelaysFromPredecessorActivation`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_EachOwnedPlanetTarget_ArmsWhenNeutralPlanetBecomesOwned`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_EachOwnedPlanetTarget_RearmsAfterNeutralInterval`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_MaximumActivationsFive_ActivatesFiveTimes`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_MaximumActivationsThree_ActivatesThreeTimes`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_MetOneShotEvent_CompletesAndLeavesPool`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_MetRepeatableEvent_CompletesAndRemainsActive`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_OneShotTarget_ActivatesTargetOnce`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_RandomDelay_WaitsUntilRolledAbsoluteTick`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_RandomTargetBeforeScheduledTick_DoesNotSelectTarget`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_RecurringScheduleUntilMet_CompletesAndRemovesEvent`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_RecurringScheduleUntilMet_UsesEvaluationBinding`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_RepeatDelay_PreventsActivationUntilCooldownExpires`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_ResultTriggeredEvent_DoesNotRunDuringScheduledPolling`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_TargetedPlanet_UsesOnePersistedSchedule`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ProcessEvents_UnmetOneShotEvent_RemainsPending`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ValidateEvents_BindingWithoutAlias_ThrowsInvalidOperationException`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ValidateEvents_BindingWithoutSource_ThrowsInvalidOperationException`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ValidateEvents_DependencyCompletedAndRemovedFromPool_DoesNotThrow`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ValidateEvents_DependencyMissingFromPoolAndNotCompleted_ThrowsInvalidOperationException`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ValidateEvents_DuplicateBindingAlias_ThrowsInvalidOperationException`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ValidateEvents_MultipleFilteredTriggersWithSameAlias_DoesNotThrow`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ValidateEvents_MultipleScheduleModes_ThrowsInvalidOperationException`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ValidateEvents_MultipleTriggersWithDifferentAliases_ThrowsInvalidOperationException`
- `Rebellion.Tests.Sectors.GameEventSystemTests.ValidateEvents_OneShotScheduleWithoutMaximumActivations_DoesNotThrow`

## Rebellion.Systems.IGameRequestHandler<T>

Source: [Assets/Scripts/Systems/GameRequestDispatcher.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameRequestDispatcher.cs#L14).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.IGameRequestHandler<T>.HandleRequests(System.Collections.Generic.IReadOnlyList<T>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameRequestDispatcher.cs#L22) | Public | GameEventExecutor deferred execution; remove dispatcher contract |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/GameRequestDispatcher.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameRequestDispatcher.cs)

Direct test references:

- `Rebellion.Tests.Sectors.MovementSystemTests.HandleMovementRequest_EventOriginatedRequestAlreadyAtDestination_EmitsArrival`
- `Rebellion.Tests.Sectors.MovementSystemTests.HandleMovementRequest_EventOriginatedRequest_PropagatesSourceToArrival`
- `Rebellion.Tests.Sectors.MovementSystemTests.HandleMovementRequest_FirstCandidateRejectsGroup_UsesNextCandidate`
- `Rebellion.Tests.Sectors.MovementSystemTests.HandleMovementRequest_ValidRequest_RoutesThroughAuthoritativeMovePath`
- `Rebellion.Tests.Sectors.MovementSystemTests.HandlePlacementRequest_GroupExceedsCapacity_LeavesEveryUnitUnchanged`
- `Rebellion.Tests.Sectors.MovementSystemTests.HandlePlacementRequest_NewDetachedUnit_AttachesAndRegistersUnit`

## Rebellion.Systems.GameRequestDispatcher

Source: [Assets/Scripts/Systems/GameRequestDispatcher.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameRequestDispatcher.cs#L28).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.GameRequestDispatcher.Subscribe<T>(Rebellion.Systems.IGameRequestHandler<T>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameRequestDispatcher.cs#L38) | Public | GameEventExecutor deferred execution; remove dispatcher contract |
| [`Rebellion.Systems.GameRequestDispatcher.Process(System.Collections.Generic.IEnumerable<Rebellion.Game.Requests.GameRequest>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameRequestDispatcher.cs#L61) | Public | GameEventExecutor deferred execution; remove dispatcher contract |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/GameEventSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameEventSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.GameRequestDispatcherTests.Process_HandlerThrows_ProcessesRemainingRequests`
- `Rebellion.Tests.Systems.GameRequestDispatcherTests.Process_RegisteredRequest_ReturnsFactsWithSourceEvent`
- `Rebellion.Tests.Systems.GameRequestDispatcherTests.Process_UnregisteredRequest_ReturnsNoResults`

## Rebellion.Systems.GameResultProcessor

Source: [Assets/Scripts/Systems/GameResultProcessor.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameResultProcessor.cs#L11).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.GameResultProcessor.Subscribe<T>(Rebellion.Systems.IGameResultHandler<T>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameResultProcessor.cs#L23) | Public | GameResultBus; preserve batch/exception semantics |
| [`Rebellion.Systems.GameResultProcessor.Observe<T>(System.Action<System.Collections.Generic.IReadOnlyList<T>>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameResultProcessor.cs#L41) | Public | GameResultBus; preserve batch/exception semantics |
| [`Rebellion.Systems.GameResultProcessor.Process(System.Collections.Generic.IEnumerable<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameResultProcessor.cs#L60) | Public | GameResultBus; preserve batch/exception semantics |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)

Direct test references:

- `Rebellion.Tests.Sectors.MovementSystemTests.BuildBlockadeRetargetingScene`
- `Rebellion.Tests.Sectors.MovementSystemTests.ProcessBlockadeStart`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_BlockadeEndsBeforeManufacturedBuildingArrival_CompletesArrival`
- `Rebellion.Tests.Systems.GameResultProcessorTests.Process_MatchingResults_InvokesOnlyMatchingHandlersInRegistrationOrder`
- `Rebellion.Tests.Systems.GameResultProcessorTests.Process_Observers_ReceiveMatchingResultsAfterAllReactionWaves`
- `Rebellion.Tests.Systems.GameResultProcessorTests.Process_ReactionResults_ProcessesBreadthFirstWavesInRegistrationOrder`

## Rebellion.Systems.HeadquartersSystem

Source: [Assets/Scripts/Systems/HeadquartersSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/HeadquartersSystem.cs#L16).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.HeadquartersSystem.HeadquartersSystem(Rebellion.Game.GameRoot, Rebellion.Systems.MovementSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/HeadquartersSystem.cs#L28) | Public | HeadquartersCommands implementation / local helper |
| [`Rebellion.Systems.HeadquartersSystem.CanMove(Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/HeadquartersSystem.cs#L40) | Private | HeadquartersQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.HeadquartersSystem.CanRelocate(Rebellion.Game.Units.Building, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/HeadquartersSystem.cs#L58) | Public | HeadquartersQueries (read rule / preview) |
| [`Rebellion.Systems.HeadquartersSystem.TryRelocate(Rebellion.Game.Units.Building, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/HeadquartersSystem.cs#L80) | Public | HeadquartersCommands implementation / local helper |
| [`Rebellion.Systems.HeadquartersSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.UnitArrivedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/HeadquartersSystem.cs#L105) | Public | Typed reaction/settled callback delegates operations to HeadquartersCommands |
| [`Rebellion.Systems.HeadquartersSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.PlanetOwnershipChangedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/HeadquartersSystem.cs#L133) | Public | Typed reaction/settled callback delegates operations to HeadquartersCommands |
| [`Rebellion.Systems.HeadquartersSystem.UpdateFixedHeadquartersMarker(Rebellion.Game.Results.PlanetOwnershipChangedResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/HeadquartersSystem.cs#L190) | Private | HeadquartersCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/HeadquartersSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/HeadquartersSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Windows/StrategyWindowCommandController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Windows/StrategyWindowCommandController.cs)

Direct test references:

- `Rebellion.Tests.Systems.HeadquartersSystemTests.HandleResults_FixedHeadquartersCaptured_ClearsMarkerAndPreservesLocation`
- `Rebellion.Tests.Systems.HeadquartersSystemTests.HandleResults_FixedHeadquartersRecaptured_RestoresMarker`
- `Rebellion.Tests.Systems.HeadquartersSystemTests.HandleResults_HeadquartersArrival_AssignsDestination`
- `Rebellion.Tests.Systems.HeadquartersSystemTests.HandleResults_HostilePlanetCapture_DestroysMobileHeadquarters`
- `Rebellion.Tests.Systems.HeadquartersSystemTests.TryRelocate_FixedHeadquarters_IsRejected`
- `Rebellion.Tests.Systems.HeadquartersSystemTests.TryRelocate_MobileHeadquarters_DepartsAndClearsPlanetMarker`

## Rebellion.Systems.IGameResultHandler<T>

Source: [Assets/Scripts/Systems/IGameResultHandler.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/IGameResultHandler.cs#L10).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.IGameResultHandler<T>.HandleResults(System.Collections.Generic.IReadOnlyList<T>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/IGameResultHandler.cs#L18) | Public | Replaced by typed callback subscription |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/GameResultProcessor.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/GameResultProcessor.cs)

Direct test references:

None in the invocation index; indirect execution may still cover this type.

## Rebellion.Systems.JediSystem

Source: [Assets/Scripts/Systems/JediSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L16).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.JediSystem.JediSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L26) | Public | JediCommands implementation / local helper |
| [`Rebellion.Systems.JediSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L36) | Public | JediCommands implementation / local helper |
| [`Rebellion.Systems.JediSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.MissionCompletedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L55) | Public | Typed reaction/settled callback delegates operations to JediCommands |
| [`Rebellion.Systems.JediSystem.ApplyForceGrowth(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IMissionParticipant>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L79) | Public | JediCommands implementation / local helper |
| [`Rebellion.Systems.JediSystem.UpdateForceDiscoveryState(Rebellion.Game.Units.Officer, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L124) | Private | JediCommands implementation / local helper |
| [`Rebellion.Systems.JediSystem.ScanForHiddenForceUsers(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L164) | Private | JediCommands implementation / local helper |
| [`Rebellion.Systems.JediSystem.GetActiveForceScanners()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L178) | Private | JediCommands implementation / local helper |
| [`Rebellion.Systems.JediSystem.ScanScannerLocation(Rebellion.Game.Units.Officer, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L191) | Private | JediCommands implementation / local helper |
| [`Rebellion.Systems.JediSystem.CanDiscoverForceUser(Rebellion.Game.Units.Officer, Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L210) | Private | JediCommands implementation / local helper |
| [`Rebellion.Systems.JediSystem.DiscoverForceUser(Rebellion.Game.Units.Officer, Rebellion.Game.Units.Officer, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs#L237) | Private | JediCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/JediSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/JediSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.JediSystemTests.ApplyForceGrowth_EligibleOfficer_GrowsForce`
- `Rebellion.Tests.Systems.JediSystemTests.ApplyForceGrowth_NotForceEligible_NoGrowth`
- `Rebellion.Tests.Systems.JediSystemTests.HandleResults_SuccessfulMission_AppliesForceGrowth`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_AlreadyDiscovering_NoRepeatedEvent`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_AlreadyEligibleCandidate_Skipped`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_BelowThresholdJedi_NoScan`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_CapturedCandidate_Skipped`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_CapturedOfficer_NoDiscoveringState`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_DiscoveringJediWithDormantCandidate_DiscoversDormant`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_DormantOfficerWithStoryGrowth_PreservesHigherForceValue`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_EmptyGame_NoEvents`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_EnemyDormantCandidate_DoesNotDiscover`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_ForceIneligibleJedi_ClearsDiscoveringState`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_ForceRankAboveThreshold_EntersDiscoveringState`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_ForceRankBelowThreshold_NoDiscovery`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_ForceRankDropsBelowThreshold_ClearsDiscoveringState`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_ForceRankExactlyAtThreshold_EntersDiscoveringState`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_HighRoll_DiscoveryFails`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_MultipleOfficers_AllProcessed`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_NonJediOfficer_ClearsDiscoveringState`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_NonPositiveDiscoveryChance_DoesNotDiscover`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_NonTrainerJedi_ClearsDiscoveringState`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_OfficerWithTemplate_InitializesForceValue`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_OfficerWithTrainingAdjustment_IncludesAdjustmentInRank`
- `Rebellion.Tests.Systems.JediSystemTests.ProcessTick_OnMissionCandidate_Skipped`

## Rebellion.Systems.MaintenanceSystem

Source: [Assets/Scripts/Systems/MaintenanceSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L20).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.MaintenanceSystem.MaintenanceSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Systems.FleetSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L40) | Public | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L57) | Public | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.GetMaintenanceRequired(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L72) | Public | MaintenanceQueries (read rule / preview) |
| [`Rebellion.Systems.MaintenanceSystem.TryScrap(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IManufacturable>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L83) | Public | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.ProcessFactionMaintenance(Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L133) | Private | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.IsInMaintenanceShortfall(int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L158) | Private | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.ClearMaintenanceShortfall(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L167) | Private | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.RecordMaintenanceShortfall(Rebellion.Game.Factions.Faction, int, int, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L180) | Private | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.TryConsumeAutoScrapPulse(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L205) | Private | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.ScrapRandomEligibleUnit(Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L226) | Private | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.IsAutoScrapEligibleType(Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L263) | Private | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.Scrap(Rebellion.Game.Units.IManufacturable, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L278) | Private | MaintenanceCommands implementation / local helper |
| [`Rebellion.Systems.MaintenanceSystem.RefundScrapMaterials(Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs#L314) | Private | MaintenanceCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/AI/Proposals/AIFacilityRemovalProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIFacilityRemovalProposal.cs)
- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/MaintenanceSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MaintenanceSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Windows/StrategyWindowCommandController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Windows/StrategyWindowCommandController.cs)

Direct test references:

- `Rebellion.Tests.Managers.GameManagerTests.ScrapCommand_LastSurfaceRegiment_ReconcilesPlanetImmediately`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.ProcessTick_ExcessBuildingsOverCapacity_ScrapsBuildings`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.ProcessTick_NoShortfall_DoesNotScrap`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.ProcessTick_Shortfall_AfterAutoscrapInterval_ScrapsOneUnit`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.ProcessTick_Shortfall_BeforeAutoscrapInterval_DoesNotScrapAgain`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.ProcessTick_Shortfall_ContinuesScrappingWhileOverCapacity`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.ProcessTick_UnitInTransit_RemainsEligibleForAutoscrap`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.ProcessTick_UnitUnderConstruction_DoesNotScrap`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.ProcessTick_UnitUnderConstruction_ReservesMaintenance`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.ProcessTick_ZeroMaintenanceInfrastructurePresent_ScrapsPositiveMaintenanceUnitFirst`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.TryScrap_OtherFactionUnit_PreservesUnit`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.TryScrap_OwnedBuilding_ReportsScrappedObjectAndContext`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.TryScrap_OwnedSurfaceRegiment_RefundsRemovesAndReportsGarrisonChange`
- `Rebellion.Tests.Sectors.MaintenanceSystemTests.TryScrap_UnitUnderConstruction_PreservesUnitAndMaterials`

## Rebellion.Systems.ManufacturingSystem

Source: [Assets/Scripts/Systems/ManufacturingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L17).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.ManufacturingSystem.ManufacturingSystem(Rebellion.Game.GameRoot, Rebellion.Systems.FleetSystem, Rebellion.Systems.MovementSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L36) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L51) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.GameObjectDestroyedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L68) | Public | Typed reaction/settled callback delegates operations to ManufacturingCommands |
| [`Rebellion.Systems.ManufacturingSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.GameObjectScrappedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L95) | Public | Typed reaction/settled callback delegates operations to ManufacturingCommands |
| [`Rebellion.Systems.ManufacturingSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.BombardmentResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L120) | Public | Typed reaction/settled callback delegates operations to ManufacturingCommands |
| [`Rebellion.Systems.ManufacturingSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.PlanetaryAssaultResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L136) | Public | Typed reaction/settled callback delegates operations to ManufacturingCommands |
| [`Rebellion.Systems.ManufacturingSystem.StartManufacturing(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable, Rebellion.SceneGraph.ISceneNode, int, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L159) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.GetProductionFleet(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L241) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.RetargetManufacturingDestination(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.ManufacturingType, Rebellion.SceneGraph.ContainerNode, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L264) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.CanStartManufacturing(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable, Rebellion.SceneGraph.ISceneNode, int, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L311) | Public | ManufacturingQueries (read rule / preview) |
| [`Rebellion.Systems.ManufacturingSystem.GetMaintenanceRefund(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L345) | Private | ManufacturingQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.ManufacturingSystem.CancelConflictingProject(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L370) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.CanAcceptManufacturingOrder(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable, Rebellion.SceneGraph.ISceneNode, int, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L398) | Public | ManufacturingQueries (read rule / preview) |
| [`Rebellion.Systems.ManufacturingSystem.HasDestinationCapacity(Rebellion.Game.Galaxy.Planet, Rebellion.SceneGraph.ISceneNode, Rebellion.Game.Units.IManufacturable, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L429) | Private | ManufacturingQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.ManufacturingSystem.HasFleetCapacity(Rebellion.Game.Units.Fleet, Rebellion.Game.Units.IManufacturable, int, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L480) | Private | ManufacturingQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.ManufacturingSystem.HasCapitalShipCapacity(Rebellion.Game.Units.CapitalShip, Rebellion.Game.Units.IManufacturable, int, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L518) | Private | ManufacturingQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.ManufacturingSystem.EstimateManufacturingTicks(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L547) | Public | ManufacturingQueries (read rule / preview) |
| [`Rebellion.Systems.ManufacturingSystem.EstimateQueueCompletionTicks(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.ManufacturingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L570) | Public | ManufacturingQueries (read rule / preview) |
| [`Rebellion.Systems.ManufacturingSystem.EstimateCompletionTicks(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L595) | Public | ManufacturingQueries (read rule / preview) |
| [`Rebellion.Systems.ManufacturingSystem.EstimateManufacturingTicks(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.ManufacturingType, long)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L643) | Private | ManufacturingQueries (read rule / preview) |
| [`Rebellion.Systems.ManufacturingSystem.GetProductionProgressAtTick(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Building>, long, long)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L698) | Private | ManufacturingQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.ManufacturingSystem.GetNextProductionTick(Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L724) | Private | ManufacturingQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.ManufacturingSystem.Enqueue(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable, Rebellion.Game.Galaxy.Planet, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L744) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.Enqueue(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable, Rebellion.Game.Units.Fleet, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L793) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.Enqueue(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable, Rebellion.Game.Units.CapitalShip, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L860) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.IsManufacturingCarrierAvailable(Rebellion.Game.Units.CapitalShip)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L910) | Private | ManufacturingQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.ManufacturingSystem.IsLiveDestination(Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L921) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.GetValidatedFaction(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L941) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.HasMaintenanceHeadroom(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.IManufacturable, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L964) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.HasMaintenanceHeadroom(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.IManufacturable, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L982) | Private | ManufacturingQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.ManufacturingSystem.CommitToQueue(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1008) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.ProcessPlanetManufacturing(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1032) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.GetActiveManufacturingTypes(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.Dictionary<Rebellion.Game.Units.ManufacturingType, System.Collections.Generic.List<Rebellion.Game.Units.IManufacturable>>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1094) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.IsQueuedItemActive(Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1124) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.RemoveInvalidPlanetDestinationItems(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Units.IManufacturable>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1136) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.HasInvalidPlanetDestination(Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1161) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.AdvanceProductionFacilities(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.ManufacturingType, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1187) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.GetProductionCycleIncrement(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1215) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.AdvanceProductionFacility(Rebellion.Game.Units.Building, bool, double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1234) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.DiscardReadyProductionPoints(System.Collections.Generic.List<Rebellion.Game.Units.Building>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1269) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.DistributeProgress(System.Collections.Generic.List<Rebellion.Game.Units.IManufacturable>, System.Collections.Generic.List<Rebellion.Game.Units.Building>, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1283) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.MoveFinishedItemsToCompleted(System.Collections.Generic.List<Rebellion.Game.Units.IManufacturable>, System.Collections.Generic.List<Rebellion.Game.Units.IManufacturable>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1327) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.ApplyStandardProgress(Rebellion.Game.Units.IManufacturable, Rebellion.Game.Units.Building, Rebellion.Game.Factions.Faction, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1351) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.MoveCompletedActiveItem(System.Collections.Generic.List<Rebellion.Game.Units.IManufacturable>, System.Collections.Generic.List<Rebellion.Game.Units.IManufacturable>, Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1371) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.GetRemainingProgress(Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1389) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.AddProgressResult(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>, Rebellion.Game.Factions.Faction, int, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1401) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.CompleteManufacturedItems(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.ManufacturingType, System.Collections.Generic.List<Rebellion.Game.Units.IManufacturable>, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1426) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.GetQueuedItems(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.ManufacturingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1448) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.CreateQueueIdleResult(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.ManufacturingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1461) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.CompleteManufacturing(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1478) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.MarkCompleteAndDispatch(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1528) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.GetRemainingQueuePoints(System.Collections.Generic.List<Rebellion.Game.Units.IManufacturable>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1550) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.ClearQueue(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Units.ManufacturingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1561) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.CancelManufacturing(Rebellion.Game.Units.IManufacturable, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1586) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.CancelManufacturing(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IManufacturable>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1643) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.ClearQueueItems(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Units.IManufacturable>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1663) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.CancelUnsupportedProduction(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Building>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1683) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.DetachQueuedItem(Rebellion.Game.Units.IManufacturable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1731) | Private | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.ClearQueuesOnOwnershipChange(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1747) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.InvalidatePlanetDestinationOrders(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1782) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.RebuildQueues()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1832) | Public | ManufacturingCommands implementation / local helper |
| [`Rebellion.Systems.ManufacturingSystem.RestoreQueueOrder(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IManufacturable>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs#L1901) | Private | ManufacturingCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/AI/Proposals/AIFacilityRemovalProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIFacilityRemovalProposal.cs)
- [Assets/Scripts/AI/Proposals/AIManufactureProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIManufactureProposal.cs)
- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/FactionAutomationSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs)
- [Assets/Scripts/Systems/ManufacturingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs)
- [Assets/Scripts/Systems/PlanetaryControlSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Construction/ConstructionOrderController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Construction/ConstructionOrderController.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Facility/FacilityWindowController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Facility/FacilityWindowController.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Status/StrategyStatusInfoBuilder.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Status/StrategyStatusInfoBuilder.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Windows/StrategyWindowCommandController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Windows/StrategyWindowCommandController.cs)

Direct test references:

- `Rebellion.Tests.AI.Proposals.AIFacilityRemovalProposalTests.Execute_WithEqualFacilityRates_RemovesUnfinishedFacility`
- `Rebellion.Tests.Editor.Simulation.HeadlessSimulationRunnerTests.ManufacturedUnitTracker_RecordCompletion_CountsFacilityOnce`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_AdvisorOrderCompletes_RefillsReleasedLaneOnly`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_SnapshotPlanet_BuildingQueuedAfterSnapshot_NotVisible`
- `Rebellion.Tests.Sectors.FogOfWarSystemTests.BuildFactionView_SnapshotQueuedBuildingOnFullLivePlanet_SkipsGhostBuilding`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_StarfighterUnderConstruction_RetargetsDestination`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.CanAcceptManufacturingOrder_WithoutMaintenanceHeadroom_ReturnsTrue`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.CancelManufacturing_LastCapitalShip_RemovesDestinationFleet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.CancelManufacturing_OtherFaction_DoesNotRemoveItem`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.CancelManufacturing_QueuedItem_RemovesOnlySelectedItem`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.CancelManufacturing_QueuedItem_RestoresMaintenanceHeadroom`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.CancelManufacturing_QueuedItems_RemovesCompleteSelection`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.CancelManufacturing_ReservedInput_CompletesAndDiscardsFacilityCycle`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ClearQueue_BuildingQueue_ClearsQueueAndQueuedDestinationBuildings`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ClearQueue_EmptyQueue_ReturnsFalse`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ClearQueue_QueuedBuilding_RemovesItemAndQueueBucket`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EnqueueBuilding_ValidBuilding_ParentIsDestinationPlanet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EnqueueCapitalShip_NoOwner_ReturnsFalse`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EnqueueCapitalShip_PlanetDestinationWithFleetPresent_StillReturnsFalse`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EnqueueCapitalShip_PlanetDestination_ReturnsFalse`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EnqueueCapitalShip_ValidShip_AttachesToFleetAtPlanet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EnqueueCapitalShip_WithFleetDestination_JoinsFleet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EnqueueRegiment_ValidRegiment_ParentIsDestinationPlanet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EnqueueStarfighter_ValidFighter_ParentIsDestinationFleet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EnqueueTwoCapitalShips_SameExplicitFleet_JoinSameFleet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EnqueueTwoCapitalShips_SameFleet_BothJoin`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_AttachedToDifferentParent_ThrowsException`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_BuildingInBuildingState_Succeeds`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_CapitalShipDestinationAvailable_QueuesPassengerOnShip`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_CapitalShipDestinationInTransit_ReturnsFalse`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_CapitalShipDestinationUnderConstruction_ReturnsFalse`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_DifferentFaction_ReturnsFalse`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_DuplicateInstance_ThrowsException`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_FleetDestinationOwnedByDifferentFaction_ReturnsFalse`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_FleetDestinationWithOnlyUnfinishedCarrier_ReturnsFalse`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_IgnoreCostFlag_BypassesFunds`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_InsufficientRefinedMaterials_StillQueues`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_MultipleBuildings_MaintainsOrder`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_OrderUsingRemainingMaintenanceCapacity_Succeeds`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_RegimentToUncolonizedPlanet_ReturnsFalse`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_TwoInstancesSameType_BothAdded`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_ValidBuilding_AddsToQueue`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_ValidBuilding_AttachesToSceneGraph`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_WithIgnoreCostTrue_DoesNotDeductStockpile`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_WithSufficientStockpile_DoesNotDeductConstructionCost`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.Enqueue_ZeroCostItem_CompletesImmediately`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EstimateCompletionTicks_ActiveFacilityCycle_UsesRemainingCycleTime`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EstimateCompletionTicks_Default_IncludesEarlierQueuedWorkAndCurrentProgress`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EstimateManufacturingTicks_InactiveFacilities_DoNotContribute`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.EstimateManufacturingTicks_MixedFacilityRates_UsesIntegerRateShares`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.GetManufacturingQueue_TwoItems_ReturnsCorrectState`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.HandleResults_AnotherProductionBuildingSurvives_RetainsQueuedWork`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.HandleResults_BombardmentDestroysLastProducer_CancelsQueuedWork`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.HandleResults_LastProductionBuildingDestroyed_CancelsQueuedWork`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.HandleResults_LastProductionBuildingScrapped_CancelsQueuedWork`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_AtFormerAIRefinedMaterialReserve_ContinuesProduction`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_AtPlayerRefinedMaterialReserve_ContinuesProduction`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_AvailableRefinedMaterial_IsNotConsumedByManufacturing`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_Blockade_AppliesGraduatedRateAndKdyRestoresFullRate`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingBatchDestinationChangedSides_CancelsAllTargetedOrders`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingBatchDestinationChangedSides_CancelsWithoutFallbackCapacity`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingCompleteOnDifferentPlanet_ShipsToDestination`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingComplete_BidirectionalRelationshipValid`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingComplete_DeployedResultHasCorrectFactionAndObject`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingComplete_DoesNotChangePopularSupport`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingComplete_EmitsDeployedResult`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingComplete_EmitsPointsRequiredResult`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingComplete_EmitsRemainingResult`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingComplete_NoDuplicateNodes`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingComplete_RemainsAttachedToParent`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingComplete_RemovesFromQueue`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingComplete_SetsStatusComplete`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingDestinationChangedSides_CancelsOrder`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingDestinationChangedSides_CancelsRegardlessOfProducerCapacity`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingForOwnedUncolonizedPlanet_ColonizesOnArrival`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_CapitalShipBuilding_RemainsInFleetWithProgress`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_CapitalShipCompleteDestinationFleetDestroyed_ShipIsLost`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_CapitalShipCompleteFleetOverHostilePlanet_ShipTravelsToFleet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_CapitalShipCompleteOnDifferentPlanet_ShipsFleet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_CapitalShipCompleteOnSamePlanet_NoMovement`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_CapitalShipComplete_RemovedFromQueue`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_CapitalShip_UsesEveryReadyFacilityWithoutConsumingMaterial`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_DestinationDestroyed_UnitIsAlsoDestroyed`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_EmptyGame_ReturnsNoResults`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_ExactCompletion_DoesNotAdvanceNextItem`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_FasterProductionSource_CompletesCycleFirst`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_FirstOfTwoItemsCompletes_DoesNotEmitIdleResult`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_FirstOfTwoItemsCompletes_PointsRequiredMatchesRemainingItem`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_FirstOfTwoItemsCompletes_RemainingCountIsOne`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_FullBlockade_HaltsProductionWithoutReservingInput`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_LastItemCompletes_EmitsIdleResult`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_LocalBuildingOrderProducerChangedSides_CancelsOrder`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_MultipleCompletions_SameTick`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_MultipleProductionSources_StackCorrectly`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_NoConstructionYard_BuildingMakesNoProgress`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_NoShipyard_ShipMakesNoProgress`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_NoTrainingFacility_RegimentMakesNoProgress`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_OrdersForTwoCapturedPlanets_CancelsEntireLane`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_OverflowProgress_CarriesToNextItem`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_OverflowProgress_StartsNextItem`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_OwnerChangeMidBuild_RetainsOriginalOwner`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_ProductionBuildingRemoved_CancelsQueuedWork`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_QueueMutation_DoesNotSkipItems`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_RegimentCompleteFleetOverHostilePlanet_TravelsToTransport`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_RegimentCompleteOnSamePlanet_AttachedImmediately`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_RegimentComplete_ShipsToDestination`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_RegimentDestinationChangedSides_CancelsOrder`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_SingleItem_AdvancesProgress`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_SixInvalidOrdersBeforeValidOrder_CancelsInvalidAndAdvancesValid`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_StarfighterCompleteFleetOverHostilePlanet_TravelsToCarrier`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_StarfighterComplete_RemainsInsideDestinationFleet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_StarfighterComplete_ShipsToDestination`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_ThreeManufacturingTypes_AllAdvance`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_Uprising_HaltsProductionWithoutReservingInput`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_WithNoRefinedMaterials_ContinuesProduction`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_ZeroProductionRate_NoProgress`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.RebuildQueues_CalledTwice_NoDuplication`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.RebuildQueues_EmptyGame_NoQueues`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.RebuildQueues_InvalidProducerPlanetID_SkipsItem`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.RebuildQueues_MultiplePlanets_CorrectGrouping`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.RebuildQueues_NoProducerPlanetID_SkipsItem`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.RebuildQueues_OnlyBuilding_IgnoresComplete`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.RebuildQueues_PersistedOrder_RestoresOriginalQueueOrder`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.RetargetManufacturingDestination_QueuedLane_MovesEveryItem`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.StartManufacturing_CapitalShipRejected_RemovesEmptyDestinationFleet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.StartManufacturing_CapitalShipWithExistingFleets_AddsToFirstFleet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.StartManufacturing_CapitalShips_CreatesOneDestinationFleet`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.StartManufacturing_DifferentProject_ReplacesEntireProductionLane`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.StartManufacturing_OrderExceedsDestinationCapacity_DoesNotQueuePartialOrder`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.StartManufacturing_SameProject_AppendsRequestedCopies`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ClearPlanetOwnership_PlanetWithManufacturingQueue_DestroysQueuedUnit`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_Default_PreservesRegimentOrderAssignedToFriendlyFleet`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_MixedRemoteOrders_CancelsDestinationAndPreservesOthers`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithInProgressBuilding_ClearsInProgressBuilding`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithManufacturingQueues_ClearsQueues`

## Rebellion.Systems.MessageSystem

Source: [Assets/Scripts/Systems/MessageSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MessageSystem.cs#L14).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.MessageSystem.MessageSystem(Rebellion.Game.GameRoot, System.Collections.Generic.IEnumerable<Rebellion.Game.Messages.MessageDefinition>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MessageSystem.cs#L24) | Public | MessageCommands implementation / local helper |
| [`Rebellion.Systems.MessageSystem.ProcessResults(System.Collections.Generic.IEnumerable<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MessageSystem.cs#L35) | Public | Typed reaction/settled callback delegates operations to MessageCommands |
| [`Rebellion.Systems.MessageSystem.HandleRequests(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Requests.MessageDeliveryRequest>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MessageSystem.cs#L51) | Public | MessageCommands operation; executor retains deferred timing and failure containment |
| [`Rebellion.Systems.MessageSystem.Deliver(System.Collections.Generic.IEnumerable<Rebellion.Game.Requests.MessageDeliveryRequest>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MessageSystem.cs#L61) | Private | MessageCommands implementation / local helper |
| [`Rebellion.Systems.MessageSystem.CreateMessage(Rebellion.Game.Requests.MessageDeliveryRequest)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MessageSystem.cs#L92) | Private | MessageCommands implementation / local helper |
| [`Rebellion.Systems.MessageSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MessageSystem.cs#L119) | Public | MessageCommands implementation / local helper |
| [`Rebellion.Systems.MessageSystem.RemoveExpiredMessages()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MessageSystem.cs#L127) | Private | MessageCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/MessageSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MessageSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.MessageSystemTests.HandleRequests_WithCombatReport_DeliversReportAsMessage`
- `Rebellion.Tests.Systems.MessageSystemTests.ProcessResults_MessagesOlderThanRetention_DoesNotExpireMessages`
- `Rebellion.Tests.Systems.MessageSystemTests.ProcessResults_WithMessageDeliveryRequest_AddsMessageToFaction`
- `Rebellion.Tests.Systems.MessageSystemTests.ProcessResults_WithoutMatchingDefinition_DoesNotCreateMessageBucket`
- `Rebellion.Tests.Systems.MessageSystemTests.ProcessTick_MessagesOlderThanRetention_RemovesExpiredMessages`

## Rebellion.Systems.MissionSystem

Source: [Assets/Scripts/Systems/MissionSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L20).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.MissionSystem.MissionSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Systems.MovementSystem, Rebellion.Systems.UprisingSystem, Rebellion.Systems.OfficerLoyaltySystem, Rebellion.Systems.PersonnelSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L42) | Public | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L66) | Public | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.GetRecruitmentAvailabilityByFaction()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L90) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.HasRecruitmentCandidates(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L102) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.AddRecruitmentExhaustedResults(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>, System.Collections.Generic.Dictionary<string, bool>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L112) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.GetRecruitmentExhaustedPlanet(System.Collections.Generic.IEnumerable<Rebellion.Game.Results.GameResult>, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L145) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.IsRecruitmentMissionResult(Rebellion.Game.Results.MissionCompletedResult, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L174) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.CanCreateMission(Rebellion.Game.Missions.MissionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L191) | Public | MissionQueries (read rule / preview) |
| [`Rebellion.Systems.MissionSystem.TryCreateMission(Rebellion.Game.Missions.MissionContext, out Rebellion.Game.Missions.Mission)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L204) | Public | MissionQueries (read rule / preview) |
| [`Rebellion.Systems.MissionSystem.GetAvailableMissionOptions(Rebellion.Game.Missions.MissionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L221) | Public | MissionQueries (read rule / preview) |
| [`Rebellion.Systems.MissionSystem.GetObjectiveSuccessProbability(Rebellion.Game.Missions.Mission, System.Collections.Generic.IEnumerable<Rebellion.Game.Units.IMissionParticipant>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L235) | Public | MissionQueries (read rule / preview) |
| [`Rebellion.Systems.MissionSystem.GetMissionOdds(Rebellion.Game.Missions.MissionContext, System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L256) | Public | MissionQueries (read rule / preview) |
| [`Rebellion.Systems.MissionSystem.InitiateMission(Rebellion.Game.Missions.MissionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L293) | Public | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.AbortMission(string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L304) | Public | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.OfficerCaptureStateResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L326) | Public | Typed reaction/settled callback delegates operations to MissionCommands |
| [`Rebellion.Systems.MissionSystem.AddMissionResults(Rebellion.Game.Missions.Mission, System.Collections.Generic.IEnumerable<Rebellion.Game.Results.GameResult>, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L355) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.ResolveMissionContext(Rebellion.Game.Missions.MissionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L378) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.CreateAndBeginMission(Rebellion.Game.Missions.MissionContext)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L416) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.ResolveMissionParticipants(System.Collections.Generic.List<Rebellion.Game.Units.IMissionParticipant>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L437) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.ResolveSceneNode(Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L462) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.UpdateMission(Rebellion.Game.Missions.Mission)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L475) | Public | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.Rebellion.Game.Missions.IMissionExecutionRuntime.ResolveDetection(Rebellion.Game.Missions.Mission, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L496) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.Rebellion.Game.Missions.IMissionExecutionRuntime.ResolveCompletedObjective(Rebellion.Game.Missions.Mission)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L508) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.ApplyOfficerDeaths(System.Collections.Generic.IEnumerable<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L534) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.Rebellion.Game.Missions.IMissionExecutionRuntime.FinishMission(Rebellion.Game.Missions.Mission, Rebellion.Game.Results.MissionCompletedResult, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L552) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.TearDownMission(Rebellion.Game.Missions.Mission, Rebellion.Game.Results.MissionCompletedResult, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L572) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.CanRemainAtMissionLocation(Rebellion.Game.Units.IMissionParticipant, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L631) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.ResolveStrandedMissionUnits(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.IMovable>, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L651) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.MoveNonReturningParticipantsToPlanet(Rebellion.Game.Missions.Mission, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L685) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.GetFreeMissionParticipants(Rebellion.Game.Missions.Mission)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L706) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.GetAdditionalReturnPassengers(Rebellion.Game.Missions.Mission, Rebellion.Game.Results.MissionCompletedResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L717) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.IsFreeParticipant(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L734) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.ResolveDetection(Rebellion.Game.Missions.Mission, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L745) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.DoesDetectorFoilMission(Rebellion.Game.Missions.Mission, Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L777) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.EstimateFoilProbability(Rebellion.Game.Missions.Mission, System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L791) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.EstimatePersonnelLossProbability(Rebellion.Game.Missions.Mission, System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L918) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.AddProbability(System.Collections.Generic.Dictionary<System.Numerics.BigInteger, double>, System.Numerics.BigInteger, double)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L948) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.GetParticipantEvasionProbability(Rebellion.Game.Missions.Mission, Rebellion.Game.Units.IMissionParticipant, Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L968) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.GetFoilProbability(Rebellion.Game.Missions.Mission, Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L989) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.CalculateFoilScore(Rebellion.Game.Missions.Mission, Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1004) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.GetAverageEspionage(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IMissionParticipant>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1021) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.GetScaledCommanderEspionage(Rebellion.Game.Units.Officer, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1036) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.RollProbability(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1048) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.ResolveDecoys(Rebellion.Game.Missions.Mission, System.Collections.Generic.List<Rebellion.SceneGraph.ISceneNode>, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1061) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.ResolveFoiledParticipant(Rebellion.Game.Missions.Mission, Rebellion.Game.Units.IMissionParticipant, System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1096) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.ResolveEvasion(Rebellion.Game.Missions.Mission, Rebellion.Game.Units.IMissionParticipant, Rebellion.SceneGraph.ISceneNode, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1121) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.GetDetectors(Rebellion.Game.Missions.Mission, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1169) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.AddEligibleDetectors(Rebellion.Game.Missions.Mission, System.Collections.Generic.IEnumerable<Rebellion.SceneGraph.ISceneNode>, System.Collections.Generic.ICollection<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1211) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.GetDetectorRating(Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1229) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.DestroySpecialForces(Rebellion.Game.Units.SpecialForces, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1244) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.CaptureOfficer(Rebellion.Game.Units.Officer, string, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>, Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1269) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.GetEvasionProbability(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1297) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.BeginMission(Rebellion.Game.Missions.Mission)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1313) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.RollMissionDuration(Rebellion.Game.Missions.Mission)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1329) | Private | MissionCommands implementation / local helper |
| [`Rebellion.Systems.MissionSystem.GetMissionTables()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1344) | Private | MissionQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MissionSystem.LookupProbability(System.Collections.Generic.Dictionary<int, int>, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs#L1357) | Private | MissionQueries helper candidate; verify shared callers/purity |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/AI/Phases/AIMissionDecoyAssignmentPhase.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Phases/AIMissionDecoyAssignmentPhase.cs)
- [Assets/Scripts/AI/Proposals/AIAbortMissionProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIAbortMissionProposal.cs)
- [Assets/Scripts/AI/Proposals/AIMissionProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIMissionProposal.cs)
- [Assets/Scripts/AI/Scoring/AIMissionProposalScorer.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Scoring/AIMissionProposalScorer.cs)
- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/MissionSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Missions/MissionCreateWindowController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Missions/MissionCreateWindowController.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Screen/StrategyController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Screen/StrategyController.cs)

Direct test references:

- `Rebellion.Tests.AI.Scoring.AIMissionProposalScorerTests.Score_SubdueUprisingBelowProbabilityFloor_ReturnsZeroDespitePriorityBonus`
- `Rebellion.Tests.Game.Missions.AbductionMissionTests.UpdateMission_SuccessfulAbductionWithSpecialForces_MovesTargetToAbductorOrigin`
- `Rebellion.Tests.Game.Missions.AbductionMissionTests.UpdateMission_SuccessfulAbduction_MovesTargetToAbductorOrigin`
- `Rebellion.Tests.Game.Missions.AbductionMissionTests.UpdateMission_TargetAlreadyCaptured_ReturnsFailed`
- `Rebellion.Tests.Game.Missions.AbductionMissionTests.UpdateMission_TargetMovedToDifferentPlanet_DoesNotRollOrImproveParticipant`
- `Rebellion.Tests.Game.Missions.AssassinationMissionTests.UpdateMission_TargetAlreadyKilled_ReturnsFailed`
- `Rebellion.Tests.Game.Missions.AssassinationMissionTests.UpdateMission_TargetMovedToDifferentPlanet_DoesNotRollOrImproveParticipant`
- `Rebellion.Tests.Game.Missions.ReconnaissanceMissionTests.UpdateMission_EnemyDetectorSucceeds_FoilsReconnaissance`
- `Rebellion.Tests.Game.Missions.RecruitmentMissionTests.UpdateMission_NoCandidatesRemain_DoesNotRollOrImproveRecruiter`
- `Rebellion.Tests.Game.Missions.RescueMissionTests.UpdateMission_OfficerNotCaptured_ReturnsFailed`
- `Rebellion.Tests.Game.Missions.RescueMissionTests.UpdateMission_RescuedOfficerAlreadyInTransit_ReturnsRescuerOnly`
- `Rebellion.Tests.Game.Missions.RescueMissionTests.UpdateMission_SuccessfulRescue_MovesTargetToRescuerOrigin`
- `Rebellion.Tests.Game.Missions.RescueMissionTests.UpdateMission_TargetMovedToDifferentPlanet_ReturnsFailed`
- `Rebellion.Tests.Game.Missions.RescueMissionTests.UpdateMission_TargetOfficerAlreadyFreed_ReturnsFailed`
- `Rebellion.Tests.Game.Missions.RescueMissionTests.UpdateMission_TargetRemovedFromScene_ReturnsFailed`
- `Rebellion.Tests.Game.Missions.SabotageMissionTests.UpdateMission_BuildingRemovedBeforeExecution_ReturnsFailed`
- `Rebellion.Tests.Game.Missions.SubdueUprisingMissionTests.UpdateMission_MultipleParticipantsSucceed_AppliesEveryAttempt`
- `Rebellion.Tests.Game.Missions.SubdueUprisingMissionTests.UpdateMission_SuccessfulRollWithInsufficientGarrison_LeavesUprisingAndFails`
- `Rebellion.Tests.Game.Missions.SubdueUprisingMissionTests.UpdateMission_SuccessfulRollWithSufficientGarrison_EndsUprisingAndImprovesAgent`
- `Rebellion.Tests.Managers.GameManagerTests.MovementCommand_SurfaceRegimentCreatesGarrisonDeficit_StartsUprisingImmediately`
- `Rebellion.Tests.Sectors.MissionSystemTests.AbortMission_ActiveMission_ReturnsParticipantAndDetachesMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.AbortMission_ParticipantInTransit_ReturnsParticipantToOrigin`
- `Rebellion.Tests.Sectors.MissionSystemTests.BeginMission_ParticipantAssigned_SetsParticipantParentToMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.CanCreateMission_InactiveOfficer_ReturnsFalse`
- `Rebellion.Tests.Sectors.MissionSystemTests.CanCreateMission_StaleCompletedViewTarget_ReturnsTrue`
- `Rebellion.Tests.Sectors.MissionSystemTests.CanCreateMission_StaleStationaryOfficerViewWithLiveTransit_ReturnsTrue`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_DisallowedResearch_ExcludesResearchOptions`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_EnemyPlanetRecruitment_ExcludesRecruitmentOption`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_ExhaustedResearch_ExcludesResearchOption`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_ManufacturableSabotageTarget_ReturnsSabotageOption`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_OwnPlanetResearch_ReturnsResearchOptions`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_PlanetOnlySabotageTarget_ExcludesSabotageOption`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_ReconnaissanceSpecialForces_ReturnsReconnaissanceOption`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_ResearchWithSingleMatchingRating_ReturnsMatchingResearchOption`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_ResearchWithoutMatchingRating_ExcludesResearchOptions`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_SelectedTrainerWithoutStudent_ExcludesJediTrainingOption`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetAvailableMissionOptions_TroopTrainingWithoutFacility_ExcludesResearchOption`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetMissionOdds_AssassinationOfMainCharacter_CannotReportSuccess`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetMissionOdds_Assassination_CombinesHitAndKillChecksPerParticipant`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetMissionOdds_Default_CombinesKnownDetectorsAndAssignedDecoys`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetMissionOdds_Default_DoesNotExposeHiddenBetrayalState`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetMissionOdds_Default_IncludesStationaryFleetDetectors`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetMissionOdds_Default_TracksWhichDecoySurvivesEachDetector`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetMissionOdds_Diplomacy_UsesObservedPlanetSupport`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetMissionOdds_OfficerTargetMission_UsesObservedTargetRating`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetMissionOdds_Reconnaissance_UsesItsGuaranteedCompletionRule`
- `Rebellion.Tests.Sectors.MissionSystemTests.GetMissionOdds_WithMultipleOfficers_CombinesPersonnelLossProbability`
- `Rebellion.Tests.Sectors.MissionSystemTests.HandleResults_CapturedMissionParticipant_TearsDownMissionAtCurrentPlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.InitiateMission_EnemyOfficerFactionViewTarget_AttachesToLivePlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.InitiateMission_EnemyRegimentFactionViewTarget_AttachesToLivePlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.InitiateMission_ExhaustedResearch_ReturnsFalse`
- `Rebellion.Tests.Sectors.MissionSystemTests.InitiateMission_IneligibleSelectedTarget_ReturnsFalse`
- `Rebellion.Tests.Sectors.MissionSystemTests.InitiateMission_JediTraining_UsesConfiguredExecutionRange`
- `Rebellion.Tests.Sectors.MissionSystemTests.InitiateMission_ResearchWithDiscipline_AttachesResearchMissionToPlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.InitiateMission_SabotageTargetOnDifferentPlanet_ReturnsFalse`
- `Rebellion.Tests.Sectors.MissionSystemTests.InitiateMission_StaleCompletedViewTarget_CreatesMissionFromObservedState`
- `Rebellion.Tests.Sectors.MissionSystemTests.InitiateMission_WithFactionViewObjects_UsesLiveSceneGraphNodes`
- `Rebellion.Tests.Sectors.MissionSystemTests.IsOnMission_AfterBeginMission_ReturnsTrue`
- `Rebellion.Tests.Sectors.MissionSystemTests.ProcessTick_RecruitmentMissionsExhaustCandidates_ReturnsOneRecruitmentExhaustedResult`
- `Rebellion.Tests.Sectors.MissionSystemTests.ProcessTick_WithCompletedMission_ReturnsMissionCompletedResult`
- `Rebellion.Tests.Sectors.MissionSystemTests.RunOrbitalCaptureMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.TearDownMission_CapturedParticipant_SkipsMovement`
- `Rebellion.Tests.Sectors.MissionSystemTests.TearDownMission_FriendlyLocation_ParticipantsRemainAtPlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.TearDownMission_FriendlyUncolonizedLocation_ReturnsOfficerToOrigin`
- `Rebellion.Tests.Sectors.MissionSystemTests.TearDownMission_HostileLocation_OriginFleetMoved_ReturnsToRecordedShip`
- `Rebellion.Tests.Sectors.MissionSystemTests.TearDownMission_ParticipantAttachedToMissionViaSceneGraph_DoesNotThrow`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_AbductionTargetBeginsTransitBeforeArrival_FailsAndTearsDown`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_AbductionTargetCapturedBeforeArrival_FailsAndTearsDown`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_AbductionTargetMovedAfterFactionViewSnapshot_FailsAndTearsDown`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_AnyParticipantInTransit_DoesNotProgressOrExecute`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_AnyParticipantInTransit_NoDetectionOrCapture`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_AssassinationTargetCapturedBeforeArrival_FailsAndTearsDown`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_BetrayingOfficer_ProducesFailedCompletion`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_CapturedByGarrisonOnEnemyPlanet_StaysOnCaptorPlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_CapturedParticipantWithDifferentCaptor_StaysOnMissionPlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_CompletedBuilding_DoesNotDetectMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_CompletedParticipantOnNeutralPlanet_ReturnsToNearestFriendlyPlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_CompletedParticipantParentedToMission_ReturnsParticipantToPlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_CompletedUnitOnIncompleteCapitalShip_DoesNotDetectMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_CompletedWithoutReturnDestination_CapturesOfficerAndDetachesMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DecoyCheck_AlwaysUsesEspionage`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectionAlreadyResolved_DoesNotRollAgain`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectionCapturesParticipant_CancelsMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectionOnOwnPlanet_NeverDetected`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectionPicksOneRandomDecoy_NotAll`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectionRollFails_MissionContinues`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectionSucceedsWithoutCaptureOrKill_FoilsMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectionWithDecoy_PreventsCapture`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectionWithSpecialForces_DestroysUnit`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectionWithoutEvasionTable_UsesConfiguredDefault`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectorRatingAndRank_SelectMatchingCommander`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DetectorWithoutCommander_CanFoil`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DiploBeforeIncite_DiploAbortsOnNextLifecycleStep`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DiplomacyCompletionFromFleet_ParticipantRemainsAtTargetPlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DiplomacyTargetCaptured_ReturnsOfficerToNearestFriendlyPlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DiplomacyWithHostileDetector_CanBeFoiled`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_DiplomacyWithoutHostileDetector_DoesNotInjureParticipant`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_EspionageDetected_AppliesFoiledParticipantConsequences`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_EvasionFails_CapturesParticipant`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_EvasionFails_MovesCaptiveToMissionPlanet`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_EvasionSucceeds_ReturnsParticipant`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_FactionViewSabotageTargetMissingAtArrival_FailsAndTearsDown`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_FailedDecoyInjuryKillsMinor_DoesNotReuseDecoy`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_FleetDetector_UsesFleetDecoyTable`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_FoilScore_UsesEspionageInsteadOfMissionRating`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_FriendlyBuilding_BlocksFleetDetection`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_FriendlyBuilding_DoesNotBlockPlanetaryDetection`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_HighDetectorRating_DecoyFails`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_InTransitFleetDetector_DoesNotFoilMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_InciteBeforeDiplo_DiploAbortsWhenAdvanced`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_InciteRemovesOpposingControlWithoutOwnTroops_SucceedsAndImprovesAgent`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_MainParticipantRemoved_ReturnsFailedMissionCompletedResult`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_MainSpecialForces_ReducesFoilScore`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_MissingOwnerFaction_DetachesMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_OfficerEvadesDetector_DoesNotApplyCaptureEvasionInjury`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_OfficerFailsToEvadeDetector_AppliesCaptureEvasionInjury`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_OfficerKilledResult_DisablesAndRetainsKilledOfficer`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_OnCompletion_DetachesMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_ParticipantInjuredAfterInitiation_DoesNotAbortMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_RecruitmentOnFriendlyPlanetWithHostileDetector_CanBeFoiled`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_RecruitmentOnFriendlyPlanetWithSuccessfulDecoy_Continues`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_RescueTargetFreedBeforeArrival_FailsAndTearsDown`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_SabotageTargetBeginsConstructionBeforeArrival_FailsAndTearsDown`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_SpecialForcesEvadesDetector_IsNotDestroyed`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_StaleMissingViewTarget_WaitsForArrivalThenFailsAndTearsDown`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_TargetPlanetDestroyedDuringTravel_WaitsForArrivalThenFails`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_TwoDetectors_FoilsWhenOnlySecondSucceeds`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_UnblockedFleetDetector_FoilsMission`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_WithDecoyParticipant_DecoyAppearsInParticipants`
- `Rebellion.Tests.Sectors.MissionSystemTests.UpdateMission_WithSpecialForcesParticipant_AppearsInParticipants`

## Rebellion.Systems.MovementSystem

Source: [Assets/Scripts/Systems/MovementSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L22).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.MovementSystem.SetCompletedBuildingMovementPolicy(System.Func<Rebellion.Game.Units.Building, bool>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L44) | Internal | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.MovementSystem(Rebellion.Game.GameRoot, Rebellion.Systems.FogOfWarSystem, Rebellion.Systems.FleetSystem, Rebellion.Systems.BlockadeSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L57) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L74) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.Rebellion.Systems.IGameResultHandler<Rebellion.Game.Results.BlockadeChangedResult>.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.BlockadeChangedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L93) | Private | Typed reaction/settled callback delegates operations to MovementCommands |
| [`Rebellion.Systems.MovementSystem.Rebellion.Systems.IGameRequestHandler<Rebellion.Game.Requests.UnitMovementRequest>.HandleRequests(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Requests.UnitMovementRequest>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L125) | Private | MovementCommands operation; executor retains deferred timing and failure containment |
| [`Rebellion.Systems.MovementSystem.TryRequestMove(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IMovable>, System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ContainerNode>, string, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L156) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.Rebellion.Systems.IGameRequestHandler<Rebellion.Game.Requests.UnitPlacementRequest>.HandleRequests(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Requests.UnitPlacementRequest>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L189) | Private | MovementCommands operation; executor retains deferred timing and failure containment |
| [`Rebellion.Systems.MovementSystem.RequestMove(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L217) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryRequestMove(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L228) | Internal | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryEstablishCapturedOfficerCustody(Rebellion.Game.Units.Officer, Rebellion.SceneGraph.ContainerNode, Rebellion.Game.Units.IMovable, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L241) | Internal | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryRequestMove(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L305) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.RequestMove(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L367) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.RequestMove(System.Collections.Generic.List<Rebellion.Game.Units.IMovable>, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L417) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.RequestMove(System.Collections.Generic.List<Rebellion.Game.Units.IMovable>, Rebellion.SceneGraph.ContainerNode, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L428) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.SendToMission(Rebellion.Game.Units.IMissionParticipant, Rebellion.Game.Missions.Mission)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L452) | Internal | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ReturnFromMission(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IMissionParticipant>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IMovable>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L472) | Internal | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CompleteMissionAtLocation(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IMissionParticipant>, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L558) | Internal | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.RestoreMovement(System.Collections.Generic.IReadOnlyDictionary<Rebellion.Game.Units.IMovable, Rebellion.Game.Units.MovementState>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L591) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ResolveMissionReturnDestination(Rebellion.Game.Units.IMissionParticipant)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L604) | Internal | MovementQueries (read rule / preview) |
| [`Rebellion.Systems.MovementSystem.AddToReturnGroup(System.Collections.Generic.IDictionary<Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.List<Rebellion.Game.Units.IMovable>>, Rebellion.SceneGraph.ContainerNode, Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L642) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryRequestMove(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, Rebellion.SceneGraph.ContainerNode, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L664) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryExecuteSelectionMove(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, Rebellion.SceneGraph.ContainerNode, string, out Rebellion.Game.Units.Fleet, out System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L691) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CanSetFleetWaypointRoute(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, System.Collections.Generic.IReadOnlyList<string>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L758) | Public | MovementQueries (read rule / preview) |
| [`Rebellion.Systems.MovementSystem.TrySetFleetWaypointRoute(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, System.Collections.Generic.IReadOnlyList<string>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L787) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ClearFleetWaypoints(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L865) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ContinueFleetWaypointRoutes()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L888) | Internal | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryGetSelectionTransitTicks(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, Rebellion.SceneGraph.ContainerNode, string, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L923) | Public | MovementQueries (read rule / preview) |
| [`Rebellion.Systems.MovementSystem.TryGetTransitTicks(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IMovable>, Rebellion.SceneGraph.ContainerNode, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L959) | Public | MovementQueries (read rule / preview) |
| [`Rebellion.Systems.MovementSystem.TryEstimateManufacturedTransitTicks(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet, Rebellion.SceneGraph.ContainerNode, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1013) | Public | MovementQueries (read rule / preview) |
| [`Rebellion.Systems.MovementSystem.TryGetDestinationPlanetForTransit(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, out Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1066) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.CanMoveGroup(System.Collections.Generic.List<Rebellion.Game.Units.IMovable>, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1094) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryResolveMoveGroupDestinations(System.Collections.Generic.List<Rebellion.Game.Units.IMovable>, Rebellion.SceneGraph.ContainerNode, out System.Collections.Generic.List<Rebellion.SceneGraph.ContainerNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1106) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryExecuteMoveGroupClearingWaypoints(System.Collections.Generic.List<Rebellion.Game.Units.IMovable>, Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1193) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryExecuteMoveGroup(System.Collections.Generic.List<Rebellion.Game.Units.IMovable>, Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1217) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryPlaceGroup(System.Collections.Generic.List<Rebellion.Game.Units.IMovable>, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1263) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryResolvePlacementGroupDestinations(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.IMovable>, Rebellion.SceneGraph.ContainerNode, out System.Collections.Generic.List<Rebellion.SceneGraph.ContainerNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1324) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryResolveControlledSelection(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, string, out System.Collections.Generic.List<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1369) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.TryResolveControlledFleets(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, string, out System.Collections.Generic.List<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1408) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.TryResolveFleetWaypointRoute(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, System.Collections.Generic.IReadOnlyList<string>, string, out System.Collections.Generic.List<Rebellion.Game.Units.Fleet>, out System.Collections.Generic.List<Rebellion.Game.Galaxy.Planet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1448) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.TryResolveCapitalShipWaypointRoute(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, System.Collections.Generic.IReadOnlyList<string>, string, out System.Collections.Generic.List<Rebellion.Game.Units.CapitalShip>, out System.Collections.Generic.List<Rebellion.Game.Galaxy.Planet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1504) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.TryResolveWaypointDestinations(System.Collections.Generic.IReadOnlyList<string>, out System.Collections.Generic.List<Rebellion.Game.Galaxy.Planet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1549) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.TryResolveSelectionMoveGroup(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, Rebellion.SceneGraph.ContainerNode, string, out System.Collections.Generic.List<Rebellion.Game.Units.IMovable>, out System.Collections.Generic.List<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1588) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.CanReceiveMoveOrder(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1653) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.IsUnderConstruction(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1686) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.IsManufacturingDestinationChange(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1699) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.IsCompletedBuilding(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1716) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.HasEscortForCapturedOfficer(Rebellion.Game.Units.Officer, System.Collections.Generic.List<Rebellion.Game.Units.IMovable>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1728) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CanEscortCapturedOfficer(Rebellion.Game.Units.IMovable, Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1744) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.UpdateMovement(Rebellion.Game.Units.IMovable, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1756) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CalculateInterpolatedPosition(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1796) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CheckArrival(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1815) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryFollowMovingFleetDestination(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1875) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CompleteMissionParticipantArrival(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1895) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.HasArrivalOwnerConflict(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1907) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.GetMovementControlOwner(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1925) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.TryRejectBlockadedArrival(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1943) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.IsBlockedFromDestinationByBlockade(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1977) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.RejectArrivalAtChangedOwner(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L1992) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CompleteArrival(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2025) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ConsumeReachedFleetWaypoint(Rebellion.Game.Units.Fleet, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2068) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.AdvanceFleetWaypointRoute(Rebellion.Game.Units.Fleet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2090) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.HasPendingCapitalShipsAtCurrentWaypoint(Rebellion.Game.Units.Fleet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2139) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CaptureFleetArrivalSnapshot(Rebellion.Game.Units.Fleet, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2168) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.AddArrivalResults(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet, string, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>, string, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2190) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CompleteManufacturingDelivery(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2229) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CanEnterHostileOrbit(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2248) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.HandleBlockadeStarted(Rebellion.Game.Results.BlockadeChangedResult, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2266) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ShouldAutorouteFromBlockade(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2317) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.FindBlockadeAutorouteDestination(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2330) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.DestroyBlockadeInboundUnit(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2386) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.DestroyEvictedUnit(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2409) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.EvacuateToNearestFriendlyPlanet(Rebellion.Game.Units.IMovable, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2433) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CanEvacuateToNearestFriendlyPlanet(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2490) | Internal | MovementQueries (read rule / preview) |
| [`Rebellion.Systems.MovementSystem.CaptureStrandedOfficer(Rebellion.Game.Units.Officer, Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2512) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.RelocateUnits(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.IMovable>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2536) | Public | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CanTravelBetweenPlanets(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2605) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.HandleArrivalRejection(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2628) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.FindEvacuationDestinations(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2669) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.ExecuteMove(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>, string, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2697) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ExecuteAcceptedMove(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>, string, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2735) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.RetargetInTransitFleetJoiners(Rebellion.Game.Units.Fleet, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2848) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.RetargetMovement(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2863) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ResolveMoveDestination(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.IReadOnlyDictionary<Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.List<Rebellion.SceneGraph.ISceneNode>>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2891) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.TryRetargetManufacturingDestination(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2909) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ApplyManufacturingDestination(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2929) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.TryResolveAcceptedDestination(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, out Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2941) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.TryResolveAcceptedDestination(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.IReadOnlyDictionary<Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.List<Rebellion.SceneGraph.ISceneNode>>, out Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L2958) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.CanMoveToUncolonizedPlanet(Rebellion.Game.Units.IMovable, Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3006) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.ClaimUncolonizedDestinationFromRegiment(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3030) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.AddPlanetGarrisonChangedResults(System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>, Rebellion.Game.Units.IMovable, params Rebellion.Game.Galaxy.Planet[])`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3081) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.ResolveFleetTarget(Rebellion.Game.Units.IMovable, Rebellion.Game.Units.Fleet, System.Collections.Generic.IReadOnlyDictionary<Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.List<Rebellion.SceneGraph.ISceneNode>>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3105) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.CanAcceptReservedChild(Rebellion.SceneGraph.ContainerNode, Rebellion.SceneGraph.ISceneNode, System.Collections.Generic.IReadOnlyDictionary<Rebellion.SceneGraph.ContainerNode, System.Collections.Generic.List<Rebellion.SceneGraph.ISceneNode>>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3160) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.CalculateTransitTicks(Rebellion.Game.Units.IMovable, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3184) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.CalculateTransitTicks(Rebellion.Game.Units.IMovable, System.Drawing.Point, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3202) | Private | MovementCommands implementation / local helper |
| [`Rebellion.Systems.MovementSystem.CalculateTransitTicks(Rebellion.Game.Units.IMovable, System.Drawing.Point, Rebellion.Game.Galaxy.Planet, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3225) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.GetMovementHyperdrive(Rebellion.Game.Units.IMovable)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3253) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.IsSameSector(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3286) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.ResolveLiveNode(Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3298) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.ResolveRegisteredNode(Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3311) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.ResolveRegisteredContainer(Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3323) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.ResolveLiveContainer(Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3335) | Private | MovementQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.MovementSystem.RequireDestinationPlanet(Rebellion.SceneGraph.ContainerNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs#L3348) | Private | MovementQueries helper candidate; verify shared callers/purity |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/AI/Proposals/AIColonizationProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIColonizationProposal.cs)
- [Assets/Scripts/AI/Proposals/AIFleetAttackProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIFleetAttackProposal.cs)
- [Assets/Scripts/AI/Proposals/AIFleetDefenseProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIFleetDefenseProposal.cs)
- [Assets/Scripts/AI/Proposals/AIOrbitalEngagementProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIOrbitalEngagementProposal.cs)
- [Assets/Scripts/AI/Proposals/AITransferUnitProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AITransferUnitProposal.cs)
- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/BombardmentSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs)
- [Assets/Scripts/Systems/CaptiveSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/CaptiveSystem.cs)
- [Assets/Scripts/Systems/HeadquartersSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/HeadquartersSystem.cs)
- [Assets/Scripts/Systems/ManufacturingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ManufacturingSystem.cs)
- [Assets/Scripts/Systems/MissionSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs)
- [Assets/Scripts/Systems/MovementSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MovementSystem.cs)
- [Assets/Scripts/Systems/PlanetaryControlSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs)
- [Assets/Scripts/Systems/SpaceCombatSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Construction/ConstructionOrderController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Construction/ConstructionOrderController.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Windows/StrategyWindowCommandController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Windows/StrategyWindowCommandController.cs)

Direct test references:

- `Rebellion.Tests.Game.Missions.MissionTests.Serialize_RoundTripActiveMission_PreservesParticipantSceneGraph`
- `Rebellion.Tests.Game.Missions.ReconnaissanceMissionTests.UpdateMission_EnemyDetectorSucceeds_FoilsReconnaissance`
- `Rebellion.Tests.Managers.GameManagerTests.MovementCommand_LastSurfaceRegimentNeutralizesPlanet_ReportsImmediately`
- `Rebellion.Tests.Managers.GameManagerTests.MovementCommand_SurfaceRegimentCreatesGarrisonDeficit_StartsUprisingImmediately`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_BlockadeStarts_ReroutesInboundStarfighter`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_FleetArrivesAtPlanetaryStarfighters_CreatesPendingCombat`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_FleetDestroyedAfterArrival_AddsFleetArrivalAndBattleMessages`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_FleetReachesWaypoint_StartsNextLegAfterCombatDetection`
- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_LoadedConvergingMultipleFleets_ResolvesSingleCombinedCombat`
- `Rebellion.Tests.Managers.GameManagerTests.ResolveCombat_UnrelatedFleetReachedWaypoint_StartsDeferredNextLeg`
- `Rebellion.Tests.Sectors.MovementSystemTests.BlockadeStarted_BlockadedFallback_IsSkipped`
- `Rebellion.Tests.Sectors.MovementSystemTests.BlockadeStarted_BlockaderOwnedInboundUnit_Continues`
- `Rebellion.Tests.Sectors.MovementSystemTests.BlockadeStarted_ExcludedInboundUnits_ContinueToDestination`
- `Rebellion.Tests.Sectors.MovementSystemTests.BlockadeStarted_InTransitBuilding_IsDestroyed`
- `Rebellion.Tests.Sectors.MovementSystemTests.BlockadeStarted_IndependentInboundUnits_RerouteFromCurrentPosition`
- `Rebellion.Tests.Sectors.MovementSystemTests.BlockadeStarted_NearerFriendlyCarrier_IsPreferredOverOwnedPlanet`
- `Rebellion.Tests.Sectors.MovementSystemTests.BlockadeStarted_NoValidFallback_DestroysAutoroutedUnit`
- `Rebellion.Tests.Sectors.MovementSystemTests.BuildFleetWithInTransitChildrenScene`
- `Rebellion.Tests.Sectors.MovementSystemTests.CanSetFleetWaypointRoute_CapitalShip_DoesNotChangeFleetMembership`
- `Rebellion.Tests.Sectors.MovementSystemTests.CanSetFleetWaypointRoute_ValidRoute_DoesNotMutateFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.ClearFleetWaypoints_ActiveRoute_PreservesCurrentMovementAndStopsContinuation`
- `Rebellion.Tests.Sectors.MovementSystemTests.HandleMovementRequest_EventOriginatedRequest_PropagatesSourceToArrival`
- `Rebellion.Tests.Sectors.MovementSystemTests.ProcessTick_FleetArrivesAtAlreadyVisitedPlanet_DoesNotDuplicate`
- `Rebellion.Tests.Sectors.MovementSystemTests.ProcessTick_FleetArrivesAtNeutralPlanet_CompletesAndMarksVisitor`
- `Rebellion.Tests.Sectors.MovementSystemTests.ProcessTick_FleetArrivesAtPlanet_MarksFactionAsVisitor`
- `Rebellion.Tests.Sectors.MovementSystemTests.ProcessTick_GroupCapturedOfficerArrivesAtCaptorPlanet_CompletesMovement`
- `Rebellion.Tests.Sectors.MovementSystemTests.ProcessTick_OfficerArrivesAtPlanet_MarksFactionAsVisitor`
- `Rebellion.Tests.Sectors.MovementSystemTests.RelocateUnits_HyperdriveFighterOccupiesRecoveryCapacity_EvacuatesHyperdriveFighterAndRecoversNonHyperdriveFighter`
- `Rebellion.Tests.Sectors.MovementSystemTests.RelocateUnits_LimitedRecoveryCapacity_PrioritizesNonHyperdriveStarfighter`
- `Rebellion.Tests.Sectors.MovementSystemTests.RelocateUnits_NoCompatibleShipAndNoHyperdrive_LeavesStarfighterWithCurrentShip`
- `Rebellion.Tests.Sectors.MovementSystemTests.RelocateUnits_NoCompatibleShip_MovesHyperdriveStarfighterToFriendlyPlanet`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_BuildingUnderConstructionToUncolonizedPlanet_IsRejected`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_BuildingUnderConstruction_RetargetsDestination`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_CapitalShipFromFleetToFleetAtSamePlanet_ReparentsWithoutTransit`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_CapitalShipInFleetDestinationCaptured_ShipRemainsInFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_CapitalShipInFriendlyFleetOverHostilePlanet_StartsTransit`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_CapitalShipToFleet_LandsAtFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_CapturedOfficerAtSamePlanet_ReparentsWithoutMovement`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_CapturedOfficer_IsNotMoved`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_CompletedBuilding_DoesNotMove`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_DifferentSystemDestination_UsesGlobalTransitMinimum`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_FleetToNeutralUncolonizedPlanet_IsAllowed`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_FleetToRejectedDestination_PreservesWaypointRoute`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_FleetWithDifferentHyperdrives_UsesSlowestCompletedShip`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_FleetWithInboundUnits_RetargetsInboundUnits`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupCapturedOfficerEscortAtDifferentLocation_NoneMove`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupCapturedOfficerEscortFromWrongFaction_NoneMove`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupCapturedOfficerWithCapturingOfficerEscort_BothMove`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupCapturedOfficerWithCapturingRegimentEscort_DoesNotMove`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupCapturedOfficerWithCapturingSpecialForcesEscort_BothMove`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupCapturedOfficerWithoutEscort_NotMoved`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupCompletedBuilding_NoneMove`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupFromDifferentShipsAtSamePlanet_MovesAllToDestinationFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupNonCapturedUnits_AllMove`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupNonCapturedUnits_SetsSharedMovementGroupID`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupToFactionViewFleet_BoardsLiveFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupUnitAlreadyInTransit_NoneMove`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupUnitUnderConstruction_RetargetsDelivery`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_GroupUnitsAtDifferentLocations_NoneMove`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_LastRegimentOffColonizedOwnedPlanet_OwnershipPersists`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_LastRegimentOffUncolonizedOwnedPlanet_DoesNotImmediatelyReleaseToNeutral`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_ManufacturedBuildingCompletedAtBlockadedProductionPlanet_RemainsLocal`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_ManufacturedUnitDestinationWithoutPlanet_Throws`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_OfficerFromBlockadedPlanet_NotAffected`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_OfficerOnCapitalShipInFleet_CanMoveToMission`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_RegimentFromBlockadedPlanet_EmitsEvacuationResult`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_RegimentFromBlockadedPlanet_HighRoll_RegimentSurvives`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_RegimentFromBlockadedPlanet_LowRoll_DestroysRegiment`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_RegimentFromFleetAtEnemyUncolonizedPlanet_IsRejected`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_RegimentFromFleetAtNeutralUncolonizedPlanet_ClaimsImmediately`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_RegimentFromFleetAtNeutralUncolonizedPlanet_HiddenObserverSnapshot_NotRefreshed`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_RegimentFromFleetAtOtherPlanetToNeutralUncolonizedPlanet_IsRejected`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_RegimentFromOtherPlanetToNeutralUncolonizedPlanet_IsRejected`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_RegimentFromUnblockedPlanet_NoEvacuationLoss`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_RegimentToNeutralColonizedPlanet_IsRejected`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_SameSectorDestination_CanUseLocalTransitMinimum`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_SpecialForcesToFleetAtSamePlanet_BoardsFirstShip`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_StarfighterToNeutralUncolonizedPlanet_IsRejected`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_StarfighterUnderConstruction_RetargetsDestination`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_ValidDestination_ImmediatelyReparentsUnit`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_ValidDestination_SetsMovementGroupID`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_ValidDestination_SetsMovementStateWithDestination`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_ValidDestination_SetsOriginPositionFromDeparturePlanet`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_ValidDestination_SetsTransitTicksGreaterThanZero`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_ValidDestination_UnitIsNoLongerAtOrigin`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_WhenDestinationRejectsUnit_LeavesUnitAtOriginWithoutMovement`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_WhenUnitAlreadyInTransit_DoesNotRedirect`
- `Rebellion.Tests.Sectors.MovementSystemTests.RequestMove_WhenUnitNotAtAnyPlanet_IsIgnored`
- `Rebellion.Tests.Sectors.MovementSystemTests.ReturnFromMission_CapturedPassenger_ReturnsWithEscortGroup`
- `Rebellion.Tests.Sectors.MovementSystemTests.ReturnFromMission_MissingOwnerAndRecordedLocation_ReturnsParticipantAsStranded`
- `Rebellion.Tests.Sectors.MovementSystemTests.ReturnFromMission_MissingRecordedLocation_ReturnsToNearestFriendlyPlanet`
- `Rebellion.Tests.Sectors.MovementSystemTests.ReturnFromMission_MissingRecordedLocation_UsesFriendlyPlanetInsteadOfUnrelatedFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.ReturnFromMission_NoFriendlyDestination_ReturnsParticipantAsStranded`
- `Rebellion.Tests.Sectors.MovementSystemTests.ReturnFromMission_ParticipantsWithDifferentOrigins_ReturnToTheirOwnLocations`
- `Rebellion.Tests.Sectors.MovementSystemTests.ReturnFromMission_PassengerWithoutParticipant_ReturnsPassengerAsStranded`
- `Rebellion.Tests.Sectors.MovementSystemTests.ReturnFromMission_RecordedPlanetCaptured_ReturnsToNearestFriendlyPlanet`
- `Rebellion.Tests.Sectors.MovementSystemTests.ReturnFromMission_RecordedShipMoved_ReturnsToRecordedShip`
- `Rebellion.Tests.Sectors.MovementSystemTests.SendToMission_OfficerAboardShip_RecordsShipAndPlanet`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryEstimateManufacturedTransitTicks_HostilePlanetDestination_ReturnsFalse`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryEstimateManufacturedTransitTicks_StarfighterToEnemyBlockadedPlanet_ReturnsFalse`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryEstimateManufacturedTransitTicks_ValidDestination_DoesNotAssignMovement`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryEstimateManufacturedTransitTicks_ViewFleetDestination_UsesLiveFleetLocation`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryGetSelectionTransitTicks_StarfighterToEnemyBlockadedPlanet_ReturnsFalse`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryGetTransitTicks_FleetWithUnfinishedSlowerShip_IgnoresUnfinishedShip`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryGetTransitTicks_ValidDestination_DoesNotMoveUnit`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_CapitalShipInMovingFleet_PreservesSourceGraph`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_CapitalShipToFleet_RemovesEmptySourceFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_CapitalShipToPlanet_CreatesDestinationFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_CapitalShipUnderConstruction_RetargetsDelivery`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_CapitalShipsAtDifferentPlanets_PreservesSourceFleets`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_FleetToFleet_MovesShipsAndRemovesSourceFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_FleetWithOnlyShipsUnderConstruction_RetargetsDelivery`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_FleetWithQueuedWaypoints_ReplacesRoute`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_GroupUnderConstructionExceedsCapacity_NoneRetarget`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_MultipleCapitalShipsToPlanet_CreatesOneDestinationFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_RegimentToEnemyBlockadedPlanet_ReturnsFalse`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_RegimentToShip_ReturnsGarrisonChange`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_SnapshotPlanet_CreatesFleetOnLiveDestination`
- `Rebellion.Tests.Sectors.MovementSystemTests.TryRequestMove_StarfighterToEnemyBlockadedPlanet_ReturnsFalse`
- `Rebellion.Tests.Sectors.MovementSystemTests.TrySetFleetWaypointRoute_CapitalShipUnderConstruction_PreservesRouteUntilComplete`
- `Rebellion.Tests.Sectors.MovementSystemTests.TrySetFleetWaypointRoute_CapitalShip_CreatesFleetAndCompletesRoute`
- `Rebellion.Tests.Sectors.MovementSystemTests.TrySetFleetWaypointRoute_FleetAlreadyMoving_QueuesContinuation`
- `Rebellion.Tests.Sectors.MovementSystemTests.TrySetFleetWaypointRoute_MultipleDestinations_ContinuesRouteAfterArrival`
- `Rebellion.Tests.Sectors.MovementSystemTests.TrySetFleetWaypointRoute_OpposingFleet_ReturnsFalse`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_BlockadeEndsBeforeManufacturedBuildingArrival_CompletesArrival`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_BuildingInTransitDestinationChangedSides_BuildingDestroyed`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_FleetInTransitToHostilePlanet_FleetArrivesAtHostilePlanet`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_FleetMovesBeforeUnitArrives_UnitStillEnRoute`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_GroupNonCapturedUnits_PreservesMovementGroupIDInArrivalResults`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_InTransitFleetWithInTransitChildren_ChildrenArriveAfterFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_InTransitFleetWithInTransitChildren_FleetArrivesBeforeChildren`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_InTransit_IncrementsElapsedTicks`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_ManufacturedBuildingDispatchedAfterBlockadeStarted_DestroysOnArrival`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_ManufacturedRegimentDispatchedAfterBlockadeStarted_DestroysOnArrival`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_NonBuildingInTransitDestinationChangedSides_UnitRerouted`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_OfficerArrivesAtMission_ClearsMovementState`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_OnArrival_ClearsMovementState`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_OnArrival_PreservesMovementGroupIDInArrivalResult`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_OnArrival_UnitRemainsAtDestination`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_RegimentInTransitToFriendlyFleetAtHostilePlanet_ArrivesInFleet`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_SpecialForcesArrivesAtMission_ClearsRoleEnrouteState`
- `Rebellion.Tests.Sectors.MovementSystemTests.UpdateMovement_WhenNotInTransit_DoesNothing`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_CaptureByShipAwayFromCaptorPlanet_BoardsCapturingShip`
- `Rebellion.Tests.Systems.CaptiveSystemTests.HandleResults_CustodyTransferArrives_DoesNotRefreshCaptureSnapshot`
- `Rebellion.Tests.Systems.ManufacturingSystemTests.ProcessTick_BuildingForOwnedUncolonizedPlanet_ColonizesOnArrival`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.HandleResults_LastStationedRegiment_PreservesMissionForLifecycleValidation`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.HandleResults_LastStationedRegiment_ReconcilesControl`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_ReleaseToNeutral_HiddenObserverSnapshot_NotRefreshed`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_ReleaseToNeutral_PreviousOwnerSnapshot_Refreshed`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_UncolonizedOwnedPlanetWithoutRegiments_ReleasesToNeutral`

## Rebellion.Systems.NamingSystem

Source: [Assets/Scripts/Systems/NamingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/NamingSystem.cs#L11).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.NamingSystem.NamingSystem(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/NamingSystem.cs#L19) | Public | NamingCommands implementation / local helper |
| [`Rebellion.Systems.NamingSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/NamingSystem.cs#L27) | Public | NamingCommands implementation / local helper |
| [`Rebellion.Systems.NamingSystem.ProcessFaction(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/NamingSystem.cs#L38) | Public | NamingCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/NamingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/NamingSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.NamingSystemTests.ProcessFaction_AlreadyNamedShip_DoesNotReplaceName`
- `Rebellion.Tests.Systems.NamingSystemTests.ProcessFaction_EligibleShips_AssignsSequentialNames`
- `Rebellion.Tests.Systems.NamingSystemTests.ProcessFaction_ExhaustedPools_AssignsGenericName`
- `Rebellion.Tests.Systems.NamingSystemTests.ProcessFaction_MoreThanTenEligibleShips_AssignsAllNames`
- `Rebellion.Tests.Systems.NamingSystemTests.ProcessFaction_NullFaction_ThrowsArgumentNullException`
- `Rebellion.Tests.Systems.NamingSystemTests.ProcessFaction_PlayerFactionWithManagement_AssignsName`
- `Rebellion.Tests.Systems.NamingSystemTests.ProcessFaction_PlayerFactionWithoutManagement_DoesNotAssignName`
- `Rebellion.Tests.Systems.NamingSystemTests.ProcessFaction_ShipUnderConstruction_DoesNotAssignName`
- `Rebellion.Tests.Systems.NamingSystemTests.ProcessFaction_ShipWithoutNamePool_DoesNotAssignName`
- `Rebellion.Tests.Systems.NamingSystemTests.ProcessTick_AIControlledFaction_AssignsName`

## Rebellion.Systems.OfficerLoyaltySystem

Source: [Assets/Scripts/Systems/OfficerLoyaltySystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L17).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.OfficerLoyaltySystem.OfficerLoyaltySystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L27) | Public | OfficerLoyaltyCommands implementation / local helper |
| [`Rebellion.Systems.OfficerLoyaltySystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.PlanetOwnershipChangedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L38) | Public | Typed reaction/settled callback delegates operations to OfficerLoyaltyCommands |
| [`Rebellion.Systems.OfficerLoyaltySystem.TryResolveMissionBetrayal(Rebellion.Game.Missions.Mission, out System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L57) | Public | OfficerLoyaltyCommands implementation / local helper |
| [`Rebellion.Systems.OfficerLoyaltySystem.FindBetrayingOfficer(Rebellion.Game.Missions.Mission)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L79) | Private | OfficerLoyaltyCommands implementation / local helper |
| [`Rebellion.Systems.OfficerLoyaltySystem.FindOfficerWhoDiscoversBetrayal(Rebellion.Game.Missions.Mission, Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L88) | Private | OfficerLoyaltyCommands implementation / local helper |
| [`Rebellion.Systems.OfficerLoyaltySystem.BetraysMission(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L101) | Private | OfficerLoyaltyCommands implementation / local helper |
| [`Rebellion.Systems.OfficerLoyaltySystem.CanDiscoverMissionBetrayal(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L123) | Private | OfficerLoyaltyCommands implementation / local helper |
| [`Rebellion.Systems.OfficerLoyaltySystem.RevealTraitor(Rebellion.Game.Missions.Mission, Rebellion.Game.Units.Officer, Rebellion.Game.Units.Officer, System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L133) | Private | OfficerLoyaltyCommands implementation / local helper |
| [`Rebellion.Systems.OfficerLoyaltySystem.ApplyIncomingControlLoyaltyShift(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L156) | Private | OfficerLoyaltyCommands implementation / local helper |
| [`Rebellion.Systems.OfficerLoyaltySystem.IsFreeLivingOfficer(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs#L188) | Private | OfficerLoyaltyCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/MissionSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs)
- [Assets/Scripts/Systems/OfficerLoyaltySystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/OfficerLoyaltySystem.cs)

Direct test references:

- `Rebellion.Tests.Sectors.OfficerLoyaltySystemTests.HandleResults_FactionGainsPlanet_ShiftsOnlyFreeLivingOfficerLoyalty`
- `Rebellion.Tests.Sectors.OfficerLoyaltySystemTests.TryResolveMissionBetrayal_BoundaryRoll_UsesOneHundredMinusLoyalty`
- `Rebellion.Tests.Sectors.OfficerLoyaltySystemTests.TryResolveMissionBetrayal_CommandOfficer_DoesNotBetray`
- `Rebellion.Tests.Sectors.OfficerLoyaltySystemTests.TryResolveMissionBetrayal_ForceCapableCompanion_DiscoversTraitor`
- `Rebellion.Tests.Sectors.OfficerLoyaltySystemTests.TryResolveMissionBetrayal_LowLoyaltyOfficer_FoilsWithoutRevealingIdentity`

## Rebellion.Systems.PersonnelSystem

Source: [Assets/Scripts/Systems/PersonnelSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PersonnelSystem.cs#L12).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.PersonnelSystem.PersonnelSystem(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PersonnelSystem.cs#L20) | Public | PersonnelCommands implementation / local helper |
| [`Rebellion.Systems.PersonnelSystem.KillOfficer(Rebellion.Game.Units.Officer)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PersonnelSystem.cs#L29) | Public | PersonnelCommands implementation / local helper |
| [`Rebellion.Systems.PersonnelSystem.CanRetire(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PersonnelSystem.cs#L45) | Public | PersonnelQueries (read rule / preview) |
| [`Rebellion.Systems.PersonnelSystem.Retire(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PersonnelSystem.cs#L56) | Public | PersonnelCommands implementation / local helper |
| [`Rebellion.Systems.PersonnelSystem.TryResolveRetirementSelection(System.Collections.Generic.IReadOnlyList<Rebellion.SceneGraph.ISceneNode>, string, out System.Collections.Generic.List<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PersonnelSystem.cs#L86) | Private | PersonnelQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.PersonnelSystem.CanRetirePerson(Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PersonnelSystem.cs#L126) | Private | PersonnelQueries helper candidate; verify shared callers/purity |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/BombardmentSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs)
- [Assets/Scripts/Systems/MissionSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs)
- [Assets/Scripts/Systems/PersonnelSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PersonnelSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Windows/StrategyWindowCommandController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Windows/StrategyWindowCommandController.cs)

Direct test references:

- `Rebellion.Tests.Game.Events.SceneConditionsTests.IsKilled_InactiveKilledOfficer_MatchesByRegisteredIdentity`
- `Rebellion.Tests.Sectors.PersonnelSystemTests.CanRetire_BlockedPersonnel_ReturnsFalse`
- `Rebellion.Tests.Sectors.PersonnelSystemTests.CanRetire_OwnedOfficerAndSpecialForces_ReturnsTrue`
- `Rebellion.Tests.Sectors.PersonnelSystemTests.CanRetire_SnapshotSelection_ResolvesLivePersonnel`
- `Rebellion.Tests.Sectors.PersonnelSystemTests.KillOfficer_ActiveOfficer_MarksKilledAndRetainsIdentity`
- `Rebellion.Tests.Sectors.PersonnelSystemTests.Retire_InvalidMember_PreservesCompleteSelection`
- `Rebellion.Tests.Sectors.PersonnelSystemTests.Retire_OwnedPersonnel_RemovesCompleteSelection`
- `Rebellion.Tests.Sectors.PersonnelSystemTests.Retire_UnauthorizedOwner_PreservesPersonnel`

## Rebellion.Systems.PlanetaryAssaultSystem

Source: [Assets/Scripts/Systems/PlanetaryAssaultSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs#L18).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.PlanetaryAssaultSystem.PlanetaryAssaultSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Systems.PlanetaryControlSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs#L35) | Public | PlanetaryAssaultCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryAssaultSystem.TryExecute(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs#L52) | Public | PlanetaryAssaultCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryAssaultSystem.Execute(System.Collections.Generic.List<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs#L81) | Public | PlanetaryAssaultCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryAssaultSystem.CanExecute(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs#L160) | Public | PlanetaryAssaultQueries (read rule / preview) |
| [`Rebellion.Systems.PlanetaryAssaultSystem.CanAssault(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs#L176) | Private | PlanetaryAssaultQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.PlanetaryAssaultSystem.ApplyResult(Rebellion.Game.Results.PlanetaryAssaultResult, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs#L202) | Private | PlanetaryAssaultCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryAssaultSystem.RecordUnitOutcomes(Rebellion.Game.Results.PlanetaryAssaultResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs#L226) | Private | PlanetaryAssaultCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryAssaultSystem.SetAssaultCombatState(System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs#L248) | Private | PlanetaryAssaultCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/AI/Proposals/AIFleetAttackProposal.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Proposals/AIFleetAttackProposal.cs)
- [Assets/Scripts/Systems/PlanetaryAssaultSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Shared/StrategyFleetCommandController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Shared/StrategyFleetCommandController.cs)

Direct test references:

- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.CanExecute_NeutralPlanetWithReadyRegiment_ReturnsTrue`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.CanExecute_ShieldedTargetOrNoReadyRegiments_ReturnsFalse`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.CanExecute_TwoReadyAndSixMovingRegiments_UsesReadyRegiments`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_AttackersDestroyed_DoesNotCapturePlanet`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_AttackingFleetWithWaypoints_ClearsRoute`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_CaptureWithFewerTroops_LandsEverySurvivor`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_Capture_LandsAtMostRequiredGarrison`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_CollateralDamage_CanDestroyCivilianFacilityAndExcludesHeadquarters`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_CollateralDamage_RollsAllTrialsBeforeSelectingTargets`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_ContestScore_UsesSourceThresholds`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_DeathStarShield_DoesNotBlockAssault`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_DefenseFire_UsesInitialAttackerIndexRange`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_EachTroop_UsesGeneralFromItsOwnFleet`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_RngFailure_ClearsFleetCombatState`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.Execute_TwoShieldGenerators_BlockAssault`
- `Rebellion.Tests.Systems.PlanetaryAssaultSystemTests.TryExecute_ValidCommand_PublishesCompletedResultBatch`

## Rebellion.Systems.PlanetaryControlSystem

Source: [Assets/Scripts/Systems/PlanetaryControlSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L17).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.PlanetaryControlSystem.PlanetaryControlSystem(Rebellion.Game.GameRoot, Rebellion.Systems.MovementSystem, Rebellion.Systems.ManufacturingSystem, Rebellion.Systems.FogOfWarSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L37) | Public | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L54) | Public | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.UpdateBlockadeSupport()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L67) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.UpdateBlockadeSupport(Rebellion.Game.Galaxy.Planet, Rebellion.Game.GameConfig.SupportShiftConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L81) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.GetBlockadingFaction(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L130) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.TryGetFavoredFaction(Rebellion.Game.Galaxy.Planet, out Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L148) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.GetBlockadeSupportInterval(Rebellion.Game.GameConfig.SupportShiftConfig, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L175) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.ResetBlockadeSupportTimer(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L189) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.ScheduleBlockadeSupport(Rebellion.Game.Galaxy.Planet, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L200) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.PlanetGarrisonChangedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L211) | Public | Typed reaction/settled callback delegates operations to PlanetaryControlCommands |
| [`Rebellion.Systems.PlanetaryControlSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.PopularSupportShiftResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L232) | Public | Typed reaction/settled callback delegates operations to PlanetaryControlCommands |
| [`Rebellion.Systems.PlanetaryControlSystem.Rebellion.Systems.IGameRequestHandler<Rebellion.Game.Requests.OwnershipChangeRequest>.HandleRequests(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Requests.OwnershipChangeRequest>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L293) | Private | PlanetaryControlCommands operation; executor retains deferred timing and failure containment |
| [`Rebellion.Systems.PlanetaryControlSystem.ReconcilePlanet(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L331) | Public | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.UpdateUncolonizedPlanets(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L368) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.UpdateUncolonizedPlanet(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L379) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.TransferPlanet(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L399) | Public | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.ClearPlanetOwnership(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L412) | Public | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.ResolveBombardmentControl(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L431) | Internal | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.ShiftBombardmentSupport(System.Collections.Generic.IEnumerable<Rebellion.Game.Galaxy.Planet>, Rebellion.Game.Factions.Faction, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L471) | Internal | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.ShiftPopularSupport(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L528) | Internal | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.ApplyCoreSupportResistance(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L564) | Internal | PlanetaryControlQueries (read rule / preview) |
| [`Rebellion.Systems.PlanetaryControlSystem.ChangePlanetControl(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L593) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.ApplyPlanetOwnershipChange(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L607) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.GetSupportController(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L662) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.GetPlanetController(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L677) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.GetActiveRegimentOwners(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L687) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.GetPlanetController(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<string>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L707) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.CanApplyControlSupportShift(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L723) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.EnqueueSupportShifts(System.Collections.Generic.Queue<(Rebellion.Game.Galaxy.Planet planet, Rebellion.Game.Factions.Faction faction, int shift)>, System.Collections.Generic.IEnumerable<Rebellion.Game.Galaxy.Planet>, Rebellion.Game.Factions.Faction, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L744) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.GetAffectedPlanets(Rebellion.Game.Galaxy.PlanetSector)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L763) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.CheckOwnershipTransfers(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L775) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.CanTransferByPopularSupport(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L808) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.CreateOwnershipChangedResult(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, Rebellion.Game.Factions.Faction, System.Collections.Generic.IEnumerable<Rebellion.Game.Factions.Faction>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L823) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.GetOwnershipChangeObservers(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L850) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.CaptureOwnershipChange(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.IEnumerable<Rebellion.Game.Factions.Faction>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L878) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.CaptureSnapshotForFaction(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L892) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.TransferBuildings(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L909) | Private | PlanetaryControlCommands implementation / local helper |
| [`Rebellion.Systems.PlanetaryControlSystem.EvictEnemyUnits(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs#L925) | Private | PlanetaryControlCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/BombardmentSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/BombardmentSystem.cs)
- [Assets/Scripts/Systems/PlanetaryAssaultSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryAssaultSystem.cs)
- [Assets/Scripts/Systems/PlanetaryControlSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/PlanetaryControlSystem.cs)
- [Assets/Scripts/Systems/UprisingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ClearPlanetOwnership_ActiveDiplomacyMission_PreservesMission`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ClearPlanetOwnership_PlanetWithManufacturingQueue_DestroysQueuedUnit`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.HandleResults_CorePopularSupportShift_AppliesResistanceAndReportsChange`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.HandleResults_LastStationedRegiment_PreservesMissionForLifecycleValidation`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.HandleResults_LastStationedRegiment_ReconcilesControl`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.HandleResults_SupportCrossesThreshold_ReportsPopularSupportOwnershipChange`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_BlockadeAtTiedSupport_AppliesOpposingShift`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_BlockadeFleetMatchesFavoredSide_IncreasesFleetSupport`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_BlockadeFleetOpposesFavoredSide_ShiftsTowardFavoredSide`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_BlockadeReinforcesWeakCoreAllianceSupport_DoesNotShift`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_NeutralPlanetAboveThreshold_TransfersOwnership`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_NeutralPlanetBelowThreshold_DoesNotTransferOwnership`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_NeutralPlanetWithRegiments_DoesNotTransferOwnership`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_OwnedPlanetWithSupport_DoesNotShiftPopularSupport`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_PopularSupportTransfer_SetsOwnershipChangeReason`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_ReleaseToNeutral_HiddenObserverSnapshot_NotRefreshed`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_ReleaseToNeutral_PreviousOwnerSnapshot_Refreshed`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_UncolonizedNeutralPlanetWithRegiment_DoesNotClaimWithoutFleetDrop`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_UncolonizedOwnedPlanetWithoutRegiments_ReleasesToNeutral`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ProcessTick_UncolonizedPlanetAboveThreshold_DoesNotTransferOwnership`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ReconcilePlanet_ColonizedNeutralPlanetWithRegiment_TransfersToRegimentOwner`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ReconcilePlanet_ColonizedPlanetLosesLastRegiment_BecomesNeutralWithoutControllingSupport`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ReconcilePlanet_ColonizedPlanetWithoutGarrison_TransfersToSupportController`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.ReconcilePlanet_UncolonizedNeutralPlanetWithRegiment_DoesNotClaim`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_BuildingAtPlanet_BuildingNotEvicted`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_CoreObserverSnapshot_RefreshesOwnershipOnly`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_Default_PreservesRegimentOrderAssignedToFriendlyFleet`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_EnemyOfficerWithNoReachableDestination_OfficerCaptured`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_EnemyRegimentWithNoReachableDestination_DestroysRegiment`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_EvictedOfficer_DoesNotChangeOfficerOwner`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_FleetAtPlanet_FleetNotEvicted`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_FleetAtPlanet_FleetOwnershipUnchanged`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_HiddenObserverSnapshot_NotRefreshed`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_InTransitFleetAtPlanet_FleetNotRedirected`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_InTransitOfficerDestinedForPlanet_EvictsOfficer`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_InactivePlanet_TransfersRetainedBuildingsToNewOwner`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_MixedRemoteOrders_CancelsDestinationAndPreservesOthers`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_OuterRimVisibleObserverSnapshot_RefreshesOwnershipOnly`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithActiveMission_DoesNotMoveMissionParticipants`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithActiveMissions_PreservesMissionsForLifecycleValidation`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithEnemyOfficers_EvictsEnemyOfficers`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithEnemyRegiments_EvictsEnemyRegiments`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithEnemyStarfighters_DestroysEnemyStarfighters`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithInProgressBuilding_ClearsInProgressBuilding`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithManufacturingQueues_ClearsQueues`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithNewOwnerDiplomacyMission_DoesNotCancelIt`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithNewOwnerFleets_DoesNotEvictNewOwnerFleets`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PlanetWithNewOwnerMissions_DoesNotCancelThem`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_PreviousOwnerSnapshot_Refreshed`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_RedirectsInTransitOfficer_OriginIsCurrentPosition`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_TransfersBuildings_ToNewOwner`
- `Rebellion.Tests.Systems.PlanetaryControlSystemTests.TransferPlanet_ValidTransfer_ChangesPlanetOwner`

## Rebellion.Systems.RecoverySystem

Source: [Assets/Scripts/Systems/RecoverySystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/RecoverySystem.cs#L14).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.RecoverySystem.RecoverySystem(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/RecoverySystem.cs#L23) | Public | RecoveryCommands implementation / local helper |
| [`Rebellion.Systems.RecoverySystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/RecoverySystem.cs#L33) | Public | RecoveryCommands implementation / local helper |
| [`Rebellion.Systems.RecoverySystem.HealOfficers(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/RecoverySystem.cs#L49) | Private | RecoveryCommands implementation / local helper |
| [`Rebellion.Systems.RecoverySystem.RepairShips(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/RecoverySystem.cs#L81) | Private | RecoveryCommands implementation / local helper |
| [`Rebellion.Systems.RecoverySystem.ReplaceSquadronLosses(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/RecoverySystem.cs#L119) | Private | RecoveryCommands implementation / local helper |
| [`Rebellion.Systems.RecoverySystem.IsAtFriendlyPlanet(Rebellion.SceneGraph.ISceneNode)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/RecoverySystem.cs#L155) | Private | RecoveryCommands implementation / local helper |
| [`Rebellion.Systems.RecoverySystem.IsAtFriendlyShipyard(Rebellion.Game.Units.CapitalShip)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/RecoverySystem.cs#L166) | Private | RecoveryCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/RecoverySystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/RecoverySystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_CapturedOfficer_DoesNotHeal`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_DamagedShipAtEnemyPlanet_RepairsSlowly`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_DamagedShipAtFriendlyPlanetWithoutShipyard_RepairsSlowly`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_DamagedShipAtFriendlyShipyard_RepairsFast`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_DamagedShipAtIncompleteFriendlyShipyard_RepairsSlowly`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_DamagedShipInTransit_DoesNotRepair`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_DamagedShipWithDirectTransit_DoesNotRepair`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_DepletedSquadronAtEnemyPlanet_ReplacesSlowly`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_DepletedSquadronAtFriendlyPlanet_ReplacesFast`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_FullSquadron_NoChange`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_HealingClampsToZero_DoesNotGoNegative`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_InjuredHighRankForceSensitiveOfficer_HealsMorePerTick`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_InjuredLowRankForceSensitiveOfficer_HealsNormally`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_InjuredOfficerAboardFriendlyFleet_ReducesInjury`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_InjuredOfficerAtEnemyPlanet_DoesNotHeal`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_InjuredOfficerAtFriendlyPlanet_ReducesInjury`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_OfficerAboardFleetInTransit_DoesNotHeal`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_OfficerFullyHealed_EmitsResult`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_OfficerInTransit_DoesNotHeal`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_OfficerOnMission_DoesNotHeal`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_OfficerPartiallyHealed_NoResult`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_RepairClampsToMax_DoesNotExceedHull`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_ReplacementClampsToMax_DoesNotExceedSquadronSize`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_ShipAtMaxHull_NoChange`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_ShipFullyRepaired_EmitsResult`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_ShipPartiallyRepaired_NoResult`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_ShipUnderConstruction_NotRepaired`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_SquadronFullyReplaced_EmitsResult`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_SquadronPartiallyReplaced_NoResult`
- `Rebellion.Tests.Systems.RecoverySystemTests.ProcessTick_SquadronUnderConstruction_NotReplaced`

## Rebellion.Systems.ResearchSystem

Source: [Assets/Scripts/Systems/ResearchSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResearchSystem.cs#L16).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.ResearchSystem.ResearchSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResearchSystem.cs#L33) | Public | ResearchCommands implementation / local helper |
| [`Rebellion.Systems.ResearchSystem.InitializeResearchTimers()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResearchSystem.cs#L44) | Private | ResearchCommands implementation / local helper |
| [`Rebellion.Systems.ResearchSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResearchSystem.cs#L59) | Public | ResearchCommands implementation / local helper |
| [`Rebellion.Systems.ResearchSystem.RefreshResearchCapacity(Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResearchSystem.cs#L87) | Private | ResearchCommands implementation / local helper |
| [`Rebellion.Systems.ResearchSystem.CountCompleteFacilities(System.Collections.Generic.List<Rebellion.Game.Galaxy.Planet>, Rebellion.Game.Research.ResearchDiscipline)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResearchSystem.cs#L139) | Private | ResearchCommands implementation / local helper |
| [`Rebellion.Systems.ResearchSystem.RollRefreshDelay(Rebellion.Game.GameConfig.ResearchConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResearchSystem.cs#L163) | Private | ResearchCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/ResearchSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResearchSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.ResearchSystemTests.ProcessTick_BusyFacility_StillAddsCapacity`
- `Rebellion.Tests.Systems.ResearchSystemTests.ProcessTick_CoreSectorFacilityAcrossMultiplePulses_AccumulatesCapacity`
- `Rebellion.Tests.Systems.ResearchSystemTests.ProcessTick_FacilityInTransit_DoesNotAddCapacity`
- `Rebellion.Tests.Systems.ResearchSystemTests.ProcessTick_FacilityUnderConstruction_DoesNotAddCapacity`
- `Rebellion.Tests.Systems.ResearchSystemTests.ProcessTick_MultipleCoreSectorFacilities_AddsAll`
- `Rebellion.Tests.Systems.ResearchSystemTests.ProcessTick_MultipleFactions_IndependentCapacity`
- `Rebellion.Tests.Systems.ResearchSystemTests.ProcessTick_NoFacilities_NoCapacity`
- `Rebellion.Tests.Systems.ResearchSystemTests.ProcessTick_OneCoreSectorShipyard_AddsOneCapacity`
- `Rebellion.Tests.Systems.ResearchSystemTests.ProcessTick_OuterRimFacility_DoesNotAddCapacity`
- `Rebellion.Tests.Systems.ResearchSystemTests.ProcessTick_PulseNotReached_DoesNotAddCapacity`

## Rebellion.Systems.ResourceProductionSystem

Source: [Assets/Scripts/Systems/ResourceProductionSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L15).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.ResourceProductionSystem.ResourceProductionSystem(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L26) | Public | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L36) | Public | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.ProcessFaction(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L50) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.ServicePendingRawMaterialRequests(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L77) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.ServicePendingRefinedMaterialRequests(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L90) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.ServicePendingMaterialRequests(System.Collections.Generic.List<string>, int, System.Func<string, Rebellion.Game.Units.Building>, System.Func<Rebellion.Game.Units.Building, int>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L107) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.GetPendingFacility(Rebellion.Game.Factions.Faction, string, Rebellion.Game.Units.BuildingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L144) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.GetPendingProductionFacility(Rebellion.Game.Factions.Faction, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L163) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.HasQueuedProduction(Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L180) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.IsPendingFacilityValid(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L199) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.GetActiveResourceFacilities(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.BuildingType)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L216) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.RebalanceResourceAllocations(System.Collections.Generic.List<Rebellion.Game.Units.Building>, int, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L250) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.IncreaseResourceAllocations(System.Collections.Generic.List<Rebellion.Game.Units.Building>, int, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L301) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.DecreaseResourceAllocations(System.Collections.Generic.List<Rebellion.Game.Units.Building>, int, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L346) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.ProcessMine(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L382) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.ProcessRefinery(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L399) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.AdvanceResourceCycle(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L422) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.CalculateResourceCycleDuration(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L447) | Private | ResourceProductionCommands implementation / local helper |
| [`Rebellion.Systems.ResourceProductionSystem.DivideRoundingUp(int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs#L479) | Private | ResourceProductionCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/ResourceProductionSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_BlockadedPlanetWithKdy_DoesNotAdvanceResourceCycle`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_CompletedMineCycle_ServicesSuspendedRefineryInSameTick`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_CompletedRefineryCycle_ServicesProductionFacilityInSameTick`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_LowerPopularSupport_ExtendsResourceCycle`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_MaintenanceAllocationAcrossMultipleMines_ReachesDemand`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_MaintenanceAllocation_ExtendsResourceCycle`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_MineAndRefineryOnDifferentPlanets_ShareMaintenanceDemand`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_MineStartupCycle_ProducesAfterStartupDuration`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_PendingProductionFacilityWithoutQueue_DoesNotConsumeMaterial`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_PendingProductionFacility_ReceivesAvailableRefinedMaterial`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_PlanetInUprising_DoesNotAdvanceResourceCycle`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_RefineriesWaitingForRawMaterial_AreServicedInRequestOrder`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_SmugglingRoll_RedirectsCompletedRefinedResourceToBeneficiary`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_SmugglingRoll_RedirectsCompletedResourceToBeneficiary`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_SuspendedProductionFacilityWithPendingRequest_ReservesAvailableRefinedMaterial`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_SuspendedRefineryWithPendingRequest_ReservesAvailableRawMaterial`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTick_SuspendedResourceFacility_PreservesAllocationAndResumesCycle`
- `Rebellion.Tests.Systems.ResourceProductionSystemTests.ProcessTicks`

## Rebellion.Systems.SmugglingSystem

Source: [Assets/Scripts/Systems/SmugglingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L16).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.SmugglingSystem.SmugglingSystem(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L38) | Public | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.EnsureConfigIsValid(Rebellion.Game.GameConfig.SmugglingConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L52) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.InitializeStates()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L84) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L112) | Public | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.ResolveProductionRecipient(Rebellion.Game.Factions.Faction, Rebellion.Game.Units.Building)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L135) | Public | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.RefreshPlanet(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L154) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.CalculateSmugglingState(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L188) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.IsSameRelationship(Rebellion.Systems.SmugglingSystem.PlanetSmugglingState, Rebellion.Systems.SmugglingSystem.PlanetSmugglingState)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L209) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.RecordSmugglingStarted(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>, Rebellion.Game.Galaxy.Planet, Rebellion.Systems.SmugglingSystem.PlanetSmugglingState)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L225) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.RecordSmugglingEnded(System.Collections.Generic.List<Rebellion.Game.Results.GameResult>, Rebellion.Game.Galaxy.Planet, Rebellion.Systems.SmugglingSystem.PlanetSmugglingState)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L251) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.RecordDiversionChanged(System.Collections.Generic.ICollection<Rebellion.Game.Results.GameResult>, Rebellion.Game.Galaxy.Planet, Rebellion.Systems.SmugglingSystem.PlanetSmugglingState, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L279) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.CalculatePercent(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L307) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.GetStationedStarfighters(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Fleet>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L356) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.GetStationedRegiments(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.IEnumerable<Rebellion.Game.Units.Fleet>, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L378) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.FindBeneficiary(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L398) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.FindFaction(string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L416) | Private | SmugglingCommands implementation / local helper |
| [`Rebellion.Systems.SmugglingSystem.IsOperational(Rebellion.Game.Units.CapitalShip)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L429) | Private | SmugglingCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Systems/ResourceProductionSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/ResourceProductionSystem.cs)
- [Assets/Scripts/Systems/SmugglingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs)

Direct test references:

- `Rebellion.Tests.Sectors.SmugglingSystemTests.ProcessTick_ControlChanged_EndsOldSmugglingAndStartsNewRelationship`
- `Rebellion.Tests.Sectors.SmugglingSystemTests.ProcessTick_DiversionChangesWithinRelationship_OnlyReportsStatChange`
- `Rebellion.Tests.Sectors.SmugglingSystemTests.ProcessTick_ExistingSmugglingState_DoesNotRepeatStartNotification`
- `Rebellion.Tests.Sectors.SmugglingSystemTests.ProcessTick_GarrisonAndFleetPresence_ReduceSmugglingPercentage`
- `Rebellion.Tests.Sectors.SmugglingSystemTests.ProcessTick_LowSupport_StartsConfiguredSmugglingLossPercentage`
- `Rebellion.Tests.Sectors.SmugglingSystemTests.ProcessTick_PlanetDestroyingShipPresent_FullySuppressesSmuggling`

## Rebellion.Systems.SmugglingSystem.PlanetSmugglingState

Source: [Assets/Scripts/Systems/SmugglingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SmugglingSystem.cs#L27).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Systems.SpaceCombatDecision

Source: [Assets/Scripts/Systems/SpaceCombatSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L18).

No declared methods. Data/nested state type; no behavior extraction inferred.

## Rebellion.Systems.SpaceCombatSystem

Source: [Assets/Scripts/Systems/SpaceCombatSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L30).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.SpaceCombatSystem.TryGetPendingCombat(out Rebellion.Game.Results.PendingCombatResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L47) | Public | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.SpaceCombatSystem(Rebellion.Game.GameRoot, Rebellion.Systems.MovementSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L58) | Public | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L73) | Public | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.TryAutoResolveAICombat(Rebellion.Systems.SpaceCombatDecision, System.Collections.Generic.HashSet<string>, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L101) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.IsEncounterStillContested(Rebellion.Systems.SpaceCombatDecision)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L126) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.BothSidesAIControlled(Rebellion.Systems.SpaceCombatDecision)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L136) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.BuildPendingCombatResult(Rebellion.Systems.SpaceCombatDecision)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L151) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ResolvePending(bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L187) | Public | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ResolvePendingRetreat(string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L203) | Public | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.TryResolveRetreat(Rebellion.Systems.SpaceCombatDecision, string, out System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L230) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.BuildRetreatResult(Rebellion.Systems.SpaceCombatDecision, bool, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L300) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.TryBeginFleetCombat(System.Collections.Generic.HashSet<string>, out Rebellion.Systems.SpaceCombatDecision)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L344) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.TryFindContestedForces(System.Collections.Generic.HashSet<string>, out Rebellion.Game.Galaxy.Planet, out string, out string, out System.Collections.Generic.List<Rebellion.Game.Units.Fleet>, out System.Collections.Generic.List<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L392) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.Resolve(Rebellion.Systems.SpaceCombatDecision, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L470) | Internal | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ResolveAutomaticFleetEncounter(Rebellion.Systems.SpaceCombatDecision)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L485) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ResolveFleetEncounter(Rebellion.Systems.SpaceCombatDecision)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L495) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.UpdateCombatEncounterResultOutcomes(Rebellion.Game.Results.SpaceCombatResult, Rebellion.Systems.SpaceCombatDecision)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L525) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.GetLiveFighters(System.Collections.Generic.IEnumerable<Rebellion.Game.Results.CombatUnitSnapshot>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L571) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.UpdateCombatEncounterWinner(Rebellion.Game.Results.SpaceCombatResult)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L586) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.GetRetreatPlanetInstanceID(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Starfighter>, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Results.SpaceCombatSideOutcome)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L604) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.GetCombatSideOutcome(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Starfighter>, string, Rebellion.Game.Galaxy.Planet, Rebellion.Game.Results.SpaceCombatSideOutcome)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L633) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ClearCombatFlags(Rebellion.Systems.SpaceCombatDecision)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L675) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.CanRetreatForces(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L694) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.GetAutomaticWithdrawalGroups(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L726) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.HasHyperdriveCapableShip(Rebellion.Game.Units.Fleet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L770) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.TryRetreatFleet(Rebellion.Game.Units.Fleet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L780) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.TryRetreatFleets(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L806) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.IsRetreatBlockedByGravityWell(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L833) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.IsRetreatBlockedByGravityWell(Rebellion.Game.Galaxy.Planet, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L850) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ResolveCombatPlanet(Rebellion.Systems.SpaceCombatDecision)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L867) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.GetFleets(System.Collections.Generic.IEnumerable<string>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L887) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.GetRepresentativeFleet(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L900) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.AreFleetsContestingPlanet(Rebellion.Game.Units.Fleet, Rebellion.Game.Units.Fleet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L911) | Internal | SpaceCombatQueries (read rule / preview) |
| [`Rebellion.Systems.SpaceCombatSystem.AreForcesContestingPlanet(Rebellion.Systems.SpaceCombatDecision)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L933) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.HasActiveSpaceUnits(Rebellion.Game.Units.Fleet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L952) | Internal | SpaceCombatQueries (read rule / preview) |
| [`Rebellion.Systems.SpaceCombatSystem.HasActiveSpaceUnits(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L967) | Private | SpaceCombatQueries (read rule / preview) |
| [`Rebellion.Systems.SpaceCombatSystem.GetActiveCapitalShips(Rebellion.Game.Units.Fleet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L987) | Private | SpaceCombatQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.SpaceCombatSystem.GetActiveStarfighters(Rebellion.Game.Units.Fleet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1000) | Private | SpaceCombatQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.SpaceCombatSystem.GetActivePlanetStarfighters(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1016) | Private | SpaceCombatQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.SpaceCombatSystem.IsActiveCapitalShip(Rebellion.Game.Units.CapitalShip)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1039) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.IsActiveStarfighter(Rebellion.Game.Units.Starfighter)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1051) | Private | SpaceCombatQueries helper candidate; verify shared callers/purity |
| [`Rebellion.Systems.SpaceCombatSystem.ResolveCombat(Rebellion.Systems.SpaceCombatDecision, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1065) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.CompleteAutomaticWithdrawals(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.ISet<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1107) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.CompleteAutomaticWithdrawal(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.ISet<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1124) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.RunManualCombat()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1154) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ResolveSpace(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, string, string, Rebellion.Game.Galaxy.Planet, int, out System.Collections.Generic.HashSet<Rebellion.SceneGraph.ISceneNode>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1167) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.CaptureCombatUnits(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1247) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ApplyCombatResult(Rebellion.Game.Results.SpaceCombatResult, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1294) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ApplyCombatLosses(System.Collections.Generic.List<Rebellion.Game.Results.ShipDamageResult>, System.Collections.Generic.List<Rebellion.Game.Results.FighterLossResult>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>, System.Collections.Generic.IReadOnlyList<Rebellion.Game.Units.Fleet>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1316) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ApplyShipDamage(System.Collections.Generic.List<Rebellion.Game.Results.ShipDamageResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1345) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.ApplyFighterSquadronLosses(System.Collections.Generic.List<Rebellion.Game.Results.FighterLossResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1390) | Private | SpaceCombatCommands implementation / local helper |
| [`Rebellion.Systems.SpaceCombatSystem.RemoveFleetFromScene(Rebellion.Game.Units.Fleet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs#L1412) | Private | SpaceCombatCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/SpaceCombatSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/SpaceCombatSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Screen/StrategyController.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Screen/StrategyController.cs)

Direct test references:

- `Rebellion.Tests.Managers.GameManagerTests.ProcessTick_FleetArrivesAtPlanetaryStarfighters_CreatesPendingCombat`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_FleetWithdrawalInterruptedByVictory_KeepsEntireFleetAtCombatPlanet`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_FleetsWithOnlyInTransitShips_DoesNotRunCombat`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_MultipleEncountersAllAI_ResolvesAll`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_MultipleUnarmedFleetsWithRetreatDestinations_RetreatsEveryFleet`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_MultipleUnarmedFleetsWithoutRetreatDestinations_DestroysEveryFleetAndReportsEveryShip`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_PlayerFleetAgainstPlanetaryStarfighters_ReturnsPendingDecision`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_PlayerInvolvedEncounter_ClearsFleetWaypointRoutes`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_PlayerInvolvedEncounter_ReturnsPendingDecision`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_PlayerInvolvedEncounter_SetsRetreatAvailability`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_UnarmedAIFleets_RetreatsBoth`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_UnarmedFleetsWithoutRetreatDestinations_DestroysAndReportsBoth`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_UnfinishedPlanetaryStarfighters_DoNotTriggerCombat`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_WeakerAIFleetBlockedByGravityWell_Fights`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_WeakerAIFleetCanRetreat_MovesToFriendlyPlanet`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_WeakerAIFleetDestroyedDuringWithdrawal_RemovesFleet`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ProcessTick_WithInTransitFleet_IgnoresInTransitFleet`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolveCarrierDestructionWithdrawal`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePendingRetreat_FleetWithoutHyperdrive_DoesNotMoveFleet`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePendingRetreat_MultipleColocatedFleets_RetreatsEveryFleetAndReportsEveryShip`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePendingRetreat_PlanetaryHyperdriveFighterWithoutFleet_MovesFighterAndEndsCombat`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePendingRetreat_PlanetaryHyperdriveFighter_MovesFighterAndDoesNotRestartCombat`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePendingRetreat_PlanetaryNonHyperdriveFighter_DoesNotMoveAnyForces`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePendingRetreat_PlayerFleet_MovesToFriendlyPlanet`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePending_AutomaticWithdrawalFleetWithoutHyperdrive_DestroysFleet`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePending_CapitalShipAgainstPlanetaryFighter_DestroysFighter`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePending_FleetAndPlanetaryNonHyperdriveFightersWithdraw_DestroysStrandedFighters`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePending_MultipleColocatedFleets_DestroysEveryLosingFleetAndReportsEveryShip`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePending_MultipleColocatedFleets_ExcludesInTransitSiblingFleet`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePending_MultipleColocatedFleets_IncludesEveryFleet`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePending_PlanetaryHyperdriveFightersReachWithdrawalThreshold_WithdrawsFighters`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePending_PlanetaryNonHyperdriveFightersReachWithdrawalThreshold_DestroysFighters`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePending_PlanetaryStarfighters_ParticipateInCombat`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.ResolvePending_WithdrawingFleetCarriesNonHyperdriveFighter_PreservesFighter`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.Resolve_CombatWithSurvivors_ClearsIsInCombatOnSurvivingFleets`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.TryResolveCombat`
- `Rebellion.Tests.Systems.SpaceCombatSystemTests.TryRunCombat`

## Rebellion.Systems.UprisingSystem

Source: [Assets/Scripts/Systems/UprisingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L18).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.UprisingSystem.UprisingSystem(Rebellion.Game.GameRoot, Rebellion.Util.Random.IRandomNumberProvider, Rebellion.Systems.PlanetaryControlSystem)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L32) | Public | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L48) | Public | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.PlanetGarrisonChangedResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L77) | Public | Typed reaction/settled callback delegates operations to UprisingCommands |
| [`Rebellion.Systems.UprisingSystem.ReconcileGarrison(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L98) | Public | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.TryExecuteMission(Rebellion.Game.Missions.Mission, out System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L124) | Internal | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ExecuteMission(Rebellion.Game.Missions.Mission)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L141) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ResolveMissionAttempt(Rebellion.Game.Missions.Mission, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L175) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ResolveInciteMissionAttempt(Rebellion.Game.Missions.InciteUprisingMission, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L189) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ResolveSubdueMissionAttempt(Rebellion.Game.Missions.SubdueUprisingMission, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L226) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.RollSubdueSupportShift(Rebellion.Game.Missions.SubdueUprisingMission, Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L265) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.InitializeGarrisonSurplus()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L286) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.GetControllingFaction(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L302) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ReconcileGarrison(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L317) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.UpdateGarrisonSurplus(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L355) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.CalculateGarrisonSurplus(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L368) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.CountControllingRegiments(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L380) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ResolveActiveUprising(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L397) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ResolveSupportDriftPulse(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L435) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ResolveIncidentPulse(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L466) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ResolveClearPulse(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L493) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ResolveUprisingIncident(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L515) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.SynchronizeActiveUprising(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L549) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.SynchronizeUprisingClearTimer(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L578) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.IsSupportDriftNext(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L604) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.IsIncidentNext(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L621) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.IsClearNext(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L638) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.IsNextDueTimer(int, int, int, int, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L660) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.IsScheduledBefore(int, int, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L694) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ArmTimer(int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L715) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.AdvanceTimer(int, int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L727) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.RollTimerDelay(int, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L738) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ClaimTimerOrder(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L750) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ResolveUprisingTableResults(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, int, int, out int, out int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L765) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.GetUprisingTroopMultiplier(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L807) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.CalculateUprisingMissionAdjustment(Rebellion.Game.Galaxy.Planet, Rebellion.Game.GameConfig.UprisingConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L828) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.GetActiveUprisingMissions(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L860) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.HasActiveInciteMission(Rebellion.Game.Galaxy.Planet)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L877) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ApplyUprisingConsequence(Rebellion.Game.Galaxy.Planet, string, int, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L890) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.DestroyRandomBuilding(Rebellion.Game.Galaxy.Planet, string, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L923) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.DestroyRandomRegiment(Rebellion.Game.Galaxy.Planet, string, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L947) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.DestroyRandomUprisingTarget<T>(System.Collections.Generic.List<T>, Rebellion.Game.Galaxy.Planet, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L978) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.CaptureRandomOfficer(Rebellion.Game.Galaxy.Planet, string, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1007) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.FreeRandomCapturedOfficer(Rebellion.Game.Galaxy.Planet, string, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1032) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.FreeAllCapturedOfficers(Rebellion.Game.Galaxy.Planet, string, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1056) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.GetIncidentOfficers(Rebellion.Game.Galaxy.Planet, string, bool)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1078) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.SetOfficerCaptureState(Rebellion.Game.Units.Officer, Rebellion.Game.Galaxy.Planet, bool, string, System.Collections.Generic.List<Rebellion.Game.Results.GameResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1103) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ApplyUprisingControllerSupportShift(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1131) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.ApplyUprisingSupportShift(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1146) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.CalculateGarrisonRequirement(Rebellion.Game.Galaxy.Planet, Rebellion.Game.Factions.Faction, Rebellion.Game.GameConfig.GarrisonConfig)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1171) | Public | UprisingQueries (read rule / preview) |
| [`Rebellion.Systems.UprisingSystem.CalculateUprisingThreshold(int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1208) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.GetThresholdTableValue(System.Collections.Generic.Dictionary<int, int>, int)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1229) | Private | UprisingCommands implementation / local helper |
| [`Rebellion.Systems.UprisingSystem.FindLeadingOpposingFaction(Rebellion.Game.Galaxy.Planet, string)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs#L1249) | Private | UprisingCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Editor/Simulation/Reporting/FleetSimulationSummaryBuilder.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Editor/Simulation/Reporting/FleetSimulationSummaryBuilder.cs)
- [Assets/Editor/Simulation/Tracking/SimulationCombatTrackers.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Editor/Simulation/Tracking/SimulationCombatTrackers.cs)
- [Assets/Scripts/AI/Director/AIAssessment.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Director/AIAssessment.cs)
- [Assets/Scripts/AI/Planners/AIFleetPlanner.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Planners/AIFleetPlanner.cs)
- [Assets/Scripts/AI/Planners/AIProductionDemandGenerator.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/AI/Planners/AIProductionDemandGenerator.cs)
- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/FactionAutomationSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/FactionAutomationSystem.cs)
- [Assets/Scripts/Systems/MissionSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/MissionSystem.cs)
- [Assets/Scripts/Systems/UprisingSystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/UprisingSystem.cs)
- [Assets/Scripts/UI/SceneUI/StrategyView/Defense/DefenseWindowProjector.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/UI/SceneUI/StrategyView/Defense/DefenseWindowProjector.cs)

Direct test references:

- `Rebellion.Tests.Systems.GarrisonRequirementTests.CalculateGarrisonRequirement_CoreWorldAlliance_NotHalved`
- `Rebellion.Tests.Systems.GarrisonRequirementTests.CalculateGarrisonRequirement_CoreWorldEmpire_Halved`
- `Rebellion.Tests.Systems.GarrisonRequirementTests.CalculateGarrisonRequirement_EfficientCoreFactionBelowThreshold_RequiresOneTroop`
- `Rebellion.Tests.Systems.GarrisonRequirementTests.CalculateGarrisonRequirement_StandardPlanet_MatchesOriginalFormula`
- `Rebellion.Tests.Systems.UprisingSystemTests.HandleResults_GarrisonDeficit_StartsUprising`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_ActiveUprisingLastBuildingDestroyed_DoesNotChangeControl`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_ActiveUprisingWithFacility_DestroysFacility`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_ActiveUprisingZeroTroopsWithOpposingSupport_TransfersControl`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_ActiveUprising_CapturedOfficerFreed`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_ActiveUprising_OfficerCaptured`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_ActiveUprising_ZeroTroops_PlanetGoesNeutral`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_EmpireGarrisonOnCoreSector_HalvesRequirement`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_EmpireGarrisonOnOuterRim_NoBonus`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_ExactGarrison_NoUprising`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_GarrisonFallsToRequirement_ReportsNearUprisingOnce`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_HighSupport_NoUprising`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_Incident_AppliesInciteAndSubdueLeadershipAdjustments`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_Incident_CapturesOnlyUsableOfficer`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_Incident_ExcludesEnrouteRegiment`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_Incident_ExcludesIncompleteFacility`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_Incident_ExcludesMissionParticipantsInTransit`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_Incident_IgnoresHostileFleetPresence`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_NeutralPlanet_Skipped`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_NoGarrison_UprisingStarts`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_SufficientGarrison_NoUprising`
- `Rebellion.Tests.Systems.UprisingSystemTests.ProcessTick_SufficientUprisingGarrison_ClearsOnlyWhenTimerExpires`
- `Rebellion.Tests.Systems.UprisingSystemTests.ReconcileGarrison_CapturedPlanetAtRequirement_ReportsNearUprising`
- `Rebellion.Tests.Systems.UprisingSystemTests.ReconcileGarrison_DeficitReturnsBeforeClearPulse_CancelsClearTimer`
- `Rebellion.Tests.Systems.UprisingSystemTests.TryExecuteMission_OfficersOutOfScoreOrder_AttemptsLowestScoreFirst`

## Rebellion.Systems.VictorySystem

Source: [Assets/Scripts/Systems/VictorySystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/VictorySystem.cs#L16).

| Current member | Access | Mapped responsibility |
| --- | --- | --- |
| [`Rebellion.Systems.VictorySystem.VictorySystem(Rebellion.Game.GameRoot)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/VictorySystem.cs#L25) | Public | VictoryCommands implementation / local helper |
| [`Rebellion.Systems.VictorySystem.ProcessTick()`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/VictorySystem.cs#L34) | Public | VictoryCommands implementation / local helper |
| [`Rebellion.Systems.VictorySystem.HandleResults(System.Collections.Generic.IReadOnlyList<Rebellion.Game.Results.HeadquartersLostResult>)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/VictorySystem.cs#L59) | Public | Typed reaction/settled callback delegates operations to VictoryCommands |
| [`Rebellion.Systems.VictorySystem.CheckHQCapture(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/VictorySystem.cs#L77) | Private | VictoryCommands implementation / local helper |
| [`Rebellion.Systems.VictorySystem.CheckMobileHQCapture(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/VictorySystem.cs#L111) | Private | VictoryCommands implementation / local helper |
| [`Rebellion.Systems.VictorySystem.BuildHQVictory(Rebellion.Game.Factions.Faction, Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/VictorySystem.cs#L140) | Private | VictoryCommands implementation / local helper |
| [`Rebellion.Systems.VictorySystem.CheckAllMainCharactersCaptured(Rebellion.Game.Factions.Faction)`](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/VictorySystem.cs#L167) | Private | VictoryCommands implementation / local helper |

Direct production callers (including same-owner helpers):

- [Assets/Scripts/Managers/GameManager.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Managers/GameManager.cs)
- [Assets/Scripts/Systems/VictorySystem.cs](https://github.com/davidadas/rebellion2/blob/c7fc3011/Assets/Scripts/Systems/VictorySystem.cs)

Direct test references:

- `Rebellion.Tests.Systems.VictorySystemTests.HandleResults_HeadquartersCaptured_ReturnsVictory`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_AfterVictoryDeclared_DoesNotDeclareVictoryAgain`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_HQCapturedConquestMode_AllLeadersCaptured_ReturnsVictoryResult`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_HQCapturedConquestMode_LeadersFree_ReturnsEmpty`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_HQCapturedConquestMode_NoMainCharacters_ReturnsVictoryResult`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_HQCapturedHeadquartersMode_ReturnsVictoryResult`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_HQNotConfigured_ReturnsEmpty`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_HQStillOwnedByDefender_ReturnsEmpty`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_MobileHeadquartersCaptured_ReturnsVictoryResult`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_MobileHeadquartersInTransit_ReturnsEmpty`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_MobileHeadquartersMissing_ReturnsEmpty`
- `Rebellion.Tests.Systems.VictorySystemTests.ProcessTick_MultipleMobileHeadquarters_UsesDefenderHeadquarters`

