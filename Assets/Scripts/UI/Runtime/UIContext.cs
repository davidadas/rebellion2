using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

public sealed class UIContext
{
    private readonly GameRoot game;
    private readonly FactionThemeLibrary themeLibrary;
    private readonly UIDispatcher dispatcher = new UIDispatcher();
    private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    public UIContext(GameRoot game, FactionThemeLibrary themeLibrary)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        if (themeLibrary == null)
            throw new ArgumentNullException(nameof(themeLibrary));

        this.game = game;
        this.themeLibrary = themeLibrary;
    }

    public GameRoot Game => game;
    public UIDispatcher Dispatcher => dispatcher;

    public FactionTheme GetTheme(string factionInstanceId)
    {
        if (string.IsNullOrEmpty(factionInstanceId))
            return themeLibrary.GetTheme("DEFAULT");

        return themeLibrary.GetTheme(factionInstanceId);
    }

    public FactionTheme GetPlayerFactionTheme()
    {
        return GetTheme(game.GetPlayerFaction().InstanceID);
    }

    public string GetPlayerFactionInstanceID()
    {
        return game.GetPlayerFaction().InstanceID;
    }

    public Color ResolveFactionColor(string factionInstanceId)
    {
        return GetTheme(factionInstanceId).GetPrimaryColor();
    }

    public Color GetFactionColor(string factionInstanceId)
    {
        return ResolveFactionColor(factionInstanceId);
    }

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
                throw new InvalidOperationException("FleetsTab.NormalImagePath missing.");

            return ResourceManager.GetSprite(path);
        }

        string nodePath = node.GetDisplayImagePath();

        if (!string.IsNullOrEmpty(nodePath))
            return ResourceManager.GetSprite(nodePath);

        throw new InvalidOperationException(
            $"No sprite mapping defined for '{node.GetDisplayName()}'."
        );
    }

    public Texture2D GetTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        if (textures.TryGetValue(path, out Texture2D texture))
            return texture;

        texture = ResourceManager.TryGetTexture(path);
        if (texture == null)
        {
            Sprite sprite = ResourceManager.TryGetSprite(path);
            texture = sprite == null ? null : sprite.texture;
        }

        if (texture == null)
            return null;

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        textures[path] = texture;
        return texture;
    }

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

    public Texture2D GetEntityStatusTexture(ISceneNode node, bool small)
    {
        return GetTexture(GetEntityStatusImagePath(node, small));
    }

    public Texture2D GetEntityCapturedOverlayTexture(ISceneNode node)
    {
        return node is Officer { IsCaptured: true }
            ? GetTexture(node.CapturedOverlayImagePath)
            : null;
    }

    private static string GetEntityStatusImagePath(ISceneNode node, bool small)
    {
        if (node == null)
            return null;

        if (node is Officer { InjuryPoints: > 0 } && !string.IsNullOrEmpty(node.InjuredImagePath))
            return node.InjuredImagePath;

        if (
            node is IMovable { Movement: not null }
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

    private static string SelectStatusPath(string preferredPath, string fallbackPath)
    {
        return !string.IsNullOrEmpty(preferredPath) ? preferredPath : fallbackPath;
    }

    private static string SelectStatusPath(bool small, string smallPath, string normalPath)
    {
        return small ? SelectStatusPath(smallPath, normalPath) : normalPath;
    }

    public Texture2D GetPlanetTexture(Planet planet, string iconPath)
    {
        if (planet == null)
            return null;

        string path = planet.IsDestroyed
            ? "Art/UI/StrategyView/ui_strategyview_destroyed_planet_icon"
            : iconPath;

        return GetTexture(path);
    }

    public Texture2D GetPlanetTexture(Planet planet)
    {
        return GetPlanetTexture(planet, planet?.GetPlanetIconPath());
    }

    private static string GetSmallTexturePath(string path)
    {
        const string primarySuffix = "_primary";
        return path.EndsWith(primarySuffix, StringComparison.OrdinalIgnoreCase)
            ? path[..^primarySuffix.Length] + "_small"
            : $"{path}_small";
    }

    public List<FactionTheme> GetAllThemes()
    {
        return themeLibrary.GetAllThemes();
    }
}
