using System;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.ChartEditing
{
    /// 타임라인 좌표계: 곡 시각 ↔ 화면 x, 레인 높이. 게임과 같은 방향(미래가 오른쪽, 판정선·커서가 왼쪽).
    /// 영역(_area) 세로 배치: 위 60~100% 상단 레인 / 30~60% 하단 레인 / 0~30% 파형.
    public class TimelineView : MonoBehaviour
    {
        [SerializeField] RectTransform _area;
        [SerializeField] float _pixelsPerSec = 300f;
        [SerializeField] float _minPixelsPerSec = 40f;
        [SerializeField] float _maxPixelsPerSec = 2400f;

        [Tooltip("재생 중 커서 고정 위치(영역 폭 비율). 게임 판정선과 같은 왼쪽 25%.")]
        [SerializeField] float _playheadRatio = 0.25f;

        public event Action ViewChanged;

        public RectTransform Area => _area;
        public float PixelsPerSec => _pixelsPerSec;
        public double ViewStartSec { get; private set; } = -1;
        public double ViewEndSec => ViewStartSec + Rect.width / _pixelsPerSec;

        Rect Rect => _area.rect;

        public float TimeToX(double sec) => Rect.xMin + (float)((sec - ViewStartSec) * _pixelsPerSec);
        public double XToTime(float localX) => ViewStartSec + (localX - Rect.xMin) / _pixelsPerSec;

        public float LaneY(Lane lane) => Rect.yMin + Rect.height * (lane == Lane.Top ? 0.8f : 0.45f);
        public float LaneHalfHeight => Rect.height * 0.12f;
        public float WaveTop => Rect.yMin + Rect.height * 0.3f;

        /// 레인 영역이면 true. 파형 띠 위를 누르면 false(커서 이동용).
        public bool TryLaneAt(float localY, out Lane lane)
        {
            float t = (localY - Rect.yMin) / Rect.height;
            lane = t >= 0.6f ? Lane.Top : Lane.Bottom;
            return t >= 0.3f;
        }

        public void ScrollBy(double seconds) => SetStart(ViewStartSec + seconds);

        /// pivotX(로컬) 아래 시각이 그대로 남도록 확대/축소.
        public void Zoom(float factor, float pivotX)
        {
            double pivotTime = XToTime(pivotX);
            _pixelsPerSec = Mathf.Clamp(_pixelsPerSec * factor, _minPixelsPerSec, _maxPixelsPerSec);
            SetStart(pivotTime - (pivotX - Rect.xMin) / _pixelsPerSec);
        }

        /// 재생 중: 이 시각이 왼쪽 25%에 오도록 화면을 따라간다.
        public void Follow(double sec) => SetStart(sec - _playheadRatio * Rect.width / _pixelsPerSec);

        /// 정지 중 점프: 화면 밖이면 따라가고, 안에 있으면 그대로 둔다(편집 중 화면이 튀지 않게).
        public void Reveal(double sec)
        {
            if (sec < ViewStartSec || sec > ViewEndSec) Follow(sec);
        }

        void SetStart(double sec)
        {
            ViewStartSec = sec;
            ViewChanged?.Invoke();
        }

        void OnRectTransformDimensionsChange() => ViewChanged?.Invoke();
    }
}
