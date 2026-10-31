using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
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
            using MemoryStream stream = new();
            new GameSerializer(
                typeof(ContentState),
                new GameSerializerSettings
                {
                    Idendent = false,
                    NewLineHandling = NewLineHandling.Entitize,
                    MemberValueProvider = ReadContentMember,
                }
            ).Serialize(stream, state);
            using SHA256 hash = SHA256.Create();
            return BitConverter
                .ToString(hash.ComputeHash(stream.ToArray()))
                .Replace("-", string.Empty);
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
        /// Reads assigned prototype identities without generating identifiers during content hashing.
        /// </summary>
        /// <param name="member">The persisted definition member.</param>
        /// <param name="target">The definition containing the member.</param>
        /// <returns>The assigned identity or the ordinary persisted member value.</returns>
        private static object ReadContentMember(MemberInfo member, object target)
        {
            return
                member.DeclaringType == typeof(BaseGameEntity)
                && member.Name == nameof(BaseGameEntity.InstanceID)
                ? ((BaseGameEntity)target).PeekInstanceID()
                : ReflectionHelper.GetMemberValue(member, target);
        }
    }
}
