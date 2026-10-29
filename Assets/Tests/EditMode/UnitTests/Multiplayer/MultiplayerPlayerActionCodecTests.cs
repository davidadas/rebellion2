using System.IO;
using System.Text;
using NUnit.Framework;
using Rebellion.App.PlayerActions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerPlayerActionCodecTests
    {
        [Test]
        public void Deserialize_Move_PreservesOrderedSelectionAndDestination()
        {
            MovePlayerAction action = new()
            {
                ItemIDs = new[] { "ship-b", "ship-a" },
                DestinationID = "destination",
            };

            MovePlayerAction restored = (MovePlayerAction)
                MultiplayerPlayerActionCodec.Deserialize(
                    action.Kind,
                    MultiplayerPlayerActionCodec.Serialize(action)
                );

            Assert.That(restored.ItemIDs, Is.EqualTo(action.ItemIDs));
            Assert.That(restored.DestinationID, Is.EqualTo(action.DestinationID));
        }

        [Test]
        public void Deserialize_Rename_PreservesLiteralUnicodeText()
        {
            RenamePlayerAction action = new() { EntityID = "ship", Name = "\u00c9toile \"$type\"" };

            RenamePlayerAction restored = (RenamePlayerAction)
                MultiplayerPlayerActionCodec.Deserialize(
                    action.Kind,
                    MultiplayerPlayerActionCodec.Serialize(action)
                );

            Assert.That(restored.Name, Is.EqualTo(action.Name));
        }

        [Test]
        public void Deserialize_Mission_PreservesObservedTargetInsteadOfResolvingHiddenState()
        {
            StartMissionPlayerAction action = new()
            {
                MissionTypeID = "mission",
                ObservedLocation = new Planet { InstanceID = "location" },
                ObservedTarget = new Officer { InstanceID = "target", DisplayName = "Observed" },
                MainParticipantIDs = new[] { "agent" },
                DecoyParticipantIDs = new[] { "decoy" },
                IsRepeating = true,
            };

            StartMissionPlayerAction restored = (StartMissionPlayerAction)
                MultiplayerPlayerActionCodec.Deserialize(
                    action.Kind,
                    MultiplayerPlayerActionCodec.Serialize(action)
                );

            Assert.That(restored.ObservedTarget, Is.TypeOf<Officer>());
            Assert.That(restored.ObservedTarget.DisplayName, Is.EqualTo("Observed"));
            Assert.That(restored.ObservedLocation.InstanceID, Is.EqualTo("location"));
            Assert.That(restored.MainParticipantIDs, Is.EqualTo(action.MainParticipantIDs));
            Assert.That(restored.DecoyParticipantIDs, Is.EqualTo(action.DecoyParticipantIDs));
            Assert.That(restored.IsRepeating, Is.True);
        }

        [Test]
        public void Deserialize_UnknownKind_RejectsRequest()
        {
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerPlayerActionCodec.Deserialize(
                    (PlayerActionKind)9999,
                    Encoding.UTF8.GetBytes("{}")
                )
            );
        }

        [TestCase("{\"EntityID\":\"ship\",\"actorFactionID\":\"opponent\"}")]
        [TestCase("{\"$type\":\"System.IO.FileInfo, mscorlib\"}")]
        [TestCase("{} {}")]
        [TestCase("null")]
        public void Deserialize_ParametersOutsideSchema_RejectsRequest(string json)
        {
            Assert.That(
                () =>
                    MultiplayerPlayerActionCodec.Deserialize(
                        PlayerActionKind.Rename,
                        Encoding.UTF8.GetBytes(json)
                    ),
                Throws.Exception
            );
        }
    }
}
