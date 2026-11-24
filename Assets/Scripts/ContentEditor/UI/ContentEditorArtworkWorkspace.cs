using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents the artwork draft as an editing workspace with direct output and layer navigation.
/// </summary>
public sealed class ContentEditorArtworkWorkspace : MonoBehaviour, ICancelable
{
    [SerializeField]
    private RectTransform panel;

    [SerializeField]
    private RectTransform header;

    [SerializeField]
    private RectTransform outputBar;

    [SerializeField]
    private RectTransform sidebar;

    [SerializeField]
    private RectTransform canvasBar;

    [SerializeField]
    private RectTransform layerActions;

    [SerializeField]
    private TMP_Text title;

    [SerializeField]
    private TMP_Text detail;

    [SerializeField]
    private TMP_Text status;

    [SerializeField]
    private TMP_Text layerCaption;

    [SerializeField]
    private TMP_Text positionCaption;

    [SerializeField]
    private TMP_Text scope;

    [SerializeField]
    private TMP_Text canvasHint;

    [SerializeField]
    private TMP_Text xCaption;

    [SerializeField]
    private TMP_Text yCaption;

    [SerializeField]
    private TMP_InputField xInput;

    [SerializeField]
    private TMP_InputField yInput;

    [SerializeField]
    private RectTransform scaleControls;

    [SerializeField]
    private TMP_Text scaleCaption;

    [SerializeField]
    private TMP_Text scaleValue;

    [SerializeField]
    private Button decreaseScale;

    [SerializeField]
    private Button increaseScale;

    [SerializeField]
    private Button back;

    [SerializeField]
    private Button save;

    [SerializeField]
    private Button undo;

    [SerializeField]
    private Button redo;

    [SerializeField]
    private Button[] outputButtons;

    [SerializeField]
    private Button draftButton;

    [SerializeField]
    private Button currentButton;

    [SerializeField]
    private Button fit;

    [SerializeField]
    private Button actual;

    [SerializeField]
    private Button add;

    [SerializeField]
    private Button importLayer;

    [SerializeField]
    private Button duplicate;

    [SerializeField]
    private Button remove;

    [SerializeField]
    private Button forward;

    [SerializeField]
    private Button backward;

    [SerializeField]
    private Button rebuild;

    [SerializeField]
    private Button varyShipWakes;

    [SerializeField]
    private ScrollAreaLayout layerScroll;

    [SerializeField]
    private ContentEditorArtworkLayerRow rowTemplate;

    [SerializeField]
    private ContentEditorArtworkCanvas artworkCanvas;

    private readonly List<ContentEditorArtworkLayerRow> rows = new();
    private IReadOnlyList<ArtworkEffectPreviewFrame> frames;
    private IReadOnlyList<ArtworkEffectPreviewFrame> current;
    private ArtworkEffectActions actions;
    private Action<Action, Action> confirmClose;
    private ArtworkEffectLayerPreview? selectedLayer;
    private int selectedIndex;
    private int outputIndex;
    private bool comparing;
    private bool dirty;
    private bool bound;

    internal bool HasUnsavedChanges => gameObject.activeSelf && dirty;

    /// <summary>
    /// Adds a dropped file to the active draft when it lands in the canvas or layer inspector.
    /// </summary>
    /// <param name="target">The visible control beneath the drop.</param>
    /// <param name="path">The dropped image file.</param>
    /// <returns>Whether the draft received the file.</returns>
    internal bool TryImportDroppedImage(Transform target, string path)
    {
        if (
            comparing
            || actions?.ImportDroppedImage == null
            || (!target.IsChildOf(artworkCanvas.transform) && !target.IsChildOf(sidebar))
        )
            return false;
        actions.ImportDroppedImage(path);
        return true;
    }

    /// <summary>
    /// Opens or refreshes the artwork editor while preserving the active output and canvas.
    /// </summary>
    /// <param name="name">The effect being edited.</param>
    /// <param name="unit">The unit and source shown in the header.</param>
    /// <param name="outputs">The layered gameplay outputs.</param>
    /// <param name="initialOutput">The output that opened the workspace.</param>
    /// <param name="selection">The selected layer index.</param>
    /// <param name="changed">Whether there are unsaved edits.</param>
    /// <param name="commands">The operations supported by this draft.</param>
    /// <param name="confirm">Prompts before discarding unsaved work.</param>
    internal void Show(
        string name,
        string unit,
        IReadOnlyList<ArtworkEffectPreviewFrame> outputs,
        int initialOutput,
        int selection,
        bool changed,
        ArtworkEffectActions commands,
        Action<Action, Action> confirm
    )
    {
        Bind();
        bool opening = frames == null;
        bool changedOutput = opening || outputIndex != initialOutput;
        outputIndex = initialOutput;
        frames = outputs;
        selectedIndex = selection;
        dirty = changed;
        actions = commands;
        confirmClose = confirm;
        if (opening)
        {
            current = null;
            comparing = false;
        }
        title.text = name;
        detail.text = unit;
        status.text = dirty ? "Unsaved artwork" : "Saved artwork";
        undo.interactable = commands.Undo != null;
        redo.interactable = commands.Redo != null;
        save.interactable = dirty;
        add.GetComponentInChildren<TMP_Text>().text = commands.AddLabel;
        rebuild.GetComponentInChildren<TMP_Text>().text = commands.RebuildLabel;
        varyShipWakes.gameObject.SetActive(commands.VaryShipWakes != null);
        gameObject.SetActive(true);
        Layout();
        Present(changedOutput);
        if (opening)
            draftButton.Select();
    }

    /// <summary>
    /// Connects authored controls to the draft commands once for the lifetime of the workspace.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        bound = true;
        back.onClick.AddListener(() => TryCancel());
        save.onClick.AddListener(() => actions?.Save());
        undo.onClick.AddListener(() => actions?.Undo?.Invoke());
        redo.onClick.AddListener(() => actions?.Redo?.Invoke());
        draftButton.onClick.AddListener(() => Compare(false));
        currentButton.onClick.AddListener(() => Compare(true));
        fit.onClick.AddListener(artworkCanvas.Fit);
        actual.onClick.AddListener(artworkCanvas.ActualPixels);
        add.onClick.AddListener(() => actions?.Add());
        importLayer.onClick.AddListener(() => actions?.Import?.Invoke());
        duplicate.onClick.AddListener(() => actions?.Duplicate());
        remove.onClick.AddListener(() => actions?.Remove());
        forward.onClick.AddListener(() => MoveOrder(1));
        backward.onClick.AddListener(() => MoveOrder(-1));
        rebuild.onClick.AddListener(() => actions?.Rebuild());
        varyShipWakes.onClick.AddListener(() => actions?.VaryShipWakes?.Invoke());
        xInput.onEndEdit.AddListener(value => Position(value, true));
        yInput.onEndEdit.AddListener(value => Position(value, false));
        decreaseScale.onClick.AddListener(() => ChangeScale(-10));
        increaseScale.onClick.AddListener(() => ChangeScale(10));
        for (int i = 0; i < outputButtons.Length; i++)
        {
            int index = i;
            outputButtons[i].onClick.AddListener(() => SelectOutput(index));
        }
        artworkCanvas.LayerSelected += SelectLayer;
        artworkCanvas.LayerMoved += (index, offset) => actions?.Move(index, offset);
        artworkCanvas.ViewChanged += UpdateCanvasHint;
    }

    /// <summary>
    /// Switches to the independent layer stack for an output and resets the view.
    /// </summary>
    /// <param name="index">The selected output tab.</param>
    private void SelectOutput(int index)
    {
        if (outputIndex == index)
            return;
        outputIndex = index;
        selectedIndex = actions.OutputChanged?.Invoke(index) ?? selectedIndex;
        Layout();
        Present(true);
    }

    /// <summary>
    /// Switches the canvas between the draft and the art currently assigned to the pack.
    /// </summary>
    /// <param name="showCurrent">Whether to show the assigned artwork.</param>
    private void Compare(bool showCurrent)
    {
        if (showCurrent == comparing)
            return;
        if (showCurrent && (current ??= actions.ReadCurrent()) == null)
            return;
        comparing = showCurrent;
        Present(false);
    }

    /// <summary>
    /// Updates the displayed frame and keeps the layer list synchronized with the canvas.
    /// </summary>
    /// <param name="resetView">Whether the new frame should initially fit the canvas.</param>
    private void Present(bool resetView)
    {
        var selected = frames[outputIndex];
        var shown = comparing ? current[outputIndex] : selected;
        for (int i = 0; i < outputButtons.Length; i++)
        {
            Button button = outputButtons[i];
            button.gameObject.SetActive(i < frames.Count);
            if (i >= frames.Count)
                continue;
            var frame = frames[i];
            button.GetComponentInChildren<TMP_Text>().text =
                $"{frame.Caption}   {frame.Size.x} × {frame.Size.y}";
            SetSelected(button, i == outputIndex);
        }
        SetSelected(draftButton, !comparing);
        SetSelected(currentButton, comparing);
        selectedLayer = null;
        ArtworkEffectLayerPreview? firstEditable = null;
        foreach (var layer in selected.Layers)
        {
            if (!layer.Linked)
                firstEditable ??= layer;
            if (layer.Index == selectedIndex)
                selectedLayer = layer;
        }
        if (selectedLayer == null && !comparing)
        {
            selectedLayer = firstEditable;
            int availableIndex = selectedLayer?.Index ?? -1;
            if (selectedIndex != availableIndex)
                actions.Select?.Invoke(availableIndex);
            selectedIndex = availableIndex;
        }
        artworkCanvas.Show(shown, selectedIndex, !comparing, resetView);
        RenderLayers(selected);
        UpdateInspector();
    }

    /// <summary>
    /// Selects a row directly, including layers that are hidden or fully overlapped.
    /// </summary>
    /// <param name="index">The selected layer's current draft index.</param>
    private void SelectLayer(int index)
    {
        selectedIndex = index;
        actions?.Select?.Invoke(index);
        Present(false);
    }

    /// <summary>
    /// Rebinds the visible stack in top-to-bottom draw order while keeping its scroll position.
    /// </summary>
    /// <param name="frame">The active draft output.</param>
    private void RenderLayers(ArtworkEffectPreviewFrame frame)
    {
        for (int i = 0; i < frame.Layers.Count; i++)
        {
            if (i == rows.Count)
                rows.Add(Instantiate(rowTemplate, layerScroll.ScrollArea.ContentRoot));
            ArtworkEffectLayerPreview layer = frame.Layers[frame.Layers.Count - i - 1];
            var row = rows[i];
            row.gameObject.SetActive(true);
            var rect = (RectTransform)row.transform;
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0, -i * 51 - 46);
            rect.offsetMax = new Vector2(-2, -i * 51);
            row.Render(
                layer,
                layer.Index == selectedIndex,
                !comparing,
                SelectLayer,
                actions.Toggle
            );
        }
        for (int i = frame.Layers.Count; i < rows.Count; i++)
            rows[i].gameObject.SetActive(false);
        layerScroll.SetContentHeight(frame.Layers.Count * 51);
        for (int i = 0; i < frame.Layers.Count; i++)
            if (frame.Layers[i].Index == selectedIndex)
                layerScroll.ScrollArea.RevealContentRect((frame.Layers.Count - i - 1) * 51, 46);
    }

    /// <summary>
    /// Shows the position and available commands for the selected layer at this output size.
    /// </summary>
    private void UpdateInspector()
    {
        Vector2Int size = frames[outputIndex].Size;
        Vector2 offset = selectedLayer?.Offset ?? Vector2.zero;
        xInput.SetTextWithoutNotify(
            selectedLayer.HasValue ? Mathf.RoundToInt(offset.x * size.x).ToString() : string.Empty
        );
        yInput.SetTextWithoutNotify(
            selectedLayer.HasValue ? Mathf.RoundToInt(-offset.y * size.y).ToString() : string.Empty
        );
        bool canEdit = !comparing && selectedLayer.HasValue;
        bool canEditPixels = canEdit && !selectedLayer.Value.Linked;
        xInput.interactable = yInput.interactable = canEditPixels;
        duplicate.interactable = remove.interactable = canEditPixels;
        forward.interactable = canEdit && GetReorderTarget(1) >= 0;
        backward.interactable = canEdit && GetReorderTarget(-1) >= 0;
        add.interactable = rebuild.interactable = varyShipWakes.interactable = !comparing;
        importLayer.interactable = !comparing && actions.Import != null;
        scope.text = selectedLayer?.Linked == true ? "Linked source" : "Pixels";
        int? percent = selectedLayer?.ScalePercent;
        scaleControls.gameObject.SetActive(
            canEditPixels && percent.HasValue && actions.Resize != null
        );
        scaleValue.text = percent.HasValue ? $"{percent}%" : string.Empty;
        decreaseScale.interactable = percent > ContentEditorArtworkLayer.MinimumScalePercent;
        increaseScale.interactable = percent < ContentEditorArtworkLayer.MaximumScalePercent;
        UpdateCanvasHint();
    }

    /// <summary>
    /// Resizes the selected effect about its center while keeping its authored position.
    /// </summary>
    /// <param name="delta">The increase or decrease in percentage points.</param>
    private void ChangeScale(int delta)
    {
        if (comparing || selectedLayer?.ScalePercent is not int percent)
            return;
        actions?.Resize?.Invoke(
            selectedIndex,
            Mathf.Clamp(
                percent + delta,
                ContentEditorArtworkLayer.MinimumScalePercent,
                ContentEditorArtworkLayer.MaximumScalePercent
            )
        );
    }

    /// <summary>
    /// Edits the selected layer's pixel offset in the active output's coordinate system.
    /// </summary>
    /// <param name="value">The entered pixel value.</param>
    /// <param name="horizontal">Whether the first axis was edited.</param>
    private void Position(string value, bool horizontal)
    {
        if (actions == null || !selectedLayer.HasValue || selectedLayer.Value.Linked || comparing)
            return;
        if (!int.TryParse(value, out int pixels))
        {
            UpdateInspector();
            return;
        }
        Vector2Int size = frames[outputIndex].Size;
        Vector2 position = selectedLayer.Value.Offset;
        Vector2 target = horizontal
            ? new Vector2((float)pixels / size.x, position.y)
            : new Vector2(position.x, -(float)pixels / size.y);
        actions.Move(selectedIndex, target - position);
    }

    /// <summary>
    /// Changes the selected layer's position in the stack without moving its artwork.
    /// </summary>
    /// <param name="direction">The relative draw-order movement.</param>
    private void MoveOrder(int direction)
    {
        if (comparing || !selectedLayer.HasValue)
            return;
        int target = GetReorderTarget(direction);
        if (target >= 0)
            actions.Reorder(selectedIndex, target);
    }

    /// <summary>
    /// Finds the adjacent editable layer within the selected output.
    /// </summary>
    /// <param name="direction">The relative draw-order movement.</param>
    /// <returns>The adjacent layer's draft index, or -1 at the end of the visible stack.</returns>
    private int GetReorderTarget(int direction)
    {
        var layers = frames[outputIndex].Layers;
        for (int index = 0; index < layers.Count; index++)
        {
            if (layers[index].Index != selectedIndex)
                continue;
            int neighbor = index + direction;
            if ((uint)neighbor < (uint)layers.Count)
                return layers[neighbor].Index;
            break;
        }
        return -1;
    }

    /// <summary>
    /// Shows the canvas scale and the relevant interaction or missing-art state.
    /// </summary>
    private void UpdateCanvasHint()
    {
        if (frames == null)
            return;
        string hint =
            comparing && current[outputIndex].IsMissing ? "No artwork assigned"
            : comparing ? "Current in pack"
            : selectedLayer?.Hidden == true ? "Selected layer is hidden"
            : selectedLayer is { Linked: false } ? "Drag layer · Space + drag to pan"
            : "Space + drag to pan";
        canvasHint.text = $"{artworkCanvas.ZoomPercent}%   ·   {hint}";
        SetSelected(fit, artworkCanvas.IsFitted);
        SetSelected(actual, !artworkCanvas.IsFitted && artworkCanvas.ZoomPercent == 100);
    }

    /// <summary>
    /// Highlights a direct output or preview mode using the current editor palette.
    /// </summary>
    /// <param name="button">The stateful control.</param>
    /// <param name="on">Whether this is the active choice.</param>
    internal static void SetSelected(Button button, bool on)
    {
        var binding = button.targetGraphic.GetComponent<ContentEditorColorBinding>();
        binding.SetRole(on ? ContentEditorColorRole.Selection : ContentEditorColorRole.Background);
    }

    /// <summary>
    /// Reflows the canvas and inspector when the native workspace resizes.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnRectTransformDimensionsChange()
    {
        if (frames != null && panel != null)
            Layout();
    }

    /// <summary>
    /// Lays out the full artwork workspace, docking the inspector under the canvas in narrow windows.
    /// </summary>
    private void Layout()
    {
        float width = panel.rect.width;
        float height = panel.rect.height;
        bool narrow = width < 790;
        float headerHeight = width < 760 ? 104 : 74;
        float outputHeight = width < 740 ? 88 : 50;
        float top = headerHeight + outputHeight;
        float room = Mathf.Max(140, height - top);
        float inspector = Mathf.Clamp(width * 0.27f, 300, 410);
        float canvasWidth = narrow ? width : width - inspector;
        float canvasHeight = narrow ? Mathf.Max(140, room * 0.44f) : room;
        Place(header, 0, 0, width, headerHeight);
        Place(outputBar, 0, headerHeight, width, outputHeight);
        Place((RectTransform)artworkCanvas.transform, 0, top, canvasWidth, canvasHeight - 46);
        Place(canvasBar, 0, top + canvasHeight - 46, canvasWidth, 46);
        Place(
            sidebar,
            narrow ? 0 : canvasWidth,
            narrow ? top + canvasHeight : top,
            narrow ? width : inspector,
            narrow ? Mathf.Max(190, room - canvasHeight) : room
        );

        Place(back.transform, 16, 18, 76, 32);
        Place(title.transform, 108, 8, Mathf.Max(120, width - (headerHeight > 74 ? 126 : 520)), 34);
        Place(
            detail.transform,
            109,
            41,
            Mathf.Max(120, width - (headerHeight > 74 ? 127 : 522)),
            22
        );
        float actionY = headerHeight > 74 ? 68 : 20;
        Place(save.transform, width - 148, actionY, 132, 34);
        Place(redo.transform, width - 219, actionY, 64, 34);
        Place(undo.transform, width - 290, actionY, 64, 34);
        Place(status.transform, Mathf.Max(12, width - 440), actionY, 138, 34);
        float compareY = outputHeight > 50 ? 49 : 8;
        Place(currentButton.transform, width - 144, compareY, 128, 34);
        Place(draftButton.transform, width - 218, compareY, 66, 34);
        float tabWidth = Mathf.Min(190, (outputHeight > 50 ? width - 40 : width - 260) / 3f);
        for (int i = 0; i < outputButtons.Length; i++)
            Place(outputButtons[i].transform, 16 + i * (tabWidth + 6), 8, tabWidth, 34);

        Place(fit.transform, 12, 7, 56, 32);
        Place(actual.transform, 75, 7, 62, 32);
        Place(canvasHint.transform, 150, 5, canvasWidth - 164, 36);

        float sideWidth = sidebar.rect.width;
        float sideHeight = sidebar.rect.height;
        bool hasVariation = varyShipWakes.gameObject.activeSelf;
        float footer =
            narrow ? 121
            : hasVariation ? 224
            : 183;
        int layerCount = frames[outputIndex].Layers.Count;
        float listHeight = Mathf.Min(
            Mathf.Max(51, layerCount * 51),
            Mathf.Max(51, Mathf.Min(narrow ? 154 : 260, sideHeight - footer - 56))
        );
        Place(layerCaption.transform, 16, 9, sideWidth - 246, 32);
        Place(add.transform, sideWidth - 224, 9, 108, 32);
        Place(importLayer.transform, sideWidth - 110, 9, 94, 32);
        Place(layerScroll.transform, 16, 50, sideWidth - 32, listHeight);
        float controlsY = 56 + listHeight;
        Place(layerActions, 0, controlsY, sideWidth, 37);
        float actionWidth = (sideWidth - 48) / 4f;
        var layerButtons = new[] { duplicate, remove, forward, backward };
        for (int i = 0; i < layerButtons.Length; i++)
            Place(layerButtons[i].transform, 16 + i * (actionWidth + 5), 0, actionWidth, 32);
        float positionY = controlsY + 48;
        float transformWidth = narrow ? sideWidth * 0.49f - 20 : sideWidth - 32;
        float scaleWidth = Mathf.Min(112, transformWidth * 0.43f);
        float positionWidth =
            actions.Resize == null ? transformWidth : transformWidth - scaleWidth - 12;
        Place(positionCaption.transform, 16, positionY, positionWidth * 0.52f, 23);
        Place(scope.transform, 16 + positionWidth * 0.52f, positionY, positionWidth * 0.48f, 23);
        float fieldWidth = positionWidth * 0.5f - 27;
        Place(xCaption.transform, 16, positionY + 27, 18, 32);
        Place(xInput.transform, 36, positionY + 27, fieldWidth, 32);
        Place(yCaption.transform, 25 + positionWidth * 0.5f, positionY + 27, 18, 32);
        Place(yInput.transform, 45 + positionWidth * 0.5f, positionY + 27, fieldWidth, 32);
        Place(scaleControls, 16 + positionWidth + 12, positionY, scaleWidth, 59);
        Place(scaleCaption.transform, 0, 0, scaleWidth, 23);
        const int valueWidth = 44;
        float stepWidth = (scaleWidth - valueWidth) * 0.5f;
        Place(decreaseScale.transform, 0, 27, stepWidth, 32);
        Place(scaleValue.transform, stepWidth, 27, valueWidth, 32);
        Place(increaseScale.transform, stepWidth + valueWidth, 27, stepWidth, 32);
        Place(
            rebuild.transform,
            narrow ? sideWidth * 0.5f + 8 : 16,
            narrow ? positionY + 27 : sideHeight - 51,
            narrow ? sideWidth * 0.5f - 24 : sideWidth - 32,
            34
        );
        if (hasVariation)
            Place(
                varyShipWakes.transform,
                narrow ? sideWidth * 0.5f + 8 : 16,
                narrow ? positionY - 13 : sideHeight - 92,
                narrow ? sideWidth * 0.5f - 24 : sideWidth - 32,
                34
            );
    }

    /// <summary>
    /// Places an authored workspace control in top-left canvas units.
    /// </summary>
    /// <param name="target">The authored control.</param>
    /// <param name="x">The left coordinate.</param>
    /// <param name="y">The top coordinate.</param>
    /// <param name="width">The allocated width.</param>
    /// <param name="height">The allocated height.</param>
    private static void Place(Transform target, float x, float y, float width, float height) =>
        UILayout.SetSourceRect(
            (RectTransform)target,
            Mathf.RoundToInt(x),
            Mathf.RoundToInt(y),
            Mathf.Max(1, Mathf.RoundToInt(width)),
            Mathf.Max(1, Mathf.RoundToInt(height))
        );

    /// <summary>
    /// Routes keyboard commands to the workspace while preserving native text editing.
    /// </summary>
    /// <param name="key">The native key event.</param>
    /// <returns>Whether a workspace action consumed the event.</returns>
    internal bool HandleKeyboardShortcut(Event key)
    {
        if (actions == null || key?.type != EventType.KeyDown || key.alt)
            return false;
        bool command = key.control || key.command;
        if (command && key.keyCode == KeyCode.S && dirty)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            actions.Save();
            key.Use();
            return true;
        }
        if (xInput.isFocused || yInput.isFocused)
            return false;
        Action action = key.keyCode switch
        {
            KeyCode.Z when command && key.shift => actions.Redo,
            KeyCode.Z when command => actions.Undo,
            KeyCode.Y when command => actions.Redo,
            KeyCode.D when command && duplicate.interactable => actions.Duplicate,
            KeyCode.Delete or KeyCode.Backspace when remove.interactable => actions.Remove,
            KeyCode.F when !command => artworkCanvas.Fit,
            _ => null,
        };
        if (action == null && !command && xInput.interactable)
        {
            Vector2 direction = key.keyCode switch
            {
                KeyCode.LeftArrow => Vector2.left,
                KeyCode.RightArrow => Vector2.right,
                KeyCode.UpArrow => Vector2.up,
                KeyCode.DownArrow => Vector2.down,
                _ => Vector2.zero,
            };
            if (direction != Vector2.zero)
                action = () =>
                    actions.Move(
                        selectedIndex,
                        direction * (key.shift ? 10f : 1f) / (Vector2)frames[outputIndex].Size
                    );
        }
        if (action == null)
            return false;
        action();
        key.Use();
        return true;
    }

    /// <summary>
    /// Returns to the unit page, guarding any unsaved draft.
    /// </summary>
    /// <returns>Whether this workspace was open.</returns>
    public bool TryCancel()
    {
        if (!gameObject.activeSelf)
            return false;
        if (!ConfirmPending())
            Dismiss();
        return true;
    }

    /// <summary>
    /// Resolves unsaved artwork before returning to the unit or closing the application.
    /// </summary>
    /// <param name="continuation">The transition after a successful save or discard.</param>
    /// <returns>Whether a decision is required before the transition.</returns>
    internal bool ConfirmPending(Action continuation = null)
    {
        if (!HasUnsavedChanges)
            return false;
        confirmClose(
            () =>
            {
                actions?.Save();
                if (!gameObject.activeSelf)
                    continuation?.Invoke();
            },
            () =>
            {
                Dismiss();
                continuation?.Invoke();
            }
        );
        return true;
    }

    /// <summary>
    /// Detaches the resolved draft before returning to the artwork page.
    /// </summary>
    internal void Dismiss()
    {
        if (actions == null)
            return;
        var onClose = actions.Close;
        actions = null;
        frames = current = null;
        dirty = false;
        artworkCanvas.Clear();
        foreach (var row in rows)
            row.Clear();
        gameObject.SetActive(false);
        onClose?.Invoke();
    }

    /// <summary>
    /// Releases draft resources if the editor closes while artwork is open.
    /// </summary>
    private void OnDestroy()
    {
        if (actions != null)
            Dismiss();
    }
}

/// <summary>
/// Supplies the edit operations and baseline artwork owned by the current draft.
/// </summary>
internal sealed class ArtworkEffectActions
{
    internal Action<int> Select;
    internal Func<int, int> OutputChanged;
    internal Action<int, Vector2> Move;
    internal Action<int, int> Resize;
    internal Action<int> Toggle;
    internal Action<int, int> Reorder;
    internal Action Add;
    internal Action Import;
    internal Action<string> ImportDroppedImage;
    internal Action Duplicate;
    internal Action Remove;
    internal Action Rebuild;
    internal Action VaryShipWakes;
    internal Action Save;
    internal Action Undo;
    internal Action Redo;
    internal Action Close;
    internal Func<IReadOnlyList<ArtworkEffectPreviewFrame>> ReadCurrent;
    internal string AddLabel;
    internal string RebuildLabel;
}
