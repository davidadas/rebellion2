# Repository Instructions

1. `AGENTS.md` and `CLAUDE.md` must always contain the same instructions. Every change made to one file must be made to the other in the same commit.
2. Use `using` directives for namespaces. Do not fully qualify types such as `Rebellion.Game.GameSummary` unless qualification is required to disambiguate conflicting type names.
3. Keep `rebellion2/Assets/Content` and the `rebellion2-media` repository in sync. Any content change made in one location must be made in the other.

4. Never make authored image assets depend on committed, copied, or hand-edited Unity `.meta` files. Assume image assets installed under `Assets/Content` arrive without `.meta` files and that Unity may regenerate ignored metadata at any time.

5. All Unity import settings and persistent prefab preview references for external images must be produced programmatically by the relevant prefab builder or its editor asset-loading code. If an image needs special import behavior, implement it in that builder/editor path; do not fix it by changing the image's `.meta` file.

6. Generated UI prefab payloads and generated Main Menu, Save Menu, and Strategy scenes are local build outputs and must not be committed. Their builders and committed base-scene templates are the source of truth. Developer builds include local preview references, while player builds generate clean prefabs that load raw files at runtime from the installation's external `Content` directory. Distributed images must not require Unity `.meta` files.

7. The original Rebellion executable disassembly is at `/Users/davidadams/Library/CloudStorage/GoogleDrive-dadams@confluent.io/.shortcut-targets-by-id/1WAtd7FKg2jYR7T2wmqxXXhErSUSv4GvJ/Uploads/rebexe-disassembly-source-trees 4`. For the original custom cursor, begin with `ptr_tables/PTR_00655458_FUN.lpcursorname_0000002b.h` (cursor resource ID `0x2B`), `ptr_tables/PTR_006bb8a0_LoadCursorA.h`, and `ptr_tables/PTR_006bb8a4_SetCursor.h`. The corresponding original executable is at the sibling path `Uploads/Star Wars - Rebellion/REBEXE.EXE`.

8. If tests need to run while Unity is open, close Unity cleanly and run the tests. Do not skip local tests or push untested changes merely because Unity is open.

9. Write pull request summaries, rationale, and validation lists in active present tense. Capitalize the opening word of every bullet or statement, such as `Adds data-driven mobile headquarters behavior` rather than `add data-driven mobile headquarters behavior`.

10. Do not use slashes in branch names intended for pull requests.

11. Use `git push-external` for pushes. Never use `git push`.
