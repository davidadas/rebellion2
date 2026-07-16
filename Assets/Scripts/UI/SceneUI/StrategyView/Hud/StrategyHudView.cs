using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents the authored strategy HUD and translates pointer input into semantic HUD requests.
/// </summary>
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
    private RawImage galacticInformationDisplayImage;

    [SerializeField]
    private RawImage pressedMainButtonImage;

    [SerializeField]
    private UIRaycastArea[] buttonViews = Array.Empty<UIRaycastArea>();

    [SerializeField]
    private UIRaycastArea speedContextView;

    [SerializeField]
    private StrategyAdvisorView advisorView;

    [SerializeField]
    private RawImage[] messageNotificationImages = Array.Empty<RawImage>();

    [SerializeField]
    private Button[] messageNotificationButtons = Array.Empty<Button>();

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    public event Action<StrategyHudAction> ControlPressed;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    public event Action<StrategyHudView> Destroyed;

    /// <summary>
    /// Occurs when a HUD button request is raised.
    /// </summary>
    public event Action<StrategyHudAction, int, int> HudButtonRequested;

    /// <summary>
    /// Occurs when a message tab request is raised.
    /// </summary>
    public event Action<MessagesTab> MessageTabRequested;

    /// <summary>
    /// Occurs when a render request is raised.
    /// </summary>
    public event Action RenderRequested;

    /// <summary>
    /// Occurs when a speed context request is raised.
    /// </summary>
    public event Action<int, int> SpeedContextRequested;

    /// <summary>
    /// Gets the authored advisor view contained by the HUD.
    /// </summary>
    public StrategyAdvisorView AdvisorView => advisorView;

    private readonly List<MessagesTab> messageNotificationTabs = new List<MessagesTab>();
    private readonly List<StrategyHudButtonViewData> renderedButtons =
        new List<StrategyHudButtonViewData>();

    private UnityAction[] messageNotificationClickHandlers = Array.Empty<UnityAction>();
    private StrategyHudAction pressedButtonAction;
    private bool eventsBound;

    /// <summary>
    /// Validates authored references and subscribes child controls when Unity creates the view.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindEvents();
    }

    /// <summary>
    /// Releases child-control subscriptions and informs the feature controller of destruction.
    /// </summary>
    private void OnDestroy()
    {
        UnbindEvents();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies a complete immutable presentation snapshot to the authored HUD controls.
    /// </summary>
    /// <param name="data">The HUD presentation snapshot.</param>
    public void Render(StrategyHudViewData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        SetBackground(data.BackgroundTexture);
        SetCounter(tickTextField, data.TickCounter);
        SetCounter(rawMaterialsTextField, data.RawMaterialsCounter);
        SetCounter(refinedMaterialsTextField, data.RefinedMaterialsCounter);
        SetCounter(maintenanceTextField, data.MaintenanceCounter);
        SetImageAtSourceRect(
            speedIndicatorImage,
            data.SpeedIndicatorTexture,
            data.SpeedIndicatorBounds
        );
        SetImageAtSourceRect(
            galacticInformationDisplayImage,
            data.GalacticInformationDisplayTexture,
            data.GalacticInformationDisplayBounds
        );
        RenderMessageNotifications(data.MessageNotifications);
        RenderButtons(data.Buttons, data.SpeedContextBounds);
        RenderPressedButtonOverlay();
    }

    /// <summary>
    /// Subscribes the authored HUD controls exactly once.
    /// </summary>
    private void BindEvents()
    {
        if (eventsBound)
            return;

        for (int i = 0; i < buttonViews.Length; i++)
        {
            UIRaycastArea button = buttonViews[i];
            button.Pressed += HandleButtonPressed;
            button.Released += HandleButtonReleased;
            button.Clicked += HandleButtonClicked;
        }

        speedContextView.ContextRequested += HandleSpeedContextRequested;
        BindMessageNotificationButtons();
        eventsBound = true;
    }

    /// <summary>
    /// Configures and subscribes each authored message-notification button.
    /// </summary>
    private void BindMessageNotificationButtons()
    {
        messageNotificationClickHandlers = new UnityAction[messageNotificationButtons.Length];
        for (int i = 0; i < messageNotificationButtons.Length; i++)
        {
            int slotIndex = i;
            Button button = messageNotificationButtons[i];
            UnityAction handler = () => HandleMessageNotificationClicked(slotIndex);
            messageNotificationClickHandlers[i] = handler;
            button.transition = Selectable.Transition.None;
            button.targetGraphic = messageNotificationImages[i];
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(handler);
        }
    }

    /// <summary>
    /// Releases all child-control subscriptions owned by the view.
    /// </summary>
    private void UnbindEvents()
    {
        if (!eventsBound)
            return;

        for (int i = 0; i < buttonViews.Length; i++)
        {
            UIRaycastArea button = buttonViews[i];
            button.Pressed -= HandleButtonPressed;
            button.Released -= HandleButtonReleased;
            button.Clicked -= HandleButtonClicked;
        }

        speedContextView.ContextRequested -= HandleSpeedContextRequested;
        for (int i = 0; i < messageNotificationButtons.Length; i++)
        {
            if (i < messageNotificationClickHandlers.Length)
                messageNotificationButtons[i]
                    .onClick.RemoveListener(messageNotificationClickHandlers[i]);
        }

        messageNotificationClickHandlers = Array.Empty<UnityAction>();
        eventsBound = false;
    }

    /// <summary>
    /// Applies the active faction HUD background without changing authored hierarchy or bounds.
    /// </summary>
    /// <param name="texture">The background texture.</param>
    private void SetBackground(Texture texture)
    {
        backgroundImage.texture = texture;
        backgroundImage.enabled = texture != null;
    }

    /// <summary>
    /// Applies dynamic text, color, and faction-specific bounds to one authored counter.
    /// </summary>
    /// <param name="textField">The authored counter label.</param>
    /// <param name="data">The counter presentation data.</param>
    private static void SetCounter(TextMeshProUGUI textField, StrategyHudCounterViewData data)
    {
        if (data == null)
            return;

        textField.text = data.Text;
        textField.color = data.Color;
        textField.gameObject.SetActive(true);
        if (data.Bounds.HasValue)
        {
            RectInt bounds = data.Bounds.Value;
            UILayout.SetSourceRect(
                textField.rectTransform,
                bounds.x,
                bounds.y,
                bounds.width,
                bounds.height
            );
        }
    }

    /// <summary>
    /// Applies themed button bounds and records the semantic action assigned to each authored slot.
    /// </summary>
    /// <param name="buttons">The rendered buttons in authored slot order.</param>
    /// <param name="speedContextBounds">The speed context-menu hit area.</param>
    private void RenderButtons(
        IReadOnlyList<StrategyHudButtonViewData> buttons,
        RectInt? speedContextBounds
    )
    {
        renderedButtons.Clear();
        int renderedCount = Mathf.Min(buttons.Count, buttonViews.Length);
        for (int i = 0; i < renderedCount; i++)
        {
            StrategyHudButtonViewData button = buttons[i];
            renderedButtons.Add(button);
            buttonViews[i].Render(button.HitArea);
        }

        for (int i = renderedCount; i < buttonViews.Length; i++)
            buttonViews[i].gameObject.SetActive(false);

        speedContextView.Render(speedContextBounds);
    }

    /// <summary>
    /// Applies notification artwork and records the messages tab assigned to each authored slot.
    /// </summary>
    /// <param name="notifications">The rendered notification slots.</param>
    private void RenderMessageNotifications(
        IReadOnlyList<StrategyHudMessageNotificationViewData> notifications
    )
    {
        messageNotificationTabs.Clear();
        int renderedCount = Mathf.Min(notifications.Count, messageNotificationImages.Length);
        for (int i = 0; i < renderedCount; i++)
        {
            StrategyHudMessageNotificationViewData notification = notifications[i];
            RawImage image = messageNotificationImages[i];
            messageNotificationTabs.Add(notification.Tab);
            SetImageAtSourceRect(image, notification.Texture, notification.Bounds, true);
            messageNotificationButtons[i].interactable = notification.Texture != null;
        }

        for (int i = renderedCount; i < messageNotificationImages.Length; i++)
            messageNotificationImages[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows the configured pressed artwork for the currently pressed semantic action.
    /// </summary>
    private void RenderPressedButtonOverlay()
    {
        StrategyHudButtonViewData button = GetButtonData(pressedButtonAction);
        if (button == null)
        {
            SetImageAtSourceRect(pressedMainButtonImage, null, null);
            return;
        }

        SetImageAtSourceRect(pressedMainButtonImage, button.PressedTexture, button.PressedBounds);
    }

    /// <summary>
    /// Starts local pressed presentation and emits the semantic control-press cue.
    /// </summary>
    /// <param name="area">The pressed authored hit area.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleButtonPressed(UIRaycastArea area, PointerEventData eventData)
    {
        StrategyHudButtonViewData button = GetButtonData(area);
        if (button == null || button.Action == StrategyHudAction.None)
            return;

        ControlPressed?.Invoke(button.Action);
        pressedButtonAction = button.Action;
        RenderPressedButtonOverlay();
    }

    /// <summary>
    /// Ends local pressed presentation when the matching authored control is released.
    /// </summary>
    /// <param name="area">The released authored hit area.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleButtonReleased(UIRaycastArea area, PointerEventData eventData)
    {
        StrategyHudButtonViewData button = GetButtonData(area);
        if (
            button == null
            || pressedButtonAction == StrategyHudAction.None
            || button.Action != pressedButtonAction
        )
            return;

        pressedButtonAction = StrategyHudAction.None;
        RenderPressedButtonOverlay();
    }

    /// <summary>
    /// Emits a HUD command with source-space pointer coordinates.
    /// </summary>
    /// <param name="area">The clicked authored hit area.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleButtonClicked(UIRaycastArea area, PointerEventData eventData)
    {
        StrategyHudButtonViewData button = GetButtonData(area);
        if (button == null || button.Action == StrategyHudAction.None)
            return;

        if (
            UILayout.TryGetSourcePosition(
                transform as RectTransform,
                eventData,
                out Vector2Int sourcePosition
            )
        )
        {
            HudButtonRequested?.Invoke(button.Action, sourcePosition.x, sourcePosition.y);
            return;
        }

        RenderRequested?.Invoke();
    }

    /// <summary>
    /// Emits a speed-menu request with source-space pointer coordinates.
    /// </summary>
    /// <param name="area">The speed-menu hit area.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleSpeedContextRequested(UIRaycastArea area, PointerEventData eventData)
    {
        if (
            UILayout.TryGetSourcePosition(
                transform as RectTransform,
                eventData,
                out Vector2Int sourcePosition
            )
        )
            SpeedContextRequested?.Invoke(sourcePosition.x, sourcePosition.y);
    }

    /// <summary>
    /// Emits the messages tab assigned to one notification slot.
    /// </summary>
    /// <param name="index">The authored notification slot index.</param>
    private void HandleMessageNotificationClicked(int index)
    {
        if (index < 0 || index >= messageNotificationTabs.Count)
            return;

        MessageTabRequested?.Invoke(messageNotificationTabs[index]);
    }

    /// <summary>
    /// Finds the presentation data assigned to an authored button hit area.
    /// </summary>
    /// <param name="area">The authored hit area.</param>
    /// <returns>The assigned button data, or null when the slot is not rendered.</returns>
    private StrategyHudButtonViewData GetButtonData(UIRaycastArea area)
    {
        if (area == null)
            return null;

        for (int i = 0; i < buttonViews.Length && i < renderedButtons.Count; i++)
        {
            if (buttonViews[i] == area)
                return renderedButtons[i];
        }

        return null;
    }

    /// <summary>
    /// Finds the rendered button assigned to a semantic action.
    /// </summary>
    /// <param name="action">The semantic HUD action.</param>
    /// <returns>The matching button data, or null when no button has the action.</returns>
    private StrategyHudButtonViewData GetButtonData(StrategyHudAction action)
    {
        if (action == StrategyHudAction.None)
            return null;

        for (int i = 0; i < renderedButtons.Count; i++)
        {
            if (renderedButtons[i].Action == action)
                return renderedButtons[i];
        }

        return null;
    }

    /// <summary>
    /// Applies optional image content and source-space bounds while synchronizing visibility.
    /// </summary>
    /// <param name="image">The authored image.</param>
    /// <param name="texture">The texture to display.</param>
    /// <param name="bounds">The optional source-space bounds.</param>
    /// <param name="raycastTarget">Whether the displayed image receives raycasts.</param>
    private static void SetImageAtSourceRect(
        RawImage image,
        Texture texture,
        RectInt? bounds,
        bool raycastTarget = false
    )
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = raycastTarget;

        if (texture == null || bounds == null)
            return;

        RectInt rect = bounds.Value;
        UILayout.SetSourceRect(image.rectTransform, rect.x, rect.y, rect.width, rect.height);
    }

    /// <summary>
    /// Verifies every authored reference required by HUD presentation and input.
    /// </summary>
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
        if (galacticInformationDisplayImage == null)
            throw new MissingReferenceException(
                $"{name}/GalacticInformationDisplayImage is missing."
            );
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
        if (advisorView == null)
            throw new MissingReferenceException($"{name}/AdvisorView is missing.");
    }
}
