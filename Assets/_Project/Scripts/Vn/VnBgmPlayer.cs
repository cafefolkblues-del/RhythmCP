using UnityEngine;

namespace RhythmCP.Vn
{
    /// BGM 크로스페이드. AudioSource 두 개를 번갈아 쓴다 — 새 곡이 커지는 동안 이전 곡이 작아진다.
    public class VnBgmPlayer : MonoBehaviour
    {
        [SerializeField] VnConfig _config;
        [SerializeField] AudioSource _a;
        [SerializeField] AudioSource _b;

        AudioSource _current, _previous;
        float _fade = 1f, _fadeSec;
        AudioClip _clip;

        void Awake()
        {
            foreach (var s in new[] { _a, _b })
            {
                s.loop = true;
                s.playOnAwake = false;
                s.volume = 0f;
            }
            _current = _a;
            _previous = _b;
        }

        void Update()
        {
            if (_fade >= 1f) return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / _fadeSec);
            ApplyVolumes();
            if (_fade >= 1f) _previous.Stop();
        }

        /// clip null = 정지(페이드아웃). 같은 곡이면 이어서 튼다.
        public void Play(AudioClip clip, bool instant)
        {
            if (clip == _clip) return;
            _clip = clip;

            (_current, _previous) = (_previous, _current);
            _current.Stop();
            _current.clip = clip;
            if (clip != null) _current.Play();

            _fadeSec = instant ? 0f : _config.BgmCrossfadeSec;
            _fade = 0f;
            if (_fadeSec <= 0f)
            {
                _fade = 1f;
                ApplyVolumes();
                _previous.Stop();
            }
        }

        void ApplyVolumes()
        {
            float v = _config.BgmVolume;
            _current.volume = _current.clip != null ? v * _fade : 0f;
            _previous.volume = v * (1f - _fade);
        }
    }
}
