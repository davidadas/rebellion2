using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the selected Encyclopedia topic and emits entry-navigation requests.
/// </summary>
public sealed class EncyclopediaDetailPanelView : MonoBehaviour
{
    [SerializeField]
    private RawImage cardImage;

    [SerializeField]
    private RawImage previousButtonImage;

    [SerializeField]
    private Button previousButton;

    [SerializeField]
    private RawImage nextButtonImage;

    [SerializeField]
    private Button nextButton;

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [SerializeField]
    private ScrollAreaView linesScrollArea;

    [SerializeField]
    private TextMeshProUGUI lineTextTemplate;

    [SerializeField]
    private Texture2D previousButtonUpTexture;

    [SerializeField]
    private Texture2D previousButtonDisabledTexture;

    [SerializeField]
    private Texture2D nextButtonUpTexture;

    [SerializeField]
    private Texture2D nextButtonDisabledTexture;

    [SerializeField]
    private int indentationWidth;

    [SerializeField]
    private int columnGap;

    [SerializeField]
    private int contentBottomPadding;

    [SerializeField]
    private int scrollStepOverlap;

    private readonly List<TextMeshProUGUI> lineTextFields = new List<TextMeshProUGUI>();
    private readonly List<string> renderedLines = new List<string>();

    private int renderedSelectedIndex = -1;
    private bool renderedAnyLines;

    /// <summary>
    /// Raised when the next Encyclopedia entry is requested.
    /// </summary>
    public event Action NextRequested;

    /// <summary>
    /// Raised when the previous Encyclopedia entry is requested.
    /// </summary>
    public event Action PreviousRequested;

    /// <summary>
    /// Validates authored references and binds controls.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        previousButton.onClick.AddListener(RequestPrevious);
        nextButton.onClick.AddListener(RequestNext);
    }

    /// <summary>
    /// Detaches the authored navigation listeners owned by this view.
    /// </summary>
    private void OnDestroy()
    {
        if (previousButton != null)
            previousButton.onClick.RemoveListener(RequestPrevious);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(RequestNext);
    }

    /// <summary>
    /// Raises the semantic previous-entry request.
    /// </summary>
    private void RequestPrevious()
    {
        PreviousRequested?.Invoke();
    }

    /// <summary>
    /// Raises the semantic next-entry request.
    /// </summary>
    private void RequestNext()
    {
        NextRequested?.Invoke();
    }

    /// <summary>
    /// Applies an immutable topic-detail presentation snapshot.
    /// </summary>
    /// <param name="data">The topic-detail presentation to render.</param>
    /// <param name="selectedIndex">The selected projected entry index.</param>
    public void Render(EncyclopediaWindowDetailRenderData data, int selectedIndex)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        gameObject.SetActive(true);
        UILayout.SetImageTexture(cardImage, data.Image);
        RenderNavigationButton(
            previousButtonImage,
            previousButton,
            data.PreviousDisabled ? previousButtonDisabledTexture : previousButtonUpTexture,
            data.PreviousDisabled
        );
        RenderNavigationButton(
            nextButtonImage,
            nextButton,
            data.NextDisabled ? nextButtonDisabledTexture : nextButtonUpTexture,
            data.NextDisabled
        );
        UILayout.SetTextContent(titleTextField, data.Title);
        RectInt lineTemplateRect = UILayout.GetSourceRect(lineTextTemplate.rectTransform);
        RenderLines(
            UILayout.WrapText(lineTextTemplate, data.Text, lineTemplateRect.width),
            selectedIndex
        );
    }

    /// <summary>
    /// Hides the complete detail panel while retaining reusable text instances.
    /// </summary>
    public void Hide()
    {
        for (int i = 0; i < lineTextFields.Count; i++)
            lineTextFields[i].gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Applies current artwork and availability to one authored navigation button.
    /// </summary>
    /// <param name="image">The navigation-button image.</param>
    /// <param name="button">The navigation button.</param>
    /// <param name="texture">The texture for the current availability state.</param>
    /// <param name="disabled">Whether navigation is unavailable.</param>
    private static void RenderNavigationButton(
        RawImage image,
        Button button,
        Texture texture,
        bool disabled
    )
    {
        UILayout.SetInteractiveImageTexture(image, texture);
        image.raycastTarget = !disabled;
        button.interactable = !disabled;
    }

    /// <summary>
    /// Renders wrapped detail lines and aligned tabular cells.
    /// </summary>
    /// <param name="lines">The wrapped detail lines.</param>
    /// <param name="selectedIndex">The selected projected entry index.</param>
    private void RenderLines(IReadOnlyList<string> lines, int selectedIndex)
    {
        linesScrollArea.gameObject.SetActive(true);
        IReadOnlyList<string> safeLines = lines ?? Array.Empty<string>();
        bool resetScroll = LinesChanged(safeLines, selectedIndex);
        int linePitch = GetLinePitch();
        linesScrollArea.SetContentHeight(
            GetScrollContentHeight(safeLines.Count, linePitch),
            GetScrollStep(linePitch),
            resetScroll
        );

        RectInt template = UILayout.GetSourceRect(lineTextTemplate.rectTransform);
        List<DetailTextRow> rows = BuildTextRows(safeLines);
        int[] columnPositions = BuildColumnPositions(rows);
        int textIndex = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            DetailTextRow row = rows[i];
            int y = template.y + i * linePitch;
            if (row.IsTabbed)
            {
                int indentX = row.IndentTabs * indentationWidth;
                for (int column = 0; column < row.Cells.Count; column++)
                {
                    string text = row.Cells[column];
                    if (string.IsNullOrEmpty(text))
                        continue;

                    int x =
                        column == 0 ? template.x + indentX : template.x + columnPositions[column];
                    RenderTextCell(textIndex++, text, x, y, template);
                }
            }
            else
            {
                string text = row.Cells.Count == 0 ? string.Empty : row.Cells[0];
                if (!string.IsNullOrEmpty(text))
                {
                    int x = template.x + row.IndentTabs * indentationWidth;
                    RenderTextCell(textIndex++, text, x, y, template);
                }
            }
        }

        for (int i = textIndex; i < lineTextFields.Count; i++)
            lineTextFields[i].gameObject.SetActive(false);

        renderedAnyLines = true;
        renderedSelectedIndex = selectedIndex;
        renderedLines.Clear();
        renderedLines.AddRange(safeLines);
    }

    /// <summary>
    /// Renders one measured detail-text cell from the authored text template.
    /// </summary>
    /// <param name="index">The reusable text-field index.</param>
    /// <param name="value">The displayed cell value.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="template">The authored text bounds.</param>
    private void RenderTextCell(int index, string value, int x, int y, RectInt template)
    {
        TextMeshProUGUI text = GetLineTextField(index);
        int width = Mathf.Max(1, template.x + template.width - x);
        UILayout.SetTemplateText(
            text,
            lineTextTemplate,
            value,
            Color.white,
            new RectInt(x, y, width, template.height)
        );
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>
    /// Parses wrapped lines into indented plain rows or aligned tabular rows.
    /// </summary>
    /// <param name="lines">The wrapped detail lines.</param>
    /// <returns>The parsed detail-row layout data.</returns>
    private static List<DetailTextRow> BuildTextRows(IReadOnlyList<string> lines)
    {
        List<DetailTextRow> rows = new List<DetailTextRow>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            string rawLine = TrimLine(lines[i]);
            if (rawLine.Length == 0)
            {
                rows.Add(new DetailTextRow(0, Array.Empty<string>(), false));
                continue;
            }

            int indentTabs = CountLeadingTabs(rawLine);
            string afterIndent = rawLine.Substring(indentTabs);
            bool tabbed = afterIndent.IndexOf('\t') >= 0;
            rows.Add(
                new DetailTextRow(
                    indentTabs,
                    tabbed ? SplitTabbedCells(afterIndent) : new[] { afterIndent },
                    tabbed
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Measures shared column positions for tabular detail rows.
    /// </summary>
    /// <param name="rows">The parsed detail-row layout data.</param>
    /// <returns>The source-space horizontal offset for each column.</returns>
    private int[] BuildColumnPositions(IReadOnlyList<DetailTextRow> rows)
    {
        int maxColumns = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].IsTabbed && rows[i].Cells.Count > maxColumns)
                maxColumns = rows[i].Cells.Count;
        }

        int[] columnWidths = new int[maxColumns];
        for (int row = 0; row < rows.Count; row++)
        {
            if (!rows[row].IsTabbed)
                continue;

            for (int column = 0; column < rows[row].Cells.Count; column++)
            {
                int width = Mathf.CeilToInt(GetTextWidth(rows[row].Cells[column]));
                if (width > columnWidths[column])
                    columnWidths[column] = width;
            }
        }

        for (int column = 0; column < columnWidths.Length; column++)
            columnWidths[column] += columnGap;

        int[] columnPositions = new int[maxColumns];
        for (int column = 1; column < columnPositions.Length; column++)
        {
            columnPositions[column] = columnPositions[column - 1] + columnWidths[column - 1];
        }

        return columnPositions;
    }

    /// <summary>
    /// Removes surrounding whitespace and terminal null characters from a detail line.
    /// </summary>
    /// <param name="line">The source detail line.</param>
    /// <returns>The normalized detail line.</returns>
    private static string TrimLine(string line)
    {
        return (line ?? string.Empty).Trim('\0').Trim(' ', '\r', '\n', '\v', '\f');
    }

    /// <summary>
    /// Counts indentation tabs at the start of a detail line.
    /// </summary>
    /// <param name="text">The normalized detail line.</param>
    /// <returns>The number of leading tab characters.</returns>
    private static int CountLeadingTabs(string text)
    {
        int count = 0;
        while (count < text.Length && text[count] == '\t')
            count++;

        return count;
    }

    /// <summary>
    /// Splits a tabular detail row while collapsing empty separator cells.
    /// </summary>
    /// <param name="text">The tabular row text.</param>
    /// <returns>The non-empty tabular cells.</returns>
    private static string[] SplitTabbedCells(string text)
    {
        List<string> cells = new List<string>();
        int start = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != '\t')
                continue;

            if (i > start)
                cells.Add(text.Substring(start, i - start));

            while (i + 1 < text.Length && text[i + 1] == '\t')
                i++;
            start = i + 1;
        }

        return cells.ToArray();
    }

    /// <summary>
    /// Returns whether detail content changed enough to reset scroll position.
    /// </summary>
    /// <param name="lines">The current wrapped detail lines.</param>
    /// <param name="selectedIndex">The current selected entry index.</param>
    /// <returns>True when selection or detail text changed.</returns>
    private bool LinesChanged(IReadOnlyList<string> lines, int selectedIndex)
    {
        if (
            !renderedAnyLines
            || renderedSelectedIndex != selectedIndex
            || renderedLines.Count != lines.Count
        )
        {
            return true;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            if (renderedLines[i] != lines[i])
                return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates the source-space content height for detail text.
    /// </summary>
    /// <param name="lineCount">The number of rendered detail lines.</param>
    /// <param name="linePitch">The authored detail-line pitch.</param>
    /// <returns>The required source-space content height.</returns>
    private int GetScrollContentHeight(int lineCount, int linePitch)
    {
        return UILayout.GetSourceRect(lineTextTemplate.rectTransform).y
            + contentBottomPadding
            + lineCount * linePitch;
    }

    /// <summary>
    /// Gets the detail panel's source-space scroll step.
    /// </summary>
    /// <param name="linePitch">The authored detail-line pitch.</param>
    /// <returns>The detail scroll step.</returns>
    private int GetScrollStep(int linePitch)
    {
        return Mathf.Max(1, linePitch - scrollStepOverlap);
    }

    /// <summary>
    /// Gets the authored vertical pitch of a detail line.
    /// </summary>
    /// <returns>The detail line pitch in source-space units.</returns>
    private int GetLinePitch()
    {
        return UILayout.GetSourceRect(lineTextTemplate.rectTransform).height;
    }

    /// <summary>
    /// Measures detail text with the authored text template.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <returns>The preferred rendered width.</returns>
    private float GetTextWidth(string text)
    {
        return lineTextTemplate.GetPreferredValues(text ?? string.Empty).x;
    }

    /// <summary>
    /// Returns or creates a reusable detail text field from the authored template.
    /// </summary>
    /// <param name="index">The reusable text-field index.</param>
    /// <returns>The detail text field at the requested index.</returns>
    private TextMeshProUGUI GetLineTextField(int index)
    {
        while (lineTextFields.Count <= index)
        {
            TextMeshProUGUI text = Instantiate(lineTextTemplate, linesScrollArea.ContentRoot);
            text.name = $"DetailLineTextField{lineTextFields.Count}";
            lineTextFields.Add(text);
        }

        return lineTextFields[index];
    }

    /// <summary>
    /// Verifies every authored child reference and layout metric required by the detail panel.
    /// </summary>
    private void VerifyReferences()
    {
        if (cardImage == null)
            throw new MissingReferenceException($"{name}/CardImage is missing.");
        if (previousButtonImage == null)
            throw new MissingReferenceException($"{name}/PreviousButtonImage is missing.");
        if (previousButton == null)
            throw new MissingReferenceException($"{name}/PreviousButton is missing.");
        if (nextButtonImage == null)
            throw new MissingReferenceException($"{name}/NextButtonImage is missing.");
        if (nextButton == null)
            throw new MissingReferenceException($"{name}/NextButton is missing.");
        if (titleTextField == null)
            throw new MissingReferenceException($"{name}/TitleTextField is missing.");
        if (linesScrollArea == null)
            throw new MissingReferenceException($"{name}/LinesScrollArea is missing.");
        if (lineTextTemplate == null)
            throw new MissingReferenceException($"{name}/LineTextTemplate is missing.");
        if (indentationWidth <= 0)
            throw new MissingReferenceException($"{name}/IndentationWidth is missing.");
        if (columnGap <= 0)
            throw new MissingReferenceException($"{name}/ColumnGap is missing.");
        if (contentBottomPadding < 0)
            throw new MissingReferenceException($"{name}/ContentBottomPadding is invalid.");
        if (scrollStepOverlap < 0)
            throw new MissingReferenceException($"{name}/ScrollStepOverlap is invalid.");

        lineTextTemplate.gameObject.SetActive(false);
    }

    /// <summary>
    /// Contains one parsed detail-text row without exposing mutable cell storage.
    /// </summary>
    private readonly struct DetailTextRow
    {
        /// <summary>
        /// Creates one parsed detail-text row.
        /// </summary>
        /// <param name="indentTabs">The number of leading indentation tabs.</param>
        /// <param name="cells">The visible text cells in display order.</param>
        /// <param name="tabbed">Whether the row uses aligned tabular cells.</param>
        public DetailTextRow(int indentTabs, IReadOnlyList<string> cells, bool tabbed)
        {
            IndentTabs = indentTabs;
            Cells = cells ?? Array.Empty<string>();
            IsTabbed = tabbed;
        }

        public int IndentTabs { get; }

        public IReadOnlyList<string> Cells { get; }

        public bool IsTabbed { get; }
    }
}
