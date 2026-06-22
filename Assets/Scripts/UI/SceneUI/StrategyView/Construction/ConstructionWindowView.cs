using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ConstructionWindowView
    : MonoBehaviour,
        IStrategyUIContextReceiver,
        IPointerClickHandler,
        IConstructionWindowControllerReceiver,
        IStrategyWindowContent,
        IGalaxyMapPlanetWindowView
{
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 Gray = new Color32(128, 128, 128, 255);
    private readonly List<RectTransform> dropdownItemRows = new List<RectTransform>();
    private readonly List<RawImage> dropdownItemImages = new List<RawImage>();
    private readonly List<TextMeshProUGUI> dropdownItemTextFields = new List<TextMeshProUGUI>();
    private readonly List<string> renderedDropdownItemNames = new List<string>();

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage titleImage;

    [SerializeField]
    private TextMeshProUGUI captionTextField;

    [SerializeField]
    private RawImage[] buttonImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private int[] buttonActions = System.Array.Empty<int>();

    [SerializeField]
    private RawImage selectedItemImage;

    [SerializeField]
    private TextMeshProUGUI selectedNameTextField;

    [SerializeField]
    private TextMeshProUGUI buildCountLabelTextField;

    [SerializeField]
    private TextMeshProUGUI buildCountTextField;

    [SerializeField]
    private RawImage incrementButtonImage;

    [SerializeField]
    private RawImage decrementButtonImage;

    [SerializeField]
    private TextMeshProUGUI constructionCostTextField;

    [SerializeField]
    private TextMeshProUGUI maintenanceCostTextField;

    [SerializeField]
    private TextMeshProUGUI completionLabelTextField;

    [SerializeField]
    private TextMeshProUGUI completionValueTextField;

    [SerializeField]
    private TextMeshProUGUI deploymentLabelTextField;

    [SerializeField]
    private TextMeshProUGUI deploymentValueTextField;

    [SerializeField]
    private RawImage dropdownButtonImage;

    [SerializeField]
    private RawImage infoButtonImage;

    [SerializeField]
    private RawImage okButtonImage;

    [SerializeField]
    private RawImage cancelButtonImage;

    [SerializeField]
    private Button incrementButton;

    [SerializeField]
    private Button decrementButton;

    [SerializeField]
    private Button dropdownButton;

    [SerializeField]
    private Button infoButton;

    [SerializeField]
    private Button okButton;

    [SerializeField]
    private Button cancelButton;

    [SerializeField]
    private RectTransform dropdownRoot;

    [SerializeField]
    private Image dropdownFrameFillImage;

    [SerializeField]
    private Image dropdownFrameTopImage;

    [SerializeField]
    private Image dropdownFrameBottomImage;

    [SerializeField]
    private Image dropdownFrameLeftImage;

    [SerializeField]
    private Image dropdownFrameRightImage;

    [SerializeField]
    private RawImage[] dropdownBackgroundImages;

    [SerializeField]
    private ScrollAreaView dropdownScrollArea;

    [SerializeField]
    private RawImage dropdownItemImageTemplate;

    [SerializeField]
    private TextMeshProUGUI dropdownItemTextTemplate;

    [SerializeField]
    private RectTransform dropdownItemRowTemplate;

    [SerializeField]
    private RectTransform dropdownItemImageAreaTemplate;

    [SerializeField]
    private RectTransform dropdownItemTextAreaTemplate;

    [SerializeField]
    private Texture2D closeButtonUpTexture;

    [SerializeField]
    private Texture2D incrementButtonUpTexture;

    [SerializeField]
    private Texture2D decrementButtonUpTexture;

    [SerializeField]
    private Texture2D dropdownButtonUpTexture;

    [SerializeField]
    private Texture2D dropdownButtonDownTexture;

    [SerializeField]
    private Texture2D infoButtonUpTexture;

    [SerializeField]
    private Texture2D okButtonUpTexture;

    [SerializeField]
    private Texture2D okButtonDisabledTexture;

    [SerializeField]
    private Texture2D cancelButtonUpTexture;

    private ConstructionWindowRenderData lastData;
    private RectInt selectedItemSlotRect;
    private bool hasSelectedItemSlotRect;
    private UIContext uiContext;
    private ConstructionWindowController constructionWindowController;
    private UIWindow windowShell;
    private int buildPanel = -1;
    private int buildSelection;
    private int buildCount = 1;
    private bool dropdownOpen;
    private bool stateInitialized;
    private bool renderedAnyDropdownItems;
    private bool renderedDropdownOpen;

    public GalaxyMapPlanet GalaxyMapPlanet { get; private set; }
    public UIWindow SourceWindow { get; private set; }
    public string ConstructionDestinationPlanetId { get; set; }
    public string ConstructionDestinationItemId { get; set; }

    public void InitializeWindow(
        GalaxyMapPlanet planet,
        UIWindow sourceWindow,
        int initialBuildPanel,
        string destinationPlanetId,
        string destinationItemId
    )
    {
        GalaxyMapPlanet = planet;
        SourceWindow = sourceWindow;
        ConstructionDestinationPlanetId = destinationPlanetId;
        ConstructionDestinationItemId = destinationItemId;
        InitializeState(initialBuildPanel);
    }

    public void ReconcilePlanet(GalaxyMapPlanet planet)
    {
        InitializeWindow(
            planet,
            SourceWindow,
            GetBuildPanel(),
            ConstructionDestinationPlanetId,
            ConstructionDestinationItemId
        );
    }

    public void InitializeConstruction(ConstructionWindowController constructionWindowController)
    {
        this.constructionWindowController = constructionWindowController;
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null || constructionWindowController == null)
            return;

        int currentBuildPanel = GetBuildPanel();
        int currentBuildCount = GetBuildCount();
        List<IManufacturable> items = constructionWindowController.GetBuildSelection(
            currentBuildPanel,
            context.PlayerFactionId
        );
        HashSet<int> canStartSelections = constructionWindowController.GetCanStartSelections(
            window,
            items,
            currentBuildCount,
            context.PlayerFactionId
        );

        Render(
            new ConstructionWindowRenderData
            {
                X = window.X,
                Y = window.Y,
                BuildPanel = currentBuildPanel,
                OwnerFactionId = GalaxyMapPlanet?.OwnerFactionId,
                Active = active,
                BuildItems = items,
                CanStartSelections = canStartSelections,
            }
        );
    }

    public void Initialize(UIContext uiContext)
    {
        this.uiContext = uiContext;
    }

    internal void InitializeState(int initialBuildPanel)
    {
        if (stateInitialized && buildPanel == initialBuildPanel)
            return;

        buildPanel = initialBuildPanel;
        buildSelection = 0;
        buildCount = 1;
        dropdownOpen = false;
        stateInitialized = true;
    }

    internal int GetBuildPanel()
    {
        return buildPanel;
    }

    internal int GetBuildCount()
    {
        return buildCount;
    }

    public void Render(ConstructionWindowRenderData data)
    {
        VerifyReferences();
        data = CreateRenderData(data);
        lastData = data;
        SyncState(data);

        RectTransform rect = transform as RectTransform;
        UILayout.SetSourcePosition(rect, data.X, data.Y);
        SetImageFromTemplate(backgroundImage);
        UILayout.SetInteractiveImageTexture(
            titleImage,
            GetTitleTexture(data.OwnerFactionId, data.Active)
        );
        SetTemplateText(captionTextField, data.Caption);
        RenderButtons();

        ConstructionDropdownItemRenderData selected = GetSelectedItem(data);
        bool hasSelection = selected != null;
        Texture selectedTexture = hasSelection ? GetItemTexture(selected.Item, false) : null;
        selectedItemImage.gameObject.SetActive(selectedTexture != null);
        if (selectedTexture != null)
            UILayout.SetHorizontallyCenteredImage(
                selectedItemImage,
                selectedTexture,
                selectedItemSlotRect
            );

        selectedNameTextField.gameObject.SetActive(hasSelection);
        if (hasSelection)
            SetTemplateText(selectedNameTextField, selected.Item.GetDisplayName());

        RenderSelectionDetails(selected);
        RenderDropdown(data);
        gameObject.SetActive(true);
    }

    private ConstructionWindowRenderData CreateRenderData(ConstructionWindowRenderData state)
    {
        ConstructionWindowRenderData data = new ConstructionWindowRenderData
        {
            X = state.X,
            Y = state.Y,
            BuildPanel = buildPanel,
            OwnerFactionId = state.OwnerFactionId,
            Active = state.Active,
            Caption = string.IsNullOrEmpty(state.Caption) ? "Build Selection" : state.Caption,
            BuildItems = state.BuildItems,
            CanStartSelections = state.CanStartSelections,
        };

        IReadOnlyList<IManufacturable> items =
            state.BuildItems ?? System.Array.Empty<IManufacturable>();
        for (int i = 0; i < items.Count; i++)
        {
            data.DropdownItems.Add(
                new ConstructionDropdownItemRenderData
                {
                    Item = items[i],
                    CanStart = ContainsSelection(state.CanStartSelections, i),
                }
            );
        }

        return data;
    }

    internal int GetDropdownScrollContentHeight(int itemCount)
    {
        return itemCount * GetDropdownItemHeight();
    }

    public ISceneNode GetSelectedBuildItem()
    {
        return GetSelectedItem(lastData)?.Item;
    }

    internal bool IsDropdownOpen => dropdownOpen;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !dropdownOpen)
            return;

        RequestFocus();
        dropdownOpen = false;
        RequestRender();
    }

    private void Awake()
    {
        VerifyReferences();
        BindControls();
    }

    private void SyncState(ConstructionWindowRenderData data)
    {
        if (data == null)
            return;

        int itemCount = data.DropdownItems?.Count ?? 0;
        if (itemCount == 0)
        {
            buildSelection = 0;
            dropdownOpen = false;
            return;
        }

        buildSelection = Mathf.Clamp(buildSelection, 0, itemCount - 1);
        buildCount = Mathf.Clamp(buildCount, 1, 255);
    }

    private ConstructionDropdownItemRenderData GetSelectedItem(ConstructionWindowRenderData data)
    {
        if (data?.DropdownItems == null || data.DropdownItems.Count == 0)
            return null;

        int index = Mathf.Clamp(buildSelection, 0, data.DropdownItems.Count - 1);
        return data.DropdownItems[index];
    }

    private static bool ContainsSelection(IReadOnlyCollection<int> selection, int index)
    {
        if (selection == null)
            return false;

        foreach (int selectionIndex in selection)
        {
            if (selectionIndex == index)
                return true;
        }

        return false;
    }

    private void BindControls()
    {
        incrementButton.onClick.AddListener(IncrementBuildCount);
        decrementButton.onClick.AddListener(DecrementBuildCount);
        dropdownButton.onClick.AddListener(ToggleDropdown);
        infoButton.onClick.AddListener(OpenInfo);
        okButton.onClick.AddListener(StartConstruction);
        cancelButton.onClick.AddListener(Cancel);
    }

    private void ToggleDropdown()
    {
        RequestFocus();
        dropdownOpen = !dropdownOpen;
        RequestRender();
    }

    private void OpenInfo()
    {
        if (uiContext == null)
            return;

        RequestFocus();
        uiContext.Dispatcher.Send(new StrategyUIRequests.OpenConstructionInfo(GetWindowId()));
    }

    private void StartConstruction()
    {
        if (uiContext == null)
            return;

        RequestFocus();
        ConstructionDropdownItemRenderData selected = GetSelectedItem(lastData);
        if (selected == null || !selected.CanStart)
            return;

        uiContext.Dispatcher.Send(
            new StrategyUIRequests.StartConstruction(
                GetWindowId(),
                buildPanel,
                buildSelection,
                buildCount
            )
        );
    }

    private void Cancel()
    {
        if (uiContext == null)
            return;

        RequestFocus();
        uiContext.Dispatcher.Send(new StrategyUIRequests.CloseWindow(GetWindowId()));
    }

    private void IncrementBuildCount()
    {
        RequestFocus();
        buildCount = Mathf.Clamp(buildCount + 1, 1, 255);
        RequestRender();
    }

    private void DecrementBuildCount()
    {
        RequestFocus();
        buildCount = Mathf.Clamp(buildCount - 1, 1, 255);
        RequestRender();
    }

    private void SelectDropdownItem(int index)
    {
        if (lastData?.DropdownItems == null || index < 0 || index >= lastData.DropdownItems.Count)
            return;

        RequestFocus();
        buildSelection = index;
        dropdownOpen = false;
        RequestRender();
    }

    private int GetWindowId()
    {
        if (windowShell == null)
            windowShell = GetComponent<UIWindow>();

        return windowShell == null ? 0 : windowShell.Id;
    }

    private void RequestFocus()
    {
        if (windowShell == null)
            windowShell = GetComponent<UIWindow>();

        windowShell?.RequestFocus();
    }

    private void RequestRender()
    {
        uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void RenderSelectionDetails(ConstructionDropdownItemRenderData selected)
    {
        bool hasSelection = selected != null;
        buildCountLabelTextField.gameObject.SetActive(hasSelection);
        buildCountTextField.gameObject.SetActive(hasSelection);
        constructionCostTextField.gameObject.SetActive(hasSelection);
        maintenanceCostTextField.gameObject.SetActive(hasSelection);
        completionLabelTextField.gameObject.SetActive(hasSelection);
        completionValueTextField.gameObject.SetActive(hasSelection);
        deploymentLabelTextField.gameObject.SetActive(hasSelection);
        deploymentValueTextField.gameObject.SetActive(hasSelection);
        incrementButtonImage.gameObject.SetActive(hasSelection);
        decrementButtonImage.gameObject.SetActive(hasSelection);
        dropdownButtonImage.gameObject.SetActive(hasSelection);
        infoButtonImage.gameObject.SetActive(hasSelection);
        okButtonImage.gameObject.SetActive(hasSelection);
        cancelButtonImage.gameObject.SetActive(hasSelection);
        incrementButton.gameObject.SetActive(hasSelection);
        decrementButton.gameObject.SetActive(hasSelection);
        dropdownButton.gameObject.SetActive(hasSelection);
        infoButton.gameObject.SetActive(hasSelection);
        okButton.gameObject.SetActive(hasSelection);
        cancelButton.gameObject.SetActive(hasSelection);

        if (!hasSelection)
            return;

        SetTemplateText(buildCountLabelTextField, "Number to build");
        SetTemplateText(buildCountTextField, buildCount.ToString());
        UILayout.SetInteractiveImageTexture(incrementButtonImage, incrementButtonUpTexture);
        UILayout.SetInteractiveImageTexture(decrementButtonImage, decrementButtonUpTexture);
        SetTemplateText(constructionCostTextField, selected.Item.GetConstructionCost().ToString());
        SetTemplateText(maintenanceCostTextField, selected.Item.GetMaintenanceCost().ToString());
        SetTemplateText(completionLabelTextField, "Best Time to Completion");
        SetTemplateText(completionValueTextField, "N/A");
        SetTemplateText(deploymentLabelTextField, "Best Time to Deployment");
        SetTemplateText(deploymentValueTextField, "N/A");
        UILayout.SetInteractiveImageTexture(
            dropdownButtonImage,
            dropdownOpen ? dropdownButtonDownTexture : dropdownButtonUpTexture
        );
        UILayout.SetInteractiveImageTexture(infoButtonImage, infoButtonUpTexture);
        UILayout.SetInteractiveImageTexture(
            okButtonImage,
            selected.CanStart ? okButtonUpTexture : okButtonDisabledTexture
        );
        UILayout.SetInteractiveImageTexture(cancelButtonImage, cancelButtonUpTexture);
        okButton.interactable = selected.CanStart;
    }

    private void RenderButtons()
    {
        for (int i = 0; i < buttonImages.Length; i++)
        {
            RawImage image = buttonImages[i];
            if (image == null)
                continue;

            int action = GetButtonAction(i);
            Texture texture =
                action == StrategyWindowButtonActions.CloseWindow
                    ? closeButtonUpTexture
                    : image.texture;
            ConfigureWindowButton(image, texture);
        }
    }

    private static void ConfigureWindowButton(RawImage image, Texture upTexture)
    {
        UILayout.SetInteractiveImageTexture(image, upTexture);
    }

    private void RenderDropdown(ConstructionWindowRenderData data)
    {
        dropdownRoot.gameObject.SetActive(dropdownOpen);
        if (!dropdownOpen)
        {
            HideDropdownItems();
            return;
        }

        for (int i = 0; i < dropdownBackgroundImages.Length; i++)
            SetImageFromTemplate(dropdownBackgroundImages[i]);

        dropdownScrollArea.gameObject.SetActive(true);
        IReadOnlyList<ConstructionDropdownItemRenderData> items =
            data.DropdownItems != null
                ? data.DropdownItems
                : System.Array.Empty<ConstructionDropdownItemRenderData>();
        bool resetScroll = DropdownItemsChanged(items) || !renderedDropdownOpen;
        dropdownScrollArea.SetContentHeight(
            GetDropdownScrollContentHeight(items.Count),
            GetDropdownItemHeight(),
            resetScroll
        );

        for (int i = 0; i < items.Count; i++)
        {
            ConstructionDropdownItemRenderData item = items[i];
            RectTransform rowRoot = GetDropdownItemRow(i);
            rowRoot.gameObject.SetActive(true);

            RectInt imageArea = UILayout.GetSourceRect(dropdownItemImageAreaTemplate);
            RectInt textArea = UILayout.GetSourceRect(dropdownItemTextAreaTemplate);
            RawImage itemImage = dropdownItemImages[i];
            TextMeshProUGUI textField = dropdownItemTextFields[i];
            UILayout.SetHorizontallyCenteredImage(
                itemImage,
                GetItemTexture(item.Item, true),
                new RectInt(imageArea.x, imageArea.y, imageArea.width, imageArea.height)
            );
            UILayout.SetTemplateText(
                textField,
                dropdownItemTextTemplate,
                item.Item.GetDisplayName(),
                i == buildSelection ? White : Gray,
                textArea
            );
        }

        for (int i = items.Count; i < dropdownItemRows.Count; i++)
            dropdownItemRows[i].gameObject.SetActive(false);

        renderedDropdownOpen = true;
        renderedAnyDropdownItems = true;
        renderedDropdownItemNames.Clear();
        for (int i = 0; i < items.Count; i++)
            renderedDropdownItemNames.Add(items[i].Item?.GetDisplayName() ?? string.Empty);
    }

    private void HideDropdownItems()
    {
        for (int i = 0; i < dropdownItemRows.Count; i++)
            dropdownItemRows[i].gameObject.SetActive(false);

        renderedDropdownOpen = false;
    }

    private int GetButtonAction(int index)
    {
        return index >= 0 && index < buttonActions.Length ? buttonActions[index] : 0;
    }

    private RectTransform GetDropdownItemRow(int index)
    {
        while (dropdownItemRows.Count <= index)
        {
            RectTransform row = Instantiate(
                dropdownItemRowTemplate,
                dropdownScrollArea.ContentRoot
            );
            row.name = $"DropdownItemRow{dropdownItemRows.Count}";
            row.gameObject.SetActive(true);
            Button button = row.GetComponent<Button>();
            if (button == null)
                throw new MissingReferenceException($"{row.name}/Button is missing.");

            int itemIndex = dropdownItemRows.Count;
            button.onClick.AddListener(() => SelectDropdownItem(itemIndex));
            RawImage image = Instantiate(dropdownItemImageTemplate, row);
            image.name = $"DropdownItemImage{dropdownItemImages.Count}";
            TextMeshProUGUI textField = Instantiate(dropdownItemTextTemplate, row);
            textField.name = $"DropdownItemTextField{dropdownItemTextFields.Count}";
            dropdownItemRows.Add(row);
            dropdownItemImages.Add(image);
            dropdownItemTextFields.Add(textField);
        }

        return dropdownItemRows[index];
    }

    private Texture2D GetTitleTexture(string ownerFactionId, bool active)
    {
        WindowTitleTheme theme = uiContext?.GetTheme(ownerFactionId)?.WindowTitleTheme;
        return uiContext?.GetTexture(active ? theme?.ActiveImagePath : theme?.InactiveImagePath);
    }

    private Texture GetItemTexture(IManufacturable item, bool small)
    {
        if (item is not ISceneNode node || uiContext == null)
            return null;

        return uiContext.GetEntityTexture(node, small);
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (titleImage == null)
            throw new MissingReferenceException($"{name}/TitleImage is missing.");
        if (captionTextField == null)
            throw new MissingReferenceException($"{name}/CaptionTextField is missing.");
        if (buttonImages == null || buttonImages.Length == 0)
            throw new MissingReferenceException($"{name}/Button images are missing.");
        if (buttonActions == null || buttonActions.Length != buttonImages.Length)
            throw new MissingReferenceException($"{name}/Button actions are missing.");
        for (int i = 0; i < buttonImages.Length; i++)
        {
            if (buttonImages[i] == null)
                throw new MissingReferenceException($"{name}/ButtonImage{i} is missing.");
        }
        if (selectedItemImage == null)
            throw new MissingReferenceException($"{name}/SelectedItemImage is missing.");
        if (selectedNameTextField == null)
            throw new MissingReferenceException($"{name}/SelectedNameTextField is missing.");
        if (buildCountLabelTextField == null)
            throw new MissingReferenceException($"{name}/BuildCountLabelTextField is missing.");
        if (buildCountTextField == null)
            throw new MissingReferenceException($"{name}/BuildCountTextField is missing.");
        if (incrementButtonImage == null)
            throw new MissingReferenceException($"{name}/IncrementButtonImage is missing.");
        if (decrementButtonImage == null)
            throw new MissingReferenceException($"{name}/DecrementButtonImage is missing.");
        if (constructionCostTextField == null)
            throw new MissingReferenceException($"{name}/ConstructionCostTextField is missing.");
        if (maintenanceCostTextField == null)
            throw new MissingReferenceException($"{name}/MaintenanceCostTextField is missing.");
        if (completionLabelTextField == null)
            throw new MissingReferenceException($"{name}/CompletionLabelTextField is missing.");
        if (completionValueTextField == null)
            throw new MissingReferenceException($"{name}/CompletionValueTextField is missing.");
        if (deploymentLabelTextField == null)
            throw new MissingReferenceException($"{name}/DeploymentLabelTextField is missing.");
        if (deploymentValueTextField == null)
            throw new MissingReferenceException($"{name}/DeploymentValueTextField is missing.");
        if (dropdownButtonImage == null)
            throw new MissingReferenceException($"{name}/DropdownButtonImage is missing.");
        if (infoButtonImage == null)
            throw new MissingReferenceException($"{name}/InfoButtonImage is missing.");
        if (okButtonImage == null)
            throw new MissingReferenceException($"{name}/OkButtonImage is missing.");
        if (cancelButtonImage == null)
            throw new MissingReferenceException($"{name}/CancelButtonImage is missing.");
        if (incrementButton == null)
            throw new MissingReferenceException($"{name}/IncrementButton is missing.");
        if (decrementButton == null)
            throw new MissingReferenceException($"{name}/DecrementButton is missing.");
        if (dropdownButton == null)
            throw new MissingReferenceException($"{name}/DropdownButton is missing.");
        if (infoButton == null)
            throw new MissingReferenceException($"{name}/InfoButton is missing.");
        if (okButton == null)
            throw new MissingReferenceException($"{name}/OkButton is missing.");
        if (cancelButton == null)
            throw new MissingReferenceException($"{name}/CancelButton is missing.");
        if (dropdownRoot == null)
            throw new MissingReferenceException($"{name}/Dropdown is missing.");
        if (dropdownFrameFillImage == null)
            throw new MissingReferenceException($"{name}/DropdownFrameFillImage is missing.");
        if (dropdownFrameTopImage == null)
            throw new MissingReferenceException($"{name}/DropdownFrameTopImage is missing.");
        if (dropdownFrameBottomImage == null)
            throw new MissingReferenceException($"{name}/DropdownFrameBottomImage is missing.");
        if (dropdownFrameLeftImage == null)
            throw new MissingReferenceException($"{name}/DropdownFrameLeftImage is missing.");
        if (dropdownFrameRightImage == null)
            throw new MissingReferenceException($"{name}/DropdownFrameRightImage is missing.");
        if (dropdownBackgroundImages == null || dropdownBackgroundImages.Length != 3)
            throw new MissingReferenceException($"{name}/DropdownBackgroundImages is missing.");
        if (dropdownScrollArea == null)
            throw new MissingReferenceException($"{name}/DropdownScrollArea is missing.");
        if (dropdownItemImageTemplate == null)
            throw new MissingReferenceException($"{name}/DropdownItemImageTemplate is missing.");
        if (dropdownItemTextTemplate == null)
            throw new MissingReferenceException($"{name}/DropdownItemTextTemplate is missing.");
        if (dropdownItemRowTemplate == null)
            throw new MissingReferenceException($"{name}/DropdownItemRowTemplate is missing.");
        if (dropdownItemRowTemplate.GetComponent<Button>() == null)
            throw new MissingReferenceException(
                $"{name}/DropdownItemRowTemplate/Button is missing."
            );
        if (dropdownItemImageAreaTemplate == null)
            throw new MissingReferenceException(
                $"{name}/DropdownItemImageAreaTemplate is missing."
            );
        if (dropdownItemTextAreaTemplate == null)
            throw new MissingReferenceException($"{name}/DropdownItemTextAreaTemplate is missing.");
        if (closeButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonUpTexture is missing.");
        if (incrementButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/IncrementButtonUpTexture is missing.");
        if (decrementButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/DecrementButtonUpTexture is missing.");
        if (dropdownButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/DropdownButtonUpTexture is missing.");
        if (dropdownButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/DropdownButtonDownTexture is missing.");
        if (infoButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonUpTexture is missing.");
        if (okButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/OkButtonUpTexture is missing.");
        if (okButtonDisabledTexture == null)
            throw new MissingReferenceException($"{name}/OkButtonDisabledTexture is missing.");
        if (cancelButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CancelButtonUpTexture is missing.");

        dropdownItemImageTemplate.gameObject.SetActive(false);
        dropdownItemTextTemplate.gameObject.SetActive(false);
        dropdownItemRowTemplate.gameObject.SetActive(false);
        dropdownItemImageAreaTemplate.gameObject.SetActive(false);
        dropdownItemTextAreaTemplate.gameObject.SetActive(false);
        InitializeTemplateRects();
    }

    private void InitializeTemplateRects()
    {
        if (hasSelectedItemSlotRect)
            return;

        selectedItemSlotRect = UILayout.GetSourceRect(selectedItemImage.rectTransform);
        hasSelectedItemSlotRect = true;
    }

    private int GetDropdownItemHeight()
    {
        return UILayout.GetSourceRect(dropdownItemRowTemplate).height;
    }

    private bool DropdownItemsChanged(IReadOnlyList<ConstructionDropdownItemRenderData> items)
    {
        if (!renderedAnyDropdownItems || renderedDropdownItemNames.Count != items.Count)
            return true;

        for (int i = 0; i < items.Count; i++)
        {
            if (renderedDropdownItemNames[i] != (items[i].Item?.GetDisplayName() ?? string.Empty))
                return true;
        }

        return false;
    }

    private static void SetImageFromTemplate(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    private static void SetImageFromTemplate(RawImage image)
    {
        SetImageFromTemplate(image, image.texture);
    }

    private static void SetImage(RawImage image, Texture texture, int x, int y)
    {
        int width = texture == null ? 0 : texture.width;
        int height = texture == null ? 0 : texture.height;
        SetImage(image, texture, x, y, width, height);
    }

    private static void SetImage(
        RawImage image,
        Texture texture,
        int x,
        int y,
        int width,
        int height
    )
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
        if (texture != null)
            SetSourceRect(image.rectTransform, x, y, width, height);
    }

    private static void SetTemplateText(TextMeshProUGUI textField, string text)
    {
        UILayout.SetTextContent(textField, text, textField.color);
    }

    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }
}

public sealed class ConstructionWindowRenderData
{
    public int X;
    public int Y;
    public int BuildPanel;
    public string OwnerFactionId;
    public bool Active;
    public string Caption;
    public IReadOnlyList<IManufacturable> BuildItems;
    public IReadOnlyCollection<int> CanStartSelections;
    public List<ConstructionDropdownItemRenderData> DropdownItems =
        new List<ConstructionDropdownItemRenderData>();
}

public sealed class ConstructionDropdownItemRenderData
{
    public IManufacturable Item;
    public bool CanStart;
}
