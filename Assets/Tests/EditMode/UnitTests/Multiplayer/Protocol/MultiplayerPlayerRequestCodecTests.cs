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

        [TestCase("\u00c9toile <ship> & \"name\"")]
        [TestCase(" \t\r\n ")]
        public void Deserialize_Rename_PreservesLiteralUnicodeText(string text)
        {
            RenameRequest action = new() { EntityID = "ship", Name = text };

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
                    Encoding.UTF8.GetBytes("<UnknownRequest />")
                )
            );
        }

        [TestCase(
            "<RenameRequest><EntityID>ship</EntityID><actorFactionID>opponent</actorFactionID></RenameRequest>"
        )]
        [TestCase("<FileInfo />")]
        [TestCase("<RenameRequest /><RenameRequest />")]
        [TestCase("<MoveRequest />")]
        public void Deserialize_ParametersOutsideSchema_RejectsRequest(string xml)
        {
            Assert.That(
                () =>
                    MultiplayerPlayerRequestCodec.Deserialize(
                        PlayerRequestKind.Rename,
                        Encoding.UTF8.GetBytes(xml)
                    ),
                Throws.Exception
            );
        }
    }
}
