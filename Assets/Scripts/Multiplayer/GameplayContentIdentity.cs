using System;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Identifies the composed gameplay definitions used by factories, rules and event execution.
    /// Pack labels and local presentation themes do not substitute for this data identity.
    /// </summary>
    internal static class GameplayContentIdentity
    {
        private static readonly JsonSerializerSettings _settings =
            MultiplayerSnapshotSerializer.CreateSettings(
                new ContentContracts(),
                new MultiplayerSnapshotSerializer.SnapshotTypes()
            );

        /// <summary>
        /// Hashes the actual composed definitions without allocating identities on unit prototypes.
        /// This operation belongs to content admission, never the browser's rendering loop.
        /// </summary>
        /// <param name="data">The loaded gameplay definitions.</param>
        /// <returns>The SHA-256 identity of the selected definitions and rules.</returns>
        public static string Compute(GameDataCatalog data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            ContentState state = new()
            {
                Rules = data.GameConfig,
                Generation = data.GenerationConfig,
                Factions = data.Factions,
                FactionRules = Array.ConvertAll(data.Factions, faction => faction.Settings),
                Sectors = data.PlanetSectors,
                Buildings = data.Buildings,
                CapitalShips = data.CapitalShips,
                Starfighters = data.Starfighters,
                Regiments = data.Regiments,
                SpecialForces = data.SpecialForces,
                Officers = data.Officers,
                Traits = data.OfficerTraits,
                Events = data.GameEvents,
                Messages = data.MessageDefinitions,
            };
            byte[] bytes = MultiplayerSnapshotSerializer.SerializeValue(
                state,
                typeof(ContentState),
                _settings
            );
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        /// <summary>
        /// Selects the catalog data consumed by simulation services and game construction.
        /// </summary>
        [PersistableObject]
        private sealed class ContentState
        {
            public GameConfig Rules { get; set; }
            public GameGenerationConfig Generation { get; set; }
            public Faction[] Factions { get; set; }
            public FactionSettings[] FactionRules { get; set; }
            public PlanetSector[] Sectors { get; set; }
            public Building[] Buildings { get; set; }
            public CapitalShip[] CapitalShips { get; set; }
            public Starfighter[] Starfighters { get; set; }
            public Regiment[] Regiments { get; set; }
            public SpecialForces[] SpecialForces { get; set; }
            public Officer[] Officers { get; set; }
            public OfficerTraitDefinition[] Traits { get; set; }
            public GameEvent[] Events { get; set; }
            public MessageDefinition[] Messages { get; set; }
        }

        /// <summary>
        /// Reuses the model schema with side-effect-free prototype identity access.
        /// </summary>
        private sealed class ContentContracts : MultiplayerSnapshotSerializer.SnapshotContracts
        {
            /// <summary>
            /// Reads assigned prototype identities without invoking lazy identifier generation.
            /// </summary>
            /// <param name="member">The existing model member.</param>
            /// <param name="memberSerialization">The selected serialization policy.</param>
            /// <returns>The read-only content contract.</returns>
            protected override JsonProperty CreateProperty(
                MemberInfo member,
                MemberSerialization memberSerialization
            )
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                if (
                    member.DeclaringType == typeof(BaseGameEntity)
                    && member.Name == nameof(BaseGameEntity.InstanceID)
                )
                    property.ValueProvider = new AssignedIdentity();
                property.Writable = false;
                return property;
            }
        }

        /// <summary>
        /// Reads the existing identity while leaving unassigned template identities untouched.
        /// </summary>
        private sealed class AssignedIdentity : IValueProvider
        {
            /// <summary>
            /// Reads the raw model identity without generating a new value.
            /// </summary>
            /// <param name="target">The existing prototype.</param>
            /// <returns>The explicitly assigned identity, including null.</returns>
            public object GetValue(object target) =>
                ((BaseGameEntity)target).GetRecoveryInstanceID();

            /// <summary>
            /// Rejects mutation through this write-only hashing policy.
            /// </summary>
            /// <param name="target">The unsupported destination.</param>
            /// <param name="value">The unsupported decoded identity.</param>
            public void SetValue(object target, object value) =>
                throw new NotSupportedException("Content identity does not decode model values.");
        }
    }
}
