using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Commands;
using Rebellion.Game.Results;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Handles authoritative gameplay commands of one concrete type.
    /// </summary>
    /// <typeparam name="T">The request type handled by the subscriber.</typeparam>
    internal interface IGameCommandHandler<T>
        where T : GameCommand
    {
        /// <summary>
        /// Handles one command batch and returns the factual results it produced.
        /// </summary>
        /// <param name="commands">The commands to execute.</param>
        /// <returns>The factual results produced by the commands.</returns>
        List<GameResult> HandleCommands(IReadOnlyList<T> commands);
    }

    /// <summary>
    /// Routes gameplay commands to their authoritative feature implementations.
    /// </summary>
    internal sealed class GameCommands : IGameCommandExecutor
    {
        private readonly Dictionary<Type, Func<GameCommand, GameCommandResult>> _handlers =
            new Dictionary<Type, Func<GameCommand, GameCommandResult>>();

        /// <summary>
        /// Registers the sole authoritative handler for one concrete command type.
        /// </summary>
        /// <typeparam name="T">The command type delivered to the handler.</typeparam>
        /// <param name="handler">The handler to invoke for matching commands.</param>
        internal void Subscribe<T>(IGameCommandHandler<T> handler)
            where T : GameCommand
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (
                !_handlers.TryAdd(
                    typeof(T),
                    command => new GameCommandResult(
                        true,
                        handler.HandleCommands(new[] { (T)command })
                    )
                )
            )
                throw new InvalidOperationException(
                    $"A command handler is already registered for '{typeof(T).Name}'."
                );
        }

        /// <summary>
        /// Registers the sole authoritative executor for one command type.
        /// </summary>
        /// <typeparam name="T">The command type executed.</typeparam>
        /// <param name="execute">The command executor.</param>
        internal void Subscribe<T>(Func<T, GameCommandResult> execute)
            where T : GameCommand
        {
            if (execute == null)
                throw new ArgumentNullException(nameof(execute));
            if (!_handlers.TryAdd(typeof(T), command => execute((T)command)))
                throw new InvalidOperationException(
                    $"A command handler is already registered for '{typeof(T).Name}'."
                );
        }

        /// <summary>
        /// Executes one command without routing its factual results.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns>The raw command outcome.</returns>
        public GameCommandResult ExecuteRaw(GameCommand command)
        {
            if (command == null)
                return new GameCommandResult(false, Array.Empty<GameResult>());
            if (
                !_handlers.TryGetValue(
                    command.GetType(),
                    out Func<GameCommand, GameCommandResult> handler
                )
            )
                throw new InvalidOperationException(
                    $"No command handler is registered for '{command.GetType().Name}'."
                );
            return handler(command) ?? new GameCommandResult(false, Array.Empty<GameResult>());
        }

        /// <summary>
        /// Executes commands in authored order, logging failed commands before continuing, and
        /// returns only their factual results.
        /// </summary>
        /// <param name="commands">The commands to execute.</param>
        /// <returns>The factual results produced by all matching handlers.</returns>
        public List<GameResult> ExecuteRaw(IEnumerable<GameCommand> commands)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (
                GameCommand command in commands?.Where(command => command != null)
                    ?? Enumerable.Empty<GameCommand>()
            )
            {
                try
                {
                    if (
                        !_handlers.TryGetValue(
                            command.GetType(),
                            out Func<GameCommand, GameCommandResult> handler
                        )
                    )
                        throw new InvalidOperationException(
                            $"No command handler is registered for '{command.GetType().Name}'."
                        );

                    GameCommandResult outcome = handler(command);
                    if (outcome?.Results == null)
                        continue;

                    foreach (GameResult result in outcome.Results.Where(result => result != null))
                    {
                        if (string.IsNullOrWhiteSpace(result.SourceEventInstanceID))
                            result.SourceEventInstanceID = command.SourceEventInstanceID;
                        results.Add(result);
                    }
                }
                catch (Exception exception)
                {
                    string eventInstanceId = command.SourceEventInstanceID ?? "unknown";
                    GameLogger.Log(
                        $"Event '{eventInstanceId}' command '{command.GetType().Name}' failed: {exception}",
                        GameLogger.LogLevel.Error
                    );
                }
            }
            return results;
        }
    }
}
