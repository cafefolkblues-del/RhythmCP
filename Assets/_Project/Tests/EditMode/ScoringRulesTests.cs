using System.Collections.Generic;
using NUnit.Framework;
using RhythmCP.Chart;
using RhythmCP.Rhythm;

namespace RhythmCP.Tests
{
    public class ScoringRulesTests
    {
        // 기본값 = RhythmConfig 기본값과 동일
        static readonly ScoringRules R = new ScoringRules(300, 150, 50, 50, 50, 10, 0.1f, 1.5f, 0.25f, 0.95f, 0.80f, 0.70f);

        [TestCase(1, 1.0f)]
        [TestCase(9, 1.0f)]
        [TestCase(10, 1.1f)]
        [TestCase(49, 1.4f)]
        [TestCase(50, 1.5f)]
        [TestCase(200, 1.5f)]
        public void ComboMultiplier_StepsEveryTen_CappedAt15(int combo, float expected)
        {
            Assert.AreEqual(expected, R.ComboMultiplier(combo), 1e-5f);
        }

        [Test]
        public void BasePoints_BySourceAndJudgement()
        {
            Assert.AreEqual(300, R.BasePoints(ComboSource.Note, Judgement.Perfect));
            Assert.AreEqual(150, R.BasePoints(ComboSource.Note, Judgement.Great));
            Assert.AreEqual(50, R.BasePoints(ComboSource.Note, Judgement.Good));
            Assert.AreEqual(50, R.BasePoints(ComboSource.HoldTick, Judgement.Perfect));
            Assert.AreEqual(50, R.BasePoints(ComboSource.MashHit, Judgement.Perfect));
        }

        [Test]
        public void Accuracy_UsesGoodWeight()
        {
            // (8 + 2×0.5 + 4×0.25) / 20 = 10/20
            Assert.AreEqual(0.5f, R.Accuracy(8, 2, 4, 20), 1e-6f);
            Assert.AreEqual(0f, R.Accuracy(0, 0, 0, 0));
        }

        [TestCase(0.99f, 0.99f, Grade.S)]
        [TestCase(0.95f, 0.80f, Grade.S)]
        [TestCase(0.94f, 0.99f, Grade.A)]
        [TestCase(0.80f, 0.99f, Grade.A)]
        [TestCase(0.79f, 0.99f, Grade.B)]
        [TestCase(1.20f, 0.69f, Grade.B)]
        public void Grade_ByScoreRatio_CappedByMinHitRate(float ratio, float hitRate, Grade expected)
        {
            Assert.AreEqual(expected, R.Evaluate(ratio, hitRate));
        }

        [Test]
        public void MaxScore_AllPerfectFullCombo_IncludesHoldTicks_ExcludesMashAndHeart()
        {
            // 120BPM → 1박 0.5초. Tap 1개 + Hold 0.5박(틱 1/4박 → 틱 1개) + 하트 + 연타
            var data = new ChartData
            {
                songId = "t",
                bpms = new List<BpmPoint> { new BpmPoint { beat = 0, bpm = 120 } },
                notes = new List<NoteData>
                {
                    new NoteData { type = NoteType.Tap, lane = Lane.Top, beat = 1 },
                    new NoteData { type = NoteType.Hold, lane = Lane.Bottom, beat = 2, endBeat = 2.5 },
                    new NoteData { type = NoteType.Heart, lane = Lane.Top, beat = 3 },
                    new NoteData { type = NoteType.Mash, lane = Lane.Top, beat = 4, endBeat = 5 },
                },
            };
            var chart = ChartLoader.Build(data);

            // 콤보 1~4 전부 ×1.0: Tap 300 + 머리 300 + 틱 50 + 꼬리 300
            Assert.AreEqual(950, R.MaxScore(chart, 0.25));
        }

        [Test]
        public void MaxScore_AppliesComboMultiplierInTimeOrder()
        {
            var notes = new List<NoteData>();
            for (int i = 0; i < 12; i++) notes.Add(new NoteData { type = NoteType.Tap, lane = Lane.Top, beat = i });
            var chart = ChartLoader.Build(new ChartData
            {
                songId = "t",
                bpms = new List<BpmPoint> { new BpmPoint { beat = 0, bpm = 120 } },
                notes = notes,
            });

            // 1~9콤보 ×1.0 = 2700, 10~12콤보 ×1.1 = 330 × 3 = 990
            Assert.AreEqual(2700 + 990, R.MaxScore(chart, 0.25));
        }
    }
}
