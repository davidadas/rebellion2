# Contributing

Contributions are welcome. Preserve established behavior, include tests where practical, and
describe how the change was validated.

## Development

Follow the [development guide](Docs/Development.md) for project setup, UI generation, tests, and
other local checks.

Do not commit generated UI prefabs, generated scenes, or copyrighted game assets. Contributors
must own a licensed copy of a supported source title and **must never share, upload, or redistribute
copyrighted game assets**.

## Pull request checks

Public checks start automatically for pull requests. Full Unity tests on fork contributions wait
for a maintainer to approve **Review private test access** in the **Fork PR tests** environment.
Review the displayed commit before approving it; a new push requires a new approval.

Maintainers can also start the **CI** workflow manually with the PR number and its full head SHA.
The test result is reported on that PR commit. Stale requests are rejected before loading private
test content.

## AI assistance

Using AI as a development tool is fine. Vibe coding is not.

Contributors must understand every submitted change, review generated code carefully, test it, and
be able to explain and maintain it. Unreviewed or unverified AI-generated changes will not be
accepted.

Never provide secrets, private repository content, or copyrighted game assets to an external AI
service.
