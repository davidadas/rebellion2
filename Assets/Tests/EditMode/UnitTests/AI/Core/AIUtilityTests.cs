using System;
using NUnit.Framework;
using Rebellion.AI.Core;
using Rebellion.Game;

namespace Rebellion.Tests.AI.Core
{
    [TestFixture]
    public class AIUtilityTests
    {
        [TestCase(0, 0)]
        [TestCase(0.25, 0.25)]
        [TestCase(1, 1)]
        public void EvaluateCurve_WithNormalizedLinearInput_ReturnsInput(
            double input,
            double expected
        )
        {
            Assert.That(
                AIUtility.EvaluateCurve(input, new GameConfig.AIResponseCurveConfig()),
                Is.EqualTo(expected).Within(0.000001)
            );
        }

        [TestCase(-0.01)]
        [TestCase(1.01)]
        [TestCase(double.NaN)]
        public void EvaluateCurve_WithUnnormalizedInput_ThrowsArgumentOutOfRangeException(
            double input
        )
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AIUtility.EvaluateCurve(input, new GameConfig.AIResponseCurveConfig())
            );
        }

        [Test]
        public void Evaluate_WithWeightedPowerCurve_AppliesWeightAfterCurve()
        {
            GameConfig.AIConsiderationConfig consideration = new GameConfig.AIConsiderationConfig
            {
                Weight = 0.4,
                Curve = new GameConfig.AIResponseCurveConfig
                {
                    Shape = GameConfig.AIResponseCurveShape.Power,
                    Exponent = 2,
                },
            };

            Assert.That(AIUtility.Evaluate(0.5, consideration), Is.EqualTo(0.1).Within(0.000001));
        }

        [Test]
        public void EvaluateDiscretePressure_WithFractionalContribution_TruncatesScore()
        {
            GameConfig.AIConsiderationConfig consideration = new GameConfig.AIConsiderationConfig
            {
                Weight = 1,
            };

            Assert.That(AIUtility.EvaluateDiscretePressure(1.0 / 3, consideration), Is.EqualTo(33));
        }

        [TestCase(0, -0.5)]
        [TestCase(0.5, 0)]
        [TestCase(1, 0.5)]
        public void EvaluateCentered_WithNormalizedInput_SpansNeutral(double input, double expected)
        {
            GameConfig.AIConsiderationConfig consideration = new GameConfig.AIConsiderationConfig
            {
                Weight = 1,
            };

            Assert.That(
                AIUtility.EvaluateCentered(input, consideration),
                Is.EqualTo(expected).Within(0.000001)
            );
        }

        [Test]
        public void Value_WithWeightedConsiderations_ReturnsWeightedAverage()
        {
            AIUtilityScore score = new AIUtilityScore();
            score.Add(1, new GameConfig.AIConsiderationConfig { Weight = 1 });
            score.Add(0, new GameConfig.AIConsiderationConfig { Weight = 0.5 });

            Assert.That(score.Value, Is.EqualTo(2.0 / 3).Within(0.000001));
        }

        [Test]
        public void Value_WithCostConsideration_InvertsCost()
        {
            AIUtilityScore score = new AIUtilityScore();
            score.AddCost(0.25, new GameConfig.AIConsiderationConfig { Weight = 1 });

            Assert.That(score.Value, Is.EqualTo(0.75).Within(0.000001));
        }

        [Test]
        public void RankValue_WithPositiveScores_PreservesOrdering()
        {
            AIUtilityScore lower = new AIUtilityScore();
            lower.Add(0.25, new GameConfig.AIConsiderationConfig { Weight = 1 });
            AIUtilityScore higher = new AIUtilityScore();
            higher.Add(0.5, new GameConfig.AIConsiderationConfig { Weight = 1 });

            Assert.That(lower.RankValue, Is.EqualTo(0.2).Within(0.000001));
            Assert.Greater(higher.RankValue, lower.RankValue);
            Assert.Less(higher.RankValue, 1);
        }

        [Test]
        public void RankValue_WithNonPositiveSignedUtility_ReturnsZero()
        {
            AIUtilityScore score = new AIUtilityScore();
            score.Add(0.25, new GameConfig.AIConsiderationConfig { Weight = 1 });
            score.AddCost(0.5, new GameConfig.AIConsiderationConfig { Weight = 1 });

            Assert.Zero(score.RankValue);
        }

        [Test]
        public void EvaluateCurve_WithSmoothStepCurve_PreservesEndpointsAndMidpoint()
        {
            GameConfig.AIResponseCurveConfig curve = new GameConfig.AIResponseCurveConfig
            {
                Shape = GameConfig.AIResponseCurveShape.SmoothStep,
            };

            Assert.That(AIUtility.EvaluateCurve(0, curve), Is.EqualTo(0));
            Assert.That(AIUtility.EvaluateCurve(0.5, curve), Is.EqualTo(0.5));
            Assert.That(AIUtility.EvaluateCurve(1, curve), Is.EqualTo(1));
        }

        [Test]
        public void EvaluateCurve_WithLogisticCurve_IsNormalizedAndCentered()
        {
            GameConfig.AIResponseCurveConfig curve = new GameConfig.AIResponseCurveConfig
            {
                Shape = GameConfig.AIResponseCurveShape.Logistic,
                Midpoint = 0.5,
                Steepness = 10,
            };

            Assert.That(AIUtility.EvaluateCurve(0, curve), Is.EqualTo(0).Within(0.000001));
            Assert.That(AIUtility.EvaluateCurve(0.5, curve), Is.EqualTo(0.5).Within(0.000001));
            Assert.That(AIUtility.EvaluateCurve(1, curve), Is.EqualTo(1).Within(0.000001));
            Assert.That(AIUtility.EvaluateCurve(0.75, curve), Is.GreaterThan(0.75));
        }

        [TestCase(0, 0, 1)]
        [TestCase(5, 10, 0.5)]
        [TestCase(20, 10, 1)]
        [TestCase(-5, 10, 0)]
        public void Fulfillment_WithRawProgress_ReturnsNormalizedValue(
            double value,
            double target,
            double expected
        )
        {
            Assert.That(
                AIUtility.Fulfillment(value, target),
                Is.EqualTo(expected).Within(0.000001)
            );
        }
    }
}
