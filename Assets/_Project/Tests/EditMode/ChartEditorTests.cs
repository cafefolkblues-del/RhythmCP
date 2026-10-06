using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmCP.Chart;
using RhythmCP.ChartEditing;

namespace RhythmCP.Tests
{
    public class ChartEditorTests
    {
        static ChartData Data(params NoteData[] notes)
        {
            var d = ChartDocument.CreateEmpty("t", Difficulty.Easy);
            d.notes.AddRange(notes);
            d.climax = new ClimaxMarker { startBeat = 0, endBeat = 4 };
            return d;
        }

        static NoteData Tap(Lane lane, double beat) => new NoteData { type = NoteType.Tap, lane = lane, beat = beat };

        // ---------- ChartDocument ----------

        [Test]
        public void Edit_ThenUndoRedo_RestoresExactState()
        {
            var doc = new ChartDocument(Data(Tap(Lane.Top, 1)));
            doc.Edit(d => d.notes.Add(Tap(Lane.Bottom, 2)));
            Assert.AreEqual(2, doc.Data.notes.Count);
            Assert.IsTrue(doc.IsDirty);

            Assert.IsTrue(doc.Undo());
            Assert.AreEqual(1, doc.Data.notes.Count);
            Assert.IsTrue(doc.Redo());
            Assert.AreEqual(2, doc.Data.notes.Count);
            Assert.IsFalse(doc.Redo());
        }

        [Test]
        public void Edit_KeepsNotesSorted_AndNewEditClearsRedo()
        {
            var doc = new ChartDocument(Data());
            doc.Edit(d => d.notes.Add(Tap(Lane.Top, 5)));
            doc.Edit(d => d.notes.Add(Tap(Lane.Top, 1)));
            CollectionAssert.AreEqual(new[] { 1.0, 5.0 }, doc.Data.notes.Select(n => n.beat).ToArray());

            doc.Undo();
            doc.Edit(d => d.notes.Add(Tap(Lane.Top, 9)));
            Assert.IsFalse(doc.CanRedo);
        }

        [Test]
        public void InvalidBpmEdit_KeepsLastTempo_ReportsError()
        {
            var doc = new ChartDocument(Data());
            doc.Edit(d => d.bpms[0].bpm = 0);
            Assert.IsNotNull(doc.TempoError);
            Assert.AreEqual(0.5, doc.Tempo.BeatToSec(1), 1e-9, "마지막 정상 TempoMap(120BPM) 유지");
            doc.Edit(d => d.bpms[0].bpm = 60);
            Assert.IsNull(doc.TempoError);
            Assert.AreEqual(1.0, doc.Tempo.BeatToSec(1), 1e-9);
        }

        [Test]
        public void CreateEmpty_CopiesTimingFromOtherDifficulty()
        {
            var easy = ChartDocument.CreateEmpty("t", Difficulty.Easy);
            easy.offsetSec = 0.3; easy.bpms[0].bpm = 174; easy.bpms.Add(new BpmPoint { beat = 64, bpm = 87 });
            var hard = ChartDocument.CreateEmpty("t", Difficulty.Hard, easy);
            Assert.AreEqual(0.3, hard.offsetSec);
            Assert.AreEqual(2, hard.bpms.Count);
            hard.bpms[0].bpm = 1;
            Assert.AreEqual(174, easy.bpms[0].bpm, "깊은 복사");
        }

        // ---------- BeatGrid ----------

        [TestCase(1.12, 4, 1.0)]
        [TestCase(1.13, 4, 1.25)]
        [TestCase(1.30, 3, 1.3333333333)]
        public void Snap_RoundsToDivision(double beat, int div, double expected)
        {
            Assert.AreEqual(expected, BeatGrid.Snap(beat, div), 1e-6);
        }

        [TestCase(1.25, true)]
        [TestCase(1.0 / 3, true)]
        [TestCase(1.1, false)]
        public void IsOnGrid_SixteenthOrTriplet(double beat, bool expected)
        {
            Assert.AreEqual(expected, BeatGrid.IsOnGrid(beat));
        }

        [Test]
        public void Lines_MarkBarsBeatsSubs_WithoutDrift()
        {
            var tempo = new TempoMap(new List<BpmPoint> { new BpmPoint { beat = 0, bpm = 120 } }, 0);
            var lines = BeatGrid.Lines(tempo, 0, 4.0, 3); // 0~8박, 셋잇단
            Assert.AreEqual(8 * 3 + 1, lines.Count);
            Assert.AreEqual(GridLineKind.Bar, lines[0].Kind);
            Assert.AreEqual(GridLineKind.Bar, lines[12].Kind, "4박째 = 2마디 시작");
            Assert.AreEqual(GridLineKind.Beat, lines[3].Kind);
            Assert.AreEqual(GridLineKind.Sub, lines[1].Kind);
        }

        [Test]
        public void Position_BarBeatSub()
        {
            Assert.AreEqual("1.1.1", BeatGrid.Position(0, 4));
            Assert.AreEqual("2.3.2", BeatGrid.Position(6.25, 4));
        }

        // ---------- ChartValidator ----------

        static List<ValidationIssue> Validate(ChartData d, double len = 100) => ChartValidator.Validate(d, len, 0.25);

        [Test]
        public void CleanChart_HasNoIssues()
        {
            Assert.IsEmpty(Validate(Data(Tap(Lane.Top, 1), Tap(Lane.Bottom, 1), Tap(Lane.Top, 2))));
        }

        [Test]
        public void Detects_Overlap_OffGrid_BeyondLength()
        {
            var issues = Validate(Data(Tap(Lane.Top, 1), Tap(Lane.Top, 1), Tap(Lane.Bottom, 1.1), Tap(Lane.Top, 400)), len: 10);
            Assert.IsTrue(issues.Any(i => i.Message.Contains("겹침") && i.Beat == 1));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("격자") && i.Beat == 1.1));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("곡 길이") && i.Beat == 400));
        }

        [Test]
        public void Detects_NoteInsideHold_AndInsideMash()
        {
            var issues = Validate(Data(
                new NoteData { type = NoteType.Hold, lane = Lane.Top, beat = 1, endBeat = 3 },
                Tap(Lane.Top, 2),
                Tap(Lane.Bottom, 2),                                                     // 다른 레인은 허용
                new NoteData { type = NoteType.Mash, lane = Lane.Bottom, beat = 8, endBeat = 10 },
                Tap(Lane.Top, 9)));
            Assert.AreEqual(1, issues.Count(i => i.Message.Contains("홀드 안")));
            Assert.AreEqual(1, issues.Count(i => i.Message.Contains("연타 구간")));
        }

        [Test]
        public void Detects_MissingClimax_AndBadBpm()
        {
            var d = Data();
            d.climax = null;
            Assert.IsTrue(Validate(d).Any(i => i.Message.Contains("클라이맥스")));
            d.bpms[0].beat = 2;
            Assert.IsTrue(Validate(d).Single().Message.StartsWith("BPM"));
        }
    }
}
