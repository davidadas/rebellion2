using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Pairs mission officers with the opposing Force users available at an encounter checkpoint.
    /// The caller supplies the officers within that checkpoint's physical scope.
    /// </summary>
    internal sealed class MissionForceEncounterSelector
    {
        private readonly GameConfig.JediConfig _config;
        private readonly IRandomNumberProvider _random;

        /// <summary>
        /// Creates selection using the active Force rules and simulation random stream.
        /// </summary>
        /// <param name="config">The encounter thresholds and candidate priorities.</param>
        /// <param name="random">The simulation random stream.</param>
        internal MissionForceEncounterSelector(
            GameConfig.JediConfig config,
            IRandomNumberProvider random
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Selects each defender once, retrying the unmatched participants for each new defender.
        /// Major officers precede minor officers, with configured priorities within each major pool.
        /// </summary>
        /// <param name="participants">The eligible primary and decoy officers in checkpoint order.</param>
        /// <param name="defenders">The available opposing officers within the checkpoint's scope.</param>
        /// <returns>The ordered participant and defender pairs, without resolving their consequences.</returns>
        internal IReadOnlyList<(Officer Participant, Officer Defender)> SelectPairs(
            IReadOnlyList<Officer> participants,
            IReadOnlyList<Officer> defenders
        )
        {
            if (participants == null)
                throw new ArgumentNullException(nameof(participants));
            if (defenders == null)
                throw new ArgumentNullException(nameof(defenders));

            List<(Officer Participant, Officer Defender)> pairs = new();
            CandidatePool defenderPool = new(defenders.Count);
            CandidatePool participantPool = new(participants.Count);
            HashSet<Officer> matched = new();
            defenderPool.Reset(defenders, matched);
            Officer defender;
            while ((defender = defenderPool.Take(_config, _random)) != null)
            {
                participantPool.Reset(participants, matched);
                Officer participant;
                while ((participant = participantPool.Take(_config, _random)) != null)
                {
                    int chance =
                        participant.ForceRank
                        + defender.ForceRank
                        + _config.EncounterProbabilityOffset;
                    if (
                        participant.ForceRank == 0
                        || defender.ForceRank == 0
                        || chance <= 0
                        || _random.NextInt(0, 100) >= chance
                    )
                        continue;

                    pairs.Add((participant, defender));
                    matched.Add(participant);
                    break;
                }
            }
            return pairs;
        }

        /// <summary>
        /// Reuses major and minor candidate pools across attempts without allocating inside a pair loop.
        /// </summary>
        private sealed class CandidatePool
        {
            private readonly List<Officer> _major;
            private readonly List<Officer> _minor;
            private readonly Dictionary<string, int> _majorIndex;

            /// <summary>
            /// Reserves storage for every officer that may enter this pool.
            /// </summary>
            /// <param name="capacity">The maximum candidate count.</param>
            internal CandidatePool(int capacity)
            {
                _major = new List<Officer>(capacity);
                _minor = new List<Officer>(capacity);
                _majorIndex = new Dictionary<string, int>(capacity, StringComparer.Ordinal);
            }

            /// <summary>
            /// Restores the unvisited pool while retaining successful participant matches.
            /// </summary>
            /// <param name="officers">The checkpoint's candidate snapshot.</param>
            /// <param name="matched">The participants already paired with another defender.</param>
            internal void Reset(IReadOnlyList<Officer> officers, HashSet<Officer> matched)
            {
                _major.Clear();
                _minor.Clear();
                _majorIndex.Clear();
                foreach (Officer officer in officers)
                {
                    if (matched.Contains(officer))
                        continue;
                    if (officer.IsMain)
                    {
                        _majorIndex.Add(officer.InstanceID, _major.Count);
                        _major.Add(officer);
                    }
                    else
                        _minor.Add(officer);
                }
            }

            /// <summary>
            /// Removes the next preferred major, random major, or random minor officer.
            /// </summary>
            /// <param name="config">The configured major-officer priority order.</param>
            /// <param name="random">The simulation random stream.</param>
            /// <returns>The selected unvisited officer, or null when exhausted.</returns>
            internal Officer Take(GameConfig.JediConfig config, IRandomNumberProvider random)
            {
                foreach (string instanceID in config.MissionEncounterPriorityOfficerInstanceIDs)
                    if (_majorIndex.TryGetValue(instanceID, out int index))
                        return RemoveMajor(index);
                if (_major.Count > 0)
                    return RemoveMajor(_major.Count > 1 ? random.NextInt(0, _major.Count) : 0);
                if (_minor.Count == 0)
                    return null;
                int selected = _minor.Count > 1 ? random.NextInt(0, _minor.Count) : 0;
                Officer officer = _minor[selected];
                _minor[selected] = _minor[_minor.Count - 1];
                _minor.RemoveAt(_minor.Count - 1);
                return officer;
            }

            /// <summary>
            /// Removes a major officer and updates the index for the swapped entry.
            /// </summary>
            /// <param name="index">The selected major-officer index.</param>
            /// <returns>The removed officer.</returns>
            private Officer RemoveMajor(int index)
            {
                Officer officer = _major[index];
                Officer last = _major[_major.Count - 1];
                _major[index] = last;
                _majorIndex[last.InstanceID] = index;
                _major.RemoveAt(_major.Count - 1);
                _majorIndex.Remove(officer.InstanceID);
                return officer;
            }
        }
    }
}
