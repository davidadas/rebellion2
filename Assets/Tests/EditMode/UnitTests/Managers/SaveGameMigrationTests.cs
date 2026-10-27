using System.IO;
using System.Text;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class SaveGameMigrationTests
    {
        [Test]
        public void Deserialize_LegacyCapitalShip_PreservesConditionAndPendingOrders()
        {
            GameRoot restored = Restore(
                CreateLegacySave(
                    @"
                <CombatUnit>
                  <InstanceID>BATTLE_SHIP</InstanceID>
                  <SourceUnitInstanceID>SOURCE</SourceUnitInstanceID>
                  <CapitalShip>
                    <InstanceID>SOURCE</InstanceID>
                    <OwnerInstanceID>OWNER</OwnerInstanceID>
                    <TypeID>CRUISER</TypeID>
                    <MaxHullStrength>100</MaxHullStrength>
                    <CurrentHullStrength>35</CurrentHullStrength>
                    <MaxShieldStrength>50</MaxShieldStrength>
                    <SublightSpeed>4</SublightSpeed>
                    <ManufacturingProgress>73</ManufacturingProgress>
                  </CapitalShip>
                  <CurrentShieldStrength>17</CurrentShieldStrength>
                  <ShieldDamageRemainder>0.25</ShieldDamageRemainder>
                  <HullDamageRemainder>0.5</HullDamageRemainder>
                  <MoveDestination><X>3</X><Y>4</Y><Z>5</Z></MoveDestination>
                  <MoveSpeedLimit>2</MoveSpeedLimit>
                  <AttackTargetInstanceID>TARGET</AttackTargetInstanceID>
                  <WeaponBanks><WeaponBankState><GroupID>gun</GroupID><Mounts>
                    <WeaponMountState><Cooldown>0.375</Cooldown><ShotsFired>2</ShotsFired></WeaponMountState>
                  </Mounts></WeaponBankState></WeaponBanks>
                  <LaunchQueue><string>FIGHTER</string></LaunchQueue>
                  <LaunchCountdown>1.5</LaunchCountdown>
                </CombatUnit>"
                )
            );
            CombatUnit unit = restored.GetActiveBattle().GetParticipants()[0].GetCombatants()[0];

            Assert.AreEqual("BATTLE_SHIP", unit.InstanceID);
            Assert.AreEqual("SOURCE", unit.SourceUnitInstanceID);
            Assert.AreEqual(CombatUnitKind.CapitalShip, unit.Data.Kind);
            Assert.AreEqual(35, unit.Condition.CurrentHullStrength);
            Assert.AreEqual(17, unit.Condition.CurrentShieldStrength);
            Assert.AreEqual(0.25f, unit.Condition.ShieldDamageRemainder);
            Assert.AreEqual(0.5f, unit.Condition.HullDamageRemainder);
            Assert.AreEqual(5f, unit.MoveOrder.Destination.Z);
            Assert.AreEqual(2f, unit.MoveOrder.SpeedLimit);
            Assert.AreEqual("TARGET", unit.Weapons.AttackTargetInstanceID);
            Assert.AreEqual(0.375f, unit.Weapons.Banks[0].Mounts[0].Cooldown);
            Assert.AreEqual(2, unit.Weapons.Banks[0].Mounts[0].ShotsFired);
            CollectionAssert.AreEqual(new[] { "FIGHTER" }, unit.Carrier.LaunchQueue);
            Assert.AreEqual(1.5f, unit.Carrier.LaunchCountdown);
            Assert.AreEqual(GameMetadata.CurrentSaveVersion, restored.Metadata.SaveVersion);
            StringAssert.DoesNotContain(
                "ManufacturingProgress",
                SerializationHelper.Serialize(unit)
            );
        }

        [Test]
        public void Deserialize_LegacyFighter_PreservesRecoveryAndUnknownShields()
        {
            GameRoot restored = Restore(
                CreateLegacySave(
                    @"
                <CombatUnit>
                  <Starfighter>
                    <InstanceID>FIGHTER</InstanceID><OwnerInstanceID>OWNER</OwnerInstanceID>
                    <MaxHullStrength>20</MaxHullStrength><CurrentHullStrength>12</CurrentHullStrength>
                    <MaxSquadronSize>12</MaxSquadronSize><CurrentSquadronSize>5</CurrentSquadronSize>
                    <ShieldStrength>8</ShieldStrength>
                  </Starfighter>
                  <CarrierSourceUnitInstanceID>CARRIER</CarrierSourceUnitInstanceID>
                  <FlightState>Returning</FlightState><FlightBayIndex>2</FlightBayIndex>
                  <FlightWaypointIndex>3</FlightWaypointIndex><FlightSegmentDistance>0.75</FlightSegmentDistance>
                  <FlightDelay>0.5</FlightDelay><RecallRequested>true</RecallRequested>
                </CombatUnit>"
                )
            );
            CombatUnit unit = restored.GetActiveBattle().GetParticipants()[0].GetCombatants()[0];

            Assert.AreEqual(CombatUnitKind.Starfighter, unit.Data.Kind);
            Assert.AreEqual(12, unit.Condition.CurrentHullStrength);
            Assert.AreEqual(5, unit.Condition.CurrentSquadronSize);
            Assert.IsNull(unit.Condition.CurrentShieldStrength);
            Assert.AreEqual("CARRIER", unit.Flight.CarrierSourceUnitInstanceID);
            Assert.AreEqual(FighterFlightState.Returning, unit.Flight.Phase);
            Assert.AreEqual(2, unit.Flight.FlightBayIndex);
            Assert.AreEqual(3, unit.Flight.FlightWaypointIndex);
            Assert.AreEqual(0.75f, unit.Flight.FlightSegmentDistance);
            Assert.AreEqual(0.5f, unit.Flight.FlightDelay);
            Assert.IsTrue(unit.Flight.RecallRequested);
        }

        [Test]
        public void Deserialize_LegacyProjectile_PreservesShotWithoutCopiedGun()
        {
            string xml =
                @"<Game><Metadata><SaveVersion>1</SaveVersion></Metadata><ActiveBattle>
                <Projectiles><BattleProjectile><ID>9</ID><DistanceTraveled>4</DistanceTraveled>
                  <Weapon><WeaponType>LaserCannon</WeaponType><DeliveryMode>Projectile</DeliveryMode>
                    <Damage>8</Damage><Range>60</Range><ProjectileSpeed>135</ProjectileSpeed>
                    <ProjectileLength>1.6</ProjectileLength><ProjectileWidth>0.11</ProjectileWidth>
                    <EffectDurationSeconds>0.18</EffectDurationSeconds>
                    <Red>0.18</Red><Green>1</Green><Blue>0.3</Blue>
                    <ShotsPerVolley>6</ShotsPerVolley><FireIntervalSeconds>0.2</FireIntervalSeconds>
                    <FireSoundPaths><string>fire.wav</string></FireSoundPaths>
                    <ImpactSoundPaths><string>impact.wav</string></ImpactSoundPaths>
                  </Weapon>
                </BattleProjectile></Projectiles></ActiveBattle></Game>";

            BattleProjectile shot = Restore(xml).GetActiveBattle().Projectiles[0];

            Assert.AreEqual(9, shot.ID);
            Assert.AreEqual(4f, shot.DistanceTraveled);
            Assert.AreEqual(60f, shot.Range);
            Assert.AreEqual(8f, shot.Projectile.Damage);
            Assert.AreEqual(135f, shot.Projectile.Speed);
            Assert.AreEqual("#2EFF4D", shot.Projectile.ColorHex);
            CollectionAssert.AreEqual(new[] { "impact.wav" }, shot.Projectile.ImpactSoundPaths);
            StringAssert.DoesNotContain("FireSoundPaths", SerializationHelper.Serialize(shot));
        }

        [Test]
        public void Deserialize_CurrentSave_RestoresWithoutLegacyConversion()
        {
            GameRoot game = new GameRoot
            {
                Metadata = new GameMetadata { SaveVersion = GameMetadata.CurrentSaveVersion },
            };
            game.SetActiveBattle(
                new ActiveBattle { Kind = BattleKind.Space, WeaponTimeRemainder = 0.012f }
            );
            string xml = SerializationHelper.Serialize(game);
            GameSerializer serializer = new GameSerializer(typeof(GameRoot));
            using MemoryStream stream = new MemoryStream();
            serializer.Serialize(stream, game);
            stream.Position = 0;

            GameRoot restored = SaveGameMigration.Deserialize(stream, serializer);

            Assert.AreEqual(xml, SerializationHelper.Serialize(restored));
        }

        [Test]
        public void Deserialize_LegacyResolvedBeam_ExcludesPresentationEffectFromSavedShots()
        {
            string xml =
                @"<Game><Metadata><SaveVersion>1</SaveVersion></Metadata><ActiveBattle>
                <Projectiles><BattleProjectile><ID>9</ID><RemainingEffectSeconds>0.2</RemainingEffectSeconds>
                  <Weapon><DeliveryMode>Hitscan</DeliveryMode><Damage>8</Damage><Range>60</Range></Weapon>
                </BattleProjectile></Projectiles></ActiveBattle></Game>";

            GameRoot restored = Restore(xml);

            Assert.IsEmpty(restored.GetActiveBattle().Projectiles);
        }

        /// <summary>
        /// Wraps a recorded combatant in the first saved battle format.
        /// </summary>
        /// <param name="combatant">The recorded combatant XML.</param>
        /// <returns>The complete saved-game XML.</returns>
        private static string CreateLegacySave(string combatant) =>
            "<Game><Metadata><SaveVersion>1</SaveVersion></Metadata><ActiveBattle>"
            + "<Participants><BattleParticipant><FactionInstanceID>OWNER</FactionInstanceID><Combatants>"
            + combatant
            + "</Combatants></BattleParticipant></Participants></ActiveBattle></Game>";

        /// <summary>
        /// Reads a saved game through the production format upgrade boundary.
        /// </summary>
        /// <param name="xml">The saved-game XML.</param>
        /// <returns>The restored game.</returns>
        private static GameRoot Restore(string xml)
        {
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            return SaveGameMigration.Deserialize(stream, new GameSerializer(typeof(GameRoot)));
        }
    }
}
