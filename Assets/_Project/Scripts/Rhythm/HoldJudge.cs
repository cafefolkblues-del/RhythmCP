using System.Collections.Generic;
using RhythmCP.Chart;

namespace RhythmCP.Rhythm
{
    public enum HoldSignalType
    {
        /// 틱 시점에 누르고 있었음 → 콤보 +1.
        Tick,

        /// 틱 시점에 떼 있었음 → 틱 길이만큼 체력 감소.
        TickDropped,

        BreakStarted,
        BreakEnded,

        /// 꼬리 판정 확정. 홀드 종료.
        Tail,
    }

    public readonly struct HoldSignal
    {
        public readonly HoldSignalType Type;

        /// TickDropped일 때 직전 틱부터 이번 틱까지의 길이(초). 체력 감소량 계산용.
        public readonly double Seconds;

        public readonly Judgement Tail;
        public readonly double Delta;

        public HoldSignal(HoldSignalType type, double seconds = 0, Judgement tail = Judgement.Miss, double delta = 0)
        {
            Type = type;
            Seconds = seconds;
            Tail = tail;
            Delta = delta;
        }
    }

    /// 머리 판정이 끝난 홀드 한 개의 진행. 머리 Hit 순간 생성되고 꼬리가 확정되면 끝난다.
    /// 규칙(2026-09-30 확정):
    ///  - 틱마다 누름 여부 확인 — 누름 = 콤보 +1, 뗌 = 틱 길이 × 초당 감소
    ///  - 중간에 떼도 다시 누르면 이어짐(BreakStarted/Ended)
    ///  - 꼬리 = 떼는 타이밍 판정. 끝을 Good 윈도우 넘게 누르고 있으면 자동 Good, 뗀 채로 끝나면 Miss
    /// 입력·시계에 의존하지 않는 순수 클래스 — EditMode 테스트로 규칙을 고정한다.
    public class HoldJudge
    {
        const double TickEpsilon = 1e-6;

        readonly double[] _ticks;
        readonly JudgeWindows _windows;
        int _nextTick;
        double _lastTickTime;
        bool _held = true;

        public PlayNote Note { get; }
        public bool Finished { get; private set; }
        public bool IsHeld => _held;

        public HoldJudge(PlayNote note, TempoMap tempo, double tickBeats, JudgeWindows windows)
        {
            Note = note;
            _windows = windows;
            _lastTickTime = note.Time;

            // 틱을 박자로 찍고 초로 변환 — BPM 변속 구간을 지나는 홀드도 틱이 박에 붙어 있게.
            var ticks = new List<double>();
            for (double b = note.Beat + tickBeats; b < note.EndBeat - TickEpsilon; b += tickBeats)
                ticks.Add(tempo.BeatToSec(b));
            _ticks = ticks.ToArray();
        }

        public void Release(double time, List<HoldSignal> output)
        {
            if (Finished || !_held) return;

            double delta = time - Note.EndTime;
            if (_windows.TryEvaluate(delta, out var judgement))
            {
                Finish(judgement, delta, output);
                return;
            }

            _held = false;
            output.Add(new HoldSignal(HoldSignalType.BreakStarted));
        }

        public void Press(double time, List<HoldSignal> output)
        {
            if (Finished || _held) return;

            _held = true;
            output.Add(new HoldSignal(HoldSignalType.BreakEnded));
        }

        public void Advance(double songTime, List<HoldSignal> output)
        {
            if (Finished) return;

            while (_nextTick < _ticks.Length && _ticks[_nextTick] <= songTime)
            {
                double t = _ticks[_nextTick++];
                double seconds = t - _lastTickTime;
                _lastTickTime = t;
                output.Add(new HoldSignal(_held ? HoldSignalType.Tick : HoldSignalType.TickDropped, seconds));
            }

            double late = songTime - Note.EndTime;
            if (_windows.IsTooLate(late))
                Finish(_held ? Judgement.Good : Judgement.Miss, late, output);
        }

        void Finish(Judgement judgement, double delta, List<HoldSignal> output)
        {
            Finished = true;
            output.Add(new HoldSignal(HoldSignalType.Tail, tail: judgement, delta: delta));
        }
    }
}
