using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIMaintenanceRecoveryProposalTests
    {
        private GameRoot _game;
        private Faction _faction;
        private Planet _planet;
        private AITurnContext _context;

        /// <summary>
        /// Creates a faction whose effective deficit is smaller than its base deficit.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = AITestSceneBuilder.CreateGame(out _faction, out Faction _);
            _game.Summary = new GameSummary { Difficulty = GameDifficulty.Hard };
            _game.SetFactionController(_faction.InstanceID, "ai", PlayerControllerType.AI);
            _game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MaintenanceCapacityPercent = 125,
            };
            _game.Config.AI.Selection.MaintenanceRecovery.Enabled = true;
            _game.Config.AI.Selection.MaintenanceRecovery.TargetHeadroom = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(_game, "sector");
            _planet = AITestSceneBuilder.AddPlanet(
                _game,
                sector,
                "planet",
                _faction.InstanceID,
                rawResourceNodes: 2
            );
            AddResourcePair("initial");
            for (int index = 0; index < 4; index++)
            {
                Regiment regiment = AITestSceneBuilder.CreateRegiment(
                    "regiment-" + index,
                    _faction.InstanceID
                );
                regiment.MaintenanceCost = 20;
                _game.AttachNode(regiment, _planet);
            }
            _context = AITestSceneBuilder.CreateContext(_game, _faction);
        }

        /// <summary>
        /// Revalidates effective solvency even when the assessment still records a deficit.
        /// </summary>
        [Test]
        public void CanSelect_HardBonusCoversLiveCosts_ReturnsFalse()
        {
            AIMaintenanceRecoveryProposal proposal = CreateProposal();
            _game.DeleteNode(_planet.GetChildren<Regiment>().First());

            Assert.AreEqual(-10, _faction.ProjectedMaintenanceHeadroom);
            Assert.IsFalse(proposal.CanSelect(_context));
        }

        /// <summary>
        /// Retires only enough units to cover the effective deficit.
        /// </summary>
        [Test]
        public void Execute_HardBonusReducesDeficit_RetiresOneUnit()
        {
            AIMaintenanceRecoveryProposal proposal = SelectProposal();

            proposal.Execute(_context);

            Assert.AreEqual(3, _planet.GetChildren<Regiment>().Count());
        }

        /// <summary>
        /// Includes unfinished production in the effective maintenance deficit.
        /// </summary>
        [Test]
        public void Execute_UnfinishedOrders_ReservesTheirMaintenance()
        {
            Regiment unfinished = AITestSceneBuilder.CreateRegiment(
                "unfinished",
                _faction.InstanceID
            );
            unfinished.MaintenanceCost = 20;
            unfinished.ManufacturingStatus = ManufacturingStatus.Building;
            _game.AttachNode(unfinished, _planet);
            _context = AITestSceneBuilder.CreateContext(_game, _faction);
            AIMaintenanceRecoveryProposal proposal = SelectProposal();

            proposal.Execute(_context);

            Assert.AreEqual(
                2,
                _planet
                    .GetChildren<Regiment>()
                    .Count(unit => unit.ManufacturingStatus == ManufacturingStatus.Complete)
            );
            Assert.AreSame(unfinished, _game.GetSceneNodeByInstanceID<Regiment>("unfinished"));
        }

        /// <summary>
        /// Keeps the base budget when the faction has no maintenance bonus.
        /// </summary>
        /// <param name="difficulty">The active difficulty.</param>
        /// <param name="controller">The faction controller type.</param>
        [TestCase(GameDifficulty.Hard, PlayerControllerType.Human)]
        [TestCase(GameDifficulty.Medium, PlayerControllerType.AI)]
        public void Execute_WithoutAiMaintenanceBonus_RetiresTwoUnits(
            GameDifficulty difficulty,
            PlayerControllerType controller
        )
        {
            _game.Summary.Difficulty = difficulty;
            _game.SetFactionController(_faction.InstanceID, "controller", controller);
            _context = AITestSceneBuilder.CreateContext(_game, _faction);
            AIMaintenanceRecoveryProposal proposal = SelectProposal();

            proposal.Execute(_context);

            Assert.AreEqual(2, _planet.GetChildren<Regiment>().Count());
        }

        /// <summary>
        /// Does not execute an obsolete recovery action after capacity increases.
        /// </summary>
        [Test]
        public void Execute_CapacityRecoversAfterSelection_RetiresNoUnits()
        {
            AIMaintenanceRecoveryProposal proposal = SelectProposal();
            AddResourcePair("recovered");

            proposal.Execute(_context);

            Assert.AreEqual(4, _planet.GetChildren<Regiment>().Count());
        }

        /// <summary>
        /// Stops after a scrap handler restores the live effective recovery target.
        /// </summary>
        [Test]
        public void Execute_CapacityRecoversDuringScrapping_StopsAfterFirstUnit()
        {
            _game.Config.AI.Selection.MaintenanceRecovery.TargetHeadroom = 30;
            AIMaintenanceRecoveryProposal proposal = SelectProposal();
            _context.Maintenance.ResultsProduced += _ => AddResourcePair("recovered");

            proposal.Execute(_context);

            Assert.AreEqual(3, _planet.GetChildren<Regiment>().Count());
        }

        /// <summary>
        /// Builds a recovery request through the planner's public contract.
        /// </summary>
        /// <returns>The current recovery request.</returns>
        private AIMaintenanceRecoveryProposal CreateProposal()
        {
            return new AIMaintenanceRecoveryPlanner()
                .Plan(_context)
                .OfType<AIMaintenanceRecoveryProposal>()
                .Single();
        }

        /// <summary>
        /// Resolves recovery through the public selection phase.
        /// </summary>
        /// <returns>The executable recovery action.</returns>
        private AIMaintenanceRecoveryProposal SelectProposal()
        {
            AIMaintenanceRecoveryProposal proposal = CreateProposal();
            proposal.SetScore(1);
            _context.AddProposal(proposal);
            return new AISelectionPhase()
                .Select(_context)
                .OfType<AIMaintenanceRecoveryProposal>()
                .Single();
        }

        /// <summary>
        /// Adds one operational resource pair without changing the committed unit costs.
        /// </summary>
        /// <param name="id">The unique suffix for both facilities.</param>
        private void AddResourcePair(string id)
        {
            AITestSceneBuilder.AddProductionFacility(
                _game,
                _planet,
                "mine-" + id,
                BuildingType.Mine,
                ManufacturingType.None
            );
            AITestSceneBuilder.AddProductionFacility(
                _game,
                _planet,
                "refinery-" + id,
                BuildingType.Refinery,
                ManufacturingType.None
            );
        }
    }
}
