# Repository Instructions

1. `AGENTS.md` and `CLAUDE.md` must always contain the same instructions. Every change made to one file must be made to the other in the same commit.
2. Use `using` directives for namespaces. Do not fully qualify types such as `Rebellion.Game.GameSummary` unless qualification is required to disambiguate conflicting type names.
3. Keep `rebellion2/Assets/Content` and the `rebellion2-media` repository in sync. Any content change made in one location must be made in the other.

4. Never make authored image assets depend on committed, copied, or hand-edited Unity `.meta` files. Assume image assets installed under `Assets/Content` arrive without `.meta` files and that Unity may regenerate ignored metadata at any time.

5. All Unity import settings and persistent prefab preview references for external images must be produced programmatically by the relevant prefab builder or its editor asset-loading code. If an image needs special import behavior, implement it in that builder/editor path; do not fix it by changing the image's `.meta` file.

6. Generated UI prefab payloads and generated Main Menu, Save Menu, and Strategy scenes are local build outputs and must not be committed. Their builders and committed base-scene templates are the source of truth. Developer builds include local preview references, while player builds generate clean prefabs that load raw files at runtime from the installation's external `Content` directory. Distributed images must not require Unity `.meta` files.

7. Do not include reverse-engineering provenance, executable or disassembly references, extraction notes, source addresses, or similar implementation-source details in committed code, comments, content, tests, commit messages, or pull request text. Describe only the resulting game behavior and design.

8. If tests need to run while Unity is open, close Unity cleanly and run the tests. Do not skip local tests or push untested changes merely because Unity is open.

9. Write pull request summaries, rationale, and validation lists in active present tense. Capitalize the opening word of every bullet or statement, such as `Adds data-driven mobile headquarters behavior` rather than `add data-driven mobile headquarters behavior`.

10. Do not use slashes in branch names intended for pull requests.

11. Use `git push-external` for pushes. Never use `git push`.
