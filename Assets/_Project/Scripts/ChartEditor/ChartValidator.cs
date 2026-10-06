using System;
using System.Collections.Generic;
using RhythmCP.Chart;

namespace RhythmCP.ChartEditing
{
    public enum IssueSeverity
    {
        Error,
        Warning,
        Info,
    }

    public readonly struct ValidationIssue
    {
        public readonly IssueSeverity Severity;
        public readonly double Beat;
        public readonly string Message;

        public ValidationIssue(IssueSeverity severity, double beat, string message)
        {
            Severity = severity;
            Beat = beat;
            Message = message;
        }
    }

    /// 채보 검사기(순수). 기억해 둔 저작 도구 3종 중 하나 — 겹침·곡 길이 초과·격자 이탈·칠 수 없는 배치를 잡는다.
    public static class ChartValidator
    {
        /// 같은 레인에서 이보다 가까우면 겹침(1/32박).
        public const double MinGapBeats = 1.0 / 32;

        public static List<ValidationIssue> Validate(ChartData data, double songLengthSec, double holdTickBeats)
        {
            var issues = new List<ValidationIssue>();

            TempoMap tempo;
            try { tempo = new TempoMap(data.bpms, data.offsetSec); }
            catch (ArgumentException e)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, 0, "BPM 목록 오류: " + e.Message));
                return issues;
            }

            var notes = new List<NoteData>(data.notes);
            notes.Sort((a, b) => a.beat.CompareTo(b.beat));

            var holds = notes.FindAll(n => n.type == NoteType.Hold);
            var mashes = notes.FindAll(n => n.type == NoteType.Mash);
            var lastByLane = new NoteData[2];

            foreach (var n in notes)
            {
                double end = n.type == NoteType.Hold || n.type == NoteType.Mash ? n.endBeat : n.beat;

                if (n.type == NoteType.Obstacle)
                    issues.Add(new ValidationIssue(IssueSeverity.Info, n.beat, "회피 장애물은 아직 미지원(게임에서 건너뜀)"));

                if (songLengthSec > 0 && tempo.BeatToSec(end) > songLengthSec)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, n.beat, "곡 길이를 넘는 노트"));

                if (!BeatGrid.IsOnGrid(n.beat))
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, n.beat, "박자 격자에서 벗어남(스냅 단위 밖)"));

                if (n.type == NoteType.Hold || n.type == NoteType.Mash)
                {
                    if (n.endBeat <= n.beat)
                        issues.Add(new ValidationIssue(IssueSeverity.Error, n.beat, $"{Name(n.type)} 끝이 시작보다 앞섬"));
                    else if (n.type == NoteType.Hold && n.endBeat - n.beat < holdTickBeats)
                        issues.Add(new ValidationIssue(IssueSeverity.Warning, n.beat, "틱보다 짧은 홀드(콤보 틱 0개)"));
                }

                if (n.type != NoteType.Mash)
                {
                    int lane = (int)n.lane;
                    var prev = lastByLane[lane];
                    if (prev != null && n.beat - prev.beat < MinGapBeats)
                        issues.Add(new ValidationIssue(IssueSeverity.Error, n.beat, $"{LaneName(n.lane)} 레인 노트 겹침"));
                    lastByLane[lane] = n;

                    foreach (var h in holds)
                        if (h != n && h.lane == n.lane && n.beat > h.beat && n.beat < h.endBeat)
                            issues.Add(new ValidationIssue(IssueSeverity.Error, n.beat, $"{LaneName(n.lane)} 레인 홀드 안에 노트"));

                    foreach (var m in mashes)
                        if (n.beat >= m.beat && n.beat <= m.endBeat)
                            issues.Add(new ValidationIssue(IssueSeverity.Error, n.beat, "연타 구간 안에 노트(어느 버튼이든 연타로 먹힘)"));
                }
            }

            if (data.climax == null)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, 0, "클라이맥스 구간 없음(카세트 컬러화 불가)"));
            else if (data.climax.endBeat <= data.climax.startBeat)
                issues.Add(new ValidationIssue(IssueSeverity.Error, data.climax.startBeat, "클라이맥스 끝이 시작보다 앞섬"));
            else if (songLengthSec > 0 && tempo.BeatToSec(data.climax.endBeat) > songLengthSec)
                issues.Add(new ValidationIssue(IssueSeverity.Error, data.climax.startBeat, "클라이맥스가 곡 길이를 넘음"));

            issues.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            return issues;
        }

        static string LaneName(Lane lane) => lane == Lane.Top ? "상단" : "하단";

        static string Name(NoteType type) => type == NoteType.Hold ? "홀드" : "연타";
    }
}
