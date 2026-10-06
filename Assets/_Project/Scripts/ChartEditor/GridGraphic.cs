using RhythmCP.Chart;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.ChartEditing
{
    /// 격자선(마디·박·칸) + 레인 띠 + 파형.
    public class GridGraphic : TimelineGraphic
    {
        [SerializeField] Color _bar = new Color(1, 1, 1, 0.45f);
        [SerializeField] Color _beat = new Color(1, 1, 1, 0.2f);
        [SerializeField] Color _sub = new Color(1, 1, 1, 0.07f);
        [SerializeField] Color _laneBand = new Color(1, 1, 1, 0.04f);
        [SerializeField] Color _wave = new Color(0.45f, 0.75f, 1f, 0.5f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!Ready) return;
            var r = rectTransform.rect;
            float h = Timeline.LaneHalfHeight;

            foreach (var lane in new[] { Lane.Top, Lane.Bottom })
            {
                float y = Timeline.LaneY(lane);
                Rect(vh, r.xMin, y - h, r.xMax, y + h, _laneBand);
            }

            var tempo = Session.Document.Tempo;
            foreach (var line in BeatGrid.Lines(tempo, Timeline.ViewStartSec, Timeline.ViewEndSec, Session.SnapDivision))
            {
                float x = Timeline.TimeToX(line.Time);
                float w = line.Kind == GridLineKind.Bar ? 1.5f : 0.75f;
                var c = line.Kind == GridLineKind.Bar ? _bar : line.Kind == GridLineKind.Beat ? _beat : _sub;
                Rect(vh, x - w, Timeline.WaveTop, x + w, r.yMax, c);
            }

            var wave = Session.Waveform;
            if (wave != null)
            {
                float mid = (r.yMin + Timeline.WaveTop) * 0.5f;
                float amp = (Timeline.WaveTop - r.yMin) * 0.48f;
                // 화면 1~2px마다 한 막대: 파형 데이터 해상도와 무관하게 화면 폭만큼만 그린다.
                for (float x = r.xMin; x < r.xMax; x += 2f)
                {
                    float peak = wave.PeakBetween(Timeline.XToTime(x), Timeline.XToTime(x + 2f));
                    if (peak <= 0f) continue;
                    Rect(vh, x, mid - peak * amp, x + 1.5f, mid + peak * amp, _wave);
                }
            }
        }
    }
}
