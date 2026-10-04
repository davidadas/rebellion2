namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Provides stable mission identifiers for consumers that evaluate several mission types.
    /// </summary>
    public static class MissionTypeIDs
    {
        public const string Abduction = AbductionMission.MissionTypeID;
        public const string Assassination = AssassinationMission.MissionTypeID;
        public const string Diplomacy = DiplomacyMission.MissionTypeID;
        public const string Espionage = EspionageMission.MissionTypeID;
        public const string InciteUprising = InciteUprisingMission.MissionTypeID;
        public const string JediTraining = JediTrainingMission.MissionTypeID;
        public const string Reconnaissance = ReconnaissanceMission.MissionTypeID;
        public const string Recruitment = RecruitmentMission.MissionTypeID;
        public const string Rescue = RescueMission.MissionTypeID;
        public const string Research = ResearchMission.MissionTypeID;
        public const string Sabotage = SabotageMission.MissionTypeID;
        public const string SubdueUprising = SubdueUprisingMission.MissionTypeID;
    }
}
