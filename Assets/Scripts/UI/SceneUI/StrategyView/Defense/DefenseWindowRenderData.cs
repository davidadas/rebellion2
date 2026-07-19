using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies one tab in the authored Defense-window order.
/// </summary>
public enum DefenseWindowTab
{
    Personnel = 0,
    Regiments = 1,
    Starfighters = 2,
    Shields = 3,
    Batteries = 4,
}

/// <summary>
/// Contains immutable presentation data for one Defense window tab.
/// </summary>
public sealed class DefenseWindowTabRenderData
{
    /// <summary>
    /// Creates one complete Defense tab presentation snapshot.
    /// </summary>
    /// <param name="tab">The represented Defense tab.</param>
    /// <param name="texture">The tab texture shown while released.</param>
    /// <param name="pressedTexture">The optional tab texture shown while pressed.</param>
    public DefenseWindowTabRenderData(DefenseWindowTab tab, Texture texture, Texture pressedTexture)
    {
        Tab = tab;
        Texture = texture;
        PressedTexture = pressedTexture;
    }

    public DefenseWindowTab Tab { get; }

    public Texture Texture { get; }

    public Texture PressedTexture { get; }
}

/// <summary>
/// Contains immutable presentation data for one Defense window.
/// </summary>
public sealed class DefenseWindowRenderData
{
    private static readonly DefenseWindowTab[] _orderedTabs =
    {
        DefenseWindowTab.Personnel,
        DefenseWindowTab.Regiments,
        DefenseWindowTab.Starfighters,
        DefenseWindowTab.Shields,
        DefenseWindowTab.Batteries,
    };
    private static readonly IReadOnlyList<DefenseWindowTab> _readOnlyOrderedTabs = Array.AsReadOnly(
        _orderedTabs
    );

    public static int TabCount => _orderedTabs.Length;

    public static IReadOnlyList<DefenseWindowTab> OrderedTabs => _readOnlyOrderedTabs;

    /// <summary>
    /// Creates one complete Defense window presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="titleTexture">The active or inactive title texture.</param>
    /// <param name="caption">The displayed planet name.</param>
    /// <param name="activeTab">The selected Defense tab.</param>
    /// <param name="tabTitle">The selected tab title.</param>
    /// <param name="tabs">The ordered tab presentations.</param>
    /// <param name="items">The ordered unit-card presentations.</param>
    public DefenseWindowRenderData(
        int x,
        int y,
        Texture titleTexture,
        string caption,
        DefenseWindowTab activeTab,
        string tabTitle,
        IReadOnlyList<DefenseWindowTabRenderData> tabs,
        IReadOnlyList<StrategyUnitCardRenderData> items
    )
    {
        X = x;
        Y = y;
        TitleTexture = titleTexture;
        Caption = caption ?? string.Empty;
        ActiveTab = activeTab;
        TabTitle = tabTitle ?? string.Empty;
        Tabs = Copy(tabs, nameof(tabs));
        Items = Copy(items, nameof(items));
    }

    public int X { get; }

    public int Y { get; }

    public Texture TitleTexture { get; }

    public string Caption { get; }

    public DefenseWindowTab ActiveTab { get; }

    public string TabTitle { get; }

    public IReadOnlyList<DefenseWindowTabRenderData> Tabs { get; }

    public IReadOnlyList<StrategyUnitCardRenderData> Items { get; }

    /// <summary>
    /// Copies a required presentation collection into an isolated read-only snapshot.
    /// </summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="items">The source collection.</param>
    /// <param name="parameterName">The source parameter name.</param>
    /// <returns>The isolated read-only collection.</returns>
    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> items, string parameterName)
    {
        return new List<T>(items ?? throw new ArgumentNullException(parameterName)).AsReadOnly();
    }
}
