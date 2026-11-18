using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;
using Rebellion.Tactical;

namespace Rebellion.Architecture.Tests
{
    [TestFixture]
    public sealed class TransportAssemblyTests
    {
        [Test]
        public void Transport_Compilation_IsSeparateFromModeImplementations()
        {
            var transport = typeof(IMultiplayerPeer).Assembly;

            Assert.That(transport, Is.Not.EqualTo(typeof(GameRoot).Assembly));
            Assert.That(transport, Is.Not.EqualTo(typeof(TacticalCombatSession).Assembly));
            Assert.That(typeof(MultiplayerPacket).Assembly, Is.EqualTo(transport));
            Assert.That(typeof(MultiplayerPacketCodec).Assembly, Is.EqualTo(transport));
            Assert.That(typeof(MultiplayerPacketFramer).Assembly, Is.EqualTo(transport));
            Assert.That(typeof(MultiplayerPacketStream).Assembly, Is.EqualTo(transport));
        }

        [Test]
        public void Transport_References_ExcludeApplicationModesAndEngine()
        {
            string[] references = typeof(IMultiplayerPeer)
                .Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain(typeof(GameRoot).Assembly.GetName().Name));
            Assert.That(
                references.Any(name =>
                    name.StartsWith("Rebellion.", StringComparison.Ordinal)
                    || name.StartsWith("Unity", StringComparison.Ordinal)
                ),
                Is.False
            );
        }
    }
}
