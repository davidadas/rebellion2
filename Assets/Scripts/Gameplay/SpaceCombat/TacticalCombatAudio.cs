using System;
using System.Collections.Generic;
using System.IO;
using Rebellion.Game.SpaceCombat;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Selects combat sound variants from the battle stream and bounds simultaneous playback.
    /// Scheduling uses elapsed presentation time without accessing an audio device or campaign graph.
    /// </summary>
    public sealed class TacticalCombatAudio
    {
        private const int _concurrentSounds = 5;
        private const double _pendingLifetimeMilliseconds = 225;
        private readonly Dictionary<
            TacticalCombatSound,
            TacticalCombatSoundDefinition
        > _definitions = new();
        private readonly List<double> _activeUntil = new(_concurrentSounds);
        private readonly Queue<Pending> _pending = new();
        private readonly IRandomNumberProvider _random;
        private double _elapsedMilliseconds;
        public event Action<TacticalCombatSoundClip> SoundRequested;

        /// <summary>
        /// Captures authored sounds while sharing the encounter's random-number stream.
        /// </summary>
        /// <param name="definitions">The sound variants and durations supplied by content.</param>
        /// <param name="random">The encounter's random-number stream.</param>
        public TacticalCombatAudio(
            IReadOnlyList<TacticalCombatSoundDefinition> definitions,
            IRandomNumberProvider random
        )
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            foreach (TacticalCombatSoundDefinition definition in definitions)
            {
                if (definition.Variants.Count == 0)
                    throw new InvalidDataException(
                        "A tactical sound requires at least one variant."
                    );
                foreach (TacticalCombatSoundClip clip in definition.Variants)
                    if (
                        string.IsNullOrWhiteSpace(clip.Path)
                        || !(clip.DurationMilliseconds > 0)
                        || float.IsInfinity(clip.DurationMilliseconds)
                    )
                        throw new InvalidDataException(
                            "A tactical sound requires an addressed clip with a finite positive duration."
                        );
                _definitions.Add(definition.Kind, definition);
            }
        }

        /// <summary>
        /// Selects a sound before checking playback capacity and retains blocked requests briefly.
        /// </summary>
        /// <param name="sound">The combat event requesting sound.</param>
        public void Enqueue(TacticalCombatSound sound)
        {
            if (!_definitions.TryGetValue(sound, out TacticalCombatSoundDefinition definition))
                return;
            UpdateQueues();
            if (!TryStart(definition))
                _pending.Enqueue(new Pending(definition, _elapsedMilliseconds));
        }

        /// <summary>
        /// Advances playback and pending requests even while tactical movement is paused.
        /// </summary>
        /// <param name="elapsedMilliseconds">The unscaled presentation interval.</param>
        public void Advance(float elapsedMilliseconds)
        {
            if (elapsedMilliseconds > 0)
                _elapsedMilliseconds += elapsedMilliseconds;
            UpdateQueues();
        }

        /// <summary>
        /// Expires completed sounds and retries pending requests in insertion order.
        /// </summary>
        private void UpdateQueues()
        {
            for (int index = _activeUntil.Count - 1; index >= 0; index--)
                if (_elapsedMilliseconds >= _activeUntil[index])
                    _activeUntil.RemoveAt(index);
            while (_pending.Count > 0)
            {
                Pending pending = _pending.Peek();
                if (_elapsedMilliseconds - pending.Created > _pendingLifetimeMilliseconds)
                    _pending.Dequeue();
                else if (_activeUntil.Count < _concurrentSounds)
                {
                    _pending.Dequeue();
                    TryStart(pending.Definition);
                }
                else
                    break;
            }
        }

        /// <summary>
        /// Draws a variant before applying the simultaneous-playback limit.
        /// </summary>
        /// <param name="definition">The requested sound's authored variants.</param>
        /// <returns>Whether playback started.</returns>
        private bool TryStart(TacticalCombatSoundDefinition definition)
        {
            int index =
                definition.Variants.Count > 1 ? _random.NextInt(0, definition.Variants.Count) : 0;
            if (_activeUntil.Count >= _concurrentSounds)
                return false;
            TacticalCombatSoundClip clip = definition.Variants[index];
            _activeUntil.Add(_elapsedMilliseconds + clip.DurationMilliseconds);
            SoundRequested?.Invoke(clip);
            return true;
        }

        private readonly struct Pending
        {
            public TacticalCombatSoundDefinition Definition { get; }
            public double Created { get; }

            /// <summary>
            /// Captures a blocked sound's definition and expiry origin.
            /// </summary>
            /// <param name="definition">The requested sound.</param>
            /// <param name="created">The request time in presentation milliseconds.</param>
            public Pending(TacticalCombatSoundDefinition definition, double created)
            {
                Definition = definition;
                Created = created;
            }
        }
    }
}
