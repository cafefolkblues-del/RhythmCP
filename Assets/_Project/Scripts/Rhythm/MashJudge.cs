using RhythmCP.Chart;

namespace RhythmCP.Rhythm
{
    /// 연타 구간 한 개. 아무 버튼이나 받고, 미스는 없다. 받는 구간 = [시작 − Good, 끝].
    public class MashJudge
    {
        readonly double _openTime;

        public PlayNote Note { get; }
        public int Hits { get; private set; }
        public bool Started { get; private set; }
        public bool Ended { get; private set; }

        public MashJudge(PlayNote note, JudgeWindows windows)
        {
            Note = note;
            _openTime = note.Time - windows.Good;
        }

        public bool Accepts(double time) => !Ended && time >= _openTime && time <= Note.EndTime;

        public void Hit() => Hits++;

        /// 시작 시각을 처음 넘은 프레임에 true (놀람이 아니라 응원 리액션 트리거 — 와이어프레임 기준).
        public bool TryStart(double songTime)
        {
            if (Started || songTime < Note.Time) return false;
            Started = true;
            return true;
        }

        public bool TryEnd(double songTime)
        {
            if (Ended || songTime <= Note.EndTime) return false;
            Ended = true;
            return true;
        }
    }
}
