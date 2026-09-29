using NUnit.Framework;
using RhythmCP.Rhythm;

namespace RhythmCP.Tests
{
    public class JudgeWindowsTests
    {
        static readonly JudgeWindows W = new JudgeWindows(0.050, 0.130, 0.200);

        [TestCase(0.0, Judgement.Perfect)]
        [TestCase(-0.050, Judgement.Perfect)]
        [TestCase(0.050, Judgement.Perfect)]
        [TestCase(0.051, Judgement.Great)]
        [TestCase(-0.130, Judgement.Great)]
        [TestCase(0.131, Judgement.Good)]
        [TestCase(-0.200, Judgement.Good)]
        public void InsideWindows(double delta, Judgement expected)
        {
            Assert.IsTrue(W.TryEvaluate(delta, out var j));
            Assert.AreEqual(expected, j);
        }

        [TestCase(-0.201)]
        [TestCase(0.201)]
        [TestCase(-1.0)]
        public void OutsideGood_IsIgnored(double delta)
        {
            Assert.IsFalse(W.TryEvaluate(delta, out _));
        }

        [Test]
        public void TooLate_OnlyAfterGoodWindow()
        {
            Assert.IsFalse(W.IsTooLate(0.200));
            Assert.IsTrue(W.IsTooLate(0.2001));
            Assert.IsFalse(W.IsTooLate(-5));
        }
    }
}
