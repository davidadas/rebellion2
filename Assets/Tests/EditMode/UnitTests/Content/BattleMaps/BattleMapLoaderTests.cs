using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using Rebellion.Game.Combat;

namespace Rebellion.Tests.Content.BattleMaps
{
    [TestFixture]
    public sealed class BattleMapLoaderTests
    {
        [Test]
        public void Load_ValidRmapPackage_DeserializesBattleMap()
        {
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                "battle-map-loader-" + Guid.NewGuid().ToString("N")
            );
            string filePath = Path.Combine(directoryPath, "deep-space.rmap");
            Directory.CreateDirectory(directoryPath);

            try
            {
                using (ZipArchive archive = ZipFile.Open(filePath, ZipArchiveMode.Create))
                using (
                    StreamWriter writer = new StreamWriter(archive.CreateEntry("map.xml").Open())
                )
                {
                    writer.Write(
                        "<RebellionMap FormatVersion=\"1\"><SpaceMap><ID>deep-space</ID>"
                            + "<Environment><ProfileID>DeepSpace</ProfileID></Environment>"
                            + "<Objects><Object ID=\"deep-space-environment\" "
                            + "AssetID=\"Engine/SpaceBattle/DeepSpaceEnvironment\" /></Objects>"
                            + "<PlayableVolume><Center X=\"0\" Y=\"0\" Z=\"0\" />"
                            + "<Size X=\"5000\" Y=\"2000\" Z=\"6000\" /></PlayableVolume>"
                            + "<SpawnVolumes><SpawnVolume ParticipantSlotID=\"attacker\" "
                            + "Purpose=\"InitialDeployment\"><Center X=\"0\" Y=\"5\" Z=\"-1700\" />"
                            + "<Size X=\"1500\" Y=\"1000\" Z=\"400\" /></SpawnVolume>"
                            + "<SpawnVolume ParticipantSlotID=\"defender\" Purpose=\"InitialDeployment\">"
                            + "<Center X=\"0\" Y=\"5\" Z=\"1700\" />"
                            + "<Size X=\"1500\" Y=\"1000\" Z=\"400\" /></SpawnVolume>"
                            + "</SpawnVolumes></SpaceMap></RebellionMap>"
                    );
                }

                BattleMap map = BattleMapLoader.Load(filePath);

                Assert.AreEqual("deep-space", map.InstanceID);
                Assert.AreEqual(BattleKind.Space, map.Kind);
                Assert.AreEqual(-2500, map.PlayableBounds.MinimumX);
                Assert.AreEqual(3000, map.PlayableBounds.MaximumZ);
                Assert.AreEqual(2, map.GetDeploymentRegions().Count);
                Assert.AreEqual("attacker", map.GetDeploymentRegions()[0].ParticipantSlotID);
                Assert.AreEqual(-1900, map.GetDeploymentRegions()[0].Bounds.MinimumZ);
                Assert.AreEqual(1900, map.GetDeploymentRegions()[1].Bounds.MaximumZ);
            }
            finally
            {
                if (Directory.Exists(directoryPath))
                    Directory.Delete(directoryPath, true);
            }
        }
    }
}
