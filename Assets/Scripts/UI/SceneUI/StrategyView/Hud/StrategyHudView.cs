using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class StrategyHudView : MonoBehaviour
{
    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private TextMeshProUGUI tickTextField;

    [SerializeField]
    private TextMeshProUGUI rawMaterialsTextField;

    [SerializeField]
    private TextMeshProUGUI refinedMaterialsTextField;

    [SerializeField]
    private TextMeshProUGUI maintenanceTextField;

    [SerializeField]
    private RawImage speedIndicatorImage;

    [SerializeField]
    private RawImage pressedMainButtonImage;

    [SerializeField]
    private StrategyHudButtonView[] buttonViews = System.Array.Empty<StrategyHudButtonView>();

    [SerializeField]
    private StrategyHudButtonView speedContextView;

    [SerializeField]
    private RawImage[] messageNotificationImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private Button[] messageNotificationButtons = System.Array.Empty<Button>();

    private readonly List<int> messageNotificationTabs = new List<int>();

    private UIContext uiContext;
    private int pressedButtonAction;
    private bool eventsBound;

    public void Initialize(UIContext context)
    {
        uiContext = context;
        BindEvents();
    }

    public void Render(StrategyHudRenderData data)
    {
        VerifyReferences();

        Texture2D backgroundTexture = GetBackgroundTexture();
        backgroundImage.texture = backgroundTexture;
        backgroundImage.enabled = backgroundTexture != null;
        backgroundImage.raycastTarget = false;

        SetCounter(tickTextField, data.TickText, StrategyHudCounterKind.Tick);
        SetCounter(
            rawMaterialsTextField,
            data.RawMaterialsText,
            StrategyHudCounterKind.RawMaterials
        );
        SetCounter(
            refinedMaterialsTextField,
            data.RefinedMaterialsText,
            StrategyHudCounterKind.RefinedMaterials
        );
        SetCounter(maintenanceTextField, data.MaintenanceText, StrategyHudCounterKind.Maintenance);

        SetImageAtSourceRect(
            speedIndicatorImage,
            GetSpeedIndicatorTexture(GetSourceSpeed(data.Speed)),
            GetHudTheme()?.SpeedIndicatorSourceLayout
        );
        RenderMessageNotifications(data);
        SetPressedMainButtonOverlay();
        RenderButtons();
    }

    private void Awake()
    {
        VerifyReferences();
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (tickTextField == null)
            throw new MissingReferenceException($"{name}/TickTextField is missing.");
        if (rawMaterialsTextField == null)
            throw new MissingReferenceException($"{name}/RawMaterialsTextField is missing.");
        if (refinedMaterialsTextField == null)
            throw new MissingReferenceException($"{name}/RefinedMaterialsTextField is missing.");
        if (maintenanceTextField == null)
            throw new MissingReferenceException($"{name}/MaintenanceTextField is missing.");
        if (speedIndicatorImage == null)
            throw new MissingReferenceException($"{name}/SpeedIndicatorImage is missing.");
        if (pressedMainButtonImage == null)
            throw new MissingReferenceException($"{name}/PressedMainButtonImage is missing.");
        if (
            messageNotificationImages == null
            || messageNotificationButtons == null
            || messageNotificationImages.Length == 0
            || messageNotificationImages.Length != messageNotificationButtons.Length
        )
            throw new MissingReferenceException($"{name}/Message notification slots are missing.");
        for (int i = 0; i < messageNotificationImages.Length; i++)
        {
            if (messageNotificationImages[i] == null)
                throw new MissingReferenceException(
                    $"{name}/MessageNotificationImage{i} is missing."
                );
            if (messageNotificationButtons[i] == null)
                throw new MissingReferenceException(
                    $"{name}/MessageNotificationButton{i} is missing."
                );
        }
        if (buttonViews == null || buttonViews.Length == 0)
            throw new MissingReferenceException($"{name}/HUD button views are missing.");
        for (int i = 0; i < buttonViews.Length; i++)
        {
            if (buttonViews[i] == null)
                throw new MissingReferenceException($"{name}/HudButton{i} is missing.");
        }
        if (speedContextView == null)
            throw new MissingReferenceException($"{name}/SpeedContextView is missing.");
    }

    private void BindEvents()
    {
        if (eventsBound)
            return;

        foreach (StrategyHudButtonView button in buttonViews)
        {
            if (button == null)
                continue;

            button.Pressed += HandleButtonPressed;
            button.Released += HandleButtonReleased;
            button.Clicked += HandleButtonClicked;
        }

        if (speedContextView != null)
            speedContextView.ContextRequested += HandleSpeedContextRequested;

        BindMessageNotificationButtons();
        eventsBound = true;
    }

    private void RenderButtons()
    {
        int index = 0;
        foreach (StrategyHudButtonTheme button in GetHudButtons())
        {
            if (index >= buttonViews.Length)
                break;

            buttonViews[index].Render(button.Action, button.HitArea);
            index++;
        }

        for (int i = index; i < buttonViews.Length; i++)
            buttonViews[i].gameObject.SetActive(false);

        speedContextView.Render(0, GetHudTheme()?.SpeedContextSourceLayout);
    }

    private void HandleButtonPressed(StrategyHudButtonView button, PointerEventData eventData)
    {
        if (button == null || button.Action == 0)
            return;

        pressedButtonAction = button.Action;
        uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void HandleButtonReleased(StrategyHudButtonView button, PointerEventData eventData)
    {
        if (button == null || pressedButtonAction == 0 || button.Action != pressedButtonAction)
            return;

        pressedButtonAction = 0;
        uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void HandleButtonClicked(StrategyHudButtonView button, PointerEventData eventData)
    {
        if (button == null || button.Action == 0)
            return;

        pressedButtonAction = 0;
        if (TryGetSourcePosition(eventData, out int x, out int y))
            uiContext?.Dispatcher.Send(
                new StrategyUIRequests.ReleaseHudButton(button.Action, x, y)
            );
        else
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void HandleSpeedContextRequested(
        StrategyHudButtonView button,
        PointerEventData eventData
    )
    {
        if (TryGetSourcePosition(eventData, out int x, out int y))
            uiContext?.Dispatcher.Send(new StrategyUIRequests.OpenSpeedContextMenu(x, y));
    }

    private bool TryGetSourcePosition(PointerEventData eventData, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (eventData == null)
            return false;

        RectTransform rect = transform as RectTransform;
        if (
            rect == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 local
            )
        )
            return false;

        int width = Mathf.RoundToInt(rect.sizeDelta.x);
        int height = Mathf.RoundToInt(rect.sizeDelta.y);
        if (width <= 0)
            width = Mathf.RoundToInt(rect.rect.width);
        if (height <= 0)
            height = Mathf.RoundToInt(rect.rect.height);

        x = Mathf.RoundToInt(local.x + width / 2f);
        y = Mathf.RoundToInt(height / 2f - local.y);
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private void SetPressedMainButtonOverlay()
    {
        StrategyHudButtonTheme slot = GetButtonTheme(pressedButtonAction);
        if (slot == null)
        {
            SetImageAtSourceRect(pressedMainButtonImage, null, null);
            return;
        }

        SetImageAtSourceRect(
            pressedMainButtonImage,
            uiContext?.GetTexture(slot.PressedImagePath),
            slot.PressedImageLayout ?? slot.HitArea
        );
    }

    private void RenderMessageNotifications(StrategyHudRenderData data)
    {
        messageNotificationTabs.Clear();

        int index = 0;
        foreach (StrategyHudMessageNotificationTheme notification in GetMessageNotifications())
        {
            if (index >= messageNotificationImages.Length)
                break;

            RawImage image = messageNotificationImages[index];
            Button button = messageNotificationButtons[index];
            messageNotificationTabs.Add(notification.Tab);

            Texture2D texture = GetMessageNotificationTexture(notification, data);
            SetImageAtSourceRect(image, texture, notification.SourceLayout, true);
            button.interactable = texture != null;
            index++;
        }

        for (int i = index; i < messageNotificationImages.Length; i++)
            messageNotificationImages[i].gameObject.SetActive(false);
    }

    private void BindMessageNotificationButtons()
    {
        for (int i = 0; i < messageNotificationButtons.Length; i++)
        {
            int slotIndex = i;
            Button button = messageNotificationButtons[i];
            button.transition = Selectable.Transition.None;
            button.targetGraphic = messageNotificationImages[i];
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => HandleMessageNotificationClicked(slotIndex));
        }
    }

    private Texture2D GetMessageNotificationTexture(
        StrategyHudMessageNotificationTheme notification,
        StrategyHudRenderData data
    )
    {
        if (notification == null)
            return null;

        bool highlighted = data?.UnreadMessageTypes?.Contains(notification.MessageType) == true;
        return uiContext?.GetTexture(
            highlighted ? notification.HighlightedImagePath : notification.DefaultImagePath
        );
    }

    private void HandleMessageNotificationClicked(int index)
    {
        if (index < 0 || index >= messageNotificationTabs.Count)
            return;

        uiContext?.Dispatcher.Send(
            new StrategyUIRequests.OpenMessagesTab(messageNotificationTabs[index])
        );
    }

    private StrategyHudButtonTheme GetButtonTheme(int action)
    {
        if (action == 0)
            return null;

        foreach (StrategyHudButtonTheme button in GetHudButtons())
        {
            if (button?.Action == action)
                return button;
        }

        return null;
    }

    private void SetCounter(TextMeshProUGUI textField, string text, StrategyHudCounterKind kind)
    {
        SourceRectLayout sourceRect = GetCounterLayout(kind);
        if (sourceRect == null)
            return;

        RectTransform textRect = textField.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = new Vector2(sourceRect.X, -sourceRect.Y);
        textRect.sizeDelta = new Vector2(sourceRect.Width, sourceRect.Height);

        textField.text = text ?? string.Empty;
        textField.color = GetHudTextColor();
        textField.textWrappingMode = TextWrappingModes.NoWrap;
        textField.overflowMode = TextOverflowModes.Overflow;
        textField.maskable = true;
        textField.raycastTarget = false;
        textField.alignment = GetCounterAlignment(kind);
        textField.gameObject.SetActive(true);
    }

    private SourceRectLayout GetCounterLayout(StrategyHudCounterKind kind)
    {
        TacticalHUDLayout theme = GetHudTheme();
        return kind switch
        {
            StrategyHudCounterKind.Tick => theme?.TickCounterSourceLayout,
            StrategyHudCounterKind.RawMaterials => theme?.RawMaterialsSourceLayout,
            StrategyHudCounterKind.RefinedMaterials => theme?.RefinedMaterialsSourceLayout,
            StrategyHudCounterKind.Maintenance => theme?.MaintenanceSourceLayout,
            _ => null,
        };
    }

    private Color GetHudTextColor()
    {
        return uiContext?.GetPlayerFactionTheme()?.GetPrimaryColor() ?? Color.white;
    }

    private static TextAlignmentOptions GetCounterAlignment(StrategyHudCounterKind kind)
    {
        return kind == StrategyHudCounterKind.Tick
            ? TextAlignmentOptions.Top
            : TextAlignmentOptions.TopRight;
    }

    private Texture2D GetBackgroundTexture()
    {
        string path = uiContext?.GetPlayerFactionTheme()?.TacticalHUDLayout?.ImagePath;
        return uiContext?.GetTexture(path);
    }

    private Texture2D GetSpeedIndicatorTexture(int sourceSpeed)
    {
        string path = uiContext
            ?.GetPlayerFactionTheme()
            ?.TacticalHUDLayout?.SpeedIndicators?.GetImagePath(sourceSpeed);
        return uiContext?.GetTexture(path);
    }

    private static int GetSourceSpeed(TickSpeed speed)
    {
        return speed switch
        {
            TickSpeed.VerySlow => 1,
            TickSpeed.Slow => 2,
            TickSpeed.Medium => 3,
            TickSpeed.Fast => 4,
            _ => 0,
        };
    }

    private TacticalHUDLayout GetHudTheme()
    {
        return uiContext?.GetPlayerFactionTheme()?.TacticalHUDLayout;
    }

    private IEnumerable<StrategyHudButtonTheme> GetHudButtons()
    {
        IEnumerable<StrategyHudButtonTheme> buttons = GetHudTheme()?.Buttons;
        return buttons ?? System.Array.Empty<StrategyHudButtonTheme>();
    }

    private IEnumerable<StrategyHudMessageNotificationTheme> GetMessageNotifications()
    {
        IEnumerable<StrategyHudMessageNotificationTheme> notifications =
            GetHudTheme()?.MessageNotifications;
        return notifications ?? System.Array.Empty<StrategyHudMessageNotificationTheme>();
    }

    private static void SetImageAtSourceRect(
        RawImage image,
        Texture2D texture,
        SourceRectLayout slot,
        bool raycastTarget = false
    )
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = raycastTarget;

        if (texture == null || slot == null)
            return;

        RectTransform imageRect = image.rectTransform;
        imageRect.anchorMin = new Vector2(0f, 1f);
        imageRect.anchorMax = new Vector2(0f, 1f);
        imageRect.pivot = new Vector2(0f, 1f);
        imageRect.anchoredPosition = new Vector2(slot.X, -slot.Y);
        imageRect.sizeDelta = new Vector2(slot.Width, slot.Height);
    }
}

public sealed class StrategyHudRenderData
{
    public string TickText { get; set; }
    public string RawMaterialsText { get; set; }
    public string RefinedMaterialsText { get; set; }
    public string MaintenanceText { get; set; }
    public TickSpeed Speed { get; set; }
    public HashSet<MessageType> UnreadMessageTypes { get; set; } = new HashSet<MessageType>();
}

public enum StrategyHudCounterKind
{
    Tick,
    RawMaterials,
    RefinedMaterials,
    Maintenance,
}
