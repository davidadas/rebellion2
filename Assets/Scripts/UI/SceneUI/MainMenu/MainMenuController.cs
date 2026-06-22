using System;
using System.Collections.Generic;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Orchestrates the main menu scene and initializes launch parameters
/// for a new or loaded game. Also coordinates UI-driven actions such as
/// playing cutscenes and managing audio transitions.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private const float _pausedAnimatorSpeed = 0f;
    private const float _playingAnimatorSpeed = 1f;

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

    [SerializeField]
    private Toggle[] difficultyToggles = Array.Empty<Toggle>();

    [SerializeField]
    private int[] difficultyValues = Array.Empty<int>();

    [SerializeField]
    private Animator[] difficultyAnimators = Array.Empty<Animator>();

    private GameVictoryCondition current;
    private readonly List<Toggle> boundDifficultyToggles = new List<Toggle>();
    private readonly List<UnityAction<bool>> boundDifficultyListeners =
        new List<UnityAction<bool>>();

    /// <summary>
    /// Initializes the main menu state and resets launch parameters
    /// to default values.
    /// </summary>
    private void Awake()
    {
        Instance = this;
        GameLaunchContext.Reset();
        SaveMenuLaunchContext.Reset();

        BindDifficultyToggles();
        ApplySelectedDifficulty();
        UpdateVictoryConditionText();
    }

    private void OnDestroy()
    {
        UnbindDifficultyToggles();
    }

    private void BindDifficultyToggles()
    {
        UnbindDifficultyToggles();

        int count = Mathf.Min(difficultyToggles.Length, difficultyValues.Length);
        for (int i = 0; i < count; i++)
        {
            Toggle toggle = difficultyToggles[i];
            int difficulty = difficultyValues[i];

            if (toggle == null)
                continue;

            UnityAction<bool> listener = isOn =>
            {
                if (isOn)
                    SelectGameDifficulty(difficulty);
            };

            toggle.onValueChanged.AddListener(listener);
            boundDifficultyToggles.Add(toggle);
            boundDifficultyListeners.Add(listener);
        }
    }

    private void UnbindDifficultyToggles()
    {
        int count = Mathf.Min(boundDifficultyToggles.Count, boundDifficultyListeners.Count);
        for (int i = 0; i < count; i++)
        {
            Toggle toggle = boundDifficultyToggles[i];
            UnityAction<bool> listener = boundDifficultyListeners[i];

            if (toggle != null)
                toggle.onValueChanged.RemoveListener(listener);
        }

        boundDifficultyToggles.Clear();
        boundDifficultyListeners.Clear();
    }

    private void ApplySelectedDifficulty()
    {
        int count = Mathf.Min(difficultyToggles.Length, difficultyValues.Length);
        for (int i = 0; i < count; i++)
        {
            Toggle toggle = difficultyToggles[i];
            if (toggle != null && toggle.isOn)
            {
                SelectGameDifficulty(difficultyValues[i]);
                return;
            }
        }

        RefreshDifficultyVisuals();
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
        RefreshDifficultyVisuals();
    }

    public void RefreshDifficultyVisuals()
    {
        int count = Mathf.Min(difficultyToggles.Length, difficultyAnimators.Length);
        for (int i = 0; i < count; i++)
        {
            Animator animator = difficultyAnimators[i];
            Toggle toggle = difficultyToggles[i];

            if (animator != null && toggle != null)
                animator.speed = toggle.isOn ? _pausedAnimatorSpeed : _playingAnimatorSpeed;
        }
    }

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

    public void OpenLoadGameMenu()
    {
        SaveMenuLaunchContext.OpenFromMainMenu();
        SceneManager.LoadScene(SaveMenuLaunchContext.SaveMenuSceneName);
    }

    /// <summary>
    /// Callback invoked after the credits cutscene finishes.
    /// Restores the main menu music.
    /// </summary>
    private void OnCreditsFinished()
    {
        AudioManager.Instance.PlayTrack("Audio/Music/battle_of_endor_medley", true);
    }

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
        GameLaunchContext.PlayIntroCutscene = true;

        AudioManager.Instance.StopMusic();

        SceneManager.LoadScene(SaveMenuLaunchContext.StrategyViewSceneName);
    }
}
