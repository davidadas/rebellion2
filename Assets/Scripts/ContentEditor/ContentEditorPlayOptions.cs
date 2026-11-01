using Rebellion.Game;

/// <summary>
/// Describes the author's campaign choices for a test of the current working content.
/// </summary>
public readonly struct ContentEditorPlayOptions
{
    public string ScenarioID { get; }
    public string FactionID { get; }
    public GameSize GalaxySize { get; }
    public GameDifficulty Difficulty { get; }

    /// <summary>
    /// Captures one requested test campaign without supplying runtime content identity.
    /// </summary>
    /// <param name="scenarioID">The authored scenario to test.</param>
    /// <param name="factionID">The playable faction selected by the author.</param>
    /// <param name="galaxySize">The galaxy size to generate.</param>
    /// <param name="difficulty">The requested difficulty.</param>
    public ContentEditorPlayOptions(
        string scenarioID,
        string factionID,
        GameSize galaxySize,
        GameDifficulty difficulty
    )
    {
        ScenarioID = scenarioID;
        FactionID = factionID;
        GalaxySize = galaxySize;
        Difficulty = difficulty;
    }
}
