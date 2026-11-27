using System.IO;
using System.Text;
using NUnit.Framework;
using Rebellion.Content;
using Rebellion.Game.Galaxy;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
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
        public void Deserialize_TacticalBegin_PreservesHostEncounterIdentityAndSeeds()
        {
            BeginTacticalCombatRequest request = new()
            {
                PlanetID = "battle-planet",
                Tick = 17,
                BattleID = "0123456789abcdef0123456789abcdef",
                Seed = uint.MaxValue,
                ScenerySeed = 12345,
            };

            BeginTacticalCombatRequest restored = (BeginTacticalCombatRequest)
                MultiplayerPlayerRequestCodec.Deserialize(
                    request.Kind,
                    MultiplayerPlayerRequestCodec.Serialize(request)
                );

            Assert.AreEqual(request.PlanetID, restored.PlanetID);
            Assert.AreEqual(request.Tick, restored.Tick);
            Assert.AreEqual(request.BattleID, restored.BattleID);
            Assert.AreEqual(request.Seed, restored.Seed);
            Assert.AreEqual(request.ScenerySeed, restored.ScenerySeed);
        }

        [Test]
        public void Deserialize_TacticalCompletion_PreservesTypedLossesAndSideOutcomes()
        {
            CompleteTacticalCombatRequest request = new()
            {
                PlanetID = "battle-planet",
                Tick = 17,
                BattleID = "0123456789abcdef0123456789abcdef",
                Outcome = new TacticalCombatOutcome
                {
                    Winner = SpaceCombatResult.BattleOutcome.Attacker,
                    AttackerOutcome = SpaceCombatResult.SideOutcome.Active,
                    DefenderOutcome = SpaceCombatResult.SideOutcome.Withdrawn,
                    Ships = new[]
                    {
                        new TacticalCombatOutcome.ShipOutcome
                        {
                            UnitID = "ship",
                            Hull = 37,
                            SystemDamage = new CapitalShipSystemDamage { Engines = 2 },
                            Withdrawn = true,
                        },
                    },
                    Fighters = new[]
                    {
                        new TacticalCombatOutcome.FighterOutcome
                        {
                            UnitID = "fighter",
                            SquadronSize = 5,
                            CarrierID = "ship",
                            Withdrawn = true,
                        },
                    },
                },
            };

            CompleteTacticalCombatRequest restored = (CompleteTacticalCombatRequest)
                MultiplayerPlayerRequestCodec.Deserialize(
                    request.Kind,
                    MultiplayerPlayerRequestCodec.Serialize(request)
                );

            Assert.AreEqual(request.BattleID, restored.BattleID);
            Assert.AreEqual(37, restored.Outcome.Ships[0].Hull);
            Assert.AreEqual(2, restored.Outcome.Ships[0].SystemDamage.Engines);
            Assert.IsTrue(restored.Outcome.Ships[0].Withdrawn);
            Assert.AreEqual(5, restored.Outcome.Fighters[0].SquadronSize);
            Assert.AreEqual("ship", restored.Outcome.Fighters[0].CarrierID);
            Assert.AreEqual(request.Outcome.Winner, restored.Outcome.Winner);
            Assert.AreEqual(request.Outcome.AttackerOutcome, restored.Outcome.AttackerOutcome);
            Assert.AreEqual(request.Outcome.DefenderOutcome, restored.Outcome.DefenderOutcome);
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
