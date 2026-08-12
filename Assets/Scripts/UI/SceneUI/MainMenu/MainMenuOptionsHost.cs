using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the Options menu from the Main Menu.
/// </summary>
public sealed class MainMenuOptionsHost : IOptionsMenuHostActions, IOptionsSaveStore, IDisposable
{
    // Source Resolution.
    private const float _surfaceWidth = 853.33f;
    private const float _surfaceHeight = 480f;

    // Menu Controls.
    private readonly OptionsMenuController _controller;
    private readonly UIWindowManager _windowManager;
    private readonly CancelStack _cancelStack;

    // Content.
    private readonly FactionThemeLibrary _themeLibrary;
    private readonly ContentAssets _contentAssets;

    // Menu Objects.
    private readonly Vector2Int _windowSize;
    private readonly GameObject _canvasObject;
    private readonly GameObject _dimmerObject;

    // Menu State.
    private bool _dirty;

    /// <summary>
    /// Builds the overlay canvas and controller under the supplied menu canvas.
    /// </summary>
    /// <param name="canvasParent">The Main Menu canvas transform.</param>
    /// <param name="prefab">The authored Options overlay prefab.</param>
    /// <param name="contentAssets">The active content asset source.</param>
    /// <param name="themeLibrary">The faction-theme source for save icons.</param>
    /// <param name="userSettings">The user-settings store.</param>
    /// <param name="audioManager">The audio manager for live volume changes.</param>
    /// <param name="inputManager">The input manager exposing key bindings.</param>
    /// <param name="cancelStack">The cancel stack so Escape closes the overlay.</param>
    public MainMenuOptionsHost(
        Transform canvasParent,
        OptionsMenuView prefab,
        ContentAssets contentAssets,
        FactionThemeLibrary themeLibrary,
        UserSettingsManager userSettings,
        AudioManager audioManager,
        InputManager inputManager,
        CancelStack cancelStack
    )
    {
        if (prefab == null)
            throw new ArgumentNullException(nameof(prefab));
        if (canvasParent == null)
            throw new ArgumentNullException(nameof(canvasParent));

        _contentAssets = contentAssets;
        _themeLibrary = themeLibrary;
        _cancelStack = cancelStack;

        RectTransform prefabRect = (RectTransform)prefab.transform;
        _windowSize = new Vector2Int(
            Mathf.RoundToInt(prefabRect.sizeDelta.x),
            Mathf.RoundToInt(prefabRect.sizeDelta.y)
        );

        // Options Menu Canvas.
        _canvasObject = new GameObject(
            "OptionsOverlayCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(_surfaceWidth, _surfaceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        RectTransform modalLayer = new GameObject(
            "ModalLayer",
            typeof(RectTransform)
        ).GetComponent<RectTransform>();
        modalLayer.SetParent(_canvasObject.transform, false);
        modalLayer.anchorMin = Vector2.zero;
        modalLayer.anchorMax = Vector2.one;
        modalLayer.offsetMin = Vector2.zero;
        modalLayer.offsetMax = Vector2.zero;

        // Background Dimmer.
        _dimmerObject = new GameObject(
            "Dimmer",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform dimmer = _dimmerObject.GetComponent<RectTransform>();
        dimmer.SetParent(canvasParent, false);
        dimmer.anchorMin = Vector2.zero;
        dimmer.anchorMax = Vector2.one;
        dimmer.offsetMin = Vector2.zero;
        dimmer.offsetMax = Vector2.zero;
        dimmer.SetAsLastSibling();
        Image dimmerImage = _dimmerObject.GetComponent<Image>();
        dimmerImage.color = new Color(0f, 0f, 0f, 0.8f);
        dimmerImage.raycastTarget = true;
        _dimmerObject.SetActive(false);

        _windowManager = _canvasObject.AddComponent<UIWindowManager>();
        _windowManager.SetContentSource(contentAssets);

        _controller = new OptionsMenuController(
            prefab,
            modalLayer,
            _windowManager,
            GetOverlayPosition,
            _windowManager.DestroyWindow,
            userSettings,
            audioManager,
            inputManager,
            MarkDirty
        );
        _controller.Initialize(this, this);
        cancelStack?.Register(_controller);
    }

    /// <summary>
    /// Gets whether the overlay is currently open.
    /// </summary>
    public bool IsOpen => _controller.IsOpen;

    /// <summary>
    /// Opens the Options menu over the Main Menu.
    /// </summary>
    public void Open()
    {
        _controller.Open();
        _dimmerObject.SetActive(_controller.IsOpen);
        _controller.RenderWindows();
        _dirty = false;
    }

    /// <summary>
    /// Updates the Options menu and background dimmer.
    /// </summary>
    public void Tick()
    {
        if (_dimmerObject.activeSelf != _controller.IsOpen)
            _dimmerObject.SetActive(_controller.IsOpen);
        if (!_dirty)
            return;

        _dirty = false;
        _controller.RenderWindows();
    }

    /// <summary>
    /// Destroys the Options menu objects.
    /// </summary>
    public void Dispose()
    {
        _cancelStack?.Unregister(_controller);
        _controller.Dispose();
        if (_dimmerObject != null)
            UnityEngine.Object.Destroy(_dimmerObject);
        if (_canvasObject != null)
            UnityEngine.Object.Destroy(_canvasObject);
    }

    /// <summary>
    /// Gets whether the Options menu can return to a running game.
    /// </summary>
    public bool CanReturnToGame => false;

    /// <summary>
    /// Gets whether the Options menu can return to the Main Menu.
    /// </summary>
    public bool CanReturnToMainMenu => false;

    /// <summary>
    /// Does nothing because there is no running game to pause.
    /// </summary>
    public void PauseForOptions() { }

    /// <summary>
    /// Does nothing because there is no running game to resume.
    /// </summary>
    public void ResumeFromOptions() { }

    /// <summary>
    /// Returns the existing save games.
    /// </summary>
    /// <returns>The existing saves, newest first.</returns>
    public IReadOnlyList<OptionsSaveSlot> GetSaveSlots()
    {
        List<OptionsSaveSlot> rows = new List<OptionsSaveSlot>();
        foreach (SaveGameEntry entry in SaveGameManager.Instance.GetSavedGames())
        {
            string name = string.IsNullOrEmpty(entry.Metadata?.SaveDisplayName)
                ? entry.FileName
                : entry.Metadata.SaveDisplayName;
            string date =
                entry.Metadata != null
                    ? entry.Metadata.LastSavedUtc.ToLocalTime().ToString("g")
                    : string.Empty;
            rows.Add(
                new OptionsSaveSlot(
                    name,
                    date,
                    ResolveFactionIcon(entry.Metadata?.PlayerFactionID),
                    false,
                    entry.FileName
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Loads the selected save game.
    /// </summary>
    /// <param name="fileName">The save file to load.</param>
    /// <returns>True when a game load started.</returns>
    public bool LoadSave(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        GameRuntime runtime = AppBootstrap.Instance?.GetRuntime();
        if (runtime == null)
            return false;

        // Start the saved game in a new session.
        runtime.EndGame();
        return runtime.LoadGame(fileName);
    }

    /// <summary>
    /// Deletes a save game.
    /// </summary>
    /// <param name="fileName">The save file to delete.</param>
    public void DeleteSave(string fileName)
    {
        SaveGameManager.Instance.DeleteSave(fileName);
    }

    /// <summary>
    /// Renames a save game.
    /// </summary>
    /// <param name="fileName">The save file to rename.</param>
    /// <param name="displayName">The new display name.</param>
    public void RenameSave(string fileName, string displayName)
    {
        SaveGameManager.Instance.SetSaveDisplayName(fileName, displayName);
    }

    /// <summary>
    /// Closes the Options menu.
    /// </summary>
    public void ReturnToMainMenu()
    {
        _controller.Close();
    }

    /// <summary>
    /// Quits the game.
    /// </summary>
    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Centers the overlay window on the source-space surface.
    /// </summary>
    /// <returns>The source-space overlay position.</returns>
    private Vector2Int GetOverlayPosition()
    {
        return new Vector2Int(
            Mathf.RoundToInt(_surfaceWidth / 2f - _windowSize.x / 2f),
            Mathf.RoundToInt(_surfaceHeight / 2f - _windowSize.y / 2f)
        );
    }

    /// <summary>
    /// Resolves a save's faction icon texture from its stored faction id.
    /// </summary>
    /// <param name="factionId">The saved faction id, or null.</param>
    /// <returns>The faction icon texture, or null.</returns>
    private Texture2D ResolveFactionIcon(string factionId)
    {
        if (string.IsNullOrEmpty(factionId) || _themeLibrary == null || _contentAssets == null)
            return null;

        string path = _themeLibrary.GetTheme(factionId)?.SaveMenuSlotIconImagePath;
        return string.IsNullOrEmpty(path) ? null : _contentAssets.GetTexture(path);
    }

    /// <summary>
    /// Re-renders the open overlay in response to a state change.
    /// </summary>
    private void MarkDirty()
    {
        _dirty = true;
    }
}
