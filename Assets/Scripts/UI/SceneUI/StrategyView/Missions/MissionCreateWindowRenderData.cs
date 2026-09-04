using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Identifies one tab in the authored Mission Create workflow order.
/// </summary>
public enum MissionCreateWindowTab
{
    Mission = 0,
    Personnel = 1,
}

/// <summary>
/// Contains the rounded mission-planning percentages displayed over a mission icon.
/// </summary>
public sealed class MissionOddsRenderData
{
    public int SuccessPercent { get; }

    public int DetectionPercent { get; }

    public string SuccessLabel => $"SUCCESS\n~{SuccessPercent}%";

    public string DetectionLabel => $"FOILED\n~{DetectionPercent}%";

    /// <summary>
    /// Creates one icon-overlay snapshot from calculated mission probabilities.
    /// </summary>
    /// <param name="successProbability">Estimated chance of final mission success.</param>
    /// <param name="detectionProbability">Estimated chance of pre-objective detection.</param>
    public MissionOddsRenderData(double successProbability, double detectionProbability)
    {
        SuccessPercent = RoundProbability(successProbability);
        DetectionPercent = RoundProbability(detectionProbability);
    }

    private static int RoundProbability(double probability) =>
        (int)Math.Round(Math.Clamp(probability, 0, 100), MidpointRounding.AwayFromZero);
}

/// <summary>
/// Contains immutable presentation data for one mission-creation tab.
/// </summary>
public sealed class MissionCreateTabRenderData
{
    public MissionCreateWindowTab Tab { get; }

    public Texture Texture { get; }

    public Texture PressedTexture { get; }

    /// <summary>
    /// Creates one complete mission-creation tab snapshot.
    /// </summary>
    /// <param name="tab">The represented Mission Create tab.</param>
    /// <param name="texture">The tab texture shown while released.</param>
    /// <param name="pressedTexture">The tab texture shown while pressed.</param>
    public MissionCreateTabRenderData(
        MissionCreateWindowTab tab,
        Texture texture,
        Texture pressedTexture
    )
    {
        Tab = tab;
        Texture = texture;
        PressedTexture = pressedTexture;
    }
}

/// <summary>
/// Contains immutable presentation data for one Mission Create window.
/// </summary>
public sealed class MissionCreateWindowRenderData
{
    private static readonly MissionCreateWindowTab[] _orderedTabs =
    {
        MissionCreateWindowTab.Mission,
        MissionCreateWindowTab.Personnel,
    };
    private static readonly IReadOnlyList<MissionCreateWindowTab> _readOnlyOrderedTabs =
        Array.AsReadOnly(_orderedTabs);

    public static int TabCount => _orderedTabs.Length;

    public static IReadOnlyList<MissionCreateWindowTab> OrderedTabs => _readOnlyOrderedTabs;

    public int X { get; }

    public int Y { get; }

    public MissionCreateWindowTab ActiveTab { get; }

    public bool DropdownOpen { get; }

    public bool CanConfirm { get; }

    public Texture TitleTexture { get; }

    public string MissionName { get; }

    public Texture SelectedMissionTexture { get; }

    public MissionOddsRenderData SelectedMissionOdds { get; }

    public bool ShowMissionOdds { get; }

    public Texture CheckboxFrameTexture { get; }

    public Texture CheckboxCheckMarkTexture { get; }

    public string TargetName { get; }

    public Texture TargetTexture { get; }

    public bool UsePlanetTargetPreview { get; }

    public Texture AgentsHeaderTexture { get; }

    public Texture DecoysHeaderTexture { get; }

    public IReadOnlyList<MissionCreateTabRenderData> Tabs { get; }

    public IReadOnlyList<StrategyDropdownItemRenderData> DropdownItems { get; }

    public IReadOnlyList<MissionParticipantRowRenderData> AgentRows { get; }

    public IReadOnlyList<MissionParticipantRowRenderData> DecoyRows { get; }

    /// <summary>
    /// Creates one complete Mission Create presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="activeTab">The selected workflow tab.</param>
    /// <param name="dropdownOpen">Whether the mission dropdown is visible.</param>
    /// <param name="canConfirm">Whether at least one primary participant is assigned.</param>
    /// <param name="titleTexture">The faction-specific window title texture.</param>
    /// <param name="missionName">The selected mission name.</param>
    /// <param name="selectedMissionTexture">The selected mission icon.</param>
    /// <param name="targetName">The mission target name.</param>
    /// <param name="targetTexture">The mission target image.</param>
    /// <param name="usePlanetTargetPreview">Whether the target is a planet and may use the authored planet-preview fallback.</param>
    /// <param name="agentsHeaderTexture">The faction-specific agents header.</param>
    /// <param name="decoysHeaderTexture">The faction-specific decoys header.</param>
    /// <param name="tabs">The ordered workflow tabs.</param>
    /// <param name="dropdownItems">The ordered mission dropdown rows.</param>
    /// <param name="agentRows">The ordered primary-agent rows.</param>
    /// <param name="decoyRows">The ordered decoy-agent rows.</param>
    /// <param name="selectedMissionOdds">The estimate displayed over the selected mission icon.</param>
    /// <param name="showMissionOdds">Whether the mission-odds overlays are enabled.</param>
    /// <param name="checkboxFrameTexture">The theme-owned checkbox frame.</param>
    /// <param name="checkboxCheckMarkTexture">The theme-owned checkbox check mark.</param>
    public MissionCreateWindowRenderData(
        int x,
        int y,
        MissionCreateWindowTab activeTab,
        bool dropdownOpen,
        bool canConfirm,
        Texture titleTexture,
        string missionName,
        Texture selectedMissionTexture,
        string targetName,
        Texture targetTexture,
        bool usePlanetTargetPreview,
        Texture agentsHeaderTexture,
        Texture decoysHeaderTexture,
        IReadOnlyList<MissionCreateTabRenderData> tabs,
        IReadOnlyList<StrategyDropdownItemRenderData> dropdownItems,
        IReadOnlyList<MissionParticipantRowRenderData> agentRows,
        IReadOnlyList<MissionParticipantRowRenderData> decoyRows,
        MissionOddsRenderData selectedMissionOdds = null,
        bool showMissionOdds = true,
        Texture checkboxFrameTexture = null,
        Texture checkboxCheckMarkTexture = null
    )
    {
        X = x;
        Y = y;
        ActiveTab = activeTab;
        DropdownOpen = dropdownOpen;
        CanConfirm = canConfirm;
        TitleTexture = titleTexture;
        MissionName = missionName ?? string.Empty;
        SelectedMissionTexture = selectedMissionTexture;
        SelectedMissionOdds = selectedMissionOdds;
        ShowMissionOdds = showMissionOdds;
        CheckboxFrameTexture = checkboxFrameTexture;
        CheckboxCheckMarkTexture = checkboxCheckMarkTexture;
        TargetName = targetName ?? string.Empty;
        TargetTexture = targetTexture;
        UsePlanetTargetPreview = usePlanetTargetPreview;
        AgentsHeaderTexture = agentsHeaderTexture;
        DecoysHeaderTexture = decoysHeaderTexture;
        Tabs = Copy(tabs, nameof(tabs));
        DropdownItems = Copy(dropdownItems, nameof(dropdownItems));
        AgentRows = Copy(agentRows, nameof(agentRows));
        DecoyRows = Copy(decoyRows, nameof(decoyRows));
    }

    /// <summary>
    /// Copies a required presentation collection into an isolated read-only snapshot.
    /// </summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="items">The source collection.</param>
    /// <param name="parameterName">The source parameter name.</param>
    /// <returns>The isolated read-only collection.</returns>
    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> items, string parameterName)
    {
        return new List<T>(items ?? throw new ArgumentNullException(parameterName)).AsReadOnly();
    }
}

/// <summary>
/// Renders compact success and detection estimates over the top of a mission icon.
/// </summary>
internal sealed class MissionOddsOverlayView : MonoBehaviour
{
    private static readonly Color32 _backdropColor = new Color32(0, 0, 0, 205);
    private static readonly Color32 _detectionColor = new Color32(255, 105, 85, 255);
    private static readonly Color32 _successColor = new Color32(90, 255, 125, 255);

    private TextMeshProUGUI detectionTextField;
    private TextMeshProUGUI successTextField;

    /// <summary>
    /// Creates an overlay fitted to the supplied mission icon.
    /// </summary>
    /// <param name="iconRoot">The mission icon receiving the overlay.</param>
    /// <param name="textStyleSource">An authored text component supplying the UI font.</param>
    /// <returns>The configured overlay.</returns>
    public static MissionOddsOverlayView Create(
        RectTransform iconRoot,
        TextMeshProUGUI textStyleSource
    )
    {
        if (iconRoot == null)
            throw new ArgumentNullException(nameof(iconRoot));
        if (textStyleSource == null)
            throw new ArgumentNullException(nameof(textStyleSource));

        GameObject overlayObject = new GameObject(
            "MissionOddsOverlay",
            typeof(RectTransform),
            typeof(Image),
            typeof(MissionOddsOverlayView)
        );
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(iconRoot, false);
        overlayRect.anchorMin = new Vector2(0, 1);
        overlayRect.anchorMax = new Vector2(1, 1);
        overlayRect.pivot = new Vector2(0.5f, 1);
        overlayRect.anchoredPosition = Vector2.zero;
        overlayRect.sizeDelta = new Vector2(0, 24);

        Image backdrop = overlayObject.GetComponent<Image>();
        backdrop.color = _backdropColor;
        backdrop.raycastTarget = false;

        MissionOddsOverlayView overlay = overlayObject.GetComponent<MissionOddsOverlayView>();
        overlay.detectionTextField = CreateTextField(
            "DetectionOddsTextField",
            overlayRect,
            textStyleSource,
            new Vector2(0, 0),
            new Vector2(0.5f, 1),
            _detectionColor
        );
        overlay.successTextField = CreateTextField(
            "SuccessOddsTextField",
            overlayRect,
            textStyleSource,
            new Vector2(0.5f, 0),
            new Vector2(1, 1),
            _successColor
        );
        overlayRect.SetAsLastSibling();
        return overlay;
    }

    /// <summary>
    /// Applies one complete mission-odds snapshot.
    /// </summary>
    /// <param name="odds">The mission odds to display.</param>
    public void Render(MissionOddsRenderData odds)
    {
        if (odds == null)
            throw new ArgumentNullException(nameof(odds));

        successTextField.text = odds.SuccessLabel;
        detectionTextField.text = odds.DetectionLabel;
        gameObject.SetActive(true);
    }

    private static TextMeshProUGUI CreateTextField(
        string objectName,
        RectTransform parent,
        TextMeshProUGUI styleSource,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color32 color
    )
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(-2, -2);

        TextMeshProUGUI textField = textObject.GetComponent<TextMeshProUGUI>();
        textField.font = styleSource.font;
        textField.fontSharedMaterial = styleSource.fontSharedMaterial;
        textField.color = color;
        textField.alignment = TextAlignmentOptions.Center;
        textField.fontStyle = FontStyles.Bold;
        textField.enableAutoSizing = true;
        textField.fontSizeMin = 5;
        textField.fontSizeMax = 10;
        textField.textWrappingMode = TextWrappingModes.NoWrap;
        textField.overflowMode = TextOverflowModes.Overflow;
        textField.raycastTarget = false;
        return textField;
    }
}
