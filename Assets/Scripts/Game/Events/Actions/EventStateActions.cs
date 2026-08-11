using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    public enum EventVariableOperation
    {
        Set,
        Add,
        Minimum,
        Maximum,
    }

    [PersistableObject(Name = "SetEventVariable")]
    public sealed class SetEventVariableAction : GameAction
    {
        public string Key { get; set; }
        public EventVariableOperation Operation { get; set; }
        public int Operand { get; set; }

        public override List<GameResult> Execute(GameActionContext context)
        {
            int previousValue = context.Game.EventRuntime.GetVariable(Key);
            int currentValue = Operation switch
            {
                EventVariableOperation.Set => Operand,
                EventVariableOperation.Add => checked(previousValue + Operand),
                EventVariableOperation.Minimum => Math.Min(previousValue, Operand),
                EventVariableOperation.Maximum => Math.Max(previousValue, Operand),
                _ => throw new InvalidOperationException(
                    $"Unsupported event variable operation '{Operation}'."
                ),
            };
            context.Game.EventRuntime.SetVariable(Key, currentValue);
            return new List<GameResult>
            {
                new EventVariableChangedResult
                {
                    Key = Key,
                    PreviousValue = previousValue,
                    CurrentValue = currentValue,
                    Tick = context.Game.CurrentTick,
                },
            };
        }
    }
}
