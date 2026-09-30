using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmCP.Character;
using RhythmCP.Chart;
using RhythmCP.Rhythm;

namespace RhythmCP.Tests
{
    public class AbilityTests
    {
        // 120BPM, offset 0 → 1박 = 0.5초
        static readonly TempoMap Tempo = new TempoMap(new List<BpmPoint> { new BpmPoint { beat = 0, bpm = 120 } }, 0);

        static PlayNote Note(NoteType type, Lane lane, double beat, double endBeat = double.NaN)
        {
            double end = double.IsNaN(endBeat) ? beat : endBeat;
            return new PlayNote { Type = type, Lane = lane, Beat = beat, Time = beat * 0.5, EndBeat = end, EndTime = end * 0.5, Speed = 1 };
        }

        [Test]
        public void Planner_AlternatesLanes_OnBeatGrid_WithinWindow()
        {
            // 1.1초 ~ 3.0초 → 3·4·5·6박
            var notes = BonusNotePlanner.Plan(new List<PlayNote>(), Tempo, 1.1, 3.0, 10, 1.0);
            CollectionAssert.AreEqual(new[] { 3.0, 4.0, 5.0, 6.0 }, notes.Select(n => n.Beat).ToArray());
            CollectionAssert.AreEqual(new[] { Lane.Top, Lane.Bottom, Lane.Top, Lane.Bottom }, notes.Select(n => n.Lane).ToArray());
            Assert.IsTrue(notes.All(n => n.IsBonus && n.Type == NoteType.Tap));
        }

        [Test]
        public void Planner_RespectsCount()
        {
            Assert.AreEqual(2, BonusNotePlanner.Plan(new List<PlayNote>(), Tempo, 0, 10, 2, 1.0).Count);
        }

        [Test]
        public void Planner_AvoidsOccupiedLane_AndMash()
        {
            var existing = new List<PlayNote>
            {
                Note(NoteType.Tap, Lane.Top, 3),                     // 3박 위 막힘 → 아래로
                Note(NoteType.Hold, Lane.Top, 4, 5),                 // 4~5박 위 막힘
                Note(NoteType.Mash, Lane.Bottom, 6, 7),              // 6~7박 양 레인 막힘
            };
            var notes = BonusNotePlanner.Plan(existing, Tempo, 1.5, 4.0, 10, 1.0);

            // 3박: 위 막힘 → 아래 / 4박: 차례=위(막힘) → 아래 / 5박: 차례=위(막힘) → 아래 / 6·7박: 연타 → 건너뜀 / 8박: 위
            CollectionAssert.AreEqual(new[] { 3.0, 4.0, 5.0, 8.0 }, notes.Select(n => n.Beat).ToArray());
            CollectionAssert.AreEqual(new[] { Lane.Bottom, Lane.Bottom, Lane.Bottom, Lane.Top }, notes.Select(n => n.Lane).ToArray());
        }

        [Test]
        public void JudgeWindows_Widen_AddsMilliseconds()
        {
            var w = new JudgeWindows(0.050, 0.130, 0.200).Widen(15, 0, 10);
            Assert.AreEqual(0.065, w.Perfect, 1e-9);
            Assert.AreEqual(0.130, w.Great, 1e-9);
            Assert.AreEqual(0.210, w.Good, 1e-9);
        }

        [Test]
        public void ScoringRules_ComboMaxAdd_RaisesCap()
        {
            var r = new ScoringRules(300, 150, 50, 50, 50, 10, 0.1f, 1.5f, 0.25f, 0.95f, 0.80f, 0.70f).WithComboMaxAdd(0.3f);
            Assert.AreEqual(1.8f, r.ComboMultiplier(100), 1e-5f);
        }
    }
}
