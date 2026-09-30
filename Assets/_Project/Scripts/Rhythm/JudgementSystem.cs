using System;
using System.Collections.Generic;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    public enum NotePart
    {
        /// Tap·Heart의 유일한 판정, Hold의 머리.
        Head,

        /// Hold 꼬리(떼는 판정).
        Tail,
    }

    public readonly struct JudgeResult
    {
        public readonly PlayNote Note;
        public readonly NotePart Part;
        public readonly Judgement Judgement;

        /// 입력 시각 − 기준 시각(초). Miss는 판정 순간의 지연값.
        public readonly double Delta;

        public JudgeResult(PlayNote note, NotePart part, Judgement judgement, double delta)
        {
            Note = note;
            Part = part;
            Judgement = judgement;
            Delta = delta;
        }

        public bool IsHit => Judgement != Judgement.Miss;
    }

    /// 판정 진입점. 레인별로 "다음에 판정할 노트"(Tap·Heart·Hold 머리)를 들고, 입력이 오면 판정하고 지나친 노트는 Miss 처리한다.
    /// 홀드 진행은 HoldJudge, 연타는 MashJudge에 맡기고, 결과 이벤트는 전부 여기서 내보낸다
    /// — 콤보·체력·UI·리액션이 구독할 곳이 하나로 유지되게.
    public class JudgementSystem : MonoBehaviour
    {
        public event Action<JudgeResult> Judged;

        /// held = 틱 시점에 누르고 있었는지. seconds = 틱 길이(떼 있었을 때 체력 감소량 계산용).
        public event Action<PlayNote, bool, double> HoldTicked;
        public event Action<PlayNote, bool> HoldBreakChanged;

        public event Action<PlayNote> MashStarted;

        /// 누적 타수.
        public event Action<PlayNote, int> MashHit;
        public event Action<PlayNote, int> MashEnded;

        readonly List<PlayNote>[] _laneNotes = { new List<PlayNote>(), new List<PlayNote>() };
        readonly int[] _cursor = new int[2];
        readonly HoldJudge[] _holds = new HoldJudge[2];
        readonly List<MashJudge> _mashes = new List<MashJudge>();
        readonly List<HoldSignal> _signals = new List<HoldSignal>();
        int _mashCursor;

        PlayChart _chart;
        SongClock _clock;
        RhythmInput _input;
        JudgeWindows _windows;
        double _holdTickBeats;
        bool _active;

        public void Init(PlayChart chart, SongClock clock, RhythmInput input, JudgeWindows windows, double holdTickBeats)
        {
            Stop();
            _chart = chart;
            _clock = clock;
            _input = input;
            _windows = windows;
            _holdTickBeats = holdTickBeats;

            foreach (var list in _laneNotes) list.Clear();
            _cursor[0] = _cursor[1] = 0;
            _holds[0] = _holds[1] = null;
            _mashes.Clear();
            _mashCursor = 0;

            foreach (var note in chart.Notes)
            {
                if (note.Type == NoteType.Mash) _mashes.Add(new MashJudge(note, windows));
                else _laneNotes[(int)note.Lane].Add(note);
            }

            _input.LanePressed += OnLanePressed;
            _input.LaneReleased += OnLaneReleased;
            _active = true;
        }

        /// 특수능력 보너스 노트. 아직 판정 안 한 구간(커서 뒤)에 시각 순서를 지켜 끼워 넣는다.
        public void AddNotes(IEnumerable<PlayNote> notes)
        {
            foreach (var note in notes)
            {
                var list = _laneNotes[(int)note.Lane];
                int i = _cursor[(int)note.Lane];
                while (i < list.Count && list[i].Time <= note.Time) i++;
                list.Insert(i, note);
            }
        }

        /// 곡 종료·실패 시 판정을 멈춘다. 남은 노트는 Miss로 치지 않는다.
        public void Stop()
        {
            if (!_active) return;
            _active = false;
            _input.LanePressed -= OnLanePressed;
            _input.LaneReleased -= OnLaneReleased;
        }

        void OnDestroy()
        {
            if (_input == null) return;
            _input.LanePressed -= OnLanePressed;
            _input.LaneReleased -= OnLaneReleased;
        }

        void OnLanePressed(Lane lane, double realtime)
        {
            // 일시정지·카운트다운 중 입력은 판정하지 않는다(⑤ 확정).
            if (!_active || _clock.IsPaused) return;
            double pressTime = _clock.RealtimeToSongTime(realtime);

            // 연타 구간 중엔 어느 버튼이든 연타로만 센다(연타 구간에 다른 노트를 겹쳐 두지 않는 게 채보 규칙).
            var mash = OpenMash(pressTime);
            if (mash != null)
            {
                mash.Hit();
                MashHit?.Invoke(mash.Note, mash.Hits);
                return;
            }

            // 이 레인 홀드가 진행 중이면 누름 = 끊긴 홀드 재개. 다음 노트 판정으로 넘기지 않는다.
            var hold = _holds[(int)lane];
            if (hold != null)
            {
                hold.Press(pressTime, _signals);
                FlushHold(lane);
                return;
            }

            // 입력 콜백이 Update보다 먼저 돌아서, 이미 지나친 노트가 아직 Miss 처리 전일 수 있다.
            // 그걸 먼저 걷어내야 이 입력이 바로 뒤 노트에 제대로 붙는다(촘촘한 연속 노트).
            PlayNote note;
            while ((note = Current(lane)) != null && _windows.IsTooLate(pressTime - note.Time))
                MissHead(lane, note, pressTime - note.Time);
            if (note == null) return;

            double delta = pressTime - note.Time;
            if (!_windows.TryEvaluate(delta, out var judgement)) return;

            _cursor[(int)lane]++;
            note.Judged = true;
            Judged?.Invoke(new JudgeResult(note, NotePart.Head, judgement, delta));

            if (note.Type == NoteType.Hold)
                _holds[(int)lane] = new HoldJudge(note, _chart.Tempo, _holdTickBeats, _windows);
        }

        void OnLaneReleased(Lane lane, double realtime)
        {
            if (!_active || _clock.IsPaused) return;
            var hold = _holds[(int)lane];
            if (hold == null) return;

            hold.Release(_clock.RealtimeToSongTime(realtime), _signals);
            FlushHold(lane);
        }

        /// 재개 직후 호출. 멈춘 사이 손을 뗐으면 그 시점부터 뗀 것으로, 다시 잡고 있으면 이어진 것으로 맞춘다(⑤ 확정).
        public void SyncHoldsAfterResume()
        {
            if (!_active) return;
            double now = _clock.SongTime;
            for (int l = 0; l < 2; l++)
            {
                var hold = _holds[l];
                if (hold == null) continue;
                bool held = _input.IsHeld((Lane)l);
                if (held && !hold.IsHeld) hold.Press(now, _signals);
                else if (!held && hold.IsHeld) hold.Release(now, _signals);
                FlushHold((Lane)l);
            }
        }

        void Update()
        {
            if (!_active || _clock.IsPaused) return;
            double now = _clock.SongTime;

            for (int l = 0; l < 2; l++)
            {
                var lane = (Lane)l;

                if (_holds[l] != null)
                {
                    _holds[l].Advance(now, _signals);
                    FlushHold(lane);
                }

                PlayNote note;
                // while: 프레임이 크게 끊기면 한 프레임에 여러 노트가 동시에 지나칠 수 있다.
                while ((note = Current(lane)) != null && _windows.IsTooLate(now - note.Time))
                    MissHead(lane, note, now - note.Time);
            }

            AdvanceMashes(now);
        }

        PlayNote Current(Lane lane)
        {
            var list = _laneNotes[(int)lane];
            int i = _cursor[(int)lane];
            return i < list.Count ? list[i] : null;
        }

        /// 머리를 놓친 홀드는 전체 실패 — 꼬리도 바로 Miss로 확정한다.
        /// ⚠️ Miss 2개가 한꺼번에 나서 체력이 미스 데미지 ×2(기본 −16) 깎인다. 판정 2개 규칙을 그대로 따른 결과.
        ///    캐주얼 기준으로 아프면 꼬리 Miss만 데미지 제외(RhythmHealth에서 Part == Tail && 머리 Miss 동반 시 무시)로 바꾼다.
        void MissHead(Lane lane, PlayNote note, double delta)
        {
            _cursor[(int)lane]++;
            note.Judged = true;
            Judged?.Invoke(new JudgeResult(note, NotePart.Head, Judgement.Miss, delta));
            if (note.Type == NoteType.Hold)
                Judged?.Invoke(new JudgeResult(note, NotePart.Tail, Judgement.Miss, delta));
        }

        void FlushHold(Lane lane)
        {
            var hold = _holds[(int)lane];
            foreach (var s in _signals)
            {
                switch (s.Type)
                {
                    case HoldSignalType.Tick:
                        HoldTicked?.Invoke(hold.Note, true, s.Seconds);
                        break;
                    case HoldSignalType.TickDropped:
                        HoldTicked?.Invoke(hold.Note, false, s.Seconds);
                        break;
                    case HoldSignalType.BreakStarted:
                        HoldBreakChanged?.Invoke(hold.Note, true);
                        break;
                    case HoldSignalType.BreakEnded:
                        HoldBreakChanged?.Invoke(hold.Note, false);
                        break;
                    case HoldSignalType.Tail:
                        Judged?.Invoke(new JudgeResult(hold.Note, NotePart.Tail, s.Tail, s.Delta));
                        break;
                }
            }
            _signals.Clear();
            if (hold.Finished) _holds[(int)lane] = null;
        }

        MashJudge OpenMash(double time)
        {
            for (int i = _mashCursor; i < _mashes.Count; i++)
            {
                if (_mashes[i].Accepts(time)) return _mashes[i];
                if (_mashes[i].Note.Time > time + _windows.Good) break;
            }
            return null;
        }

        void AdvanceMashes(double now)
        {
            for (int i = _mashCursor; i < _mashes.Count; i++)
            {
                var mash = _mashes[i];
                if (mash.TryStart(now)) MashStarted?.Invoke(mash.Note);
                if (mash.TryEnd(now))
                {
                    mash.Note.Judged = true;
                    MashEnded?.Invoke(mash.Note, mash.Hits);
                }
                if (!mash.Ended) break;
            }
            while (_mashCursor < _mashes.Count && _mashes[_mashCursor].Ended) _mashCursor++;
        }
    }
}
