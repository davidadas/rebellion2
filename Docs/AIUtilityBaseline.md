# AI Utility Migration Baseline

The utility-AI migration uses one deterministic simulation during development. Run the three
additional seeds only after every decision domain has moved to the shared utility model.

## Iteration Run

- Seed: `12345`
- Difficulty: `Medium`
- Galaxy size: `Large`
- Requested ticks: `1000`
- Completed ticks: `1000`
- AI interval: `1`
- Result: No victory
- Snapshot: `SimulationResults/current-medium-seed-12345-tick-1000.json`
- Snapshot SHA-256: `e32df1d6622efd5fdd41e02cf8869f6f555f871424d069443a92a638afe85210`
- Code revision: `9b694c46dcf43772ff0d88be5d8a80d36257675c`
- Media revision: `06d16ca6222a0d749bd9bc700aaa05e75fb335f3`
- Game configuration SHA-256: `793b1d0f034ba7488f1abe7fcebb7a9771541833de13b83a46fe79725cde1ddc`

The existing snapshot format does not record difficulty or repository revisions. Those values are
therefore recorded here from the run invocation and checked-out repositories. Future simulation
summaries should carry their complete run identity directly.

## Behavioral Result

| Metric | Alliance | Empire |
| --- | ---: | ---: |
| Planets | 48 | 83 |
| Fleets | 7 | 13 |
| Capital ships | 18 | 64 |
| Starfighters | 30 | 119 |
| Regiments | 179 | 324 |
| Special forces | 7 | 5 |
| Buildings | 272 | 818 |
| Construction facilities | 9 | 60 |
| Shipyards | 7 | 31 |
| Training facilities | 5 | 19 |
| Defense facilities | 32 | 87 |
| Manufactured buildings | 213 | 469 |
| Manufactured capital ships | 48 | 104 |
| Manufactured starfighters | 48 | 147 |
| Manufactured regiments | 259 | 303 |
| Manufactured special forces | 16 | 18 |
| Assaults attempted | 3 | 14 |
| Assaults succeeded | 3 | 14 |
| Assaults failed | 0 | 0 |
| Immediate uprisings | 0 | 0 |
| Missions succeeded | 461 | 540 |
| Missions failed | 197 | 251 |
| Missions foiled | 334 | 156 |
| Personnel captures | 6 | 11 |
| Personnel killed | 1 | 1 |
| Raw-material stockpile | 171 | 139 |
| Refined-material stockpile | 7,637 | 18,984 |
| Maintenance headroom | 1,945 | 6,328 |

## Comparison Discipline

During migration, every behavior-affecting change uses seed `12345`, `Medium` difficulty, and
`1000` ticks. Compare the complete simulation summary, not only victory or assault count. Exact
matches are required for structural migrations intended to preserve behavior. When a deliberate
curve change makes an exact match impossible, record and explain every gameplay delta separately
from timing changes.

After the migration is complete, validate the final model against seeds `1892256962`, `1767770646`,
and `507859324` without tuning specifically for those results.

## Final Validation

The completed migration was validated on Medium difficulty for 1000 ticks on the large galaxy.
Seed `12345` remained the development seed. The other three seeds were held until the complete
migration, then used to reject two isolated tuning attempts: increasing attack-reinforcement
pressure and replacing unified fleet-allocation utility with a stronger readiness bias. The final
configuration keeps one normalized fleet-allocation score and the original reinforcement weight.

| Seed | Faction | Planets | Fleets | Capital ships | Starfighters | Regiments | Shipyards | Construction | Assaults |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 12345 | Alliance | 60 | 9 | 51 | 69 | 212 | 30 | 29 | 3/3 |
| 12345 | Empire | 91 | 14 | 74 | 446 | 311 | 52 | 59 | 14/14 |
| 1892256962 | Alliance | 91 | 14 | 74 | 88 | 312 | 32 | 50 | 3/3 |
| 1892256962 | Empire | 82 | 13 | 105 | 1,312 | 307 | 73 | 72 | 21/21 |
| 1767770646 | Alliance | 139 | 20 | 149 | 747 | 568 | 65 | 81 | 12/12 |
| 1767770646 | Empire | 37 | 8 | 46 | 198 | 134 | 18 | 20 | 5/5 |
| 507859324 | Alliance | 49 | 8 | 33 | 58 | 213 | 12 | 24 | 2/2 |
| 507859324 | Empire | 95 | 14 | 98 | 272 | 323 | 49 | 67 | 12/12 |

Across the four seeds, assaults were 72 versus the 84-run baseline. Seed `12345` and seed
`1767770646` matched baseline assault totals, seed `1892256962` increased from 21 to 24, and seed
`507859324` decreased from 27 to 14. Aggregate planets decreased 2.7%, capital ships decreased
4.4%, starfighters increased 17.3%, regiments increased 2.8%, shipyards decreased 21.0%, and
construction facilities decreased 0.5%. No run reached a victory condition.

Final median/p90/p99 tick timings were 142.839/338.932/492.585 ms, 197.739/601.974/956.226 ms,
196.424/505.565/676.331 ms, and 161.806/364.485/518.950 ms in seed order. The p99 remains above
the 300 ms target on every seed. Performance remains a separate follow-up; this migration does not
claim to have solved it.
