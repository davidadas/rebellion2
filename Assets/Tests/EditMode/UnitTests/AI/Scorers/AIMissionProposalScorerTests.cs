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
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;
using OfficerRating = Rebellion.Game.Units.SkillRating;

namespace Rebellion.Tests.AI.Scorers
{
    [TestFixture]
    public class AIMissionProposalScorerTests
    {
        /// <summary>Values a selected diplomat in the candidate sector more than the same diplomat assigned elsewhere.</summary>
        [Test]
        public void Score_SelectedDiplomatInCandidateSector_PrefersCommittedGroup()
        {
            (AITurnContext context, PlanetSector[] sectors) =
                CreateDiplomacyGroupCompetitionContext(0, 0);
            Officer selected = context.Game.GetSceneNodeByInstanceID<Officer>("diplomat-0");
            Officer candidate = context.Game.GetSceneNodeByInstanceID<Officer>("diplomat-1");
            Planet target = sectors[0]
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == "target-0-1");
            Planet localDestination = sectors[0]
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == "target-0-0");
            Planet otherDestination = sectors[1]
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == "target-1-0");
            AIMissionProposal proposal = new(new[] { candidate }, MissionTypeIDs.Diplomacy, target);
            AIMissionProposalScorer scorer = new();

            double local = scorer.Score(
                context,
                proposal,
                new Dictionary<Officer, Planet> { [selected] = localDestination }
            );
            double elsewhere = scorer.Score(
                context,
                proposal,
                new Dictionary<Officer, Planet> { [selected] = otherDestination }
            );

            Assert.Greater(local, elsewhere);
        }

        /// <summary>Stops counting a diplomat as available to every sector after assigning that officer elsewhere.</summary>
        [Test]
        public void Score_SelectedDiplomatInOtherSector_ReducesUncommittedOpportunity()
        {
            (AITurnContext context, PlanetSector[] sectors) =
                CreateDiplomacyGroupCompetitionContext(0, 0);
            Officer selected = context.Game.GetSceneNodeByInstanceID<Officer>("diplomat-0");
            Officer candidate = context.Game.GetSceneNodeByInstanceID<Officer>("diplomat-1");
            Planet target = sectors[0]
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == "target-0-1");
            Planet destination = sectors[1]
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == "target-1-0");
            AIMissionProposal proposal = new(new[] { candidate }, MissionTypeIDs.Diplomacy, target);
            AIMissionProposalScorer scorer = new();

            double available = scorer.Score(context, proposal);
            double committed = scorer.Score(
                context,
                proposal,
                new Dictionary<Officer, Planet> { [selected] = destination }
            );

            Assert.Less(committed, available);
        }

        /// <summary>Raises diplomacy value only when acquiring the planet supplies an industrial opportunity.</summary>
        /// <param name="ownedSpace">The owned planet's building capacity.</param>
        /// <param name="expectedBonus">Whether acquisition should increase the target's value.</param>
        [TestCase(0, true)]
        [TestCase(20, false)]
        public void GetDiplomacyTargetValue_IndustrialSiteNeeded_BoostsOnlyUsefulAcquisitions(
            int ownedSpace,
            bool expectedBonus
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "industry-sector");
            AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "owned",
                faction.InstanceID,
                energyCapacity: ownedSpace
            );
            Planet candidate = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "neutral",
                null,
                energyCapacity: 20
            );
            AITestSceneBuilder.RevealPlanet(game, faction, candidate);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            game.Config.AI.MissionPlanning.Utility.Diplomacy.IndustrialOpportunity.Weight = 0;
            double withoutBonus = AIMissionProposalScorer.GetDiplomacyTargetValue(
                context,
                candidate
            );
            game.Config.AI.MissionPlanning.Utility.Diplomacy.IndustrialOpportunity.Weight = 0.3;

            double withBonus = AIMissionProposalScorer.GetDiplomacyTargetValue(context, candidate);

            Assert.AreEqual(expectedBonus, withBonus > withoutBonus);
            if (!expectedBonus)
                Assert.AreEqual(withoutBonus, withBonus);
        }

        [TestCase(3, 0, 1, 4)]
        [TestCase(3, 1, 1, 4)]
        [TestCase(1, 1, 0, 3)]
        public void Score_DiplomacyGroupCompetition_ConcentratesOnPreferredNearbySector(
            int nearbyRivals,
            int alternativeRivals,
            int preferredSector,
            int expectedDiplomats
        )
        {
            (AITurnContext context, PlanetSector[] sectors) =
                CreateDiplomacyGroupCompetitionContext(nearbyRivals, alternativeRivals);
            context.AddProposals(new AIMissionPlanner().Plan(context));

            AIMissionProposal[] selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == MissionTypeIDs.Diplomacy)
                .ToArray();

            Assert.AreEqual(4, selected.Length);
            Assert.AreEqual(
                expectedDiplomats,
                selected.Count(proposal =>
                    proposal.TargetPlanet.GetParentOfType<PlanetSector>().InstanceID
                    == sectors[preferredSector].InstanceID
                )
            );
        }

        [Test]
        public void Score_DiplomacyGroupWithEqualRivals_DoesNotSeekDistantEmptySector()
        {
            (AITurnContext context, PlanetSector[] sectors) =
                CreateDiplomacyGroupCompetitionContext(1, 1);
            context.AddProposals(new AIMissionPlanner().Plan(context));

            AIMissionProposal[] selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == MissionTypeIDs.Diplomacy)
                .ToArray();

            Assert.AreEqual(4, selected.Length);
            Assert.IsFalse(
                selected.Any(proposal =>
                    proposal.TargetPlanet.GetParentOfType<PlanetSector>().InstanceID
                    == sectors[2].InstanceID
                )
            );
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(8)]
        public void GetScoreUpperBound_DiplomacyExpansionCosts_AppliesSameDiscountAsScore(
            int rivals
        )
        {
            (AITurnContext context, Officer officer, Planet[] targets) =
                CreateDiplomacyCompetitionContext(rivals, 0);
            var scorer = new AIMissionProposalScorer();
            var proposal = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.Diplomacy,
                targets[0]
            );
            double adjusted = scorer.Score(context, proposal);
            double adjustedBound = scorer.GetScoreUpperBound(context, proposal);
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.Competition.Weight = 0;
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.TravelTime.Weight = 0;
            double unadjusted = scorer.Score(context, proposal);
            double unadjustedBound = scorer.GetScoreUpperBound(context, proposal);

            Assert.Greater(adjusted, 0);
            Assert.GreaterOrEqual(adjustedBound, adjusted);
            Assert.That(
                adjustedBound / unadjustedBound,
                Is.EqualTo(adjusted / unadjusted).Within(1e-12)
            );
        }

        [TestCase(0, 0, 0)]
        [TestCase(1, 1, 0)]
        [TestCase(3, 0, 1)]
        [TestCase(8, 1, 1)]
        public void Score_DiplomacyCompetitionAndDistance_SelectsNearbyViableSector(
            int nearbyRivals,
            int alternativeRivals,
            int expectedTarget
        )
        {
            (AITurnContext context, Officer officer, Planet[] targets) =
                CreateDiplomacyCompetitionContext(nearbyRivals, alternativeRivals);
            context.AddProposals(new AIMissionPlanner().Plan(context));

            AIMissionProposal selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIMissionProposal>()
                .Single(proposal => proposal.MissionTypeID == MissionTypeIDs.Diplomacy);

            Assert.AreEqual(targets[expectedTarget].InstanceID, selected.TargetPlanet.InstanceID);
            Assert.AreSame(officer, selected.Participant);
        }

        [Test]
        public void Score_DiplomacyWithIncreasingCompetition_DecreasesWithoutRejectingTarget()
        {
            var scorer = new AIMissionProposalScorer();
            double previous = double.MaxValue;
            foreach (int rivals in new[] { 0, 1, 3, 8 })
            {
                (AITurnContext context, Officer officer, Planet[] targets) =
                    CreateDiplomacyCompetitionContext(rivals, 0);

                double score = scorer.Score(
                    context,
                    new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, targets[0])
                );

                Assert.Greater(score, 0);
                Assert.Less(score, previous);
                previous = score;
            }
        }

        [Test]
        public void Score_DiplomacyBeyondGeneralTravelSaturation_StillPrefersShorterJourney()
        {
            (AITurnContext context, Officer officer, Planet[] targets) =
                CreateDiplomacyCompetitionContext(0, 0);
            targets[0].PositionX = 200;
            targets[1].PositionX = 500;
            var scorer = new AIMissionProposalScorer();

            double nearer = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, targets[0])
            );
            double farther = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, targets[1])
            );

            Assert.Greater(nearer, farther);
        }

        [Test]
        public void Score_DiplomacyWithDistantUncontestedAlternative_PreservesCloserContestedTarget()
        {
            (AITurnContext context, Officer officer, Planet[] targets) =
                CreateDiplomacyCompetitionContext(3, 0);
            targets[1].PositionX = 10000;
            targets[2].PositionX = 10000;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            context.AddProposals(new AIMissionPlanner().Plan(context));

            AIMissionProposal selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIMissionProposal>()
                .Single(proposal => proposal.MissionTypeID == MissionTypeIDs.Diplomacy);

            Assert.AreEqual(targets[0].InstanceID, selected.TargetPlanet.InstanceID);
        }

        [Test]
        public void Score_DiplomacyOnOwnedPlanet_DoesNotApplyExpansionCosts()
        {
            (AITurnContext context, Officer officer, Planet[] targets) =
                CreateDiplomacyCompetitionContext(3, 0);
            targets[0].OwnerInstanceID = context.Faction.InstanceID;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            var scorer = new AIMissionProposalScorer();
            var proposal = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.Diplomacy,
                targets[0]
            );
            double withCosts = scorer.Score(context, proposal);
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.Competition.Weight = 0;
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.TravelTime.Weight = 0;

            Assert.AreEqual(withCosts, scorer.Score(context, proposal));
        }

        [TestCase(3, 3, 90, 0, true, 40, true)]
        [TestCase(2, 3, 90, 0, true, 40, false)]
        [TestCase(3, 2, 90, 0, true, 40, false)]
        [TestCase(3, 3, 74, 0, true, 40, false)]
        [TestCase(3, 3, 90, 1000, true, 40, false)]
        [TestCase(3, 3, 90, 0, false, 40, false)]
        [TestCase(3, 3, 90, 0, true, 0, false)]
        [TestCase(4, 3, 90, 0, true, 40, true)]
        public void Score_DiplomacyGroup_AppliesConcentrationOnlyForNearbyKnownOpportunities(
            int diplomatCount,
            int targetCount,
            int rating,
            int originX,
            bool revealed,
            int support,
            bool expectsBonus
        )
        {
            (AITurnContext context, Officer officer, Planet target) =
                CreateDiplomacyConcentrationContext(
                    diplomatCount,
                    targetCount,
                    rating,
                    originX,
                    revealed,
                    support
                );
            AIMissionProposal proposal = new(new[] { officer }, MissionTypeIDs.Diplomacy, target);
            AIMissionProposalScorer scorer = new();
            GameConfig.AIDiplomacyUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility
                .Diplomacy;
            utility.Concentration.Weight = 0;
            double withoutBonus = scorer.Score(context, proposal);
            utility.Concentration.Weight = 0.6;

            double withBonus = scorer.Score(context, proposal);

            if (expectsBonus)
                Assert.Greater(withBonus, withoutBonus);
            else
                Assert.AreEqual(withoutBonus, withBonus);
        }

        [Test]
        public void GetDiplomacyTargetValue_KnownHostileFleet_DoesNotReceiveConcentrationBonus()
        {
            (AITurnContext context, _, Planet target) = CreateDiplomacyConcentrationContext(
                targetCount: 4
            );
            Fleet blockade = EntityFactory.CreateFleet("blockade", "rebels");
            context.Game.AttachNode(blockade, target);
            context.Game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("blockader", "rebels"),
                blockade
            );
            AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, target);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            Planet known = context.Assessment.GetKnownPlanet(target.InstanceID);
            Assert.IsTrue(context.Assessment.HasKnownHostileFleet(known));
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.Concentration.Weight = 0;
            double withoutBonus = AIMissionProposalScorer.GetDiplomacyTargetValue(context, known);
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.Concentration.Weight = 0.6;

            Assert.AreEqual(
                withoutBonus,
                AIMissionProposalScorer.GetDiplomacyTargetValue(context, known)
            );
        }

        [Test]
        public void Score_DistantDiplomat_DoesNotReceiveNearbyGroupBonus()
        {
            (AITurnContext context, _, Planet target) = CreateDiplomacyConcentrationContext();
            Planet remote = AITestSceneBuilder.AddPlanet(
                context.Game,
                context.Game.GetSceneNodeByInstanceID<PlanetSector>("origin-sector"),
                "remote",
                context.Faction.InstanceID,
                positionX: 1000
            );
            Officer officer = EntityFactory.CreateOfficer(
                "remote-diplomat",
                context.Faction.InstanceID
            );
            officer.Ratings[OfficerRating.Diplomacy] = 90;
            context.Game.AttachNode(officer, remote);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            AIMissionProposal proposal = new(new[] { officer }, MissionTypeIDs.Diplomacy, target);
            AIMissionProposalScorer scorer = new();
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.Concentration.Weight = 0;
            double withoutBonus = scorer.Score(context, proposal);
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.Concentration.Weight = 0.6;

            Assert.AreEqual(withoutBonus, scorer.Score(context, proposal));
        }

        [Test]
        public void Score_NearbyDiplomacyGroup_SelectsDifferentNeutralWorldsInSameSector()
        {
            (AITurnContext context, _, Planet target) = CreateDiplomacyConcentrationContext();
            PlanetSector competitor = AITestSceneBuilder.AddSector(
                context.Game,
                "competitor-sector"
            );
            competitor.SectorType = PlanetSectorType.Core;
            Planet alternative = null;
            for (int index = 0; index < 10; index++)
            {
                string owner =
                    index < 2 ? context.Faction.InstanceID
                    : index == 2 ? null
                    : "rebels";
                Planet candidate = AITestSceneBuilder.AddPlanet(
                    context.Game,
                    competitor,
                    "competitor-" + index,
                    owner,
                    positionX: 10
                );
                candidate.SetPopularSupport(
                    context.Faction.InstanceID,
                    owner == context.Faction.InstanceID ? 100 : 40
                );
                candidate.SetPopularSupport("rebels", owner == context.Faction.InstanceID ? 0 : 60);
                candidate.AddVisitor(context.Faction.InstanceID);
                AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, candidate);
                if (index == 2)
                    alternative = candidate;
            }
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.Concentration.Weight = 0;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            foreach (AIProposal proposal in new AIMissionPlanner().Plan(context))
                context.AddProposal(proposal);
            AIMissionProposal[] withoutBonus = new AISelectionPhase()
                .Select(context)
                .OfType<AIMissionProposal>()
                .ToArray();

            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.Concentration.Weight = 0.6;
            foreach (AIProposal proposal in new AIMissionPlanner().Plan(context))
                context.AddProposal(proposal);

            AIMissionProposal[] selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == MissionTypeIDs.Diplomacy)
                .ToArray();

            Assert.AreEqual(3, selected.Length);
            Assert.IsTrue(
                withoutBonus.Any(proposal =>
                    proposal.TargetPlanet.InstanceID == alternative.InstanceID
                )
            );
            Assert.AreEqual(
                3,
                selected.Select(proposal => proposal.Participant.InstanceID).Distinct().Count()
            );
            Assert.AreEqual(
                3,
                selected.Select(proposal => proposal.TargetPlanet.InstanceID).Distinct().Count()
            );
            Assert.IsTrue(
                selected.All(proposal =>
                    proposal.TargetPlanet.GetParentOfType<PlanetSector>().InstanceID
                    == target.GetParentOfType<PlanetSector>().InstanceID
                )
            );
        }

        [Test]
        public void Score_StaleDefenderIntel_RecordsAssessmentFoilProbability()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            AIMissionProposal proposal = new(
                new[] { context.Game.GetSceneNodeByInstanceID<Officer>("spy") },
                MissionTypeIDs.Espionage,
                context.Assessment.GetKnownPlanet("target")
            );
            MissionOdds expected = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                proposal.CreateContext()
            );

            new AIMissionProposalScorer().Score(context, proposal);

            Assert.Greater(expected.FoilProbability, 0);
            Assert.AreEqual(expected.FoilProbability, proposal.FoilProbability, 0.001);
        }

        [Test]
        public void Score_AbortMissionProposal_ReturnsZero()
        {
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double score = scorer.Score(null, new AIAbortMissionProposal(null));

            Assert.Zero(score);
        }

        [Test]
        public void Score_DiplomacyProposal_ReturnsHigherScoreForLowerSupportPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet lowSupport = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "low-support",
                empire.InstanceID
            );
            Planet highSupport = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "high-support",
                empire.InstanceID
            );
            lowSupport.SetPopularSupport(empire.InstanceID, 10);
            highSupport.SetPopularSupport(empire.InstanceID, 90);
            lowSupport.AddVisitor(empire.InstanceID);
            highSupport.AddVisitor(empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Diplomacy] = 100;
            game.AttachNode(officer, lowSupport);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double lowSupportScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, lowSupport)
            );
            double highSupportScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, highSupport)
            );

            Assert.Greater(lowSupportScore, highSupportScore);
        }

        [Test]
        public void Score_DiplomacyProposal_PrioritizesCoreWorld()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector core = AITestSceneBuilder.AddSector(game, "core");
            core.SectorType = PlanetSectorType.Core;
            PlanetSector outerRim = AITestSceneBuilder.AddSector(game, "outer-rim");
            outerRim.SectorType = PlanetSectorType.OuterRim;
            Planet coreWorld = AITestSceneBuilder.AddPlanet(game, core, "core-world", null);
            Planet outerRimWorld = AITestSceneBuilder.AddPlanet(
                game,
                outerRim,
                "outer-rim-world",
                null
            );
            Planet origin = AITestSceneBuilder.AddPlanet(game, core, "origin", empire.InstanceID);
            coreWorld.SetPopularSupport(empire.InstanceID, 1);
            outerRimWorld.SetPopularSupport(empire.InstanceID, 99);
            coreWorld.AddVisitor(empire.InstanceID);
            outerRimWorld.AddVisitor(empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Diplomacy] = 100;
            game.AttachNode(officer, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double coreScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, coreWorld)
            );
            double outerRimScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, outerRimWorld)
            );

            Assert.Greater(coreScore, outerRimScore);
        }

        [Test]
        public void GetDiplomacyTargetValue_WithInfrastructureDeficit_PrioritizesMatchingFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.TrainingFacilityHubTargetCount = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet facilityWorld = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "facility-world",
                null
            );
            Planet emptyWorld = AITestSceneBuilder.AddPlanet(game, system, "empty-world", null);
            facilityWorld.SetPopularSupport(empire.InstanceID, 50);
            emptyWorld.SetPopularSupport(empire.InstanceID, 50);
            AITestSceneBuilder.AddProductionFacility(
                game,
                facilityWorld,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double facilityValue = AIMissionProposalScorer.GetDiplomacyTargetValue(
                context,
                facilityWorld
            );
            double emptyValue = AIMissionProposalScorer.GetDiplomacyTargetValue(
                context,
                emptyWorld
            );

            Assert.Greater(facilityValue, emptyValue);
        }

        [TestCase(true, false, true, 0.25)]
        [TestCase(false, false, true, 0.25)]
        [TestCase(true, true, true, 0.25)]
        [TestCase(false, true, true, 0.25)]
        [TestCase(true, false, true, 0)]
        [TestCase(false, true, true, 0)]
        [TestCase(true, false, false, 0.25)]
        [TestCase(false, true, false, 0.25)]
        public void Score_ComparableDiplomacyTargets_AppliesConfiguredHeadquartersSectorPreference(
            bool useEmpire,
            bool ownedTargets,
            bool hasHeadquarters,
            double weight
        )
        {
            (AITurnContext context, Officer diplomat, Planet homeTarget, Planet otherTarget) =
                CreateHeadquartersDiplomacyContext(useEmpire, ownedTargets, hasHeadquarters);
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.HeadquartersSector.Weight =
                weight;
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double homeScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { diplomat }, MissionTypeIDs.Diplomacy, homeTarget)
            );
            double otherScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { diplomat }, MissionTypeIDs.Diplomacy, otherTarget)
            );

            if (hasHeadquarters && weight > 0)
                Assert.Greater(homeScore, otherScore);
            else
                Assert.AreEqual(otherScore, homeScore, 0.000001);
        }

        [Test]
        public void GetDiplomacyTargetValue_TargetOutsideHeadquartersSector_DoesNotChangeWhenBonusEnabled()
        {
            (AITurnContext context, _, _, Planet otherTarget) = CreateHeadquartersDiplomacyContext(
                true,
                false,
                true
            );
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.HeadquartersSector.Weight = 0;
            double withoutBonus = AIMissionProposalScorer.GetDiplomacyTargetValue(
                context,
                otherTarget
            );
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.HeadquartersSector.Weight =
                0.25;

            double withBonus = AIMissionProposalScorer.GetDiplomacyTargetValue(
                context,
                otherTarget
            );

            Assert.AreEqual(withoutBonus, withBonus);
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        public void Score_CompetingDiplomacySectors_PrioritizesHeadquartersThenStrongestPresence(
            bool useEmpire,
            bool hasHeadquarters
        )
        {
            (AITurnContext context, Officer diplomat, Planet homeTarget, Planet otherTarget) =
                CreateHeadquartersDiplomacyContext(useEmpire, false, hasHeadquarters);
            PlanetSector homeSector = homeTarget.GetParentOfType<PlanetSector>();
            PlanetSector otherSector = otherTarget.GetParentOfType<PlanetSector>();
            homeSector.SectorType = hasHeadquarters
                ? PlanetSectorType.OuterRim
                : PlanetSectorType.Core;
            otherSector.SectorType = hasHeadquarters
                ? PlanetSectorType.Core
                : PlanetSectorType.OuterRim;
            for (int index = 0; index < 9; index++)
            {
                AITestSceneBuilder.AddPlanet(
                    context.Game,
                    otherSector,
                    "owned-" + index,
                    context.Faction.InstanceID
                );
                if (index < 8)
                    AITestSceneBuilder.AddPlanet(
                        context.Game,
                        homeSector,
                        "neutral-" + index,
                        null
                    );
            }
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.HeadquartersSector.Weight = 1;
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.SystemPresence.Weight = 0.6;
            AITurnContext updated = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double homeScore = scorer.Score(
                updated,
                new AIMissionProposal(new[] { diplomat }, MissionTypeIDs.Diplomacy, homeTarget)
            );
            double otherScore = scorer.Score(
                updated,
                new AIMissionProposal(new[] { diplomat }, MissionTypeIDs.Diplomacy, otherTarget)
            );

            if (hasHeadquarters)
                Assert.Greater(homeScore, otherScore);
            else
                Assert.Greater(otherScore, homeScore);
        }

        [Test]
        public void GetDiplomacyTargetValue_HeadquartersChangesSector_PrefersCurrentHeadquartersSector()
        {
            (AITurnContext context, _, Planet homeTarget, Planet otherTarget) =
                CreateHeadquartersDiplomacyContext(false, true, true);
            context.Game.Config.AI.MissionPlanning.Utility.Diplomacy.HeadquartersSector.Weight =
                0.25;
            context.Faction.HQInstanceID = otherTarget.InstanceID;
            AITurnContext updated = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            double homeValue = AIMissionProposalScorer.GetDiplomacyTargetValue(updated, homeTarget);
            double otherValue = AIMissionProposalScorer.GetDiplomacyTargetValue(
                updated,
                otherTarget
            );

            Assert.Greater(otherValue, homeValue);
        }

        [Test]
        public void Score_RecruitmentProposal_ReturnsHigherScoreForHigherSupportPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet lowSupport = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "low-support",
                empire.InstanceID
            );
            Planet highSupport = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "high-support",
                empire.InstanceID
            );
            lowSupport.SetPopularSupport(empire.InstanceID, 20);
            highSupport.SetPopularSupport(empire.InstanceID, 80);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.IsMain = true;
            officer.Ratings[OfficerRating.Leadership] = 80;
            game.AttachNode(officer, lowSupport);
            Officer recruit = EntityFactory.CreateOfficer("recruit", null);
            recruit.RecruitingFactionInstanceIDs = new List<string> { empire.InstanceID };
            game.GetUnrecruitedOfficers().Add(recruit);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double lowSupportScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Recruitment, lowSupport)
            );
            double highSupportScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Recruitment, highSupport)
            );

            Assert.Greater(highSupportScore, lowSupportScore);
        }

        [Test]
        public void Score_ReconnaissanceProposal_WithDistantTarget_RemainsSelectable()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target",
                null,
                positionX: 600
            );
            SpecialForces probe = AITestSceneBuilder.CreateSpecialForces(
                "probe",
                empire.InstanceID
            );
            probe.AllowedMissionTypeIDs.Add(MissionTypeIDs.Reconnaissance);
            probe.Ratings[OfficerRating.Espionage] = 30;
            game.AttachNode(probe, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double score = new AIMissionProposalScorer().Score(
                context,
                new AIMissionProposal(new[] { probe }, MissionTypeIDs.Reconnaissance, target)
            );

            Assert.Greater(score, 0);
        }

        [Test]
        public void Score_SubdueUprisingBelowProbabilityFloor_ReturnsZeroDespitePriorityBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "uprising",
                empire.InstanceID
            );
            planet.BeginUprising();
            planet.SetPopularSupport(empire.InstanceID, 0);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.SubdueUprising);
            participant.Ratings[OfficerRating.Leadership] = 0;
            game.AttachNode(participant, planet);
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { -1000, 19 },
            };
            game.Config.AI.MissionPlanning.MinimumUprisingMissionSuccessPercent = 20;
            game.Config.AI.MissionPlanning.Utility.Priority.SubdueUprising.Weight = 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionProposal(
                new[] { participant },
                MissionTypeIDs.SubdueUprising,
                planet
            );

            Assert.IsTrue(proposal.CanExecute(context));
            MissionOdds odds = context.MissionQueries.GetMissionOdds(proposal.CreateContext());
            Assert.IsNotNull(odds);
            Assert.Less(
                odds.ObjectiveSuccessProbability,
                game.Config.AI.MissionPlanning.MinimumUprisingMissionSuccessPercent
            );
            double score = new AIMissionProposalScorer().Score(context, proposal);

            Assert.AreEqual(0, score);
        }

        [Test]
        public void GetScoreUpperBound_ExecutableProposal_DoesNotUnderestimateScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.AddVisitor(empire.InstanceID);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.Espionage);
            participant.Ratings[OfficerRating.Espionage] = 60;
            game.AttachNode(participant, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { participant },
                MissionTypeIDs.Espionage,
                target
            );
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double score = scorer.Score(context, proposal);
            double upperBound = scorer.GetScoreUpperBound(context, proposal);

            Assert.GreaterOrEqual(upperBound, score);
        }

        [Test]
        public void GetScoreUpperBound_DistantTarget_ReturnsLowerBoundThanLocalTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet localTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "local-target",
                rebels.InstanceID
            );
            Planet distantTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "distant-target",
                rebels.InstanceID
            );
            origin.PositionX = 0;
            origin.PositionY = 0;
            localTarget.PositionX = 0;
            localTarget.PositionY = 0;
            distantTarget.PositionX = 100;
            distantTarget.PositionY = 0;
            localTarget.AddVisitor(empire.InstanceID);
            distantTarget.AddVisitor(empire.InstanceID);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.Espionage);
            game.AttachNode(participant, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double localUpperBound = scorer.GetScoreUpperBound(
                context,
                new AIMissionProposal(new[] { participant }, MissionTypeIDs.Espionage, localTarget)
            );
            double distantUpperBound = scorer.GetScoreUpperBound(
                context,
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Espionage,
                    distantTarget
                )
            );

            Assert.Less(distantUpperBound, localUpperBound);
        }

        [Test]
        public void Score_ReconnaissanceProposal_IgnoresParticipantRating()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            SpecialForces lowRatedProbe = AITestSceneBuilder.CreateSpecialForces(
                "low-probe",
                empire.InstanceID
            );
            SpecialForces highRatedProbe = AITestSceneBuilder.CreateSpecialForces(
                "high-probe",
                empire.InstanceID
            );
            lowRatedProbe.Ratings[OfficerRating.Espionage] = 1;
            highRatedProbe.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(lowRatedProbe, origin);
            game.AttachNode(highRatedProbe, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double lowRatedScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { lowRatedProbe },
                    MissionTypeIDs.Reconnaissance,
                    target
                )
            );
            double highRatedScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { highRatedProbe },
                    MissionTypeIDs.Reconnaissance,
                    target
                )
            );

            Assert.AreEqual(lowRatedScore, highRatedScore);
        }

        [Test]
        public void Score_MultipleMainParticipants_UsesCombinedMissionSuccessProbability()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.AddVisitor(empire.InstanceID);
            SpecialForces firstParticipant = AITestSceneBuilder.CreateSpecialForces(
                "first",
                empire.InstanceID
            );
            SpecialForces secondParticipant = AITestSceneBuilder.CreateSpecialForces(
                "second",
                empire.InstanceID
            );
            firstParticipant.AllowedMissionTypeIDs.Add(MissionTypeIDs.Espionage);
            secondParticipant.AllowedMissionTypeIDs.Add(MissionTypeIDs.Espionage);
            firstParticipant.Ratings[OfficerRating.Espionage] = 0;
            secondParticipant.Ratings[OfficerRating.Espionage] = 0;
            game.AttachNode(firstParticipant, origin);
            game.AttachNode(secondParticipant, origin);
            game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { 0, 50 },
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double singleParticipantScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { firstParticipant }, MissionTypeIDs.Espionage, target)
            );
            double multipleParticipantScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { firstParticipant, secondParticipant },
                    MissionTypeIDs.Espionage,
                    target
                )
            );

            Assert.Greater(multipleParticipantScore, singleParticipantScore);
        }

        [Test]
        public void Score_MissionWithDistantDecoy_UsesFarthestParticipantTravelDistance()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet distantOrigin = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "distant-origin",
                empire.InstanceID,
                positionX: 600
            );
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target",
                rebels.InstanceID,
                positionX: 100
            );
            target.AddVisitor(empire.InstanceID);
            Officer mainParticipant = EntityFactory.CreateOfficer("main", empire.InstanceID);
            Officer nearDecoy = EntityFactory.CreateOfficer("near-decoy", empire.InstanceID);
            Officer distantDecoy = EntityFactory.CreateOfficer("distant-decoy", empire.InstanceID);
            mainParticipant.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(mainParticipant, origin);
            game.AttachNode(nearDecoy, origin);
            game.AttachNode(distantDecoy, distantOrigin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double nearScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { mainParticipant },
                    MissionTypeIDs.Espionage,
                    target,
                    decoyParticipants: new[] { nearDecoy }
                )
            );
            double distantScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { mainParticipant },
                    MissionTypeIDs.Espionage,
                    target,
                    decoyParticipants: new[] { distantDecoy }
                )
            );

            Assert.Greater(nearScore, distantScore);
        }

        [Test]
        public void Score_HostileMissionWithEffectiveDecoy_ReturnsHigherScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.AddVisitor(empire.InstanceID);
            Regiment detector = AITestSceneBuilder.CreateRegiment("detector", rebels.InstanceID);
            detector.DetectionRating = 50;
            game.AttachNode(detector, target);
            Officer participant = EntityFactory.CreateOfficer("participant", empire.InstanceID);
            Officer decoy = EntityFactory.CreateOfficer("decoy", empire.InstanceID);
            participant.Ratings[OfficerRating.Espionage] = 50;
            decoy.Ratings[OfficerRating.Espionage] = 50;
            game.AttachNode(participant, origin);
            game.AttachNode(decoy, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { -1000, 50 },
            };
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int> { { -1000, 80 } };
            game.Config.ProbabilityTables.Mission.PlanetaryDecoy = new Dictionary<int, int>
            {
                { -1000, 75 },
            };
            game.Config.AI.MissionPlanning.Utility.Objective.FoilRisk.Weight = 0.1;
            game.Config.AI.MissionPlanning.MinimumMissionScore = 0;
            game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 100;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double soloScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { participant }, MissionTypeIDs.Espionage, target)
            );
            double decoyedScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Espionage,
                    target,
                    decoyParticipants: new[] { decoy }
                )
            );

            Assert.Greater(decoyedScore, soloScore);
        }

        [Test]
        public void Score_OfficerMissionAbovePersonnelLossLimit_RemainsAvailableForDecoy()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Regiment detector = AITestSceneBuilder.CreateRegiment("detector", rebels.InstanceID);
            detector.DetectionRating = 100;
            game.AttachNode(detector, target);
            Officer participant = EntityFactory.CreateOfficer("participant", empire.InstanceID);
            participant.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(participant, origin);
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

            double score = new AIMissionProposalScorer().Score(
                context,
                new AIMissionProposal(new[] { participant }, MissionTypeIDs.Espionage, target)
            );

            Assert.Greater(score, 0);
        }

        [Test]
        public void Score_SpecialForcesMissionAbovePersonnelLossLimit_RemainsSelectable()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Regiment detector = AITestSceneBuilder.CreateRegiment("detector", rebels.InstanceID);
            detector.DetectionRating = 100;
            game.AttachNode(detector, target);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            participant.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 0;
            game.Config.AI.MissionPlanning.Utility.Objective.FoilRisk.Weight = 0;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double score = new AIMissionProposalScorer().Score(
                context,
                new AIMissionProposal(new[] { participant }, MissionTypeIDs.Espionage, target)
            );

            Assert.Greater(score, 0);
        }

        [Test]
        public void Score_SabotageProposal_AddsTargetPriorityBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.MissionPlanning.MinimumSabotageSuccessPercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Building firstShield = AddShield(game, target, "shield-1", rebels.InstanceID);
            AddShield(game, target, "shield-2", rebels.InstanceID);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            Building shipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet attackFleet = EntityFactory.CreateFleet("attack-fleet", empire.InstanceID);
            attackFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(attackFleet, origin);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            participant.Ratings[OfficerRating.Combat] = 60;
            game.AttachNode(participant, origin);
            game.Config.AI.MissionPlanning.Utility.Sabotage.Shield.Weight = 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double shieldScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Sabotage,
                    target,
                    selectedTarget: firstShield
                )
            );
            double shipyardScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Sabotage,
                    target,
                    selectedTarget: shipyard
                )
            );

            Assert.Greater(shieldScore, shipyardScore);
        }

        [Test]
        public void Score_SabotageProposal_UsesTacticalTargetPriorityOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.MissionPlanning.MinimumSabotageSuccessPercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(rebels.InstanceID, 40);
            target.SetPopularSupport(empire.InstanceID, 60);

            Building defense = AITestSceneBuilder.CreateBuildingTemplate(
                "defense",
                BuildingType.Weapon
            );
            defense.OwnerInstanceID = rebels.InstanceID;
            game.AttachNode(defense, target);
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", rebels.InstanceID);
            game.AttachNode(regiment, target);
            Starfighter starfighter = AITestSceneBuilder.CreateStarfighter(
                "starfighter",
                rebels.InstanceID
            );
            game.AttachNode(starfighter, target);
            Building shipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet attackFleet = EntityFactory.CreateFleet("attack-fleet", empire.InstanceID);
            attackFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(attackFleet, origin);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                empire.InstanceID
            );
            participant.Ratings[OfficerRating.Combat] = 60;
            game.AttachNode(participant, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double defenseScore = ScoreSabotage(scorer, context, participant, target, defense);
            double regimentScore = ScoreSabotage(scorer, context, participant, target, regiment);
            double starfighterScore = ScoreSabotage(
                scorer,
                context,
                participant,
                target,
                starfighter
            );
            double shipyardScore = ScoreSabotage(scorer, context, participant, target, shipyard);

            Assert.Greater(defenseScore, regimentScore);
            Assert.Greater(regimentScore, starfighterScore);
            Assert.Greater(starfighterScore, shipyardScore);
        }

        [TestCase(BuildingType.Shipyard)]
        [TestCase(BuildingType.TrainingFacility)]
        [TestCase(BuildingType.ConstructionFacility)]
        public void Score_SabotageProductionFacility_ExceedsEconomicFacility(BuildingType type)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Building production = AITestSceneBuilder.CreateBuildingTemplate("production", type);
            Building mine = AITestSceneBuilder.CreateBuildingTemplate("mine", BuildingType.Mine);
            production.OwnerInstanceID = rebels.InstanceID;
            mine.OwnerInstanceID = rebels.InstanceID;
            game.AttachNode(production, target);
            game.AttachNode(mine, target);
            SpecialForces saboteur = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                empire.InstanceID
            );
            saboteur.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            saboteur.Ratings[SkillRating.Espionage] = 100;
            game.AttachNode(saboteur, origin);
            game.Config.AI.MissionPlanning.Utility.Sabotage.Infrastructure.Weight = 1;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double productionScore = ScoreSabotage(scorer, context, saboteur, target, production);
            double economicScore = ScoreSabotage(scorer, context, saboteur, target, mine);

            Assert.Greater(productionScore, economicScore);
        }

        [Test]
        public void Score_SabotageProposal_WithDestabilizingRegiment_PrioritizesTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet favored = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "favored",
                rebels.InstanceID
            );
            Planet unfavored = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "unfavored",
                rebels.InstanceID
            );
            favored.SetPopularSupport(rebels.InstanceID, 0);
            favored.SetPopularSupport(empire.InstanceID, 100);
            unfavored.SetPopularSupport(rebels.InstanceID, 60);
            unfavored.SetPopularSupport(empire.InstanceID, 40);
            Regiment favoredRegiment = AITestSceneBuilder.CreateRegiment(
                "favored-regiment",
                rebels.InstanceID
            );
            Regiment unfavoredRegiment = AITestSceneBuilder.CreateRegiment(
                "unfavored-regiment",
                rebels.InstanceID
            );
            Regiment favoredReserve = AITestSceneBuilder.CreateRegiment(
                "favored-reserve",
                rebels.InstanceID
            );
            game.AttachNode(favoredRegiment, favored);
            game.Config.AI.Garrison.GarrisonDivisor = 5;
            int favoredRequirement = UprisingQueries.CalculateGarrisonRequirement(
                favored,
                rebels,
                game.Config.AI.Garrison
            );
            Assert.AreEqual(12, favoredRequirement);
            for (int index = 1; index < favoredRequirement; index++)
            {
                game.AttachNode(
                    AITestSceneBuilder.CreateRegiment(
                        $"favored-regiment-{index}",
                        rebels.InstanceID
                    ),
                    favored
                );
            }
            game.AttachNode(unfavoredRegiment, unfavored);
            game.Config.AI.MissionPlanning.Utility.Sabotage.FavoredSupportRegiment.Weight = 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            double favoredScore = AIMissionProposalScorer.GetSabotageTargetValue(
                context,
                favored,
                favoredRegiment
            );
            double unfavoredScore = AIMissionProposalScorer.GetSabotageTargetValue(
                context,
                unfavored,
                unfavoredRegiment
            );

            Assert.Greater(favoredScore, unfavoredScore);

            game.AttachNode(favoredReserve, favored);
            context = AITestSceneBuilder.CreateContext(game, empire);
            double reinforcedScore = AIMissionProposalScorer.GetSabotageTargetValue(
                context,
                favored,
                favoredRegiment
            );

            Assert.AreEqual(unfavoredScore, reinforcedScore, 0.000001);
        }

        [Test]
        public void Score_TargetedOfficerMission_WithWeakerTarget_ReturnsHigherScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet enemyPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy",
                rebels.InstanceID
            );
            enemyPlanet.AddVisitor(empire.InstanceID);
            Officer actor = EntityFactory.CreateOfficer("actor", empire.InstanceID);
            actor.Ratings[OfficerRating.Combat] = 100;
            Officer weakTarget = EntityFactory.CreateOfficer("weak", rebels.InstanceID);
            weakTarget.Ratings[OfficerRating.Combat] = 10;
            Officer strongTarget = EntityFactory.CreateOfficer("strong", rebels.InstanceID);
            strongTarget.Ratings[OfficerRating.Combat] = 90;
            game.AttachNode(actor, origin);
            game.AttachNode(weakTarget, enemyPlanet);
            game.AttachNode(strongTarget, enemyPlanet);
            AITestSceneBuilder.RevealPlanet(game, empire, enemyPlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double weakTargetScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { actor },
                    MissionTypeIDs.Abduction,
                    enemyPlanet,
                    selectedTarget: weakTarget,
                    targetOfficer: weakTarget
                )
            );
            double strongTargetScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { actor },
                    MissionTypeIDs.Abduction,
                    enemyPlanet,
                    selectedTarget: strongTarget,
                    targetOfficer: strongTarget
                )
            );

            Assert.Greater(weakTargetScore, strongTargetScore);
        }

        [Test]
        public void Score_HostileMissionWithSpecialForcesTechnology_PrefersSpecialForces()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Building sabotageTarget = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Combat] = 100;
            officer.Ratings[OfficerRating.Espionage] = 100;
            SpecialForces specialForces = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                empire.InstanceID
            );
            specialForces.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            specialForces.Ratings[OfficerRating.Combat] = 100;
            specialForces.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(officer, origin);
            game.AttachNode(specialForces, origin);
            empire.RebuildResearchCatalog(new IManufacturable[] { specialForces });
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double officerScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { officer },
                    MissionTypeIDs.Sabotage,
                    target,
                    selectedTarget: sabotageTarget
                )
            );
            double specialForcesScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { specialForces },
                    MissionTypeIDs.Sabotage,
                    target,
                    selectedTarget: sabotageTarget
                )
            );

            Assert.Greater(specialForcesScore, officerScore);
        }

        [Test]
        public void Score_EnemyEspionageWithSpecialForcesTechnology_PrefersSpecialForces()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.AddVisitor(empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Espionage] = 100;
            SpecialForces specialForces = AITestSceneBuilder.CreateSpecialForces(
                "spy",
                empire.InstanceID
            );
            specialForces.AllowedMissionTypeIDs.Add(MissionTypeIDs.Espionage);
            specialForces.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(officer, origin);
            game.AttachNode(specialForces, origin);
            empire.RebuildResearchCatalog(new IManufacturable[] { specialForces });
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double officerScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Espionage, target)
            );
            double specialForcesScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { specialForces }, MissionTypeIDs.Espionage, target)
            );

            Assert.Greater(specialForcesScore, officerScore);
        }

        [Test]
        public void Score_MobileHeadquartersEspionageAgentTargetingHeadquarters_PrioritizesMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            headquarters.AddVisitor(empire.InstanceID);
            target.AddVisitor(empire.InstanceID);
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            empire.Settings.Headquarters.IsMobile = true;
            SpecialForces headquartersAgent = AITestSceneBuilder.CreateSpecialForces(
                "headquarters-agent",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            headquartersAgent.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(headquartersAgent, headquarters);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double headquartersScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { headquartersAgent },
                    MissionTypeIDs.Espionage,
                    headquarters
                )
            );
            double remoteScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { headquartersAgent }, MissionTypeIDs.Espionage, target)
            );

            Assert.Greater(headquartersScore, remoteScore);
        }

        [Test]
        public void Score_MobileHeadquartersEspionageAgentTargetingOtherPlanet_DoesNotAddHeadquartersPriority()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            Planet otherOrigin = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "other-origin",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.AddVisitor(empire.InstanceID);
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            empire.Settings.Headquarters.IsMobile = true;
            SpecialForces headquartersAgent = AITestSceneBuilder.CreateSpecialForces(
                "headquarters-agent",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            SpecialForces otherAgent = AITestSceneBuilder.CreateSpecialForces(
                "other-agent",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            headquartersAgent.Ratings[OfficerRating.Espionage] = 100;
            otherAgent.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(headquartersAgent, headquarters);
            game.AttachNode(otherAgent, otherOrigin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double headquartersAgentScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { headquartersAgent }, MissionTypeIDs.Espionage, target)
            );
            double otherAgentScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { otherAgent }, MissionTypeIDs.Espionage, target)
            );

            Assert.AreEqual(otherAgentScore, headquartersAgentScore, 0.000001);
        }

        [Test]
        public void Score_FixedHeadquartersEspionageAgent_DoesNotAddMobilePriority()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            Planet otherOrigin = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "other-origin",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.AddVisitor(empire.InstanceID);
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            empire.Settings.Headquarters.IsMobile = false;
            SpecialForces headquartersAgent = AITestSceneBuilder.CreateSpecialForces(
                "headquarters-agent",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            SpecialForces otherAgent = AITestSceneBuilder.CreateSpecialForces(
                "other-agent",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            headquartersAgent.Ratings[OfficerRating.Espionage] = 100;
            otherAgent.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(headquartersAgent, headquarters);
            game.AttachNode(otherAgent, otherOrigin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double headquartersScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { headquartersAgent }, MissionTypeIDs.Espionage, target)
            );
            double otherScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { otherAgent }, MissionTypeIDs.Espionage, target)
            );

            Assert.AreEqual(otherScore, headquartersScore, 0.000001);
        }

        [Test]
        public void Score_StaleAttackTargetEspionage_PrioritizesAttackTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet attackTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "attack-target",
                rebels.InstanceID
            );
            Planet otherTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "other-target",
                rebels.InstanceID
            );
            Officer spy = EntityFactory.CreateOfficer("spy", empire.InstanceID);
            spy.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(spy, origin);
            Fleet fleet = EntityFactory.CreateFleet("attack-fleet", empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = attackTarget.InstanceID,
            };
            game.AttachNode(fleet, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, attackTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, otherTarget);
            game.CurrentTick = game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double attackTargetScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { spy },
                    MissionTypeIDs.Espionage,
                    context.Assessment.GetKnownPlanet(attackTarget.InstanceID)
                )
            );
            double otherTargetScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { spy },
                    MissionTypeIDs.Espionage,
                    context.Assessment.GetKnownPlanet(otherTarget.InstanceID)
                )
            );

            Assert.Greater(attackTargetScore, otherTargetScore);
        }

        /// <summary>
        /// Creates a configurable group of diplomats beside an unclaimed sector.
        /// </summary>
        /// <param name="diplomatCount">The number of idle diplomats.</param>
        /// <param name="targetCount">The number of neutral target worlds.</param>
        /// <param name="rating">Each diplomat's skill rating.</param>
        /// <param name="originX">The diplomats' horizontal location.</param>
        /// <param name="revealed">Whether the target worlds are known.</param>
        /// <param name="support">The faction's initial popular support on target worlds.</param>
        /// <returns>The turn context, one diplomat, and one target.</returns>
        private static (
            AITurnContext Context,
            Officer Officer,
            Planet Target
        ) CreateDiplomacyConcentrationContext(
            int diplomatCount = 3,
            int targetCount = 3,
            int rating = 90,
            int originX = 0,
            bool revealed = true,
            int support = 40
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(
                out Faction faction,
                out Faction opponent
            );
            GameConfig.AIDiplomacyUtilityConfig utility = game.Config
                .AI
                .MissionPlanning
                .Utility
                .Diplomacy;
            PlanetSector originSector = AITestSceneBuilder.AddSector(game, "origin-sector");
            Planet origin = AITestSceneBuilder.AddPlanet(
                game,
                originSector,
                "origin",
                faction.InstanceID,
                positionX: originX
            );
            origin.SetPopularSupport(faction.InstanceID, 100);
            PlanetSector targetSector = AITestSceneBuilder.AddSector(game, "target-sector");
            targetSector.SectorType = PlanetSectorType.OuterRim;
            Planet target = null;
            for (int index = 0; index < targetCount; index++)
            {
                Planet candidate = AITestSceneBuilder.AddPlanet(
                    game,
                    targetSector,
                    "target-" + index,
                    null,
                    positionX: 30 + index
                );
                candidate.SetPopularSupport(faction.InstanceID, support);
                candidate.SetPopularSupport(opponent.InstanceID, 100 - support);
                if (revealed)
                {
                    candidate.AddVisitor(faction.InstanceID);
                    AITestSceneBuilder.RevealPlanet(game, faction, candidate);
                }
                target ??= candidate;
            }
            Officer officer = null;
            for (int index = 0; index < diplomatCount; index++)
            {
                Officer candidate = EntityFactory.CreateOfficer(
                    "diplomat-" + index,
                    faction.InstanceID
                );
                candidate.Ratings[OfficerRating.Diplomacy] = rating;
                game.AttachNode(candidate, origin);
                officer ??= candidate;
            }
            return (AITestSceneBuilder.CreateContext(game, faction), officer, target);
        }

        /// <summary>
        /// Creates a distributed diplomacy group with competing neutral-sector opportunities.
        /// </summary>
        /// <param name="nearbyRivals">Strong rivals in the sector nearest most of the group.</param>
        /// <param name="alternativeRivals">Strong rivals in the other nearby sector.</param>
        /// <returns>The acting turn and three candidate sectors.</returns>
        private static (
            AITurnContext Context,
            PlanetSector[] Sectors
        ) CreateDiplomacyGroupCompetitionContext(int nearbyRivals, int alternativeRivals)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            game.Config.Movement.DistanceDivisor = 1;
            game.Config.Movement.DefaultPersonnelHyperdrive = 100;
            PlanetSector home = AITestSceneBuilder.AddSector(game, "home");
            Planet origin = AITestSceneBuilder.AddPlanet(game, home, "origin", faction.InstanceID);
            Planet alternativeOrigin = AITestSceneBuilder.AddPlanet(
                game,
                home,
                "alternative-origin",
                faction.InstanceID,
                positionX: 50
            );
            origin.SetPopularSupport(faction.InstanceID, 100);
            alternativeOrigin.SetPopularSupport(faction.InstanceID, 100);
            for (int index = 0; index < 4; index++)
            {
                Officer officer = EntityFactory.CreateOfficer(
                    "diplomat-" + index,
                    faction.InstanceID
                );
                officer.SetBaseRating(SkillRating.Diplomacy, 90);
                game.AttachNode(officer, index < 3 ? origin : alternativeOrigin);
            }

            int[] distances = { 20, 50, 10000 };
            int[] rivals = { nearbyRivals, alternativeRivals, 0 };
            PlanetSector[] sectors = new PlanetSector[distances.Length];
            for (int sectorIndex = 0; sectorIndex < sectors.Length; sectorIndex++)
            {
                PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector-" + sectorIndex);
                sector.SectorType = PlanetSectorType.Core;
                sectors[sectorIndex] = sector;
                for (int targetIndex = 0; targetIndex < 4; targetIndex++)
                {
                    Planet target = AITestSceneBuilder.AddPlanet(
                        game,
                        sector,
                        $"target-{sectorIndex}-{targetIndex}",
                        null,
                        positionX: distances[sectorIndex] + targetIndex
                    );
                    target.SetPopularSupport(faction.InstanceID, 40);
                    target.SetPopularSupport(enemy.InstanceID, 60);
                    target.AddVisitor(faction.InstanceID);
                    AITestSceneBuilder.RevealPlanet(game, faction, target);
                }
                Planet rivalBase = AITestSceneBuilder.AddPlanet(
                    game,
                    sector,
                    "base-" + sectorIndex,
                    enemy.InstanceID,
                    positionX: distances[sectorIndex]
                );
                for (int rivalIndex = 0; rivalIndex < rivals[sectorIndex]; rivalIndex++)
                {
                    Officer rival = EntityFactory.CreateOfficer(
                        $"rival-{sectorIndex}-{rivalIndex}",
                        enemy.InstanceID
                    );
                    rival.SetBaseRating(SkillRating.Diplomacy, 90);
                    game.AttachNode(rival, rivalBase);
                }
                AITestSceneBuilder.RevealPlanet(game, faction, rivalBase);
            }
            return (AITestSceneBuilder.CreateContext(game, faction), sectors);
        }

        /// <summary>
        /// Creates comparable expansion opportunities with known rival diplomats and different travel times.
        /// </summary>
        /// <param name="nearbyRivals">Rivals in the nearest sector.</param>
        /// <param name="alternativeRivals">Rivals in the next-nearest sector.</param>
        /// <returns>The acting turn, diplomat, and three neutral target planets.</returns>
        private static (
            AITurnContext Context,
            Officer Officer,
            Planet[] Targets
        ) CreateDiplomacyCompetitionContext(int nearbyRivals, int alternativeRivals)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            game.Config.Movement.DistanceDivisor = 1;
            game.Config.Movement.DefaultPersonnelHyperdrive = 100;
            GameConfig.AIDiplomacyUtilityConfig tuning = game.Config
                .AI
                .MissionPlanning
                .Utility
                .Diplomacy;
            game.Config.AI.MissionPlanning.Utility.Diplomacy =
                new GameConfig.AIDiplomacyUtilityConfig
                {
                    CoreWorld = new GameConfig.AIConsiderationConfig { Weight = 0.2 },
                    Competition = tuning.Competition,
                    TravelTime = tuning.TravelTime,
                };
            PlanetSector home = AITestSceneBuilder.AddSector(game, "home");
            Planet origin = AITestSceneBuilder.AddPlanet(game, home, "origin", faction.InstanceID);
            Officer diplomat = EntityFactory.CreateOfficer("diplomat", faction.InstanceID);
            diplomat.SetBaseRating(SkillRating.Diplomacy, 90);
            game.AttachNode(diplomat, origin);
            int[] distances = { 40, 60, 1000 };
            int[] rivals = { nearbyRivals, alternativeRivals, 0 };
            Planet[] targets = new Planet[3];
            for (int index = 0; index < targets.Length; index++)
            {
                PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector-" + index);
                sector.SectorType = PlanetSectorType.Core;
                Planet target = AITestSceneBuilder.AddPlanet(
                    game,
                    sector,
                    "target-" + index,
                    null,
                    positionX: distances[index]
                );
                target.SetPopularSupport(faction.InstanceID, 40);
                target.SetPopularSupport(enemy.InstanceID, 60);
                Planet rivalBase = AITestSceneBuilder.AddPlanet(
                    game,
                    sector,
                    "base-" + index,
                    enemy.InstanceID
                );
                for (int rivalIndex = 0; rivalIndex < rivals[index]; rivalIndex++)
                {
                    Officer rival = EntityFactory.CreateOfficer(
                        $"rival-{index}-{rivalIndex}",
                        enemy.InstanceID
                    );
                    rival.SetBaseRating(SkillRating.Diplomacy, 90);
                    game.AttachNode(rival, rivalBase);
                }
                target.AddVisitor(faction.InstanceID);
                AITestSceneBuilder.RevealPlanet(game, faction, target);
                AITestSceneBuilder.RevealPlanet(game, faction, rivalBase);
                targets[index] = target;
            }
            return (AITestSceneBuilder.CreateContext(game, faction), diplomat, targets);
        }

        /// <summary>
        /// Creates otherwise equivalent diplomacy targets inside and outside the headquarters sector.
        /// </summary>
        /// <param name="useEmpire">Whether the acting faction is the Empire.</param>
        /// <param name="ownedTargets">Whether both target planets belong to the acting faction.</param>
        /// <param name="hasHeadquarters">Whether the faction has an active headquarters.</param>
        /// <returns>The turn context, diplomat, and comparable targets.</returns>
        private static (
            AITurnContext Context,
            Officer Diplomat,
            Planet HomeTarget,
            Planet OtherTarget
        ) CreateHeadquartersDiplomacyContext(
            bool useEmpire,
            bool ownedTargets,
            bool hasHeadquarters
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.MissionPlanning.Utility.Diplomacy.SystemPresence.Weight = 0;
            game.Config.AI.MissionPlanning.Utility.Diplomacy.TravelTime.Weight = 0;
            game.Config.AI.MissionPlanning.Utility.Diplomacy.Competition.Weight = 0;
            Faction faction = useEmpire ? empire : rebels;
            Faction opposingFaction = useEmpire ? rebels : empire;
            PlanetSector homeSector = AITestSceneBuilder.AddSector(game, "home-sector");
            PlanetSector otherSector = AITestSceneBuilder.AddSector(game, "other-sector");
            homeSector.SectorType = PlanetSectorType.Core;
            otherSector.SectorType = PlanetSectorType.Core;
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                homeSector,
                "headquarters",
                faction.InstanceID
            );
            headquarters.SetPopularSupport(faction.InstanceID, 100);
            faction.HQInstanceID = hasHeadquarters ? headquarters.InstanceID : null;
            string targetOwner = ownedTargets ? faction.InstanceID : null;
            Planet homeTarget = AITestSceneBuilder.AddPlanet(
                game,
                homeSector,
                "home-target",
                targetOwner
            );
            Planet otherTarget = AITestSceneBuilder.AddPlanet(
                game,
                otherSector,
                "other-target",
                targetOwner
            );
            foreach (Planet target in new[] { homeTarget, otherTarget })
            {
                target.SetPopularSupport(faction.InstanceID, 50);
                target.SetPopularSupport(opposingFaction.InstanceID, 50);
                target.AddVisitor(faction.InstanceID);
            }
            Officer diplomat = EntityFactory.CreateOfficer("diplomat", faction.InstanceID);
            diplomat.Ratings[OfficerRating.Diplomacy] = 100;
            game.AttachNode(diplomat, headquarters);
            return (
                AITestSceneBuilder.CreateContext(game, faction),
                diplomat,
                homeTarget,
                otherTarget
            );
        }

        /// <summary>
        /// Adds shield.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <returns>The result of add shield.</returns>
        private static Building AddShield(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId
        )
        {
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                instanceId,
                BuildingType.Defense
            );
            shield.OwnerInstanceID = ownerInstanceId;
            shield.ShieldStrength = 10;
            shield.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(shield, planet);
            return shield;
        }

        /// <summary>
        /// Executes score sabotage.
        /// </summary>
        /// <param name="scorer">The scorer.</param>
        /// <param name="context">The context.</param>
        /// <param name="participant">The participant.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="target">The target.</param>
        /// <returns>The result of score sabotage.</returns>
        private static double ScoreSabotage(
            AIMissionProposalScorer scorer,
            AITurnContext context,
            IMissionParticipant participant,
            Planet planet,
            IManufacturable target
        )
        {
            if (
                participant is SpecialForces specialForces
                && !specialForces.AllowedMissionTypeIDs.Contains(MissionTypeIDs.Sabotage)
            )
                specialForces.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);

            return scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Sabotage,
                    planet,
                    selectedTarget: target
                )
            );
        }
    }
}
