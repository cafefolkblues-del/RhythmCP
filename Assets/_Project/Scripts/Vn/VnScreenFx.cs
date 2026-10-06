using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// 라인 fx: fade(검은 화면에서 밝아짐) · shake(무대 흔들림) · colorBleed(임시: 화면 전체 채도 복귀).
    /// VN 기본은 흑백 — URP Volume의 Color Adjustments 채도로 건다. 캔버스는 Screen Space - Camera여야 후처리를 받는다.
    /// colorBleed는 M6 부분색(흑백 위 일부만 번짐) 셰이더가 오면 교체.
    public class VnScreenFx : MonoBehaviour
    {
        [SerializeField] VnConfig _config;

        [Tooltip("Color Adjustments 오버라이드가 든 Volume. 비우면 흑백·colorBleed 생략.")]
        [SerializeField] Volume _volume;

        [Tooltip("흔들 대상(무대 루트). 대사창은 흔들지 않는다.")]
        [SerializeField] RectTransform _shakeTarget;

        [Tooltip("화면 전체 검은 Image. 레이캐스트 끄기.")]
        [SerializeField] Image _fadeOverlay;

        ColorAdjustments _color;
        VnColorTween _fadeTween;
        Vector2 _shakeOrigin;
        float _shakeLeft;
        float _saturation, _saturationTarget;

        void Awake()
        {
            _fadeTween = new VnColorTween(_fadeOverlay);
            _fadeOverlay.color = Color.clear;
            if (_shakeTarget != null) _shakeOrigin = _shakeTarget.anchoredPosition;

            // volume.profile은 공유 에셋 사본을 만들어 준다 — 플레이 중 채도를 바꿔도 프로필 에셋이 더러워지지 않게.
            if (_volume != null && _volume.profile.TryGet(out _color))
            {
                _color.saturation.overrideState = true;
                _saturation = _saturationTarget = _config.BaseSaturation;
                _color.saturation.value = _saturation;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _fadeTween.Tick(dt);

            if (_shakeTarget != null && _shakeLeft > 0f)
            {
                _shakeLeft = Mathf.Max(0f, _shakeLeft - dt);
                float k = _shakeLeft / _config.ShakeSec;   // 점점 약해진다
                _shakeTarget.anchoredPosition = _shakeOrigin + Random.insideUnitCircle * (_config.ShakeAmplitude * k);
                if (_shakeLeft <= 0f) _shakeTarget.anchoredPosition = _shakeOrigin;
            }

            if (_color != null && !Mathf.Approximately(_saturation, _saturationTarget))
            {
                float speed = Mathf.Abs(_config.BaseSaturation) / Mathf.Max(0.01f, _config.ColorBleedSec);
                _saturation = Mathf.MoveTowards(_saturation, _saturationTarget, speed * dt);
                _color.saturation.value = _saturation;
            }
        }

        /// 라인마다 호출. colorBleed는 그 라인 동안만 켜지고, 다음 라인에 없으면 다시 흑백으로.
        public void Play(List<string> fx, bool instant)
        {
            bool bleed = fx != null && fx.Contains("colorBleed");
            _saturationTarget = bleed ? 0f : _config.BaseSaturation;
            if (instant && _color != null)
            {
                _saturation = _saturationTarget;
                _color.saturation.value = _saturation;
            }

            if (fx == null || instant) return;
            if (fx.Contains("fade"))
            {
                _fadeOverlay.color = Color.black;
                _fadeTween.To(Color.clear, _config.ScreenFadeSec);
            }
            if (fx.Contains("shake")) _shakeLeft = _config.ShakeSec;
        }
    }
}
