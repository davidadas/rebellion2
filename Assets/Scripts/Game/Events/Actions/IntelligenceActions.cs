using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
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
    [PersistableObject(Name = "InformantIntelligence")]
    public sealed class InformantIntelligenceAction : GameAction
    {
        public int MaximumPopularSupport { get; set; } = 100;
        public string Title { get; set; }
        public string Body { get; set; }
        public MessageType MessageType { get; set; } = MessageType.Advice;
        public MessageBackgroundImage BackgroundImage { get; set; }
        public string AudioPath { get; set; }
        public AdvisorNotification AdvisorNotification { get; set; }
        public List<InformantFactionRoute> FactionRoutes { get; set; } =
            new List<InformantFactionRoute>();
        public List<PlanetIntelligenceCategory> IntelligenceChoices { get; set; } =
            new List<PlanetIntelligenceCategory>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            throw new InvalidOperationException(
                "InformantIntelligence must execute from a planet-scoped game event."
            );
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            AdvisorNotification?.Validate();
            Planet planet = context?.GetScopeTarget<Planet>();
            if (planet == null)
                return Execute(game);

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
            return new List<GameResult>
            {
                new PlanetIntelligenceResult
                {
                    Recipient = recipient,
                    Planet = planet,
                    Categories = categories,
                    Tick = game.CurrentTick,
                },
                new NarrativeMessageResult
                {
                    Recipient = recipient,
                    SubjectNode = planet,
                    Location = planet,
                    MessageType = MessageType,
                    Subject = Title,
                    Body = Body,
                    BackgroundImageKey = BackgroundImage?.Key,
                    BackgroundImagePath = BackgroundImage?.Path,
                    AudioPath = AudioPath,
                    AdvisorNotification = AdvisorNotification,
                    Tick = game.CurrentTick,
                },
            };
        }
    }
}
