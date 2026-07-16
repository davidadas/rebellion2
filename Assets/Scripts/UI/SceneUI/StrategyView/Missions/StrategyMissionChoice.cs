using Rebellion.Game.Missions;
using Rebellion.Game.Research;

/// <summary>
/// Captures one mission option and its faction-theme presentation key.
/// </summary>
public sealed class StrategyMissionChoice
{
    /// <summary>
    /// Creates a strategy mission choice from one available game option.
    /// </summary>
    /// <param name="option">The available mission option.</param>
    public StrategyMissionChoice(MissionOption option)
    {
        if (option == null)
            throw new System.ArgumentNullException(nameof(option));

        MissionTypeID = option.MissionTypeID;
        Discipline = option.Discipline;
        Name = option.DisplayName;
        IconKey = MissionIconKeys.GetMissionIconKey(option.MissionTypeID, option.Discipline);
    }

    /// <summary>
    /// Gets the mission type ID.
    /// </summary>
    public string MissionTypeID { get; }

    /// <summary>
    /// Gets the discipline.
    /// </summary>
    public ResearchDiscipline? Discipline { get; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the icon key.
    /// </summary>
    public string IconKey { get; }
}
