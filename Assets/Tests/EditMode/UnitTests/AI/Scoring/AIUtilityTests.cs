using NUnit.Framework;
using Rebellion.AI.Scoring;
using Rebellion.Game;

namespace Rebellion.Tests.AI.Scoring
{
    [TestFixture]
    public class AIUtilityTests
    {
        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(0.25, 0.25)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        public void LinearCurveClampsNormalizedInput(double input, double expected)
        {
            Assert.That(
                AIUtility.EvaluateCurve(input, new GameConfig.AIResponseCurveConfig()),
                Is.EqualTo(expected).Within(0.000001)
            );
        }

        [Test]
        public void ConsiderationAppliesWeightAfterCurve()
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
        public void RawConsiderationNormalizesAgainstConfiguredMaximum()
        {
            GameConfig.AIConsiderationConfig consideration = new GameConfig.AIConsiderationConfig
            {
                Weight = 0.3,
                InputMaximum = 10,
            };

            Assert.That(AIUtility.EvaluateRaw(3, consideration), Is.EqualTo(0.09).Within(0.000001));
        }

        [Test]
        public void RawPressureUsesPercentagePointDomain()
        {
            GameConfig.AIConsiderationConfig consideration = new GameConfig.AIConsiderationConfig
            {
                Weight = 1,
                InputMaximum = 300,
            };

            Assert.That(
                AIUtility.EvaluateRawPressure(150, consideration),
                Is.EqualTo(50).Within(0.000001)
            );
        }

        [Test]
        public void DiscreteConsiderationTruncatesFractionalContribution()
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
        public void CenteredConsiderationSpansBothSidesOfNeutral(double input, double expected)
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
        public void UtilityScoreReturnsWeightedAverage()
        {
            AIUtilityScore score = new AIUtilityScore();
            score.Add(1, new GameConfig.AIConsiderationConfig { Weight = 1 });
            score.Add(0, new GameConfig.AIConsiderationConfig { Weight = 0.5 });

            Assert.That(score.Value, Is.EqualTo(2.0 / 3).Within(0.000001));
        }

        [Test]
        public void UtilityScoreInvertsCosts()
        {
            AIUtilityScore score = new AIUtilityScore();
            score.AddCost(0.25, new GameConfig.AIConsiderationConfig { Weight = 1 });

            Assert.That(score.Value, Is.EqualTo(0.75).Within(0.000001));
        }

        [Test]
        public void SmoothStepPreservesEndpointsAndMidpoint()
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
        public void LogisticCurveIsNormalizedAndCentered()
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
        public void FulfillmentReturnsNormalizedProgress(
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
