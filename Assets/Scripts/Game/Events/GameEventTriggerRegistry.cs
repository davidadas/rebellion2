using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines the stable, explicitly typed simulation-result contracts available to content.
    /// </summary>
    internal static class GameEventTriggerRegistry
    {
        private interface ITriggerContract
        {
            Type ResultType { get; }
            bool Matches(GameResult result);
            bool TryReadArgument(GameResult result, string argument, out object value);
        }

        private sealed class TriggerContract<TResult> : ITriggerContract
            where TResult : GameResult
        {
            private readonly Dictionary<string, Func<TResult, object>> _arguments = new(
                StringComparer.Ordinal
            );

            public Type ResultType => typeof(TResult);

            internal TriggerContract<TResult> Argument(string name, Func<TResult, object> getter)
            {
                _arguments.Add(name, getter);
                return this;
            }

            public bool Matches(GameResult result) => result is TResult;

            public bool TryReadArgument(GameResult result, string argument, out object value)
            {
                if (result is TResult typed && _arguments.TryGetValue(argument, out var getter))
                {
                    value = getter(typed);
                    return true;
                }

                value = null;
                return false;
            }
        }

        private static readonly Dictionary<string, ITriggerContract> Contracts = BuildContracts();

        internal static Type GetResultType(string eventID) => GetContract(eventID).ResultType;

        internal static bool Matches(string eventID, GameResult result) =>
            result != null && GetContract(eventID).Matches(result);

        internal static void Bind(
            GameEventExecutionContext context,
            GameEventTrigger trigger,
            GameResult result
        )
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (trigger == null || result == null)
                return;

            ITriggerContract contract = GetContract(trigger.Event);
            foreach (GameEventTriggerBinding binding in trigger.Bindings)
            {
                if (!contract.TryReadArgument(result, binding.Argument, out object value))
                    throw new InvalidOperationException(
                        $"Trigger '{trigger.Event}' does not expose argument '{binding.Argument}'."
                    );
                context.Bind(binding.As, value);
            }
        }

        private static ITriggerContract GetContract(string eventID)
        {
            if (
                string.IsNullOrWhiteSpace(eventID)
                || !Contracts.TryGetValue(eventID, out ITriggerContract contract)
            )
                throw new InvalidOperationException($"Unknown game-event trigger '{eventID}'.");
            return contract;
        }

        private static Dictionary<string, ITriggerContract> BuildContracts()
        {
            Dictionary<string, ITriggerContract> contracts = new(StringComparer.Ordinal);
            Register<UnitArrivedResult>(contracts, "core:unit.arrived")
                .Argument("Unit", result => result.Unit)
                .Argument("Destination", result => result.Destination)
                .Argument("SourceEvent", result => result.SourceEventInstanceID);
            Register<DuelResult>(contracts, "core:duel.completed")
                .Argument("Officer", result => result.EncounteredOfficer)
                .Argument("Opponent", result => result.OpposingOfficer)
                .Argument("Location", result => result.Location)
                .Argument("OfficerCaptured", result => result.EncounteredOfficerCaptured)
                .Argument("OfficerInjury", result => result.EncounteredOfficerInjury)
                .Argument("OpponentInjury", result => result.OpposingOfficerInjury)
                .Argument("ImagePath", result => result.ImagePath)
                .Argument("AudioPath", result => result.AudioPath)
                .Argument("SourceEvent", result => result.SourceEventInstanceID);
            Register<OfficerCaptureStateResult>(contracts, "core:officer.capture-changed")
                .Argument("Officer", result => result.TargetOfficer ?? result.CapturedOfficer)
                .Argument("LinkedOfficer", result => result.LinkedOfficer)
                .Argument("Context", result => result.Context)
                .Argument("IsCaptured", result => result.IsCaptured)
                .Argument("SourceEvent", result => result.SourceEventInstanceID);
            Register<MissionCompletedResult>(contracts, "core:mission.completed")
                .Argument("Mission", result => result.Mission)
                .Argument("Outcome", result => result.Outcome)
                .Argument("CompletionReason", result => result.CompletionReason)
                .Argument("Participants", result => result.Participants)
                .Argument("Location", result => result.Location)
                .Argument("ReturnDestination", result => result.ReturnDestination)
                .Argument("SourceEvent", result => result.SourceEventInstanceID);
            Register<ForceDiscoveryResult>(contracts, "core:force.discovered")
                .Argument("Officer", result => result.Officer)
                .Argument("Discoverer", result => result.Discoverer)
                .Argument("ForceRank", result => result.ForceRank)
                .Argument("SourceEvent", result => result.SourceEventInstanceID);
            return contracts;
        }

        private static TriggerContract<TResult> Register<TResult>(
            IDictionary<string, ITriggerContract> contracts,
            string eventID
        )
            where TResult : GameResult
        {
            TriggerContract<TResult> contract = new();
            contracts.Add(eventID, contract);
            return contract;
        }
    }
}
