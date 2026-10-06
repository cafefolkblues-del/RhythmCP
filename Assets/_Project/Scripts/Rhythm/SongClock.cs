using RhythmCP.Settings;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 곡 재생 + 판정 기준 시계. SongTime = 오디오 파일 기준 현재 재생 위치(초), 오프셋 미적용.
    ///  - 판정 오프셋은 입력 쪽(RealtimeToSongTime)에만, 화면 오프셋은 표시 쪽(VisualTime)에만 적용 → 둘을 따로 보정할 수 있다.
    /// 판정·노트 위치가 같은 프레임에 같은 값을 보도록 다른 리듬 컴포넌트보다 먼저 갱신한다.
    [DefaultExecutionOrder(-100)]
    public class SongClock : MonoBehaviour
    {
        [SerializeField] AudioSource _source;

        [Tooltip("오프셋 보정 창의 메트로놈처럼 날것의 입력 시각이 필요한 곳은 끈다.")]
        [SerializeField] bool _applyUserOffsets = true;

        [Tooltip("재개할 때 예약 재생까지 두는 여유. 오디오 스레드가 예약을 받을 시간.")]
        [SerializeField] double _resumeScheduleSec = 0.05;

        double _startDsp;
        double _lastDsp;
        double _lastDspRealtime;
        double _songTime;
        double _songTimeRealtime;
        double _pausedSongTime;
        bool _running;

        public bool IsRunning => _running;
        public bool IsPaused { get; private set; }
        public double SongTime => _songTime;
        public double ClipLength => _source.clip != null ? _source.clip.length : 0;

        /// 곡 시각 → 그 순간이 울리는 dspTime. 보조음(채보 에디터 타격음·메트로놈)을 샘플 단위로 예약 재생할 때 쓴다.
        public double SongTimeToDsp(double songTime) => _startDsp + songTime;

        /// 노트 표시용 시각. 화면 오프셋만큼 앞선 시각으로 그린다.
        public double VisualTime => _songTime + (_applyUserOffsets ? GameSettings.VisualOffsetSec : 0);

        public void Begin(AudioClip clip, double leadInSec)
        {
            _source.clip = clip;
            _source.loop = false;
            IsPaused = false;

            // PlayScheduled: Play()는 다음 오디오 버퍼 경계에서 시작돼 실제 시작 시각을 알 수 없다.
            // 예약 재생은 시작 dspTime을 우리가 정하므로 판정 기준점이 정확해진다.
            _startDsp = AudioSettings.dspTime + leadInSec;
            _source.PlayScheduled(_startDsp);
            ResetInterpolation(-leadInSec);
            _running = true;
        }

        /// 원하는 곡 위치부터 재생(채보 에디터의 커서 재생). 일시정지 후 재개와 같은 경로를 써서 기준점 계산이 하나로 유지된다.
        public void PlayFrom(AudioClip clip, double songTime)
        {
            _source.clip = clip;
            _source.loop = false;
            _running = true;
            IsPaused = true;
            _pausedSongTime = songTime;
            Resume();
        }

        public void Stop()
        {
            _running = false;
            IsPaused = false;
            _source.Stop();
        }

        public void Pause()
        {
            if (!_running || IsPaused) return;
            IsPaused = true;
            _pausedSongTime = _songTime;

            // Pause/UnPause 대신 Stop + 재개 시 위치 지정 후 예약 재생: UnPause는 다음 버퍼 경계에서 풀려
            // 재개 시각을 알 수 없고, 그러면 재개 후 판정 기준점이 버퍼 하나만큼 흔들린다.
            _source.Stop();
        }

        /// 멈춘 자리부터 다시. 리드인 중(곡 시작 전)에 멈췄으면 남은 리드인부터 이어간다.
        public void Resume()
        {
            if (!_running || !IsPaused) return;
            IsPaused = false;

            double t = _pausedSongTime;
            double startAt = AudioSettings.dspTime + _resumeScheduleSec;
            var clip = _source.clip;

            if (t < 0)
            {
                _source.timeSamples = 0;
                _source.PlayScheduled(startAt - t);
            }
            else if (clip != null && t < clip.length)
            {
                // timeSamples: time(초)은 압축 클립에서 탐색 오차가 생길 수 있어 샘플 단위로 지정.
                _source.timeSamples = Mathf.Clamp((int)(t * clip.frequency), 0, clip.samples - 1);
                _source.PlayScheduled(startAt);
            }

            _startDsp = startAt - t;
            ResetInterpolation(t);
        }

        /// 입력 이벤트가 실제로 발생한 실시간(realtimeSinceStartup 기준) → 그 순간의 SongTime(판정 오프셋 적용).
        /// 입력 콜백은 이 컴포넌트의 Update보다 먼저 불리므로 "지금"이 아니라 마지막 갱신 시점을 기준으로 잰다.
        public double RealtimeToSongTime(double realtime)
        {
            double t = _songTime + (realtime - _songTimeRealtime);
            return _applyUserOffsets ? t - GameSettings.JudgeOffsetSec : t;
        }

        void ResetInterpolation(double songTime)
        {
            _lastDsp = AudioSettings.dspTime;
            _lastDspRealtime = Time.realtimeSinceStartupAsDouble;
            _songTime = songTime;
            _songTimeRealtime = _lastDspRealtime;
        }

        void Update()
        {
            if (!_running || IsPaused) return;

            // dspTime은 오디오 버퍼 단위(Best latency 256샘플 ≈ 5ms)로만 갱신돼 프레임 사이에서 멈춰 있다.
            // 값이 바뀐 순간의 실시간을 기억해 두고, 그 뒤 흐른 실시간을 더해 부드럽게 보간한다.
            double dsp = AudioSettings.dspTime;
            double now = Time.realtimeSinceStartupAsDouble;
            if (dsp != _lastDsp)
            {
                _lastDsp = dsp;
                _lastDspRealtime = now;
            }

            double t = _lastDsp + (now - _lastDspRealtime) - _startDsp;

            // 보간값이 다음 dsp 갱신 때 살짝 뒤로 튈 수 있어 역행만 막는다(노트가 떨리는 것 방지).
            // 재개 직후엔 예약 여유(_resumeScheduleSec)만큼 멈춰 있다가 이어진다.
            if (t > _songTime) _songTime = t;
            _songTimeRealtime = now;
        }
    }
}
