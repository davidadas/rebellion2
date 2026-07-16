using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders an authored Mission Create window and emits semantic mission-creation gestures.
/// </summary>
public sealed class MissionCreateWindowView : MonoBehaviour, IPointerClickHandler
{
    private const int _titleImageCount = 2;

    private readonly List<MissionParticipantRowView> agentRowViews =
        new List<MissionParticipantRowView>();
    private readonly List<MissionParticipantRowView> decoyRowViews =
        new List<MissionParticipantRowView>();
    private readonly List<StrategyDropdownItemView> dropdownItemRows =
        new List<StrategyDropdownItemView>();
    private readonly List<string> renderedAgentNames = new List<string>();
    private readonly List<string> renderedDecoyNames = new List<string>();
    private readonly List<string> renderedDropdownItemNames = new List<string>();

    [Header("Window")]
    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [SerializeField]
    private RawImage[] titleImages = Array.Empty<RawImage>();

    [Header("Tabs")]
    [SerializeField]
    private RawImage[] tabImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] tabPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] tabButtons = Array.Empty<Button>();

    [SerializeField]
    private Texture2D[] tabActiveTextures = Array.Empty<Texture2D>();

    [SerializeField]
    private Texture2D[] tabInactiveTextures = Array.Empty<Texture2D>();

    [Header("Actions")]
    [SerializeField]
    private Button infoButton;

    [SerializeField]
    private Button okButton;

    [SerializeField]
    private Button cancelButton;

    [Header("Mission Selection")]
    [SerializeField]
    private RectTransform missionSelectionRoot;

    [SerializeField]
    private RawImage dropdownButtonImage;

    [SerializeField]
    private RawImagePressVisual dropdownButtonPressVisual;

    [SerializeField]
    private Button dropdownButton;

    [SerializeField]
    private RawImage selectedMissionImage;

    [SerializeField]
    private TextMeshProUGUI selectedMissionNameTextField;

    [SerializeField]
    private RawImage targetPreviewImage;

    [SerializeField]
    private TextMeshProUGUI targetPreviewNameTextField;

    [SerializeField]
    private RectTransform dropdownRoot;

    [SerializeField]
    private ScrollAreaView dropdownScrollArea;

    [SerializeField]
    private StrategyDropdownItemView dropdownItemRowTemplate;

    [SerializeField]
    private RectTransform dropdownContentPaddingTemplate;

    [Header("Personnel")]
    [SerializeField]
    private RectTransform personnelRoot;

    [SerializeField]
    private RawImage agentsHeaderImage;

    [SerializeField]
    private RawImage decoysHeaderImage;

    [SerializeField]
    private Button moveRightButton;

    [SerializeField]
    private Button moveLeftButton;

    [SerializeField]
    private ScrollAreaView agentsScrollArea;

    [SerializeField]
    private MissionParticipantRowView agentRowTemplate;

    [SerializeField]
    private ScrollAreaView decoysScrollArea;

    [SerializeField]
    private MissionParticipantRowView decoyRowTemplate;

    [Header("Authored Assets")]
    [SerializeField]
    private Texture2D missionBackgroundTexture;

    [SerializeField]
    private Texture2D personnelBackgroundTexture;

    [SerializeField]
    private Texture2D titleTexture;

    [SerializeField]
    private Texture2D dropdownButtonUpTexture;

    [SerializeField]
    private Texture2D dropdownButtonDownTexture;

    private UnityAction cancelListener;
    private UnityAction dropdownListener;
    private bool dropdownOpen;
    private bool hasTargetPreviewSlotRect;
    private UnityAction infoListener;
    private UnityAction moveLeftListener;
    private UnityAction moveRightListener;
    private UnityAction okListener;
    private bool renderedAnyAgents;
    private bool renderedAnyDecoys;
    private bool renderedAnyDropdownItems;
    private readonly UnityAction[] tabListeners = new UnityAction[
        MissionCreateWindowRenderData.TabCount
    ];
    private RectInt targetPreviewSlotRect;

    /// <summary>
    /// Occurs when a cancel request is raised.
    /// </summary>
    internal event Action<MissionCreateWindowView> CancelRequested;

    /// <summary>
    /// Occurs when a confirm request is raised.
    /// </summary>
    internal event Action<MissionCreateWindowView> ConfirmRequested;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    internal event Action<MissionCreateWindowView> Destroyed;

    /// <summary>
    /// Occurs when a dropdown dismiss request is raised.
    /// </summary>
    internal event Action<MissionCreateWindowView> DropdownDismissRequested;

    /// <summary>
    /// Occurs when a dropdown item request is raised.
    /// </summary>
    internal event Action<MissionCreateWindowView, int> DropdownItemRequested;

    /// <summary>
    /// Occurs when a dropdown toggle request is raised.
    /// </summary>
    internal event Action<MissionCreateWindowView> DropdownToggleRequested;

    /// <summary>
    /// Occurs when an info request is raised.
    /// </summary>
    internal event Action<MissionCreateWindowView> InfoRequested;

    /// <summary>
    /// Occurs when a move participants request is raised.
    /// </summary>
    internal event Action<
        MissionCreateWindowView,
        MissionParticipantRole
    > MoveParticipantsRequested;

    /// <summary>
    /// Occurs when the participant is clicked.
    /// </summary>
    internal event Action<
        MissionCreateWindowView,
        MissionParticipantRole,
        int,
        PointerEventData
    > ParticipantClicked;

    /// <summary>
    /// Occurs when the participant is pressed.
    /// </summary>
    internal event Action<
        MissionCreateWindowView,
        MissionParticipantRole,
        int,
        PointerEventData
    > ParticipantPressed;

    /// <summary>
    /// Occurs when a tab request is raised.
    /// </summary>
    internal event Action<MissionCreateWindowView, MissionCreateWindowTab> TabRequested;

    private Texture planetTargetPreviewTexture;

    /// <summary>
    /// Verifies the authored hierarchy and binds local controls.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindControls();
    }

    /// <summary>
    /// Releases local subscriptions and notifies the feature controller.
    /// </summary>
    private void OnDestroy()
    {
        UnbindControls();
        UnbindDropdownRows();
        UnbindParticipantRows(agentRowViews);
        UnbindParticipantRows(decoyRowViews);
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Dismisses an open mission dropdown when the pointer lands outside its authored controls.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (
            eventData?.button == PointerEventData.InputButton.Left
            && dropdownOpen
            && !IsDropdownInteraction(eventData)
        )
            DropdownDismissRequested?.Invoke(this);
    }

    /// <summary>
    /// Applies one complete Mission Create presentation snapshot.
    /// </summary>
    /// <param name="data">The immutable Mission Create snapshot.</param>
    public void Render(MissionCreateWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        dropdownOpen = data.DropdownOpen;
        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetInteractiveImageTexture(
            backgroundImage,
            data.ActiveTab == MissionCreateWindowTab.Mission
                ? missionBackgroundTexture
                : personnelBackgroundTexture
        );
        RenderTitle(data.TitleTexture, data.ActiveTab);
        RenderTabs(data.Tabs, data.ActiveTab);

        if (data.ActiveTab == MissionCreateWindowTab.Mission)
        {
            RenderMissionSelectionPane(data);
            HidePersonnelPane();
        }
        else
        {
            HideMissionSelectionPane();
            RenderPersonnelPane(data);
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Gets the authored active or inactive fallback texture for one tab.
    /// </summary>
    /// <param name="tab">The tab index.</param>
    /// <param name="active">Whether the tab is selected.</param>
    /// <returns>The fallback texture, or null.</returns>
    private Texture2D GetFallbackTabTexture(int tab, bool active)
    {
        Texture2D[] textures = active ? tabActiveTextures : tabInactiveTextures;
        return textures != null && tab >= 0 && tab < textures.Length ? textures[tab] : null;
    }

    /// <summary>
    /// Calculates dropdown content height from authored row geometry.
    /// </summary>
    /// <param name="itemCount">The number of visible mission choices.</param>
    /// <returns>The required source-space content height.</returns>
    internal int GetDropdownScrollContentHeight(int itemCount)
    {
        return UILayout.GetSourceRect(dropdownContentPaddingTemplate).height
            + itemCount * GetDropdownItemHeight();
    }

    /// <summary>
    /// Gets the authored dropdown scroll step.
    /// </summary>
    /// <returns>The source-space row height.</returns>
    internal int GetDropdownScrollStep()
    {
        return GetDropdownItemHeight();
    }

    /// <summary>
    /// Calculates participant-list content height from authored row geometry.
    /// </summary>
    /// <param name="participantCount">The number of rendered participants.</param>
    /// <returns>The required source-space content height.</returns>
    internal int GetParticipantScrollContentHeight(int participantCount)
    {
        return UILayout.GetSourceRect(agentRowTemplate.transform as RectTransform).y
            + participantCount * GetParticipantRowHeight();
    }

    /// <summary>
    /// Gets the authored participant-list scroll step.
    /// </summary>
    /// <returns>The source-space row height.</returns>
    internal int GetParticipantScrollStep()
    {
        return GetParticipantRowHeight();
    }

    /// <summary>
    /// Binds authored controls in stable visual order.
    /// </summary>
    private void BindControls()
    {
        for (int index = 0; index < tabButtons.Length; index++)
        {
            MissionCreateWindowTab tab = MissionCreateWindowRenderData.OrderedTabs[index];
            UnityAction listener = () => TabRequested?.Invoke(this, tab);
            tabListeners[index] = listener;
            tabButtons[index].onClick.AddListener(listener);
        }

        dropdownListener = () => DropdownToggleRequested?.Invoke(this);
        moveRightListener = () =>
            MoveParticipantsRequested?.Invoke(this, MissionParticipantRole.Agent);
        moveLeftListener = () =>
            MoveParticipantsRequested?.Invoke(this, MissionParticipantRole.Decoy);
        infoListener = () => InfoRequested?.Invoke(this);
        okListener = () => ConfirmRequested?.Invoke(this);
        cancelListener = () => CancelRequested?.Invoke(this);
        dropdownButton.onClick.AddListener(dropdownListener);
        moveRightButton.onClick.AddListener(moveRightListener);
        moveLeftButton.onClick.AddListener(moveLeftListener);
        infoButton.onClick.AddListener(infoListener);
        okButton.onClick.AddListener(okListener);
        cancelButton.onClick.AddListener(cancelListener);
    }

    /// <summary>
    /// Removes only the control listeners owned by this view.
    /// </summary>
    private void UnbindControls()
    {
        for (int index = 0; index < tabButtons.Length && index < tabListeners.Length; index++)
        {
            if (tabButtons[index] != null && tabListeners[index] != null)
                tabButtons[index].onClick.RemoveListener(tabListeners[index]);
        }

        if (dropdownButton != null && dropdownListener != null)
            dropdownButton.onClick.RemoveListener(dropdownListener);
        if (moveRightButton != null && moveRightListener != null)
            moveRightButton.onClick.RemoveListener(moveRightListener);
        if (moveLeftButton != null && moveLeftListener != null)
            moveLeftButton.onClick.RemoveListener(moveLeftListener);
        if (infoButton != null && infoListener != null)
            infoButton.onClick.RemoveListener(infoListener);
        if (okButton != null && okListener != null)
            okButton.onClick.RemoveListener(okListener);
        if (cancelButton != null && cancelListener != null)
            cancelButton.onClick.RemoveListener(cancelListener);
    }

    /// <summary>
    /// Applies faction title art and the tab-specific title text color.
    /// </summary>
    /// <param name="texture">The resolved title texture.</param>
    /// <param name="activeTab">The active workflow tab.</param>
    private void RenderTitle(Texture texture, MissionCreateWindowTab activeTab)
    {
        Texture resolvedTexture = texture ?? titleTexture;
        foreach (RawImage titleImage in titleImages)
            UILayout.SetInteractiveImageTexture(titleImage, resolvedTexture);

        titleTextField.color =
            activeTab == MissionCreateWindowTab.Mission ? Color.black : Color.white;
    }

    /// <summary>
    /// Applies ordered faction tab textures.
    /// </summary>
    /// <param name="tabs">The ordered tab snapshots.</param>
    /// <param name="activeTab">The selected workflow tab.</param>
    private void RenderTabs(
        IReadOnlyList<MissionCreateTabRenderData> tabs,
        MissionCreateWindowTab activeTab
    )
    {
        if (tabs == null || tabs.Count != tabImages.Length)
            throw new ArgumentException(
                "Mission Create tab presentation count does not match the prefab."
            );

        for (int index = 0; index < tabImages.Length; index++)
        {
            MissionCreateTabRenderData tab = tabs[index];
            if (tab.Tab != MissionCreateWindowRenderData.OrderedTabs[index])
                throw new ArgumentException(
                    "Mission Create tab presentation order does not match the prefab."
                );

            tabImages[index].gameObject.SetActive(true);
            tabPressVisuals[index]
                .SetTextures(
                    tab.Texture ?? GetFallbackTabTexture(index, tab.Tab == activeTab),
                    tab.PressedTexture ?? GetFallbackTabTexture(index, true)
                );
        }
    }

    /// <summary>
    /// Renders the mission-selection workflow pane.
    /// </summary>
    /// <param name="data">The complete Mission Create snapshot.</param>
    private void RenderMissionSelectionPane(MissionCreateWindowRenderData data)
    {
        missionSelectionRoot.gameObject.SetActive(true);
        dropdownButtonPressVisual.SetTextures(
            data.DropdownOpen ? dropdownButtonDownTexture : dropdownButtonUpTexture,
            dropdownButtonDownTexture
        );
        selectedMissionImage.gameObject.SetActive(data.SelectedMissionTexture != null);
        if (data.SelectedMissionTexture != null)
            UILayout.SetImageTexture(selectedMissionImage, data.SelectedMissionTexture);

        selectedMissionNameTextField.gameObject.SetActive(!string.IsNullOrEmpty(data.MissionName));
        if (!string.IsNullOrEmpty(data.MissionName))
            UILayout.SetTextContent(selectedMissionNameTextField, data.MissionName);

        Texture targetTexture =
            data.TargetTexture ?? (data.UsePlanetTargetPreview ? planetTargetPreviewTexture : null);
        targetPreviewImage.gameObject.SetActive(targetTexture != null);
        if (targetTexture != null)
            UILayout.SetCenteredImage(targetPreviewImage, targetTexture, targetPreviewSlotRect);

        targetPreviewNameTextField.gameObject.SetActive(!string.IsNullOrEmpty(data.TargetName));
        if (!string.IsNullOrEmpty(data.TargetName))
            UILayout.SetTextContent(targetPreviewNameTextField, data.TargetName);

        RenderDropdown(data.DropdownOpen, data.DropdownItems);
    }

    /// <summary>
    /// Hides the mission-selection workflow pane and all reusable dropdown rows.
    /// </summary>
    private void HideMissionSelectionPane()
    {
        missionSelectionRoot.gameObject.SetActive(false);
        dropdownRoot.gameObject.SetActive(false);
        HideDropdownItems();
    }

    /// <summary>
    /// Renders or hides the mission dropdown and its reusable rows.
    /// </summary>
    /// <param name="open">Whether the dropdown is visible.</param>
    /// <param name="items">The ordered dropdown-row snapshots.</param>
    private void RenderDropdown(bool open, IReadOnlyList<StrategyDropdownItemRenderData> items)
    {
        dropdownRoot.gameObject.SetActive(open);
        if (!open)
        {
            HideDropdownItems();
            return;
        }

        bool resetScroll = DropdownItemsChanged(items);
        dropdownScrollArea.SetContentHeight(
            GetDropdownScrollContentHeight(items.Count),
            GetDropdownScrollStep(),
            resetScroll
        );
        for (int index = 0; index < items.Count; index++)
            GetDropdownItemRow(index).Render(items[index]);

        for (int index = items.Count; index < dropdownItemRows.Count; index++)
            dropdownItemRows[index].gameObject.SetActive(false);

        renderedAnyDropdownItems = true;
        StoreRenderedNames(renderedDropdownItemNames, items);
    }

    /// <summary>
    /// Hides every instantiated dropdown row.
    /// </summary>
    private void HideDropdownItems()
    {
        foreach (StrategyDropdownItemView row in dropdownItemRows)
            row.gameObject.SetActive(false);
    }

    /// <summary>
    /// Renders the agent and decoy personnel workflow pane.
    /// </summary>
    /// <param name="data">The complete Mission Create snapshot.</param>
    private void RenderPersonnelPane(MissionCreateWindowRenderData data)
    {
        personnelRoot.gameObject.SetActive(true);
        UILayout.SetImageTexture(agentsHeaderImage, data.AgentsHeaderTexture);
        UILayout.SetImageTexture(decoysHeaderImage, data.DecoysHeaderTexture);
        RenderParticipantRows(
            data.AgentRows,
            agentRowViews,
            agentRowTemplate,
            agentsScrollArea,
            MissionParticipantRole.Agent
        );
        RenderParticipantRows(
            data.DecoyRows,
            decoyRowViews,
            decoyRowTemplate,
            decoysScrollArea,
            MissionParticipantRole.Decoy
        );
    }

    /// <summary>
    /// Hides the personnel workflow pane and all reusable participant rows.
    /// </summary>
    private void HidePersonnelPane()
    {
        personnelRoot.gameObject.SetActive(false);
        HideRows(agentRowViews);
        HideRows(decoyRowViews);
    }

    /// <summary>
    /// Renders one participant role list while preserving compatible scroll state.
    /// </summary>
    /// <param name="rows">The ordered participant-row snapshots.</param>
    /// <param name="views">The reusable row instances for this role.</param>
    /// <param name="template">The authored role-specific row template.</param>
    /// <param name="scrollArea">The authored role-specific scroll area.</param>
    /// <param name="role">The semantic participant role.</param>
    private void RenderParticipantRows(
        IReadOnlyList<MissionParticipantRowRenderData> rows,
        List<MissionParticipantRowView> views,
        MissionParticipantRowView template,
        ScrollAreaView scrollArea,
        MissionParticipantRole role
    )
    {
        bool resetScroll = ParticipantRowsChanged(rows, role);
        scrollArea.SetContentHeight(
            GetParticipantScrollContentHeight(rows.Count),
            GetParticipantScrollStep(),
            resetScroll
        );
        for (int index = 0; index < rows.Count; index++)
        {
            MissionParticipantRowView row = GetParticipantRowView(
                index,
                views,
                template,
                scrollArea.ContentRoot,
                role
            );
            row.SetPosition(role, index);
            row.Render(rows[index]);
        }

        for (int index = rows.Count; index < views.Count; index++)
            views[index].gameObject.SetActive(false);

        List<string> renderedNames =
            role == MissionParticipantRole.Agent ? renderedAgentNames : renderedDecoyNames;
        StoreRenderedNames(renderedNames, rows);
        if (role == MissionParticipantRole.Agent)
            renderedAnyAgents = true;
        else
            renderedAnyDecoys = true;
    }

    /// <summary>
    /// Hides every row in one reusable participant-row collection.
    /// </summary>
    /// <param name="rows">The participant rows to hide.</param>
    private static void HideRows(IEnumerable<MissionParticipantRowView> rows)
    {
        foreach (MissionParticipantRowView row in rows)
            row.gameObject.SetActive(false);
    }

    /// <summary>
    /// Gets or creates one mission dropdown row from the authored template.
    /// </summary>
    /// <param name="index">The requested visual index.</param>
    /// <returns>The reusable dropdown row.</returns>
    private StrategyDropdownItemView GetDropdownItemRow(int index)
    {
        while (dropdownItemRows.Count <= index)
        {
            StrategyDropdownItemView row = Instantiate(
                dropdownItemRowTemplate,
                dropdownScrollArea.ContentRoot
            );
            row.name = $"DropdownItemRow{dropdownItemRows.Count}";
            row.SetIndex(dropdownItemRows.Count);
            row.Clicked += HandleDropdownItemClicked;
            dropdownItemRows.Add(row);
        }

        return dropdownItemRows[index];
    }

    /// <summary>
    /// Gets or creates one participant row from the authored role template.
    /// </summary>
    /// <param name="index">The requested visual index.</param>
    /// <param name="views">The reusable role-specific row instances.</param>
    /// <param name="template">The authored role-specific row template.</param>
    /// <param name="parent">The role-specific scroll content root.</param>
    /// <param name="role">The semantic participant role.</param>
    /// <returns>The reusable participant row.</returns>
    private MissionParticipantRowView GetParticipantRowView(
        int index,
        List<MissionParticipantRowView> views,
        MissionParticipantRowView template,
        RectTransform parent,
        MissionParticipantRole role
    )
    {
        while (views.Count <= index)
        {
            MissionParticipantRowView row = Instantiate(template, parent);
            row.name = $"MissionParticipantRow{views.Count}";
            row.SetPosition(role, views.Count);
            row.Released += HandleParticipantRowReleased;
            row.Pressed += HandleParticipantRowPressed;
            views.Add(row);
        }

        return views[index];
    }

    /// <summary>
    /// Forwards one mission-dropdown selection by stable visual index.
    /// </summary>
    /// <param name="row">The selected dropdown row.</param>
    private void HandleDropdownItemClicked(StrategyDropdownItemView row)
    {
        if (row != null)
            DropdownItemRequested?.Invoke(this, row.Index);
    }

    /// <summary>
    /// Forwards one participant click with its semantic role and stable visual index.
    /// </summary>
    /// <param name="row">The clicked participant row.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleParticipantRowReleased(
        MissionParticipantRowView row,
        PointerEventData eventData
    )
    {
        if (row != null)
            ParticipantClicked?.Invoke(this, row.Role, row.Index, eventData);
    }

    /// <summary>
    /// Forwards one participant press with its semantic role and stable visual index.
    /// </summary>
    /// <param name="row">The pressed participant row.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleParticipantRowPressed(
        MissionParticipantRowView row,
        PointerEventData eventData
    )
    {
        if (row != null)
            ParticipantPressed?.Invoke(this, row.Role, row.Index, eventData);
    }

    /// <summary>
    /// Removes event subscriptions from instantiated dropdown rows.
    /// </summary>
    private void UnbindDropdownRows()
    {
        foreach (StrategyDropdownItemView row in dropdownItemRows)
        {
            if (row != null)
                row.Clicked -= HandleDropdownItemClicked;
        }
    }

    /// <summary>
    /// Removes event subscriptions from instantiated participant rows.
    /// </summary>
    /// <param name="rows">The participant rows to unbind.</param>
    private void UnbindParticipantRows(IEnumerable<MissionParticipantRowView> rows)
    {
        foreach (MissionParticipantRowView row in rows)
        {
            if (row == null)
                continue;

            row.Released -= HandleParticipantRowReleased;
            row.Pressed -= HandleParticipantRowPressed;
        }
    }

    /// <summary>
    /// Reports whether the ordered dropdown names changed since the previous render.
    /// </summary>
    /// <param name="items">The current dropdown rows.</param>
    /// <returns>True when the scroll position should reset.</returns>
    private bool DropdownItemsChanged(IReadOnlyList<StrategyDropdownItemRenderData> items)
    {
        return !renderedAnyDropdownItems || NamesChanged(renderedDropdownItemNames, items);
    }

    /// <summary>
    /// Reports whether one participant role's ordered names changed since the previous render.
    /// </summary>
    /// <param name="rows">The current participant rows.</param>
    /// <param name="role">The semantic participant role.</param>
    /// <returns>True when the scroll position should reset.</returns>
    private bool ParticipantRowsChanged(
        IReadOnlyList<MissionParticipantRowRenderData> rows,
        MissionParticipantRole role
    )
    {
        bool renderedAny =
            role == MissionParticipantRole.Agent ? renderedAnyAgents : renderedAnyDecoys;
        IReadOnlyList<string> renderedNames =
            role == MissionParticipantRole.Agent ? renderedAgentNames : renderedDecoyNames;
        return !renderedAny || NamesChanged(renderedNames, rows);
    }

    /// <summary>
    /// Compares stored names against dropdown rows in stable order.
    /// </summary>
    /// <param name="renderedNames">The names stored after the previous render.</param>
    /// <param name="rows">The current dropdown rows.</param>
    /// <returns>True when count or content changed.</returns>
    private static bool NamesChanged(
        IReadOnlyList<string> renderedNames,
        IReadOnlyList<StrategyDropdownItemRenderData> rows
    )
    {
        if (renderedNames.Count != rows.Count)
            return true;

        for (int index = 0; index < rows.Count; index++)
        {
            if (renderedNames[index] != rows[index].Label)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Compares stored names against participant rows in stable order.
    /// </summary>
    /// <param name="renderedNames">The names stored after the previous render.</param>
    /// <param name="rows">The current participant rows.</param>
    /// <returns>True when count or content changed.</returns>
    private static bool NamesChanged(
        IReadOnlyList<string> renderedNames,
        IReadOnlyList<MissionParticipantRowRenderData> rows
    )
    {
        if (renderedNames.Count != rows.Count)
            return true;

        for (int index = 0; index < rows.Count; index++)
        {
            if (renderedNames[index] != rows[index].Name)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Stores ordered dropdown names after a successful render.
    /// </summary>
    /// <param name="destination">The reusable name buffer.</param>
    /// <param name="rows">The rendered dropdown rows.</param>
    private static void StoreRenderedNames(
        ICollection<string> destination,
        IReadOnlyList<StrategyDropdownItemRenderData> rows
    )
    {
        destination.Clear();
        for (int index = 0; index < rows.Count; index++)
            destination.Add(rows[index].Label);
    }

    /// <summary>
    /// Stores ordered participant names after a successful render.
    /// </summary>
    /// <param name="destination">The reusable name buffer.</param>
    /// <param name="rows">The rendered participant rows.</param>
    private static void StoreRenderedNames(
        ICollection<string> destination,
        IReadOnlyList<MissionParticipantRowRenderData> rows
    )
    {
        destination.Clear();
        for (int index = 0; index < rows.Count; index++)
            destination.Add(rows[index].Name);
    }

    /// <summary>
    /// Gets the authored mission-dropdown row height.
    /// </summary>
    /// <returns>The source-space row height.</returns>
    private int GetDropdownItemHeight()
    {
        return dropdownItemRowTemplate.Height;
    }

    /// <summary>
    /// Gets the authored participant row height.
    /// </summary>
    /// <returns>The source-space row height.</returns>
    private int GetParticipantRowHeight()
    {
        return UILayout.GetSourceRect(agentRowTemplate.transform as RectTransform).height;
    }

    /// <summary>
    /// Reports whether a pointer event belongs to the dropdown or its toggle button.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <returns>True when the event belongs to a dropdown control.</returns>
    private bool IsDropdownInteraction(PointerEventData eventData)
    {
        return IsRaycastTargetUnder(eventData.pointerCurrentRaycast.gameObject, dropdownRoot)
            || IsRaycastTargetUnder(eventData.pointerPressRaycast.gameObject, dropdownRoot)
            || IsRaycastTargetUnder(eventData.pointerCurrentRaycast.gameObject, dropdownButtonImage)
            || IsRaycastTargetUnder(eventData.pointerPressRaycast.gameObject, dropdownButtonImage);
    }

    /// <summary>
    /// Reports whether a raycast object belongs to one authored component hierarchy.
    /// </summary>
    /// <param name="target">The raycast object.</param>
    /// <param name="root">The authored hierarchy root.</param>
    /// <returns>True when the target is a descendant of the root.</returns>
    private static bool IsRaycastTargetUnder(GameObject target, Component root)
    {
        return target != null && root != null && target.transform.IsChildOf(root.transform);
    }

    /// <summary>
    /// Ensures every authored Mission Create reference is assigned.
    /// </summary>
    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (titleTextField == null)
            throw new MissingReferenceException($"{name}/TitleTextField is missing.");
        VerifyReferenceArray(titleImages, _titleImageCount, "TitleImages");
        VerifyReferenceArray(tabImages, MissionCreateWindowRenderData.TabCount, "TabImages");
        VerifyReferenceArray(
            tabPressVisuals,
            MissionCreateWindowRenderData.TabCount,
            "TabPressVisuals"
        );
        VerifyReferenceArray(tabButtons, MissionCreateWindowRenderData.TabCount, "TabButtons");
        VerifyReferenceArray(
            tabActiveTextures,
            MissionCreateWindowRenderData.TabCount,
            "TabActiveTextures"
        );
        VerifyReferenceArray(
            tabInactiveTextures,
            MissionCreateWindowRenderData.TabCount,
            "TabInactiveTextures"
        );
        if (infoButton == null)
            throw new MissingReferenceException($"{name}/InfoButton is missing.");
        if (okButton == null)
            throw new MissingReferenceException($"{name}/OkButton is missing.");
        if (cancelButton == null)
            throw new MissingReferenceException($"{name}/CancelButton is missing.");
        if (missionSelectionRoot == null)
            throw new MissingReferenceException($"{name}/MissionSelection is missing.");
        if (dropdownButtonImage == null)
            throw new MissingReferenceException($"{name}/DropdownButtonImage is missing.");
        if (dropdownButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/DropdownButtonPressVisual is missing.");
        if (dropdownButton == null)
            throw new MissingReferenceException($"{name}/DropdownButton is missing.");
        if (selectedMissionImage == null)
            throw new MissingReferenceException($"{name}/SelectedMissionImage is missing.");
        if (selectedMissionNameTextField == null)
            throw new MissingReferenceException($"{name}/SelectedMissionNameTextField is missing.");
        if (targetPreviewImage == null)
            throw new MissingReferenceException($"{name}/TargetPreviewImage is missing.");
        if (targetPreviewNameTextField == null)
            throw new MissingReferenceException($"{name}/TargetPreviewNameTextField is missing.");
        if (dropdownRoot == null)
            throw new MissingReferenceException($"{name}/Dropdown is missing.");
        if (dropdownScrollArea == null)
            throw new MissingReferenceException($"{name}/DropdownScrollArea is missing.");
        if (dropdownItemRowTemplate == null)
            throw new MissingReferenceException($"{name}/DropdownItemRowTemplate is missing.");
        if (dropdownContentPaddingTemplate == null)
            throw new MissingReferenceException(
                $"{name}/DropdownContentPaddingTemplate is missing."
            );
        if (personnelRoot == null)
            throw new MissingReferenceException($"{name}/Personnel is missing.");
        if (agentsHeaderImage == null)
            throw new MissingReferenceException($"{name}/AgentsHeaderImage is missing.");
        if (decoysHeaderImage == null)
            throw new MissingReferenceException($"{name}/DecoysHeaderImage is missing.");
        if (moveRightButton == null)
            throw new MissingReferenceException($"{name}/MoveRightButton is missing.");
        if (moveLeftButton == null)
            throw new MissingReferenceException($"{name}/MoveLeftButton is missing.");
        if (agentsScrollArea == null)
            throw new MissingReferenceException($"{name}/AgentsScrollArea is missing.");
        if (agentRowTemplate == null)
            throw new MissingReferenceException($"{name}/AgentRowTemplate is missing.");
        if (decoysScrollArea == null)
            throw new MissingReferenceException($"{name}/DecoysScrollArea is missing.");
        if (decoyRowTemplate == null)
            throw new MissingReferenceException($"{name}/DecoyRowTemplate is missing.");
        if (missionBackgroundTexture == null)
            throw new MissingReferenceException($"{name}/MissionBackgroundTexture is missing.");
        if (personnelBackgroundTexture == null)
            throw new MissingReferenceException($"{name}/PersonnelBackgroundTexture is missing.");
        if (titleTexture == null)
            throw new MissingReferenceException($"{name}/TitleTexture is missing.");
        if (dropdownButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/DropdownButtonUpTexture is missing.");
        if (dropdownButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/DropdownButtonDownTexture is missing.");
        dropdownItemRowTemplate.gameObject.SetActive(false);
        dropdownContentPaddingTemplate.gameObject.SetActive(false);
        agentRowTemplate.gameObject.SetActive(false);
        decoyRowTemplate.gameObject.SetActive(false);
        planetTargetPreviewTexture ??= targetPreviewImage.texture;
        if (hasTargetPreviewSlotRect)
            return;

        targetPreviewSlotRect = UILayout.GetSourceRect(targetPreviewImage.rectTransform);
        hasTargetPreviewSlotRect = true;
    }

    /// <summary>
    /// Ensures an authored component or texture array has the required assigned entries.
    /// </summary>
    /// <typeparam name="T">The Unity object type.</typeparam>
    /// <param name="items">The serialized array.</param>
    /// <param name="count">The required entry count.</param>
    /// <param name="fieldName">The field name used in failures.</param>
    private void VerifyReferenceArray<T>(T[] items, int count, string fieldName)
        where T : UnityEngine.Object
    {
        if (items == null || items.Length != count)
            throw new MissingReferenceException($"{name}/{fieldName} is missing.");
        for (int index = 0; index < items.Length; index++)
        {
            if (items[index] == null)
                throw new MissingReferenceException($"{name}/{fieldName}[{index}] is missing.");
        }
    }
}
