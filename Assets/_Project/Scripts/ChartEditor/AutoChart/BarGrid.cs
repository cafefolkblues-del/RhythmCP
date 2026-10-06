using System;
using System.Collections.Generic;
using System.Linq;
using RhythmCP.Chart;

namespace RhythmCP.ChartEditing
{
    /// 마디 하나의 재료: 박 범위, 그 안의 온셋, 구간 에너지로 정한 목표 노트 수, 프레이즈 위치.
    public class BarInfo
    {
        public int Index;
        public double StartBeat;
        public double StartSec;
        public double EndSec;
        public double TargetNotes;
        public string SegmentLabel;
        public int SegmentOrdinal;
        public int IndexInSegment;
        public List<AnalysisData.Onset> Onsets = new List<AnalysisData.Onset>();

        /// 4마디 프레이즈의 마지막 마디.
        public bool PhraseEnd => Index % 4 == 3;
    }

    /// 마디 격자(0박부터 4박 단위) + 마디 첫 박 추정. 순수 계산.
    public static class BarGrid
    {
        public static List<BarInfo> Build(AnalysisData a, TempoMap tempo, AutoChartParams p)
        {
            var bars = new List<BarInfo>();
            double duration = a.DurationSec;
            double lastBeat = tempo.SecToBeat(duration);
            var segments = a.segments.Count > 0
                ? a.segments
                : new List<AnalysisData.Segment> { new AnalysisData.Segment { startMs = 0, endMs = a.durationMs, energy = 1, label = "A" } };

            // 구간별 NPS: 총 노트(목표 NPS × 길이)를 에너지×길이 비례로 나누고 [min, max]로 제한(스펙 §4-①).
            double total = p.targetNps * duration;
            double weightSum = segments.Sum(s => Math.Max(0.05, s.energy) * (s.EndSec - s.StartSec));
            double SegNps(AnalysisData.Segment s) =>
                Math.Max(p.segmentMinNps, Math.Min(p.segmentMaxNps, weightSum > 0 ? total * Math.Max(0.05, s.energy) / weightSum : p.targetNps));

            double tol = p.hitToleranceBeats;
            var labelSeen = new Dictionary<string, int>();
            int lastSegIndex = -1, indexInSeg = 0, ordinal = 0;

            for (int i = 0; i * BeatGrid.BeatsPerBar < lastBeat; i++)
            {
                double b0 = i * BeatGrid.BeatsPerBar;
                double s0 = tempo.BeatToSec(b0), s1 = tempo.BeatToSec(b0 + BeatGrid.BeatsPerBar);
                double mid = (s0 + s1) / 2;
                int segIndex = Math.Max(0, segments.FindIndex(s => mid >= s.StartSec && mid < s.EndSec));
                if (segments.Count > 0 && mid >= segments[segments.Count - 1].EndSec) segIndex = segments.Count - 1;
                var seg = segments[segIndex];

                if (segIndex != lastSegIndex)
                {
                    lastSegIndex = segIndex;
                    indexInSeg = 0;
                    string label = seg.label ?? "?";
                    labelSeen.TryGetValue(label, out ordinal);
                    labelSeen[label] = ordinal + 1;
                }

                double t0 = tempo.BeatToSec(b0 - tol), t1 = tempo.BeatToSec(b0 + BeatGrid.BeatsPerBar - tol);
                var onsets = a.onsets.Where(o => o.Sec >= t0 && o.Sec < t1).ToList();
                // 목표 = 구간 밀도 × 마디 길이, 단 그 마디에 실제 있는 소리 수를 넘지 않게 — 조용한 마디에 빈 노트를 채우지 않도록.
                int audible = onsets.Count(o => o.strength >= p.strongOnset * 0.5);
                double target = mid > duration ? 0 : Math.Min(SegNps(seg) * (s1 - s0), audible);
                bars.Add(new BarInfo
                {
                    Index = i,
                    StartBeat = b0,
                    StartSec = s0,
                    EndSec = s1,
                    // 곡 밖(끝난 뒤)은 목표 0 — 자연스럽게 rest가 고른다.
                    TargetNotes = target,
                    SegmentLabel = seg.label ?? "?",
                    SegmentOrdinal = ordinal,
                    IndexInSegment = indexInSeg++,
                    Onsets = onsets,
                });
            }
            return bars;
        }

        /// 마디 첫 박 추정: 저음 온셋(킥)이 가장 많이 몰리는 박 위상(0~3). 0이 아니면 "마디 첫 박 +k박"을 추천한다.
        /// 분석기는 박 위치만 알고 어느 박이 마디 시작인지 모른다 — 패턴이 마디 기준이라 틀리면 백비트가 엇박에 꽂힌다.
        public static int EstimateDownbeatShift(AnalysisData a, TempoMap tempo, double toleranceBeats = 0.125)
        {
            var score = new double[BeatGrid.BeatsPerBar];
            foreach (var o in a.onsets)
            {
                if (o.band != "low") continue;
                double beat = tempo.SecToBeat(o.Sec);
                double nearest = Math.Round(beat);
                if (nearest < 0 || Math.Abs(beat - nearest) > toleranceBeats) continue;
                score[(int)nearest % BeatGrid.BeatsPerBar] += o.strength;
            }
            int best = 0;
            for (int k = 1; k < score.Length; k++) if (score[k] > score[best] * 1.1) best = k;
            return best;
        }
    }
}
