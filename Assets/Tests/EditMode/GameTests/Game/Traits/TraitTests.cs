using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Traits;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Traits
{
    [TestFixture]
    public sealed class TraitTests
    {
        [Test]
        public void SerializeDeserialize_PreservesAuthoredEffects()
        {
            Trait original = new Trait
            {
                ID = "smuggler",
                Name = "Smuggler",
                Description = "Personnel traveling with this character move twice as quickly.",
                Effects = new List<Modifier>
                {
                    new Modifier
                    {
                        Type = ModifierType.TravelSpeed,
                        Operation = ModifierOperation.Multiply,
                        Value = 2m,
                        Target = ModifierTarget.MovementGroup,
                        Recipient = ModifierRecipient.Officer,
                    },
                },
            };

            string xml = SerializationHelper.Serialize(original);
            Trait deserialized = SerializationHelper.Deserialize<Trait>(xml);

            Assert.AreEqual(original.ID, deserialized.ID);
            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Description, deserialized.Description);
            Assert.AreEqual(1, deserialized.Effects.Count);
            Assert.AreEqual(ModifierType.TravelSpeed, deserialized.Effects[0].Type);
            Assert.AreEqual(ModifierOperation.Multiply, deserialized.Effects[0].Operation);
            Assert.AreEqual(2m, deserialized.Effects[0].Value);
            Assert.AreEqual(ModifierTarget.MovementGroup, deserialized.Effects[0].Target);
            Assert.AreEqual(ModifierRecipient.Officer, deserialized.Effects[0].Recipient);
        }

        [Test]
        public void SerializeDeserialize_SupportsSeatOfPowerLeadershipEffect()
        {
            Trait original = new Trait
            {
                ID = "seat-of-power",
                Name = "Seat of Power",
                Effects = new List<Modifier>
                {
                    new Modifier
                    {
                        Type = ModifierType.Leadership,
                        Operation = ModifierOperation.Multiply,
                        Value = 1.5m,
                        Target = ModifierTarget.Faction,
                        Recipient = ModifierRecipient.Officer,
                    },
                },
            };

            string xml = SerializationHelper.Serialize(original);
            Trait deserialized = SerializationHelper.Deserialize<Trait>(xml);

            Assert.AreEqual(ModifierType.Leadership, deserialized.Effects[0].Type);
            Assert.AreEqual(1.5m, deserialized.Effects[0].Value);
            Assert.AreEqual(ModifierTarget.Faction, deserialized.Effects[0].Target);
        }

        [Test]
        public void Resolve_FactionTrait_AppliesOnlyToMatchingFactionAndRecipient()
        {
            Trait seatOfPower = new Trait
            {
                ID = "seat-of-power",
                Effects = new List<Modifier>
                {
                    new Modifier
                    {
                        Type = ModifierType.Leadership,
                        Value = 1.5m,
                        Target = ModifierTarget.Faction,
                        Recipient = ModifierRecipient.Officer,
                    },
                },
            };
            GameRoot game = BuildGame(out Planet planet);
            Officer emperor = AttachOfficer(game, planet, "emperor", "empire");
            emperor.TraitIDs.Add(seatOfPower.ID);
            Officer imperial = AttachOfficer(game, planet, "imperial", "empire");
            Officer rebel = AttachOfficer(game, planet, "rebel", "rebels");
            game.ConfigureModifiers(new[] { seatOfPower }, null);

            Assert.AreEqual(75, game.Modifiers.Resolve(ModifierType.Leadership, imperial, 50));
            Assert.AreEqual(50, game.Modifiers.Resolve(ModifierType.Leadership, rebel, 50));
        }

        [Test]
        public void Resolve_MovementGroupTrait_AppliesToOfficerPassengersOnly()
        {
            Trait smuggler = new Trait
            {
                ID = "smuggler",
                Effects = new List<Modifier>
                {
                    new Modifier
                    {
                        Type = ModifierType.TravelSpeed,
                        Value = 2m,
                        Target = ModifierTarget.MovementGroup,
                        Recipient = ModifierRecipient.Officer,
                    },
                },
            };
            GameRoot game = BuildGame(out Planet planet);
            Officer smugglerOfficer = AttachOfficer(game, planet, "han", "rebels");
            smugglerOfficer.TraitIDs.Add(smuggler.ID);
            Officer passenger = AttachOfficer(game, planet, "passenger", "rebels");
            Officer other = AttachOfficer(game, planet, "other", "rebels");
            smugglerOfficer.Movement = new MovementState { MovementGroupID = "together" };
            passenger.Movement = new MovementState { MovementGroupID = "together" };
            other.Movement = new MovementState { MovementGroupID = "elsewhere" };
            game.ConfigureModifiers(new[] { smuggler }, null);

            Assert.AreEqual(2m, game.Modifiers.Resolve(ModifierType.TravelSpeed, passenger, 1m));
            Assert.AreEqual(1m, game.Modifiers.Resolve(ModifierType.TravelSpeed, other, 1m));
        }

        [Test]
        public void Resolve_StatusEffect_StacksAdditionBeforeMultiplicationAndIgnoresExpiredEffect()
        {
            StatusEffect effect = new StatusEffect
            {
                ID = "inspired",
                Modifiers = new List<Modifier>
                {
                    new Modifier
                    {
                        Type = ModifierType.Leadership,
                        Operation = ModifierOperation.Add,
                        Value = 10m,
                    },
                    new Modifier
                    {
                        Type = ModifierType.Leadership,
                        Operation = ModifierOperation.Multiply,
                        Value = 1.5m,
                    },
                },
            };
            GameRoot game = BuildGame(out Planet planet);
            Officer officer = AttachOfficer(game, planet, "officer", "rebels");
            officer.ActiveStatusEffects[effect.ID] = 20;
            game.CurrentTick = 19;
            game.ConfigureModifiers(null, new[] { effect });

            Assert.AreEqual(90, game.Modifiers.Resolve(ModifierType.Leadership, officer, 50));

            game.CurrentTick = 20;
            game.Modifiers.RemoveExpiredStatusEffects();
            Assert.IsFalse(officer.ActiveStatusEffects.ContainsKey(effect.ID));
            Assert.AreEqual(50, game.Modifiers.Resolve(ModifierType.Leadership, officer, 50));
        }

        private static GameRoot BuildGame(out Planet planet)
        {
            GameRoot game = new GameRoot(new GameConfig());
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            planet = new Planet { InstanceID = "planet", OwnerInstanceID = "rebels" };
            game.AttachNode(planet, sector);
            return game;
        }

        private static Officer AttachOfficer(
            GameRoot game,
            Planet planet,
            string id,
            string ownerID
        )
        {
            Officer officer = new Officer { InstanceID = id, OwnerInstanceID = ownerID };
            game.AttachNode(officer, planet);
            return officer;
        }
    }
}
