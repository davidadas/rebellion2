using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates a saved campaign whose home system is already contested and plays it, so the
/// tactical space battle can be reached without playing through the opening turns. The
/// automated variants also take command on their own and write screenshots of the battle.
/// </summary>
[InitializeOnLoad]
public static class TacticalBattleDebugMenu
{
    private const string _allianceMenuPath = "Rebellion/Debug/Play Tactical Battle Test (Alliance)";
    private const string _empireMenuPath = "Rebellion/Debug/Play Tactical Battle Test (Empire)";
    private const string _allianceAutomatedMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Alliance, automated)";
    private const string _empireAutomatedMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Empire, automated)";
    private const string _allianceManyFightersMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Alliance, 100 fighters)";
    private const string _allianceManyFightersAutomatedMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Alliance, 100 fighters, automated)";
    private const string _allianceVersusStationMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Alliance vs Death Star)";
    private const string _allianceVersusStationAutomatedMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Alliance vs Death Star, automated)";
    private const string _empireWithStationMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Empire with Death Star)";
    private const string _empireWithStationAutomatedMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Empire with Death Star, automated)";
    private const string _empireFleetBattleMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Fleet battle, Empire)";
    private const string _allianceFleetBattleMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Fleet battle, Alliance)";
    private const string _empireFleetBattleAutomatedMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Fleet battle, Empire, automated)";
    private const string _allianceFleetBattleAutomatedMenuPath =
        "Rebellion/Debug/Play Tactical Battle Test (Fleet battle, Alliance, automated)";
    private const int _manyFighterSquadronCount = 100;
    private const int _stationTestSquadronCount = 8;
    private const double _stationShotDelaySeconds = 2.5;
    private const double _handOverDelaySeconds = 12.0;
    private const string _stationKey = "Rebellion.TacticalBattleDebug.Station";
    private const string _autoButtonName = "AutoButton";
    private const string _rosterSideButtonName = "RosterSideButton";
    private const string _recoverButtonName = "RecoverButton";
    private const double _gravityWellShotDelaySeconds = 4.0;
    private const double _selectionShotDelaySeconds = 6.0;
    private const double _selectionShotStepSeconds = 0.5;
    private const int _selectionShotStageCount = 9;
    private const string _allianceFactionId = "FNALL1";
    private const string _empireFactionId = "FNEMP1";
    private const string _saveFileName = "tactical-battle-test";
    private const string _saveDisplayName = "Tactical Battle Test";
    private const string _pendingLoadKey = "Rebellion.TacticalBattleDebug.PendingLoad";
    private const string _automatedKey = "Rebellion.TacticalBattleDebug.Automated";
    private const string _bootScenePath = "Assets/Scenes/BootScene.unity";
    private const string _mainMenuSceneName = "MainMenu";
    private const string _strategySceneName = "StrategyView";
    private const string _screenshotFolderName = "TacticalShots";
    private const double _mainMenuSettleSeconds = 1.0;
    private const double _takeCommandTimeoutSeconds = 45.0;
    private static readonly double[] _screenshotDelays = { 0.8, 3.0, 15.0, 45.0, 120.0 };

    // The fleet-battle test's Imperial fleet: a line of Star Destroyers, an Interdictor to hold
    // the enemy in the system, and a screen of escorts. Hangars: 33 squadrons.
    private static readonly ShipGroup[] _empireBattleShips =
    {
        new ShipGroup("CSEM006", 5), // Imperial Star Destroyer
        new ShipGroup("CSEM010", 1), // Interdictor Cruiser
        new ShipGroup("CSEM005", 1), // Victory Destroyer I
        new ShipGroup("CSEM012", 1), // Strike Cruiser
        new ShipGroup("CSEM004", 2), // Carrack Light Cruiser
        new ShipGroup("CSEM007", 2), // Lancer Frigate
    };

    // Each Star Destroyer's six bays: two TIE Fighter squadrons, two of Interceptors, one of
    // Bombers and one of Defenders.
    private static readonly string[] _empireBattleSquadrons =
    {
        "SFEM01",
        "SFEM03",
        "SFEM01",
        "SFEM03",
        "SFEM02",
        "SFEM04",
    };

    // The fleet-battle test's Alliance fleet: Mon Calamari Cruisers with frigates, corvettes,
    // gunships, a carrier and a CC-7700, the Alliance's own interdictor. Hangars: 22 squadrons.
    private static readonly ShipGroup[] _allianceBattleShips =
    {
        new ShipGroup("ALCS008", 4), // Mon Calamari Cruiser
        new ShipGroup("ALCS005", 1), // Alliance Escort Carrier
        new ShipGroup("CSAL011", 1), // Assault Frigate
        new ShipGroup("CSAL010", 1), // CC-7700 Frigate
        new ShipGroup("ALCS007", 2), // Nebulon-B Frigate
        new ShipGroup("ALCS006", 2), // Corellian Corvette
        new ShipGroup("CSAL009", 2), // Corellian Gunship
    };

    // X-wings and A-wings twice over for every Y-wing and B-wing squadron.
    private static readonly string[] _allianceBattleSquadrons =
    {
        "SFAL02",
        "SFAL03",
        "SFAL01",
        "SFAL02",
        "SFAL03",
        "SFAL04",
    };
    private static double _mainMenuReadyTime;
    private static double _strategyReadyTime;
    private static double _commandedTime;
    private static int _screenshotsTaken;
    private static bool _explosionShotTaken;
    private static bool _tractorShotTaken;
    private static bool _superlaserShotTaken;
    private static bool _stationShotTaken;
    private static bool _stationGoneShotTaken;
    private static bool _handedOver;
    private static bool _gravityWellShotTaken;
    private static int _selectionShotStage;
    private static double _selectionShotTime;
    private static string _pendingEventShot;

    /// <summary>
    /// Watches play mode so the prepared save loads itself once the main menu is up and, when
    /// automated, the battle is taken over and photographed.
    /// </summary>
    static TacticalBattleDebugMenu()
    {
        EditorApplication.update += AutoLoadPendingSave;
        EditorApplication.update += DriveAutomatedBattle;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    /// <summary>
    /// Creates and plays a contested campaign as the Alliance.
    /// </summary>
    [MenuItem(_allianceMenuPath, false, 300)]
    public static void PlayTacticalBattleTestAsAlliance()
    {
        PlayTacticalBattleTest(_allianceFactionId, automated: false);
    }

    /// <summary>
    /// Creates and plays a contested campaign as the Empire.
    /// </summary>
    [MenuItem(_empireMenuPath, false, 301)]
    public static void PlayTacticalBattleTestAsEmpire()
    {
        PlayTacticalBattleTest(_empireFactionId, automated: false);
    }

    /// <summary>
    /// Creates and plays a contested campaign as the Alliance, takes command automatically, and
    /// writes screenshots of the battle to the project's Logs folder.
    /// </summary>
    [MenuItem(_allianceAutomatedMenuPath, false, 302)]
    public static void PlayTacticalBattleTestAsAllianceAutomated()
    {
        PlayTacticalBattleTest(_allianceFactionId, automated: true);
    }

    /// <summary>
    /// Creates and plays a contested campaign as the Empire, takes command automatically, and
    /// writes screenshots of the battle to the project's Logs folder.
    /// </summary>
    [MenuItem(_empireAutomatedMenuPath, false, 303)]
    public static void PlayTacticalBattleTestAsEmpireAutomated()
    {
        PlayTacticalBattleTest(_empireFactionId, automated: true);
    }

    /// <summary>
    /// Creates and plays a contested campaign as the Alliance with the home fleet topped up to a
    /// hundred squadrons, to exercise the roster and the arena with many fighters.
    /// </summary>
    [MenuItem(_allianceManyFightersMenuPath, false, 304)]
    public static void PlayTacticalBattleTestAsAllianceWithManyFighters()
    {
        PlayTacticalBattleTest(_allianceFactionId, automated: false, _manyFighterSquadronCount);
    }

    /// <summary>
    /// The automated form of the hundred-squadron Alliance battle.
    /// </summary>
    [MenuItem(_allianceManyFightersAutomatedMenuPath, false, 305)]
    public static void PlayTacticalBattleTestAsAllianceWithManyFightersAutomated()
    {
        PlayTacticalBattleTest(_allianceFactionId, automated: true, _manyFighterSquadronCount);
    }

    /// <summary>
    /// Creates and plays a contested campaign as the Alliance against an Imperial fleet that
    /// brings a Death Star, to exercise the enemy superlaser and the trench run.
    /// </summary>
    [MenuItem(_allianceVersusStationMenuPath, false, 306)]
    public static void PlayTacticalBattleTestAsAllianceVersusStation()
    {
        PlayTacticalBattleTest(_allianceFactionId, automated: false, withStation: true);
    }

    /// <summary>
    /// The automated form of the Alliance battle against a Death Star.
    /// </summary>
    [MenuItem(_allianceVersusStationAutomatedMenuPath, false, 307)]
    public static void PlayTacticalBattleTestAsAllianceVersusStationAutomated()
    {
        PlayTacticalBattleTest(_allianceFactionId, automated: true, withStation: true);
    }

    /// <summary>
    /// Creates and plays a contested campaign as the Empire with a Death Star in the home fleet,
    /// to exercise the superlaser charge bar and an enemy trench run.
    /// </summary>
    [MenuItem(_empireWithStationMenuPath, false, 308)]
    public static void PlayTacticalBattleTestAsEmpireWithStation()
    {
        PlayTacticalBattleTest(_empireFactionId, automated: false, withStation: true);
    }

    /// <summary>
    /// The automated form of the Imperial battle with a Death Star.
    /// </summary>
    [MenuItem(_empireWithStationAutomatedMenuPath, false, 309)]
    public static void PlayTacticalBattleTestAsEmpireWithStationAutomated()
    {
        PlayTacticalBattleTest(_empireFactionId, automated: true, withStation: true);
    }

    /// <summary>
    /// Creates and plays a set-piece fleet battle as the Empire: five Imperial Star Destroyers
    /// with an Interdictor, Lancers and other escorts against four Mon Calamari Cruisers and
    /// their escorts, every hangar full, at a planet with nothing else in orbit.
    /// </summary>
    [MenuItem(_empireFleetBattleMenuPath, false, 310)]
    public static void PlayTacticalBattleTestFleetBattleAsEmpire()
    {
        PlayTacticalBattleTest(_empireFactionId, automated: false, fleetBattle: true);
    }

    /// <summary>
    /// Creates and plays the set-piece fleet battle as the Alliance.
    /// </summary>
    [MenuItem(_allianceFleetBattleMenuPath, false, 311)]
    public static void PlayTacticalBattleTestFleetBattleAsAlliance()
    {
        PlayTacticalBattleTest(_allianceFactionId, automated: false, fleetBattle: true);
    }

    /// <summary>
    /// The automated form of the set-piece fleet battle as the Empire.
    /// </summary>
    [MenuItem(_empireFleetBattleAutomatedMenuPath, false, 312)]
    public static void PlayTacticalBattleTestFleetBattleAsEmpireAutomated()
    {
        PlayTacticalBattleTest(_empireFactionId, automated: true, fleetBattle: true);
    }

    /// <summary>
    /// The automated form of the set-piece fleet battle as the Alliance.
    /// </summary>
    [MenuItem(_allianceFleetBattleAutomatedMenuPath, false, 313)]
    public static void PlayTacticalBattleTestFleetBattleAsAllianceAutomated()
    {
        PlayTacticalBattleTest(_allianceFactionId, automated: true, fleetBattle: true);
    }

    /// <summary>
    /// Generates a fresh campaign, moves an enemy fleet into the player's home system, and
    /// returns the game ready to save.
    /// </summary>
    /// <param name="pack">The active content pack.</param>
    /// <param name="playerFactionId">The faction the player commands.</param>
    /// <param name="playerSquadronCount">
    /// The squadron count to top the player's home fleet up to, or zero to leave it as generated.
    /// </param>
    /// <param name="withStation">
    /// Whether a planet-destroying battle station joins the fleet of the faction that builds it.
    /// </param>
    /// <param name="fleetBattle">
    /// Whether to stage the set-piece fleet battle at a planet of its own instead of contesting
    /// the home system with the generated fleets.
    /// </param>
    /// <returns>The generated game with a contested system.</returns>
    internal static GameRoot CreateContestedGame(
        ContentPack pack,
        string playerFactionId,
        int playerSquadronCount = 0,
        bool withStation = false,
        bool fleetBattle = false
    )
    {
        if (pack == null)
            throw new ArgumentNullException(nameof(pack));

        GameSummary summary = pack.CreateDefaultGameSummary();
        summary.GalaxySize = GameSize.Small;
        summary.Difficulty = GameDifficulty.Easy;
        if (pack.Scenario.PlayableFactionIDs.Contains(playerFactionId))
            summary.PlayerFactionID = playerFactionId;

        GameRoot game = new GameBuilder(summary, pack.GameData).Build();
        Faction player = game.GetPlayerFaction();
        Planet home =
            game.GetSceneNodeByInstanceID<Planet>(player.HQInstanceID)
            ?? throw new InvalidOperationException(
                "The player faction has no headquarters planet."
            );
        Faction enemy =
            game.GetFactions().FirstOrDefault(faction => faction.InstanceID != player.InstanceID)
            ?? throw new InvalidOperationException("The scenario has no opposing faction.");
        if (fleetBattle)
        {
            AddFleetBattle(game, pack, player, enemy);
            return game;
        }

        List<Fleet> fleets = game.Galaxy.GetChildren<Fleet>(recursive: true).ToList();
        Fleet homeFleet = fleets
            .Where(fleet =>
                fleet.GetOwnerInstanceID() == player.InstanceID
                && fleet.GetParentOfType<Planet>() == home
                && fleet.GetChildren<CapitalShip>().Count > 0
            )
            .OrderByDescending(fleet => fleet.GetChildren<CapitalShip>().Count)
            .FirstOrDefault();
        if (homeFleet == null)
        {
            homeFleet =
                SelectFleet(fleets, player.InstanceID, int.MaxValue)
                ?? throw new InvalidOperationException(
                    "The player faction has no fleet to defend with."
                );
            game.MoveNode(homeFleet, home);
        }

        int homeShipCount = homeFleet.GetChildren<CapitalShip>().Count;
        Fleet enemyFleet =
            SelectFleet(fleets, enemy.InstanceID, homeShipCount)
            ?? throw new InvalidOperationException(
                "The opposing faction has no fleet to attack with."
            );
        game.MoveNode(enemyFleet, home);
        if (playerSquadronCount > 0)
            AddSquadrons(game, pack, homeFleet, home, playerSquadronCount);
        if (withStation)
            AddStation(game, pack, homeFleet, enemyFleet, home);
        Debug.Log(
            $"Tactical battle test: {enemyFleet.GetDisplayName()} ({enemyFleet.GetChildren<CapitalShip>().Count} ships) now contests {home.GetDisplayName()} against {homeFleet.GetDisplayName()} ({homeShipCount} ships)."
        );
        return game;
    }

    /// <summary>
    /// Rebuilds the Strategy prefabs, writes the contested save, points play mode at the boot
    /// scene, and starts playing.
    /// </summary>
    /// <param name="playerFactionId">The faction the player commands.</param>
    /// <param name="automated">Whether to take command and photograph the battle automatically.</param>
    /// <param name="playerSquadronCount">
    /// The squadron count to top the player's home fleet up to, or zero to leave it as generated.
    /// </param>
    /// <param name="withStation">Whether a battle station joins the battle.</param>
    /// <param name="fleetBattle">Whether to stage the set-piece fleet battle instead.</param>
    private static void PlayTacticalBattleTest(
        string playerFactionId,
        bool automated,
        int playerSquadronCount = 0,
        bool withStation = false,
        bool fleetBattle = false
    )
    {
        StrategyViewPrefabBuilder.Rebuild();
        ContentPack pack = ContentPackLoader.OpenActive();
        GameRoot game = CreateContestedGame(
            pack,
            playerFactionId,
            playerSquadronCount,
            withStation,
            fleetBattle
        );
        SaveGameManager.Instance.SaveGameData(game, _saveFileName, _saveDisplayName);
        Debug.Log(
            $"Tactical battle test save written to {SaveGameManager.Instance.GetSaveFilePath(_saveFileName)}."
        );

        SessionState.SetString(_pendingLoadKey, _saveFileName);
        SessionState.SetBool(_automatedKey, automated);
        SessionState.SetBool(_stationKey, withStation);
        _mainMenuReadyTime = 0;
        _strategyReadyTime = 0;
        _commandedTime = 0;
        _screenshotsTaken = 0;
        _explosionShotTaken = false;
        _tractorShotTaken = false;
        _superlaserShotTaken = false;
        _stationShotTaken = false;
        _stationGoneShotTaken = false;
        _handedOver = false;
        _gravityWellShotTaken = false;
        _selectionShotStage = 0;
        _pendingEventShot = null;
        SceneAsset bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(_bootScenePath);
        if (bootScene != null)
            EditorSceneManager.playModeStartScene = bootScene;
        if (!EditorApplication.isPlaying)
            EditorApplication.EnterPlaymode();
    }

    /// <summary>
    /// Stages the set-piece fleet battle: a new fleet for each faction at a planet that has no
    /// fleet or garrison fighters of its own, so the battle is exactly the two fleets listed in
    /// this class, with every hangar full.
    /// </summary>
    /// <param name="game">The generated game.</param>
    /// <param name="pack">The content pack whose templates the units are built from.</param>
    /// <param name="player">The faction the player commands.</param>
    /// <param name="enemy">The opposing faction.</param>
    private static void AddFleetBattle(
        GameRoot game,
        ContentPack pack,
        Faction player,
        Faction enemy
    )
    {
        Planet planet =
            SelectBattlefield(game, player)
            ?? throw new InvalidOperationException(
                "No planet is free to stage the fleet battle at."
            );
        UnitFactory factory = new UnitFactory(
            pack.GameData.Buildings,
            pack.GameData.CapitalShips,
            pack.GameData.Starfighters,
            pack.GameData.Regiments,
            pack.GameData.SpecialForces
        );
        foreach (Faction faction in new[] { player, enemy })
        {
            bool imperial = faction.InstanceID == _empireFactionId;
            Fleet fleet = CreateBattleFleet(
                game,
                factory,
                faction,
                planet,
                imperial ? _empireBattleShips : _allianceBattleShips,
                imperial ? _empireBattleSquadrons : _allianceBattleSquadrons
            );
            IReadOnlyList<CapitalShip> ships = fleet.GetChildren<CapitalShip>();
            int squadrons = ships.Sum(ship => ship.GetChildren<Starfighter>().Count);
            Debug.Log(
                $"Tactical battle test: {faction.InstanceID} brings {fleet.GetDisplayName()} to {planet.GetDisplayName()} with {ships.Count} ships and {squadrons} squadrons: {string.Join(", ", ships.GroupBy(ship => ship.GetDisplayName()).Select(group => $"{group.Count()} {group.Key}"))}."
            );
        }
    }

    /// <summary>
    /// Picks the planet the set-piece fleet battle is fought at: one with no fleet in orbit or
    /// on its way and no garrison fighters, away from either headquarters, and one of the
    /// player's own when there is such a planet.
    /// </summary>
    /// <param name="game">The generated game.</param>
    /// <param name="player">The faction the player commands.</param>
    /// <returns>The planet, or null when every planet has a fleet or fighters.</returns>
    private static Planet SelectBattlefield(GameRoot game, Faction player)
    {
        return game.GetSceneNodesByType<Planet>()
            .Where(planet =>
                !planet.IsDestroyed
                && !planet.IsHeadquarters
                && planet.GetChildren<Fleet>().Count == 0
                && planet.GetChildren<Starfighter>().Count == 0
            )
            .OrderByDescending(planet => planet.OwnerInstanceID == player.InstanceID)
            .FirstOrDefault();
    }

    /// <summary>
    /// Builds a fleet from a list of ship types at a planet and fills every hangar, cycling
    /// through a list of fighter types bay by bay across the fleet.
    /// </summary>
    /// <param name="game">The generated game.</param>
    /// <param name="factory">The factory that builds units from the pack's templates.</param>
    /// <param name="faction">The faction that owns the fleet.</param>
    /// <param name="planet">The planet the fleet stands at.</param>
    /// <param name="shipGroups">The fleet's ships, by type.</param>
    /// <param name="squadronTypeIds">The fighter types its hangars are filled with, in order.</param>
    /// <returns>The new fleet.</returns>
    private static Fleet CreateBattleFleet(
        GameRoot game,
        UnitFactory factory,
        Faction faction,
        Planet planet,
        IReadOnlyList<ShipGroup> shipGroups,
        IReadOnlyList<string> squadronTypeIds
    )
    {
        string owner = faction.InstanceID;
        Fleet fleet = faction.CreateFleet();
        game.AttachNode(fleet, planet);
        int bay = 0;
        foreach (ShipGroup group in shipGroups)
        {
            for (int number = 0; number < group.Count; number++)
            {
                CapitalShip ship = factory.Create<CapitalShip>(group.TypeId, owner);
                game.AttachNode(ship, fleet);
                while (ship.GetExcessStarfighterCapacity() > 0)
                {
                    Starfighter squadron = factory.Create<Starfighter>(
                        squadronTypeIds[bay % squadronTypeIds.Count],
                        owner
                    );
                    game.AttachNode(squadron, ship);
                    bay++;
                }
            }
        }

        return fleet;
    }

    /// <summary>
    /// Picks the fleet with capital ships whose size is closest to a target, preferring larger fleets.
    /// </summary>
    /// <param name="fleets">Every fleet in the galaxy.</param>
    /// <param name="ownerInstanceId">The owner whose fleets are considered.</param>
    /// <param name="targetShipCount">The preferred capital-ship count.</param>
    /// <returns>The selected fleet, or null when the owner has no armed fleet.</returns>
    private static Fleet SelectFleet(
        IEnumerable<Fleet> fleets,
        string ownerInstanceId,
        int targetShipCount
    )
    {
        return fleets
            .Where(fleet =>
                fleet.GetOwnerInstanceID() == ownerInstanceId
                && fleet.Movement == null
                && fleet.GetChildren<CapitalShip>().Count > 0
            )
            .OrderBy(fleet => Math.Abs(fleet.GetChildren<CapitalShip>().Count - targetShipCount))
            .ThenByDescending(fleet => fleet.GetChildren<CapitalShip>().Count)
            .FirstOrDefault();
    }

    /// <summary>
    /// Tops a fleet up with squadrons until it carries a given number, cycling through the
    /// fighter types it already has (or the ones its faction can build) so the roster shows
    /// several groups. Ships take squadrons up to their hangar capacity; the rest garrison the
    /// planet, which fights alongside the fleet as planetary fighters.
    /// </summary>
    /// <param name="game">The generated game.</param>
    /// <param name="pack">The content pack whose templates the squadrons are built from.</param>
    /// <param name="fleet">The fleet receiving squadrons.</param>
    /// <param name="planet">The planet that garrisons the squadrons the ships cannot hold.</param>
    /// <param name="squadronCount">The squadron count to reach.</param>
    /// <param name="everyBuildableType">
    /// Whether to cycle through every fighter type the faction builds as well as the ones the
    /// fleet already has.
    /// </param>
    private static void AddSquadrons(
        GameRoot game,
        ContentPack pack,
        Fleet fleet,
        Planet planet,
        int squadronCount,
        bool everyBuildableType = false
    )
    {
        IReadOnlyList<CapitalShip> ships = fleet.GetChildren<CapitalShip>();
        if (ships.Count == 0)
            return;

        string owner = fleet.GetOwnerInstanceID();
        List<Starfighter> existing = ships
            .SelectMany(ship => ship.GetChildren<Starfighter>())
            .ToList();
        List<string> typeIds = existing
            .Select(squadron => squadron.TypeID)
            .Where(typeId => !string.IsNullOrEmpty(typeId))
            .Distinct()
            .ToList();
        if (typeIds.Count == 0 || everyBuildableType)
        {
            typeIds = typeIds
                .Concat(
                    pack.GameData.Starfighters.Where(template =>
                            template.ManufacturingFactionInstanceIDs?.Contains(owner) == true
                        )
                        .Select(template => template.TypeID)
                )
                .Distinct()
                .ToList();
        }

        if (typeIds.Count == 0)
            return;

        UnitFactory factory = new UnitFactory(
            pack.GameData.Buildings,
            pack.GameData.CapitalShips,
            pack.GameData.Starfighters,
            pack.GameData.Regiments,
            pack.GameData.SpecialForces
        );
        // Only the planet's owner can garrison it; a visiting fleet that runs out of hangar
        // space gets another carrier instead.
        bool canGarrison = planet.OwnerInstanceID == owner;
        for (int index = existing.Count; index < squadronCount; index++)
        {
            CapitalShip hangar = fleet
                .GetChildren<CapitalShip>()
                .FirstOrDefault(ship => ship.GetExcessStarfighterCapacity() > 0);
            if (hangar == null && !canGarrison)
            {
                hangar = AddCarrier(game, pack, factory, fleet);
                if (hangar == null)
                    break;
            }

            Starfighter squadron = factory.Create<Starfighter>(
                typeIds[index % typeIds.Count],
                owner
            );
            game.AttachNode(squadron, hangar ?? (ISceneNode)planet);
        }

        Debug.Log(
            $"Tactical battle test: {fleet.GetDisplayName()} now carries {Math.Max(existing.Count, squadronCount)} squadrons of {typeIds.Count} types."
        );
    }

    /// <summary>
    /// Adds the roomiest carrier a fleet's faction builds to the fleet.
    /// </summary>
    /// <param name="game">The generated game.</param>
    /// <param name="pack">The content pack whose templates the carrier is built from.</param>
    /// <param name="factory">The factory that builds units from the pack's templates.</param>
    /// <param name="fleet">The fleet receiving the carrier.</param>
    /// <returns>The new carrier, or null when the faction builds no ship with a hangar.</returns>
    private static CapitalShip AddCarrier(
        GameRoot game,
        ContentPack pack,
        UnitFactory factory,
        Fleet fleet
    )
    {
        string owner = fleet.GetOwnerInstanceID();
        CapitalShip template = pack
            .GameData.CapitalShips.Where(ship =>
                ship.StarfighterCapacity > 0
                && !ship.CanDestroyPlanets
                && ship.ManufacturingFactionInstanceIDs?.Contains(owner) == true
            )
            .OrderByDescending(ship => ship.StarfighterCapacity)
            .FirstOrDefault();
        if (template == null)
            return null;

        CapitalShip carrier = factory.Create<CapitalShip>(template.TypeID, owner);
        game.AttachNode(carrier, fleet);
        return carrier;
    }

    /// <summary>
    /// Adds a planet-destroying battle station to whichever of the two fleets belongs to a
    /// faction that builds one, and gives the other fleet squadrons of every fighter type its
    /// faction builds, so it has the torpedoes a trench run needs.
    /// </summary>
    /// <param name="game">The generated game.</param>
    /// <param name="pack">The content pack whose templates the units are built from.</param>
    /// <param name="homeFleet">The player's fleet at the contested planet.</param>
    /// <param name="enemyFleet">The opposing fleet at the contested planet.</param>
    /// <param name="planet">The contested planet.</param>
    private static void AddStation(
        GameRoot game,
        ContentPack pack,
        Fleet homeFleet,
        Fleet enemyFleet,
        Planet planet
    )
    {
        CapitalShip template =
            pack.GameData.CapitalShips.FirstOrDefault(ship => ship.CanDestroyPlanets)
            ?? throw new InvalidOperationException(
                "The content pack has no planet-destroying capital ship."
            );
        bool homeBuildsIt =
            template.ManufacturingFactionInstanceIDs?.Contains(homeFleet.GetOwnerInstanceID())
            == true;
        Fleet stationFleet = homeBuildsIt ? homeFleet : enemyFleet;
        Fleet opposingFleet = homeBuildsIt ? enemyFleet : homeFleet;
        UnitFactory factory = new UnitFactory(
            pack.GameData.Buildings,
            pack.GameData.CapitalShips,
            pack.GameData.Starfighters,
            pack.GameData.Regiments,
            pack.GameData.SpecialForces
        );
        CapitalShip station = factory.Create<CapitalShip>(
            template.TypeID,
            stationFleet.GetOwnerInstanceID()
        );
        game.AttachNode(station, stationFleet);
        int opposingSquadrons = opposingFleet
            .GetChildren<CapitalShip>()
            .Sum(ship => ship.GetChildren<Starfighter>().Count);
        AddSquadrons(
            game,
            pack,
            opposingFleet,
            planet,
            opposingSquadrons + _stationTestSquadronCount,
            everyBuildableType: true
        );
        Debug.Log(
            $"Tactical battle test: {station.GetDisplayName()} joined {stationFleet.GetDisplayName()}."
        );
    }

    /// <summary>
    /// Loads the prepared save as soon as the running game reaches its main menu.
    /// </summary>
    private static void AutoLoadPendingSave()
    {
        string fileName = SessionState.GetString(_pendingLoadKey, string.Empty);
        if (string.IsNullOrEmpty(fileName) || !EditorApplication.isPlaying)
            return;

        AppBootstrap bootstrap = AppBootstrap.Instance;
        if (bootstrap == null || SceneManager.GetActiveScene().name != _mainMenuSceneName)
        {
            _mainMenuReadyTime = 0;
            return;
        }

        if (_mainMenuReadyTime <= 0)
        {
            _mainMenuReadyTime = EditorApplication.timeSinceStartup + _mainMenuSettleSeconds;
            return;
        }

        if (EditorApplication.timeSinceStartup < _mainMenuReadyTime)
            return;

        SessionState.EraseString(_pendingLoadKey);
        if (!bootstrap.LoadSave(fileName))
            Debug.LogWarning($"Tactical battle test: could not load save '{fileName}'.");
    }

    /// <summary>
    /// In an automated run, takes command of the pending battle once the Strategy scene is up,
    /// then writes screenshots of the Game view and of the arena's render target.
    /// </summary>
    private static void DriveAutomatedBattle()
    {
        if (!SessionState.GetBool(_automatedKey, false) || !EditorApplication.isPlaying)
            return;
        if (SceneManager.GetActiveScene().name != _strategySceneName)
            return;

        double now = EditorApplication.timeSinceStartup;
        if (_strategyReadyTime <= 0)
            _strategyReadyTime = now;

        if (_commandedTime <= 0)
        {
            // The loading screen stays up until the player continues; press it for them first.
            LoadingScreenView loadingScreen =
                UnityEngine.Object.FindFirstObjectByType<LoadingScreenView>();
            if (loadingScreen != null)
            {
                foreach (
                    UnityEngine.UI.Button button in loadingScreen.GetComponentsInChildren<UnityEngine.UI.Button>(
                        false
                    )
                )
                {
                    if (button.interactable)
                        button.onClick.Invoke();
                }

                return;
            }

            // Wait for the battle alert itself, so the session is bound and the player's side
            // is known, exactly as when a player clicks Take Command.
            if (UnityEngine.Object.FindFirstObjectByType<BattleAlertWindowView>() == null)
            {
                if (now - _strategyReadyTime > _takeCommandTimeoutSeconds)
                {
                    SessionState.EraseBool(_automatedKey);
                    Debug.LogWarning("TACTICAL-AUTO gave up waiting for the battle alert.");
                }

                return;
            }

            StrategyController controller =
                UnityEngine.Object.FindFirstObjectByType<StrategyController>();
            if (controller?.TryTakeCommandOfPendingBattle() == true)
            {
                _commandedTime = now;
                Debug.Log("TACTICAL-AUTO took command of the pending battle.");
            }
            else if (now - _strategyReadyTime > _takeCommandTimeoutSeconds)
            {
                SessionState.EraseBool(_automatedKey);
                Debug.LogWarning("TACTICAL-AUTO gave up waiting for a pending battle.");
            }

            return;
        }

        if (_screenshotsTaken >= _screenshotDelays.Length)
        {
            SessionState.EraseBool(_automatedKey);
            return;
        }

        // A battle-station run hands the player's side to automatic control once the opening
        // shots are taken, so nobody has to click for the superlaser to fire or a trench run to
        // be flown.
        if (
            !_handedOver
            && SessionState.GetBool(_stationKey, false)
            && now - _commandedTime > _handOverDelaySeconds
        )
        {
            _handedOver = true;
            if (
                PressButton(
                    UnityEngine.Object.FindFirstObjectByType<TacticalBattleView>(),
                    _autoButtonName
                )
            )
                Debug.Log("TACTICAL-AUTO handed the player's side to automatic control.");
        }

        TacticalBattleScene scene = UnityEngine.Object.FindFirstObjectByType<TacticalBattleScene>(
            FindObjectsInactive.Include
        );
        // Effects are short-lived, so they get their own shots when they first appear: the camera
        // backs out over the whole arena on one update and the next update captures it.
        if (_pendingEventShot != null)
        {
            CaptureShots(_pendingEventShot, scene);
            _pendingEventShot = null;
        }
        else if (!_explosionShotTaken && scene?.HasActiveExplosions == true)
        {
            _explosionShotTaken = true;
            _pendingEventShot = "event-explosion";
            scene.FrameArena();
        }
        else if (!_tractorShotTaken && scene?.HasActiveTractorEffects == true)
        {
            _tractorShotTaken = true;
            _pendingEventShot = "event-tractor";
            scene.FrameArena();
        }
        else if (!_superlaserShotTaken && scene?.HasActiveSuperlaserBeams == true)
        {
            _superlaserShotTaken = true;
            _pendingEventShot = "event-superlaser";
            scene.FrameArena();
        }
        else if (
            !_gravityWellShotTaken
            && _selectionShotStage == 0
            && now - _commandedTime > _gravityWellShotDelaySeconds
            && scene?.HasActiveGravityWellEffects == true
        )
        {
            // Taken before anything is selected, while the camera still follows nothing.
            _gravityWellShotTaken = true;
            _pendingEventShot = "gravity-well";
            scene.FrameGravityWell();
        }
        else if (
            !_stationShotTaken
            && now - _commandedTime > _stationShotDelaySeconds
            && scene?.HasVisibleStation == true
        )
        {
            // A battle station stands beyond the arena, so it gets a shot framed on it.
            _stationShotTaken = true;
            _pendingEventShot = "station";
            scene.FrameStation();
        }
        else if (
            _stationShotTaken
            && !_stationGoneShotTaken
            && Time.timeScale > 0f
            && scene?.HasVisibleStation == false
        )
        {
            // The station has blown up or jumped out, and any movie over it has finished.
            _stationGoneShotTaken = true;
            _pendingEventShot = "event-station-gone";
            scene.FrameStation();
        }
        else
        {
            StepSelectionShots(now, scene);
        }

        if (now - _commandedTime < _screenshotDelays[_screenshotsTaken])
            return;

        CaptureShots(_screenshotsTaken.ToString(), scene);
        _screenshotsTaken++;
    }

    /// <summary>
    /// Photographs the selection box once in an automated run. It picks and zooms to the
    /// player's first ship from the roster and captures it, does the same for the enemy's first
    /// ship, then hands the roster and the camera back. Last it picks the player's first
    /// squadron, recalls it to a hangar with the Recover button and captures that; the timed
    /// shots that follow show the squadron's roster slot once it is aboard. Each step waits for
    /// the one before it to be drawn, and a capture never shares a step with a change, because
    /// the Game view is captured at the end of the frame.
    /// </summary>
    /// <param name="now">The editor time, in seconds.</param>
    /// <param name="scene">The tactical scene, or null when it is not in the hierarchy.</param>
    private static void StepSelectionShots(double now, TacticalBattleScene scene)
    {
        if (_selectionShotStage >= _selectionShotStageCount)
            return;

        bool first = _selectionShotStage == 0;
        double waited = now - (first ? _commandedTime : _selectionShotTime);
        if (waited < (first ? _selectionShotDelaySeconds : _selectionShotStepSeconds))
            return;

        TacticalBattleView view = UnityEngine.Object.FindFirstObjectByType<TacticalBattleView>();
        switch (_selectionShotStage)
        {
            case 0:
            case 3:
                // The first press picks the unit and the second zooms the camera in on it.
                PressFirstRosterSlot(view);
                PressFirstRosterSlot(view);
                break;
            case 1:
                CaptureShots("selection-friendly", scene);
                break;
            case 2:
                PressButton(view, _rosterSideButtonName);
                break;
            case 4:
                CaptureShots("selection-enemy", scene);
                break;
            case 5:
                PressButton(view, _rosterSideButtonName);
                scene?.FrameArena();
                break;
            case 6:
                PressFirstSquadronSlot(view);
                break;
            case 7:
                if (PressButton(view, _recoverButtonName))
                    Debug.Log("TACTICAL-AUTO recalled the first squadron to a hangar.");
                else
                    Debug.LogWarning("TACTICAL-AUTO could not press Recover.");
                break;
            default:
                CaptureShots("recover", scene);
                break;
        }

        _selectionShotStage++;
        _selectionShotTime = now;
    }

    /// <summary>
    /// Clicks the first slot the roster bar shows, which is the listed side's first ship.
    /// </summary>
    /// <param name="view">The tactical battle view, or null when it is not in the hierarchy.</param>
    private static void PressFirstRosterSlot(TacticalBattleView view)
    {
        TacticalRosterSlotView slot = view
            ?.GetComponentsInChildren<TacticalRosterSlotView>(false)
            .FirstOrDefault();
        UnityEngine.UI.Button button = slot?.GetComponent<UnityEngine.UI.Button>();
        if (button?.interactable == true)
            button.onClick.Invoke();
        else
            Debug.LogWarning("TACTICAL-AUTO found no roster slot to press.");
    }

    /// <summary>
    /// Clicks the roster bar's first squadron slot, which picks the listed side's first squadron.
    /// </summary>
    /// <param name="view">The tactical battle view, or null when it is not in the hierarchy.</param>
    private static void PressFirstSquadronSlot(TacticalBattleView view)
    {
        TacticalRosterSlotView slot = view
            ?.GetComponentsInChildren<TacticalRosterSlotView>(false)
            .FirstOrDefault(candidate =>
                candidate.name.StartsWith(
                    TacticalBattleView.RosterSquadronSlotPrefix,
                    StringComparison.Ordinal
                )
            );
        UnityEngine.UI.Button button = slot?.GetComponent<UnityEngine.UI.Button>();
        if (button?.interactable == true)
            button.onClick.Invoke();
        else
            Debug.LogWarning("TACTICAL-AUTO found no squadron slot to press.");
    }

    /// <summary>
    /// Clicks a named button of the tactical battle view when it accepts input.
    /// </summary>
    /// <param name="view">The tactical battle view, or null when it is not in the hierarchy.</param>
    /// <param name="buttonName">The button object's name.</param>
    /// <returns>True when the button was clicked.</returns>
    private static bool PressButton(TacticalBattleView view, string buttonName)
    {
        UnityEngine.UI.Button button = view
            ?.GetComponentsInChildren<UnityEngine.UI.Button>(true)
            .FirstOrDefault(candidate => candidate.name == buttonName);
        if (button?.interactable != true)
            return false;

        button.onClick.Invoke();
        return true;
    }

    /// <summary>
    /// Writes a Game-view screenshot and an arena render-target snapshot.
    /// </summary>
    /// <param name="label">The label that names the files.</param>
    /// <param name="scene">The tactical scene, or null when it is not in the hierarchy.</param>
    private static void CaptureShots(string label, TacticalBattleScene scene)
    {
        string folder = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Logs", _screenshotFolderName)
        );
        Directory.CreateDirectory(folder);
        string stamp = DateTime.Now.ToString("HHmmss");
        string gameViewPath = Path.Combine(folder, $"gameview-{label}-{stamp}.png");
        ScreenCapture.CaptureScreenshot(gameViewPath);
        Debug.Log($"TACTICAL-SHOT gameview {gameViewPath}");

        string arenaPath = Path.Combine(folder, $"arena-{label}-{stamp}.png");
        if (scene?.TryWriteDebugSnapshot(arenaPath) == true)
            Debug.Log($"TACTICAL-SHOT arena {arenaPath}");
        else
            Debug.LogWarning(
                $"TACTICAL-SHOT arena snapshot unavailable (scene found={scene != null}, active={scene?.gameObject.activeInHierarchy == true})."
            );
    }

    /// <summary>
    /// Restores the normal play-mode start scene once the test session ends.
    /// </summary>
    /// <param name="state">The play-mode transition.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        SessionState.EraseString(_pendingLoadKey);
        SessionState.EraseBool(_automatedKey);
        SessionState.EraseBool(_stationKey);
        EditorSceneManager.playModeStartScene = null;
    }

    /// <summary>
    /// Names one ship type in a test fleet and how many of it the fleet has.
    /// </summary>
    private readonly struct ShipGroup
    {
        /// <summary>
        /// Creates a group of ships of one type.
        /// </summary>
        /// <param name="typeId">The capital ship type.</param>
        /// <param name="count">How many ships of that type the fleet has.</param>
        internal ShipGroup(string typeId, int count)
        {
            TypeId = typeId;
            Count = count;
        }

        internal string TypeId { get; }

        internal int Count { get; }
    }
}
