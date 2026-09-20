using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rebellion.Game.Commands;
using Rebellion.Game.Results;
using Rebellion.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.Simulation
{
    /// <summary>
    /// Verifies authoritative command routing independently from factual result reactions.
    /// </summary>
    [TestFixture]
    public sealed class GameCommandsTests
    {
        [Test]
        public void ExecuteRaw_RegisteredCommand_ReturnsFactsWithSourceEvent()
        {
            GameCommands dispatcher = new GameCommands();
            dispatcher.Subscribe(new TestCommandHandler());

            List<GameResult> results = dispatcher.ExecuteRaw(
                new[] { new TestCommand { SourceEventInstanceID = "source-event" } }
            );

            Assert.AreEqual("source-event", results[0].SourceEventInstanceID);
        }

        [Test]
        public void ExecuteRaw_UnregisteredCommand_ReturnsNoResults()
        {
            GameCommands dispatcher = new GameCommands();
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'unknown' command 'TestCommand' failed:")
            );

            List<GameResult> results = dispatcher.ExecuteRaw(new[] { new TestCommand() });

            Assert.IsEmpty(results);
        }

        [Test]
        public void ExecuteRaw_HandlerThrows_ProcessesRemainingCommands()
        {
            GameCommands dispatcher = new GameCommands();
            dispatcher.Subscribe(new TestCommandHandler());
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'failed-event' command 'TestCommand' failed:")
            );

            List<GameResult> results = dispatcher.ExecuteRaw(
                new[]
                {
                    new TestCommand { SourceEventInstanceID = "failed-event", Throws = true },
                    new TestCommand { SourceEventInstanceID = "successful-event" },
                }
            );

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("successful-event", results[0].SourceEventInstanceID);
        }

        private sealed class TestCommand : GameCommand
        {
            public bool Throws { get; set; }
        }

        private sealed class TestCommandHandler : IGameCommandHandler<TestCommand>
        {
            /// <summary>
            /// Produces one factual result for each test command.
            /// </summary>
            /// <param name="commands">The commands.</param>
            /// <returns>The result of handle commands.</returns>
            public List<GameResult> HandleCommands(IReadOnlyList<TestCommand> commands)
            {
                if (commands[0].Throws)
                    throw new InvalidOperationException("test failure");

                return new List<GameResult> { new PlanetStatChangedResult() };
            }
        }
    }
}
