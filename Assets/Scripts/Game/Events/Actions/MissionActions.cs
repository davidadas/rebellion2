using System;
using System.Collections.Generic;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Starts a mission definition with explicit semantic unit roles.
    /// </summary>
    [PersistableObject(Name = "StartMission")]
    public sealed class StartMissionAction : GameAction
    {
        [PersistableAttribute]
        public string MissionDefinitionID { get; set; }

        public List<MissionRoleAssignment> Roles { get; set; } = new List<MissionRoleAssignment>();

        public override List<GameResult> Execute(GameRoot game)
        {
            foreach (MissionRoleAssignment role in Roles)
            {
                if (game.GetSceneNodeByInstanceID<ISceneNode>(role.UnitInstanceID) == null)
                    throw new InvalidOperationException(
                        $"StartMission could not resolve role '{role.Name}' unit '{role.UnitInstanceID}'."
                    );
            }

            return new List<GameResult>
            {
                new CustomMissionRequestedResult
                {
                    MissionDefinitionID = MissionDefinitionID,
                    Roles = Roles.ConvertAll(role => new MissionRoleAssignment
                    {
                        Name = role.Name,
                        UnitInstanceID = role.UnitInstanceID,
                    }),
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Announces a bounty-hunter attack through the normal result pipeline.
    /// </summary>
    [PersistableObject(Name = "BountyAttack")]
    public sealed class BountyAttackAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"BountyAttack could not resolve officer '{OfficerInstanceID}'."
                );

            return new List<GameResult>
            {
                new BountyAttackResult { Officer = officer, Tick = game.CurrentTick },
            };
        }
    }
}
