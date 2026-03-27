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

    /// <summary>
    ///
    /// </summary>
    /// <param name="planet"></param>
    /// <param name="context"></param>
    /// <exception cref="GameException"></exception>
    public void Initialize(Planet planet, UIContext context)
    {
        this.planet = planet;
        this.context = context;

        RectTransform rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.anchoredPosition = new Vector2(planet.PositionX, planet.PositionY);
        rect.sizeDelta = new Vector2(30, 30);
        string factionOwnerId = planet.WasVisitedBy(context.GetPlayerFactionInstanceID())
            ? planet.GetOwnerInstanceID()
            : "UNKNOWN";
        FactionTheme theme = context.GetTheme(factionOwnerId);

        string path = theme?.GalaxyBackground?.PlanetIcons?.Large;
        UnityEngine.Debug.Log(theme);

        if (string.IsNullOrEmpty(path))
            throw new GameException("Planet icon path missing.");

        Image img = gameObject.AddComponent<Image>();
        img.sprite = ResourceManager.Instance.GetSprite(path);
        img.raycastTarget = false;
    }
}
