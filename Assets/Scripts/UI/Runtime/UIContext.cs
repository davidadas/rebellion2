using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;
using UnityEngine;

/// <summary>
/// Provides strategy presentation state, theme lookup, and cached UI assets.
/// </summary>
public sealed class UIContext
{
    private GameRoot game;
    private readonly FactionThemeLibrary themeLibrary;
    private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private readonly HashSet<string> missingTextures = new HashSet<string>();

    /// <summary>
    /// Creates a presentation context for one active game.
    /// </summary>
    /// <param name="game">The active game.</param>
    /// <param name="themeLibrary">The faction-theme library.</param>
    /// <param name="encyclopediaCatalog">The Encyclopedia catalog.</param>
    public UIContext(
        GameRoot game,
        FactionThemeLibrary themeLibrary,
        EncyclopediaCatalog encyclopediaCatalog
    )
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        if (themeLibrary == null)
            throw new ArgumentNullException(nameof(themeLibrary));

        if (encyclopediaCatalog == null)
            throw new ArgumentNullException(nameof(encyclopediaCatalog));

        this.game = game;
        this.themeLibrary = themeLibrary;
        EncyclopediaCatalog = encyclopediaCatalog;
    }

    public GameRoot Game => game;

    /// <summary>
    /// Replaces the game used for strategy projection after a hot load.
    /// </summary>
    /// <param name="newGame">The replacement active game.</param>
    public void ReplaceGame(GameRoot newGame)
    {
        game = newGame ?? throw new ArgumentNullException(nameof(newGame));
    }

    public EncyclopediaCatalog EncyclopediaCatalog { get; }

    /// <summary>
    /// Gets the configured theme for a faction identifier.
    /// </summary>
    /// <param name="factionInstanceId">The faction identifier.</param>
    /// <returns>The matching theme or the configured default theme.</returns>
    public FactionTheme GetTheme(string factionInstanceId)
    {
        return themeLibrary.GetTheme(factionInstanceId);
    }

    /// <summary>
    /// Gets the current player faction's presentation theme.
    /// </summary>
    /// <returns>The current player faction theme.</returns>
    public FactionTheme GetPlayerFactionTheme()
    {
        return GetTheme(game.GetPlayerFaction().InstanceID);
    }

    /// <summary>
    /// Gets the current player faction identifier.
    /// </summary>
    /// <returns>The current player faction identifier.</returns>
    public string GetPlayerFactionInstanceID()
    {
        return game.GetPlayerFaction().InstanceID;
    }

    /// <summary>
    /// Resolves the configured display color for a faction.
    /// </summary>
    /// <param name="factionInstanceId">The faction identifier.</param>
    /// <returns>The configured faction color.</returns>
    public Color ResolveFactionColor(string factionInstanceId)
    {
        return GetTheme(factionInstanceId).GetPrimaryColor();
    }

    /// <summary>
    /// Resolves the primary sprite used to represent a scene node.
    /// </summary>
    /// <param name="node">The scene node to represent.</param>
    /// <returns>The resolved sprite.</returns>
    public Sprite GetSprite(ISceneNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        if (node is Fleet)
        {
            string ownerId = node.OwnerInstanceID;

            if (string.IsNullOrEmpty(ownerId))
                throw new InvalidOperationException("Fleet missing OwnerInstanceID.");

            FactionTheme theme = GetTheme(ownerId);

            string path = theme?.PlanetWindowTheme?.FleetsPane?.FleetsImagePath;

            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("Fleet-pane image path is missing.");

            return ResourceManager.GetSprite(path);
        }

        string nodePath = node.GetDisplayImagePath();

        if (!string.IsNullOrEmpty(nodePath))
            return ResourceManager.GetSprite(nodePath);

        throw new InvalidOperationException(
            $"No sprite mapping defined for '{node.GetDisplayName()}'."
        );
    }

    /// <summary>
    /// Resolves and caches a point-filtered texture by resource path.
    /// </summary>
    /// <param name="path">The resource path.</param>
    /// <returns>The resolved texture, or <see langword="null"/>.</returns>
    public Texture2D GetTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        if (textures.TryGetValue(path, out Texture2D texture))
            return texture;

        if (missingTextures.Contains(path))
            return null;

        texture = ResourceManager.TryGetTexture(path);
        if (texture == null)
        {
            Sprite sprite = ResourceManager.TryGetSprite(path);
            texture = sprite == null ? null : sprite.texture;
        }

        if (texture == null)
        {
            missingTextures.Add(path);
            return null;
        }

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        textures[path] = texture;
        return texture;
    }

    /// <summary>
    /// Resolves a scene node's regular or compact display texture.
    /// </summary>
    /// <param name="node">The scene node to represent.</param>
    /// <param name="small">Whether compact artwork is preferred.</param>
    /// <returns>The resolved display texture, or <see langword="null"/>.</returns>
    public Texture2D GetEntityTexture(ISceneNode node, bool small)
    {
        if (node == null)
            return null;

        string path = node.GetDisplayImagePath();
        if (string.IsNullOrEmpty(path))
            return null;

        if (small)
        {
            Texture2D configuredSmallTexture = GetTexture(node.SmallDisplayImagePath);
            if (configuredSmallTexture != null)
                return configuredSmallTexture;

            Texture2D smallTexture = GetTexture(GetSmallTexturePath(path));
            if (smallTexture != null)
                return smallTexture;
        }

        return GetTexture(path);
    }

    /// <summary>
    /// Resolves transient status artwork for a scene node.
    /// </summary>
    /// <param name="node">The scene node to inspect.</param>
    /// <param name="small">Whether compact artwork is preferred.</param>
    /// <returns>The resolved status texture, or <see langword="null"/>.</returns>
    public Texture2D GetEntityStatusTexture(ISceneNode node, bool small)
    {
        return GetTexture(GetEntityStatusImagePath(node, small));
    }

    /// <summary>
    /// Resolves the captured overlay for a captured officer.
    /// </summary>
    /// <param name="node">The scene node to inspect.</param>
    /// <returns>The captured overlay texture, or <see langword="null"/>.</returns>
    public Texture2D GetEntityCapturedOverlayTexture(ISceneNode node)
    {
        return node is Officer { IsCaptured: true }
            ? GetTexture(node.CapturedOverlayImagePath)
            : null;
    }

    /// <summary>
    /// Resolves a planet texture while honoring destroyed-planet presentation.
    /// </summary>
    /// <param name="planet">The planet to represent.</param>
    /// <param name="iconPath">The regular planet icon path.</param>
    /// <returns>The resolved planet texture, or <see langword="null"/>.</returns>
    public Texture2D GetPlanetTexture(Planet planet, string iconPath)
    {
        if (planet == null)
            return null;

        string path = planet.IsDestroyed
            ? GetPlayerFactionTheme()?.GalaxyBackground?.DestroyedPlanetIconPath
            : iconPath;

        return GetTexture(path);
    }

    /// <summary>
    /// Resolves a planet's configured display texture.
    /// </summary>
    /// <param name="planet">The planet to represent.</param>
    /// <returns>The resolved planet texture, or <see langword="null"/>.</returns>
    public Texture2D GetPlanetTexture(Planet planet)
    {
        return GetPlanetTexture(planet, planet?.GetPlanetIconPath());
    }

    /// <summary>
    /// Gets all configured non-default faction themes in load order.
    /// </summary>
    /// <returns>An isolated list of faction themes.</returns>
    public List<FactionTheme> GetAllThemes()
    {
        return themeLibrary.GetAllThemes();
    }

    /// <summary>
    /// Selects the configured transient status image path for a scene node.
    /// </summary>
    /// <param name="node">The scene node to inspect.</param>
    /// <param name="small">Whether compact artwork is preferred.</param>
    /// <returns>The selected resource path, or <see langword="null"/>.</returns>
    private static string GetEntityStatusImagePath(ISceneNode node, bool small)
    {
        if (node == null)
            return null;

        if (node is Officer { InjuryPoints: > 0 } && !string.IsNullOrEmpty(node.InjuredImagePath))
            return node.InjuredImagePath;

        if (
            node is IMovable movable
            && movable.GetTransitMovement() != null
            && !string.IsNullOrEmpty(
                SelectStatusPath(small, node.InTransitSmallImagePath, node.InTransitImagePath)
            )
        )
            return SelectStatusPath(small, node.InTransitSmallImagePath, node.InTransitImagePath);

        if (
            node is CapitalShip capitalShip
            && capitalShip.IsDamaged()
            && !string.IsNullOrEmpty(
                SelectStatusPath(small, node.DamagedSmallImagePath, node.DamagedImagePath)
            )
        )
            return SelectStatusPath(small, node.DamagedSmallImagePath, node.DamagedImagePath);

        if (
            node is Starfighter starfighter
            && starfighter.HasLosses()
            && !string.IsNullOrEmpty(
                SelectStatusPath(small, node.DamagedSmallImagePath, node.DamagedImagePath)
            )
        )
            return SelectStatusPath(small, node.DamagedSmallImagePath, node.DamagedImagePath);

        return null;
    }

    /// <summary>
    /// Selects a preferred resource path with a fallback.
    /// </summary>
    /// <param name="preferredPath">The preferred resource path.</param>
    /// <param name="fallbackPath">The fallback resource path.</param>
    /// <returns>The preferred non-empty path or the fallback.</returns>
    private static string SelectStatusPath(string preferredPath, string fallbackPath)
    {
        return !string.IsNullOrEmpty(preferredPath) ? preferredPath : fallbackPath;
    }

    /// <summary>
    /// Selects compact or regular status artwork according to presentation mode.
    /// </summary>
    /// <param name="small">Whether compact artwork is preferred.</param>
    /// <param name="smallPath">The compact resource path.</param>
    /// <param name="normalPath">The regular resource path.</param>
    /// <returns>The selected resource path.</returns>
    private static string SelectStatusPath(bool small, string smallPath, string normalPath)
    {
        return small ? SelectStatusPath(smallPath, normalPath) : normalPath;
    }

    /// <summary>
    /// Converts a primary display path to its compact-art naming convention.
    /// </summary>
    /// <param name="path">The primary display resource path.</param>
    /// <returns>The compact display resource path.</returns>
    private static string GetSmallTexturePath(string path)
    {
        const string primarySuffix = "_primary";
        return path.EndsWith(primarySuffix, StringComparison.OrdinalIgnoreCase)
            ? path[..^primarySuffix.Length] + "_small"
            : $"{path}_small";
    }
}
