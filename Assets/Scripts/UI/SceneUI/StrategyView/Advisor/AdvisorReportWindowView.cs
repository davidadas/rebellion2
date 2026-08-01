using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders immutable advisor-report presentations into authored controls and reusable rows.
/// </summary>
public sealed class AdvisorReportWindowView : MonoBehaviour
{
    [Header("Frame")]
    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage galaxyImage;

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [Header("Rows")]
    [SerializeField]
    private ScrollAreaView rowsScrollArea;

    [SerializeField]
    private AdvisorReportRowView overviewRowTemplate;

    [SerializeField]
    private AdvisorReportRowView objectiveRowTemplate;

    [SerializeField]
    private RectTransform rowsPaddingTemplate;

    [Header("Commands")]
    [SerializeField]
    private RawImage infoButtonImage;

    [SerializeField]
    private RawImage closeButtonImage;

    [SerializeField]
    private RawImagePressVisual closeButtonPressVisual;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private Texture2D infoButtonDisabledTexture;

    [SerializeField]
    private Texture2D closeButtonUpTexture;

    [SerializeField]
    private Texture2D closeButtonDownTexture;

    private readonly List<AdvisorReportRowView> overviewRows = new List<AdvisorReportRowView>();
    private readonly List<AdvisorReportRowView> objectiveRows = new List<AdvisorReportRowView>();
    private AdvisorReportMode? renderedMode;
    private int renderedRowCount = -1;

    /// <summary>
    /// Occurs when the authored close control is activated.
    /// </summary>
    public event Action<AdvisorReportWindowView> CloseRequested;

    /// <summary>
    /// Occurs when this view is destroyed so its controller can release the session.
    /// </summary>
    public event Action<AdvisorReportWindowView> Destroyed;

    /// <summary>
    /// Verifies authored references and binds the close command before runtime use.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        closeButton.onClick.AddListener(RequestClose);
    }

    /// <summary>
    /// Removes local listeners and notifies the feature controller of destruction.
    /// </summary>
    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(RequestClose);
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies a complete advisor-report presentation to the authored hierarchy.
    /// </summary>
    /// <param name="data">The advisor-report presentation snapshot.</param>
    public void Render(AdvisorReportWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetImageTexture(backgroundImage, data.BackgroundTexture);
        UILayout.SetImageTexture(galaxyImage, data.GalaxyTexture);
        UILayout.SetTextContent(titleTextField, data.Title);
        UILayout.SetImageTexture(infoButtonImage, infoButtonDisabledTexture);
        closeButtonPressVisual.SetInteractiveTextures(closeButtonUpTexture, closeButtonDownTexture);
        RenderRows(data.Rows, data.Mode);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Emits a semantic request to close this advisor-report window.
    /// </summary>
    internal void RequestClose()
    {
        CloseRequested?.Invoke(this);
    }

    /// <summary>
    /// Renders all projected rows with the template and cache for the active mode.
    /// </summary>
    /// <param name="rows">The projected report rows.</param>
    /// <param name="mode">The report mode selecting the authored row presentation.</param>
    private void RenderRows(IReadOnlyList<AdvisorReportRowRenderData> rows, AdvisorReportMode mode)
    {
        AdvisorReportRowView rowTemplate = GetRowTemplate(mode);
        List<AdvisorReportRowView> rowViews = GetRowViews(mode);
        List<AdvisorReportRowView> hiddenRowViews = GetHiddenRowViews(mode);
        int rowHeight = rowTemplate.Height;
        rowsScrollArea.SetContentHeight(
            rows.Count * rowHeight + UILayout.GetSourceRect(rowsPaddingTemplate).height,
            rowHeight,
            renderedMode != mode || renderedRowCount != rows.Count
        );

        for (int i = 0; i < rows.Count; i++)
        {
            AdvisorReportRowRenderData row = rows[i];
            GetRowView(rowViews, rowTemplate, i).Render(row);
        }

        HideRows(rowViews, rows.Count);
        HideRows(hiddenRowViews, 0);
        renderedMode = mode;
        renderedRowCount = rows.Count;
    }

    /// <summary>
    /// Gets the authored row template for the active report mode.
    /// </summary>
    /// <param name="mode">The report mode.</param>
    /// <returns>The active row template.</returns>
    private AdvisorReportRowView GetRowTemplate(AdvisorReportMode mode)
    {
        return mode switch
        {
            AdvisorReportMode.GalaxyOverview => overviewRowTemplate,
            AdvisorReportMode.Objectives => objectiveRowTemplate,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    /// <summary>
    /// Gets the reusable row cache for the active report mode.
    /// </summary>
    /// <param name="mode">The report mode.</param>
    /// <returns>The active row cache.</returns>
    private List<AdvisorReportRowView> GetRowViews(AdvisorReportMode mode)
    {
        return mode switch
        {
            AdvisorReportMode.GalaxyOverview => overviewRows,
            AdvisorReportMode.Objectives => objectiveRows,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    /// <summary>
    /// Gets the row cache that must be hidden for the active report mode.
    /// </summary>
    /// <param name="mode">The report mode.</param>
    /// <returns>The inactive row cache.</returns>
    private List<AdvisorReportRowView> GetHiddenRowViews(AdvisorReportMode mode)
    {
        return mode switch
        {
            AdvisorReportMode.GalaxyOverview => objectiveRows,
            AdvisorReportMode.Objectives => overviewRows,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    /// <summary>
    /// Gets or creates a reusable row in one mode-specific cache.
    /// </summary>
    /// <param name="rows">The mode-specific row cache.</param>
    /// <param name="template">The authored row template.</param>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable row view.</returns>
    private AdvisorReportRowView GetRowView(
        List<AdvisorReportRowView> rows,
        AdvisorReportRowView template,
        int index
    )
    {
        while (rows.Count <= index)
        {
            AdvisorReportRowView row = Instantiate(template, rowsScrollArea.ContentRoot);
            row.name = $"AdvisorReportRow{rows.Count}";
            rows.Add(row);
        }

        return rows[index];
    }

    /// <summary>
    /// Hides reusable rows at or after a display index.
    /// </summary>
    /// <param name="rows">The row cache to update.</param>
    /// <param name="firstHiddenIndex">The first row to hide.</param>
    private static void HideRows(IReadOnlyList<AdvisorReportRowView> rows, int firstHiddenIndex)
    {
        for (int i = firstHiddenIndex; i < rows.Count; i++)
        {
            if (rows[i] != null)
                rows[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Verifies the complete authored hierarchy and row templates.
    /// </summary>
    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (galaxyImage == null)
            throw new MissingReferenceException($"{name}/GalaxyImage is missing.");
        if (titleTextField == null)
            throw new MissingReferenceException($"{name}/TitleTextField is missing.");
        if (rowsScrollArea == null)
            throw new MissingReferenceException($"{name}/RowsScrollArea is missing.");
        if (overviewRowTemplate == null)
            throw new MissingReferenceException($"{name}/OverviewRowTemplate is missing.");
        if (objectiveRowTemplate == null)
            throw new MissingReferenceException($"{name}/ObjectiveRowTemplate is missing.");
        if (rowsPaddingTemplate == null)
            throw new MissingReferenceException($"{name}/RowsPaddingTemplate is missing.");
        if (infoButtonImage == null)
            throw new MissingReferenceException($"{name}/InfoButtonImage is missing.");
        if (closeButtonImage == null || closeButtonPressVisual == null || closeButton == null)
            throw new MissingReferenceException($"{name}/CloseButton is missing.");
        if (infoButtonDisabledTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonDisabledTexture is missing.");
        if (closeButtonUpTexture == null || closeButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/CloseButton textures are missing.");

        overviewRowTemplate.gameObject.SetActive(false);
        objectiveRowTemplate.gameObject.SetActive(false);
        rowsPaddingTemplate.gameObject.SetActive(false);
    }
}
