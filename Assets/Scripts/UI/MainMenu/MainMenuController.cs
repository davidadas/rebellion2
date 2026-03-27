using System.Diagnostics;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Orchestrates the main menu scene and initializes launch parameters
/// for a new or loaded game. Also coordinates UI-driven actions such as
/// playing cutscenes and managing audio transitions.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // Singleton reference to the active MainMenuController instance.
    public static MainMenuController Instance;

    // Video clip used for the credits sequence.
    [SerializeField]
    private VideoClip CreditsClip;

    // Icon displayed when the Headquarters victory condition is selected.
    [SerializeField]
    private GameObject headquartersOnlyIcon;

    // UI text element for displaying the victory condition section header.
    [SerializeField]
    private TMP_Text victoryConditionText;

    private GameVictoryCondition current;

    /// <summary>
    /// Initializes the main menu state and resets launch parameters
    /// to default values.
    /// </summary>
    private void Awake()
    {
        Instance = this;
        GameLaunchContext.Reset();

        UpdateVictoryConditionText();
    }

    /// <summary>
    /// Begins main menu background music.
    /// </summary>
    public void Start()
    {
        AudioManager.Instance.PlayTrack("Audio/Music/battle_of_endor_medley", true);
    }

    /// <summary>
    /// Updates the selected player faction in the launch context.
    /// </summary>
    public void SelectFaction(string factionId)
    {
        GameLaunchContext.Summary.PlayerFactionID = factionId;
    }

    /// <summary>
    /// Updates the selected galaxy size in the launch context.
    /// </summary>
    public void SelectGameSize(GameSize size)
    {
        GameLaunchContext.Summary.GalaxySize = size;
    }

    /// <summary>
    /// Updates the selected victory condition in the launch context.
    /// </summary>
    public void SelectVictoryCondition(GameVictoryCondition condition)
    {
        GameLaunchContext.Summary.VictoryCondition = condition;
    }

    /// <summary>
    /// Updates the selected resource availability in the launch context.
    /// </summary>
    public void SelectResourceAvailability(GameResourceAvailability availability)
    {
        GameLaunchContext.Summary.ResourceAvailability = availability;
    }

    /// <summary>
    /// Updates the selected starting research level in the launch context.
    /// </summary>
    public void SelectStartingResearchLevel(int level)
    {
        GameLaunchContext.Summary.StartingResearchLevel = level;
    }

    /// <summary>
    /// Updates the selected game difficulty in the launch context.
    /// </summary>
    public void SelectGameDifficulty(int difficulty)
    {
        GameLaunchContext.Summary.Difficulty = (GameDifficulty)difficulty;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="size"></param>
    public void SelectGalaxySize(int size)
    {
        GameLaunchContext.Summary.GalaxySize = (GameSize)size;
    }

    public void ToggleVictoryCondition()
    {
        current =
            current == GameVictoryCondition.Conquest
                ? GameVictoryCondition.Headquarters
                : GameVictoryCondition.Conquest;
        GameLaunchContext.Summary.VictoryCondition = current;

        if (headquartersOnlyIcon != null)
        {
            headquartersOnlyIcon.SetActive(current == GameVictoryCondition.Headquarters);
        }

        UpdateVictoryConditionText();
    }

    /// <summary>
    /// Plays the credits cutscene and restores menu music upon completion.
    /// </summary>
    public void ShowCredits()
    {
        // Fade out current music for a smooth transition.
        AudioManager.Instance.FadeOutMusic(0.5f);

        // Play the credits cutscene.
        CutsceneManager.Instance.Play(CreditsClip, OnCreditsFinished);
    }

    /// <summary>
    /// Callback invoked after the credits cutscene finishes.
    /// Restores the main menu music.
    /// </summary>
    private void OnCreditsFinished()
    {
        AudioManager.Instance.PlayTrack("Audio/Music/battle_of_endor_medley", true);
    }

    /// <summary>
    ///
    /// </summary>
    private void UpdateVictoryConditionText()
    {
        string text =
            current == GameVictoryCondition.Headquarters ? "Headquarters Victory" : "Standard Game";

        if (victoryConditionText != null)
            victoryConditionText.text = text;
    }

    /// <summary>
    /// Finalizes launch parameters and transitions into the Strategy scene.
    /// </summary>
    public void StartGame()
    {
        GameLaunchContext.IsLoadGame = false;
        GameLaunchContext.SaveFileName = null;

        AudioManager.Instance.StopMusic();

        SceneManager.LoadScene("StrategyView");
    }
}
