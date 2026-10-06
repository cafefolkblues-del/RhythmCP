using System;
using System.Collections.Generic;
using System.Linq;
using RhythmCP.Chart;

namespace RhythmCP.ChartEditing
{
    /// 마디마다 리듬 패턴·레인 패턴을 고른다. 곡 전체를 한 번에 보는 동적 계획법(비터비)이라
    /// "같은 패턴 3마디 연속 금지", "같은 레인 최대 N연속", "빠른 연타는 같은 레인"처럼 마디를 넘는 규칙을 지키면서 총점이 가장 큰 조합을 찾는다.
    /// 반복 구간(같은 라벨)은 1차 결과의 첫 등장 패턴을 기준으로 가점을 주고 한 번 더 푼다.
    public static class PatternSelector
    {
        const double Invalid = double.NegativeInfinity;

        // ---------------------------------------------------------------- 리듬

        public static int[] SelectRhythms(List<BarInfo> bars, List<PatternLibrary.RhythmPattern> patterns, TempoMap tempo, AutoChartParams p)
        {
            int[] first = SolveRhythms(bars, patterns, tempo, p, null);
            if (!p.reuseSections) return first;
            return SolveRhythms(bars, patterns, tempo, p, ReferenceFor(bars, first));
        }

        /// 같은 라벨의 앞선 등장에서 같은 순번 마디가 고른 값(없으면 -1).
        public static int[] ReferenceFor(List<BarInfo> bars, int[] chosen)
        {
            var reference = new int[bars.Count];
            for (int i = 0; i < bars.Count; i++)
            {
                reference[i] = -1;
                if (bars[i].SegmentOrdinal == 0) continue;
                for (int j = 0; j < i; j++)
                {
                    if (bars[j].SegmentLabel == bars[i].SegmentLabel && bars[j].SegmentOrdinal == 0 && bars[j].IndexInSegment == bars[i].IndexInSegment)
                    {
                        reference[i] = chosen[j];
                        break;
                    }
                }
            }
            return reference;
        }

        static int[] SolveRhythms(List<BarInfo> bars, List<PatternLibrary.RhythmPattern> patterns, TempoMap tempo, AutoChartParams p, int[] reference)
        {
            int n = bars.Count, m = patterns.Count;
            if (n == 0) return new int[0];

            var local = new double[n, m];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                {
                    local[i, j] = LocalRhythmScore(bars[i], patterns[j], tempo, p);
                    if (reference != null && reference[i] == j && local[i, j] > Invalid) local[i, j] += p.reuseBonus;
                }

            // 상태 = (직전 마디 패턴, 현재 마디 패턴). 직전 = m 은 "없음"(첫 마디). 세 마디 연속 같은 패턴은 전이 불가.
            int S = m + 1;
            var score = new double[n, S, m];
            var back = new int[n, S, m];
            for (int a = 0; a < S; a++) for (int b = 0; b < m; b++) score[0, a, b] = a == m ? local[0, b] : Invalid;

            for (int i = 1; i < n; i++)
            {
                for (int b = 0; b < m; b++)
                for (int c = 0; c < m; c++)
                {
                    score[i, b, c] = Invalid;
                    if (local[i, c] == Invalid || !CrossBarOk(bars[i - 1], patterns[b], patterns[c], tempo, p)) continue;
                    double best = Invalid;
                    int arg = -1;
                    for (int a = 0; a < S; a++)
                    {
                        double prev = score[i - 1, a, b];
                        if (prev == Invalid || (a == b && b == c)) continue;
                        if (prev > best) { best = prev; arg = a; }
                    }
                    if (arg < 0) continue;
                    score[i, b, c] = best + local[i, c] - (b == c ? p.repeatPenalty : 0);
                    back[i, b, c] = arg;
                }
                for (int c = 0; c < m; c++) score[i, m, c] = Invalid;
            }

            // 끝에서 최고점 → 거꾸로 따라가기.
            double top = Invalid;
            int lastA = 0, lastB = 0;
            for (int a = 0; a < S; a++) for (int b = 0; b < m; b++)
                if (score[n - 1, a, b] > top) { top = score[n - 1, a, b]; lastA = a; lastB = b; }

            var result = new int[n];
            if (top == Invalid)
            {
                // 모든 경로가 막힘(라이브러리가 너무 작음 등) — 마디별 최고점으로 대신한다.
                for (int i = 0; i < n; i++) result[i] = ArgMax(local, i, m);
                return result;
            }
            int cur = lastB, prevState = lastA;
            for (int i = n - 1; i >= 0; i--)
            {
                result[i] = cur;
                if (i == 0) break;
                int pp = back[i, prevState, cur];
                cur = prevState;
                prevState = pp;
            }
            return result;
        }

        static int ArgMax(double[,] local, int i, int m)
        {
            int best = 0;
            for (int j = 1; j < m; j++) if (local[i, j] > local[i, best]) best = j;
            return best;
        }

        public static double LocalRhythmScore(BarInfo bar, PatternLibrary.RhythmPattern pattern, TempoMap tempo, AutoChartParams p)
        {
            if (pattern.triplet && !p.allowTriplets) return Invalid;

            // 최소 간격: 마디 안 이웃 위치끼리.
            for (int k = 1; k < pattern.beats.Count; k++)
                if (!GapOk(bar.StartBeat + pattern.beats[k - 1], bar.StartBeat + pattern.beats[k], tempo, p)) return Invalid;

            double hit = 0;
            int empty = 0;
            foreach (var pos in pattern.beats)
            {
                var o = Strongest(bar, bar.StartBeat + pos, tempo, p.hitToleranceBeats);
                if (o != null) hit += o.strength * MetricWeight(pos);
                else empty++;
            }

            double miss = 0;
            foreach (var o in bar.Onsets)
            {
                if (o.strength < p.strongOnset) continue;
                double rel = tempo.SecToBeat(o.Sec) - bar.StartBeat;
                if (!pattern.beats.Any(pos => Math.Abs(pos - rel) <= p.hitToleranceBeats)) miss += o.strength;
            }

            double score = p.hitWeight * hit - p.missWeight * miss - p.emptyPenalty * empty - p.densityWeight * Math.Abs(pattern.beats.Count - bar.TargetNotes);

            // 마디 위치: 프레이즈 끝엔 rest·fill·pickup, 그 밖에선 fill·pickup 감점. N마디마다 쉬는 마디 가점(Kantan 규칙).
            bool endish = pattern.role == "rest" || pattern.role == "fill" || pattern.role == "pickup";
            if (bar.PhraseEnd && endish) score += p.phraseBonus;
            if (!bar.PhraseEnd && (pattern.role == "fill" || pattern.role == "pickup")) score -= p.phraseBonus;
            if (p.restEveryBars > 0 && bar.Index % p.restEveryBars == p.restEveryBars - 1 && pattern.role == "rest") score += p.restBonus;
            return score;
        }

        /// 첫 박 > 정박 > 반 박 > 그 밖. 강박 중심 배치를 유도.
        static double MetricWeight(double pos) =>
            Math.Abs(pos) < 1e-6 ? 1.3 : Math.Abs(pos - Math.Round(pos)) < 1e-6 ? 1.0 : Math.Abs(pos * 2 - Math.Round(pos * 2)) < 1e-6 ? 0.8 : 0.7;

        public static AnalysisData.Onset Strongest(BarInfo bar, double beat, TempoMap tempo, double tolBeats)
        {
            AnalysisData.Onset best = null;
            foreach (var o in bar.Onsets)
            {
                if (Math.Abs(tempo.SecToBeat(o.Sec) - beat) > tolBeats) continue;
                if (best == null || o.strength > best.strength) best = o;
            }
            return best;
        }

        static bool CrossBarOk(BarInfo prevBar, PatternLibrary.RhythmPattern prev, PatternLibrary.RhythmPattern next, TempoMap tempo, AutoChartParams p)
        {
            if (prev.beats.Count == 0 || next.beats.Count == 0) return true;
            return GapOk(prevBar.StartBeat + prev.beats[prev.beats.Count - 1], prevBar.StartBeat + BeatGrid.BeatsPerBar + next.beats[0], tempo, p);
        }

        static bool GapOk(double beatA, double beatB, TempoMap tempo, AutoChartParams p) =>
            beatB - beatA >= p.minGapBeats - 1e-6 && tempo.BeatToSec(beatB) - tempo.BeatToSec(beatA) >= p.minGapSec - 1e-6;

        // ---------------------------------------------------------------- 레인

        public class LaneNote
        {
            public double Beat;
            public string Band;
            public double Strength;
        }

        /// 마디별 노트(리듬 결정 후)에 레인 템플릿(A/B)을 입힌다. 반환 = 마디별 (템플릿 번호, A가 상단인지) — 노트 없는 마디는 (-1, false).
        public static (int lane, bool aTop)[] SelectLanes(List<List<LaneNote>> barNotes, List<PatternLibrary.LanePattern> templates, AutoChartParams p, int[] reference = null)
        {
            int n = barNotes.Count;
            // 옵션 = 템플릿 × (A=상단/하단). 상태 = (마지막 레인, 같은 레인 연속 수, 직전 옵션).
            int opts = templates.Count * 2;
            int maxRun = Math.Max(1, p.maxSameLane);
            int optStates = opts + 1; // + "없음"
            int S = 2 * maxRun * optStates;

            int Enc(int lane, int run, int opt) => (lane * maxRun + (run - 1)) * optStates + opt;

            var score = new double[n + 1, S];
            var back = new int[n + 1, S];
            var choice = new int[n + 1, S];
            for (int s = 0; s < S; s++) score[0, s] = Invalid;
            score[0, Enc(0, 1, opts)] = 0; // 시작: 가상의 "레인 0, 연속 1", 첫 노트가 이와 무관하도록 아래에서 처리

            double lastBeatPrev = double.NegativeInfinity;
            bool started = false;

            for (int i = 0; i < n; i++)
            {
                for (int s = 0; s < S; s++) score[i + 1, s] = Invalid;
                var notes = barNotes[i];
                double prevLast = lastBeatPrev;

                for (int s = 0; s < S; s++)
                {
                    double cur = score[i, s];
                    if (cur == Invalid) continue;
                    int lastLane = s / optStates / maxRun, run = (s / optStates) % maxRun + 1, prevOpt = s % optStates;

                    if (notes.Count == 0)
                    {
                        // 쉬는 마디는 레인 상태를 그대로 넘긴다(연속 수는 쉼으로 끊긴다).
                        int ns = Enc(lastLane, 1, opts);
                        if (cur > score[i + 1, ns]) { score[i + 1, ns] = cur; back[i + 1, ns] = s; choice[i + 1, ns] = -1; }
                        continue;
                    }

                    for (int o = 0; o < opts; o++)
                    {
                        var seq = Apply(templates[o / 2].lanes, o % 2 == 0, notes.Count);
                        if (!TryWalk(seq, notes, started ? lastLane : -1, started && notes[0].Beat - prevLast <= p.fastPairBeats + 1e-6, run, prevLast, p, out int endLane, out int endRun)) continue;

                        double add = 0;
                        for (int k = 0; k < notes.Count; k++) add += BandScore(notes[k], seq[k]) * p.bandWeight;
                        if (o == prevOpt) add -= p.laneRepeatPenalty;
                        if (reference != null && reference[i] == o) add += p.reuseBonus;

                        int ns = Enc(endLane, endRun, o);
                        if (cur + add > score[i + 1, ns]) { score[i + 1, ns] = cur + add; back[i + 1, ns] = s; choice[i + 1, ns] = o; }
                    }
                }

                if (notes.Count > 0)
                {
                    lastBeatPrev = notes[notes.Count - 1].Beat;
                    started = true;
                }
            }

            int bestS = -1;
            for (int s = 0; s < S; s++) if (score[n, s] > Invalid && (bestS < 0 || score[n, s] > score[n, bestS])) bestS = s;

            var result = new (int, bool)[n];
            if (bestS < 0)
            {
                // 제약을 다 만족하는 조합이 없음 — 같은 레인 연속 제한만 풀고 다시.
                if (p.maxSameLane < 64)
                {
                    var relaxed = Clone(p);
                    relaxed.maxSameLane = 64;
                    return SelectLanes(barNotes, templates, relaxed, reference);
                }
                for (int i = 0; i < n; i++) result[i] = (-1, false);
                return result;
            }
            for (int i = n, s = bestS; i > 0; i--)
            {
                int o = choice[i, s];
                result[i - 1] = o < 0 ? (-1, false) : (o / 2, o % 2 == 0);
                s = back[i, s];
            }
            return result;
        }

        static AutoChartParams Clone(AutoChartParams p) => Newtonsoft.Json.JsonConvert.DeserializeObject<AutoChartParams>(Newtonsoft.Json.JsonConvert.SerializeObject(p));

        /// 템플릿 문자열(A/B)을 노트 수만큼 반복 적용. 반환: 노트별 레인(0 = 상단, 1 = 하단).
        public static int[] Apply(string template, bool aTop, int count)
        {
            var seq = new int[count];
            for (int k = 0; k < count; k++)
            {
                bool isA = template[k % template.Length] == 'A';
                seq[k] = isA == aTop ? 0 : 1;
            }
            return seq;
        }

        /// 마디 노트들을 레인 순서대로 걸으며 제약 확인: 반 박 이하 이웃은 같은 레인, 같은 레인 최대 N연속(마디를 넘어서도 셈).
        static bool TryWalk(int[] seq, List<LaneNote> notes, int lastLane, bool firstIsFastFromPrev, int run, double prevBeat, AutoChartParams p, out int endLane, out int endRun)
        {
            endLane = lastLane;
            endRun = run;
            int lane = lastLane, r = lastLane < 0 ? 0 : run;
            double beat = prevBeat;
            for (int k = 0; k < seq.Length; k++)
            {
                bool fast = k == 0 ? firstIsFastFromPrev : notes[k].Beat - notes[k - 1].Beat <= p.fastPairBeats + 1e-6;
                if (fast && lane >= 0 && seq[k] != lane) return false;
                r = seq[k] == lane ? r + 1 : 1;
                if (r > p.maxSameLane) return false;
                lane = seq[k];
                beat = notes[k].Beat;
            }
            endLane = lane;
            endRun = r;
            return true;
        }

        /// 저음 노트 = 하단, 고음 노트 = 상단이면 +, 반대면 −(세기 비례). 중음은 0.
        static double BandScore(LaneNote note, int lane)
        {
            int want = note.Band == "high" ? 0 : note.Band == "low" ? 1 : -1;
            if (want < 0) return 0;
            return (want == lane ? 1 : -1) * Math.Max(0.2, note.Strength);
        }
    }
}
