using System;
using System.Collections.Generic;
using NUnit.Framework;
using RhythmCP.Chart;

namespace RhythmCP.Tests
{
    public class TempoMapTests
    {
        const double Eps = 1e-9;

        static List<BpmPoint> Bpms(params (double beat, double bpm)[] points)
        {
            var list = new List<BpmPoint>();
            foreach (var p in points) list.Add(new BpmPoint { beat = p.beat, bpm = p.bpm });
            return list;
        }

        [Test]
        public void SingleBpm_BeatToSec_IncludesOffset()
        {
            var map = new TempoMap(Bpms((0, 120)), 0.25);
            Assert.AreEqual(0.25, map.BeatToSec(0), Eps);
            Assert.AreEqual(0.25 + 4 * 0.5, map.BeatToSec(4), Eps);
        }

        [Test]
        public void BpmChange_SecondSegmentUsesNewBpm()
        {
            // 0~8박 120BPM(0.5초/박) → 8박부터 150BPM(0.4초/박)
            var map = new TempoMap(Bpms((0, 120), (8, 150)), 0);
            Assert.AreEqual(4.0, map.BeatToSec(8), Eps);
            Assert.AreEqual(4.0 + 2 * 0.4, map.BeatToSec(10), Eps);
            Assert.AreEqual(150, map.BpmAtBeat(8));
            Assert.AreEqual(120, map.BpmAtBeat(7.999));
        }

        [Test]
        public void SecToBeat_IsInverseOfBeatToSec()
        {
            var map = new TempoMap(Bpms((0, 128), (16, 174), (48.5, 90)), 0.137);
            foreach (var beat in new[] { -2.0, 0, 3.25, 16, 30.333, 48.5, 60 })
                Assert.AreEqual(beat, map.SecToBeat(map.BeatToSec(beat)), 1e-6, $"beat {beat}");
        }

        [Test]
        public void NegativeBeat_ExtrapolatesFirstSegment()
        {
            var map = new TempoMap(Bpms((0, 120)), 1.0);
            Assert.AreEqual(0.0, map.BeatToSec(-2), Eps);
        }

        [Test]
        public void InvalidBpmLists_Throw()
        {
            Assert.Throws<ArgumentException>(() => new TempoMap(Bpms(), 0));
            Assert.Throws<ArgumentException>(() => new TempoMap(Bpms((1, 120)), 0));
            Assert.Throws<ArgumentException>(() => new TempoMap(Bpms((0, 0)), 0));
            Assert.Throws<ArgumentException>(() => new TempoMap(Bpms((0, 120), (0, 140)), 0));
        }
    }
}
