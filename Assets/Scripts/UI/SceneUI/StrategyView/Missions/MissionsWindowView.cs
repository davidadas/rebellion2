using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders an authored Missions window and emits semantic Missions-window gestures.
/// </summary>
public sealed class MissionsWindowView : MonoBehaviour, IPointerClickHandler
{
    private readonly List<MissionListRowView> missionRowViews = new List<MissionListRowView>();
    private readonly List<MissionParticipantRowView> participantRowViews =
        new List<MissionParticipantRowView>();
    private readonly List<string> renderedMissionNames = new List<string>();
    private readonly List<string> renderedParticipantNames = new List<string>();

    [Header("Window")]
    [SerializeField]
    private RawImage titleImage;

    [SerializeField]
    private TextMeshProUGUI captionTextField;

    [Header("Missions")]
    [SerializeField]
    private ScrollAreaView missionListScrollArea;

    [SerializeField]
    private MissionListRowView missionListRowTemplate;

    [SerializeField]
    private RectTransform missionListContentPaddingTemplate;

    [Header("Selected Mission")]
    [SerializeField]
    private TextMeshProUGUI targetTitleTextField;

    [SerializeField]
    private RawImage targetImage;

    [SerializeField]
    private TextMeshProUGUI targetNameTextField;

    [Header("Participant Tabs")]
    [SerializeField]
    private RectTransform tabsRoot;

    [SerializeField]
    private RawImage[] tabImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] tabPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] tabButtons = Array.Empty<Button>();

    [Header("Participants")]
    [SerializeField]
    private ScrollAreaView participantsScrollArea;

    [SerializeField]
    private MissionParticipantRowView participantRowTemplate;

    private MissionParticipantRole activeRole;
    private bool hasTargetImageSlotRect;
    private bool renderedAnyMissions;
    private bool renderedAnyParticipants;
    private MissionParticipantRole renderedParticipantRole;
    private int renderedParticipantSelectedMissionIndex = -1;
    private readonly UnityAction[] tabListeners = new UnityAction[
        MissionsWindowRenderData.TabCount
    ];
    private int selectedMissionIndex;
    private RectInt targetImageSlotRect;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    internal event Action<MissionsWindowView> Destroyed;

    /// <summary>
    /// Occurs when the mission is double-clicked.
    /// </summary>
    internal event Action<MissionsWindowView, int, PointerEventData> MissionDoubleClicked;

    /// <summary>
    /// Occurs when a pointer drop is received by the mission.
    /// </summary>
    internal event Action<MissionsWindowView, int, PointerEventData> MissionDropped;

    /// <summary>
    /// Occurs when the mission is pressed.
    /// </summary>
    internal event Action<MissionsWindowView, int, PointerEventData> MissionPressed;

    /// <summary>
    /// Occurs when the mission is released.
    /// </summary>
    internal event Action<MissionsWindowView, int, PointerEventData> MissionReleased;

    /// <summary>
    /// Occurs when the participant is pressed.
    /// </summary>
    internal event Action<MissionsWindowView, int, PointerEventData> ParticipantPressed;

    /// <summary>
    /// Occurs when the surface is clicked.
    /// </summary>
    internal event Action<MissionsWindowView, PointerEventData> SurfaceClicked;

    /// <summary>
    /// Occurs when a tab request is raised.
    /// </summary>
    internal event Action<MissionsWindowView, MissionParticipantRole> TabRequested;

    /// <summary>
    /// Verifies the authored hierarchy and binds local tab controls.
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
        UnbindMissionRows();
        UnbindParticipantRows();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Emits a semantic surface click for mission targeting.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData?.button == PointerEventData.InputButton.Left)
            SurfaceClicked?.Invoke(this, eventData);
    }

    /// <summary>
    /// Applies one complete Missions-window presentation snapshot.
    /// </summary>
    /// <param name="data">The immutable Missions-window snapshot.</param>
    public void Render(MissionsWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        activeRole = data.ActiveRole;
        selectedMissionIndex = data.SelectedMissionIndex;
        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetInteractiveImageTexture(titleImage, data.TitleTexture);
        UILayout.SetTextContent(captionTextField, data.Caption);
        RenderMissionRows(data.Missions);
        RenderSelectedMission(data);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Calculates the mission-list content height from authored row geometry.
    /// </summary>
    /// <param name="missionCount">The number of rendered missions.</param>
    /// <returns>The required source-space content height.</returns>
    internal int GetMissionListScrollContentHeight(int missionCount)
    {
        return UILayout.GetSourceRect(missionListContentPaddingTemplate).height
            + missionCount * GetMissionListRowHeight();
    }

    /// <summary>
    /// Gets the authored mission-list scroll step.
    /// </summary>
    /// <returns>The source-space row height.</returns>
    internal int GetMissionListScrollStep()
    {
        return GetMissionListRowHeight();
    }

    /// <summary>
    /// Calculates participant-list content height from authored row geometry.
    /// </summary>
    /// <param name="participantCount">The number of rendered participants.</param>
    /// <returns>The required source-space content height.</returns>
    internal int GetParticipantScrollContentHeight(int participantCount)
    {
        return UILayout.GetSourceRect(participantRowTemplate.transform as RectTransform).y
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
    /// Gets the active participant row represented by one context-menu pointer event.
    /// </summary>
    /// <param name="eventData">The context-menu pointer event.</param>
    /// <returns>The participant's visual index, or negative one.</returns>
    internal int GetParticipantIndex(PointerEventData eventData)
    {
        GameObject target =
            eventData == null
                ? null
                : eventData.pointerCurrentRaycast.gameObject
                    ?? eventData.pointerPressRaycast.gameObject;
        MissionParticipantRowView row = target?.GetComponentInParent<MissionParticipantRowView>();
        return row && row.gameObject.activeInHierarchy && participantRowViews.Contains(row)
            ? row.Index
            : -1;
    }

    /// <summary>
    /// Binds authored participant-tab buttons in stable visual order.
    /// </summary>
    private void BindControls()
    {
        for (int index = 0; index < tabButtons.Length; index++)
        {
            MissionParticipantRole role = MissionsWindowRenderData.OrderedRoles[index];
            UnityAction listener = () => TabRequested?.Invoke(this, role);
            tabListeners[index] = listener;
            tabButtons[index].onClick.AddListener(listener);
        }
    }

    /// <summary>
    /// Removes only the tab listeners owned by this view.
    /// </summary>
    private void UnbindControls()
    {
        for (int index = 0; index < tabButtons.Length && index < tabListeners.Length; index++)
        {
            if (tabButtons[index] != null && tabListeners[index] != null)
                tabButtons[index].onClick.RemoveListener(tabListeners[index]);
        }
    }

    /// <summary>
    /// Renders the mission list while preserving its scroll offset when rows are unchanged.
    /// </summary>
    /// <param name="rows">The ordered mission-row snapshots.</param>
    private void RenderMissionRows(IReadOnlyList<MissionListRowRenderData> rows)
    {
        bool resetScroll = MissionsChanged(rows);
        missionListScrollArea.SetContentHeight(
            GetMissionListScrollContentHeight(rows.Count),
            GetMissionListScrollStep(),
            resetScroll
        );
        for (int index = 0; index < rows.Count; index++)
        {
            MissionListRowView row = GetMissionRowView(index);
            row.SetIndex(index);
            row.Render(rows[index]);
        }

        for (int index = rows.Count; index < missionRowViews.Count; index++)
            missionRowViews[index].gameObject.SetActive(false);

        renderedAnyMissions = true;
        StoreRenderedNames(renderedMissionNames, rows);
    }

    /// <summary>
    /// Renders or hides the selected-mission detail pane.
    /// </summary>
    /// <param name="data">The complete Missions-window snapshot.</param>
    private void RenderSelectedMission(MissionsWindowRenderData data)
    {
        targetTitleTextField.gameObject.SetActive(data.HasSelectedMission);
        targetImage.gameObject.SetActive(data.HasSelectedMission && data.TargetTexture != null);
        targetNameTextField.gameObject.SetActive(data.HasSelectedMission);
        tabsRoot.gameObject.SetActive(data.HasSelectedMission);
        participantsScrollArea.gameObject.SetActive(data.HasSelectedMission);

        if (!data.HasSelectedMission)
        {
            HideTabs();
            HideParticipants();
            return;
        }

        UILayout.SetCenteredImage(targetImage, data.TargetTexture, targetImageSlotRect);
        UILayout.SetTextContent(targetNameTextField, data.TargetName);
        RenderTabs(data.Tabs);
        RenderParticipants(data.Participants);
    }

    /// <summary>
    /// Applies ordered tab textures to the authored participant tabs.
    /// </summary>
    /// <param name="tabs">The ordered tab snapshots.</param>
    private void RenderTabs(IReadOnlyList<MissionsWindowTabRenderData> tabs)
    {
        if (tabs == null || tabs.Count != tabImages.Length)
            throw new ArgumentException(
                "Missions tab presentation count does not match the prefab."
            );

        for (int index = 0; index < tabImages.Length; index++)
        {
            MissionsWindowTabRenderData tab = tabs[index];
            if (tab.Role != MissionsWindowRenderData.OrderedRoles[index])
                throw new ArgumentException(
                    "Missions tab presentation order does not match the prefab."
                );

            tabImages[index].gameObject.SetActive(true);
            tabPressVisuals[index].SetTextures(tab.Texture, tab.PressedTexture);
        }
    }

    /// <summary>
    /// Hides every participant tab.
    /// </summary>
    private void HideTabs()
    {
        for (int index = 0; index < tabImages.Length; index++)
            tabImages[index].gameObject.SetActive(false);
    }

    /// <summary>
    /// Renders the active participant list while preserving compatible scroll state.
    /// </summary>
    /// <param name="rows">The ordered participant-row snapshots.</param>
    private void RenderParticipants(IReadOnlyList<MissionParticipantRowRenderData> rows)
    {
        bool resetScroll = ParticipantsChanged(rows);
        participantsScrollArea.SetContentHeight(
            GetParticipantScrollContentHeight(rows.Count),
            GetParticipantScrollStep(),
            resetScroll
        );
        for (int index = 0; index < rows.Count; index++)
        {
            MissionParticipantRowView row = GetParticipantRowView(index);
            row.SetPosition(activeRole, index);
            row.Render(rows[index]);
        }

        for (int index = rows.Count; index < participantRowViews.Count; index++)
            participantRowViews[index].gameObject.SetActive(false);

        renderedAnyParticipants = true;
        renderedParticipantRole = activeRole;
        renderedParticipantSelectedMissionIndex = selectedMissionIndex;
        StoreRenderedNames(renderedParticipantNames, rows);
    }

    /// <summary>
    /// Hides every instantiated participant row.
    /// </summary>
    private void HideParticipants()
    {
        for (int index = 0; index < participantRowViews.Count; index++)
            participantRowViews[index].gameObject.SetActive(false);
    }

    /// <summary>
    /// Gets or creates one mission row from the authored template.
    /// </summary>
    /// <param name="index">The requested visual index.</param>
    /// <returns>The reusable mission row.</returns>
    private MissionListRowView GetMissionRowView(int index)
    {
        while (missionRowViews.Count <= index)
        {
            MissionListRowView row = Instantiate(
                missionListRowTemplate,
                missionListScrollArea.ContentRoot
            );
            row.name = $"MissionListRow{missionRowViews.Count}";
            row.DoubleClicked += HandleMissionRowDoubleClicked;
            row.Dropped += HandleMissionRowDropped;
            row.Pressed += HandleMissionRowPressed;
            row.Released += HandleMissionRowReleased;
            missionRowViews.Add(row);
        }

        return missionRowViews[index];
    }

    /// <summary>
    /// Gets or creates one participant row from the authored template.
    /// </summary>
    /// <param name="index">The requested visual index.</param>
    /// <returns>The reusable participant row.</returns>
    private MissionParticipantRowView GetParticipantRowView(int index)
    {
        while (participantRowViews.Count <= index)
        {
            MissionParticipantRowView row = Instantiate(
                participantRowTemplate,
                participantsScrollArea.ContentRoot
            );
            row.name = $"MissionParticipantRow{participantRowViews.Count}";
            row.Pressed += HandleParticipantRowPressed;
            participantRowViews.Add(row);
        }

        return participantRowViews[index];
    }

    /// <summary>
    /// Forwards a mission-row press with its stable visual index.
    /// </summary>
    /// <param name="row">The row that received the pointer.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleMissionRowPressed(MissionListRowView row, PointerEventData eventData)
    {
        if (row != null)
            MissionPressed?.Invoke(this, row.Index, eventData);
    }

    /// <summary>
    /// Forwards a mission-row release with its stable visual index.
    /// </summary>
    /// <param name="row">The row that received the pointer.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleMissionRowReleased(MissionListRowView row, PointerEventData eventData)
    {
        if (row != null)
            MissionReleased?.Invoke(this, row.Index, eventData);
    }

    /// <summary>
    /// Forwards a mission-row drop with its stable visual index.
    /// </summary>
    /// <param name="row">The row that received the drop.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleMissionRowDropped(MissionListRowView row, PointerEventData eventData)
    {
        if (row != null)
            MissionDropped?.Invoke(this, row.Index, eventData);
    }

    /// <summary>
    /// Forwards a mission-row double click with its stable visual index.
    /// </summary>
    /// <param name="row">The row that received the double click.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleMissionRowDoubleClicked(MissionListRowView row, PointerEventData eventData)
    {
        if (row != null)
            MissionDoubleClicked?.Invoke(this, row.Index, eventData);
    }

    /// <summary>
    /// Forwards a participant-row press with its stable visual index.
    /// </summary>
    /// <param name="row">The row that received the pointer.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleParticipantRowPressed(
        MissionParticipantRowView row,
        PointerEventData eventData
    )
    {
        if (row != null)
            ParticipantPressed?.Invoke(this, row.Index, eventData);
    }

    /// <summary>
    /// Removes event subscriptions from every instantiated mission row.
    /// </summary>
    private void UnbindMissionRows()
    {
        foreach (MissionListRowView row in missionRowViews)
        {
            if (row == null)
                continue;

            row.DoubleClicked -= HandleMissionRowDoubleClicked;
            row.Dropped -= HandleMissionRowDropped;
            row.Pressed -= HandleMissionRowPressed;
            row.Released -= HandleMissionRowReleased;
        }
    }

    /// <summary>
    /// Removes event subscriptions from every instantiated participant row.
    /// </summary>
    private void UnbindParticipantRows()
    {
        foreach (MissionParticipantRowView row in participantRowViews)
        {
            if (row != null)
                row.Pressed -= HandleParticipantRowPressed;
        }
    }

    /// <summary>
    /// Reports whether the ordered mission names changed since the previous render.
    /// </summary>
    /// <param name="rows">The current mission-row snapshots.</param>
    /// <returns>True when the scroll position should reset.</returns>
    private bool MissionsChanged(IReadOnlyList<MissionListRowRenderData> rows)
    {
        return !renderedAnyMissions || NamesChanged(renderedMissionNames, rows);
    }

    /// <summary>
    /// Reports whether the participant tab or ordered names changed since the previous render.
    /// </summary>
    /// <param name="rows">The current participant-row snapshots.</param>
    /// <returns>True when the scroll position should reset.</returns>
    private bool ParticipantsChanged(IReadOnlyList<MissionParticipantRowRenderData> rows)
    {
        return !renderedAnyParticipants
            || renderedParticipantRole != activeRole
            || renderedParticipantSelectedMissionIndex != selectedMissionIndex
            || NamesChanged(renderedParticipantNames, rows);
    }

    /// <summary>
    /// Compares stored names against mission rows in stable order.
    /// </summary>
    /// <param name="renderedNames">The names stored after the previous render.</param>
    /// <param name="rows">The current mission rows.</param>
    /// <returns>True when count or content changed.</returns>
    private static bool NamesChanged(
        IReadOnlyList<string> renderedNames,
        IReadOnlyList<MissionListRowRenderData> rows
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
    /// Stores ordered mission names after a successful render.
    /// </summary>
    /// <param name="destination">The reusable name buffer.</param>
    /// <param name="rows">The rendered mission rows.</param>
    private static void StoreRenderedNames(
        ICollection<string> destination,
        IReadOnlyList<MissionListRowRenderData> rows
    )
    {
        destination.Clear();
        for (int index = 0; index < rows.Count; index++)
            destination.Add(rows[index].Name);
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
    /// Gets the authored mission-list row height.
    /// </summary>
    /// <returns>The source-space row height.</returns>
    private int GetMissionListRowHeight()
    {
        return UILayout.GetSourceRect(missionListRowTemplate.transform as RectTransform).height;
    }

    /// <summary>
    /// Gets the authored participant row height.
    /// </summary>
    /// <returns>The source-space row height.</returns>
    private int GetParticipantRowHeight()
    {
        return UILayout.GetSourceRect(participantRowTemplate.transform as RectTransform).height;
    }

    /// <summary>
    /// Ensures every authored Missions-window reference is assigned.
    /// </summary>
    private void VerifyReferences()
    {
        if (titleImage == null)
            throw new MissingReferenceException($"{name}/TitleImage is missing.");
        if (captionTextField == null)
            throw new MissingReferenceException($"{name}/CaptionTextField is missing.");
        if (missionListScrollArea == null)
            throw new MissingReferenceException($"{name}/MissionListScrollArea is missing.");
        if (missionListRowTemplate == null)
            throw new MissingReferenceException($"{name}/MissionListRowTemplate is missing.");
        if (missionListContentPaddingTemplate == null)
            throw new MissingReferenceException(
                $"{name}/MissionListContentPaddingTemplate is missing."
            );
        if (targetTitleTextField == null)
            throw new MissingReferenceException($"{name}/TargetTitleTextField is missing.");
        if (targetImage == null)
            throw new MissingReferenceException($"{name}/TargetImage is missing.");
        if (targetNameTextField == null)
            throw new MissingReferenceException($"{name}/TargetNameTextField is missing.");
        if (tabsRoot == null)
            throw new MissingReferenceException($"{name}/Tabs is missing.");
        if (tabImages == null || tabImages.Length != MissionsWindowRenderData.TabCount)
            throw new MissingReferenceException($"{name}/TabImages are missing.");
        if (tabPressVisuals == null || tabPressVisuals.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/TabPressVisuals are missing.");
        if (tabButtons == null || tabButtons.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/TabButtons are missing.");
        for (int index = 0; index < tabImages.Length; index++)
        {
            if (tabImages[index] == null)
                throw new MissingReferenceException(
                    $"{name}/{MissionsWindowRenderData.OrderedRoles[index]}TabButtonImage is missing."
                );
            if (tabPressVisuals[index] == null)
                throw new MissingReferenceException($"{name}/TabPressVisual{index} is missing.");
            if (tabButtons[index] == null)
                throw new MissingReferenceException($"{name}/TabButton{index} is missing.");
        }
        if (participantsScrollArea == null)
            throw new MissingReferenceException($"{name}/ParticipantsScrollArea is missing.");
        if (participantRowTemplate == null)
            throw new MissingReferenceException($"{name}/ParticipantRowTemplate is missing.");
        missionListRowTemplate.gameObject.SetActive(false);
        missionListContentPaddingTemplate.gameObject.SetActive(false);
        participantRowTemplate.gameObject.SetActive(false);
        if (hasTargetImageSlotRect)
            return;

        targetImageSlotRect = UILayout.GetSourceRect(targetImage.rectTransform);
        hasTargetImageSlotRect = true;
    }
}
