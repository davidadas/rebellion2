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

    public string MissionTypeID { get; }

    public ResearchDiscipline? Discipline { get; }

    public string Name { get; }

    public string IconKey { get; }
}
