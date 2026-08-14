using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Presentation.Advisor;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Builds faction message deliveries from game results and configured message definitions.
    /// </summary>
    public partial class MessageFactory
    {
        private readonly MessageDefinition[] _definitions;
        private readonly MessageDefinitionResolver _definitionResolver;
        private readonly MessageTemplateBuilder _templateBuilder = new MessageTemplateBuilder();
        private readonly AuthoredMessageDeliveryFactory _authoredMessageFactory;
        private readonly MessageDeliveryBuilder _deliveryBuilder;

        /// <summary>
        /// Creates a message factory backed by the supplied message definitions.
        /// </summary>
        /// <param name="definitions">The message definitions used to select templates and images.</param>
        public MessageFactory(IEnumerable<MessageDefinition> definitions)
        {
            _definitions = definitions?.ToArray() ?? Array.Empty<MessageDefinition>();
            _definitionResolver = new MessageDefinitionResolver(_definitions);
            _deliveryBuilder = new MessageDeliveryBuilder();
            _authoredMessageFactory = new AuthoredMessageDeliveryFactory(_templateBuilder);
        }

        /// <summary>
        /// Creates messages for the factions affected by the supplied game results.
        /// </summary>
        /// <param name="results">The game results to translate into message deliveries.</param>
        /// <param name="game">The game state used to resolve affected factions and display names.</param>
        /// <returns>The messages to add to each recipient faction.</returns>
        public List<MessageDelivery> CreateMessages(IEnumerable<GameResult> results, GameRoot game)
        {
            _deliveryBuilder.Clear();
            MessageResultBatch batch = MessageResultBatch.Create(results);
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
            List<MessageDelivery> deliveries = new List<MessageDelivery>();

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

            deliveries.AddRange(_authoredMessageFactory.CreateDeliveries(batch.AuthoredRequests));

            return deliveries;
        }

        /// <summary>
        /// Adds Force-assisted traitor discovery reports.
        /// </summary>
        /// <param name="results">The traitor discovery results to process.</param>
        /// <param name="game">The game state used to resolve recipients and opposing factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddTraitorDiscoveryMessages(
            IEnumerable<TraitorDiscoveredResult> results,
            GameRoot game,
            List<MessageDelivery> deliveries
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
            List<MessageDelivery> deliveries
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
            List<MessageDelivery> deliveries
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
            List<MessageDelivery> deliveries
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
            List<MessageDelivery> deliveries
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
            List<MessageDelivery> deliveries
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
            List<MessageDelivery> deliveries
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
        /// <param name="deliveries">The delivery list to append to.</param>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="message">The message to deliver.</param>
        /// <param name="sourceResults">The simulation results that produced the automatic message.</param>
        private void AddDelivery(
            List<MessageDelivery> deliveries,
            Faction faction,
            Message message,
            params GameResult[] sourceResults
        )
        {
            _deliveryBuilder.Add(deliveries, faction, message, sourceResults);
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
            return _deliveryBuilder.WithNotification(message, notification);
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
            return _deliveryBuilder.WithSubject(message, notification, officer);
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
            return _definitionResolver.GetDefinition(
                resultType,
                outcome,
                planetOwnership,
                buildingType,
                manufacturingType,
                discipline,
                gameObjectTypeId,
                planetDestroyed,
                factionInstanceId
            );
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
            return _definitionResolver.GetMissionDefinition(
                resultType,
                outcome,
                missionTypeID,
                completionReason
            );
        }

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
