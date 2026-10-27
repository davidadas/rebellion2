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
    /// </remarks>
    internal static class MissionEncounterOdds
    {
        /// <summary>
        /// Advances surviving decoy pools through one checkpoint and returns terminal risks.
        /// </summary>
        /// <param name="detectors">Ordered defender probabilities for this checkpoint.</param>
        /// <param name="decoyIndexes">Indexes of decoys present at this checkpoint.</param>
        /// <param name="survivors">Incoming unfoiled probability by decoy pool.</param>
        /// <returns>The checkpoint's foil and primary-officer-loss probabilities, from zero to one.</returns>
        internal static Result Apply(
            IReadOnlyList<DefenderProbabilities> detectors,
            IReadOnlyList<int> decoyIndexes,
            IReadOnlyDictionary<BigInteger, double> survivors
        )
        {
            if (detectors == null)
                throw new ArgumentNullException(nameof(detectors));
            if (decoyIndexes == null)
                throw new ArgumentNullException(nameof(decoyIndexes));
            if (survivors == null)
                throw new ArgumentNullException(nameof(survivors));
            if (detectors.Count == 0 || survivors.Count == 0)
                return new Result(0, 0, new Dictionary<BigInteger, double>(survivors));

            int officerCount = detectors[0].EvasionPercentages.Count;
            foreach (DefenderProbabilities detector in detectors)
                detector.ValidateDimensions(decoyIndexes, officerCount);

            Dictionary<State, Mass> states = new Dictionary<State, Mass>();
            foreach ((BigInteger pool, double probability) in survivors)
                Add(
                    states,
                    new State(pool, 0, EvasionTotals.Create(officerCount)),
                    new Mass(probability, 0)
                );

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
                        Add(next, state, mass.Scale(diversion / selectable));
                        Add(
                            next,
                            state.WithoutDecoy(index).WithDetector(detector),
                            mass.Detect(detector.NoFoilProbability)
                                .Scale((1d - diversion) / selectable)
                        );
                    }
                }
                states = next;
            }

            Dictionary<BigInteger, double> remaining = new Dictionary<BigInteger, double>();
            double foil = 0;
            double loss = 0;
            foreach ((State state, Mass mass) in states)
            {
                foil += mass.Foiled;
                loss += mass.Foiled * state.GetPersonnelLossProbability();
                if (mass.Unfoiled > 0)
                {
                    remaining.TryGetValue(state.Decoys, out double existing);
                    remaining[state.Decoys] = existing + mass.Unfoiled;
                }
            }
            return new Result(foil, loss, remaining);
        }

        /// <summary>
        /// Contains the probabilities and survivors produced by one encounter checkpoint.
        /// </summary>
        internal sealed class Result
        {
            internal double FoilProbability { get; }
            internal double PersonnelLossProbability { get; }
            internal IReadOnlyDictionary<BigInteger, double> Survivors { get; }

            /// <summary>
            /// Creates an encounter result.
            /// </summary>
            /// <param name="foilProbability">The probability that the mission is foiled.</param>
            /// <param name="personnelLossProbability">The probability that a primary officer is lost.</param>
            /// <param name="survivors">Unfoiled probability mass grouped by remaining decoys.</param>
            internal Result(
                double foilProbability,
                double personnelLossProbability,
                IReadOnlyDictionary<BigInteger, double> survivors
            )
            {
                FoilProbability = foilProbability;
                PersonnelLossProbability = personnelLossProbability;
                Survivors = survivors ?? throw new ArgumentNullException(nameof(survivors));
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
                existing.Foiled + mass.Foiled
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

            /// <summary>
            /// Creates one defender's immutable encounter inputs.
            /// </summary>
            /// <param name="noFoilProbability">The chance this defender does not foil the team.</param>
            /// <param name="diversionProbabilities">Diversion chances indexed by mission decoy.</param>
            /// <param name="evasionPercentages">Each primary officer's integer evasion percentage.</param>
            internal DefenderProbabilities(
                double noFoilProbability,
                IReadOnlyList<double> diversionProbabilities,
                IReadOnlyList<int> evasionPercentages
            )
            {
                if (noFoilProbability < 0 || noFoilProbability > 1)
                    throw new ArgumentOutOfRangeException(nameof(noFoilProbability));
                NoFoilProbability = noFoilProbability;
                DiversionProbabilities =
                    diversionProbabilities?.ToArray()
                    ?? throw new ArgumentNullException(nameof(diversionProbabilities));
                EvasionPercentages =
                    evasionPercentages?.ToArray()
                    ?? throw new ArgumentNullException(nameof(evasionPercentages));
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

            /// <summary>
            /// Creates the two probability channels.
            /// </summary>
            /// <param name="unfoiled">The weight with no successful foil.</param>
            /// <param name="foiled">The weight with at least one successful foil.</param>
            internal Mass(double unfoiled, double foiled)
            {
                Unfoiled = unfoiled;
                Foiled = foiled;
            }

            /// <summary>
            /// Advances both channels past one active detector.
            /// </summary>
            /// <param name="noFoilProbability">The defender's no-foil chance.</param>
            /// <returns>The updated probability channels.</returns>
            internal Mass Detect(double noFoilProbability) =>
                new Mass(
                    Unfoiled * noFoilProbability,
                    Foiled + Unfoiled * (1d - noFoilProbability)
                );

            /// <summary>
            /// Applies a decoy-selection and diversion branch weight.
            /// </summary>
            /// <param name="probability">The branch weight.</param>
            /// <returns>The scaled probability channels.</returns>
            internal Mass Scale(double probability) =>
                new Mass(Unfoiled * probability, Foiled * probability);
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

            /// <summary>
            /// Creates an immutable sufficient-statistics key.
            /// </summary>
            /// <param name="decoys">The available-decoy bit set.</param>
            /// <param name="detectorCount">The number of active defenders.</param>
            /// <param name="evasionTotals">Immutable sums of integer evasion percentages.</param>
            internal State(BigInteger decoys, int detectorCount, EvasionTotals evasionTotals)
            {
                Decoys = decoys;
                _detectorCount = detectorCount;
                _evasionTotals =
                    evasionTotals ?? throw new ArgumentNullException(nameof(evasionTotals));
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
                new State(Decoys & ~(BigInteger.One << index), _detectorCount, _evasionTotals);

            /// <summary>
            /// Accumulates one active defender's evasion values.
            /// </summary>
            /// <param name="detector">The defender to include.</param>
            /// <returns>The updated key, or this key when officer-loss details are not requested.</returns>
            internal State WithDetector(DefenderProbabilities detector)
            {
                if (_evasionTotals.Count == 0)
                    return this;
                return new State(
                    Decoys,
                    _detectorCount + 1,
                    _evasionTotals.Add(detector.EvasionPercentages)
                );
            }

            /// <summary>
            /// Calculates the shared conditional officer-loss factor for these histories.
            /// </summary>
            /// <returns>The conditional probability that at least one primary officer is lost.</returns>
            internal double GetPersonnelLossProbability()
            {
                if (_detectorCount == 0)
                    return 0;
                double noLoss = 1;
                foreach (long total in _evasionTotals.Values)
                    noLoss *= total / (100d * _detectorCount);
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
                hash.Add(_evasionTotals);
                return hash.ToHashCode();
            }
        }
    }
}
