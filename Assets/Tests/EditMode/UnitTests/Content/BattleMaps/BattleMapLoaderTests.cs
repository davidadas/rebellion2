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
                {
                    using (
                        StreamWriter writer = new StreamWriter(
                            archive.CreateEntry("map.xml").Open()
                        )
                    )
                    {
                        writer.Write(
                            "<RebellionMap FormatVersion=\"1\"><SpaceMap><ID>deep-space</ID>"
                                + "<Environment><BackgroundColor R=\"0.001\" G=\"0.002\" B=\"0.006\" A=\"1\" />"
                                + "<AmbientSkyColor R=\"0.05\" G=\"0.065\" B=\"0.11\" A=\"1\" />"
                                + "<AmbientEquatorColor R=\"0.025\" G=\"0.035\" B=\"0.065\" A=\"1\" />"
                                + "<AmbientGroundColor R=\"0.015\" G=\"0.022\" B=\"0.045\" A=\"1\" />"
                                + "</Environment><Objects>"
                                + "<Planet ID=\"orbital-planet\" ModelAssetPath=\"models/planet.glb\" "
                                + "CloudTextureAssetPath=\"textures/clouds.png\" Diameter=\"700\" "
                                + "AtmosphereRadiusRatio=\"1.062\" CloudRotationSpeed=\"0.3333333\">"
                                + "<Position X=\"340\" Y=\"-90\" Z=\"900\" />"
                                + "<Rotation X=\"0\" Y=\"0\" Z=\"0\" />"
                                + "<SunDirection X=\"0.8\" Y=\"0.46\" Z=\"-0.38\" /></Planet>"
                                + "<Starfield ID=\"distant-stars\" Count=\"320\" Seed=\"72491\" "
                                + "InnerRadius=\"325\" OuterRadius=\"475\" MinimumSize=\"0.035\" "
                                + "MaximumSize=\"0.19\" MinimumBrightness=\"0.05\" MaximumBrightness=\"0.26\" "
                                + "SecondaryColorProbability=\"0.16\"><PrimaryColor R=\"0.68\" G=\"0.78\" "
                                + "B=\"0.92\" A=\"1\" /><SecondaryColor R=\"0.38\" G=\"0.58\" "
                                + "B=\"0.86\" A=\"1\" /></Starfield>"
                                + "<DebrisField ID=\"battlefield-debris\" "
                                + "ModelAssetPath=\"models/asteroid.glb\" ShowDistance=\"440\" "
                                + "HideDistance=\"500\" CellCount=\"20\" CellNoise=\"0.45\" "
                                + "DebrisCountTarget=\"32\" MinimumScale=\"1.5\" MaximumScale=\"12\" "
                                + "ScaleBias=\"1.25\" RandomRotation=\"true\" Seed=\"11427\" />"
                                + "</Objects>"
                                + "<Lights><DirectionalLight ID=\"starlight-key\" Intensity=\"1.15\" "
                                + "CastShadows=\"true\" IsSun=\"true\"><Rotation X=\"58\" Y=\"65\" Z=\"0\" />"
                                + "<Color R=\"1\" G=\"0.94\" B=\"0.84\" A=\"1\" />"
                                + "</DirectionalLight></Lights>"
                                + "<CameraStarts><CameraStart ParticipantSlotID=\"attacker\">"
                                + "<Position X=\"0\" Y=\"120\" Z=\"-2250\" />"
                                + "<Rotation X=\"12\" Y=\"0\" Z=\"0\" />"
                                + "<FieldOfView>48</FieldOfView></CameraStart>"
                                + "<CameraStart ParticipantSlotID=\"defender\">"
                                + "<Position X=\"0\" Y=\"120\" Z=\"2250\" />"
                                + "<Rotation X=\"12\" Y=\"180\" Z=\"0\" />"
                                + "<FieldOfView>48</FieldOfView></CameraStart></CameraStarts>"
                                + "<PlayableVolume><Center X=\"0\" Y=\"0\" Z=\"0\" />"
                                + "<Size X=\"5000\" Y=\"2000\" Z=\"6000\" /></PlayableVolume>"
                                + "<SpawnVolumes><SpawnVolume ParticipantSlotID=\"attacker\" "
                                + "Purpose=\"InitialDeployment\"><Center X=\"0\" Y=\"5\" Z=\"-1700\" />"
                                + "<Size X=\"1500\" Y=\"1000\" Z=\"400\" />"
                                + "<Facing X=\"0\" Y=\"0\" Z=\"0\" /></SpawnVolume>"
                                + "<SpawnVolume ParticipantSlotID=\"defender\" Purpose=\"InitialDeployment\">"
                                + "<Center X=\"0\" Y=\"5\" Z=\"1700\" />"
                                + "<Size X=\"1500\" Y=\"1000\" Z=\"400\" />"
                                + "<Facing X=\"0\" Y=\"180\" Z=\"0\" /></SpawnVolume>"
                                + "</SpawnVolumes></SpaceMap></RebellionMap>"
                        );
                    }

                    WriteAsset(
                        archive,
                        "assets/models/asteroid.glb",
                        new byte[] { 0x67, 0x6c, 0x54, 0x46 }
                    );
                    WriteAsset(
                        archive,
                        "assets/models/planet.glb",
                        new byte[] { 0x67, 0x6c, 0x54, 0x46 }
                    );
                    WriteAsset(
                        archive,
                        "assets/textures/clouds.png",
                        new byte[] { 0x89, 0x50, 0x4e, 0x47 }
                    );
                }

                BattleMap map = BattleMapLoader.Load(filePath);

                Assert.AreEqual("deep-space", map.InstanceID);
                Assert.AreEqual(BattleKind.Space, map.Kind);
                Assert.AreEqual(0.001f, map.Environment.BackgroundColor.Red);
                Assert.AreEqual(1, map.GetPlanets().Count);
                Assert.AreEqual("orbital-planet", map.GetPlanets()[0].InstanceID);
                Assert.AreEqual(700f, map.GetPlanets()[0].Diameter);
                Assert.AreEqual(900f, map.GetPlanets()[0].Position.Z);
                Assert.AreEqual(1.062f, map.GetPlanets()[0].AtmosphereRadiusRatio);
                Assert.AreEqual("models/planet.glb", map.GetPlanets()[0].ModelAssetPath);
                Assert.AreEqual("textures/clouds.png", map.GetPlanets()[0].CloudTextureAssetPath);
                Assert.AreEqual(1, map.GetStarfields().Count);
                Assert.AreEqual(72491, map.GetStarfields()[0].Seed);
                Assert.AreEqual(320, map.GetStarfields()[0].Count);
                Assert.AreEqual(1, map.GetDebrisFields().Count);
                Assert.AreEqual("models/asteroid.glb", map.GetDebrisFields()[0].ModelAssetPath);
                Assert.AreEqual(32f, map.GetDebrisFields()[0].DebrisCountTarget);
                Assert.AreEqual(11427, map.GetDebrisFields()[0].Seed);
                Assert.IsTrue(
                    map.TryGetEmbeddedAsset("models/asteroid.glb", out byte[] modelBytes)
                );
                CollectionAssert.AreEqual(new byte[] { 0x67, 0x6c, 0x54, 0x46 }, modelBytes);
                Assert.IsTrue(map.TryGetEmbeddedAsset("models/planet.glb", out _));
                Assert.IsTrue(map.TryGetEmbeddedAsset("textures/clouds.png", out _));
                Assert.AreEqual(1, map.GetDirectionalLights().Count);
                Assert.IsTrue(map.GetDirectionalLights()[0].IsSun);
                Assert.IsTrue(map.GetDirectionalLights()[0].CastShadows);
                Assert.AreEqual(2, map.GetCameraStarts().Count);
                Assert.AreEqual("attacker", map.GetCameraStarts()[0].ParticipantSlotID);
                Assert.AreEqual(-2250, map.GetCameraStarts()[0].Position.Z);
                Assert.AreEqual(48, map.GetCameraStarts()[0].FieldOfView);
                Assert.AreEqual(-2500, map.PlayableBounds.MinimumX);
                Assert.AreEqual(3000, map.PlayableBounds.MaximumZ);
                Assert.AreEqual(2, map.GetDeploymentRegions().Count);
                Assert.AreEqual("attacker", map.GetDeploymentRegions()[0].ParticipantSlotID);
                Assert.AreEqual(-1900, map.GetDeploymentRegions()[0].Bounds.MinimumZ);
                Assert.AreEqual(180, map.GetDeploymentRegions()[1].Facing.Y);
                Assert.AreEqual(1900, map.GetDeploymentRegions()[1].Bounds.MaximumZ);
            }
            finally
            {
                if (Directory.Exists(directoryPath))
                    Directory.Delete(directoryPath, true);
            }
        }

        /// <summary>
        /// Writes one complete in-memory asset to a test map package.
        /// </summary>
        /// <param name="archive">The package being authored.</param>
        /// <param name="path">The package-relative entry path.</param>
        /// <param name="bytes">The asset payload.</param>
        private static void WriteAsset(ZipArchive archive, string path, byte[] bytes)
        {
            using Stream stream = archive.CreateEntry(path).Open();
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
