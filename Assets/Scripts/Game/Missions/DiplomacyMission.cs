using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    public class DiplomacyMission : Mission
    {
        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public DiplomacyMission()
            : base()
        {
            ConfigKey = "Diplomacy";
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Diplomacy;
        }

        private DiplomacyMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                "Diplomacy",
                ownerInstanceId,
                RequirePlanetTarget(target, "Diplomacy").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Diplomacy,
                null
            ) { }

        /// <summary>
        /// Returns a new DiplomacyMission if the target is a valid planet, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <returns>A configured mission, or null if the planet is ineligible.</returns>
        public static DiplomacyMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Target is Planet planet))
                return null;

            if (
                !planet.IsColonized
                || planet.IsInUprising
                || !planet.WasVisitedBy(ctx.OwnerInstanceId)
                || planet.GetPopularSupport(ctx.OwnerInstanceId) >= 100
            )
                return null;

            string planetOwner = planet.GetOwnerInstanceID();
            if (planetOwner != null && planetOwner != ctx.OwnerInstanceId)
                return null;

            return new DiplomacyMission(
                ctx.OwnerInstanceId,
                ctx.Target,
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Extends base cancellation to also cancel when the target planet enters uprising or
        /// is taken by a third faction.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission should be aborted.</returns>
        public override bool ShouldAbort(GameRoot game)
        {
            if (base.ShouldAbort(game))
                return true;
            if (GetParent() is Planet planet)
            {
                if (planet.IsInUprising)
                    return true;
                string owner = planet.GetOwnerInstanceID();
                if (owner != null && owner != OwnerInstanceID)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Diplomacy missions are never foiled — they target own or neutral planets.
        /// </summary>
        /// <param name="defenseScore">Ignored.</param>
        /// <returns>Always 0.</returns>
        protected override double GetFoilProbability(double defenseScore) => 0;

        /// <summary>
        /// Returns the participant's diplomacy success probability for the current target.
        /// </summary>
        /// <param name="agent">The participant whose diplomacy rating is evaluated.</param>
        /// <returns>The participant's diplomacy success probability.</returns>
        protected override double GetAgentProbability(IMissionParticipant agent)
        {
            if (!(GetParent() is Planet planet))
                return base.GetAgentProbability(agent);

            int score =
                GetTargetTroopState(planet)
                - planet.GetPopularSupport(OwnerInstanceID)
                + agent.GetEffectiveRating(OfficerRating.Diplomacy);
            return SuccessProbabilityTable.Lookup(score);
        }

        /// <summary>
        /// Applies diplomacy popular support movement to the target planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for configured support rolls.</param>
        /// <returns>Always empty; ownership transfers are handled by the planetary control system.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            Planet planet = GetParent() as Planet;
            if (planet == null)
                return new List<GameResult>();

            GameConfig.SupportShiftConfig config = game.Config.SupportShift;
            int currentSupport = planet.GetPopularSupport(OwnerInstanceID);
            int supportAfterExecuteBonus =
                currentSupport
                + GetFactionSuccessSupportBonus(game, config.DiplomacyCompletionSupportBonus);
            int supportShift = GetFactionSupportShift(planet, config, provider);
            planet.SetPopularSupport(OwnerInstanceID, supportAfterExecuteBonus + supportShift);

            return new List<GameResult>();
        }

        /// <summary>
        /// Returns the fixed support bonus applied after a successful diplomacy roll.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="bonus">The configured support bonus.</param>
        /// <returns>The support bonus signed for the mission faction.</returns>
        private int GetFactionSuccessSupportBonus(GameRoot game, int bonus)
        {
            foreach (Faction faction in game.GetFactions())
            {
                if (faction.InstanceID == OwnerInstanceID)
                    return faction.Settings.InvertSupportShift ? -bonus : bonus;
            }

            return bonus;
        }

        /// <summary>
        /// Returns the rolled support shift for an owned or neutral diplomacy target.
        /// </summary>
        /// <param name="planet">The mission target planet.</param>
        /// <param name="config">Support shift configuration values.</param>
        /// <param name="provider">RNG provider for configured support rolls.</param>
        /// <returns>The support shift to apply to the mission faction.</returns>
        private int GetFactionSupportShift(
            Planet planet,
            GameConfig.SupportShiftConfig config,
            IRandomNumberProvider provider
        )
        {
            string planetOwner = planet.GetOwnerInstanceID();
            if (planetOwner == OwnerInstanceID)
            {
                return RollSupportShift(
                    config.DiplomacyOwnedPlanetSupportBase,
                    config.DiplomacyOwnedPlanetSupportRange,
                    provider
                );
            }

            if (string.IsNullOrEmpty(planetOwner))
            {
                return RollSupportShift(
                    config.DiplomacyNeutralPlanetSupportBase,
                    config.DiplomacyNeutralPlanetSupportRange,
                    provider
                );
            }

            return 0;
        }

        /// <summary>
        /// Rolls a support shift from the configured base value and random range.
        /// </summary>
        /// <param name="baseShift">The configured base support shift.</param>
        /// <param name="range">The configured random support range.</param>
        /// <param name="provider">RNG provider for the support roll.</param>
        /// <returns>The rolled support shift.</returns>
        private static int RollSupportShift(
            int baseShift,
            int range,
            IRandomNumberProvider provider
        )
        {
            return baseShift + (range > 0 ? provider.NextInt(0, range + 1) : 0);
        }

        /// <summary>
        /// Returns the target troop-state value used for diplomacy scoring.
        /// </summary>
        /// <param name="planet">The mission target planet.</param>
        /// <returns>The target troop-state value, or 0 when no completed regiment is present.</returns>
        private static int GetTargetTroopState(Planet planet)
        {
            foreach (Regiment regiment in planet.GetAllRegiments())
            {
                if (regiment.ManufacturingStatus == ManufacturingStatus.Complete)
                    return regiment.UprisingDefense;
            }

            return 0;
        }

        /// <summary>
        /// Returns true while the planet remains eligible for further diplomacy attempts.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the planet is owned or neutral, not in uprising, and support is below 100.</returns>
        public override bool CanContinue(GameRoot game)
        {
            if (GetParent() is Planet planet)
            {
                return (
                        planet.GetOwnerInstanceID() == GetOwnerInstanceID()
                        || planet.GetOwnerInstanceID() == null
                    )
                    && planet.GetPopularSupport(GetOwnerInstanceID()) < 100
                    && !planet.IsInUprising;
            }
            return false;
        }
    }
}
