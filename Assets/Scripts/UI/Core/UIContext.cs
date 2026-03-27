using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.SceneGraph;
using UnityEngine;

public sealed class UIContext
{
    private readonly GameRoot game;
    private readonly FactionThemeLibrary themeLibrary;

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
                throw new GameException("Fleet missing OwnerInstanceID.");

            FactionTheme theme = GetTheme(ownerId);

            string path = theme?.PlanetWindowTheme?.FleetsPane?.FleetsImagePath;

            if (string.IsNullOrEmpty(path))
                throw new GameException("FleetsTab.NormalImagePath missing.");

            return ResourceManager.Instance.GetSprite(path);
        }

        string nodePath = node.GetDisplayImagePath();

        if (!string.IsNullOrEmpty(nodePath))
            return ResourceManager.Instance.GetSprite(nodePath);

        throw new GameException($"No sprite mapping defined for '{node.GetDisplayName()}'.");
    }

    public List<FactionTheme> GetAllThemes()
    {
        return themeLibrary.GetAllThemes();
    }
}
