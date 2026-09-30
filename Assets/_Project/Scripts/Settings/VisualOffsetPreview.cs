using System.Collections.Generic;
using RhythmCP.Rhythm;
using UnityEngine;

namespace RhythmCP.Settings
{
    /// 화면 오프셋 수동 조정용 미리보기: 클릭음에 맞춰 노트가 판정선에 닿는지 보면서 ±로 맞춘다.
    /// 노트 위치는 실제 플레이와 같은 SongClock.VisualTime으로 계산 → 조정 결과가 그대로 플레이에 반영된다.
    public class VisualOffsetPreview : MonoBehaviour
    {
        [SerializeField] SongClock _clock;
        [SerializeField] AudioClip _metronome;
        [SerializeField] double _firstBeatSec = 1.0;
        [SerializeField] double _beatIntervalSec = 0.5;
        [SerializeField] int _beatCount = 16;

        [SerializeField] RectTransform _judgeLine;

        [Tooltip("미리 만들어 둔 노트 이미지들(풀). 화면에 동시에 보일 개수만큼.")]
        [SerializeField] List<RectTransform> _notes = new List<RectTransform>();

        [Tooltip("UI 픽셀/초.")]
        [SerializeField] float _pixelsPerSec = 500f;

        void OnEnable() => _clock.Begin(_metronome, 0.2);

        void OnDisable() => _clock.Stop();

        void Update()
        {
            // 클립 끝까지 가면 처음부터 다시(무한 반복 미리보기).
            if (_clock.SongTime > _firstBeatSec + _beatCount * _beatIntervalSec) _clock.Begin(_metronome, 0.2);

            double now = _clock.VisualTime;
            float lineX = _judgeLine.anchoredPosition.x;
            int next = Mathf.Max(0, (int)System.Math.Floor((now - _firstBeatSec) / _beatIntervalSec));

            for (int i = 0; i < _notes.Count; i++)
            {
                int beat = next + i;
                var note = _notes[i];
                if (beat >= _beatCount)
                {
                    note.gameObject.SetActive(false);
                    continue;
                }
                double beatTime = _firstBeatSec + beat * _beatIntervalSec;
                note.gameObject.SetActive(true);
                note.anchoredPosition = new Vector2(lineX + (float)((beatTime - now) * _pixelsPerSec), note.anchoredPosition.y);
            }
        }
    }
}
