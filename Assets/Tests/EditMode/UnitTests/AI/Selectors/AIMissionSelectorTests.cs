using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Selectors
{
    [TestFixture]
    public class AIMissionSelectorTests
    {
        [Test]
        public void FinalizeSelection_StaleIntelOfficerRiskAboveLimit_RejectsMission()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            context.Game.Config.AI.MissionPlanning.MaximumUnprotectedOfficerMissionFoilProbability =
                100;
            context.Game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 1;
            AIMissionProposal proposal = new(
                new[] { context.Game.GetSceneNodeByInstanceID<Officer>("spy") },
                MissionTypeIDs.Espionage,
                context.Assessment.GetKnownPlanet("target")
            );
            new AIMissionProposalScorer().Score(context, proposal);
            context.SetSelectedProposals(new[] { proposal });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            Assert.IsEmpty(context.SelectedProposals);
        }

        [Test]
        public void FinalizeSelection_DefenseEstimateWithinLimits_DoesNotAddLegacyAgePenalty()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            context.Game.Config.AI.MissionPlanning.MaximumUnprotectedOfficerMissionFoilProbability =
                100;
            context.Game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 100;
            context
                .Game
                .Config
                .AI
                .MissionPlanning
                .HostileMissionIntelAgeFoilPenaltyPerRefreshInterval = 100;
            AIMissionProposal proposal = new(
                new[] { context.Game.GetSceneNodeByInstanceID<Officer>("spy") },
                MissionTypeIDs.Espionage,
                context.Assessment.GetKnownPlanet("target")
            );
            new AIMissionProposalScorer().Score(context, proposal);
            context.Game.Config.AI.MissionPlanning.MaximumUnprotectedOfficerMissionFoilProbability =
                (int)System.Math.Ceiling(proposal.FoilProbability);
            context.SetSelectedProposals(new[] { proposal });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            CollectionAssert.AreEqual(new[] { proposal }, context.SelectedProposals);
        }

        [Test]
        public void FinalizeSelection_WithCompleteSabotageTeam_PreservesChosenRoles()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            SpecialForces first = CreateSpecialForces("first", empire.InstanceID);
            SpecialForces second = CreateSpecialForces("second", empire.InstanceID);
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            SpecialForces spare = CreateSpecialForces("spare", empire.InstanceID);
            first.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            second.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            decoy.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            game.AttachNode(first, origin);
            game.AttachNode(second, origin);
            game.AttachNode(decoy, origin);
            game.AttachNode(spare, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal proposal = new(
                new[] { first, second },
                MissionTypeIDs.Sabotage,
                target,
                decoyParticipants: new[] { decoy },
                teamIsFinal: true
            );
            context.SetSelectedProposals(new[] { proposal });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            CollectionAssert.AreEqual(new[] { first, second }, selected.MainParticipants);
            CollectionAssert.AreEqual(new[] { decoy }, selected.DecoyParticipants);
        }

        [Test]
        public void TrySelect_UnprotectedFinalSpecialForcesTeam_DoesNotReserveParticipants()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            SpecialForces main = CreateSpecialForces("main", empire.InstanceID);
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            main.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            decoy.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            Building objective = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "yard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            game.AttachNode(main, origin);
            game.AttachNode(decoy, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionSelector selector = new(new AISelectionState());
            AIMissionProposal unprotected = new(
                new[] { main },
                MissionTypeIDs.Sabotage,
                target,
                selectedTarget: objective,
                teamIsFinal: true
            );
            AIMissionProposal protectedTeam = new(
                new[] { main },
                MissionTypeIDs.Sabotage,
                target,
                selectedTarget: objective,
                decoyParticipants: new[] { decoy },
                teamIsFinal: true
            );

            Assert.IsFalse(selector.TrySelect(context, unprotected));
            Assert.IsTrue(selector.TrySelect(context, protectedTeam));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TrySelect_UnsafeDiplomacy_DoesNotReserveDestination(bool ownedDestination)
        {
            AITurnContext context = CreateDiplomacyBlockadeContext(
                ownedDestination,
                out Officer blockedOfficer,
                out Officer safeOfficer,
                out Planet destination
            );
            AIMissionProposal unsafeMission = new(
                new[] { blockedOfficer },
                MissionTypeIDs.Diplomacy,
                destination
            );
            AIMissionProposal safeMission = new(
                new[] { safeOfficer },
                MissionTypeIDs.Diplomacy,
                destination
            );
            Assert.Greater(
                context
                    .Assessment.GetMissionOdds(
                        context.MissionQueries,
                        unsafeMission.CreateContext()
                    )
                    .PersonnelLossProbability,
                context.Game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability
            );
            AIMissionSelector selector = new(new AISelectionState());

            Assert.IsFalse(selector.TrySelect(context, unsafeMission));
            Assert.IsTrue(selector.TrySelect(context, safeMission));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TrySelect_UnsafeDiplomacy_DoesNotReserveOfficer(bool ownedDestination)
        {
            AITurnContext context = CreateDiplomacyBlockadeContext(
                ownedDestination,
                out Officer blockedOfficer,
                out _,
                out Planet destination
            );
            AIMissionProposal unsafeMission = new(
                new[] { blockedOfficer },
                MissionTypeIDs.Diplomacy,
                destination
            );
            AISelectionState state = new();

            new AIMissionSelector(state).TrySelect(context, unsafeMission);

            Assert.IsTrue(
                state.CanReserve(
                    new[]
                    {
                        new AISelectionReservation(
                            AISelectionReservationKind.MissionActor,
                            blockedOfficer.InstanceID
                        ),
                    }
                )
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TrySelect_OfficerDecoyExceedsLossLimit_RejectsOnlyUnsafeTeam(bool safeDecoy)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            SpecialForces main = CreateSpecialForces("main", empire.InstanceID);
            Officer decoy = EntityFactory.CreateOfficer("decoy", empire.InstanceID);
            game.AttachNode(main, origin);
            game.AttachNode(decoy, origin);
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "detector",
                    OwnerInstanceID = rebels.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                target
            );
            game.Config.ProbabilityTables.Mission.PlanetaryDecoy = new Dictionary<int, int>
            {
                { -1000, safeDecoy ? 100 : 0 },
            };
            game.Config.ProbabilityTables.Mission.Evasion = new Dictionary<int, int>
            {
                { -1000, 0 },
            };
            game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 20;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal mission = new(
                new[] { main },
                MissionTypeIDs.Espionage,
                target,
                decoyParticipants: new[] { decoy },
                teamIsFinal: true
            );

            bool selected = new AIMissionSelector(new AISelectionState()).TrySelect(
                context,
                mission
            );

            Assert.AreEqual(safeDecoy, selected);
        }

        [Test]
        public void FinalizeSelection_SpecialForcesLedTeamHasUnsafeOfficerDecoy_RemovesMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            SpecialForces main = CreateSpecialForces("main", empire.InstanceID);
            Officer decoy = EntityFactory.CreateOfficer("decoy", empire.InstanceID);
            game.AttachNode(main, origin);
            game.AttachNode(decoy, origin);
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "detector",
                    OwnerInstanceID = rebels.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                target
            );
            game.Config.ProbabilityTables.Mission.PlanetaryDecoy = new Dictionary<int, int>
            {
                { -1000, 0 },
            };
            game.Config.ProbabilityTables.Mission.Evasion = new Dictionary<int, int>
            {
                { -1000, 0 },
            };
            game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 20;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal mission = new(
                new[] { main },
                MissionTypeIDs.Espionage,
                target,
                decoyParticipants: new[] { decoy }
            );
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            Assert.IsEmpty(context.SelectedProposals.OfType<AIMissionProposal>());
        }

        [TestCase(ResearchDiscipline.ShipDesign, true)]
        [TestCase(ResearchDiscipline.ShipDesign, false)]
        [TestCase(ResearchDiscipline.FacilityDesign, true)]
        [TestCase(ResearchDiscipline.FacilityDesign, false)]
        [TestCase(ResearchDiscipline.TroopTraining, true)]
        [TestCase(ResearchDiscipline.TroopTraining, false)]
        public void TrySelect_DistinctResearchersInSameDiscipline_AcceptsBothProposals(
            ResearchDiscipline discipline,
            bool samePlanet
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet firstPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "first-planet",
                faction.InstanceID
            );
            Planet secondPlanet = samePlanet
                ? firstPlanet
                : AITestSceneBuilder.AddPlanet(game, sector, "second-planet", faction.InstanceID);
            foreach (Planet planet in new[] { firstPlanet, secondPlanet }.Distinct())
                AddResearchFacility(game, planet, discipline);
            Officer firstOfficer = EntityFactory.CreateOfficer("first-officer", faction.InstanceID);
            Officer secondOfficer = EntityFactory.CreateOfficer(
                "second-officer",
                faction.InstanceID
            );
            firstOfficer.SetBaseRating(Officer.GetRatingForResearchDiscipline(discipline), 60);
            secondOfficer.SetBaseRating(Officer.GetRatingForResearchDiscipline(discipline), 60);
            game.AttachNode(firstOfficer, firstPlanet);
            game.AttachNode(secondOfficer, secondPlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIMissionProposal first = new(
                new[] { firstOfficer },
                MissionTypeIDs.Research,
                firstPlanet,
                discipline: discipline
            );
            AIMissionProposal second = new(
                new[] { secondOfficer },
                MissionTypeIDs.Research,
                secondPlanet,
                discipline: discipline
            );
            AIMissionSelector selector = new(new AISelectionState());

            Assert.IsTrue(selector.TrySelect(context, first));
            Assert.IsTrue(selector.TrySelect(context, second));
        }

        [Test]
        public void TrySelect_SameResearcherInDifferentDisciplines_RejectsSecondProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "planet",
                faction.InstanceID
            );
            Officer officer = EntityFactory.CreateOfficer("officer", faction.InstanceID);
            officer.ShipResearch = 60;
            officer.FacilityResearch = 60;
            game.AttachNode(officer, planet);
            AddResearchFacility(game, planet, ResearchDiscipline.ShipDesign);
            AddResearchFacility(game, planet, ResearchDiscipline.FacilityDesign);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIMissionProposal first = new(
                new[] { officer },
                MissionTypeIDs.Research,
                planet,
                discipline: ResearchDiscipline.ShipDesign
            );
            AIMissionProposal second = new(
                new[] { officer },
                MissionTypeIDs.Research,
                planet,
                discipline: ResearchDiscipline.FacilityDesign
            );
            AIMissionSelector selector = new(new AISelectionState());

            Assert.IsTrue(selector.TrySelect(context, first));
            Assert.IsFalse(selector.TrySelect(context, second));
        }

        [Test]
        public void TrySelect_DistinctDiplomatsAtSamePlanet_RejectsSecondProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "planet",
                faction.InstanceID
            );
            planet.AddVisitor(faction.InstanceID);
            Officer firstOfficer = EntityFactory.CreateOfficer("first-officer", faction.InstanceID);
            Officer secondOfficer = EntityFactory.CreateOfficer(
                "second-officer",
                faction.InstanceID
            );
            game.AttachNode(firstOfficer, planet);
            game.AttachNode(secondOfficer, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIMissionProposal first = new(new[] { firstOfficer }, MissionTypeIDs.Diplomacy, planet);
            AIMissionProposal second = new(
                new[] { secondOfficer },
                MissionTypeIDs.Diplomacy,
                planet
            );
            AIMissionSelector selector = new(new AISelectionState());

            Assert.IsTrue(selector.TrySelect(context, first));
            Assert.IsFalse(selector.TrySelect(context, second));
        }

        [Test]
        public void Execute_WithOfficerLedHostileMission_AssignsDecoyIntentUnit()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            game.AttachNode(officer, origin);
            game.AttachNode(decoy, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSpecialForcesIntent(decoy, SpecialForcesIntent.Decoy);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { officer },
                EspionageMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            CollectionAssert.AreEqual(new[] { decoy }, selected.DecoyParticipants);
        }

        [Test]
        public void Execute_WithTwoAvailableDecoys_AssignsBothDecoys()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            SpecialForces firstDecoy = CreateSpecialForces("first-decoy", empire.InstanceID);
            SpecialForces secondDecoy = CreateSpecialForces("second-decoy", empire.InstanceID);
            game.AttachNode(officer, origin);
            game.AttachNode(firstDecoy, origin);
            game.AttachNode(secondDecoy, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSpecialForcesIntent(firstDecoy, SpecialForcesIntent.Decoy);
            context.SetSpecialForcesIntent(secondDecoy, SpecialForcesIntent.Decoy);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.Espionage,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            CollectionAssert.AreEquivalent(
                new[] { firstDecoy, secondDecoy },
                selected.DecoyParticipants
            );
        }

        [Test]
        public void Execute_WithDecoyReducingOfficerLossBelowLimit_KeepsMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Regiment detector = AITestSceneBuilder.CreateRegiment("detector", rebels.InstanceID);
            detector.DetectionRating = 100;
            game.AttachNode(detector, target);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            game.AttachNode(officer, origin);
            game.AttachNode(decoy, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            game.Config.ProbabilityTables.Mission.Evasion = new Dictionary<int, int>
            {
                { -1000, 0 },
            };
            game.Config.ProbabilityTables.Mission.PlanetaryDecoy = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 5;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSpecialForcesIntent(decoy, SpecialForcesIntent.Decoy);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { officer },
                EspionageMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            mission.SetFoilProbability(100);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            CollectionAssert.AreEqual(new[] { decoy }, selected.DecoyParticipants);
        }

        [Test]
        public void Execute_WithUnprotectedOfficerLossAboveLimit_RemovesMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Regiment detector = AITestSceneBuilder.CreateRegiment("detector", rebels.InstanceID);
            detector.DetectionRating = 100;
            game.AttachNode(detector, target);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            game.AttachNode(officer, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            game.Config.ProbabilityTables.Mission.Evasion = new Dictionary<int, int>
            {
                { -1000, 0 },
            };
            game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 5;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { officer },
                EspionageMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            Assert.IsEmpty(context.SelectedProposals.OfType<AIMissionProposal>());
        }

        [Test]
        public void FinalizeSelection_WithSpecialForcesLedMission_AssignsCompatibleDecoy()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            SpecialForces primary = CreateSpecialForces("primary", empire.InstanceID);
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            game.AttachNode(primary, origin);
            game.AttachNode(decoy, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSpecialForcesIntent(primary, SpecialForcesIntent.PrimaryAgent);
            context.SetSpecialForcesIntent(decoy, SpecialForcesIntent.Decoy);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { primary },
                EspionageMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            CollectionAssert.AreEqual(new[] { decoy }, selected.DecoyParticipants);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void FinalizeSelection_WithSelectedSpecialForcesMissions_PairsWithoutReusingParticipants(
            int participantCount
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            List<AIMissionProposal> missions = new();
            for (int index = 0; index < participantCount; index++)
            {
                SpecialForces participant = CreateSpecialForces(
                    $"participant-{index}",
                    empire.InstanceID
                );
                game.AttachNode(participant, origin);
                AIMissionProposal mission = new(
                    new[] { participant },
                    EspionageMission.MissionTypeID,
                    target
                );
                mission.SetScore(50 - index);
                missions.Add(mission);
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSelectedProposals(missions);

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            List<AIMissionProposal> selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .ToList();
            Assert.AreEqual(participantCount / 2, selected.Count);
            Assert.AreSame(missions[0].Participant, selected[0].Participant);
            Assert.IsTrue(selected.All(mission => mission.DecoyParticipants.Count == 1));
            Assert.AreEqual(
                selected.Count * 2,
                selected.SelectMany(mission => mission.Participants).Distinct().Count()
            );
        }

        [Test]
        public void FinalizeSelection_WithPairedSpecialForces_StartsMissionWithBothParticipants()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            SpecialForces primary = CreateSpecialForces("primary", empire.InstanceID);
            SpecialForces partner = CreateSpecialForces("partner", empire.InstanceID);
            game.AttachNode(primary, origin);
            game.AttachNode(partner, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal proposal = new(
                new[] { primary },
                EspionageMission.MissionTypeID,
                target
            );
            proposal.SetScore(50);
            context.SetSelectedProposals(new[] { proposal });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);
            context.SelectedProposals.OfType<AIMissionProposal>().Single().Execute(context);

            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            CollectionAssert.AreEqual(new[] { primary }, mission.GetMainParticipants());
            CollectionAssert.AreEqual(new[] { partner }, mission.GetDecoyParticipants());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FinalizeSelection_WithUnclaimedSpecialForces_RequiresCompatiblePartner(
            bool compatible
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            SpecialForces primary = CreateSpecialForces("primary", empire.InstanceID);
            SpecialForces partner = CreateSpecialForces("partner", empire.InstanceID);
            if (!compatible)
                partner.AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Reconnaissance };
            game.AttachNode(primary, origin);
            game.AttachNode(partner, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal mission = new(
                new[] { primary },
                EspionageMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .SingleOrDefault();
            if (compatible)
                CollectionAssert.AreEqual(new[] { partner }, selected.DecoyParticipants);
            else
                Assert.IsNull(selected);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FinalizeSelection_WithSoloSpecialForces_HoldsOnlyHostileMission(bool hostile)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "target",
                hostile ? rebels.InstanceID : empire.InstanceID
            );
            SpecialForces primary = CreateSpecialForces("primary", empire.InstanceID);
            game.AttachNode(primary, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal mission = new(
                new[] { primary },
                EspionageMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            Assert.AreEqual(
                hostile ? 0 : 1,
                context.SelectedProposals.OfType<AIMissionProposal>().Count()
            );
        }

        [Test]
        public void Execute_WithOnlyOfficerAvailable_KeepsEspionageMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            game.AttachNode(officer, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { officer },
                EspionageMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            Assert.AreSame(mission, context.SelectedProposals.Single());
        }

        [Test]
        public void Execute_WithUnclaimedOfficerAvailable_AssignsOfficerAsDecoy()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Officer participant = EntityFactory.CreateOfficer("participant", empire.InstanceID);
            Officer decoy = EntityFactory.CreateOfficer("decoy", empire.InstanceID);
            game.AttachNode(participant, origin);
            game.AttachNode(decoy, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { participant },
                EspionageMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            CollectionAssert.AreEqual(new[] { decoy }, selected.DecoyParticipants);
        }

        [Test]
        public void Execute_WithStaleIntelAndNoDecoy_RemovesOfficerMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.MissionPlanning.MissionDefenseIntelFullWeightTicks = 0;
            game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks = 20;
            game.Config.AI.MissionPlanning.HostileMissionIntelAgeFoilPenaltyPerRefreshInterval = 5;
            game.Config.AI.MissionPlanning.MaximumUnprotectedOfficerMissionFoilProbability = 20;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            game.AttachNode(officer, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = 100;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { officer },
                EspionageMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            Assert.IsEmpty(context.SelectedProposals);
        }

        [Test]
        public void Execute_WithTwoRiskyOfficerMissions_CombinesThemIntoProtectedMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.MissionPlanning.MissionDefenseIntelFullWeightTicks = 0;
            game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks = 20;
            game.Config.AI.MissionPlanning.HostileMissionIntelAgeFoilPenaltyPerRefreshInterval = 5;
            game.Config.AI.MissionPlanning.MaximumUnprotectedOfficerMissionFoilProbability = 20;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet firstTarget = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "first-target",
                rebels.InstanceID
            );
            Planet secondTarget = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "second-target",
                rebels.InstanceID
            );
            Officer firstOfficer = EntityFactory.CreateOfficer("first-officer", empire.InstanceID);
            Officer secondOfficer = EntityFactory.CreateOfficer(
                "second-officer",
                empire.InstanceID
            );
            game.AttachNode(firstOfficer, origin);
            game.AttachNode(secondOfficer, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, firstTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, secondTarget);
            game.CurrentTick = 100;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal firstMission = new AIMissionProposal(
                new[] { firstOfficer },
                EspionageMission.MissionTypeID,
                firstTarget
            );
            firstMission.SetScore(100);
            AIMissionProposal secondMission = new AIMissionProposal(
                new[] { secondOfficer },
                EspionageMission.MissionTypeID,
                secondTarget
            );
            secondMission.SetScore(50);
            context.SetSelectedProposals(new[] { firstMission, secondMission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            Assert.AreEqual(firstTarget, selected.TargetPlanet);
            CollectionAssert.AreEqual(new[] { secondOfficer }, selected.DecoyParticipants);
        }

        [Test]
        public void Execute_WithOfficerLedNeutralMission_DoesNotAssignDecoy()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", null);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            game.AttachNode(officer, origin);
            game.AttachNode(decoy, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSpecialForcesIntent(decoy, SpecialForcesIntent.Decoy);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { officer },
                DiplomacyMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            Assert.IsEmpty(selected.DecoyParticipants);
        }

        [Test]
        public void Execute_WithFreshIntelAndNoDecoy_KeepsOfficerMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            game.AttachNode(officer, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { officer },
                SabotageMission.MissionTypeID,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            Assert.AreSame(mission, context.SelectedProposals.Single());
        }

        [Test]
        public void Execute_WithScarceDecoy_AssignsHighestFoilRiskMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet firstTarget = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "first-target",
                rebels.InstanceID
            );
            Planet secondTarget = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "second-target",
                rebels.InstanceID
            );
            Officer firstOfficer = EntityFactory.CreateOfficer("first-officer", empire.InstanceID);
            Officer secondOfficer = EntityFactory.CreateOfficer(
                "second-officer",
                empire.InstanceID
            );
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            game.AttachNode(firstOfficer, origin);
            game.AttachNode(secondOfficer, origin);
            game.AttachNode(decoy, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, firstTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, secondTarget);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSpecialForcesIntent(decoy, SpecialForcesIntent.Decoy);
            AIMissionProposal lowerRisk = new AIMissionProposal(
                new[] { firstOfficer },
                EspionageMission.MissionTypeID,
                firstTarget
            );
            lowerRisk.SetScore(100);
            lowerRisk.SetFoilProbability(10);
            AIMissionProposal higherRisk = new AIMissionProposal(
                new[] { secondOfficer },
                EspionageMission.MissionTypeID,
                secondTarget
            );
            higherRisk.SetScore(50);
            higherRisk.SetFoilProbability(60);
            context.SetSelectedProposals(new[] { lowerRisk, higherRisk });

            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);

            AIMissionProposal[] selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .ToArray();
            Assert.IsEmpty(
                selected.Single(proposal => proposal.TargetPlanet == firstTarget).DecoyParticipants
            );
            CollectionAssert.AreEqual(
                new[] { decoy },
                selected.Single(proposal => proposal.TargetPlanet == secondTarget).DecoyParticipants
            );
        }

        private static void AddResearchFacility(
            GameRoot game,
            Planet planet,
            ResearchDiscipline discipline
        )
        {
            BuildingType type = discipline switch
            {
                ResearchDiscipline.ShipDesign => BuildingType.Shipyard,
                ResearchDiscipline.TroopTraining => BuildingType.TrainingFacility,
                _ => BuildingType.ConstructionFacility,
            };
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                planet.InstanceID + "-" + discipline,
                type,
                discipline.ToManufacturingType()
            );
        }

        private static AITurnContext CreateDiplomacyBlockadeContext(
            bool ownedDestination,
            out Officer blockedOfficer,
            out Officer safeOfficer,
            out Planet destination
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet blockedOrigin = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "blocked-origin",
                faction.InstanceID
            );
            Planet safeOrigin = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "safe-origin",
                faction.InstanceID
            );
            destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                ownedDestination ? faction.InstanceID : null
            );
            destination.AddVisitor(faction.InstanceID);
            Fleet blockade = new() { InstanceID = "blockade", OwnerInstanceID = enemy.InstanceID };
            game.AttachNode(blockade, blockedOrigin);
            CapitalShip detector = AITestSceneBuilder.CreateCapitalShip(
                "detector",
                enemy.InstanceID
            );
            detector.DetectionRating = 100;
            game.AttachNode(detector, blockade);
            blockedOfficer = EntityFactory.CreateOfficer("blocked-officer", faction.InstanceID);
            safeOfficer = EntityFactory.CreateOfficer("safe-officer", faction.InstanceID);
            game.AttachNode(blockedOfficer, blockedOrigin);
            game.AttachNode(safeOfficer, safeOrigin);
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            game.Config.ProbabilityTables.Mission.Evasion = new Dictionary<int, int>
            {
                { -1000, 0 },
            };
            game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 20;
            AITestSceneBuilder.RevealPlanet(game, faction, blockedOrigin);
            AITestSceneBuilder.RevealPlanet(game, faction, destination);
            return AITestSceneBuilder.CreateContext(game, faction);
        }

        /// <summary>
        /// Creates special forces.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <returns>The created special forces.</returns>
        private static SpecialForces CreateSpecialForces(string instanceId, string ownerInstanceId)
        {
            return new SpecialForces
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { EspionageMission.MissionTypeID },
            };
        }
    }
}
