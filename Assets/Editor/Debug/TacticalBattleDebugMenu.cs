using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Generation;
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
    private static double _mainMenuReadyTime;
    private static double _strategyReadyTime;
    private static double _commandedTime;
    private static int _screenshotsTaken;
    private static bool _explosionShotTaken;
    private static bool _tractorShotTaken;
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
    /// Generates a fresh campaign, moves an enemy fleet into the player's home system, and
    /// returns the game ready to save.
    /// </summary>
    /// <param name="pack">The active content pack.</param>
    /// <param name="playerFactionId">The faction the player commands.</param>
    /// <returns>The generated game with a contested home system.</returns>
    internal static GameRoot CreateContestedGame(ContentPack pack, string playerFactionId)
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
    private static void PlayTacticalBattleTest(string playerFactionId, bool automated)
    {
        StrategyViewPrefabBuilder.Rebuild();
        ContentPack pack = ContentPackLoader.OpenActive();
        GameRoot game = CreateContestedGame(pack, playerFactionId);
        SaveGameManager.Instance.SaveGameData(game, _saveFileName, _saveDisplayName);
        Debug.Log(
            $"Tactical battle test save written to {SaveGameManager.Instance.GetSaveFilePath(_saveFileName)}."
        );

        SessionState.SetString(_pendingLoadKey, _saveFileName);
        SessionState.SetBool(_automatedKey, automated);
        _mainMenuReadyTime = 0;
        _strategyReadyTime = 0;
        _commandedTime = 0;
        _screenshotsTaken = 0;
        _explosionShotTaken = false;
        _tractorShotTaken = false;
        _pendingEventShot = null;
        SceneAsset bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(_bootScenePath);
        if (bootScene != null)
            EditorSceneManager.playModeStartScene = bootScene;
        if (!EditorApplication.isPlaying)
            EditorApplication.EnterPlaymode();
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

        if (now - _commandedTime < _screenshotDelays[_screenshotsTaken])
            return;

        CaptureShots(_screenshotsTaken.ToString(), scene);
        _screenshotsTaken++;
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
        EditorSceneManager.playModeStartScene = null;
    }
}
