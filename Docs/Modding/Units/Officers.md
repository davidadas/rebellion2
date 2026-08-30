# Officers

Officers are unique authored characters. Add each officer as an `Officer` entry beneath the
`Officers` root in the faction's `OfficersPath` catalog.

## Example

```xml
<Officers>
  <Officer>
    <TypeID>OFFICER_TYPE_01</TypeID>
    <InstanceID>OFFICER_EXAMPLE</InstanceID>
    <DisplayName>Example Officer</DisplayName>
    <DisplayImagePath>Pack/Factions/Example/Units/Officers/OFFICER_TYPE_01/display</DisplayImagePath>
    <SmallDisplayImagePath>Pack/Factions/Example/Units/Officers/OFFICER_TYPE_01/small-display</SmallDisplayImagePath>
    <MessageImagePath>Pack/Factions/Example/Units/Officers/OFFICER_TYPE_01/message</MessageImagePath>
    <InjuredImagePath>Pack/Shared/Units/Officers/personnel-injured-status</InjuredImagePath>
    <CapturedOverlayImagePath>Pack/Factions/Example/Units/Officers/OFFICER_TYPE_01/captured-overlay</CapturedOverlayImagePath>
    <OwnerInstanceID>FACTION_EXAMPLE</OwnerInstanceID>
    <IsMain>false</IsMain>
    <IsRecruitable>true</IsRecruitable>
    <RecruitingFactionInstanceIDs>
      <String>FACTION_EXAMPLE</String>
    </RecruitingFactionInstanceIDs>
    <Ratings>
      <Entry><Key>Diplomacy</Key><Value>40</Value></Entry>
      <Entry><Key>Espionage</Key><Value>25</Value></Entry>
      <Entry><Key>Combat</Key><Value>30</Value></Entry>
      <Entry><Key>Leadership</Key><Value>45</Value></Entry>
    </Ratings>
    <Loyalty>60</Loyalty>
    <ShipResearch>0</ShipResearch>
    <TroopResearch>10</TroopResearch>
    <FacilityResearch>0</FacilityResearch>
    <CanBetray>true</CanBetray>
    <JediProbability>0</JediProbability>
    <JediLevel>0</JediLevel>
    <JediLevelVariance>0</JediLevelVariance>
    <CurrentRank>None</CurrentRank>
  </Officer>
</Officers>
```

## Identity and recruitment

| Field | Purpose |
| --- | --- |
| `InstanceID` | Stable identity used by events, missions, saves, and starting-officer rules. |
| `OwnerInstanceID` | Faction that owns the officer when the definition is loaded. |
| `IsMain` | Marks a principal character. |
| `IsRecruitable` | Allows the officer to enter play through recruitment. |
| `RecruitingFactionInstanceIDs` | Factions that may recruit the officer. |
| `Loyalty` | Base loyalty used by betrayal and allegiance mechanics. |
| `CanBetray` | Allows the officer to become a traitor. |
| `IsTraitor` | Initial traitor state. Normally `false`. |

The four entries in `Ratings` are the officer's base `Diplomacy`, `Espionage`, `Combat`, and
`Leadership` ratings. `ShipResearch`, `TroopResearch`, and `FacilityResearch` are stored separately
but participate in research missions as officer ratings.

Each rating may have a matching variance field: `DiplomacyVariance`, `EspionageVariance`,
`CombatVariance`, `LeadershipVariance`, `LoyaltyVariance`, `ShipResearchVariance`,
`TroopResearchVariance`, and `FacilityResearchVariance`. New-game generation adds an inclusive roll
from zero through a positive variance, or from a negative variance through zero. Use `0` for a fixed
value.

`CanHeal` allows an injured officer to recover. `FastHeal` selects the faster recovery amount from
game configuration. Capture, injury, death, movement, and mission-return fields are runtime state
and should not be initialized in the officer catalog.

## Force and command fields

| Field | Purpose |
| --- | --- |
| `JediProbability` | Percentage chance that new-game generation marks the officer Force-sensitive. |
| `JediLevel` | Base Force value used when the officer becomes eligible. It is not a probability. |
| `JediLevelVariance` | Random variation applied when Force eligibility is initialized. |
| `IsKnownJedi` | Whether the officer's Force ability begins publicly known. |
| `IsJediTrainer` | Whether the officer may train another eligible officer. |
| `GrowsForceOnMission` | Whether successful mission activity may advance Force ability. |
| `CurrentRank` | Initial command rank: `None`, `Commander`, `General`, or `Admiral`. |
| `AllowedRanks` | Command ranks this officer may hold. Each item is an `OfficerRank` value. |

## Voice sets

Each voice category contains zero or more extensionless asset paths. The game may choose among
multiple paths in a category.

```xml
<VoiceSet>
  <Order>
    <Path>Pack/Factions/Example/Units/Officers/OFFICER_TYPE_01/Voice/order-01</Path>
  </Order>
  <PersonnelArrived>
    <Path>Pack/Factions/Example/Units/Officers/OFFICER_TYPE_01/Voice/personnel-arrived-01</Path>
  </PersonnelArrived>
  <MissionSuccess>
    <Path>Pack/Factions/Example/Units/Officers/OFFICER_TYPE_01/Voice/mission-success-01</Path>
  </MissionSuccess>
</VoiceSet>
```

Supported categories are `Order`, `PersonnelArrived`, `MissionSuccess`, `MissionFailure`,
`MissionAbort`, `Released`, `Recovered`, `EnemyDetected`, `ForceGrowth`, `ForceUserDiscovered`,
`TraitorDiscovered`, and `RescueAttempt`.

## Starting officers

Scenario generation refers to the officer's `InstanceID`, not `TypeID`:

```xml
<Officers>
  <NumStartingOfficers>
    <Small>1</Small>
    <Medium>1</Medium>
    <Large>1</Large>
  </NumStartingOfficers>
  <StartingOfficers>
    <StartingOfficerRule>
      <OfficerInstanceID>OFFICER_EXAMPLE</OfficerInstanceID>
      <GalaxySizes>
        <GameSize>Small</GameSize>
        <GameSize>Medium</GameSize>
        <GameSize>Large</GameSize>
      </GalaxySizes>
      <DestinationInstanceID>PLANET_EXAMPLE</DestinationInstanceID>
    </StartingOfficerRule>
  </StartingOfficers>
</Officers>
```

Omit the destination to let generation choose a valid starting planet. `DestinationTypeID` targets
a generated planet type; `DestinationInstanceID` targets one stable planet instance.

---

<p align="center"><a href="Index.md">← Unit guide</a> · <a href="Facilities.md">Facilities →</a></p>
