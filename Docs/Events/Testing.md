# Testing and Troubleshooting

Run the content repository's build first. It validates the XML against `game-events.xsd` and catches
invalid elements, attributes, nesting, and enum values.

```bash
cd /path/to/rebellion2-media
./build.sh all
```

Copy or install the updated content into `rebellion2/Assets/Content`, open the project in Unity, and
start a new campaign using that content pack. For quick testing, temporarily use a small `At` tick,
`RandomDelay`, or `RandomInterval`, then restore the intended schedule before committing.

If an event does not run:

- Check the Unity log for content-load or runtime validation errors.
- Confirm `pack.xml` points to the correct `GameEventsPath`.
- Confirm every `InstanceID`, referenced event, faction, planet, unit, and media path exists.
- Confirm the event does not combine `Schedule` with `Triggers`.
- Confirm trigger argument names and `$binding` aliases match exactly.
- Confirm every top-level `Bind` resolves exactly one node.
- Confirm conditions can pass in the tested game state.
- Confirm the event has not reached `MaximumActivations` or matched its recurring schedule's `Until`.

Finally, save and reload after the event has run. Verify recurring schedules, activation counts,
event variables, retained units, display changes, and multi-stage chains still behave correctly.
Event IDs and variable keys are persisted, so changing them can invalidate existing event state.

---

<p align="center"><a href="Examples.md">← Examples</a> · <a href="Index.md">Event guide</a></p>
