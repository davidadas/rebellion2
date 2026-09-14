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

The completed structural migration produced an exact normalized match for seed `12345`. The held
seeds were then run once each, without tuning. Every run used Medium difficulty, 1000 ticks, the
large galaxy, code revision `ee9c7fd6833305dd92f9a8a563901f99e60992b0`, and media revision
`36b5998676672e1072a51326c4b3f14e83caf66c`.

| Seed | Faction | Planets | Fleets | Capital ships | Starfighters | Regiments | Shipyards | Construction | Assaults |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 12345 | Alliance | 48 | 7 | 18 | 30 | 179 | 7 | 9 | 3/3 |
| 12345 | Empire | 83 | 13 | 64 | 119 | 324 | 31 | 60 | 14/14 |
| 1892256962 | Alliance | 90 | 14 | 168 | 262 | 415 | 71 | 76 | 10/10 |
| 1892256962 | Empire | 79 | 12 | 62 | 581 | 229 | 38 | 43 | 11/11 |
| 1767770646 | Alliance | 131 | 20 | 179 | 203 | 491 | 97 | 67 | 4/4 |
| 1767770646 | Empire | 55 | 9 | 33 | 407 | 169 | 33 | 33 | 13/13 |
| 507859324 | Alliance | 26 | 5 | 31 | 20 | 66 | 17 | 12 | 4/4 |
| 507859324 | Empire | 125 | 19 | 127 | 908 | 469 | 81 | 83 | 30/30 |

No run reached a victory condition. The final structural slice changed seed-12345 timing from
146.023/238.770/320.054 ms median/p90/p99 to 150.322/247.357/321.168 ms. The 0.35% p99 increase is
below the 10% regression limit, but the absolute p99 remains above the 300 ms target. Held-seed p99
values were 609.004, 688.738, and 681.908 ms respectively; performance remains separate follow-up
work rather than a correctness claim for this migration.
