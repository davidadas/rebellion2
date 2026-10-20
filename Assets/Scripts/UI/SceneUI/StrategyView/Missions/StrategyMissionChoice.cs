using Rebellion.Game.Missions;
using Rebellion.Game.Research;

/// <summary>
/// Captures one mission option and its faction-theme presentation key.
/// </summary>
public sealed class StrategyMissionChoice
{
    public string MissionTypeID { get; }

    public ResearchDiscipline? Discipline { get; }

    public string Name { get; }

    public string IconKey { get; }

    public MissionTargetKind TargetKind { get; }

    public bool IsRepeating { get; }

    /// <summary>
    /// Creates a strategy mission choice from one available game option.
    /// </summary>
    /// <param name="option">The available mission option.</param>
    /// <param name="isRepeating">Whether this choice requests repeating attempts; defaults to false.</param>
    public StrategyMissionChoice(MissionOption option, bool isRepeating = false)
    {
        if (option == null)
            throw new System.ArgumentNullException(nameof(option));
        MissionTypeID = option.MissionTypeID;
        Discipline = option.Discipline;
        Name = isRepeating ? option.DisplayName + " (Repeat)" : option.DisplayName;
        IconKey = MissionIconKeys.GetMissionIconKey(option.MissionTypeID, option.Discipline);
        TargetKind = option.TargetKind;
        IsRepeating = isRepeating;
    }
}
