using System.Collections.Generic;
using RhythmCP.Chart;
using RhythmCP.Rhythm;
using UnityEngine;

namespace RhythmCP.ChartEditing
{
    /// 녹음식 입력: 재생하면서 레인 키를 누르면 노트로 기록. 기억해 둔 저작 도구 3종 중 하나.
    /// 입력은 게임과 같은 RhythmInput + SongClock.RealtimeToSongTime(판정 오프셋 포함) → 녹음 타이밍 = 게임 판정 타이밍.
    /// 짧게 누름 = Tap, 반 박 이상 누름 = Hold(2026-10-06 확정). 한 번 녹음 = 되돌리기 1단위(세션이 한 번에 반영).
    public class RecordingController : MonoBehaviour
    {
        [SerializeField] RhythmInput _input;
        [SerializeField] PlaybackController _playback;
        [SerializeField] double _holdMinBeats = 0.5;

        readonly List<NoteData> _pending = new List<NoteData>();
        readonly double[] _downBeat = new double[2];
        readonly bool[] _down = new bool[2];
        TempoMap _tempo;
        int _division;
        bool _snap;

        public bool IsRecording { get; private set; }
        public IReadOnlyList<NoteData> Pending => _pending;

        public void Begin(TempoMap tempo, int division, bool snap)
        {
            _tempo = tempo;
            _division = division;
            _snap = snap;
            _pending.Clear();
            _down[0] = _down[1] = false;
            IsRecording = true;
            _input.LanePressed += OnPressed;
            _input.LaneReleased += OnReleased;
        }

        /// 녹음 종료. 누르고 있던 키는 지금 시각에서 뗀 것으로 마무리한다.
        public List<NoteData> End()
        {
            if (!IsRecording) return new List<NoteData>();
            IsRecording = false;
            _input.LanePressed -= OnPressed;
            _input.LaneReleased -= OnReleased;
            for (int l = 0; l < 2; l++)
                if (_down[l]) Close((Lane)l, _tempo.SecToBeat(_playback.SongTime));
            return new List<NoteData>(_pending);
        }

        void OnDestroy()
        {
            _input.LanePressed -= OnPressed;
            _input.LaneReleased -= OnReleased;
        }

        void OnPressed(Lane lane, double realtime)
        {
            if (!_playback.IsPlaying) return;
            int i = (int)lane;
            if (_down[i]) return; // 같은 레인 두 번째 키(F 누른 채 D)는 무시 — 녹음은 레인당 하나씩
            _down[i] = true;
            _downBeat[i] = _tempo.SecToBeat(_playback.Clock.RealtimeToSongTime(realtime));
        }

        void OnReleased(Lane lane, double realtime)
        {
            if (!_down[(int)lane]) return;
            Close(lane, _tempo.SecToBeat(_playback.Clock.RealtimeToSongTime(realtime)));
        }

        void Close(Lane lane, double upBeat)
        {
            int i = (int)lane;
            _down[i] = false;
            double start = Snap(_downBeat[i]);
            double end = Snap(upBeat);

            var note = new NoteData { lane = lane, beat = start, speed = 1f };
            if (upBeat - _downBeat[i] >= _holdMinBeats && end > start)
            {
                note.type = NoteType.Hold;
                note.endBeat = end;
            }
            else note.type = NoteType.Tap;
            _pending.Add(note);
        }

        double Snap(double beat) => _snap ? BeatGrid.Snap(beat, _division) : beat;
    }
}
