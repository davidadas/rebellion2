using System;
using System.Diagnostics;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Records opt-in synchronous phase timings in bounded storage without inspecting game entities.
    /// This owner follows the session's single-threaded simulation and presentation execution.
    /// </summary>
    public sealed class SimulationPhaseTrace
    {
        private SimulationTimingSample[] _samples;
        private int _count;
        private int _generation;
        private long _droppedSamples;

        /// <summary>
        /// Gets whether new synchronous work scopes are being recorded.
        /// </summary>
        public bool IsRecording { get; private set; }

        /// <summary>
        /// Starts recording into an explicitly sized buffer, replacing any previous stopped capture.
        /// </summary>
        /// <param name="capacity">The maximum number of samples retained before dropping later samples.</param>
        public void Start(int capacity)
        {
            if (IsRecording)
                throw new InvalidOperationException(
                    "A simulation timing capture is already running."
                );
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _samples = new SimulationTimingSample[capacity];
            _count = 0;
            _droppedSamples = 0;
            _generation++;
            IsRecording = true;
        }

        /// <summary>
        /// Stops recording and copies the retained scalar samples for analysis outside the hot path.
        /// </summary>
        /// <returns>The stopped samples and explicit overflow count.</returns>
        public SimulationTimingCapture Stop()
        {
            if (!IsRecording)
                throw new InvalidOperationException("No simulation timing capture is running.");
            IsRecording = false;
            SimulationTimingSample[] copied = new SimulationTimingSample[_count];
            Array.Copy(_samples, copied, _count);
            return new SimulationTimingCapture(copied, _droppedSamples, _samples.Length);
        }

        /// <summary>
        /// Begins one synchronous scope; disabled recording performs no allocation or clock read.
        /// The scope must end before an iterator yields control to another frame.
        /// </summary>
        /// <param name="phase">The fixed work boundary being measured.</param>
        /// <param name="tick">The current game day.</param>
        /// <returns>A value scope that records completion when disposed once.</returns>
        public Scope Measure(SimulationPhase phase, int tick)
        {
            return IsRecording
                ? new Scope(this, _generation, tick, phase, Stopwatch.GetTimestamp())
                : default;
        }

        /// <summary>
        /// Completes a current scope using bounded scalar writes, omitting old capture generations.
        /// </summary>
        /// <param name="generation">The recording generation that began the work.</param>
        /// <param name="tick">The current game day.</param>
        /// <param name="phase">The fixed work boundary.</param>
        /// <param name="startedAt">The monotonic timestamp captured when the scope began.</param>
        private void Complete(int generation, int tick, SimulationPhase phase, long startedAt)
        {
            if (!IsRecording || generation != _generation)
                return;
            long elapsed = Stopwatch.GetTimestamp() - startedAt;
            if (_count == _samples.Length)
            {
                _droppedSamples++;
                return;
            }
            _samples[_count++] = new SimulationTimingSample(tick, phase, elapsed);
        }

        /// <summary>
        /// Measures exactly one synchronous call without allocating a disposable object.
        /// </summary>
        public readonly struct Scope : IDisposable
        {
            private readonly SimulationPhaseTrace _owner;
            private readonly int _generation;
            private readonly int _tick;
            private readonly SimulationPhase _phase;
            private readonly long _startedAt;

            /// <summary>
            /// Captures the scalar position of a recording scope.
            /// </summary>
            /// <param name="owner">The recording session's trace.</param>
            /// <param name="generation">The current capture generation.</param>
            /// <param name="tick">The measured game day.</param>
            /// <param name="phase">The synchronous work boundary.</param>
            /// <param name="startedAt">The starting monotonic timestamp.</param>
            internal Scope(
                SimulationPhaseTrace owner,
                int generation,
                int tick,
                SimulationPhase phase,
                long startedAt
            )
            {
                _owner = owner;
                _generation = generation;
                _tick = tick;
                _phase = phase;
                _startedAt = startedAt;
            }

            /// <summary>
            /// Records this scope's elapsed work when its capture is still active.
            /// </summary>
            public void Dispose()
            {
                _owner?.Complete(_generation, _tick, _phase, _startedAt);
            }
        }
    }
}
