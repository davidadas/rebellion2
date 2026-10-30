using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Transfers one completed world and its runtime settings while retaining model reference identity.
    /// </summary>
    internal static class MultiplayerRecoverySnapshotCodec
    {
        private static readonly JsonSerializerSettings _settings = CreateSettings();

        /// <summary>
        /// Captures the completed world after all turn processing and battle decisions finish.
        /// </summary>
        /// <param name="session">The paused authoritative simulation.</param>
        /// <returns>The compressed, authenticated continuation envelope.</returns>
        public static MultiplayerSnapshot Capture(GameSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            GameSessionRecoveryState state = session.CaptureRecoveryState();
            return MultiplayerSnapshotCodec.CapturePayload(
                MultiplayerSnapshotSerializer.SerializeValue(
                    state,
                    typeof(GameSessionRecoveryState),
                    _settings
                )
            );
        }

        /// <summary>
        /// Decodes one independent graph without running a gameplay phase or reconstructing a decision.
        /// </summary>
        /// <param name="payload">The compressed continuation.</param>
        /// <param name="hash">The expected payload hash.</param>
        /// <returns>The decoded continuation ready for the session owner's restoration contract.</returns>
        public static GameSessionRecoveryState Deserialize(byte[] payload, string hash)
        {
            return (GameSessionRecoveryState)
                MultiplayerSnapshotSerializer.DeserializeValue(
                    MultiplayerSnapshotCodec.RestorePayload(payload, hash),
                    typeof(GameSessionRecoveryState),
                    _settings
                );
        }

        /// <summary>
        /// Constructs an independent session against the exact verified content without disk-load reconciliation.
        /// </summary>
        /// <param name="payload">The compressed continuation.</param>
        /// <param name="hash">The expected payload hash.</param>
        /// <param name="data">The exact installed gameplay catalog.</param>
        /// <returns>The independently recovered session.</returns>
        public static GameSession Restore(byte[] payload, string hash, GameDataCatalog data)
        {
            return GameSession.RestoreRecoveryState(Deserialize(payload, hash), data);
        }

        /// <summary>
        /// Retains reference identity across the completed world and its runtime settings.
        /// </summary>
        /// <returns>The strict continuation schema policy.</returns>
        private static JsonSerializerSettings CreateSettings()
        {
            JsonSerializerSettings settings = MultiplayerSnapshotSerializer.CreateSettings(
                new RecoveryContracts(),
                new MultiplayerSnapshotSerializer.SnapshotTypes()
            );
            settings.PreserveReferencesHandling = PreserveReferencesHandling.All;
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;
            return settings;
        }

        /// <summary>
        /// Adds the runtime links and authoritative faction rules omitted from disk persistence.
        /// </summary>
        private sealed class RecoveryContracts : MultiplayerSnapshotSerializer.SnapshotContracts
        {
            /// <summary>
            /// Writes fixed-size arrays by value while retaining tracked references for their object elements.
            /// </summary>
            /// <param name="objectType">The admitted collection type.</param>
            /// <returns>The collection contract supported by the reference-aware reader.</returns>
            protected override JsonArrayContract CreateArrayContract(Type objectType)
            {
                JsonArrayContract contract = base.CreateArrayContract(objectType);
                if (objectType.IsArray)
                    contract.IsReference = false;
                return contract;
            }

            /// <summary>
            /// Includes only explicitly required runtime links in addition to the persisted model contract.
            /// </summary>
            /// <param name="objectType">The model or continuation fact type.</param>
            /// <returns>The admitted ordered members.</returns>
            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                List<MemberInfo> members = base.GetSerializableMembers(objectType);
                if (typeof(BaseSceneNode).IsAssignableFrom(objectType))
                {
                    members.Add(
                        typeof(BaseSceneNode).GetProperty(nameof(BaseSceneNode.ParentNode))
                    );
                    members.Add(
                        typeof(BaseSceneNode).GetProperty(nameof(BaseSceneNode.LastParentNode))
                    );
                }
                return members.Distinct().ToList();
            }

            /// <summary>
            /// Avoids identifier minting and hierarchy transitions while decoding reference-linked state.
            /// </summary>
            /// <param name="member">The admitted model member.</param>
            /// <param name="memberSerialization">The enclosing contract policy.</param>
            /// <returns>The member's side-effect-free recovery accessor.</returns>
            protected override JsonProperty CreateProperty(
                MemberInfo member,
                MemberSerialization memberSerialization
            )
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                if (
                    member.DeclaringType == typeof(GameRoot)
                    && ReflectionHelper.GetMemberType(member) == typeof(GalaxyMap)
                )
                    property.ValueProvider = new RecoveryGalaxyProvider(property.ValueProvider);
                else if (
                    member.DeclaringType == typeof(BaseGameEntity)
                    && member.Name == nameof(BaseGameEntity.InstanceID)
                )
                    property.ValueProvider = new RecoveryIdentityProvider(property.ValueProvider);
                else if (
                    member.DeclaringType == typeof(FactionSettings)
                    && member.Name == nameof(FactionSettings.ResourceProcessingPointsPerFacility)
                )
                    property.ValueProvider = new RecoveryProcessingRateProvider(
                        property.ValueProvider
                    );
                return property;
            }
        }

        /// <summary>
        /// Restores the exact processing rate without normalizing an untouched constructor value.
        /// </summary>
        private sealed class RecoveryProcessingRateProvider : IValueProvider
        {
            private readonly IValueProvider _inner;

            /// <summary>
            /// Retains the model read accessor while replacing normalized assignment.
            /// </summary>
            /// <param name="inner">The cached processing-rate accessor.</param>
            public RecoveryProcessingRateProvider(IValueProvider inner)
            {
                _inner = inner;
            }

            /// <summary>
            /// Reads the existing processing rate.
            /// </summary>
            /// <param name="target">The source faction settings.</param>
            /// <returns>The captured rate.</returns>
            public object GetValue(object target) => _inner.GetValue(target);

            /// <summary>
            /// Restores the rate without applying configuration-editing constraints.
            /// </summary>
            /// <param name="target">The decoded faction settings.</param>
            /// <param name="value">The exact captured rate.</param>
            public void SetValue(object target, object value) =>
                ((FactionSettings)target).RestoreRecoveryResourceProcessingPoints((int)value);
        }

        /// <summary>
        /// Restores the decoded graph without the ordinary galaxy setter's parent/index reconstruction.
        /// </summary>
        private sealed class RecoveryGalaxyProvider : IValueProvider
        {
            private readonly IValueProvider _inner;

            /// <summary>
            /// Retains the existing read accessor while replacing transitionful assignment.
            /// </summary>
            /// <param name="inner">The cached model accessor.</param>
            public RecoveryGalaxyProvider(IValueProvider inner)
            {
                _inner = inner;
            }

            /// <summary>
            /// Reads the authoritative galaxy.
            /// </summary>
            /// <param name="target">The source game.</param>
            /// <returns>The galaxy object.</returns>
            public object GetValue(object target)
            {
                return _inner.GetValue(target);
            }

            /// <summary>
            /// Assigns an already linked galaxy without changing any persisted or historical parent state.
            /// </summary>
            /// <param name="target">The decoded game.</param>
            /// <param name="value">The decoded galaxy.</param>
            public void SetValue(object target, object value)
            {
                ((GameRoot)target).SetRecoveryGalaxy((GalaxyMap)value);
            }
        }

        /// <summary>
        /// Preserves raw nullable identities rather than allocating identities through a model getter.
        /// </summary>
        private sealed class RecoveryIdentityProvider : IValueProvider
        {
            private readonly IValueProvider _inner;

            /// <summary>
            /// Retains the normal identifier setter for decoded values.
            /// </summary>
            /// <param name="inner">The cached model accessor.</param>
            public RecoveryIdentityProvider(IValueProvider inner)
            {
                _inner = inner;
            }

            /// <summary>
            /// Reads the assigned identity without invoking lazy allocation.
            /// </summary>
            /// <param name="target">The source entity.</param>
            /// <returns>The original identity, including null.</returns>
            public object GetValue(object target)
            {
                return ((BaseGameEntity)target).GetRecoveryInstanceID();
            }

            /// <summary>
            /// Restores the encoded identity without generating a replacement.
            /// </summary>
            /// <param name="target">The decoded entity.</param>
            /// <param name="value">The original nullable identity.</param>
            public void SetValue(object target, object value)
            {
                _inner.SetValue(target, value);
            }
        }
    }
}
