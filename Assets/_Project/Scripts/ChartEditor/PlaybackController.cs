using System;
using RhythmCP.Chart;
using RhythmCP.Rhythm;
using UnityEngine;

namespace RhythmCP.ChartEditing
{
    /// 커서 위치부터 재생·정지, A-B 구간 반복, 보조음(노트 타격음·메트로놈).
    /// 시계는 게임과 같은 SongClock — 에디터에서 들리는 타이밍 = 게임 타이밍.
    public class PlaybackController : MonoBehaviour
    {
        [SerializeField] SongClock _clock;

        [Tooltip("보조음 예약 재생용 소스 풀. 겹쳐 울려도 앞 소리가 끊기지 않을 만큼.")]
        [SerializeField] AudioSource[] _assistSources;
        [SerializeField] AudioClip _hitClip;
        [SerializeField] AudioClip _tickClip;

        [Tooltip("이만큼 앞의 보조음을 미리 예약한다. 프레임 시각이 아니라 오디오 시계로 울리게.")]
        [SerializeField] double _scheduleAheadSec = 0.15;

        public event Action Stopped;

        public SongClock Clock => _clock;
        public bool IsPlaying => _clock.IsRunning;
        public double SongTime => _clock.SongTime;

        public bool AssistHits { get; set; } = true;
        public bool Metronome { get; set; }
        public bool LoopEnabled { get; set; }
        public double LoopStartSec { get; set; }
        public double LoopEndSec { get; set; }

        ChartDocument _doc;
        AudioClip _clip;
        double _scheduledUntil;
        int _nextSource;

        public void Play(AudioClip clip, ChartDocument doc, double fromSec)
        {
            _clip = clip;
            _doc = doc;
            _clock.PlayFrom(clip, fromSec);
            _scheduledUntil = fromSec - 0.001;
        }

        public void Stop()
        {
            if (!IsPlaying) return;
            _clock.Stop();
            foreach (var s in _assistSources) s.Stop();
            Stopped?.Invoke();
        }

        void Update()
        {
            if (!IsPlaying) return;
            double now = _clock.SongTime;

            if (LoopEnabled && LoopEndSec > LoopStartSec && now >= LoopEndSec)
            {
                foreach (var s in _assistSources) s.Stop();
                Play(_clip, _doc, LoopStartSec);
                return;
            }

            if (_clip != null && now > _clip.length + 0.5)
            {
                Stop();
                return;
            }

            double horizon = now + _scheduleAheadSec;
            if (horizon <= _scheduledUntil) return;
            var tempo = _doc.Tempo;

            if (AssistHits)
            {
                foreach (var n in _doc.Data.notes)
                {
                    if (n.type == NoteType.Mash || n.type == NoteType.Obstacle) continue;
                    double t = tempo.BeatToSec(n.beat);
                    if (t > _scheduledUntil && t <= horizon) Schedule(_hitClip, t);
                }
            }

            if (Metronome)
            {
                double b = Math.Floor(tempo.SecToBeat(_scheduledUntil)) + 1;
                for (double t; (t = tempo.BeatToSec(b)) <= horizon; b++)
                    if (t > _scheduledUntil) Schedule(_tickClip, t);
            }

            _scheduledUntil = horizon;
        }

        void Schedule(AudioClip clip, double songTime)
        {
            if (clip == null || _assistSources.Length == 0) return;
            var src = _assistSources[_nextSource];
            _nextSource = (_nextSource + 1) % _assistSources.Length;
            src.clip = clip;
            // PlayScheduled: 프레임마다 PlayOneShot하면 프레임 간격만큼 흔들려 싱크 확인용으로 못 쓴다.
            src.PlayScheduled(_clock.SongTimeToDsp(songTime));
        }
    }
}
