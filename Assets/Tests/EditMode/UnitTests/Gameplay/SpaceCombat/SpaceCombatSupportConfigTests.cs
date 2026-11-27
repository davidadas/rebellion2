using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Content;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class SpaceCombatSupportConfigTests
    {
        [TestCase(
            "first",
            GameDifficulty.Easy,
            GameConfig.SpaceCombatSupportContext.FirstFactionEasy
        )]
        [TestCase(
            "first",
            GameDifficulty.Medium,
            GameConfig.SpaceCombatSupportContext.FirstFactionMedium
        )]
        [TestCase(
            "first",
            GameDifficulty.Hard,
            GameConfig.SpaceCombatSupportContext.FirstFactionHard
        )]
        [TestCase(
            "second",
            GameDifficulty.Easy,
            GameConfig.SpaceCombatSupportContext.SecondFactionEasy
        )]
        [TestCase(
            "second",
            GameDifficulty.Medium,
            GameConfig.SpaceCombatSupportContext.SecondFactionMedium
        )]
        [TestCase(
            "second",
            GameDifficulty.Hard,
            GameConfig.SpaceCombatSupportContext.SecondFactionHard
        )]
        public void TryResolveProfile_SavedLaunchSetup_SelectsExactContext(
            string launchFaction,
            GameDifficulty difficulty,
            GameConfig.SpaceCombatSupportContext expected
        )
        {
            GameRoot game = CreateGame(launchFaction, difficulty);
            GameConfig.SpaceCombatSupportConfig config = CreateConfig();

            Assert.IsTrue(
                config.TryResolveProfile(game, CreateTacticalDefinition(), out var profile, out _)
            );
            Assert.AreSame(config.Profiles[expected], profile);
        }

        [Test]
        public void TryResolveProfile_ChangedLocalFactionAndControllers_KeepsSavedLaunchContext()
        {
            GameRoot game = CreateGame("first", GameDifficulty.Hard);
            game.LocalPlayerFactionID = "second";
            game.SetFactionController("first", "computer", PlayerControllerType.AI);
            game.SetFactionController("second", "viewer", PlayerControllerType.Human);
            GameConfig.SpaceCombatSupportConfig config = CreateConfig();

            Assert.IsTrue(
                config.TryResolveProfile(game, CreateTacticalDefinition(), out var profile, out _)
            );
            Assert.AreSame(
                config.Profiles[GameConfig.SpaceCombatSupportContext.FirstFactionHard],
                profile
            );
        }

        [TestCase("first")]
        [TestCase("second")]
        public void TryResolveProfile_RestoredMultiplayerIdentity_SelectsMultiplayerProfile(
            string localFaction
        )
        {
            GameRoot game = CreateGame("unmapped-launch-faction", (GameDifficulty)99);
            game.Metadata.MultiplayerCampaign = new MultiplayerCampaignIdentity
            {
                HostUserID = "host",
                GuestUserID = "guest",
                HostFactionID = "first",
                GuestFactionID = "second",
            };
            GameRoot restored = SerializationHelper.Deserialize<GameRoot>(
                SerializationHelper.Serialize(game)
            );
            restored.LocalPlayerFactionID = localFaction;
            GameConfig.SpaceCombatSupportConfig config = CreateConfig();

            Assert.IsTrue(
                config.TryResolveProfile(
                    restored,
                    CreateTacticalDefinition(),
                    out var profile,
                    out _
                )
            );
            Assert.AreSame(
                config.Profiles[GameConfig.SpaceCombatSupportContext.Multiplayer],
                profile
            );
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("third")]
        public void TryResolveProfile_UnsupportedLaunchFaction_DoesNotSelectAnotherProfile(
            string faction
        )
        {
            GameRoot game = CreateGame(faction, GameDifficulty.Easy);

            Assert.IsFalse(
                CreateConfig()
                    .TryResolveProfile(game, CreateTacticalDefinition(), out var profile, out _)
            );
            Assert.IsNull(profile);
        }

        [Test]
        public void TryResolveProfile_UnsupportedDifficulty_DoesNotSelectAnotherProfile()
        {
            GameRoot game = CreateGame("first", (GameDifficulty)99);

            Assert.IsFalse(
                CreateConfig()
                    .TryResolveProfile(game, CreateTacticalDefinition(), out var profile, out _)
            );
            Assert.IsNull(profile);
        }

        [Test]
        public void TryResolveProfile_MissingCampaignFaction_DoesNotSelectAnotherProfile()
        {
            GameRoot game = CreateGame("first", GameDifficulty.Easy);
            game.GetFactions().RemoveAt(1);

            Assert.IsFalse(
                CreateConfig()
                    .TryResolveProfile(game, CreateTacticalDefinition(), out var profile, out _)
            );
            Assert.IsNull(profile);
        }

        [TestCase("missing")]
        [TestCase("duplicate-index")]
        [TestCase("duplicate-identity")]
        [TestCase("invalid-index")]
        public void TryResolveProfile_InvalidAuthoredFactionMap_DoesNotInferSideIdentity(
            string scenario
        )
        {
            TacticalCombatDefinition definition = CreateTacticalDefinition();
            if (scenario == "missing")
                definition.Factions.RemoveAt(1);
            else if (scenario == "duplicate-index")
                definition.Factions[1].FactionIndex = 0;
            else if (scenario == "duplicate-identity")
                definition.Factions[1].FactionInstanceID = "first";
            else
                definition.Factions[1].FactionIndex = 2;

            Assert.IsFalse(
                CreateConfig()
                    .TryResolveProfile(
                        CreateGame("first", GameDifficulty.Easy),
                        definition,
                        out var profile,
                        out _
                    )
            );
            Assert.IsNull(profile);
        }

        [Test]
        public void TryResolveProfile_MissingSelectedProfile_DoesNotUseOtherContexts()
        {
            GameConfig.SpaceCombatSupportConfig config = CreateConfig();
            config.Profiles[(GameConfig.SpaceCombatSupportContext)0] = config.Profiles[
                GameConfig.SpaceCombatSupportContext.FirstFactionEasy
            ];
            config.Profiles.Remove(GameConfig.SpaceCombatSupportContext.FirstFactionEasy);

            Assert.IsFalse(
                config.TryResolveProfile(
                    CreateGame("first", GameDifficulty.Easy),
                    CreateTacticalDefinition(),
                    out var profile,
                    out _
                )
            );
            Assert.IsNull(profile);
        }

        [TestCase("divisor")]
        [TestCase("first-side")]
        [TestCase("second-side")]
        [TestCase("first-leading-flat")]
        [TestCase("first-leading-gap")]
        [TestCase("first-trailing-flat")]
        [TestCase("first-trailing-gap")]
        [TestCase("first-cap")]
        [TestCase("first-threshold")]
        [TestCase("second-leading-flat")]
        [TestCase("second-leading-gap")]
        [TestCase("second-trailing-flat")]
        [TestCase("second-trailing-gap")]
        [TestCase("second-cap")]
        [TestCase("second-threshold")]
        public void TryResolveProfile_MissingRequiredInput_DoesNotSupplyDefault(string missing)
        {
            GameConfig.SpaceCombatSupportConfig config = CreateConfig();
            GameConfig.SpaceCombatSupportProfile profile = config.Profiles[
                GameConfig.SpaceCombatSupportContext.FirstFactionEasy
            ];
            switch (missing)
            {
                case "divisor":
                    profile.Divisor = null;
                    break;
                case "first-side":
                    profile.FirstSide = null;
                    break;
                case "second-side":
                    profile.SecondSide = null;
                    break;
                case "first-leading-flat":
                    profile.FirstSide.LeadingFlat = null;
                    break;
                case "first-leading-gap":
                    profile.FirstSide.LeadingGapScale = null;
                    break;
                case "first-trailing-flat":
                    profile.FirstSide.TrailingFlat = null;
                    break;
                case "first-trailing-gap":
                    profile.FirstSide.TrailingGapScale = null;
                    break;
                case "first-cap":
                    profile.FirstSide.MagnitudeCap = null;
                    break;
                case "first-threshold":
                    profile.FirstSide.SupportThreshold = null;
                    break;
                case "second-leading-flat":
                    profile.SecondSide.LeadingFlat = null;
                    break;
                case "second-leading-gap":
                    profile.SecondSide.LeadingGapScale = null;
                    break;
                case "second-trailing-flat":
                    profile.SecondSide.TrailingFlat = null;
                    break;
                case "second-trailing-gap":
                    profile.SecondSide.TrailingGapScale = null;
                    break;
                case "second-cap":
                    profile.SecondSide.MagnitudeCap = null;
                    break;
                case "second-threshold":
                    profile.SecondSide.SupportThreshold = null;
                    break;
            }

            Assert.IsFalse(
                config.TryResolveProfile(
                    CreateGame("first", GameDifficulty.Easy),
                    CreateTacticalDefinition(),
                    out var selected,
                    out _
                )
            );
            Assert.IsNull(selected);
        }

        [Test]
        public void TryResolveProfile_ZeroDivisor_DoesNotSupplyDefault()
        {
            GameConfig.SpaceCombatSupportConfig config = CreateConfig();
            config.Profiles[GameConfig.SpaceCombatSupportContext.FirstFactionEasy].Divisor = 0;

            Assert.IsFalse(
                config.TryResolveProfile(
                    CreateGame("first", GameDifficulty.Easy),
                    CreateTacticalDefinition(),
                    out _,
                    out _
                )
            );
        }

        [Test]
        public void TryResolveProfile_ExplicitZeroWeights_PreservesAuthoredZeros()
        {
            GameConfig.SpaceCombatSupportConfig config = CreateConfig();
            GameConfig.SpaceCombatSupportProfile profile = config.Profiles[
                GameConfig.SpaceCombatSupportContext.FirstFactionEasy
            ];
            profile.FirstSide = new GameConfig.SpaceCombatSupportSideConfig
            {
                LeadingFlat = 0,
                LeadingGapScale = 0,
                TrailingFlat = 0,
                TrailingGapScale = 0,
                MagnitudeCap = 0,
                SupportThreshold = 0,
            };

            Assert.IsTrue(
                config.TryResolveProfile(
                    CreateGame("first", GameDifficulty.Easy),
                    CreateTacticalDefinition(),
                    out var selected,
                    out _
                )
            );
            Assert.AreSame(profile, selected);
        }

        [Test]
        public void CreateCopy_NestedProfiles_OwnsIndependentValues()
        {
            GameConfig.SpaceCombatSupportConfig source = CreateConfig();
            string before = SerializationHelper.Serialize(source);

            GameConfig.SpaceCombatSupportConfig copy = source.CreateCopy();
            copy.Profiles[GameConfig.SpaceCombatSupportContext.FirstFactionEasy]
                .FirstSide
                .LeadingGapScale = 999;
            copy.Profiles[GameConfig.SpaceCombatSupportContext.SecondFactionHard]
                .SecondSide
                .SupportThreshold = 7;
            copy.Profiles.Remove(GameConfig.SpaceCombatSupportContext.Multiplayer);

            Assert.AreEqual(before, SerializationHelper.Serialize(source));
        }

        [Test]
        public void CreateCopy_MissingInputs_PreservesMissingValues()
        {
            GameConfig.SpaceCombatSupportConfig source = new()
            {
                Profiles = new()
                {
                    [GameConfig.SpaceCombatSupportContext.FirstFactionEasy] = new(),
                },
            };

            GameConfig.SpaceCombatSupportConfig restored =
                SerializationHelper.Deserialize<GameConfig.SpaceCombatSupportConfig>(
                    SerializationHelper.Serialize(source.CreateCopy())
                );

            Assert.IsNull(
                restored.Profiles[GameConfig.SpaceCombatSupportContext.FirstFactionEasy].Divisor
            );
            Assert.IsNull(
                restored.Profiles[GameConfig.SpaceCombatSupportContext.FirstFactionEasy].FirstSide
            );
            Assert.IsNull(
                restored.Profiles[GameConfig.SpaceCombatSupportContext.FirstFactionEasy].SecondSide
            );
        }

        [Test]
        public void Serialize_AllProfileInputs_RoundTripsExactly()
        {
            GameConfig.SpaceCombatSupportConfig source = CreateConfig();
            string xml = SerializationHelper.Serialize(source);

            GameConfig.SpaceCombatSupportConfig restored =
                SerializationHelper.Deserialize<GameConfig.SpaceCombatSupportConfig>(xml);

            Assert.AreEqual(xml, SerializationHelper.Serialize(restored));
        }

        /// <summary>
        /// Creates a campaign whose live controllers are independent of saved setup.
        /// </summary>
        /// <param name="faction">The saved launch faction.</param>
        /// <param name="difficulty">The saved launch difficulty.</param>
        /// <returns>The campaign containing both authored strategic factions.</returns>
        private static GameRoot CreateGame(string faction, GameDifficulty difficulty)
        {
            GameRoot game = new()
            {
                Summary = new GameSummary { PlayerFactionID = faction, Difficulty = difficulty },
            };
            game.GetFactions().Add(new Faction { InstanceID = "first" });
            game.GetFactions().Add(new Faction { InstanceID = "second" });
            return game;
        }

        /// <summary>
        /// Creates the shared index map with deliberately noncanonical faction IDs.
        /// </summary>
        /// <returns>The authored strategic-index definitions.</returns>
        private static TacticalCombatDefinition CreateTacticalDefinition() =>
            new()
            {
                Factions = new()
                {
                    new() { FactionInstanceID = "first", FactionIndex = 0 },
                    new() { FactionInstanceID = "second", FactionIndex = 1 },
                },
            };

        /// <summary>
        /// Creates seven distinguishable, fully supplied profile records.
        /// </summary>
        /// <returns>The authored profile collection.</returns>
        private static GameConfig.SpaceCombatSupportConfig CreateConfig()
        {
            GameConfig.SpaceCombatSupportConfig config = new();
            foreach (
                GameConfig.SpaceCombatSupportContext context in Enum.GetValues(
                    typeof(GameConfig.SpaceCombatSupportContext)
                )
            )
                config.Profiles.Add(
                    context,
                    new GameConfig.SpaceCombatSupportProfile
                    {
                        Divisor = (int)context,
                        FirstSide = CreateSide((int)context * 10),
                        SecondSide = CreateSide((int)context * 20),
                    }
                );
            return config;
        }

        /// <summary>
        /// Creates a side whose every scalar has an independent observable value.
        /// </summary>
        /// <param name="value">The basis for the six authored values.</param>
        /// <returns>The fully supplied side settings.</returns>
        private static GameConfig.SpaceCombatSupportSideConfig CreateSide(int value) =>
            new()
            {
                LeadingFlat = value,
                LeadingGapScale = value + 1,
                TrailingFlat = value + 2,
                TrailingGapScale = value + 3,
                MagnitudeCap = value + 4,
                SupportThreshold = (uint)value + 5,
            };
    }
}
