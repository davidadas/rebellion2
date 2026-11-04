using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Rebellion.Util.Logging
{
    /// <summary>
    /// Records opt-in, tick-scoped performance measurements as buffered JSON lines.
    /// </summary>
    public static class PerformanceLogger
    {
        private const string _pathEnvironmentVariable = "REB2_PERFORMANCE_LOG_PATH";
        private const int _initialFrameCapacity = 32;
        private const int _writerBufferSize = 64 * 1024;

        private static readonly Dictionary<OperationKey, OperationAggregate> _operations = new();
        private static Frame[] _frames = new Frame[_initialFrameCapacity];
        private static StreamWriter _writer;
        private static Process _process;
        private static Func<long> _allocatedBytesReader;
        private static Dictionary<Type, string> _subjectLabels;
        private static volatile bool _enabled;
        private static bool _segmentActive;
        private static bool _processCpuSupported;
        private static bool _allocatedBytesSupported;
        private static int _gcMaxGeneration;
        private static int _ownerThreadId;
        private static int _depth;
        private static int _nextScopeToken;
        private static int _currentTick;
        private static long _sequence;
        private static long _currentSequence;
        private static long _segmentStartedTicks;
        private static DateTime _segmentStartedUtc;
        private static long _activeTicks;
        private static long _rootChunks;
        private static long _processCpuTicks;
        private static long _allocatedBytes;
        private static int _generationZeroCollections;
        private static int _generationOneCollections;
        private static int _generationTwoCollections;

        /// <summary>
        /// Gets whether performance recording is enabled for the calling thread.
        /// </summary>
        public static bool IsEnabled =>
            _enabled && Thread.CurrentThread.ManagedThreadId == _ownerThreadId;

        /// <summary>
        /// Configures logging from the optional process environment exactly once during type initialization.
        /// </summary>
        static PerformanceLogger()
        {
            try
            {
                string configuredPath = Environment.GetEnvironmentVariable(
                    _pathEnvironmentVariable
                );
                if (!string.IsNullOrWhiteSpace(configuredPath))
                    Configure(configuredPath);
            }
            catch
            {
                Disable();
            }
        }

        /// <summary>
        /// Configures a new buffered JSON-lines output file and makes the calling thread its owner.
        /// </summary>
        /// <param name="filePath">The output file to create or replace.</param>
        public static void Configure(string filePath)
        {
            Disable();
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                string fullPath = Path.GetFullPath(filePath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
                _gcMaxGeneration = GC.MaxGeneration;
                _processCpuSupported = TryCreateProcess(out _process);
                _allocatedBytesSupported = TryCreateAllocatedBytesReader(out _allocatedBytesReader);
                FileStream stream = new FileStream(
                    fullPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    _writerBufferSize,
                    FileOptions.SequentialScan
                );
                _writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    _writerBufferSize
                );
                HeaderRecord header = new HeaderRecord
                {
                    Kind = "header",
                    FormatVersion = 1,
                    CreatedUtc = DateTime.UtcNow.ToString("O"),
                    GcMaxGeneration = _gcMaxGeneration,
                    ProcessCpuSupported = _processCpuSupported,
                    AllocatedBytesSupported = _allocatedBytesSupported,
                };
                _writer.WriteLine(UnityEngine.JsonUtility.ToJson(header));
                _writer.Flush();
                UnityEngine.Application.quitting -= Flush;
                UnityEngine.Application.quitting += Flush;
                _enabled = true;
            }
            catch
            {
                Disable();
            }
        }

        /// <summary>
        /// Begins one independently reportable segment of a strategic tick.
        /// </summary>
        /// <param name="tick">The strategic tick being processed.</param>
        /// <returns>True when this call owns a newly started segment and must end it.</returns>
        public static bool BeginTick(int tick)
        {
            if (!IsEnabled)
                return false;

            try
            {
                if (_segmentActive)
                {
                    if (_currentTick != tick)
                        Disable();
                    return false;
                }
                if (_depth != 0)
                {
                    Disable();
                    return false;
                }

                _operations.Clear();
                _currentTick = tick;
                _currentSequence = ++_sequence;
                _segmentStartedTicks = Stopwatch.GetTimestamp();
                _segmentStartedUtc = DateTime.UtcNow;
                _activeTicks = 0;
                _rootChunks = 0;
                _processCpuTicks = 0;
                _allocatedBytes = 0;
                _generationZeroCollections = 0;
                _generationOneCollections = 0;
                _generationTwoCollections = 0;
                _segmentActive = true;
                return true;
            }
            catch
            {
                Disable();
                return false;
            }
        }

        /// <summary>
        /// Ends the active tick segment and appends its single aggregated report to the buffer.
        /// </summary>
        /// <param name="state">The processing state reached at the segment boundary.</param>
        public static void EndTick(string state)
        {
            if (!IsEnabled || !_segmentActive)
                return;

            try
            {
                long segmentEndedTicks = Stopwatch.GetTimestamp();
                if (_depth != 0)
                {
                    Disable();
                    return;
                }

                List<OperationRecord> records = new List<OperationRecord>(_operations.Count);
                foreach (KeyValuePair<OperationKey, OperationAggregate> entry in _operations)
                {
                    records.Add(
                        new OperationRecord
                        {
                            FactionId = entry.Key.FactionId,
                            Operation = entry.Key.Operation,
                            SubjectType = entry.Key.SubjectType,
                            ParentOperation = entry.Key.ParentOperation,
                            ParentSubjectType = entry.Key.ParentSubjectType,
                            Calls = entry.Value.Calls,
                            InclusiveMs = ToMilliseconds(entry.Value.InclusiveTicks),
                            SelfMs = ToMilliseconds(entry.Value.SelfTicks),
                            MaxMs = ToMilliseconds(entry.Value.MaxTicks),
                            Items = entry.Value.Items,
                        }
                    );
                }
                records.Sort(
                    (left, right) =>
                    {
                        int comparison = string.CompareOrdinal(left.FactionId, right.FactionId);
                        if (comparison != 0)
                            return comparison;
                        comparison = string.CompareOrdinal(
                            left.ParentOperation,
                            right.ParentOperation
                        );
                        if (comparison != 0)
                            return comparison;
                        comparison = string.CompareOrdinal(
                            left.ParentSubjectType,
                            right.ParentSubjectType
                        );
                        if (comparison != 0)
                            return comparison;
                        comparison = string.CompareOrdinal(left.Operation, right.Operation);
                        return comparison != 0
                            ? comparison
                            : string.CompareOrdinal(left.SubjectType, right.SubjectType);
                    }
                );

                TickSegmentRecord report = new TickSegmentRecord
                {
                    Kind = "tickSegment",
                    Tick = _currentTick,
                    State = state,
                    Sequence = _currentSequence,
                    StartedUtc = _segmentStartedUtc.ToString("O"),
                    WallMs = ToMilliseconds(Math.Max(0, segmentEndedTicks - _segmentStartedTicks)),
                    ActiveMs = ToMilliseconds(_activeTicks),
                    RootChunks = _rootChunks,
                    GcMaxGeneration = _gcMaxGeneration,
                    GenerationZeroCollections = _generationZeroCollections,
                    GenerationOneCollections = _generationOneCollections,
                    GenerationTwoCollections = _generationTwoCollections,
                    ProcessCpuSupported = _processCpuSupported,
                    ProcessCpuMs = _processCpuSupported
                        ? TimeSpan.FromTicks(_processCpuTicks).TotalMilliseconds
                        : 0,
                    AllocatedBytesSupported = _allocatedBytesSupported,
                    AllocatedBytes = _allocatedBytesSupported ? _allocatedBytes : 0,
                    Operations = records.ToArray(),
                };
                _writer.WriteLine(UnityEngine.JsonUtility.ToJson(report));
                _segmentActive = false;
            }
            catch
            {
                Disable();
            }
        }

        /// <summary>
        /// Begins one synchronous operation measurement when recording is enabled.
        /// </summary>
        /// <param name="operation">The stable operation label.</param>
        /// <param name="factionId">The owner label, or null to inherit the active parent label.</param>
        /// <param name="items">The number of logical items represented by this call.</param>
        /// <param name="subjectType">The optional subject category.</param>
        /// <returns>An allocation-free scope that completes the measurement when disposed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Scope Measure(
            string operation,
            string factionId = null,
            long items = 0,
            Type subjectType = null
        ) =>
            !IsEnabled || !_segmentActive
                ? default
                : BeginScopeSafely(operation, factionId, items, subjectType);

        /// <summary>
        /// Measures each enabled enumerable step without retaining a measurement across a yield.
        /// </summary>
        /// <typeparam name="T">The yielded value type.</typeparam>
        /// <param name="source">The source enumerable.</param>
        /// <param name="operation">The stable operation label.</param>
        /// <param name="factionId">The owner label, or null to inherit the active parent label.</param>
        /// <param name="items">The number of logical items represented by each step.</param>
        /// <param name="subjectType">The optional subject category.</param>
        /// <returns>The original source when disabled, or a step-measuring wrapper when enabled.</returns>
        public static IEnumerable<T> MeasureSteps<T>(
            IEnumerable<T> source,
            string operation,
            string factionId = null,
            long items = 0,
            Type subjectType = null
        )
        {
            return !IsEnabled || !_segmentActive
                ? source
                : MeasureStepsEnabled(source, operation, factionId, items, subjectType);
        }

        /// <summary>
        /// Flushes buffered performance reports to disk.
        /// </summary>
        public static void Flush()
        {
            if (!IsEnabled)
                return;

            try
            {
                _writer.Flush();
            }
            catch
            {
                Disable();
            }
        }

        /// <summary>
        /// Guards the enabled measurement path from optional logger failures.
        /// </summary>
        /// <param name="operation">The stable operation label.</param>
        /// <param name="factionId">The explicit owner label, or null.</param>
        /// <param name="items">The represented item count.</param>
        /// <param name="subjectType">The optional subject category.</param>
        /// <returns>The enabled scope, or a disabled scope when logging fails.</returns>
        private static Scope BeginScopeSafely(
            string operation,
            string factionId,
            long items,
            Type subjectType
        )
        {
            try
            {
                return BeginScope(operation, factionId, items, subjectType);
            }
            catch
            {
                Disable();
                return default;
            }
        }

        /// <summary>
        /// Starts the full enabled measurement path and captures its reusable frame.
        /// </summary>
        /// <param name="operation">The stable operation label.</param>
        /// <param name="factionId">The explicit owner label, or null.</param>
        /// <param name="items">The represented item count.</param>
        /// <param name="subjectType">The optional subject category.</param>
        /// <returns>The enabled scope.</returns>
        private static Scope BeginScope(
            string operation,
            string factionId,
            long items,
            Type subjectType
        )
        {
            if (_depth == _frames.Length)
                Array.Resize(ref _frames, _frames.Length * 2);

            string subjectLabel = GetSubjectLabel(subjectType);
            string parentOperation = null;
            string parentSubjectType = null;
            if (_depth > 0)
            {
                Frame parent = _frames[_depth - 1];
                factionId ??= parent.Key.FactionId;
                parentOperation = parent.Key.Operation;
                parentSubjectType = parent.Key.SubjectType;
            }

            int token = ++_nextScopeToken;
            if (token == 0)
                token = ++_nextScopeToken;
            Frame frame = new Frame
            {
                Token = token,
                Key = new OperationKey(
                    factionId,
                    operation,
                    subjectLabel,
                    parentOperation,
                    parentSubjectType
                ),
                Items = items,
            };
            if (_depth == 0)
            {
                frame.GenerationZeroCollections = GC.CollectionCount(0);
                if (_gcMaxGeneration >= 1)
                    frame.GenerationOneCollections = GC.CollectionCount(1);
                if (_gcMaxGeneration >= 2)
                    frame.GenerationTwoCollections = GC.CollectionCount(2);
                if (_processCpuSupported)
                {
                    if (TryReadProcessCpuTicks(out long processCpuTicks))
                        frame.ProcessCpuTicks = processCpuTicks;
                    else
                    {
                        _processCpuSupported = false;
                        _processCpuTicks = 0;
                    }
                }
                if (_allocatedBytesSupported)
                {
                    if (TryReadAllocatedBytes(out long allocatedBytes))
                        frame.AllocatedBytes = allocatedBytes;
                    else
                    {
                        _allocatedBytesSupported = false;
                        _allocatedBytes = 0;
                    }
                }
            }
            frame.Started = Stopwatch.GetTimestamp();
            _frames[_depth++] = frame;
            return new Scope(token);
        }

        /// <summary>
        /// Reuses immutable runtime type labels while preserving custom type name getters.
        /// </summary>
        /// <param name="subjectType">The optional subject category.</param>
        /// <returns>The subject name, or null when no category was supplied.</returns>
        private static string GetSubjectLabel(Type subjectType)
        {
            if (subjectType is null)
                return null;
            if (subjectType.GetType() != typeof(object).GetType())
                return subjectType.Name;

            _subjectLabels ??= new Dictionary<Type, string>();
            if (!_subjectLabels.TryGetValue(subjectType, out string label))
            {
                label = subjectType.Name;
                _subjectLabels.Add(subjectType, label);
            }
            return label;
        }

        /// <summary>
        /// Completes one enabled scope and folds its result into the active segment.
        /// </summary>
        /// <param name="token">The scope token to complete.</param>
        private static void EndScope(int token)
        {
            if (!_enabled || token == 0)
                return;

            try
            {
                if (
                    Thread.CurrentThread.ManagedThreadId != _ownerThreadId
                    || _depth == 0
                    || _frames[_depth - 1].Token != token
                )
                {
                    Disable();
                    return;
                }

                long ended = Stopwatch.GetTimestamp();
                Frame frame = _frames[--_depth];
                long inclusiveTicks = Math.Max(0, ended - frame.Started);
                long selfTicks = Math.Max(0, inclusiveTicks - frame.ChildTicks);
                int generationZeroCollections = 0;
                int generationOneCollections = 0;
                int generationTwoCollections = 0;
                long processCpuTicks = 0;
                long allocatedBytes = 0;
                bool processCpuRead = false;
                bool allocatedBytesRead = false;
                if (_depth == 0)
                {
                    generationZeroCollections = GC.CollectionCount(0);
                    if (_gcMaxGeneration >= 1)
                        generationOneCollections = GC.CollectionCount(1);
                    if (_gcMaxGeneration >= 2)
                        generationTwoCollections = GC.CollectionCount(2);
                    processCpuRead =
                        _processCpuSupported && TryReadProcessCpuTicks(out processCpuTicks);
                    allocatedBytesRead =
                        _allocatedBytesSupported && TryReadAllocatedBytes(out allocatedBytes);
                }
                if (_operations.TryGetValue(frame.Key, out OperationAggregate aggregate))
                {
                    aggregate.Calls++;
                    aggregate.InclusiveTicks += inclusiveTicks;
                    aggregate.SelfTicks += selfTicks;
                    aggregate.MaxTicks = Math.Max(aggregate.MaxTicks, inclusiveTicks);
                    aggregate.Items += frame.Items;
                    _operations[frame.Key] = aggregate;
                }
                else
                {
                    _operations.Add(
                        frame.Key,
                        new OperationAggregate
                        {
                            Calls = 1,
                            InclusiveTicks = inclusiveTicks,
                            SelfTicks = selfTicks,
                            MaxTicks = inclusiveTicks,
                            Items = frame.Items,
                        }
                    );
                }

                if (_depth > 0)
                {
                    Frame parent = _frames[_depth - 1];
                    parent.ChildTicks += inclusiveTicks;
                    _frames[_depth - 1] = parent;
                    return;
                }

                _activeTicks += inclusiveTicks;
                _rootChunks++;
                _generationZeroCollections += Math.Max(
                    0,
                    generationZeroCollections - frame.GenerationZeroCollections
                );
                if (_gcMaxGeneration >= 1)
                    _generationOneCollections += Math.Max(
                        0,
                        generationOneCollections - frame.GenerationOneCollections
                    );
                if (_gcMaxGeneration >= 2)
                    _generationTwoCollections += Math.Max(
                        0,
                        generationTwoCollections - frame.GenerationTwoCollections
                    );
                if (_processCpuSupported)
                {
                    if (processCpuRead)
                        _processCpuTicks += Math.Max(0, processCpuTicks - frame.ProcessCpuTicks);
                    else
                    {
                        _processCpuSupported = false;
                        _processCpuTicks = 0;
                    }
                }
                if (_allocatedBytesSupported)
                {
                    if (allocatedBytesRead)
                        _allocatedBytes += Math.Max(0, allocatedBytes - frame.AllocatedBytes);
                    else
                    {
                        _allocatedBytesSupported = false;
                        _allocatedBytes = 0;
                    }
                }
            }
            catch
            {
                Disable();
            }
        }

        /// <summary>
        /// Enumerates and measures each source step while disposing the scope before yielding.
        /// </summary>
        /// <typeparam name="T">The yielded value type.</typeparam>
        /// <param name="source">The source enumerable.</param>
        /// <param name="operation">The stable operation label.</param>
        /// <param name="factionId">The explicit owner label, or null.</param>
        /// <param name="items">The represented item count for each step.</param>
        /// <param name="subjectType">The optional subject category.</param>
        /// <returns>The measured sequence.</returns>
        private static IEnumerable<T> MeasureStepsEnabled<T>(
            IEnumerable<T> source,
            string operation,
            string factionId,
            long items,
            Type subjectType
        )
        {
            using IEnumerator<T> enumerator = source.GetEnumerator();
            while (true)
            {
                bool hasCurrent;
                T current = default;
                using (Measure(operation, factionId, items, subjectType))
                {
                    hasCurrent = enumerator.MoveNext();
                    if (hasCurrent)
                        current = enumerator.Current;
                }

                if (!hasCurrent)
                    yield break;
                yield return current;
            }
        }

        /// <summary>
        /// Creates and validates the optional process CPU counter.
        /// </summary>
        /// <param name="process">The process counter when supported.</param>
        /// <returns>True when the process CPU counter can be read.</returns>
        private static bool TryCreateProcess(out Process process)
        {
            process = null;
            try
            {
                process = Process.GetCurrentProcess();
                _ = process.TotalProcessorTime;
                return true;
            }
            catch
            {
                try
                {
                    process?.Dispose();
                }
                catch
                {
                    // Optional diagnostics remain disabled when cleanup is unavailable.
                }
                process = null;
                return false;
            }
        }

        /// <summary>
        /// Resolves and validates the current-thread allocation counter.
        /// </summary>
        /// <param name="reader">The allocation counter when supported.</param>
        /// <returns>True when the runtime reports changing allocation counts.</returns>
        private static bool TryCreateAllocatedBytesReader(out Func<long> reader)
        {
            reader = null;
            try
            {
                MethodInfo method = typeof(GC).GetMethod(
                    "GetAllocatedBytesForCurrentThread",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null
                );
                if (method == null || method.ReturnType != typeof(long))
                    return false;
                reader = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                long before = reader();
                byte[] probe = new byte[32];
                long after = reader();
                GC.KeepAlive(probe);
                if (after <= before)
                {
                    reader = null;
                    return false;
                }
                return true;
            }
            catch
            {
                reader = null;
                return false;
            }
        }

        /// <summary>
        /// Reads the optional process CPU counter.
        /// </summary>
        /// <param name="ticks">The process CPU duration in TimeSpan ticks.</param>
        /// <returns>True when the counter was read.</returns>
        private static bool TryReadProcessCpuTicks(out long ticks)
        {
            ticks = 0;
            try
            {
                if (_process == null)
                    return false;
                ticks = _process.TotalProcessorTime.Ticks;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the optional current-thread allocation counter.
        /// </summary>
        /// <param name="bytes">The allocated byte count.</param>
        /// <returns>True when the counter was read.</returns>
        private static bool TryReadAllocatedBytes(out long bytes)
        {
            bytes = 0;
            try
            {
                if (_allocatedBytesReader == null)
                    return false;
                bytes = _allocatedBytesReader();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Converts monotonic stopwatch ticks to milliseconds.
        /// </summary>
        /// <param name="ticks">The stopwatch duration.</param>
        /// <returns>The duration in milliseconds.</returns>
        private static double ToMilliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;

        /// <summary>
        /// Disables recording and releases optional resources without surfacing logger failures.
        /// </summary>
        private static void Disable()
        {
            _enabled = false;
            _segmentActive = false;
            _depth = 0;
            _ownerThreadId = 0;
            _subjectLabels?.Clear();
            try
            {
                _writer?.Dispose();
            }
            catch
            {
                // Logging failures must not interrupt gameplay.
            }
            _writer = null;
            try
            {
                _process?.Dispose();
            }
            catch
            {
                // Optional counter cleanup must not interrupt gameplay.
            }
            _process = null;
            _allocatedBytesReader = null;
            _processCpuSupported = false;
            _allocatedBytesSupported = false;
        }

        /// <summary>
        /// Represents one allocation-free synchronous measurement token.
        /// </summary>
        public readonly struct Scope : IDisposable
        {
            private readonly int _token;

            /// <summary>
            /// Creates an enabled measurement scope.
            /// </summary>
            /// <param name="token">The active frame token.</param>
            internal Scope(int token)
            {
                _token = token;
            }

            /// <summary>
            /// Completes the associated measurement when enabled.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                if (_token != 0)
                    EndScope(_token);
            }
        }

        private readonly struct OperationKey : IEquatable<OperationKey>
        {
            public readonly string FactionId;
            public readonly string Operation;
            public readonly string SubjectType;
            public readonly string ParentOperation;
            public readonly string ParentSubjectType;

            /// <summary>
            /// Creates an aggregate operation key.
            /// </summary>
            /// <param name="factionId">The inherited or explicit owner label.</param>
            /// <param name="operation">The operation label.</param>
            /// <param name="subjectType">The subject type label.</param>
            /// <param name="parentOperation">The immediate parent operation label.</param>
            /// <param name="parentSubjectType">The immediate parent subject type label.</param>
            public OperationKey(
                string factionId,
                string operation,
                string subjectType,
                string parentOperation,
                string parentSubjectType
            )
            {
                FactionId = factionId;
                Operation = operation;
                SubjectType = subjectType;
                ParentOperation = parentOperation;
                ParentSubjectType = parentSubjectType;
            }

            /// <summary>
            /// Returns whether another key contains the same aggregate labels.
            /// </summary>
            /// <param name="other">The key to compare.</param>
            /// <returns>True when all labels match ordinally.</returns>
            public bool Equals(OperationKey other) =>
                string.Equals(FactionId, other.FactionId, StringComparison.Ordinal)
                && string.Equals(Operation, other.Operation, StringComparison.Ordinal)
                && string.Equals(SubjectType, other.SubjectType, StringComparison.Ordinal)
                && string.Equals(ParentOperation, other.ParentOperation, StringComparison.Ordinal)
                && string.Equals(
                    ParentSubjectType,
                    other.ParentSubjectType,
                    StringComparison.Ordinal
                );

            /// <summary>
            /// Returns whether an object is an equivalent operation key.
            /// </summary>
            /// <param name="obj">The object to compare.</param>
            /// <returns>True when the object contains the same labels.</returns>
            public override bool Equals(object obj) => obj is OperationKey other && Equals(other);

            /// <summary>
            /// Returns an ordinal hash for the aggregate labels.
            /// </summary>
            /// <returns>The aggregate hash code.</returns>
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash =
                        hash * 31 + StringComparer.Ordinal.GetHashCode(FactionId ?? string.Empty);
                    hash =
                        hash * 31 + StringComparer.Ordinal.GetHashCode(Operation ?? string.Empty);
                    hash =
                        hash * 31 + StringComparer.Ordinal.GetHashCode(SubjectType ?? string.Empty);
                    hash =
                        hash * 31
                        + StringComparer.Ordinal.GetHashCode(ParentOperation ?? string.Empty);
                    hash =
                        hash * 31
                        + StringComparer.Ordinal.GetHashCode(ParentSubjectType ?? string.Empty);
                    return hash;
                }
            }
        }

        private struct Frame
        {
            public int Token;
            public OperationKey Key;
            public long Items;
            public long Started;
            public long ChildTicks;
            public long ProcessCpuTicks;
            public long AllocatedBytes;
            public int GenerationZeroCollections;
            public int GenerationOneCollections;
            public int GenerationTwoCollections;
        }

        private struct OperationAggregate
        {
            public long Calls;
            public long InclusiveTicks;
            public long SelfTicks;
            public long MaxTicks;
            public long Items;
        }

        [Serializable]
        private sealed class HeaderRecord
        {
            public string Kind;
            public int FormatVersion;
            public string CreatedUtc;
            public int GcMaxGeneration;
            public bool ProcessCpuSupported;
            public bool AllocatedBytesSupported;
        }

        [Serializable]
        private sealed class TickSegmentRecord
        {
            public string Kind;
            public int Tick;
            public string State;
            public long Sequence;
            public string StartedUtc;
            public double WallMs;
            public double ActiveMs;
            public long RootChunks;
            public int GcMaxGeneration;
            public int GenerationZeroCollections;
            public int GenerationOneCollections;
            public int GenerationTwoCollections;
            public bool ProcessCpuSupported;
            public double ProcessCpuMs;
            public bool AllocatedBytesSupported;
            public long AllocatedBytes;
            public OperationRecord[] Operations;
        }

        [Serializable]
        private sealed class OperationRecord
        {
            public string FactionId;
            public string Operation;
            public string SubjectType;
            public string ParentOperation;
            public string ParentSubjectType;
            public long Calls;
            public double InclusiveMs;
            public double SelfMs;
            public double MaxMs;
            public long Items;
        }
    }
}
