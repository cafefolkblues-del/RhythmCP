using System;
using System.Collections.Generic;
using System.Linq;
using RhythmCP.Chart;

namespace RhythmCP.ChartEditing
{
    public class AutoChartResult
    {
        public List<NoteData> Notes = new List<NoteData>();
        public ClimaxMarker Climax;
        public List<string> Warnings = new List<string>();

        /// 마디별로 고른 리듬·레인 패턴 ID(눈금 띠 표시·튜닝·공동 작업용). 노트 없는 마디의 레인은 null.
        public List<string> BarRhythms = new List<string>();
        public List<string> BarLanes = new List<string>();

        /// 0이 아니면 "마디 첫 박 +k박" 추천(저음 온셋이 그 위상에 몰림).
        public int DownbeatShift;

        public ChartMetrics Metrics;
    }

    /// 생성 결과 지표 — 목표 수치는 실측으로 잡기로 해서(유저 결정) 리포트로만 보여준다.
    public class ChartMetrics
    {
        public int Notes;
        public double Nps;
        public double LaneSwitchRate;
        public int MaxSameLane;
        public int MaxEqualGapRun;
        public int DistinctRhythms;
        public string TopGaps;

        public override string ToString() =>
            $"NPS {Nps:0.00} · 레인 전환 {LaneSwitchRate:P0} · 같은 레인 최대 {MaxSameLane} · 같은 간격 최대 {MaxEqualGapRun} · 리듬 {DistinctRhythms}종 · 간격 {TopGaps}";

        public static ChartMetrics Measure(List<NoteData> all, double durationSec, int distinctRhythms)
        {
            var notes = all.Where(n => n.type != NoteType.Heart).OrderBy(n => n.beat).ToList();
            var m = new ChartMetrics { Notes = notes.Count, Nps = durationSec > 0 ? notes.Count / durationSec : 0, DistinctRhythms = distinctRhythms };
            if (notes.Count < 2) return m;

            int switches = 0, run = 1, gapRun = 1;
            m.MaxSameLane = 1;
            m.MaxEqualGapRun = 1;
            var gaps = new Dictionary<double, int>();
            for (int i = 1; i < notes.Count; i++)
            {
                if (notes[i].lane != notes[i - 1].lane) { switches++; run = 1; }
                else m.MaxSameLane = Math.Max(m.MaxSameLane, ++run);

                double g = Math.Round(notes[i].beat - notes[i - 1].beat, 3);
                gaps[g] = gaps.TryGetValue(g, out var c) ? c + 1 : 1;
                if (i >= 2 && Math.Abs(g - Math.Round(notes[i - 1].beat - notes[i - 2].beat, 3)) < 1e-6) m.MaxEqualGapRun = Math.Max(m.MaxEqualGapRun, ++gapRun);
                else gapRun = 1;
            }
            m.LaneSwitchRate = (double)switches / (notes.Count - 1);
            int total = notes.Count - 1;
            m.TopGaps = string.Join(" ", gaps.OrderByDescending(k => k.Value).Take(3).Select(k => $"{k.Key:0.##}박 {(double)k.Value / total:P0}"));
            return m;
        }
    }

    /// EASY 자동 배치 — 2단계 패턴 방식(2026-10-07). 분석 + 채보 TempoMap + 패턴 라이브러리 → 노트.
    ///  ① 마디 격자 ② 마디별 리듬 패턴(동적 계획법) ③ 레인 템플릿(동적 계획법) ④ 반복 구간 재사용 ⑤ 홀드·하트·클라이맥스.
    /// 순수 함수 — 같은 입력이면 같은 결과라 테스트로 규칙을 고정한다.
    public static class AutoCharter
    {
        public static AutoChartResult Generate(AnalysisData analysis, TempoMap tempo, AutoChartParams p, PatternLibrary library)
        {
            var result = new AutoChartResult();
            var bars = BarGrid.Build(analysis, tempo, p);
            var rhythms = library.rhythms;
            var templates = library.lanes.Where(l => !string.IsNullOrEmpty(l.lanes)).ToList();

            int[] chosen = PatternSelector.SelectRhythms(bars, rhythms, tempo, p);

            // 마디별 노트 위치 + 그 자리 온셋(대역·세기·지속).
            var barNotes = new List<List<PatternSelector.LaneNote>>();
            var onsetAt = new Dictionary<double, AnalysisData.Onset>();
            for (int i = 0; i < bars.Count; i++)
            {
                var list = new List<PatternSelector.LaneNote>();
                foreach (var pos in rhythms[chosen[i]].beats)
                {
                    double beat = bars[i].StartBeat + pos;
                    var o = PatternSelector.Strongest(bars[i], beat, tempo, p.hitToleranceBeats);
                    if (o != null) onsetAt[beat] = o;
                    list.Add(new PatternSelector.LaneNote { Beat = beat, Band = o?.band, Strength = o?.strength ?? 0 });
                }
                barNotes.Add(list);
            }

            var lanes = PatternSelector.SelectLanes(barNotes, templates, p);
            if (p.reuseSections)
            {
                var optionIndex = lanes.Select(l => l.lane < 0 ? -1 : l.lane * 2 + (l.aTop ? 0 : 1)).ToArray();
                lanes = PatternSelector.SelectLanes(barNotes, templates, p, PatternSelector.ReferenceFor(bars, optionIndex));
            }

            for (int i = 0; i < bars.Count; i++)
            {
                result.BarRhythms.Add(rhythms[chosen[i]].id);
                result.BarLanes.Add(lanes[i].lane < 0 ? null : templates[lanes[i].lane].id);
                if (lanes[i].lane < 0) continue;
                var seq = PatternSelector.Apply(templates[lanes[i].lane].lanes, lanes[i].aTop, barNotes[i].Count);
                for (int k = 0; k < barNotes[i].Count; k++)
                    result.Notes.Add(new NoteData { type = NoteType.Tap, lane = seq[k] == 0 ? Lane.Top : Lane.Bottom, beat = barNotes[i][k].Beat, speed = 1f });
            }

            result.Notes.Sort((x, y) => x.beat.CompareTo(y.beat));
            MakeHolds(result.Notes, onsetAt, tempo, p);
            AddHearts(result, tempo, p, analysis.DurationSec);
            result.Climax = FindClimax(analysis, tempo, p, analysis.DurationSec);
            result.Notes.Sort((a, b) => a.beat != b.beat ? a.beat.CompareTo(b.beat) : a.lane.CompareTo(b.lane));

            result.DownbeatShift = BarGrid.EstimateDownbeatShift(analysis, tempo, p.hitToleranceBeats);
            if (result.DownbeatShift != 0) result.Warnings.Add($"저음이 {result.DownbeatShift}박 뒤에 몰림 — 마디 첫 박 +{result.DownbeatShift}박 추천");
            result.Metrics = ChartMetrics.Measure(result.Notes, analysis.DurationSec, result.BarRhythms.Distinct().Count());
            return result;
        }

        // ---------------------------------------------------------------- 홀드

        /// 그 자리 온셋의 소리가 1박 이상 이어지고 다음 노트까지 여유가 있으면 홀드(스펙 §4-③).
        /// 끝은 반 박 격자, 다음 노트보다 최소 간격만큼 앞(내림)에서 끊는다.
        static void MakeHolds(List<NoteData> notes, Dictionary<double, AnalysisData.Onset> onsetAt, TempoMap tempo, AutoChartParams p)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                if (!onsetAt.TryGetValue(n.beat, out var o) || o.SustainSec <= 0) continue;
                double sustainEnd = tempo.SecToBeat(tempo.BeatToSec(n.beat) + o.SustainSec);
                double limit = i + 1 < notes.Count ? notes[i + 1].beat - MinGapBeats(notes[i + 1].beat, tempo, p) : double.MaxValue;
                double end = Math.Min(BeatGrid.Snap(sustainEnd, 2), Math.Floor(limit * 2) / 2);
                if (end - n.beat >= p.holdMinBeats - 1e-6)
                {
                    n.type = NoteType.Hold;
                    n.endBeat = end;
                }
            }
        }

        static double MinGapBeats(double beat, TempoMap tempo, AutoChartParams p) =>
            Math.Max(p.minGapBeats, p.minGapSec / (60.0 / tempo.BpmAtBeat(beat)));

        // ---------------------------------------------------------------- 하트

        /// 약 N초마다 1개, 그 창 안에서 노트 사이 빈틈이 가장 큰 곳 가운데(박 단위)에.
        static void AddHearts(AutoChartResult result, TempoMap tempo, AutoChartParams p, double duration)
        {
            if (p.heartIntervalSec <= 0 || result.Notes.Count < 2) return;
            var hearts = new List<NoteData>();
            for (double center = p.heartIntervalSec; center < duration - 2; center += p.heartIntervalSec)
            {
                double lo = tempo.SecToBeat(center - p.heartIntervalSec / 2), hi = tempo.SecToBeat(center + p.heartIntervalSec / 2);
                NoteData before = null, after = null;
                double bestGap = 0;
                for (int i = 0; i + 1 < result.Notes.Count; i++)
                {
                    var x = result.Notes[i];
                    var y = result.Notes[i + 1];
                    double xEnd = x.type == NoteType.Hold ? x.endBeat : x.beat;
                    double mid = (xEnd + y.beat) / 2;
                    if (mid < lo || mid > hi) continue;
                    if (y.beat - xEnd > bestGap) { bestGap = y.beat - xEnd; before = x; after = y; }
                }
                if (before == null) continue;
                double beforeEnd = before.type == NoteType.Hold ? before.endBeat : before.beat;
                double beat = Math.Floor((beforeEnd + after.beat) / 2);
                if (beat - beforeEnd < MinGapBeats(beat, tempo, p) || after.beat - beat < MinGapBeats(after.beat, tempo, p)) continue;
                hearts.Add(new NoteData { type = NoteType.Heart, lane = before.lane == Lane.Top ? Lane.Bottom : Lane.Top, beat = beat, speed = 1f });
            }
            result.Notes.AddRange(hearts);
        }

        // ---------------------------------------------------------------- 클라이맥스

        /// 에너지 적분이 가장 큰 N초 창 → 시작을 마디에 맞추고 끝은 N초 뒤 박에(스펙 §4-④).
        static ClimaxMarker FindClimax(AnalysisData a, TempoMap tempo, AutoChartParams p, double duration)
        {
            if (a.segments.Count == 0 || duration <= p.climaxSec) return null;
            double bestStart = 0, bestScore = -1;
            for (double s = 0; s + p.climaxSec <= duration; s += 0.5)
            {
                double score = 0;
                foreach (var seg in a.segments)
                {
                    double overlap = Math.Min(seg.EndSec, s + p.climaxSec) - Math.Max(seg.StartSec, s);
                    if (overlap > 0) score += overlap * seg.energy;
                }
                // 같은 점수면 늦은 쪽 — 곡 후반 절정을 선호(스펙: 끝점 기준 창).
                if (score >= bestScore - 1e-9) { bestScore = score; bestStart = s; }
            }
            double startBeat = Math.Max(0, Math.Round(tempo.SecToBeat(bestStart) / BeatGrid.BeatsPerBar) * BeatGrid.BeatsPerBar);
            double endBeat = Math.Round(tempo.SecToBeat(tempo.BeatToSec(startBeat) + p.climaxSec));
            return new ClimaxMarker { startBeat = startBeat, endBeat = endBeat };
        }
    }
}
