using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RhythmCP.Chart;
using RhythmCP.ChartEditing;

namespace RhythmCP.Tests
{
    /// 2단계 패턴 방식 자동 배치 규칙 고정.
    public class AutoCharterTests
    {
        // 120BPM, offset 0 → 1박 = 0.5초, 1마디 = 2초
        static readonly TempoMap Tempo = new TempoMap(new List<BpmPoint> { new BpmPoint { beat = 0, bpm = 120 } }, 0);

        static PatternLibrary Seed() => PatternLibrary.Load("Assets/_Project/Data/AutoChart/patterns_easy.json");

        static AnalysisData.Onset On(double beat, double strength = 1, string band = "high", double sustainSec = 0.05) =>
            new AnalysisData.Onset { tMs = (int)System.Math.Round(Tempo.BeatToSec(beat) * 1000), strength = strength, band = band, sustainMs = (int)(sustainSec * 1000) };

        static AnalysisData Analysis(int bars, IEnumerable<AnalysisData.Onset> onsets, string label = "A") => new AnalysisData
        {
            durationMs = bars * 2000,
            onsets = onsets.OrderBy(o => o.tMs).ToList(),
            segments = new List<AnalysisData.Segment> { new AnalysisData.Segment { startMs = 0, endMs = bars * 2000, energy = 1, label = label } },
        };

        static AutoChartParams P(System.Action<AutoChartParams> tweak = null)
        {
            var p = new AutoChartParams { heartIntervalSec = 0, restEveryBars = 0, reuseSections = false };
            tweak?.Invoke(p);
            return p;
        }

        [Test]
        public void SeedLibrary_Loads()
        {
            var lib = Seed();
            Assert.IsNotNull(lib);
            Assert.That(lib.rhythms.Count, Is.GreaterThanOrEqualTo(16));
            Assert.That(lib.lanes.Count, Is.GreaterThanOrEqualTo(9));
        }

        [Test]
        public void QuarterOnsets_PickQuarterLikePatterns_SilenceIsRest()
        {
            // 마디 0~1: 매 박 온셋, 마디 2: 무음
            var onsets = Enumerable.Range(0, 8).Select(b => On(b));
            var r = AutoCharter.Generate(Analysis(3, onsets), Tempo, P(p => p.targetNps = 2), Seed());
            Assert.AreEqual("rest", r.BarRhythms[2]);
            Assert.IsTrue(r.Notes.Where(n => n.beat < 8).All(n => n.beat == System.Math.Round(n.beat)), "박 위 노트만");
        }

        [Test]
        public void NoPatternThreeBarsInARow()
        {
            var onsets = Enumerable.Range(0, 32).Select(b => On(b)); // 8마디 내내 4분
            var r = AutoCharter.Generate(Analysis(8, onsets), Tempo, P(p => p.targetNps = 2), Seed());
            for (int i = 2; i < r.BarRhythms.Count; i++)
                Assert.IsFalse(r.BarRhythms[i] == r.BarRhythms[i - 1] && r.BarRhythms[i] == r.BarRhythms[i - 2], $"마디 {i}");
        }

        [Test]
        public void FastBpm_ExcludesEighthPatterns()
        {
            // 200BPM: 반 박 = 0.15초 < 최소 0.3초 → 8분이 든 패턴은 후보에서 빠진다
            var fast = new TempoMap(new List<BpmPoint> { new BpmPoint { beat = 0, bpm = 200 } }, 0);
            var a = new AnalysisData
            {
                durationMs = 12000,
                onsets = Enumerable.Range(0, 80).Select(i => new AnalysisData.Onset { tMs = (int)(fast.BeatToSec(i * 0.5) * 1000), strength = 1, band = "high" }).ToList(),
                segments = new List<AnalysisData.Segment> { new AnalysisData.Segment { startMs = 0, endMs = 12000, energy = 1, label = "A" } },
            };
            var r = AutoCharter.Generate(a, fast, P(p => p.targetNps = 4), Seed());
            var beats = r.Notes.Select(n => n.beat).OrderBy(b => b).ToList();
            for (int i = 1; i < beats.Count; i++)
                Assert.GreaterOrEqual(fast.BeatToSec(beats[i]) - fast.BeatToSec(beats[i - 1]), 0.3 - 1e-6);
        }

        [Test]
        public void Lanes_FollowBand_KickBottomSnareTop()
        {
            // 킥(저음) 0·2박, 스네어(고음) 1·3박 반복
            var onsets = Enumerable.Range(0, 16).Select(b => On(b, 1, b % 2 == 0 ? "low" : "high"));
            var r = AutoCharter.Generate(Analysis(4, onsets), Tempo, P(p => p.targetNps = 2), Seed());
            var onBeat = r.Notes.Where(n => n.type != NoteType.Heart).ToList();
            int agree = onBeat.Count(n => (((int)n.beat % 2 == 0) ? Lane.Bottom : Lane.Top) == n.lane);
            Assert.That(agree, Is.GreaterThanOrEqualTo(onBeat.Count * 0.75), $"대역 일치 {agree}/{onBeat.Count}");
        }

        [Test]
        public void Lanes_MaxSameLane_AndFastPairsSameLane()
        {
            // 전부 고음(상단 선호)이어도 같은 레인 4연속 제한, 반 박 쌍은 같은 레인
            var onsets = Enumerable.Range(0, 64).Select(i => On(i * 0.5, i % 4 == 0 ? 1 : 0.6, "high"));
            var r = AutoCharter.Generate(Analysis(8, onsets), Tempo, P(p => p.targetNps = 2.5), Seed());
            var notes = r.Notes.OrderBy(n => n.beat).ToList();
            int run = 1;
            for (int i = 1; i < notes.Count; i++)
            {
                if (notes[i].beat - notes[i - 1].beat <= 0.5 + 1e-6) Assert.AreEqual(notes[i - 1].lane, notes[i].lane, $"반 박 쌍 {notes[i].beat}");
                run = notes[i].lane == notes[i - 1].lane ? run + 1 : 1;
                Assert.LessOrEqual(run, 4, $"같은 레인 연속 {notes[i].beat}");
            }
        }

        [Test]
        public void RestBar_EveryNBars()
        {
            var onsets = Enumerable.Range(0, 64).Select(b => On(b, 0.5)); // 16마디 내내 약한 4분
            var r = AutoCharter.Generate(Analysis(16, onsets), Tempo, P(p => { p.restEveryBars = 8; p.restBonus = 3; }), Seed());
            Assert.That(r.BarRhythms[7], Is.EqualTo("rest").Or.EqualTo("one"));
            Assert.That(r.BarRhythms[15], Is.EqualTo("rest").Or.EqualTo("one"));
        }

        [Test]
        public void RepeatedSection_ReusesPatterns()
        {
            // A(4마디) B(4마디) A(4마디): A의 리듬이 같으면 두 번째 A는 첫 A와 같은 패턴
            var rnd = new System.Random(7);
            var aRhythm = Enumerable.Range(0, 4).Select(_ => Enumerable.Range(0, 8).Where(__ => rnd.NextDouble() < 0.5).Select(k => k * 0.5).ToList()).ToList();
            var onsets = new List<AnalysisData.Onset>();
            for (int bar = 0; bar < 12; bar++)
            {
                var src = bar < 4 || bar >= 8 ? aRhythm[bar % 4] : new List<double> { 0, 2 };
                onsets.AddRange(src.Select(pos => On(bar * 4 + pos, 0.9, pos % 1 == 0 ? "low" : "high")));
            }
            var a = Analysis(12, onsets);
            a.segments = new List<AnalysisData.Segment>
            {
                new AnalysisData.Segment { startMs = 0, endMs = 8000, energy = 1, label = "A" },
                new AnalysisData.Segment { startMs = 8000, endMs = 16000, energy = 1, label = "B" },
                new AnalysisData.Segment { startMs = 16000, endMs = 24000, energy = 1, label = "A" },
            };
            var r = AutoCharter.Generate(a, Tempo, P(p => p.reuseSections = true), Seed());
            CollectionAssert.AreEqual(r.BarRhythms.Take(4).ToList(), r.BarRhythms.Skip(8).Take(4).ToList());
        }

        [Test]
        public void DownbeatShift_FindsKickPhase()
        {
            // 킥이 매 마디 2박째(위상 1)에 → +1박 추천
            var onsets = Enumerable.Range(0, 8).Select(bar => On(bar * 4 + 1, 1, "low"))
                .Concat(Enumerable.Range(0, 32).Select(b => On(b, 0.3, "high")));
            Assert.AreEqual(1, BarGrid.EstimateDownbeatShift(Analysis(8, onsets), Tempo));
        }

        [Test]
        public void SustainedOnset_BecomesHold()
        {
            var onsets = new[] { On(0, 1, "low", 1.6), On(4, 1), On(6, 1) };
            var r = AutoCharter.Generate(Analysis(2, onsets), Tempo, P(p => p.targetNps = 1), Seed());
            var hold = r.Notes.FirstOrDefault(n => n.type == NoteType.Hold);
            Assert.IsNotNull(hold);
            Assert.AreEqual(0, hold.beat);
            Assert.GreaterOrEqual(hold.endBeat, 1.0);
        }

        [Test]
        public void Harvest_AddsOnlyNewPatterns()
        {
            var lib = Seed();
            int before = lib.rhythms.Count;
            var (id1, _, added1) = lib.Harvest(new List<double> { 0, 1, 2, 3 }, "AABB");
            Assert.AreEqual("quarters", id1);
            Assert.IsFalse(added1);
            var (id2, lane2, added2) = lib.Harvest(new List<double> { 0, 0.5, 1.5, 3 }, "ABAB");
            Assert.IsTrue(added2);
            Assert.AreEqual(before + 1, lib.rhythms.Count);
            Assert.AreEqual("ABAB", lane2);
            Assert.IsTrue(id2.StartsWith("h_"));
        }

        [Test]
        public void SameInput_SameOutput()
        {
            var onsets = Enumerable.Range(0, 60).Select(i => On(i * 0.5, (i * 37 % 11) / 11.0, i % 3 == 0 ? "low" : "high"));
            var a = Analysis(8, onsets);
            string x = string.Join(",", AutoCharter.Generate(a, Tempo, new AutoChartParams(), Seed()).Notes.Select(n => $"{n.beat}{n.lane}{n.type}"));
            string y = string.Join(",", AutoCharter.Generate(a, Tempo, new AutoChartParams(), Seed()).Notes.Select(n => $"{n.beat}{n.lane}{n.type}"));
            Assert.AreEqual(x, y);
        }
    }
}
