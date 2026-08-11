using System;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects one boolean officer state for a data-defined condition.
    /// </summary>
    public abstract class OfficerBooleanConditional : GameConditional
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            return officer != null && Evaluate(officer);
        }

        protected abstract bool Evaluate(Officer officer);
    }

    [PersistableObject(Name = "IsCaptured")]
    public sealed class IsCapturedConditional : OfficerBooleanConditional
    {
        protected override bool Evaluate(Officer officer) => officer.IsCaptured;
    }

    [PersistableObject(Name = "IsKilled")]
    public sealed class IsKilledConditional : OfficerBooleanConditional
    {
        protected override bool Evaluate(Officer officer) => officer.IsKilled;
    }

    [PersistableObject(Name = "IsInjured")]
    public sealed class IsInjuredConditional : OfficerBooleanConditional
    {
        protected override bool Evaluate(Officer officer) => officer.InjuryPoints > 0;
    }

    [PersistableObject(Name = "IsForceEligible")]
    public sealed class IsForceEligibleConditional : OfficerBooleanConditional
    {
        protected override bool Evaluate(Officer officer) => officer.IsForceEligible;
    }

    /// <summary>
    /// Tests which faction currently holds a captured officer.
    /// </summary>
    [PersistableObject(Name = "IsCapturedBy")]
    public sealed class IsCapturedByConditional : GameConditional
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameConditionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            return officer?.IsCaptured == true && officer.CaptorInstanceID == FactionInstanceID;
        }
    }

    /// <summary>
    /// Compares one officer's effective Force rank with an authored threshold.
    /// </summary>
    [PersistableObject(Name = "HasForceRank")]
    public sealed class HasForceRankConditional : GameConditional
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public EventVariableComparison Comparison { get; set; }

        [PersistableAttribute]
        public int ExpectedRank { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameConditionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                return false;

            int current = officer.ForceRank;
            return Comparison switch
            {
                EventVariableComparison.Equal => current == ExpectedRank,
                EventVariableComparison.NotEqual => current != ExpectedRank,
                EventVariableComparison.GreaterThan => current > ExpectedRank,
                EventVariableComparison.GreaterThanOrEqual => current >= ExpectedRank,
                EventVariableComparison.LessThan => current < ExpectedRank,
                EventVariableComparison.LessThanOrEqual => current <= ExpectedRank,
                _ => throw new InvalidOperationException(
                    $"Unsupported Force-rank comparison '{Comparison}'."
                ),
            };
        }
    }
}
