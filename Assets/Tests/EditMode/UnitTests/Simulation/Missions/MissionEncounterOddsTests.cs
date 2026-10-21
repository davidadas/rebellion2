using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    /// <summary>
    /// Checks probability-state compression against exhaustive exact rational arithmetic.
    /// </summary>
    [TestFixture]
    public sealed class MissionEncounterOddsTests
    {
        /// <summary>
        /// Verifies cached defender packets cannot be mutated through their read-only views.
        /// </summary>
        [Test]
        public void Evaluate_CachedDefenderInput_RejectsMutation()
        {
            var defender = new MissionEncounterOdds.DefenderProbabilities(
                0.5,
                new[] { 0.4 },
                new[] { 60 }
            );
            var cache = new MissionEncounterOdds.Cache(2);
            cache.Evaluate(
                new[] { defender },
                new[] { 0 },
                new Dictionary<BigInteger, double> { { 1, 1 } }
            );

            Assert.Throws<NotSupportedException>(() =>
                ((IList<double>)defender.DiversionProbabilities)[0] = 0.9
            );
        }

        /// <summary>
        /// Verifies a consumer cannot corrupt a shared cached probability distribution.
        /// </summary>
        [Test]
        public void Evaluate_CachedResult_RejectsMutation()
        {
            var defender = new MissionEncounterOdds.DefenderProbabilities(
                0.5,
                Array.Empty<double>(),
                new[] { 60 }
            );
            var result = new MissionEncounterOdds.Cache(2).Evaluate(
                new[] { defender },
                Array.Empty<int>(),
                new Dictionary<BigInteger, double> { { 0, 1 } }
            );

            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<BigInteger, double>)result.Survivors)[0] = 0.9
            );
        }

        /// <summary>
        /// Verifies eviction changes reuse only, never numerical results.
        /// </summary>
        [Test]
        public void Evaluate_FullCache_EvictsLeastRecentlyUsedEncounter()
        {
            var cache = new MissionEncounterOdds.Cache(2);
            var survivors = new Dictionary<BigInteger, double> { { 0, 1 } };
            var a = new[]
            {
                new MissionEncounterOdds.DefenderProbabilities(
                    0.2,
                    Array.Empty<double>(),
                    new[] { 60 }
                ),
            };
            var b = new[]
            {
                new MissionEncounterOdds.DefenderProbabilities(
                    0.4,
                    Array.Empty<double>(),
                    new[] { 60 }
                ),
            };
            var c = new[]
            {
                new MissionEncounterOdds.DefenderProbabilities(
                    0.6,
                    Array.Empty<double>(),
                    new[] { 60 }
                ),
            };
            var firstA = cache.Evaluate(a, Array.Empty<int>(), survivors);
            var firstB = cache.Evaluate(b, Array.Empty<int>(), survivors);
            cache.Evaluate(a, Array.Empty<int>(), survivors);

            cache.Evaluate(c, Array.Empty<int>(), survivors);
            var reusedA = cache.Evaluate(a, Array.Empty<int>(), survivors);
            var replacedB = cache.Evaluate(b, Array.Empty<int>(), survivors);

            Assert.AreSame(firstA, reusedA);
            Assert.AreNotSame(firstB, replacedB);
            Assert.AreEqual(firstB.PersonnelLossProbability, replacedB.PersonnelLossProbability);
        }

        /// <summary>
        /// Verifies disabling reuse keeps exactly the same numerical behavior.
        /// </summary>
        [Test]
        public void Evaluate_ZeroCapacity_ComputesWithoutRetainingResults()
        {
            var cache = new MissionEncounterOdds.Cache(0);
            var defenders = new[]
            {
                new MissionEncounterOdds.DefenderProbabilities(
                    0.5,
                    Array.Empty<double>(),
                    new[] { 60 }
                ),
            };
            var survivors = new Dictionary<BigInteger, double> { { 0, 1 } };
            var first = cache.Evaluate(defenders, Array.Empty<int>(), survivors);

            var second = cache.Evaluate(defenders, Array.Empty<int>(), survivors);

            Assert.AreNotSame(first, second);
            Assert.AreEqual(first.PersonnelLossProbability, second.PersonnelLossProbability);
        }

        /// <summary>
        /// Checks shared evasion factors without losing distinct decoy and foiling branches.
        /// </summary>
        [Test]
        public void Apply_UniformOfficerEvasionWithDifferentDetection_MatchesExactEnumeration()
        {
            var detectors = new[]
            {
                new MissionEncounterOdds.DefenderProbabilities(
                    0.8,
                    new[] { 0.4, 0.7 },
                    new[] { 30, 60 }
                ),
                new MissionEncounterOdds.DefenderProbabilities(
                    0.5,
                    new[] { 0.6, 0.2 },
                    new[] { 30, 60 }
                ),
                new MissionEncounterOdds.DefenderProbabilities(
                    0.9,
                    new[] { 0.3, 0.8 },
                    new[] { 30, 60 }
                ),
            };
            var incoming = new Dictionary<BigInteger, double> { { 3, 1 } };
            var exactIncoming = new Dictionary<BigInteger, Fraction> { { 3, Fraction.One } };
            (Fraction foil, Fraction loss) = Exhaustive(detectors, new[] { 0, 1 }, exactIncoming);

            var result = MissionEncounterOdds.Apply(detectors, new[] { 0, 1 }, incoming);

            Assert.That(result.FoilProbability, Is.EqualTo(foil.Value).Within(1e-12));
            Assert.That(result.PersonnelLossProbability, Is.EqualTo(loss.Value).Within(1e-12));
        }

        /// <summary>
        /// Verifies numerically equivalent encounters reuse a scoped result.
        /// </summary>
        [Test]
        public void Evaluate_EquivalentNumericalInputs_ReusesResult()
        {
            var cache = new MissionEncounterOdds.Cache(64);
            var first = cache.Evaluate(
                new[]
                {
                    new MissionEncounterOdds.DefenderProbabilities(
                        0.5,
                        new[] { 0.4 },
                        new[] { 60 }
                    ),
                },
                new[] { 0 },
                new Dictionary<BigInteger, double> { { 1, 1 } }
            );

            var second = cache.Evaluate(
                new[]
                {
                    new MissionEncounterOdds.DefenderProbabilities(
                        0.5,
                        new[] { 0.4 },
                        new[] { 60 }
                    ),
                },
                new[] { 0 },
                new Dictionary<BigInteger, double> { { 1, 1 } }
            );

            Assert.AreSame(first, second);
        }

        /// <summary>
        /// Verifies reused input containers cannot alter an earlier cache identity.
        /// </summary>
        [Test]
        public void Evaluate_ChangedIncomingProbability_DoesNotReusePreviousResult()
        {
            var cache = new MissionEncounterOdds.Cache(64);
            var defenders = new[]
            {
                new MissionEncounterOdds.DefenderProbabilities(
                    0.5,
                    Array.Empty<double>(),
                    new[] { 60 }
                ),
            };
            var survivors = new Dictionary<BigInteger, double> { { 0, 1 } };
            var first = cache.Evaluate(defenders, Array.Empty<int>(), survivors);
            survivors[0] = 0.5;

            var second = cache.Evaluate(defenders, Array.Empty<int>(), survivors);
            survivors[0] = 1;
            var restored = cache.Evaluate(defenders, Array.Empty<int>(), survivors);

            Assert.AreNotSame(first, second);
            Assert.AreEqual(first.FoilProbability / 2, second.FoilProbability);
            Assert.AreSame(first, restored);
        }

        /// <summary>
        /// Verifies decoy-loss history remains part of the numerical cache identity.
        /// </summary>
        [Test]
        public void Evaluate_ChangedNoLossProbability_DoesNotReusePreviousResult()
        {
            var cache = new MissionEncounterOdds.Cache(64);
            var defenders = new[]
            {
                new MissionEncounterOdds.DefenderProbabilities(
                    0.5,
                    Array.Empty<double>(),
                    new[] { 60 }
                ),
            };
            var survivors = new Dictionary<BigInteger, double> { { 0, 1 } };
            var first = cache.Evaluate(defenders, Array.Empty<int>(), survivors);

            var second = cache.Evaluate(
                defenders,
                Array.Empty<int>(),
                survivors,
                new Dictionary<BigInteger, double> { { 0, 0.5 } }
            );

            Assert.AreNotSame(first, second);
            Assert.AreEqual(first.PersonnelLossProbability / 2, second.PersonnelLossProbability);
        }

        /// <summary>
        /// Verifies defender changes do not reuse an earlier encounter result.
        /// </summary>
        [Test]
        public void Evaluate_ChangedDefenderProbabilities_DoesNotReusePreviousResult()
        {
            var cache = new MissionEncounterOdds.Cache(64);
            var survivors = new Dictionary<BigInteger, double> { { 0, 1 } };
            var first = cache.Evaluate(
                new[]
                {
                    new MissionEncounterOdds.DefenderProbabilities(
                        0.5,
                        Array.Empty<double>(),
                        new[] { 60 }
                    ),
                },
                Array.Empty<int>(),
                survivors
            );

            var second = cache.Evaluate(
                new[]
                {
                    new MissionEncounterOdds.DefenderProbabilities(
                        0.75,
                        Array.Empty<double>(),
                        new[] { 60 }
                    ),
                },
                Array.Empty<int>(),
                survivors
            );

            Assert.AreNotSame(first, second);
            Assert.AreEqual(0.25, second.FoilProbability);
        }

        /// <summary>
        /// Verifies decoy and primary losses use disjoint probability weights.
        /// </summary>
        /// <param name="noFoil">The defender's no-foil probability.</param>
        /// <param name="expectedLoss">The combined officer-loss probability.</param>
        [TestCase(1d, 0.125d)]
        [TestCase(0.5d, 0.2375d)]
        [TestCase(0d, 0.35d)]
        public void Apply_DecoyAndPrimaryOfficerRisk_DoesNotDoubleCountLoss(
            double noFoil,
            double expectedLoss
        )
        {
            var detector = new MissionEncounterOdds.DefenderProbabilities(
                noFoil,
                new[] { 0.5 },
                new[] { 50 },
                new[] { 0.25 },
                new[] { 0.8 }
            );

            var result = MissionEncounterOdds.Apply(
                new[] { detector },
                new[] { 0 },
                new Dictionary<BigInteger, double> { { 1, 1 } }
            );

            Assert.That(result.PersonnelLossProbability, Is.EqualTo(expectedLoss).Within(1e-12));
        }

        /// <summary>
        /// Verifies earlier unfoiled decoy losses remain excluded from later no-loss weights.
        /// </summary>
        [Test]
        public void Apply_PreviousDecoyLoss_PreservesSurvivalSubsetAcrossCheckpoints()
        {
            var detector = new MissionEncounterOdds.DefenderProbabilities(
                0.5,
                new[] { 0.5 },
                new[] { 50 },
                new[] { 0.25 },
                new[] { 0.8 }
            );
            var first = MissionEncounterOdds.Apply(
                new[] { detector },
                new[] { 0 },
                new Dictionary<BigInteger, double> { { 1, 1 } }
            );

            var second = MissionEncounterOdds.Apply(
                new[] { detector },
                new[] { 0 },
                first.Survivors,
                first.NoLossSurvivors
            );

            Assert.That(
                first.PersonnelLossProbability + second.PersonnelLossProbability,
                Is.EqualTo(0.4125d).Within(1e-12)
            );
            Assert.That(second.NoLossSurvivors.Values.Sum(), Is.EqualTo(0.4375d).Within(1e-12));
        }

        /// <summary>
        /// Tests varied teams, decoy choices, defender odds, and cross-checkpoint survivors.
        /// </summary>
        [Test]
        public void Apply_GeneratedEncounters_MatchesExhaustiveRationalEnumeration()
        {
            for (int seed = 0; seed < 240; seed++)
            {
                Random random = new Random(seed);
                int decoyCount = seed % 4;
                int officerCount = seed / 4 % 4;
                BigInteger pool = (BigInteger.One << decoyCount) - 1;
                Dictionary<BigInteger, double> actual = new Dictionary<BigInteger, double>
                {
                    { pool, 1 },
                };
                Dictionary<BigInteger, Fraction> expected = new Dictionary<BigInteger, Fraction>
                {
                    { pool, Fraction.One },
                };
                double actualFoil = 0;
                double actualLoss = 0;
                Fraction expectedFoil = Fraction.Zero;
                Fraction expectedLoss = Fraction.Zero;
                for (int checkpoint = 0; checkpoint < 3; checkpoint++)
                {
                    int[] present = Enumerable
                        .Range(0, decoyCount)
                        .Where(_ => random.Next(3) != 0)
                        .ToArray();
                    List<MissionEncounterOdds.DefenderProbabilities> detectors =
                        new List<MissionEncounterOdds.DefenderProbabilities>();
                    for (int index = 0; index < seed % 7; index++)
                        detectors.Add(
                            new MissionEncounterOdds.DefenderProbabilities(
                                NextPercentage(random) / 100d,
                                Enumerable
                                    .Range(0, decoyCount)
                                    .Select(_ => NextPercentage(random) / 100d)
                                    .ToArray(),
                                Enumerable
                                    .Range(0, officerCount)
                                    .Select(_ => NextPercentage(random))
                                    .ToArray()
                            )
                        );

                    MissionEncounterOdds.Result result = MissionEncounterOdds.Apply(
                        detectors,
                        present,
                        actual
                    );
                    (Fraction referenceFoil, Fraction referenceLoss) = Exhaustive(
                        detectors,
                        present,
                        expected
                    );
                    actual = new Dictionary<BigInteger, double>(result.Survivors);
                    actualFoil += result.FoilProbability;
                    actualLoss += result.PersonnelLossProbability;
                    expectedFoil += referenceFoil;
                    expectedLoss += referenceLoss;
                    Assert.That(
                        actualFoil,
                        Is.EqualTo(expectedFoil.Value).Within(1e-11),
                        $"seed={seed} checkpoint={checkpoint} foil"
                    );
                    Assert.That(
                        actualLoss,
                        Is.EqualTo(expectedLoss.Value).Within(1e-11),
                        $"seed={seed} checkpoint={checkpoint} loss"
                    );
                    CollectionAssert.AreEquivalent(
                        expected.Keys,
                        actual.Keys,
                        $"seed={seed} checkpoint={checkpoint} pools"
                    );
                    foreach ((BigInteger key, Fraction probability) in expected)
                        Assert.That(
                            actual[key],
                            Is.EqualTo(probability.Value).Within(1e-11),
                            $"seed={seed} checkpoint={checkpoint} pool={key}"
                        );
                    Assert.That(actualFoil + actual.Values.Sum(), Is.EqualTo(1d).Within(1e-11));
                    Assert.That(actualLoss, Is.InRange(0, actualFoil + 1e-12));
                }
            }
        }

        /// <summary>
        /// Ensures large officer teams do not require an officer-subset state space.
        /// </summary>
        [Test]
        public void Apply_ThirtyFivePrimaryOfficers_MatchesExactRisk()
        {
            int[] evasion = Enumerable.Range(0, 35).Select(index => 50 + index).ToArray();
            List<MissionEncounterOdds.DefenderProbabilities> detectors =
                new List<MissionEncounterOdds.DefenderProbabilities>
                {
                    new MissionEncounterOdds.DefenderProbabilities(
                        0.5,
                        Array.Empty<double>(),
                        evasion
                    ),
                };
            Dictionary<BigInteger, double> survivors = new Dictionary<BigInteger, double>
            {
                { 0, 1 },
            };
            MissionEncounterOdds.Result result = MissionEncounterOdds.Apply(
                detectors,
                Array.Empty<int>(),
                survivors
            );
            double noLoss = evasion.Aggregate(1d, (product, value) => product * value / 100d);
            Assert.That(result.FoilProbability, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(
                result.PersonnelLossProbability,
                Is.EqualTo(0.5 * (1 - noLoss)).Within(1e-12)
            );
        }

        /// <summary>
        /// Ensures decoy identities are not restricted to machine-word bit widths.
        /// </summary>
        [Test]
        public void Apply_DecoyAboveBitSixtyFour_PreservesPoolAndLossOdds()
        {
            double[] diversions = new double[71];
            diversions[70] = 0.5;
            BigInteger pool = BigInteger.One << 70;
            Dictionary<BigInteger, double> survivors = new Dictionary<BigInteger, double>
            {
                { pool, 1 },
            };
            MissionEncounterOdds.Result result = MissionEncounterOdds.Apply(
                new[]
                {
                    new MissionEncounterOdds.DefenderProbabilities(0.5, diversions, new[] { 60 }),
                },
                new[] { 70 },
                survivors
            );
            Assert.That(result.FoilProbability, Is.EqualTo(0.25).Within(1e-12));
            Assert.That(result.PersonnelLossProbability, Is.EqualTo(0.1).Within(1e-12));
            Assert.That(result.Survivors[pool], Is.EqualTo(0.5).Within(1e-12));
            Assert.That(result.Survivors[0], Is.EqualTo(0.25).Within(1e-12));
        }

        /// <summary>
        /// Checks a large fleet against the closed-form first-failed-diversion distribution.
        /// </summary>
        [Test]
        public void Apply_TwoHundredFiftyEightDefenders_MatchesClosedForm()
        {
            const int count = 258;
            const double diversion = 0.91;
            const double noFoil = 0.87;
            MissionEncounterOdds.DefenderProbabilities[] detectors = Enumerable
                .Range(0, count)
                .Select(_ => new MissionEncounterOdds.DefenderProbabilities(
                    noFoil,
                    new[] { diversion },
                    new[] { 96 }
                ))
                .ToArray();
            Dictionary<BigInteger, double> survivors = new Dictionary<BigInteger, double>
            {
                { 1, 1 },
            };
            MissionEncounterOdds.Result result = MissionEncounterOdds.Apply(
                detectors,
                new[] { 0 },
                survivors
            );
            double expectedSurvival = Math.Pow(diversion, count);
            for (int failedAt = 0; failedAt < count; failedAt++)
                expectedSurvival +=
                    Math.Pow(diversion, failedAt)
                    * (1 - diversion)
                    * Math.Pow(noFoil, count - failedAt);
            Assert.That(result.FoilProbability, Is.EqualTo(1 - expectedSurvival).Within(1e-11));
            Assert.That(
                result.PersonnelLossProbability,
                Is.EqualTo((1 - expectedSurvival) * 0.04).Within(1e-11)
            );
        }

        /// <summary>
        /// Preserves the product of officer-specific average evasion rather than averaging team loss.
        /// </summary>
        [Test]
        public void Apply_OfficersWithOppositeDefenderStrengths_PreservesJointLossProbability()
        {
            MissionEncounterOdds.DefenderProbabilities[] detectors =
            {
                new MissionEncounterOdds.DefenderProbabilities(
                    0,
                    Array.Empty<double>(),
                    new[] { 0, 100 }
                ),
                new MissionEncounterOdds.DefenderProbabilities(
                    0,
                    Array.Empty<double>(),
                    new[] { 100, 0 }
                ),
            };
            Dictionary<BigInteger, double> survivors = new Dictionary<BigInteger, double>
            {
                { 0, 1 },
            };

            MissionEncounterOdds.Result result = MissionEncounterOdds.Apply(
                detectors,
                Array.Empty<int>(),
                survivors
            );

            Assert.That(result.FoilProbability, Is.EqualTo(1));
            Assert.That(result.PersonnelLossProbability, Is.EqualTo(0.75));
        }

        /// <summary>
        /// Produces exact percentage boundaries and varied interior values.
        /// </summary>
        /// <param name="random">The seeded fixture generator.</param>
        /// <returns>An integer percentage from zero through one hundred.</returns>
        private static int NextPercentage(Random random)
        {
            int value = random.Next(5);
            return value == 0 ? 0
                : value == 1 ? 100
                : random.Next(1, 100);
        }

        /// <summary>
        /// Enumerates every diversion history using rational, rather than floating-point, weights.
        /// </summary>
        /// <param name="detectors">Ordered defender inputs.</param>
        /// <param name="present">Decoys present at this checkpoint.</param>
        /// <param name="survivors">Incoming and outgoing exact surviving weights.</param>
        /// <returns>The exact foil and officer-loss probabilities.</returns>
        private static (Fraction Foil, Fraction Loss) Exhaustive(
            IReadOnlyList<MissionEncounterOdds.DefenderProbabilities> detectors,
            IReadOnlyList<int> present,
            Dictionary<BigInteger, Fraction> survivors
        )
        {
            if (detectors.Count == 0)
                return (Fraction.Zero, Fraction.Zero);
            Dictionary<BigInteger, Fraction> next = new Dictionary<BigInteger, Fraction>();
            Fraction foil = Fraction.Zero;
            Fraction loss = Fraction.Zero;
            foreach ((BigInteger pool, Fraction weight) in survivors)
                Enumerate(
                    detectors,
                    present,
                    0,
                    pool,
                    weight,
                    new List<int>(),
                    next,
                    ref foil,
                    ref loss
                );
            survivors.Clear();
            foreach ((BigInteger pool, Fraction weight) in next)
                if (!weight.IsZero)
                    survivors.Add(pool, weight);
            return (foil, loss);
        }

        /// <summary>
        /// Branches on each individual decoy choice and evaluates only complete defender histories.
        /// </summary>
        /// <param name="detectors">Ordered defender probabilities.</param>
        /// <param name="present">Present decoy indexes.</param>
        /// <param name="position">The next defender.</param>
        /// <param name="pool">Decoys remaining in this history.</param>
        /// <param name="weight">Exact history probability.</param>
        /// <param name="active">Identities of all undistracted defenders.</param>
        /// <param name="survivors">Accumulated exact surviving pools.</param>
        /// <param name="foil">Accumulated foil probability.</param>
        /// <param name="loss">Accumulated officer-loss probability.</param>
        private static void Enumerate(
            IReadOnlyList<MissionEncounterOdds.DefenderProbabilities> detectors,
            IReadOnlyList<int> present,
            int position,
            BigInteger pool,
            Fraction weight,
            List<int> active,
            Dictionary<BigInteger, Fraction> survivors,
            ref Fraction foil,
            ref Fraction loss
        )
        {
            if (weight.IsZero)
                return;
            if (position == detectors.Count)
            {
                Fraction noFoil = Fraction.One;
                foreach (int index in active)
                    noFoil *= Fraction.Percentage(detectors[index].NoFoilProbability * 100);
                Fraction foiled = weight * (Fraction.One - noFoil);
                Fraction noLoss = Fraction.One;
                if (active.Count > 0)
                    for (
                        int officer = 0;
                        officer < detectors[0].EvasionPercentages.Count;
                        officer++
                    )
                    {
                        long total = active.Sum(index =>
                            (long)detectors[index].EvasionPercentages[officer]
                        );
                        noLoss *= new Fraction(total, 100L * active.Count);
                    }
                foil += foiled;
                loss += foiled * (Fraction.One - noLoss);
                survivors.TryGetValue(pool, out Fraction previous);
                survivors[pool] = previous + weight * noFoil;
                return;
            }

            int[] choices = present
                .Where(index => (pool & (BigInteger.One << index)) != 0)
                .ToArray();
            if (choices.Length == 0)
            {
                active.Add(position);
                Enumerate(
                    detectors,
                    present,
                    position + 1,
                    pool,
                    weight,
                    active,
                    survivors,
                    ref foil,
                    ref loss
                );
                active.RemoveAt(active.Count - 1);
                return;
            }
            foreach (int index in choices)
            {
                Fraction diversion = Fraction.Percentage(
                    detectors[position].DiversionProbabilities[index] * 100
                );
                Fraction selected = weight * new Fraction(1, choices.Length);
                Enumerate(
                    detectors,
                    present,
                    position + 1,
                    pool,
                    selected * diversion,
                    active,
                    survivors,
                    ref foil,
                    ref loss
                );
                active.Add(position);
                Enumerate(
                    detectors,
                    present,
                    position + 1,
                    pool & ~(BigInteger.One << index),
                    selected * (Fraction.One - diversion),
                    active,
                    survivors,
                    ref foil,
                    ref loss
                );
                active.RemoveAt(active.Count - 1);
            }
        }

        /// <summary>
        /// Provides reduced rational arithmetic for the independent test oracle.
        /// </summary>
        private readonly struct Fraction
        {
            private readonly BigInteger _numerator;
            private readonly BigInteger _denominator;
            internal static Fraction Zero => new Fraction(0, 1);
            internal static Fraction One => new Fraction(1, 1);
            internal bool IsZero => _numerator.IsZero;
            internal double Value => (double)_numerator / (double)Denominator;
            private BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;

            /// <summary>
            /// Creates a reduced exact rational.
            /// </summary>
            /// <param name="numerator">The fraction numerator.</param>
            /// <param name="denominator">The nonzero positive denominator.</param>
            internal Fraction(BigInteger numerator, BigInteger denominator)
            {
                BigInteger gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
                _numerator = numerator / gcd;
                _denominator = denominator / gcd;
            }

            /// <summary>
            /// Creates an exact fraction from an authored integer percentage.
            /// </summary>
            /// <param name="value">The integer percentage represented as a double.</param>
            /// <returns>The reduced percentage fraction.</returns>
            internal static Fraction Percentage(double value) =>
                new Fraction((int)Math.Round(value), 100);

            /// <summary>
            /// Adds two exact probabilities.
            /// </summary>
            /// <param name="left">First probability.</param>
            /// <param name="right">Second probability.</param>
            /// <returns>The exact sum.</returns>
            public static Fraction operator +(Fraction left, Fraction right) =>
                new Fraction(
                    left._numerator * right.Denominator + right._numerator * left.Denominator,
                    left.Denominator * right.Denominator
                );

            /// <summary>
            /// Subtracts two exact probabilities.
            /// </summary>
            /// <param name="left">First probability.</param>
            /// <param name="right">Second probability.</param>
            /// <returns>The exact difference.</returns>
            public static Fraction operator -(Fraction left, Fraction right) =>
                new Fraction(
                    left._numerator * right.Denominator - right._numerator * left.Denominator,
                    left.Denominator * right.Denominator
                );

            /// <summary>
            /// Multiplies independent exact probability factors.
            /// </summary>
            /// <param name="left">First factor.</param>
            /// <param name="right">Second factor.</param>
            /// <returns>The exact product.</returns>
            public static Fraction operator *(Fraction left, Fraction right) =>
                new Fraction(
                    left._numerator * right._numerator,
                    left.Denominator * right.Denominator
                );
        }
    }
}
