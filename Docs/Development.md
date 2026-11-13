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

For native UI audits, pass `-nativeAuditBuild` to `StandalonePlayerBuild.Build` and use a
separate player output directory. This adds the `REBELLION_NATIVE_AUDIT` compilation symbol;
`Assets/Tests/NativeAudit` requires that symbol in standalone players. Launch each audit player
with its own absolute `-nativeAuditControl` directory. Without that launch argument, the observer
does not start. Ordinary builds omit the symbol.

Write one JSON command to a temporary file, then rename it to `request.json` in that directory.
Commands have a unique numeric `id` and an `operation`: `state`, `point`, `capture`, or `wait`.
For example, `{"id":1,"operation":"point","target":"MultiplayerButton"}` resolves the
center of a unique active control, its event-system raycasts, and public UI state. Exact hierarchy
paths disambiguate repeated names. Coordinates use Unity's viewport origin at the bottom left.
The completed response is published once as `response-1.json`; captures also create `frame-1.png`.
Use a fresh control directory for each process and never reuse command IDs within it.

For native service audits, explicit `browse`, `host`, `join`, `ready`, `launch`, `resume`, `save`,
`quicksave`, `load`, `speed`, and `fight` commands call the existing public service contracts.
`key` queues a virtual Input System keyboard press and release. These commands do not invoke
private callbacks. Poll `operationPending`, then check `operationSucceeded`, coordinator status,
and the resulting scene and files: completion of a service call alone does not establish admission.
`fight` submits one explicit local Fight response; it does not automate future battles.
Simulation profiling stays in the editor/headless runners. The network simulation runner records
per-day tick, step, barrier, and verification durations with `Stopwatch`. Ordinary gameplay has
no phase-trace collector or native timing commands.

Service audits do not establish physical input delivery. Drive input through the operating system
when testing actual shortcuts and controls, and verify the resulting UI, checkpoint, or save
independently. A resolved pointer target does not prove a click was delivered. State responses include active control paths,
interactivity, enabled action names, shortcut suppression, and selected-control focus; they omit
input-field contents and credentials. Scene queries run only for explicit commands, with request
polling at five hertz. Keep both audit clients on the same compiled build and use isolated saves.

## Multiplayer synchronization

`GameSessionManager` selects the session owner before constructing simulation services. Multiplayer
campaigns require an `IGameSessionOwner`; presentation reports readiness for that specific session
and never replaces its execution owner. Local and network player requests use the session's writable
command boundary. Commands wait while a tick executes, and network commands also wait for the
completed day's verification barrier.

Admission compares a source identity embedded in the compiled game assembly and a SHA-256 digest
of the composed gameplay catalog. The source identity covers production scripts, assembly settings,
package dependencies and Unity version; it is independent of checkout paths and line endings.
Content checks apply to joining, content changes, shared loads and reconnects. Pack/version labels
alone do not admit a peer. After changing source inputs, allow Unity's identity regeneration and
script compilation to finish before testing or building.

Each launch, shared load and reconnect receives an initialization identity. Readiness, recovery
snapshots and gameplay packets from an outgoing initialization cannot enter its replacement.
Each initialization transfers one world; its receipt must match the payload hash and restored-world
fingerprint.
Completed commands include their queued movement and manufacturing reactions before the caller
receives success. Tick completion and the handoff to human combat input also settle these reactions.
Persistence admission rejects pending reactions; requesting a save does not flush gameplay work.
Ownership-changing operations also publish their returned facts before completion. A constant-time
pending-fact counter rejects unfinished ownership reactions instead of persisting observer tables.
Normal loading also settles derived-state rebuild work before exposing a savable session.
Mission roster baselines are saved as IDs on the mission. Victory suppression belongs only to the
live session, which ends through the campaign-ending flow.

Campaign completion waits for settled final replay and both ending presentations before releasing
the connection, then removes each participant's completed reconnect checkpoint. A departure before
the first current-campaign checkpoint uses the session-end notice instead of attempting recovery.

Planet notifications use the existing result bus plus model support/presence observables.
Authored activation changes use the same mutation boundary, including sector reactivation. They
reconcile affected locations after gameplay reactions; only suppression changes include other planets
in the affected sectors. Planet subscriptions are enumerated once when a session connects. Normal
batches with no relevant changes do not scan the world. Loading silently initializes the notification
baseline. Resource diversion is computed once per producing planet per resource phase and does not
depend on notification history. Uprising progression remains timed simulation work.

Rejoining follows room ownership while simulation authority retains the campaign's authenticated
host role. The shared persistence channel is created after the agreed state handshake. Initial
synchronization, shared loading, and rejoining use the same persisted world contract and ordinary
loading methods. Both peers load the selected world before comparing loaded-state fingerprints;
there is no separate simulation recovery envelope, loader, or reference-aware recovery codec.

Manual saves and reconnect checkpoints require completed turn processing, including battle
decisions. Disconnect handling lets an executing tick finish. If it stops awaiting battle input,
the survivor restores the last completed checkpoint; progress since that checkpoint is lost.
Both players respond again when the encounter occurs. Requested launch/save/load checkpoints are
captured before another day starts; no full-world checkpoint is added to the per-day loop.
Coordinated loads detect encounters in an agreed turn after synchronization.

A rejoin offer transfers one ordinary world payload. Its receiver acknowledges the existing
initial-state barrier after normal loading and presentation startup, using the same receipt checks
as initial synchronization. The supplier can be either authenticated participant. The supplied
world remains blocked against persistence until acknowledgment, so a local save cannot change its
shared-save token during admission. The accepted rejoin names update the reserved host/guest names
and retained lobby presentation without changing its approved setup. Rejoining code is in
`MultiplayerCoordinator.Rejoin`.

The active RNG seed and ID-generation mode are normal world properties; their positions/counters
were already persisted. A faction saves its last control-loss support-shift tick through normal
persistence, preserving the existing once-per-faction-per-tick rule across ordinary saves as well.
Research timers were already saved normally. Loading keeps the same initialization and derived
state rebuilding for both participants. Completed-day verification checks persisted state and
runtime rules while excluding participant UI state and runtime parent-object caches.
A mismatch or execution failure stops replay without retrying a potentially applied action.

Simulation verification performs one graph traversal per completed multiplayer day per peer, after
the tick iterator releases its execution boundary. The headless network runner measures its cost;
verification is not a frame-loop poll.

Scene-node lookup uses a standard dictionary rebuilt from the decoded hierarchy. Save files and
network snapshots do not preserve a second registry membership list, dictionary storage positions,
or vacant slots. Faction ownership indexes are also rebuilt from the hierarchy. Random automatic
scrapping orders eligible units by ordinal ID before drawing an index; acquisition history is not
saved. Manufacturing queues are rebuilt from saved producer IDs and sequence values.

Human participants select their own game speed. The effective clock uses the slowest human
selection in the order Pause, Very Slow, Slow, Medium, Fast. Either player can pause; time advances
only when neither remains paused. A faster preference takes effect as the slower participant raises
their selection, without requiring identical choices. Keyboard stepping follows the local selection.
The strategy HUD shows the effective clock, both acknowledged choices, and faster-speed requests.

Each human side in a pending fleet encounter must respond before resolution. A single human facing
an AI side resolves after that human's response. If either side selects withdrawal, its existing
withdrawal eligibility and movement rules apply; when both select withdrawal, both plans are
prepared before either is applied and the encounter is a draw. The battle window shows response
receipt without exposing the other participant's choice. Pending responses belong to the runtime
continuation and survive recovery.

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
| `App` | Application bootstrap, startup selection, and scene handoff. |
| `Sessions` | Session lifetime, service construction, simulation command admission, and clock ownership. |
| `Sessions/PlayerRequests` | Typed player requests, outcomes, execution, and local or coordinated dispatch. |
| `Saves` | Campaign and preset files, save listings, and metadata publication. |
| `Multiplayer` | Online authentication, lobbies, transport, ordered gameplay, shared saves, and reconnects. |
| `Analytics` | Consented campaign and application metrics. |
| `UserSettings` | User preferences and their application to runtime services. |
| `UI/Audio`, `UI/Graphics`, `UI/Input` | Presentation and device owners used by startup, settings, and interactive UI. |
| `Game/Scripting` | Authored action, conditional, selector, binding, and trigger instructions. |
| `Game/GameEvents` | Event definitions, schedules, and saved activation history. |
| `Gameplay/Scripting` | Interpretation, evaluation, temporary execution contexts, and gameplay action handling. |
| `Gameplay/GameEvents` | Event validation, activation, and campaign tick/result integration. |
| `Gameplay` (root) | Tick execution, result delivery, and gameplay reaction processing. |

Gameplay feature folders retain commands for operations, queries for inspection and estimates, tick processors for time-driven work, and observers for result-driven reactions. A supporting class does not need its own tick to belong to a gameplay feature.

A system holds at most one capital ship that can destroy planets (one Death Star), whoever owns
it, counting ships still under construction and fleets on their way there. `Planet` refuses a
fleet that carries a second and `Fleet` refuses the ship itself, both in `CanAcceptChild` (which
movement, evacuation, withdrawal and manufacturing destinations consult) and when a node is added
directly; a manufacturing order for one is limited to a single ship bound for a system that has
none. A fleet withdrawing from a battle is judged as it will arrive: a planet destroyer it loses in
the battle, or strands without a hyperdrive, is gone before the fleet moves and does not keep its
escorts out of a system that holds another (`SpaceCombatWithdrawalPlanner`,
`Planet.CanAcceptFleet`).

`SpaceCombatAutoResolver` keeps a withdrawing unit in the fight until it is gone. The unit can be
shot at, and shoots back, while it covers a fixed escape leg: 0.6 starting distances, as far as a
starting line lies from its own edge of the battlefield, and then `AutoResolveWithdrawalDistance`
beyond it. The leg is the same however far the unit has advanced, because the resolver tracks no
positions.

A capital ship that can destroy planets does not join the battle line in an auto-resolved battle.
It stands off where no weapon can reach it (`BattleStationState`, under the rules in
`BattleStationRules`) and takes part in two ways. Its superlaser opens the battle charged, holds
its first shot for ten rounds (resolver iterations), then destroys the toughest enemy capital ship
outright and recharges over `BattleStationSuperlaserRechargeRounds`. The other side destroys the
station by sending its torpedo squadrons to fly the trench: they cover six starting distances,
under fire like any other unit, and the run is decided once every squadron sent has arrived or
been lost on the way. A flight under way counts as progress, so the battle is not settled as a
stalemate before the run is rolled. A run needs torpedoes. With them it succeeds at
`BattleStationTrenchRunBaseChance` while the fighters flying it at least match the enemy fighters
still in the fight, and less in proportion when outnumbered, never below
`BattleStationTrenchRunMinimumChance`. Success destroys the station; failure costs every squadron
that flew the run. No run is possible while a completed shield generator on the planet lists the
station's type (`Building.ProtectedUnitTypeIDs`, the Death Star Shield). A side with a station in
the fight does not retreat at the strength threshold, a station is never the loser of a stalemate,
and a side with nothing left to fight but a station it cannot attack withdraws. The three settings
are authored in the application `game.xml` with the other space-combat rules.

Space combat offers two resolutions for a player-involved encounter. **Auto-Resolve** runs the
headless `SpaceCombatAutoResolver`. **Take Command** opens the interim tactical battle: a
`TacticalSpaceBattle` under `Gameplay/SpaceCombat/Tactical` that simulates on a flat plane and
reuses the auto-resolver's weapon-arc, shield, ion, recharge, and withdrawal rules while the
player selects capital ships, issues move and attack orders, retreats, or hands control to the
simulation. Fighters launch and engage on their own. The battle produces the same
`SpaceCombatResult` as auto-resolution and is applied through `SpaceCombatCommands`, so messages,
blockades, withdrawals, and fleet cleanup behave identically. The finished battle is applied at
the session's command boundary (`GameSession.ExecuteCommandAsync`), and its result reaches the
battle alert through the result pipeline like every other resolution. The battle is simulated on
one machine, so Take Command is offered only in a local session and only for an encounter that
waits for one human response; a coordinated (multiplayer) session, or an encounter between two
human sides, is settled by the players' responses instead. Its presentation lives in
`UI/SceneUI/StrategyView/Tactical` as a hidden full-screen layer of the authored Strategy root prefab.
Units are drawn as 3D models by `TacticalBattleScene`: an off-canvas rig on the `TacticalBattle`
layer (25) whose perspective camera orbits a focus point on the battle plane and renders into the
arena image. The rig is lit by two directional lights and by a flat ambient volume of its own
(`HdrpRenderTargetBuilder.AddTacticalAmbientVolume`: volume layer 24 and
`TacticalBattleAmbientVolumeProfile.asset`), kept apart from the much dimmer flat ambient volume
that the main menu and the loading screen share, because a loading screen can be on show while the
strategy scene is loaded. The battle builds its own materials while it runs (placeholders,
selection and deck markers, beams, particle effects, repaired hulls) by cloning two assets the rig
references, `TacticalBattleLitMaterial` and `TacticalBattleParticleMaterial`, so that a player
build carries the shader and its variants; the render pipeline's default material exists only in
the editor. The view opens at a fixed angle onto the battle plane from behind the player's side,
and the camera is locked to the play space: its focus stays inside the arena and it can back out
only far enough to frame the whole arena; it opens with both fleets in frame. The attacking fleet
drops out of hyperspace as the battle opens, decelerating into place from off the map, and ships
that withdraw accelerate away into hyperspace the same way; both are presentation only. The 2D
overlay (the bars stacked above capital-ship hulls, plus shots; fighters carry none) projects
through the same camera and clicks are cast back onto the plane, so picking stays exact. Every
selected unit, and an enemy unit the player clicks to inspect, is marked in the scene itself by a
wireframe box fitted to its hull and turning with it, drawn in its faction's color with lines
that keep one width on screen at any zoom. The wheel
(a trackpad's smaller scroll deltas zoom in proportion)
or plus and minus zoom, right-drag orbits (Q/E turn, R/F tilt between a low angle and top-down),
middle-drag or WASD and the
arrow keys pan, Home or H reframes the fleet (most Mac keyboards have no Home key), and Tab
selects and frames the next friendly ship, and Space pauses; the Speed button steps playback through
1x, 2x, 4x and 8x. Clicking a unit in the arena or on its roster slot picks it without moving the
camera; clicking it again while it is the one unit picked zooms the camera in on it and follows it
as it moves, so a fighter stays in view (Tab frames the ship it picks straight away);
panning or Home hands the camera back, while zooming and orbiting keep following. A roster bar under the title lists one side's capital
ships and every one of its squadrons, grouped by fighter type with a gap between groups; when the
squadrons do not fit beside the ships they take rows of their own, shrinking to a readable minimum
and wrapping, and the bar grows to hold them. Ships shrink to stay on the first row and wrap
once they reach the smallest size that still shows their number. Clicking a slot inspects (and, for your own units,
selects) the unit, and the side toggle switches the listed side. A selection is built up
with two keys, read each frame with the camera keys (`TacticalCameraInput`): with Shift held, a
click on a roster slot selects every unit from the last one picked through it, ships first and
then squadrons as the bar lists them; with Control held, a click on a slot or on a unit in the
arena adds that unit or takes it out again, and a drag box adds to the selection instead of
replacing it (Command does the same as Control, and the hint names Cmd on a Mac). Neither moves
the camera, and a right-click then orders the whole selection: ships
keep their formation on a move, and ships and squadrons alike go for an attacked target. A move
click is cast onto the deck the selected ships stand on rather than the battle plane, so a ship on
a raised deck ends up under the pointer and not beneath it. Clicks pick
units against their drawn hulls. Units choose automatic targets by a priority class: ships whose
turbolasers and ion cannons outweigh their laser cannons hunt capital ships first, laser-heavy ships
screen against fighters, and squadrons dogfight before turning on capital ships. A unit takes the
nearest engageable unit of its preferred class and stays on it until it is gone; the panel's Targets
button flips the priority of the inspected unit, or of the whole selection, on the player's side.
Capital ships turn slowly to point their strongest battery against that class at the target and hold
that facing between volleys rather than swinging each arc around as it charges.
Fleets form up in opposite corners of an arena three times the configured starting distance
across, each facing the other's corner, so battles open at long range and close over time. A fleet
forms up in lines across its heading, as many ships to a line as fit across the arena with a
quarter of the hulls' width left between them, and a fleet too large for one line stacks up to
three of them on decks one above another instead of standing in ranks behind each other, so every
ship starts at the same distance from the enemy: its ships of the line first with the largest in
the middle, then its escorts (ships whose laser cannons outweigh their heavy batteries,
`TacticalCapitalShipUnit.IsEscort`) and unarmed ships. One line stands on the battle plane, two half
a deck above and below it, three on it, above it and below it, a deck being a little more than the
largest hull in the battle is long (`TacticalSpaceBattle.DeckSpacing`); lines above and below the
first stand half a slot to the side so no hull hides the one beneath it, and only a fleet too large
for three decks forms further ranks behind. A formation that would overhang the arena is slid back
inside it. Height is presentation and elbow room only: every unit has an `Altitude`, a capital ship
keeps the deck it formed up on, and a squadron drifts toward the height of what it is flying at,
but ranges, targeting and orders are all measured on the plane, so the battle plays as a flat one.
Ships on different decks pass over and under each other; ships on the same deck are kept apart.
Weapon and tractor ranges run from hull to hull. A ship closes until the gap to
its target is a little over half the range of its main battery against that class of target, and
then holds (`GetPreferredRange`). Left to itself it also stops as soon as any enemy capital ship is
that near, waits behind a friend that is already holding its place instead of pushing it on, and
gives way to such a friend when crowded, so two fleets end as lines standing off from each other
rather than as one knot of hulls. An escort with no orders keeps station beside the nearest ship of
the line and shoots at whatever comes within reach instead of chasing fighters into the enemy
fleet; with no ship of the line left it hunts on its own. A ship under the player's attack order
goes for its target regardless. Ships with tractor
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
back do not count as progress, squadrons flying a trench do, and the weaker side withdraws when it
can and is destroyed otherwise.
Every fighter has a thin airframe worth of
durability, and its shields count for more than their rating on top of it, so an unshielded TIE no
longer falls to the first hit but still falls several times sooner than an X-wing (squadrons all
deal the same damage over time, so durability is what sets them apart); fighter weapons recharge
more slowly than in auto-resolution,
turbolasers mostly miss fighters (each turbolaser shot at a squadron rolls against
`TacticalTurbolaserFighterHitChance`, and a miss flies wide past it; laser cannons always hit), and
capital-ship weapon ranges are doubled for the larger arena; these are tactical-only settings in the
space-combat config (`TacticalFighterHullStrength`, `TacticalFighterShieldMultiplier`,
`TacticalFighterRechargeMultiplier`,
`TacticalTurbolaserFighterHitChance`, `TacticalCapitalWeaponRangeMultiplier`,
`TacticalShieldRechargeMultiplier`, `TacticalShieldGeneratorRepairMultiplier`), authored in the
application `game.xml` with the other combat rules. Capital-ship shields recharge at half their
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
selection box. A right-click on one of your own capital ships posts the selected squadrons around
it as a screen (`TacticalSpaceBattle.IssueScreenOrder`): each circles that ship and takes on only
the enemies that come within a short reach of its hull, letting go of one that gets away again,
until the ship is gone, the side retreats or hands over to the simulation, or the squadron is
given another order. Ships selected with the squadrons still take that click as a move order.
The panel shows a squadron's weapon lanes with their power in the system slots and their ranges and
charge below. Torpedoes burst in a small explosion where they land, and capital energy bolts are
drawn thicker the heavier the battery behind them, while ion fire and fighter bolts keep a fixed
width and no bolt drops below a visible floor. Squadrons always fly at full speed. A selected
squadron can be recalled to a hangar with the panel's Recover button, which sends it to its own
carrier or, when that has no bay free, to the nearest friendly ship that has one
(`TacticalSpaceBattle.IssueRecoverOrder`); a squadron that is screening a ship docks with that
ship when it has a bay. A ship holds as many squadrons as its
`StarfighterCapacity`, counting those on their way as well as those aboard, and a depleted squadron
takes a whole bay. A recalled squadron flies straight for its ship, takes three seconds over the
hull to come aboard (it shrinks into the hangar as it lands), and is then off the battlefield for
good: it is not launched again, its roster slot turns a cool blue and keeps its count (a lost
squadron's goes grey), it is lost if the ship is destroyed and it leaves with the ship if the ship
withdraws. An attack or trench-run order given
before it is aboard calls the recall off. A retreat recalls every squadron that has no hyperdrive
at once to ships that can jump (`TacticalBattleForce.RecallSquadrons`); squadrons with a
hyperdrive leave the bays to them and jump out on their own, giving up any bay they were making
for. A ship holds its jump until the squadrons bound for it are aboard, and a squadron with
neither a hyperdrive nor a bay stays with the nearest ship still leaving and is lost once the
last of them has gone. `SpaceCombatResult.FighterDockings` carries
each docked squadron and its ship out of the battle: the squadron is left aboard that ship (one
that never docked makes way for it when the hangar is full,
`SpaceCombatWithdrawalResolver.SeatFighters`), and a withdrawing fleet takes its docked squadrons
with it whatever drive they have. Every hit on a capital
ship also has a small chance (`TacticalShieldGeneratorHitChance`) of knocking its shield generator
offline, so a strafing squadron's many light hits threaten it more than a few broadsides: the shields
stop recharging, the panel's Shields system shows offline and the shield text notes the generator,
and the ship works the damage off over the battle. Whatever repair is still owed leaves the battle
with the ship as `CapitalShip.ShieldGeneratorRepairDays` (via `ShipDamageResult`), shows on the
strategic status card, and the recovery tick repairs it a day at a time, twice as fast in dock.
An auto-resolved battle honours the repair still owed (`ShieldGeneratorRules`, shared by both
resolvers): the ship's shields do not recharge until the generator is worked back, at the same
rate. It never knocks a generator out itself and leaves the repair days owed as they were.
Capital-ship
models are drawn at three times their simulation radius and fighters at 1.6 times; fighter squadrons
circle the unit they attack inside weapon range, and squadrons with nothing to engage patrol around
the nearest friendly capital ship instead of parking. The scene plays a pooled burst of flash, fire,
and smoke particles where a unit is destroyed (hulls, bolts, bursts and beams are all drawn at
their units' heights, and a capital ship off the battle plane carries a thin stem down or up to it
with a small foot where it lands, in a dim shade of its side's color, so a ship that is higher can
be told from one that is farther away), streams blue particles onto a ship held in a
tractor beam, and wraps every ship that is projecting a gravity well (an Interdictor Cruiser or a
CC-7700 Frigate, by `CapitalShip.HasGravityWell`) in a wider field of orange-brown particles for as
long as it is in the battle, which shows the player what is holding a fleet in the system; all of
them use the render pipeline's default lit material with a generated soft-disc
texture, alpha clipping, and exposure-independent emission, so they need no authored assets. Each unit's model is
`Units/<ID>/Models/model.glb` beside its display image, loaded through the application
`ContentModelCache`; units without a model show a placeholder hull. The strategy screen starts parsing the models at a
contested planet as soon as the battle alert appears, and the battle holds at its opening positions
(the clock reads "Fleets deploying", for at most eight seconds) while models are still loading. Those models currently live on
the media branch `feature-3d-combat`.
A capital ship that can destroy planets (the Death Star) does not join the battle line. It stands
off beyond its own edge of the arena as a `TacticalBattleStation`, where nothing can shoot at it,
and takes part in two ways. Its superlaser opens the battle charged: the owning player clicks the
charge bar at the top of the information panel and then an enemy capital ship (in the arena or on
the roster), the beam destroys that ship outright a second later, and the laser recharges over
`BattleStationSuperlaserRechargeRounds` (360 rounds, three minutes). The other side destroys the station by
selecting squadrons and right-clicking it, which sends them off the battlefield to fly its trench;
the run is decided once every squadron sent has arrived or been lost on the way. A run needs
torpedoes. With them it succeeds at `BattleStationTrenchRunBaseChance` (90%) while the fighters flying
it at least match the enemy fighters still alive, and less in proportion when outnumbered, never
below `BattleStationTrenchRunMinimumChance` (25%). Success destroys the station; failure costs every
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
