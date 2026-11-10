# Development

## Requirements

- Unity 6000.4.12f1
- .NET SDK
- A Bash-compatible shell
- Python 3.11 or later for desktop packaging
- Ownership of *Star Wars: Rebellion* or *Star Wars: Empire at War: Gold Pack* through GOG or Steam
- Access to the separate `rebellion2-media` repository

Access to development content does not grant redistribution rights. **NEVER share, upload, commit,
or otherwise redistribute `rebellion2-media`, `Assets/Content`, or any copyrighted game asset.**

## Setup

1. Clone `rebellion2` and `rebellion2-media` beside one another.
2. Copy `rebellion2-media/Content/` into `rebellion2/Assets/Content/`.
3. Copy `rebellion2-media/Models/MainMenu/` into
   `rebellion2/Assets/Art/Models/MainMenu/`.
4. Overlay `rebellion2-media/UnityAssets/` onto `rebellion2/Assets/`, preserving `.meta` files.
   This installs native Unity packages, including the main-menu planet, at their logical art
   paths.
5. Open `rebellion2` in Unity.
6. Run **Rebellion > Build > Build All UI**.

The copied content, models, Unity assets, all generated prefabs, and all generated scenes are local
development artifacts and are ignored by Git. This includes the boot scene and cutscene playback
assets. Manage the media checkout and refresh these local copies outside Unity.

CI installs clean media mirrors automatically with `rsync --delete` before Unity builds and tests.

## Commands

```bash
./build.sh format     # Check C# formatting
./build.sh xmlformat  # Format XML data
./build.sh lint       # Run static analysis
./build.sh test       # Run Unity EditMode tests
./build.sh coverage   # Run tests with coverage thresholds
./build.sh build      # Build and verify the game and content-pack editor
./build.sh clean      # Remove build artifacts
./build.sh all        # Run the complete local verification suite
```

Running `./build.sh` without a command is equivalent to `./build.sh all`.

Set `UNITY` if Unity is installed somewhere other than the detected platform default. Standalone
builds use the host platform unless `BUILD_TARGET` and `BUILD_PLAYER_PATH` are set:

```bash
BUILD_TARGET=StandaloneWindows64 BUILD_PLAYER_PATH=build/rebellion2.exe ./build.sh build
```

Player builds generate the UI through `StandalonePlayerBuild.Build` and verify that development
content under `Assets/Content/` was not embedded in the player.

## Content-pack editor and packaging

Windows and Linux builds include `rebellion2-editor` beside `rebellion2`. The macOS editor is a
helper app inside the game app's `Contents/Helpers` folder, so game installers and updates carry it
with the game. Both apps use the same external Content directory. Launch the editor to create or open
a pack in the installation's `Mods` folder. A new pack inherits its selected base; saving writes
only edited XML files and imported assets. Export creates a zip of those local files after
checking every declared scenario through the content loader, reference checks, campaign generation,
runtime initialization, and referenced media decoding. Progress shows the current check; Escape
cancels validation or export without replacing an existing archive.

Package a completed Windows build with:

```bash
python3 Tools/package_desktop.py --platform StandaloneWindows64 \
  --build-dir build/content-editor --output dist/rebellion2-windows.zip
```

Use `StandaloneLinux64` or `StandaloneOSX` and an output ending in `.tar.gz` for Linux or macOS.
Without `--output`, the command only verifies the build. The shared desktop layout requires Mono
players. Use `--game-name Rebellion2` for release builds that use that executable base name.
Set `PYTHON` when `build.sh` should use a
different Python executable.

`--stage-dir <new-folder>` creates the same clean runtime file tree for installer packaging without
making an archive. Release installers use this stage to exclude Unity debug output and local files.

Archives contain both players, their runtimes, native file dialogs, dependency notices, and a
`bundle.json` inventory with file hashes. A `.sha256` file accompanies each archive. Installed
content, local mods, and debug output are excluded. The installer must supply the existing external
`Content` folder beside the players; this archive does not replace the content distribution.

CI runs packaging tests during lint, then runs game tests and builds in parallel. Each build verifies
and uploads a desktop bundle; its presence does not mean the game tests have passed. These artifacts
are retained for seven days; creating them does not publish a release or update an installation.
## Code organization

`Game` contains game objects, authored configuration and instructions, and campaign state. These types can own behavior, including their own calculations and invariants. They depend only on `Game`, `SceneGraph`, and `Util` within this codebase.

`Gameplay` contains the operations, queries, calculations, and reactions used to run a campaign. Both player-facing controllers and AI use these services. Supporting request and result types stay with the feature that owns them.

| Area | Ownership |
| --- | --- |
| `Game/Scripting` | Authored action, conditional, selector, binding, and trigger instructions. |
| `Game/GameEvents` | Event definitions, schedules, and saved activation history. |
| `Gameplay/Scripting` | Interpretation, evaluation, temporary execution contexts, and gameplay action handling. |
| `Gameplay/GameEvents` | Event validation, activation, and campaign tick/result integration. |
| `Gameplay` (root) | Session construction, service registration, tick scheduling, and result delivery. |

Gameplay feature folders retain commands for operations, queries for inspection and estimates, tick processors for time-driven work, and observers for result-driven reactions. A supporting class does not need its own tick to belong to a gameplay feature.

Space combat offers two resolutions for a player-involved encounter. **Auto-Resolve** runs the
headless `SpaceCombatAutoResolver`. **Take Command** opens the interim tactical battle: a
`TacticalSpaceBattle` under `Gameplay/SpaceCombat/Tactical` that simulates on a flat plane and
reuses the auto-resolver's weapon-arc, shield, ion, recharge, and withdrawal rules while the
player selects capital ships, issues move and attack orders, retreats, or hands control to the
simulation. Fighters launch and engage on their own. The battle produces the same
`SpaceCombatResult` as auto-resolution and is applied through `SpaceCombatCommands`, so messages,
blockades, withdrawals, and fleet cleanup behave identically. Its presentation lives in
`UI/SceneUI/StrategyView/Tactical` as a hidden full-screen layer of the authored Strategy root prefab.
Units are drawn as 3D models by `TacticalBattleScene`: an off-canvas rig on the `TacticalBattle`
layer (25) whose perspective camera orbits a focus point on the battle plane and renders into the
arena image. The view opens at a fixed angle onto the battle plane from behind the player's side,
and the camera is locked to the play space: its focus stays inside the arena and it can back out
only far enough to frame the whole arena; it opens with both fleets in frame. The attacking fleet
drops out of hyperspace as the battle opens, decelerating into place from off the map, and ships
that withdraw accelerate away into hyperspace the same way; both are presentation only. The 2D
overlay (the bars stacked above capital-ship hulls, plus shots; fighters carry none) projects
through the same camera and clicks are cast back onto the plane, so picking stays exact. Every
selected unit, and an enemy unit the player clicks to inspect, is marked in the scene itself by a
wireframe box fitted to its hull and turning with it, drawn in its faction's color with lines
that keep one width on screen at any zoom. The wheel
or plus and minus zoom, right-drag orbits (Q/E turn, R/F tilt between a low angle and top-down),
middle-drag or WASD and the
arrow keys pan, Home reframes the fleet, and Tab
selects and frames the next friendly ship, and Space pauses; the Speed button steps playback through
1x, 2x, 4x and 8x. Clicking a unit (or picking it with
Tab or the roster) snaps the camera onto it and follows it as it moves, so a fighter stays in view;
panning or Home hands the camera back, while zooming and orbiting keep following. A roster bar under the title lists one side's capital
ships and every one of its squadrons, grouped by fighter type with a gap between groups; when the
squadrons do not fit beside the ships they take rows of their own, shrinking to a readable minimum
and wrapping, and the bar grows to hold them. Ships shrink to stay on the first row and wrap
once they reach the smallest size that still shows their number. Clicking a slot inspects (and, for your own units,
selects and frames) the unit, and the side toggle switches the listed side. Clicks pick
units against their drawn hulls. Units choose automatic targets by a priority class: ships whose
turbolasers and ion cannons outweigh their laser cannons hunt capital ships first, laser-heavy ships
screen against fighters, and squadrons dogfight before turning on capital ships. A unit takes the
nearest engageable unit of its preferred class and stays on it until it is gone; the panel's Targets
button flips the priority of the inspected unit, or of the whole selection, on the player's side.
Capital ships turn slowly to point their strongest battery against that class at the target and hold
that facing between volleys rather than swinging each arc around as it charges.
Fleets form up in opposite corners of an arena three times the configured starting distance
across, each facing the other's corner, so battles open at long range and close over time. Ships with tractor
beams fight from weapon range, close to beam range only on a capital ship that is running, and hold
it while its hull stays in range: a held
ship crawls at a fifth of its speed and cannot complete a hyperspace jump, and a beam ship keeps
hold of a running ship, following it, until the beam ship dies or the hull slips out of range.
Withdrawing ships stay under fire, and return it, until they are gone. Automatic resolution
follows the same rule: a unit leaving there flies as far as a starting line lies from the edge of
this arena (0.6 starting distances) and then `AutoResolveWithdrawalDistance` beyond it, so an
escape takes about as long either way. Whether a side may retreat is judged live
each frame: an enemy ship with a gravity well holds it in the system until that ship is destroyed
or jumps out, and a side with no hyperdrive-capable unit left, or no safe system to jump to, cannot
leave either. The Retreat button names the reason while it is greyed (Interdicted, No Hyperdrive,
No Escape) and the hint spells it out; the strategic layer hands the battle only the escape route
(`SpaceCombatWithdrawalResolver.HasEscapeRoute`) and leaves the gravity-well check to the
simulation. A battle in which neither side loses hull or fighters, nor is driven to a new low on
shields, for `AutoResolveStagnationIterations` rounds ends as a stalemate: shields that dip and heal
back do not count as progress, and the weaker side withdraws when it can and is destroyed otherwise.
Every fighter has an airframe worth of
durability on top of its shields, fighter weapons recharge more slowly than in auto-resolution,
turbolasers mostly miss fighters (each turbolaser shot at a squadron rolls against
`TacticalTurbolaserFighterHitChance`, and a miss flies wide past it; laser cannons always hit), and
capital-ship weapon ranges are doubled for the larger arena; these are tactical-only settings in the
space-combat config (`TacticalFighterHullStrength`, `TacticalFighterRechargeMultiplier`,
`TacticalTurbolaserFighterHitChance`, `TacticalCapitalWeaponRangeMultiplier`,
`TacticalShieldRechargeMultiplier`, `TacticalShieldGeneratorRepairMultiplier`) with code defaults,
since `game.xml` lives in the media repository. Capital-ship shields recharge at half their
strategic rate, because arcs recharge one at a time and full-rate shields healed completely between
a Star Destroyer's volleys, and a knocked-out generator takes twice as long to come back
during a battle than other components (the repair days it carries out are unchanged). Squadron
torpedoes are held for capital ships whose
shields are down (at or below a twentieth of their maximum) and are drawn as slower, heavier grey
bolts; ion fire is blue and other fire takes the faction color. Hull bars over ships use the faction theme color, so
Alliance bars are red and Imperial bars green whichever side the player commands. Torpedoes
crawl: at least two seconds in flight and well under a bolt's speed. Each squadron in a force
circles on its own ring (a golden-ratio spread of orbit radii, with alternating directions), so
squadrons attacking the same ship do not fly stacked on top of each other. Your squadrons can be
selected and sent after an enemy with a right-click; a directed squadron holds that target until it
is gone, then goes back to choosing its own, and a selected squadron carries a status bar and the
selection box.
The panel shows a squadron's weapon lanes with their power in the system slots and their ranges and
charge below. Torpedoes burst in a small explosion where they land, and capital energy bolts are
drawn thicker the heavier the battery behind them, while ion fire and fighter bolts keep a fixed
width and no bolt drops below a visible floor. Squadrons always fly at full speed, and a
squadron waiting to be recovered circles its carrier until the carrier jumps. Every hit on a capital
ship also has a small chance (`TacticalShieldGeneratorHitChance`) of knocking its shield generator
offline, so a strafing squadron's many light hits threaten it more than a few broadsides: the shields
stop recharging, the panel's Shields system shows offline and the shield text notes the generator,
and the ship works the damage off over the battle. Whatever repair is still owed leaves the battle
with the ship as `CapitalShip.ShieldGeneratorRepairDays` (via `ShipDamageResult`), shows on the
strategic status card, and the recovery tick repairs it a day at a time, twice as fast in dock.
Capital-ship
models are drawn at three times their simulation radius and fighters at 1.6 times; fighter squadrons
circle the unit they attack inside weapon range, and squadrons with nothing to engage patrol around
the nearest friendly capital ship instead of parking. The scene plays a pooled burst of flash, fire,
and smoke particles where a unit is destroyed and streams blue particles onto a ship held in a
tractor beam; both use the render pipeline's default lit material with a generated soft-disc
texture, alpha clipping, and exposure-independent emission, so they need no authored assets. Each unit's model is
`Units/<ID>/Models/model.glb` beside its display image, loaded through the application
`ContentModelCache`; units without a model show a placeholder hull. The strategy screen starts parsing the models at a
contested planet as soon as the battle alert appears, and the battle holds at its opening positions
(the clock reads "Fleets deploying", for at most eight seconds) while models are still loading. Those models currently live on
the media branch `feature-3d-combat`.
A system holds at most one capital ship that can destroy planets (one Death Star), whoever owns
it, counting ships still under construction and fleets on their way there. `Planet` refuses a
fleet that carries a second and `Fleet` refuses the ship itself, both in `CanAcceptChild` (which
movement, evacuation, withdrawal and manufacturing destinations consult) and when a node is added
directly; a manufacturing order for one is limited to a single ship bound for a system that has
none.
A capital ship that can destroy planets (the Death Star) does not join the battle line. It stands
off beyond its own edge of the arena as a `TacticalBattleStation`, where nothing can shoot at it,
and takes part in two ways. Its superlaser opens the battle charged: the owning player clicks the
charge bar at the top of the information panel and then an enemy capital ship (in the arena or on
the roster), the beam destroys that ship outright a second later, and the laser recharges over
`TacticalSuperlaserRechargeSeconds` (three minutes). The other side destroys the station by
selecting squadrons and right-clicking it, which sends them off the battlefield to fly its trench;
the run is decided once every squadron sent has arrived or been lost on the way. A run needs
torpedoes. With them it succeeds at `TacticalTrenchRunBaseChance` (90%) while the fighters flying
it at least match the enemy fighters still alive, and less in proportion when outnumbered, never
below `TacticalTrenchRunMinimumChance` (25%). Success destroys the station; failure costs every
squadron that flew the run. No run is possible while a completed shield generator on the planet
lists the station's type (`Building.ProtectedUnitTypeIDs`, the Death Star Shield). The outcome's
movie comes from the viewing faction's theme (`TrenchRunCutscenes`, keyed by ship type) and the
battle holds while it plays. An automatic side uses both mechanics: it fires its superlaser at the
toughest enemy capital ship once the opening seconds have passed, and it sends its torpedo
squadrons at an unshielded station. A side with a station in the fight does not retreat at the
strength threshold, a station is never the loser of a stalemate, and an automatic side with
nothing left to fight but a station it cannot attack withdraws. Automatic resolution (battles the
player does not command) follows the same rules, read from the same `BattleStationRules` and
`BattleStationState`: the station stays out of the line, fires on the same schedule, and torpedo
squadrons fly about as far as the flight across the tactical battlefield (six starting distances),
under fire like any other unit, before they enter the trench and their run is rolled.
Weapon fire is drawn as short bolts that fly from the muzzle to the target at a fixed speed, so
long shots stay visible longer. Laser colors come from each faction's theme primary color. Battle
sounds use the shared tactical
effects under `Pack/Shared/Tactical/Audio` (weapon fire, impacts, fighter launch, hyperspace);
sounds missing from the installed content are skipped silently. The editor menu
**Rebellion > Debug > Play Tactical Battle Test** writes a save with a contested home system and plays it;
the **100 fighters** variants first top the Alliance home fleet up to a hundred squadrons, the
**Death Star** variants add a Death Star to the Imperial fleet and torpedo squadrons to its opponent
(their automated forms hand the player's side to automatic control after twelve seconds, so the
superlaser fires and the trench run is flown without a click), and
the **automated** variants also dismiss the loading screen, take command, and write Game-view and
arena screenshots to `Logs/TacticalShots`, including shots framed on the whole arena when the first
explosion and the first tractor beam appear. Loaded models get their own child under each unit so the
fit measures the mesh alone, and their materials are forced double-sided because several capital-ship
exports cull away under single-sided rendering.

`ContentEditor` is a separate assembly included in Unity and the standalone authoring player. Its
workspace controller handles editing requests, its session owns selection and continuous edits, and
its projector supplies presentation data. Projects and documents own editable state; storage owns
file access, while validation and export have their own owners. General input, tooltip, dialog,
preview, scrolling, and divider behavior lives in `UI/Components` and has no editor dependency.
Shared menu builders support textured and flat control styles.

The map's **Fit to view** command changes zoom and pan only. **Auto arrange** adjusts saved
coordinates to clear overlaps and the window frame, moving planets in a sector or whole sector
clusters on the galaxy map. Layout runs off the UI thread, supports Escape to cancel, and applies
as one undoable edit after the complete placement is valid.

## XML serialization

`PersistableObject`, `PersistableMember`, and `PersistableAttribute` retain the names used when
writing XML. Add stackable `PersistableReadAlias` declarations to accept additional input names
for a persistable class, field, or property:

```csharp
[PersistableMember(Name = "CurrentName")]
[PersistableReadAlias("AlternateName")]
[PersistableReadAlias("OldName", Deprecated = true)]
public string Value { get; set; }
```

Both aliases load into `Value`; serialization continues to write `CurrentName`. `Deprecated`
defaults to `false` and records an alias's status without rejecting it. Aliases do not make an
otherwise unpersisted member persistable. Conflicting member aliases and ambiguous polymorphic
type aliases are rejected. A member declaring aliases may be supplied only once per object,
including when a document mixes its canonical name with an alias.

The serializer and `SerializationMetadata` share the same read-name lookups. Schema-validated
content must also allow the accepted names in its XSD; read aliases do not bypass schema validation.

## Game scripting

`GameActionInterpreter` interprets branching and action sequences. It passes concrete actions through `IGameActionExecutor`, implemented by `GameActionCommands`, which owns movement, mission, ownership, message, and encounter dependencies.

`GameEvaluationContext` holds bindings, source attribution, the triggering result, and results produced during an activation. `GameConditionContext` supplies condition-evaluation inputs. `GameActionContext` collects action results and deferred operations. These temporary contexts stay within the scripting subsystem.

Actions retain their authored order. Capture cleanup remains immediate; queued command requests execute after the full action batch. Event scheduling, activation history, XML names, and save fields retain their existing contracts.

See the [scripting reference](Modding/Scripting/Index.md) for authored XML.
