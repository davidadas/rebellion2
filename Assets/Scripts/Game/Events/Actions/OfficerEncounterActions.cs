using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Resolves deterministic Force-awareness checks shared by encounter actions.
    /// </summary>
    internal static class ForceEncounterDetection
    {
        /// <summary>
        /// Rolls whether two known Force ranks produce a detectable encounter.
        /// </summary>
        /// <param name="first">The first officer.</param>
        /// <param name="second">The second officer.</param>
        /// <param name="chanceModifier">The authored percentage-point modifier.</param>
        /// <param name="provider">The deterministic simulation random source.</param>
        /// <returns>True when the detection roll succeeds.</returns>
        public static bool Succeeds(
            Officer first,
            Officer second,
            int chanceModifier,
            IRandomNumberProvider provider
        )
        {
            if (first == null || second == null || provider == null)
                return false;

            int firstRank = first.ForceRank;
            int secondRank = second.ForceRank;
            if (firstRank == 0 || secondRank == 0)
                return false;

            int chance = Math.Min(100, Math.Max(0, firstRank + secondRank + chanceModifier));
            return chance > 0 && provider.NextInt(0, 100) < chance;
        }
    }

    /// <summary>
    /// Requests authoritative resolution of an encounter between two opposing officers.
    /// </summary>
    [PersistableObject(Name = "TriggerDuel")]
    public class TriggerDuelAction : GameAction
    {
        [PersistableAttribute]
        public string FirstOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string SecondOfficerInstanceID { get; set; }

        public string ImagePath { get; set; }
        public string VoicePath { get; set; }

        /// <summary>
        /// Creates an empty action for content deserialization.
        /// </summary>
        public TriggerDuelAction()
            : base() { }

        /// <summary>
        /// Requests authoritative resolution of a linked-officer encounter.
        /// </summary>
        /// <param name="game">The game state used to resolve the officers.</param>
        /// <returns>The encounter request, or no result when either officer is unavailable.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, null, null);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            Officer encountered = game.GetSceneNodeByInstanceID<Officer>(FirstOfficerInstanceID);
            Officer opposing = game.GetSceneNodeByInstanceID<Officer>(SecondOfficerInstanceID);
            if (encountered == null || opposing == null)
                return new List<GameResult>();

            if (
                !ForceEncounterDetection.Succeeds(
                    encountered,
                    opposing,
                    -100,
                    provider ?? game.Random
                )
            )
                return new List<GameResult>();

            if (context?.TriggerResult is MissionCompletedResult completion)
            {
                bool firstParticipated = completion.Participants.Contains(encountered);
                bool secondParticipated = completion.Participants.Contains(opposing);
                if (firstParticipated == secondParticipated)
                    return new List<GameResult>();
                if (secondParticipated)
                    (encountered, opposing) = (opposing, encountered);
            }

            return new List<GameResult>
            {
                new OfficerEncounterRequestedResult
                {
                    EncounteredOfficer = encountered,
                    OpposingOfficer = opposing,
                    ImagePath = ImagePath,
                    VoicePath = VoicePath,
                    Tick = game.CurrentTick,
                },
            };
        }
    }
}
