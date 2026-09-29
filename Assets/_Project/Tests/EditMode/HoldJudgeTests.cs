using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmCP.Chart;
using RhythmCP.Rhythm;

namespace RhythmCP.Tests
{
    public class HoldJudgeTests
    {
        static readonly JudgeWindows W = new JudgeWindows(0.050, 0.130, 0.200);

        // 120BPM, offset 0 → 1박 = 0.5초. 홀드 0~2박 = 0~1초, 1/4박 틱 = 0.125초 간격 7개.
        static readonly TempoMap Tempo = new TempoMap(new List<BpmPoint> { new BpmPoint { beat = 0, bpm = 120 } }, 0);

        static HoldJudge NewHold()
        {
            var note = new PlayNote { Type = NoteType.Hold, Beat = 0, Time = 0, EndBeat = 2, EndTime = 1.0, Speed = 1 };
            return new HoldJudge(note, Tempo, 0.25, W);
        }

        static int Count(List<HoldSignal> s, HoldSignalType t) => s.Count(x => x.Type == t);

        [Test]
        public void HeldThrough_TicksEachQuarterBeat_ThenAutoGood()
        {
            var hold = NewHold();
            var s = new List<HoldSignal>();
            hold.Advance(1.0, s);
            Assert.AreEqual(7, Count(s, HoldSignalType.Tick));
            Assert.IsFalse(hold.Finished, "끝 시각엔 아직 떼는 판정 대기");

            hold.Advance(1.21, s);
            var tail = s.Single(x => x.Type == HoldSignalType.Tail);
            Assert.AreEqual(Judgement.Good, tail.Tail, "Good 윈도우를 넘겨 계속 누르면 자동 Good");
            Assert.IsTrue(hold.Finished);
        }

        [Test]
        public void ReleaseOnTime_JudgesTail()
        {
            var hold = NewHold();
            var s = new List<HoldSignal>();
            hold.Advance(0.99, s);
            hold.Release(1.03, s);
            Assert.AreEqual(Judgement.Perfect, s.Single(x => x.Type == HoldSignalType.Tail).Tail);
        }

        [Test]
        public void EarlyRelease_DropsTicksWithTickLength_UntilRepressed()
        {
            var hold = NewHold();
            var s = new List<HoldSignal>();
            hold.Advance(0.30, s);           // 틱 0.125, 0.25 누름
            hold.Release(0.30, s);           // 끝보다 0.7초 이름 → 끊김
            hold.Advance(0.55, s);           // 틱 0.375, 0.5 뗌
            hold.Press(0.55, s);
            hold.Advance(0.80, s);           // 틱 0.625, 0.75 누름

            Assert.AreEqual(4, Count(s, HoldSignalType.Tick));
            Assert.AreEqual(2, Count(s, HoldSignalType.TickDropped));
            Assert.AreEqual(1, Count(s, HoldSignalType.BreakStarted));
            Assert.AreEqual(1, Count(s, HoldSignalType.BreakEnded));
            foreach (var d in s.Where(x => x.Type == HoldSignalType.TickDropped))
                Assert.AreEqual(0.125, d.Seconds, 1e-9);
        }

        [Test]
        public void ReleasedAtEnd_TailMiss()
        {
            var hold = NewHold();
            var s = new List<HoldSignal>();
            hold.Release(0.5, s);
            hold.Advance(1.21, s);
            Assert.AreEqual(Judgement.Miss, s.Single(x => x.Type == HoldSignalType.Tail).Tail);
        }

        [Test]
        public void BpmChangeInsideHold_TicksFollowBeats()
        {
            // 0~1박 120BPM(0.5초/박), 1박부터 60BPM(1초/박). 홀드 0~2박 = 0~1.5초.
            var tempo = new TempoMap(new List<BpmPoint> { new BpmPoint { beat = 0, bpm = 120 }, new BpmPoint { beat = 1, bpm = 60 } }, 0);
            var note = new PlayNote { Type = NoteType.Hold, Beat = 0, Time = 0, EndBeat = 2, EndTime = 1.5, Speed = 1 };
            var hold = new HoldJudge(note, tempo, 0.5, W);
            var s = new List<HoldSignal>();
            hold.Advance(0.49, s);
            Assert.AreEqual(1, Count(s, HoldSignalType.Tick), "0.5박 = 0.25초");
            hold.Advance(0.99, s);
            Assert.AreEqual(2, Count(s, HoldSignalType.Tick), "1박 = 0.5초, 1.5박 = 1.0초");
            hold.Advance(1.0, s);
            Assert.AreEqual(3, Count(s, HoldSignalType.Tick));
        }
    }

    public class MashJudgeTests
    {
        static readonly JudgeWindows W = new JudgeWindows(0.050, 0.130, 0.200);

        [Test]
        public void AcceptsFromGoodBeforeStart_UntilEnd()
        {
            var mash = new MashJudge(new PlayNote { Type = NoteType.Mash, Time = 1.0, EndTime = 2.0 }, W);
            Assert.IsFalse(mash.Accepts(0.79));
            Assert.IsTrue(mash.Accepts(0.80));
            Assert.IsTrue(mash.Accepts(2.0));
            Assert.IsFalse(mash.Accepts(2.01));
        }

        [Test]
        public void StartAndEnd_FireOnce()
        {
            var mash = new MashJudge(new PlayNote { Type = NoteType.Mash, Time = 1.0, EndTime = 2.0 }, W);
            Assert.IsFalse(mash.TryStart(0.9));
            Assert.IsTrue(mash.TryStart(1.0));
            Assert.IsFalse(mash.TryStart(1.1));
            Assert.IsFalse(mash.TryEnd(2.0));
            Assert.IsTrue(mash.TryEnd(2.01));
            Assert.IsFalse(mash.Accepts(1.5), "끝난 연타는 더 안 받음");
        }
    }
}
