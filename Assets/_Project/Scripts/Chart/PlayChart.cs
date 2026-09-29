using System.Collections.Generic;

namespace RhythmCP.Chart
{
    /// 플레이용으로 변환된 채보. 시간은 전부 오디오 시각(초), 노트는 시각 오름차순.
    /// 판정 상태를 노트에 직접 들고 있으므로 플레이 1회마다 ChartLoader.Build로 새로 만든다.
    public class PlayChart
    {
        public ChartData Source { get; }
        public TempoMap Tempo { get; }
        public IReadOnlyList<PlayNote> Notes { get; }

        public bool HasClimax { get; }
        public double ClimaxStartSec { get; }
        public double ClimaxEndSec { get; }

        /// 마지막 노트(홀드·연타는 끝)가 끝나는 시각. 곡 종료 판정용.
        public double LastNoteEndSec { get; }

        public PlayChart(ChartData source, TempoMap tempo, List<PlayNote> notes)
        {
            Source = source;
            Tempo = tempo;
            Notes = notes;

            if (source.climax != null)
            {
                HasClimax = true;
                ClimaxStartSec = tempo.BeatToSec(source.climax.startBeat);
                ClimaxEndSec = tempo.BeatToSec(source.climax.endBeat);
            }

            foreach (var note in notes)
                if (note.EndTime > LastNoteEndSec) LastNoteEndSec = note.EndTime;
        }
    }

    public class PlayNote
    {
        public NoteType Type;
        public Lane Lane;
        public double Beat;
        public double Time;

        /// Hold / Mash 끝 시각. 그 외는 Time과 같다.
        public double EndTime;

        public float Speed;

        /// 같은 박자에 반대 레인 Tap이 있으면 동시치기(Gemini). 데이터에 따로 적지 않고 로더가 판단한다.
        public bool IsGemini;

        public bool Judged;
    }
}
