using System;
using System.Collections.Generic;
using Rebellion.Game;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hosts the shared in-game Options overlay inside the Main Menu as a settings + load-only screen.
/// The overlay is authored in 853x480 source space, so this builds a dedicated source-space canvas
/// above the (2560x1440) menu canvas and drives the same <see cref="OptionsMenuController"/> the
/// strategy scene uses. Saving is disabled here because there is no running game.
/// </summary>
public sealed class MainMenuOptionsHost : IOptionsMenuActions
{
    private const float _surfaceWidth = 853.33f;
    private const float _surfaceHeight = 480f;

    private readonly OptionsMenuController controller;
    private readonly UIWindowManager windowManager;
    private readonly CancelStack cancelStack;
    private readonly FactionThemeLibrary themeLibrary;
    private readonly ContentAssets contentAssets;
    private readonly Vector2Int windowSize;
    private readonly GameObject dimmerObject;

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

        this.contentAssets = contentAssets;
        this.themeLibrary = themeLibrary;
        this.cancelStack = cancelStack;

        RectTransform prefabRect = (RectTransform)prefab.transform;
        windowSize = new Vector2Int(
            Mathf.RoundToInt(prefabRect.sizeDelta.x),
            Mathf.RoundToInt(prefabRect.sizeDelta.y)
        );

        // Authored as its own ROOT canvas (not nested under the menu canvas): a CanvasScaler is
        // ignored on a nested canvas, which would leave the overlay tiny and mis-placed. As a root
        // canvas its scaler maps the 853x480 source space correctly, matching the strategy scene.
        GameObject canvasObject = new GameObject(
            "OptionsOverlayCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(_surfaceWidth, _surfaceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        RectTransform modalLayer = new GameObject("ModalLayer", typeof(RectTransform))
            .GetComponent<RectTransform>();
        modalLayer.SetParent(canvasObject.transform, false);
        modalLayer.anchorMin = Vector2.zero;
        modalLayer.anchorMax = Vector2.one;
        modalLayer.offsetMin = Vector2.zero;
        modalLayer.offsetMax = Vector2.zero;

        // A full-screen dimmer behind the window, matching the strategy scene's modal backdrop. It
        // is toggled with the overlay's open state (see Tick) so it never lingers after closing.
        dimmerObject = new GameObject(
            "Dimmer",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform dimmer = dimmerObject.GetComponent<RectTransform>();
        dimmer.SetParent(modalLayer, false);
        dimmer.anchorMin = Vector2.zero;
        dimmer.anchorMax = Vector2.one;
        dimmer.offsetMin = Vector2.zero;
        dimmer.offsetMax = Vector2.zero;
        Image dimmerImage = dimmerObject.GetComponent<Image>();
        dimmerImage.color = new Color(0f, 0f, 0f, 0.8f);
        dimmerImage.raycastTarget = true;
        dimmerObject.SetActive(false);

        windowManager = canvasObject.AddComponent<UIWindowManager>();
        windowManager.SetContentSource(contentAssets);

        controller = new OptionsMenuController(
            prefab,
            modalLayer,
            windowManager,
            GetOverlayPosition,
            windowManager.DestroyWindow,
            userSettings,
            audioManager,
            inputManager,
            MarkDirty
        );
        controller.Initialize(this);
        cancelStack?.Register(controller);
    }

    /// <summary>Gets whether the overlay is currently open.</summary>
    public bool IsOpen => controller.IsOpen;

    /// <summary>Opens the Options overlay over the Main Menu.</summary>
    public void Open()
    {
        controller.Open();
    }

    /// <summary>Renders the open overlay and keeps the dimmer in sync; safe to call every frame.</summary>
    public void Tick()
    {
        if (dimmerObject.activeSelf != controller.IsOpen)
            dimmerObject.SetActive(controller.IsOpen);
        controller.RenderWindows();
    }

    /// <summary>Gets whether saving is available (never from the Main Menu).</summary>
    public bool CanSave => false;

    /// <summary>Gets whether loading is available.</summary>
    public bool CanLoad => true;

    /// <summary>Gets whether a running game exists to return to (never from the Main Menu).</summary>
    public bool CanReturnToGame => false;

    /// <summary>Gets whether returning to the Main Menu is possible (never: already there).</summary>
    public bool CanReturnToMainMenu => false;

    /// <summary>No-op: the Main Menu has no running game to pause.</summary>
    public void PauseForOptions() { }

    /// <summary>No-op: the Main Menu has no running game to resume.</summary>
    public void ResumeFromOptions() { }

    /// <summary>Enumerates existing saves (no create-new row without a running game).</summary>
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

    /// <summary>No-op: cannot create a save without a running game.</summary>
    public void CreateNewSave() { }

    /// <summary>No-op: cannot create a save without a running game.</summary>
    /// <param name="displayName">Ignored.</param>
    public void CreateNamedSave(string displayName) { }

    /// <summary>No-op: cannot overwrite a save without a running game.</summary>
    /// <param name="fileName">Ignored.</param>
    /// <param name="displayName">Ignored.</param>
    public void OverwriteSave(string fileName, string displayName) { }

    /// <summary>Cold-starts the selected save, launching the strategy scene.</summary>
    /// <param name="fileName">The save file to load.</param>
    /// <returns>True when a game load started.</returns>
    public bool LoadSave(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        GameRuntime runtime = AppBootstrap.Instance?.GetRuntime();
        if (runtime == null)
            return false;

        // From the Main Menu there is no live session to hot-reload into; end any lingering one so
        // the load cold-starts by launching the strategy scene.
        runtime.EndGame();
        return runtime.LoadGame(fileName);
    }

    /// <summary>Deletes a save file.</summary>
    /// <param name="fileName">The save file to delete.</param>
    public void DeleteSave(string fileName)
    {
        SaveGameManager.Instance.DeleteSave(fileName);
    }

    /// <summary>Renames a save's display name.</summary>
    /// <param name="fileName">The save file to rename.</param>
    /// <param name="displayName">The new display name.</param>
    public void RenameSave(string fileName, string displayName)
    {
        SaveGameManager.Instance.SetSaveDisplayName(fileName, displayName);
    }

    /// <summary>Closes the overlay; the Main Menu is already the destination.</summary>
    public void ReturnToMainMenu()
    {
        controller.Close();
    }

    /// <summary>Quits the application from the Main Menu overlay.</summary>
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
            Mathf.RoundToInt(_surfaceWidth / 2f - windowSize.x / 2f),
            Mathf.RoundToInt(_surfaceHeight / 2f - windowSize.y / 2f)
        );
    }

    /// <summary>
    /// Resolves a save's faction icon texture from its stored faction id.
    /// </summary>
    /// <param name="factionId">The saved faction id, or null.</param>
    /// <returns>The faction icon texture, or null.</returns>
    private Texture2D ResolveFactionIcon(string factionId)
    {
        if (string.IsNullOrEmpty(factionId) || themeLibrary == null || contentAssets == null)
            return null;

        string path = themeLibrary.GetTheme(factionId)?.SaveMenuSlotIconImagePath;
        return string.IsNullOrEmpty(path) ? null : contentAssets.GetTexture(path);
    }

    /// <summary>
    /// Re-renders the open overlay in response to a state change.
    /// </summary>
    private void MarkDirty()
    {
        controller.RenderWindows();
    }
}
