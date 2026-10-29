using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fits an animated control to the cockpit with draggable surface corners.
/// </summary>
public sealed class MainMenuPlacementWindow : EditorWindow
{
    [SerializeField]
    private Rect _draft;

    [SerializeField]
    private float _draftRotation;

    [SerializeField]
    private UIQuadrilateral _draftCorners = UIQuadrilateral.Unit;

    [SerializeField]
    private bool _locked;

    [SerializeField]
    private bool _showCorners = true;

    private bool _dragging;
    private int _draggedCorner = -1;
    private Vector2 _dragOffset;
    private double _nextRepaint;
    private PerspectiveImage _previewImage;
    private Mesh _previewMesh;
    private Material _previewMaterial;
    private RenderTexture _previewTexture;

    /// <summary>
    /// Opens the native placement tool without discarding an existing draft.
    /// </summary>
    [MenuItem("Rebellion/Preview/Place multiplayer button")]
    public static void Open()
    {
        MainMenuPlacementWindow window = GetWindow<MainMenuPlacementWindow>(
            "Multiplayer button placement"
        );
        if (window._draft.width <= 0)
        {
            window.RestorePlacement();
            window.position = new Rect(100, 100, 1280, 850);
        }
        window._locked = false;
        window.minSize = new Vector2(900, 600);
        window.Show();
        window.Focus();
    }

    /// <summary>
    /// Refreshes the animated preview at its authored cadence.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void Update()
    {
        if (EditorApplication.timeSinceStartup < _nextRepaint)
            return;
        _nextRepaint = EditorApplication.timeSinceStartup + 0.066;
        Repaint();
    }

    /// <summary>
    /// Releases the temporary native graphic, material, and mesh.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnDisable()
    {
        if (_previewImage != null)
            DestroyImmediate(_previewImage.gameObject);
        if (_previewMesh != null)
            DestroyImmediate(_previewMesh);
        if (_previewMaterial != null)
            DestroyImmediate(_previewMaterial);
        if (_previewTexture != null)
        {
            _previewTexture.Release();
            DestroyImmediate(_previewTexture);
        }
    }

    /// <summary>
    /// Draws the real artwork, projected animation, and authoring controls.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Multiplayer button placement", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            _locked
                ? "Placement saved. Unlock to adjust it again."
                : "Drag each corner dot to fit the screen. Drag inside to move the whole button.",
            MessageType.Info
        );
        Texture2D background = ContentPackEditor.Assets.GetTexture(
            "Application/MainMenu/UI/ui_mainmenu_background"
        );
        Rect area = GUILayoutUtility.GetRect(100, 10000, 100, 10000, GUILayout.ExpandHeight(true));
        float width = Mathf.Min(area.width, area.height * 16f / 9f);
        Vector2 size = new Vector2(width, width * 9f / 16f);
        Rect canvas = new Rect(area.center - size / 2, size);
        GUI.DrawTexture(canvas, background, ScaleMode.StretchToFill);
        DrawButton(canvas);
        HandleDrag(canvas);
        using (new EditorGUI.DisabledScope(_locked))
        {
            EditorGUI.BeginChangeCheck();
            float scale = EditorGUILayout.Slider(
                "Size (%)",
                _draft.width / MainMenuLayoutSettings.DefaultMultiplayer.width * 100,
                1,
                100
                    / Mathf.Max(
                        MainMenuLayoutSettings.DefaultMultiplayer.width,
                        MainMenuLayoutSettings.DefaultMultiplayer.height
                    )
            );
            if (EditorGUI.EndChangeCheck())
                Resize(scale);
            _draftRotation = EditorGUILayout.Slider(
                "Rotation (degrees)",
                _draftRotation,
                -180,
                180
            );
            _showCorners = EditorGUILayout.Toggle("Show corner handles", _showCorners);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(_locked ? "Unlock" : "Lock in", GUILayout.Height(32)))
            {
                if (_locked)
                    _locked = false;
                else
                    LockPlacement();
            }
            if (GUILayout.Button("Revert", GUILayout.Height(32)))
                RestorePlacement();
        }
    }

    /// <summary>
    /// Draws the production graphic mesh and optional corner dots without a surrounding box.
    /// </summary>
    /// <param name="canvas">The artwork rectangle in GUI coordinates.</param>
    private void DrawButton(Rect canvas)
    {
        if (Event.current.type != EventType.Repaint)
            return;
        if (_previewImage == null)
        {
            GameObject preview = new GameObject(
                "Placement preview",
                typeof(RectTransform),
                typeof(PerspectiveImage)
            )
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            _previewImage = preview.GetComponent<PerspectiveImage>();
            _previewImage.rectTransform.pivot = Vector2.zero;
            _previewImage.rectTransform.sizeDelta = Vector2.one;
            _previewMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            _previewMaterial = new Material(
                AssetDatabase.LoadAssetAtPath<Shader>(
                    "Assets/Editor/Preview/PlacementPreview.shader"
                )
            )
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
        int frame = (int)(EditorApplication.timeSinceStartup / 0.066) % 30 + 1;
        _previewImage.sprite = ContentPackEditor.Assets.GetSprite(
            "Application/MainMenu/UI/ui_mainmenu_multiplayer_" + frame.ToString("00")
        );
        _previewImage.Configure(_draftCorners, SurfaceCorners());
        _previewImage.WriteMesh(_previewMesh);
        _previewMaterial.mainTexture = _previewImage.mainTexture;
        Rect button = ButtonBounds(canvas);
        RenderButtonTexture(button.size);
        Matrix4x4 guiMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(-_draftRotation, button.center);
        GUI.DrawTexture(button, _previewTexture, ScaleMode.StretchToFill);
        GUI.matrix = guiMatrix;
        if (_showCorners && !_locked)
        {
            Color handleColor = Handles.color;
            Handles.color = Color.cyan;
            UIQuadrilateral surface = SurfaceCorners();
            for (int index = 0; index < 4; index++)
                Handles.DrawSolidDisc(ToGui(canvas, surface[index]), Vector3.forward, 4);
            Handles.color = handleColor;
        }
    }

    /// <summary>
    /// Renders straight-alpha colour into an sRGB target for the GUI texture path.
    /// </summary>
    /// <param name="size">The preview dimensions in GUI points.</param>
    private void RenderButtonTexture(Vector2 size)
    {
        int width = Mathf.Max(1, Mathf.CeilToInt(size.x * EditorGUIUtility.pixelsPerPoint));
        int height = Mathf.Max(1, Mathf.CeilToInt(size.y * EditorGUIUtility.pixelsPerPoint));
        if (
            _previewTexture == null
            || _previewTexture.width != width
            || _previewTexture.height != height
        )
        {
            if (_previewTexture != null)
            {
                _previewTexture.Release();
                DestroyImmediate(_previewTexture);
            }
            _previewTexture = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            )
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
            };
            _previewTexture.Create();
        }
        RenderTexture previousTarget = RenderTexture.active;
        bool previousSrgbWrite = GL.sRGBWrite;
        GL.PushMatrix();
        try
        {
            RenderTexture.active = _previewTexture;
            GL.Clear(false, true, Color.clear);
            GL.LoadOrtho();
            _previewMaterial.SetPass(0);
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.DrawMeshNow(_previewMesh, Matrix4x4.identity);
        }
        finally
        {
            GL.PopMatrix();
            RenderTexture.active = previousTarget;
            GL.sRGBWrite = previousSrgbWrite;
        }
    }

    /// <summary>
    /// Handles corner fitting and whole-control movement in the same projected coordinates.
    /// </summary>
    /// <param name="canvas">The artwork rectangle in GUI coordinates.</param>
    private void HandleDrag(Rect canvas)
    {
        Event input = Event.current;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        UIQuadrilateral surface = SurfaceCorners();
        if (!_locked && input.type == EventType.MouseDown && input.button == 0)
        {
            _draggedCorner = -1;
            if (_showCorners)
                for (int index = 0; index < 4; index++)
                    if (Vector2.Distance(input.mousePosition, ToGui(canvas, surface[index])) <= 8)
                    {
                        _draggedCorner = index;
                        break;
                    }
            if (_draggedCorner >= 0 || surface.Contains(FromGui(canvas, input.mousePosition)))
            {
                _dragging = true;
                _dragOffset = input.mousePosition - ButtonBounds(canvas).center;
                GUIUtility.hotControl = control;
                input.Use();
            }
        }
        if (_dragging && input.type == EventType.MouseDrag)
        {
            if (_draggedCorner >= 0)
            {
                UIQuadrilateral changed = surface.WithCorner(
                    _draggedCorner,
                    FromGui(canvas, input.mousePosition)
                );
                if (changed.IsValid)
                {
                    Matrix4x4 mapping =
                        changed.ToMatrix()
                        * MainMenuLayoutSettings.MultiplayerSurface.ToMatrix().inverse;
                    UIQuadrilateral texture = UIQuadrilateral.Unit.Transform(mapping);
                    if (texture.IsValid)
                        FitCorners(texture);
                }
            }
            else
            {
                Vector2 center = input.mousePosition - _dragOffset;
                _draft.center = new Vector2(
                    (center.x - canvas.x) / canvas.width,
                    1 - (center.y - canvas.y) / canvas.height
                );
            }
            input.Use();
        }
        if (_dragging && input.type == EventType.MouseUp)
        {
            _dragging = false;
            _draggedCorner = -1;
            GUIUtility.hotControl = 0;
            input.Use();
        }
    }

    /// <summary>
    /// Encloses the projected texture while keeping its visible corners fixed in the cockpit.
    /// </summary>
    /// <param name="texture">The new full-texture quadrilateral in the current rectangle.</param>
    private void FitCorners(UIQuadrilateral texture)
    {
        Rect bounds = texture.Bounds;
        Vector2 delta = Vector2.Scale(bounds.center - Vector2.one / 2, _draft.size);
        Vector2 pixels = Vector2.Scale(delta, new Vector2(16, 9));
        pixels = Quaternion.Euler(0, 0, _draftRotation) * pixels;
        Vector2 center = _draft.center + new Vector2(pixels.x / 16, pixels.y / 9);
        _draft.size = Vector2.Scale(_draft.size, bounds.size);
        _draft.center = center;
        Matrix4x4 normalize =
            Matrix4x4.Scale(new Vector3(1 / bounds.width, 1 / bounds.height, 1))
            * Matrix4x4.Translate(-bounds.position);
        _draftCorners = texture.Transform(normalize);
    }

    /// <summary>
    /// Returns the calibrated visible surface after applying the current texture projection.
    /// </summary>
    /// <returns>The four visible corners in normalized image coordinates.</returns>
    private UIQuadrilateral SurfaceCorners()
    {
        return MainMenuLayoutSettings.MultiplayerSurface.Transform(_draftCorners.ToMatrix());
    }

    /// <summary>
    /// Returns the unrotated control rectangle in GUI coordinates.
    /// </summary>
    /// <param name="canvas">The artwork rectangle.</param>
    /// <returns>The control rectangle.</returns>
    private Rect ButtonBounds(Rect canvas)
    {
        return new Rect(
            canvas.x + _draft.x * canvas.width,
            canvas.y + (1 - _draft.yMax) * canvas.height,
            _draft.width * canvas.width,
            _draft.height * canvas.height
        );
    }

    /// <summary>
    /// Converts a normalized image point to a rotated GUI position.
    /// </summary>
    /// <param name="canvas">The artwork rectangle.</param>
    /// <param name="point">The normalized image point.</param>
    /// <returns>The GUI position.</returns>
    private Vector2 ToGui(Rect canvas, Vector2 point)
    {
        Rect button = ButtonBounds(canvas);
        Vector2 offset = Vector2.Scale(
            point - Vector2.one / 2,
            new Vector2(button.width, -button.height)
        );
        return button.center + (Vector2)(Quaternion.Euler(0, 0, -_draftRotation) * offset);
    }

    /// <summary>
    /// Converts a GUI pointer position into unrotated normalized image coordinates.
    /// </summary>
    /// <param name="canvas">The artwork rectangle.</param>
    /// <param name="point">The GUI pointer position.</param>
    /// <returns>The normalized image position.</returns>
    private Vector2 FromGui(Rect canvas, Vector2 point)
    {
        Rect button = ButtonBounds(canvas);
        Vector2 offset = Quaternion.Euler(0, 0, _draftRotation) * (point - button.center);
        return new Vector2(offset.x / button.width + 0.5f, 0.5f - offset.y / button.height);
    }

    /// <summary>
    /// Resizes the projected control around its center without changing its fitted shape.
    /// </summary>
    /// <param name="scale">The percentage of the default width.</param>
    private void Resize(float scale)
    {
        Vector2 center = _draft.center;
        float ratio = MainMenuLayoutSettings.DefaultMultiplayer.width * scale / 100 / _draft.width;
        _draft.size *= ratio;
        _draft.center = center;
    }

    /// <summary>
    /// Restores every saved transform value.
    /// </summary>
    private void RestorePlacement()
    {
        _draft = MainMenuLayoutSettings.instance.Multiplayer;
        _draftRotation = MainMenuLayoutSettings.instance.MultiplayerRotation;
        _draftCorners = MainMenuLayoutSettings.instance.MultiplayerCorners;
    }

    /// <summary>
    /// Saves the fitted placement and updates its prefab and any live main menu.
    /// </summary>
    private void LockPlacement()
    {
        MainMenuLayoutSettings.instance.SetMultiplayer(_draft, _draftRotation, _draftCorners);
        const string prefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ApplyPlacement(root.GetComponentInChildren<MainMenuView>(true));
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        if (EditorApplication.isPlaying)
            foreach (MainMenuView view in Object.FindObjectsByType<MainMenuView>())
                ApplyPlacement(view);
        _locked = true;
    }

    /// <summary>
    /// Updates the projected image owned by one prefab or live menu.
    /// </summary>
    /// <param name="view">The menu receiving the saved placement.</param>
    private void ApplyPlacement(MainMenuView view)
    {
        SerializedObject serialized = new SerializedObject(view);
        Button button = (Button)serialized.FindProperty("multiplayerButton").objectReferenceValue;
        MainMenuLayoutSettings.ApplyPlacement(
            (PerspectiveImage)button.targetGraphic,
            _draft,
            _draftRotation,
            _draftCorners
        );
    }
}
