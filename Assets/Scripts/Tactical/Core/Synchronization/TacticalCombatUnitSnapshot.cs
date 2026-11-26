using System;
using Rebellion.Game.Combat;

namespace Rebellion.Tactical.Synchronization
{
    /// <summary>
    /// Captures a participant's visible tactical state independently of campaign ownership.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatUnitSnapshot
    {
        public string UnitID;
        public TacticalCombatSnapshotVector Position;
        public TacticalCombatSnapshotVector Heading;
        public TacticalCombatSnapshotVector Velocity;
        public TacticalCombatSnapshotVector DesiredHeading;
        public TacticalCombatSnapshotVector DesiredMovement;
        public CombatUnitStatus Status;
        public string CarrierID;
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
    }
}
