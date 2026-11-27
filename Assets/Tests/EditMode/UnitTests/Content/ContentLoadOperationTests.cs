using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentLoadOperationTests
    {
        private string _directory;
        private string _settingsPath;
        private string _modDirectory;
        private ContentPack _fixturePack;
        private UserSettingsManager _settings;
        private SaveGameManager _saves;
        private ContentPack _previous;

        /// <summary>
        /// Creates isolated preferences and saves with a distinct previous content identity.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "content-load-" + Guid.NewGuid().ToString("N")
            );
            _settingsPath = Path.Combine(_directory, "user-settings.json");
            _settings = CreateSettings(_settingsPath);
            _settings.Settings.Content.ActivePackID = "previous";
            _settings.Settings.Content.ActiveScenarioID = "previous-scenario";
            _settings.Settings.Content.DisabledModIDs = new[] { "other-pack-mod", "previous-mod" };
            _settings.Save();
            _saves = new SaveGameManager(Path.Combine(_directory, "saves"));
            ContentPack source = TestPackFixture.Install(Path.Combine(_directory, "Content"));
            _fixturePack = source;
            _previous = new ContentPack(
                source.ContentRootPath,
                source.PackRootPath,
                source.FileResolver,
                new ContentPackDefinition { ID = "previous" },
                source.Scenario,
                source.Factions,
                source.GameData,
                new Dictionary<string, ContentPreloadManifest>()
            );
        }

        /// <summary>
        /// Deletes only files and content owned by this fixture.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_modDirectory != null && Directory.Exists(_modDirectory))
                Directory.Delete(_modDirectory, true);
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public async Task PrepareContentReloadAsync_PendingSelection_LeavesSettingsFileUnchangedAsync()
        {
            byte[] before = File.ReadAllBytes(_settingsPath);
            ContentLoadOperation operation = CreateOperation();

            await operation.PrepareContentReloadAsync(
                _fixturePack.Definition.ID,
                Array.Empty<string>()
            );
            _settings.Save();

            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
            Assert.IsTrue(operation.RequiresContentRestart);
            Assert.AreEqual("MainMenu", operation.DestinationScene);
            Assert.IsNull(operation.SaveFileName);
        }

        [Test]
        public void PrepareContentReloadAsync_MissingStartingOfficer_PreservesAppliedSelection()
        {
            CreateMod();
            string relative = _fixturePack.Scenario.GenerationConfigPath;
            XDocument generation = XDocument.Load(
                Path.Combine(_fixturePack.PackRootPath, relative)
            );
            generation.Descendants("OfficerInstanceID").First().Value = "missing-officer";
            string address = Path.Combine(_modDirectory, "Content", "Pack", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(address));
            generation.Save(address);
            ContentLoadOperation operation = CreateOperation();

            var error = Assert.ThrowsAsync<InvalidDataException>(() =>
                operation.PrepareContentReloadAsync(
                    _fixturePack.Definition.ID,
                    Array.Empty<string>()
                )
            );

            Assert.That(error.Message, Does.Contain("starting officer 'missing-officer'"));
            Assert.That(operation.Pack, Is.SameAs(_previous));
            Assert.That(
                CreateSettings(_settingsPath).Settings.Content.ActivePackID,
                Is.EqualTo("previous")
            );
        }

        [Test]
        public void PrepareContentReloadAsync_ComposedArtworkAliasCycle_PreservesAppliedSelection()
        {
            CreateMod();
            string modPack = Path.Combine(_modDirectory, "Content", "Pack");
            Directory.CreateDirectory(modPack);
            File.WriteAllText(
                Path.Combine(_fixturePack.PackRootPath, ContentArtworkAliases.FileName),
                "<ArtworkAliases><Image From='Pack/first' To='Pack/second'/></ArtworkAliases>"
            );
            File.WriteAllText(
                Path.Combine(modPack, ContentArtworkAliases.FileName),
                "<ArtworkAliases><Image From='Pack/second' To='Pack/first'/></ArtworkAliases>"
            );
            ContentLoadOperation operation = CreateOperation();

            var error = Assert.ThrowsAsync<InvalidDataException>(() =>
                operation.PrepareContentReloadAsync(
                    _fixturePack.Definition.ID,
                    Array.Empty<string>()
                )
            );

            Assert.That(error.Message, Does.Contain("cycle between content layers"));
            Assert.That(operation.Pack, Is.SameAs(_previous));
        }

        [Test]
        public void Complete_InitializedCandidate_PersistsCompleteModSelection()
        {
            ContentLoadOperation operation = CreatePreparedOperation();

            operation.Complete(operation.Pack, _settings);

            UserSettingsManager reloaded = CreateSettings(_settingsPath);
            Assert.AreEqual(operation.Pack.Definition.ID, reloaded.Settings.Content.ActivePackID);
            CollectionAssert.AreEqual(
                new[] { "candidate-mod", "other-pack-mod" },
                reloaded.Settings.Content.DisabledModIDs
            );
        }

        [Test]
        public async Task Complete_AppliedOrder_RoundTripsThroughUserSettingsAsync()
        {
            string modID = CreateMod();
            ContentLoadOperation operation = CreateOperation();
            await operation.PrepareContentReloadAsync(
                _fixturePack.Definition.ID,
                Array.Empty<string>(),
                new[] { modID }
            );

            operation.Complete(operation.Pack, _settings);

            UserSettingsManager reloaded = CreateSettings(_settingsPath);
            Assert.That(
                reloaded.Settings.Content.GetModLoadOrder(_fixturePack.Definition.ID),
                Is.EqualTo(operation.Pack.FileResolver.Mods.Select(mod => mod.ID))
            );
            Assert.That(
                reloaded.Settings.Content.GetModVersions(_fixturePack.Definition.ID),
                Is.EqualTo(operation.Pack.FileResolver.Mods.Select(mod => mod.Version))
            );
        }

        [Test]
        public void HasCommittedContent_ReplacementReady_ChangesOnlyAfterSuccessfulCommit()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            Assert.IsFalse(operation.HasCommittedContent);

            operation.Complete(operation.Pack, _settings);

            Assert.IsTrue(operation.HasCommittedContent);
            Assert.IsFalse(operation.BeginRecovery());
        }

        [TestCase(7)]
        [TestCase(31)]
        [TestCase(89)]
        public async Task Complete_ShuffledMixedMods_RestartReproducesTheAppliedStackAsync(int seed)
        {
            const string shipCatalog = "Pack/Factions/Alliance/Data/capital-ships.xml";
            const string regimentCatalog = "Pack/Factions/Alliance/Data/regiments.xml";
            const string artwork = "Pack/Assets/stack-probe";
            var mods = new List<string>();
            var icons = new Dictionary<string, byte[]>();
            for (int variant = 0; variant < 8; variant++)
            {
                string upkeep = $"upkeep-{variant}";
                string shields = $"shields-{variant}";
                string names = $"ship-name-{variant}";
                string infantry = $"infantry-{variant}";
                string travel = $"travel-{variant}";
                string art = $"art-{variant}";
                mods.AddRange(new[] { upkeep, shields, names, infantry, travel, art });
                WriteModText(
                    upkeep,
                    shipCatalog,
                    $"<CapitalShips Inherit='true'><CapitalShip><TypeID>CSAL001</TypeID><MaintenanceCost>{40 + variant}</MaintenanceCost></CapitalShip></CapitalShips>"
                );
                WriteModText(
                    shields,
                    shipCatalog,
                    $"<CapitalShips Inherit='true'><CapitalShip><TypeID>CSAL001</TypeID><MaxShieldStrength>{240 + variant}</MaxShieldStrength></CapitalShip></CapitalShips>"
                );
                WriteModText(
                    names,
                    shipCatalog,
                    $"<CapitalShips Inherit='true'><CapitalShip><TypeID>CSAL001</TypeID><DisplayName>Trial cruiser {variant}</DisplayName></CapitalShip></CapitalShips>"
                );
                WriteModText(
                    infantry,
                    regimentCatalog,
                    $"<Regiments Inherit='true'><Regiment><TypeID>REAL001</TypeID><ConstructionCost>{15 + variant}</ConstructionCost></Regiment></Regiments>"
                );
                WriteModText(
                    travel,
                    "Application/Rules/game.xml",
                    $"<GameConfig Inherit='true'><Movement><DistanceDivisor>{2 + variant}</DistanceDivisor></Movement></GameConfig>"
                );
                CreateMod(art);
                byte[] icon = CreateStackIcon(variant);
                icons.Add(art, icon);
                string target = Path.Combine(_modDirectory, "Content", artwork + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllBytes(target, icon);
            }
            var random = new System.Random(seed);
            string[] order = mods.OrderBy(_ => random.Next()).ToArray();
            ContentLoadOperation operation = CreateOperation();
            await operation.PrepareContentReloadAsync(
                _fixturePack.Definition.ID,
                Array.Empty<string>(),
                order
            );
            operation.Complete(operation.Pack, _settings);
            WriteModText(
                "new-unapplied",
                shipCatalog,
                "<CapitalShips Inherit='true'><CapitalShip><TypeID>CSAL001</TypeID><MaintenanceCost>999</MaintenanceCost></CapitalShip></CapitalShips>"
            );
            UserContentSettings persisted = CreateSettings(_settingsPath).Settings.Content;

            ContentPack reopened = ContentPackLoader.OpenActive(
                _fixturePack.ContentRootPath,
                persisted.ActivePackID,
                persisted.ActiveScenarioID,
                persisted.DisabledModIDs,
                persisted.GetModLoadOrder,
                getModVersions: persisted.GetModVersions
            );

            var ship = reopened.GameData.CapitalShips.Single(unit => unit.TypeID == "CSAL001");
            var regiment = reopened.GameData.Regiments.Single(unit => unit.TypeID == "REAL001");
            Assert.That(reopened.FileResolver.Mods.Select(mod => mod.ID), Is.EqualTo(order));
            Assert.That(ship.MaintenanceCost, Is.EqualTo(40 + LastVariant("upkeep-")));
            Assert.That(ship.MaxShieldStrength, Is.EqualTo(240 + LastVariant("shields-")));
            Assert.That(ship.DisplayName, Is.EqualTo($"Trial cruiser {LastVariant("ship-name-")}"));
            Assert.That(regiment.ConstructionCost, Is.EqualTo(15 + LastVariant("infantry-")));
            Assert.That(
                reopened.GameData.GameConfig.Movement.DistanceDivisor,
                Is.EqualTo(2 + LastVariant("travel-"))
            );
            Assert.That(
                File.ReadAllBytes(reopened.FileResolver.ResolveFile(artwork, ".png")),
                Is.EqualTo(icons[$"art-{LastVariant("art-")}"])
            );

            int LastVariant(string prefix) =>
                int.Parse(
                    order.Last(id => id.StartsWith(prefix, System.StringComparison.Ordinal))[^1..]
                );
        }

        [Test]
        public async Task PrepareContentReloadAsync_IndependentCampaignMods_LoadsBothScenariosAsync()
        {
            WriteCampaignMod("border-skirmish", "Border skirmish", makeDefault: true);
            WriteCampaignMod("rim-defense", "Rim defense", makeDefault: false);
            ContentLoadOperation operation = CreateOperation();

            await operation.PrepareContentReloadAsync(
                _fixturePack.Definition.ID,
                Array.Empty<string>(),
                new[] { "rim-defense", "border-skirmish" }
            );

            Assert.That(operation.Pack.Scenario.ID, Is.EqualTo("border-skirmish"));
            Assert.That(
                operation.Pack.Scenarios.Select(scenario => scenario.ID),
                Is.EquivalentTo(new[] { "standard", "border-skirmish", "rim-defense" })
            );
        }

        [Test]
        public async Task PrepareContentReloadAsync_IndividuallyValidRuleEdits_RejectsTheInvalidCombinationAsync()
        {
            const string rules = "Application/Rules/game.xml";
            GameConfig defaults = _fixturePack.GameData.GameConfig.CreateCopy();
            defaults.Combat.Bombardment.StrikeRollMinimum = 1;
            defaults.Combat.Bombardment.StrikeRollMaximum = 10;
            File.WriteAllText(
                Path.Combine(_fixturePack.ContentRootPath, rules),
                SerializationHelper.Serialize(defaults)
            );
            WriteModText(
                "harder-minimum",
                rules,
                "<GameConfig Inherit='true'><Combat><Bombardment><StrikeRollMinimum>8</StrikeRollMinimum></Bombardment></Combat></GameConfig>"
            );
            WriteModText(
                "lower-maximum",
                rules,
                "<GameConfig Inherit='true'><Combat><Bombardment><StrikeRollMaximum>5</StrikeRollMaximum></Bombardment></Combat></GameConfig>"
            );
            await CreateOperation()
                .PrepareContentReloadAsync(_fixturePack.Definition.ID, new[] { "lower-maximum" });
            await CreateOperation()
                .PrepareContentReloadAsync(_fixturePack.Definition.ID, new[] { "harder-minimum" });
            byte[] committed = File.ReadAllBytes(_settingsPath);
            ContentLoadOperation combined = CreateOperation();

            var error = Assert.ThrowsAsync<InvalidDataException>(() =>
                combined.PrepareContentReloadAsync(
                    _fixturePack.Definition.ID,
                    Array.Empty<string>()
                )
            );

            Assert.That(error.Message, Does.Contain("strike roll minimum must not exceed"));
            Assert.That(combined.Pack, Is.SameAs(_previous));
            Assert.That(File.ReadAllBytes(_settingsPath), Is.EqualTo(committed));
        }

        [Test]
        public void Complete_DifferentContent_RejectsCompletionWithoutWritingSettings()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            byte[] before = File.ReadAllBytes(_settingsPath);

            Assert.Throws<InvalidOperationException>(() =>
                operation.Complete(_previous, _settings)
            );

            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void Complete_SettingsWriteFailure_RestoresPreferencesBeforeShutdownSave()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            string blockedPath = Path.Combine(_directory, "blocked.json");
            Directory.CreateDirectory(blockedPath);
            UserSettingsManager blocked = CreateSettings(blockedPath);

            Assert.Throws<IOException>(() => operation.Complete(operation.Pack, blocked));

            Directory.Delete(blockedPath);
            blocked.Save();
            UserSettingsManager reloaded = CreateSettings(blockedPath);
            Assert.AreEqual("previous", reloaded.Settings.Content.ActivePackID);
            CollectionAssert.AreEqual(
                new[] { "other-pack-mod", "previous-mod" },
                reloaded.Settings.Content.DisabledModIDs
            );
            Assert.IsTrue(operation.BeginRecovery());
        }

        [Test]
        public async Task BeginRecovery_PendingSavedGame_SelectsPreviousContentWithoutSaveRequestAsync()
        {
            SaveGame(_fixturePack);
            ContentLoadOperation operation = CreateOperation();
            await operation.PrepareSavedGameAsync(_saves, "campaign");

            Assert.IsTrue(operation.BeginRecovery());

            Assert.AreSame(_previous, operation.Pack);
            Assert.AreEqual("MainMenu", operation.DestinationScene);
            Assert.IsNull(operation.SaveFileName);
        }

        [Test]
        public void BeginRecovery_RecoveryAlreadyStarted_RejectsAnotherAttempt()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            operation.BeginRecovery();

            Assert.IsFalse(operation.BeginRecovery());
        }

        [Test]
        public void Complete_RestoredContent_PreservesOriginalSettingsFile()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            byte[] before = File.ReadAllBytes(_settingsPath);
            operation.BeginRecovery();
            _settings.Settings.Content.DisabledModIDs[0] = "uncommitted-change";

            operation.Complete(_previous, _settings);

            Assert.AreEqual("previous", _settings.Settings.Content.ActivePackID);
            CollectionAssert.AreEqual(
                new[] { "other-pack-mod", "previous-mod" },
                _settings.Settings.Content.DisabledModIDs
            );
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
            Assert.IsFalse(operation.BeginRecovery());
        }

        [Test]
        public void Complete_AlreadyCompleted_DoesNotWriteSettingsAgain()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            operation.Complete(operation.Pack, _settings);
            byte[] before = File.ReadAllBytes(_settingsPath);
            _settings.Settings.Content.ActivePackID = "later-selection";

            operation.Complete(operation.Pack, _settings);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
            Assert.IsFalse(operation.BeginRecovery());
        }

        [Test]
        public async Task PrepareSavedGameAsync_DifferentModSelection_PreservesNewGamePreferencesAsync()
        {
            string modID = CreateMod();
            ContentPack required = ContentPackLoader.OpenExact(
                _fixturePack.ContentRootPath,
                _fixturePack.Definition.ID,
                _fixturePack.Scenario.ID,
                Array.Empty<string>()
            );
            SaveGame(required);
            _settings.Settings.Content.DisabledModIDs = new[] { "other-pack-mod", modID };
            _settings.Settings.Content.SetModLoadOrder(
                _fixturePack.Definition.ID,
                new[] { "new-game-mod" }
            );
            _settings.Save();
            byte[] before = File.ReadAllBytes(_settingsPath);
            ContentLoadOperation operation = CreateOperation();

            await operation.PrepareSavedGameAsync(_saves, "campaign");
            operation.Complete(operation.Pack, _settings);

            Assert.IsTrue(operation.Pack.FileResolver.Mods.Any(mod => mod.ID == modID));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
            Assert.AreSame(_previous, operation.MenuPack);
        }

        [Test]
        public async Task PrepareSavedGameAsync_MatchingContent_ReusesContentWithoutSavingPreferencesAsync()
        {
            ContentPack current = _fixturePack;
            SaveGame(current);
            ContentLoadOperation operation = new ContentLoadOperation(
                current,
                _settings.Settings.Content,
                new[] { "main-menu", "strategy" }
            );
            byte[] before = File.ReadAllBytes(_settingsPath);

            await operation.PrepareSavedGameAsync(_saves, "campaign");
            operation.Complete(current, _settings);

            Assert.IsFalse(operation.RequiresContentRestart);
            Assert.AreSame(current, operation.Pack);
            Assert.AreEqual("campaign", operation.SaveFileName);
            Assert.AreEqual("StrategyView", operation.DestinationScene);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public async Task PrepareContentReloadAsync_CallerChangesPreferences_CommitsOriginalSelectionAsync()
        {
            ContentLoadOperation operation = CreateOperation();
            string[] disabled = { "selected-mod" };
            Task preparation = operation.PrepareContentReloadAsync(
                _fixturePack.Definition.ID,
                disabled
            );
            disabled[0] = "changed-mod";
            await preparation;

            operation.Complete(operation.Pack, _settings);

            CollectionAssert.AreEqual(
                new[] { "selected-mod" },
                _settings.Settings.Content.DisabledModIDs
            );
        }

        [Test]
        public void PrepareContentReloadAsync_AlreadyPrepared_RejectsAnotherRequest()
        {
            ContentLoadOperation operation = CreatePreparedOperation();

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                operation.PrepareContentReloadAsync(
                    _fixturePack.Definition.ID,
                    Array.Empty<string>()
                )
            );
        }

        [Test]
        public void PrepareContentReloadAsync_MissingPack_RecoversWithPreviousContent()
        {
            ContentLoadOperation operation = CreateOperation();

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                operation.PrepareContentReloadAsync("missing-pack", Array.Empty<string>())
            );

            Assert.AreSame(_previous, operation.Pack);
            Assert.IsFalse(operation.RequiresContentRestart);
            Assert.IsTrue(operation.BeginRecovery());
            operation.Complete(_previous, _settings);
        }

        [Test]
        public void Fail_RecoveryCannotInitialize_RejectsFurtherRecoveryAndCompletion()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            operation.BeginRecovery();

            operation.Fail();

            Assert.IsFalse(operation.BeginRecovery());
            Assert.Throws<InvalidOperationException>(() =>
                operation.Complete(_previous, _settings)
            );
        }

        [Test]
        public async Task BeginRecovery_PreparationStillRunning_RetainsPreviousSelectionAfterPreparationAsync()
        {
            ContentLoadOperation operation = CreateOperation();
            Task preparation = operation.PrepareContentReloadAsync(
                _fixturePack.Definition.ID,
                Array.Empty<string>()
            );

            Assert.IsTrue(operation.BeginRecovery());
            await preparation;

            Assert.AreSame(_previous, operation.Pack);
            Assert.IsTrue(operation.IsRecovering);
            operation.Complete(_previous, _settings);
        }

        [Test]
        public async Task BeginRecovery_SameContentSceneReady_ReturnsToMenuWithoutReconstructionAsync()
        {
            ContentPack current = _fixturePack;
            SaveGame(current);
            ContentLoadOperation operation = new ContentLoadOperation(
                current,
                _settings.Settings.Content,
                new[] { "main-menu", "strategy" }
            );
            await operation.PrepareSavedGameAsync(_saves, "campaign");
            operation.Complete(current, _settings);

            Assert.IsTrue(operation.BeginRecovery());

            Assert.IsFalse(operation.RequiresContentRestart);
            Assert.AreEqual("MainMenu", operation.DestinationScene);
            Assert.IsNull(operation.SaveFileName);
            operation.Complete(current, _settings);
            Assert.IsFalse(operation.BeginRecovery());
        }

        [Test]
        public void PrepareCustomGame_ValidSelection_PreservesDetachedConfigurationWithoutSaving()
        {
            ContentPack selected = _fixturePack;
            GameSummary summary = GameLaunchContext.CreateDefaultSummary(selected);
            summary.Difficulty = GameDifficulty.Hard;
            summary.CustomSettings = new CustomGameSettings
            {
                GenerationConfig = selected.GameData.GenerationConfig.CreateCopy(),
            };
            byte[] before = File.ReadAllBytes(_settingsPath);
            ContentLoadOperation operation = CreateOperation();

            operation.PrepareCustomGame(selected, summary);
            summary.Difficulty = GameDifficulty.Easy;
            summary.CustomSettings.GenerationConfig.FacilityGeneration.CoreMineMultiplier = 999;

            Assert.AreSame(selected, operation.Pack);
            Assert.IsTrue(operation.RequiresContentRestart);
            Assert.AreEqual("StrategyView", operation.DestinationScene);
            Assert.IsNull(operation.SaveFileName);
            Assert.AreEqual(GameDifficulty.Hard, operation.CustomGameSummary.Difficulty);
            Assert.AreNotEqual(
                999,
                operation
                    .CustomGameSummary
                    .CustomSettings
                    .GenerationConfig
                    .FacilityGeneration
                    .CoreMineMultiplier
            );
            GameSummary exposed = operation.CustomGameSummary;
            exposed.Difficulty = GameDifficulty.Easy;
            Assert.AreEqual(GameDifficulty.Hard, operation.CustomGameSummary.Difficulty);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void PrepareCustomGame_MismatchedIdentity_RejectsLaunchWithoutSaving()
        {
            GameSummary summary = GameLaunchContext.CreateDefaultSummary(_fixturePack);
            summary.PackID = "missing";
            byte[] before = File.ReadAllBytes(_settingsPath);
            ContentLoadOperation operation = CreateOperation();

            Assert.Throws<InvalidOperationException>(() =>
                operation.PrepareCustomGame(_fixturePack, summary)
            );

            Assert.AreSame(_previous, operation.Pack);
            Assert.IsNull(operation.CustomGameSummary);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void BeginRecovery_CustomGame_DiscardsLaunchAndPreservesPreviousPreferences()
        {
            ContentLoadOperation operation = CreateOperation();
            operation.PrepareCustomGame(
                _fixturePack,
                GameLaunchContext.CreateDefaultSummary(_fixturePack)
            );
            byte[] before = File.ReadAllBytes(_settingsPath);

            Assert.IsTrue(operation.BeginRecovery());
            operation.Complete(_previous, _settings);

            Assert.AreSame(_previous, operation.Pack);
            Assert.AreEqual("MainMenu", operation.DestinationScene);
            Assert.IsNull(operation.CustomGameSummary);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void Complete_CustomGame_RemembersAppliedOrderForTheSelectedPack()
        {
            string modID = CreateMod();
            ContentPack selected = ContentPackLoader.OpenDefaultScenario(
                _fixturePack.ContentRootPath,
                _fixturePack.Definition.ID,
                Array.Empty<string>(),
                new[] { modID }
            );
            ContentLoadOperation operation = CreateOperation();
            operation.PrepareCustomGame(selected, GameLaunchContext.CreateDefaultSummary(selected));

            operation.Complete(operation.Pack, _settings);

            Assert.That(
                _settings.Settings.Content.GetModLoadOrder(selected.Definition.ID),
                Is.EqualTo(selected.FileResolver.Mods.Select(mod => mod.ID))
            );
        }

        [Test]
        public void Complete_CustomGame_PreservesOtherPacksModPreferences()
        {
            string modID = CreateMod();
            ContentPack selected = ContentPackLoader.OpenExact(
                _fixturePack.ContentRootPath,
                _fixturePack.Definition.ID,
                _fixturePack.Scenario.ID,
                new[] { modID }
            );
            ContentLoadOperation operation = CreateOperation();
            operation.PrepareCustomGame(selected, GameLaunchContext.CreateDefaultSummary(selected));

            operation.Complete(operation.Pack, _settings);

            UserContentSettings content = CreateSettings(_settingsPath).Settings.Content;
            Assert.AreEqual(_fixturePack.Definition.ID, content.ActivePackID);
            Assert.AreEqual(_fixturePack.Scenario.ID, content.ActiveScenarioID);
            CollectionAssert.AreEquivalent(
                new[] { "other-pack-mod", "previous-mod", modID },
                content.DisabledModIDs
            );
        }

        /// <summary>
        /// Creates a pending operation over the fixture's committed content preferences.
        /// </summary>
        /// <returns>The operation.</returns>
        private ContentLoadOperation CreateOperation()
        {
            return new ContentLoadOperation(
                _previous,
                _settings.Settings.Content,
                new[] { "main-menu", "strategy" }
            );
        }

        /// <summary>
        /// Prepares installed replacement content for lifecycle tests without mutating preferences.
        /// </summary>
        /// <returns>The prepared operation.</returns>
        private ContentLoadOperation CreatePreparedOperation()
        {
            ContentLoadOperation operation = CreateOperation();
            operation
                .PrepareContentReloadAsync(
                    _fixturePack.Definition.ID,
                    new[] { "other-pack-mod", "candidate-mod" }
                )
                .GetAwaiter()
                .GetResult();
            return operation;
        }

        /// <summary>
        /// Writes a minimal save declaring the supplied content selection.
        /// </summary>
        /// <param name="pack">The content required by the save.</param>
        private void SaveGame(ContentPack pack)
        {
            _saves.SaveGameData(
                new GameRoot
                {
                    Summary = new GameSummary
                    {
                        PackID = pack.Definition.ID,
                        PackVersion = pack.Definition.Version,
                        ScenarioID = pack.Scenario.ID,
                        ModIDs = pack.FileResolver.Mods.Select(mod => mod.ID).ToArray(),
                        ModVersions = pack.FileResolver.Mods.Select(mod => mod.Version).ToArray(),
                    },
                },
                "campaign"
            );
        }

        /// <summary>
        /// Creates a compatible mod used to verify saved-game preference reconciliation.
        /// </summary>
        /// <param name="modID">The fixture ID, or null to generate one.</param>
        /// <param name="extendsPack">Whether the fixture can register additional scenarios.</param>
        /// <returns>The mod identifier.</returns>
        private string CreateMod(string modID = null, bool extendsPack = false)
        {
            ContentPack pack = _fixturePack;
            modID ??= "content-load-test-" + Guid.NewGuid().ToString("N");
            _modDirectory = Path.Combine(
                Directory.GetParent(pack.ContentRootPath).FullName,
                "Mods",
                modID
            );
            Directory.CreateDirectory(_modDirectory);
            File.WriteAllText(
                Path.Combine(_modDirectory, "mod.xml"),
                $"<ContentModDefinition><ID>{modID}</ID><Version>1.0.0</Version>"
                    + $"<DisplayName>Content load test</DisplayName><BasePackID>{pack.Definition.ID}</BasePackID>"
                    + $"<BasePackVersion>{pack.Definition.Version}</BasePackVersion>"
                    + (extendsPack ? "<ExtendsPack>true</ExtendsPack>" : "")
                    + "</ContentModDefinition>"
            );
            return modID;
        }

        private void WriteModText(string modID, string address, string text)
        {
            CreateMod(modID);
            string target = Path.Combine(_modDirectory, "Content", address);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, text);
        }

        private void WriteCampaignMod(string modID, string displayName, bool makeDefault)
        {
            CreateMod(modID, extendsPack: true);
            string relative = $"Scenarios/{modID}/scenario.xml";
            string target = Path.Combine(_modDirectory, "Content", "Pack", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            ContentDefinitionSerializer.Serialize(
                target,
                new ContentScenarioDefinition
                {
                    ID = modID,
                    DisplayName = displayName,
                    GenerationConfigPath = _fixturePack.Scenario.GenerationConfigPath,
                    DefaultPlayerFactionID = _fixturePack.Scenario.DefaultPlayerFactionID,
                    PlayableFactionIDs = _fixturePack.Scenario.PlayableFactionIDs.ToList(),
                }
            );
            File.WriteAllText(
                Path.Combine(_modDirectory, "Content", "Pack", "pack.xml"),
                "<ContentPack Inherit='true'>"
                    + (makeDefault ? $"<DefaultScenarioID>{modID}</DefaultScenarioID>" : "")
                    + $"<ScenarioPaths><Path>{relative}</Path></ScenarioPaths></ContentPack>"
            );
        }

        private static byte[] CreateStackIcon(int variant)
        {
            var icon = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                icon.SetPixels32(
                    Enumerable.Repeat(new Color32((byte)(variant * 29), 60, 190, 255), 4).ToArray()
                );
                icon.Apply();
                return icon.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(icon);
            }
        }

        /// <summary>
        /// Loads isolated preferences without applying changes to the real display.
        /// </summary>
        /// <param name="path">The fixture-owned preference path.</param>
        /// <returns>The preference service.</returns>
        private static UserSettingsManager CreateSettings(string path)
        {
            DisplayManager display = new DisplayManager(
                () => new[] { new Vector2Int(1920, 1080) },
                () => new Vector2Int(1920, 1080),
                (_, _, _) => { }
            );
            UserSettingsManager settings = new UserSettingsManager(null, display, null, path);
            settings.Load();
            return settings;
        }
    }
}
