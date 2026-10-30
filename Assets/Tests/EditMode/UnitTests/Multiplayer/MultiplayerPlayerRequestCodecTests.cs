using System.IO;
using System.Text;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Units;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerPlayerRequestCodecTests
    {
        [Test]
        public void Deserialize_Move_PreservesOrderedSelectionAndDestination()
        {
            MoveRequest action = new()
            {
                ItemIDs = new[] { "ship-b", "ship-a" },
                DestinationID = "destination",
            };

            MoveRequest restored = (MoveRequest)
                MultiplayerPlayerRequestCodec.Deserialize(
                    action.Kind,
                    MultiplayerPlayerRequestCodec.Serialize(action)
                );

            Assert.That(restored.ItemIDs, Is.EqualTo(action.ItemIDs));
            Assert.That(restored.DestinationID, Is.EqualTo(action.DestinationID));
        }

        [Test]
        public void Deserialize_Rename_PreservesLiteralUnicodeText()
        {
            RenameRequest action = new() { EntityID = "ship", Name = "\u00c9toile \"$type\"" };

            RenameRequest restored = (RenameRequest)
                MultiplayerPlayerRequestCodec.Deserialize(
                    action.Kind,
                    MultiplayerPlayerRequestCodec.Serialize(action)
                );

            Assert.That(restored.Name, Is.EqualTo(action.Name));
        }

        [Test]
        public void Deserialize_Mission_PreservesObservedTargetInsteadOfResolvingHiddenState()
        {
            StartMissionRequest action = new()
            {
                MissionTypeID = "mission",
                ObservedLocation = new Planet { InstanceID = "location" },
                ObservedTarget = new Officer { InstanceID = "target", DisplayName = "Observed" },
                MainParticipantIDs = new[] { "agent" },
                DecoyParticipantIDs = new[] { "decoy" },
                IsRepeating = true,
            };

            StartMissionRequest restored = (StartMissionRequest)
                MultiplayerPlayerRequestCodec.Deserialize(
                    action.Kind,
                    MultiplayerPlayerRequestCodec.Serialize(action)
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
                MultiplayerPlayerRequestCodec.Deserialize(
                    (PlayerRequestKind)9999,
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
                    MultiplayerPlayerRequestCodec.Deserialize(
                        PlayerRequestKind.Rename,
                        Encoding.UTF8.GetBytes(json)
                    ),
                Throws.Exception
            );
        }
    }
}
