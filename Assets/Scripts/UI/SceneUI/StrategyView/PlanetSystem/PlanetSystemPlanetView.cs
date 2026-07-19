using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one authored planet-system planet and reports semantic pointer interaction.
/// </summary>
public sealed class PlanetSystemPlanetView
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler,
        IPointerDownHandler,
        IPointerClickHandler,
        IDropHandler
{
    private static readonly PlanetIcon[] _hitTestIcons =
    {
        PlanetIcon.Facility,
        PlanetIcon.Defense,
        PlanetIcon.Fleet,
        PlanetIcon.Mission,
    };

    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage planetImage;

    [SerializeField]
    private RawImage facilityImage;

    [SerializeField]
    private RawImage defenseImage;

    [SerializeField]
    private RawImage fleetImage;

    [SerializeField]
    private RawImage missionImage;

    [SerializeField]
    private RawImage headquartersImage;

    [SerializeField]
    private TextMeshProUGUI planetNameTextField;

    [SerializeField]
    private RectTransform energyBarRoot;

    [SerializeField]
    private RectTransform rawBarRoot;

    [SerializeField]
    private RectTransform supportBarRoot;

    [SerializeField]
    private Image energyBarBackgroundImage;

    [SerializeField]
    private Image energyBarFillImage;

    [SerializeField]
    private Image[] energyBarCellImages = Array.Empty<Image>();

    [SerializeField]
    private Image rawBarBackgroundImage;

    [SerializeField]
    private Image rawBarFillImage;

    [SerializeField]
    private Image[] rawBarCellImages = Array.Empty<Image>();

    [SerializeField]
    private Image supportBarBackgroundImage;

    [SerializeField]
    private Image supportBarFillImage;

    private PlanetSystemBarView energyBar;
    private PlanetSystemPlanetRenderData lastRenderData;
    private PlanetSystemBarView rawBar;
    private PlanetSystemBarView supportBar;

    /// <summary>
    /// Occurs when the control is clicked.
    /// </summary>
    internal event Action<
        PlanetSystemPlanetView,
        PlanetSystemWindowElement,
        PointerEventData
    > Clicked;

    /// <summary>
    /// Occurs when the pointer hover is cleared.
    /// </summary>
    internal event Action<PlanetSystemPlanetView> HoverCleared;

    /// <summary>
    /// Occurs when the pointer hovers over the control.
    /// </summary>
    internal event Action<
        PlanetSystemPlanetView,
        PlanetSystemWindowElement,
        PointerEventData
    > Hovered;

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    internal event Action<
        PlanetSystemPlanetView,
        PlanetSystemWindowElement,
        PointerEventData
    > Pressed;

    /// <summary>
    /// Occurs when the control is released.
    /// </summary>
    internal event Action<
        PlanetSystemPlanetView,
        PlanetSystemWindowElement,
        PointerEventData
    > Released;

    /// <summary>
    /// Renders one complete planet presentation at its projected source-space position.
    /// </summary>
    /// <param name="data">The immutable planet presentation.</param>
    /// <param name="position">The projected source-space planet image position.</param>
    public void Render(PlanetSystemPlanetRenderData data, Vector2Int position)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        EnsureBars();
        lastRenderData = data;

        RectInt planetTemplate = UILayout.GetSourceRect(planetImage.rectTransform);
        int planetWidth = planetTemplate.width;
        RectTransform root = transform as RectTransform;
        RectInt rootTemplate = UILayout.GetSourceRect(root);
        UILayout.SetSourceRect(
            root,
            position.x - planetTemplate.x,
            position.y - planetTemplate.y,
            rootTemplate.width,
            rootTemplate.height
        );

        SetImage(planetImage, data.PlanetTexture);
        RenderIcon(
            facilityImage,
            PlanetIcon.Facility,
            data.FacilityTexture,
            data.FacilityPressedTexture
        );
        RenderIcon(
            defenseImage,
            PlanetIcon.Defense,
            data.DefenseTexture,
            data.DefensePressedTexture
        );
        RenderIcon(fleetImage, PlanetIcon.Fleet, data.FleetTexture, data.FleetPressedTexture);
        RenderIcon(
            missionImage,
            PlanetIcon.Mission,
            data.MissionTexture,
            data.MissionPressedTexture
        );
        SetImage(headquartersImage, data.HeadquartersTexture);
        energyBar.Render(data.EnergyBar, planetWidth);
        rawBar.Render(data.RawResourceBar, planetWidth);
        supportBar.Render(data.SupportBar, planetWidth);
        UILayout.SetTextContent(planetNameTextField, data.Name, data.NameColor);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Gets the current source-space bounds of the planet view.
    /// </summary>
    /// <returns>The rendered source-space bounds.</returns>
    internal RectInt GetRenderedSourceRect()
    {
        return UILayout.GetSourceRect(transform as RectTransform);
    }

    /// <summary>
    /// Gets the current source-space bounds of the planet image within the parent window.
    /// </summary>
    /// <returns>The rendered planet-image bounds.</returns>
    internal RectInt GetRenderedPlanetImageSourceRect()
    {
        RectInt root = GetRenderedSourceRect();
        RectInt image = UILayout.GetSourceRect(planetImage.rectTransform);
        return new RectInt(root.x + image.x, root.y + image.y, image.width, image.height);
    }

    /// <summary>
    /// Tries to resolve a visible icon's authored transform.
    /// </summary>
    /// <param name="icon">The requested planet icon.</param>
    /// <param name="rect">Receives the authored icon transform.</param>
    /// <returns>True when the icon is visible.</returns>
    internal bool TryGetIconSourceRect(PlanetIcon icon, out RectTransform rect)
    {
        RawImage image = GetIconImage(icon);
        if (!image || !image.isActiveAndEnabled)
        {
            rect = null;
            return false;
        }

        rect = image.rectTransform;
        return true;
    }

    /// <summary>
    /// Tries to resolve the fleet image used by a drag preview.
    /// </summary>
    /// <param name="texture">Receives the fleet drag image.</param>
    /// <param name="rect">Receives the authored fleet-image transform.</param>
    /// <returns>True when a fleet image is available.</returns>
    internal bool TryGetFleetDragImage(out Texture texture, out RectTransform rect)
    {
        texture = null;
        if (!TryGetIconSourceRect(PlanetIcon.Fleet, out rect) || lastRenderData == null)
            return false;

        texture = lastRenderData.FleetPressedTexture ?? lastRenderData.FleetTexture;
        return texture != null;
    }

    /// <summary>
    /// Tries to create a semantic hit from the current pointer raycast.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="element">Receives the semantic presentation element.</param>
    /// <returns>True when a visible planet element was hit.</returns>
    internal bool TryCreateElement(
        PointerEventData eventData,
        out PlanetSystemWindowElement element
    )
    {
        element = CreateElement(null, eventData, true);
        return element != null;
    }

    /// <summary>
    /// Tries to create a semantic hit from an explicit raycast target.
    /// </summary>
    /// <param name="target">The explicit target object.</param>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="element">Receives the semantic presentation element.</param>
    /// <returns>True when a visible planet element was hit.</returns>
    internal bool TryCreateElement(
        GameObject target,
        PointerEventData eventData,
        out PlanetSystemWindowElement element
    )
    {
        element = CreateElement(target, eventData, true);
        return element != null;
    }

    /// <summary>
    /// Reports pointer entry over the planet presentation.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        DispatchHover(eventData);
    }

    /// <summary>
    /// Reports pointer exit from the planet presentation.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        HoverCleared?.Invoke(this);
    }

    /// <summary>
    /// Reports pointer movement across the planet presentation.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerMove(PointerEventData eventData)
    {
        DispatchHover(eventData);
    }

    /// <summary>
    /// Reports a left or right pointer press over a visible planet element.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (
            eventData?.button
                is not (PointerEventData.InputButton.Left or PointerEventData.InputButton.Right)
            || CreateElement(null, eventData, true) is not PlanetSystemWindowElement element
        )
            return;

        Pressed?.Invoke(this, element, eventData);
    }

    /// <summary>
    /// Reports a completed left click and double click over a visible planet element.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (
            eventData?.button != PointerEventData.InputButton.Left
            || CreateElement(null, eventData, true) is not PlanetSystemWindowElement element
        )
            return;

        if (eventData.clickCount > 1)
            Clicked?.Invoke(this, element, eventData);
        Released?.Invoke(this, element, eventData);
    }

    /// <summary>
    /// Reports a drop over a visible planet element.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnDrop(PointerEventData eventData)
    {
        PlanetSystemWindowElement element = CreateElement(null, eventData, false);
        if (element != null)
            Released?.Invoke(this, element, eventData);
    }

    /// <summary>
    /// Verifies authored references and captures bar layouts.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        EnsureBars();
    }

    /// <summary>
    /// Renders one overlay icon from its normal and pressed images.
    /// </summary>
    /// <param name="image">The destination image.</param>
    /// <param name="icon">The represented icon.</param>
    /// <param name="normalTexture">The normal image.</param>
    /// <param name="pressedTexture">The pressed image.</param>
    private void RenderIcon(
        RawImage image,
        PlanetIcon icon,
        Texture2D normalTexture,
        Texture2D pressedTexture
    )
    {
        bool pressed = lastRenderData.SelectedIcon == icon || lastRenderData.HoveredIcon == icon;
        SetImage(image, pressed ? pressedTexture : normalTexture);
    }

    /// <summary>
    /// Applies an optional texture without changing authored bounds.
    /// </summary>
    /// <param name="image">The destination image.</param>
    /// <param name="texture">The displayed image.</param>
    private static void SetImage(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = texture != null;
    }

    /// <summary>
    /// Reports the currently hovered planet element.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void DispatchHover(PointerEventData eventData)
    {
        PlanetSystemWindowElement element = CreateElement(null, eventData, true);
        if (element == null)
            HoverCleared?.Invoke(this);
        else
            Hovered?.Invoke(this, element, eventData);
    }

    /// <summary>
    /// Creates a semantic hit from source-space geometry and raycast fallback.
    /// </summary>
    /// <param name="target">The optional explicit target object.</param>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="allowPressFallback">Whether the pressed raycast target may be used.</param>
    /// <returns>The semantic hit, or null.</returns>
    private PlanetSystemWindowElement CreateElement(
        GameObject target,
        PointerEventData eventData,
        bool allowPressFallback
    )
    {
        if (lastRenderData == null || eventData == null)
            return null;

        target ??= eventData.pointerCurrentRaycast.gameObject;
        if (target == null && allowPressFallback)
            target = eventData.pointerPressRaycast.gameObject;

        PlanetIcon icon = PlanetIcon.None;
        bool planetImageHit = false;
        if (TryGetPointerSourcePosition(eventData, out int sourceX, out int sourceY))
        {
            icon = GetSourceIcon(sourceX, sourceY);
            planetImageHit = icon == PlanetIcon.None && IsPlanetImageSourcePoint(sourceX, sourceY);
        }

        if (icon == PlanetIcon.None && !planetImageHit)
        {
            icon = GetTargetIcon(target);
            planetImageHit = IsPlanetImageTarget(target);
        }

        return icon == PlanetIcon.None && !planetImageHit
            ? null
            : new PlanetSystemWindowElement(lastRenderData.PlanetIndex, icon, planetImageHit);
    }

    /// <summary>
    /// Gets the visible overlay icon represented by a raycast target.
    /// </summary>
    /// <param name="target">The raycast target.</param>
    /// <returns>The matching icon, or none.</returns>
    private PlanetIcon GetTargetIcon(GameObject target)
    {
        if (IsTargetOrChild(target, facilityImage) && facilityImage.isActiveAndEnabled)
            return PlanetIcon.Facility;
        if (IsTargetOrChild(target, defenseImage) && defenseImage.isActiveAndEnabled)
            return PlanetIcon.Defense;
        if (IsTargetOrChild(target, fleetImage) && fleetImage.isActiveAndEnabled)
            return PlanetIcon.Fleet;
        if (IsTargetOrChild(target, missionImage) && missionImage.isActiveAndEnabled)
            return PlanetIcon.Mission;
        return PlanetIcon.None;
    }

    /// <summary>
    /// Gets the visible overlay icon containing a source-space point.
    /// </summary>
    /// <param name="x">The local source-space horizontal coordinate.</param>
    /// <param name="y">The local source-space vertical coordinate.</param>
    /// <returns>The matching icon, or none.</returns>
    private PlanetIcon GetSourceIcon(int x, int y)
    {
        foreach (PlanetIcon icon in _hitTestIcons)
        {
            if (IsImageSourcePoint(GetIconImage(icon), x, y))
                return icon;
        }

        return PlanetIcon.None;
    }

    /// <summary>
    /// Determines whether a target belongs to the planet or headquarters image.
    /// </summary>
    /// <param name="target">The raycast target.</param>
    /// <returns>True when the planet image was hit.</returns>
    private bool IsPlanetImageTarget(GameObject target)
    {
        return IsTargetOrChild(target, planetImage) || IsTargetOrChild(target, headquartersImage);
    }

    /// <summary>
    /// Determines whether a source-space point lies in the planet or headquarters image.
    /// </summary>
    /// <param name="x">The local source-space horizontal coordinate.</param>
    /// <param name="y">The local source-space vertical coordinate.</param>
    /// <returns>True when the point lies in the planet image.</returns>
    private bool IsPlanetImageSourcePoint(int x, int y)
    {
        return IsImageSourcePoint(planetImage, x, y) || IsImageSourcePoint(headquartersImage, x, y);
    }

    /// <summary>
    /// Converts a pointer position into local source-space coordinates.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="x">Receives the local horizontal coordinate.</param>
    /// <param name="y">Receives the local vertical coordinate.</param>
    /// <returns>True when the pointer lies within the planet view.</returns>
    private bool TryGetPointerSourcePosition(PointerEventData eventData, out int x, out int y)
    {
        x = 0;
        y = 0;
        RectTransform rect = transform as RectTransform;
        if (
            rect == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                eventData.position,
                eventData.pressEventCamera ?? eventData.enterEventCamera,
                out Vector2 local
            )
        )
            return false;

        RectInt root = UILayout.GetSourceRect(rect);
        x = Mathf.RoundToInt(local.x);
        y = Mathf.RoundToInt(-local.y);
        return x >= 0 && x < root.width && y >= 0 && y < root.height;
    }

    /// <summary>
    /// Determines whether a visible image contains a source-space point.
    /// </summary>
    /// <param name="image">The candidate image.</param>
    /// <param name="x">The local horizontal coordinate.</param>
    /// <param name="y">The local vertical coordinate.</param>
    /// <returns>True when the point lies inside the image.</returns>
    private static bool IsImageSourcePoint(RawImage image, int x, int y)
    {
        return image
            && image.isActiveAndEnabled
            && UILayout.GetSourceRect(image.rectTransform).Contains(new Vector2Int(x, y));
    }

    /// <summary>
    /// Determines whether a raycast target is an image or its child.
    /// </summary>
    /// <param name="target">The raycast target.</param>
    /// <param name="image">The candidate image.</param>
    /// <returns>True when the target belongs to the image.</returns>
    private static bool IsTargetOrChild(GameObject target, Component image)
    {
        return target != null
            && image != null
            && (target.transform == image.transform || target.transform.IsChildOf(image.transform));
    }

    /// <summary>
    /// Gets the image component for one planet overlay icon.
    /// </summary>
    /// <param name="icon">The requested icon.</param>
    /// <returns>The matching image, or null.</returns>
    private RawImage GetIconImage(PlanetIcon icon)
    {
        return icon switch
        {
            PlanetIcon.Facility => facilityImage,
            PlanetIcon.Defense => defenseImage,
            PlanetIcon.Fleet => fleetImage,
            PlanetIcon.Mission => missionImage,
            _ => null,
        };
    }

    /// <summary>
    /// Creates bar renderers from the authored bar components.
    /// </summary>
    private void EnsureBars()
    {
        energyBar ??= new PlanetSystemBarView(
            energyBarRoot,
            energyBarBackgroundImage,
            energyBarFillImage,
            energyBarCellImages
        );
        rawBar ??= new PlanetSystemBarView(
            rawBarRoot,
            rawBarBackgroundImage,
            rawBarFillImage,
            rawBarCellImages
        );
        supportBar ??= new PlanetSystemBarView(
            supportBarRoot,
            supportBarBackgroundImage,
            supportBarFillImage,
            Array.Empty<Image>()
        );
    }

    /// <summary>
    /// Verifies every authored child reference required by the planet view.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (planetImage == null)
            throw new MissingReferenceException($"{name}/PlanetImage is missing.");
        if (facilityImage == null)
            throw new MissingReferenceException($"{name}/FacilityImage is missing.");
        if (defenseImage == null)
            throw new MissingReferenceException($"{name}/DefenseImage is missing.");
        if (fleetImage == null)
            throw new MissingReferenceException($"{name}/FleetImage is missing.");
        if (missionImage == null)
            throw new MissingReferenceException($"{name}/MissionImage is missing.");
        if (headquartersImage == null)
            throw new MissingReferenceException($"{name}/HeadquartersImage is missing.");
        if (planetNameTextField == null)
            throw new MissingReferenceException($"{name}/PlanetNameTextField is missing.");
        if (energyBarRoot == null)
            throw new MissingReferenceException($"{name}/EnergyBar is missing.");
        if (rawBarRoot == null)
            throw new MissingReferenceException($"{name}/RawBar is missing.");
        if (supportBarRoot == null)
            throw new MissingReferenceException($"{name}/SupportBar is missing.");
        if (energyBarBackgroundImage == null || energyBarFillImage == null)
            throw new MissingReferenceException($"{name}/EnergyBar images are missing.");
        VerifySegmentedBarReferences("EnergyBar", energyBarCellImages);
        if (rawBarBackgroundImage == null || rawBarFillImage == null)
            throw new MissingReferenceException($"{name}/RawBar images are missing.");
        VerifySegmentedBarReferences("RawBar", rawBarCellImages);
        if (supportBarBackgroundImage == null || supportBarFillImage == null)
            throw new MissingReferenceException($"{name}/SupportBar images are missing.");
    }

    /// <summary>
    /// Verifies the serialized cell pool for one segmented bar.
    /// </summary>
    /// <param name="barName">The authored bar name.</param>
    /// <param name="cellImages">The serialized cell images.</param>
    private void VerifySegmentedBarReferences(string barName, IReadOnlyList<Image> cellImages)
    {
        if (cellImages == null || cellImages.Count < 2)
            throw new MissingReferenceException($"{name}/{barName} cell images are incomplete.");

        for (int index = 0; index < cellImages.Count; index++)
        {
            if (cellImages[index] == null)
            {
                throw new MissingReferenceException(
                    $"{name}/{barName}/Cell{index}Image is missing."
                );
            }
        }
    }

    /// <summary>
    /// Renders one authored segmented or continuous planet status bar.
    /// </summary>
    private sealed class PlanetSystemBarView
    {
        private readonly Image background;
        private readonly RectInt backgroundTemplate;
        private readonly RectInt cellTemplate;
        private readonly int cellStride;
        private readonly IReadOnlyList<Image> cells;
        private readonly Image fill;
        private readonly RectInt fillTemplate;
        private readonly RectTransform root;
        private readonly RectInt rootTemplate;

        /// <summary>
        /// Creates a bar renderer from authored child components.
        /// </summary>
        /// <param name="root">The authored bar root.</param>
        /// <param name="background">The authored background image.</param>
        /// <param name="fill">The authored continuous fill image.</param>
        /// <param name="cells">The authored segmented cells.</param>
        public PlanetSystemBarView(
            RectTransform root,
            Image background,
            Image fill,
            IReadOnlyList<Image> cells
        )
        {
            this.root = root;
            this.background = background;
            this.fill = fill;
            this.cells = cells ?? Array.Empty<Image>();
            rootTemplate = UILayout.GetSourceRect(root);
            backgroundTemplate = UILayout.GetSourceRect(background.rectTransform);
            fillTemplate = UILayout.GetSourceRect(fill.rectTransform);
            cellTemplate = default;
            cellStride = 0;
            if (this.cells.Count >= 2)
            {
                cellTemplate = UILayout.GetSourceRect(this.cells[0].rectTransform);
                RectInt secondCellTemplate = UILayout.GetSourceRect(this.cells[1].rectTransform);
                cellStride = secondCellTemplate.x - cellTemplate.x;
            }
        }

        /// <summary>
        /// Applies one immutable status-bar presentation.
        /// </summary>
        /// <param name="data">The status-bar presentation.</param>
        /// <param name="continuousWidth">The authored width for a continuous bar.</param>
        public void Render(PlanetSystemBarRenderData data, int continuousWidth)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            root.gameObject.SetActive(data.Visible);
            if (!data.Visible)
                return;

            int barWidth =
                data.CellCount > 0 ? GetSegmentedBarWidth(data.CellCount) : continuousWidth;
            UILayout.SetSourceRect(
                root,
                rootTemplate.x,
                rootTemplate.y,
                barWidth,
                rootTemplate.height
            );
            background.color = data.BackgroundColor;
            UILayout.SetSourceRect(
                background.rectTransform,
                backgroundTemplate.x,
                backgroundTemplate.y,
                barWidth,
                backgroundTemplate.height
            );

            if (data.CellCount <= 0)
            {
                RenderContinuousFill(data, continuousWidth);
                HideCells(0);
                return;
            }

            fill.gameObject.SetActive(false);
            for (int index = 0; index < data.CellCount; index++)
            {
                Image cell = cells[index];
                RectInt rect = GetCellRect(index);
                cell.color = index < data.LitCells ? data.FillColor : data.EmptyColor;
                UILayout.SetSourceRect(cell.rectTransform, rect.x, rect.y, rect.width, rect.height);
                cell.gameObject.SetActive(true);
            }
            HideCells(data.CellCount);
        }

        /// <summary>
        /// Renders a continuous bar fill within authored bounds.
        /// </summary>
        /// <param name="data">The status-bar presentation.</param>
        /// <param name="continuousWidth">The authored continuous bar width.</param>
        private void RenderContinuousFill(PlanetSystemBarRenderData data, int continuousWidth)
        {
            int fillWidth = Mathf.RoundToInt(Mathf.Clamp01(data.FillRatio) * continuousWidth);
            bool visible = fillWidth > 0 && data.FillColor.a > 0;
            fill.gameObject.SetActive(visible);
            if (!visible)
                return;

            fill.color = data.FillColor;
            UILayout.SetSourceRect(
                fill.rectTransform,
                fillTemplate.x,
                fillTemplate.y,
                fillWidth,
                fillTemplate.height
            );
        }

        /// <summary>
        /// Calculates a segmented cell's bounds from the first two authored cells.
        /// </summary>
        /// <param name="index">The requested cell index.</param>
        /// <returns>The source-space cell bounds.</returns>
        private RectInt GetCellRect(int index)
        {
            return new RectInt(
                cellTemplate.x + index * cellStride,
                cellTemplate.y,
                cellTemplate.width,
                cellTemplate.height
            );
        }

        /// <summary>
        /// Calculates the total segmented bar width from authored cell spacing.
        /// </summary>
        /// <param name="cellCount">The displayed cell count.</param>
        /// <returns>The source-space bar width.</returns>
        private int GetSegmentedBarWidth(int cellCount)
        {
            if (cells.Count < 2)
                throw new MissingReferenceException($"{root.name} requires two authored cells.");

            return cellCount * cellStride;
        }

        /// <summary>
        /// Hides reusable segmented cells after the active range.
        /// </summary>
        /// <param name="startIndex">The first cell index to hide.</param>
        private void HideCells(int startIndex)
        {
            for (int index = startIndex; index < cells.Count; index++)
                cells[index].gameObject.SetActive(false);
        }
    }
}
