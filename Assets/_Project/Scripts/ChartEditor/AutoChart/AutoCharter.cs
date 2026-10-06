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

        /// 가장 가까운 격자와 1/8박 넘게 어긋났던 온셋 수 — 많으면 BPM·오프셋이 틀렸다는 신호(스펙 §3 STEP 5).
        public int OffGridCount;
    }

    /// EASY 자동 배치(채보 스펙 §3 STEP 4~5, §4 기준). 분석 결과 + 채보의 TempoMap → 노트.
    /// 순수 함수 — 같은 입력이면 항상 같은 결과라 테스트로 규칙을 고정하고, 에디터에선 수치를 바꿔 가며 바로 다시 돌린다.
    public static class AutoCharter
    {
        class Pick
        {
            public AnalysisData.Onset Onset;
            public double Beat;
            public Lane Lane;
        }

        public static AutoChartResult Generate(AnalysisData analysis, TempoMap tempo, AutoChartParams p)
        {
            var result = new AutoChartResult();
            double duration = analysis.DurationSec;

            var picks = SelectOnsets(analysis, tempo, p, result);
            AssignLanes(picks, tempo, p);
            BuildNotes(picks, tempo, p, result);
            AddHearts(result, tempo, p, duration, analysis);
            result.Climax = FindClimax(analysis, tempo, p, duration);

            result.Notes.Sort((a, b) => a.beat != b.beat ? a.beat.CompareTo(b.beat) : a.lane.CompareTo(b.lane));
            if (result.OffGridCount > picks.Count * 0.2 && picks.Count > 0)
                result.Warnings.Add($"격자에서 크게 벗어난 온셋 {result.OffGridCount}개 — BPM·오프셋 확인 필요");
            return result;
        }

        // ---------------------------------------------------------------- ① 밀도 · 선별 · 스냅

        static List<Pick> SelectOnsets(AnalysisData a, TempoMap tempo, AutoChartParams p, AutoChartResult result)
        {
            var segments = a.segments.Count > 0
                ? a.segments
                : new List<AnalysisData.Segment> { new AnalysisData.Segment { startMs = 0, endMs = a.durationMs, energy = 1 } };

            // 총 노트 수 = 목표 NPS × 곡 길이. 구간 에너지(×길이)에 비례해 나누고 구간 NPS를 [min, max]로 제한
            // — 조용한 구간도 비지 않고, 시끄러운 구간도 EASY를 깨지 않게(스펙 §4-①).
            double total = p.targetNps * a.DurationSec;
            double weightSum = segments.Sum(s => Math.Max(0.05, s.energy) * (s.EndSec - s.StartSec));
            var picks = new List<Pick>();

            foreach (var seg in segments)
            {
                double len = seg.EndSec - seg.StartSec;
                if (len <= 0) continue;
                double share = weightSum > 0 ? total * Math.Max(0.05, seg.energy) * len / weightSum : 0;
                int budget = (int)Math.Round(Math.Max(p.segmentMinNps * len, Math.Min(p.segmentMaxNps * len, share)));

                // 세기 순으로 채택하되, 같은 칸·최소 간격 안의 노트는 버린다(이미 뽑힌 더 센 쪽이 남는다).
                var candidates = a.onsets.Where(o => o.Sec >= seg.StartSec && o.Sec < seg.EndSec)
                                          .OrderByDescending(o => o.strength).ThenBy(o => o.tMs);
                int taken = 0;
                foreach (var o in candidates)
                {
                    if (taken >= budget) break;
                    double beat = SnapBest(tempo.SecToBeat(o.Sec), p, out double deviation);
                    if (beat < 0) continue;
                    if (!FarEnough(picks, beat, tempo, p)) continue;
                    if (deviation > p.offGridFlagBeats) result.OffGridCount++;
                    picks.Add(new Pick { Onset = o, Beat = beat });
                    taken++;
                }
            }

            picks.Sort((x, y) => x.Beat.CompareTo(y.Beat));
            return picks;
        }

        /// 허용된 격자(1/1·1/2·1/4박, 셋잇단 토글) 중 가장 가까운 곳.
        static double SnapBest(double beat, AutoChartParams p, out double deviation)
        {
            double best = BeatGrid.Snap(beat, 4);
            deviation = Math.Abs(best - beat);
            if (p.allowTriplets)
            {
                double t = BeatGrid.Snap(beat, 3);
                if (Math.Abs(t - beat) < deviation)
                {
                    best = t;
                    deviation = Math.Abs(t - beat);
                }
            }
            return best;
        }

        static double MinGapBeats(double beat, TempoMap tempo, AutoChartParams p)
        {
            double secPerBeat = 60.0 / tempo.BpmAtBeat(beat);
            return Math.Max(p.minGapBeats, p.minGapSec / secPerBeat);
        }

        static bool FarEnough(List<Pick> picks, double beat, TempoMap tempo, AutoChartParams p)
        {
            double gap = MinGapBeats(beat, tempo, p) - 1e-6;
            foreach (var other in picks)
                if (Math.Abs(other.Beat - beat) < gap) return false;
            return true;
        }

        // ---------------------------------------------------------------- ② 레인

        /// 기본: 저음 → 하단, 고음 → 상단, 중음 → 직전 레인. 연속 구간(런)은 시작 레인에 고정하고,
        /// 런 도중엔 반대 대역이 N노트 연속일 때만 전환(스펙 §4-②, Q5 확정).
        static void AssignLanes(List<Pick> picks, TempoMap tempo, AutoChartParams p)
        {
            Lane lane = Lane.Top;
            int opposite = 0;
            for (int i = 0; i < picks.Count; i++)
            {
                Lane? want = BandLane(picks[i].Onset.band);
                bool newRun = i == 0 || !InRun(picks[i - 1], picks[i], tempo, p);

                if (newRun)
                {
                    lane = want ?? lane;
                    opposite = 0;
                }
                else if (want.HasValue && want.Value != lane)
                {
                    if (++opposite >= p.laneSwitchMinRun)
                    {
                        lane = want.Value;
                        opposite = 0;
                    }
                }
                else opposite = 0;

                picks[i].Lane = lane;
            }
        }

        static Lane? BandLane(string band) => band switch
        {
            "low" => Lane.Bottom,
            "high" => Lane.Top,
            _ => null,
        };

        static bool InRun(Pick a, Pick b, TempoMap tempo, AutoChartParams p) =>
            b.Beat - a.Beat <= p.runGapBeats + 1e-6
            || tempo.BeatToSec(b.Beat) - tempo.BeatToSec(a.Beat) <= p.runGapSec + 1e-6;

        // ---------------------------------------------------------------- ③ 타입(탭·홀드)

        /// 온셋 뒤 소리가 1박 이상 이어지고 그사이 다음 노트가 없으면 홀드(스펙 §4-③).
        /// 끝은 반 박 격자에 맞추고 다음 노트보다 최소 간격만큼 앞에서 끊는다.
        static void BuildNotes(List<Pick> picks, TempoMap tempo, AutoChartParams p, AutoChartResult result)
        {
            for (int i = 0; i < picks.Count; i++)
            {
                var pick = picks[i];
                var note = new NoteData { type = NoteType.Tap, lane = pick.Lane, beat = pick.Beat, speed = 1f };

                double sustainEnd = tempo.SecToBeat(tempo.BeatToSec(pick.Beat) + pick.Onset.SustainSec);
                double limit = i + 1 < picks.Count ? picks[i + 1].Beat - MinGapBeats(picks[i + 1].Beat, tempo, p) : double.MaxValue;
                // 끝은 반 박 격자. 다음 노트 쪽 한계는 내림(Floor)으로 맞춰 간격을 절대 침범하지 않게.
                double end = Math.Min(BeatGrid.Snap(sustainEnd, 2), Math.Floor(limit * 2) / 2);

                if (end - pick.Beat >= p.holdMinBeats - 1e-6)
                {
                    note.type = NoteType.Hold;
                    note.endBeat = end;
                }
                result.Notes.Add(note);
            }
        }

        // ---------------------------------------------------------------- 하트

        /// 약 N초마다 1개, 그 창 안에서 노트 사이 빈틈이 가장 큰 곳 가운데(박 단위)에 — 버스트 직후 골에 자연스럽게 들어간다.
        static void AddHearts(AutoChartResult result, TempoMap tempo, AutoChartParams p, double duration, AnalysisData a)
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
                    double gap = y.beat - xEnd;
                    if (gap > bestGap) { bestGap = gap; before = x; after = y; }
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

        /// 에너지 적분이 가장 큰 N초 창 → 시작을 마디(4박)에 맞추고 끝은 N초 뒤 박에(스펙 §4-④).
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
