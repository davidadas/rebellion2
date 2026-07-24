using Rebellion.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Video;

/// <summary>
/// Owns main-menu launch state, audio, cutscenes, and scene navigation.
/// </summary>
public sealed class MainMenuController : MonoBehaviour
{
    private const string _menuMusicPath = "Audio/Music/battle_of_endor_1_medley";

    [SerializeField]
    private MainMenuView view;

    [FormerlySerializedAs("CreditsClip")]
    [SerializeField]
    private VideoClip creditsClip;

    [SerializeField]
    [Min(0f)]
    private float creditsMusicFadeDuration = 0.5f;

    private GameVictoryCondition currentVictoryCondition;

    /// <summary>
    /// Resets launch state and renders the authored initial selections.
    /// </summary>
    private void Awake()
    {
        if (Application.isPlaying)
        {
            if (view == null)
                throw new MissingReferenceException($"{name} has no main-menu view.");
            if (creditsClip == null)
                throw new MissingReferenceException($"{name} has no credits clip.");
        }

        GameLaunchContext.Reset();
        SaveMenuLaunchContext.Reset();
        currentVictoryCondition = GameLaunchContext.Summary.VictoryCondition;

        if (view == null)
            return;

        view.RenderVictoryCondition(currentVictoryCondition);
        if (view.TryGetSelectedDifficulty(out GameDifficulty difficulty))
            SelectGameDifficulty(difficulty);
    }

    /// <summary>
    /// Subscribes to semantic view requests while the controller is active.
    /// </summary>
    private void OnEnable()
    {
        if (view == null)
            return;

        view.GalaxySizeSelected += SelectGalaxySize;
        view.DifficultySelected += SelectGameDifficulty;
        view.StartGameRequested += HandleStartGameRequested;
        view.VictoryConditionToggleRequested += HandleVictoryConditionToggleRequested;
        view.LoadGameRequested += OpenLoadGameMenu;
        view.CreditsRequested += ShowCredits;
        view.AudioCueRequested += PlayAudioCue;
    }

    /// <summary>
    /// Starts the main-menu music.
    /// </summary>
    private void Start()
    {
        AudioManager.EnsureExists().PlayTrack(_menuMusicPath, true);
    }

    /// <summary>
    /// Unsubscribes from semantic view requests when the controller is disabled.
    /// </summary>
    private void OnDisable()
    {
        if (view == null)
            return;

        view.GalaxySizeSelected -= SelectGalaxySize;
        view.DifficultySelected -= SelectGameDifficulty;
        view.StartGameRequested -= HandleStartGameRequested;
        view.VictoryConditionToggleRequested -= HandleVictoryConditionToggleRequested;
        view.LoadGameRequested -= OpenLoadGameMenu;
        view.CreditsRequested -= ShowCredits;
        view.AudioCueRequested -= PlayAudioCue;
    }

    /// <summary>
    /// Selects the player faction for the next game.
    /// </summary>
    /// <param name="factionId">The configured faction identifier.</param>
    internal void SelectFaction(string factionId)
    {
        GameLaunchContext.Summary.PlayerFactionID = factionId;
    }

    /// <summary>
    /// Selects the galaxy size for the next game.
    /// </summary>
    /// <param name="size">The selected galaxy size.</param>
    internal void SelectGalaxySize(GameSize size)
    {
        GameLaunchContext.Summary.GalaxySize = size;
    }

    /// <summary>
    /// Selects the victory condition for the next game and refreshes its presentation.
    /// </summary>
    /// <param name="condition">The selected victory condition.</param>
    internal void SelectVictoryCondition(GameVictoryCondition condition)
    {
        currentVictoryCondition = condition;
        GameLaunchContext.Summary.VictoryCondition = condition;
        view?.RenderVictoryCondition(condition);
    }

    /// <summary>
    /// Selects the difficulty for the next game.
    /// </summary>
    /// <param name="difficulty">The selected difficulty.</param>
    internal void SelectGameDifficulty(GameDifficulty difficulty)
    {
        GameLaunchContext.Summary.Difficulty = difficulty;
    }

    /// <summary>
    /// Applies the requested faction selection and starts a new game.
    /// </summary>
    /// <param name="factionId">The configured faction identifier.</param>
    private void HandleStartGameRequested(string factionId)
    {
        SelectFaction(factionId);
        StartGame();
    }

    /// <summary>
    /// Toggles between the supported victory conditions.
    /// </summary>
    private void HandleVictoryConditionToggleRequested()
    {
        SelectVictoryCondition(
            currentVictoryCondition == GameVictoryCondition.Conquest
                ? GameVictoryCondition.Headquarters
                : GameVictoryCondition.Conquest
        );
    }

    /// <summary>
    /// Plays a UI audio cue emitted by the view.
    /// </summary>
    /// <param name="resourcePath">The audio resource path.</param>
    private void PlayAudioCue(string resourcePath)
    {
        AudioManager.EnsureExists().PlaySfx(resourcePath);
    }

    /// <summary>
    /// Plays the credits cutscene and restores menu music when it finishes.
    /// </summary>
    private void ShowCredits()
    {
        AudioManager.EnsureExists().FadeOutMusic(creditsMusicFadeDuration);
        CutsceneManager.Instance.Play(creditsClip, OnCreditsFinished);
    }

    /// <summary>
    /// Opens the save-menu scene in load mode.
    /// </summary>
    private void OpenLoadGameMenu()
    {
        SaveMenuLaunchContext.OpenFromMainMenu();
        SceneManager.LoadScene(SaveMenuLaunchContext.SaveMenuSceneName);
    }

    /// <summary>
    /// Restarts main-menu music after the credits cutscene.
    /// </summary>
    private void OnCreditsFinished()
    {
        AudioManager.EnsureExists().PlayTrack(_menuMusicPath, true);
    }

    /// <summary>
    /// Finalizes new-game launch state and opens the strategy scene.
    /// </summary>
    private void StartGame()
    {
        GameLaunchContext.IsLoadGame = false;
        GameLaunchContext.SaveFileName = null;
        GameLaunchContext.PlayIntroCutscene = true;

        AudioManager.EnsureExists().StopMusic();
        SceneManager.LoadScene(SaveMenuLaunchContext.StrategyViewSceneName);
    }
}
