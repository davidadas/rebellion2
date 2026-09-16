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
Seed `12345` remained the development seed. The other three seeds were held until the migration was
complete. Global proposal selection uses signed benefit-minus-cost utility so different proposal
types remain comparable; local fixed-vector choices continue to use weighted averages.

| Seed | Faction | Planets | Fleets | Capital ships | Starfighters | Regiments | Shipyards | Construction | Assaults |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 12345 | Alliance | 67 | 10 | 43 | 47 | 261 | 21 | 28 | 2/2 |
| 12345 | Empire | 78 | 12 | 82 | 356 | 241 | 35 | 42 | 14/14 |
| 1892256962 | Alliance | 79 | 13 | 120 | 77 | 215 | 76 | 59 | 5/5 |
| 1892256962 | Empire | 91 | 14 | 110 | 838 | 359 | 60 | 74 | 24/24 |
| 1767770646 | Alliance | 121 | 18 | 178 | 362 | 423 | 84 | 72 | 12/12 |
| 1767770646 | Empire | 59 | 9 | 58 | 299 | 164 | 41 | 41 | 14/14 |
| 507859324 | Alliance | 66 | 10 | 31 | 19 | 253 | 31 | 28 | 0/0 |
| 507859324 | Empire | 102 | 15 | 74 | 647 | 342 | 57 | 63 | 20/20 |

Across the four seeds, assaults were 91 versus the 84-run baseline. Aggregate planets increased
0.2%, capital ships increased 5.6%, starfighters decreased 2.8%, regiments decreased 2.5%,
shipyards decreased 3.3%, and construction facilities increased 0.7%. No run reached a victory
condition. Seed `507859324` remains the weakest individual result at 20 assaults versus 27 in the
baseline; the aggregate parity result does not conceal that held-seed variance.

Final median/p90/p99 tick timings were 147.554/324.677/488.867 ms, 200.961/537.319/744.880 ms,
185.710/455.202/645.945 ms, and 168.717/348.536/640.469 ms in seed order. The p99 remains above
the 300 ms target on every seed. Performance remains a separate follow-up; this migration does not
claim to have solved it.

## Normalization Consistency Repair

The post-migration consistency repair used code revision `b0abebd2` as its four-seed baseline. It
made curve inputs strict, replaced anonymous raw-domain divisors with named semantic endpoints,
and consolidated duplicated consideration factories. Strict evaluation exposed two callers that
had depended on silent clamping: facility-portfolio deviation and regiment-transfer readiness
gain. Both now explicitly saturate normalized values at their domain boundary. Fleet assembly and
infrastructure allocation retain their established mathematical mappings under named domains.

All four Medium, large-galaxy, tick-1000 runs completed without an out-of-range curve input or a
victory. Canonicalized reports for seeds `12345`, `1767770646`, `1892256962`, and `507859324`
matched their corresponding `b0abebd2` baselines exactly after removing only `OutputPath`.

Mean median/p90/p99 tick timings changed from 237.999/458.050/739.520 ms to
240.987/461.973/732.580 ms, or +1.26%/+0.86%/-0.94%. The repair adds one constant-time range check
per evaluated curve and no scene traversal, sort, collection materialization, or nested entity
query.
