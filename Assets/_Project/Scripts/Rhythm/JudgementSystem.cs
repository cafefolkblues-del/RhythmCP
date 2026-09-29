using System;
using System.Collections.Generic;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    public readonly struct JudgeResult
    {
        public readonly PlayNote Note;
        public readonly Judgement Judgement;

        /// 입력 시각 − 노트 시각(초). Miss는 판정 순간의 지연값.
        public readonly double Delta;

        public JudgeResult(PlayNote note, Judgement judgement, double delta)
        {
            Note = note;
            Judgement = judgement;
            Delta = delta;
        }
    }

    /// 레인별로 "다음에 판정할 노트"를 들고, 입력이 오면 판정하고 지나친 노트는 Miss 처리한다.
    /// ① 범위: Tap만. 나머지 종류는 ②에서 이 클래스에 분기를 추가한다.
    public class JudgementSystem : MonoBehaviour
    {
        public event Action<JudgeResult> Judged;

        readonly List<PlayNote>[] _laneNotes = { new List<PlayNote>(), new List<PlayNote>() };
        readonly int[] _cursor = new int[2];

        SongClock _clock;
        RhythmInput _input;
        JudgeWindows _windows;
        bool _active;

        public void Init(PlayChart chart, SongClock clock, RhythmInput input, JudgeWindows windows)
        {
            Stop();
            _clock = clock;
            _input = input;
            _windows = windows;

            foreach (var list in _laneNotes) list.Clear();
            _cursor[0] = _cursor[1] = 0;

            int skipped = 0;
            foreach (var note in chart.Notes)
            {
                if (note.Type != NoteType.Tap) { skipped++; continue; }
                _laneNotes[(int)note.Lane].Add(note);
            }
            if (skipped > 0)
                Debug.LogWarning($"[JudgementSystem] Tap 외 노트 {skipped}개는 ② 구현 전이라 판정하지 않음");

            _input.LanePressed += OnLanePressed;
            _active = true;
        }

        /// 곡 종료·실패 시 판정을 멈춘다. 남은 노트는 Miss로 치지 않는다.
        public void Stop()
        {
            if (!_active) return;
            _active = false;
            _input.LanePressed -= OnLanePressed;
        }

        void OnDestroy()
        {
            if (_input != null) _input.LanePressed -= OnLanePressed;
        }

        void OnLanePressed(Lane lane, double realtime)
        {
            if (!_active) return;

            double pressTime = _clock.RealtimeToSongTime(realtime);

            // 입력 콜백이 Update보다 먼저 돌아서, 이미 지나친 노트가 아직 Miss 처리 전일 수 있다.
            // 그걸 먼저 걷어내야 이 입력이 바로 뒤 노트에 제대로 붙는다(촘촘한 연속 노트).
            PlayNote note;
            while ((note = Current(lane)) != null && _windows.IsTooLate(pressTime - note.Time))
                Resolve(lane, note, Judgement.Miss, pressTime - note.Time);
            if (note == null) return;

            double delta = pressTime - note.Time;
            if (!_windows.TryEvaluate(delta, out var judgement)) return;

            Resolve(lane, note, judgement, delta);
        }

        void Update()
        {
            if (!_active) return;

            for (int l = 0; l < _laneNotes.Length; l++)
            {
                var lane = (Lane)l;
                PlayNote note;
                // while: 프레임이 크게 끊기면 한 프레임에 여러 노트가 동시에 지나칠 수 있다.
                while ((note = Current(lane)) != null && _windows.IsTooLate(_clock.SongTime - note.Time))
                    Resolve(lane, note, Judgement.Miss, _clock.SongTime - note.Time);
            }
        }

        PlayNote Current(Lane lane)
        {
            var list = _laneNotes[(int)lane];
            int i = _cursor[(int)lane];
            return i < list.Count ? list[i] : null;
        }

        void Resolve(Lane lane, PlayNote note, Judgement judgement, double delta)
        {
            note.Judged = true;
            _cursor[(int)lane]++;
            Judged?.Invoke(new JudgeResult(note, judgement, delta));
        }
    }
}
