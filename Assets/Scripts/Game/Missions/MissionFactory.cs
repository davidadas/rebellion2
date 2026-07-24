using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Thin router that delegates mission creation to each Mission subclass's TryCreate method.
    /// Handles shared validation (faction restrictions, participant eligibility) before routing.
    /// </summary>
    public class MissionFactory
    {
        private readonly GameRoot _game;
        private static readonly List<MissionOption> _options = new List<MissionOption>
        {
            new MissionOption(DiplomacyMission.MissionTypeID, "Diplomacy", OfficerRating.Diplomacy),
            new MissionOption(
                RecruitmentMission.MissionTypeID,
                "Recruitment",
                OfficerRating.Leadership
            ),
            new MissionOption(
                RescueMission.MissionTypeID,
                "Rescue",
                OfficerRating.Combat,
                OfficerRating.Espionage
            ),
            new MissionOption(
                SabotageMission.MissionTypeID,
                "Sabotage",
                OfficerRating.Combat,
                OfficerRating.Espionage
            ),
            new MissionOption(
                AbductionMission.MissionTypeID,
                "Abduction",
                OfficerRating.Combat,
                OfficerRating.Espionage
            ),
            new MissionOption(
                SubdueUprisingMission.MissionTypeID,
                "Subdue Uprising",
                OfficerRating.Leadership
            ),
            new MissionOption(
                AssassinationMission.MissionTypeID,
                "Assassination",
                OfficerRating.Combat,
                OfficerRating.Espionage
            ),
            new MissionOption(
                InciteUprisingMission.MissionTypeID,
                "Incite Uprising",
                OfficerRating.Leadership,
                OfficerRating.Espionage
            ),
            new MissionOption(
                ReconnaissanceMission.MissionTypeID,
                "Reconnaissance",
                OfficerRating.Espionage,
                OfficerRating.Espionage
            ),
            new MissionOption(
                ResearchMission.MissionTypeID,
                "Ship Design Research",
                OfficerRating.ShipResearch,
                discipline: ResearchDiscipline.ShipDesign
            ),
            new MissionOption(
                ResearchMission.MissionTypeID,
                "Troop Training Research",
                OfficerRating.TroopResearch,
                discipline: ResearchDiscipline.TroopTraining
            ),
            new MissionOption(
                ResearchMission.MissionTypeID,
                "Facility Design Research",
                OfficerRating.FacilityResearch,
                discipline: ResearchDiscipline.FacilityDesign
            ),
            new MissionOption(
                JediTrainingMission.MissionTypeID,
                "Jedi Training",
                OfficerRating.Diplomacy
            ),
            new MissionOption(
                EspionageMission.MissionTypeID,
                "Espionage",
                OfficerRating.Espionage,
                OfficerRating.Espionage
            ),
        };

        /// <summary>
        /// Initializes a mission factory for the supplied game state.
        /// </summary>
        /// <param name="game">The game state used for validation and mission configuration.</param>
        public MissionFactory(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Returns the mission options that can create a mission from the supplied context.
        /// </summary>
        /// <param name="context">The resolved mission context containing the target and participants to evaluate.</param>
        /// <returns>The mission options that pass mission creation validation.</returns>
        public List<MissionOption> GetAvailableMissionOptions(MissionContext context)
        {
            List<MissionOption> options = new List<MissionOption>();
            if (context == null)
                return options;

            foreach (MissionOption option in _options)
            {
                MissionContext optionContext = CreateOptionContext(context, option);
                if (TryCreateMission(optionContext, out _))
                    options.Add(option);
            }

            return options;
        }

        /// <summary>
        /// Creates a mission when the supplied context is valid.
        /// </summary>
        /// <param name="context">The resolved mission context to evaluate.</param>
        /// <param name="mission">The created mission when creation succeeds.</param>
        /// <returns>True when a mission was created.</returns>
        public bool TryCreateMission(MissionContext context, out Mission mission)
        {
            mission = null;
            if (_game == null || context == null || string.IsNullOrEmpty(context.MissionTypeID))
                return false;

            MissionContext resolvedContext = new MissionContext
            {
                Game = _game,
                MissionTypeID = context.MissionTypeID,
                OwnerInstanceId = context.OwnerInstanceId,
                Location = context.Location,
                SelectedTarget = context.SelectedTarget,
                MainParticipants = context.MainParticipants ?? new List<IMissionParticipant>(),
                DecoyParticipants = context.DecoyParticipants ?? new List<IMissionParticipant>(),
                TargetOfficer = context.TargetOfficer ?? context.SelectedTarget as Officer,
                Discipline = context.Discipline,
            };

            if (resolvedContext.MainParticipants.Count == 0)
                return false;

            Faction faction = _game.Factions.Find(f =>
                f.InstanceID == resolvedContext.OwnerInstanceId
            );
            if (faction == null)
                return false;

            if (faction.DisallowedMissionTypeIDs.Contains(resolvedContext.MissionTypeID))
                return false;

            if (!HasOperationalTarget(resolvedContext))
                return false;

            foreach (
                IMissionParticipant missionParticipant in resolvedContext.MainParticipants.Concat(
                    resolvedContext.DecoyParticipants
                )
            )
            {
                if (
                    missionParticipant?.GetOwnerInstanceID() != resolvedContext.OwnerInstanceId
                    || missionParticipant.IsOnMission()
                    || !missionParticipant.IsMovable()
                    || missionParticipant.CanPerformMission(resolvedContext.MissionTypeID) != true
                )
                    return false;
            }

            mission = TryCreateByType(resolvedContext);
            return mission != null;
        }

        private bool HasOperationalTarget(MissionContext context)
        {
            ISceneNode target = context.MissionTypeID switch
            {
                SabotageMission.MissionTypeID => context.SelectedTarget ?? context.Location,
                AbductionMission.MissionTypeID
                or AssassinationMission.MissionTypeID
                or RescueMission.MissionTypeID => context.TargetOfficer
                    ?? context.SelectedTarget as Officer,
                _ => null,
            };
            if (target == null)
                return true;

            ISceneNode liveTarget = _game.GetSceneNodeByInstanceID<ISceneNode>(target.InstanceID);
            return Mission.IsOperationalTarget(liveTarget);
        }

        /// <summary>
        /// Creates a mission context for a specific mission option.
        /// </summary>
        /// <param name="context">The source context containing target and participant details.</param>
        /// <param name="option">The mission option being evaluated.</param>
        /// <returns>The context populated with the option's mission type and discipline.</returns>
        private static MissionContext CreateOptionContext(
            MissionContext context,
            MissionOption option
        )
        {
            return new MissionContext
            {
                Game = context.Game,
                MissionTypeID = option.MissionTypeID,
                OwnerInstanceId = context.OwnerInstanceId,
                Location = context.Location,
                SelectedTarget = context.SelectedTarget,
                MainParticipants = context.MainParticipants,
                DecoyParticipants = context.DecoyParticipants,
                TargetOfficer = context.TargetOfficer,
                Discipline = option.Discipline,
            };
        }

        /// <summary>
        /// Dispatches to the appropriate mission subclass's TryCreate based on mission context.
        /// </summary>
        /// <param name="ctx">Context containing participants, target, and owner info.</param>
        /// <returns>The created mission, or null if creation fails.</returns>
        private static Mission TryCreateByType(MissionContext ctx)
        {
            return ctx.MissionTypeID switch
            {
                ReconnaissanceMission.MissionTypeID => ReconnaissanceMission.TryCreate(ctx),
                DiplomacyMission.MissionTypeID => DiplomacyMission.TryCreate(ctx),
                RecruitmentMission.MissionTypeID => RecruitmentMission.TryCreate(ctx),
                SubdueUprisingMission.MissionTypeID => SubdueUprisingMission.TryCreate(ctx),
                AbductionMission.MissionTypeID => AbductionMission.TryCreate(ctx),
                AssassinationMission.MissionTypeID => AssassinationMission.TryCreate(ctx),
                EspionageMission.MissionTypeID => EspionageMission.TryCreate(ctx),
                SabotageMission.MissionTypeID => SabotageMission.TryCreate(ctx),
                InciteUprisingMission.MissionTypeID => InciteUprisingMission.TryCreate(ctx),
                RescueMission.MissionTypeID => RescueMission.TryCreate(ctx),
                ResearchMission.MissionTypeID => ctx.Discipline.HasValue
                    ? ResearchMission.TryCreate(ctx, ctx.Discipline.Value)
                    : null,
                JediTrainingMission.MissionTypeID => JediTrainingMission.TryCreate(ctx),
                _ => null,
            };
        }
    }
}
