using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Builds faction message deliveries from game results and configured message definitions.
    /// </summary>
    public class MessageFactory
    {
        private readonly MessageDefinition[] _definitions;
        private readonly MessageTemplateBuilder _templateBuilder = new MessageTemplateBuilder();

        /// <summary>
        /// Creates a message factory backed by the supplied message definitions.
        /// </summary>
        /// <param name="definitions">The message definitions used to select templates and images.</param>
        public MessageFactory(IEnumerable<MessageDefinition> definitions)
        {
            _definitions = definitions?.ToArray() ?? Array.Empty<MessageDefinition>();
        }

        /// <summary>
        /// Creates messages for the factions affected by the supplied game results.
        /// </summary>
        /// <param name="results">The game results to translate into message deliveries.</param>
        /// <param name="game">The game state used to resolve affected factions and display names.</param>
        /// <returns>The messages to add to each recipient faction.</returns>
        public List<(Faction faction, Message message)> CreateMessages(
            IEnumerable<GameResult> results,
            GameRoot game
        )
        {
            GameResult[] resultArray = results?.ToArray() ?? Array.Empty<GameResult>();
            MissionCompletedResult[] missionResults = resultArray
                .OfType<MissionCompletedResult>()
                .ToArray();
            OfficerKilledResult[] killedResults = resultArray
                .OfType<OfficerKilledResult>()
                .ToArray();
            ForceDiscoveryResult[] forceDiscoveryResults = resultArray
                .OfType<ForceDiscoveryResult>()
                .ToArray();
            GameObjectSabotagedResult[] sabotageResults = resultArray
                .OfType<GameObjectSabotagedResult>()
                .ToArray();
            List<(Faction faction, Message message)> deliveries =
                new List<(Faction faction, Message message)>();

            AddArrivalMessages(resultArray.OfType<UnitArrivedResult>(), game, deliveries);
            AddMissionMessages(missionResults, killedResults, sabotageResults, game, deliveries);
            AddRecruitmentMessages(resultArray.OfType<RecruitmentExhaustedResult>(), deliveries);
            AddOfficerMessages(
                resultArray.OfType<OfficerRecruitedResult>(),
                resultArray.OfType<OfficerCaptureStateResult>(),
                resultArray.OfType<OfficerInjuredResult>(),
                killedResults,
                missionResults,
                game,
                deliveries
            );
            AddForceMessages(
                resultArray.OfType<ForceExperienceResult>(),
                forceDiscoveryResults,
                game,
                deliveries
            );
            AddNarrativeMessages(
                resultArray.OfType<DagobahCompletedResult>(),
                resultArray.OfType<HeritageRevealedResult>(),
                game,
                deliveries
            );
            AddSabotageMessages(sabotageResults, game, deliveries);
            AddResearchMessages(
                resultArray.OfType<ResearchOrderedResult>(),
                resultArray.OfType<ResearchExhaustedResult>(),
                deliveries
            );
            AddUprisingMessages(
                resultArray.OfType<PlanetNearUprisingResult>(),
                resultArray.OfType<PlanetUprisingStartedResult>(),
                resultArray.OfType<PlanetUprisingEndedResult>(),
                game,
                deliveries
            );
            AddPopularSupportOwnershipMessages(
                resultArray.OfType<PlanetOwnershipChangedResult>(),
                game,
                deliveries
            );
            AddBlockadeMessages(
                resultArray.OfType<BlockadeChangedResult>(),
                resultArray.OfType<EvacuationLossesResult>(),
                game,
                deliveries
            );
            AddMaintenanceMessages(
                resultArray.OfType<GameObjectAutoscrappedResult>(),
                game,
                deliveries
            );
            AddRepairMessages(
                resultArray.OfType<ShipHullDamageResult>(),
                resultArray.OfType<FighterDamageResult>(),
                game,
                deliveries
            );
            AddCombatMessages(
                resultArray.OfType<SpaceCombatResult>(),
                resultArray.OfType<BombardmentResult>(),
                resultArray.OfType<PlanetaryAssaultResult>(),
                game,
                deliveries
            );
            AddDeploymentMessages(resultArray.OfType<GameObjectDeployedResult>(), game, deliveries);
            AddManufacturingMessages(resultArray.OfType<ManufacturingIdleResult>(), deliveries);
            AddSeatOfPowerMessages(
                resultArray.OfType<SeatOfPowerChangedResult>(),
                game,
                deliveries
            );

            return deliveries;
        }

        /// <summary>
        /// Creates a fleet arrival message.
        /// </summary>
        /// <param name="faction">The faction that owns the arriving fleet.</param>
        /// <param name="fleet">The fleet that arrived.</param>
        /// <param name="destination">The planet where the fleet arrived.</param>
        /// <returns>The fleet arrival message, or null when no matching definition exists.</returns>
        private Message CreateFleetArrived(Faction faction, Fleet fleet, Planet destination)
        {
            return WithAdvisorNotification(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.FleetArrived),
                        faction,
                        new Dictionary<string, string>
                        {
                            { "fleet", fleet?.GetDisplayName() ?? string.Empty },
                            { "system", destination?.GetDisplayName() ?? string.Empty },
                        }
                    ),
                    destination,
                    fleet
                ),
                AdvisorNotificationCode.FleetArrived
            );
        }

        /// <summary>
        /// Creates a grouped capital ship arrival message.
        /// </summary>
        /// <param name="faction">The faction that owns the arriving ships.</param>
        /// <param name="ships">The ships that arrived together.</param>
        /// <param name="destination">The planet where the ships arrived.</param>
        /// <returns>The ship arrival message, or null when no matching definition exists.</returns>
        private Message CreateShipsArrived(
            Faction faction,
            IEnumerable<CapitalShip> ships,
            Planet destination
        )
        {
            CapitalShip[] shipArray =
                ships?.Where(ship => ship != null).ToArray() ?? Array.Empty<CapitalShip>();
            string shipList = string.Join("\n", shipArray.Select(ship => ship.GetDisplayName()));
            return WithAdvisorNotification(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.ShipsArrived),
                        faction,
                        new Dictionary<string, string>
                        {
                            { "ships", shipList },
                            { "system", destination?.GetDisplayName() ?? string.Empty },
                        }
                    ),
                    destination,
                    shipArray.FirstOrDefault()
                ),
                AdvisorNotificationCode.UnitsArrived
            );
        }

        /// <summary>
        /// Creates a grouped arrival report for personnel that traveled together.
        /// </summary>
        /// <param name="faction">The faction receiving the report.</param>
        /// <param name="personnel">The officers included in the arrival.</param>
        /// <param name="destination">The planet where the officers arrived.</param>
        /// <param name="game">The game state used for voice selection randomness.</param>
        /// <returns>The personnel arrival message, or null when no officers were provided.</returns>
        private Message CreatePersonnelArrived(
            Faction faction,
            IEnumerable<Officer> personnel,
            Planet destination,
            GameRoot game
        )
        {
            Officer[] officers =
                personnel?.Where(officer => officer != null).ToArray() ?? Array.Empty<Officer>();
            if (officers.Length == 0)
                return null;

            Officer reporter = officers.FirstOrDefault(officer =>
                officer.HasVoicePath(OfficerVoiceLineType.PersonnelArrived)
            );
            Officer[] listedPersonnel =
                reporter == null
                    ? officers
                    : officers.Where(officer => officer != reporter).ToArray();
            string personnelList = string.Join(
                "\n",
                listedPersonnel.Select(officer => officer.GetDisplayName())
            );
            MessageResultType resultType =
                reporter == null ? MessageResultType.PersonnelArrived
                : listedPersonnel.Length == 0 ? MessageResultType.PersonnelArrivedByOfficer
                : MessageResultType.PersonnelArrivedByOfficerWithCompany;
            Message message = WithEventLocation(
                CreateMessage(
                    GetDefinition(resultType),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "officer", reporter?.GetDisplayName() ?? string.Empty },
                        { "system", destination?.GetDisplayName() ?? string.Empty },
                        { "personnel", personnelList },
                    },
                    overlayImagePath: GetMessageImagePath(reporter ?? officers[0]),
                    officerVoicePath: reporter?.GetVoicePath(
                        OfficerVoiceLineType.PersonnelArrived,
                        game?.Random
                    )
                ),
                destination,
                reporter ?? officers[0]
            );

            return reporter == null
                ? WithAdvisorNotification(message, AdvisorNotificationCode.FieldPersonnel)
                : WithAdvisorSubject(message, AdvisorSubjectNotification.Report, reporter);
        }

        /// <summary>
        /// Creates the emperor seat-of-power message.
        /// </summary>
        /// <param name="faction">The faction that owns the emperor.</param>
        /// <param name="officer">The officer returning to the seat of power.</param>
        /// <param name="game">The game state used for voice selection randomness.</param>
        /// <returns>The seat-of-power message, or null when no matching definition exists.</returns>
        private Message CreateEmperorSeatOfPower(Faction faction, Officer officer, GameRoot game)
        {
            return WithAdvisorSubject(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.EmperorSeatOfPower),
                        faction,
                        new Dictionary<string, string>(),
                        overlayImagePath: GetMessageImagePath(officer),
                        officerVoicePath: officer?.GetVoicePath(
                            OfficerVoiceLineType.SeatOfPower,
                            game?.Random
                        )
                    ),
                    GetOfficerPlanet(officer),
                    officer
                ),
                AdvisorSubjectNotification.Report,
                officer
            );
        }

        /// <summary>
        /// Creates a facility deployment message.
        /// </summary>
        /// <param name="faction">The faction that owns the deployed building.</param>
        /// <param name="building">The deployed building.</param>
        /// <param name="destination">The planet where the building deployed.</param>
        /// <returns>The facility deployment message, or null when no matching definition exists.</returns>
        private Message CreateFacilityDeployed(
            Faction faction,
            Building building,
            Planet destination
        )
        {
            BuildingType buildingType = building?.BuildingType ?? BuildingType.None;
            if (buildingType == BuildingType.None)
                return null;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.FacilityDeployed, buildingType: buildingType),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "item", building?.GetDisplayName() ?? string.Empty },
                        { "system", destination?.GetDisplayName() ?? string.Empty },
                    },
                    imageOverride: GetMessageImagePath(building)
                ),
                destination,
                building
            );
        }

        /// <summary>
        /// Creates a manufacturing idle message.
        /// </summary>
        /// <param name="faction">The faction whose manufacturing queue became idle.</param>
        /// <param name="manufacturingType">The idle manufacturing queue type.</param>
        /// <param name="planet">The planet where the queue became idle.</param>
        /// <returns>The manufacturing idle message, or null when no matching definition exists.</returns>
        private Message CreateManufacturingIdle(
            Faction faction,
            ManufacturingType manufacturingType,
            Planet planet
        )
        {
            if (manufacturingType == ManufacturingType.None)
                return null;

            return WithAdvisorNotification(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(
                            MessageResultType.ManufacturingIdle,
                            manufacturingType: manufacturingType
                        ),
                        faction,
                        new Dictionary<string, string>
                        {
                            { "system", planet?.GetDisplayName() ?? string.Empty },
                        }
                    ),
                    planet
                ),
                AdvisorNotificationCode.Manufacturing
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
        /// <returns>The mission report message, or null when no matching definition exists.</returns>
        private Message CreateMissionReport(
            Faction faction,
            MissionCompletedResult result,
            Planet target,
            GameRoot game,
            HashSet<string> killedOfficerIDs,
            IEnumerable<OfficerKilledResult> killedResults,
            IEnumerable<GameObjectSabotagedResult> sabotageResults
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

            Message message = WithEventLocation(
                CreateMessage(
                    GetMissionDefinition(
                        MessageResultType.MissionReport,
                        outcome,
                        GetMissionTypeID(result),
                        completionReason
                    ),
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
                ? WithAdvisorNotification(message, AdvisorNotificationCode.FieldPersonnel)
                : WithAdvisorSubject(message, AdvisorSubjectNotification.Report, reporter);
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
            return WithAdvisorNotification(message, AdvisorNotificationCode.AgentReport);
        }

        /// <summary>
        /// Creates an officer status message.
        /// </summary>
        /// <param name="resultType">The message result type to use.</param>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="officer">The officer described by the message.</param>
        /// <param name="planet">The planet associated with the officer state.</param>
        /// <param name="game">The game state used for voice selection randomness.</param>
        /// <param name="includeOfficerOverlay">Whether to include the officer portrait overlay.</param>
        /// <returns>The officer status message, or null when no matching definition exists.</returns>
        private Message CreateOfficerMessage(
            MessageResultType resultType,
            Faction faction,
            Officer officer,
            Planet planet,
            GameRoot game,
            bool includeOfficerOverlay = true
        )
        {
            if (officer == null)
                return null;

            Message message = WithEventLocation(
                CreateMessage(
                    GetDefinition(resultType),
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
                    overlayImagePath: includeOfficerOverlay ? GetMessageImagePath(officer) : null,
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
        /// Creates the narrative report for completed Dagobah training.
        /// </summary>
        /// <param name="faction">The faction receiving the report.</param>
        /// <param name="result">The completed narrative result.</param>
        /// <param name="game">The game state used to select officer audio.</param>
        /// <returns>The completed narrative message, or null when no officer is present.</returns>
        private Message CreateDagobahCompleted(
            Faction faction,
            DagobahCompletedResult result,
            GameRoot game
        )
        {
            Officer officer = result?.Officer;
            if (officer == null)
                return null;

            return WithAdvisorSubject(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.DagobahCompleted),
                        faction,
                        new Dictionary<string, string>(),
                        overlayImagePath: GetMessageImagePath(officer),
                        officerVoicePath: officer.GetVoicePath(
                            OfficerVoiceLineType.DagobahCompleted,
                            game?.Random
                        )
                    ),
                    GetOfficerPlanet(officer),
                    officer
                ),
                AdvisorSubjectNotification.Report,
                officer
            );
        }

        /// <summary>
        /// Creates the narrative report for a revealed officer heritage.
        /// </summary>
        /// <param name="faction">The faction receiving the report.</param>
        /// <param name="result">The revealed heritage result.</param>
        /// <returns>The heritage message, or null when no officer is present.</returns>
        private Message CreateHeritageRevealed(Faction faction, HeritageRevealedResult result)
        {
            Officer officer = result?.Officer;
            if (officer == null)
                return null;

            return WithAdvisorSubject(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.HeritageRevealed),
                        faction,
                        new Dictionary<string, string>(),
                        overlayImagePath: GetMessageImagePath(officer)
                    ),
                    GetOfficerPlanet(officer),
                    officer
                ),
                AdvisorSubjectNotification.Report,
                officer
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

            bool abilityRevealed = result.Officer.HasVoicePath(
                OfficerVoiceLineType.ForceAbilityRevealed
            );
            bool canTrain = JediTrainingMission.CanLeadTraining(result.Discoverer, game);
            MessageResultType resultType =
                abilityRevealed ? MessageResultType.ForceAbilityRevealed
                : canTrain ? MessageResultType.ForceUserDiscovered
                : MessageResultType.ForceUserDiscoveredByStudent;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(resultType),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "officer", result.Officer.GetDisplayName() ?? string.Empty },
                    },
                    overlayImagePath: GetMessageImagePath(
                        abilityRevealed ? result.Officer : result.Discoverer
                    )
                ),
                GetOfficerPlanet(result.Officer),
                result.Officer
            );
        }

        /// <summary>
        /// Creates a capital ship repair message when hull damage has been fully repaired.
        /// </summary>
        /// <param name="faction">The faction that owns the capital ship.</param>
        /// <param name="result">The ship hull damage result.</param>
        /// <returns>The repair message, or null when the ship is still damaged or no definition exists.</returns>
        private Message CreateCapitalShipRepaired(Faction faction, ShipHullDamageResult result)
        {
            if (result?.Ship == null || result.Ship.IsDamaged())
                return null;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.CapitalShipRepaired),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "item", GetDisplayName(result.Ship) },
                        { "attachment", GetAttachmentName(result.Ship) },
                    }
                ),
                result.Ship.GetParentOfType<Planet>(),
                result.Ship
            );
        }

        /// <summary>
        /// Creates a starfighter repair message when a squadron has returned to full strength.
        /// </summary>
        /// <param name="faction">The faction that owns the starfighter squadron.</param>
        /// <param name="result">The fighter damage result.</param>
        /// <returns>The repair message, or null when the squadron still has losses or no definition exists.</returns>
        private Message CreateStarfighterRepaired(Faction faction, FighterDamageResult result)
        {
            if (result?.Fighter == null || result.Fighter.HasLosses())
                return null;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.StarfighterRepaired),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "item", GetDisplayName(result.Fighter) },
                        { "attachment", GetAttachmentName(result.Fighter) },
                    }
                ),
                result.Fighter.GetParentOfType<Planet>(),
                result.Fighter
            );
        }

        /// <summary>
        /// Creates a sabotage strike message for the owner of the destroyed object.
        /// </summary>
        /// <param name="faction">The faction that owned the sabotaged object.</param>
        /// <param name="result">The sabotage result.</param>
        /// <param name="target">The planet where sabotage occurred.</param>
        /// <returns>The sabotage strike message, or null when no matching definition exists.</returns>
        private Message CreateSabotageStrike(
            Faction faction,
            GameObjectSabotagedResult result,
            Planet target
        )
        {
            if (result == null)
                return null;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.SabotageStrike),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "item", GetDisplayName(result.SabotagedObject) },
                        { "system", target?.GetDisplayName() ?? string.Empty },
                    }
                ),
                target,
                result.SabotagedObject as ISceneNode
            );
        }

        /// <summary>
        /// Creates a research completion message.
        /// </summary>
        /// <param name="faction">The faction that completed research.</param>
        /// <param name="result">The completed research result.</param>
        /// <returns>The research completion message, or null when no matching definition exists.</returns>
        private Message CreateResearchComplete(Faction faction, ResearchOrderedResult result)
        {
            if (result?.Technology == null)
                return null;

            string item = GetDisplayName(result.Technology.GetReference());
            return CreateMessage(
                GetDefinition(MessageResultType.ResearchComplete, discipline: result.Discipline),
                faction,
                new Dictionary<string, string> { { "item", item } }
            );
        }

        /// <summary>
        /// Creates a research exhausted message.
        /// </summary>
        /// <param name="faction">The faction whose research discipline is exhausted.</param>
        /// <param name="result">The exhausted research result.</param>
        /// <returns>The research exhausted message, or null when no matching definition exists.</returns>
        private Message CreateResearchExhausted(Faction faction, ResearchExhaustedResult result)
        {
            if (result == null)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.ResearchExhausted, discipline: result.Discipline),
                faction,
                new Dictionary<string, string>()
            );
        }

        /// <summary>
        /// Creates an uprising started message.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="result">The uprising started result.</param>
        /// <param name="controller">The faction that controls the planet.</param>
        /// <returns>The uprising started message, or null when no matching definition exists.</returns>
        private Message CreateUprisingStarted(
            Faction faction,
            PlanetUprisingStartedResult result,
            Faction controller
        )
        {
            if (result == null)
                return null;

            Message message = WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.UprisingStarted),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "faction", controller?.GetDisplayName() ?? string.Empty },
                        { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                    }
                ),
                result.Planet
            );
            AdvisorNotificationCode notification =
                faction?.InstanceID == controller?.InstanceID
                    ? AdvisorNotificationCode.NegativePopularSupport
                    : AdvisorNotificationCode.PositivePopularSupport;
            return WithAdvisorNotification(message, notification);
        }

        /// <summary>
        /// Creates a warning that a controlled planet is close to uprising.
        /// </summary>
        /// <param name="faction">The faction controlling the affected planet.</param>
        /// <param name="result">The near-uprising result.</param>
        /// <returns>The warning message, or null when no matching definition exists.</returns>
        private Message CreateNearUprising(Faction faction, PlanetNearUprisingResult result)
        {
            if (result == null)
                return null;

            return WithAdvisorNotification(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.NearUprising),
                        faction,
                        new Dictionary<string, string>
                        {
                            { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                        }
                    ),
                    result.Planet
                ),
                AdvisorNotificationCode.NegativePopularSupport
            );
        }

        /// <summary>
        /// Creates an uprising ended message.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="result">The uprising ended result.</param>
        /// <param name="controller">The faction that controls the planet.</param>
        /// <returns>The uprising ended message, or null when no matching definition exists.</returns>
        private Message CreateUprisingEnded(
            Faction faction,
            PlanetUprisingEndedResult result,
            Faction controller
        )
        {
            if (result == null)
                return null;

            return WithAdvisorNotification(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.UprisingEnded),
                        faction,
                        new Dictionary<string, string>
                        {
                            { "faction", controller?.GetDisplayName() ?? string.Empty },
                            { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                        },
                        controller
                    ),
                    result.Planet
                ),
                AdvisorNotificationCode.PositivePopularSupport
            );
        }

        /// <summary>
        /// Creates a message for a planet joining a faction through popular support.
        /// </summary>
        /// <param name="result">The planet ownership change result.</param>
        /// <returns>The planet joined message, or null when no matching definition exists.</returns>
        private Message CreatePlanetJoinedBySupport(PlanetOwnershipChangedResult result)
        {
            if (result?.NewOwner == null)
                return null;

            return WithAdvisorNotification(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.PlanetJoinedBySupport),
                        result.NewOwner,
                        new Dictionary<string, string>
                        {
                            { "faction", result.NewOwner.GetDisplayName() ?? string.Empty },
                            { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                        }
                    ),
                    result.Planet
                ),
                AdvisorNotificationCode.PositivePopularSupport
            );
        }

        /// <summary>
        /// Creates a message for a planet joining an opposing faction through popular support.
        /// </summary>
        /// <param name="result">The planet ownership change result.</param>
        /// <param name="recipient">The faction that observed the ownership change.</param>
        /// <returns>The planet joined enemy message, or null when no matching definition exists.</returns>
        private Message CreatePlanetJoinedEnemyBySupport(
            PlanetOwnershipChangedResult result,
            Faction recipient
        )
        {
            if (result?.NewOwner == null || recipient == null)
                return null;

            if (recipient.InstanceID == result.NewOwner.InstanceID)
                return null;

            return WithAdvisorNotification(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.PlanetJoinedEnemyBySupport),
                        recipient,
                        new Dictionary<string, string>
                        {
                            { "faction", result.NewOwner.GetDisplayName() ?? string.Empty },
                            { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                        },
                        result.NewOwner
                    ),
                    result.Planet
                ),
                AdvisorNotificationCode.NegativePopularSupport
            );
        }

        /// <summary>
        /// Creates a message for a planet becoming neutral through popular support.
        /// </summary>
        /// <param name="result">The planet ownership change result.</param>
        /// <param name="recipient">The faction that observed the ownership change.</param>
        /// <returns>The planet neutrality message, or null when no matching definition exists.</returns>
        private Message CreatePlanetDeclaredNeutralityBySupport(
            PlanetOwnershipChangedResult result,
            Faction recipient
        )
        {
            if (result?.PreviousOwner == null || result.NewOwner != null || recipient == null)
                return null;

            return WithAdvisorNotification(
                WithEventLocation(
                    CreateMessage(
                        GetDefinition(MessageResultType.PlanetDeclaredNeutralityBySupport),
                        recipient,
                        new Dictionary<string, string>
                        {
                            { "faction", result.PreviousOwner.GetDisplayName() ?? string.Empty },
                            { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                        }
                    ),
                    result.Planet
                ),
                AdvisorNotificationCode.NegativePopularSupport
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
        /// Creates the blockading faction's blockade started message.
        /// </summary>
        /// <param name="faction">The faction that started the blockade.</param>
        /// <param name="result">The blockade changed result.</param>
        /// <param name="targetFaction">The faction that owns the blockaded planet.</param>
        /// <returns>The blockade initiated message, or null when no matching definition exists.</returns>
        private Message CreateBlockadeInitiated(
            Faction faction,
            BlockadeChangedResult result,
            Faction targetFaction
        )
        {
            if (result?.Blockaded != true)
                return null;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.BlockadeInitiated),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "faction", faction?.GetDisplayName() ?? string.Empty },
                        { "target", targetFaction?.GetDisplayName() ?? string.Empty },
                        { "fleet", result.BlockadingFleet?.GetDisplayName() ?? string.Empty },
                        { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                    },
                    targetFaction
                ),
                result.Planet,
                result.BlockadingFleet
            );
        }

        /// <summary>
        /// Creates the target faction's blockade detected message.
        /// </summary>
        /// <param name="faction">The faction that owns the blockaded planet.</param>
        /// <param name="result">The blockade changed result.</param>
        /// <param name="blockadingFaction">The faction that started the blockade.</param>
        /// <returns>The blockade detected message, or null when no matching definition exists.</returns>
        private Message CreateBlockadeDetected(
            Faction faction,
            BlockadeChangedResult result,
            Faction blockadingFaction
        )
        {
            if (result?.Blockaded != true)
                return null;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.BlockadeDetected),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "faction", blockadingFaction?.GetDisplayName() ?? string.Empty },
                        { "fleet", result.BlockadingFleet?.GetDisplayName() ?? string.Empty },
                        { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                    }
                ),
                result.Planet,
                result.BlockadingFleet
            );
        }

        /// <summary>
        /// Creates an evacuation losses message.
        /// </summary>
        /// <param name="faction">The faction that lost units during evacuation.</param>
        /// <param name="result">The evacuation losses result.</param>
        /// <returns>The evacuation losses message, or null when no matching definition exists.</returns>
        private Message CreateEvacuationLosses(Faction faction, EvacuationLossesResult result)
        {
            if (result == null)
                return null;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.EvacuationLosses),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "system", result.Location?.GetDisplayName() ?? string.Empty },
                        { "units", FormatLostUnits(result) },
                    }
                ),
                result.Location
            );
        }

        /// <summary>
        /// Creates an autoscrap message for maintenance losses.
        /// </summary>
        /// <param name="faction">The faction that owned the destroyed object.</param>
        /// <param name="result">The autoscrap result.</param>
        /// <param name="location">The planet where the object was destroyed.</param>
        /// <returns>The autoscrap message, or null when no matching definition exists.</returns>
        private Message CreateMaintenanceAutoscrap(
            Faction faction,
            GameObjectAutoscrappedResult result,
            Planet location
        )
        {
            if (result == null)
                return null;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.MaintenanceAutoscrap),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "item", GetDisplayName(result.DestroyedObject) },
                        { "system", location?.GetDisplayName() ?? string.Empty },
                    }
                ),
                location,
                result.DestroyedObject as ISceneNode
            );
        }

        /// <summary>
        /// Creates a space battle result message for one faction.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="result">The space combat result.</param>
        /// <param name="opponent">The opposing faction.</param>
        /// <returns>The space battle message, or null when no matching definition exists.</returns>
        private Message CreateSpaceBattle(
            Faction faction,
            SpaceCombatResult result,
            Faction opponent
        )
        {
            if (result == null)
                return null;

            MessageResultOutcome outcome = GetSpaceBattleOutcome(faction, result);
            if (outcome == MessageResultOutcome.None)
                return null;

            return WithEventLocation(
                CreateMessage(
                    GetDefinition(MessageResultType.SpaceBattle, outcome),
                    faction,
                    new Dictionary<string, string>
                    {
                        { "faction", faction?.GetDisplayName() ?? string.Empty },
                        { "opponent", opponent?.GetDisplayName() ?? string.Empty },
                        { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                    }
                ),
                result.Planet,
                GetSpaceBattleFleet(faction, result)
            );
        }

        /// <summary>
        /// Creates a bombardment result message for one faction.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="result">The bombardment result.</param>
        /// <param name="targetFaction">The faction that owned the target planet.</param>
        /// <returns>The bombardment message, or null when no matching definition exists.</returns>
        private Message CreateBombardment(
            Faction faction,
            BombardmentResult result,
            Faction targetFaction
        )
        {
            if (result == null)
                return null;

            Message message = CreateMessage(
                GetDefinition(
                    MessageResultType.Bombardment,
                    GetBombardmentOutcome(result),
                    GetBombardmentPlanetOwnership(result)
                ),
                faction,
                new Dictionary<string, string>
                {
                    { "faction", result.AttackingFaction?.GetDisplayName() ?? string.Empty },
                    { "target", targetFaction?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                }
            );
            AdvisorNotificationCode notification =
                faction?.InstanceID == result.AttackingFaction?.InstanceID
                    ? AdvisorNotificationCode.None
                    : AdvisorNotificationCode.Bombardment;

            return WithEventLocation(WithAdvisorNotification(message, notification), result.Planet);
        }

        /// <summary>
        /// Creates a planetary assault result message for one faction.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="result">The planetary assault result.</param>
        /// <param name="targetFaction">The faction that owned the assaulted planet.</param>
        /// <returns>The planetary assault message, or null when no matching definition exists.</returns>
        private Message CreatePlanetaryAssault(
            Faction faction,
            PlanetaryAssaultResult result,
            Faction targetFaction
        )
        {
            if (result == null)
                return null;

            Message message = CreateMessage(
                GetDefinition(
                    MessageResultType.PlanetaryAssault,
                    result.Success ? MessageResultOutcome.Success : MessageResultOutcome.Failed,
                    GetAssaultPlanetOwnership(result)
                ),
                faction,
                new Dictionary<string, string>
                {
                    { "faction", result.AttackingFaction?.GetDisplayName() ?? string.Empty },
                    { "target", targetFaction?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                },
                result.AttackingFaction
            );
            AdvisorNotificationCode notification =
                faction?.InstanceID == result.AttackingFaction?.InstanceID
                    ? AdvisorNotificationCode.None
                    : AdvisorNotificationCode.PlanetaryAssault;

            return WithEventLocation(WithAdvisorNotification(message, notification), result.Planet);
        }

        /// <summary>
        /// Adds messages for arriving fleets, ships, personnel, and buildings.
        /// </summary>
        /// <param name="arrivals">The arrival results to process.</param>
        /// <param name="game">The game state used to resolve owning factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddArrivalMessages(
            IEnumerable<UnitArrivedResult> arrivals,
            GameRoot game,
            List<(Faction faction, Message message)> deliveries
        )
        {
            var shipGroups =
                new Dictionary<
                    (string OwnerInstanceID, string DestinationInstanceID, string MovementGroupID),
                    List<CapitalShip>
                >();
            var shipDestinations =
                new Dictionary<
                    (string OwnerInstanceID, string DestinationInstanceID, string MovementGroupID),
                    Planet
                >();
            var personnelGroups =
                new Dictionary<
                    (string OwnerInstanceID, string DestinationInstanceID, string MovementGroupID),
                    List<Officer>
                >();
            var personnelDestinations =
                new Dictionary<
                    (string OwnerInstanceID, string DestinationInstanceID, string MovementGroupID),
                    Planet
                >();

            foreach (UnitArrivedResult arrival in arrivals)
            {
                if (arrival.Unit is Fleet fleet)
                {
                    Faction faction = GetFaction(game, fleet.GetOwnerInstanceID());
                    AddDelivery(
                        deliveries,
                        faction,
                        CreateFleetArrived(faction, fleet, arrival.Destination)
                    );
                    continue;
                }

                if (arrival.Unit is CapitalShip ship)
                {
                    string movementGroupID = string.IsNullOrEmpty(arrival.MovementGroupID)
                        ? ship.GetInstanceID()
                        : arrival.MovementGroupID;
                    var key = (
                        ship.GetOwnerInstanceID(),
                        arrival.Destination?.GetInstanceID(),
                        movementGroupID
                    );
                    if (!shipGroups.TryGetValue(key, out List<CapitalShip> ships))
                    {
                        ships = new List<CapitalShip>();
                        shipGroups[key] = ships;
                        shipDestinations[key] = arrival.Destination;
                    }

                    ships.Add(ship);
                    continue;
                }

                if (arrival.Unit is Officer officer)
                {
                    string movementGroupID = string.IsNullOrEmpty(arrival.MovementGroupID)
                        ? officer.GetInstanceID()
                        : arrival.MovementGroupID;
                    var key = (
                        officer.GetOwnerInstanceID(),
                        arrival.Destination?.GetInstanceID(),
                        movementGroupID
                    );
                    if (!personnelGroups.TryGetValue(key, out List<Officer> personnel))
                    {
                        personnel = new List<Officer>();
                        personnelGroups[key] = personnel;
                        personnelDestinations[key] = arrival.Destination;
                    }

                    personnel.Add(officer);
                    continue;
                }

                if (arrival.Unit is Building building)
                {
                    Faction faction = GetFaction(game, building.GetOwnerInstanceID());
                    AddDelivery(
                        deliveries,
                        faction,
                        CreateFacilityDeployed(faction, building, arrival.Destination)
                    );
                }
            }

            foreach (var group in shipGroups)
            {
                Faction faction = GetFaction(game, group.Key.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    faction,
                    CreateShipsArrived(faction, group.Value, shipDestinations[group.Key])
                );
            }

            foreach (var group in personnelGroups)
            {
                Faction faction = GetFaction(game, group.Key.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    faction,
                    CreatePersonnelArrived(
                        faction,
                        group.Value,
                        personnelDestinations[group.Key],
                        game
                    )
                );
            }
        }

        /// <summary>
        /// Adds messages for completed missions.
        /// </summary>
        /// <param name="results">The completed mission results to process.</param>
        /// <param name="killedResults">The officer death results in the current batch.</param>
        /// <param name="sabotageResults">The sabotage results in the current batch.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddMissionMessages(
            IEnumerable<MissionCompletedResult> results,
            IEnumerable<OfficerKilledResult> killedResults,
            IEnumerable<GameObjectSabotagedResult> sabotageResults,
            GameRoot game,
            List<(Faction faction, Message message)> deliveries
        )
        {
            OfficerKilledResult[] killedArray =
                killedResults?.ToArray() ?? Array.Empty<OfficerKilledResult>();
            HashSet<string> killedOfficerIDs = killedArray
                .Select(result => result.TargetOfficer?.InstanceID)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();

            foreach (MissionCompletedResult result in results)
            {
                Planet target = GetMissionTarget(result);
                Faction actorFaction = GetFaction(game, result.Mission?.OwnerInstanceID);
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
                        sabotageResults
                    )
                );

                Faction targetFaction = GetFaction(game, target?.OwnerInstanceID);
                if (targetFaction?.InstanceID == actorFaction?.InstanceID)
                    continue;

                AddDelivery(
                    deliveries,
                    targetFaction,
                    CreateEnemyMissionFoiled(targetFaction, result, target)
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
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (RecruitmentExhaustedResult result in results)
                AddDelivery(deliveries, result.Faction, CreateRecruitmentExhausted(result));
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
            List<(Faction faction, Message message)> deliveries
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
                    )
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
                    )
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
                    )
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
                    )
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
                        MessageResultType.OfficerKilled,
                        faction,
                        result.TargetOfficer,
                        planet,
                        game,
                        false
                    )
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
            List<(Faction faction, Message message)> deliveries
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
                AddDelivery(deliveries, faction, CreateForceUserDiscovered(faction, result, game));
            }

            foreach (ForceExperienceResult result in experienceResults)
            {
                if (discoveredOfficerIDs.Contains(result.Officer?.InstanceID ?? string.Empty))
                    continue;

                if (!ShouldCreateForceGrowthMessage(result, game))
                    continue;

                Faction faction = GetOwnerFaction(game, result.Officer);
                AddDelivery(deliveries, faction, CreateForceGrowth(faction, result, game));
            }
        }

        /// <summary>
        /// Adds narrative event reports to the pending message deliveries.
        /// </summary>
        /// <param name="dagobahResults">The completed Dagobah narrative results.</param>
        /// <param name="heritageResults">The revealed heritage results.</param>
        /// <param name="game">The game state used to resolve message recipients.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddNarrativeMessages(
            IEnumerable<DagobahCompletedResult> dagobahResults,
            IEnumerable<HeritageRevealedResult> heritageResults,
            GameRoot game,
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (DagobahCompletedResult result in dagobahResults)
            {
                Faction faction = GetOwnerFaction(game, result.Officer);
                AddDelivery(deliveries, faction, CreateDagobahCompleted(faction, result, game));
            }

            foreach (HeritageRevealedResult result in heritageResults)
            {
                Faction faction = GetOwnerFaction(game, result.Officer);
                AddDelivery(deliveries, faction, CreateHeritageRevealed(faction, result));
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
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (GameObjectSabotagedResult result in results)
            {
                Planet target = GetSabotageTarget(result);
                string ownerInstanceID = GetOwnerInstanceID(result.SabotagedObject);
                if (string.IsNullOrEmpty(ownerInstanceID))
                    ownerInstanceID = target?.OwnerInstanceID;

                Faction faction = GetFaction(game, ownerInstanceID);
                AddDelivery(deliveries, faction, CreateSabotageStrike(faction, result, target));
            }
        }

        /// <summary>
        /// Adds messages for completed and exhausted research.
        /// </summary>
        /// <param name="orderedResults">The completed research results to process.</param>
        /// <param name="exhaustedResults">The exhausted research results to process.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddResearchMessages(
            IEnumerable<ResearchOrderedResult> orderedResults,
            IEnumerable<ResearchExhaustedResult> exhaustedResults,
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (ResearchOrderedResult result in orderedResults)
                AddDelivery(
                    deliveries,
                    result.Faction,
                    CreateResearchComplete(result.Faction, result)
                );

            foreach (ResearchExhaustedResult result in exhaustedResults)
                AddDelivery(
                    deliveries,
                    result.Faction,
                    CreateResearchExhausted(result.Faction, result)
                );
        }

        /// <summary>
        /// Adds messages for uprising start and end results.
        /// </summary>
        /// <param name="nearResults">The near-uprising results to process.</param>
        /// <param name="startedResults">The uprising started results to process.</param>
        /// <param name="endedResults">The uprising ended results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddUprisingMessages(
            IEnumerable<PlanetNearUprisingResult> nearResults,
            IEnumerable<PlanetUprisingStartedResult> startedResults,
            IEnumerable<PlanetUprisingEndedResult> endedResults,
            GameRoot game,
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (PlanetNearUprisingResult result in nearResults)
            {
                Faction controller = GetFaction(game, result.Planet?.OwnerInstanceID);
                AddDelivery(deliveries, controller, CreateNearUprising(controller, result));
            }

            foreach (PlanetUprisingStartedResult result in startedResults)
            {
                Faction controller = GetFaction(game, result.Planet?.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    controller,
                    CreateUprisingStarted(controller, result, controller)
                );

                if (result.InstigatorFaction?.InstanceID == controller?.InstanceID)
                    continue;

                AddDelivery(
                    deliveries,
                    result.InstigatorFaction,
                    CreateUprisingStarted(result.InstigatorFaction, result, controller)
                );
            }

            foreach (PlanetUprisingEndedResult result in endedResults)
            {
                Faction controller =
                    GetFaction(game, result.Planet?.OwnerInstanceID) ?? result.Faction;
                AddDelivery(
                    deliveries,
                    controller,
                    CreateUprisingEnded(controller, result, controller)
                );
            }
        }

        /// <summary>
        /// Adds messages for popular-support ownership changes.
        /// </summary>
        /// <param name="results">The planet ownership change results to process.</param>
        /// <param name="game">The game state used to resolve observing factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddPopularSupportOwnershipMessages(
            IEnumerable<PlanetOwnershipChangedResult> results,
            GameRoot game,
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (PlanetOwnershipChangedResult result in results)
            {
                if (result.Reason != PlanetOwnershipChangeReason.PopularSupport)
                    continue;

                foreach (Faction recipient in GetOwnershipChangeRecipients(result, game))
                {
                    Message message =
                        recipient == result.NewOwner ? CreatePlanetJoinedBySupport(result)
                        : result.NewOwner != null
                            ? CreatePlanetJoinedEnemyBySupport(result, recipient)
                        : CreatePlanetDeclaredNeutralityBySupport(result, recipient);
                    AddDelivery(deliveries, recipient, message);
                }
            }
        }

        /// <summary>
        /// Resolves every faction entitled to observe a planet ownership change.
        /// </summary>
        /// <param name="result">The ownership-change result.</param>
        /// <param name="game">The game state containing candidate factions.</param>
        /// <returns>The observing factions, including the previous and new owners.</returns>
        private static IEnumerable<Faction> GetOwnershipChangeRecipients(
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

        /// <summary>
        /// Adds messages for blockades and evacuation losses.
        /// </summary>
        /// <param name="blockadeResults">The blockade changed results to process.</param>
        /// <param name="evacuationResults">The evacuation loss results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddBlockadeMessages(
            IEnumerable<BlockadeChangedResult> blockadeResults,
            IEnumerable<EvacuationLossesResult> evacuationResults,
            GameRoot game,
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (BlockadeChangedResult result in blockadeResults)
            {
                if (!result.Blockaded)
                    continue;

                Faction blockadingFaction = GetFaction(
                    game,
                    result.BlockadingFleet?.GetOwnerInstanceID()
                );
                Faction targetFaction = GetFaction(game, result.Planet?.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    blockadingFaction,
                    CreateBlockadeInitiated(blockadingFaction, result, targetFaction)
                );

                if (targetFaction?.InstanceID == blockadingFaction?.InstanceID)
                    continue;

                AddDelivery(
                    deliveries,
                    targetFaction,
                    CreateBlockadeDetected(targetFaction, result, blockadingFaction)
                );
            }

            foreach (EvacuationLossesResult result in evacuationResults)
                AddDelivery(
                    deliveries,
                    result.Faction,
                    CreateEvacuationLosses(result.Faction, result)
                );
        }

        /// <summary>
        /// Adds messages for autoscrapped objects.
        /// </summary>
        /// <param name="autoscrapResults">The autoscrap results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddMaintenanceMessages(
            IEnumerable<GameObjectAutoscrappedResult> autoscrapResults,
            GameRoot game,
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (GameObjectAutoscrappedResult result in autoscrapResults)
            {
                Planet location = GetResultPlanet(
                    result.Context ?? result.Ref ?? result.DestroyedObject
                );
                Faction faction =
                    GetOwnerFaction(game, result.DestroyedObject)
                    ?? GetOwnerFaction(game, result.Ref)
                    ?? GetFaction(game, location?.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    faction,
                    CreateMaintenanceAutoscrap(faction, result, location)
                );
            }
        }

        /// <summary>
        /// Adds messages for completed capital ship and starfighter repairs.
        /// </summary>
        /// <param name="shipResults">The capital ship hull damage results to process.</param>
        /// <param name="fighterResults">The starfighter damage results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddRepairMessages(
            IEnumerable<ShipHullDamageResult> shipResults,
            IEnumerable<FighterDamageResult> fighterResults,
            GameRoot game,
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (ShipHullDamageResult result in shipResults)
            {
                Faction faction = GetOwnerFaction(game, result.Ship);
                AddDelivery(deliveries, faction, CreateCapitalShipRepaired(faction, result));
            }

            foreach (FighterDamageResult result in fighterResults)
            {
                Faction faction = GetOwnerFaction(game, result.Fighter);
                AddDelivery(deliveries, faction, CreateStarfighterRepaired(faction, result));
            }
        }

        /// <summary>
        /// Adds messages for space combat, bombardment, and planetary assault results.
        /// </summary>
        /// <param name="battleResults">The space combat results to process.</param>
        /// <param name="bombardmentResults">The bombardment results to process.</param>
        /// <param name="assaultResults">The planetary assault results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddCombatMessages(
            IEnumerable<SpaceCombatResult> battleResults,
            IEnumerable<BombardmentResult> bombardmentResults,
            IEnumerable<PlanetaryAssaultResult> assaultResults,
            GameRoot game,
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (SpaceCombatResult result in battleResults)
            {
                Faction attacker = GetFaction(
                    game,
                    GetSpaceCombatOwnerInstanceID(result, CombatSide.Attacker)
                );
                Faction defender = GetFaction(
                    game,
                    GetSpaceCombatOwnerInstanceID(result, CombatSide.Defender)
                );
                AddDelivery(deliveries, attacker, CreateSpaceBattle(attacker, result, defender));
                if (defender?.InstanceID != attacker?.InstanceID)
                    AddDelivery(
                        deliveries,
                        defender,
                        CreateSpaceBattle(defender, result, attacker)
                    );
            }

            foreach (BombardmentResult result in bombardmentResults)
            {
                if (result?.AttackingFaction == null || result.Planet == null)
                    continue;

                Faction defender =
                    result.OwnershipChange?.PreviousOwner
                    ?? GetFaction(game, result.Planet?.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    result.AttackingFaction,
                    CreateBombardment(result.AttackingFaction, result, defender)
                );
                if (defender?.InstanceID != result.AttackingFaction?.InstanceID)
                    AddDelivery(
                        deliveries,
                        defender,
                        CreateBombardment(defender, result, defender)
                    );
            }

            foreach (PlanetaryAssaultResult result in assaultResults)
            {
                if (result?.AttackingFaction == null || result.Planet == null)
                    continue;

                Faction defender =
                    result.OwnershipChange?.PreviousOwner
                    ?? GetFaction(game, result.Planet?.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    result.AttackingFaction,
                    CreatePlanetaryAssault(result.AttackingFaction, result, defender)
                );
                if (defender?.InstanceID != result.AttackingFaction?.InstanceID)
                    AddDelivery(
                        deliveries,
                        defender,
                        CreatePlanetaryAssault(defender, result, defender)
                    );
            }
        }

        /// <summary>
        /// Adds messages for deployed buildings.
        /// </summary>
        /// <param name="results">The deployment results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddDeploymentMessages(
            IEnumerable<GameObjectDeployedResult> results,
            GameRoot game,
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (GameObjectDeployedResult result in results)
            {
                if (result.GameObject is not Building building || building.Movement != null)
                    continue;

                Faction faction = GetFaction(game, building.GetOwnerInstanceID());
                AddDelivery(
                    deliveries,
                    faction,
                    CreateFacilityDeployed(faction, building, building.GetParentOfType<Planet>())
                );
            }
        }

        /// <summary>
        /// Adds messages for idle manufacturing queues.
        /// </summary>
        /// <param name="results">The manufacturing idle results to process.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
        private void AddManufacturingMessages(
            IEnumerable<ManufacturingIdleResult> results,
            List<(Faction faction, Message message)> deliveries
        )
        {
            foreach (ManufacturingIdleResult result in results)
                AddDelivery(
                    deliveries,
                    result.Faction,
                    CreateManufacturingIdle(
                        result.Faction,
                        result.ManufacturingType,
                        result.ProductionPlanet
                    )
                );
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
            List<(Faction faction, Message message)> deliveries
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
                    CreateEmperorSeatOfPower(faction, result.Officer, game)
                );
            }
        }

        /// <summary>
        /// Adds a non-null message delivery for a non-null faction.
        /// </summary>
        /// <param name="deliveries">The delivery list to append to.</param>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="message">The message to deliver.</param>
        private static void AddDelivery(
            List<(Faction faction, Message message)> deliveries,
            Faction faction,
            Message message
        )
        {
            if (faction == null || message == null)
                return;

            deliveries.Add((faction, message));
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
                GetDefaultAdvisorNotification(definition?.ResultType)
            );
        }

        /// <summary>
        /// Resolves the default advisor notification for a message result type.
        /// </summary>
        /// <param name="resultType">The message result type, or null.</param>
        /// <returns>The matching advisor notification code.</returns>
        private static AdvisorNotificationCode GetDefaultAdvisorNotification(
            MessageResultType? resultType
        )
        {
            return resultType switch
            {
                MessageResultType.FleetArrived => AdvisorNotificationCode.FleetArrived,
                MessageResultType.ShipsArrived => AdvisorNotificationCode.UnitsArrived,
                MessageResultType.ManufacturingIdle => AdvisorNotificationCode.Manufacturing,
                MessageResultType.CapitalShipRepaired =>
                    AdvisorNotificationCode.CapitalShipRepaired,
                MessageResultType.StarfighterRepaired =>
                    AdvisorNotificationCode.StarfighterRepaired,
                MessageResultType.SabotageStrike => AdvisorNotificationCode.Maintenance,
                MessageResultType.ResearchComplete => AdvisorNotificationCode.Research,
                MessageResultType.ResearchExhausted => AdvisorNotificationCode.Research,
                MessageResultType.BlockadeInitiated => AdvisorNotificationCode.BlockadeInitiated,
                MessageResultType.BlockadeDetected => AdvisorNotificationCode.BlockadeDetected,
                MessageResultType.MaintenanceAutoscrap => AdvisorNotificationCode.Maintenance,
                MessageResultType.Bombardment => AdvisorNotificationCode.Bombardment,
                MessageResultType.PlanetaryAssault => AdvisorNotificationCode.PlanetaryAssault,
                _ => AdvisorNotificationCode.None,
            };
        }

        /// <summary>
        /// Assigns an advisor notification code to a message.
        /// </summary>
        /// <param name="message">The message to update.</param>
        /// <param name="notification">The advisor notification code.</param>
        /// <returns>The updated message, or null when no message was supplied.</returns>
        private static Message WithAdvisorNotification(
            Message message,
            AdvisorNotificationCode notification
        )
        {
            if (message != null)
                message.AdvisorNotificationCode = (int)notification;

            return message;
        }

        /// <summary>
        /// Assigns an officer-specific advisor notification to a message.
        /// </summary>
        /// <param name="message">The message to update.</param>
        /// <param name="notification">The officer notification kind.</param>
        /// <param name="officer">The officer represented by the notification.</param>
        /// <returns>The updated message.</returns>
        private static Message WithAdvisorSubject(
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

            message.AdvisorSubjectNotification = notification;
            message.AdvisorSubjectTypeID = officer.TypeID;
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
        /// <returns>The matching message definition, or null when none exists.</returns>
        private MessageDefinition GetDefinition(
            MessageResultType resultType,
            MessageResultOutcome outcome = MessageResultOutcome.None,
            MessagePlanetOwnership planetOwnership = MessagePlanetOwnership.None,
            BuildingType buildingType = BuildingType.None,
            ManufacturingType manufacturingType = ManufacturingType.None,
            ResearchDiscipline? discipline = null
        )
        {
            return _definitions.FirstOrDefault(definition =>
                definition.ResultType == resultType
                && definition.Outcome == outcome
                && definition.PlanetOwnership == planetOwnership
                && definition.BuildingType == buildingType
                && definition.ManufacturingType == manufacturingType
                && string.IsNullOrEmpty(definition.MissionTypeID)
                && definition.MissionCompletionReason == MissionCompletionReason.None
                && (!discipline.HasValue || definition.ResearchDiscipline == discipline.Value)
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

            if (!canUseGenericDefinition)
                return null;

            return FindMissionDefinition(resultType, outcome, null, MissionCompletionReason.None);
        }

        /// <summary>
        /// Finds an exact mission message definition match.
        /// </summary>
        /// <param name="resultType">The message result type to match.</param>
        /// <param name="outcome">The mission outcome selector to match.</param>
        /// <param name="missionTypeID">The mission type selector to match.</param>
        /// <param name="completionReason">The completion reason selector to match.</param>
        /// <returns>The matching mission definition, or null when none exists.</returns>
        private MessageDefinition FindMissionDefinition(
            MessageResultType resultType,
            MessageResultOutcome outcome,
            string missionTypeID,
            MissionCompletionReason completionReason
        )
        {
            return _definitions.FirstOrDefault(candidate =>
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
        }

        /// <summary>
        /// Checks whether a completion reason can fall back to a generic mission definition.
        /// </summary>
        /// <param name="completionReason">The completion reason to evaluate.</param>
        /// <returns>True when a generic definition can be used; otherwise false.</returns>
        private static bool CanUseGenericMissionDefinition(MissionCompletionReason completionReason)
        {
            return completionReason
                is MissionCompletionReason.None
                    or MissionCompletionReason.Success
                    or MissionCompletionReason.Failure
                    or MissionCompletionReason.Foiled
                    or MissionCompletionReason.ResearchBreakthrough;
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
        /// Gets the display name of an entity's immediate scene parent.
        /// </summary>
        /// <param name="entity">The entity whose parent should be described.</param>
        /// <returns>The parent display name, or an empty string when none is available.</returns>
        private static string GetAttachmentName(IGameEntity entity)
        {
            if (entity is not ISceneNode sceneNode)
                return string.Empty;

            return sceneNode.GetParent()?.GetDisplayName() ?? string.Empty;
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

            if (forceRank >= config.RankLabelForceMaster)
                return "Jedi Master";
            if (forceRank >= config.RankLabelForceKnight)
                return "Jedi Knight";
            if (forceRank >= config.RankLabelForceStudent)
                return "Jedi Student";
            if (forceRank >= config.RankLabelTrainee)
                return "Trainee";
            if (forceRank >= config.RankLabelNovice)
                return "Novice";
            return "None";
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
            if (result?.Officer == null)
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
        /// Formats evacuation losses as a newline-separated unit list.
        /// </summary>
        /// <param name="result">The evacuation losses result.</param>
        /// <returns>The formatted unit list.</returns>
        private static string FormatLostUnits(EvacuationLossesResult result)
        {
            IEnumerable<IGameEntity> units = result
                .LostShips.Cast<IGameEntity>()
                .Concat(result.LostStarfighters)
                .Concat(result.LostRegiments);

            return string.Join("\n", units.Select(GetDisplayName).Where(name => name.Length > 0));
        }

        /// <summary>
        /// Gets the message outcome for a space battle from one faction's perspective.
        /// </summary>
        /// <param name="faction">The faction whose perspective should be evaluated.</param>
        /// <param name="result">The space combat result.</param>
        /// <returns>The message outcome for the faction.</returns>
        private static MessageResultOutcome GetSpaceBattleOutcome(
            Faction faction,
            SpaceCombatResult result
        )
        {
            if (result.Winner == CombatSide.Draw)
                return MessageResultOutcome.Stalemate;

            if (faction?.InstanceID == GetSpaceCombatOwnerInstanceID(result, CombatSide.Attacker))
                return result.Winner == CombatSide.Attacker
                    ? MessageResultOutcome.Victory
                    : MessageResultOutcome.Defeat;

            if (faction?.InstanceID == GetSpaceCombatOwnerInstanceID(result, CombatSide.Defender))
                return result.Winner == CombatSide.Defender
                    ? MessageResultOutcome.Victory
                    : MessageResultOutcome.Defeat;

            return MessageResultOutcome.None;
        }

        /// <summary>
        /// Resolves the fleet belonging to one faction in a space battle result.
        /// </summary>
        /// <param name="faction">The faction whose fleet should be returned.</param>
        /// <param name="result">The space battle result.</param>
        /// <returns>The faction's participating fleet, or null.</returns>
        private static Fleet GetSpaceBattleFleet(Faction faction, SpaceCombatResult result)
        {
            if (faction?.InstanceID == GetSpaceCombatOwnerInstanceID(result, CombatSide.Attacker))
                return result.AttackerFleet;

            return faction?.InstanceID == GetSpaceCombatOwnerInstanceID(result, CombatSide.Defender)
                ? result.DefenderFleet
                : null;
        }

        /// <summary>
        /// Returns the owner identifier captured for one completed space-combat side.
        /// </summary>
        /// <param name="result">The completed space-combat result.</param>
        /// <param name="side">The represented combat side.</param>
        /// <returns>The captured owner identifier, with the participating fleet as fallback.</returns>
        private static string GetSpaceCombatOwnerInstanceID(
            SpaceCombatResult result,
            CombatSide side
        )
        {
            return side switch
            {
                CombatSide.Attacker => string.IsNullOrEmpty(result?.AttackerOwnerInstanceID)
                    ? result?.AttackerFleet?.GetOwnerInstanceID()
                    : result.AttackerOwnerInstanceID,
                CombatSide.Defender => string.IsNullOrEmpty(result?.DefenderOwnerInstanceID)
                    ? result?.DefenderFleet?.GetOwnerInstanceID()
                    : result.DefenderOwnerInstanceID,
                _ => null,
            };
        }

        /// <summary>
        /// Gets the message outcome for a bombardment result.
        /// </summary>
        /// <param name="result">The bombardment result.</param>
        /// <returns>The bombardment message outcome.</returns>
        private static MessageResultOutcome GetBombardmentOutcome(BombardmentResult result)
        {
            if (HasBombardmentTargetLosses(result))
                return MessageResultOutcome.TargetLosses;

            if (HasBombardmentAttackerLosses(result))
                return MessageResultOutcome.AttackerLosses;

            return MessageResultOutcome.NoLosses;
        }

        /// <summary>
        /// Checks whether bombardment destroyed target-side assets.
        /// </summary>
        /// <param name="result">The bombardment result.</param>
        /// <returns>True when target-side assets were destroyed; otherwise false.</returns>
        private static bool HasBombardmentTargetLosses(BombardmentResult result)
        {
            return result.PlanetDestroyed
                || result.HeadquartersDestroyed
                || result.EnergyCapacityDamage > 0
                || result.AllocatedEnergyDamage > 0
                || result.DestroyedBuildings.Any()
                || result.DestroyedRegiments.Any();
        }

        /// <summary>
        /// Checks whether bombardment destroyed attacker-side regiments.
        /// </summary>
        /// <param name="result">The bombardment result.</param>
        /// <returns>True when attacker-side regiments were destroyed; otherwise false.</returns>
        private static bool HasBombardmentAttackerLosses(BombardmentResult result)
        {
            return result.DestroyedCapitalShips.Any() || result.AttackerShipDamage.Any();
        }

        /// <summary>
        /// Gets the planet ownership selector that applied before bombardment changed control.
        /// </summary>
        /// <param name="result">The bombardment result.</param>
        /// <returns>The planet ownership selector for the bombardment message.</returns>
        private static MessagePlanetOwnership GetBombardmentPlanetOwnership(
            BombardmentResult result
        )
        {
            if (result?.OwnershipChange != null)
                return result.OwnershipChange.PreviousOwner == null
                    ? MessagePlanetOwnership.Neutral
                    : MessagePlanetOwnership.Owned;

            return GetPlanetOwnership(result?.Planet);
        }

        /// <summary>
        /// Gets the planet ownership selector for a planetary assault result.
        /// </summary>
        /// <param name="result">The planetary assault result.</param>
        /// <returns>The planet ownership selector for the assault message.</returns>
        private static MessagePlanetOwnership GetAssaultPlanetOwnership(
            PlanetaryAssaultResult result
        )
        {
            if (result?.OwnershipChange != null)
                return result.OwnershipChange.PreviousOwner == null
                    ? MessagePlanetOwnership.Neutral
                    : MessagePlanetOwnership.Owned;

            return GetPlanetOwnership(result?.Planet);
        }

        /// <summary>
        /// Gets the message ownership selector for a planet.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The planet ownership selector.</returns>
        private static MessagePlanetOwnership GetPlanetOwnership(Planet planet)
        {
            return string.IsNullOrEmpty(planet?.OwnerInstanceID)
                ? MessagePlanetOwnership.Neutral
                : MessagePlanetOwnership.Owned;
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
