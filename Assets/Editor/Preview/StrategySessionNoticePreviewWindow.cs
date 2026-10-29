using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.App;
using Rebellion.App.PlayerActions;
using Rebellion.Game;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Previews session notices with the production strategy views and each installed faction theme.
/// </summary>
public sealed class StrategySessionNoticePreviewWindow : EditorWindow
{
    private static readonly string[] _factions = { "Alliance", "Empire" };
    private int _factionIndex;
    private string _message = "Synchronizing the game";
    private Texture2D _screenshot;

    /// <summary>
    /// Opens the native strategy notice placement review.
    /// </summary>
    [MenuItem("Rebellion/Preview/Strategy session notice")]
    public static void Open()
    {
        StrategySessionNoticePreviewWindow window = GetWindow<StrategySessionNoticePreviewWindow>();
        window.titleContent = new GUIContent("Session notice placement");
        window.minSize = new Vector2(960, 600);
        window.Show();
    }

    /// <summary>
    /// Selects the faction and _message displayed by the production strategy controls.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        _factionIndex = GUILayout.Toolbar(_factionIndex, _factions);
        _message = EditorGUILayout.TextField("Status", _message);
        if ((EditorGUI.EndChangeCheck() || _screenshot == null) && !EditorApplication.isCompiling)
        {
            if (_screenshot != null)
                DestroyImmediate(_screenshot);
            _screenshot = Render(_factions[_factionIndex], _message);
        }
        Rect available = GUILayoutUtility.GetRect(
            100,
            10000,
            100,
            10000,
            GUILayout.ExpandHeight(true)
        );
        if (_screenshot != null)
            GUI.DrawTexture(available, _screenshot, ScaleMode.ScaleToFit);
    }

    /// <summary>
    /// Releases the native render owned by this preview window.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnDisable()
    {
        if (_screenshot != null)
            DestroyImmediate(_screenshot);
    }

    /// <summary>
    /// Rebuilds the strategy root and exports both faction views through the native Unity renderer.
    /// </summary>
    public static void ExportAll()
    {
        StrategyViewPrefabBuilder.BuildStrategyViewRootPrefab();
        string directory = Path.Combine(Path.GetTempPath(), "rebellion-strategy-notice-preview");
        Directory.CreateDirectory(directory);
        foreach (string faction in _factions)
        {
            Texture2D result = Render(faction, "Synchronizing the game");
            try
            {
                File.WriteAllBytes(Path.Combine(directory, faction + ".png"), result.EncodeToPNG());
            }
            finally
            {
                DestroyImmediate(result);
            }
        }
        Debug.Log("Strategy notice previews exported to " + directory);
    }

    /// <summary>
    /// Renders one themed production canvas with populated portrait rows and a visible session notice.
    /// </summary>
    /// <param name="faction">The installed faction whose strategy theme is displayed.</param>
    /// <param name="noticeText">The status text to place inside the galaxy play area.</param>
    /// <returns>The caller-owned native _screenshot.</returns>
    private static Texture2D Render(string faction, string noticeText)
    {
        GameDataCatalog data = ContentPackEditor.LoadGameData();
        FactionTheme theme = data.FactionThemes.Single(item =>
            item.FactionInstanceID == (faction == "Alliance" ? "FNALL1" : "FNEMP1")
        );
        PreviewRenderUtility preview = new PreviewRenderUtility();
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab"
            );
            GameObject root = Instantiate(prefab.transform.Find("UI/Canvas").gameObject);
            preview.AddSingleGO(root);
            ContentBindings.Apply(root, ContentPackEditor.Assets);
            root.GetComponentInChildren<StrategyController>(true).enabled = false;
            root.GetComponentInChildren<GameFlowController>(true).enabled = false;
            StrategyHudView hud = root.GetComponentInChildren<StrategyHudView>(true);
            StrategyHudController controller = new StrategyHudController(
                () => null,
                () => theme,
                ContentPackEditor.Assets.GetTexture,
                _ => { },
                new PreviewActions()
            );
            hud.Render(
                controller.CreateViewData(
                    new StrategyHudRenderData(
                        "0",
                        "575",
                        "613",
                        TickSpeed.Paused,
                        Array.Empty<Rebellion.Game.Messages.MessageType>(),
                        connectionNotice: noticeText
                    ),
                    theme
                )
            );
            hud.AdvisorView.gameObject.SetActive(false);
            Texture2D galaxy = ContentPackEditor.Assets.GetTexture(
                theme.GalaxyBackground.ImagePath
            );
            root.GetComponentInChildren<GalaxyMapView>(true)
                .Render(
                    new GalaxyMapRenderData(
                        galaxy,
                        new RectInt(
                            theme.GalaxyBackground.SourcePosition.X,
                            theme.GalaxyBackground.SourcePosition.Y,
                            UILayout.ToSourceUnits(galaxy.width),
                            UILayout.ToSourceUnits(galaxy.height)
                        ),
                        Color.white,
                        default,
                        Array.Empty<GalaxyMapClusterRenderData>()
                    )
                );
            root.GetComponentInChildren<BookmarkBarView>(true)
                .Render(Array.Empty<BookmarkRenderData>(), theme.StrategyBookmarkLayout);
            SourceRectLayout area = theme.StrategyWindowPlacements.WindowBounds;
            IdleBarView idle = root.GetComponentInChildren<IdleBarView>(true);
            idle.Render(
                new IdleBarRenderData(
                    true,
                    data.Officers.Where(officer =>
                            officer.DisplayImagePath?.StartsWith(
                                "Pack/Factions/" + faction + "/",
                                StringComparison.Ordinal
                            ) == true
                        )
                        .Take(14)
                        .Select(officer => new IdleBarEntry(
                            officer,
                            ContentPackEditor.Assets.GetTexture(officer.DisplayImagePath)
                        ))
                        .ToArray(),
                    new RectInt(area.X, area.Y, area.Width, area.Height),
                    true
                )
            );
            Camera camera = preview.camera;
            camera.cameraType = CameraType.Game;
            camera.orthographic = true;
            camera.orthographicSize = 240;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.transform.rotation = Quaternion.identity;
            HdrpRenderTargetBuilder.ConfigureCamera(camera);
            preview.BeginStaticPreview(new Rect(0, 0, 1920, 1080));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            Canvas.ForceUpdateCanvases();
            preview.Render(true);
            return preview.EndStaticPreview();
        }
        finally
        {
            preview.Cleanup();
        }
    }

    /// <summary>
    /// Keeps the isolated editor review from issuing gameplay commands.
    /// </summary>
    private sealed class PreviewActions : IPlayerActionDispatcher
    {
        /// <summary>
        /// Rejects interaction in the presentation-only preview.
        /// </summary>
        /// <param name="action">The unused gameplay request.</param>
        /// <returns>A completed rejection.</returns>
        public Task<PlayerActionResult> ExecuteAsync(PlayerAction action) =>
            Task.FromResult(new PlayerActionResult(false, "Preview"));
    }
}
