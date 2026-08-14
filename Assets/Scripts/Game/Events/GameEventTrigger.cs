using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects one stable simulation event and declares the result arguments exposed locally.
    /// </summary>
    [PersistableObject(Name = "Trigger")]
    public sealed class GameEventTrigger
    {
        [PersistableAttribute]
        public string Event { get; set; }

        public List<GameEventTriggerBinding> Bindings { get; set; } =
            new List<GameEventTriggerBinding>();

        public GameEventTrigger() { }

        public GameEventTrigger(string eventID, params (string Argument, string As)[] bindings)
        {
            Event = eventID;
            foreach ((string argument, string localName) in bindings)
                Bindings.Add(new GameEventTriggerBinding { Argument = argument, As = localName });
        }

        internal Type ResultType => GameEventTriggerRegistry.GetResultType(Event);

        /// <summary>
        /// Gets the statically typed arguments exposed by this trigger contract.
        /// </summary>
        [PersistableIgnore]
        public IReadOnlyDictionary<string, Type> AvailableArguments =>
            GameEventTriggerRegistry.GetArguments(Event);

        internal bool Matches(GameResult result) => GameEventTriggerRegistry.Matches(Event, result);

        internal Type GetArgumentType(string argument) =>
            GameEventTriggerRegistry.GetArgumentType(Event, argument);

        internal void Bind(GameEventExecutionContext context, GameResult result) =>
            GameEventTriggerRegistry.Bind(context, this, result);
    }

    /// <summary>
    /// Gives one public trigger argument a local name within an event activation.
    /// </summary>
    [PersistableObject(Name = "Bind")]
    public sealed class GameEventTriggerBinding
    {
        [PersistableAttribute]
        public string Argument { get; set; }

        [PersistableAttribute]
        public string As { get; set; }
    }
}

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
            IReadOnlyDictionary<string, Type> Arguments { get; }
            bool Matches(GameResult result);
            bool TryReadArgument(GameResult result, string argument, out object value);
            bool TryGetArgumentType(string argument, out Type type);
        }

        private sealed class TriggerContract<TResult> : ITriggerContract
            where TResult : GameResult
        {
            private sealed class TriggerArgument
            {
                internal Type Type { get; set; }
                internal Func<TResult, object> Getter { get; set; }
            }

            private readonly Dictionary<string, TriggerArgument> _arguments = new(
                StringComparer.Ordinal
            );

            public Type ResultType => typeof(TResult);

            public IReadOnlyDictionary<string, Type> Arguments =>
                _arguments.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value.Type,
                    StringComparer.Ordinal
                );

            internal TriggerContract<TResult> Argument<TValue>(
                string name,
                Func<TResult, TValue> getter
            )
            {
                _arguments.Add(
                    name,
                    new TriggerArgument { Type = typeof(TValue), Getter = result => getter(result) }
                );
                return this;
            }

            public bool Matches(GameResult result) => result is TResult;

            public bool TryReadArgument(GameResult result, string argument, out object value)
            {
                if (
                    result is TResult typed
                    && _arguments.TryGetValue(argument, out TriggerArgument contract)
                )
                {
                    value = contract.Getter(typed);
                    return true;
                }

                value = null;
                return false;
            }

            public bool TryGetArgumentType(string argument, out Type type)
            {
                if (_arguments.TryGetValue(argument, out TriggerArgument contract))
                {
                    type = contract.Type;
                    return true;
                }
                type = null;
                return false;
            }
        }

        private static readonly Dictionary<string, ITriggerContract> Contracts = BuildContracts();

        internal static Type GetResultType(string eventID) => GetContract(eventID).ResultType;

        internal static IReadOnlyDictionary<string, Type> GetArguments(string eventID) =>
            GetContract(eventID).Arguments;

        internal static bool Matches(string eventID, GameResult result) =>
            result != null && GetContract(eventID).Matches(result);

        internal static Type GetArgumentType(string eventID, string argument)
        {
            if (GetContract(eventID).TryGetArgumentType(argument, out Type type))
                return type;
            throw new InvalidOperationException(
                $"Trigger '{eventID}' does not expose argument '{argument}'."
            );
        }

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
                .Argument("UnitInstanceID", result => result.Unit?.InstanceID)
                .Argument("Destination", result => result.Destination)
                .Argument("DestinationInstanceID", result => result.Destination?.InstanceID)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<DuelResult>(contracts, "core:duel.completed")
                .Argument("Officer", result => result.EncounteredOfficer)
                .Argument("OfficerInstanceID", result => result.EncounteredOfficer?.InstanceID)
                .Argument("Opponent", result => result.OpposingOfficer)
                .Argument("OpponentInstanceID", result => result.OpposingOfficer?.InstanceID)
                .Argument("Location", result => result.Location)
                .Argument("OfficerCaptured", result => result.EncounteredOfficerCaptured)
                .Argument("OfficerInjury", result => result.EncounteredOfficerInjury)
                .Argument("OpponentInjury", result => result.OpposingOfficerInjury)
                .Argument("ImagePath", result => result.ImagePath)
                .Argument("AudioPath", result => result.AudioPath)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<OfficerCaptureStateResult>(contracts, "core:officer.capture-changed")
                .Argument("Officer", result => result.TargetOfficer ?? result.CapturedOfficer)
                .Argument(
                    "OfficerInstanceID",
                    result => (result.TargetOfficer ?? result.CapturedOfficer)?.InstanceID
                )
                .Argument("LinkedOfficer", result => result.LinkedOfficer)
                .Argument("Context", result => result.Context)
                .Argument("IsCaptured", result => result.IsCaptured)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<MissionCompletedResult>(contracts, "core:mission.completed")
                .Argument("Mission", result => result.Mission)
                .Argument("Outcome", result => result.Outcome)
                .Argument("CompletionReason", result => result.CompletionReason)
                .Argument("Participants", result => result.Participants)
                .Argument("Location", result => result.Location)
                .Argument("ReturnDestination", result => result.ReturnDestination)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
            Register<ForceDiscoveryResult>(contracts, "core:force.discovered")
                .Argument("Officer", result => result.Officer)
                .Argument("Discoverer", result => result.Discoverer)
                .Argument("ForceRank", result => result.ForceRank)
                .Argument("SourceEventInstanceID", result => result.SourceEventInstanceID);
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
