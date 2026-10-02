# Mission Betrayal Trace

## Reference

- Binary: `REBEXE.EXE`
- SHA-256: `B3FE3997CAB9A6E96403D638875DCBA25484E4D8601751AFEC748471AC0ED6AB`
- Disassembly project: `SectorCheck.gpr`

## Betrayal roll

`FUN_00589f10` calls the mission virtual at offset `0x1c8`. Diplomacy's vtable at
`0x00667a18` and Research's vtable at `0x00664db8` both route that virtual to
`FUN_00592500`, which permits the check only while the mission is in phase 9.

When permitted, `FUN_00589f10` traverses every mission participant through the callback at
`0x00588da0`. That callback reads the participant's loyalty through virtual offset
`0x1f8`, subtracts it from the global value 100, and passes the result to
`FUN_0053e2f0`. `FUN_0053e2f0` succeeds when a random value in `[0, 100)` is lower
than that limit. The betrayal probability is therefore exactly `100 - loyalty`. The
callback continues through all participants and stores the most recent betrayer at state
offset `0x40`.

The callback does not inspect command rank or the unable-to-betray flag. The latter is
enforced by the data and loyalty mutation path: those characters remain at loyalty 100,
which produces a zero-percent betrayal roll.

## Mission termination

When at least one participant betrays, `FUN_00589f10` writes 1 to state offset `0x4c`.
`FUN_0058a020` and `FUN_0058a130` then skip normal objective processing.
`FUN_0058a1c0` consumes the flag and calls mission virtual offset `0x1dc` with completion
status 3 or 4. Diplomacy and Research both route this virtual to `FUN_005233d0`, which
calls `FUN_00521900` to store the nonzero completion status. Their completion callback
at `FUN_00524b20` calls `FUN_005227d0`; because the completion status is nonzero,
`FUN_005227d0` calls `FUN_00521980` with phase 11. Phase 11 is terminal.

Consequently, betrayal aborts the ongoing Diplomacy or Research mission. It is not an
ordinary failed objective attempt and the mission does not repeat.

The executable stores completion code 3 or 4 for this path. `TEXTSTRA.DLL` exposes the
mission-report category `Mission Failed` at string resource 20503 and contains no
`Mission Foiled` string. The disassembly does not establish a one-to-one mapping from
those numeric completion codes to this project's `MissionOutcome` enum, so this change
does not rename the project's existing outcome representation.

## Participant cleanup

After assigning the terminal status, `FUN_0058a1c0` uses the cleanup traversals
`FUN_00587f80` and `FUN_00587b70`, with callbacks at `0x00589360` and `0x005891e0`.
This branch does not run the hostile-detector resolution used for a detected mission.
The callbacks perform mission participant cleanup and return handling; they do not select
a detector or perform the detector-based evasion, injury, capture, or destruction rolls.

The runtime must therefore terminate and tear down a betrayed mission without calling
`ResolveFoiledParticipants`. Normal teardown remains responsible for returning surviving
participants and for handling a participant who has no usable return destination.

## Traitor discovery

After setting state offset `0x4c`, `FUN_00589f10` traverses the participants again through
the callback at `0x00589490`, which calls `FUN_00588e80`. `FUN_00588e80` excludes the
traitor, reads each other participant's Force rating at offset `0x8c`, and passes that
rating to `FUN_0055e550`. `FUN_0055e550` calls `FUN_0053e2f0`, so discovery succeeds when
a random value in `[0, 100)` is lower than the participant's Force rating. Traversal stops
at the first successful discoverer. The resulting state change emits the
`TraitorDiscovered` notification through `FUN_004f2090`.

## Regression expectations

- A loyalty-zero participant aborts an otherwise-repeatable Diplomacy mission.
- A loyalty-zero participant aborts an otherwise-repeatable Research mission.
- A betrayal produces a terminal result but does not expose the team to a hostile detector
  confrontation.
- Another participant discovers the traitor when its Force-rating roll succeeds.
- The betrayal boundary is strict: a roll of `100 - loyalty - 1` succeeds and a roll of
  `100 - loyalty` fails.
