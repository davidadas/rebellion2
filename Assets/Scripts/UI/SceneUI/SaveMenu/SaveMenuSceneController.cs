using System.IO;
using Rebellion.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SaveMenuSceneController : MonoBehaviour
{
    [SerializeField]
    private RectTransform contentHost;

    [SerializeField]
    private SaveMenuWindowView saveMenuWindow;

    private SaveMenuDataBuilder saveMenuDataBuilder;
    private GameRuntime runtime;

    private void Awake()
    {
        runtime = AppBootstrap.EnsureExists().GetRuntime();
        saveMenuDataBuilder = new SaveMenuDataBuilder();
        VerifyReferences();
        saveMenuWindow.CommandRequested += HandleCommandRequested;
        saveMenuWindow.RenderRequested += HandleRenderRequested;
    }

    private void Start()
    {
        UpdateContentHostLayout();
        Render();
    }

    private void OnDestroy()
    {
        if (saveMenuWindow == null)
            return;

        saveMenuWindow.CommandRequested -= HandleCommandRequested;
        saveMenuWindow.RenderRequested -= HandleRenderRequested;
    }

    private void OnRectTransformDimensionsChange()
    {
        UpdateContentHostLayout();
    }

    private void HandleCommandRequested(SaveMenuWindowCommandRequest request)
    {
        if (request == null)
            return;

        switch (request.Command)
        {
            case SaveMenuWindowCommand.SaveSlot:
                SaveSlot(request.Slot);
                Render();
                break;
            case SaveMenuWindowCommand.LoadSlot:
                if (!LoadSlot(request.Slot))
                    Render();
                break;
            case SaveMenuWindowCommand.ReturnStrategy:
                ReturnToLaunchScene();
                break;
            case SaveMenuWindowCommand.ReturnCockpit:
            case SaveMenuWindowCommand.Airlock:
                ReturnToMainMenu();
                break;
            case SaveMenuWindowCommand.ToggleMusic:
                ToggleMusicVolume();
                Render();
                break;
            case SaveMenuWindowCommand.SetMusicVolume:
                AudioManager.Instance?.SetMusicVolume(request.Value);
                Render();
                break;
            case SaveMenuWindowCommand.SetSfxVolume:
                AudioManager.Instance?.SetSfxVolume(request.Value);
                Render();
                break;
            default:
                Render();
                break;
        }
    }

    private void HandleRenderRequested(SaveMenuWindowView view)
    {
        Render();
    }

    private void Render()
    {
        UpdateContentHostLayout();
        saveMenuWindow.Render(
            saveMenuDataBuilder.CreateRenderData(0, 0, GetPlayerFactionId(), CanSave())
        );
    }

    private bool CanSave()
    {
        return SaveMenuLaunchContext.CanSave && runtime?.HasActiveGame == true;
    }

    private void SaveSlot(int slot)
    {
        if (!SaveMenuDataBuilder.IsValidSaveSlot(slot) || runtime?.HasActiveGame != true)
            return;

        string fileName = SaveMenuDataBuilder.GetSaveSlotFileName(slot);
        string displayName = SaveMenuDataBuilder.GetSaveSlotDisplayName(slot);
        runtime.SaveGame(fileName, displayName);
    }

    private bool LoadSlot(int slot)
    {
        if (!SaveMenuDataBuilder.IsValidSaveSlot(slot))
            return false;

        string fileName = SaveMenuDataBuilder.GetSaveSlotFileName(slot);
        if (!File.Exists(SaveGameManager.Instance.GetSaveFilePath(fileName)))
            return false;

        if (runtime == null || !runtime.LoadGame(fileName))
            return false;

        SaveMenuLaunchContext.OpenFromStrategyView();
        SceneManager.LoadScene(SaveMenuLaunchContext.StrategyViewSceneName);
        return true;
    }

    private void ReturnToLaunchScene()
    {
        if (
            SaveMenuLaunchContext.ReturnSceneName == SaveMenuLaunchContext.StrategyViewSceneName
            && runtime?.HasActiveGame == true
        )
        {
            SceneManager.LoadScene(SaveMenuLaunchContext.StrategyViewSceneName);
            return;
        }

        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        runtime?.EndGame();
        SaveMenuLaunchContext.Reset();
        SceneManager.LoadScene(SaveMenuLaunchContext.MainMenuSceneName);
    }

    private string GetPlayerFactionId()
    {
        GameRoot game = runtime?.GetActiveGame();
        return game?.Summary?.PlayerFactionID;
    }

    private static void ToggleMusicVolume()
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetMusicVolume(AudioManager.Instance.musicVolume > 0f ? 0f : 1f);
    }

    private void UpdateContentHostLayout()
    {
        if (contentHost == null || saveMenuWindow == null)
            return;

        Vector2 sourceSize = GetSourceSize();
        if (sourceSize.x <= 0f || sourceSize.y <= 0f)
            return;

        RectTransform parent = contentHost.parent as RectTransform;
        Rect parentRect = parent == null ? ((RectTransform)transform).rect : parent.rect;
        float scale =
            parentRect.width <= 0f || parentRect.height <= 0f
                ? 1f
                : Mathf.Min(parentRect.width / sourceSize.x, parentRect.height / sourceSize.y);

        contentHost.anchorMin = new Vector2(0.5f, 0.5f);
        contentHost.anchorMax = new Vector2(0.5f, 0.5f);
        contentHost.pivot = new Vector2(0.5f, 0.5f);
        contentHost.anchoredPosition = Vector2.zero;
        contentHost.sizeDelta = sourceSize;
        contentHost.localScale = new Vector3(scale, scale, 1f);
    }

    private Vector2 GetSourceSize()
    {
        RectTransform rect = saveMenuWindow.transform as RectTransform;
        if (rect == null)
            return Vector2.zero;

        Vector2 size = rect.sizeDelta;
        if (size.x <= 0f)
            size.x = rect.rect.width;
        if (size.y <= 0f)
            size.y = rect.rect.height;

        return size;
    }

    private void VerifyReferences()
    {
        if (contentHost == null)
            throw new MissingReferenceException("ContentHost is missing.");
        if (saveMenuWindow == null)
            throw new MissingReferenceException("SaveMenuWindow is missing.");
    }
}
