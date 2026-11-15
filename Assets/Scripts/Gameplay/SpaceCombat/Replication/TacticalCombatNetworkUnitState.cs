using System;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures a participant's visible tactical state independently of campaign ownership.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkUnitState
    {
        public string UnitID;
        public TacticalCombatNetworkVector Position;
        public TacticalCombatNetworkVector Heading;
        public TacticalCombatNetworkVector Velocity;
        public TacticalCombatNetworkVector DesiredHeading;
        public TacticalCombatNetworkVector DesiredMovement;
        public TacticalCombatUnitStatus Status;
        public string CarrierID;
        public string TargetID;
        public float Hull;
        public float Shields;
        public float ShieldLimit;
        public float AttackDelay;
        public float MovementDelay;
        public float WeaponRechargeSample;
        public float ShieldRechargeSample;
        public uint[] ComponentDamage = new uint[5];
        public float[] Charge = new float[4];
        public bool[] Ready = new bool[4];
        public float HyperspaceMilliseconds;
        public float TractorRestraint;
        public bool HasTractorSources;
        public float CommandBudget;
        public float TurnRate;
        public bool HasCollision;
        public bool HasGroup;
        public int GroupID;
        public bool HasRequestedGroup;
        public int RequestedGroupID;
    }
}
