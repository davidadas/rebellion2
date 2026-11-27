using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lists known local working folders alongside packages published by the connected team.
/// </summary>
public sealed class ContentEditorProjectsView : MonoBehaviour
{
    [SerializeField]
    private TMP_Text localHeading;

    [SerializeField]
    private TMP_Text publishedHeading;

    [SerializeField]
    private RectTransform publishedHeader;

    [SerializeField]
    private ScrollAreaLayout localScroll;

    [SerializeField]
    private ScrollAreaLayout publishedScroll;

    [SerializeField]
    private ContentEditorProjectRowView localTemplate;

    [SerializeField]
    private ContentEditorProjectRowView publishedTemplate;

    [SerializeField]
    private TMP_Text localStatus;

    [SerializeField]
    private TMP_Text publishedStatus;

    [SerializeField]
    private Button refresh;

    [SerializeField]
    private Button connect;

    [SerializeField]
    private UICheckboxView showDeprecated;

    private ListView<ContentEditorProjectRowView> localRows;
    private ListView<ContentEditorProjectRowView> publishedRows;
    private ContentEditorLocalProject[] local = Array.Empty<ContentEditorLocalProject>();
    private ThunderstorePackage[] published = Array.Empty<ThunderstorePackage>();
    private ContentPackEditorProject current;
    private ContentEditorLocalProject currentEntry;
    private bool localError;
    private bool editingEnabled = true;
    private bool connected;
    private bool publishedStatusOverride;
    private bool bound;
    internal event Action<string> OpenRequested;
    internal event Action<string> PublishRequested;
    internal event Action<string> PackagePageRequested;
    internal event Action ConnectRequested;
    internal event Action RefreshRequested;

    /// <summary>
    /// Connects authored list and account controls once.
    /// </summary>
    private void Awake() => Bind();

    /// <summary>
    /// Connects list sizing and button actions for runtime and authoring previews.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (
            localScroll == null
            || publishedScroll == null
            || localTemplate == null
            || publishedTemplate == null
            || localStatus == null
            || publishedStatus == null
            || refresh == null
            || connect == null
            || showDeprecated == null
        )
            throw new MissingReferenceException(name + " has incomplete project lists.");
        localRows = new ListView<ContentEditorProjectRowView>(
            localScroll.ScrollArea,
            localTemplate,
            "LocalProject"
        );
        publishedRows = new ListView<ContentEditorProjectRowView>(
            publishedScroll.ScrollArea,
            publishedTemplate,
            "PublishedProject"
        );
        localScroll.WidthChanged += Render;
        publishedScroll.WidthChanged += Render;
        refresh.onClick.AddListener(RequestRefresh);
        connect.onClick.AddListener(RequestConnection);
        showDeprecated.ValueChanged += HandleDeprecatedChanged;
        bound = true;
    }

    /// <summary>
    /// Reflows both project lists to the space remaining at the application content scale.
    /// </summary>
    /// <param name="pageSize">The logical dimensions of the projects page.</param>
    internal void SetViewport(Vector2 pageSize)
    {
        int width = Mathf.FloorToInt(pageSize.x);
        int height = Mathf.Max(1, Mathf.FloorToInt(pageSize.y) - 180);
        int columnWidth = Mathf.Max(1, (width - 32) / 2);
        int right = columnWidth + 32;
        bool compact = columnWidth < 528;
        int statusTop = compact ? 88 : 44;
        int listTop = statusTop + 56;
        UILayout.SetSourceRect((RectTransform)transform, 0, 152, width, height);
        UILayout.SetSourceRect(localHeading.rectTransform, 0, 0, columnWidth, 36);
        UILayout.SetSourceRect(
            publishedHeading.rectTransform,
            right,
            0,
            compact ? columnWidth : 216,
            36
        );
        UILayout.SetSourceRect(publishedHeader, right, compact ? 44 : 0, columnWidth, 36);
        ((RectTransform)showDeprecated.transform).SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            Mathf.Min(192, Mathf.Max(1, columnWidth - 120))
        );
        UILayout.SetSourceRect(localStatus.rectTransform, 0, statusTop, columnWidth, 40);
        UILayout.SetSourceRect(publishedStatus.rectTransform, right, statusTop, columnWidth, 40);
        UILayout.SetSourceRect(
            (RectTransform)localScroll.transform,
            0,
            listTop,
            columnWidth + 16,
            Mathf.Max(1, height - listTop)
        );
        UILayout.SetSourceRect(
            (RectTransform)publishedScroll.transform,
            right,
            listTop,
            columnWidth + 16,
            Mathf.Max(1, height - listTop)
        );
        Render();
    }

    /// <summary>
    /// Keeps the live editor represented without rebuilding it when returning from the overview.
    /// </summary>
    /// <param name="project">The currently open project, when present.</param>
    internal void SetCurrentProject(ContentPackEditorProject project)
    {
        Bind();
        current = project;
        currentEntry =
            project?.IsInstalled == false ? ContentEditorLocalProject.FromProject(project) : null;
        Render();
    }

    /// <summary>
    /// Replaces local metadata without opening project documents or changing their files.
    /// </summary>
    /// <param name="projects">The remembered folders and their availability.</param>
    internal void RenderLocal(ContentEditorLocalProject[] projects)
    {
        Bind();
        local = projects;
        Render();
    }

    /// <summary>
    /// Shows the current team's published packages or the explicit disconnected state.
    /// </summary>
    /// <param name="packages">The connected team's package metadata.</param>
    /// <param name="isConnected">Whether a publishing connection is active.</param>
    internal void RenderPublished(ThunderstorePackage[] packages, bool isConnected)
    {
        Bind();
        connected = isConnected;
        published = isConnected ? packages : Array.Empty<ThunderstorePackage>();
        connect.gameObject.SetActive(!isConnected);
        refresh.gameObject.SetActive(isConnected);
        showDeprecated.gameObject.SetActive(isConnected);
        publishedStatusOverride = false;
        Render();
    }

    /// <summary>
    /// Reports a local-folder registry problem without discarding the current editor.
    /// </summary>
    /// <param name="message">The registry operation result.</param>
    /// <param name="isError">Whether the operation failed.</param>
    internal void ShowLocalStatus(string message, bool isError = false)
    {
        localError = isError;
        SetStatus(localStatus, message, isError);
    }

    /// <summary>
    /// Reports loading, empty, or failed published-package queries beside their list.
    /// </summary>
    /// <param name="message">The query result.</param>
    /// <param name="isError">Whether the query failed.</param>
    internal void ShowPublishedStatus(string message, bool isError = false)
    {
        publishedStatusOverride = true;
        SetStatus(publishedStatus, message, isError);
    }

    /// <summary>
    /// Keeps one published-package refresh active without disabling local projects.
    /// </summary>
    /// <param name="value">Whether a remote refresh is running.</param>
    internal void SetBusy(bool value)
    {
        refresh.interactable = !value;
        if (value)
            ShowPublishedStatus("Loading published packages…");
    }

    /// <summary>
    /// Protects project opening and publishing while installed content files are changing.
    /// </summary>
    /// <param name="enabled">Whether authoring actions may use the installed content.</param>
    internal void SetEditingEnabled(bool enabled)
    {
        if (editingEnabled == enabled)
            return;
        editingEnabled = enabled;
        Render();
    }

    /// <summary>
    /// Combines remembered folders with the open project's live metadata.
    /// </summary>
    /// <returns>Local mods followed by eras, with the working copy first within its type.</returns>
    private ContentEditorLocalProject[] GetLocalProjects()
    {
        IEnumerable<ContentEditorLocalProject> projects =
            currentEntry != null
                ? new[] { currentEntry }.Concat(
                    local.Where(item => item.Folder != currentEntry.Folder)
                )
                : local;
        return projects.OrderBy(item => item.IsEra).ToArray();
    }

    /// <summary>
    /// Reuses both lists at their visible viewport widths.
    /// </summary>
    private void Render()
    {
        if (!bound)
            return;
        ContentEditorLocalProject[] projects = GetLocalProjects();
        if (!localError)
            ShowLocalStatus(
                projects.Length == 0
                    ? "Projects you create or open appear here."
                    : "Working folders on this device."
            );
        int pitch = ContentEditorProjectRowView.Height + ContentEditorProjectRowView.Gap;
        localRows.Render(
            projects,
            Mathf.Max(0, projects.Length * pitch - ContentEditorProjectRowView.Gap),
            38,
            false,
            pitch,
            (row, item, index) =>
            {
                bool active = current?.PackRootPath == item.Folder;
                row.Render(
                    item.Name,
                    item.Error
                        ?? (item.IsEra ? "Era" : ContentEditorPackDialogView.ModProjectLabel)
                            + " · "
                            + item.Version
                            + (active ? " · Open now" : string.Empty),
                    item.Folder,
                    active ? "Edit" : "Open",
                    (item.Error == null || item.NeedsType) && editingEnabled
                        ? () => OpenRequested?.Invoke(item.Folder)
                        : null,
                    "Publish…",
                    item.Error == null && editingEnabled
                        ? () => PublishRequested?.Invoke(item.Folder)
                        : null
                );
                row.SetLayout(
                    index * pitch,
                    Mathf.Max(1, Mathf.FloorToInt(localScroll.ScrollArea.ViewportWidth))
                );
            }
        );
        ThunderstorePackage[] visible = published
            .Where(item => showDeprecated.IsChecked || !item.is_deprecated)
            .ToArray();
        if (!publishedStatusOverride)
            SetStatus(
                publishedStatus,
                !connected ? "Connect Thunderstore to see your team's published packages."
                    : visible.Length > 0 ? "Published by your connected team."
                    : published.Length > 0
                        ? "No current packages. Select Show deprecated to see older listings."
                    : "No published packages for this team.",
                false
            );
        publishedRows.Render(
            visible,
            Mathf.Max(0, visible.Length * pitch - ContentEditorProjectRowView.Gap),
            38,
            false,
            pitch,
            (row, item, index) =>
            {
                ContentEditorLocalProject linked = projects.FirstOrDefault(project =>
                    project.PackageID == item.full_name && project.Error == null
                );
                row.Render(
                    item.name.Replace('_', ' '),
                    item.owner
                        + " · "
                        + item.Latest.version_number
                        + (item.is_deprecated ? " · Deprecated" : string.Empty),
                    linked?.Folder ?? "No local working copy linked",
                    "View page",
                    () => PackagePageRequested?.Invoke(item.package_url),
                    "Edit",
                    linked == null || !editingEnabled
                        ? null
                        : () => OpenRequested?.Invoke(linked.Folder)
                );
                row.SetLayout(
                    index * pitch,
                    Mathf.Max(1, Mathf.FloorToInt(publishedScroll.ScrollArea.ViewportWidth))
                );
            }
        );
    }

    /// <summary>
    /// Applies readable status text and the shared error palette.
    /// </summary>
    /// <param name="label">The owning status label.</param>
    /// <param name="message">The operation result.</param>
    /// <param name="isError">Whether the operation failed.</param>
    private static void SetStatus(TMP_Text label, string message, bool isError)
    {
        label.text = message;
        label
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(isError ? ContentEditorColorRole.Error : ContentEditorColorRole.Muted);
    }

    /// <summary>
    /// Requests a refresh of the current team's published packages.
    /// </summary>
    private void RequestRefresh() => RefreshRequested?.Invoke();

    /// <summary>
    /// Requests the shared connection workflow without checking credentials implicitly.
    /// </summary>
    private void RequestConnection() => ConnectRequested?.Invoke();

    /// <summary>
    /// Refilters cached published packages without making another account or catalogue request.
    /// </summary>
    /// <param name="_">The checkbox's new selection.</param>
    private void HandleDeprecatedChanged(bool _) => Render();

    /// <summary>
    /// Releases the lists' owned subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        localRows.Clear();
        publishedRows.Clear();
        localScroll.WidthChanged -= Render;
        publishedScroll.WidthChanged -= Render;
        refresh.onClick.RemoveListener(RequestRefresh);
        connect.onClick.RemoveListener(RequestConnection);
        showDeprecated.ValueChanged -= HandleDeprecatedChanged;
    }
}
