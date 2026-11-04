using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Combines exact encounter probabilities using sufficient statistics instead of defender histories.
    /// </summary>
    /// <remarks>
    /// A history has weight p, no-foil factor q, k active defenders, and evasion-percentage
    /// sums s[o]. Its foil contribution is p*(1-q), and its officer-loss contribution is
    /// p*(1-q)*(1-product(s[o]/(100*k))). Future decoy choices depend only on the remaining
    /// decoy pool. Histories with the same pool, k, and s can therefore be merged by adding
    /// their unfoiled and foiled weights. An active defender with no-foil chance r transforms
    /// (u,f) into (u*r, f+u*(1-r)); a diverted defender leaves both weights unchanged.
    /// These linear transitions preserve the history sums by induction. No probability mass
    /// is sampled, truncated, or discarded. Floating-point summation order can differ.
    /// Parallel no-loss channels retain earlier decoy-officer losses without counting a later
    /// primary-officer loss twice. Per-officer injury survival factors apply to terminal risks.
    /// </remarks>
    internal static class MissionEncounterOdds
    {
        /// <summary>
        /// Reuses exact numerical encounters in a bounded, caller-owned least-recently-used cache.
        /// </summary>
        internal sealed class Cache
        {
            private readonly int _capacity;
            private readonly Dictionary<
                Input,
                LinkedListNode<(Input Input, Result Result)>
            > _results = new();
            private readonly LinkedList<(Input Input, Result Result)> _recent = new();

            /// <summary>
            /// Creates an empty bounded numerical cache without retaining gameplay entities.
            /// </summary>
            /// <param name="capacity">Maximum retained encounters; nonpositive values disable reuse.</param>
            internal Cache(int capacity) => _capacity = Math.Max(0, capacity);

            /// <summary>
            /// Returns the exact result for an immutable snapshot of encounter inputs.
            /// </summary>
            /// <param name="detectors">Ordered defender probabilities.</param>
            /// <param name="decoyIndexes">Ordered indexes of decoys present.</param>
            /// <param name="survivors">Incoming unfoiled weight by decoy pool.</param>
            /// <param name="noLossSurvivors">Incoming no-loss subset, or null when equal to survivors.</param>
            /// <returns>The shared read-only encounter result.</returns>
            internal Result Evaluate(
                IReadOnlyList<DefenderProbabilities> detectors,
                IReadOnlyList<int> decoyIndexes,
                IReadOnlyDictionary<BigInteger, double> survivors,
                IReadOnlyDictionary<BigInteger, double> noLossSurvivors = null
            )
            {
                if (_capacity == 0)
                    return Apply(detectors, decoyIndexes, survivors, noLossSurvivors);
                Input input = new(detectors, decoyIndexes, survivors, noLossSurvivors);
                if (_results.TryGetValue(input, out var found))
                {
                    _recent.Remove(found);
                    _recent.AddLast(found);
                    return found.Value.Result;
                }
                Result result = Apply(detectors, decoyIndexes, survivors, noLossSurvivors);
                if (_results.Count == _capacity)
                {
                    _results.Remove(_recent.First.Value.Input);
                    _recent.RemoveFirst();
                }
                _results.Add(input, _recent.AddLast((input, result)));
                return result;
            }
        }

        /// <summary>
        /// Identifies a numerical encounter without depending on gameplay entity identities.
        /// </summary>
        private sealed class Input : IEquatable<Input>
        {
            private readonly int[] _decoyIndexes;
            private readonly DefenderProbabilities[] _detectors;
            private readonly BigInteger[] _pools;
            private readonly double[] _values;
            private readonly int _hashCode;

            /// <summary>
            /// Snapshots incoming weights and shares immutable numerical defender packets.
            /// </summary>
            /// <param name="detectors">Ordered defender probabilities.</param>
            /// <param name="decoyIndexes">Ordered present decoy indexes.</param>
            /// <param name="survivors">Incoming unfoiled weights.</param>
            /// <param name="noLossSurvivors">Incoming no-loss weights, or null for all incoming weight.</param>
            internal Input(
                IReadOnlyList<DefenderProbabilities> detectors,
                IReadOnlyList<int> decoyIndexes,
                IReadOnlyDictionary<BigInteger, double> survivors,
                IReadOnlyDictionary<BigInteger, double> noLossSurvivors
            )
            {
                if (detectors == null)
                    throw new ArgumentNullException(nameof(detectors));
                if (decoyIndexes == null)
                    throw new ArgumentNullException(nameof(decoyIndexes));
                if (survivors == null)
                    throw new ArgumentNullException(nameof(survivors));
                _decoyIndexes = decoyIndexes.ToArray();
                _detectors = detectors.ToArray();
                _pools = new BigInteger[survivors.Count];
                _values = new double[survivors.Count * 2];
                int index = 0;
                foreach (var pair in survivors)
                {
                    _pools[index] = pair.Key;
                    _values[index * 2] = pair.Value;
                    _values[index * 2 + 1] =
                        noLossSurvivors == null ? pair.Value : noLossSurvivors[pair.Key];
                    index++;
                }
                HashCode hash = new();
                AddValues(ref hash, _decoyIndexes);
                AddValues(ref hash, _pools);
                AddValues(ref hash, _values);
                hash.Add(_detectors.Length);
                foreach (DefenderProbabilities detector in _detectors)
                {
                    hash.Add(detector.NoFoilProbability);
                    AddValues(ref hash, detector.DiversionProbabilities);
                    AddValues(ref hash, detector.EvasionPercentages);
                    AddValues(ref hash, detector.DecoyLossProbabilities);
                    AddValues(ref hash, detector.OfficerSurvivalModifiers);
                }
                _hashCode = hash.ToHashCode();
            }

            /// <summary>
            /// Compares all copied numerical inputs rather than only their hash.
            /// </summary>
            /// <param name="other">The other encounter snapshot.</param>
            /// <returns>True when both computations have identical inputs and traversal order.</returns>
            public bool Equals(Input other)
            {
                if (
                    other == null
                    || _hashCode != other._hashCode
                    || _detectors.Length != other._detectors.Length
                    || !SameValues(_decoyIndexes, other._decoyIndexes)
                    || !SameValues(_pools, other._pools)
                    || !SameValues(_values, other._values)
                )
                    return false;
                for (int index = 0; index < _detectors.Length; index++)
                {
                    DefenderProbabilities a = _detectors[index];
                    DefenderProbabilities b = other._detectors[index];
                    if (
                        !a.NoFoilProbability.Equals(b.NoFoilProbability)
                        || !SameValues(a.DiversionProbabilities, b.DiversionProbabilities)
                        || !SameValues(a.EvasionPercentages, b.EvasionPercentages)
                        || !SameValues(a.DecoyLossProbabilities, b.DecoyLossProbabilities)
                        || !SameValues(a.OfficerSurvivalModifiers, b.OfficerSurvivalModifiers)
                    )
                        return false;
                }
                return true;
            }

            /// <summary>
            /// Compares an object to this numerical encounter.
            /// </summary>
            /// <param name="obj">The object to compare.</param>
            /// <returns>True when it has identical numerical inputs.</returns>
            public override bool Equals(object obj) => Equals(obj as Input);

            /// <summary>
            /// Returns the precomputed structural hash.
            /// </summary>
            /// <returns>The numerical-input hash.</returns>
            public override int GetHashCode() => _hashCode;

            /// <summary>
            /// Adds one complete numerical vector without allocating an enumerator.
            /// </summary>
            /// <typeparam name="T">The numerical element type.</typeparam>
            /// <param name="hash">The accumulated structural hash.</param>
            /// <param name="values">The numerical vector.</param>
            private static void AddValues<T>(ref HashCode hash, IReadOnlyList<T> values)
            {
                hash.Add(values.Count);
                for (int index = 0; index < values.Count; index++)
                    hash.Add(values[index]);
            }

            /// <summary>
            /// Compares a complete numerical vector without allocating an enumerator.
            /// </summary>
            /// <typeparam name="T">The numerical element type.</typeparam>
            /// <param name="a">The first vector.</param>
            /// <param name="b">The second vector.</param>
            /// <returns>True when every numerical value and its order match.</returns>
            private static bool SameValues<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
            {
                if (a.Count != b.Count)
                    return false;
                for (int index = 0; index < a.Count; index++)
                    if (!EqualityComparer<T>.Default.Equals(a[index], b[index]))
                        return false;
                return true;
            }
        }

        /// <summary>
        /// Advances surviving decoy pools through one checkpoint and returns terminal risks.
        /// </summary>
        /// <param name="detectors">Ordered defender probabilities for this checkpoint.</param>
        /// <param name="decoyIndexes">Indexes of decoys present at this checkpoint.</param>
        /// <param name="survivors">Incoming unfoiled probability by decoy pool.</param>
        /// <param name="noLossSurvivors">Incoming unfoiled weight with every officer surviving, or null when equal to survivors.</param>
        /// <returns>The checkpoint's foil and officer-loss probabilities, from zero to one.</returns>
        internal static Result Apply(
            IReadOnlyList<DefenderProbabilities> detectors,
            IReadOnlyList<int> decoyIndexes,
            IReadOnlyDictionary<BigInteger, double> survivors,
            IReadOnlyDictionary<BigInteger, double> noLossSurvivors = null
        )
        {
            if (detectors == null)
                throw new ArgumentNullException(nameof(detectors));
            if (decoyIndexes == null)
                throw new ArgumentNullException(nameof(decoyIndexes));
            if (survivors == null)
                throw new ArgumentNullException(nameof(survivors));
            if (detectors.Count == 0 || survivors.Count == 0)
                return new Result(
                    0,
                    0,
                    new Dictionary<BigInteger, double>(survivors),
                    noLossSurvivors
                );

            int officerCount = detectors[0].EvasionPercentages.Count;
            foreach (DefenderProbabilities detector in detectors)
            {
                detector.ValidateDimensions(decoyIndexes, officerCount);
                if (
                    !detector.OfficerSurvivalModifiers.SequenceEqual(
                        detectors[0].OfficerSurvivalModifiers
                    )
                )
                    throw new ArgumentException(
                        "Officer survival modifiers must align across defenders."
                    );
            }

            bool uniformEvasion = detectors.All(detector =>
                detector.EvasionPercentages.SequenceEqual(detectors[0].EvasionPercentages)
            );
            EvasionTotals initialEvasion = uniformEvasion
                ? EvasionTotals.Create(detectors[0].EvasionPercentages)
                : EvasionTotals.Create(officerCount);
            Dictionary<State, Mass> states = new Dictionary<State, Mass>();
            foreach ((BigInteger pool, double probability) in survivors)
                Add(
                    states,
                    new State(pool, 0, initialEvasion, uniformEvasion),
                    new Mass(
                        probability,
                        0,
                        noLossSurvivors == null ? probability : noLossSurvivors[pool],
                        0
                    )
                );

            double loss = 0;
            foreach (DefenderProbabilities detector in detectors)
            {
                Dictionary<State, Mass> next = new Dictionary<State, Mass>();
                foreach ((State state, Mass mass) in states)
                {
                    int selectable = 0;
                    foreach (int index in decoyIndexes)
                        if (state.HasDecoy(index))
                            selectable++;

                    if (selectable == 0)
                    {
                        Add(
                            next,
                            state.WithDetector(detector),
                            mass.Detect(detector.NoFoilProbability)
                        );
                        continue;
                    }

                    foreach (int index in decoyIndexes)
                    {
                        if (!state.HasDecoy(index))
                            continue;
                        double diversion = detector.DiversionProbabilities[index];
                        double confronted = (1d - diversion) / selectable;
                        double decoyLoss = detector.DecoyLossProbabilities[index];
                        Add(next, state, mass.Scale(diversion / selectable));
                        loss += (mass.UnfoiledNoLoss + mass.FoiledNoLoss) * confronted * decoyLoss;
                        Add(
                            next,
                            state.WithoutDecoy(index).WithDetector(detector),
                            mass.Detect(detector.NoFoilProbability)
                                .Scale(confronted)
                                .SurviveDecoy(1d - decoyLoss)
                        );
                    }
                }
                states = next;
            }

            Dictionary<BigInteger, double> remaining = new Dictionary<BigInteger, double>();
            Dictionary<BigInteger, double> remainingNoLoss = new Dictionary<BigInteger, double>();
            double foil = 0;
            foreach ((State state, Mass mass) in states)
            {
                foil += mass.Foiled;
                loss +=
                    mass.FoiledNoLoss
                    * state.GetPersonnelLossProbability(detectors[0].OfficerSurvivalModifiers);
                if (mass.Unfoiled > 0)
                {
                    remaining.TryGetValue(state.Decoys, out double existing);
                    remaining[state.Decoys] = existing + mass.Unfoiled;
                    remainingNoLoss.TryGetValue(state.Decoys, out double existingNoLoss);
                    remainingNoLoss[state.Decoys] = existingNoLoss + mass.UnfoiledNoLoss;
                }
            }
            return new Result(foil, loss, remaining, remainingNoLoss);
        }

        /// <summary>
        /// Contains the probabilities and survivors produced by one encounter checkpoint.
        /// </summary>
        internal sealed class Result
        {
            internal double FoilProbability { get; }
            internal double PersonnelLossProbability { get; }
            internal IReadOnlyDictionary<BigInteger, double> Survivors { get; }
            internal IReadOnlyDictionary<BigInteger, double> NoLossSurvivors { get; }

            /// <summary>
            /// Creates an encounter result.
            /// </summary>
            /// <param name="foilProbability">The probability that the mission is foiled.</param>
            /// <param name="personnelLossProbability">The probability that an officer is lost.</param>
            /// <param name="survivors">Unfoiled probability mass grouped by remaining decoys.</param>
            /// <param name="noLossSurvivors">The unfoiled subset with every officer surviving, or null when equal to survivors.</param>
            internal Result(
                double foilProbability,
                double personnelLossProbability,
                IReadOnlyDictionary<BigInteger, double> survivors,
                IReadOnlyDictionary<BigInteger, double> noLossSurvivors = null
            )
            {
                FoilProbability = foilProbability;
                PersonnelLossProbability = personnelLossProbability;
                if (survivors == null)
                    throw new ArgumentNullException(nameof(survivors));
                Survivors = new System.Collections.ObjectModel.ReadOnlyDictionary<
                    BigInteger,
                    double
                >(new Dictionary<BigInteger, double>(survivors));
                NoLossSurvivors =
                    noLossSurvivors == null
                        ? Survivors
                        : new System.Collections.ObjectModel.ReadOnlyDictionary<BigInteger, double>(
                            new Dictionary<BigInteger, double>(noLossSurvivors)
                        );
            }
        }

        /// <summary>
        /// Adds both probability channels to an equivalent state.
        /// </summary>
        /// <param name="states">The destination distribution.</param>
        /// <param name="state">The sufficient-statistics key.</param>
        /// <param name="mass">The probability channels to add.</param>
        private static void Add(Dictionary<State, Mass> states, State state, Mass mass)
        {
            if (mass.Unfoiled <= 0 && mass.Foiled <= 0)
                return;
            states.TryGetValue(state, out Mass existing);
            states[state] = new Mass(
                existing.Unfoiled + mass.Unfoiled,
                existing.Foiled + mass.Foiled,
                existing.UnfoiledNoLoss + mass.UnfoiledNoLoss,
                existing.FoiledNoLoss + mass.FoiledNoLoss
            );
        }

        /// <summary>
        /// Contains fixed defender probabilities resolved before state enumeration.
        /// </summary>
        internal sealed class DefenderProbabilities
        {
            internal double NoFoilProbability { get; }
            internal IReadOnlyList<double> DiversionProbabilities { get; }
            internal IReadOnlyList<int> EvasionPercentages { get; }
            internal IReadOnlyList<double> DecoyLossProbabilities { get; }
            internal IReadOnlyList<double> OfficerSurvivalModifiers { get; }

            /// <summary>
            /// Creates one defender's immutable encounter inputs.
            /// </summary>
            /// <param name="noFoilProbability">The chance this defender does not foil the team.</param>
            /// <param name="diversionProbabilities">Diversion chances indexed by mission decoy.</param>
            /// <param name="evasionPercentages">Each primary officer's integer evasion percentage.</param>
            /// <param name="decoyLossProbabilities">The officer-loss chance after each decoy fails diversion, or null for no decoy losses.</param>
            /// <param name="officerSurvivalModifiers">Each primary officer's survival factor after injury checks, or null for guaranteed survival.</param>
            internal DefenderProbabilities(
                double noFoilProbability,
                IReadOnlyList<double> diversionProbabilities,
                IReadOnlyList<int> evasionPercentages,
                IReadOnlyList<double> decoyLossProbabilities = null,
                IReadOnlyList<double> officerSurvivalModifiers = null
            )
            {
                if (noFoilProbability < 0 || noFoilProbability > 1)
                    throw new ArgumentOutOfRangeException(nameof(noFoilProbability));
                NoFoilProbability = noFoilProbability;
                DiversionProbabilities = Array.AsReadOnly(
                    diversionProbabilities?.ToArray()
                        ?? throw new ArgumentNullException(nameof(diversionProbabilities))
                );
                EvasionPercentages = Array.AsReadOnly(
                    evasionPercentages?.ToArray()
                        ?? throw new ArgumentNullException(nameof(evasionPercentages))
                );
                DecoyLossProbabilities = Array.AsReadOnly(
                    decoyLossProbabilities?.ToArray() ?? new double[DiversionProbabilities.Count]
                );
                OfficerSurvivalModifiers = Array.AsReadOnly(
                    officerSurvivalModifiers?.ToArray()
                        ?? Enumerable.Repeat(1d, EvasionPercentages.Count).ToArray()
                );
                if (
                    DecoyLossProbabilities.Count != DiversionProbabilities.Count
                    || DecoyLossProbabilities.Any(value => value < 0 || value > 1)
                )
                    throw new ArgumentOutOfRangeException(nameof(decoyLossProbabilities));
                if (
                    OfficerSurvivalModifiers.Count != EvasionPercentages.Count
                    || OfficerSurvivalModifiers.Any(value => value < 0 || value > 1)
                )
                    throw new ArgumentOutOfRangeException(nameof(officerSurvivalModifiers));
                if (DiversionProbabilities.Any(value => value < 0 || value > 1))
                    throw new ArgumentOutOfRangeException(nameof(diversionProbabilities));
                if (EvasionPercentages.Any(value => value < 0 || value > 100))
                    throw new ArgumentOutOfRangeException(nameof(evasionPercentages));
            }

            /// <summary>
            /// Verifies that indexed probabilities match this encounter's dimensions.
            /// </summary>
            /// <param name="decoyIndexes">The decoys present at the checkpoint.</param>
            /// <param name="officerCount">The expected number of primary officers.</param>
            internal void ValidateDimensions(IReadOnlyList<int> decoyIndexes, int officerCount)
            {
                if (EvasionPercentages.Count != officerCount)
                    throw new ArgumentException("Defender officer probabilities must align.");
                foreach (int index in decoyIndexes)
                    if (index < 0 || index >= DiversionProbabilities.Count)
                        throw new ArgumentOutOfRangeException(nameof(decoyIndexes));
            }
        }

        /// <summary>
        /// Retains separate nonnegative unfoiled and foiled probability weights.
        /// </summary>
        private readonly struct Mass
        {
            internal double Unfoiled { get; }
            internal double Foiled { get; }
            internal double UnfoiledNoLoss { get; }
            internal double FoiledNoLoss { get; }

            /// <summary>
            /// Creates outcome weights and their no-loss subsets.
            /// </summary>
            /// <param name="unfoiled">The weight with no successful foil.</param>
            /// <param name="foiled">The weight with at least one successful foil.</param>
            /// <param name="unfoiledNoLoss">The unfoiled subset with every officer surviving, or null for all unfoiled weight.</param>
            /// <param name="foiledNoLoss">The foiled subset with every decoy officer surviving, or null for all foiled weight.</param>
            internal Mass(
                double unfoiled,
                double foiled,
                double? unfoiledNoLoss = null,
                double? foiledNoLoss = null
            )
            {
                Unfoiled = unfoiled;
                Foiled = foiled;
                UnfoiledNoLoss = unfoiledNoLoss ?? unfoiled;
                FoiledNoLoss = foiledNoLoss ?? foiled;
            }

            /// <summary>
            /// Advances both channels past one active detector.
            /// </summary>
            /// <param name="noFoilProbability">The defender's no-foil chance.</param>
            /// <returns>The updated probability channels.</returns>
            internal Mass Detect(double noFoilProbability) =>
                new Mass(
                    Unfoiled * noFoilProbability,
                    Foiled + Unfoiled * (1d - noFoilProbability),
                    UnfoiledNoLoss * noFoilProbability,
                    FoiledNoLoss + UnfoiledNoLoss * (1d - noFoilProbability)
                );

            /// <summary>
            /// Applies a decoy-selection and diversion branch weight.
            /// </summary>
            /// <param name="probability">The branch weight.</param>
            /// <returns>The scaled probability channels.</returns>
            internal Mass Scale(double probability) =>
                new Mass(
                    Unfoiled * probability,
                    Foiled * probability,
                    UnfoiledNoLoss * probability,
                    FoiledNoLoss * probability
                );

            /// <summary>
            /// Restricts the no-loss channels after a decoy confrontation.
            /// </summary>
            /// <param name="probability">The chance that the confronted decoy survives free.</param>
            /// <returns>The same outcome weights with updated no-loss subsets.</returns>
            internal Mass SurviveDecoy(double probability) =>
                new Mass(
                    Unfoiled,
                    Foiled,
                    UnfoiledNoLoss * probability,
                    FoiledNoLoss * probability
                );
        }

        /// <summary>
        /// Owns immutable officer-evasion totals used in encounter-state keys.
        /// </summary>
        private sealed class EvasionTotals : IEquatable<EvasionTotals>
        {
            private readonly long[] _values;
            private readonly int _hashCode;

            internal int Count => _values.Length;
            internal IEnumerable<long> Values => _values;

            /// <summary>
            /// Creates an immutable total vector and precomputes its hash code.
            /// </summary>
            /// <param name="values">The owned total values.</param>
            private EvasionTotals(long[] values)
            {
                _values = values ?? throw new ArgumentNullException(nameof(values));
                HashCode hash = new HashCode();
                foreach (long value in _values)
                    hash.Add(value);
                _hashCode = hash.ToHashCode();
            }

            /// <summary>
            /// Creates a zero-filled total vector.
            /// </summary>
            /// <param name="count">The number of officers represented.</param>
            /// <returns>The immutable total vector.</returns>
            internal static EvasionTotals Create(int count)
            {
                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count));
                return new EvasionTotals(new long[count]);
            }

            /// <summary>
            /// Copies a shared per-officer evasion vector.
            /// </summary>
            /// <param name="percentages">The evasion percentage against every defender.</param>
            /// <returns>The immutable evasion vector.</returns>
            internal static EvasionTotals Create(IReadOnlyList<int> percentages) =>
                new EvasionTotals(percentages.Select(value => (long)value).ToArray());

            /// <summary>
            /// Adds one defender's officer-evasion values.
            /// </summary>
            /// <param name="percentages">The defender's indexed evasion percentages.</param>
            /// <returns>A new immutable total vector.</returns>
            internal EvasionTotals Add(IReadOnlyList<int> percentages)
            {
                if (percentages == null || percentages.Count != _values.Length)
                    throw new ArgumentException(
                        "Officer probabilities must align.",
                        nameof(percentages)
                    );

                long[] totals = new long[_values.Length];
                for (int index = 0; index < totals.Length; index++)
                    totals[index] = _values[index] + percentages[index];
                return new EvasionTotals(totals);
            }

            /// <summary>
            /// Compares the complete immutable total vector.
            /// </summary>
            /// <param name="other">The other vector.</param>
            /// <returns>True when all values match.</returns>
            public bool Equals(EvasionTotals other)
            {
                if (ReferenceEquals(this, other))
                    return true;
                if (other == null || _hashCode != other._hashCode || Count != other.Count)
                    return false;
                for (int index = 0; index < _values.Length; index++)
                    if (_values[index] != other._values[index])
                        return false;
                return true;
            }

            /// <summary>
            /// Compares another object to this total vector.
            /// </summary>
            /// <param name="obj">The object to compare.</param>
            /// <returns>True when it is an equal total vector.</returns>
            public override bool Equals(object obj) => Equals(obj as EvasionTotals);

            /// <summary>
            /// Returns the precomputed structural hash code.
            /// </summary>
            /// <returns>The structural hash code.</returns>
            public override int GetHashCode() => _hashCode;
        }

        /// <summary>
        /// Identifies histories with identical future transitions and terminal loss factors.
        /// </summary>
        private readonly struct State : IEquatable<State>
        {
            internal BigInteger Decoys { get; }
            private readonly int _detectorCount;
            private readonly EvasionTotals _evasionTotals;
            private readonly bool _uniformEvasion;

            /// <summary>
            /// Creates an immutable sufficient-statistics key.
            /// </summary>
            /// <param name="decoys">The available-decoy bit set.</param>
            /// <param name="detectorCount">The number of active defenders.</param>
            /// <param name="evasionTotals">Immutable sums of integer evasion percentages.</param>
            /// <param name="uniformEvasion">Whether one fixed evasion vector represents every active-defender subset.</param>
            internal State(
                BigInteger decoys,
                int detectorCount,
                EvasionTotals evasionTotals,
                bool uniformEvasion
            )
            {
                Decoys = decoys;
                _detectorCount = detectorCount;
                _evasionTotals =
                    evasionTotals ?? throw new ArgumentNullException(nameof(evasionTotals));
                _uniformEvasion = uniformEvasion;
            }

            /// <summary>
            /// Returns whether the decoy remains available.
            /// </summary>
            /// <param name="index">The decoy index.</param>
            /// <returns>True when the decoy's bit is set.</returns>
            internal bool HasDecoy(int index) =>
                (Decoys & (BigInteger.One << index)) != BigInteger.Zero;

            /// <summary>
            /// Removes a decoy after its diversion fails.
            /// </summary>
            /// <param name="index">The failed decoy index.</param>
            /// <returns>The updated immutable key.</returns>
            internal State WithoutDecoy(int index) =>
                new State(
                    Decoys & ~(BigInteger.One << index),
                    _detectorCount,
                    _evasionTotals,
                    _uniformEvasion
                );

            /// <summary>
            /// Accumulates one active defender's evasion values.
            /// </summary>
            /// <param name="detector">The defender to include.</param>
            /// <returns>The updated key, or this key when officer-loss details are not requested.</returns>
            internal State WithDetector(DefenderProbabilities detector)
            {
                if (_evasionTotals.Count == 0)
                    return this;
                // Averages of an identical evasion vector are unchanged by defender count.
                // Only the distinction between no confrontation and at least one remains relevant.
                if (_uniformEvasion)
                    return _detectorCount == 0 ? new State(Decoys, 1, _evasionTotals, true) : this;
                return new State(
                    Decoys,
                    _detectorCount + 1,
                    _evasionTotals.Add(detector.EvasionPercentages),
                    false
                );
            }

            /// <summary>
            /// Calculates the shared conditional officer-loss factor for these histories.
            /// </summary>
            /// <param name="survivalModifiers">Each primary officer's survival factor after injury checks.</param>
            /// <returns>The conditional probability that at least one primary officer is lost.</returns>
            internal double GetPersonnelLossProbability(IReadOnlyList<double> survivalModifiers)
            {
                if (_detectorCount == 0)
                    return 0;
                double noLoss = 1;
                int officer = 0;
                foreach (long total in _evasionTotals.Values)
                    noLoss *= total / (100d * _detectorCount) * survivalModifiers[officer++];
                return 1d - noLoss;
            }

            /// <summary>
            /// Compares all sufficient statistics without comparing history identities.
            /// </summary>
            /// <param name="other">The other key.</param>
            /// <returns>True when both histories have identical future and terminal behavior.</returns>
            public bool Equals(State other)
            {
                if (
                    Decoys != other.Decoys
                    || _detectorCount != other._detectorCount
                    || _uniformEvasion != other._uniformEvasion
                    || !_evasionTotals.Equals(other._evasionTotals)
                )
                    return false;
                return true;
            }

            /// <summary>
            /// Compares another object to this sufficient-statistics key.
            /// </summary>
            /// <param name="obj">The object to compare.</param>
            /// <returns>True when the object is an equal state.</returns>
            public override bool Equals(object obj) => obj is State other && Equals(other);

            /// <summary>
            /// Hashes the immutable sufficient statistics.
            /// </summary>
            /// <returns>The dictionary hash code.</returns>
            public override int GetHashCode()
            {
                HashCode hash = new HashCode();
                hash.Add(Decoys);
                hash.Add(_detectorCount);
                hash.Add(_uniformEvasion);
                hash.Add(_evasionTotals);
                return hash.ToHashCode();
            }
        }
    }
}
