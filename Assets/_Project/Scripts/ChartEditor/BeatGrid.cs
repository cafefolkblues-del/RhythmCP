using System;
using System.Collections.Generic;
using RhythmCP.Chart;

namespace RhythmCP.ChartEditing
{
    public enum GridLineKind
    {
        Bar,
        Beat,
        Sub,
    }

    public readonly struct GridLine
    {
        public readonly double Beat;
        public readonly double Time;
        public readonly GridLineKind Kind;

        public GridLine(double beat, double time, GridLineKind kind)
        {
            Beat = beat;
            Time = time;
            Kind = kind;
        }
    }

    /// 박자 격자: 스냅과 화면에 그릴 격자선. 마디는 0박부터 4박 단위(박자표 변경은 아직 미지원).
    public static class BeatGrid
    {
        public static readonly int[] Divisions = { 1, 2, 3, 4, 6, 8, 12, 16 };
        public const int BeatsPerBar = 4;
        const double Eps = 1e-6;

        /// division = 한 박을 몇 칸으로 나눌지(4 = 16분음표).
        public static double Snap(double beat, int division) => Math.Round(beat * division) / division;

        /// 스냅 단위(Divisions) 중 하나에라도 맞으면 true — 1/16박 격자(1·2·4·8·16)나 1/12박 격자(3·6·12) 위.
        /// 둘 다 아니면 검사기가 경고한다.
        public static bool IsOnGrid(double beat, double tolerance = 1e-3)
        {
            return Near(beat * 16, 16) || Near(beat * 12, 12);

            // v = 칸 단위 값. 허용 오차(박)를 칸 단위로 바꿔 비교.
            bool Near(double v, double cellsPerBeat) => Math.Abs(v - Math.Round(v)) < tolerance * cellsPerBeat;
        }

        public static List<GridLine> Lines(TempoMap tempo, double fromSec, double toSec, int division)
        {
            var lines = new List<GridLine>();
            double from = Math.Floor(tempo.SecToBeat(fromSec) * division) / division;
            double to = tempo.SecToBeat(toSec);
            // 정수 카운터로 박을 만든다 — beat += 1/division을 반복하면 셋잇단에서 오차가 쌓여 마디선 판정이 틀어진다.
            long start = (long)Math.Round(from * division);
            for (long i = start; ; i++)
            {
                double beat = (double)i / division;
                if (beat > to + Eps) break;
                var kind = i % (division * BeatsPerBar) == 0 ? GridLineKind.Bar
                    : i % division == 0 ? GridLineKind.Beat
                    : GridLineKind.Sub;
                lines.Add(new GridLine(beat, tempo.BeatToSec(beat), kind));
            }
            return lines;
        }

        /// "마디.박.칸" 표기(1부터). 예: 33.2.3
        public static string Position(double beat, int division)
        {
            double snapped = Math.Max(0, beat);
            long bar = (long)Math.Floor(snapped / BeatsPerBar);
            double inBar = snapped - bar * BeatsPerBar;
            long beatIn = (long)Math.Floor(inBar + Eps);
            long sub = (long)Math.Floor((inBar - beatIn) * division + Eps);
            return $"{bar + 1}.{beatIn + 1}.{sub + 1}";
        }
    }
}
