using System;

namespace Rebellion.AI
{
    /// <summary>
    /// Unit categories that the automation issue system tracks.
    /// Each maps to a class of game object the AI may need to build or deploy.
    /// </summary>
    public enum IssueUnitType
    {
        CapitalShip,
        Starfighter,
        Troop,
        ConstructionFacility,
        GarrisonBase,
    }

    /// <summary>
    /// A single "this planet needs N more units of type X" record.
    /// Produced by IssueProviders, consumed by AIManager to drive production and deployment.
    /// </summary>
    public class AutomationIssue
    {
        public string PlanetInstanceID { get; set; }
        public IssueUnitType UnitType { get; set; }
        public int DesiredCount { get; set; }
        public int ActualCount { get; set; }
        public int Deficit => Math.Max(0, DesiredCount - ActualCount);
        public float Score { get; set; }
    }
}
