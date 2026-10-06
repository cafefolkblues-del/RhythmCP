using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmCP.Chart;
using RhythmCP.ChartEditing;

namespace RhythmCP.Tests
{
    public class AutoCharterTests
    {
        // 120BPM, offset 0 → 1박 = 0.5초
        static readonly TempoMap Tempo = new TempoMap(new List<BpmPoint> { new BpmPoint { beat = 0, bpm = 120 } }, 0);

        static AnalysisData.Onset On(double sec, double strength, string band = "high", double sustainSec = 0.05) =>
            new AnalysisData.Onset { tMs = (int)System.Math.Round(sec * 1000), strength = strength, band = band, sustainMs = (int)(sustainSec * 1000) };

        static AnalysisData Analysis(double durationSec, params AnalysisData.Onset[] onsets) => new AnalysisData
        {
            durationMs = (int)(durationSec * 1000),
            onsets = onsets.ToList(),
            segments = new List<AnalysisData.Segment> { new AnalysisData.Segment { startMs = 0, endMs = (int)(durationSec * 1000), energy = 1 } },
        };

        static AutoChartParams NoHearts() => new AutoChartParams { heartIntervalSec = 0 };

        [Test]
        public void Budget_FollowsTargetNps()
        {
            // 20초, 8분음표마다 온셋(초당 4개) → 목표 1.5 NPS면 약 30개
            var onsets = Enumerable.Range(0, 80).Select(i => On(i * 0.25, 0.5 + (i % 7) * 0.05)).ToArray();
            var r = AutoCharter.Generate(Analysis(20, onsets), Tempo, NoHearts());
            Assert.That(r.Notes.Count, Is.InRange(25, 31));
        }

        [Test]
        public void MinGap_IsMaxOfHalfBeatAnd300ms()
        {
            // 1/4박(0.125초) 간격 온셋이 빽빽해도, 결과 노트 사이는 반 박(0.25초)·300ms 중 큰 값 = 0.3초 이상
            var onsets = Enumerable.Range(0, 160).Select(i => On(i * 0.125, 1.0 - i * 0.001)).ToArray();
            var r = AutoCharter.Generate(Analysis(20, onsets), Tempo, new AutoChartParams { heartIntervalSec = 0, targetNps = 10, segmentMaxNps = 10 });
            var secs = r.Notes.Select(n => Tempo.BeatToSec(n.beat)).OrderBy(t => t).ToList();
            for (int i = 1; i < secs.Count; i++) Assert.GreaterOrEqual(secs[i] - secs[i - 1], 0.3 - 1e-6);
        }

        [Test]
        public void SnapsToQuarterBeat_AndCountsOffGrid()
        {
            // 1.02초 → 2.04박 → 2박(1/4 격자, 오차 0.04박), 3.2초 → 6.4박 → 6.5박(오차 0.1박) — 둘 다 1/8박 이내라 경고 아님
            var r = AutoCharter.Generate(Analysis(10, On(1.02, 1), On(3.2, 0.9)), Tempo, NoHearts());
            CollectionAssert.AreEqual(new[] { 2.0, 6.5 }, r.Notes.Select(n => n.beat).ToArray());
            Assert.AreEqual(0, r.OffGridCount);

            // 셋잇단 토글: 1/3박 근처는 셋잇단에 붙는다
            var t = AutoCharter.Generate(Analysis(10, On(Tempo.BeatToSec(4 + 1.0 / 3), 1)), Tempo, new AutoChartParams { heartIntervalSec = 0, allowTriplets = true });
            Assert.AreEqual(4 + 1.0 / 3, t.Notes[0].beat, 1e-9);
        }

        [Test]
        public void Lanes_ByBand_RunKeepsLane_SwitchAfterTwoOpposite()
        {
            // 0.5초(1박) 간격 = 한 런. low로 시작 → 하단 고정. high 1개는 무시, high 2연속에서 전환.
            var r = AutoCharter.Generate(Analysis(10,
                On(0.5, 1, "low"), On(1.0, 1, "high"), On(1.5, 1, "low"),
                On(2.0, 1, "high"), On(2.5, 1, "high"), On(3.0, 1, "mid")), Tempo, NoHearts());
            CollectionAssert.AreEqual(
                new[] { Lane.Bottom, Lane.Bottom, Lane.Bottom, Lane.Bottom, Lane.Top, Lane.Top },
                r.Notes.Select(n => n.lane).ToArray());
        }

        [Test]
        public void NewRun_TakesBandLane()
        {
            // 2초 넘게 쉬면 새 런 → 대역 레인으로 다시 시작
            var r = AutoCharter.Generate(Analysis(10, On(0.5, 1, "low"), On(4.0, 1, "high")), Tempo, NoHearts());
            CollectionAssert.AreEqual(new[] { Lane.Bottom, Lane.Top }, r.Notes.Select(n => n.lane).ToArray());
        }

        [Test]
        public void SustainedOnset_BecomesHold_EndingBeforeNextNote()
        {
            // 1초(2박)에 2초 지속 → 2박~6박이지만 다음 노트가 5박이라 4.5박에서 끊김(최소 간격 0.6박 → 반 박 내림)
            var r = AutoCharter.Generate(Analysis(10, On(1.0, 1, "high", 2.0), On(2.5, 0.9, "high")), Tempo, NoHearts());
            var hold = r.Notes.Single(n => n.type == NoteType.Hold);
            Assert.AreEqual(2.0, hold.beat);
            Assert.AreEqual(4.0, hold.endBeat, 1e-9);
            Assert.LessOrEqual(hold.endBeat, 5.0 - 0.6);
        }

        [Test]
        public void ShortSustain_StaysTap()
        {
            var r = AutoCharter.Generate(Analysis(10, On(1.0, 1, "high", 0.3)), Tempo, NoHearts());
            Assert.AreEqual(NoteType.Tap, r.Notes.Single().type);
        }

        [Test]
        public void Hearts_RoughlyEveryInterval_InGaps()
        {
            var onsets = Enumerable.Range(0, 60).Select(i => On(i * 1.0, 1)).ToArray(); // 60초, 1초마다
            var r = AutoCharter.Generate(Analysis(60, onsets), Tempo, new AutoChartParams());
            var hearts = r.Notes.Where(n => n.type == NoteType.Heart).ToList();
            Assert.That(hearts.Count, Is.InRange(1, 3));
            foreach (var h in hearts)
                Assert.IsFalse(r.Notes.Any(n => n != h && System.Math.Abs(n.beat - h.beat) < 0.6), "하트는 다른 노트와 최소 간격 유지");
        }

        [Test]
        public void Climax_PicksLoudest30s_StartsOnBar()
        {
            var a = Analysis(90);
            a.segments = new List<AnalysisData.Segment>
            {
                new AnalysisData.Segment { startMs = 0, endMs = 40000, energy = 0.3 },
                new AnalysisData.Segment { startMs = 40000, endMs = 75000, energy = 1.0 },
                new AnalysisData.Segment { startMs = 75000, endMs = 90000, energy = 0.4 },
            };
            var r = AutoCharter.Generate(a, Tempo, NoHearts());
            Assert.IsNotNull(r.Climax);
            Assert.AreEqual(0, r.Climax.startBeat % 4, "마디 시작");
            double start = Tempo.BeatToSec(r.Climax.startBeat), end = Tempo.BeatToSec(r.Climax.endBeat);
            Assert.That(start, Is.InRange(40.0, 45.0));
            Assert.AreEqual(30.0, end - start, 0.5);
        }

        [Test]
        public void SameInput_SameOutput()
        {
            var onsets = Enumerable.Range(0, 50).Select(i => On(i * 0.37, (i * 37 % 11) / 11.0, i % 3 == 0 ? "low" : "high", i % 5 == 0 ? 1.2 : 0.1)).ToArray();
            var a = Analysis(20, onsets);
            string x = ChartSerializer.ToJson(new ChartData { notes = AutoCharter.Generate(a, Tempo, new AutoChartParams()).Notes });
            string y = ChartSerializer.ToJson(new ChartData { notes = AutoCharter.Generate(a, Tempo, new AutoChartParams()).Notes });
            Assert.AreEqual(x, y);
        }
    }
}
