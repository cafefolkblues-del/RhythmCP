using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 곡 재생 + 판정 기준 시계. SongTime = 오디오 파일 기준 현재 재생 위치(초).
    /// 판정·노트 위치가 같은 프레임에 같은 값을 보도록 다른 리듬 컴포넌트보다 먼저 갱신한다.
    [DefaultExecutionOrder(-100)]
    public class SongClock : MonoBehaviour
    {
        [SerializeField] AudioSource _source;

        // ⑤ 오프셋 보정 자리. 유저 캘리브레이션 값이 들어오면 여기에 더한다(+ = 판정을 늦춤).
        double _calibrationSec;

        double _startDsp;
        double _lastDsp;
        double _lastDspRealtime;
        double _songTime;
        double _songTimeRealtime;
        bool _running;

        public bool IsRunning => _running;
        public double SongTime => _songTime;
        public double ClipLength => _source.clip != null ? _source.clip.length : 0;

        public void Begin(AudioClip clip, double leadInSec)
        {
            _source.clip = clip;
            _source.loop = false;

            // PlayScheduled: Play()는 다음 오디오 버퍼 경계에서 시작돼 실제 시작 시각을 알 수 없다.
            // 예약 재생은 시작 dspTime을 우리가 정하므로 판정 기준점이 정확해진다.
            _startDsp = AudioSettings.dspTime + leadInSec;
            _source.PlayScheduled(_startDsp);

            _lastDsp = AudioSettings.dspTime;
            _lastDspRealtime = Time.realtimeSinceStartupAsDouble;
            _songTime = -leadInSec;
            _songTimeRealtime = _lastDspRealtime;
            _running = true;
        }

        public void Stop()
        {
            _running = false;
            _source.Stop();
        }

        /// 입력 이벤트가 실제로 발생한 실시간(realtimeSinceStartup 기준) → 그 순간의 SongTime.
        /// 입력 콜백은 이 컴포넌트의 Update보다 먼저 불리므로 "지금"이 아니라 마지막 갱신 시점을 기준으로 잰다.
        public double RealtimeToSongTime(double realtime) => _songTime + (realtime - _songTimeRealtime);

        void Update()
        {
            if (!_running) return;

            // dspTime은 오디오 버퍼 단위(Best latency 256샘플 ≈ 5ms)로만 갱신돼 프레임 사이에서 멈춰 있다.
            // 값이 바뀐 순간의 실시간을 기억해 두고, 그 뒤 흐른 실시간을 더해 부드럽게 보간한다.
            double dsp = AudioSettings.dspTime;
            double now = Time.realtimeSinceStartupAsDouble;
            if (dsp != _lastDsp)
            {
                _lastDsp = dsp;
                _lastDspRealtime = now;
            }

            double t = _lastDsp + (now - _lastDspRealtime) - _startDsp - _calibrationSec;

            // 보간값이 다음 dsp 갱신 때 살짝 뒤로 튈 수 있어 역행만 막는다(노트가 떨리는 것 방지).
            if (t > _songTime) _songTime = t;
            _songTimeRealtime = now;
        }
    }
}
