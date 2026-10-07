using System;
using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>Describes every hostile detection attempt facing one personnel movement group.</summary>
    public sealed class PersonnelMovementEncounterOdds
    {
        /// <summary>Gets the eligible detector attempts in scene traversal order.</summary>
        public IReadOnlyList<PersonnelMovementDetectorOdds> Detectors { get; }

        /// <summary>Gets the eligible Force-user encounters in traversal order.</summary>
        public IReadOnlyList<PersonnelMovementForceEncounterOdds> ForceEncounters { get; }

        /// <summary>Creates a personnel movement encounter projection.</summary>
        /// <param name="detectors">The eligible detector attempts.</param>
        /// <param name="forceEncounters">The eligible Force-user encounters.</param>
        internal PersonnelMovementEncounterOdds(
            IReadOnlyList<PersonnelMovementDetectorOdds> detectors,
            IReadOnlyList<PersonnelMovementForceEncounterOdds> forceEncounters
        )
        {
            Detectors = detectors ?? throw new ArgumentNullException(nameof(detectors));
            ForceEncounters =
                forceEncounters ?? throw new ArgumentNullException(nameof(forceEncounters));
        }
    }

    /// <summary>Describes one Force-user encounter involving personnel in transit.</summary>
    public sealed class PersonnelMovementForceEncounterOdds
    {
        /// <summary>Gets the traveling officer exposed by the encounter.</summary>
        public Officer Participant { get; }

        /// <summary>Gets the hostile officer opposing the traveler.</summary>
        public Officer Defender { get; }

        /// <summary>Gets the percentage chance that the encounter exposes the movement group.</summary>
        public int DetectionProbability { get; }

        /// <summary>Creates one Force-user encounter projection.</summary>
        /// <param name="participant">The traveling officer.</param>
        /// <param name="defender">The hostile officer.</param>
        /// <param name="detectionProbability">The encounter probability.</param>
        internal PersonnelMovementForceEncounterOdds(
            Officer participant,
            Officer defender,
            int detectionProbability
        )
        {
            Participant = participant ?? throw new ArgumentNullException(nameof(participant));
            Defender = defender ?? throw new ArgumentNullException(nameof(defender));
            DetectionProbability = detectionProbability;
        }
    }

    /// <summary>Describes one hostile detector's odds against a personnel movement group.</summary>
    public sealed class PersonnelMovementDetectorOdds
    {
        private readonly IReadOnlyDictionary<IMissionParticipant, double> _evasionProbabilities;

        /// <summary>Gets the hostile detector.</summary>
        public ISceneNode Detector { get; }

        /// <summary>Gets the eligible officer supporting the detector, when one is assigned.</summary>
        public Officer Commander { get; }

        /// <summary>Gets this detector's chance to detect the movement group.</summary>
        public int DetectionProbability { get; }

        /// <summary>Creates one detector projection.</summary>
        /// <param name="detector">The hostile detector.</param>
        /// <param name="commander">The supporting commander, when present.</param>
        /// <param name="detectionProbability">The detection percentage.</param>
        /// <param name="evasionProbabilities">Participant evasion percentages.</param>
        internal PersonnelMovementDetectorOdds(
            ISceneNode detector,
            Officer commander,
            int detectionProbability,
            IReadOnlyDictionary<IMissionParticipant, double> evasionProbabilities
        )
        {
            Detector = detector ?? throw new ArgumentNullException(nameof(detector));
            Commander = commander;
            DetectionProbability = detectionProbability;
            _evasionProbabilities =
                evasionProbabilities
                ?? throw new ArgumentNullException(nameof(evasionProbabilities));
        }

        /// <summary>Returns one participant's chance to evade this detector.</summary>
        /// <param name="participant">The participant attempting to evade.</param>
        /// <returns>The configured evasion percentage, or zero when absent.</returns>
        public double GetEvasionProbability(IMissionParticipant participant)
        {
            return
                participant != null
                && _evasionProbabilities.TryGetValue(participant, out double probability)
                ? probability
                : 0;
        }
    }
}
