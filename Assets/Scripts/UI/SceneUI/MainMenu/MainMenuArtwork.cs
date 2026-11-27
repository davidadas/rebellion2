using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Applies a pack's artwork and placement to the authored main-menu controls.
/// </summary>
public sealed class MainMenuArtwork : UIBehaviour
{
    [SerializeField]
    private Control[] controls;

    [SerializeField]
    private Image cockpit;

    private MainMenuLayout layout;
    private bool applyLayoutPositions;

    public IReadOnlyList<Control> Controls => controls;

    /// <summary>
    /// Records the editable targets shared by the game and its preview.
    /// </summary>
    /// <param name="targets">The authored menu controls in drawing order.</param>
    /// <param name="background">The cockpit texture containing the default galaxy artwork.</param>
    public void Configure(Control[] targets, Image background)
    {
        controls = targets;
        cockpit = background;
    }

    /// <summary>
    /// Installs the authored model overrides before the model rigs begin loading.
    /// </summary>
    /// <param name="value">The pack's layout and artwork overrides.</param>
    /// <param name="applyLayoutPositions">Whether this component owns positions for the viewport.</param>
    public void Prepare(MainMenuLayout value, bool applyLayoutPositions = true)
    {
        layout = value;
        this.applyLayoutPositions = applyLayoutPositions;
        foreach (Control control in controls)
        {
            string path = layout?.FindControl(control.ID)?.ModelPath;
            if (control.Model != null && !string.IsNullOrWhiteSpace(path))
                control.Model.SetAddress(path, normalize: true);
        }
        ApplyPositions();
    }

    /// <summary>
    /// Applies the configured images after the authored content has been loaded.
    /// </summary>
    /// <param name="assets">The active pack's content source.</param>
    public void ApplyImages(IContentAssetSource assets)
    {
        Texture2D cockpitTexture = cockpit.sprite.texture;
        foreach (Control control in controls)
        {
            MainMenuControlLayout entry = layout?.FindControl(control.ID);
            string path = entry?.ImagePath;
            if (control.BakedBacking != null)
            {
                RawImage graphic = (RawImage)control.Graphic;
                graphic.texture = cockpitTexture;
                bool replaced = !string.IsNullOrWhiteSpace(path);
                if (replaced)
                {
                    graphic.texture = ContentBindings.RequireTexture(assets, path);
                    graphic.uvRect = new Rect(0f, 0f, 1f, 1f);
                }
                control.SetBakedReplacement(replaced);
            }
            else if (!string.IsNullOrWhiteSpace(path))
            {
                if (control.Graphic is Image image)
                    image.sprite = ContentBindings.RequireSprite(assets, path);
                else if (control.Graphic is RawImage rawImage && control.Model == null)
                    rawImage.texture = ContentBindings.RequireTexture(assets, path);
            }
            if (control.Selection != null && !string.IsNullOrWhiteSpace(entry?.SelectionImagePath))
                control.Selection.sprite = ContentBindings.RequireSprite(
                    assets,
                    entry.SelectionImagePath
                );
        }
    }

    /// <summary>
    /// Recomputes the reference-pixel offsets if the actual menu viewport changes size.
    /// </summary>
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        ApplyPositions();
    }

    /// <summary>
    /// Proportions the layout to the current viewport and moves each corresponding hit area.
    /// </summary>
    private void ApplyPositions()
    {
        if (!applyLayoutPositions || layout == null)
            return;
        Vector2 size = ((RectTransform)transform).rect.size;
        Vector2 scale = new(
            size.x / MainMenuLayout.ReferenceWidth,
            size.y / MainMenuLayout.ReferenceHeight
        );
        foreach (Control control in controls)
        {
            MainMenuControlLayout entry = layout.FindControl(control.ID);
            if (entry == null)
                continue;
            control.Move(new Vector2(entry.X * scale.x, -entry.Y * scale.y));
            if (control.Selection != null)
                control.MoveSelection(
                    new Vector2(entry.SelectionX * scale.x, -entry.SelectionY * scale.y)
                );
        }
    }

    /// <summary>
    /// Identifies a drawable menu item and the control that travels with it.
    /// </summary>
    [Serializable]
    public sealed class Control
    {
        [SerializeField]
        private RectTransform target;

        [SerializeField]
        private Graphic graphic;

        [SerializeField]
        private Image selection;

        [SerializeField]
        private Image bakedBacking;

        [SerializeField]
        private ContentModelBinding model;

        [SerializeField]
        private string caption;

        [SerializeField]
        private string artworkName;

        [SerializeField]
        private string selectionName;

        private Vector2? authoredPosition;
        private Vector2? authoredSelectionPosition;
        private Vector2 positionOffset;
        private bool replacesBakedImage;

        public string ID => target.name;
        public RectTransform Target => target;
        public Graphic Graphic => graphic;
        public Image Selection => selection;
        public Image BakedBacking => bakedBacking;
        public ContentModelBinding Model => model;
        public string Caption => caption;
        public string ArtworkName => artworkName;
        public string SelectionName => selectionName;
        public bool CanReplaceImage => model == null && graphic is Image or RawImage;
        public bool IsRenderedBackdrop =>
            model == null && graphic is RawImage { texture: RenderTexture };

        /// <summary>
        /// Associates a displayed item with the geometry and model that supply it.
        /// </summary>
        /// <param name="target">The control or layer to move.</param>
        /// <param name="graphic">The image on that control, when replaceable.</param>
        /// <param name="model">The model backing the item, when replaceable.</param>
        /// <param name="caption">The name displayed in the editor.</param>
        /// <param name="artworkName">The display name of the built-in artwork.</param>
        /// <param name="selectionName">The display name of the built-in selection overlay.</param>
        /// <param name="selection">The optional overlay used for this control's selected state.</param>
        /// <param name="bakedBacking">The original galaxy monitor backing, enabled when its art moves.</param>
        public Control(
            RectTransform target,
            Graphic graphic,
            ContentModelBinding model,
            string caption,
            string artworkName,
            string selectionName,
            Image selection,
            Image bakedBacking
        )
        {
            this.target = target;
            this.graphic = graphic;
            this.model = model;
            this.caption = caption;
            this.artworkName = artworkName;
            this.selectionName = selectionName;
            this.selection = selection;
            this.bakedBacking = bakedBacking;
        }

        /// <summary>
        /// Places the item relative to its authored position.
        /// </summary>
        /// <param name="offset">The displacement in the owning canvas coordinates.</param>
        public void Move(Vector2 offset)
        {
            authoredPosition ??= target.anchoredPosition;
            positionOffset = offset;
            target.anchoredPosition = authoredPosition.Value + offset;
            UpdateBakedVisibility();
        }

        /// <summary>
        /// Places the selection overlay relative to its own authored position.
        /// </summary>
        /// <param name="offset">The displacement in the owning canvas coordinates.</param>
        public void MoveSelection(Vector2 offset)
        {
            RectTransform rect = selection.rectTransform;
            authoredSelectionPosition ??= rect.anchoredPosition;
            rect.anchoredPosition = authoredSelectionPosition.Value + offset;
        }

        /// <summary>
        /// Shows the replacement layer even when the monitor retains its original position.
        /// </summary>
        /// <param name="replaced">Whether the pack supplied its own galaxy image.</param>
        public void SetBakedReplacement(bool replaced)
        {
            replacesBakedImage = replaced;
            UpdateBakedVisibility();
        }

        /// <summary>
        /// Covers the original baked monitor whenever its independent artwork is used.
        /// </summary>
        private void UpdateBakedVisibility()
        {
            if (bakedBacking == null)
                return;
            bool showArtwork = replacesBakedImage || positionOffset != Vector2.zero;
            bakedBacking.enabled = showArtwork;
            graphic.enabled = showArtwork;
        }
    }
}
