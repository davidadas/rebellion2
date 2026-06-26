using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Game.Missions
{
    internal sealed class ReconnaissanceMissionBehavior : MissionBehavior
    {
        public override bool CanceledOnOwnershipChange => false;
        public override bool ImprovesParticipantRatings => false;

        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            Planet planet = RequirePlanet(request.Target);
            if (planet == null)
                return null;

            if (
                planet.GetOwnerInstanceID() == request.OwnerInstanceID
                || planet.WasVisitedBy(request.OwnerInstanceID)
            )
                return null;

            if (
                request.MainParticipants.Count > 0
                && !request
                    .MainParticipants.OfType<SpecialForces>()
                    .Any(sf => sf.AllowedMissionTypeIDs.Contains(MissionTypeIDs.Reconnaissance))
            )
                return null;

            return CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                planet,
                request.MainParticipants,
                request.DecoyParticipants
            );
        }

        public override bool IsMissionSatisfied(Mission mission, GameRoot game)
        {
            return mission.GetParent() is Planet planet
                && !planet.WasVisitedBy(mission.GetOwnerInstanceID());
        }

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Planet planet = mission.GetParent() as Planet;
            planet?.AddVisitor(mission.GetOwnerInstanceID());
            return new List<GameResult>();
        }
    }

    internal sealed class DiplomacyMissionBehavior : MissionBehavior
    {
        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            Planet planet = RequirePlanet(request.Target);
            if (planet == null)
                return null;

            if (
                !planet.IsColonized
                || planet.IsInUprising
                || !planet.WasVisitedBy(request.OwnerInstanceID)
                || planet.GetPopularSupport(request.OwnerInstanceID) >= 100
            )
                return null;

            string planetOwner = planet.GetOwnerInstanceID();
            if (planetOwner != null && planetOwner != request.OwnerInstanceID)
                return null;

            return CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                planet,
                request.MainParticipants,
                request.DecoyParticipants
            );
        }

        public override MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game)
        {
            if (mission.GetParent() is Planet planet)
            {
                if (planet.IsInUprising)
                    return MissionCompletionReason.Failure;

                string owner = planet.GetOwnerInstanceID();
                if (owner != null && owner != mission.OwnerInstanceID)
                    return MissionCompletionReason.Failure;
            }

            return null;
        }

        public override double GetFoilProbability(
            Mission mission,
            double defenseScore,
            GameRoot game
        ) => 0;

        public override double GetAgentProbability(
            Mission mission,
            IMissionParticipant agent,
            GameRoot game
        )
        {
            if (!(mission.GetParent() is Planet planet))
                return mission.GetDefaultAgentProbability(agent, game);

            int opposingSupport = planet.GetOpposingPopularSupport(mission.OwnerInstanceID);
            int score =
                GetTargetTroopState(planet)
                - opposingSupport
                + agent.GetEffectiveRating(OfficerRating.Diplomacy);
            return mission.LookupSuccessProbability(game, score);
        }

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Planet planet = mission.GetParent() as Planet;
            if (planet == null)
                return new List<GameResult>();

            GameConfig.SupportShiftConfig config = game.Config.SupportShift;
            int currentSupport = planet.GetPopularSupport(mission.OwnerInstanceID);
            int supportAfterExecuteBonus =
                currentSupport
                + GetFactionSuccessSupportBonus(
                    game,
                    mission.OwnerInstanceID,
                    config.DiplomacyCompletionSupportBonus
                );
            int supportShift = GetFactionSupportShift(
                planet,
                mission.OwnerInstanceID,
                config,
                provider
            );
            planet.SetPopularSupport(
                mission.OwnerInstanceID,
                supportAfterExecuteBonus + supportShift
            );

            return new List<GameResult>();
        }

        public override bool ShouldRepeatAfterCompletion(Mission mission, GameRoot game)
        {
            if (mission.GetParent() is Planet planet)
            {
                return (
                        planet.GetOwnerInstanceID() == mission.GetOwnerInstanceID()
                        || planet.GetOwnerInstanceID() == null
                    )
                    && planet.GetPopularSupport(mission.GetOwnerInstanceID()) < 100
                    && !planet.IsInUprising;
            }

            return false;
        }

        private static int GetFactionSuccessSupportBonus(
            GameRoot game,
            string ownerInstanceID,
            int bonus
        )
        {
            foreach (Faction faction in game.GetFactions())
            {
                if (faction.InstanceID == ownerInstanceID)
                    return faction.Settings.InvertSupportShift ? -bonus : bonus;
            }

            return bonus;
        }

        private static int GetFactionSupportShift(
            Planet planet,
            string ownerInstanceID,
            GameConfig.SupportShiftConfig config,
            IRandomNumberProvider provider
        )
        {
            string planetOwner = planet.GetOwnerInstanceID();
            if (planetOwner == ownerInstanceID)
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

        private static int RollSupportShift(
            int baseShift,
            int range,
            IRandomNumberProvider provider
        )
        {
            return baseShift + (range > 0 ? provider.NextInt(0, range + 1) : 0);
        }

        private static int GetTargetTroopState(Planet planet)
        {
            foreach (Regiment regiment in planet.GetAllRegiments())
            {
                if (regiment.ManufacturingStatus == ManufacturingStatus.Complete)
                    return regiment.UprisingDefense;
            }

            return 0;
        }
    }

    internal sealed class RecruitmentMissionBehavior : MissionBehavior
    {
        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            List<Officer> unrecruited = request.Game.GetUnrecruitedOfficers(
                request.OwnerInstanceID
            );
            bool areMainCharacters = request.MainParticipants.Any(o =>
                o is Officer { IsMain: true }
            );
            if (request.MainParticipants.Count > 0 && !areMainCharacters)
                return null;

            if (unrecruited.Count == 0 || request.Target == null)
                return null;

            return new Mission(
                definition,
                request.OwnerInstanceID,
                request.Target.GetInstanceID(),
                request.MainParticipants,
                request.DecoyParticipants
            );
        }

        public override bool IsMissionSatisfied(Mission mission, GameRoot game)
        {
            return game.GetUnrecruitedOfficers(mission.OwnerInstanceID).Count > 0;
        }

        public override double GetFoilProbability(
            Mission mission,
            double defenseScore,
            GameRoot game
        ) => 0;

        public override double GetAgentProbability(
            Mission mission,
            IMissionParticipant agent,
            GameRoot game
        )
        {
            if (!(mission.GetParent() is Planet planet))
                return mission.GetDefaultAgentProbability(agent, game);

            int opposingSupport = planet.GetOpposingPopularSupport(mission.OwnerInstanceID);
            int score = agent.GetEffectiveRating(OfficerRating.Leadership) - opposingSupport;
            return mission.LookupSuccessProbability(game, score);
        }

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Planet planet = mission.GetParent() as Planet;
            if (provider == null || planet == null)
                return new List<GameResult>();

            Officer target = game.GetUnrecruitedOfficers(mission.OwnerInstanceID)
                .RandomElement(provider);
            if (target == null)
                return new List<GameResult>();

            Faction faction = game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);
            mission.TargetOfficerInstanceID = target.InstanceID;
            target.OwnerInstanceID = mission.OwnerInstanceID;
            game.RemoveUnrecruitedOfficer(target);
            game.AttachNode(target, planet);

            GameLogger.Log($"Recruited {target.GetDisplayName()} to {mission.OwnerInstanceID}");

            return new List<GameResult>
            {
                new OfficerRecruitedResult
                {
                    Officer = target,
                    Faction = faction,
                    Planet = planet,
                    Tick = game.CurrentTick,
                },
            };
        }

        public override bool ShouldRepeatAfterCompletion(Mission mission, GameRoot game)
        {
            return game.GetUnrecruitedOfficers(mission.OwnerInstanceID).Count > 0;
        }
    }

    internal sealed class SubdueUprisingMissionBehavior : MissionBehavior
    {
        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            Planet planet = RequirePlanet(request.Target);
            if (planet == null)
                return null;

            if (planet.GetOwnerInstanceID() != request.OwnerInstanceID || !planet.IsInUprising)
                return null;

            return CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                planet,
                request.MainParticipants,
                request.DecoyParticipants
            );
        }

        public override MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game)
        {
            return mission.GetParent() is Planet p && p.IsInUprising
                ? null
                : MissionCompletionReason.Failure;
        }

        public override bool IsMissionSatisfied(Mission mission, GameRoot game)
        {
            return mission.GetParent() is Planet { IsInUprising: true };
        }

        public override double GetFoilProbability(
            Mission mission,
            double defenseScore,
            GameRoot game
        ) => 0;

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Planet planet = mission.GetParent() as Planet;
            planet.EndUprising();

            return new List<GameResult>
            {
                new PlanetUprisingEndedResult
                {
                    Planet = planet,
                    Faction = game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID),
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    internal sealed class InciteUprisingMissionBehavior : MissionBehavior
    {
        public override bool CanceledOnOwnershipChange => false;

        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            Planet planet = RequirePlanet(request.Target);
            if (planet == null)
                return null;

            string owner = planet.GetOwnerInstanceID();
            if (
                string.IsNullOrEmpty(owner)
                || owner == request.OwnerInstanceID
                || planet.IsInUprising
            )
                return null;

            return CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                planet,
                request.MainParticipants,
                request.DecoyParticipants
            );
        }

        public override MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game)
        {
            return mission.GetParent() is Planet p && p.IsInUprising
                ? MissionCompletionReason.Failure
                : null;
        }

        public override double GetAgentProbability(
            Mission mission,
            IMissionParticipant agent,
            GameRoot game
        )
        {
            if (!(mission.GetParent() is Planet planet))
                throw new InvalidOperationException(
                    "InciteUprising mission must be attached to a Planet."
                );

            int leadershipSkill = agent.GetEffectiveRating(OfficerRating.Leadership);
            int enemySupport = planet.GetPopularSupport(planet.OwnerInstanceID);

            int regimentStrength = 0;
            foreach (ISceneNode child in planet.GetChildren())
            {
                if (
                    child is Regiment regiment
                    && regiment.OwnerInstanceID != mission.OwnerInstanceID
                )
                    regimentStrength += regiment.DefenseRating;
            }

            int score = leadershipSkill - enemySupport - regimentStrength;
            return mission.LookupSuccessProbability(game, score);
        }

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Planet planet = mission.GetParent() as Planet;
            planet.BeginUprising();

            return new List<GameResult>
            {
                new PlanetUprisingStartedResult
                {
                    Planet = planet,
                    InstigatorFaction = game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID),
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    internal sealed class AbductionMissionBehavior : MissionBehavior
    {
        public override bool CanceledOnOwnershipChange => false;

        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            Planet planet = RequirePlanet(request.Target);
            if (planet == null)
                return null;

            Officer target = request.TargetOfficer;
            Planet targetPlanet = target?.GetParentOfType<Planet>();
            if (
                target == null
                || target.GetOwnerInstanceID() == request.OwnerInstanceID
                || target.IsCaptured
                || targetPlanet?.InstanceID != planet.InstanceID
            )
                return null;

            Mission mission = CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                planet,
                request.MainParticipants,
                request.DecoyParticipants
            );
            mission.TargetOfficerInstanceID = target.InstanceID;
            return mission;
        }

        public override MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game)
        {
            return HasValidTarget(mission, game) ? null : MissionCompletionReason.TargetUnavailable;
        }

        public override bool IsMissionSatisfied(Mission mission, GameRoot game)
        {
            return HasValidTarget(mission, game);
        }

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(
                mission.TargetOfficerInstanceID
            );
            if (target == null)
                return new List<GameResult>();

            target.IsCaptured = true;
            target.CaptorInstanceID = mission.OwnerInstanceID;
            target.CanEscape = true;

            return new List<GameResult>
            {
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = true,
                    Context = mission.GetParent() as Planet,
                    Tick = game.CurrentTick,
                },
            };
        }

        public override IEnumerable<IMovable> GetSuccessfulReturnPassengers(
            Mission mission,
            GameRoot game
        )
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(
                mission.TargetOfficerInstanceID
            );
            if (target?.IsCaptured == true && target.CaptorInstanceID == mission.OwnerInstanceID)
                yield return target;
        }

        private static bool HasValidTarget(Mission mission, GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(
                mission.TargetOfficerInstanceID
            );
            return target?.IsCaptured == false
                && target.GetParentOfType<Planet>() == mission.GetParent() as Planet;
        }
    }

    internal sealed class AssassinationMissionBehavior : MissionBehavior
    {
        public override bool CanceledOnOwnershipChange => false;

        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            Planet planet = RequirePlanet(request.Target);
            if (planet == null)
                return null;

            Officer target = request.TargetOfficer;
            Planet targetPlanet = target?.GetParentOfType<Planet>();
            if (
                target == null
                || target.GetOwnerInstanceID() == request.OwnerInstanceID
                || target.IsCaptured
                || target.IsKilled
                || targetPlanet?.InstanceID != planet.InstanceID
            )
                return null;

            Mission mission = CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                planet,
                request.MainParticipants,
                request.DecoyParticipants
            );
            mission.TargetOfficerInstanceID = target.InstanceID;
            return mission;
        }

        public override MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game)
        {
            return HasValidTarget(mission, game) ? null : MissionCompletionReason.TargetUnavailable;
        }

        public override bool IsMissionSatisfied(Mission mission, GameRoot game)
        {
            return HasValidTarget(mission, game);
        }

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(
                mission.TargetOfficerInstanceID
            );
            if (target == null)
                return new List<GameResult>();

            List<GameResult> results = new List<GameResult>();
            Planet planet = mission.GetParent() as Planet;

            int injury = RollInjury(game.Config.Assassination, provider);
            target.ApplyInjury(injury, game.Config.Recovery.MaxInjuryPoints);
            results.Add(
                new OfficerInjuredResult
                {
                    Officer = target,
                    Severity = injury,
                    Tick = game.CurrentTick,
                }
            );

            if (RollKillCheck(game.Config.Assassination, provider))
            {
                target.IsKilled = true;
                game.DetachNode(target);
                results.Add(
                    new OfficerKilledResult
                    {
                        TargetOfficer = target,
                        Assassin =
                            mission.MainParticipants.Count > 0 ? mission.MainParticipants[0] : null,
                        Context = planet,
                        Tick = game.CurrentTick,
                    }
                );
            }

            return results;
        }

        private static bool HasValidTarget(Mission mission, GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(
                mission.TargetOfficerInstanceID
            );
            return target?.IsKilled == false
                && !target.IsCaptured
                && target.GetParentOfType<Planet>() == mission.GetParent() as Planet;
        }

        private static int RollInjury(
            GameConfig.AssassinationConfig config,
            IRandomNumberProvider provider
        )
        {
            return config.BaseInjury
                + provider.NextInt(0, config.PrimaryInjuryRange + 1)
                + provider.NextInt(0, config.SecondaryInjuryRange + 1);
        }

        private static bool RollKillCheck(
            GameConfig.AssassinationConfig config,
            IRandomNumberProvider provider
        )
        {
            return provider.NextDouble() * 100 <= config.KillProbability;
        }
    }

    internal sealed class RescueMissionBehavior : MissionBehavior
    {
        public override bool CanceledOnOwnershipChange => false;

        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            Planet planet = RequirePlanet(request.Target);
            if (planet == null)
                return null;

            Officer target = request.TargetOfficer;
            Planet targetPlanet = target?.GetParentOfType<Planet>();
            if (
                target == null
                || target.GetOwnerInstanceID() != request.OwnerInstanceID
                || !target.IsCaptured
                || targetPlanet?.InstanceID != planet.InstanceID
            )
                return null;

            Mission mission = CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                planet,
                request.MainParticipants,
                request.DecoyParticipants
            );
            mission.TargetOfficerInstanceID = target.InstanceID;
            return mission;
        }

        public override MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game)
        {
            return HasValidTarget(mission, game) ? null : MissionCompletionReason.TargetUnavailable;
        }

        public override bool IsMissionSatisfied(Mission mission, GameRoot game)
        {
            return HasValidTarget(mission, game);
        }

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(
                mission.TargetOfficerInstanceID
            );
            if (target == null)
                return new List<GameResult>();

            target.IsCaptured = false;
            target.CaptorInstanceID = null;
            target.CanEscape = false;

            return new List<GameResult>
            {
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = false,
                    Context = mission.GetParent() as Planet,
                    Tick = game.CurrentTick,
                },
                new OfficerRescuedResult
                {
                    Officer = target,
                    RescuingFaction = game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID),
                    Location = mission.GetParent() as Planet,
                    Tick = game.CurrentTick,
                },
            };
        }

        public override IEnumerable<IMovable> GetSuccessfulReturnPassengers(
            Mission mission,
            GameRoot game
        )
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(
                mission.TargetOfficerInstanceID
            );
            if (
                target?.IsCaptured == false
                && target.GetOwnerInstanceID() == mission.OwnerInstanceID
            )
                yield return target;
        }

        private static bool HasValidTarget(Mission mission, GameRoot game)
        {
            Officer captive = game.GetSceneNodeByInstanceID<Officer>(
                mission.TargetOfficerInstanceID
            );
            return captive?.IsCaptured == true
                && captive.GetParentOfType<Planet>() == mission.GetParent() as Planet;
        }
    }

    internal sealed class SabotageMissionBehavior : MissionBehavior
    {
        public override bool CanceledOnOwnershipChange => false;
        public override bool ImprovesParticipantRatings => false;

        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            if (request.Target == null)
                return null;

            ISceneNode sabotageTarget = request.SpecificTarget ?? request.Target;
            if (sabotageTarget == null || sabotageTarget is Officer)
                return null;

            if (
                sabotageTarget is IManufacturable manufacturable
                && manufacturable.GetManufacturingStatus() == ManufacturingStatus.Building
            )
                return null;

            if (sabotageTarget is IMovable movable && movable.Movement != null)
                return null;

            Planet missionPlanet =
                request.Target as Planet ?? sabotageTarget.GetParentOfType<Planet>();
            if (missionPlanet == null)
                return null;

            if (
                request.SpecificTarget != null
                && sabotageTarget.GetParentOfType<Planet>()?.InstanceID != missionPlanet.InstanceID
            )
                return null;

            Mission mission = CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                missionPlanet,
                request.MainParticipants,
                request.DecoyParticipants
            );
            mission.TargetInstanceID = sabotageTarget.GetInstanceID();
            return mission;
        }

        public override MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game)
        {
            return HasValidTarget(mission, game) ? null : MissionCompletionReason.TargetUnavailable;
        }

        public override bool IsMissionSatisfied(Mission mission, GameRoot game)
        {
            return HasValidTarget(mission, game);
        }

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Planet planet = mission.GetParent() as Planet;
            ISceneNode target = GetSabotageTarget(mission, game);
            if (target == null)
                return new List<GameResult>();

            game.DetachNode(target);

            return new List<GameResult>
            {
                new GameObjectSabotagedResult
                {
                    SabotagedObject = target,
                    Saboteur =
                        mission.MainParticipants.Count > 0 ? mission.MainParticipants[0] : null,
                    Context = planet,
                    Tick = game.CurrentTick,
                },
            };
        }

        private static bool HasValidTarget(Mission mission, GameRoot game)
        {
            ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(mission.TargetInstanceID);
            if (target is Planet planet)
                return planet.GetAllBuildings().Count > 0;

            if (target == null || target is Officer)
                return false;

            if (
                target is IManufacturable manufacturable
                && manufacturable.GetManufacturingStatus() == ManufacturingStatus.Building
            )
                return false;

            if (target is IMovable movable && movable.Movement != null)
                return false;

            return target.GetParentOfType<Planet>() == mission.GetParent() as Planet;
        }

        private static ISceneNode GetSabotageTarget(Mission mission, GameRoot game)
        {
            ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(mission.TargetInstanceID);
            if (target is not Planet planet)
                return target;

            List<Building> buildings = planet.GetAllBuildings();
            return buildings.Count > 0 ? buildings[0] : null;
        }
    }

    internal sealed class EspionageMissionBehavior : MissionBehavior
    {
        public override bool CanceledOnOwnershipChange => false;
        public override bool AppliesFoiledParticipantConsequences => false;

        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            Planet planet = RequirePlanet(request.Target);
            if (planet?.WasVisitedBy(request.OwnerInstanceID) != true)
                return null;

            return CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                planet,
                request.MainParticipants,
                request.DecoyParticipants
            );
        }

        public override bool IsMissionSatisfied(Mission mission, GameRoot game)
        {
            return mission.GetParent() is Planet;
        }

        public override List<GameResult> Execute(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            List<GameResult> results = new List<GameResult>();
            List<IMissionParticipant> successfulParticipants = new List<IMissionParticipant>();

            foreach (IMissionParticipant participant in mission.MainParticipants)
            {
                double successThreshold = GetAgentProbability(mission, participant, game);
                double rolledValue = provider.NextDouble() * 100;
                if (rolledValue < successThreshold)
                    successfulParticipants.Add(participant);
            }

            MissionOutcome outcome;
            if (successfulParticipants.Count > 0 && IsMissionSatisfied(mission, game))
            {
                outcome = MissionOutcome.Success;
                results.AddRange(OnSuccess(mission, game, provider));
                ImproveSuccessfulParticipants(mission, successfulParticipants);
            }
            else
            {
                outcome = MissionOutcome.Failed;
                results.AddRange(OnFailed(mission, game, provider));
            }

            results.Add(mission.BuildCompletedResult(outcome, game));
            return results;
        }

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            Planet planet = mission.GetParent() as Planet;
            Faction faction = game?.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);
            PlanetSystem system = planet?.GetParentOfType<PlanetSystem>();

            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordPlanetSnapshot(faction, planet, system, game?.CurrentTick ?? 0);

            return new List<GameResult>();
        }

        private static void ImproveSuccessfulParticipants(
            Mission mission,
            List<IMissionParticipant> participants
        )
        {
            if (
                !(mission.GetParent() is Planet planet)
                || planet.GetOwnerInstanceID() == mission.OwnerInstanceID
            )
                return;

            foreach (IMissionParticipant participant in participants)
            {
                if (participant is Officer officer && participant.CanImproveMissionRating)
                    officer.IncrementBaseRating(mission.ParticipantRating);
            }
        }
    }

    internal sealed class ResearchMissionBehavior : MissionBehavior
    {
        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            Planet planet = RequirePlanet(request.Target);
            if (planet == null || planet.GetOwnerInstanceID() != request.OwnerInstanceID)
                return null;

            if (!request.Discipline.HasValue)
                return null;

            List<IMissionParticipant> actingParticipants = new List<IMissionParticipant>();
            if (request.MainParticipants?.Count > 0)
                actingParticipants.Add(request.MainParticipants[0]);

            Mission mission = CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                planet,
                actingParticipants,
                request.DecoyParticipants
            );
            mission.Discipline = request.Discipline.Value;
            mission.ParticipantRating = Officer.GetRatingForResearchDiscipline(
                request.Discipline.Value
            );
            mission.DisplayName = GetMissionName(request.Discipline.Value);
            return mission;
        }

        public override MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game)
        {
            Planet planet = mission.GetParent() as Planet;
            if (
                IsMissionSatisfied(mission, game) && HasResearchFacility(planet, mission.Discipline)
            )
                return null;

            if (
                planet != null
                && planet.GetOwnerInstanceID() == mission.OwnerInstanceID
                && !HasResearchFacility(planet, mission.Discipline)
            )
            {
                return MissionCompletionReason.NoResearchFacilities;
            }

            return MissionCompletionReason.TargetUnavailable;
        }

        public override bool IsMissionSatisfied(Mission mission, GameRoot game)
        {
            return mission.GetParent() is Planet p
                && p.GetOwnerInstanceID() == mission.OwnerInstanceID;
        }

        public override double GetFoilProbability(
            Mission mission,
            double defenseScore,
            GameRoot game
        ) => 0;

        public override List<GameResult> Execute(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            List<GameResult> results = new List<GameResult>();
            MissionOutcome outcome = MissionOutcome.Failed;
            MissionCompletionReason completionReason =
                GetAbortReason(mission, game) ?? MissionCompletionReason.TargetUnavailable;
            Faction faction = game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);
            Planet planet = mission.GetParent() as Planet;

            if (
                faction != null
                && IsMissionSatisfied(mission, game)
                && HasResearchFacility(planet, mission.Discipline)
            )
            {
                int earnedPoints = AccumulatePointsFromParticipants(
                    mission,
                    game.Config.Research,
                    provider
                );
                if (earnedPoints > 0)
                {
                    outcome = MissionOutcome.Success;
                    AwardAccumulatedPoints(mission, faction, earnedPoints, game, results);
                    completionReason = results.OfType<ResearchOrderedResult>().Any()
                        ? MissionCompletionReason.ResearchBreakthrough
                        : MissionCompletionReason.ResearchProgress;
                }
                else
                {
                    completionReason = MissionCompletionReason.Failure;
                }
            }

            results.Add(mission.BuildCompletedResult(outcome, completionReason, game));
            return results;
        }

        public override bool ShouldRepeatAfterCompletion(Mission mission, GameRoot game)
        {
            return IsMissionSatisfied(mission, game);
        }

        private static string GetMissionName(ResearchDiscipline discipline)
        {
            return discipline switch
            {
                ResearchDiscipline.ShipDesign => "Ship Design",
                ResearchDiscipline.TroopTraining => "Troop Training",
                ResearchDiscipline.FacilityDesign => "Facility Design",
                _ => "Research",
            };
        }

        internal static bool HasResearchFacility(Planet planet, ResearchDiscipline discipline)
        {
            if (planet == null || discipline == ResearchDiscipline.None)
                return false;

            return discipline switch
            {
                ResearchDiscipline.ShipDesign => planet
                    .GetProductionFacilities(ManufacturingType.Ship)
                    .Count > 0
                    || planet.GetProductionFacilities(ManufacturingType.Troop).Count > 0,
                ResearchDiscipline.TroopTraining => planet
                    .GetProductionFacilities(ManufacturingType.Troop)
                    .Count > 0
                    || planet.GetProductionFacilities(ManufacturingType.Building).Count > 0,
                ResearchDiscipline.FacilityDesign => planet
                    .GetProductionFacilities(ManufacturingType.Building)
                    .Count > 0
                    || planet
                        .GetAllBuildings()
                        .Any(building =>
                            building.BuildingType == BuildingType.Mine
                            && building.GetManufacturingStatus() == ManufacturingStatus.Complete
                        ),
                _ => false,
            };
        }

        private static int AccumulatePointsFromParticipants(
            Mission mission,
            GameConfig.ResearchConfig config,
            IRandomNumberProvider provider
        )
        {
            int earnedPoints = 0;
            foreach (IMissionParticipant participant in mission.MainParticipants)
            {
                if (!(participant is Officer officer) || !RollSuccess(mission, officer, provider))
                    continue;

                earnedPoints += RollReward(config, provider);
                officer.IncrementBaseRating(mission.Discipline);
            }

            return earnedPoints;
        }

        private static bool RollSuccess(
            Mission mission,
            Officer officer,
            IRandomNumberProvider provider
        )
        {
            int chance = officer.GetBaseRating(mission.Discipline);
            return provider.NextDouble() * 100 < chance;
        }

        private static int RollReward(
            GameConfig.ResearchConfig config,
            IRandomNumberProvider provider
        )
        {
            return config.BaseResearchPoints + provider.NextInt(0, config.ResearchDiceRange + 1);
        }

        private static void AwardAccumulatedPoints(
            Mission mission,
            Faction faction,
            int earnedPoints,
            GameRoot game,
            List<GameResult> results
        )
        {
            Technology unlocked = faction.ApplyResearchProgress(mission.Discipline, earnedPoints);
            if (unlocked == null)
                return;

            results.Add(BuildOrderedResult(mission, faction, unlocked, game));
            if (faction.IsResearchExhausted(mission.Discipline))
                results.Add(BuildExhaustedResult(mission, faction, game));
        }

        private static ResearchOrderedResult BuildOrderedResult(
            Mission mission,
            Faction faction,
            Technology unlocked,
            GameRoot game
        )
        {
            return new ResearchOrderedResult
            {
                Tick = game.CurrentTick,
                Faction = faction,
                Discipline = mission.Discipline,
                ResearchOrder = faction.GetHighestUnlockedOrder(mission.Discipline),
                Capacity = faction.GetResearchCapacityRemaining(mission.Discipline),
                Technology = unlocked,
            };
        }

        private static ResearchExhaustedResult BuildExhaustedResult(
            Mission mission,
            Faction faction,
            GameRoot game
        )
        {
            return new ResearchExhaustedResult
            {
                Tick = game.CurrentTick,
                Faction = faction,
                Discipline = mission.Discipline,
                PreviousState = 0,
                NewState = 1,
            };
        }
    }

    internal sealed class JediTrainingMissionBehavior : MissionBehavior
    {
        public override bool ImprovesParticipantRatings => false;

        public override Mission TryCreate(MissionStartRequest request, MissionDefinition definition)
        {
            Planet planet = RequirePlanet(request.Target);
            if (planet == null || planet.GetOwnerInstanceID() != request.OwnerInstanceID)
                return null;

            string trainerId = SelectTrainer(request.Game, request.OwnerInstanceID, planet);
            if (trainerId == null)
                return null;

            Mission mission = CreatePlanetMission(
                definition,
                request.OwnerInstanceID,
                planet,
                request.MainParticipants,
                request.DecoyParticipants
            );
            mission.TrainerInstanceID = trainerId;
            mission.DisplayName = "Jedi Training";
            return mission;
        }

        public override MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game)
        {
            Officer trainer = game.GetSceneNodeByInstanceID<Officer>(mission.TrainerInstanceID);
            if (trainer == null)
                return MissionCompletionReason.Failure;

            return trainer.IsCaptured || trainer.IsKilled ? MissionCompletionReason.Failure : null;
        }

        public override double GetAgentProbability(
            Mission mission,
            IMissionParticipant agent,
            GameRoot game
        )
        {
            if (agent is Officer officer)
                return mission.LookupSuccessProbability(game, officer.ForceRank);

            return 0;
        }

        public override double GetFoilProbability(
            Mission mission,
            double defenseScore,
            GameRoot game
        ) => 0;

        public override List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            List<GameResult> results = new List<GameResult>();
            Officer trainer = game.GetSceneNodeByInstanceID<Officer>(mission.TrainerInstanceID);

            if (trainer?.IsForceEligible != true)
                return results;

            foreach (Officer student in mission.MainParticipants.OfType<Officer>())
            {
                if (!student.IsForceEligible || student.ForceRank >= trainer.ForceRank)
                    continue;

                int gap = trainer.ForceRank - student.ForceRank;
                int catchUpRange = gap * game.Config.Jedi.TrainingCatchUpPercent / 100;

                if (catchUpRange <= 0)
                    continue;

                int bonus = provider.NextInt(0, catchUpRange + 1);
                student.ForceTrainingAdjustment += bonus;

                results.Add(
                    new ForceTrainingResult
                    {
                        Officer = student,
                        Progress = bonus,
                        Detail = trainer.ForceRank,
                        Tick = game.CurrentTick,
                    }
                );

                GameLogger.Log(
                    $"{student.GetDisplayName()} gained {bonus} training adjustment from {trainer.GetDisplayName()} (rank {student.ForceRank})"
                );
            }

            return results;
        }

        public override bool ShouldRepeatAfterCompletion(Mission mission, GameRoot game)
        {
            int threshold = game.Config.Jedi.ForceQualifiedThreshold;
            return mission.MainParticipants.OfType<Officer>().Any(s => s.ForceRank < threshold);
        }

        private static string SelectTrainer(GameRoot game, string ownerInstanceId, Planet planet)
        {
            Officer trainer = game.GetSceneNodesByType<Officer>()
                .Where(o =>
                    o.GetOwnerInstanceID() == ownerInstanceId
                    && o.IsJedi
                    && o.IsJediTrainer
                    && o.IsForceEligible
                    && o.GetParentOfType<Planet>() == planet
                    && !o.IsCaptured
                    && !o.IsOnMission()
                )
                .OrderByDescending(o => o.ForceRank)
                .FirstOrDefault();
            return trainer?.InstanceID;
        }
    }
}
