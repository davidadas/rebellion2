using Rebellion.Game.Research;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Identifies the type of covert mission.
    /// </summary>
    public enum MissionType
    {
        None,
        Reconnaissance,
        Diplomacy,
        Recruitment,
        SubdueUprising,
        Abduction,
        Assassination,
        Espionage,
        Sabotage,
        InciteUprising,
        Rescue,
        Research,
        JediTraining,
    }

    public sealed class MissionOption
    {
        public MissionOption(
            MissionType type,
            ResearchDiscipline? discipline,
            string name,
            string iconKey
        )
        {
            Type = type;
            Discipline = discipline;
            Name = name;
            IconKey = iconKey;
        }

        public MissionType Type { get; }
        public ResearchDiscipline? Discipline { get; }
        public string Name { get; }
        public string IconKey { get; }
    }

    public static class MissionIconKeys
    {
        public const string Diplomacy = "Diplomacy";
        public const string Rescue = "Rescue";
        public const string Sabotage = "Sabotage";
        public const string Espionage = "Espionage";
        public const string Reconnaissance = "Reconnaissance";
        public const string Recruitment = "Recruitment";
        public const string Abduction = "Abduction";
        public const string ResearchShipDesign = "ResearchShipDesign";
        public const string ResearchFacilityDesign = "ResearchFacilityDesign";
        public const string ResearchTroopTraining = "ResearchTroopTraining";
        public const string InciteUprising = "InciteUprising";
        public const string JediTraining = "JediTraining";
        public const string SubdueUprising = "SubdueUprising";
        public const string Assassination = "Assassination";
    }
}
