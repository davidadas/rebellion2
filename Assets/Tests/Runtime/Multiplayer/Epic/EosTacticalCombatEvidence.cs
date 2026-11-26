using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.Synchronization;
using JsonUtility = UnityEngine.JsonUtility;

namespace Rebellion.Tests.MultiplayerIntegration
{
    /// <summary>
    /// Compares shared combat facts without capturing frames or consuming their pending effects.
    /// </summary>
    internal sealed class EosTacticalCombatEvidence : IDisposable
    {
        private readonly SHA256 _events = SHA256.Create();
        private readonly MemoryStream _buffer = new();
        private readonly BinaryWriter _writer;
        private bool _completed;

        /// <summary>
        /// Reuses one event-sized buffer while hashing an arbitrarily long ordered event stream.
        /// </summary>
        internal EosTacticalCombatEvidence()
        {
            _writer = new BinaryWriter(_buffer, Encoding.UTF8, true);
        }

        /// <summary>
        /// Records exactly the discharge fields transported to the replica.
        /// </summary>
        /// <param name="shot">The authoritative or projected discharge.</param>
        internal void RecordShot(TacticalCombatShot shot)
        {
            BeginEvent(TacticalCombatEventKind.Shot);
            _writer.Write(shot.Source.InstanceID);
            _writer.Write(shot.Target.InstanceID);
            _writer.Write((int)shot.Weapon);
            _writer.Write(shot.Strength);
            EndEvent();
        }

        /// <summary>
        /// Records exactly the impact fields transported to the replica.
        /// </summary>
        /// <param name="unit">The struck participant.</param>
        /// <param name="weapon">The applied weapon type.</param>
        /// <param name="impact">The observed impact kind.</param>
        internal void RecordImpact(
            CombatUnit unit,
            TacticalWeaponType weapon,
            TacticalCombatImpactKind impact
        )
        {
            BeginEvent(TacticalCombatEventKind.Impact);
            _writer.Write(unit.InstanceID);
            _writer.Write((int)weapon);
            _writer.Write((int)impact);
            EndEvent();
        }

        /// <summary>
        /// Records one destruction, withdrawal, or recovery identity in its observed event order.
        /// </summary>
        /// <param name="kind">The transported unit-event discriminator.</param>
        /// <param name="unit">The affected participant.</param>
        internal void RecordUnit(TacticalCombatEventKind kind, CombatUnit unit)
        {
            BeginEvent(kind);
            _writer.Write(unit.InstanceID);
            EndEvent();
        }

        /// <summary>
        /// Finalizes the digest after terminal state and all reliable effects have been acknowledged.
        /// </summary>
        /// <returns>The ordered event stream's SHA256 digest.</returns>
        internal string Complete()
        {
            if (!_completed)
            {
                _events.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                _completed = true;
            }
            return Hex(_events.Hash);
        }

        /// <summary>
        /// Captures only public replicated state, excluding each player's independent inspection and presentation.
        /// </summary>
        /// <param name="session">The paused local authoritative or replica view.</param>
        /// <param name="sidecarPath">The role-specific diagnostic file written once for this paused state.</param>
        /// <returns>The canonical shared-state digest at this view's system time.</returns>
        internal static string CapturePausedState(TacticalCombatSession session, string sidecarPath)
        {
            using MemoryStream buffer = new();
            using StateWriter writer = new(buffer);
            writer.WriteField("SystemTime", session.SystemTime);
            writer.WriteField("IsPaused", session.IsPaused);
            writer.WriteField("IsComplete", session.IsComplete);
            writer.WriteField("Winner", (int)session.Winner);
            for (int index = 0; index < session.TerminalFlags.Count; index++)
                writer.WriteField("TerminalFlags." + index, session.TerminalFlags[index]);
            Dictionary<int, TacticalCombatGroup> groups = session.Groups.ToDictionary(group =>
                group.ID
            );
            foreach (CombatSide side in new[] { CombatSide.Attacker, CombatSide.Defender })
            {
                TacticalCombatSide state = session.GetSide(side);
                writer.Prefix = "Sides." + side;
                writer.WriteField("Side", (int)side);
                writer.WriteField("IsAutomated", state.IsAutomated);
                writer.WriteField("IsWithdrawing", state.IsWithdrawing);
                writer.WriteField("WithdrawalMilliseconds", state.WithdrawalMilliseconds);
                writer.WriteField("WithdrawalPhase", (int?)state.WithdrawalPhase ?? -1);
                writer.WriteField("AttackBudget", state.AttackBudget);
                for (int category = 0; category < 12; category++)
                {
                    TacticalCombatGroup group =
                        category < 8
                            ? session.GetObjectGroup(side, category)
                            : session.GetFighterGroup(side, category - 8);
                    WriteGroupID(writer, "Categories." + category, group?.ID);
                    if (group != null)
                        groups[group.ID] = group;
                }
            }
            writer.Prefix = string.Empty;
            writer.WriteField("Units.Count", session.Units.Count);
            foreach (
                CombatUnit unit in session.Units.OrderBy(
                    unit => unit.InstanceID,
                    StringComparer.Ordinal
                )
            )
                WriteUnit(writer, unit);
            writer.Prefix = string.Empty;
            writer.WriteField("Groups.Count", groups.Count);
            foreach (
                TacticalCombatGroup group in groups.Values.OrderBy(group =>
                    unchecked((uint)group.ID)
                )
            )
                WriteGroup(writer, group);
            writer.Flush();
            using SHA256 hash = SHA256.Create();
            string fingerprint = Hex(hash.ComputeHash(buffer.GetBuffer(), 0, (int)buffer.Length));
            writer.Save(sidecarPath, fingerprint);
            return fingerprint;
        }

        /// <summary>
        /// Releases the bounded event buffer and cryptographic state.
        /// </summary>
        public void Dispose()
        {
            _writer.Dispose();
            _buffer.Dispose();
            _events.Dispose();
        }

        /// <summary>
        /// Starts a length-independent binary event record without reallocating previous records.
        /// </summary>
        /// <param name="kind">The ordered event type.</param>
        private void BeginEvent(TacticalCombatEventKind kind)
        {
            if (_completed)
                throw new InvalidOperationException(
                    "A terminal event digest received another combat event."
                );
            _buffer.Position = 0;
            _buffer.SetLength(0);
            _writer.Write((int)kind);
        }

        /// <summary>
        /// Adds the complete binary record, retaining only the hash state and reusable current-event buffer.
        /// </summary>
        private void EndEvent()
        {
            _writer.Flush();
            byte[] bytes = _buffer.GetBuffer();
            _events.TransformBlock(bytes, 0, (int)_buffer.Length, bytes, 0);
        }

        /// <summary>
        /// Writes the public unit facts carried by complete tactical state updates.
        /// </summary>
        /// <param name="writer">The canonical binary destination.</param>
        /// <param name="unit">The participant to compare.</param>
        private static void WriteUnit(StateWriter writer, CombatUnit unit)
        {
            writer.Prefix = "Units." + unit.InstanceID;
            writer.WriteField("InstanceID", unit.InstanceID);
            writer.WriteField("Side", (int)unit.Side);
            writer.WriteField("Status", (int)unit.Status);
            writer.WriteField("CarrierID", unit.CarrierID ?? string.Empty);
            WriteVector(writer, "Position", unit.Position);
            WriteVector(writer, "Heading", unit.Heading);
            WriteVector(writer, "Velocity", unit.Velocity);
            WriteVector(writer, "DesiredHeading", unit.DesiredHeading);
            WriteVector(writer, "DesiredMovement", unit.DesiredMovement);
            writer.WriteField("Systems.Hull", unit.Hull);
            writer.WriteField("Systems.Shields", unit.Shields);
            writer.WriteField("Systems.MaximumShields", unit.MaximumShields);
            writer.WriteField("Systems.AttackDelay", unit.AttackDelay);
            writer.WriteField("Systems.MovementDelay", unit.MovementDelay);
            for (int index = 0; index < 5; index++)
                writer.WriteField(
                    "Systems.ComponentDamage." + index,
                    unit.GetDamage((TacticalShipSystem)index)
                );
            for (int index = 0; index < 4; index++)
            {
                writer.WriteField(
                    "Weapons.Charge." + index,
                    unit.GetBatteryCharge((TacticalWeaponArc)index)
                );
                writer.WriteField(
                    "Weapons.Ready." + index,
                    unit.IsBatteryReady((TacticalWeaponArc)index)
                );
            }
            writer.WriteField("HyperspaceMilliseconds", unit.HyperspaceMilliseconds);
            writer.WriteField("TractorRestraint", unit.TractorRestraint);
            writer.WriteField("HasTractorSources", unit.HasTractorSources);
            writer.WriteField("CommandBudget", unit.CommandBudget);
            writer.WriteField("TurnRate", unit.TurnRate);
            writer.WriteField("HasCollision", unit.HasCollision);
            WriteGroupID(writer, "GroupID", unit.GroupID);
            WriteGroupID(writer, "RequestedGroupID", unit.RequestedGroupID);
        }

        /// <summary>
        /// Writes shared applied and requested group ownership, targets, navigation, and formation.
        /// </summary>
        /// <param name="writer">The canonical binary destination.</param>
        /// <param name="group">The public group to compare.</param>
        private static void WriteGroup(StateWriter writer, TacticalCombatGroup group)
        {
            writer.Prefix = "Groups." + group.ID.ToString(CultureInfo.InvariantCulture);
            writer.WriteField("ID", group.ID);
            writer.WriteField("Side", (int)group.Side);
            writer.WriteField("Kind", (int)group.Kind);
            writer.WriteField("Order", (int)group.Order);
            writer.WriteField("ActiveOrder", (int)group.ActiveOrder);
            writer.WriteField("CloseAttack", group.CloseAttack);
            writer.WriteField("ActiveCloseAttack", group.ActiveCloseAttack);
            writer.WriteField("RequestedFormation", (int)group.RequestedFormation);
            writer.WriteField("Formation.Kind", (int)group.Formation.Kind);
            WriteVector(writer, "Formation.Position", group.Formation.Position);
            WriteVector(writer, "Formation.Heading", group.Formation.Heading);
            WriteVector(writer, "Formation.Velocity", group.Formation.Velocity);
            writer.WriteField("Formation.MovementSpeed", group.Formation.MovementSpeed);
            writer.WriteField("Formation.TurnRate", group.Formation.TurnRate);
            writer.WriteField("Formation.StableUpdates", group.Formation.StableUpdates);
            writer.WriteField("FighterCategory", group.FighterCategory);
            writer.WriteField("PrimaryMember", group.PrimaryMember?.InstanceID ?? string.Empty);
            writer.WriteField("RequestedLeader", group.RequestedLeader?.InstanceID ?? string.Empty);
            WriteMembers(writer, "Members", group.Members);
            WriteMembers(writer, "RequestedMembers", group.RequestedMembers);
            WriteMembers(writer, "Targets", group.Targets);
            WriteMembers(writer, "RequestedTargets", group.RequestedTargets);
            WriteMembers(writer, "ComputedTargets", group.ComputedTargets);
            writer.WriteField("PrimaryWaypoint.HasValue", group.PrimaryWaypoint.HasValue);
            if (group.PrimaryWaypoint.HasValue)
                WriteVector(writer, "PrimaryWaypoint", group.PrimaryWaypoint.Value);
            writer.WriteField("RequestedWaypoints.Count", group.RequestedWaypoints.Count);
            for (int index = 0; index < group.RequestedWaypoints.Count; index++)
                WriteVector(writer, "RequestedWaypoints." + index, group.RequestedWaypoints[index]);
            writer.WriteField("HasAttackRun", group.AttackRun != null);
            if (group.AttackRun != null)
            {
                writer.WriteField("AttackRun.State", (int)group.AttackRun.State);
                writer.WriteField(
                    "AttackRun.RemainingMilliseconds",
                    group.AttackRun.RemainingMilliseconds
                );
            }
        }

        /// <summary>
        /// Preserves a group's ordered unit references without depending on object identity.
        /// </summary>
        /// <param name="writer">The canonical binary destination.</param>
        /// <param name="name">The diagnostic field path within the current group.</param>
        /// <param name="units">The ordered referenced participants.</param>
        private static void WriteMembers(
            StateWriter writer,
            string name,
            IReadOnlyList<CombatUnit> units
        )
        {
            writer.WriteField(name + ".Count", units.Count);
            for (int index = 0; index < units.Count; index++)
                writer.WriteField(name + "." + index, units[index].InstanceID);
        }

        /// <summary>
        /// Preserves null group identity separately from every valid signed group identifier.
        /// </summary>
        /// <param name="writer">The canonical binary destination.</param>
        /// <param name="name">The diagnostic field path within the current scope.</param>
        /// <param name="id">The optional group identifier.</param>
        private static void WriteGroupID(StateWriter writer, string name, int? id)
        {
            writer.WriteField(name + ".HasValue", id.HasValue);
            if (id.HasValue)
                writer.WriteField(name + ".Value", id.Value);
        }

        /// <summary>
        /// Writes vector components without rounding their transported floating-point bits.
        /// </summary>
        /// <param name="writer">The canonical binary destination.</param>
        /// <param name="name">The diagnostic field path within the current scope.</param>
        /// <param name="vector">The shared vector.</param>
        private static void WriteVector(StateWriter writer, string name, Vector3 vector)
        {
            writer.WriteField(name + ".X", vector.X);
            writer.WriteField(name + ".Y", vector.Y);
            writer.WriteField(name + ".Z", vector.Z);
        }

        /// <summary>
        /// Converts a fixed-length digest into report-safe hexadecimal text.
        /// </summary>
        /// <param name="bytes">The completed hash.</param>
        /// <returns>The lowercase hexadecimal digest.</returns>
        private static string Hex(byte[] bytes) =>
            BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

        /// <summary>
        /// Writes the hash input and its named diagnostic values together so sidecars describe the actual comparison.
        /// </summary>
        private sealed class StateWriter : BinaryWriter
        {
            private readonly PausedState _state = new();
            internal string Prefix = string.Empty;

            /// <summary>
            /// Retains the canonical binary destination while collecting one paused-state diagnostic.
            /// </summary>
            /// <param name="buffer">The bytes consumed by the state fingerprint.</param>
            internal StateWriter(MemoryStream buffer)
                : base(buffer, Encoding.UTF8, true) { }

            /// <summary>
            /// Appends one typed field without rounding its binary hash input.
            /// </summary>
            /// <param name="name">The field name within the current participant or group.</param>
            /// <param name="value">A public replicated Boolean, integer, float, or identity.</param>
            internal void WriteField(string name, object value)
            {
                string bits = string.Empty;
                string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                switch (value)
                {
                    case bool flag:
                        Write(flag);
                        break;
                    case int integer:
                        Write(integer);
                        break;
                    case uint unsigned:
                        Write(unsigned);
                        break;
                    case float number:
                        Write(number);
                        text = number.ToString("R", CultureInfo.InvariantCulture);
                        bits = BitConverter
                            .SingleToInt32Bits(number)
                            .ToString("x8", CultureInfo.InvariantCulture);
                        break;
                    case string identity:
                        Write(identity);
                        break;
                    default:
                        throw new ArgumentException(
                            "A paused comparison encountered an unsupported field type.",
                            nameof(value)
                        );
                }
                _state.fields.Add(
                    new PausedField
                    {
                        path = string.IsNullOrEmpty(Prefix) ? name : Prefix + "." + name,
                        type = value.GetType().Name,
                        value = text,
                        floatBits = bits,
                    }
                );
            }

            /// <summary>
            /// Writes this role's one-time sidecar after its exact binary input has been hashed.
            /// </summary>
            /// <param name="path">The role-specific diagnostic destination.</param>
            /// <param name="fingerprint">The digest published in the role report.</param>
            internal void Save(string path, string fingerprint)
            {
                _state.fingerprint = fingerprint;
                File.WriteAllText(path, JsonUtility.ToJson(_state, true));
            }
        }

        /// <summary>
        /// Stores one canonical state input using only credential-free public combat fields.
        /// </summary>
        [Serializable]
        private sealed class PausedState
        {
            public string fingerprint;
            public List<PausedField> fields = new();
        }

        /// <summary>
        /// Names one typed hash input and preserves a floating-point field's exact transported bits.
        /// </summary>
        [Serializable]
        private sealed class PausedField
        {
            public string path;
            public string type;
            public string value;
            public string floatBits;
        }
    }
}
