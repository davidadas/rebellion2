using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StatusWindowView
    : MonoBehaviour,
        IStrategyUIContextReceiver,
        IStrategyWindowContent
{
    private readonly List<RawImage> statusImageViews = new List<RawImage>();
    private readonly List<TextMeshProUGUI> labelTextFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> leftRowTextFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> rightRowTextFields = new List<TextMeshProUGUI>();
    private readonly List<StatusWindowRowRenderData> renderedRows =
        new List<StatusWindowRowRenderData>();

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private TextMeshProUGUI headerTextField;

    [SerializeField]
    private RectTransform imagesRoot;

    [SerializeField]
    private RawImage statusImageTemplate;

    [SerializeField]
    private RectTransform statusImageAreaTemplate;

    [SerializeField]
    private TextMeshProUGUI labelTextTemplate;

    [SerializeField]
    private ScrollAreaView rowsScrollArea;

    [SerializeField]
    private TextMeshProUGUI leftRowTextTemplate;

    [SerializeField]
    private TextMeshProUGUI rightRowTextTemplate;

    [SerializeField]
    private RawImage infoButtonImage;

    [SerializeField]
    private RawImage closeButtonImage;

    [SerializeField]
    private Button infoButton;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private Texture2D infoButtonUpTexture;

    [SerializeField]
    private Texture2D infoButtonDisabledTexture;

    [SerializeField]
    private Texture2D closeButtonUpTexture;

    private StatusWindowRenderData lastData;
    private UIContext uiContext;
    private UIWindow windowShell;
    private bool renderedAnyRows;

    public UIWindow SourceWindow { get; private set; }
    public StrategyStatusTarget StatusTarget { get; private set; }
    public bool InfoDisabled { get; set; }

    public void InitializeWindow(
        UIWindow sourceWindow,
        StrategyStatusTarget statusTarget,
        bool infoDisabled
    )
    {
        SourceWindow = sourceWindow;
        StatusTarget = statusTarget;
        InfoDisabled = infoDisabled;
    }

    public void Initialize(UIContext uiContext)
    {
        this.uiContext = uiContext;
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null)
            return;

        StatusWindowRenderData data = new StrategyStatusInfoBuilder(context).CreateRenderData(
            window
        );
        if (data != null)
            Render(data);
    }

    public void Render(StatusWindowRenderData data)
    {
        VerifyReferences();
        data = CreateRenderData(data);
        lastData = data;

        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        SetImageFromTemplate(backgroundImage, GetBackgroundTexture(data.OwnerFactionId));
        RenderHeader(data);
        RenderImages(data.ImageTextures, data.CenterImage);
        RenderLabel(data.LabelLines);
        RenderRows(data.Rows);
        UILayout.SetInteractiveImageTexture(
            infoButtonImage,
            GetInfoButtonTexture(data.InfoDisabled)
        );
        infoButton.interactable = !data.InfoDisabled;
        UILayout.SetInteractiveImageTexture(closeButtonImage, GetCloseButtonTexture());
        gameObject.SetActive(true);
    }

    internal int GetRenderedRowCount(IReadOnlyList<StrategyStatusRow> rows)
    {
        return BuildStatusTextLines(rows, GetRowWrapWidth(), GetRowWrapFontSize()).Count;
    }

    internal int GetLabelWrapWidth()
    {
        return UILayout.GetSourceRect(labelTextTemplate.rectTransform).width;
    }

    internal int GetLabelWrapFontSize()
    {
        return Mathf.RoundToInt(labelTextTemplate.fontSize);
    }

    internal int GetRowWrapWidth()
    {
        return Mathf.RoundToInt(rowsScrollArea.ViewportWidth);
    }

    internal int GetRowWrapFontSize()
    {
        return Mathf.RoundToInt(leftRowTextTemplate.fontSize);
    }

    internal int GetRowScrollContentHeight(int rowCount)
    {
        return rowCount * GetRowHeight();
    }

    private StatusWindowRenderData CreateRenderData(StatusWindowRenderData state)
    {
        StatusWindowRenderData data = new StatusWindowRenderData
        {
            X = state.X,
            Y = state.Y,
            OwnerFactionId = state.OwnerFactionId,
            CenterImage = state.CenterImage,
            InfoDisabled = state.InfoDisabled,
            Header = state.Header,
            Label = state.Label,
            SourceRows = state.SourceRows,
            Images = state.Images,
            SourceStatusImageItems = state.SourceStatusImageItems,
            SourceImageItems = state.SourceImageItems,
            SourceOverlayImageItems = state.SourceOverlayImageItems,
        };

        if (state.Images != null)
        {
            foreach (StatusWindowImage image in state.Images)
                data.ImageTextures.Add(GetImageTexture(image, state.OwnerFactionId));
        }

        foreach (ISceneNode item in state.SourceStatusImageItems ?? Array.Empty<ISceneNode>())
            data.ImageTextures.Add(GetStatusImageTexture(item));

        if (state.SourceImageItems != null)
        {
            foreach (ISceneNode item in state.SourceImageItems)
                data.ImageTextures.Add(GetImageTexture(item));
        }

        foreach (ISceneNode item in state.SourceOverlayImageItems ?? Array.Empty<ISceneNode>())
            data.ImageTextures.Add(GetOverlayImageTexture(item));

        data.LabelLines.AddRange(
            WrapText(state.Label, GetLabelWrapWidth(), GetLabelWrapFontSize())
        );

        foreach (
            StatusWindowTextLine line in BuildStatusTextLines(
                state.SourceRows,
                GetRowWrapWidth(),
                GetRowWrapFontSize()
            )
        )
        {
            data.Rows.Add(new StatusWindowRowRenderData { Left = line.Left, Right = line.Right });
        }

        return data;
    }

    private Texture2D GetImageTexture(ISceneNode item)
    {
        if (item is Planet planet)
            return uiContext?.GetPlanetTexture(planet);

        return uiContext?.GetEntityTexture(item, false);
    }

    private Texture2D GetStatusImageTexture(ISceneNode item)
    {
        return uiContext?.GetEntityStatusTexture(item, false);
    }

    private Texture2D GetOverlayImageTexture(ISceneNode item)
    {
        return uiContext?.GetEntityCapturedOverlayTexture(item);
    }

    private Texture2D GetImageTexture(StatusWindowImage image, string ownerFactionId)
    {
        string themePath = GetImageThemePath(image, ownerFactionId);
        return uiContext?.GetTexture(themePath);
    }

    private string GetImageThemePath(StatusWindowImage image, string ownerFactionId)
    {
        StatusWindowTheme theme = uiContext?.GetTheme(ownerFactionId)?.StrategyWindows?.Status;
        return image switch
        {
            StatusWindowImage.FleetBanner => theme?.FleetBannerImagePath,
            StatusWindowImage.FleetBannerEnroute => theme?.FleetBannerEnrouteImagePath,
            StatusWindowImage.FleetBannerDamaged => theme?.FleetBannerDamagedImagePath,
            StatusWindowImage.Shipyard => theme?.ShipyardImagePath,
            StatusWindowImage.Construction => theme?.ConstructionImagePath,
            StatusWindowImage.Training => theme?.TrainingImagePath,
            StatusWindowImage.FactionConstruction => theme?.FactionConstructionImagePath,
            StatusWindowImage.Enroute => theme?.EnrouteImagePath,
            StatusWindowImage.PersonnelBackground => theme?.PersonnelBackgroundImagePath,
            _ => null,
        };
    }

    private void Awake()
    {
        VerifyReferences();
        infoButton.onClick.AddListener(OpenInfo);
        closeButton.onClick.AddListener(Close);
    }

    private void OpenInfo()
    {
        if (uiContext == null || lastData?.InfoDisabled == true)
            return;

        uiContext.Dispatcher.Send(new StrategyUIRequests.OpenStatusInfo(GetWindowId()));
    }

    private void Close()
    {
        if (uiContext == null)
            return;

        uiContext.Dispatcher.Send(new StrategyUIRequests.CloseWindow(GetWindowId()));
    }

    private int GetWindowId()
    {
        if (windowShell == null)
            windowShell = GetComponent<UIWindow>();

        return windowShell == null ? 0 : windowShell.Id;
    }

    private void RenderHeader(StatusWindowRenderData data)
    {
        headerTextField.gameObject.SetActive(!string.IsNullOrEmpty(data.Header));
        if (string.IsNullOrEmpty(data.Header))
            return;

        UILayout.SetTextContent(headerTextField, data.Header, Color.white);
    }

    private void RenderImages(IReadOnlyList<Texture2D> textures, bool centerImage)
    {
        IReadOnlyList<Texture2D> safeTextures = textures ?? System.Array.Empty<Texture2D>();
        int visibleIndex = 0;
        for (int i = 0; i < safeTextures.Count; i++)
        {
            Texture2D texture = safeTextures[i];
            if (texture == null)
                continue;

            RawImage image = GetStatusImage(visibleIndex);
            RectInt imageArea = UILayout.GetSourceRect(statusImageAreaTemplate);
            RectInt imageRect = GetStatusImageRect(texture, imageArea, centerImage);
            SetImage(image, texture, imageRect.x, imageRect.y, imageRect.width, imageRect.height);
            visibleIndex++;
        }

        for (int i = visibleIndex; i < statusImageViews.Count; i++)
            statusImageViews[i].gameObject.SetActive(false);
    }

    private void RenderLabel(IReadOnlyList<string> labelLines)
    {
        IReadOnlyList<string> safeLines = labelLines ?? System.Array.Empty<string>();
        RectInt template = UILayout.GetSourceRect(labelTextTemplate.rectTransform);
        for (int i = 0; i < safeLines.Count; i++)
        {
            TextMeshProUGUI textField = GetLabelTextField(i);
            UILayout.SetTemplateText(
                textField,
                labelTextTemplate,
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

        for (int i = safeLines.Count; i < labelTextFields.Count; i++)
            labelTextFields[i].gameObject.SetActive(false);
    }

    private void RenderRows(IReadOnlyList<StatusWindowRowRenderData> rows)
    {
        IReadOnlyList<StatusWindowRowRenderData> safeRows =
            rows ?? System.Array.Empty<StatusWindowRowRenderData>();
        bool resetScroll = RowsChanged(safeRows);
        RectInt leftTemplate = UILayout.GetSourceRect(leftRowTextTemplate.rectTransform);
        RectInt rightTemplate = UILayout.GetSourceRect(rightRowTextTemplate.rectTransform);
        rowsScrollArea.SetContentHeight(
            safeRows.Count * leftTemplate.height,
            leftTemplate.height,
            resetScroll
        );

        for (int i = 0; i < safeRows.Count; i++)
        {
            StatusWindowRowRenderData row = safeRows[i];
            TextMeshProUGUI left = GetLeftRowTextField(i);
            TextMeshProUGUI right = GetRightRowTextField(i);
            int offset = i * leftTemplate.height;
            UILayout.SetTemplateText(
                left,
                leftRowTextTemplate,
                row.Left,
                Color.white,
                new RectInt(
                    leftTemplate.x,
                    leftTemplate.y + offset,
                    leftTemplate.width,
                    leftTemplate.height
                )
            );
            UILayout.SetTemplateText(
                right,
                rightRowTextTemplate,
                row.Right,
                Color.white,
                new RectInt(
                    rightTemplate.x,
                    rightTemplate.y + offset,
                    rightTemplate.width,
                    rightTemplate.height
                )
            );
        }

        for (int i = safeRows.Count; i < leftRowTextFields.Count; i++)
        {
            leftRowTextFields[i].gameObject.SetActive(false);
            rightRowTextFields[i].gameObject.SetActive(false);
        }

        renderedAnyRows = true;
        renderedRows.Clear();
        for (int i = 0; i < safeRows.Count; i++)
            renderedRows.Add(safeRows[i]);
    }

    private RawImage GetStatusImage(int index)
    {
        while (statusImageViews.Count <= index)
        {
            RawImage image = Instantiate(statusImageTemplate, imagesRoot);
            image.name = $"StatusImage{statusImageViews.Count}";
            statusImageViews.Add(image);
        }

        return statusImageViews[index];
    }

    private TextMeshProUGUI GetLabelTextField(int index)
    {
        while (labelTextFields.Count <= index)
        {
            TextMeshProUGUI textField = Instantiate(labelTextTemplate, transform);
            textField.name = $"LabelTextField{labelTextFields.Count}";
            labelTextFields.Add(textField);
        }

        return labelTextFields[index];
    }

    private TextMeshProUGUI GetLeftRowTextField(int index)
    {
        while (leftRowTextFields.Count <= index)
        {
            TextMeshProUGUI textField = Instantiate(
                leftRowTextTemplate,
                rowsScrollArea.ContentRoot
            );
            textField.name = $"LeftRowTextField{leftRowTextFields.Count}";
            leftRowTextFields.Add(textField);
        }

        return leftRowTextFields[index];
    }

    private TextMeshProUGUI GetRightRowTextField(int index)
    {
        while (rightRowTextFields.Count <= index)
        {
            TextMeshProUGUI textField = Instantiate(
                rightRowTextTemplate,
                rowsScrollArea.ContentRoot
            );
            textField.name = $"RightRowTextField{rightRowTextFields.Count}";
            rightRowTextFields.Add(textField);
        }

        return rightRowTextFields[index];
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (headerTextField == null)
            throw new MissingReferenceException($"{name}/HeaderTextField is missing.");
        if (imagesRoot == null)
            throw new MissingReferenceException($"{name}/Images is missing.");
        if (statusImageTemplate == null)
            throw new MissingReferenceException($"{name}/StatusImageTemplate is missing.");
        if (statusImageAreaTemplate == null)
            throw new MissingReferenceException($"{name}/StatusImageAreaTemplate is missing.");
        if (labelTextTemplate == null)
            throw new MissingReferenceException($"{name}/LabelTextTemplate is missing.");
        if (rowsScrollArea == null)
            throw new MissingReferenceException($"{name}/RowsScrollArea is missing.");
        if (leftRowTextTemplate == null)
            throw new MissingReferenceException($"{name}/LeftRowTextTemplate is missing.");
        if (rightRowTextTemplate == null)
            throw new MissingReferenceException($"{name}/RightRowTextTemplate is missing.");
        if (infoButtonImage == null)
            throw new MissingReferenceException($"{name}/InfoButtonImage is missing.");
        if (closeButtonImage == null)
            throw new MissingReferenceException($"{name}/CloseButtonImage is missing.");
        if (infoButton == null)
            throw new MissingReferenceException($"{name}/InfoButton is missing.");
        if (closeButton == null)
            throw new MissingReferenceException($"{name}/CloseButton is missing.");
        if (infoButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonUpTexture is missing.");
        if (infoButtonDisabledTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonDisabledTexture is missing.");
        if (closeButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonUpTexture is missing.");

        statusImageTemplate.gameObject.SetActive(false);
        statusImageAreaTemplate.gameObject.SetActive(false);
        labelTextTemplate.gameObject.SetActive(false);
        leftRowTextTemplate.gameObject.SetActive(false);
        rightRowTextTemplate.gameObject.SetActive(false);
    }

    private int GetRowHeight()
    {
        return UILayout.GetSourceRect(leftRowTextTemplate.rectTransform).height;
    }

    private bool RowsChanged(IReadOnlyList<StatusWindowRowRenderData> rows)
    {
        if (!renderedAnyRows || renderedRows.Count != rows.Count)
            return true;

        for (int i = 0; i < rows.Count; i++)
        {
            if (renderedRows[i].Left != rows[i].Left || renderedRows[i].Right != rows[i].Right)
                return true;
        }

        return false;
    }

    private Texture2D GetBackgroundTexture(string ownerFactionId)
    {
        StatusWindowTheme theme = uiContext?.GetTheme(ownerFactionId)?.StrategyWindows?.Status;
        return uiContext?.GetTexture(theme?.BackgroundImagePath);
    }

    private Texture2D GetInfoButtonTexture(bool disabled)
    {
        if (disabled)
            return infoButtonDisabledTexture;

        return infoButtonUpTexture;
    }

    private Texture2D GetCloseButtonTexture()
    {
        return closeButtonUpTexture;
    }

    private static RectInt GetStatusImageRect(
        Texture2D texture,
        RectInt imageArea,
        bool centerImage
    )
    {
        int width = texture.width;
        int height = texture.height;
        if (width > imageArea.width || height > imageArea.height)
        {
            float scale = Mathf.Min(
                imageArea.width / (float)width,
                imageArea.height / (float)height
            );
            width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
        }

        int x = imageArea.x + (centerImage ? (imageArea.width - width) / 2 : 0);
        int y = imageArea.y + (centerImage ? (imageArea.height - height) / 2 : 0);
        return new RectInt(x, y, width, height);
    }

    private static void SetImageAtTemplateOrigin(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    private static void SetImageFromTemplate(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
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

    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static List<StatusWindowTextLine> BuildStatusTextLines(
        IReadOnlyList<StrategyStatusRow> rows,
        int width,
        int fontSize
    )
    {
        List<StatusWindowTextLine> lines = new List<StatusWindowTextLine>();
        int sideWidth = width / 2;
        int leftWidth = sideWidth;
        int rightWidth = sideWidth - 10;

        IReadOnlyList<StrategyStatusRow> safeRows = rows ?? System.Array.Empty<StrategyStatusRow>();
        foreach (StrategyStatusRow row in safeRows)
        {
            List<string> leftLines = WrapText(row.Left, leftWidth, fontSize);
            List<string> rightLines = WrapText(row.Right, rightWidth, fontSize);
            if (leftLines.Count == 0)
                leftLines.Add(" ");
            if (rightLines.Count == 0)
                rightLines.Add(" ");

            int count = Math.Max(leftLines.Count, rightLines.Count);
            int rightOffset =
                leftLines.Count > 1 && rightLines.Count < leftLines.Count
                    ? leftLines.Count - rightLines.Count
                    : 0;

            for (int i = 0; i < count; i++)
            {
                string left = i < leftLines.Count ? leftLines[i] : " ";
                int rightIndex = i - rightOffset;
                string right =
                    rightIndex >= 0 && rightIndex < rightLines.Count ? rightLines[rightIndex] : " ";
                lines.Add(new StatusWindowTextLine(left, right));
            }
        }

        return lines;
    }

    private static List<string> WrapText(string text, int width, int fontSize)
    {
        List<string> lines = new List<string>();
        if (string.IsNullOrEmpty(text))
            return lines;

        int characterWidth = Math.Max(4, Mathf.CeilToInt(fontSize * 0.5f));
        int maxCharacters = Math.Max(1, width / characterWidth);
        string[] words = text.Split(' ');
        string line = string.Empty;

        foreach (string word in words)
        {
            if (word.Length > maxCharacters)
            {
                if (line.Length > 0)
                {
                    lines.Add(line);
                    line = string.Empty;
                }

                for (int i = 0; i < word.Length; i += maxCharacters)
                    lines.Add(word.Substring(i, Math.Min(maxCharacters, word.Length - i)));
                continue;
            }

            string next = line.Length == 0 ? word : line + " " + word;
            if (next.Length > maxCharacters)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = next;
            }
        }

        if (line.Length > 0)
            lines.Add(line);

        return lines;
    }
}

public sealed class StatusWindowRenderData
{
    public int X;
    public int Y;
    public string OwnerFactionId;
    public bool CenterImage;
    public bool InfoDisabled;
    public string Header;
    public string Label;
    public IReadOnlyList<StrategyStatusRow> SourceRows;
    public IReadOnlyList<StatusWindowImage> Images;
    public IReadOnlyList<ISceneNode> SourceStatusImageItems;
    public IReadOnlyList<ISceneNode> SourceImageItems;
    public IReadOnlyList<ISceneNode> SourceOverlayImageItems;
    public List<Texture2D> ImageTextures = new List<Texture2D>();
    public List<string> LabelLines = new List<string>();
    public List<StatusWindowRowRenderData> Rows = new List<StatusWindowRowRenderData>();
}

public sealed class StatusWindowRowRenderData
{
    public string Left;
    public string Right;
}

public sealed class StrategyStatusInfo
{
    public string OwnerFactionId;
    public string Header;
    public string Label;
    public bool CenterImage;
    public List<StatusWindowImage> Images = new List<StatusWindowImage>();
    public List<ISceneNode> StatusImageItems = new List<ISceneNode>();
    public List<ISceneNode> ImageItems = new List<ISceneNode>();
    public List<ISceneNode> OverlayImageItems = new List<ISceneNode>();
    public List<StrategyStatusRow> Rows = new List<StrategyStatusRow>();
}

public enum StatusWindowImage
{
    Shipyard,
    Construction,
    Training,
    FleetBanner,
    FleetBannerEnroute,
    FleetBannerDamaged,
    FactionConstruction,
    Enroute,
    PersonnelBackground,
}

public sealed class StrategyStatusRow
{
    public StrategyStatusRow(string left, string right)
    {
        Left = left ?? string.Empty;
        Right = right ?? string.Empty;
    }

    public string Left { get; }
    public string Right { get; }
}

public sealed class StatusWindowTextLine
{
    public StatusWindowTextLine(string left, string right)
    {
        Left = left ?? string.Empty;
        Right = right ?? string.Empty;
    }

    public string Left { get; }
    public string Right { get; }
}
