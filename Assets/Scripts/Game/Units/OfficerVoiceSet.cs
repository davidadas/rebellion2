using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Groups the recordings an officer can use for simulation and message events.
    /// </summary>
    [PersistableObject]
    public sealed class OfficerVoiceSet
    {
        [PersistableCollectionItem(Name = "Path")]
        public List<string> Order { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> PersonnelArrived { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionSuccess { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionFailure { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionAbort { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> Released { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> Recovered { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> EnemyDetected { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> ForceGrowth { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> ForceUserDiscovered { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> TraitorDiscovered { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> RescueAttempt { get; set; } = new List<string>();

        public IReadOnlyList<string> Get(OfficerVoiceLineType type)
        {
            return type switch
            {
                OfficerVoiceLineType.Order => Order,
                OfficerVoiceLineType.PersonnelArrived => PersonnelArrived,
                OfficerVoiceLineType.MissionSuccess => MissionSuccess,
                OfficerVoiceLineType.MissionFailure => MissionFailure,
                OfficerVoiceLineType.MissionAbort => MissionAbort,
                OfficerVoiceLineType.Released => Released,
                OfficerVoiceLineType.Recovered => Recovered,
                OfficerVoiceLineType.EnemyDetected => EnemyDetected,
                OfficerVoiceLineType.ForceGrowth => ForceGrowth,
                OfficerVoiceLineType.ForceUserDiscovered => ForceUserDiscovered,
                OfficerVoiceLineType.TraitorDiscovered => TraitorDiscovered,
                OfficerVoiceLineType.RescueAttempt => RescueAttempt,
                _ => null,
            };
        }

        public void MergeFrom(OfficerVoiceSet authored)
        {
            if (authored == null)
                return;

            ReplaceWhenAuthored(authored.Order, Order);
            ReplaceWhenAuthored(authored.PersonnelArrived, PersonnelArrived);
            ReplaceWhenAuthored(authored.MissionSuccess, MissionSuccess);
            ReplaceWhenAuthored(authored.MissionFailure, MissionFailure);
            ReplaceWhenAuthored(authored.MissionAbort, MissionAbort);
            ReplaceWhenAuthored(authored.Released, Released);
            ReplaceWhenAuthored(authored.Recovered, Recovered);
            ReplaceWhenAuthored(authored.EnemyDetected, EnemyDetected);
            ReplaceWhenAuthored(authored.ForceGrowth, ForceGrowth);
            ReplaceWhenAuthored(authored.ForceUserDiscovered, ForceUserDiscovered);
            ReplaceWhenAuthored(authored.TraitorDiscovered, TraitorDiscovered);
            ReplaceWhenAuthored(authored.RescueAttempt, RescueAttempt);
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
