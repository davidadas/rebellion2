using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ConfirmDialogWindowView
    : MonoBehaviour,
        IStrategyUIContextReceiver,
        IStrategyWindowContent
{
    private readonly List<TextMeshProUGUI> lineTextFields = new List<TextMeshProUGUI>();
    private readonly List<string> renderedLines = new List<string>();

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage titleImage;

    [SerializeField]
    private RawImage confirmButtonImage;

    [SerializeField]
    private RawImage cancelButtonImage;

    [SerializeField]
    private Button confirmButton;

    [SerializeField]
    private Button cancelButton;

    [SerializeField]
    private ScrollAreaView linesScrollArea;

    [SerializeField]
    private TextMeshProUGUI lineTemplate;

    [SerializeField]
    private Texture2D confirmButtonUpTexture;

    [SerializeField]
    private Texture2D cancelButtonUpTexture;

    private ConfirmDialogWindowRenderData lastData;
    private UIContext uiContext;
    private UIWindow windowShell;
    private ConfirmDialogKind renderedKind;
    private bool renderedAnyLines;

    public UIWindow SourceWindow { get; private set; }
    public ConfirmDialogKind Kind { get; private set; }
    public List<ISceneNode> Items { get; } = new List<ISceneNode>();
    public StrategyMissionTarget MoveTarget { get; private set; }

    public void InitializeWindow(
        UIWindow sourceWindow,
        ConfirmDialogKind kind,
        IEnumerable<ISceneNode> items,
        StrategyMissionTarget moveTarget
    )
    {
        SourceWindow = sourceWindow;
        Kind = kind;
        MoveTarget = moveTarget;
        Items.Clear();
        if (items != null)
            Items.AddRange(items);
    }

    public void Initialize(UIContext uiContext)
    {
        this.uiContext = uiContext;
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null)
            return;

        List<string> lines;
        if (Kind == ConfirmDialogKind.Scrap)
        {
            lines = new List<string> { "Scrap these units?" };
            lines.AddRange(Items.Select(item => item.GetDisplayName()));
        }
        else if (Kind == ConfirmDialogKind.Retire)
        {
            lines = new List<string> { "Retire these personnel?" };
            lines.AddRange(Items.Select(item => item.GetDisplayName()));
        }
        else if (Kind == ConfirmDialogKind.Move)
        {
            lines = new List<string> { "Transit Time in Days" };
            lines.AddRange(Items.Select(item => item.GetDisplayName()));
        }
        else
        {
            return;
        }

        Render(
            new ConfirmDialogWindowRenderData
            {
                X = window.X,
                Y = window.Y,
                Kind = Kind,
                Lines = lines,
            }
        );
    }

    public void Render(ConfirmDialogWindowRenderData data)
    {
        VerifyReferences();
        lastData = data;

        RectTransform rect = transform as RectTransform;
        UILayout.SetSourcePosition(rect, data.X, data.Y);

        SetImageAtTemplateOrigin(backgroundImage, GetBackgroundTexture());
        UILayout.SetInteractiveImageTexture(titleImage, GetTitleTexture(data.Kind));
        UILayout.SetInteractiveImageTexture(confirmButtonImage, confirmButtonUpTexture);
        UILayout.SetInteractiveImageTexture(cancelButtonImage, cancelButtonUpTexture);

        RenderLines(data.Lines, data.Kind);
        gameObject.SetActive(true);
    }

    private void RenderLines(IReadOnlyList<string> lines, ConfirmDialogKind kind)
    {
        IReadOnlyList<string> safeLines = lines ?? System.Array.Empty<string>();
        bool resetScroll = LinesChanged(safeLines, kind);
        RectInt template = UILayout.GetSourceRect(lineTemplate.rectTransform);
        float contentHeight = template.y + safeLines.Count * template.height;
        linesScrollArea.SetContentHeight(contentHeight, template.height, resetScroll);

        for (int i = 0; i < safeLines.Count; i++)
        {
            TextMeshProUGUI textField = GetLineTextField(i);
            UILayout.SetTemplateText(
                textField,
                lineTemplate,
                safeLines[i],
                Color.white,
                new RectInt(
                    template.x,
                    template.y + i * template.height,
                    template.width,
                    template.height
                )
            );
        }

        for (int i = safeLines.Count; i < lineTextFields.Count; i++)
            lineTextFields[i].gameObject.SetActive(false);

        renderedKind = kind;
        renderedAnyLines = true;
        renderedLines.Clear();
        for (int i = 0; i < safeLines.Count; i++)
            renderedLines.Add(safeLines[i]);
    }

    private TextMeshProUGUI GetLineTextField(int index)
    {
        while (lineTextFields.Count <= index)
        {
            TextMeshProUGUI textField = Instantiate(lineTemplate, linesScrollArea.ContentRoot);
            textField.name = $"LineTextField{lineTextFields.Count}";
            lineTextFields.Add(textField);
        }

        return lineTextFields[index];
    }

    private void Awake()
    {
        VerifyReferences();
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Cancel);
    }

    private void Confirm()
    {
        SendChoice(true);
    }

    private void Cancel()
    {
        SendChoice(false);
    }

    private void SendChoice(bool confirmed)
    {
        if (uiContext == null)
            return;

        uiContext.Dispatcher.Send(
            new StrategyUIRequests.ConfirmDialogChoice(GetWindowId(), confirmed)
        );
    }

    private int GetWindowId()
    {
        if (windowShell == null)
            windowShell = GetComponent<UIWindow>();

        return windowShell == null ? 0 : windowShell.Id;
    }

    private bool LinesChanged(IReadOnlyList<string> lines, ConfirmDialogKind kind)
    {
        if (!renderedAnyLines || renderedKind != kind || renderedLines.Count != lines.Count)
            return true;

        for (int i = 0; i < lines.Count; i++)
        {
            if (renderedLines[i] != lines[i])
                return true;
        }

        return false;
    }

    private Texture2D GetBackgroundTexture()
    {
        return uiContext?.GetTexture(GetTheme()?.BackgroundImagePath);
    }

    private Texture2D GetTitleTexture(ConfirmDialogKind kind)
    {
        ConfirmDialogTheme theme = GetTheme();
        string path = kind switch
        {
            ConfirmDialogKind.Scrap => theme?.ScrapTitleImagePath,
            ConfirmDialogKind.Retire => theme?.RetireTitleImagePath,
            _ => theme?.MoveTitleImagePath,
        };
        return uiContext?.GetTexture(path);
    }

    private ConfirmDialogTheme GetTheme()
    {
        return uiContext?.GetPlayerFactionTheme()?.ConfirmDialogTheme;
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (titleImage == null)
            throw new MissingReferenceException($"{name}/TitleImage is missing.");
        if (confirmButtonImage == null)
            throw new MissingReferenceException($"{name}/ConfirmButtonImage is missing.");
        if (cancelButtonImage == null)
            throw new MissingReferenceException($"{name}/CancelButtonImage is missing.");
        if (confirmButton == null)
            throw new MissingReferenceException($"{name}/ConfirmButton is missing.");
        if (cancelButton == null)
            throw new MissingReferenceException($"{name}/CancelButton is missing.");
        if (linesScrollArea == null)
            throw new MissingReferenceException($"{name}/LinesScrollArea is missing.");
        if (lineTemplate == null)
            throw new MissingReferenceException($"{name}/LineTemplate is missing.");
        if (confirmButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/ConfirmButtonUpTexture is missing.");
        if (cancelButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CancelButtonUpTexture is missing.");

        lineTemplate.gameObject.SetActive(false);
    }

    private static void SetImageAtTemplateOrigin(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }
}

public sealed class ConfirmDialogWindowRenderData
{
    public int X;
    public int Y;
    public ConfirmDialogKind Kind;
    public List<string> Lines = new List<string>();
}

public enum ConfirmDialogKind
{
    Move,
    Scrap,
    Retire,
}
