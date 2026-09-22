using Rebellion.Game.Missions;
using Rebellion.Game.Research;

/// <summary>
/// Maps mission types and research disciplines to faction-theme mission icon keys.
/// </summary>
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

    /// <summary>
    /// Gets the faction-theme icon key for a mission type and optional research discipline.
    /// </summary>
    /// <param name="missionTypeID">The mission type identifier.</param>
    /// <param name="discipline">The optional research discipline.</param>
    /// <returns>The matching icon key, or null for an unsupported mission type.</returns>
    public static string GetMissionIconKey(
        string missionTypeID,
        ResearchDiscipline? discipline = null
    )
    {
        return missionTypeID switch
        {
            DiplomacyMission.MissionTypeID => Diplomacy,
            RescueMission.MissionTypeID => Rescue,
            SabotageMission.MissionTypeID => Sabotage,
            EspionageMission.MissionTypeID => Espionage,
            ReconnaissanceMission.MissionTypeID => Reconnaissance,
            RecruitmentMission.MissionTypeID => Recruitment,
            AbductionMission.MissionTypeID => Abduction,
            ResearchMission.MissionTypeID => GetResearchMissionIconKey(discipline),
            InciteUprisingMission.MissionTypeID => InciteUprising,
            JediTrainingMission.MissionTypeID => JediTraining,
            SubdueUprisingMission.MissionTypeID => SubdueUprising,
            AssassinationMission.MissionTypeID => Assassination,
            _ => null,
        };
    }

    /// <summary>
    /// Gets the research mission icon key for one optional discipline.
    /// </summary>
    /// <param name="discipline">The optional research discipline.</param>
    /// <returns>The discipline icon key, defaulting to ship design.</returns>
    private static string GetResearchMissionIconKey(ResearchDiscipline? discipline)
    {
        return discipline switch
        {
            ResearchDiscipline.ShipDesign => ResearchShipDesign,
            ResearchDiscipline.FacilityDesign => ResearchFacilityDesign,
            ResearchDiscipline.TroopTraining => ResearchTroopTraining,
            _ => ResearchShipDesign,
        };
    }
}
