using RhythmCP.Rhythm;
using TMPro;
using UnityEngine;

namespace RhythmCP.UI
{
    /// HUD 곡 정보(곡명 · 난이도) + 하단 진행바와 클라이맥스 구간 표시.
    /// TODO(연출): 곡 시작 때 좌하단에 곡명이 흘러 지나가는 연출 고려 중 — 카타나 제로 참고.
    public class SongProgressView : MonoBehaviour
    {
        [SerializeField] RhythmSession _session;
        [SerializeField] TMP_Text _title;
        [SerializeField] RectTransform _fill;
        [SerializeField] RectTransform _climax;

        bool _laidOut;

        void Update()
        {
            // 채보는 RhythmSession.Start에서 로드되므로 준비된 첫 프레임에 한 번만 배치한다.
            if (_session.Chart == null) return;
            if (!_laidOut) LayOut();

            double end = _session.EndTime;
            float ratio = end > 0 ? Mathf.Clamp01((float)(_session.SongTime / end)) : 0f;
            _fill.anchorMax = new Vector2(ratio, _fill.anchorMax.y);
        }

        void LayOut()
        {
            _laidOut = true;
            _title.text = $"{_session.Song.Title} · {_session.Difficulty.ToString().ToUpperInvariant()}";

            var chart = _session.Chart;
            _climax.gameObject.SetActive(chart.HasClimax);
            if (!chart.HasClimax) return;

            double end = _session.EndTime;
            _climax.anchorMin = new Vector2(Mathf.Clamp01((float)(chart.ClimaxStartSec / end)), 0f);
            _climax.anchorMax = new Vector2(Mathf.Clamp01((float)(chart.ClimaxEndSec / end)), 1f);
            _climax.offsetMin = _climax.offsetMax = Vector2.zero;
        }
    }
}
