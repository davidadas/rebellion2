using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Presentation.Advisor;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Routes intelligence from a planet controller to one recipient faction.
    /// </summary>
    [PersistableObject]
    public sealed class InformantFactionRoute
    {
        public string ControllerFactionInstanceID { get; set; }
        public string RecipientFactionInstanceID { get; set; }
    }

    /// <summary>
    /// Resolves a controlled-world informant check with data-defined faction routing
    /// and uniformly weighted intelligence categories.
    /// </summary>
    [PersistableObject(Name = "GatherInformantIntelligence")]
    public sealed class GatherInformantIntelligenceAction : GameAction
    {
        public int MaximumPopularSupport { get; set; } = 100;
        public string Subject { get; set; }
        public string Body { get; set; }
        public MessageType MessageType { get; set; } = MessageType.Advice;
        public MessageBackgroundImage BackgroundImage { get; set; }
        public MessageAudio BackgroundAudio { get; set; }
        public AdvisorNotification AdvisorNotification { get; set; }
        public List<InformantFactionRoute> FactionRoutes { get; set; } =
            new List<InformantFactionRoute>();
        public List<PlanetIntelligenceCategory> IntelligenceChoices { get; set; } =
            new List<PlanetIntelligenceCategory>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            IRandomNumberProvider provider = context.Random;
            Planet planet = context.Activation?.GetTarget<Planet>();
            if (planet == null)
                throw new InvalidOperationException(
                    "GatherInformantIntelligence must execute from a planet-scoped game event."
                );

            InformantFactionRoute route = FactionRoutes.FirstOrDefault(candidate =>
                candidate.ControllerFactionInstanceID == planet.OwnerInstanceID
            );
            if (route == null)
                return new List<GameResult>();

            int support = Math.Max(
                0,
                Math.Min(MaximumPopularSupport, planet.GetPopularSupport(planet.OwnerInstanceID))
            );
            if (provider.NextInt(0, MaximumPopularSupport) < support)
                return new List<GameResult>();

            PlanetIntelligenceCategory categories = IntelligenceChoices[
                provider.NextInt(0, IntelligenceChoices.Count)
            ];
            Faction recipient = game.GetFactionByOwnerInstanceID(route.RecipientFactionInstanceID);
            string backgroundImagePath = MessageMediaResolver.Resolve(BackgroundImage, context);
            string backgroundAudioPath = MessageMediaResolver.Resolve(BackgroundAudio, context);
            return new List<GameResult>
            {
                new PlanetIntelligenceResult
                {
                    Recipient = recipient,
                    Planet = planet,
                    Categories = categories,
                    Tick = game.CurrentTick,
                },
                new MessageRequestedResult
                {
                    Recipient = recipient,
                    SubjectNode = planet,
                    Location = planet,
                    MessageType = MessageType,
                    Subject = Subject,
                    Body = Body,
                    BackgroundImageKey = BackgroundImage?.Key,
                    BackgroundImagePath = backgroundImagePath,
                    BackgroundAudioPath = backgroundAudioPath,
                    AdvisorNotification = AdvisorNotification,
                    Tick = game.CurrentTick,
                },
            };
        }
    }
}
