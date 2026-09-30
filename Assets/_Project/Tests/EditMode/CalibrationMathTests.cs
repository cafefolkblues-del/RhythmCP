using System.Collections.Generic;
using NUnit.Framework;
using RhythmCP.Settings;

namespace RhythmCP.Tests
{
    public class CalibrationMathTests
    {
        [Test]
        public void Suggest_UsesMedian_IgnoresOutlier()
        {
            // 대부분 +30ms 늦게 치고 한 번 크게 헛침(+240ms) → 평균이면 끌려가지만 중앙값은 30
            var d = new List<double> { 0.028, 0.030, 0.031, 0.029, 0.032, 0.030, 0.240 };
            Assert.AreEqual(30f, CalibrationMath.SuggestMs(d));
        }

        [Test]
        public void Suggest_TooFewSamples_ReturnsNull()
        {
            Assert.IsNull(CalibrationMath.SuggestMs(new List<double> { 0.01, 0.02 }));
        }

        [Test]
        public void Suggest_ClampedToLimit()
        {
            var d = new List<double> { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 };
            Assert.AreEqual(GameSettings.OffsetLimitMs, CalibrationMath.SuggestMs(d));
        }

        [TestCase(1.02, true, 0, 0.02)]
        [TestCase(1.49, true, 1, -0.01)]
        [TestCase(0.70, false, -1, 0)]
        [TestCase(9.20, false, 16, 0)]
        public void MatchBeat_NearestClick(double tap, bool ok, int index, double delta)
        {
            bool matched = CalibrationMath.TryMatchBeat(tap, 1.0, 0.5, 16, out int i, out double dlt);
            Assert.AreEqual(ok, matched);
            if (ok)
            {
                Assert.AreEqual(index, i);
                Assert.AreEqual(delta, dlt, 1e-9);
            }
        }
    }
}
