# Analytics

Player analytics is opt-in. Collection is compiled into standalone builds only when the build is
created with `-enableAnalytics`; the Unity Editor and ordinary local builds never initialize or
send analytics. Diagnostics remain controlled by their separate build flag and setting.

The Unity Analytics Event Manager must contain the custom schemas below in every environment that
receives builds. Event and parameter names are case-sensitive. Parameters with the same name are
shared between schemas and must use the same type.

## Events

| Event | When recorded |
| --- | --- |
| `campaign_started` | A new campaign starts, including the selected player faction and initial force balance. |
| `campaign_checkpoint` | Every 100 completed simulation ticks, including the current force balance. |

## Parameters

| Parameter | Type |
| --- | --- |
| `campaign_id` | string |
| `content_pack_id` | string |
| `content_pack_version` | string |
| `player_faction_id` | string |
| `difficulty` | string |
| `galaxy_size` | string |
| `victory_condition` | string |
| `resource_availability` | string |
| `custom_game` | bool |
| `current_tick` | int |

The following force-balance parameters are integers:

- `ai_faction_count`
- `player_planets`, `ai_planets`
- `player_capital_ships`, `ai_capital_ships`
- `player_starfighters`, `ai_starfighters`
- `player_regiments`, `ai_regiments`
