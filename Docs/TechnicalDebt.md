# Cleanup backlog

Record confirmed implementation issues here. Keep entries open until the fix and its validation are complete.

- [ ] **Replace the shield integration assembly workaround.**

  Our `TacticalShieldEffect` adapter lives under `Assets/FXV/RebellionIntegration/` instead of
  alongside tactical presentation code. The FXV package has no assembly definition, and
  `TacticalViewPrefabBuilder.BuildShieldTemplate` resolves our adapter through the string
  `TacticalShieldEffect, Assembly-CSharp`.

  Give FXV its own assembly definition, add the appropriate explicit reference from
  `Assets/Scripts/GameAssembly.asmdef`, move our adapter into
  `Assets/Scripts/UI/SceneUI/TacticalView/`, and use typed component creation in the builder.
  Keep the purchased package in the private media overlay.

  Verify assembly dependencies, regenerate the tactical UI, and run the complete formatting,
  lint, architecture, and Unity test checks before pushing the fix.

- [x] **Separate weapon definitions from emitted projectile data.**

  `WeaponData` owns firing cadence, volley/recharge settings, range, and firing audio.
  Its `ProjectileData` owns delivery, damage, travel, appearance, and impact audio.
  An emitted `BattleProjectile` captures only projectile properties and effective range.
  Projectile color uses `ColorHex`. Both factions' authored weapon definitions use this contract.

- [x] **Clarify ownership within the saved battle unit.**

  `CombatUnitData` captures combat capabilities without strategic production or travel fields.
  `CombatUnitCondition` is the sole owner of current hull, shields, squadron strength, and
  fractional damage/recharge. Movement, weapons, fighter flight, and carrier queues have
  separate saved state records in `CombatUnit.cs`. Source-unit identity is retained.
  Save format 2 migrates the previous ship-copy representation and pending orders on load.

- [x] **Establish a distinct home and runtime owner for tactical gameplay.**

  Saved state lives under `Game/Combat`, battle execution under `Tactical`, and Unity input
  and rendering under `UI/SceneUI/TacticalView`. `TacticalBattleSession` owns the movement,
  carrier, and combat update sequence, independently of the rendered-model collection.
  `TacticalViewController` loads assets, bridges scene timing, and presents the resulting state.
  Strategic tick processing and strategic combat resolution remain under `Simulation`.

- [ ] **Complete the active-battle save and resume route.**

  `GameRoot` serializes its `ActiveBattle`, and tests cover state round trips and combat
  continuation after direct deserialization. Production code does not yet call
  `GameRoot.SetActiveBattle`; the preview launch context is separate. The tactical load-menu
  handler and the content-loading destination both route saved games to `StrategyView`.

  Connect battle entry, saving, and scene restoration when integrating strategic battles.
  Recreate runtime services from saved battle state. Decide whether selected tactical speed
  should persist; the current runtime clock is not serialized and its selected speed resets.

- [x] **Separate resolved hitscan presentation lifetime from active projectile state.**

  `ActiveBattle.Projectiles` contains unresolved physical shots. Hitscan damage resolves
  immediately; `TacticalCombatView` keeps its short-lived beam visuals separately. Resolved
  beams are not restored on load. Save migration drops their obsolete effect-only records
  while preserving live physical shots.
