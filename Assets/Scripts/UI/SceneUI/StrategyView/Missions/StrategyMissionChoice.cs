using Rebellion.Game.Missions;
using Rebellion.Game.Research;

public sealed class StrategyMissionChoice
{
    public StrategyMissionChoice(MissionOption option)
    {
        Type = option.Type;
        Discipline = option.Discipline;
        Name = option.Name;
        IconKey = option.IconKey;
    }

    public MissionType Type { get; }
    public ResearchDiscipline? Discipline { get; }
    public string Name { get; }
    public string IconKey { get; }
}
