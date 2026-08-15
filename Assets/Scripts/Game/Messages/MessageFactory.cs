using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Notifications;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

#region Core
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Builds faction message deliveries from game results and configured message definitions.
    /// </summary>
    public partial class MessageFactory
    {
        private readonly MessageDefinition[] _definitions;
        private readonly MessageTemplateBuilder _templateBuilder = new MessageTemplateBuilder();
        private readonly AuthoredMessageRequestFactory _authoredMessageRequestFactory;
        private readonly Dictionary<Message, MessageRequestedResult> _requests = new();

        /// <summary>
        /// Creates a message factory backed by the supplied message definitions.
        /// </summary>
        /// <param name="definitions">The message definitions used to select templates and images.</param>
        public MessageFactory(IEnumerable<MessageDefinition> definitions)
        {
            _definitions = definitions?.ToArray() ?? Array.Empty<MessageDefinition>();
            _authoredMessageRequestFactory = new AuthoredMessageRequestFactory(_templateBuilder);
        }

        /// <summary>
        /// Creates messages for the factions affected by the supplied game results.
        /// </summary>
        /// <param name="results">The game results to translate into message deliveries.</param>
        /// <param name="game">The game state used to resolve affected factions and display names.</param>
        /// <returns>The messages to add to each recipient faction.</returns>
        public List<MessageRequestedResult> CreateMessages(
            IEnumerable<GameResult> results,
            GameRoot game
        )
        {
            _requests.Clear();
            GameResult[] batch =
                results?.Where(result => result != null).ToArray() ?? Array.Empty<GameResult>();
            MissionCompletedResult[] missionResults = batch
                .OfType<MissionCompletedResult>()
                .ToArray();
            OfficerKilledResult[] killedResults = batch.OfType<OfficerKilledResult>().ToArray();
            ForceDiscoveryResult[] forceDiscoveryResults = batch
                .OfType<ForceDiscoveryResult>()
                .ToArray();
            GameObjectSabotagedResult[] sabotageResults = batch
                .OfType<GameObjectSabotagedResult>()
                .ToArray();
            SystemsRevealedResult[] systemIntelligenceResults = batch
                .OfType<SystemsRevealedResult>()
                .ToArray();
            List<MessageRequestedResult> deliveries = new List<MessageRequestedResult>();

            AddArrivalMessages(batch.OfType<UnitArrivedResult>(), game, deliveries);
            AddFacilityLossMessages(
                batch.OfType<GameObjectDestroyedOnArrivalResult>(),
                game,
                deliveries
            );
            AddSmugglingMessages(batch.OfType<SmugglingChangedResult>(), deliveries);
            AddMissionMessages(
                missionResults,
                killedResults,
                sabotageResults,
                systemIntelligenceResults,
                game,
                deliveries
            );
            AddRecruitmentMessages(batch.OfType<RecruitmentExhaustedResult>(), deliveries);
            AddOfficerMessages(
                batch.OfType<OfficerRecruitedResult>(),
                batch.OfType<OfficerCaptureStateResult>(),
                batch.OfType<OfficerInjuredResult>(),
                killedResults,
                missionResults,
                game,
                deliveries
            );
            AddTraitorDiscoveryMessages(batch.OfType<TraitorDiscoveredResult>(), game, deliveries);
            AddForceMessages(
                batch.OfType<ForceExperienceResult>(),
                forceDiscoveryResults,
                game,
                deliveries
            );
            AddSabotageMessages(sabotageResults, game, deliveries);
            AddResearchMessages(
                batch.OfType<ResearchOrderedResult>(),
                batch.OfType<ResearchExhaustedResult>(),
                deliveries
            );
            AddUprisingMessages(
                batch.OfType<PlanetNearUprisingResult>(),
                batch.OfType<PlanetUprisingStartedResult>(),
                batch.OfType<PlanetUprisingEndedResult>(),
                game,
                deliveries
            );
            AddOwnershipMessages(batch.OfType<PlanetOwnershipChangedResult>(), game, deliveries);
            AddObjectiveMessages(
                batch.OfType<PlanetOwnershipChangedResult>(),
                batch.OfType<HeadquartersDestroyedResult>(),
                game,
                deliveries
            );
            AddIncidentMessages(batch.OfType<PlanetIncidentResult>(), game, deliveries);
            AddBlockadeMessages(
                batch.OfType<BlockadeChangedResult>(),
                batch.OfType<EvacuationLossesResult>(),
                game,
                deliveries
            );
            AddMaintenanceMessages(batch.OfType<GameObjectAutoscrappedResult>(), game, deliveries);
            AddRepairMessages(
                batch.OfType<ShipHullDamageResult>(),
                batch.OfType<FighterDamageResult>(),
                game,
                deliveries
            );
            AddCombatMessages(
                batch.OfType<SpaceCombatResult>(),
                batch.OfType<BombardmentResult>(),
                batch.OfType<PlanetaryAssaultResult>(),
                game,
                deliveries
            );
            AddDeploymentMessages(batch.OfType<GameObjectDeployedResult>(), game, deliveries);
            AddManufacturingMessages(batch.OfType<ManufacturingIdleResult>(), deliveries);
            AddSeatOfPowerMessages(batch.OfType<SeatOfPowerChangedResult>(), game, deliveries);

            return deliveries;
        }

        /// <summary>
        /// Creates deliveries explicitly authored by event actions.
        /// </summary>
        public List<MessageRequestedResult> CreateAuthoredMessages(
            IEnumerable<MessageRequestedResult> requests
        ) => _authoredMessageRequestFactory.CreateRequests(requests).ToList();

        /// <summary>
        /// Adds Force-assisted traitor discovery reports.
        /// </summary>
        /// <param name="results">The traitor discovery results to process.</param>
        /// <param name="game">The game state used to resolve recipients and opposing factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddTraitorDiscoveryMessages(
            IEnumerable<TraitorDiscoveredResult> results,
            GameRoot game,
            List<MessageRequestedResult> deliveries
        )
        {
            foreach (TraitorDiscoveredResult result in results)
            {
                Faction faction = GetOwnerFaction(game, result.DiscoveredBy ?? result.Officer);
                Faction opposingFaction = game
                    ?.GetFactions()
                    .FirstOrDefault(candidate => candidate.InstanceID != faction?.InstanceID);
                Planet planet = GetOfficerPlanet(result.Officer, result.Context);
                Officer discoverer = result.DiscoveredBy as Officer;
                Message message = WithAdvisorSubject(
                    WithEventLocation(
                        CreateMessage(
                            GetDefinition(MessageResultType.TraitorDiscovered),
                            faction,
                            new Dictionary<string, string>
                            {
                                {
                                    "discoverer",
                                    GetDisplayName(result.DiscoveredBy) ?? string.Empty
                                },
                                { "traitor", result.Officer?.GetDisplayName() ?? string.Empty },
                                { "enemy", opposingFaction?.GetDisplayName() ?? string.Empty },
                            },
                            overlayImagePath: GetMessageImagePath(discoverer),
                            officerVoicePath: discoverer?.GetVoicePath(
                                OfficerVoiceLineType.TraitorDiscovered,
                                game?.Random
                            )
                        ),
                        planet,
                        result.Officer,
                        discoverer
                    ),
                    AdvisorSubjectNotification.Report,
                    discoverer
                );
                AddDelivery(deliveries, faction, message, result);
            }
        }

        /// <summary>
        /// Creates the emperor seat-of-power message.
        /// </summary>
        /// <param name="faction">The faction that owns the emperor.</param>
        /// <param name="officer">The officer returning to the seat of power.</param>
        /// <returns>The seat-of-power message, or null when no matching definition exists.</returns>
        private Message CreateEmperorSeatOfPower(Faction faction, Officer officer)
        {
            return WithAdvisorSubject(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.EmperorSeatOfPower),
                        faction,
                        new Dictionary<string, string>(),
                        overlayImagePath: GetMessageImagePath(officer),
                        officerVoicePath: null
                    ),
                    GetOfficerPlanet(officer),
                    officer
                ),
                AdvisorSubjectNotification.Report,
                officer
            );
        }

        /// <summary>
        /// Creates the mission report for the acting faction.
        /// </summary>
        /// <param name="faction">The faction that launched the mission.</param>
        /// <param name="result">The completed mission result.</param>
        /// <param name="target">The mission target planet.</param>
        /// <param name="game">The game state used to resolve mission-specific targets.</param>
        /// <param name="killedOfficerIDs">Officer ids killed by results in the current batch.</param>
        /// <param name="killedResults">Officer death results in the current batch.</param>
        /// <param name="sabotageResults">Sabotage results in the current batch.</param>
        /// <param name="systemIntelligence">Additional systems revealed by this mission.</param>
        /// <returns>The mission report message, or null when no matching definition exists.</returns>
        private Message CreateMissionReport(
            Faction faction,
            MissionCompletedResult result,
            Planet target,
            GameRoot game,
            HashSet<string> killedOfficerIDs,
            IEnumerable<OfficerKilledResult> killedResults,
            IEnumerable<GameObjectSabotagedResult> sabotageResults,
            SystemsRevealedResult systemIntelligence
        )
        {
            if (result == null)
                return null;

            MessageResultOutcome outcome = result.Outcome switch
            {
                MissionOutcome.Success => MessageResultOutcome.Success,
                MissionOutcome.Foiled => MessageResultOutcome.Foiled,
                _ => MessageResultOutcome.Failed,
            };
            MissionCompletionReason completionReason = GetMissionCompletionReason(result);
            string missionName = GetMissionName(result);
            Officer jediTrainer = (result.Mission as JediTrainingMission)?.Trainer;
            string participantName =
                jediTrainer?.GetDisplayName() ?? GetMissionParticipantName(result);
            string officerName = GetMissionOfficerName(result, game, killedResults);
            string targetName = GetMissionObjectTargetName(result, game, sabotageResults);
            string assassinationResult = GetAssassinationResultText(result, killedOfficerIDs);
            OfficerVoiceLineType voiceLineType = GetMissionVoiceLineType(result);
            Officer reporter = jediTrainer ?? GetMissionParticipantOfficer(result, voiceLineType);
            MessageDefinition definition = GetMissionDefinition(
                MessageResultType.MissionReport,
                outcome,
                GetMissionTypeID(result),
                completionReason
            );
            string missionDetails = BuildMissionDetailList(
                definition,
                systemIntelligence?.AdditionalSystems
            );

            Message message = WithEventLocation(
                CreateMessage(
                    definition,
                    faction,
                    new Dictionary<string, string>
                    {
                        { "mission", missionName },
                        { "system", GetTargetName(result, target) },
                        {
                            "participant",
                            string.IsNullOrEmpty(participantName) ? missionName : participantName
                        },
                        { "officer", string.IsNullOrEmpty(officerName) ? "target" : officerName },
                        { "target", string.IsNullOrEmpty(targetName) ? "target" : targetName },
                        { "assassination_result", assassinationResult },
                        { "details", missionDetails },
                    },
                    overlayImagePath: jediTrainer == null
                        ? GetMissionParticipantOverlayImagePath(result)
                        : GetMessageImagePath(jediTrainer),
                    officerVoicePath: reporter?.GetVoicePath(voiceLineType, game?.Random)
                ),
                target,
                GetMissionNavigationTarget(result),
                result.Mission
            );

            if (message != null && result.CanContinue)
                message.MissionInstanceID = result.MissionInstanceID;

            return reporter == null
                ? WithAdvisorNotification(message, AdvisorNotificationType.FieldPersonnel)
                : WithAdvisorSubject(message, AdvisorSubjectNotification.Report, reporter);
        }

        /// <summary>
        /// Builds a configured mission-detail list for systems revealed beyond the primary target.
        /// </summary>
        private static string BuildMissionDetailList(
            MessageDefinition definition,
            IEnumerable<PlanetSystem> systems
        )
        {
            PlanetSystem[] systemArray = systems?.Where(system => system != null).ToArray();
            if (definition == null || systemArray == null || systemArray.Length == 0)
                return string.Empty;

            string items = string.Concat(
                systemArray.Select(system =>
                    MessageTemplateBuilder.Interpolate(
                        definition.DetailListItemTemplate,
                        new Dictionary<string, string> { { "system", system.GetDisplayName() } }
                    )
                )
            );
            return (definition.DetailListHeaderTemplate ?? string.Empty) + items;
        }

        /// <summary>
        /// Creates the target-faction report for a foiled enemy mission.
        /// </summary>
        /// <param name="faction">The faction that owned the mission target.</param>
        /// <param name="result">The completed mission result.</param>
        /// <param name="target">The mission target planet.</param>
        /// <returns>The enemy mission foiled message, or null when the result is not foiled or no definition exists.</returns>
        private Message CreateEnemyMissionFoiled(
            Faction faction,
            MissionCompletedResult result,
            Planet target
        )
        {
            if (result == null || result.Outcome != MissionOutcome.Foiled)
                return null;

            Message message = WithEventLocation(
                CreateMessage(
                    GetMissionDefinition(
                        MessageResultType.EnemyMissionFoiled,
                        MessageResultOutcome.Foiled,
                        GetMissionTypeID(result),
                        MissionCompletionReason.Foiled
                    ),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "mission", GetMissionName(result) },
                        { "system", GetTargetName(result, target) },
                    }
                ),
                target
            );
            return WithAdvisorNotification(message, AdvisorNotificationType.AgentReport);
        }

        /// <summary>
        /// Creates an officer status message.
        /// </summary>
        /// <param name="resultType">The message result type to use.</param>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="officer">The officer described by the message.</param>
        /// <param name="planet">The planet associated with the officer state.</param>
        /// <param name="game">The game state used for voice selection randomness.</param>
        /// <returns>The officer status message, or null when no matching definition exists.</returns>
        private Message CreateOfficerMessage(
            MessageResultType resultType,
            Faction faction,
            Officer officer,
            Planet planet,
            GameRoot game
        )
        {
            if (officer == null)
                return null;

            MessageDefinition definition = GetDefinition(resultType);
            Message message = WithEventLocation(
                CreateMessage(
                    definition,
                    faction,
                    new Dictionary<string, string>
                    {
                        { "officer", officer.GetDisplayName() ?? string.Empty },
                        {
                            "captor",
                            GetFaction(game, officer.CaptorInstanceID)?.GetDisplayName()
                                ?? string.Empty
                        },
                        { "system", planet?.GetDisplayName() ?? string.Empty },
                    },
                    overlayImagePath: definition?.ShowOfficerOverlay == true
                        ? GetMessageImagePath(officer)
                        : null,
                    officerVoicePath: GetOfficerMessageVoicePath(resultType, officer, game)
                ),
                planet,
                officer
            );
            AdvisorSubjectNotification notification = resultType switch
            {
                MessageResultType.OfficerCaptured => AdvisorSubjectNotification.Captured,
                MessageResultType.EnemyOfficerCaptured => AdvisorSubjectNotification.Captured,
                MessageResultType.OfficerReleased => AdvisorSubjectNotification.Released,
                MessageResultType.OfficerRecruited => AdvisorSubjectNotification.Report,
                MessageResultType.OfficerInjured => AdvisorSubjectNotification.Report,
                MessageResultType.OfficerRecovered => AdvisorSubjectNotification.Report,
                MessageResultType.OfficerKilled => AdvisorSubjectNotification.Report,
                MessageResultType.OfficerAssassinated => AdvisorSubjectNotification.Report,
                _ => AdvisorSubjectNotification.None,
            };
            return WithAdvisorSubject(message, notification, officer);
        }

        /// <summary>
        /// Creates a force growth message for an officer.
        /// </summary>
        /// <param name="faction">The faction that owns the officer.</param>
        /// <param name="result">The force experience result.</param>
        /// <param name="game">The game state used to resolve rank labels.</param>
        /// <returns>The force growth message, or null when no matching definition exists.</returns>
        private Message CreateForceGrowth(
            Faction faction,
            ForceExperienceResult result,
            GameRoot game
        )
        {
            if (result?.Officer == null)
                return null;

            return WithAdvisorSubject(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.ForceGrowth),
                        faction,
                        new Dictionary<string, string>
                        {
                            {
                                "rank",
                                GetForceRankText(
                                    GetCurrentForceRank(result),
                                    result.Officer.IsJedi,
                                    game
                                )
                            },
                        },
                        overlayImagePath: GetMessageImagePath(result.Officer),
                        officerVoicePath: result.Officer.GetVoicePath(
                            OfficerVoiceLineType.ForceGrowth,
                            game?.Random
                        )
                    ),
                    result.Officer.GetParentOfType<Planet>(),
                    result.Officer
                ),
                AdvisorSubjectNotification.Report,
                result.Officer
            );
        }

        /// <summary>
        /// Creates a report identifying an officer whose Force potential was discovered.
        /// </summary>
        /// <param name="faction">The faction receiving the report.</param>
        /// <param name="result">The Force discovery result.</param>
        /// <param name="game">The game state used to evaluate the discoverer's training rank.</param>
        /// <returns>The Force discovery message, or null when the result is incomplete.</returns>
        private Message CreateForceUserDiscovered(
            Faction faction,
            ForceDiscoveryResult result,
            GameRoot game
        )
        {
            if (result?.Officer == null || result.Discoverer == null)
                return null;

            bool canTrain = JediTrainingMission.CanLeadTraining(result.Discoverer, game);
            MessageResultType resultType = canTrain
                ? MessageResultType.ForceUserDiscovered
                : MessageResultType.ForceUserDiscoveredByStudent;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(resultType),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "officer", result.Officer.GetDisplayName() ?? string.Empty },
                    },
                    overlayImagePath: GetMessageImagePath(result.Discoverer)
                ),
                GetOfficerPlanet(result.Officer),
                result.Officer
            );
        }

        /// <summary>
        /// Creates a sabotage strike message for the owner of the destroyed object.
        /// </summary>
        /// <param name="faction">The faction that owned the sabotaged object.</param>
        /// <param name="results">The sabotage results grouped into this report.</param>
        /// <param name="target">The planet where sabotage occurred.</param>
        /// <param name="definition">The content-defined presentation selected for the group.</param>
        /// <returns>The sabotage strike message, or null when no matching definition exists.</returns>
        private Message CreateSabotageStrike(
            Faction faction,
            IEnumerable<GameObjectSabotagedResult> results,
            Planet target,
            MessageDefinition definition
        )
        {
            GameObjectSabotagedResult[] resultArray = results
                ?.Where(result => result != null)
                .ToArray();
            if (resultArray == null || resultArray.Length == 0)
                return null;

            return WithEventLocation(
                CreateMessage(
                    definition,
                    faction,
                    new Dictionary<string, string>
                    {
                        {
                            "item",
                            string.Join(
                                "\n",
                                resultArray.Select(result => GetDisplayName(result.SabotagedObject))
                            )
                        },
                        { "system", target?.GetDisplayName() ?? string.Empty },
                    }
                ),
                target,
                resultArray[0].SabotagedObject as ISceneNode
            );
        }

        /// <summary>
        /// Creates a message when recruitment can no longer continue because no candidates remain.
        /// </summary>
        /// <param name="result">The recruitment exhausted result.</param>
        /// <returns>The recruitment exhausted message, or null when the result does not match.</returns>
        private Message CreateRecruitmentExhausted(RecruitmentExhaustedResult result)
        {
            if (result?.Faction == null)
                return null;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.RecruitmentExhausted),
                    result.Faction,
                    new Dictionary<string, string>()
                ),
                result.Planet
            );
        }

        /// <summary>
        /// Adds messages for completed missions.
        /// </summary>
        /// <param name="results">The completed mission results to process.</param>
        /// <param name="killedResults">The officer death results in the current batch.</param>
        /// <param name="sabotageResults">The sabotage results in the current batch.</param>
        /// <param name="systemIntelligenceResults">Additional-system intelligence results.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddMissionMessages(
            IEnumerable<MissionCompletedResult> results,
            IEnumerable<OfficerKilledResult> killedResults,
            IEnumerable<GameObjectSabotagedResult> sabotageResults,
            IEnumerable<SystemsRevealedResult> systemIntelligenceResults,
            GameRoot game,
            List<MessageRequestedResult> deliveries
        )
        {
            OfficerKilledResult[] killedArray =
                killedResults?.ToArray() ?? Array.Empty<OfficerKilledResult>();
            HashSet<string> killedOfficerIDs = killedArray
                .Select(result => result.TargetOfficer?.InstanceID)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();
            Dictionary<string, SystemsRevealedResult> systemIntelligenceByMission = (
                systemIntelligenceResults ?? Array.Empty<SystemsRevealedResult>()
            )
                .Where(result => !string.IsNullOrEmpty(result.MissionInstanceID))
                .GroupBy(result => result.MissionInstanceID)
                .ToDictionary(group => group.Key, group => group.Last());

            foreach (MissionCompletedResult result in results)
            {
                Planet target = GetMissionTarget(result);
                Faction actorFaction = GetFaction(game, result.Mission?.OwnerInstanceID);
                systemIntelligenceByMission.TryGetValue(
                    result.MissionInstanceID ?? string.Empty,
                    out SystemsRevealedResult systemIntelligence
                );
                AddDelivery(
                    deliveries,
                    actorFaction,
                    CreateMissionReport(
                        actorFaction,
                        result,
                        target,
                        game,
                        killedOfficerIDs,
                        killedArray,
                        sabotageResults,
                        systemIntelligence
                    ),
                    result
                );

                Faction targetFaction = GetFaction(game, target?.OwnerInstanceID);
                if (targetFaction?.InstanceID == actorFaction?.InstanceID)
                    continue;

                AddDelivery(
                    deliveries,
                    targetFaction,
                    CreateEnemyMissionFoiled(targetFaction, result, target),
                    result
                );
            }
        }

        /// <summary>
        /// Adds messages for side-level recruitment exhaustion results.
        /// </summary>
        /// <param name="results">The recruitment exhausted results to process.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddRecruitmentMessages(
            IEnumerable<RecruitmentExhaustedResult> results,
            List<MessageRequestedResult> deliveries
        )
        {
            foreach (RecruitmentExhaustedResult result in results)
                AddDelivery(deliveries, result.Faction, CreateRecruitmentExhausted(result), result);
        }

        /// <summary>
        /// Adds messages for officer recruitment, capture, injury, recovery, and death results.
        /// </summary>
        /// <param name="recruitedResults">The officer recruitment results to process.</param>
        /// <param name="captureResults">The officer capture state results to process.</param>
        /// <param name="injuredResults">The officer injury results to process.</param>
        /// <param name="killedResults">The officer death results to process.</param>
        /// <param name="missionResults">The mission results in the current batch.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddOfficerMessages(
            IEnumerable<OfficerRecruitedResult> recruitedResults,
            IEnumerable<OfficerCaptureStateResult> captureResults,
            IEnumerable<OfficerInjuredResult> injuredResults,
            IEnumerable<OfficerKilledResult> killedResults,
            IEnumerable<MissionCompletedResult> missionResults,
            GameRoot game,
            List<MessageRequestedResult> deliveries
        )
        {
            OfficerKilledResult[] killedArray =
                killedResults?.ToArray() ?? Array.Empty<OfficerKilledResult>();
            HashSet<string> killedOfficerIDs = killedArray
                .Select(result => result.TargetOfficer?.InstanceID)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();
            HashSet<string> reportedRecruitmentOfficerIDs = (
                missionResults ?? Enumerable.Empty<MissionCompletedResult>()
            )
                .Where(result =>
                    result.Outcome == MissionOutcome.Success
                    && result.Mission?.ConfigKey == MissionTypeIDs.Recruitment
                )
                .Select(result => GetMissionOfficerInstanceID(result.Mission))
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();

            foreach (OfficerRecruitedResult result in recruitedResults)
            {
                if (
                    reportedRecruitmentOfficerIDs.Contains(
                        result.Officer?.InstanceID ?? string.Empty
                    )
                )
                {
                    continue;
                }

                Planet planet = result.Planet ?? GetOfficerPlanet(result.Officer);
                AddDelivery(
                    deliveries,
                    result.Faction,
                    CreateOfficerMessage(
                        MessageResultType.OfficerRecruited,
                        result.Faction,
                        result.Officer,
                        planet,
                        game
                    ),
                    result
                );
            }

            foreach (OfficerCaptureStateResult result in captureResults)
            {
                Officer officer = GetCaptureStateOfficer(result);
                Faction ownerFaction = GetOwnerFaction(game, officer);
                Planet planet = GetOfficerPlanet(officer, result.Context);
                AddDelivery(
                    deliveries,
                    ownerFaction,
                    CreateOfficerMessage(
                        result.IsCaptured
                            ? MessageResultType.OfficerCaptured
                            : MessageResultType.OfficerReleased,
                        ownerFaction,
                        officer,
                        planet,
                        game
                    ),
                    result
                );

                if (!result.IsCaptured)
                    continue;

                Faction captorFaction = GetFaction(game, officer?.CaptorInstanceID);
                if (captorFaction?.InstanceID == ownerFaction?.InstanceID)
                    continue;

                AddDelivery(
                    deliveries,
                    captorFaction,
                    CreateOfficerMessage(
                        MessageResultType.EnemyOfficerCaptured,
                        captorFaction,
                        officer,
                        planet,
                        game
                    ),
                    result
                );
            }

            foreach (OfficerInjuredResult result in injuredResults)
            {
                if (
                    result.Severity > 0
                    && killedOfficerIDs.Contains(result.Officer?.InstanceID ?? string.Empty)
                )
                {
                    continue;
                }

                Faction faction = GetOwnerFaction(game, result.Officer);
                Planet planet = GetOfficerPlanet(result.Officer);
                AddDelivery(
                    deliveries,
                    faction,
                    CreateOfficerMessage(
                        result.Severity > 0
                            ? MessageResultType.OfficerInjured
                            : MessageResultType.OfficerRecovered,
                        faction,
                        result.Officer,
                        planet,
                        game
                    ),
                    result
                );
            }

            foreach (OfficerKilledResult result in killedArray)
            {
                Faction faction = GetOwnerFaction(game, result.TargetOfficer);
                Planet planet = GetOfficerPlanet(result.TargetOfficer, result.Context);
                AddDelivery(
                    deliveries,
                    faction,
                    CreateOfficerMessage(
                        result.Assassin == null
                            ? MessageResultType.OfficerKilled
                            : MessageResultType.OfficerAssassinated,
                        faction,
                        result.TargetOfficer,
                        planet,
                        game
                    ),
                    result
                );
            }
        }

        /// <summary>
        /// Adds force growth messages for rank changes not already covered by discovery messages.
        /// </summary>
        /// <param name="experienceResults">The force experience results to process.</param>
        /// <param name="discoveryResults">The force discovery results in the current batch.</param>
        /// <param name="game">The game state used to resolve recipient factions and rank labels.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddForceMessages(
            IEnumerable<ForceExperienceResult> experienceResults,
            IEnumerable<ForceDiscoveryResult> discoveryResults,
            GameRoot game,
            List<MessageRequestedResult> deliveries
        )
        {
            ForceDiscoveryResult[] discoveryArray = (
                discoveryResults ?? Enumerable.Empty<ForceDiscoveryResult>()
            ).ToArray();
            HashSet<string> discoveredOfficerIDs = discoveryArray
                .Where(result => result.EventType == ForceEventType.ForceUserDiscovered)
                .Select(result => result.Officer?.InstanceID)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();

            foreach (
                ForceDiscoveryResult result in discoveryArray.Where(result =>
                    result.EventType == ForceEventType.ForceUserDiscovered
                )
            )
            {
                Faction faction = GetOwnerFaction(game, result.Discoverer);
                AddDelivery(
                    deliveries,
                    faction,
                    CreateForceUserDiscovered(faction, result, game),
                    result
                );
            }

            foreach (ForceExperienceResult result in experienceResults)
            {
                if (discoveredOfficerIDs.Contains(result.Officer?.InstanceID ?? string.Empty))
                    continue;

                if (!ShouldCreateForceGrowthMessage(result, game))
                    continue;

                Faction faction = GetOwnerFaction(game, result.Officer);
                AddDelivery(deliveries, faction, CreateForceGrowth(faction, result, game), result);
            }
        }

        /// <summary>
        /// Adds messages for sabotaged game objects.
        /// </summary>
        /// <param name="results">The sabotage results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddSabotageMessages(
            IEnumerable<GameObjectSabotagedResult> results,
            GameRoot game,
            List<MessageRequestedResult> deliveries
        )
        {
            var reportItems = (results ?? Enumerable.Empty<GameObjectSabotagedResult>())
                .Where(result => result != null)
                .Select(result =>
                {
                    Planet target = GetSabotageTarget(result);
                    string ownerInstanceID = GetOwnerInstanceID(result.SabotagedObject);
                    if (string.IsNullOrEmpty(ownerInstanceID))
                        ownerInstanceID = target?.OwnerInstanceID;

                    return new
                    {
                        Result = result,
                        Target = target,
                        Faction = GetFaction(game, ownerInstanceID),
                        Definition = GetDefinition(
                            MessageResultType.SabotageStrike,
                            gameObjectTypeId: result.SabotagedObject?.GetTypeID()
                        ),
                    };
                })
                .Where(item => item.Faction != null && item.Definition != null);

            foreach (
                var group in reportItems.GroupBy(item =>
                    (
                        item.Faction.InstanceID,
                        TargetInstanceID: item.Target?.InstanceID,
                        item.Definition
                    )
                )
            )
            {
                var first = group.First();
                AddDelivery(
                    deliveries,
                    first.Faction,
                    CreateSabotageStrike(
                        first.Faction,
                        group.Select(item => item.Result),
                        first.Target,
                        first.Definition
                    ),
                    group.Select(item => (GameResult)item.Result).ToArray()
                );
            }
        }

        /// <summary>
        /// Adds messages for seat-of-power changes.
        /// </summary>
        /// <param name="results">The seat-of-power changed results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddSeatOfPowerMessages(
            IEnumerable<SeatOfPowerChangedResult> results,
            GameRoot game,
            List<MessageRequestedResult> deliveries
        )
        {
            foreach (SeatOfPowerChangedResult result in results)
            {
                if (!result.IsAtSeat)
                    continue;

                Faction faction = GetFaction(game, result.Officer?.GetOwnerInstanceID());
                AddDelivery(
                    deliveries,
                    faction,
                    CreateEmperorSeatOfPower(faction, result.Officer),
                    result
                );
            }
        }

        /// <summary>
        /// Adds a non-null message delivery for a non-null faction.
        /// </summary>
        /// <param name="requests">The request list to append to.</param>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="message">The message to deliver.</param>
        /// <param name="sourceResults">The simulation results that produced the automatic message.</param>
        private void AddDelivery(
            ICollection<MessageRequestedResult> requests,
            Faction faction,
            Message message,
            params GameResult[] sourceResults
        )
        {
            _ = sourceResults;
            if (faction == null || message == null)
                return;

            MessageRequestedResult request = GetRequest(message);
            request.Recipient = faction;
            request.Message = message;
            requests.Add(request);
        }

        /// <summary>
        /// Gets the transient request metadata associated with a constructed message.
        /// </summary>
        private MessageRequestedResult GetRequest(Message message)
        {
            if (!_requests.TryGetValue(message, out MessageRequestedResult request))
            {
                request = new MessageRequestedResult { Message = message };
                _requests.Add(message, request);
            }
            return request;
        }

        /// <summary>
        /// Creates a message from a definition and interpolation values.
        /// </summary>
        /// <param name="definition">The message definition that supplies the template and image map.</param>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="values">The values to substitute into the templates.</param>
        /// <param name="imageFaction">The faction used for faction-specific image selection.</param>
        /// <param name="imageOverride">The explicit image path to use before definition image lookup.</param>
        /// <param name="overlayImagePath">The optional image path to render over the message background.</param>
        /// <param name="officerVoicePath">The optional officer voice line to play for this message.</param>
        /// <returns>The created message, or null when the definition is missing.</returns>
        private Message CreateMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            Faction imageFaction = null,
            string imageOverride = null,
            string overlayImagePath = null,
            string officerVoicePath = null
        )
        {
            return WithAdvisorNotification(
                _templateBuilder.Build(
                    definition,
                    faction,
                    values,
                    imageFaction,
                    imageOverride,
                    overlayImagePath,
                    officerVoicePath
                ),
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        /// <summary>
        /// Assigns an advisor notification code to a message.
        /// </summary>
        /// <param name="message">The message to update.</param>
        /// <param name="notification">The advisor notification code.</param>
        /// <returns>The updated message, or null when no message was supplied.</returns>
        private Message WithAdvisorNotification(
            Message message,
            AdvisorNotificationType notification
        )
        {
            if (message != null)
                GetRequest(message).NotificationType = notification;
            return message;
        }

        /// <summary>
        /// Assigns an officer-specific advisor notification to a message.
        /// </summary>
        /// <param name="message">The message to update.</param>
        /// <param name="notification">The officer notification kind.</param>
        /// <param name="officer">The officer represented by the notification.</param>
        /// <returns>The updated message.</returns>
        private Message WithAdvisorSubject(
            Message message,
            AdvisorSubjectNotification notification,
            Officer officer
        )
        {
            if (
                message == null
                || notification == AdvisorSubjectNotification.None
                || officer == null
            )
                return message;

            MessageRequestedResult request = GetRequest(message);
            request.AdvisorSubjectNotification = notification;
            request.AdvisorSubjectTypeID = officer.TypeID;
            return message;
        }

        /// <summary>
        /// Assigns a planet location to a message.
        /// </summary>
        /// <param name="message">The message to update.</param>
        /// <param name="planet">The planet associated with the event.</param>
        /// <param name="target">The primary navigation target.</param>
        /// <param name="secondaryTarget">The optional secondary navigation target.</param>
        /// <returns>The same message instance after the event location is assigned.</returns>
        private static Message WithEventLocation(
            Message message,
            Planet planet,
            ISceneNode target = null,
            ISceneNode secondaryTarget = null
        )
        {
            if (message != null)
            {
                message.EventLocationInstanceID = planet?.GetInstanceID();
                message.NavigationTargetInstanceID = (target ?? planet)?.GetInstanceID();
                message.NavigationSecondaryTargetInstanceID = secondaryTarget?.GetInstanceID();
            }

            return message;
        }

        /// <summary>
        /// Finds the configured message definition for a result selector.
        /// </summary>
        /// <param name="resultType">The message result type to match.</param>
        /// <param name="outcome">The result outcome to match.</param>
        /// <param name="planetOwnership">The planet ownership selector to match.</param>
        /// <param name="buildingType">The building type selector to match.</param>
        /// <param name="manufacturingType">The manufacturing type selector to match.</param>
        /// <param name="discipline">The research discipline selector to match.</param>
        /// <param name="gameObjectTypeId">The affected game object's type selector to match.</param>
        /// <param name="planetDestroyed">Whether the result destroyed its target planet.</param>
        /// <param name="factionInstanceId">The recipient faction selector to match.</param>
        /// <returns>The matching message definition, or null when none exists.</returns>
        private MessageDefinition GetDefinition(
            MessageResultType resultType,
            MessageResultOutcome outcome = MessageResultOutcome.None,
            MessagePlanetOwnership planetOwnership = MessagePlanetOwnership.None,
            BuildingType buildingType = BuildingType.None,
            ManufacturingType manufacturingType = ManufacturingType.None,
            ResearchDiscipline? discipline = null,
            string gameObjectTypeId = null,
            bool planetDestroyed = false,
            string factionInstanceId = null
        )
        {
            return _definitions
                .Where(definition =>
                    definition.ResultType == resultType
                    && definition.Outcome == outcome
                    && definition.PlanetOwnership == planetOwnership
                    && definition.BuildingType == buildingType
                    && definition.ManufacturingType == manufacturingType
                    && string.IsNullOrEmpty(definition.MissionTypeID)
                    && definition.MissionCompletionReason == MissionCompletionReason.None
                    && (!discipline.HasValue || definition.ResearchDiscipline == discipline.Value)
                    && Matches(definition.GameObjectTypeID, gameObjectTypeId)
                    && definition.PlanetDestroyed == planetDestroyed
                    && Matches(definition.FactionInstanceID, factionInstanceId)
                )
                .OrderByDescending(definition =>
                    !string.IsNullOrWhiteSpace(definition.GameObjectTypeID)
                )
                .ThenByDescending(definition =>
                    !string.IsNullOrWhiteSpace(definition.FactionInstanceID)
                )
                .FirstOrDefault();
        }

        /// <summary>
        /// Finds the best message definition for a completed mission selector.
        /// </summary>
        /// <param name="resultType">The message result type to match.</param>
        /// <param name="outcome">The mission outcome selector to match.</param>
        /// <param name="missionTypeID">The mission type selector to match.</param>
        /// <param name="completionReason">The completion reason selector to match.</param>
        /// <returns>The matching mission definition, or null when none exists.</returns>
        private MessageDefinition GetMissionDefinition(
            MessageResultType resultType,
            MessageResultOutcome outcome,
            string missionTypeID,
            MissionCompletionReason completionReason = MissionCompletionReason.None
        )
        {
            MessageDefinition definition = FindMissionDefinition(
                resultType,
                outcome,
                missionTypeID,
                completionReason
            );
            if (definition != null)
                return definition;

            bool canUseGenericDefinition = CanUseGenericMissionDefinition(completionReason);
            if (completionReason != MissionCompletionReason.None && canUseGenericDefinition)
            {
                definition = FindMissionDefinition(
                    resultType,
                    outcome,
                    missionTypeID,
                    MissionCompletionReason.None
                );
            }
            if (definition != null || string.IsNullOrEmpty(missionTypeID))
                return definition;

            definition = FindMissionDefinition(resultType, outcome, null, completionReason);
            if (definition != null || completionReason == MissionCompletionReason.None)
                return definition;

            return canUseGenericDefinition
                ? FindMissionDefinition(resultType, outcome, null, MissionCompletionReason.None)
                : null;
        }

        private MessageDefinition FindMissionDefinition(
            MessageResultType resultType,
            MessageResultOutcome outcome,
            string missionTypeID,
            MissionCompletionReason completionReason
        ) =>
            _definitions.FirstOrDefault(candidate =>
                candidate.ResultType == resultType
                && candidate.Outcome == outcome
                && candidate.PlanetOwnership == MessagePlanetOwnership.None
                && candidate.BuildingType == BuildingType.None
                && candidate.ManufacturingType == ManufacturingType.None
                && string.Equals(
                    candidate.MissionTypeID ?? string.Empty,
                    missionTypeID ?? string.Empty,
                    StringComparison.Ordinal
                )
                && candidate.MissionCompletionReason == completionReason
            );

        private static bool CanUseGenericMissionDefinition(
            MissionCompletionReason completionReason
        ) =>
            completionReason
                is MissionCompletionReason.None
                    or MissionCompletionReason.Success
                    or MissionCompletionReason.Failure
                    or MissionCompletionReason.Foiled
                    or MissionCompletionReason.ResearchBreakthrough;

        /// <summary>
        /// Gets the display name for a completed mission result.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <returns>The mission display name, or an empty string when none is available.</returns>
        private static string GetMissionName(MissionCompletedResult result)
        {
            return result.MissionName ?? result.Mission?.GetDisplayName() ?? string.Empty;
        }

        /// <summary>
        /// Gets the display name of the first mission participant.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <returns>The participant display name, or an empty string when none is available.</returns>
        private static string GetMissionParticipantName(MissionCompletedResult result)
        {
            string name =
                GetFirstParticipantDisplayName(result?.Participants)
                ?? GetFirstParticipantDisplayName(result?.Mission?.GetAllParticipants());
            return name ?? string.Empty;
        }

        /// <summary>
        /// Finds the first mission participant with audio for the requested outcome.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <param name="voiceLineType">The requested officer voice line type.</param>
        /// <returns>The matching officer, or null when none is available.</returns>
        private static Officer GetMissionParticipantOfficer(
            MissionCompletedResult result,
            OfficerVoiceLineType voiceLineType
        )
        {
            return GetFirstParticipantOfficer(result?.Participants, voiceLineType)
                ?? GetFirstParticipantOfficer(result?.Mission?.GetAllParticipants(), voiceLineType);
        }

        /// <summary>
        /// Resolves the scene node opened from a completed mission message.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <returns>The first participant scene node or the mission itself.</returns>
        private static ISceneNode GetMissionNavigationTarget(MissionCompletedResult result)
        {
            return (result?.Participants ?? Enumerable.Empty<IMissionParticipant>())
                    .OfType<ISceneNode>()
                    .FirstOrDefault()
                ?? (
                    result?.Mission?.GetAllParticipants() ?? Enumerable.Empty<IMissionParticipant>()
                )
                    .OfType<ISceneNode>()
                    .FirstOrDefault()
                ?? result?.Mission;
        }

        /// <summary>
        /// Resolves officer audio for a personnel message result.
        /// </summary>
        /// <param name="resultType">The personnel message result type.</param>
        /// <param name="officer">The officer represented by the message.</param>
        /// <param name="game">The game state used to select officer audio.</param>
        /// <returns>The matching voice path, or null when no line applies.</returns>
        private static string GetOfficerMessageVoicePath(
            MessageResultType resultType,
            Officer officer,
            GameRoot game
        )
        {
            OfficerVoiceLineType? voiceLineType = resultType switch
            {
                MessageResultType.OfficerRecruited => OfficerVoiceLineType.PersonnelArrived,
                MessageResultType.OfficerReleased => OfficerVoiceLineType.Released,
                MessageResultType.OfficerRecovered => OfficerVoiceLineType.Recovered,
                _ => null,
            };
            return voiceLineType.HasValue
                ? officer?.GetVoicePath(voiceLineType.Value, game?.Random)
                : null;
        }

        /// <summary>
        /// Gets the voice line type for a completed mission result.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <returns>The voice line type that matches the mission outcome.</returns>
        private static OfficerVoiceLineType GetMissionVoiceLineType(MissionCompletedResult result)
        {
            if (result?.CompletionReason == MissionCompletionReason.TargetUnavailable)
                return OfficerVoiceLineType.MissionAbort;

            return result?.Outcome == MissionOutcome.Success
                ? OfficerVoiceLineType.MissionSuccess
                : OfficerVoiceLineType.MissionFailure;
        }

        /// <summary>
        /// Gets the first display name from a mission participant collection.
        /// </summary>
        /// <param name="participants">The mission participants to inspect.</param>
        /// <returns>The first participant display name, or null when none is available.</returns>
        private static string GetFirstParticipantDisplayName(
            IEnumerable<IMissionParticipant> participants
        )
        {
            return (participants ?? Enumerable.Empty<IMissionParticipant>())
                .OfType<IGameEntity>()
                .Select(GetDisplayName)
                .FirstOrDefault(name => !string.IsNullOrEmpty(name));
        }

        /// <summary>
        /// Gets the first officer participant with a configured voice line.
        /// </summary>
        /// <param name="participants">The mission participants to inspect.</param>
        /// <param name="voiceLineType">The voice line type to require.</param>
        /// <returns>The first matching officer, or null when none is available.</returns>
        private static Officer GetFirstParticipantOfficer(
            IEnumerable<IMissionParticipant> participants,
            OfficerVoiceLineType voiceLineType
        )
        {
            return (participants ?? Enumerable.Empty<IMissionParticipant>())
                .OfType<Officer>()
                .FirstOrDefault(officer => officer.HasVoicePath(voiceLineType));
        }

        /// <summary>
        /// Gets the mission type identifier from a completed mission result.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <returns>The mission type identifier, or null when none is available.</returns>
        private static string GetMissionTypeID(MissionCompletedResult result)
        {
            if (result == null)
                return null;

            if (!string.IsNullOrEmpty(result.MissionTypeID))
                return result.MissionTypeID;

            return result.Mission?.ConfigKey;
        }

        /// <summary>
        /// Gets the completion reason selector for a completed mission result.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <returns>The completion reason selector.</returns>
        private static MissionCompletionReason GetMissionCompletionReason(
            MissionCompletedResult result
        )
        {
            if (result == null)
                return MissionCompletionReason.None;

            if (result.CompletionReason != MissionCompletionReason.None)
                return result.CompletionReason;

            return result.Outcome switch
            {
                MissionOutcome.Success => MissionCompletionReason.Success,
                MissionOutcome.Foiled => MissionCompletionReason.Foiled,
                _ => MissionCompletionReason.Failure,
            };
        }

        /// <summary>
        /// Gets the display name for a mission target.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <param name="target">The resolved target planet.</param>
        /// <returns>The target display name, or an empty string when none is available.</returns>
        private static string GetTargetName(MissionCompletedResult result, Planet target)
        {
            return target?.GetDisplayName() ?? result.TargetName ?? string.Empty;
        }

        /// <summary>
        /// Gets the display name for the officer targeted by a mission.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <param name="game">The game state used to resolve live officers.</param>
        /// <param name="killedResults">Officer death results in the current batch.</param>
        /// <returns>The target officer display name, or an empty string when none is available.</returns>
        private static string GetMissionOfficerName(
            MissionCompletedResult result,
            GameRoot game,
            IEnumerable<OfficerKilledResult> killedResults
        )
        {
            string officerID = GetMissionOfficerInstanceID(result?.Mission);
            if (string.IsNullOrEmpty(officerID))
                return string.Empty;

            string sceneName = GetDisplayName(game?.GetSceneNodeByInstanceID<Officer>(officerID));
            if (!string.IsNullOrEmpty(sceneName))
                return sceneName;

            return GetDisplayName(
                (killedResults ?? Enumerable.Empty<OfficerKilledResult>())
                    .Select(killedResult => killedResult.TargetOfficer)
                    .FirstOrDefault(officer => officer?.InstanceID == officerID)
            );
        }

        /// <summary>
        /// Gets the target officer instance ID for missions that target officers.
        /// </summary>
        /// <param name="mission">The mission to inspect.</param>
        /// <returns>The target officer instance ID, or null when the mission does not target an officer.</returns>
        private static string GetMissionOfficerInstanceID(Mission mission)
        {
            if (
                mission?.ConfigKey == MissionTypeIDs.Recruitment
                || mission?.ConfigKey == MissionTypeIDs.Abduction
                || mission?.ConfigKey == MissionTypeIDs.Assassination
                || mission?.ConfigKey == MissionTypeIDs.Rescue
            )
                return GetMissionTargetOfficerInstanceID(mission);

            return null;
        }

        /// <summary>
        /// Gets the stored target officer instance ID from an officer-targeting mission.
        /// </summary>
        /// <param name="mission">The mission to inspect.</param>
        /// <returns>The target officer instance ID, or null when none is available.</returns>
        private static string GetMissionTargetOfficerInstanceID(Mission mission)
        {
            return mission switch
            {
                RecruitmentMission recruitment => recruitment.TargetOfficerInstanceID,
                AbductionMission abduction => abduction.TargetOfficerInstanceID,
                AssassinationMission assassination => assassination.TargetOfficerInstanceID,
                RescueMission rescue => rescue.TargetOfficerInstanceID,
                _ => null,
            };
        }

        /// <summary>
        /// Gets the display name for the object targeted by a sabotage mission.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <param name="game">The game state used to resolve live objects.</param>
        /// <param name="sabotageResults">Sabotage results in the current batch.</param>
        /// <returns>The sabotage target display name, or an empty string when none is available.</returns>
        private static string GetMissionObjectTargetName(
            MissionCompletedResult result,
            GameRoot game,
            IEnumerable<GameObjectSabotagedResult> sabotageResults
        )
        {
            if (result?.Mission?.ConfigKey != MissionTypeIDs.Sabotage)
                return string.Empty;

            string targetInstanceID = result.Mission is SabotageMission sabotage
                ? sabotage.SabotageTargetInstanceID
                : null;
            IGameEntity target = game?.GetSceneNodeByInstanceID<IGameEntity>(targetInstanceID);
            string targetName = GetDisplayName(target);
            if (!string.IsNullOrEmpty(targetName))
                return targetName;

            return GetDisplayName(
                (sabotageResults ?? Enumerable.Empty<GameObjectSabotagedResult>())
                    .Select(sabotageResult => sabotageResult.SabotagedObject)
                    .FirstOrDefault(sabotagedObject =>
                        sabotagedObject?.GetInstanceID() == targetInstanceID
                    )
            );
        }

        /// <summary>
        /// Gets the assassination result phrase for a successful assassination mission.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <param name="killedOfficerIDs">Officer ids killed by results in the current batch.</param>
        /// <returns>The assassination result phrase, or an empty string when not applicable.</returns>
        private static string GetAssassinationResultText(
            MissionCompletedResult result,
            HashSet<string> killedOfficerIDs
        )
        {
            if (
                result?.Outcome != MissionOutcome.Success
                || result.Mission?.ConfigKey != MissionTypeIDs.Assassination
            )
            {
                return string.Empty;
            }

            return killedOfficerIDs.Contains(GetMissionTargetOfficerInstanceID(result.Mission))
                ? "has been eliminated"
                : "has been injured";
        }

        /// <summary>
        /// Gets the display name for a game entity.
        /// </summary>
        /// <param name="entity">The entity whose display name should be returned.</param>
        /// <returns>The entity display name, or an empty string when none is available.</returns>
        private static string GetDisplayName(IGameEntity entity)
        {
            return entity?.GetDisplayName() ?? string.Empty;
        }

        /// <summary>
        /// Gets the display label for a force rank.
        /// </summary>
        /// <param name="forceRank">The numeric force rank to label.</param>
        /// <param name="isJedi">Whether the officer is a Jedi.</param>
        /// <param name="game">The game state containing Jedi rank configuration.</param>
        /// <returns>The force rank label.</returns>
        private static string GetForceRankText(int forceRank, bool isJedi, GameRoot game)
        {
            GameConfig.JediConfig config = game?.Config?.Jedi;
            if (config == null || !isJedi)
                return "None";

            return config.GetRankLabel(forceRank) switch
            {
                ForceRankLabel.ForceMaster => "Jedi Master",
                ForceRankLabel.ForceKnight => "Jedi Knight",
                ForceRankLabel.ForceStudent => "Jedi Student",
                ForceRankLabel.Trainee => "Trainee",
                ForceRankLabel.Novice => "Novice",
                _ => "None",
            };
        }

        /// <summary>
        /// Checks whether a force experience result should produce a rank-change message.
        /// </summary>
        /// <param name="result">The force experience result.</param>
        /// <param name="game">The game state containing Jedi rank configuration.</param>
        /// <returns>True when the displayed rank changes; otherwise false.</returns>
        private static bool ShouldCreateForceGrowthMessage(
            ForceExperienceResult result,
            GameRoot game
        )
        {
            if (result?.Officer == null || result.SuppressRankChangeMessage)
                return false;

            string previousRank = GetForceRankText(
                GetPreviousForceRank(result),
                result.Officer.IsJedi,
                game
            );
            string currentRank = GetForceRankText(
                GetCurrentForceRank(result),
                result.Officer.IsJedi,
                game
            );
            return previousRank != currentRank;
        }

        /// <summary>
        /// Gets the previous force rank for a force experience result.
        /// </summary>
        /// <param name="result">The force experience result.</param>
        /// <returns>The previous force rank.</returns>
        private static int GetPreviousForceRank(ForceExperienceResult result)
        {
            if (HasRecordedForceRank(result))
                return result.PreviousForceRank;

            return Math.Max(
                0,
                (result.Officer?.ForceRank ?? 0) - Math.Max(0, result.ExperienceGained)
            );
        }

        /// <summary>
        /// Gets the current force rank for a force experience result.
        /// </summary>
        /// <param name="result">The force experience result.</param>
        /// <returns>The current force rank.</returns>
        private static int GetCurrentForceRank(ForceExperienceResult result)
        {
            if (HasRecordedForceRank(result))
                return result.CurrentForceRank;

            return result.Officer?.ForceRank ?? 0;
        }

        /// <summary>
        /// Checks whether a force experience result recorded explicit rank values.
        /// </summary>
        /// <param name="result">The force experience result.</param>
        /// <returns>True when explicit rank values are present; otherwise false.</returns>
        private static bool HasRecordedForceRank(ForceExperienceResult result)
        {
            return result.PreviousForceRank != 0 || result.CurrentForceRank != 0;
        }

        /// <summary>
        /// Gets the configured message image path for a game entity.
        /// </summary>
        /// <param name="entity">The entity whose message image path should be returned.</param>
        /// <returns>The message image path, or null when none is configured.</returns>
        private static string GetMessageImagePath(IGameEntity entity)
        {
            return string.IsNullOrEmpty(entity?.MessageImagePath) ? null : entity.MessageImagePath;
        }

        /// <summary>
        /// Gets the overlay image path for the first mission participant with a message image.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <returns>The participant image path, or null when none is available.</returns>
        private static string GetMissionParticipantOverlayImagePath(MissionCompletedResult result)
        {
            string imagePath = GetFirstParticipantImagePath(result?.Participants);
            if (!string.IsNullOrEmpty(imagePath))
                return imagePath;

            return GetFirstParticipantImagePath(result?.Mission?.GetAllParticipants());
        }

        /// <summary>
        /// Gets the first configured message image path from a participant collection.
        /// </summary>
        /// <param name="participants">The mission participants to inspect.</param>
        /// <returns>The first message image path, or null when none is available.</returns>
        private static string GetFirstParticipantImagePath(
            IEnumerable<IMissionParticipant> participants
        )
        {
            return (participants ?? Enumerable.Empty<IMissionParticipant>())
                .OfType<IGameEntity>()
                .Select(GetMessageImagePath)
                .FirstOrDefault(path => !string.IsNullOrEmpty(path));
        }

        /// <summary>
        /// Gets the officer whose capture state changed.
        /// </summary>
        /// <param name="result">The capture state result.</param>
        /// <returns>The officer whose capture state changed, or null when none is available.</returns>
        private static Officer GetCaptureStateOfficer(OfficerCaptureStateResult result)
        {
            return result?.TargetOfficer ?? result?.CapturedOfficer ?? result?.LinkedOfficer;
        }

        /// <summary>
        /// Gets the planet targeted by a completed mission.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <returns>The target planet, or null when the target cannot be resolved.</returns>
        private static Planet GetMissionTarget(MissionCompletedResult result)
        {
            return result?.Mission?.GetParent() as Planet
                ?? result?.Mission?.GetLastParent() as Planet;
        }

        /// <summary>
        /// Gets the planet associated with an officer result.
        /// </summary>
        /// <param name="officer">The officer to locate.</param>
        /// <param name="context">The optional result context.</param>
        /// <returns>The associated planet, or null when none can be resolved.</returns>
        private static Planet GetOfficerPlanet(Officer officer, IGameEntity context = null)
        {
            return GetResultPlanet(context)
                ?? officer?.GetParentOfType<Planet>()
                ?? officer?.GetLastParent() as Planet;
        }

        /// <summary>
        /// Gets the planet where sabotage occurred.
        /// </summary>
        /// <param name="result">The sabotage result.</param>
        /// <returns>The sabotage target planet, or null when the target cannot be resolved.</returns>
        private static Planet GetSabotageTarget(GameObjectSabotagedResult result)
        {
            if (result?.Context is Planet contextPlanet)
                return contextPlanet;

            if (result?.SabotagedObject is ISceneNode sceneNode)
                return sceneNode.GetParentOfType<Planet>() ?? sceneNode.GetLastParent() as Planet;

            return null;
        }

        /// <summary>
        /// Gets the planet associated with a result entity.
        /// </summary>
        /// <param name="entity">The entity to resolve to a planet.</param>
        /// <returns>The associated planet, or null when none can be resolved.</returns>
        private static Planet GetResultPlanet(IGameEntity entity)
        {
            if (entity is Planet planet)
                return planet;

            if (entity is ISceneNode sceneNode)
                return sceneNode.GetParentOfType<Planet>() ?? sceneNode.GetLastParent() as Planet;

            return null;
        }

        /// <summary>
        /// Gets the owner instance ID for a game entity.
        /// </summary>
        /// <param name="entity">The entity whose owner should be returned.</param>
        /// <returns>The owner instance ID, or null when none is available.</returns>
        private static string GetOwnerInstanceID(IGameEntity entity)
        {
            return entity is ISceneNode sceneNode ? sceneNode.GetOwnerInstanceID() : null;
        }

        /// <summary>
        /// Finds a faction by owner instance ID.
        /// </summary>
        /// <param name="game">The game state containing factions.</param>
        /// <param name="ownerInstanceID">The owner instance ID to match.</param>
        /// <returns>The matching faction, or null when none is found.</returns>
        private static Faction GetFaction(GameRoot game, string ownerInstanceID)
        {
            return string.IsNullOrEmpty(ownerInstanceID)
                ? null
                : game
                    ?.GetFactions()
                    .FirstOrDefault(faction => faction.InstanceID == ownerInstanceID);
        }

        /// <summary>
        /// Finds the owning faction for a game entity.
        /// </summary>
        /// <param name="game">The game state containing factions.</param>
        /// <param name="entity">The entity whose owner should be resolved.</param>
        /// <returns>The owning faction, or null when none is found.</returns>
        private static Faction GetOwnerFaction(GameRoot game, IGameEntity entity)
        {
            return GetFaction(game, GetOwnerInstanceID(entity));
        }
    }
}
#endregion
#region MessageFactory.Arrivals
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Groups completed movement arrivals and translates them into faction reports.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddArrivalMessages(
            IEnumerable<UnitArrivedResult> arrivals,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            UnitArrivedResult[] arrivalResults = arrivals.ToArray();
            var shipGroups =
                new Dictionary<
                    (string Owner, string Destination, string Group),
                    List<CapitalShip>
                >();
            var shipDestinations =
                new Dictionary<(string Owner, string Destination, string Group), Planet>();
            var personnelGroups =
                new Dictionary<
                    (string Owner, string Destination, string Group),
                    List<IGameEntity>
                >();
            var personnelDestinations =
                new Dictionary<(string Owner, string Destination, string Group), Planet>();
            var unitGroups =
                new Dictionary<
                    (string Owner, string Destination, string Group),
                    List<IGameEntity>
                >();
            var unitDestinations =
                new Dictionary<(string Owner, string Destination, string Group), Planet>();

            foreach (UnitArrivedResult arrival in arrivalResults)
            {
                if (arrival.Unit is Fleet fleet)
                {
                    Faction faction = GetArrivalFaction(game, fleet.GetOwnerInstanceID());
                    AddArrivalDelivery(
                        deliveries,
                        faction,
                        CreateFleet(faction, fleet, arrival.Destination),
                        arrival
                    );
                    continue;
                }
                if (arrival.Unit is CapitalShip ship)
                {
                    var key = Key(ship, arrival);
                    AddGroup(shipGroups, shipDestinations, key, ship, arrival.Destination);
                    continue;
                }
                if (arrival.Unit is Officer or SpecialForces)
                {
                    IGameEntity personnel = arrival.Unit;
                    var key = Key(personnel, arrival);
                    AddGroup(
                        personnelGroups,
                        personnelDestinations,
                        key,
                        personnel,
                        arrival.Destination
                    );
                    continue;
                }
                if (arrival.Unit is Regiment or Starfighter)
                {
                    IGameEntity unit = arrival.Unit;
                    var key = Key(unit, arrival);
                    AddGroup(unitGroups, unitDestinations, key, unit, arrival.Destination);
                    continue;
                }
                if (arrival.Unit is Building building)
                {
                    Faction faction = GetArrivalFaction(game, building.GetOwnerInstanceID());
                    Message message =
                        building.BuildingType == BuildingType.Headquarters
                            ? CreateHeadquarters(faction, building, arrival.Destination)
                            : this.CreateFacilityMessage(faction, building, arrival.Destination);
                    AddArrivalDelivery(deliveries, faction, message, arrival);
                }
            }

            foreach (var group in shipGroups)
            {
                Faction faction = GetArrivalFaction(game, group.Key.Owner);
                AddArrivalDelivery(
                    deliveries,
                    faction,
                    CreateShips(faction, group.Value, shipDestinations[group.Key]),
                    arrivalResults
                        .Where(result => group.Value.Contains(result.Unit as CapitalShip))
                        .Cast<GameResult>()
                        .ToArray()
                );
            }
            foreach (var group in personnelGroups)
            {
                Faction faction = GetArrivalFaction(game, group.Key.Owner);
                AddArrivalDelivery(
                    deliveries,
                    faction,
                    CreatePersonnel(faction, group.Value, personnelDestinations[group.Key], game),
                    arrivalResults
                        .Where(result => group.Value.Contains(result.Unit))
                        .Cast<GameResult>()
                        .ToArray()
                );
            }
            foreach (var group in unitGroups)
            {
                Faction faction = GetArrivalFaction(game, group.Key.Owner);
                AddArrivalDelivery(
                    deliveries,
                    faction,
                    CreateUnits(faction, group.Value, unitDestinations[group.Key]),
                    arrivalResults
                        .Where(result => group.Value.Contains(result.Unit))
                        .Cast<GameResult>()
                        .ToArray()
                );
            }
        }

        private Message CreateFleet(Faction faction, Fleet fleet, Planet destination)
        {
            Message message = BuildArrivalMessage(
                MessageResultType.FleetArrived,
                faction,
                new Dictionary<string, string>
                {
                    { "fleet", fleet?.GetDisplayName() ?? string.Empty },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
            SetArrivalLocation(message, destination, fleet);
            return WithAdvisorNotification(message, AdvisorNotificationType.FleetArrived);
        }

        private Message CreateShips(
            Faction faction,
            IEnumerable<CapitalShip> ships,
            Planet destination
        )
        {
            CapitalShip[] array =
                ships?.Where(ship => ship != null).ToArray() ?? Array.Empty<CapitalShip>();
            Message message = BuildArrivalMessage(
                MessageResultType.ShipsArrived,
                faction,
                new Dictionary<string, string>
                {
                    { "ships", string.Join("\n", array.Select(ship => ship.GetDisplayName())) },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
            SetArrivalLocation(message, destination, array.FirstOrDefault());
            return WithAdvisorNotification(message, AdvisorNotificationType.UnitsArrived);
        }

        private Message CreateUnits(
            Faction faction,
            IEnumerable<IGameEntity> units,
            Planet destination
        )
        {
            IGameEntity[] array = units?.Where(unit => unit != null).ToArray();
            if (array == null || array.Length == 0)
                return null;
            Message message = BuildArrivalMessage(
                MessageResultType.UnitsArrived,
                faction,
                new Dictionary<string, string>
                {
                    { "units", string.Join("\n", array.Select(unit => unit.GetDisplayName())) },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
            SetArrivalLocation(message, destination, array[0] as ISceneNode);
            return WithAdvisorNotification(message, AdvisorNotificationType.UnitsArrived);
        }

        private Message CreatePersonnel(
            Faction faction,
            IEnumerable<IGameEntity> personnel,
            Planet destination,
            GameRoot game
        )
        {
            IGameEntity[] array =
                personnel?.Where(unit => unit != null).ToArray() ?? Array.Empty<IGameEntity>();
            if (array.Length == 0)
                return null;
            Officer reporter = array
                .OfType<Officer>()
                .FirstOrDefault(officer =>
                    officer.HasVoicePath(OfficerVoiceLineType.PersonnelArrived)
                );
            IGameEntity[] listed =
                reporter == null ? array : array.Where(unit => unit != reporter).ToArray();
            MessageResultType resultType =
                reporter == null ? MessageResultType.PersonnelArrived
                : listed.Length == 0 ? MessageResultType.PersonnelArrivedByOfficer
                : MessageResultType.PersonnelArrivedByOfficerWithCompany;
            Message message = BuildArrivalMessage(
                resultType,
                faction,
                new Dictionary<string, string>
                {
                    { "officer", reporter?.GetDisplayName() ?? string.Empty },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                    {
                        "personnel",
                        string.Join("\n", listed.Select(unit => unit.GetDisplayName()))
                    },
                },
                overlayImagePath: (reporter ?? array[0]).MessageImagePath,
                officerVoicePath: reporter?.GetVoicePath(
                    OfficerVoiceLineType.PersonnelArrived,
                    game.Random
                )
            );
            SetArrivalLocation(message, destination, reporter ?? array[0] as ISceneNode);
            return reporter == null
                ? WithAdvisorNotification(message, AdvisorNotificationType.FieldPersonnel)
                : WithAdvisorSubject(message, AdvisorSubjectNotification.Report, reporter);
        }

        private Message CreateHeadquarters(
            Faction faction,
            Building headquarters,
            Planet destination
        )
        {
            MessageDefinition definition = GetDefinition(
                MessageResultType.HeadquartersArrived,
                factionInstanceId: faction?.InstanceID
            );
            Message message = BuildArrivalMessage(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                },
                imageOverride: headquarters?.MessageImagePath
            );
            SetArrivalLocation(message, destination, headquarters);
            return WithAdvisorNotification(message, AdvisorNotificationType.UnitsArrived);
        }

        private Message BuildArrivalMessage(
            MessageResultType resultType,
            Faction faction,
            Dictionary<string, string> values,
            string overlayImagePath = null,
            string officerVoicePath = null
        ) =>
            BuildArrivalMessage(
                GetDefinition(resultType),
                faction,
                values,
                overlayImagePath: overlayImagePath,
                officerVoicePath: officerVoicePath
            );

        private Message BuildArrivalMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            string imageOverride = null,
            string overlayImagePath = null,
            string officerVoicePath = null
        )
        {
            Message message = _templateBuilder.Build(
                definition,
                faction,
                values,
                imageOverride: imageOverride,
                overlayImagePath: overlayImagePath,
                officerVoicePath: officerVoicePath
            );
            return WithAdvisorNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static (string Owner, string Destination, string Group) Key(
            IGameEntity unit,
            UnitArrivedResult arrival
        ) =>
            (
                (unit as ISceneNode)?.GetOwnerInstanceID(),
                arrival.Destination?.InstanceID,
                string.IsNullOrEmpty(arrival.MovementGroupID)
                    ? unit.InstanceID
                    : arrival.MovementGroupID
            );

        private static void AddGroup<T>(
            IDictionary<(string Owner, string Destination, string Group), List<T>> groups,
            IDictionary<(string Owner, string Destination, string Group), Planet> destinations,
            (string Owner, string Destination, string Group) key,
            T item,
            Planet destination
        )
        {
            if (!groups.TryGetValue(key, out List<T> items))
            {
                items = new List<T>();
                groups.Add(key, items);
                destinations.Add(key, destination);
            }
            items.Add(item);
        }

        private static void SetArrivalLocation(Message message, Planet planet, ISceneNode target)
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = (target ?? planet)?.InstanceID;
        }

        private void AddArrivalDelivery(
            ICollection<MessageRequestedResult> deliveries,
            Faction faction,
            Message message,
            params GameResult[] sourceResults
        ) => AddDelivery(deliveries, faction, message, sourceResults);

        private static Faction GetArrivalFaction(GameRoot game, string ownerID) =>
            string.IsNullOrEmpty(ownerID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == ownerID);
    }
}
#endregion
#region MessageFactory.Blockades
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates blockade and evacuation results into faction reports.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddBlockadeMessages(
            IEnumerable<BlockadeChangedResult> blockadeResults,
            IEnumerable<EvacuationLossesResult> evacuationResults,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (BlockadeChangedResult result in blockadeResults)
            {
                if (!result.Blockaded)
                    continue;
                Faction blockadingFaction = GetBlockadeFaction(
                    game,
                    result.BlockadingFleet?.GetOwnerInstanceID()
                );
                Faction targetFaction = GetBlockadeFaction(game, result.Planet?.OwnerInstanceID);
                AddBlockadeDelivery(
                    deliveries,
                    blockadingFaction,
                    result,
                    targetFaction,
                    MessageResultType.BlockadeInitiated
                );
                if (targetFaction?.InstanceID != blockadingFaction?.InstanceID)
                {
                    AddBlockadeDelivery(
                        deliveries,
                        targetFaction,
                        result,
                        blockadingFaction,
                        MessageResultType.BlockadeDetected
                    );
                }
            }

            foreach (EvacuationLossesResult result in evacuationResults)
            {
                if (result == null)
                    continue;
                MessageDefinition definition = GetDefinition(MessageResultType.EvacuationLosses);
                Message message = BuildBlockadeMessage(
                    definition,
                    result.Faction,
                    new Dictionary<string, string>
                    {
                        { "system", result.Location?.GetDisplayName() ?? string.Empty },
                        { "units", FormatLostUnits(result) },
                    }
                );
                SetBlockadeLocation(message, result.Location, result.Location);
                AddDelivery(deliveries, result.Faction, message, result);
            }
        }

        private void AddBlockadeDelivery(
            ICollection<MessageRequestedResult> deliveries,
            Faction recipient,
            BlockadeChangedResult result,
            Faction otherFaction,
            MessageResultType resultType
        )
        {
            Dictionary<string, string> values = new Dictionary<string, string>
            {
                {
                    "faction",
                    (
                        resultType == MessageResultType.BlockadeInitiated ? recipient : otherFaction
                    )?.GetDisplayName() ?? string.Empty
                },
                { "fleet", result.BlockadingFleet?.GetDisplayName() ?? string.Empty },
                { "system", result.Planet?.GetDisplayName() ?? string.Empty },
            };
            if (resultType == MessageResultType.BlockadeInitiated)
                values["target"] = otherFaction?.GetDisplayName() ?? string.Empty;

            MessageDefinition definition = GetDefinition(resultType);
            Message message = BuildBlockadeMessage(
                definition,
                recipient,
                values,
                resultType == MessageResultType.BlockadeInitiated ? otherFaction : null
            );
            SetBlockadeLocation(message, result.Planet, result.BlockadingFleet);
            AddDelivery(deliveries, recipient, message, result);
        }

        private Message BuildBlockadeMessage(
            MessageDefinition definition,
            Faction recipient,
            Dictionary<string, string> values,
            Faction imageFaction = null
        )
        {
            Message message = _templateBuilder.Build(definition, recipient, values, imageFaction);
            return WithAdvisorNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static void SetBlockadeLocation(
            Message message,
            ISceneNode planet,
            ISceneNode target
        )
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = (target ?? planet)?.InstanceID;
        }

        private static string FormatLostUnits(EvacuationLossesResult result)
        {
            IEnumerable<IGameEntity> units = result
                .LostShips.Cast<IGameEntity>()
                .Concat(result.LostStarfighters)
                .Concat(result.LostRegiments);
            return string.Join(
                "\n",
                units
                    .Select(unit => unit?.GetDisplayName() ?? string.Empty)
                    .Where(name => name.Length > 0)
            );
        }

        private static Faction GetBlockadeFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
#endregion
#region MessageFactory.Combat
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates space combat, bombardment, and planetary assault results into reports.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddCombatMessages(
            IEnumerable<SpaceCombatResult> battles,
            IEnumerable<BombardmentResult> bombardments,
            IEnumerable<PlanetaryAssaultResult> assaults,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (SpaceCombatResult result in battles)
            {
                Faction attacker = GetCombatFaction(game, GetOwnerID(result, CombatSide.Attacker));
                Faction defender = GetCombatFaction(game, GetOwnerID(result, CombatSide.Defender));
                AddCombatDelivery(
                    deliveries,
                    attacker,
                    CreateSpaceBattle(attacker, result, defender),
                    result
                );
                if (defender?.InstanceID != attacker?.InstanceID)
                    AddCombatDelivery(
                        deliveries,
                        defender,
                        CreateSpaceBattle(defender, result, attacker),
                        result
                    );
            }

            foreach (BombardmentResult result in bombardments)
            {
                if (result?.AttackingFaction == null || result.Planet == null)
                    continue;
                Faction defender =
                    result.OwnershipChange?.PreviousOwner
                    ?? GetCombatFaction(game, result.Planet.OwnerInstanceID);
                AddCombatDelivery(
                    deliveries,
                    result.AttackingFaction,
                    CreateBombardment(result.AttackingFaction, result, defender),
                    result
                );
                if (defender?.InstanceID != result.AttackingFaction.InstanceID)
                    AddCombatDelivery(
                        deliveries,
                        defender,
                        CreateBombardment(defender, result, defender),
                        result
                    );
            }

            foreach (PlanetaryAssaultResult result in assaults)
            {
                if (result?.AttackingFaction == null || result.Planet == null)
                    continue;
                Faction defender =
                    result.OwnershipChange?.PreviousOwner
                    ?? GetCombatFaction(game, result.Planet.OwnerInstanceID);
                AddCombatDelivery(
                    deliveries,
                    result.AttackingFaction,
                    CreateAssault(result.AttackingFaction, result, defender),
                    result
                );
                if (defender?.InstanceID != result.AttackingFaction.InstanceID)
                    AddCombatDelivery(
                        deliveries,
                        defender,
                        CreateAssault(defender, result, defender),
                        result
                    );
            }
        }

        private Message CreateSpaceBattle(
            Faction faction,
            SpaceCombatResult result,
            Faction opponent
        )
        {
            if (result == null)
                return null;
            MessageResultOutcome outcome = GetOutcome(faction, result);
            if (outcome == MessageResultOutcome.None)
                return null;

            MessageDefinition definition = GetDefinition(MessageResultType.SpaceBattle, outcome);
            Dictionary<string, string> values = new Dictionary<string, string>
            {
                { "faction", faction?.GetDisplayName() ?? string.Empty },
                { "opponent", opponent?.GetDisplayName() ?? string.Empty },
                { "system", result.Planet?.GetDisplayName() ?? string.Empty },
            };
            AddNarrative(values, definition, faction, opponent, result, outcome);
            Message message = BuildCombatMessage(definition, faction, values);
            SetCombatLocation(message, result.Planet, GetFleet(faction, result));
            return message;
        }

        private Message CreateBombardment(
            Faction faction,
            BombardmentResult result,
            Faction targetFaction
        )
        {
            MessageDefinition definition = GetDefinition(
                MessageResultType.Bombardment,
                GetBombardmentOutcome(result),
                GetBombardmentOwnership(result),
                planetDestroyed: result.PlanetDestroyed
            );
            Message message = BuildCombatMessage(
                definition,
                faction,
                CombatValues(result.AttackingFaction, targetFaction, result.Planet)
            );
            WithAdvisorNotification(
                message,
                faction?.InstanceID == result.AttackingFaction?.InstanceID
                    ? AdvisorNotificationType.None
                    : AdvisorNotificationType.Bombardment
            );
            SetCombatLocation(message, result.Planet, result.Planet);
            return message;
        }

        private Message CreateAssault(
            Faction faction,
            PlanetaryAssaultResult result,
            Faction targetFaction
        )
        {
            MessageDefinition definition = GetDefinition(
                MessageResultType.PlanetaryAssault,
                result.Success ? MessageResultOutcome.Success : MessageResultOutcome.Failed,
                GetAssaultOwnership(result)
            );
            Message message = BuildCombatMessage(
                definition,
                faction,
                CombatValues(result.AttackingFaction, targetFaction, result.Planet),
                result.AttackingFaction
            );
            WithAdvisorNotification(
                message,
                faction?.InstanceID == result.AttackingFaction?.InstanceID
                    ? AdvisorNotificationType.None
                    : AdvisorNotificationType.PlanetaryAssault
            );
            SetCombatLocation(message, result.Planet, result.Planet);
            return message;
        }

        private Message BuildCombatMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            Faction imageFaction = null
        )
        {
            Message message = _templateBuilder.Build(definition, faction, values, imageFaction);
            return WithAdvisorNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static void AddNarrative(
            Dictionary<string, string> values,
            MessageDefinition definition,
            Faction faction,
            Faction opponent,
            SpaceCombatResult result,
            MessageResultOutcome outcome
        )
        {
            SpaceBattleNarrativeTemplates templates = definition?.SpaceBattleNarrative;
            if (templates == null)
                return;
            values["headline"] = Render(GetHeadline(templates, outcome), values);
            values["situation"] = Render(
                GetSituation(templates, faction, opponent, result, outcome),
                values
            );
            values["fleetOutcome"] = BuildFleetOutcome(
                templates,
                faction,
                opponent,
                result,
                outcome,
                values
            );
        }

        private static MessageResultOutcome GetOutcome(Faction faction, SpaceCombatResult result)
        {
            if (result.Winner == CombatSide.Draw)
                return MessageResultOutcome.Stalemate;
            if (faction?.InstanceID == GetOwnerID(result, CombatSide.Attacker))
                return result.Winner == CombatSide.Attacker
                    ? MessageResultOutcome.Victory
                    : MessageResultOutcome.Defeat;
            if (faction?.InstanceID == GetOwnerID(result, CombatSide.Defender))
                return result.Winner == CombatSide.Defender
                    ? MessageResultOutcome.Victory
                    : MessageResultOutcome.Defeat;
            return MessageResultOutcome.None;
        }

        private static string GetHeadline(
            SpaceBattleNarrativeTemplates templates,
            MessageResultOutcome outcome
        ) =>
            outcome switch
            {
                MessageResultOutcome.Victory => templates.VictoryHeadline,
                MessageResultOutcome.Defeat => templates.DefeatHeadline,
                MessageResultOutcome.Stalemate => templates.StalemateHeadline,
                _ => string.Empty,
            };

        private static string GetSituation(
            SpaceBattleNarrativeTemplates templates,
            Faction faction,
            Faction opponent,
            SpaceCombatResult result,
            MessageResultOutcome outcome
        )
        {
            if (outcome == MessageResultOutcome.Stalemate)
                return templates.NoVictor;
            string ownerID = result.Planet?.OwnerInstanceID;
            if (string.IsNullOrEmpty(ownerID))
                return outcome == MessageResultOutcome.Victory
                    ? templates.NeutralVictory
                    : templates.NeutralDefeat;

            CombatSide side = GetSide(faction, result);
            bool factionOwnsPlanet = ownerID == faction?.InstanceID;
            bool opponentOwnsPlanet = ownerID == opponent?.InstanceID;
            if (outcome == MessageResultOutcome.Victory)
            {
                if (factionOwnsPlanet)
                    return side == CombatSide.Defender
                        ? templates.SuccessfullyDefended
                        : templates.BlockadeBroken;
                return side == CombatSide.Defender
                    ? templates.BlockadeMaintained
                    : templates.BlockadeEstablished;
            }
            if (factionOwnsPlanet)
                return side == CombatSide.Defender
                    ? templates.BlockadeEstablished
                    : templates.BlockadeMaintained;
            if (opponentOwnsPlanet)
                return side == CombatSide.Attacker
                    ? templates.AttackFailed
                    : templates.BlockadeBroken;
            return templates.AttackFailed;
        }

        private static string BuildFleetOutcome(
            SpaceBattleNarrativeTemplates templates,
            Faction faction,
            Faction opponent,
            SpaceCombatResult result,
            MessageResultOutcome outcome,
            Dictionary<string, string> values
        )
        {
            SpaceCombatSideOutcome factionOutcome = GetSideOutcome(faction, result);
            SpaceCombatSideOutcome opponentOutcome = GetSideOutcome(opponent, result);
            if (
                outcome == MessageResultOutcome.Stalemate
                && factionOutcome == SpaceCombatSideOutcome.Destroyed
                && opponentOutcome == SpaceCombatSideOutcome.Destroyed
            )
                return Render(templates.AllShipsDestroyed, values);

            List<string> lines = new List<string>();
            AddFleetLine(lines, templates, faction, factionOutcome, result, values, true);
            AddFleetLine(lines, templates, opponent, opponentOutcome, result, values, false);
            return string.Join("\n", lines);
        }

        private static void AddFleetLine(
            ICollection<string> lines,
            SpaceBattleNarrativeTemplates templates,
            Faction faction,
            SpaceCombatSideOutcome outcome,
            SpaceCombatResult result,
            Dictionary<string, string> values,
            bool includeRetreatDestination
        )
        {
            if (outcome is SpaceCombatSideOutcome.Active or SpaceCombatSideOutcome.Unknown)
                return;
            Dictionary<string, string> lineValues = new Dictionary<string, string>(values)
            {
                ["fleetFaction"] = faction?.GetDisplayName() ?? string.Empty,
            };
            string template;
            if (outcome == SpaceCombatSideOutcome.Destroyed)
            {
                template = templates.FleetDestroyed;
            }
            else
            {
                Planet destination = includeRetreatDestination
                    ? GetFleet(faction, result)?.GetParentOfType<Planet>()
                    : null;
                if (destination == result.Planet)
                    destination = null;
                lineValues["retreatSystem"] = destination?.GetDisplayName() ?? string.Empty;
                template =
                    destination == null ? templates.FleetWithdrawn : templates.FleetWithdrawnTo;
            }
            string line = Render(template, lineValues);
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }

        private static CombatSide GetSide(Faction faction, SpaceCombatResult result)
        {
            if (faction?.InstanceID == GetOwnerID(result, CombatSide.Attacker))
                return CombatSide.Attacker;
            if (faction?.InstanceID == GetOwnerID(result, CombatSide.Defender))
                return CombatSide.Defender;
            return CombatSide.Draw;
        }

        private static SpaceCombatSideOutcome GetSideOutcome(
            Faction faction,
            SpaceCombatResult result
        ) =>
            GetSide(faction, result) switch
            {
                CombatSide.Attacker => result.AttackerOutcome,
                CombatSide.Defender => result.DefenderOutcome,
                _ => SpaceCombatSideOutcome.Unknown,
            };

        private static Fleet GetFleet(Faction faction, SpaceCombatResult result)
        {
            if (faction?.InstanceID == GetOwnerID(result, CombatSide.Attacker))
                return result.AttackerFleet;
            return faction?.InstanceID == GetOwnerID(result, CombatSide.Defender)
                ? result.DefenderFleet
                : null;
        }

        private static string GetOwnerID(SpaceCombatResult result, CombatSide side) =>
            side switch
            {
                CombatSide.Attacker => string.IsNullOrEmpty(result?.AttackerOwnerInstanceID)
                    ? result?.AttackerFleet?.GetOwnerInstanceID()
                    : result.AttackerOwnerInstanceID,
                CombatSide.Defender => string.IsNullOrEmpty(result?.DefenderOwnerInstanceID)
                    ? result?.DefenderFleet?.GetOwnerInstanceID()
                    : result.DefenderOwnerInstanceID,
                _ => null,
            };

        private static MessageResultOutcome GetBombardmentOutcome(BombardmentResult result)
        {
            if (
                result.PlanetDestroyed
                || result.HeadquartersDestroyed
                || result.EnergyCapacityDamage > 0
                || result.AllocatedEnergyDamage > 0
                || result.DestroyedBuildings.Any()
                || result.DestroyedRegiments.Any()
            )
                return MessageResultOutcome.TargetLosses;
            return result.DestroyedCapitalShips.Any() || result.AttackerShipDamage.Any()
                ? MessageResultOutcome.AttackerLosses
                : MessageResultOutcome.NoLosses;
        }

        private static MessagePlanetOwnership GetBombardmentOwnership(BombardmentResult result) =>
            result?.OwnershipChange != null
                ? Ownership(result.OwnershipChange.PreviousOwner?.InstanceID)
                : Ownership(result?.Planet?.OwnerInstanceID);

        private static MessagePlanetOwnership GetAssaultOwnership(PlanetaryAssaultResult result) =>
            result?.OwnershipChange != null
                ? Ownership(result.OwnershipChange.PreviousOwner?.InstanceID)
                : Ownership(result?.Planet?.OwnerInstanceID);

        private static MessagePlanetOwnership Ownership(string ownerID) =>
            string.IsNullOrEmpty(ownerID)
                ? MessagePlanetOwnership.Neutral
                : MessagePlanetOwnership.Owned;

        private static Dictionary<string, string> CombatValues(
            Faction attacker,
            Faction target,
            Planet planet
        ) =>
            new Dictionary<string, string>
            {
                { "faction", attacker?.GetDisplayName() ?? string.Empty },
                { "target", target?.GetDisplayName() ?? string.Empty },
                { "system", planet?.GetDisplayName() ?? string.Empty },
            };

        private static string Render(string template, Dictionary<string, string> values) =>
            MessageTemplateBuilder.Interpolate(template, values);

        private static void SetCombatLocation(Message message, Planet planet, IGameEntity target)
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = target?.InstanceID ?? planet?.InstanceID;
        }

        private void AddCombatDelivery(
            ICollection<MessageRequestedResult> deliveries,
            Faction faction,
            Message message,
            GameResult sourceResult
        ) => AddDelivery(deliveries, faction, message, sourceResult);

        private static Faction GetCombatFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
#endregion
#region MessageFactory.Deployments
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates completed deployments and failed facility arrivals into unit reports.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddDeploymentMessages(
            IEnumerable<GameObjectDeployedResult> results,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            GameObjectDeployedResult[] deploymentResults = (
                results ?? Enumerable.Empty<GameObjectDeployedResult>()
            )
                .Where(result => result?.GameObject is IManufacturable)
                .ToArray();

            foreach (
                GameObjectDeployedResult result in deploymentResults.Where(result =>
                    result.GameObject is not Regiment
                )
            )
            {
                IManufacturable unit = (IManufacturable)result.GameObject;
                ISceneNode node = unit as ISceneNode;
                Planet destination = node?.GetParentOfType<Planet>();
                Faction faction = GetDeploymentFaction(game, unit.GetOwnerInstanceID());
                Message message = unit is Building building
                    ? building.Movement == null
                        ? CreateFacilityMessage(faction, building, destination)
                        : null
                    : CreateUnit(faction, unit as IGameEntity, destination, game);
                AddDelivery(deliveries, faction, message, result);
            }

            var regimentItems = deploymentResults
                .Where(result => result.GameObject is Regiment)
                .Select(result =>
                {
                    Regiment regiment = (Regiment)result.GameObject;
                    Planet destination = regiment.GetParentOfType<Planet>();
                    Faction faction = GetDeploymentFaction(game, regiment.GetOwnerInstanceID());
                    return new
                    {
                        Regiment = regiment,
                        Result = result,
                        Destination = destination,
                        Faction = faction,
                        Definition = GetDefinition(
                            MessageResultType.RegimentDeployed,
                            gameObjectTypeId: regiment.TypeID
                        ),
                    };
                })
                .Where(item => item.Faction != null && item.Definition != null);
            foreach (
                var group in regimentItems.GroupBy(item =>
                    (
                        item.Faction.InstanceID,
                        DestinationInstanceID: item.Destination?.InstanceID,
                        item.Definition
                    )
                )
            )
            {
                var first = group.First();
                AddDelivery(
                    deliveries,
                    first.Faction,
                    CreateRegiments(
                        first.Faction,
                        group.Select(item => item.Regiment),
                        first.Destination,
                        first.Definition
                    ),
                    group.Select(item => (GameResult)item.Result).ToArray()
                );
            }
        }

        private void AddFacilityLossMessages(
            IEnumerable<GameObjectDestroyedOnArrivalResult> results,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (GameObjectDestroyedOnArrivalResult result in results)
            {
                if (result.DestroyedObject is not Building building)
                    continue;
                Planet destination = GetDeploymentPlanet(result.Context ?? result.Ref);
                Faction faction = GetDeploymentFaction(game, building.GetOwnerInstanceID());
                MessageDefinition definition = GetDefinition(MessageResultType.FacilityLost);
                Message message = BuildDeploymentMessage(
                    definition,
                    faction,
                    new Dictionary<string, string>
                    {
                        { "item", building.GetDisplayName() ?? string.Empty },
                        { "system", destination?.GetDisplayName() ?? string.Empty },
                    }
                );
                SetDeploymentLocation(message, destination, destination);
                AddDelivery(deliveries, faction, message, result);
            }
        }

        public Message CreateFacilityMessage(Faction faction, Building building, Planet destination)
        {
            BuildingType buildingType = building?.BuildingType ?? BuildingType.None;
            if (buildingType == BuildingType.None)
                return null;
            MessageDefinition definition = GetDefinition(
                MessageResultType.FacilityDeployed,
                buildingType: buildingType
            );
            Message message = BuildDeploymentMessage(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "item", building.GetDisplayName() ?? string.Empty },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                },
                imageOverride: building.MessageImagePath
            );
            SetDeploymentLocation(message, destination, building);
            return message;
        }

        private Message CreateUnit(
            Faction faction,
            IGameEntity unit,
            Planet destination,
            GameRoot game
        )
        {
            MessageResultType resultType = unit switch
            {
                CapitalShip ship when IsPlanetDestroying(ship, game) =>
                    MessageResultType.DeathStarDeployed,
                CapitalShip => MessageResultType.CapitalShipDeployed,
                Starfighter => MessageResultType.StarfighterDeployed,
                Regiment => MessageResultType.RegimentDeployed,
                _ => MessageResultType.None,
            };
            if (resultType == MessageResultType.None)
                return null;
            string itemName = unit.GetDisplayName() ?? string.Empty;
            MessageDefinition definition = GetDefinition(resultType, gameObjectTypeId: unit.TypeID);
            Message message = BuildDeploymentMessage(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "item", itemName },
                    { "type", itemName },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                },
                imageOverride: unit.EncyclopediaImagePath
            );
            SetDeploymentLocation(message, destination, unit as ISceneNode);
            return message;
        }

        private Message CreateRegiments(
            Faction faction,
            IEnumerable<Regiment> regiments,
            Planet destination,
            MessageDefinition definition
        )
        {
            Regiment[] regimentArray = regiments?.Where(regiment => regiment != null).ToArray();
            if (regimentArray == null || regimentArray.Length == 0)
                return null;
            string firstName = regimentArray[0].GetDisplayName() ?? string.Empty;
            Message message = BuildDeploymentMessage(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "item", firstName },
                    {
                        "items",
                        string.Join(
                            "\n",
                            regimentArray.Select(regiment => regiment.GetDisplayName())
                        )
                    },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                },
                imageOverride: regimentArray[0].EncyclopediaImagePath
            );
            SetDeploymentLocation(message, destination, regimentArray[0]);
            return message;
        }

        private Message BuildDeploymentMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            string imageOverride = null
        )
        {
            Message message = _templateBuilder.Build(
                definition,
                faction,
                values,
                imageOverride: imageOverride
            );
            return WithAdvisorNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static bool IsPlanetDestroying(CapitalShip ship, GameRoot game) =>
            ship != null
            && game?.Config?.Combat?.Bombardment?.PlanetDestroyingCapitalShipTypeIDs?.Contains(
                ship.TypeID
            ) == true;

        private static Planet GetDeploymentPlanet(IGameEntity entity) =>
            entity is Planet planet ? planet
            : entity is ISceneNode node
                ? node.GetParentOfType<Planet>() ?? node.GetLastParent() as Planet
            : null;

        private static void SetDeploymentLocation(Message message, Planet planet, ISceneNode target)
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = (target ?? planet)?.InstanceID;
        }

        private static Faction GetDeploymentFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
#endregion
#region MessageFactory.Economy
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates economic simulation results into faction message deliveries.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddSmugglingMessages(
            IEnumerable<SmugglingChangedResult> results,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (SmugglingChangedResult result in results)
            {
                AddSmugglingDelivery(deliveries, result.Controller, result, false);
                AddSmugglingDelivery(deliveries, result.Beneficiary, result, true);
            }
        }

        private void AddManufacturingMessages(
            IEnumerable<ManufacturingIdleResult> results,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (ManufacturingIdleResult result in results)
            {
                if (result.ManufacturingType == ManufacturingType.None)
                    continue;

                Message message = BuildEconomyMessage(
                    GetDefinition(
                        MessageResultType.ManufacturingIdle,
                        manufacturingType: result.ManufacturingType
                    ),
                    result.Faction,
                    new Dictionary<string, string>
                    {
                        { "system", result.ProductionPlanet?.GetDisplayName() ?? string.Empty },
                    }
                );
                if (message != null)
                {
                    message.EventLocationInstanceID = result.ProductionPlanet?.InstanceID;
                    message.NavigationTargetInstanceID = result.ProductionPlanet?.InstanceID;
                }
                WithAdvisorNotification(message, AdvisorNotificationType.Manufacturing);
                AddDelivery(deliveries, result.Faction, message, result);
            }
        }

        private void AddSmugglingDelivery(
            ICollection<MessageRequestedResult> deliveries,
            Faction recipient,
            SmugglingChangedResult result,
            bool receivesBenefits
        )
        {
            bool active = result.NewPercent != 0;
            MessageResultType resultType = (receivesBenefits, active) switch
            {
                (false, true) => MessageResultType.SmugglingLosses,
                (false, false) => MessageResultType.SmugglingLossesEnded,
                (true, true) => MessageResultType.SmugglingBenefits,
                _ => MessageResultType.SmugglingBenefitsEnded,
            };
            Message message = BuildEconomyMessage(
                GetDefinition(resultType),
                recipient,
                new Dictionary<string, string>
                {
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                }
            );
            if (message != null)
            {
                message.EventLocationInstanceID = result.Planet?.InstanceID;
                message.NavigationTargetInstanceID = result.Planet?.InstanceID;
            }
            AddDelivery(deliveries, recipient, message, result);
        }

        private Message BuildEconomyMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values
        )
        {
            Message message = _templateBuilder.Build(definition, faction, values);
            return WithAdvisorNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }
    }
}
#endregion
#region MessageFactory.Maintenance
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Groups maintenance losses and translates them into faction reports.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddMaintenanceMessages(
            IEnumerable<GameObjectAutoscrappedResult> results,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            MessageDefinition definition = GetDefinition(MessageResultType.MaintenanceAutoscrap);
            var reportItems = (results ?? Enumerable.Empty<GameObjectAutoscrappedResult>())
                .Where(result => result != null)
                .Select(result =>
                {
                    Planet location = GetMaintenancePlanet(
                        result.Context ?? result.Ref ?? result.DestroyedObject
                    );
                    Faction faction =
                        GetOwner(game, result.DestroyedObject)
                        ?? GetOwner(game, result.Ref)
                        ?? GetMaintenanceFaction(game, location?.OwnerInstanceID);
                    return new
                    {
                        Result = result,
                        Location = location,
                        Faction = faction,
                    };
                })
                .Where(item => item.Faction != null && definition != null);

            foreach (
                var group in reportItems.GroupBy(item =>
                    (item.Faction.InstanceID, LocationInstanceID: item.Location?.InstanceID)
                )
            )
            {
                var first = group.First();
                GameObjectAutoscrappedResult[] groupedResults = group
                    .Select(item => item.Result)
                    .ToArray();
                Message message = _templateBuilder.Build(
                    definition,
                    first.Faction,
                    new Dictionary<string, string>
                    {
                        {
                            "item",
                            groupedResults[0].DestroyedObject?.GetDisplayName() ?? string.Empty
                        },
                        {
                            "items",
                            string.Join(
                                "\n",
                                groupedResults.Select(result =>
                                    result.DestroyedObject?.GetDisplayName() ?? string.Empty
                                )
                            )
                        },
                        { "system", first.Location?.GetDisplayName() ?? string.Empty },
                    }
                );
                if (message != null)
                {
                    message.EventLocationInstanceID = first.Location?.InstanceID;
                    message.NavigationTargetInstanceID =
                        (groupedResults[0].DestroyedObject as ISceneNode)?.InstanceID
                        ?? first.Location?.InstanceID;
                }
                WithAdvisorNotification(
                    message,
                    AdvisorNotificationPolicy.GetDefault(definition.ResultType)
                );
                AddDelivery(
                    deliveries,
                    first.Faction,
                    message,
                    groupedResults.Cast<GameResult>().ToArray()
                );
            }
        }

        private static Planet GetMaintenancePlanet(IGameEntity entity)
        {
            if (entity is Planet planet)
                return planet;
            return entity is ISceneNode node
                ? node.GetParentOfType<Planet>() ?? node.GetLastParent() as Planet
                : null;
        }

        private static Faction GetOwner(GameRoot game, IGameEntity entity) =>
            entity is ISceneNode node
                ? GetMaintenanceFaction(game, node.GetOwnerInstanceID())
                : null;

        private static Faction GetMaintenanceFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
#endregion
#region MessageFactory.Politics
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates uprising and popular-support ownership results into faction messages.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddUprisingMessages(
            IEnumerable<PlanetNearUprisingResult> nearResults,
            IEnumerable<PlanetUprisingStartedResult> startedResults,
            IEnumerable<PlanetUprisingEndedResult> endedResults,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (PlanetNearUprisingResult result in nearResults)
            {
                Faction controller = GetPoliticalFaction(game, result.Planet?.OwnerInstanceID);
                AddPoliticalDelivery(
                    deliveries,
                    controller,
                    CreateNearUprising(controller, result),
                    result
                );
            }

            foreach (PlanetUprisingStartedResult result in startedResults)
            {
                Faction controller = GetPoliticalFaction(game, result.Planet?.OwnerInstanceID);
                AddPoliticalDelivery(
                    deliveries,
                    controller,
                    CreateUprisingStarted(controller, result, controller),
                    result
                );
                if (result.InstigatorFaction?.InstanceID != controller?.InstanceID)
                {
                    AddPoliticalDelivery(
                        deliveries,
                        result.InstigatorFaction,
                        CreateUprisingStarted(result.InstigatorFaction, result, controller),
                        result
                    );
                }
            }

            foreach (PlanetUprisingEndedResult result in endedResults)
            {
                Faction controller =
                    GetPoliticalFaction(game, result.Planet?.OwnerInstanceID) ?? result.Faction;
                AddPoliticalDelivery(
                    deliveries,
                    controller,
                    CreateUprisingEnded(controller, result, controller),
                    result
                );
            }
        }

        private void AddOwnershipMessages(
            IEnumerable<PlanetOwnershipChangedResult> results,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (PlanetOwnershipChangedResult result in results)
            {
                if (result.Reason != PlanetOwnershipChangeReason.PopularSupport)
                    continue;

                foreach (Faction recipient in GetRecipients(result, game))
                {
                    Message message =
                        recipient == result.NewOwner ? CreateJoined(result)
                        : result.NewOwner != null ? CreateJoinedEnemy(result, recipient)
                        : CreateNeutrality(result, recipient);
                    AddPoliticalDelivery(deliveries, recipient, message, result);
                }
            }
        }

        private Message CreateNearUprising(Faction faction, PlanetNearUprisingResult result)
        {
            if (result == null)
                return null;
            return BuildPoliticalMessage(
                MessageResultType.NearUprising,
                faction,
                new Dictionary<string, string>
                {
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                },
                result.Planet?.InstanceID,
                AdvisorNotificationType.NegativePopularSupport
            );
        }

        private Message CreateUprisingStarted(
            Faction faction,
            PlanetUprisingStartedResult result,
            Faction controller
        )
        {
            if (result == null)
                return null;
            AdvisorNotificationType notification =
                faction?.InstanceID == controller?.InstanceID
                    ? AdvisorNotificationType.NegativePopularSupport
                    : AdvisorNotificationType.PositivePopularSupport;
            return BuildPoliticalMessage(
                MessageResultType.UprisingStarted,
                faction,
                new Dictionary<string, string>
                {
                    { "faction", controller?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                },
                result.Planet?.InstanceID,
                notification
            );
        }

        private Message CreateUprisingEnded(
            Faction faction,
            PlanetUprisingEndedResult result,
            Faction controller
        )
        {
            if (result == null)
                return null;
            return BuildPoliticalMessage(
                MessageResultType.UprisingEnded,
                faction,
                new Dictionary<string, string>
                {
                    { "faction", controller?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                },
                result.Planet?.InstanceID,
                AdvisorNotificationType.PositivePopularSupport,
                controller
            );
        }

        private Message CreateJoined(PlanetOwnershipChangedResult result)
        {
            if (result?.NewOwner == null)
                return null;
            return BuildPoliticalMessage(
                MessageResultType.PlanetJoinedBySupport,
                result.NewOwner,
                Values(result.NewOwner, result.Planet?.GetDisplayName()),
                result.Planet?.InstanceID,
                AdvisorNotificationType.PositivePopularSupport
            );
        }

        private Message CreateJoinedEnemy(PlanetOwnershipChangedResult result, Faction recipient)
        {
            if (
                result?.NewOwner == null
                || recipient == null
                || recipient.InstanceID == result.NewOwner.InstanceID
            )
                return null;
            return BuildPoliticalMessage(
                MessageResultType.PlanetJoinedEnemyBySupport,
                recipient,
                Values(result.NewOwner, result.Planet?.GetDisplayName()),
                result.Planet?.InstanceID,
                AdvisorNotificationType.NegativePopularSupport,
                result.NewOwner
            );
        }

        private Message CreateNeutrality(PlanetOwnershipChangedResult result, Faction recipient)
        {
            if (result?.PreviousOwner == null || result.NewOwner != null || recipient == null)
                return null;
            return BuildPoliticalMessage(
                MessageResultType.PlanetDeclaredNeutralityBySupport,
                recipient,
                Values(result.PreviousOwner, result.Planet?.GetDisplayName()),
                result.Planet?.InstanceID,
                AdvisorNotificationType.NegativePopularSupport
            );
        }

        private Message BuildPoliticalMessage(
            MessageResultType resultType,
            Faction faction,
            Dictionary<string, string> values,
            string planetInstanceID,
            AdvisorNotificationType notification,
            Faction imageFaction = null
        )
        {
            MessageDefinition definition = GetDefinition(resultType);
            Message message = _templateBuilder.Build(definition, faction, values, imageFaction);
            if (message != null)
            {
                message.EventLocationInstanceID = planetInstanceID;
                message.NavigationTargetInstanceID = planetInstanceID;
            }
            if (message != null)
                GetRequest(message).NotificationType = notification;
            return message;
        }

        private void AddPoliticalDelivery(
            ICollection<MessageRequestedResult> deliveries,
            Faction faction,
            Message message,
            GameResult sourceResult
        ) => AddDelivery(deliveries, faction, message, sourceResult);

        private static Dictionary<string, string> Values(Faction faction, string system) =>
            new Dictionary<string, string>
            {
                { "faction", faction?.GetDisplayName() ?? string.Empty },
                { "system", system ?? string.Empty },
            };

        private static IEnumerable<Faction> GetRecipients(
            PlanetOwnershipChangedResult result,
            GameRoot game
        )
        {
            HashSet<string> recipientIds = new HashSet<string>(
                result.ObserverFactionInstanceIDs ?? Enumerable.Empty<string>()
            );
            if (result.PreviousOwner != null)
                recipientIds.Add(result.PreviousOwner.InstanceID);
            if (result.NewOwner != null)
                recipientIds.Add(result.NewOwner.InstanceID);
            return game.GetFactions().Where(faction => recipientIds.Contains(faction.InstanceID));
        }

        private static Faction GetPoliticalFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
#endregion
#region MessageFactory.Repairs
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates completed ship repairs into faction message deliveries.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddRepairMessages(
            IEnumerable<ShipHullDamageResult> shipResults,
            IEnumerable<FighterDamageResult> fighterResults,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (ShipHullDamageResult result in shipResults)
            {
                if (result?.Ship == null || result.Ship.IsDamaged())
                    continue;
                AddRepairDelivery(
                    deliveries,
                    game,
                    result.Ship,
                    MessageResultType.CapitalShipRepaired,
                    result
                );
            }

            foreach (FighterDamageResult result in fighterResults)
            {
                if (result?.Fighter == null || result.Fighter.HasLosses())
                    continue;
                AddRepairDelivery(
                    deliveries,
                    game,
                    result.Fighter,
                    MessageResultType.StarfighterRepaired,
                    result
                );
            }
        }

        private void AddRepairDelivery(
            ICollection<MessageRequestedResult> deliveries,
            GameRoot game,
            ISceneNode unit,
            MessageResultType resultType,
            GameResult sourceResult
        )
        {
            Faction faction = game.GetFactions()
                .FirstOrDefault(candidate => candidate.InstanceID == unit.GetOwnerInstanceID());
            MessageDefinition definition = GetDefinition(resultType);
            Message message = _templateBuilder.Build(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "item", unit.GetDisplayName() ?? string.Empty },
                    { "attachment", unit.GetParent()?.GetDisplayName() ?? string.Empty },
                }
            );
            if (message != null)
            {
                message.EventLocationInstanceID = unit.GetParentOfType<Planet>()?.InstanceID;
                message.NavigationTargetInstanceID = unit.InstanceID;
            }
            WithAdvisorNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
            AddDelivery(deliveries, faction, message, sourceResult);
        }
    }
}
#endregion
#region MessageFactory.Research
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates research results into faction message deliveries.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddResearchMessages(
            IEnumerable<ResearchOrderedResult> completedResults,
            IEnumerable<ResearchExhaustedResult> exhaustedResults,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (ResearchOrderedResult result in completedResults)
            {
                if (result?.Technology == null)
                    continue;

                Message message = BuildResearchMessage(
                    GetDefinition(
                        MessageResultType.ResearchComplete,
                        discipline: result.Discipline
                    ),
                    result.Faction,
                    new Dictionary<string, string>
                    {
                        { "item", GetResearchDisplayName(result.Technology.GetReference()) },
                    }
                );
                AddDelivery(deliveries, result.Faction, message, result);
            }

            foreach (ResearchExhaustedResult result in exhaustedResults)
            {
                if (result == null)
                    continue;

                Message message = BuildResearchMessage(
                    GetDefinition(
                        MessageResultType.ResearchExhausted,
                        discipline: result.Discipline
                    ),
                    result.Faction,
                    new Dictionary<string, string>()
                );
                AddDelivery(deliveries, result.Faction, message, result);
            }
        }

        private Message BuildResearchMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values
        )
        {
            Message message = _templateBuilder.Build(definition, faction, values);
            return WithAdvisorNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static string GetResearchDisplayName(IGameEntity entity) =>
            entity?.GetDisplayName() ?? string.Empty;
    }
}
#endregion
#region MessageFactory.Strategy
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates strategic objectives and planet incidents into faction reports.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddObjectiveMessages(
            IEnumerable<PlanetOwnershipChangedResult> ownershipResults,
            IEnumerable<HeadquartersDestroyedResult> headquartersResults,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (PlanetOwnershipChangedResult result in ownershipResults)
            {
                MessageDefinition definition = Find(
                    MessageResultType.PlanetCaptured,
                    result.Planet?.InstanceID,
                    result.PreviousOwner?.InstanceID,
                    result.NewOwner?.InstanceID,
                    null
                );
                if (definition == null)
                    continue;

                foreach (Faction recipient in GetOwnershipRecipients(result, game))
                {
                    Message message = BuildStrategicMessage(
                        definition,
                        recipient,
                        new Dictionary<string, string>
                        {
                            { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                            {
                                "previousFaction",
                                result.PreviousOwner?.GetDisplayName() ?? string.Empty
                            },
                            { "newFaction", result.NewOwner?.GetDisplayName() ?? string.Empty },
                        },
                        result.NewOwner
                    );
                    SetStrategicLocation(message, result.Planet, result.Planet);
                    AddDelivery(deliveries, recipient, message, result);
                }
            }

            foreach (HeadquartersDestroyedResult result in headquartersResults)
            {
                MessageDefinition definition = Find(
                    MessageResultType.HeadquartersDestroyed,
                    result.Planet?.InstanceID,
                    null,
                    null,
                    result.Defender?.InstanceID
                );
                if (definition == null)
                    continue;

                foreach (
                    Faction recipient in new[] { result.Attacker, result.Defender }
                        .Where(faction => faction != null)
                        .Distinct()
                )
                {
                    Message message = BuildStrategicMessage(
                        definition,
                        recipient,
                        new Dictionary<string, string>
                        {
                            { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                            { "attacker", result.Attacker?.GetDisplayName() ?? string.Empty },
                            { "defender", result.Defender?.GetDisplayName() ?? string.Empty },
                        },
                        result.Attacker
                    );
                    SetStrategicLocation(message, result.Planet, result.Headquarters);
                    AddDelivery(deliveries, recipient, message, result);
                }
            }
        }

        private void AddIncidentMessages(
            IEnumerable<PlanetIncidentResult> results,
            GameRoot game,
            ICollection<MessageRequestedResult> deliveries
        )
        {
            foreach (PlanetIncidentResult result in results)
            {
                Faction recipient = GetStrategicFaction(game, result.Planet?.OwnerInstanceID);
                if (recipient == null)
                    continue;
                MessageResultType resultType = result.IncidentType switch
                {
                    IncidentType.Disaster => MessageResultType.NaturalDisaster,
                    IncidentType.Resource when result.NewValue > result.OldValue =>
                        MessageResultType.NewResources,
                    IncidentType.Resource => MessageResultType.ResourcesDepleted,
                    _ => MessageResultType.None,
                };
                if (resultType == MessageResultType.None)
                    continue;

                bool hasDestroyedObjects = result.DestroyedObjects.Count > 0;
                MessageDefinition definition = _definitions.FirstOrDefault(candidate =>
                    candidate.ResultType == resultType
                    && (
                        resultType == MessageResultType.NaturalDisaster
                            ? candidate.HasDestroyedObjects == hasDestroyedObjects
                            : candidate.PlanetStat == result.ChangedStat
                    )
                );
                if (definition == null)
                    continue;

                Message message = BuildStrategicMessage(
                    definition,
                    recipient,
                    new Dictionary<string, string>
                    {
                        { "system", result.Planet.GetDisplayName() },
                        {
                            "destroyedObjects",
                            string.Join(
                                Environment.NewLine,
                                result.DestroyedObjects.Select(entity => entity.GetDisplayName())
                            )
                        },
                    },
                    recipient
                );
                SetStrategicLocation(
                    message,
                    result.Planet,
                    result.DestroyedObjects.OfType<ISceneNode>().FirstOrDefault()
                );
                AddDelivery(deliveries, recipient, message, result);
            }
        }

        private MessageDefinition Find(
            MessageResultType resultType,
            string planetInstanceID,
            string previousOwnerInstanceID,
            string newOwnerInstanceID,
            string factionInstanceID
        ) =>
            _definitions.FirstOrDefault(definition =>
                definition.ResultType == resultType
                && Matches(definition.PlanetInstanceID, planetInstanceID)
                && Matches(definition.PreviousOwnerInstanceID, previousOwnerInstanceID)
                && Matches(definition.NewOwnerInstanceID, newOwnerInstanceID)
                && Matches(definition.FactionInstanceID, factionInstanceID)
            );

        private Message BuildStrategicMessage(
            MessageDefinition definition,
            Faction recipient,
            Dictionary<string, string> values,
            Faction imageFaction
        )
        {
            Message message = _templateBuilder.Build(definition, recipient, values, imageFaction);
            return WithAdvisorNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static void SetStrategicLocation(
            Message message,
            ISceneNode planet,
            ISceneNode target
        )
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = (target ?? planet)?.InstanceID;
        }

        private static IEnumerable<Faction> GetOwnershipRecipients(
            PlanetOwnershipChangedResult result,
            GameRoot game
        )
        {
            HashSet<string> recipientIDs = new HashSet<string>(
                result.ObserverFactionInstanceIDs ?? Enumerable.Empty<string>()
            );
            if (result.PreviousOwner != null)
                recipientIDs.Add(result.PreviousOwner.InstanceID);
            if (result.NewOwner != null)
                recipientIDs.Add(result.NewOwner.InstanceID);
            return game.GetFactions().Where(faction => recipientIDs.Contains(faction.InstanceID));
        }

        private static bool Matches(string selector, string value) =>
            string.IsNullOrWhiteSpace(selector)
            || string.Equals(selector, value, StringComparison.Ordinal);

        private static Faction GetStrategicFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
#endregion
#region AuthoredMessageRequestFactory
namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates authored message requests into concrete faction deliveries.
    /// </summary>
    internal sealed class AuthoredMessageRequestFactory
    {
        private readonly MessageTemplateBuilder _templateBuilder;

        public AuthoredMessageRequestFactory(MessageTemplateBuilder templateBuilder)
        {
            _templateBuilder = templateBuilder;
        }

        /// <summary>
        /// Creates deliveries for valid authored message requests.
        /// </summary>
        public IEnumerable<MessageRequestedResult> CreateRequests(
            IEnumerable<MessageRequestedResult> results
        )
        {
            foreach (MessageRequestedResult result in results)
            {
                MessageRequestedResult delivery = CreateRequest(result);
                if (delivery != null)
                    yield return delivery;
            }
        }

        private MessageRequestedResult CreateRequest(MessageRequestedResult result)
        {
            if (result?.Recipient == null)
                return null;

            MessageDefinition definition = new MessageDefinition
            {
                MessageType = result.MessageType,
                Subject = result.Subject,
                Body = result.Body,
                BackgroundImage = CreateBackground(result),
                BackgroundAudioPath = result.BackgroundAudioPath,
            };
            Message message = _templateBuilder.Build(
                definition,
                result.Recipient,
                new Dictionary<string, string>
                {
                    { "subject", result.SubjectNode?.GetDisplayName() ?? string.Empty },
                    {
                        "relatedSubject",
                        result.RelatedSubjectNode?.GetDisplayName() ?? string.Empty
                    },
                    { "location", result.Location?.GetDisplayName() ?? string.Empty },
                    { "faction", result.Recipient.GetDisplayName() },
                },
                overlayImagePath: result.OverlayImagePath,
                officerVoicePath: result.OfficerVoicePath
            );
            if (message == null)
                return null;

            message.EventLocationInstanceID = result.Location?.InstanceID;
            message.NavigationTargetInstanceID = result.SubjectNode?.InstanceID;

            MessageRequestedResult delivery = new MessageRequestedResult
            {
                Recipient = result.Recipient,
                Message = message,
                AdvisorNotification = result.AdvisorNotification,
                AdvisorSubjectTypeID = result.SubjectNode?.TypeID,
            };
            ApplyAdvisorPreset(delivery);
            return delivery;
        }

        private static MessageBackgroundImage CreateBackground(MessageRequestedResult result)
        {
            if (
                string.IsNullOrWhiteSpace(result.BackgroundImageKey)
                && string.IsNullOrWhiteSpace(result.BackgroundImagePath)
            )
                return null;

            return new MessageBackgroundImage
            {
                Key = result.BackgroundImageKey,
                Path = result.BackgroundImagePath,
            };
        }

        private static void ApplyAdvisorPreset(MessageRequestedResult delivery)
        {
            AdvisorNotification notification = delivery.AdvisorNotification;
            if (notification?.Preset.HasValue != true)
                return;

            switch (notification.Preset.Value)
            {
                case AdvisorNotificationPreset.SubjectReport:
                    delivery.AdvisorSubjectNotification = AdvisorSubjectNotification.Report;
                    break;
                case AdvisorNotificationPreset.SubjectCaptured:
                    delivery.AdvisorSubjectNotification = AdvisorSubjectNotification.Captured;
                    break;
                case AdvisorNotificationPreset.SubjectReleased:
                    delivery.AdvisorSubjectNotification = AdvisorSubjectNotification.Released;
                    break;
                default:
                    delivery.NotificationType = notification.Preset.Value.ToNotificationType();
                    break;
            }
        }
    }
}
#endregion
