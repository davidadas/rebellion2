using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Schema;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentPackLoaderTests
    {
        private const string _fixtureSchemaXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<xs:schema xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
  <xs:element name=""GameConfig"">
    <xs:complexType>
      <xs:all>
        <xs:element name=""Movement"">
          <xs:complexType>
            <xs:all>
              <xs:element name=""DistanceScale"" type=""xs:decimal""/>
            </xs:all>
          </xs:complexType>
        </xs:element>
        <xs:element name=""Research"">
          <xs:complexType>
            <xs:all>
              <xs:element name=""BaseResearchPoints"" type=""xs:integer""/>
            </xs:all>
          </xs:complexType>
        </xs:element>
      </xs:all>
    </xs:complexType>
  </xs:element>
</xs:schema>";
        private const string _fixtureDefaultsXml =
            "<GameConfig><Movement><DistanceScale>12</DistanceScale></Movement></GameConfig>";
        private const string _fixtureCompleteDefaultsXml =
            "<GameConfig><Movement><DistanceScale>12</DistanceScale></Movement>"
            + "<Research><BaseResearchPoints>1</BaseResearchPoints></Research></GameConfig>";
        private const string _jabbaRescueCompletionKey = "jabba.rescue.completed";

        [Test]
        public void OpenActive_JabbaRescueEvents_UseIndependentStartsAndSharedCompletionGate()
        {
            Dictionary<string, GameEvent> events = TestContent.Data.GameEvents.ToDictionary(
                gameEvent => gameEvent.InstanceID
            );
            string[] startEventIDs =
            {
                "LUKE_RESCUES_HAN_FROM_JABBA",
                "LEIA_RESCUES_HAN_FROM_JABBA",
                "CHEWBACCA_RESCUES_HAN_FROM_JABBA",
            };
            string[] resolutionEventIDs =
            {
                "LUKE_RESCUE_OF_HAN_RESOLVES",
                "LEIA_RESCUE_OF_HAN_RESOLVES",
                "CHEWBACCA_RESCUE_OF_HAN_RESOLVES",
            };
            HashSet<string> attemptEventIDs = startEventIDs
                .Concat(resolutionEventIDs)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string startEventID in startEventIDs)
            {
                string[] attemptDependencies = EnumerateConditionals(
                        events[startEventID].Conditionals
                    )
                    .OfType<HasEventActivatedConditional>()
                    .Select(conditional => conditional.EventInstanceID)
                    .Where(attemptEventIDs.Contains)
                    .ToArray();

                Assert.IsEmpty(
                    attemptDependencies,
                    $"Rescue start event '{startEventID}' must not wait for another rescue attempt."
                );
            }

            foreach (string resolutionEventID in resolutionEventIDs)
            {
                IfAction completionGate =
                    events[resolutionEventID].Actions.SingleOrDefault() as IfAction;
                Assert.IsNotNull(
                    completionGate,
                    $"Rescue resolution '{resolutionEventID}' requires a completion gate."
                );
                EvaluateEventVariableConditional completionCondition = completionGate
                    .Conditionals.OfType<EvaluateEventVariableConditional>()
                    .SingleOrDefault();
                Assert.IsNotNull(completionCondition);
                Assert.AreEqual(_jabbaRescueCompletionKey, completionCondition.Key);
                Assert.AreEqual(ComparisonOperator.Equal, completionCondition.Comparison);
                Assert.AreEqual(0, completionCondition.CompareTo);

                PerformSkillCheckAction skillCheck = completionGate
                    .Actions.OfType<PerformSkillCheckAction>()
                    .Single();
                SetEventVariableAction completionAction =
                    skillCheck.OnSuccess.FirstOrDefault() as SetEventVariableAction;
                Assert.IsNotNull(completionAction);
                Assert.AreEqual(_jabbaRescueCompletionKey, completionAction.Key);
                Assert.AreEqual(EventVariableOperation.Set, completionAction.Operation);
                Assert.AreEqual(1, completionAction.Operand);
            }

            GameEvent cleanup = events["JABBA_RESCUE_CLEANUP"];
            Assert.AreEqual(1, cleanup.MaximumActivations);
            EvaluateEventVariableConditional cleanupCondition = cleanup
                .Conditionals.OfType<EvaluateEventVariableConditional>()
                .Single();
            Assert.AreEqual(_jabbaRescueCompletionKey, cleanupCondition.Key);
            Assert.AreEqual(ComparisonOperator.Equal, cleanupCondition.Comparison);
            Assert.AreEqual(1, cleanupCondition.CompareTo);

            string[] cleanedAttempts = cleanup
                .Actions.OfType<IfAction>()
                .SelectMany(action => EnumerateConditionals(action.Conditionals))
                .OfType<HasEventActivatedConditional>()
                .Select(conditional => conditional.EventInstanceID)
                .ToArray();
            CollectionAssert.AreEquivalent(startEventIDs, cleanedAttempts);
        }

        [TestCase(RuntimePlatform.OSXPlayer, "Game.app/Contents/Resources/Data")]
        [TestCase(RuntimePlatform.OSXPlayer, "Game.app/Contents")]
        [TestCase(RuntimePlatform.LinuxPlayer, "Game_Data")]
        [TestCase(RuntimePlatform.WindowsPlayer, "Game_Data")]
        public void ResolvePlayerContentRootPath_DesktopPlayer_ReturnsDirectoryBesideArtifact(
            RuntimePlatform platform,
            string relativeDataPath
        )
        {
            string playerDirectory = Path.Combine(Path.GetTempPath(), "content-pack-player-layout");
            string dataPath = Path.Combine(playerDirectory, relativeDataPath);

            string contentRoot = ContentPackLoader.ResolvePlayerContentRootPath(dataPath, platform);

            Assert.AreEqual(Path.Combine(playerDirectory, "Content"), contentRoot);
        }

        [Test]
        public void ResolvePlayerContentRootPath_MacBundleLayout_DoesNotDependOnPlatformEnum()
        {
            string playerDirectory = Path.Combine(Path.GetTempPath(), "content-pack-mac-layout");
            string dataPath = Path.Combine(
                playerDirectory,
                "Game.app",
                "Contents",
                "Resources",
                "Data"
            );

            string contentRoot = ContentPackLoader.ResolvePlayerContentRootPath(
                dataPath,
                RuntimePlatform.LinuxPlayer
            );

            Assert.AreEqual(Path.Combine(playerDirectory, "Content"), contentRoot);
        }

        [Test]
        public void LoadGameConfig_NoPackOverridePath_UsesApplicationDefaults()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureCompleteDefaultsXml,
                packOverrideXml: null
            );

            Assert.AreEqual(12, config.Movement.DistanceScale);
            Assert.AreEqual(1, config.Research.BaseResearchPoints);
        }

        [Test]
        public void LoadGameConfig_PackOverrideLeaf_ReplacesDefaultValue()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureCompleteDefaultsXml,
                "<GameConfig><Movement><DistanceScale>7</DistanceScale></Movement></GameConfig>"
            );

            Assert.AreEqual(7, config.Movement.DistanceScale);
            Assert.AreEqual(1, config.Research.BaseResearchPoints);
        }

        [Test]
        public void LoadGameConfig_PackSuppliesSectionMissingFromDefaults_MergesIntoDefaults()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureDefaultsXml,
                "<GameConfig><Research><BaseResearchPoints>3</BaseResearchPoints></Research></GameConfig>"
            );

            Assert.AreEqual(12, config.Movement.DistanceScale);
            Assert.AreEqual(3, config.Research.BaseResearchPoints);
        }

        [Test]
        public void LoadGameConfig_MergedDocumentMissingRequiredElement_RejectsDocument()
        {
            Assert.Throws<XmlSchemaValidationException>(() =>
                LoadGameConfigFromFixture(_fixtureDefaultsXml, packOverrideXml: null)
            );
        }

        [Test]
        public void LoadGameConfig_UnknownOverrideElement_RejectsDocument()
        {
            Assert.Throws<XmlSchemaValidationException>(() =>
                LoadGameConfigFromFixture(
                    _fixtureCompleteDefaultsXml,
                    "<GameConfig><Bogus>1</Bogus></GameConfig>"
                )
            );
        }

        /// <summary>
        /// Loads game config from fixture.
        /// </summary>
        /// <param name="applicationDefaultsXml">The application defaults xml.</param>
        /// <param name="packOverrideXml">The pack override xml.</param>
        /// <returns>The loaded game config from fixture.</returns>
        private static GameConfig LoadGameConfigFromFixture(
            string applicationDefaultsXml,
            string packOverrideXml
        )
        {
            string contentRoot = Path.Combine(
                Path.GetTempPath(),
                "content-pack-loader-config-" + Guid.NewGuid().ToString("N")
            );
            try
            {
                string rulesRoot = Path.Combine(contentRoot, "Application", "Rules");
                string schemasRoot = Path.Combine(contentRoot, "Application", "Schemas");
                string packRoot = Path.Combine(contentRoot, "Packs", "Fixture");
                Directory.CreateDirectory(rulesRoot);
                Directory.CreateDirectory(schemasRoot);
                Directory.CreateDirectory(Path.Combine(packRoot, "Rules"));
                File.WriteAllText(Path.Combine(rulesRoot, "game.xml"), applicationDefaultsXml);
                File.WriteAllText(Path.Combine(schemasRoot, "game-config.xsd"), _fixtureSchemaXml);
                string packOverridePath = null;
                if (packOverrideXml != null)
                {
                    packOverridePath = "Rules/game.xml";
                    File.WriteAllText(Path.Combine(packRoot, "Rules", "game.xml"), packOverrideXml);
                }

                return ContentPackLoader.LoadGameConfig(contentRoot, packRoot, packOverridePath);
            }
            finally
            {
                if (Directory.Exists(contentRoot))
                    Directory.Delete(contentRoot, true);
            }
        }

        /// <summary>
        /// Enumerates authored conditions, including conditions nested in composite nodes.
        /// </summary>
        /// <param name="conditionals">The conditions to traverse.</param>
        /// <returns>Every condition in the authored tree.</returns>
        private static IEnumerable<GameConditional> EnumerateConditionals(
            IEnumerable<GameConditional> conditionals
        )
        {
            foreach (GameConditional conditional in conditionals)
            {
                yield return conditional;
                IEnumerable<GameConditional> children = conditional switch
                {
                    AllConditional all => all.Conditionals,
                    AnyConditional any => any.Conditionals,
                    NotConditional not => not.Conditionals,
                    XorConditional xor => xor.Conditionals,
                    _ => Array.Empty<GameConditional>(),
                };
                foreach (GameConditional child in EnumerateConditionals(children))
                    yield return child;
            }
        }
    }
}
