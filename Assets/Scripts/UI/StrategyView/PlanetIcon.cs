using System;
using Rebellion.Game;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///
/// </summary>
public sealed class PlanetIcon : MonoBehaviour
{
    private Planet planet;
    private UIContext context;
    private GalaxyCoordinateMapper mapper;

    /// <summary>
    ///
    /// </summary>
    /// <param name="planet"></param>
    /// <param name="context"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void Initialize(Planet planet, UIContext context, GalaxyCoordinateMapper mapper)
    {
        this.planet = planet;
        this.context = context;
        this.mapper = mapper;

        RectTransform rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.anchoredPosition = this.mapper.Map(planet.PositionX, planet.PositionY);
        rect.sizeDelta = new Vector2(30, 30);
        string factionOwnerId = planet.WasVisitedBy(context.GetPlayerFactionInstanceID())
            ? planet.GetOwnerInstanceID()
            : "UNKNOWN";
        FactionTheme theme = context.GetTheme(factionOwnerId);

        string path = theme?.GalaxyBackground?.PlanetIcons?.Large;

        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("Planet icon path missing.");

        Image img = gameObject.AddComponent<Image>();
        img.sprite = ResourceManager.GetSprite(path);
        img.raycastTarget = false;
    }
}
