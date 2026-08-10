using System;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects one boolean officer state for a data-defined condition.
    /// </summary>
    public enum OfficerStateKind
    {
        Available,
        Captured,
        Killed,
        Injured,
        ForceEligible,
    }

    /// <summary>
    /// Tests a data-selected runtime state on one officer.
    /// </summary>
    [PersistableObject(Name = "OfficerState")]
    public class OfficerStateConditional : GameConditional
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute(Name = "Is")]
        public OfficerStateKind? Is { get; set; }

        [PersistableAttribute(Name = "IsNot")]
        public OfficerStateKind? IsNot { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                return false;

            if (Is.HasValue == IsNot.HasValue)
                throw new InvalidOperationException(
                    "OfficerState requires exactly one of Is or IsNot."
                );

            OfficerStateKind state = Is ?? IsNot.Value;
            bool current = state switch
            {
                OfficerStateKind.Available => !officer.IsKilled && !officer.IsCaptured,
                OfficerStateKind.Captured => officer.IsCaptured,
                OfficerStateKind.Killed => officer.IsKilled,
                OfficerStateKind.Injured => officer.InjuryPoints > 0,
                OfficerStateKind.ForceEligible => officer.IsForceEligible,
                _ => throw new InvalidOperationException($"Unsupported officer state '{state}'."),
            };
            return Is.HasValue ? current : !current;
        }
    }

    /// <summary>
    /// Tests which faction currently holds a captured officer.
    /// </summary>
    [PersistableObject(Name = "OfficerCaptor")]
    public sealed class OfficerCaptorConditional : GameConditional
    {
        public string OfficerInstanceID { get; set; }
        public string FactionInstanceID { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            return officer?.IsCaptured == true && officer.CaptorInstanceID == FactionInstanceID;
        }
    }

    /// <summary>
    /// Compares one officer's effective Force rank with an authored threshold.
    /// </summary>
    [PersistableObject(Name = "OfficerForceRank")]
    public class OfficerForceRankConditional : GameConditional
    {
        public string OfficerInstanceID { get; set; }
        public EventVariableComparison Comparison { get; set; }
        public int ForceRank { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                return false;

            int current = officer.ForceRank;
            return Comparison switch
            {
                EventVariableComparison.Equal => current == ForceRank,
                EventVariableComparison.NotEqual => current != ForceRank,
                EventVariableComparison.GreaterThan => current > ForceRank,
                EventVariableComparison.GreaterThanOrEqual => current >= ForceRank,
                EventVariableComparison.LessThan => current < ForceRank,
                EventVariableComparison.LessThanOrEqual => current <= ForceRank,
                _ => throw new InvalidOperationException(
                    $"Unsupported Force-rank comparison '{Comparison}'."
                ),
            };
        }
    }
}
