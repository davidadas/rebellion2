using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Replaces the authored image paths used for an officer.
    /// </summary>
    [PersistableObject(Name = "SetOfficerImages")]
    public sealed class SetOfficerImagesAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
        public string DisplayImagePath { get; set; }
        public string SmallDisplayImagePath { get; set; }
        public string MessageImagePath { get; set; }
        public string EncyclopediaImagePath { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetOfficerImages could not resolve officer '{OfficerInstanceID}'."
                );

            if (!string.IsNullOrWhiteSpace(DisplayImagePath))
                officer.DisplayImagePath = DisplayImagePath;
            if (!string.IsNullOrWhiteSpace(SmallDisplayImagePath))
                officer.SmallDisplayImagePath = SmallDisplayImagePath;
            if (!string.IsNullOrWhiteSpace(MessageImagePath))
                officer.MessageImagePath = MessageImagePath;
            if (!string.IsNullOrWhiteSpace(EncyclopediaImagePath))
                officer.EncyclopediaImagePath = EncyclopediaImagePath;
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Replaces selected officer voice-line collections with authored asset paths.
    /// </summary>
    [PersistableObject(Name = "SetOfficerVoiceSet")]
    public sealed class SetOfficerVoiceSetAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        public List<string> PersonnelArrivedVoicePaths { get; set; } = new List<string>();
        public List<string> MissionAbortVoicePaths { get; set; } = new List<string>();
        public List<string> ReleasedVoicePaths { get; set; } = new List<string>();
        public List<string> RecoveredVoicePaths { get; set; } = new List<string>();
        public List<string> EnemyDetectedVoicePaths { get; set; } = new List<string>();
        public List<string> ForceGrowthVoicePaths { get; set; } = new List<string>();
        public List<string> TraitorDiscoveredVoicePaths { get; set; } = new List<string>();
        public List<string> RescueAttemptVoicePaths { get; set; } = new List<string>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetOfficerVoiceSet could not resolve officer '{OfficerInstanceID}'."
                );

            ReplaceWhenAuthored(PersonnelArrivedVoicePaths, officer.PersonnelArrivedVoicePaths);
            ReplaceWhenAuthored(MissionAbortVoicePaths, officer.MissionAbortVoicePaths);
            ReplaceWhenAuthored(ReleasedVoicePaths, officer.ReleasedVoicePaths);
            ReplaceWhenAuthored(RecoveredVoicePaths, officer.RecoveredVoicePaths);
            ReplaceWhenAuthored(EnemyDetectedVoicePaths, officer.EnemyDetectedVoicePaths);
            ReplaceWhenAuthored(ForceGrowthVoicePaths, officer.ForceGrowthVoicePaths);
            ReplaceWhenAuthored(TraitorDiscoveredVoicePaths, officer.TraitorDiscoveredVoicePaths);
            ReplaceWhenAuthored(RescueAttemptVoicePaths, officer.RescueAttemptVoicePaths);
            return new List<GameResult>();
        }

        private static void ReplaceWhenAuthored(List<string> authored, List<string> destination)
        {
            if (authored == null || authored.Count == 0)
                return;
            destination.Clear();
            destination.AddRange(authored);
        }
    }
}
