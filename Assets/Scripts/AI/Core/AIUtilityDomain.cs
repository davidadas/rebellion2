namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Owns the named game-unit scales used to produce normalized strategic-AI utility inputs.
    /// </summary>
    internal static class AIUtilityDomain
    {
        internal const double Percent = 100;
        internal const double TravelCost = 100;
        internal const double FleetPlanetValue = 1000;
        internal const double InfrastructurePlanetValue = 10000;
        internal const double AssemblyOrderingScale = int.MaxValue;
        internal const double DefenseStrengthGap = 10000;
        internal const double ProductionDemandPressure = 600;
        internal const double ProductionCapability = 1000;
        internal const double OfficerRating = 300;
        internal const double StarfighterWeaponRating = 20;
        internal const double RegimentRating = 10;
        internal const double DiplomacyFacilityCount = 10;
        internal const double DiplomacyResourceNodeCount = 15;
        internal const double ColonizationCapacity = 20;
        internal const double InfrastructureFacilityCount = 100;
        internal const double FleetRegimentCount = 100;
        internal const double AttackRequirementCount = 10;
        internal const double SectorSupport = 10;
        internal const double ConstructionCost = 100;
        internal const double DuplicateFleetUnitCount = 10;
        internal const double IntelligenceAge = 1000;
        internal const double JediTrainingValue = 300;
    }
}
